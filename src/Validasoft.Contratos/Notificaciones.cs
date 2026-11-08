using System.Text.Json;

namespace Validasoft.Contratos;

// Eventos públicos, webhooks y notificaciones (docs/05 §6–§8, NW-1…NW-12).

// ---------- Eventos públicos (sobre v1) ----------

/// <summary>
/// Sobre común de un evento público (docs/05 §6.2): el mismo JSON en el cuerpo del webhook y en <c>GET /eventos</c>.
/// <see cref="Cursor"/>: posición del evento en el tenant (monotónica; se pasa en <c>?desde=</c>). <see cref="Datos"/>
/// depende del tipo (<c>Datos*Dto</c> de abajo); nunca lleva revisores, contactos ni URLs de binarios.
/// </summary>
public sealed record EventoPublicoDto(
    Guid Id, string Tipo, int Version, DateTimeOffset OcurridoEn, Guid TenantId, Guid? EmpresaId, string Cursor, JsonElement Datos);

/// <summary>Página del modo pull: <see cref="Cursor"/> es desde dónde seguir (el último devuelto, o el mismo <c>desde</c> si no hubo nada).</summary>
public sealed record PaginaEventosDto(IReadOnlyList<EventoPublicoDto> Datos, string Cursor, bool HayMas);

/// <summary>Tipo de evento público del catálogo v1 (<c>GET /eventos/tipos</c>).</summary>
public sealed record TipoEventoPublicoDto(string Tipo, string Descripcion);

/// <summary>Requisito (documento pedido) mencionado en un evento de expediente: id y código del tipo.</summary>
public sealed record RequisitoEnEventoDto(Guid RequisitoId, string Codigo);

/// <summary>Motivo tipificado tal como se comunicó al tercero.</summary>
public sealed record MotivoEnEventoDto(string Codigo, string Texto);

/// <summary>Texto legal aceptado (tipo y versión).</summary>
public sealed record ConsentimientoEnEventoDto(string Tipo, int Version);

/// <summary><c>vinculo.creado</c>. <see cref="Referencias"/>: una por sistema integrado que comparte el vínculo (I-0).</summary>
public sealed record DatosVinculoCreadoDto(
    Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, Guid TerceroId, IdentificacionDto? Identificacion, string TipoVinculo,
    Guid? FlujoId, string Origen, string Estado);

/// <summary><c>vinculo.invitacion_enviada</c> (sin el correo de destino).</summary>
public sealed record DatosInvitacionEnviadaDto(Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, string Canal, DateTimeOffset Expira);

/// <summary><c>vinculo.invitacion_aceptada</c>: textos aceptados (tipo y versión), sin la evidencia (IP, correo).</summary>
public sealed record DatosInvitacionAceptadaDto(
    Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, DateTimeOffset AceptadaEn, IReadOnlyList<ConsentimientoEnEventoDto> Consentimientos);

/// <summary>
/// <c>vinculo.activado</c> · <c>vinculo.bloqueado</c> · <c>vinculo.desbloqueado</c> · <c>vinculo.desvinculado</c> ·
/// <c>vinculo.anulado</c>: estado resultante y el anterior (si se conoce), motivo o justificación, fin del bloqueo y el
/// expediente que activó el vínculo.
/// </summary>
public sealed record DatosEstadoVinculoDto(
    Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, string Estado, string? EstadoAnterior, string? Motivo,
    DateTimeOffset? Hasta, Guid? ExpedienteId);

/// <summary><c>vinculo.referencia_agregada</c>: otro sistema comparte el vínculo.</summary>
public sealed record DatosReferenciaAgregadaDto(Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, ReferenciaExternaDto Referencia);

/// <summary>
/// <c>expediente.abierto</c> · <c>expediente.en_revision</c> · <c>expediente.observado</c> · <c>expediente.reenviado</c> ·
/// <c>expediente.aprobado</c> · <c>expediente.rechazado</c>. <see cref="Estado"/> es el del expediente tras el evento.
/// <see cref="Causa"/> (en_revision): <c>CargaCompleta</c> o <c>RevisionReanudada</c>; (reenviado): <c>Tercero</c> o
/// <c>Integrador</c>. <see cref="Requisitos"/>: observados (observado) o reabiertos (reenviado).
/// </summary>
public sealed record DatosExpedienteDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, string TipoExpediente, string Estado,
    string? Causa = null, IReadOnlyList<RequisitoEnEventoDto>? Requisitos = null, IReadOnlyList<MotivoEnEventoDto>? Motivos = null,
    string? Justificacion = null, Guid? TerceroId = null, string? TipoVinculo = null);

/// <summary>
/// <c>documento.compartido</c> · <c>documento.por_vencer</c> · <c>documento.vencido</c>. <see cref="Tipo"/>: código del
/// canónico (global) o del tipo del tenant (por vínculo). <see cref="Causa"/> (compartido): <c>InvitacionAceptada</c>,
/// <c>VersionNueva</c> o <c>Importada</c>. Nunca lleva URLs ni el binario.
/// </summary>
public sealed record DatosDocumentoDto(
    Guid DocumentoId, Guid TerceroId, string Tipo, int Version, Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias,
    string? Causa = null, DateOnly? VigenteHasta = null, int? DiasRestantes = null, int? DiasGracia = null);

/// <summary>Emisión del certificado en un evento público: folio con emisión, estado tras el evento, SHA-256 (hex) y URL pública.</summary>
public sealed record CertificadoEnEventoDto(string Folio, int Emision, string Estado, string Hash, DateOnly? VigenteHasta, string UrlVerificacion);

/// <summary>
/// <c>certificado.emitido</c> · <c>certificado.reemplazado</c> · <c>certificado.suspendido</c> · <c>certificado.restablecido</c> ·
/// <c>certificado.revocado</c> · <c>certificado.vencido</c> (NW-13). <see cref="Certificado"/> es la emisión a la que se
/// refiere el evento (en <c>reemplazado</c>, la anterior; la nueva va en <see cref="FolioReemplazo"/>). <see cref="Motivo"/>
/// en suspendido, restablecido y revocado. Nunca lleva el contenido canónico, el PDF ni quién revocó.
/// </summary>
public sealed record DatosCertificadoDto(
    Guid CertificadoId, Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, Guid TerceroId, CertificadoEnEventoDto Certificado,
    string? EstadoAnterior = null, string? Motivo = null, Guid? ExpedienteId = null, string? ExpedienteFolio = null, string? FolioReemplazo = null);

// ---------- Webhooks ----------

/// <summary>
/// <see cref="Tipos"/>: tipos del catálogo o <c>["*"]</c>. <see cref="Empresas"/>: ids o <c>["*"]</c>/nulo (todas las del
/// alcance de la credencial). <see cref="SistemaId"/>: obligatorio solo con una llave que no es de un sistema integrado.
/// </summary>
public sealed record CrearWebhookDto(
    string Url, IReadOnlyList<string> Tipos, IReadOnlyList<string>? Empresas = null, Guid? SistemaId = null, string? Descripcion = null);

/// <summary>Campos nulos = sin cambio.</summary>
public sealed record EditarWebhookDto(string? Url = null, IReadOnlyList<string>? Tipos = null, IReadOnlyList<string>? Empresas = null,
    string? Descripcion = null);

/// <summary>
/// Suscripción. <see cref="Secreto"/> solo viene al crear y al rotar (se muestra una sola vez); con
/// <see cref="SecretoAnteriorVence"/> el anterior sigue firmando hasta esa fecha.
/// </summary>
public sealed record WebhookDto(
    Guid Id, Guid SistemaId, string Sistema, string Url, string? Descripcion, IReadOnlyList<string> Tipos, IReadOnlyList<string> Empresas,
    int Version, bool Activa, int FallosSeguidos, DateTimeOffset? DeshabilitadaEn, string? CausaDeshabilitacion, DateTimeOffset CreadaEn,
    DateTimeOffset? UltimaEntregaEn, DateTimeOffset? SecretoAnteriorVence, string? Secreto = null);

/// <summary>Un intento: <see cref="Resultado"/> <c>Exitoso</c> o <c>Fallido</c>; <see cref="Error"/> sin datos sensibles (timeout, conexión rechazada…).</summary>
public sealed record IntentoEntregaDto(int Numero, Guid IntentoId, DateTimeOffset Fecha, int? CodigoHttp, long DuracionMs, string Resultado,
    string? Error, bool Manual);

/// <summary>Estado: <c>Pendiente</c>, <c>Entregada</c>, <c>Reintentando</c>, <c>Fallida</c> o <c>Cancelada</c>.</summary>
public sealed record EntregaWebhookDto(
    Guid Id, Guid WebhookId, Guid EventoId, string TipoEvento, string Cursor, Guid? EmpresaId, string Estado, int Intentos,
    DateTimeOffset CreadaEn, DateTimeOffset? EntregadaEn, DateTimeOffset? ProximoIntentoEn, IReadOnlyList<IntentoEntregaDto> Historial);

// ---------- Notificaciones a personas (campana y preferencias) ----------

/// <summary>
/// Notificación de la campana. <see cref="Tipo"/>: <c>expediente.nuevo_en_area</c>, <c>expediente.tercero_respondio</c>,
/// <c>expediente.aprobado</c>, <c>expediente.rechazado</c>, <c>documento.por_vencer</c> o <c>webhook.deshabilitado</c>.
/// <see cref="Enlace"/>: ruta de <c>apps/web</c> (<c>/e/{empresa}/expedientes/{id}</c>…), sin host.
/// </summary>
public sealed record NotificacionDto(
    Guid Id, string Tipo, string Titulo, string Mensaje, Guid? EmpresaId, Guid? ExpedienteId, Guid? VinculoId, string? Folio, string? Enlace,
    DateTimeOffset CreadaEn, DateTimeOffset? LeidaEn);

/// <summary>Bandeja de la campana: página (más recientes primero) y cuántas no leídas tiene la persona en total.</summary>
public sealed record BandejaNotificacionesDto(IReadOnlyList<NotificacionDto> Datos, string? SiguienteCursor, int NoLeidas);

public sealed record NotificacionesMarcadasDto(int Marcadas);

/// <summary>
/// Preferencias de la persona: <see cref="Correo"/> por tipo de notificación (<c>true</c> = también por correo). Los tipos
/// que no aparecen usan su valor por defecto (<see cref="TiposConCorreo"/> lista los que pueden ir por correo).
/// </summary>
public sealed record PreferenciasNotificacionDto(IReadOnlyDictionary<string, bool> Correo, IReadOnlyList<string> TiposConCorreo);

public sealed record ActualizarPreferenciasNotificacionDto(IReadOnlyDictionary<string, bool> Correo);
