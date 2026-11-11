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
    DateTimeOffset Fecha,
    PlazoDeCargue? PlazoCargue = null);

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
/// cubre si el tercero ya la tenía compartida con el vínculo. Un requisito de <see cref="TipoDeRequisito.Seccion"/> (F2)
/// pide una sección de la ficha (<see cref="Seccion"/>, <see cref="Periodo"/>): la versión es la de la sección
/// (<c>DocumentoId</c> = id del stream, <c>Sha256</c> = huella) y solo la cubre si es completa para la empresa
/// (<see cref="Completitud"/>, que se guarda); <see cref="Aceptada"/> es la que la empresa ya había aceptado en el vínculo (base
/// de la comparación anterior → nuevo de una actualización).
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
    VersionDeRequisito? Version,
    AprobacionPrevia? Previa = null,
    TipoDeRequisito TipoRequisito = TipoDeRequisito.Documento,
    string? Seccion = null,
    int? Periodo = null,
    bool ExigeSoporte = false,
    VersionDeRequisito? Aceptada = null,
    CompletitudParaEmpresa? Completitud = null);

/// <summary>
/// La misma empresa ya aprobó esa misma versión del documento en otro expediente del mismo tercero (otro tipo de vínculo,
/// RQ-11): <see cref="DocumentoId"/> y <see cref="Version"/> la identifican; el expediente la aprueba solo mientras el
/// requisito siga en esa versión.
/// </summary>
public sealed record AprobacionPrevia(Guid ExpedienteId, string Folio, string TipoVinculo, Guid DocumentoId, int Version);

/// <summary>Los requisitos de la versión del flujo, congelados en el expediente (docs/03 §6.3). Un flujo sin requisitos no lo emite.</summary>
public sealed record RequisitosCalculados(IReadOnlyList<RequisitoCalculado> Requisitos, DateTimeOffset Fecha);

/// <summary>
/// Una actualización abierta (RV-1) amplía su alcance: otro requisito del tercero cambió después de abrirse —una versión nueva
/// de una sección que la empresa ya había aceptado o de un documento que ya había aprobado— y entra al mismo expediente, en vez
/// de esperar a que se cierre, con la versión que lo cubre (<see cref="RequisitoCalculado"/>, calculado igual que al abrir). Las
/// áreas que lo revisan vuelven a quedar pendientes: si su etapa ya se había aprobado, se reabre. Solo en expedientes de
/// <c>Actualizacion</c>; idempotente (un requisito que el expediente ya tiene no se agrega dos veces).
/// </summary>
public sealed record RequisitosAgregados(IReadOnlyList<RequisitoCalculado> Requisitos, DateTimeOffset Fecha);

/// <summary>Una versión actual compartida con el vínculo pasa a cubrir el requisito (la anterior queda en su historial).</summary>
public sealed record VersionAdjuntadaARequisito(Guid RequisitoId, VersionDeRequisito Version, int? NumeroAnterior, DateTimeOffset Fecha);

/// <summary>Los veredictos vigentes del requisito eran sobre la versión anterior: dejan de contar (solo ese requisito).</summary>
public sealed record VeredictosInvalidadosPorNuevaVersion(Guid RequisitoId, int VersionAnterior, IReadOnlyList<Guid> AreasAfectadas,
    DateTimeOffset Fecha);

/// <summary>
/// Veredicto de un área sobre un requisito y una versión concreta (<see cref="SobreVersion"/>). Origen <c>Humano</c>
/// (miembro del área), <c>Sistema</c> (área automática) o <c>Administrador</c> (aprobación en nombre del área). Al observar una
/// sección de la ficha, <see cref="Campos"/> señala (opcional) los campos que el tercero debe corregir (rutas snake_case).
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
    DateTimeOffset Fecha,
    IReadOnlyList<string>? Campos = null);

/// <summary>
/// Completitud para la empresa de la versión vigente de la sección que pide el requisito (FM-16, F2), guardada cada vez que
/// cambia: el motivo legible de lo que falta («Falta el soporte de la información general») no se deduce en pantalla.
/// </summary>
public sealed record CompletitudDeSeccionEvaluada(Guid RequisitoId, CompletitudParaEmpresa Completitud, DateTimeOffset Fecha);

/// <summary>
/// La empresa aceptó, al aprobarse el expediente, la versión de la sección que cubre el requisito (FM-3, FM-29). Es el punto
/// de dominio del que F3 sacará el evento público <c>seccion.aceptada</c>; hoy alimenta la aceptación por vínculo
/// (<c>SeccionAceptada</c>).
/// </summary>
public sealed record SeccionAceptadaPorEmpresa(Guid RequisitoId, string Seccion, int? Periodo, Guid SeccionId, int Version, string Huella,
    DateTimeOffset Fecha);

/// <summary>Un administrador exime el requisito con justificación (visible en el certificado, P-5-4); sus veredictos dejan de contar.</summary>
public sealed record RequisitoEximido(Guid RequisitoId, string Por, string Justificacion, DateTimeOffset Fecha);

/// <summary>
/// El tercero ratificó en una renovación que el documento no cambió (RN-2, I-14): el requisito queda cubierto con la
/// versión actual y sus áreas lo aprueban sin revisarlo (A-7), con la ratificación como referencia.
/// </summary>
public sealed record RequisitoRatificado(Guid RequisitoId, VersionDeRequisito Version, string Por, DateTimeOffset Fecha);

/// <summary>Ratificación del tercero sobre una versión del requisito.</summary>
public sealed record RatificacionDeRequisito(VersionDeRequisito Version, string Por, DateTimeOffset Fecha);

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
/// reenvío (FL-13). El área cuya observación se anuló vuelve a quedar pendiente. También la emite una actualización devuelta
/// al tercero cuando los requisitos que se le agregan (<see cref="RequisitosAgregados"/>) reabren una etapa anterior a la que observó.
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

/// <summary>
/// Se le recordó al tercero que su plazo de cargue vence (LT-2): <see cref="DiasAntes"/> es el recordatorio del flujo que se
/// cumplió (los mayores que aún no habían salido quedan cubiertos por este).
/// </summary>
public sealed record RecordatorioDeCargueEnviado(int DiasAntes, DateTimeOffset PlazoHasta, DateTimeOffset Fecha);

/// <summary>
/// Venció el plazo de cargue con el expediente en manos del tercero (<c>Abierto</c> u <c>Observado</c>, LT-2): queda
/// <c>Caducado</c> y el vínculo vuelve a <c>Invitado</c>. La empresa lo puede reabrir.
/// </summary>
public sealed record ExpedienteCaducado(EstadoExpediente EstadoPrevio, DateTimeOffset PlazoHasta, DateTimeOffset Fecha);

/// <summary>
/// La empresa reabrió el expediente caducado (P-4-2, LT-3): vuelve al estado en que caducó con un plazo nuevo completo.
/// </summary>
public sealed record ExpedienteReabierto(EstadoExpediente EstadoRestituido, DateTimeOffset PlazoHasta, string? Justificacion, DateTimeOffset Fecha);
