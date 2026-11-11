using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// El tercero recibió un recordatorio de las observaciones que tiene pendientes (NT-2): lo pidió una persona de la empresa
/// (<see cref="Por"/> = su id) o salió solo (<see cref="Automatico"/>, <see cref="Por"/> = <c>recordatorio-automatico</c>).
/// <see cref="DiasObservado"/>: cuántos días llevaba devuelto el expediente. Va al historial y lo recoge la campana del tercero.
/// </summary>
public sealed record RecordatorioDeObservacionesEnviado(string Por, bool Automatico, int DiasObservado, DateTimeOffset Fecha);

/// <summary>Por qué un expediente no admite hoy un recordatorio de observaciones.</summary>
public enum MotivoSinRecordatorio
{
    /// <summary>El expediente no está devuelto al tercero (<c>Observado</c>): no hay observaciones que recordar.</summary>
    NoObservado,

    /// <summary>Ya se le recordó hoy (límite de uno por día y expediente, como el recordatorio de vencimientos).</summary>
    YaRecordadoHoy
}

/// <summary>
/// Recordatorio de observaciones pendientes (NT-2). Mientras el expediente está devuelto al tercero (<c>Observado</c>), la
/// empresa puede recordárselo a mano o dejar que salga solo cada N días; en ambos casos a lo sumo uno por día de calendario del
/// negocio. Lo único que cambia en el expediente es la fecha del último recordatorio: no toca el plazo ni las decisiones.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>Cuándo se le recordó por última vez al tercero (en la devolución en curso o en una anterior).</summary>
    public DateTimeOffset? UltimoRecordatorioDeObservaciones { get; set; }

    /// <summary>Cuántos recordatorios se le han enviado en total (manuales y automáticos).</summary>
    public int RecordatoriosDeObservaciones { get; set; }

    /// <summary>Nulo si hoy se le puede recordar al tercero; si no, la razón.</summary>
    public MotivoSinRecordatorio? SinRecordatorioPorque(DateTimeOffset ahora)
    {
        if (Estado != EstadoExpediente.Observado)
            return MotivoSinRecordatorio.NoObservado;
        return UltimoRecordatorioDeObservaciones is { } ultimo && CalculadoraDeVigencia.Fecha(ultimo) == CalculadoraDeVigencia.Fecha(ahora)
            ? MotivoSinRecordatorio.YaRecordadoHoy
            : null;
    }

    /// <summary>
    /// El recordatorio de observaciones. 409 <c>expediente-no-observado</c> si no está devuelto al tercero y 409
    /// <c>recordatorio-ya-enviado-hoy</c> si ya se le recordó hoy.
    /// </summary>
    public RecordatorioDeObservacionesEnviado RecordarObservaciones(string por, bool automatico, DateTimeOffset ahora)
    {
        switch (SinRecordatorioPorque(ahora))
        {
            case MotivoSinRecordatorio.NoObservado:
                throw ReglaDeNegocioException.Conflicto("expediente-no-observado",
                    $"Solo se recuerdan las observaciones de un expediente devuelto al tercero; este está {Estado}.");
            case MotivoSinRecordatorio.YaRecordadoHoy:
                throw ReglaDeNegocioException.Conflicto("recordatorio-ya-enviado-hoy", "Ya se le recordó hoy al tercero: se puede volver a recordar mañana.");
        }

        return new RecordatorioDeObservacionesEnviado(por, automatico, DiasObservado(ahora), ahora);
    }

    /// <summary>
    /// Si toca el recordatorio automático (NT-3): han pasado <paramref name="cadaDias"/> días de calendario desde que se devolvió
    /// el expediente o desde el último recordatorio de esta devolución, y hoy se puede recordar.
    /// </summary>
    public bool TocaRecordatorioAutomatico(int cadaDias, DateTimeOffset ahora)
    {
        if (cadaDias <= 0 || SinRecordatorioPorque(ahora) is not null || ObservadoEn is not { } observadoEn)
            return false;
        var desde = UltimoRecordatorioDeObservaciones is { } ultimo && ultimo >= observadoEn ? ultimo : observadoEn;
        return CalculadoraDeVigencia.Fecha(ahora).DayNumber - CalculadoraDeVigencia.Fecha(desde).DayNumber >= cadaDias;
    }

    /// <summary>Días de calendario que lleva devuelto al tercero (0 el mismo día).</summary>
    public int DiasObservado(DateTimeOffset ahora) =>
        ObservadoEn is { } observadoEn
            ? Math.Max(0, CalculadoraDeVigencia.Fecha(ahora).DayNumber - CalculadoraDeVigencia.Fecha(observadoEn).DayNumber)
            : 0;

    public void Apply(RecordatorioDeObservacionesEnviado e)
    {
        UltimoRecordatorioDeObservaciones = e.Fecha;
        RecordatoriosDeObservaciones++;
        ActualizadoEn = e.Fecha;
    }
}
