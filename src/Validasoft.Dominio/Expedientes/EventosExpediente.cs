using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Estado del expediente (docs/03 §3.1). <c>Caducado</c> llega con los plazos de cargue (requisitos).</summary>
public enum EstadoExpediente
{
    Abierto,
    EnRevision,
    Observado,
    Aprobado,
    Rechazado,
    Anulado,
    Caducado
}

public enum TipoExpediente
{
    Inicial,
    Renovacion,
    Actualizacion
}

/// <summary>Sobre el expediente o un requisito solo hay Aprobado u Observado; el rechazo es del expediente (B-7).</summary>
public enum Veredicto
{
    Aprobado,
    Observado
}

public enum OrigenVeredicto
{
    Humano,
    Sistema,
    Administrador
}

public enum EstadoEtapa
{
    Esperando,
    Activa,
    Aprobada
}

/// <summary>Estado de un área dentro del expediente (derivado de sus veredictos vigentes).</summary>
public enum EstadoAreaExpediente
{
    /// <summary>Su etapa aún no se activa.</summary>
    Esperando,

    /// <summary>Etapa activa y le faltan aprobaciones.</summary>
    Pendiente,

    Aprobada,

    /// <summary>Tiene una observación vigente.</summary>
    Observada
}

/// <summary>
/// Estado de un requisito del expediente (docs/03 §3.1), derivado de su versión, sus veredictos vigentes y su exención.
/// Esta fase usa <c>Pendiente</c> (sin versión), <c>Cargado</c> (con versión, aún sin revisión o corregido esperando el
/// reenvío), <c>EnRevision</c>, <c>Observado</c>, <c>Aprobado</c> (todas sus áreas) y <c>Eximido</c>.
/// </summary>
public enum EstadoRequisito
{
    Pendiente,
    Cargado,
    EnRevision,
    Observado,
    Aprobado,
    Eximido,
    NoAplica,
    Vencido,
    Desactualizado
}

/// <summary>Cómo se eligió el flujo (docs/03 §12.1). <c>Regla</c> llega con las reglas de selección.</summary>
public enum OrigenFlujo
{
    Explicito,
    Regla,
    PorDefecto
}

/// <summary>Quién responde a una observación: el tercero desde su sesión o el sistema integrado en su nombre.</summary>
public enum OrigenReenvio
{
    Tercero,
    Integrador
}

/// <summary>Quién hizo algo en el expediente (complementa los metadatos del evento; aquí se usa para decidir).</summary>
public sealed record ActorDeExpediente(ActorTipo Tipo, string Id);

/// <summary>Motivo tipificado tal como se le comunicó al tercero (copia del catálogo en ese momento).</summary>
public sealed record MotivoAplicado(string Codigo, string TextoAlTercero);

/// <summary>Copia de un área del flujo al abrir el expediente (con su código y nombre de ese momento).</summary>
public sealed record AreaDelFlujo(Guid AreaId, string Codigo, string Nombre, int AprobadoresRequeridos, bool PuedeRechazarExpediente,
    bool Automatica, int? SlaHoras);

public sealed record EtapaDelFlujo(int Orden, IReadOnlyList<AreaDelFlujo> Areas);

// Eventos del stream Expediente-{id}. Actor, sistema y correlación viajan en los metadatos del evento (docs/03 §3).

/// <summary>
/// Apertura. Lleva una copia de lo que el expediente necesita para decidir sin leer otros agregados: identificación del
/// tercero (conflicto de intereses), quién lo abrió y quién invitó (separación de funciones) y datos para la bandeja.
/// </summary>
public sealed record ExpedienteAbierto(
    Guid ExpedienteId,
    Guid EmpresaId,
    Guid VinculoId,
    TipoExpediente Tipo,
    string Folio,
    Guid TerceroId,
    string TerceroNombre,
    string TerceroIdentificacion,
    Guid TipoVinculoId,
    string TipoVinculoCodigo,
    string TipoVinculoNombre,
    ActorDeExpediente AbiertoPor,
    IReadOnlyList<string> InvitadoPor,
    DateTimeOffset Fecha);

/// <summary>Versión de flujo elegida, copiada entera: cambiar el flujo después no altera este expediente.</summary>
public sealed record FlujoSeleccionado(
    Guid FlujoId,
    Guid FlujoVersionId,
    string Codigo,
    string Nombre,
    int Version,
    OrigenFlujo Origen,
    IReadOnlyList<EtapaDelFlujo> Etapas,
    PoliticaDevolucion PoliticaDevolucion,
    bool SeparacionDeFunciones,
    DateTimeOffset Fecha);

/// <summary>
/// Fin de la fase de carga: sin requisitos al abrirse (FL-1); con requisitos, cuando todos los obligatorios están
/// <c>Cargado</c> o <c>Eximido</c> (docs/03 §6.3).
/// </summary>
public sealed record ExpedienteEnviadoARevision(DateTimeOffset Fecha);

/// <summary>Versión de la bóveda que cubre un requisito (siempre compartida con el vínculo del expediente).</summary>
public sealed record VersionDeRequisito(Guid DocumentoId, int Numero, string Sha256, DateTimeOffset CargadaEn);

/// <summary>
/// Foto de un requisito al abrir el expediente: el tipo del tenant (con su canónico y ámbito, para casar documentos de la
/// bóveda), si es obligatorio, las áreas que lo revisan (ya resueltas: vacío en el flujo ⇒ todas) y la versión que lo
/// cubre si el tercero ya la tenía compartida con el vínculo.
/// </summary>
public sealed record RequisitoCalculado(
    Guid RequisitoId,
    Guid TipoDocumentoTenantId,
    string Codigo,
    string Nombre,
    string? TipoCanonico,
    AmbitoDocumento Ambito,
    bool Obligatorio,
    IReadOnlyList<Guid> AreasQueRevisan,
    int? AprobadoresRequeridos,
    VersionDeRequisito? Version);

/// <summary>Los requisitos de la versión del flujo, congelados en el expediente (docs/03 §6.3). Un flujo sin requisitos no lo emite.</summary>
public sealed record RequisitosCalculados(IReadOnlyList<RequisitoCalculado> Requisitos, DateTimeOffset Fecha);

/// <summary>Una versión actual compartida con el vínculo pasa a cubrir el requisito (la anterior queda en su historial).</summary>
public sealed record VersionAdjuntadaARequisito(Guid RequisitoId, VersionDeRequisito Version, int? NumeroAnterior, DateTimeOffset Fecha);

/// <summary>Los veredictos vigentes del requisito eran sobre la versión anterior: dejan de contar (solo ese requisito).</summary>
public sealed record VeredictosInvalidadosPorNuevaVersion(Guid RequisitoId, int VersionAnterior, IReadOnlyList<Guid> AreasAfectadas,
    DateTimeOffset Fecha);

/// <summary>
/// Veredicto de un área sobre un requisito y una versión concreta (<see cref="SobreVersion"/>). Origen <c>Humano</c>
/// (miembro del área), <c>Sistema</c> (área automática) o <c>Administrador</c> (aprobación en nombre del área).
/// </summary>
public sealed record VeredictoDeRequisitoEmitido(
    Guid VeredictoId,
    Guid RequisitoId,
    Guid AreaId,
    Veredicto Veredicto,
    OrigenVeredicto Origen,
    string? RevisorId,
    int SobreVersion,
    IReadOnlyList<MotivoAplicado> Motivos,
    string? Observacion,
    string? Justificacion,
    DateTimeOffset Fecha);

/// <summary>Un administrador exime el requisito con justificación (visible en el certificado, P-5-4); sus veredictos dejan de contar.</summary>
public sealed record RequisitoEximido(Guid RequisitoId, string Por, string Justificacion, DateTimeOffset Fecha);

/// <summary>Todas las áreas de la etapa reciben el expediente al mismo tiempo.</summary>
public sealed record EtapaActivada(int Orden, IReadOnlyList<Guid> Areas, DateTimeOffset Fecha);

/// <summary>
/// Veredicto de un área sobre el expediente completo (<see cref="RequisitoId"/> siempre nulo; flujos sin requisitos,
/// FL-3). <see cref="RevisorId"/>: usuario (humano), administrador que aprobó en nombre del área, o nulo (sistema). Con
/// requisitos, el sistema lo emite cuando el área aprobó todo su alcance (<see cref="DerivadoDeRequisitos"/>, RQ-5): su
/// origen dice quién aprobó los requisitos (<c>Administrador</c> si intervino, si no <c>Humano</c>, si no <c>Sistema</c>).
/// </summary>
public sealed record VeredictoDeAreaEmitido(
    Guid VeredictoId,
    Guid AreaId,
    Veredicto Veredicto,
    OrigenVeredicto Origen,
    string? RevisorId,
    IReadOnlyList<MotivoAplicado> Motivos,
    string? Observacion,
    string? Justificacion,
    Guid? RequisitoId,
    DateTimeOffset Fecha,
    bool DerivadoDeRequisitos = false);

/// <summary>El expediente vuelve al tercero; mientras tanto ninguna área emite veredictos. Con requisitos, cuáles se observaron.</summary>
public sealed record ExpedienteObservado(IReadOnlyList<Guid> AreasQueObservaron, DateTimeOffset Fecha, IReadOnlyList<Guid>? RequisitosObservados = null);

/// <summary>
/// Respuesta a la observación: las áreas que observaron vuelven a quedar pendientes; las demás conservan lo suyo. Con
/// requisitos, solo los observados vuelven a revisión (<see cref="RequisitosReabiertos"/>).
/// </summary>
public sealed record ExpedienteReenviado(OrigenReenvio Origen, string? Respuesta, IReadOnlyList<Guid> AreasReabiertas, DateTimeOffset Fecha,
    IReadOnlyList<Guid>? RequisitosReabiertos = null);

/// <summary>
/// Un administrador anuló la última observación vigente (o eximió el último requisito observado,
/// <see cref="RequisitoEximidoId"/>): el expediente deja de estar devuelto al tercero y la revisión sigue sin esperar un
/// reenvío (FL-13). El área cuya observación se anuló vuelve a quedar pendiente.
/// </summary>
public sealed record RevisionReanudada(Guid VeredictoAnuladoId, DateTimeOffset Fecha, Guid? RequisitoEximidoId = null);

public sealed record EtapaAprobada(int Orden, DateTimeOffset Fecha);

public sealed record ExpedienteAprobado(DateTimeOffset Fecha);

/// <summary>Rechazo terminal (B-7, P-5-5): solo un área con <c>PuedeRechazarExpediente</c>, con motivos y justificación.</summary>
public sealed record ExpedienteRechazado(Guid AreaId, string RevisorId, IReadOnlyList<MotivoAplicado> Motivos, string Justificacion,
    DateTimeOffset Fecha);

public sealed record ExpedienteAnulado(EstadoExpediente EstadoPrevio, string Motivo, DateTimeOffset Fecha);

/// <summary>Un administrador deja sin efecto un veredicto vigente; el área recalcula su estado.</summary>
public sealed record VeredictoAnuladoPorAdministrador(Guid VeredictoId, Guid AreaId, string AdministradorId, string Justificacion,
    DateTimeOffset Fecha);
