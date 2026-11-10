using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Tests;

/// <summary>Las fechas de calendario del negocio se toman en su zona horaria, no en UTC (FE-1).</summary>
public class FechaDeNegocioTests
{
    [Fact]
    public void De_noche_en_Bogota_sigue_siendo_el_mismo_dia_aunque_en_UTC_ya_sea_el_siguiente()
    {
        // 9 de noviembre, 9:00 p. m. en Bogotá = 10 de noviembre, 2:00 a. m. UTC.
        var instante = new DateTimeOffset(2026, 11, 10, 2, 0, 0, TimeSpan.Zero);

        CalculadoraDeVigencia.Fecha(instante).ShouldBe(new DateOnly(2026, 11, 9));
    }

    [Fact]
    public void Al_amanecer_en_Bogota_ya_es_el_dia_siguiente()
    {
        var instante = new DateTimeOffset(2026, 11, 10, 5, 0, 0, TimeSpan.Zero); // 12:00 a. m. en Bogotá

        CalculadoraDeVigencia.Fecha(instante).ShouldBe(new DateOnly(2026, 11, 10));
    }

    [Fact]
    public void Una_zona_desconocida_cae_en_UTC()
    {
        CalculadoraDeVigencia.Zona("Zona/Inexistente").ShouldBe(TimeZoneInfo.Utc);
        CalculadoraDeVigencia.Zona(null).ShouldBe(TimeZoneInfo.Utc);
    }
}
