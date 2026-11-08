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

/// <summary>
/// Dónde va a parar una carga: el documento (por su clave natural), cómo crearlo si no existe, su bucket, las reglas de
/// archivo efectivas (TD-1) y el tipo del tenant que lo pide (nulo en un global sin vínculo de contexto).
/// </summary>
internal sealed record DestinoDeCarga(
    string ClaveNatural, Func<DateTimeOffset, DocumentoCreado> Crear, string Bucket, Guid? ContextoVinculoId, ReglasDeArchivo Reglas,
    TipoDocumentoTenant? Tipo);

/// <summary>
/// Carga en dos pasos por el tercero (docs/05 §2.9, docs/07 §6.1). El API nunca recibe los bytes: el paso 1 reserva la
/// versión y entrega una URL prefirmada PUT a una zona temporal; el paso 2 lee el objeto, calcula el SHA-256, valida
/// tamaño y tipo real (magic bytes) y revisa el PDF; entonces registra la versión, la vuelve <c>Actual</c> (o la
/// devuelve) y la comparte con las empresas que corresponda. Los límites son los del tipo (TD-1) y, si el tipo pide
/// campos (TD-4), los datos llegan en el paso 1 y viajan con la versión.
/// </summary>
public static class CargaEndpoints
{
    /// <summary>
    /// Paso 1. 201 con la URL de subida; 200 <c>duplicado</c> si el SHA-256 declarado (y los datos) ya son una versión del
    /// documento; 413 si el tamaño declarado supera el máximo del tipo; 415 si el tipo declarado no está admitido para el
    /// tipo; 422 <c>clase-no-soportada</c>, <c>archivo-no-permitido</c> (tipo solo de formulario), <c>datos-no-permitidos</c>
    /// y los de validación de campos (<c>campo-requerido</c>, <c>valor-invalido</c>, <c>campo-desconocido</c>).
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

        var destino = await ResolverDestinoAsync(dto.TipoCanonico, dto.TipoDocumentoTenantId, dto.VinculoId, tercero, sesiones, reglas, ct);
        destino.Reglas.ValidarDeclarado(mime, dto.TamanoBytes);
        var datos = DatosDelArchivo(destino.Tipo, dto, reloj);

        var reserva = await Peticiones.ConReintentosAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            await using var sesion = sesiones.Boveda();
            var documento = await DocumentoParaEscribirAsync(sesion, destino, ahora, ct);

            if (sha256 is not null && documento.BuscarPorHuella(sha256, datos?.Hash) is { } duplicada)
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
                Expira = ahora + reglas.VigenciaSubida,
                Reglas = destino.Reglas,
                Datos = datos is null ? null : [.. datos.Datos],
                HashDatos = datos?.Hash
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
    /// Paso 2. El servidor lee el objeto subido: más grande que el máximo del tipo ⇒ 413; tipo real (magic bytes) no
    /// admitido para el tipo ⇒ 415; SHA-256 distinto al declarado ⇒ 422 <c>hash-no-coincide</c>; el mismo archivo con los
    /// mismos datos ya registrado ⇒ 200 <c>duplicado</c>. Un PDF con contenido activo o más páginas que las del tipo queda
    /// <c>Devuelta</c> con su motivo; si no, pasa a <c>Actual</c> y se comparte. Sincrónico: responde 200 con el estado final
    /// (sin IA no hay análisis en segundo plano). Cada vínculo que recibió la versión recibe además, por el outbox, la orden
    /// de adjuntarla a los requisitos de su expediente abierto (<see cref="SincronizarRequisitosDelVinculo"/>, RQ-2) y de
    /// calcular su vigencia (<see cref="CalcularVigenciasDelDocumento"/>, TD-6).
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

        var limites = carga.Reglas ?? ReglasDeArchivo.Globales(reglas.TamanoMaximoBytes, reglas.MaximoPaginas);
        var objeto = await almacen.ConsultarAsync(carga.Bucket, carga.ClaveTemporal, ct)
                     ?? throw ReglaDeNegocioException.Conflicto("archivo-no-cargado",
                         "Aún no recibimos el archivo: súbelo con la URL de carga y vuelve a confirmar.");

        var contenido = await ConLimpiezaAsync(almacen, carga.Bucket, carga.ClaveTemporal, async () =>
        {
            limites.ValidarTamano(objeto.Tamano);
            return await LeerAsync(almacen, carga.Bucket, carga.ClaveTemporal, limites, ct);
        }, ct);
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(contenido));
        var inspeccion = InspectorDeArchivos.Inspeccionar(contenido);
        await ConLimpiezaAsync(almacen, carga.Bucket, carga.ClaveTemporal, () =>
        {
            if (carga.Sha256Declarado is { } declarado && declarado != sha256)
                throw ReglaDeNegocioException.Validacion("hash-no-coincide", "El archivo recibido no coincide con el SHA-256 declarado.");
            limites.ValidarTipoReal(inspeccion.MimeReal);
            return Task.FromResult(true);
        }, ct);

        var datos = carga.DatosVerificados;
        if (documento.DetectarDuplicado(sha256, ahora, datos?.Hash) is { } duplicado)
        {
            stream.AppendOne(duplicado);
            carga.Estado = EstadoCarga.Duplicada;
            sesion.Store(carga);
            await sesion.SaveChangesAsync(ct);
            await almacen.EliminarAsync(carga.Bucket, carga.ClaveTemporal, ct);
            return new ConfirmacionCargaDto(true, documento.VersionRequerida(duplicado.NumeroExistente).ADto(documento.Id, null, reloj.Hoy()), []);
        }

        var devolucion = InspectorDeArchivos.MotivoDeDevolucion(inspeccion, null) ?? limites.PorPaginas(inspeccion.Paginas);
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
        var eventos = documento.RegistrarVersionArchivo(archivo, devolucion, Antivirus.NoDisponible, reglas.ExigirAntivirus, ahora, datos);
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

        return new ConfirmacionCargaDto(false, proyectado.VersionRequerida(numero).ADto(documento.Id, null, reloj.Hoy()), compartidos);
    }

    /// <summary>
    /// Lee el objeto completo cortando al pasar el máximo (por si cambió entretanto) ⇒ 413. Lo usan la carga del tercero y la
    /// plantilla de la empresa.
    /// </summary>
    internal static async Task<byte[]> LeerAsync(IAlmacenDeArchivos almacen, string bucket, string clave, ReglasDeArchivo limites, CancellationToken ct)
    {
        await using var flujo = await almacen.AbrirLecturaAsync(bucket, clave, ct);
        using var memoria = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;
        while ((leidos = await flujo.ReadAsync(buffer, ct)) > 0)
        {
            limites.ValidarTamano(memoria.Length + leidos);
            memoria.Write(buffer, 0, leidos);
        }

        return memoria.ToArray();
    }

    /// <summary>Si la validación falla (413/415/422), la subida temporal se borra antes de responder: nada queda a medias.</summary>
    internal static async Task<T> ConLimpiezaAsync<T>(IAlmacenDeArchivos almacen, string bucket, string clave, Func<Task<T>> accion, CancellationToken ct)
    {
        try
        {
            return await accion();
        }
        catch (ReglaDeNegocioException)
        {
            await almacen.EliminarAsync(bucket, clave, ct);
            throw;
        }
    }

    /// <summary>
    /// Datos del paso 1 según el modo del tipo (TD-4): un tipo solo de formulario no recibe archivo (422
    /// <c>archivo-no-permitido</c>); uno de solo archivo no recibe datos (<c>datos-no-permitidos</c>); uno con ambos valida aquí
    /// los campos (obligatorios, tipos, opciones, fechas ISO).
    /// </summary>
    private static DatosVerificados? DatosDelArchivo(TipoDocumentoTenant? tipo, IniciarCargaDto dto, TimeProvider reloj)
    {
        if (tipo is { PideArchivo: false })
            throw ReglaDeNegocioException.Validacion("archivo-no-permitido",
                $"{tipo.Nombre} se entrega solo con su formulario: envía los datos a /public/v1/boveda/formularios.");
        if (tipo is not { PideDatos: true })
        {
            return dto.Datos is { Count: > 0 }
                ? throw ReglaDeNegocioException.Validacion("datos-no-permitidos", "Este documento no lleva campos: sube solo el archivo.")
                : null;
        }

        return EsquemaDeFormulario.ValidarDatos(tipo.Campos, MapeosDocumentos.ATexto(dto.Datos), reloj.Hoy());
    }

    /// <summary>
    /// El documento de la clave natural del destino, tomado con bloqueo de fila (FL-12: dos cargas simultáneas no reservan el
    /// mismo número); si no existe, se inicia su stream en esta sesión (una carrera por la clave única se reintenta).
    /// </summary>
    internal static async Task<Documento> DocumentoParaEscribirAsync(IDocumentSession sesion, DestinoDeCarga destino, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var clave = destino.ClaveNatural;
        var existente = await sesion.Query<Documento>().FirstOrDefaultAsync(d => d.ClaveNatural == clave, ct);
        if (existente is not null)
            return (await sesion.Events.FetchForExclusiveWriting<Documento>(existente.Id, ct)).Aggregate!;

        var creado = destino.Crear(ahora);
        sesion.Events.StartStream<Documento>(creado.DocumentoId, creado);
        return Documento.Create(creado);
    }

    /// <summary>
    /// Un tipo canónico global va a la bóveda del tercero (bucket <c>boveda</c>). Un tipo del tenant necesita el vínculo
    /// (de su sesión) para saber de qué empresa es; si es <c>PorVinculo</c> el documento es de ese vínculo (bucket del tenant).
    /// Las reglas de archivo son las del tipo del tenant (o, en un global, las del tipo que la empresa del vínculo de
    /// contexto configuró para ese canónico; sin vínculo, las del canónico), siempre acotadas por las globales (TD-1).
    /// </summary>
    internal static async Task<DestinoDeCarga> ResolverDestinoAsync(string? tipoCanonico, Guid? tipoDocumentoTenantId, Guid? vinculoId,
        SesionDeTercero tercero, ISesiones sesiones, ReglasDeCarga reglas, CancellationToken ct)
    {
        var enSesion = vinculoId is { } idVinculo ? tercero.VinculoRequerido(idVinculo) : null;
        await using var consulta = sesiones.ConsultaGlobal();

        TipoDocumentoTenant? tipoTenant = null;
        if (tipoDocumentoTenantId is { } tipoId)
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
            if (string.IsNullOrWhiteSpace(tipoCanonico))
                throw ReglaDeNegocioException.Validacion("tipo-documento-requerido", "Indica el tipo canónico o el tipo de documento de la empresa.");

            var canonico = CatalogoCanonico.Requerido(tipoCanonico);
            if (canonico.Ambito == AmbitoDocumento.PorVinculo && enSesion is null)
                throw ReglaDeNegocioException.Validacion("vinculo-requerido",
                    $"{canonico.Codigo} es un documento por vínculo: indica el vínculo (empresa) al que pertenece.");
            if (enSesion is not null)
            {
                // El tipo que la empresa del vínculo configuró para el canónico: por vínculo (p. ej. POLIZA) define el
                // documento; global, solo sus reglas de archivo.
                var tenantId = enSesion.TenantId;
                var codigo = canonico.Codigo;
                var candidatos = await consulta.Query<TipoDocumentoTenant>()
                    .Where(t => t.AnyTenant() && t.TenantId == tenantId && t.TipoCanonico == codigo && t.Activo)
                    .ToListAsync(ct);
                tipoTenant = canonico.Ambito == AmbitoDocumento.PorVinculo
                    ? candidatos.FirstOrDefault(t => t.Ambito == AmbitoDocumento.PorVinculo)
                      ?? throw ReglaDeNegocioException.Validacion("tipo-documento-invalido", $"La empresa no tiene configurado el tipo {codigo}.")
                    : candidatos.Where(t => t.Ambito == AmbitoDocumento.Global).OrderBy(t => t.Codigo).FirstOrDefault();
            }
        }

        var terceroId = tercero.TerceroId;
        if (tipoTenant is null || tipoTenant.Ambito == AmbitoDocumento.Global)
        {
            var canonico = CatalogoCanonico.Requerido(tipoTenant?.TipoCanonico ?? tipoCanonico);
            Documento.AsegurarClaseSoportada(canonico.Clase, canonico.Codigo);
            if (canonico.Ambito != AmbitoDocumento.Global)
                throw ReglaDeNegocioException.Validacion("vinculo-requerido", $"{canonico.Codigo} es un documento por vínculo.");
            return new DestinoDeCarga(Documento.ClaveGlobal(terceroId, canonico.Codigo), ahora => Documento.CrearGlobal(terceroId, canonico, ahora),
                ClavesDeAlmacen.BucketBoveda, enSesion?.VinculoId,
                ReglasDeArchivo.Efectivas(canonico, tipoTenant, reglas.TamanoMaximoBytes, reglas.MaximoPaginas), null);
        }

        Documento.AsegurarCargaSoportada(tipoTenant);
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
            ClavesDeAlmacen.BucketDeTenant(propio.TenantId), vinculo.Id,
            tipo.ReglasEfectivas(reglas.TamanoMaximoBytes, reglas.MaximoPaginas), tipo);
    }
}

/// <summary>
/// Versión solo de formulario (TD-4): el tercero envía los valores de los campos de un tipo <c>Formulario</c> de la empresa
/// de su vínculo y nace una versión del documento <c>PorVinculo</c> con sus <c>DatosDeclarados</c>, sin binario. Misma
/// mecánica que un archivo: deduplicación (por la huella de los datos), versión <c>Actual</c>, compartición con el
/// vínculo, adjunción a su expediente abierto y vigencia.
/// </summary>
public static class FormulariosEndpoints
{
    /// <summary>
    /// 200 <c>ConfirmacionCargaDto</c> (<c>duplicado: true</c> si los mismos datos ya son una versión). 422
    /// <c>campo-requerido</c>, <c>valor-invalido</c>, <c>campo-desconocido</c> (con <c>campo</c>), <c>archivo-requerido</c>
    /// (el tipo pide también el archivo: va por la carga en dos pasos con <c>datos</c>), <c>clase-no-soportada</c>,
    /// <c>tipo-documento-invalido</c>; 404 vínculo fuera de la sesión; 409 <c>estado-invalido</c>.
    /// </summary>
    [WolverinePost("/public/v1/boveda/formularios")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ConfirmacionCargaDto> Enviar(EnviarFormularioDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        IMessageBus mensajes, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        if (dto.TipoDocumentoTenantId is null && string.IsNullOrWhiteSpace(dto.TipoCanonico))
            throw ReglaDeNegocioException.Validacion("tipo-documento-requerido", "Indica el tipo de documento de la empresa.");

        var destino = await CargaEndpoints.ResolverDestinoAsync(dto.TipoCanonico, dto.TipoDocumentoTenantId, dto.VinculoId, tercero, sesiones,
            reglas, ct);
        var tipo = destino.Tipo is { PideDatos: true } conDatos
            ? conDatos
            : throw ReglaDeNegocioException.Validacion("clase-no-soportada", "Ese documento no tiene formulario: súbelo como archivo.");
        if (tipo.PideArchivo)
            throw ReglaDeNegocioException.Validacion("archivo-requerido",
                $"{tipo.Nombre} pide también el archivo: inicia la carga en /public/v1/boveda/documentos con los datos.");
        var datos = EsquemaDeFormulario.ValidarDatos(tipo.Campos, MapeosDocumentos.ATexto(dto.Datos), reloj.Hoy());

        return await Peticiones.ConReintentosAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            Guid documentoId;
            await using (var creacion = sesiones.Boveda())
            {
                documentoId = (await CargaEndpoints.DocumentoParaEscribirAsync(creacion, destino, ahora, ct)).Id;
                await creacion.SaveChangesAsync(ct);
            }

            await using var sesion = sesiones.Boveda(mensajes);
            var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
            var documento = BovedaEndpoints.DelTercero(stream.Aggregate, tercero);

            if (documento.DetectarDuplicado(datos.Hash, ahora, datos.Hash) is { } duplicado)
            {
                stream.AppendOne(duplicado);
                await sesion.SaveChangesAsync(ct);
                return new ConfirmacionCargaDto(true, documento.VersionRequerida(duplicado.NumeroExistente).ADto(documento.Id, null, reloj.Hoy()), []);
            }

            var eventos = documento.RegistrarVersionFormulario(datos, destino.ContextoVinculoId, tercero.TerceroId.ToString(), ahora);
            foreach (var evento in eventos)
                stream.AppendOne(evento);
            var proyectado = documento.Proyectar(eventos);
            var compartidos = await ComparticionDeDocumentos.CompartirActualAsync(sesion, stream, proyectado, OrigenComparticion.Automatica, ahora, ct,
                mensajes, contexto);
            await sesion.SaveChangesAsync(ct);
            return new ConfirmacionCargaDto(false, proyectado.VersionRequerida(proyectado.VersionActual!.Value).ADto(documento.Id, null, reloj.Hoy()),
                compartidos);
        });
    }
}
