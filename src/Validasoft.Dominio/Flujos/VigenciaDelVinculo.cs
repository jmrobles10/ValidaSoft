using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Flujos;

/// <summary>
/// Vigencia periódica del vínculo completo (VV-1, la «vigencia del proveedor» del SRM, <c>Prov_DiasVencimiento</c>): a los
/// <see cref="Dias"/> de su última aprobación el vínculo vence y el tercero debe ratificar o actualizar las secciones de la ficha
/// que pide el flujo. <c>Dias = 0</c>: sin vencimiento periódico (las secciones con vigencia propia siguen valiendo).
/// <see cref="DiasAviso"/> son los días antes de vencer en que se le avisa (el primero deja el vínculo <c>PorVencer</c>, como
/// <c>DiasAviso</c> de un tipo de documento) y <see cref="DiasGracia"/> los que siguen siendo válido después de la fecha: el
/// vínculo pasa a <c>Vencido</c> al terminar la gracia (la misma regla de los documentos, TD-6).
/// </summary>
public sealed record VigenciaDelVinculo(int Dias, IReadOnlyList<int> DiasAviso, int DiasGracia)
{
    public const int DiasMaximos = 3650;
    public const int MaximoAvisos = 5;
    public const int GraciaMaxima = 365;

    /// <summary>Avisos por omisión (como los de un tipo de documento): a 30, 15 y 5 días de vencer.</summary>
    public static readonly IReadOnlyList<int> AvisosPorOmision = [30, 15, 5];

    /// <summary>Sin vencimiento periódico del vínculo (lo que rige si el flujo no la configura).</summary>
    public static readonly VigenciaDelVinculo Sin = new(0, AvisosPorOmision, 0);

    public bool Activa => Dias > 0;

    /// <summary>
    /// 0–3650 días (422 <c>vigencia-vinculo-invalida</c>); hasta 5 avisos positivos y, con vigencia, menores que ella
    /// (<c>aviso-vigencia-invalido</c>); gracia de 0 a 365 días (<c>gracia-vigencia-invalida</c>). Los avisos quedan sin repetidos y de
    /// mayor a menor.
    /// </summary>
    public VigenciaDelVinculo Validar()
    {
        if (Dias is < 0 or > DiasMaximos)
            throw ReglaDeNegocioException.Validacion("vigencia-vinculo-invalida", $"La vigencia del vínculo va de 0 (sin vencimiento) a {DiasMaximos} días.");
        if (DiasGracia is < 0 or > GraciaMaxima)
            throw ReglaDeNegocioException.Validacion("gracia-vigencia-invalida", $"La gracia de la vigencia va de 0 a {GraciaMaxima} días.");

        var avisos = (DiasAviso ?? AvisosPorOmision).Distinct().OrderDescending().ToList();
        if (avisos.Count > MaximoAvisos)
            throw ReglaDeNegocioException.Validacion("aviso-vigencia-invalido", $"La vigencia admite hasta {MaximoAvisos} avisos.");
        var tope = Activa ? Dias - 1 : GraciaMaxima;
        if (avisos.Any(a => a < 1 || a > tope))
            throw ReglaDeNegocioException.Validacion("aviso-vigencia-invalido",
                Activa
                    ? $"Cada aviso es un número de días antes de vencer, entre 1 y {tope}."
                    : $"Cada aviso es un número de días antes de vencer, entre 1 y {GraciaMaxima}.");
        return new VigenciaDelVinculo(Dias, avisos, DiasGracia);
    }
}
