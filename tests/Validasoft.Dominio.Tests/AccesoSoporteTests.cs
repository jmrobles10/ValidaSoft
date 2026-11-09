using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Dominio.Tests;

/// <summary>Acceso temporal de soporte de la Plataforma (B-6).</summary>
public class AccesoSoporteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(-5));

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void Dura_entre_1_y_72_horas_pide_justificacion_y_expira_solo()
    {
        Codigo(() => AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 0, "Revisar importación", "ana", Ahora)).ShouldBe("duracion-invalida");
        Codigo(() => AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 73, "Revisar importación", "ana", Ahora)).ShouldBe("duracion-invalida");
        Codigo(() => AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 4, "  corto ", "ana", Ahora)).ShouldBe("justificacion-requerida");

        var acceso = AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 24, "  Revisar error al importar vínculos  ", "ana", Ahora);
        acceso.Justificacion.ShouldBe("Revisar error al importar vínculos");
        acceso.ExpiraEn.ShouldBe(Ahora.AddHours(24));
        acceso.VigenteEn(Ahora.AddHours(23.9)).ShouldBeTrue();
        acceso.VigenteEn(Ahora.AddHours(24)).ShouldBeFalse();
    }

    [Fact]
    public void Revocar_corta_el_acceso_y_no_se_revoca_dos_veces_ni_uno_expirado()
    {
        var acceso = AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 2, "Revisar error al importar vínculos", "ana", Ahora);
        acceso.Revocar("beto", Ahora.AddMinutes(30));
        acceso.VigenteEn(Ahora.AddMinutes(31)).ShouldBeFalse();
        acceso.RevocadoPor.ShouldBe("beto");
        Codigo(() => acceso.Revocar("beto", Ahora.AddMinutes(40))).ShouldBe("acceso-no-vigente");

        var expirado = AccesoSoporte.Conceder(Guid.NewGuid(), Guid.NewGuid(), 1, "Revisar error al importar vínculos", "ana", Ahora);
        Codigo(() => expirado.Revocar("beto", Ahora.AddHours(2))).ShouldBe("acceso-no-vigente");
    }
}
