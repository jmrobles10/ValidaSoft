namespace Validasoft.Contratos;

// ---------- Aprobación antes de invitar (decisión AI) ----------

/// <summary>
/// Quién aprueba las invitaciones de un tipo de vínculo. <see cref="Tipo"/>: <c>Rol</c> (<see cref="Valor"/> es <c>Administrador</c>, <c>OficialCumplimiento</c> o
/// <c>Revisor</c>) o <c>Area</c> (<see cref="Valor"/> es el código de un área, que se busca en la empresa del vínculo).
/// </summary>
public sealed record AprobadoresDeInvitacionDto(string Tipo, string Valor);

/// <summary>
/// La invitación de un vínculo cuyo tipo exige aprobación (<c>VinculoDto.InvitacionPorAprobar</c>; nulo si el vínculo no pasó por aprobación).
/// <see cref="Estado"/>: <c>Pendiente</c> (retenida: no salió el correo ni existe el enlace), <c>Aprobada</c> (otra persona la aprobó y salió) o <c>Rechazada</c> (el vínculo
/// quedó <c>Anulado</c> con <see cref="MotivoDeRechazo"/>). <see cref="SolicitadaPor"/> y <see cref="DecididaPor"/> son <c>{tipoDeActor}:{id}</c>, con su nombre si es
/// una persona. <see cref="PuedesDecidir"/> (solo en el detalle y la bandeja): la persona que consulta puede aprobarla o rechazarla.
/// </summary>
public sealed record InvitacionPorAprobarDto(
    string Estado, string CorreoDestino, int VigenciaHoras, DateTimeOffset SolicitadaEn, string SolicitadaPor, string? SolicitadaPorNombre = null,
    string? DecididaPor = null, string? DecididaPorNombre = null, DateTimeOffset? DecididaEn = null, string? MotivoDeRechazo = null, bool? PuedesDecidir = null);

/// <summary>
/// Una invitación retenida en la bandeja de quienes aprueban (<c>GET /invitaciones/por-aprobar</c>). <see cref="PuedesDecidir"/>: quien consulta aprueba el tipo del
/// vínculo y no es quien la pidió.
/// </summary>
public sealed record InvitacionPorAprobarItemDto(
    Guid VinculoId, Guid TerceroId, string TerceroNombre, IdentificacionDto TerceroIdentificacion, TipoVinculoResumenDto TipoVinculo, string CorreoDestino,
    DateTimeOffset SolicitadaEn, string SolicitadaPor, string? SolicitadaPorNombre, bool PuedesDecidir);

/// <summary>Datos de <c>vinculo.invitacion_por_aprobar</c>: la invitación quedó retenida (sin el correo de destino).</summary>
public sealed record DatosInvitacionPorAprobarDto(
    Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, int VigenciaHoras, DateTimeOffset SolicitadaEn);

/// <summary>Datos de <c>vinculo.invitacion_aprobada</c>: otra persona aprobó la invitación (a continuación sale <c>vinculo.invitacion_enviada</c>).</summary>
public sealed record DatosInvitacionAprobadaDto(Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, DateTimeOffset AprobadaEn);

/// <summary>Datos de <c>vinculo.invitacion_rechazada</c>: otra persona rechazó la invitación; el vínculo queda <c>Anulado</c> (<c>vinculo.anulado</c>) con ese motivo.</summary>
public sealed record DatosInvitacionRechazadaDto(Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, string Motivo, DateTimeOffset RechazadaEn);

/// <summary>Datos de <c>invitaciones.lote_por_aprobar</c>, <c>invitaciones.lote_aprobado</c> e <c>invitaciones.lote_rechazado</c> (con su <see cref="Motivo"/>).</summary>
public sealed record DatosLoteDecididoDto(Guid LoteId, string Nombre, string Estado, TotalesLoteDto Totales, string? Motivo = null);
