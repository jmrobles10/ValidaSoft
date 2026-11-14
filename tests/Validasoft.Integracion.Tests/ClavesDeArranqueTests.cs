using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Infraestructura.Certificados;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Barrera de las claves de desarrollo (CL-1) y arranque con las claves de sellado retiradas (CL-2), sin base ni red: fuera de
/// <c>Development</c> el proceso no arranca con las claves publicadas en el repositorio ni sin las obligatorias, y la lista de
/// valores conocidos sigue a <c>appsettings.Development.json</c> y al compose.
/// </summary>
public sealed partial class ClavesDeArranqueTests
{
    private static string Aleatoria() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static IConfiguration Config(Dictionary<string, string?> valores) => new ConfigurationBuilder().AddInMemoryCollection(valores).Build();

    /// <summary>Claves propias válidas: lo que debe tener un despliegue real.</summary>
    private static Dictionary<string, string?> Propias() => new()
    {
        [ClavesDeArranque.ClaveFirma] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
        [ClavesDeArranque.ClaveBootstrap] = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)),
        [ClavesDeArranque.ClaveMaestra] = Aleatoria(),
        [ClavesDeArranque.ClavePrivada] = Aleatoria()
    };

    /// <summary>Las cuatro claves tal como están publicadas en <c>appsettings.Development.json</c>.</summary>
    private static Dictionary<string, string?> DeDesarrollo()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Ruta("src/Validasoft.Api/appsettings.Development.json")));
        var validasoft = json.RootElement.GetProperty("Validasoft");
        return new Dictionary<string, string?>
        {
            [ClavesDeArranque.ClaveFirma] = validasoft.GetProperty("Sesiones").GetProperty("ClaveFirma").GetString(),
            [ClavesDeArranque.ClaveBootstrap] = validasoft.GetProperty("Plataforma").GetProperty("ClaveBootstrap").GetString(),
            [ClavesDeArranque.ClaveMaestra] = validasoft.GetProperty("Secretos").GetProperty("ClaveMaestra").GetString(),
            [ClavesDeArranque.ClavePrivada] = validasoft.GetProperty("Certificados").GetProperty("ClavePrivada").GetString()
        };
    }

    private static string Ruta(string relativa)
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "Validasoft.slnx")))
            carpeta = carpeta.Parent;
        return Path.Combine((carpeta ?? throw new InvalidOperationException("No se encontró la raíz del repositorio.")).FullName, relativa);
    }

    private sealed class Entorno(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Validasoft.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    [Fact]
    public void En_Development_las_claves_publicadas_se_aceptan()
    {
        ClavesDeArranque.Problemas(Config(DeDesarrollo()), new Entorno("Development")).ShouldBeEmpty();
        ClavesDeArranque.Problemas(Config([]), new Entorno("development")).ShouldBeEmpty(); // ni siquiera exige que existan
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("Testing")]
    [InlineData("")]
    public void Fuera_de_Development_las_cuatro_claves_de_desarrollo_impiden_arrancar(string entorno)
    {
        var problemas = ClavesDeArranque.Problemas(Config(DeDesarrollo()), new Entorno(entorno));

        problemas.Count.ShouldBe(4);
        foreach (var clave in new[] { ClavesDeArranque.ClaveFirma, ClavesDeArranque.ClaveBootstrap, ClavesDeArranque.ClaveMaestra, ClavesDeArranque.ClavePrivada })
            problemas.ShouldContain(p => p.StartsWith(clave, StringComparison.Ordinal) && p.Contains("desarrollo conocido", StringComparison.Ordinal));
    }

    [Fact]
    public void Exigir_lanza_con_todos_los_problemas_sin_mostrar_los_valores()
    {
        var valores = DeDesarrollo();
        var error = Should.Throw<InvalidOperationException>(() => ClavesDeArranque.Exigir(Config(valores), new Entorno("Production")));

        error.Message.ShouldContain("Production");
        error.Message.ShouldContain("variables-de-entorno.md");
        foreach (var clave in valores.Keys)
            error.Message.ShouldContain(clave);
        foreach (var valor in valores.Values)
            error.Message.ShouldNotContain(valor!); // el mensaje nombra la clave de configuración, nunca su valor
    }

    [Fact]
    public void Fuera_de_Development_las_claves_propias_y_validas_arrancan()
    {
        ClavesDeArranque.Problemas(Config(Propias()), new Entorno("Production")).ShouldBeEmpty();
        Should.NotThrow(() => ClavesDeArranque.Exigir(Config(Propias()), new Entorno("Production")));
    }

    [Fact]
    public void Fuera_de_Development_faltan_las_obligatorias_pero_la_de_arranque_de_Plataforma_es_opcional()
    {
        var problemas = ClavesDeArranque.Problemas(Config([]), new Entorno("Production"));

        problemas.Count.ShouldBe(3);
        problemas.ShouldContain(p => p.StartsWith(ClavesDeArranque.ClaveFirma, StringComparison.Ordinal) && p.Contains("falta", StringComparison.Ordinal));
        problemas.ShouldContain(p => p.StartsWith(ClavesDeArranque.ClaveMaestra, StringComparison.Ordinal));
        problemas.ShouldContain(p => p.StartsWith(ClavesDeArranque.ClavePrivada, StringComparison.Ordinal));
        problemas.ShouldNotContain(p => p.Contains("ClaveBootstrap", StringComparison.Ordinal));

        var sinBootstrap = Propias();
        sinBootstrap.Remove(ClavesDeArranque.ClaveBootstrap);
        ClavesDeArranque.Problemas(Config(sinBootstrap), new Entorno("Production")).ShouldBeEmpty();
    }

    [Fact]
    public void Una_clave_de_firma_corta_no_arranca()
    {
        var valores = Propias();
        valores[ClavesDeArranque.ClaveFirma] = "demasiado-corta";

        ClavesDeArranque.Problemas(Config(valores), new Entorno("Production")).ShouldHaveSingleItem().ShouldContain("32 bytes");
    }

    [Theory]
    [InlineData("Validasoft:Sesiones:ClaveFirma", "otra-clave-solo-local-que-alguien-copio-del-repo")]
    [InlineData("Validasoft:Plataforma:ClaveBootstrap", "OTRA CLAVE SOLO LOCAL de pruebas con más de 32")]
    public void El_rotulo_solo_local_delata_una_clave_de_desarrollo_aunque_su_valor_no_sea_el_publicado(string clave, string valor)
    {
        var valores = Propias();
        valores[clave] = valor;

        ClavesDeArranque.Problemas(Config(valores), new Entorno("Production")).ShouldHaveSingleItem().ShouldStartWith(clave);
    }

    [Fact]
    public void Las_claves_en_base64_se_reconocen_aunque_traigan_espacios_o_saltos_de_linea()
    {
        var conocidas = DeDesarrollo();
        var valores = Propias();
        valores[ClavesDeArranque.ClaveMaestra] = "  " + conocidas[ClavesDeArranque.ClaveMaestra] + "\n"; // un secreto leído de archivo
        var privada = conocidas[ClavesDeArranque.ClavePrivada]!;
        valores[ClavesDeArranque.ClavePrivada] = privada[..20] + "\r\n" + privada[20..]; // base64 partido en líneas: los mismos bytes

        ClavesDeArranque.Problemas(Config(valores), new Entorno("Production")).Count.ShouldBe(2);
    }

    [Fact]
    public async Task El_servicio_de_arranque_falla_fuera_de_Development_y_no_en_Development()
    {
        var produccion = new VerificacionDeClavesDeArranque(Config(DeDesarrollo()), new Entorno("Production"));
        var error = await Should.ThrowAsync<InvalidOperationException>(() => produccion.StartAsync(CancellationToken.None));
        error.Message.ShouldContain("desarrollo conocido");

        await new VerificacionDeClavesDeArranque(Config(DeDesarrollo()), new Entorno("Development")).StartAsync(CancellationToken.None);
        await new VerificacionDeClavesDeArranque(Config(Propias()), new Entorno("Production")).StartAsync(CancellationToken.None);
    }

    // --- La lista de valores conocidos sigue a lo que está publicado en el repositorio ---

    [Fact]
    public void Los_valores_de_appsettings_Development_estan_en_la_lista_de_conocidos()
    {
        foreach (var (clave, valor) in DeDesarrollo())
        {
            valor.ShouldNotBeNullOrWhiteSpace();
            ClavesDeArranque.EsDeDesarrollo(clave, valor).ShouldBeTrue($"{clave} de appsettings.Development.json no se reconoce como clave de desarrollo");
            ClavesDeArranque.ConocidasDeDesarrollo[clave].ShouldContain(valor);
        }
    }

    [Fact]
    public void Los_valores_del_compose_estan_en_la_lista_de_conocidos()
    {
        var compose = File.ReadAllText(Ruta("infra/compose/compose.yaml"));
        var claves = new Dictionary<string, string>
        {
            ["Validasoft__Sesiones__ClaveFirma"] = ClavesDeArranque.ClaveFirma,
            ["Validasoft__Plataforma__ClaveBootstrap"] = ClavesDeArranque.ClaveBootstrap,
            ["Validasoft__Secretos__ClaveMaestra"] = ClavesDeArranque.ClaveMaestra,
            ["Validasoft__Certificados__ClavePrivada"] = ClavesDeArranque.ClavePrivada
        };

        var encontradas = 0;
        foreach (Match m in VariableDelCompose().Matches(compose))
        {
            if (!claves.TryGetValue(m.Groups["nombre"].Value, out var clave))
                continue;
            encontradas++;
            ClavesDeArranque.EsDeDesarrollo(clave, m.Groups["valor"].Value).ShouldBeTrue($"{m.Groups["nombre"].Value} del compose no se reconoce como clave de desarrollo");
        }

        encontradas.ShouldBeGreaterThanOrEqualTo(3); // firma, arranque y maestra: la semilla de los certificados llega de appsettings.Development.json
    }

    [Fact]
    public void Las_claves_de_appsettings_base_estan_vacias()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Ruta("src/Validasoft.Api/appsettings.json")));
        var validasoft = json.RootElement.GetProperty("Validasoft");
        validasoft.GetProperty("Sesiones").GetProperty("ClaveFirma").GetString().ShouldBeEmpty();
        validasoft.GetProperty("Plataforma").GetProperty("ClaveBootstrap").GetString().ShouldBeEmpty();
        validasoft.GetProperty("Secretos").GetProperty("ClaveMaestra").GetString().ShouldBeEmpty();
        validasoft.GetProperty("Certificados").GetProperty("ClavePrivada").GetString().ShouldBeEmpty();
    }

    [GeneratedRegex("""^\s*(?<nombre>Validasoft__[A-Za-z_]+):\s*"(?<valor>[^"]*)"\s*$""", RegexOptions.Multiline)]
    private static partial Regex VariableDelCompose();

    // --- Arranque con las claves de sellado retiradas (CL-2) ---

    private static ClavesDeCertificados ConRetirada(string retiradaEn)
    {
        var valores = new Dictionary<string, string?>
        {
            ["Validasoft:Certificados:ClavePrivada"] = Convert.ToBase64String(SelloEd25519.GenerarSemilla()),
            ["Validasoft:Certificados:ClavesAnteriores:0:Kid"] = "vs-2026",
            ["Validasoft:Certificados:ClavesAnteriores:0:ClavePublica"] = Convert.ToBase64String(SelloEd25519.ClavePublica(SelloEd25519.GenerarSemilla())),
            ["Validasoft:Certificados:ClavesAnteriores:0:RetiradaEn"] = retiradaEn
        };
        return new ClavesDeCertificados(Config(valores));
    }

    private static readonly DateTimeOffset Hoy = new(2027, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task El_arranque_de_certificados_acepta_una_retirada_pasada_y_rechaza_una_en_el_futuro()
    {
        await new VerificacionDeClavesDeCertificados(ConRetirada("2027-01-15T00:00:00Z"), new RelojFijo(Hoy)).StartAsync(CancellationToken.None);
        // Un día de holgura por el reloj o la hora local; más allá, la clave tendría un plazo sin fin.
        await new VerificacionDeClavesDeCertificados(ConRetirada("2027-03-02T00:00:00Z"), new RelojFijo(Hoy)).StartAsync(CancellationToken.None);

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            new VerificacionDeClavesDeCertificados(ConRetirada("2027-04-01T00:00:00Z"), new RelojFijo(Hoy)).StartAsync(CancellationToken.None));
        error.Message.ShouldContain("vs-2026");
        error.Message.ShouldContain("RetiradaEn está en el futuro");
    }

    [Fact]
    public async Task El_arranque_de_certificados_falla_si_una_retirada_no_trae_su_fecha()
    {
        var sinFecha = ConRetirada("");

        var error = await Should.ThrowAsync<InvalidOperationException>(() =>
            new VerificacionDeClavesDeCertificados(sinFecha, new RelojFijo(Hoy)).StartAsync(CancellationToken.None));
        error.Message.ShouldContain("RetiradaEn falta");
    }
}
