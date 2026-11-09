using System.Text.Json;

namespace Validasoft.Contratos;

// ---------- Auditoría (docs/05 §2.7 y §4 RegistroAuditoriaDto; AU-1 del registro) ----------

/// <summary>Quién actuó: <c>Usuario</c>, <c>Tercero</c>, <c>Integracion</c>, <c>Sistema</c> o <c>Plataforma</c>.</summary>
public sealed record ActorAuditoriaDto(string Tipo, string? Id, string? Nombre, string? Sistema);

/// <summary>Sobre qué se actuó (p. ej. <c>Flujo</c>, <c>ApiKey</c>, <c>Documento</c>, <c>Certificado</c>).</summary>
public sealed record EntidadAuditadaDto(string Tipo, string Id, string? Nombre);

/// <summary>Un campo que cambió (ruta con puntos para lo anidado). <c>TipoCambio</c>: <c>Agregado</c>, <c>Modificado</c> o <c>Eliminado</c>.</summary>
public sealed record DiffCampoDto(string Campo, JsonElement? Antes, JsonElement? Despues, string TipoCambio);

/// <summary>
/// Un registro de la auditoría. <c>Tipo</c>: <c>Configuracion</c> (con <c>Antes</c>, <c>Despues</c> y <c>Diff</c>) o <c>Acceso</c>
/// (descarga de un documento o del PDF de un certificado, con <c>Ip</c> y <c>UserAgent</c>). <c>EmpresaId</c> nulo: el cambio es
/// de todo el grupo (usuarios, API keys, tipos de documento…).
/// </summary>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    string Tipo,
    DateTimeOffset Fecha,
    Guid? EmpresaId,
    ActorAuditoriaDto Actor,
    EntidadAuditadaDto Entidad,
    string Accion,
    JsonElement? Antes,
    JsonElement? Despues,
    IReadOnlyList<DiffCampoDto> Diff,
    string? Justificacion,
    string? Detalle,
    Guid? VinculoId,
    string? Ip,
    string? UserAgent,
    string? CorrelationId);
