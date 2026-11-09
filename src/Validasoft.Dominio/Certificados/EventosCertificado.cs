namespace Validasoft.Dominio.Certificados;

/// <summary>Estado del certificado (docs/03 §3.1). <c>Suspendido</c> por bloqueo del vínculo (B-5); <c>ConPendientes</c> llega con H6.</summary>
public enum EstadoCertificado
{
    Vigente,
    ConPendientes,
    Suspendido,
    Vencido,
    Revocado,
    Reemplazado
}

/// <summary>Quién revoca: un administrador (decisión humana con justificación) o el sistema al anular o desvincular el vínculo.</summary>
public enum OrigenRevocacion
{
    Administrador,
    Sistema
}

/// <summary>
/// Sello de la plataforma (docs/03 §7.1, P-8-3): firma Ed25519 con la clave vigente, identificada por <see cref="Kid"/>
/// (la clave pública se publica en <c>/.well-known/validasoft-certificados.json</c>). La implementación vive fuera del
/// dominio (la criptografía es de infraestructura); el dominio solo decide qué se firma: los 32 bytes del SHA-256.
/// </summary>
public interface ISelladorDeCertificados
{
    string Kid { get; }

    byte[] Firmar(byte[] mensaje);
}

// Eventos del stream Certificado-{id} (uno por vínculo). Actor y correlación viajan en los metadatos (docs/03 §3).

/// <summary>
/// Emisión <c>-{n}</c> del certificado del vínculo: el contenido canónico (RFC 8785) tal como se hasheó, su SHA-256, el
/// sello Ed25519 (base64) de esos 32 bytes con la clave <see cref="Kid"/> y la URL pública de verificación.
/// </summary>
public sealed record CertificadoEmitido(
    Guid CertificadoId,
    Guid TenantId,
    Guid EmpresaId,
    Guid VinculoId,
    Guid TerceroId,
    Guid ExpedienteId,
    string ExpedienteFolio,
    string FolioRaiz,
    int Emision,
    string Folio,
    string ContenidoCanonico,
    string HashContenido,
    string Sello,
    string Kid,
    DateOnly? VigenteHasta,
    string UrlVerificacion,
    DateTimeOffset Fecha);

/// <summary>La emisión anterior queda <c>Reemplazado</c> (sigue verificable) porque se emitió <see cref="EmisionNueva"/>.</summary>
public sealed record CertificadoReemplazado(int EmisionAnterior, int EmisionNueva, string Motivo, DateTimeOffset Fecha);

/// <summary>El vínculo se bloqueó (B-5): no es revocación; al desbloquear se restablece si sigue vigente.</summary>
public sealed record CertificadoSuspendido(int Emision, string Motivo, EstadoCertificado EstadoPrevio, DateTimeOffset Fecha);

/// <summary>
/// Una versión nueva de un documento aceptado está en revalidación (RV-3): el certificado sigue siendo válido pero informa
/// que tiene pendientes hasta que el expediente de actualización se apruebe (entonces se reemite).
/// </summary>
public sealed record CertificadoConPendientes(int Emision, string Motivo, Guid ExpedienteId, DateTimeOffset Fecha);

/// <summary>Venció el plazo de la revalidación sin resolverse (RV-4): los pendientes pasan a ser bloqueantes hasta la reemisión.</summary>
public sealed record PlazoDeRevalidacionVencido(int Emision, Guid ExpedienteId, DateTimeOffset Fecha);

/// <summary>Vuelve al estado previo a la suspensión (desbloqueo).</summary>
public sealed record CertificadoRestablecido(int Emision, EstadoCertificado EstadoRestituido, string Motivo, DateTimeOffset Fecha);

/// <summary>Irreversible: un administrador con justificación o el sistema al anular o desvincular el vínculo.</summary>
public sealed record CertificadoRevocado(int Emision, string Motivo, OrigenRevocacion Origen, string Por, DateTimeOffset Fecha);

/// <summary>Pasó su <c>VigenteHasta</c> (la menor vigencia de los documentos aceptados que tienen una).</summary>
public sealed record CertificadoVencido(int Emision, DateOnly VigenteHasta, DateTimeOffset Fecha);

/// <summary>PDF de la emisión guardado en el bucket del tenant, con su SHA-256 (se genera después del commit, CE-5).</summary>
public sealed record PdfDeCertificadoGenerado(int Emision, string Bucket, string Clave, string Sha256, long Tamano, DateTimeOffset Fecha);

/// <summary>Se entregó una URL prefirmada del PDF (auditoría de acceso, como <c>DocumentoAccedido</c> en BO-7).</summary>
public sealed record CertificadoDescargado(int Emision, string ActorTipo, string? ActorId, string Ip, string UserAgent, bool Publico,
    DateTimeOffset Fecha);
