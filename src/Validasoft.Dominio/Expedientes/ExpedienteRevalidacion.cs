using System.Text.Json.Serialization;

namespace Validasoft.Dominio.Expedientes;

public sealed partial class Expediente
{
    /// <summary>Plazo por omisión para resolver una revalidación (RV-4, A-2): lo que rige si ningún requisito trae el suyo.</summary>
    public const int DiasDePlazoDeRevalidacionPorOmision = 15;

    /// <summary>
    /// Días para resolver la revalidación antes de que los pendientes del certificado bloqueen (RV-4): el menor de los plazos
    /// configurados en los requisitos que incluye (<c>diasRevalidacion</c> del tipo de documento o del requisito de sección del
    /// flujo) y, si ninguno trae el suyo, <see cref="DiasDePlazoDeRevalidacionPorOmision"/>. Se deriva de los requisitos: si la
    /// actualización se amplía con uno de plazo menor, el plazo se acorta.
    /// </summary>
    [JsonIgnore]
    public int DiasDePlazoDeRevalidacion =>
        Requisitos.Where(r => r.DiasRevalidacion is > 0).Select(r => r.DiasRevalidacion!.Value).DefaultIfEmpty(DiasDePlazoDeRevalidacionPorOmision).Min();

    /// <summary>Hasta cuándo puede resolverse la revalidación antes de que sus pendientes bloqueen (RV-4).</summary>
    [JsonIgnore]
    public DateTimeOffset PlazoDeRevalidacionHasta => AbiertoEn.AddDays(DiasDePlazoDeRevalidacion);
}
