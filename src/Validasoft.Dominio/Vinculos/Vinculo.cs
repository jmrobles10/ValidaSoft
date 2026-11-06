using System.Text.Json;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Vinculos;

public sealed record Bloqueo(string Motivo, DateTimeOffset Desde, DateTimeOffset? Hasta);

public sealed record InvitacionVigente(string Correo, DateTimeOffset Expira, int Envios);

/// <summary>
/// Relación <c>(Empresa, Tercero, TipoDeVinculo)</c> — agregado event-sourced de vida larga (docs/03 §5).
/// Los métodos de decisión validan las reglas y devuelven el evento; los <c>Apply</c> solo evolucionan el
/// estado y los usa Marten para reconstruirlo. Esta primera entrega cubre creación, invitación, aceptación,
/// atributos, bloqueo y ciclo de terminación; las transiciones que dependen de expedientes
/// (<c>EnValidacion</c>, <c>Activo</c>, vencimientos) llegan con H4/H6.
/// </summary>
public sealed class Vinculo
{
    private static readonly EstadoVinculo[] Terminales = [EstadoVinculo.Anulado, EstadoVinculo.Desvinculado];

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TerceroId { get; set; }

    public Guid TipoVinculoId { get; set; }

    public Guid? FlujoId { get; set; }

    public EstadoVinculo Estado { get; set; }

    public EstadoVinculo? EstadoPrevioAlBloqueo { get; set; }

    public Dictionary<string, JsonElement> Atributos { get; set; } = [];

    public OrigenVinculo Origen { get; set; }

    /// <summary>Una referencia por sistema integrado que comparte esta validación (I-0).</summary>
    public List<ReferenciaExterna> Referencias { get; set; } = [];

    /// <summary>Copia plana de <see cref="Referencias"/> (<c>SISTEMA|id</c>) para buscar por referencia en Marten.</summary>
    public List<string> ClavesReferencia { get; set; } = [];

    /// <summary>Correo del tercero al que se dirigen las invitaciones de este vínculo.</summary>
    public string CorreoTercero { get; set; } = string.Empty;

    public InvitacionVigente? Invitacion { get; set; }

    public bool InvitacionAceptada { get; set; }

    public DateTimeOffset? InvitacionAceptadaEn { get; set; }

    public Bloqueo? Bloqueo { get; set; }

    /// <summary>Si alguna vez tuvo un expediente aprobado (habilita renovación al reintegrar).</summary>
    public bool TuvoAprobacion { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    // ---------- Decisiones ----------

    public static VinculoCreado Crear(Guid empresaId, Guid terceroId, Guid tipoVinculoId, Guid? flujoId,
        Dictionary<string, JsonElement> atributos, OrigenVinculo origen, ReferenciaExterna? referencia,
        string correoInvitacion, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(correoInvitacion) || !correoInvitacion.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invitacion-requerido",
                "El vínculo necesita un correo válido del tercero para enviarle la invitación.");

        if (referencia is not null)
            ValidarReferencia(referencia);

        return new VinculoCreado(Guid.CreateVersion7(), empresaId, terceroId, tipoVinculoId, flujoId, atributos,
            origen, referencia, correoInvitacion.Trim().ToLowerInvariant(), ahora);
    }

    /// <summary>
    /// Otro sistema pide el mismo vínculo <c>(empresa, tercero, tipo)</c>: en lugar de duplicarlo, se agrega su
    /// referencia y ambos comparten la validación (I-0). Devuelve <c>null</c> si esa referencia ya estaba
    /// (idempotente); el mismo sistema con otro id es conflicto.
    /// </summary>
    public ReferenciaExternaAgregada? AgregarReferencia(ReferenciaExterna referencia, DateTimeOffset ahora)
    {
        ValidarReferencia(referencia);

        var delSistema = Referencias.FirstOrDefault(r => r.Sistema == referencia.Sistema);
        if (delSistema is not null)
        {
            return delSistema.Id == referencia.Id
                ? null
                : throw ReglaDeNegocioException.Conflicto("referencia-en-conflicto",
                    $"El vínculo ya tiene la referencia {delSistema.Id} del sistema {delSistema.Sistema}.");
        }

        return new ReferenciaExternaAgregada(referencia.Sistema, referencia.Id, ahora);
    }

    /// <summary>Solo en <c>Invitado</c> y mientras el tercero no haya aceptado (docs/03 §5.3).</summary>
    public InvitacionEnviada EnviarInvitacion(string? correoDestino, TimeSpan vigencia, Guid? loteId, DateTimeOffset ahora)
    {
        if (Estado != EstadoVinculo.Invitado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"Solo se invita un vínculo en estado Invitado; este está {Estado}.");

        if (InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-ya-aceptada", "El tercero ya aceptó la invitación de esta empresa.");

        if (vigencia <= TimeSpan.Zero || vigencia > TimeSpan.FromDays(30))
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "La vigencia del enlace debe estar entre 1 hora y 30 días.");

        var correo = string.IsNullOrWhiteSpace(correoDestino) ? CorreoTercero : correoDestino.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invitacion-requerido", "Falta un correo válido para la invitación.");

        return new InvitacionEnviada("Correo", correo, ahora + vigencia, loteId, ahora);
    }

    /// <summary>
    /// El tercero acepta (primer acceso). Desde aquí la bóveda se puede compartir con esta empresa (B-1,
    /// ADR-0009). No cambia el estado: <c>EnValidacion</c> llega con el expediente inicial.
    /// </summary>
    public InvitacionAceptada AceptarInvitacion(string correo, DateTimeOffset ahora)
    {
        if (InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-ya-aceptada", "La invitación ya fue aceptada.");

        if (Estado is EstadoVinculo.Bloqueado or EstadoVinculo.Anulado or EstadoVinculo.Desvinculado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se puede aceptar una invitación de un vínculo {Estado}.");

        if (Invitacion is null)
            throw ReglaDeNegocioException.Conflicto("sin-invitacion", "Este vínculo no tiene una invitación enviada.");

        if (Invitacion.Expira <= ahora)
            throw ReglaDeNegocioException.Conflicto("invitacion-expirada", "El enlace de invitación expiró; pide a la empresa que lo reenvíe.");

        return new InvitacionAceptada(correo.Trim().ToLowerInvariant(), ahora);
    }

    public AtributosDeVinculoActualizados ActualizarAtributos(Dictionary<string, JsonElement> nuevos, DateTimeOffset ahora)
    {
        AsegurarNoTerminal("actualizar atributos");
        return new AtributosDeVinculoActualizados(Atributos, nuevos, ahora);
    }

    /// <summary>Privado de la empresa (P-5-3). Suspende sin borrar historial (B-5).</summary>
    public VinculoBloqueado Bloquear(string motivo, DateTimeOffset? hasta, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "El bloqueo necesita un motivo.");
        if (Estado == EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("ya-bloqueado", "El vínculo ya está bloqueado.");
        AsegurarNoTerminal("bloquear");

        if (hasta is not null && hasta <= ahora)
            throw ReglaDeNegocioException.Validacion("fecha-invalida", "La fecha de fin del bloqueo debe ser futura.");

        return new VinculoBloqueado(Estado, motivo.Trim(), hasta, ahora);
    }

    /// <summary>Restituye el estado previo al bloqueo (B-5).</summary>
    public VinculoDesbloqueado Desbloquear(string justificacion, DateTimeOffset ahora)
    {
        RequerirTexto(justificacion, "justificacion-requerida", "El desbloqueo necesita una justificación.");
        if (Estado != EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("no-bloqueado", "El vínculo no está bloqueado.");

        return new VinculoDesbloqueado(EstadoPrevioAlBloqueo ?? EstadoVinculo.Invitado, justificacion.Trim(), ahora);
    }

    public VinculoDesvinculado Desvincular(string motivo, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "La desvinculación necesita un motivo.");
        AsegurarNoTerminal("desvincular");
        return new VinculoDesvinculado(Estado, motivo.Trim(), ahora);
    }

    /// <summary>
    /// Desde <c>Desvinculado</c>: si alguna vez fue aprobado vuelve a validación (renovación); si no, a
    /// <c>Invitado</c> para empezar de nuevo.
    /// </summary>
    public VinculoReintegrado Reintegrar(string justificacion, DateTimeOffset ahora)
    {
        RequerirTexto(justificacion, "justificacion-requerida", "El reintegro necesita una justificación.");
        if (Estado != EstadoVinculo.Desvinculado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", "Solo se reintegra un vínculo desvinculado.");

        return new VinculoReintegrado(TuvoAprobacion ? EstadoVinculo.EnValidacion : EstadoVinculo.Invitado, justificacion.Trim(), ahora);
    }

    /// <summary>Terminal; desde cualquier estado no terminal, incluido Bloqueado.</summary>
    public VinculoAnulado Anular(string motivo, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "La anulación necesita un motivo.");
        AsegurarNoTerminal("anular");
        return new VinculoAnulado(Estado, motivo.Trim(), ahora);
    }

    // ---------- Evolución (Marten) ----------

    public static Vinculo Create(VinculoCreado e) => new()
    {
        Id = e.VinculoId,
        EmpresaId = e.EmpresaId,
        TerceroId = e.TerceroId,
        TipoVinculoId = e.TipoVinculoId,
        FlujoId = e.FlujoId,
        Estado = EstadoVinculo.Invitado,
        Atributos = e.Atributos,
        Origen = e.Origen,
        Referencias = e.ReferenciaExterna is null ? [] : [e.ReferenciaExterna],
        ClavesReferencia = e.ReferenciaExterna is null ? [] : [e.ReferenciaExterna.Clave()],
        CorreoTercero = e.CorreoInvitacion,
        CreadoEn = e.Fecha,
        ActualizadoEn = e.Fecha
    };

    public void Apply(ReferenciaExternaAgregada e)
    {
        var referencia = new ReferenciaExterna(e.Sistema, e.Id);
        Referencias.Add(referencia);
        ClavesReferencia.Add(referencia.Clave());
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionEnviada e)
    {
        Invitacion = new InvitacionVigente(e.CorreoDestino, e.Expira, (Invitacion?.Envios ?? 0) + 1);
        CorreoTercero = e.CorreoDestino;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionAceptada e)
    {
        InvitacionAceptada = true;
        InvitacionAceptadaEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AtributosDeVinculoActualizados e)
    {
        Atributos = e.Despues;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoBloqueado e)
    {
        EstadoPrevioAlBloqueo = e.EstadoPrevio;
        Estado = EstadoVinculo.Bloqueado;
        Bloqueo = new Bloqueo(e.Motivo, e.Fecha, e.Hasta);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoDesbloqueado e)
    {
        Estado = e.EstadoRestituido;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoDesvinculado e)
    {
        Estado = EstadoVinculo.Desvinculado;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoReintegrado e)
    {
        Estado = e.EstadoResultante;
        if (e.EstadoResultante == EstadoVinculo.Invitado)
        {
            InvitacionAceptada = false;
            InvitacionAceptadaEn = null;
        }

        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoAnulado e)
    {
        Estado = EstadoVinculo.Anulado;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas comunes ----------

    private void AsegurarNoTerminal(string accion)
    {
        if (Terminales.Contains(Estado))
            throw ReglaDeNegocioException.Conflicto("estado-terminal", $"No se puede {accion} un vínculo {Estado}.");
    }

    private static void ValidarReferencia(ReferenciaExterna referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia.Sistema) || string.IsNullOrWhiteSpace(referencia.Id))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "La referencia externa necesita sistema e id.");
    }

    private static void RequerirTexto(string? valor, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw ReglaDeNegocioException.Validacion(codigo, mensaje);
    }
}
