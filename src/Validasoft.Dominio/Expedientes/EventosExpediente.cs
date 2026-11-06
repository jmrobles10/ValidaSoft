using Validasoft.Dominio.Comun;
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

/// <summary>Sin requisitos documentales (fase actual), el expediente pasa directo a revisión al abrirse.</summary>
public sealed record ExpedienteEnviadoARevision(DateTimeOffset Fecha);

/// <summary>Todas las áreas de la etapa reciben el expediente al mismo tiempo.</summary>
public sealed record EtapaActivada(int Orden, IReadOnlyList<Guid> Areas, DateTimeOffset Fecha);

/// <summary>
/// Veredicto de un área. <see cref="RequisitoId"/> nulo = sobre el expediente completo (fase sin documentos, FL-3);
/// cuando existan requisitos, el veredicto de área se derivará de los de sus requisitos sin reinterpretar estos eventos.
/// <see cref="RevisorId"/>: usuario (humano), administrador que aprobó en nombre del área, o nulo (sistema).
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
    DateTimeOffset Fecha);

/// <summary>El expediente vuelve al tercero; mientras tanto ninguna área emite veredictos.</summary>
public sealed record ExpedienteObservado(IReadOnlyList<Guid> AreasQueObservaron, DateTimeOffset Fecha);

/// <summary>Respuesta a la observación: las áreas que observaron vuelven a quedar pendientes; las demás conservan lo suyo.</summary>
public sealed record ExpedienteReenviado(OrigenReenvio Origen, string? Respuesta, IReadOnlyList<Guid> AreasReabiertas, DateTimeOffset Fecha);

/// <summary>
/// Un administrador anuló la última observación vigente: el expediente deja de estar devuelto al tercero y la revisión
/// sigue sin esperar un reenvío (FL-13). El área cuya observación se anuló vuelve a quedar pendiente.
/// </summary>
public sealed record RevisionReanudada(Guid VeredictoAnuladoId, DateTimeOffset Fecha);

public sealed record EtapaAprobada(int Orden, DateTimeOffset Fecha);

public sealed record ExpedienteAprobado(DateTimeOffset Fecha);

/// <summary>Rechazo terminal (B-7, P-5-5): solo un área con <c>PuedeRechazarExpediente</c>, con motivos y justificación.</summary>
public sealed record ExpedienteRechazado(Guid AreaId, string RevisorId, IReadOnlyList<MotivoAplicado> Motivos, string Justificacion,
    DateTimeOffset Fecha);

public sealed record ExpedienteAnulado(EstadoExpediente EstadoPrevio, string Motivo, DateTimeOffset Fecha);

/// <summary>Un administrador deja sin efecto un veredicto vigente; el área recalcula su estado.</summary>
public sealed record VeredictoAnuladoPorAdministrador(Guid VeredictoId, Guid AreaId, string AdministradorId, string Justificacion,
    DateTimeOffset Fecha);
