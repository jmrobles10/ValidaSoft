using System.Text.Json;

namespace Validasoft.Contratos;

// ---------- Certificados (docs/05 §2.6 y §4; CE-1…CE-10 del registro) ----------

public sealed record EmpresaDelCertificadoDto(Guid Id, string Nombre, string? LogoUrl);

public sealed record TitularDelCertificadoDto(string Nombre, string IdentificacionEnmascarada, string Pais, string TipoPersona);

public sealed record FlujoDelCertificadoDto(string Codigo, string Nombre, int Version);

public sealed record TipoVinculoDelCertificadoDto(string Codigo, string Nombre);

/// <summary>Documento que sostuvo la aprobación: versión y SHA-256 o, si se eximió, la justificación (P-5-4).</summary>
public sealed record RequisitoDelCertificadoDto(
    string Codigo, string Nombre, int? Version, string? Sha256, DateOnly? VigenteHasta, bool Eximido, string? Justificacion);

/// <summary><see cref="Origen"/>: <c>Humano | Sistema | Administrador</c>. Nunca el nombre de quién aprobó.</summary>
public sealed record AreaDelCertificadoDto(string Area, string Codigo, string Origen, DateTimeOffset Fecha);

/// <summary>Sello Ed25519 sobre los 32 bytes del SHA-256 del contenido canónico; la clave está en <see cref="ClavePublicaUrl"/>.</summary>
public sealed record FirmaDelCertificadoDto(string Algoritmo, string Kid, string ClavePublicaUrl, string Valor);

public sealed record PdfDelCertificadoDto(string Sha256, long Tamano, DateTimeOffset GeneradoEn);

public sealed record SuspensionDelCertificadoDto(string Motivo, DateTimeOffset Desde);

/// <summary><see cref="Origen"/>: <c>Administrador | Sistema</c>.</summary>
public sealed record RevocacionDelCertificadoDto(string Motivo, string Origen, DateTimeOffset Fecha);

/// <summary>
/// Una emisión del certificado vista por la empresa (<c>certificados:leer</c>). <see cref="Folio"/> lleva la emisión
/// (<c>{Prefijo}-C-{Año}-{Secuencia}-{n}</c>); <see cref="Estado"/> es el de esa emisión (<c>Reemplazado</c> si hay una
/// más nueva). <see cref="ContenidoCanonico"/> es exactamente lo que se hasheó (claves en orden RFC 8785). <see cref="Pdf"/>
/// es nulo mientras se genera. <see cref="EnVerificacion"/> (VR-11, solo la emisión actual): el certificado sigue vigente mientras la empresa
/// revisa lo que el tercero actualizó; <see cref="Estado"/> sigue siendo <c>ConPendientes</c> y <see cref="Bloqueante"/> <c>false</c>.
/// </summary>
public sealed record CertificadoDto(
    Guid Id,
    string Folio,
    string FolioRaiz,
    int Emision,
    bool EsLaActual,
    string Estado,
    bool Bloqueante,
    EmpresaDelCertificadoDto Empresa,
    Guid VinculoId,
    Guid ExpedienteId,
    string ExpedienteFolio,
    TitularDelCertificadoDto Tercero,
    TipoVinculoDelCertificadoDto TipoVinculo,
    FlujoDelCertificadoDto Flujo,
    DateTimeOffset EmitidoEn,
    DateOnly? VigenteHasta,
    string Huella,
    string HashContenido,
    string UrlVerificacion,
    IReadOnlyList<RequisitoDelCertificadoDto> Requisitos,
    IReadOnlyList<AreaDelCertificadoDto> Areas,
    IReadOnlyList<string> Pendientes,
    SuspensionDelCertificadoDto? Suspension,
    RevocacionDelCertificadoDto? Revocacion,
    PdfDelCertificadoDto? Pdf,
    int Descargas,
    FirmaDelCertificadoDto Firma,
    JsonElement ContenidoCanonico,
    long Version,
    bool EnVerificacion = false);

/// <summary>Fila del historial de emisiones del vínculo.</summary>
public sealed record EmisionDelCertificadoDto(
    string Folio, int Emision, string Estado, DateTimeOffset EmitidoEn, DateOnly? VigenteHasta, string Huella, string ExpedienteFolio,
    bool PdfDisponible);

/// <summary>
/// Certificado del vínculo: <see cref="Actual"/> es la emisión más reciente (en el estado que tenga; nula si el vínculo
/// nunca se aprobó) y <see cref="Emisiones"/> el historial, de la más reciente a la primera.
/// </summary>
public sealed record CertificadoDelVinculoDto(Guid VinculoId, CertificadoDto? Actual, IReadOnlyList<EmisionDelCertificadoDto> Emisiones);

public sealed record RevocarCertificadoDto(string Justificacion);

public sealed record EmisorPublicoDto(string Empresa, string? LogoUrl);

public sealed record TitularPublicoDto(string Nombre, string IdentificacionEnmascarada);

/// <summary>
/// Verificación pública (P-8-1): sin documentos, contactos ni revisores. <see cref="ContenidoCanonico"/> permite recalcular
/// el SHA-256 en el cliente (RFC 8785 + SHA-256 = <see cref="HashContenido"/>) y verificar <see cref="Firma"/> con la clave
/// pública de <c>/.well-known/validasoft-certificados.json</c>. <see cref="ReemplazadoPor"/>: folio de la emisión actual si
/// esta fue reemplazada. <see cref="PdfDescargable"/>: el PDF se entrega solo de una emisión <c>Vigente</c>.
/// </summary>
public sealed record CertificadoPublicoDto(
    string Folio,
    int Emision,
    string Estado,
    bool Bloqueante,
    TitularPublicoDto Titular,
    EmisorPublicoDto Emisor,
    FlujoDelCertificadoDto Flujo,
    DateTimeOffset EmitidoEn,
    DateOnly? VigenteHasta,
    string Huella,
    string HashContenido,
    JsonElement ContenidoCanonico,
    string UrlVerificacion,
    IReadOnlyList<string> Pendientes,
    FirmaDelCertificadoDto Firma,
    DateTimeOffset EstadoDesde,
    string? ReemplazadoPor,
    bool PdfDescargable,
    bool EnVerificacion = false);

/// <summary>Clave pública Ed25519 en bruto (base64, 32 bytes) y como JWK (RFC 8037) para WebCrypto.</summary>
public sealed record ClavePublicaDeCertificadosDto(string Kid, string Algoritmo, string ClavePublica, JwkDto Jwk, bool Vigente);

public sealed record JwkDto(string Kty, string Crv, string X, string Kid, string Alg, string Use);

/// <summary>
/// <c>GET /.well-known/validasoft-certificados.json</c>: las claves con que se sellan los certificados (la vigente y las
/// anteriores, para verificar emisiones viejas tras una rotación) y cómo se verifica.
/// </summary>
public sealed record ClavesDeCertificadosDto(
    string Emisor, string Algoritmo, string MensajeFirmado, IReadOnlyList<string> Esquemas, IReadOnlyList<ClavePublicaDeCertificadosDto> Claves);

/// <summary>
/// Fila de la lista de certificados de la empresa (CE-11): la emisión actual de cada vínculo con su estado, si bloquea
/// (suspendido, vencido, revocado o con pendientes ya bloqueantes) y su vigencia.
/// </summary>
public sealed record FilaCertificadoDto(Guid Id, string Folio, int Emision, Guid VinculoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo,
    string Estado, bool Bloqueante, DateTimeOffset EmitidoEn, DateOnly? VigenteHasta, DateTimeOffset EstadoDesde, bool EnVerificacion = false);
