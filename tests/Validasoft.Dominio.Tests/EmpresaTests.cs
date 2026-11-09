using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Dominio.Tests;

/// <summary>Datos y marca de la empresa (P2ConfigEmpresa): contacto de soporte, color de marca y logo.</summary>
public class EmpresaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(-5));

    private static Empresa Nueva() =>
        Empresa.Crear(Guid.NewGuid(), Identificacion.Crear("CO", "NIT", "800197268", "4"), "Constructora Norte S.A.S.", "norte", null, null, Ahora);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void Actualiza_soporte_color_y_logo_y_nulo_conserva_lo_que_habia()
    {
        var empresa = Nueva();
        var png = "data:image/png;base64," + Convert.ToBase64String(new byte[1024]);

        empresa.ActualizarMarca(" proveedores@norte.example ", "#e07a1f", png);
        empresa.ReplyTo.ShouldBe("proveedores@norte.example");
        empresa.ColorMarca.ShouldBe("#E07A1F");
        empresa.LogoUrl.ShouldBe(png);

        empresa.ActualizarMarca(null, null, null);
        empresa.ColorMarca.ShouldBe("#E07A1F");

        // Cadena vacía quita.
        empresa.ActualizarMarca("", "", "");
        empresa.ReplyTo.ShouldBeNull();
        empresa.ColorMarca.ShouldBeNull();
        empresa.LogoUrl.ShouldBeNull();

        empresa.ActualizarMarca(null, null, "https://cdn.norte.example/logo.svg");
        empresa.LogoUrl.ShouldBe("https://cdn.norte.example/logo.svg");
    }

    [Fact]
    public void Rechaza_correo_color_y_logo_invalidos()
    {
        var empresa = Nueva();
        Codigo(() => empresa.ActualizarMarca("no-es-correo", null, null)).ShouldBe("contacto-soporte-invalido");
        Codigo(() => empresa.ActualizarMarca(null, "naranja", null)).ShouldBe("color-marca-invalido");
        Codigo(() => empresa.ActualizarMarca(null, "#FFF", null)).ShouldBe("color-marca-invalido");
        Codigo(() => empresa.ActualizarMarca(null, null, "http://inseguro.example/logo.png")).ShouldBe("logo-invalido");
        Codigo(() => empresa.ActualizarMarca(null, null, "data:image/jpeg;base64,AAAA")).ShouldBe("logo-invalido");
        var grande = "data:image/png;base64," + Convert.ToBase64String(new byte[Empresa.TamanoMaximoLogo + 3]);
        Codigo(() => empresa.ActualizarMarca(null, null, grande)).ShouldBe("logo-demasiado-grande");
    }
}
