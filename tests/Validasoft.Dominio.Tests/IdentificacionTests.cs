using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Tests;

public class IdentificacionTests
{
    [Theory]
    [InlineData("800197268", "4")] // DIAN
    [InlineData("899999068", "1")] // Ecopetrol
    public void Calcula_el_digito_de_verificacion_del_NIT(string nit, string dv) =>
        DigitoVerificacion.NitColombia(nit).ShouldBe(dv);

    [Fact]
    public void Calcula_el_digito_de_verificacion_del_RUT_chileno() =>
        DigitoVerificacion.RutChile("12345678").ShouldBe("5");

    [Theory]
    [InlineData("800.197.268-4")]
    [InlineData("800197268-4")]
    [InlineData(" 800 197 268 ")]
    public void Normaliza_el_NIT_sin_digito_de_verificacion_en_la_clave(string numero)
    {
        var id = Identificacion.Crear("co", "nit", numero);

        id.Numero.ShouldBe("800197268");
        id.DigitoVerificacion.ShouldBe("4");
        id.Clave.ShouldBe("CO|NIT|800197268");
    }

    [Fact]
    public void Rechaza_un_digito_de_verificacion_que_no_corresponde()
    {
        var error = Should.Throw<ReglaDeNegocioException>(() => Identificacion.Crear("CO", "NIT", "800197268", "9"));
        error.Codigo.ShouldBe("digito-verificacion-invalido");
    }

    [Fact]
    public void Rechaza_un_dv_en_el_numero_distinto_al_informado_aparte()
    {
        var error = Should.Throw<ReglaDeNegocioException>(() => Identificacion.Crear("CO", "NIT", "800197268-4", "5"));
        error.Codigo.ShouldBe("digito-verificacion-invalido");
    }

    [Fact]
    public void La_cedula_no_lleva_digito_de_verificacion()
    {
        var id = Identificacion.Crear("CO", "CC", "1.020.304.050");

        id.Numero.ShouldBe("1020304050");
        id.DigitoVerificacion.ShouldBeNull();
    }

    [Fact]
    public void Un_pais_sin_catalogo_propio_admite_pasaporte_e_id_fiscal()
    {
        CatalogoIdentificacion.TiposDe("DE").Select(t => t.Codigo).ShouldBe(["PASAPORTE", "ID_FISCAL"]);
        Identificacion.Crear("DE", "PASAPORTE", "c01x00t47").Numero.ShouldBe("C01X00T47");
    }

    [Theory]
    [InlineData("COL", "NIT", "pais-invalido")]
    [InlineData("CO", "RFC", "tipo-identificacion-invalido")]
    [InlineData("CO", "CC", "12", "numero-identificacion-invalido")]
    public void Rechaza_datos_invalidos(string pais, string tipo, string numeroOCodigo, string? codigo = null)
    {
        var numero = codigo is null ? "800197268" : numeroOCodigo;
        var esperado = codigo ?? numeroOCodigo;

        Should.Throw<ReglaDeNegocioException>(() => Identificacion.Crear(pais, tipo, numero)).Codigo.ShouldBe(esperado);
    }
}
