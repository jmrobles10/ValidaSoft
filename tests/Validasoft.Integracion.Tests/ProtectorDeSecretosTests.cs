using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Clave maestra de los secretos en reposo (PT-1, PT-2), sin base ni red: versión (kid) en el texto cifrado, rotación (la
/// actual cifra, las anteriores descifran), compatibilidad con el formato derivado de la clave de sesiones y configuración
/// inválida.
/// </summary>
public sealed class ProtectorDeSecretosTests
{
    private const string FirmaDeSesiones = "dev-sesiones-solo-local-cambiar-000000000001";
    private static readonly string Clave1 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    private static readonly string Clave2 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static IProtectorDeSecretos Protector(string? maestra, string[]? anteriores = null, string? firma = FirmaDeSesiones, string[]? firmasHeredadas = null)
    {
        var valores = new Dictionary<string, string?> { ["Validasoft:Secretos:ClaveMaestra"] = maestra, ["Validasoft:Sesiones:ClaveFirma"] = firma };
        foreach (var (clave, i) in (anteriores ?? []).Select((c, i) => (c, i)))
            valores[$"Validasoft:Secretos:ClavesAnteriores:{i}"] = clave;
        foreach (var (clave, i) in (firmasHeredadas ?? []).Select((c, i) => (c, i)))
            valores[$"Validasoft:Secretos:ClavesFirmaHeredadas:{i}"] = clave;
        return new ProtectorDeSecretos(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
    }

    private static string Kid(string base64) => ClavesDeSecretos.Kid(Convert.FromBase64String(base64));

    [Fact]
    public void Cifra_con_la_clave_maestra_y_su_version_en_el_texto()
    {
        var protector = Protector(Clave1);
        var a = protector.Proteger("sk-proj-secreta-1234");
        var b = protector.Proteger("sk-proj-secreta-1234");

        a.ShouldStartWith($"vs1.{Kid(Clave1)}.");
        a.ShouldNotBe(b); // nonce aleatorio
        a.ShouldNotContain("secreta");
        protector.Desproteger(a).ShouldBe("sk-proj-secreta-1234");
        protector.EstaConClaveActual(a).ShouldBeTrue();
        protector.ClaveVigente.ShouldBe(Kid(Clave1));
        protector.ClavesAnteriores.ShouldBeEmpty();
    }

    [Fact]
    public void Al_rotar_la_anterior_descifra_y_recifrar_pasa_a_la_nueva()
    {
        var cifrado = Protector(Clave1).Proteger("whsec_123");
        var rotado = Protector(Clave2, [Clave1]);

        rotado.Desproteger(cifrado).ShouldBe("whsec_123");
        rotado.EstaConClaveActual(cifrado).ShouldBeFalse();
        rotado.ClavesAnteriores.ShouldBe([Kid(Clave1)]);

        var nuevo = rotado.Recifrar(cifrado);
        nuevo.ShouldStartWith($"vs1.{Kid(Clave2)}.");
        rotado.Recifrar(nuevo).ShouldBe(nuevo); // ya vigente: igual
        Protector(Clave2).Desproteger(nuevo).ShouldBe("whsec_123");

        // Sin la anterior configurada, lo viejo ya no se lee (y el error dice qué clave falta, nunca el secreto).
        var error = Should.Throw<CryptographicException>(() => Protector(Clave2).Desproteger(cifrado));
        error.Message.ShouldContain(Kid(Clave1));
    }

    [Fact]
    public void Cambiar_la_version_del_texto_invalida_la_etiqueta()
    {
        var cifrado = Protector(Clave1).Proteger("1234567890");
        var partes = cifrado.Split('.');
        var manipulado = $"vs1.{Kid(Clave2)}.{partes[2]}";
        Should.Throw<CryptographicException>(() => Protector(Clave2, [Clave1]).Desproteger(manipulado));
    }

    [Fact]
    public void Lo_cifrado_antes_con_la_clave_de_sesiones_se_sigue_leyendo_aun_rotando_esa_clave()
    {
        var antiguo = PlataformaApiTests.CifrarComoAntes("token-del-outbox", FirmaDeSesiones);

        var protector = Protector(Clave1);
        protector.Desproteger(antiguo).ShouldBe("token-del-outbox");
        protector.EstaConClaveActual(antiguo).ShouldBeFalse();
        protector.Recifrar(antiguo).ShouldStartWith($"vs1.{Kid(Clave1)}.");

        // Se rotó la clave de sesiones: con la anterior en ClavesFirmaHeredadas se sigue leyendo; sin ella, no.
        const string nuevaFirma = "otra-clave-de-sesiones-de-al-menos-32-bytes-01";
        Protector(Clave1, firma: nuevaFirma, firmasHeredadas: [FirmaDeSesiones]).Desproteger(antiguo).ShouldBe("token-del-outbox");
        Should.Throw<CryptographicException>(() => Protector(Clave1, firma: nuevaFirma).Desproteger(antiguo));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-base64!")]
    [InlineData("AAAA")]
    public void Sin_clave_maestra_valida_no_cifra(string? maestra)
    {
        var error = Should.Throw<InvalidOperationException>(() => Protector(maestra).Proteger("x"));
        error.Message.ShouldContain("Validasoft:Secretos:ClaveMaestra");
    }
}
