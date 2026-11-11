using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Informes;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>Genera el ZIP de la exportación de la bóveda de un tercero (cola durable y secuencial <c>exportaciones</c>, OD-9).</summary>
public sealed record GenerarExportacionDeBoveda(Guid ExportacionId);

/// <summary>
/// El tercero exporta toda su bóveda (OD-9, docs/07 §8: acceso y portabilidad). Se pide, se genera en segundo plano y se baja con una URL
/// prefirmada (<c>/public/v1/exportaciones/{id}/descarga</c>) mientras la exportación esté vigente (7 días por omisión).
/// </summary>
public static class ExportacionDeBovedaEndpoints
{
    public const int MaximoPorDiaPorOmision = 5;

    public static ExportacionBovedaDto ADto(ExportacionDeBoveda e) => new(e.Id, e.Estado.ToString(), e.SolicitadaEn, e.TerminadaEn, e.ExpiraEn, e.Archivos, e.Bytes, e.Sha256,
        e.HashManifiesto, e.Kid, e.CodigoDeError, e.Descargas);

    /// <summary>
    /// Pide la exportación (202): un ZIP con los archivos de la bóveda, sus datos en JSON y un manifiesto firmado con el SHA-256 de cada
    /// archivo. 409 <c>exportacion-en-curso</c> si ya hay una generándose; 429 <c>demasiadas-exportaciones</c> pasado el tope diario.
    /// </summary>
    [WolverinePost("/public/v1/boveda/exportar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Solicitar(HttpContext http, ISesiones sesiones, IConfiguration configuracion, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var ahora = reloj.GetUtcNow();
        var tope = Math.Max(1, configuracion.GetValue("Validasoft:Habeas:ExportacionesPorDia", MaximoPorDiaPorOmision));

        await using var consulta = sesiones.ConsultaGlobal();
        var propias = await consulta.Query<ExportacionDeBoveda>().Where(e => e.TerceroId == tercero.TerceroId).ToListAsync(ct);
        if (propias.Any(e => e.EnCurso))
            throw ReglaDeNegocioException.Conflicto("exportacion-en-curso", "Ya tienes una exportación en preparación; espera a que termine.");
        if (propias.Count(e => e.SolicitadaEn > ahora.AddDays(-1)) >= tope)
            throw ReglaDeNegocioException.DemasiadosIntentos("demasiadas-exportaciones", $"Hiciste {tope} exportaciones hoy; inténtalo de nuevo mañana.");

        var exportacion = ExportacionDeBoveda.Solicitar(tercero.TerceroId, tercero.Correo, BovedaEndpoints.Ip(http), ahora);
        await using var sesion = sesiones.Global(mensajes);
        sesion.Store(exportacion);
        await mensajes.PublishAsync(new GenerarExportacionDeBoveda(exportacion.Id));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/public/v1/exportaciones/{exportacion.Id}", ADto(exportacion));
    }

    [WolverineGet("/public/v1/exportaciones")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<ExportacionBovedaDto>> Listar(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaGlobal();
        var propias = await consulta.Query<ExportacionDeBoveda>().Where(e => e.TerceroId == tercero.TerceroId).OrderByDescending(e => e.SolicitadaEn).Take(20).ToListAsync(ct);
        return [.. propias.Select(ADto)];
    }

    [WolverineGet("/public/v1/exportaciones/{id}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ExportacionBovedaDto> Obtener(Guid id, HttpContext http, ISesiones sesiones, CancellationToken ct) =>
        ADto(await CargarAsync(id, SesionDeTercero.De(http.User), sesiones, ct));

    /// <summary>
    /// URL prefirmada (5 minutos) para bajar el ZIP; queda anotada la descarga (quién, cuándo, desde dónde). 409
    /// <c>exportacion-no-disponible</c> si aún no está lista; 410 <c>exportacion-vencida</c>.
    /// </summary>
    [WolverinePost("/public/v1/exportaciones/{id}/descarga")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<DescargaDto> Descargar(Guid id, HttpContext http, ISesiones sesiones, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var exportacion = await CargarAsync(id, SesionDeTercero.De(http.User), sesiones, ct);
        exportacion.AsegurarDescargable(ahora);
        exportacion.RegistrarDescarga(BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http), ahora);
        await using (var sesion = sesiones.Global())
        {
            sesion.Store(exportacion);
            await sesion.SaveChangesAsync(ct);
        }

        var nombre = $"boveda-{CalculadoraDeVigencia.Fecha(exportacion.TerminadaEn ?? ahora):yyyy-MM-dd}.zip";
        var url = await almacen.UrlDeDescargaAsync(exportacion.Bucket!, exportacion.Clave!, nombre, Exportaciones.TipoZip, reglas.VigenciaDescarga, ct);
        return new DescargaDto(url.Url, url.Expira, nombre, Exportaciones.TipoZip);
    }

    private static async Task<ExportacionDeBoveda> CargarAsync(Guid id, SesionDeTercero tercero, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var exportacion = await consulta.LoadAsync<ExportacionDeBoveda>(id, ct);
        return exportacion is not null && exportacion.TerceroId == tercero.TerceroId
            ? exportacion
            : throw ReglaDeNegocioException.NoEncontrado("exportacion-no-encontrada", "No existe esa exportación.");
    }
}

/// <summary>
/// Genera el ZIP de la bóveda sin cargarlo en memoria (OD-9): cada archivo pasa del almacenamiento al ZIP por un buffer y se hashea al
/// pasar; el ZIP se arma en un archivo temporal y se sube de una vez. Lleva, además de los archivos, sus datos en JSON (tercero,
/// ficha, cuentas con el número enmascarado, vínculos con sus consentimientos, certificados y solicitudes de habeas data), un
/// <c>manifiesto.json</c> (RFC 8785) con la ruta, el tamaño y el SHA-256 de todo, su firma Ed25519 (<c>manifiesto.firma.json</c>, la misma
/// clave que sella los certificados) y un <c>LEEME.txt</c> con cómo verificarlo.
/// </summary>
public static class GenerarExportacionDeBovedaHandler
{
    public const string EsquemaDelManifiesto = "validasoft.exportacion-boveda.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private sealed record ArchivoDelZip(string Ruta, string Tipo, string Sha256, long Bytes, Guid? DocumentoId = null, int? Version = null, bool? CoincideConLaBoveda = null);

    public static async Task Handle(GenerarExportacionDeBoveda mensaje, ISesiones sesiones, IAlmacenDeArchivos almacen, ClavesDeCertificados sellador, IMessageContext mensajes,
        IConfiguration configuracion, TimeProvider reloj, ILogger<GenerarExportacionDeBoveda> logger, CancellationToken ct)
    {
        ExportacionDeBoveda? exportacion;
        await using (var consulta = sesiones.ConsultaGlobal())
            exportacion = await consulta.LoadAsync<ExportacionDeBoveda>(mensaje.ExportacionId, ct);
        if (exportacion is null || exportacion.Estado is not (EstadoDeExportacionDeBoveda.Pendiente or EstadoDeExportacionDeBoveda.Generando))
            return;

        await Guardar(sesiones, exportacion, e => e.Iniciar(reloj.GetUtcNow()), ct);
        var rutaTemporal = Path.Combine(Path.GetTempPath(), $"validasoft-exportacion-{exportacion.Id:N}.zip");
        try
        {
            var resultado = await ArmarAsync(exportacion, sesiones, almacen, sellador, rutaTemporal, reloj.GetUtcNow(), ct);
            var bytes = new FileInfo(rutaTemporal).Length;
            string sha256;
            await using (var archivo = File.OpenRead(rutaTemporal))
                sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(archivo, ct));

            var clave = ClavesDeAlmacen.ExportacionDeBoveda(exportacion.TerceroId, exportacion.Id);
            await almacen.GuardarArchivoAsync(ClavesDeAlmacen.BucketBoveda, clave, rutaTemporal, Exportaciones.TipoZip,
                new Dictionary<string, string> { ["sha256"] = sha256, ["tercero"] = exportacion.TerceroId.ToString("N") }, ct);

            var vigencia = TimeSpan.FromDays(Math.Clamp(configuracion.GetValue("Validasoft:Habeas:DiasDeVigenciaDeExportaciones", 7), 1, 30));
            var ahora = reloj.GetUtcNow();
            await Guardar(sesiones, exportacion,
                e => e.Terminar(ClavesDeAlmacen.BucketBoveda, clave, resultado.Archivos, bytes, sha256, resultado.HashManifiesto, resultado.Kid, vigencia, ahora), ct);
            if (!string.IsNullOrWhiteSpace(exportacion.Correo))
                await mensajes.PublishAsync(new CorreoDeExportacionListaPendiente(exportacion.Correo, ahora + vigencia, resultado.Archivos));
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "La exportación {Exportacion} de la bóveda falló.", exportacion.Id);
            await Guardar(sesiones, exportacion, x => x.Fallar("generacion-fallida", reloj.GetUtcNow()), ct);
        }
        finally
        {
            if (File.Exists(rutaTemporal))
                File.Delete(rutaTemporal);
        }
    }

    private static async Task Guardar(ISesiones sesiones, ExportacionDeBoveda exportacion, Action<ExportacionDeBoveda> cambio, CancellationToken ct)
    {
        cambio(exportacion);
        await using var sesion = sesiones.Global();
        sesion.Store(exportacion);
        await sesion.SaveChangesAsync(ct);
    }

    private sealed record ResultadoDelZip(int Archivos, string HashManifiesto, string Kid);

    private static async Task<ResultadoDelZip> ArmarAsync(ExportacionDeBoveda exportacion, ISesiones sesiones, IAlmacenDeArchivos almacen, ClavesDeCertificados sellador,
        string ruta, DateTimeOffset ahora, CancellationToken ct)
    {
        var terceroId = exportacion.TerceroId;
        await using var global = sesiones.ConsultaGlobal();
        await using var boveda = sesiones.ConsultaBoveda();
        var tercero = await global.LoadAsync<Tercero>(terceroId, ct) ?? throw new InvalidOperationException("El tercero ya no existe.");
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
        var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).OrderBy(s => s.Seccion).ThenBy(s => s.Periodo).ToListAsync(ct);
        var cuentas = await boveda.Query<CuentaBancaria>().Where(c => c.TerceroId == terceroId).OrderBy(c => c.RegistradaEn).ToListAsync(ct);
        var vinculos = await global.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId).OrderBy(v => v.CreadoEn).ToListAsync(ct);
        var certificados = await global.Query<Certificado>().Where(c => c.AnyTenant() && c.TerceroId == terceroId).ToListAsync(ct);
        var solicitudes = await boveda.Query<SolicitudHabeas>().Where(s => s.TerceroId == terceroId).OrderBy(s => s.RecibidaEn).ToListAsync(ct);
        var empresas = (await global.LoadManyAsync<Empresa>(ct, vinculos.Select(v => v.EmpresaId).Concat(certificados.Select(c => c.EmpresaId)).Distinct().ToArray()))
            .ToDictionary(e => e.Id, e => e.Nombre);

        var archivos = new List<ArchivoDelZip>();
        await using var salida = new FileStream(ruta, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous);
        using (var zip = new ZipArchive(salida, ZipArchiveMode.Create, leaveOpen: true))
        {
            // Los archivos de la bóveda: toda versión que tenga binario y no esté devuelta, retirada ni suprimida.
            foreach (var documento in documentos)
            {
                foreach (var version in documento.Versiones.Where(v => v.TieneArchivo && !v.Suprimida && v.Estado is not (EstadoVersion.Retirada or EstadoVersion.Devuelta)))
                {
                    var carpeta = Limpio(documento.TipoCanonico ?? documento.NombreTipo);
                    var rutaEnZip = $"documentos/{carpeta}/{documento.Id:N}/v{version.Numero}-{Limpio(version.NombreOriginal)}";
                    var (sha, bytes) = await CopiarAsync(zip, rutaEnZip, await almacen.AbrirLecturaAsync(version.Bucket, version.ClaveObjeto, ct), ct);
                    archivos.Add(new ArchivoDelZip(rutaEnZip, "documento", sha, bytes, documento.Id, version.Numero,
                        string.Equals(sha, version.Sha256, StringComparison.OrdinalIgnoreCase)));
                }
            }

            // Los datos, en JSON legible.
            archivos.Add(await EscribirJsonAsync(zip, "datos/tercero.json", new
            {
                tercero.Id, identificacion = new { tercero.Identificacion.Pais, tercero.Identificacion.Tipo, tercero.Identificacion.Numero, tercero.Identificacion.DigitoVerificacion },
                tipoPersona = tercero.TipoPersona.ToString(), tercero.Nombre, tercero.NombreComercial, contactos = tercero.Contactos, tercero.EsExtranjero, tercero.CreadoEn
            }, ct));
            archivos.Add(await EscribirJsonAsync(zip, "datos/documentos.json", documentos.Select(d => new
            {
                d.Id, tipo = d.NombreTipo, d.TipoCanonico, ambito = d.Ambito.ToString(), clase = d.Clase.ToString(), d.VersionActual,
                versiones = d.Versiones.Select(v => new
                {
                    v.Numero, estado = v.Estado.ToString(), v.Sha256, v.Tamano, v.NombreOriginal, v.CargadaEn, v.CargadaPor, v.Suprimida, v.RetenidaPorLey,
                    v.BaseLegalDeRetencion, v.RetenidaHasta, datosDeclarados = v.Datos?.Select(x => new { x.Clave, x.Etiqueta, x.Valor }),
                    compartidaCon = d.Comparticiones.Where(c => c.Numero == v.Numero).Select(c => empresas.GetValueOrDefault(c.EmpresaId) ?? "una empresa").Distinct()
                })
            }), ct));
            foreach (var seccion in secciones.Where(s => s.Vigente is not null))
            {
                var vigente = seccion.Vigente!;
                archivos.Add(await EscribirJsonAsync(zip, $"datos/ficha/{Limpio(seccion.Seccion)}{(seccion.Periodo is { } p ? $"-{p}" : string.Empty)}.json",
                    new { seccion.Seccion, seccion.Periodo, version = vigente.Numero, vigente.Huella, vigente.Fecha, datos = vigente.Datos, soportes = vigente.Soportes }, ct));
            }

            archivos.Add(await EscribirJsonAsync(zip, "datos/cuentas-bancarias.json", cuentas.Select(c => new
            {
                c.Id, c.Banco, tipoCuenta = c.TipoCuenta.ToString(), numero = c.NumeroEnmascarado, c.Titular, c.IdentificacionTitular, c.Moneda, estado = c.Estado.ToString(), c.RegistradaEn
            }), ct));
            archivos.Add(await EscribirJsonAsync(zip, "datos/vinculos.json", vinculos.Select(v => new
            {
                v.Id, empresa = empresas.GetValueOrDefault(v.EmpresaId), estado = v.Estado.ToString(), v.CreadoEn, v.InvitacionAceptada, v.InvitacionAceptadaEn,
                consentimientos = v.Consentimientos.Select(c => new { c.Tipo, c.Version, c.Hash })
            }), ct));
            archivos.Add(await EscribirJsonAsync(zip, "datos/certificados.json", certificados.Select(c => new
            {
                empresa = empresas.GetValueOrDefault(c.EmpresaId), c.FolioRaiz, estado = c.Estado.ToString(),
                emisiones = c.Emisiones.Select(e => new { e.Folio, e.EmitidoEn, e.VigenteHasta, estado = e.Estado.ToString(), e.HashContenido, e.Sello, e.Kid, e.UrlVerificacion })
            }), ct));
            archivos.Add(await EscribirJsonAsync(zip, "datos/solicitudes-habeas-data.json", solicitudes.Select(s => new
            {
                s.Radicado, tipo = s.Tipo.ToString(), alcance = s.Alcance?.ToString(), destino = s.EmpresaId is { } e ? empresas.GetValueOrDefault(e) ?? "una empresa" : "Validasoft",
                estado = s.Estado.ToString(), resultado = s.Resultado?.ToString(), s.RecibidaEn, s.VenceEl, s.ResueltaEn, s.ConstanciaHash
            }), ct));

            // Manifiesto canónico y su firma: lo que se firma son exactamente los bytes del manifiesto.
            var manifiesto = Jcs.Canonicalizar(JsonSerializer.Serialize(new
            {
                esquema = EsquemaDelManifiesto, exportacionId = exportacion.Id, terceroId, generadoEn = ahora.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                totalArchivos = archivos.Count,
                archivos = archivos.OrderBy(a => a.Ruta, StringComparer.Ordinal).Select(a => new
                {
                    ruta = a.Ruta, tipo = a.Tipo, sha256 = a.Sha256, bytes = a.Bytes, documentoId = a.DocumentoId, version = a.Version, coincideConLaBoveda = a.CoincideConLaBoveda
                })
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            var bytesManifiesto = Encoding.UTF8.GetBytes(manifiesto);
            var hashManifiesto = Convert.ToHexStringLower(SHA256.HashData(bytesManifiesto));
            await EscribirAsync(zip, "manifiesto.json", bytesManifiesto, ct);
            var firma = new { algoritmo = SelloEd25519.Algoritmo, kid = sellador.Kid, hashManifiesto, firma = Convert.ToBase64String(sellador.Firmar(bytesManifiesto)), clavesPublicas = "/.well-known/validasoft-certificados.json" };
            await EscribirAsync(zip, "manifiesto.firma.json", JsonSerializer.SerializeToUtf8Bytes(firma, Json), ct);
            await EscribirAsync(zip, "LEEME.txt", Encoding.UTF8.GetBytes(Leeme(sellador.Kid, hashManifiesto)), ct);
            return new ResultadoDelZip(archivos.Count, hashManifiesto, sellador.Kid);
        }
    }

    private static string Leeme(string kid, string hashManifiesto) =>
        "Exportación de tu bóveda de Validasoft\n" +
        "=====================================\n\n" +
        "documentos/        tus archivos, uno por versión.\n" +
        "datos/             tus datos en JSON (tu información, documentos, ficha, cuentas con el número enmascarado, vínculos, certificados y solicitudes).\n" +
        "manifiesto.json    la ruta, el tamaño y el SHA-256 de cada archivo (JSON canónico, RFC 8785).\n" +
        "manifiesto.firma.json  la firma Ed25519 de Validasoft sobre los bytes exactos de manifiesto.json.\n\n" +
        "Cómo verificarla\n" +
        "1. Calcula el SHA-256 de cada archivo y compáralo con el del manifiesto.\n" +
        $"2. Calcula el SHA-256 de manifiesto.json: debe ser {hashManifiesto}.\n" +
        $"3. Verifica la firma Ed25519 (clave {kid}) con la clave pública que Validasoft publica en /.well-known/validasoft-certificados.json.\n";

    /// <summary>Copia un objeto del almacenamiento a una entrada del ZIP calculando su SHA-256 y su tamaño al pasar.</summary>
    private static async Task<(string Sha256, long Bytes)> CopiarAsync(ZipArchive zip, string ruta, Stream origen, CancellationToken ct)
    {
        await using (origen)
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using var entrada = zip.CreateEntry(ruta, CompressionLevel.Optimal).Open();
            var buffer = new byte[81920];
            int leidos;
            while ((leidos = await origen.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, leidos);
                await entrada.WriteAsync(buffer.AsMemory(0, leidos), ct);
                total += leidos;
            }

            return (Convert.ToHexStringLower(hash.GetHashAndReset()), total);
        }
    }

    private static async Task<ArchivoDelZip> EscribirJsonAsync(ZipArchive zip, string ruta, object contenido, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(contenido, Json);
        await EscribirAsync(zip, ruta, bytes, ct);
        return new ArchivoDelZip(ruta, "datos", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength);
    }

    private static async Task EscribirAsync(ZipArchive zip, string ruta, byte[] contenido, CancellationToken ct)
    {
        await using var entrada = zip.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        await entrada.WriteAsync(contenido, ct);
    }

    /// <summary>Nombre seguro dentro del ZIP: sin rutas ni caracteres reservados.</summary>
    private static string Limpio(string nombre)
    {
        var texto = new string([.. Path.GetFileName(nombre).Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_')]);
        return texto.Length == 0 ? "archivo" : texto.Length > 120 ? texto[^120..] : texto;
    }
}

/// <summary>Elimina los ZIP vencidos: el objeto del almacenamiento y la ruta de descarga (OD-10).</summary>
public static class LimpiarExportacionesDeBovedaHandler
{
    public static async Task Handle(LimpiarExportacionesDeBoveda mensaje, ISesiones sesiones, IAlmacenDeArchivos almacen, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = mensaje.Ahora ?? reloj.GetUtcNow();
        List<ExportacionDeBoveda> vencidas;
        await using (var consulta = sesiones.ConsultaGlobal())
            vencidas = [.. await consulta.Query<ExportacionDeBoveda>().Where(e => e.Estado == EstadoDeExportacionDeBoveda.Lista && e.ExpiraEn <= ahora).ToListAsync(ct)];

        foreach (var exportacion in vencidas)
        {
            if (exportacion.Bucket is not null && exportacion.Clave is not null)
                await almacen.EliminarAsync(exportacion.Bucket, exportacion.Clave, ct);
            exportacion.Vencer();
            await using var sesion = sesiones.Global();
            sesion.Store(exportacion);
            await sesion.SaveChangesAsync(ct);
        }
    }
}
