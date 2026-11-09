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
}
