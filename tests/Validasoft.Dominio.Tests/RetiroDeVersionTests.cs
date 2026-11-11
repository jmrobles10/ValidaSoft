using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Retiro de una versión por el tercero (RT): qué le pasa a cada empresa (impacto antes de retirar), cómo se lee lo que cada empresa
/// tiene de un documento en la tarjeta de la bóveda y cómo el expediente guarda el motivo del retiro de la versión que reemplaza.
/// </summary>
public class RetiroDeVersionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(-5));

    // ---------- impacto ----------

    [Fact]
    public void Nadie_la_recibio_no_toca_a_ninguna_empresa()
    {
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, Recibio: false, Aprobo: false, Revisa: false, PuedeRevalidar: true)).ShouldBeNull();
    }

    [Fact]
    public void La_empresa_que_la_aprobo_revalida_la_que_queda_vigente_y_la_que_la_revisa_vuelve_a_revisarla()
    {
        var aprobo = ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, Recibio: true, Aprobo: true, Revisa: false, PuedeRevalidar: true))!;
        aprobo.Situacion.ShouldBe(SituacionAnteRetiro.Aprobo);
        aprobo.Efecto.ShouldBe(EfectoDeRetiro.Revalidar);

        var revisa = ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, Recibio: true, Aprobo: false, Revisa: true, PuedeRevalidar: false))!;
        revisa.Situacion.ShouldBe(SituacionAnteRetiro.Revisa);
        revisa.Efecto.ShouldBe(EfectoDeRetiro.VolverARevisar);

        // Revisar manda sobre aprobar: una actualización abierta que ya tiene la versión la cambia.
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, true, Aprobo: true, Revisa: true, PuedeRevalidar: false))!.Efecto
            .ShouldBe(EfectoDeRetiro.VolverARevisar);
    }

    [Fact]
    public void Quien_solo_la_recibio_vuelve_a_la_anterior_y_si_no_hay_anterior_se_queda_sin_version()
    {
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, Recibio: true, Aprobo: false, Revisa: false, PuedeRevalidar: true))!
            .ShouldBe(new ImpactoParaEmpresa(SituacionAnteRetiro.Recibio, EfectoDeRetiro.VuelveALaAnterior));
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, null, Recibio: true, Aprobo: true, Revisa: false, PuedeRevalidar: true))!.Efecto
            .ShouldBe(EfectoDeRetiro.SinVersion);
    }

    [Fact]
    public void Retirar_una_version_que_no_es_la_vigente_o_aprobada_sin_poder_revalidar_no_cambia_nada()
    {
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(false, 8, Recibio: true, Aprobo: true, Revisa: false, PuedeRevalidar: true))!.Efecto
            .ShouldBe(EfectoDeRetiro.SinCambios);
        // Aprobó, pero su vínculo no puede abrir la revalidación (p. ej. ya tiene un expediente abierto sin este documento).
        ImpactoDeRetiro.Calcular(new HechosDeRetiro(true, 7, Recibio: true, Aprobo: true, Revisa: false, PuedeRevalidar: false))!.Efecto
            .ShouldBe(EfectoDeRetiro.SinCambios);
    }

    // ---------- lo que tiene cada empresa ----------

    [Theory]
    // Aprobó la 8 (retirada), la vigente es la 7 y revisa la 7 en una revalidación abierta.
    [InlineData(7, 7, null, 8, true, 7, false, SituacionConEmpresa.RevalidaRetirada, 8)]
    // Aprobó la 6 (superada), revisa la vigente 7 en una actualización abierta.
    [InlineData(7, 7, null, 6, false, 7, false, SituacionConEmpresa.Revalida, 6)]
    // Aprobó la 8 que se retiró y aún no se le abre la revalidación.
    [InlineData(7, 7, null, 8, true, null, false, SituacionConEmpresa.PorRevalidar, 8)]
    [InlineData(7, 7, null, 7, false, null, false, SituacionConEmpresa.AproboVigente, null)]
    [InlineData(7, 7, null, null, false, 7, false, SituacionConEmpresa.Revisando, null)]
    [InlineData(7, 7, null, null, false, 7, true, SituacionConEmpresa.Observada, null)]
    [InlineData(7, 7, null, null, false, null, false, SituacionConEmpresa.TieneVigente, null)]
    // Tiene la 6 y no recibe la vigente 7.
    [InlineData(7, 6, null, null, false, null, false, SituacionConEmpresa.TieneOtra, 7)]
    // No tiene nada de este documento.
    [InlineData(7, null, null, null, false, null, false, SituacionConEmpresa.NoRecibe, null)]
    // Todo se retiró: tenía la 1 y no queda vigente.
    [InlineData(null, null, 1, null, false, null, false, SituacionConEmpresa.SinVigente, 1)]
    public void Cada_empresa_se_lee_con_una_sola_situacion_que_no_contradice_la_vigente(int? vigente, int? recibida, int? retirada, int? aprobada,
        bool aprobadaRetirada, int? enRevision, bool observada, SituacionConEmpresa esperada, int? otra)
    {
        var lectura = SituacionDelDocumento.Calcular(new HechosConEmpresa(vigente, recibida, retirada, aprobada, aprobadaRetirada, enRevision, observada));

        lectura.Situacion.ShouldBe(esperada);
        lectura.Otra.ShouldBe(otra);
    }
}
