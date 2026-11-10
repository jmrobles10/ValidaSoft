using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Flujos;

/// <summary>
/// Plazo que tiene el tercero para completar su expediente inicial (P-4-2, LT-1): <see cref="Dias"/> desde que el expediente
/// queda en sus manos (al abrirse y en cada devolución) y los <see cref="Recordatorios"/> (días antes de vencer) en que se le
/// recuerda. Al vencer, el expediente pasa a <c>Caducado</c> y la empresa lo puede reabrir. <c>Dias = 0</c>: sin plazo.
/// </summary>
public sealed record PlazoDeCargue(int Dias, IReadOnlyList<int> Recordatorios)
{
    public const int DiasMaximos = 365;
    public const int MaximoRecordatorios = 5;

    /// <summary>Por defecto (P-4-2): 30 días con recordatorios a 15, 7 y 1 día de vencer.</summary>
    public static readonly PlazoDeCargue PorDefecto = new(30, [15, 7, 1]);

    public bool Activo => Dias > 0;

    /// <summary>
    /// 0–365 días (<c>plazo-cargue-invalido</c>); hasta 5 recordatorios, cada uno entre 1 y <c>Dias - 1</c>
    /// (<c>recordatorio-invalido</c>). Los normaliza sin repetidos y de mayor a menor. Sin plazo no hay recordatorios.
    /// </summary>
    public PlazoDeCargue Validar()
    {
        if (Dias is < 0 or > DiasMaximos)
            throw ReglaDeNegocioException.Validacion("plazo-cargue-invalido", $"El plazo de cargue va de 0 (sin plazo) a {DiasMaximos} días.");

        var recordatorios = (Recordatorios ?? []).Distinct().OrderDescending().ToList();
        if (!Activo)
            return new PlazoDeCargue(0, []);
        if (recordatorios.Count > MaximoRecordatorios)
            throw ReglaDeNegocioException.Validacion("recordatorio-invalido", $"El plazo admite hasta {MaximoRecordatorios} recordatorios.");
        if (recordatorios.Any(r => r < 1 || r >= Dias))
            throw ReglaDeNegocioException.Validacion("recordatorio-invalido",
                $"Cada recordatorio es un número de días antes de vencer, entre 1 y {Dias - 1}.");
        return new PlazoDeCargue(Dias, recordatorios);
    }
}
