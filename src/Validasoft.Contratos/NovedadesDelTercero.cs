namespace Validasoft.Contratos;

// Campana del tercero y recordatorio de observaciones pendientes (NT-1…NT-4, docs/05 §8.5).

// ---------- Campana del tercero (/public/v1/notificaciones) ----------

/// <summary>
/// Notificación de la campana del tercero. <see cref="Tipo"/>: <c>tercero.observado</c>, <c>tercero.recordatorio</c>,
/// <c>tercero.actualizacion</c>, <c>tercero.resultado</c>, <c>tercero.documento_por_vencer</c>, <c>tercero.documento_vencido</c>,
/// <c>tercero.certificado</c>, <c>tercero.plazo_de_cargue</c>, <c>tercero.propuesta</c> o <c>tercero.invitacion</c>.
/// <see cref="Empresa"/>: la empresa que la origina, con su marca (nula en lo que no es de ninguna empresa, como una propuesta
/// desde un documento). <see cref="Enlace"/>: ruta de <c>apps/public</c> (<c>/expediente/{id}</c>, <c>/boveda</c>, <c>/ficha</c>…), sin host.
/// </summary>
public sealed record NotificacionTerceroDto(
    Guid Id, string Tipo, string Titulo, string Mensaje, EmpresaPublicaDto? Empresa, Guid? ExpedienteId, Guid? VinculoId, string? Folio, string? Enlace,
    DateTimeOffset CreadaEn, DateTimeOffset? LeidaEn);

/// <summary>Bandeja de la campana del tercero: página (más recientes primero) y cuántas no leídas tiene en total, de todas sus empresas.</summary>
public sealed record BandejaNotificacionesTerceroDto(IReadOnlyList<NotificacionTerceroDto> Datos, string? SiguienteCursor, int NoLeidas);

// ---------- Recordatorio de observaciones pendientes (NT-2) ----------

/// <summary>
/// Recordatorio masivo: <see cref="Expedientes"/> (hasta 200) o, con <see cref="Todos"/>, todos los expedientes de la empresa que
/// están devueltos al tercero (hasta 500), opcionalmente solo aquellos por los que pasa el <see cref="Area"/> (los que ve en su bandeja). Lo que no se puede recordar
/// (no está observado, ya se recordó hoy, sin correo) se omite y se informa por expediente.
/// </summary>
public sealed record RecordarObservacionesDto(IReadOnlyList<Guid>? Expedientes = null, bool Todos = false, Guid? Area = null);

/// <summary>Resultado del recordatorio de un expediente: <c>Enviado</c> o el motivo de la omisión (<c>NoObservado</c>, <c>YaRecordadoHoy</c>, <c>SinCorreo</c>, <c>NoEncontrado</c>).</summary>
public sealed record ResultadoRecordatorioDto(Guid ExpedienteId, string? Folio, string Resultado);

/// <summary><see cref="Enviados"/> y <see cref="Omitidos"/> suman los expedientes revisados; <see cref="Detalle"/> trae uno por cada uno.</summary>
public sealed record RecordatoriosEnviadosDto(int Enviados, int Omitidos, IReadOnlyList<ResultadoRecordatorioDto> Detalle);

/// <summary>Último recordatorio que recibió el tercero por un expediente observado y cuántos van; nulo si nunca se le ha recordado.</summary>
public sealed record RecordatorioObservacionesDto(DateTimeOffset UltimoEn, int Total);

// ---------- Recordatorio automático (NT-3) ----------

/// <summary>
/// Recordatorio automático de la empresa: con <see cref="Activo"/>, cada <see cref="CadaDias"/> días (1 a 60) mientras el expediente
/// siga devuelto al tercero, a lo sumo uno por día. Apagado por omisión.
/// </summary>
public sealed record RecordatorioAutomaticoDto(bool Activo, int CadaDias, DateTimeOffset? ActualizadoEn = null);

public sealed record ActualizarRecordatorioAutomaticoDto(bool Activo, int CadaDias);

/// <summary>Corre ya el barrido del recordatorio automático (Plataforma); <see cref="Fecha"/> simula «ahora» (solo en Development).</summary>
public sealed record RevisarRecordatoriosDto(Guid? TenantId = null, DateTimeOffset? Fecha = null);

public sealed record ResultadoRevisionRecordatoriosDto(int Revisados, int Enviados);
