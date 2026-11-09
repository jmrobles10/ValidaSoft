using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Dominio.Tests;

/// <summary>Redacción de los correos al tercero (lo que lee el proveedor).</summary>
public class CorreoTests
{
    [Fact]
    public void Los_motivos_se_unen_como_oraciones_sin_punto_y_coma()
    {
        RedactorDeCorreos.UnirMotivos(
                ["Los datos informados no coinciden con los soportes.", "Falta la firma requerida.", "El documento está vencido."])
            .ShouldBe("Los datos informados no coinciden con los soportes. Falta la firma requerida. El documento está vencido.");
    }

    [Fact]
    public void Un_motivo_sin_punto_final_lo_recibe_y_los_vacios_se_omiten()
    {
        RedactorDeCorreos.UnirMotivos(["  Ilegible  ", "", "¿Es de otra empresa?"]).ShouldBe("Ilegible. ¿Es de otra empresa?");
    }

    private static RedactorDeCorreos Redactor() =>
        new(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection([new("Validasoft:Correo:UrlPublica", "https://valida.test")]).Build());

    [Fact]
    public void El_rechazo_llega_al_tercero_con_los_motivos_tal_como_se_le_comunican()
    {
        var mensaje = Redactor().RedactarResultado(new CorreoDeResultadoPendiente("ana@aceros.co", "Constructora Norte S.A.S.", "NORTE-E-2026-000001",
            false, ["El documento no corresponde a la persona o empresa que se está validando", "Los datos no coinciden."]));

        mensaje.Asunto.ShouldBe("Constructora Norte S.A.S. cerró tu validación NORTE-E-2026-000001 sin aprobarla");
        mensaje.Texto.ShouldContain("Motivos: El documento no corresponde a la persona o empresa que se está validando. Los datos no coinciden.");
        mensaje.Html.ShouldContain("Motivos: El documento no corresponde");
    }

    [Fact]
    public void La_aprobacion_llega_al_tercero_con_el_enlace_para_ver_su_certificado()
    {
        var mensaje = Redactor().RedactarResultado(new CorreoDeResultadoPendiente("ana@aceros.co", "Constructora Norte S.A.S.", "NORTE-E-2026-000001", true, []));

        mensaje.Asunto.ShouldBe("Constructora Norte S.A.S. aprobó tu validación NORTE-E-2026-000001");
        mensaje.Texto.ShouldContain("tu vínculo está activo y ya tienes tu certificado");
        mensaje.Texto.ShouldContain("https://valida.test");
        mensaje.Texto.ShouldNotContain("Motivos");
    }
}
