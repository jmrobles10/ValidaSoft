using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Ia;

/// <summary>
/// Tarifa general de las validaciones adicionales de IA (PI-12): lo que se cobra por cada una que una empresa consume por encima de las incluidas en su plan.
/// La fija Plataforma; cada empresa puede tener un precio propio en su plan (<see cref="PlanDeIaEmpresa.PrecioAdicional"/>). Global (esquema
/// <c>plataforma</c>), un solo documento con id <see cref="IdUnico"/>. Mientras nadie la fije, vale <see cref="PorDefecto"/>.
/// </summary>
public sealed partial class TarifaDeIa
{
    public const string IdUnico = "ia";

    /// <summary>Tope de cordura del precio (evita un cero de más).</summary>
    public const decimal PrecioMaximo = 100_000_000m;

    public const string MonedaPorDefecto = "COP";

    public string Id { get; set; } = IdUnico;

    public decimal PrecioPorValidacionAdicional { get; set; }

    /// <summary>Código ISO 4217 de la moneda en que se cobra (<c>COP</c>, <c>USD</c>…).</summary>
    public string Moneda { get; set; } = MonedaPorDefecto;

    public DateTimeOffset? ActualizadaEn { get; set; }

    public string? ActualizadaPor { get; set; }

    /// <summary>Sin tarifa fijada: las validaciones adicionales no se valoran (precio cero) hasta que Plataforma la configure.</summary>
    public static TarifaDeIa PorDefecto() => new();

    /// <summary>Fija el precio y la moneda. 422 <c>precio-adicional-invalido</c> o <c>moneda-invalida</c>.</summary>
    public void Fijar(decimal precio, string? moneda, string actor, DateTimeOffset ahora)
    {
        if (precio is < 0m or > PrecioMaximo)
            throw ReglaDeNegocioException.Validacion("precio-adicional-invalido", $"El precio de cada validación adicional debe estar entre 0 y {PrecioMaximo:0}.");
        var codigo = (string.IsNullOrWhiteSpace(moneda) ? MonedaPorDefecto : moneda).Trim().ToUpperInvariant();
        if (!RegexMoneda().IsMatch(codigo))
            throw ReglaDeNegocioException.Validacion("moneda-invalida", "La moneda debe ser un código de tres letras (COP, USD…).");

        PrecioPorValidacionAdicional = decimal.Round(precio, 2, MidpointRounding.AwayFromZero);
        Moneda = codigo;
        ActualizadaEn = ahora;
        ActualizadaPor = actor;
    }

    /// <summary>El precio de cada validación adicional de una empresa con ese plan: el suyo o, si no tiene, el de la tarifa general.</summary>
    public decimal PrecioPara(PlanDeIaEmpresa? plan) => plan?.PrecioAdicional ?? PrecioPorValidacionAdicional;

    /// <summary>Lo que vale lo adicional consumido en el mes con ese precio.</summary>
    public static decimal ValorDe(int adicionalesConsumidas, decimal precio) => decimal.Round(adicionalesConsumidas * precio, 2, MidpointRounding.AwayFromZero);

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex RegexMoneda();
}
