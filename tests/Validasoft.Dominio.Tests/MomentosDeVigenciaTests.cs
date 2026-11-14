using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// El aviso y el vencimiento se disparan a una hora de la zona de negocio configurada (<c>Validasoft:ZonaHoraria</c>), no a una hora UTC
/// fija que solo valía para Bogotá (OC-2).
/// </summary>
public class MomentosDeVigenciaTests
{
    private static readonly TimeZoneInfo Bogota = CalculadoraDeVigencia.Zona("America/Bogota");
    private static readonly TimeZoneInfo Madrid = CalculadoraDeVigencia.Zona("Europe/Madrid");

    [Fact]
    public void En_Bogota_el_aviso_sale_a_las_8_de_la_manana_es_decir_las_13_UTC()
    {
        var momento = CalculadoraDeVigencia.MomentoDeAviso(new DateOnly(2026, 11, 10), Bogota);

        momento.UtcDateTime.ShouldBe(new DateTime(2026, 11, 10, 13, 0, 0, DateTimeKind.Utc));
        TimeZoneInfo.ConvertTime(momento, Bogota).TimeOfDay.ShouldBe(TimeSpan.FromHours(8));
    }

    [Fact]
    public void En_Bogota_el_vencimiento_es_la_medianoche_del_dia_siguiente_a_la_gracia_es_decir_las_5_UTC()
    {
        var momento = CalculadoraDeVigencia.MomentoDeVencimiento(new DateOnly(2026, 11, 10), Bogota);

        momento.UtcDateTime.ShouldBe(new DateTime(2026, 11, 11, 5, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Con_otra_zona_el_aviso_sigue_siendo_a_las_8_locales_y_no_a_las_13_UTC()
    {
        // Madrid: UTC+1 en invierno, UTC+2 en verano.
        CalculadoraDeVigencia.MomentoDeAviso(new DateOnly(2026, 1, 15), Madrid).UtcDateTime.ShouldBe(new DateTime(2026, 1, 15, 7, 0, 0, DateTimeKind.Utc));
        CalculadoraDeVigencia.MomentoDeAviso(new DateOnly(2026, 7, 15), Madrid).UtcDateTime.ShouldBe(new DateTime(2026, 7, 15, 6, 0, 0, DateTimeKind.Utc));
        CalculadoraDeVigencia.MomentoDeAviso(new DateOnly(2026, 7, 15), TimeZoneInfo.Utc).UtcDateTime.ShouldBe(new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Con_otra_zona_el_vencimiento_es_la_medianoche_local_del_dia_siguiente()
    {
        // Fin de la gracia el 15 de julio: vence a las 00:00 del 16 en Madrid (UTC+2) = 22:00 UTC del 15.
        CalculadoraDeVigencia.MomentoDeVencimiento(new DateOnly(2026, 7, 15), Madrid).UtcDateTime
            .ShouldBe(new DateTime(2026, 7, 15, 22, 0, 0, DateTimeKind.Utc));
        CalculadoraDeVigencia.MomentoDeVencimiento(new DateOnly(2026, 1, 15), Madrid).UtcDateTime
            .ShouldBe(new DateTime(2026, 1, 15, 23, 0, 0, DateTimeKind.Utc));
        CalculadoraDeVigencia.MomentoDeVencimiento(new DateOnly(2026, 1, 15), TimeZoneInfo.Utc).UtcDateTime
            .ShouldBe(new DateTime(2026, 1, 16, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void El_aviso_cae_en_la_fecha_calendario_correcta_de_la_zona_configurada()
    {
        // La fecha de calendario del instante del aviso, en su zona, es la fecha del aviso (no la del día anterior o siguiente en UTC).
        foreach (var zona in new[] { Bogota, Madrid, TimeZoneInfo.Utc, CalculadoraDeVigencia.Zona("Pacific/Auckland"), CalculadoraDeVigencia.Zona("America/Los_Angeles") })
        {
            var fecha = new DateOnly(2026, 6, 20);
            var aviso = CalculadoraDeVigencia.MomentoDeAviso(fecha, zona);
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(aviso, zona).DateTime).ShouldBe(fecha, zona.Id);
            var vence = CalculadoraDeVigencia.MomentoDeVencimiento(fecha, zona);
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(vence, zona).DateTime).ShouldBe(fecha.AddDays(1), zona.Id);
        }
    }

    [Fact]
    public void Una_medianoche_que_no_existe_por_el_salto_de_la_hora_de_verano_se_corre_a_la_1_local()
    {
        // Zona de prueba que adelanta el reloj a medianoche del 8 de marzo (00:00 pasa a 01:00): la 00:00 de ese día no existe.
        var regla = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), TimeSpan.FromHours(1),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 8),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 11, 1));
        var zona = TimeZoneInfo.CreateCustomTimeZone("prueba-salto", TimeSpan.FromHours(-5), "salto", "salto", "salto verano", [regla]);

        var momento = CalculadoraDeVigencia.MomentoDeVencimiento(new DateOnly(2026, 3, 7), zona);

        // 01:00 con el desfase de verano (-4) = 05:00 UTC; el instante existe y es posterior a la medianoche inexistente.
        momento.UtcDateTime.ShouldBe(new DateTime(2026, 3, 8, 5, 0, 0, DateTimeKind.Utc));
        zona.IsInvalidTime(new DateTime(2026, 3, 8, 0, 0, 0, DateTimeKind.Unspecified)).ShouldBeTrue();
    }

    [Fact]
    public void Sin_indicar_zona_se_usa_la_zona_de_negocio_configurada()
    {
        var fecha = new DateOnly(2026, 11, 10);

        CalculadoraDeVigencia.MomentoDeAviso(fecha).ShouldBe(CalculadoraDeVigencia.MomentoDeAviso(fecha, CalculadoraDeVigencia.ZonaDeNegocio));
        CalculadoraDeVigencia.MomentoDeVencimiento(fecha).ShouldBe(CalculadoraDeVigencia.MomentoDeVencimiento(fecha, CalculadoraDeVigencia.ZonaDeNegocio));
    }
}
