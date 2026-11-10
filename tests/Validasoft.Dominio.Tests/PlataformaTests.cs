using System.Security.Cryptography;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Dominio.Tests;

/// <summary>Sistemas y operadores de Plataforma (PT-4) y la forma de la llave de OpenAI (PT-6).</summary>
public sealed class PlataformaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly string Pem = RSA.Create(2048).ExportSubjectPublicKeyInfoPem();

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void El_sistema_de_plataforma_normaliza_codigo_y_origenes_y_valida_la_clave()
    {
        var sistema = SistemaDePlataforma.Fijar(null, " backoffice ", "Backoffice", Pem, ["https://Admin.Validasoft.co/", "https://admin.validasoft.co"],
            false, Ahora);
        sistema.Codigo.ShouldBe("BACKOFFICE");
        sistema.OrigenesPermitidos.ShouldBe(["https://admin.validasoft.co"]);
        sistema.Activo.ShouldBeTrue();

        Codigo(() => SistemaDePlataforma.Fijar(null, "1MAL", "X", Pem, null, false, Ahora)).ShouldBe("codigo-invalido");
        Codigo(() => SistemaDePlataforma.Fijar(null, "BO", "", Pem, null, false, Ahora)).ShouldBe("nombre-requerido");
        Codigo(() => SistemaDePlataforma.Fijar(null, "BO", "X", "no es pem", null, false, Ahora)).ShouldBe("clave-publica-invalida");
        Codigo(() => SistemaDePlataforma.Fijar(null, "BO", "X", Pem, ["http://admin.validasoft.co"], false, Ahora)).ShouldBe("origen-invalido");
        SistemaDePlataforma.Fijar(null, "BO", "X", Pem, ["http://localhost:5173"], true, Ahora).OrigenesPermitidos.ShouldBe(["http://localhost:5173"]);
    }

    [Fact]
    public void Reemplazar_el_sistema_conserva_su_alta_y_lo_reactiva()
    {
        var sistema = SistemaDePlataforma.Fijar(null, "BO", "Backoffice", Pem, null, false, Ahora);
        sistema.Desactivar(Ahora.AddDays(1));
        sistema.Activo.ShouldBeFalse();
        Codigo(() => sistema.Desactivar(Ahora)).ShouldBe("sistema-inactivo");

        var otraClave = RSA.Create(2048).ExportSubjectPublicKeyInfoPem();
        var reemplazado = SistemaDePlataforma.Fijar(sistema, "BO", "Backoffice 2", otraClave, null, false, Ahora.AddDays(2));
        reemplazado.ShouldBeSameAs(sistema);
        (reemplazado.Activo, reemplazado.Nombre, reemplazado.CreadoEn, reemplazado.ClavePublicaPem).ShouldBe((true, "Backoffice 2", Ahora, otraClave));
    }

    [Fact]
    public void El_operador_se_registra_con_su_sistema_y_el_sistema_es_la_fuente_de_nombre_y_correo()
    {
        var operador = OperadorDePlataforma.Registrar("BO", " jperez ", " Juana Pérez ", "Juana@Validasoft.CO", Ahora);
        (operador.Id, operador.IdExterno, operador.Nombre, operador.Correo).ShouldBe(("BO:jperez", "jperez", "Juana Pérez", "juana@validasoft.co"));

        operador.Acceder("Juana Pérez", "juana@validasoft.co", Ahora.AddHours(1)).ShouldBeFalse();
        operador.Acceder("Juana P. Gómez", "juana@validasoft.co", Ahora.AddHours(2)).ShouldBeTrue();
        operador.UltimoAcceso.ShouldBe(Ahora.AddHours(2));

        operador.Activo = false;
        Codigo(() => operador.Acceder("Juana", "juana@validasoft.co", Ahora)).ShouldBe("operador-inactivo");
        Codigo(() => OperadorDePlataforma.Registrar("BO", new string('x', 129), "X", "x@v.co", Ahora)).ShouldBe("asercion-incompleta");
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("sk-proj con espacios 1234567890")]
    public void Una_llave_de_OpenAI_mal_formada_se_rechaza(string llave) =>
        Codigo(() => PlanDeIaEmpresa.ValidarLlave(llave)).ShouldBe("api-key-invalida");

    [Fact]
    public void Una_llave_bien_formada_se_recorta() =>
        PlanDeIaEmpresa.ValidarLlave("  sk-proj-0123456789abcdefghij  ").ShouldBe("sk-proj-0123456789abcdefghij");
}
