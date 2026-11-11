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

    private static readonly Guid GrupoNorte = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid GrupoSur = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Las_credenciales_de_un_grupo_se_cifran_con_la_clave_de_ese_grupo_y_no_las_abre_otro()
    {
        var protector = Protector(Clave1);

        var norte = protector.ProtegerDeGrupo(GrupoNorte, "clave-de-infolaft-9876");
        var otra = protector.ProtegerDeGrupo(GrupoNorte, "clave-de-infolaft-9876");

        norte.ShouldStartWith($"vs2.{Kid(Clave1)}.");
        norte.ShouldNotContain("infolaft");
        norte.ShouldNotBe(otra); // nonce aleatorio
        protector.DesprotegerDeGrupo(GrupoNorte, norte).ShouldBe("clave-de-infolaft-9876");
        protector.EstaCifradoParaGrupoConClaveActual(norte).ShouldBeTrue();
        // Copiar el texto cifrado a otro grupo no sirve: la clave se deriva del id del grupo.
        Should.Throw<CryptographicException>(() => protector.DesprotegerDeGrupo(GrupoSur, norte));
        // Tampoco el protector general lo lee: no es un secreto de la instalación.
        Should.Throw<CryptographicException>(() => protector.Desproteger(norte));
    }

    [Fact]
    public void Lo_guardado_con_la_clave_de_la_instalacion_se_lee_y_pasa_a_la_del_grupo_al_recifrar()
    {
        var protector = Protector(Clave1);
        var deLaInstalacion = protector.Proteger("clave-antigua-1234");

        protector.DesprotegerDeGrupo(GrupoNorte, deLaInstalacion).ShouldBe("clave-antigua-1234");
        protector.EstaCifradoParaGrupoConClaveActual(deLaInstalacion).ShouldBeFalse();

        var delGrupo = protector.RecifrarDeGrupo(GrupoNorte, deLaInstalacion);
        delGrupo.ShouldStartWith("vs2.");
        protector.DesprotegerDeGrupo(GrupoNorte, delGrupo).ShouldBe("clave-antigua-1234");
        protector.RecifrarDeGrupo(GrupoNorte, delGrupo).ShouldBe(delGrupo); // ya vigente: igual
    }

    [Fact]
    public void Al_rotar_la_clave_maestra_la_del_grupo_cambia_y_la_anterior_sigue_descifrando()
    {
        var antes = Protector(Clave1).ProtegerDeGrupo(GrupoSur, "usuario-y-clave-5555");
        var rotado = Protector(Clave2, [Clave1]);

        rotado.DesprotegerDeGrupo(GrupoSur, antes).ShouldBe("usuario-y-clave-5555");
        rotado.EstaCifradoParaGrupoConClaveActual(antes).ShouldBeFalse();
        var nuevo = rotado.RecifrarDeGrupo(GrupoSur, antes);
        nuevo.ShouldStartWith($"vs2.{Kid(Clave2)}.");
        Protector(Clave2).DesprotegerDeGrupo(GrupoSur, nuevo).ShouldBe("usuario-y-clave-5555");
        // Sin la clave anterior configurada, el error dice cuál falta y nunca el secreto.
        Should.Throw<CryptographicException>(() => Protector(Clave2).DesprotegerDeGrupo(GrupoSur, antes)).Message.ShouldContain(Kid(Clave1));
    }

    [Fact]
    public void Un_texto_de_grupo_manipulado_no_se_acepta()
    {
        var protector = Protector(Clave1);
        var cifrado = protector.ProtegerDeGrupo(GrupoNorte, "1234567890");
        var partes = cifrado.Split('.');

        Should.Throw<CryptographicException>(() => protector.DesprotegerDeGrupo(GrupoNorte, $"vs2.{Kid(Clave2)}.{partes[2]}"));
        Should.Throw<CryptographicException>(() => protector.DesprotegerDeGrupo(GrupoNorte, "vs2.corto"));
        // Se altera el primer carácter del cuerpo (el nonce): la etiqueta ya no cuadra siempre, sin depender de cómo terminaba el texto.
        var alterado = (partes[2][0] == 'A' ? 'B' : 'A') + partes[2][1..];
        Should.Throw<CryptographicException>(() => protector.DesprotegerDeGrupo(GrupoNorte, $"vs2.{partes[1]}.{alterado}"));
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
