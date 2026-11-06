using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>Dónde va a parar una carga: el documento (por su clave natural), cómo crearlo si no existe y su bucket.</summary>
internal sealed record DestinoDeCarga(string ClaveNatural, Func<DateTimeOffset, DocumentoCreado> Crear, string Bucket, Guid? ContextoVinculoId);

/// <summary>
/// Carga en dos pasos por el tercero (docs/05 §2.9, docs/07 §6.1). El API nunca recibe los bytes: el paso 1 reserva la
/// versión y entrega una URL prefirmada PUT a una zona temporal; el paso 2 lee el objeto, calcula el SHA-256, valida
/// tamaño y tipo real (magic bytes) y revisa el PDF; entonces registra la versión, la vuelve <c>Actual</c> (o la
/// devuelve) y la comparte con las empresas que corresponda.
/// </summary>
public static class CargaEndpoints
{
    /// <summary>
    /// Paso 1. 201 con la URL de subida; 200 <c>duplicado</c> si el SHA-256 declarado ya es una versión del documento;
    /// 413 si el tamaño declarado supera el máximo; 415 si el tipo declarado no está permitido; 422 <c>clase-no-soportada</c>
    /// para formularios.
    /// </summary>
    [WolverinePost("/public/v1/boveda/documentos")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Iniciar(IniciarCargaDto dto, HttpContext http, ISesiones sesiones, IAlmacenDeArchivos almacen,
        ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var nombre = CargaDeDocumento.NormalizarNombre(dto.NombreArchivo);
        var sha256 = CargaDeDocumento.NormalizarSha256(dto.Sha256);
        var mime = (dto.TipoMime ?? string.Empty).Trim().ToLowerInvariant();

        if (dto.TamanoBytes <= 0)
            throw ReglaDeNegocioException.Validacion("tamano-invalido", "Indica el tamaño del archivo en bytes.");
        if (dto.TamanoBytes > reglas.TamanoMaximoBytes)
            throw ReglaDeNegocioException.DemasiadoGrande($"El archivo supera el máximo de {reglas.TamanoMaximoBytes / (1024 * 1024)} MB.");
        if (!InspectorDeArchivos.EsPermitido(mime))
            throw ReglaDeNegocioException.TipoNoSoportado($"Solo se aceptan {string.Join(", ", InspectorDeArchivos.Permitidos)}.");

        var destino = await ResolverDestinoAsync(dto, tercero, sesiones, ct);

        var reserva = await Peticiones.ConReintentosAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            await using var sesion = sesiones.Boveda();
            var clave = destino.ClaveNatural;
            var existente = await sesion.Query<Documento>().FirstOrDefaultAsync(d => d.ClaveNatural == clave, ct);

            Documento documento;
            if (existente is null)
            {
                var creado = destino.Crear(ahora);
                sesion.Events.StartStream<Documento>(creado.DocumentoId, creado);
                documento = Documento.Create(creado);
            }
            else
            {
                // Bloqueo de fila del stream: dos cargas simultáneas del mismo documento no reservan el mismo número (FL-12).
                documento = (await sesion.Events.FetchForExclusiveWriting<Documento>(existente.Id, ct)).Aggregate!;
            }

            if (sha256 is not null && documento.BuscarPorHash(sha256) is { } duplicada)
                return (documento.Id, duplicada.Numero, (CargaDeDocumento?)null);

            var documentoId = documento.Id;
            var pendientes = await sesion.Query<CargaDeDocumento>()
                .Where(c => c.DocumentoId == documentoId && c.Estado == EstadoCarga.Pendiente)
                .ToListAsync(ct);
            foreach (var pendiente in pendientes)
            {
                pendiente.Estado = EstadoCarga.Reemplazada;
                sesion.Store(pendiente);
            }

            var cargaId = Guid.CreateVersion7();
            var carga = new CargaDeDocumento
            {
                Id = cargaId,
                DocumentoId = documentoId,
                TerceroId = tercero.TerceroId,
                Numero = documento.SiguienteNumero,
                Bucket = destino.Bucket,
                ClaveTemporal = ClavesDeAlmacen.Carga(cargaId),
                NombreOriginal = nombre,
                TamanoDeclarado = dto.TamanoBytes,
                MimeDeclarado = mime,
                Sha256Declarado = sha256,
                ContextoVinculoId = destino.ContextoVinculoId,
                Estado = EstadoCarga.Pendiente,
                CreadaEn = ahora,
                Expira = ahora + reglas.VigenciaSubida
            };
            sesion.Store(carga);
            await sesion.SaveChangesAsync(ct);
            return (documentoId, carga.Numero, (CargaDeDocumento?)carga);
        });

        if (reserva.Item3 is not { } nueva)
            return Results.Ok(new CargaPrefirmadaDto(reserva.Item1, reserva.Item2, true, null));

        await almacen.AsegurarBucketAsync(nueva.Bucket, ct);
        var url = await almacen.UrlDeSubidaAsync(nueva.Bucket, nueva.ClaveTemporal, nueva.MimeDeclarado, reglas.VigenciaSubida, ct);
        return Results.Created($"/public/v1/boveda/documentos/{nueva.DocumentoId}",
            new CargaPrefirmadaDto(nueva.DocumentoId, nueva.Numero, false,
                new InstruccionCargaDto(url.Metodo, url.Url, url.Cabeceras, url.Expira)));
    }

    /// <summary>
    /// Paso 2. El servidor lee el objeto subido: más grande que el máximo ⇒ 413; tipo real (magic bytes) no permitido ⇒ 415;
    /// SHA-256 distinto al declarado ⇒ 422 <c>hash-no-coincide</c>; el mismo archivo ya registrado ⇒ 200 <c>duplicado</c>.
    /// Un PDF con contenido activo o demasiadas páginas queda <c>Devuelta</c> con su motivo; si no, pasa a <c>Actual</c> y se
    /// comparte. Sincrónico: responde 200 con el estado final (sin IA no hay análisis en segundo plano). Cada vínculo que
    /// recibió la versión recibe además, por el outbox, la orden de adjuntarla a los requisitos de su expediente abierto
    /// (<see cref="SincronizarRequisitosDelVinculo"/>, RQ-2).
    /// </summary>
    [WolverinePost("/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/confirmar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ConfirmacionCargaDto> Confirmar(Guid documentoId, int numero, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, IMessageBus mensajes, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var ahora = reloj.GetUtcNow();

        await using var sesion = sesiones.Boveda(mensajes);
        var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
        var documento = BovedaEndpoints.DelTercero(stream.Aggregate, tercero);

        var carga = await sesion.Query<CargaDeDocumento>()
            .Where(c => c.DocumentoId == documentoId && c.Numero == numero && c.Estado == EstadoCarga.Pendiente)
            .OrderByDescending(c => c.CreadaEn)
            .FirstOrDefaultAsync(ct);
        if (carga is null || carga.TerceroId != tercero.TerceroId)
            throw documento.Buscar(numero) is not null
                ? ReglaDeNegocioException.Conflicto("version-ya-confirmada", $"La versión {numero} ya se confirmó.")
                : ReglaDeNegocioException.NoEncontrado("carga-no-encontrada", "No hay una carga pendiente para esa versión: iníciala de nuevo.");

        var objeto = await almacen.ConsultarAsync(carga.Bucket, carga.ClaveTemporal, ct)
                     ?? throw ReglaDeNegocioException.Conflicto("archivo-no-cargado",
                         "Aún no recibimos el archivo: súbelo con la URL de carga y vuelve a confirmar.");

        if (objeto.Tamano > reglas.TamanoMaximoBytes)
        {
            await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
            throw ReglaDeNegocioException.DemasiadoGrande($"El archivo supera el máximo de {reglas.TamanoMaximoBytes / (1024 * 1024)} MB.");
        }

        var contenido = await LeerAsync(almacen, carga, reglas.TamanoMaximoBytes, ct);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(contenido));
        if (carga.Sha256Declarado is { } declarado && declarado != sha256)
        {
            await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
            throw ReglaDeNegocioException.Validacion("hash-no-coincide", "El archivo recibido no coincide con el SHA-256 declarado.");
        }

        var inspeccion = InspectorDeArchivos.Inspeccionar(contenido);
        if (!inspeccion.Soportado)
        {
            await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
            throw ReglaDeNegocioException.TipoNoSoportado(
                $"El contenido no es un {string.Join(", ", InspectorDeArchivos.Permitidos)} válido (la extensión o el tipo declarado no bastan).");
        }

        if (documento.DetectarDuplicado(sha256, ahora) is { } duplicado)
        {
            stream.AppendOne(duplicado);
            carga.Estado = EstadoCarga.Duplicada;
            sesion.Store(carga);
            await sesion.SaveChangesAsync(ct);
            await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
            return new ConfirmacionCargaDto(true, documento.VersionRequerida(duplicado.NumeroExistente).ADto(documento.Id), []);
        }

        var devolucion = InspectorDeArchivos.MotivoDeDevolucion(inspeccion, reglas.MaximoPaginas);
        var claveDefinitiva = devolucion is not null
            ? ClavesDeAlmacen.Cuarentena(documento.Id, numero)
            : documento.Ambito == AmbitoDocumento.Global
                ? ClavesDeAlmacen.Global(documento.TerceroId, sha256)
                : ClavesDeAlmacen.PorVinculo(documento.EmpresaId!.Value, documento.VinculoId!.Value, sha256);

        await almacen.CopiarAsync(carga.Bucket, carga.ClaveTemporal, carga.Bucket, claveDefinitiva, inspeccion.MimeReal!,
            new Dictionary<string, string>
            {
                ["nombre-original"] = Uri.EscapeDataString(carga.NombreOriginal),
                ["documento-id"] = documento.Id.ToString(),
                ["version-n"] = numero.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["mime"] = inspeccion.MimeReal!
            }, ct);

        var archivo = new ArchivoVerificado(numero, sha256, carga.Bucket, claveDefinitiva, carga.MimeDeclarado, inspeccion.MimeReal!,
            contenido.LongLength, carga.NombreOriginal, inspeccion.Paginas, carga.ContextoVinculoId, tercero.TerceroId.ToString());
        var eventos = documento.RegistrarVersionArchivo(archivo, devolucion, Antivirus.NoDisponible, reglas.ExigirAntivirus, ahora);
        foreach (var evento in eventos)
            stream.AppendOne(evento);

        var proyectado = documento.Proyectar(eventos);
        var compartidos = proyectado.VersionActual == numero
            ? await ComparticionDeDocumentos.CompartirActualAsync(sesion, stream, proyectado, OrigenComparticion.Automatica, ahora, ct, mensajes, contexto)
            : [];

        carga.Estado = EstadoCarga.Confirmada;
        sesion.Store(carga);
        await sesion.SaveChangesAsync(ct);
        await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);

        return new ConfirmacionCargaDto(false, proyectado.VersionRequerida(numero).ADto(documento.Id), compartidos);
    }

    /// <summary>Lee el objeto completo (ya se verificó que no supera el máximo; se corta por si cambió entretanto).</summary>
    private static async Task<byte[]> LeerAsync(IAlmacenDeArchivos almacen, CargaDeDocumento carga, long maximo, CancellationToken ct)
    {
        await using var flujo = await almacen.AbrirLecturaAsync(carga.Bucket, carga.ClaveTemporal, ct);
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await flujo.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + leidos > maximo)
            {
                await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
                throw ReglaDeNegocioException.DemasiadoGrande($"El archivo supera el máximo de {maximo / (1024 * 1024)} MB.");
            }

            memoria.Write(buffer, 0, leidos);
        }

        return memoria.ToArray();
    }

    /// <summary>
    /// Un tipo canónico global va a la bóveda del tercero (bucket <c>boveda</c>). Un tipo del tenant necesita el vínculo
    /// (de su sesión) para saber de qué empresa es; si es <c>PorVinculo</c> el documento es de ese vínculo (bucket del tenant).
    /// </summary>
    private static async Task<DestinoDeCarga> ResolverDestinoAsync(IniciarCargaDto dto, SesionDeTercero tercero, ISesiones sesiones,
        CancellationToken ct)
    {
        var enSesion = dto.VinculoId is { } vinculoId ? tercero.VinculoRequerido(vinculoId) : null;
        await using var consulta = sesiones.ConsultaGlobal();

        TipoDocumentoTenant? tipoTenant = null;
        if (dto.TipoDocumentoTenantId is { } tipoId)
        {
            if (enSesion is null)
                throw ReglaDeNegocioException.Validacion("vinculo-requerido", "Un tipo de documento de la empresa necesita el vínculo al que pertenece.");
            var tenantId = enSesion.TenantId;
            tipoTenant = await consulta.Query<TipoDocumentoTenant>()
                .Where(t => t.AnyTenant() && t.Id == tipoId && t.TenantId == tenantId)
                .FirstOrDefaultAsync(ct);
            if (tipoTenant is not { Activo: true })
                throw ReglaDeNegocioException.Validacion("tipo-documento-invalido", "La empresa no tiene ese tipo de documento o está inactivo.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(dto.TipoCanonico))
                throw ReglaDeNegocioException.Validacion("tipo-documento-requerido", "Indica el tipo canónico o el tipo de documento de la empresa.");

            var canonico = CatalogoCanonico.Requerido(dto.TipoCanonico);
            if (canonico.Ambito == AmbitoDocumento.PorVinculo)
            {
                // Un canónico por vínculo (p. ej. POLIZA) se carga con el tipo que la empresa del vínculo configuró para él.
                if (enSesion is null)
                    throw ReglaDeNegocioException.Validacion("vinculo-requerido",
                        $"{canonico.Codigo} es un documento por vínculo: indica el vínculo (empresa) al que pertenece.");
                var tenantId = enSesion.TenantId;
                var codigo = canonico.Codigo;
                var candidatos = await consulta.Query<TipoDocumentoTenant>()
                    .Where(t => t.AnyTenant() && t.TenantId == tenantId && t.TipoCanonico == codigo && t.Activo)
                    .ToListAsync(ct);
                tipoTenant = candidatos.FirstOrDefault(t => t.Ambito == AmbitoDocumento.PorVinculo)
                             ?? throw ReglaDeNegocioException.Validacion("tipo-documento-invalido", $"La empresa no tiene configurado el tipo {codigo}.");
            }
        }

        var terceroId = tercero.TerceroId;
        if (tipoTenant is null || tipoTenant.Ambito == AmbitoDocumento.Global)
        {
            var canonico = CatalogoCanonico.Requerido(tipoTenant?.TipoCanonico ?? dto.TipoCanonico);
            Documento.AsegurarClaseSoportada(canonico.Clase, canonico.Codigo);
            if (canonico.Ambito != AmbitoDocumento.Global)
                throw ReglaDeNegocioException.Validacion("vinculo-requerido", $"{canonico.Codigo} es un documento por vínculo.");
            return new DestinoDeCarga(Documento.ClaveGlobal(terceroId, canonico.Codigo), ahora => Documento.CrearGlobal(terceroId, canonico, ahora),
                ClavesDeAlmacen.BucketBoveda, enSesion?.VinculoId);
        }

        Documento.AsegurarClaseSoportada(tipoTenant.Clase, tipoTenant.Codigo);
        var propio = enSesion!;
        var vinculoPropio = propio.VinculoId;
        var vinculo = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == vinculoPropio).FirstOrDefaultAsync(ct);
        if (vinculo is null || vinculo.TerceroId != terceroId)
            throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
        if (vinculo.Estado is EstadoVinculo.Anulado or EstadoVinculo.Desvinculado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se cargan documentos a un vínculo {vinculo.Estado}.");

        var tipo = tipoTenant;
        return new DestinoDeCarga(Documento.ClavePorVinculo(vinculo.Id, tipo.Id),
            ahora => Documento.CrearPorVinculo(terceroId, tipo, propio.TenantId, propio.EmpresaId, vinculo.Id, ahora),
            ClavesDeAlmacen.BucketDeTenant(propio.TenantId), vinculo.Id);
    }
}
