using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Vinculos;

/// <summary>
/// Situación de la aprobación previa a invitar (decisión AI). Un vínculo sin este valor es el de siempre: su invitación salió (o saldrá) sin que nadie la apruebe.
/// No es un <see cref="EstadoVinculo"/>: el vínculo sigue <c>Invitado</c> y lo que cambia es si ya se le puede escribir al tercero.
/// </summary>
public enum EstadoAprobacionDeInvitacion
{
    /// <summary>La invitación está retenida: no salió el correo ni existe el enlace, y el tercero no ve el vínculo.</summary>
    Pendiente,

    /// <summary>Otra persona la aprobó y la invitación salió como cualquier otra.</summary>
    Aprobada,

    /// <summary>Otra persona la rechazó, con su motivo: el vínculo quedó <c>Anulado</c>.</summary>
    Rechazada
}

/// <summary>Quién pidió o decidió una invitación: el tipo de actor y su id (para una persona, su id de usuario).</summary>
public sealed record ActorDeInvitacion(ActorTipo Tipo, string Id);

/// <summary>
/// La invitación quedó retenida: el tipo de vínculo exige que otra persona la apruebe antes de escribirle al tercero. <see cref="CorreoDestino"/> y
/// <see cref="VigenciaHoras"/> son los que se usarán al aprobarla (el enlace nace en ese momento, con esa vigencia).
/// </summary>
public sealed record InvitacionPorAprobarSolicitada(string CorreoDestino, int VigenciaHoras, ActorDeInvitacion SolicitadaPor, DateTimeOffset Fecha);

/// <summary>Una persona distinta de quien la pidió aprobó la invitación; en la misma transacción sale la <see cref="InvitacionEnviada"/> normal.</summary>
public sealed record InvitacionAprobada(ActorDeInvitacion AprobadaPor, ActorDeInvitacion SolicitadaPor, string CorreoDestino, DateTimeOffset Fecha);

/// <summary>Una persona distinta de quien la pidió rechazó la invitación, con su motivo; en la misma transacción el vínculo queda <see cref="VinculoAnulado"/>.</summary>
public sealed record InvitacionRechazada(ActorDeInvitacion RechazadaPor, ActorDeInvitacion SolicitadaPor, string Motivo, DateTimeOffset Fecha);

/// <summary>
/// Aprobación antes de invitar (decisión AI, equivale al estado -5 del SRM): con el tipo de vínculo en <c>RequiereAprobacionParaInvitar</c> la invitación no sale al
/// crear el vínculo (ni al reinvitar a otro correo): queda retenida como un sub-estado del vínculo —que sigue <c>Invitado</c>— hasta que otra persona la apruebe
/// (sale la invitación de siempre) o la rechace con un motivo (el vínculo queda <c>Anulado</c>). Mientras está retenida no hay correo, ni enlace, ni aviso a los
/// contactos del tercero, ni expediente, y el tercero no ve el vínculo.
/// </summary>
public sealed partial class Vinculo
{
    /// <summary>Los vínculos con la invitación retenida (el que filtra la bandeja y el que usa la propiedad: una sola definición).</summary>
    public static readonly Expression<Func<Vinculo, bool>> EsInvitacionPorAprobar = v =>
        v.Estado == EstadoVinculo.Invitado && v.AprobacionDeInvitacion == EstadoAprobacionDeInvitacion.Pendiente;

    /// <summary>Nulo si no pasó por aprobación; si no, en qué va.</summary>
    public EstadoAprobacionDeInvitacion? AprobacionDeInvitacion { get; set; }

    /// <summary>Quién pidió la invitación (la persona o la integración que creó el vínculo o la reenvió a otro correo).</summary>
    public ActorDeInvitacion? InvitacionSolicitadaPor { get; set; }

    public DateTimeOffset? InvitacionSolicitadaEn { get; set; }

    /// <summary>Correo al que irá la invitación al aprobarla.</summary>
    public string? InvitacionPendienteCorreo { get; set; }

    /// <summary>Vigencia del enlace que se emite al aprobarla.</summary>
    public int InvitacionPendienteVigenciaHoras { get; set; }

    public ActorDeInvitacion? InvitacionDecididaPor { get; set; }

    public DateTimeOffset? InvitacionDecididaEn { get; set; }

    public string? InvitacionRechazadaMotivo { get; set; }

    /// <summary>¿La invitación espera la aprobación de otra persona? (sin importar el estado: una vinculación bloqueada la sigue esperando).</summary>
    [JsonIgnore]
    public bool InvitacionPorAprobar => AprobacionDeInvitacion == EstadoAprobacionDeInvitacion.Pendiente;

    /// <summary>
    /// ¿Esta invitación necesita aprobación? Sí si el tipo la exige y no hay ya una invitación enviada a ese mismo correo (la aprobación vale para el correo que se
    /// aprobó: reenviarle al mismo correo no la pide de nuevo; invitar a otro, sí).
    /// </summary>
    public bool NecesitaAprobacionParaInvitar(bool tipoLaExige, string? correoDestino)
    {
        if (!tipoLaExige)
            return false;
        var correo = string.IsNullOrWhiteSpace(correoDestino) ? CorreoTercero : correoDestino.Trim().ToLowerInvariant();
        return Invitacion is null || !string.Equals(Invitacion.Correo, correo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Retiene la invitación en lugar de enviarla. Las mismas reglas que <see cref="EnviarInvitacion"/> (solo <c>Invitado</c>, sin aceptar, vigencia y correo válidos);
    /// 409 <c>invitacion-por-aprobar</c> si ya espera la aprobación de una.
    /// </summary>
    public InvitacionPorAprobarSolicitada SolicitarAprobacionDeInvitacion(string? correoDestino, TimeSpan vigencia, ActorDeInvitacion solicitante, DateTimeOffset ahora)
    {
        AsegurarSinInvitacionRetenida();
        var correo = ComprobarInvitable(correoDestino, vigencia);
        return new InvitacionPorAprobarSolicitada(correo, (int)Math.Round(vigencia.TotalHours), solicitante, ahora);
    }

    /// <summary>
    /// Otra persona aprueba la invitación retenida: devuelve la aprobación y la <see cref="InvitacionEnviada"/> que sale a continuación (al correo y con la vigencia
    /// pedidos). Quien aprueba no puede ser quien la pidió (403 <c>separacion-de-funciones</c>) ni tener la identificación del tercero (403
    /// <c>conflicto-de-intereses</c>, la misma regla de los veredictos); 409 <c>sin-invitacion-por-aprobar</c> si no hay nada retenido y <c>estado-invalido</c> si el
    /// vínculo ya no está <c>Invitado</c> (p. ej. bloqueado).
    /// </summary>
    public (InvitacionAprobada Aprobada, InvitacionEnviada Enviada) AprobarInvitacion(Revisor aprobador, string terceroIdentificacion, Guid? loteId, DateTimeOffset ahora)
    {
        AsegurarPendienteDeDecision();
        AsegurarDecisorHabilitado(aprobador, terceroIdentificacion);

        var correo = InvitacionPendienteCorreo ?? CorreoTercero;
        var solicitante = InvitacionSolicitadaPor ?? new ActorDeInvitacion(ActorTipo.Sistema, "sistema");
        return (new InvitacionAprobada(Decisor(aprobador), solicitante, correo, ahora),
            new InvitacionEnviada("Correo", correo, ahora.AddHours(Math.Max(1, InvitacionPendienteVigenciaHoras)), loteId, ahora));
    }

    /// <summary>
    /// Otra persona rechaza la invitación retenida, con motivo (422 <c>motivo-requerido</c>): el vínculo queda <c>Anulado</c> con ese motivo. Las mismas reglas de
    /// quién decide que <see cref="AprobarInvitacion"/>.
    /// </summary>
    public (InvitacionRechazada Rechazada, VinculoAnulado Anulado) RechazarInvitacion(Revisor rechazador, string terceroIdentificacion, string motivo, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Rechazar la invitación necesita un motivo.");
        AsegurarPendienteDeDecision();
        AsegurarDecisorHabilitado(rechazador, terceroIdentificacion);

        var solicitante = InvitacionSolicitadaPor ?? new ActorDeInvitacion(ActorTipo.Sistema, "sistema");
        return (new InvitacionRechazada(Decisor(rechazador), solicitante, motivo.Trim(), ahora), new VinculoAnulado(Estado, motivo.Trim(), ahora));
    }

    public void Apply(InvitacionPorAprobarSolicitada e)
    {
        AprobacionDeInvitacion = EstadoAprobacionDeInvitacion.Pendiente;
        InvitacionSolicitadaPor = e.SolicitadaPor;
        InvitacionSolicitadaEn = e.Fecha;
        InvitacionPendienteCorreo = e.CorreoDestino;
        InvitacionPendienteVigenciaHoras = e.VigenciaHoras;
        InvitacionDecididaPor = null;
        InvitacionDecididaEn = null;
        InvitacionRechazadaMotivo = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionAprobada e)
    {
        AprobacionDeInvitacion = EstadoAprobacionDeInvitacion.Aprobada;
        InvitacionDecididaPor = e.AprobadaPor;
        InvitacionDecididaEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionRechazada e)
    {
        AprobacionDeInvitacion = EstadoAprobacionDeInvitacion.Rechazada;
        InvitacionDecididaPor = e.RechazadaPor;
        InvitacionDecididaEn = e.Fecha;
        InvitacionRechazadaMotivo = e.Motivo;
        ActualizadoEn = e.Fecha;
    }

    private void AsegurarSinInvitacionRetenida()
    {
        if (InvitacionPorAprobar)
            throw ReglaDeNegocioException.Conflicto("invitacion-por-aprobar",
                "La invitación de este vínculo espera la aprobación de otra persona: nadie puede enviarla hasta que la apruebe o la rechace.");
    }

    private void AsegurarPendienteDeDecision()
    {
        if (!InvitacionPorAprobar)
            throw ReglaDeNegocioException.Conflicto("sin-invitacion-por-aprobar", "Este vínculo no tiene una invitación esperando aprobación.");
        if (Estado != EstadoVinculo.Invitado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"La invitación se decide con el vínculo Invitado; este está {Estado}.");
    }

    /// <summary>Quien decide no es quien pidió la invitación ni el propio tercero: la misma separación de funciones y conflicto de intereses de los veredictos (I-21, P-4-8).</summary>
    private void AsegurarDecisorHabilitado(Revisor decisor, string terceroIdentificacion)
    {
        if (!string.IsNullOrWhiteSpace(decisor.ClaveIdentificacion) && decisor.ClaveIdentificacion == terceroIdentificacion)
            throw ReglaDeNegocioException.Prohibido("conflicto-de-intereses",
                "Tu identificación coincide con la del tercero: no puedes decidir sobre su invitación.");

        if (InvitacionSolicitadaPor is { Tipo: ActorTipo.Usuario } solicitante &&
            string.Equals(solicitante.Id, decisor.UsuarioId.ToString(), StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Prohibido("separacion-de-funciones",
                "Quien pidió la invitación no puede aprobarla ni rechazarla: la decide otra persona.");
    }

    private static ActorDeInvitacion Decisor(Revisor decisor) => new(ActorTipo.Usuario, decisor.UsuarioId.ToString());
}
