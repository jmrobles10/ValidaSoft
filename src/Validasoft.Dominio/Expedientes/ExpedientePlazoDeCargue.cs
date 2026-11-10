using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Plazo de cargue del expediente inicial (P-4-2, LT-1…LT-3). Corre mientras el expediente está en manos del tercero: desde
/// que se abre (fase de carga) y de nuevo, completo, en cada devolución (<c>Observado</c>); se detiene al pasar a revisión
/// y al cerrarse. El job de plazos llama a <see cref="RevisarPlazo"/>: recuerda en los días configurados y, al vencer, caduca.
/// La empresa reabre el caducado con un plazo nuevo (<see cref="Reabrir"/>). Todo se deriva de los eventos: el plazo vigente
/// se recalcula al aplicarlos.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>
    /// Lo que toca hacer con el plazo a la hora <paramref name="ahora"/>: nada, un recordatorio (el más cercano a vencer de los
    /// que ya se cumplieron y no salieron) o la caducidad. Idempotente: un recordatorio enviado no se repite y un expediente que
    /// ya no está en manos del tercero no caduca.
    /// </summary>
    public IReadOnlyList<object> RevisarPlazo(DateTimeOffset ahora)
    {
        if (Estado is not (EstadoExpediente.Abierto or EstadoExpediente.Observado) || PlazoCargueHasta is not { } hasta || PlazoCargue is not { } plazo)
            return [];
        if (ahora >= hasta)
            return [new ExpedienteCaducado(Estado, hasta, ahora)];

        var cumplidos = plazo.Recordatorios.Where(n => !RecordatoriosEnviados.Contains(n) && hasta.AddDays(-n) <= ahora).ToList();
        return cumplidos.Count == 0 ? [] : [new RecordatorioDeCargueEnviado(cumplidos.Min(), hasta, ahora)];
    }

    /// <summary>
    /// Reabre un expediente caducado (P-4-2: «en un clic»): vuelve al estado en que caducó con el plazo del flujo completo
    /// desde ahora. 409 <c>estado-invalido</c> si no está caducado.
    /// </summary>
    public ExpedienteReabierto Reabrir(string? justificacion, DateTimeOffset ahora)
    {
        if (Estado != EstadoExpediente.Caducado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se reabre un expediente caducado; este está {Estado}.");
        var dias = PlazoCargue is { Activo: true } plazo ? plazo.Dias : PlazoDeCargue.PorDefecto.Dias;
        return new ExpedienteReabierto(EstadoAlCaducar ?? EstadoExpediente.Abierto, ahora.AddDays(dias), Texto(justificacion, "justificacion"), ahora);
    }

    public void Apply(RecordatorioDeCargueEnviado e)
    {
        // El recordatorio cubre también los mayores que no alcanzaron a salir (p. ej. el job estuvo detenido).
        foreach (var n in (PlazoCargue?.Recordatorios ?? []).Where(n => n >= e.DiasAntes && !RecordatoriosEnviados.Contains(n)))
            RecordatoriosEnviados.Add(n);
        if (!RecordatoriosEnviados.Contains(e.DiasAntes))
            RecordatoriosEnviados.Add(e.DiasAntes);
        RecalcularRevisionDePlazo();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteCaducado e)
    {
        Estado = EstadoExpediente.Caducado;
        EstadoAlCaducar = e.EstadoPrevio;
        CaducadoEn = e.Fecha;
        CerradoEn = e.Fecha;
        ProximaRevisionDePlazo = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteReabierto e)
    {
        Estado = e.EstadoRestituido;
        EstadoAlCaducar = null;
        CaducadoEn = null;
        CerradoEn = null;
        Reaperturas++;
        PlazoCargueHasta = e.PlazoHasta;
        RecordatoriosEnviados = [];
        RecalcularRevisionDePlazo();
        ActualizadoEn = e.Fecha;
    }

    /// <summary>El expediente queda en manos del tercero: el plazo completo corre desde <paramref name="desde"/>.</summary>
    private void IniciarPlazo(DateTimeOffset desde)
    {
        if (PlazoCargue is not { Activo: true } plazo)
        {
            TerminarPlazo();
            return;
        }

        PlazoCargueHasta = desde.AddDays(plazo.Dias);
        RecordatoriosEnviados = [];
        RecalcularRevisionDePlazo();
    }

    /// <summary>Pasa a revisión o se cierra: ya no hay plazo que vigilar.</summary>
    private void TerminarPlazo()
    {
        PlazoCargueHasta = null;
        RecordatoriosEnviados = [];
        ProximaRevisionDePlazo = null;
    }

    /// <summary>El más cercano entre los recordatorios pendientes y el vencimiento.</summary>
    private void RecalcularRevisionDePlazo()
    {
        if (PlazoCargueHasta is not { } hasta)
        {
            ProximaRevisionDePlazo = null;
            return;
        }

        var pendientes = (PlazoCargue?.Recordatorios ?? []).Where(n => !RecordatoriosEnviados.Contains(n)).Select(n => hasta.AddDays(-n));
        ProximaRevisionDePlazo = pendientes.Append(hasta).Min();
    }
}
