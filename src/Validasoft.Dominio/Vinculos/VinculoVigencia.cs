using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Vinculos;

/// <summary>
/// Vigencia periódica del vínculo y fin de los bloqueos con fecha (VV-1…VV-5). El flujo con el que se aprueba un expediente dice
/// cuántos días vale la aprobación; al aprobarse, <see cref="VigenciaDelVinculoFijada"/> guarda la fecha hasta la que es
/// vigente y el vínculo lleva en <see cref="ProximaRevisionDeVigencia"/> el próximo momento en que el job debe mirarlo (aviso,
/// vencimiento o fin del bloqueo): el índice del barrido, como <c>ProximaRevisionDePlazo</c> del expediente.
/// </summary>
public sealed partial class Vinculo
{
    /// <summary>
    /// Fin de la vigencia (inclusive): la más próxima entre la aprobación + los días del flujo y la vigencia propia de cada
    /// sección aceptada. Nula = sin vencimiento periódico.
    /// </summary>
    public DateOnly? VigenteHasta { get; set; }

    /// <summary>Días antes de <see cref="VigenteHasta"/> en que se avisa (el primero deja el vínculo <c>PorVencer</c>).</summary>
    public List<int> DiasAvisoVigencia { get; set; } = [];

    /// <summary>Días después de <see cref="VigenteHasta"/> en que el vínculo sigue valiendo antes de pasar a <c>Vencido</c>.</summary>
    public int DiasGraciaVigencia { get; set; }

    /// <summary>Días de aviso que ya se cumplieron con la vigencia actual (no se repiten).</summary>
    public List<int> AvisosDeVigenciaCumplidos { get; set; } = [];

    /// <summary>
    /// Venció por su vigencia y no había renovación abierta (la del pre-vencimiento, VV-3): sigue en <c>Vencido</c> hasta que el job
    /// la abre. Falso si la renovación ya estaba abierta al vencer: esa misma sigue su curso.
    /// </summary>
    public bool RenovacionPorVigenciaPendiente { get; set; }

    /// <summary>Cuándo se aprobó por última vez (la base de la vigencia periódica).</summary>
    public DateTimeOffset? UltimaAprobacionEn { get; set; }

    /// <summary>
    /// Próximo momento en que el job de vigencias debe revisar el vínculo: el siguiente aviso, el vencimiento, el reintento de abrir
    /// la renovación o el fin del bloqueo. Nulo si no hay nada pendiente. Índice del barrido.
    /// </summary>
    public DateTimeOffset? ProximaRevisionDeVigencia { get; set; }

    /// <summary>Cuándo termina la gracia y el vínculo pasa a <c>Vencido</c> por su vigencia; nulo sin vigencia.</summary>
    [JsonIgnore]
    public DateTimeOffset? VencimientoDeVigencia =>
        VigenteHasta is { } hasta ? CalculadoraDeVigencia.MomentoDeVencimiento(CalculadoraDeVigencia.FinDeGracia(hasta, DiasGraciaVigencia)!.Value) : null;

    // ---------- Decisiones ----------

    /// <summary>
    /// Fija la vigencia del vínculo al aprobarse un expediente. Nulo si no cambia nada (sin vigencia ni antes ni ahora, o la misma
    /// vigencia sin avisos cumplidos): así un vínculo sin vigencia periódica no suma eventos.
    /// </summary>
    public VigenciaDelVinculoFijada? FijarVigencia(DateOnly? vigenteHasta, IReadOnlyList<int> diasAviso, int diasGracia, DateTimeOffset ahora)
    {
        var avisos = diasAviso.Where(d => d > 0).Distinct().OrderDescending().ToList();
        if (vigenteHasta is null && VigenteHasta is null)
            return null;
        if (vigenteHasta == VigenteHasta && diasGracia == DiasGraciaVigencia && avisos.SequenceEqual(DiasAvisoVigencia) &&
            AvisosDeVigenciaCumplidos.Count == 0 && !RenovacionPorVigenciaPendiente)
            return null;

        return new VigenciaDelVinculoFijada(vigenteHasta, avisos, Math.Max(0, diasGracia), ahora);
    }

    /// <summary>
    /// Llegó el día de un aviso: un vínculo aprobado <c>Activo</c> o <c>PorVencer</c> que aún no vence. El primero abre el
    /// pre-vencimiento (el vínculo queda <c>PorVencer</c> y se abre la renovación, VV-3) y los siguientes solo recuerdan. Los avisos
    /// que ya se pasaron (el job se atrasó) se cumplen juntos con un solo evento. Si ya le toca vencer no avisa: vence.
    /// </summary>
    public VinculoPorVencer? AvisarVigencia(DateTimeOffset ahora)
    {
        if (!TuvoAprobacion || Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer) || VigenteHasta is not { } hasta)
            return null;
        // Eximido de todo (EX-1): no se avisa ni se recuerda la renovación mientras la exención rija; al terminar, el job vuelve a mirar.
        if (ExencionTotal(CalculadoraDeVigencia.Fecha(ahora)) is not null)
            return null;
        if (VencimientoDeVigencia is { } vence && ahora >= vence)
            return null;

        var cumplidos = DiasAvisoVigencia
            .Where(d => !AvisosDeVigenciaCumplidos.Contains(d) && ahora >= CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-d)))
            .Order()
            .ToList();
        if (cumplidos.Count == 0)
            return null;

        return new VinculoPorVencer(hasta, Math.Max(0, hasta.DayNumber - CalculadoraDeVigencia.Fecha(ahora).DayNumber), cumplidos, ahora);
    }

    /// <summary>
    /// Terminó la gracia de la vigencia: el vínculo aprobado, <c>Activo</c> o <c>PorVencer</c>, pasa a <c>Vencido</c> (VV-3), tenga
    /// o no abierta la renovación del pre-vencimiento. Nulo si no le toca todavía o ya no está en esos estados (bloqueado, vencido,
    /// terminado).
    /// </summary>
    public VinculoVencido? VencerPorVigencia(DateTimeOffset ahora, string descripcion) =>
        TuvoAprobacion && Estado is (EstadoVinculo.Activo or EstadoVinculo.PorVencer) && VencimientoDeVigencia is { } vence && ahora >= vence &&
        ExencionTotal(CalculadoraDeVigencia.Fecha(ahora)) is null // eximido de todo (EX-1): la vigencia no lo vence mientras la exención rija
            ? new VinculoVencido(descripcion, ahora, CausaDeVencimiento.Vigencia)
            : null;

    /// <summary>
    /// Llegó la fecha de fin de un bloqueo temporal (VV-5): restituye el estado previo igual que el desbloqueo manual, con el
    /// motivo en la justificación. Nulo si no está bloqueado, el bloqueo no tiene fecha o aún no llega (idempotente: un desbloqueo
    /// manual anterior lo deja sin efecto).
    /// </summary>
    public VinculoDesbloqueado? DesbloquearPorFecha(DateTimeOffset ahora)
    {
        if (Estado != EstadoVinculo.Bloqueado || Bloqueo is not { Hasta: { } hasta } || ahora < hasta)
            return null;

        return Desbloquear($"El bloqueo temporal terminó el {CalculadoraDeVigencia.Fecha(hasta):dd/MM/yyyy}.", ahora);
    }

    // ---------- Evolución ----------

    public void Apply(VigenciaDelVinculoFijada e)
    {
        VigenteHasta = e.VigenteHasta;
        DiasAvisoVigencia = [.. e.DiasAviso];
        DiasGraciaVigencia = e.DiasGracia;
        AvisosDeVigenciaCumplidos = [];
        // Una vigencia nueva (se aprobó la renovación del pre-vencimiento, otro expediente, o no había nada que ratificar) deja al
        // vínculo al día: la cuenta de los días parte de esa aprobación (VV-1) y no vuelve a vencer.
        if (Estado == EstadoVinculo.PorVencer || (Estado == EstadoVinculo.Vencido && RenovacionPorVigenciaPendiente))
            Estado = EstadoVinculo.Activo;
        RenovacionPorVigenciaPendiente = false;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoPorVencer e)
    {
        if (Estado == EstadoVinculo.Activo)
            Estado = EstadoVinculo.PorVencer;
        AvisosDeVigenciaCumplidos = [.. AvisosDeVigenciaCumplidos.Union(e.AvisosCumplidos)];
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas internas ----------

    /// <summary>
    /// Deriva <see cref="ProximaRevisionDeVigencia"/> del estado (lo llama cada evento que lo mueve): bloqueado, la fecha de fin
    /// del bloqueo; <c>Activo</c> o <c>PorVencer</c> con vigencia, el siguiente aviso sin cumplir o el vencimiento; vencido por
    /// vigencia con la renovación sin abrir, ya (se reintenta); terminal o sin vigencia, nada.
    /// </summary>
    private void RecalcularProximaRevision()
    {
        ProximaRevisionDeVigencia = ProximoMomentoDeVigencia();
    }

    /// <summary>
    /// Lo primero que hay que mirar: lo de la vigencia del vínculo o, si llega antes, el fin de una exención (EX-4: al terminar se recalcula el
    /// estado). Un vínculo terminado no tiene nada pendiente.
    /// </summary>
    private DateTimeOffset? ProximoMomentoDeVigencia()
    {
        if (Estado is EstadoVinculo.Desvinculado or EstadoVinculo.Anulado)
            return null;

        var vigencia = MomentoDeLaVigenciaDelVinculo();
        var exencion = Exenciones
            .Where(e => e is { Estado: EstadoDeExencion.Vigente, Hasta: not null })
            .Select(e => (DateTimeOffset?)CalculadoraDeVigencia.MomentoDeVencimiento(e.Hasta!.Value))
            .Min();
        return vigencia is null ? exencion : exencion is null ? vigencia : vigencia < exencion ? vigencia : exencion;
    }

    private DateTimeOffset? MomentoDeLaVigenciaDelVinculo()
    {
        if (Estado == EstadoVinculo.Bloqueado)
            return Bloqueo?.Hasta;
        // Con una exención total registrada (EX-1) no hay aviso, vencimiento ni reintento que esperar: el que cuenta es el fin de la exención.
        if (Exenciones.Any(e => e is { Todos: true, Estado: EstadoDeExencion.Vigente }))
            return null;
        if (VigenteHasta is not { } hasta || VencimientoDeVigencia is not { } vence)
            return null;

        if (Estado == EstadoVinculo.Vencido)
            return RenovacionPorVigenciaPendiente && ExpedienteAbiertoId is null ? vence : null;
        if (Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer) || !TuvoAprobacion)
            return null;

        var proximos = DiasAvisoVigencia
            .Where(d => !AvisosDeVigenciaCumplidos.Contains(d))
            .Select(d => CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-d)))
            .Where(m => m < vence)
            .Append(vence);
        return proximos.Min();
    }
}
