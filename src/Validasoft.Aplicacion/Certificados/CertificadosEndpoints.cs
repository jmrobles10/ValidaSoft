using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Certificados;

/// <summary>
/// Certificados de la empresa activa (<c>X-Empresa</c>): el del vínculo con su historial, una emisión por folio, la descarga
/// auditada del PDF (URL prefirmada) y la revocación por un administrador. Un folio de otra empresa responde 404 (T-4).
/// </summary>
public static class CertificadosEndpoints
{
    /// <summary>Emisión actual (en el estado que tenga) e historial; sin certificado (nunca aprobado) ⇒ <c>actual: null</c>.</summary>
    [WolverineGet("/api/v1/vinculos/{id}/certificado")]
    [Authorize(Policy = Permisos.CertificadosLeer)]
    public static async Task<CertificadoDelVinculoDto> DelVinculo(Guid id, SesionDeEmpresa empresa, EmisorDeCertificados emisor, CancellationToken ct)
    {
        await empresa.CargarVinculoAsync(id, ct);
        var certificado = await empresa.Sesion.LoadAsync<Certificado>(Certificado.IdPara(id), ct);
        if (certificado is null || certificado.EmpresaId != empresa.EmpresaId)
            return new CertificadoDelVinculoDto(id, null, []);

        var datos = await empresa.Sesion.LoadAsync<Empresa>(certificado.EmpresaId, ct);
        return new CertificadoDelVinculoDto(id, certificado.ADto(certificado.Actual, datos, emisor),
            [.. certificado.Emisiones.OrderByDescending(e => e.Numero).Select(e => e.AResumen())]);
    }

    /// <summary>Folio con emisión (<c>…-{n}</c>) ⇒ esa emisión; folio raíz ⇒ la actual.</summary>
    [WolverineGet("/api/v1/certificados/{folio}")]
    [Authorize(Policy = Permisos.CertificadosLeer)]
    public static async Task<CertificadoDto> Obtener(string folio, SesionDeEmpresa empresa, EmisorDeCertificados emisor, CancellationToken ct)
    {
        var (certificado, emision) = await BuscarAsync(empresa, folio, ct);
        return certificado.ADto(emision, await empresa.Sesion.LoadAsync<Empresa>(certificado.EmpresaId, ct), emisor);
    }

    /// <summary>
    /// URL prefirmada del PDF de la emisión (5 min, <c>attachment</c>). Cada entrega queda en el stream del certificado
    /// (<c>CertificadoDescargado</c> con actor, IP y user-agent). Si el PDF aún no se generó, se genera aquí.
    /// </summary>
    [WolverinePost("/api/v1/certificados/{folio}/descarga")]
    [Authorize(Policy = Permisos.CertificadosLeer)]
    public static async Task<DescargaDto> Descargar(string folio, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http,
        ArchivoDeCertificados archivo, IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var (certificado, emision) = await BuscarAsync(empresa, folio, ct);
        var pdf = emision.Pdf ?? await archivo.AsegurarPdfAsync(certificado.Tenant, certificado.Id, emision.Numero, ct)
                  ?? throw ReglaDeNegocioException.Conflicto("pdf-pendiente", "El PDF del certificado aún no está disponible.");

        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<Certificado>(certificado.Id, ct);
        stream.AppendOne(stream.Aggregate!.RegistrarDescarga(emision.Numero, contexto.ActorTipo.ToString(), contexto.ActorId,
            BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http), false, reloj.GetUtcNow()));
        await empresa.Sesion.SaveChangesAsync(ct);
        return await UrlAsync(almacen, reglas, pdf, emision.Folio, ct);
    }

    /// <summary>
    /// Revocación por decisión humana (fraude confirmado, error de emisión): <c>expedientes:administrar</c> y justificación.
    /// Irreversible; solo la emisión actual (una reemplazada ⇒ 409 <c>emision-no-actual</c>).
    /// </summary>
    [WolverinePost("/api/v1/certificados/{folio}/revocar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<CertificadoDto> Revocar(string folio, RevocarCertificadoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        EmisorDeCertificados emisor, TimeProvider reloj, CancellationToken ct)
    {
        var (encontrado, emision) = await BuscarAsync(empresa, folio, ct);
        if (emision.Numero != encontrado.EmisionActual)
            throw ReglaDeNegocioException.Conflicto("emision-no-actual",
                $"La emisión {emision.Folio} fue reemplazada por {encontrado.Actual.Folio}: se revoca la actual.");

        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<Certificado>(encontrado.Id, ct);
        var certificado = stream.Aggregate!;
        stream.AppendOne(certificado.Revocar(dto?.Justificacion, OrigenRevocacion.Administrador, contexto.ActorId, reloj.GetUtcNow()));
        await empresa.Sesion.SaveChangesAsync(ct);

        var revocado = (await empresa.Sesion.LoadAsync<Certificado>(certificado.Id, ct))!;
        return revocado.ADto(revocado.Actual, await empresa.Sesion.LoadAsync<Empresa>(revocado.EmpresaId, ct), emisor);
    }

    private static async Task<(Certificado, EmisionDeCertificado)> BuscarAsync(SesionDeEmpresa empresa, string folio, CancellationToken ct)
    {
        var leido = FoliosDeCertificado.Leer(folio) ?? throw NoEncontrado();
        var raiz = leido.Raiz;
        var empresaId = empresa.EmpresaId;
        var certificado = await empresa.Sesion.Query<Certificado>().FirstOrDefaultAsync(c => c.EmpresaId == empresaId && c.FolioRaiz == raiz, ct)
                          ?? throw NoEncontrado();
        var emision = (leido.Emision is { } n ? certificado.Buscar(n) : certificado.Actual) ?? throw NoEncontrado();
        return (certificado, emision);
    }

    internal static async Task<DescargaDto> UrlAsync(IAlmacenDeArchivos almacen, ReglasDeCarga reglas, PdfDeCertificado pdf, string folio,
        CancellationToken ct)
    {
        var nombre = $"{folio}.pdf";
        var url = await almacen.UrlDeDescargaAsync(pdf.Bucket, pdf.Clave, nombre, ArchivoDeCertificados.TipoContenido, reglas.VigenciaDescarga, ct);
        return new DescargaDto(url.Url, url.Expira, nombre, ArchivoDeCertificados.TipoContenido);
    }

    internal static ReglaDeNegocioException NoEncontrado() =>
        ReglaDeNegocioException.NoEncontrado("certificado-no-encontrado", "No existe un certificado con ese folio.");
}

/// <summary>
/// Verificación pública (sin credencial, con el límite por IP de <c>/public/v1</c>) y claves del sello. El folio no es
/// único entre tenants (el prefijo solo lo es en su tenant): la huella que acompaña al folio en el PDF y en el QR lo
/// desambigua (CE-8).
/// </summary>
public static class VerificacionPublicaEndpoints
{
    public const string RutaClaves = "/.well-known/validasoft-certificados.json";

    /// <summary>
    /// <c>CertificadoPublicoDto</c> de la emisión (la actual con el folio raíz). <c>?huella=</c> (12 hexadecimales, con o sin
    /// separadores) es opcional salvo que el folio exista en más de un tenant (409 <c>folio-ambiguo</c>).
    /// </summary>
    [WolverineGet("/public/v1/verificar/{folio}")]
    [AllowAnonymous]
    public static async Task<CertificadoPublicoDto> Verificar(string folio, string? huella, ISesiones sesiones, EmisorDeCertificados emisor,
        HttpContext http, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var (certificado, emision) = await BuscarAsync(consulta, folio, huella, ct);
        http.Response.Headers.CacheControl = "no-store";
        return certificado.APublico(emision, await consulta.LoadAsync<Empresa>(certificado.EmpresaId, ct), emisor);
    }

    /// <summary>
    /// Descarga pública del PDF solo de una emisión <c>Vigente</c> (otro estado ⇒ 409 <c>certificado-no-vigente</c>): quien
    /// verifica un certificado vigente puede guardar la copia; uno suspendido, vencido, revocado o reemplazado no se
    /// redistribuye. Queda auditada como descarga pública (IP y user-agent).
    /// </summary>
    [WolverinePost("/public/v1/verificar/{folio}/descarga")]
    [AllowAnonymous]
    public static async Task<DescargaDto> Descargar(string folio, string? huella, ISesiones sesiones, HttpContext http, ArchivoDeCertificados archivo,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        Certificado certificado;
        EmisionDeCertificado emision;
        await using (var consulta = sesiones.ConsultaGlobal())
            (certificado, emision) = await BuscarAsync(consulta, folio, huella, ct);
        if (emision.Estado != EstadoCertificado.Vigente)
            throw ReglaDeNegocioException.Conflicto("certificado-no-vigente", $"El certificado está {emision.Estado}: su PDF no se entrega.");

        var pdf = emision.Pdf ?? await archivo.AsegurarPdfAsync(certificado.Tenant, certificado.Id, emision.Numero, ct)
                  ?? throw ReglaDeNegocioException.Conflicto("pdf-pendiente", "El PDF del certificado aún no está disponible.");

        await using var sesion = sesiones.ParaTenant(certificado.Tenant);
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(certificado.Id, ct);
        stream.AppendOne(stream.Aggregate!.RegistrarDescarga(emision.Numero, "Publico", null, BovedaEndpoints.Ip(http), BovedaEndpoints.UserAgent(http),
            true, reloj.GetUtcNow()));
        await sesion.SaveChangesAsync(ct);
        return await CertificadosEndpoints.UrlAsync(almacen, reglas, pdf, emision.Folio, ct);
    }

    /// <summary>Claves públicas Ed25519 del sello (la vigente y las anteriores) en bruto y como JWK (RFC 8037).</summary>
    [WolverineGet(RutaClaves)]
    [AllowAnonymous]
    public static ClavesDeCertificadosDto Claves(EmisorDeCertificados emisor, HttpContext http)
    {
        http.Response.Headers.CacheControl = "public, max-age=3600";
        return new ClavesDeCertificadosDto("Validasoft", SelloEd25519.Algoritmo,
            "Ed25519 sobre los 32 bytes del SHA-256 (hashContenido en hexadecimal) del contenidoCanonico canonicalizado con RFC 8785 (JCS).",
            [ContenidoDeCertificado.EsquemaV1],
            [
                .. emisor.Claves.Publicadas().Select(c => new ClavePublicaDeCertificadosDto(c.Kid, SelloEd25519.Algoritmo,
                    Convert.ToBase64String(c.ClavePublica),
                    new JwkDto("OKP", SelloEd25519.Algoritmo, Base64Url(c.ClavePublica), c.Kid, "EdDSA", "sig"), c.Vigente))
            ]);
    }

    private static async Task<(Certificado, EmisionDeCertificado)> BuscarAsync(IQuerySession consulta, string folio, string? huella,
        CancellationToken ct)
    {
        var leido = FoliosDeCertificado.Leer(folio) ?? throw CertificadosEndpoints.NoEncontrado();
        var prefijo = HuellaDeCertificado.Normalizar(huella);
        if (!string.IsNullOrWhiteSpace(huella) && prefijo is null)
            throw ReglaDeNegocioException.Validacion("huella-invalida", "La huella son 12 caracteres hexadecimales (xxxx·xxxx·xxxx).");

        var raiz = leido.Raiz;
        var candidatos = await consulta.Query<Certificado>().Where(c => c.AnyTenant() && c.FolioRaiz == raiz).ToListAsync(ct);
        var coincidencias = candidatos
            .SelectMany(c => Emisiones(c, leido.Emision, prefijo).Select(e => (Certificado: c, Emision: e)))
            .Where(x => prefijo is null || x.Emision.HashContenido.StartsWith(prefijo, StringComparison.Ordinal))
            .ToList();

        return coincidencias.Count switch
        {
            0 => throw CertificadosEndpoints.NoEncontrado(),
            1 => coincidencias[0],
            _ => throw ReglaDeNegocioException.Conflicto("folio-ambiguo",
                "Hay más de un certificado con ese folio: indica también la huella que aparece junto al folio (xxxx·xxxx·xxxx).")
        };
    }

    /// <summary>Con número de emisión, esa; sin él, la actual (o, con huella, cualquiera de sus emisiones).</summary>
    private static IEnumerable<EmisionDeCertificado> Emisiones(Certificado certificado, int? numero, string? prefijo) =>
        numero is { } n ? certificado.Buscar(n) is { } una ? [una] : []
        : prefijo is null ? [certificado.Actual]
        : certificado.Emisiones;

    internal static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Certificado → DTO. Nunca revisores, contactos ni quién revocó (está en el historial del stream).</summary>
internal static class MapeosCertificados
{
    public static CertificadoDto ADto(this Certificado c, EmisionDeCertificado e, Empresa? empresa, EmisorDeCertificados emisor)
    {
        var contenido = ContenidoDeCertificado.Leer(e.ContenidoCanonico);
        return new CertificadoDto(
            c.Id, e.Folio, c.FolioRaiz, e.Numero, e.Numero == c.EmisionActual, e.Estado.ToString(), Bloqueante(c, e),
            new EmpresaDelCertificadoDto(c.EmpresaId, contenido.Empresa.Nombre, empresa?.LogoUrl),
            c.VinculoId, e.ExpedienteId, e.ExpedienteFolio,
            new TitularDelCertificadoDto(contenido.Tercero.Nombre, contenido.Tercero.IdentificacionEnmascarada, contenido.Tercero.Pais,
                contenido.Tercero.TipoPersona),
            new TipoVinculoDelCertificadoDto(contenido.TipoVinculo.Codigo, contenido.TipoVinculo.Nombre),
            new FlujoDelCertificadoDto(contenido.Flujo.Codigo, contenido.Flujo.Nombre, contenido.Flujo.Version),
            e.EmitidoEn, e.VigenteHasta, e.Huella, e.HashContenido, e.UrlVerificacion,
            [.. contenido.Requisitos.Select(r => new RequisitoDelCertificadoDto(r.Codigo, r.Nombre, r.VersionN, r.Sha256, r.VigenteHasta, r.Eximido, r.Justificacion))],
            [.. contenido.Areas.Select(a => new AreaDelCertificadoDto(a.Area, a.Codigo, a.Origen, a.Fecha))],
            [],
            e.Estado == EstadoCertificado.Suspendido && c.MotivoSuspension is { } motivo ? new SuspensionDelCertificadoDto(motivo, e.EstadoDesde) : null,
            e.Revocacion is { } r ? new RevocacionDelCertificadoDto(r.Motivo, r.Origen.ToString(), r.Fecha) : null,
            e.Pdf is { } pdf ? new PdfDelCertificadoDto(pdf.Sha256, pdf.Tamano, pdf.GeneradoEn) : null,
            e.Descargas,
            Firma(e, emisor),
            Contenido(e),
            c.Version);
    }

    public static EmisionDelCertificadoDto AResumen(this EmisionDeCertificado e) =>
        new(e.Folio, e.Numero, e.Estado.ToString(), e.EmitidoEn, e.VigenteHasta, e.Huella, e.ExpedienteFolio, e.Pdf is not null);

    public static CertificadoPublicoDto APublico(this Certificado c, EmisionDeCertificado e, Empresa? empresa, EmisorDeCertificados emisor)
    {
        var contenido = ContenidoDeCertificado.Leer(e.ContenidoCanonico);
        return new CertificadoPublicoDto(
            e.Folio, e.Numero, e.Estado.ToString(), Bloqueante(c, e),
            new TitularPublicoDto(contenido.Tercero.Nombre, contenido.Tercero.IdentificacionEnmascarada),
            new EmisorPublicoDto(contenido.Empresa.Nombre, empresa?.LogoUrl),
            new FlujoDelCertificadoDto(contenido.Flujo.Codigo, contenido.Flujo.Nombre, contenido.Flujo.Version),
            e.EmitidoEn, e.VigenteHasta, e.Huella, e.HashContenido, Contenido(e), e.UrlVerificacion, [], Firma(e, emisor), e.EstadoDesde,
            e.Estado == EstadoCertificado.Reemplazado ? c.Actual.Folio : null,
            e.Estado == EstadoCertificado.Vigente);
    }

    /// <summary>
    /// Suspendido (B-5), revocado, vencido o, la emisión actual, con pendientes de revalidación vencidos (RV-4): no debe
    /// aceptarse (consulta <c>bloqueado: true</c>).
    /// </summary>
    private static bool Bloqueante(Certificado c, EmisionDeCertificado e) =>
        e.Estado is EstadoCertificado.Suspendido or EstadoCertificado.Revocado or EstadoCertificado.Vencido ||
        (e.Numero == c.EmisionActual && e.Estado == EstadoCertificado.ConPendientes && c.PendientesBloqueantes);

    private static FirmaDelCertificadoDto Firma(EmisionDeCertificado e, EmisorDeCertificados emisor) =>
        new(SelloEd25519.Algoritmo, e.Kid, $"{emisor.UrlPublica.TrimEnd('/')}{VerificacionPublicaEndpoints.RutaClaves}", e.Sello);

    /// <summary>El JSON canónico tal cual (claves ya en orden RFC 8785): el serializador lo escribe sin tocarlo.</summary>
    private static JsonElement Contenido(EmisionDeCertificado e)
    {
        using var documento = JsonDocument.Parse(e.ContenidoCanonico);
        return documento.RootElement.Clone();
    }
}

/// <summary>Lista de certificados de la empresa activa (CE-11, docs/06 P2 «Certificados»).</summary>
public static class ListaDeCertificadosEndpoints
{
    /// <summary>
    /// <paramref name="estado"/>: Vigente, ConPendientes, Suspendido, Vencido, Revocado o <c>Bloqueante</c> (los que la
    /// consulta de certificación responde bloqueados). <paramref name="q"/>: parte del nombre o la identificación del
    /// tercero, o del folio. Ordenados por la vigencia más próxima; cursor opaco.
    /// </summary>
    [WolverineGet("/api/v1/certificados")]
    [Authorize(Policy = Permisos.CertificadosLeer)]
    public static async Task<PaginaDto<FilaCertificadoDto>> Listar(SesionDeEmpresa empresa, string? estado, string? q, int? limite, string? cursor,
        CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var bloqueantes = string.Equals(estado, "Bloqueante", StringComparison.OrdinalIgnoreCase);
        EstadoCertificado? filtro = null;
        if (!string.IsNullOrWhiteSpace(estado) && !bloqueantes)
            filtro = Enum.TryParse<EstadoCertificado>(estado, true, out var e) && Enum.IsDefined(e)
                ? e
                : throw ReglaDeNegocioException.Validacion("estado-invalido",
                    $"'{estado}' no es un estado de certificado ({string.Join(", ", Enum.GetNames<EstadoCertificado>())} o Bloqueante).");

        var empresaId = empresa.EmpresaId;
        var certificados = await empresa.Sesion.Query<Certificado>().Where(c => c.EmpresaId == empresaId).ToListAsync(ct);
        var vinculos = (await empresa.Vinculos().ToListAsync(ct)).ToDictionary(v => v.Id);
        var terceros = (await empresa.Sesion.LoadManyAsync<Dominio.Terceros.Tercero>(ct, certificados.Select(c => c.TerceroId).Distinct().ToArray()))
            .ToDictionary(t => t.Id);
        var tipos = (await empresa.Sesion.Query<Dominio.Configuracion.TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);

        var texto = q?.Trim();
        var filas = certificados
            .Where(c => c.Emisiones.Count > 0)
            .Where(c => (filtro is null || c.Estado == filtro) && (!bloqueantes || c.Bloqueante))
            .Select(c =>
            {
                var actual = c.Actual;
                var tercero = terceros.GetValueOrDefault(c.TerceroId);
                var tipo = vinculos.TryGetValue(c.VinculoId, out var vinculo) ? tipos.GetValueOrDefault(vinculo.TipoVinculoId) : null;
                return new FilaCertificadoDto(c.Id, actual.Folio, actual.Numero, c.VinculoId,
                    new TerceroResumenDto(c.TerceroId, tercero?.Nombre ?? string.Empty, tercero?.ClaveIdentificacion ?? string.Empty),
                    new TipoVinculoResumenDto(tipo?.Id ?? Guid.Empty, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
                    c.Estado.ToString(), c.Bloqueante, actual.EmitidoEn, actual.VigenteHasta, actual.EstadoDesde);
            })
            .Where(f => string.IsNullOrEmpty(texto) ||
                        f.Tercero.Nombre.Contains(texto, StringComparison.CurrentCultureIgnoreCase) ||
                        f.Tercero.Identificacion.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                        f.Folio.Contains(texto, StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f.VigenteHasta ?? DateOnly.MaxValue).ThenBy(f => f.Tercero.Nombre)
            .ToList();

        var siguiente = filas.Count > desde + tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null;
        return new PaginaDto<FilaCertificadoDto>([.. filas.Skip(desde).Take(tamano)], siguiente);
    }
}
