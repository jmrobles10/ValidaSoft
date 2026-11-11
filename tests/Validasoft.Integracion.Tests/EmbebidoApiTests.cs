using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Alba;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Embebido desde otro dominio (SE-1…SE-6): orígenes de los sistemas integrados con auditoría, <c>frame-ancestors</c>
/// de la app embebible con esos orígenes, endpoint público de orígenes, apps servidas por la API con fallback, CORS
/// opcional y <c>X-Forwarded-For</c> solo desde proxies confiables.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class EmbebidoApiTests(ApiFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private sealed record Escenario(string Admin, Guid TenantId);

    private async Task<Escenario> TenantAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma,
            new CrearTenantDto(slug, "Constructora Demo", "co", "America/Bogota"), 201);
        return new Escenario(tenant.ApiKeyAdministrador.Valor!, tenant.Tenant.Id);
    }

    private async Task<SistemaIntegradoDto> SistemaAsync(Escenario e, string codigo, params string[] origenes)
    {
        using var rsa = RSA.Create(2048);
        return await EnviarAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", e.Admin,
            new CrearSistemaIntegradoDto(codigo, codigo + " del cliente", rsa.ExportSubjectPublicKeyInfoPem(), origenes), 201);
    }

    private static string OrigenUnico(string prefijo) => $"https://{prefijo}-{Guid.NewGuid().ToString("N")[..8]}.cliente.test";

    [Fact]
    public async Task Los_origenes_del_sistema_se_validan_se_normalizan_y_quedan_en_la_auditoria()
    {
        var e = await TenantAsync();
        var sistema = await SistemaAsync(e, "SRM", "HTTPS://SRM.Cliente.test/", "https://srm.cliente.test:443");
        sistema.OrigenesPermitidos.ShouldBe(["https://srm.cliente.test"]);

        foreach (var invalido in new[] { "http://srm.cliente.test", "https://srm.cliente.test/compras", "https://*.cliente.test",
                     "https://srm.cliente.test?x=1", "https://usuario@srm.cliente.test", "srm.cliente.test", "javascript:alert(1)" })
        {
            var respuesta = await EnviarAsync("PUT", $"/api/v1/sistemas/{sistema.Id}/origenes", e.Admin, new FijarOrigenesDto([invalido]), 422);
            (await CodigoAsync(respuesta)).ShouldBe("origen-invalido", invalido);
        }

        // En desarrollo (la prueba corre como Development) se admite http hacia localhost, en cualquier puerto.
        var editado = await EnviarAsync<SistemaIntegradoDto>("PUT", $"/api/v1/sistemas/{sistema.Id}/origenes", e.Admin,
            new FijarOrigenesDto(["https://srm.cliente.test:8443", "http://localhost:5173", "https://bücher.cliente.test"]), 200);
        editado.OrigenesPermitidos.ShouldBe(["https://srm.cliente.test:8443", "http://localhost:5173", "https://xn--bcher-kva.cliente.test"]);

        var demasiados = Enumerable.Range(1, 21).Select(i => $"https://s{i}.cliente.test").ToList();
        (await CodigoAsync(await EnviarAsync("PUT", $"/api/v1/sistemas/{sistema.Id}/origenes", e.Admin, new FijarOrigenesDto(demasiados), 422)))
            .ShouldBe("origenes-excesivos");

        (await EnviarAsync<SistemaIntegradoDto>("GET", $"/api/v1/sistemas/{sistema.Id}", e.Admin, null, 200)).OrigenesPermitidos
            .ShouldBe(editado.OrigenesPermitidos);

        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(e.TenantId.ToString("N"));
        var id = sistema.Id.ToString();
        var registros = await consulta.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "SistemaIntegrado" && r.EntidadId == id)
            .OrderBy(r => r.Fecha).ToListAsync();
        registros.Select(r => r.Accion).ShouldBe([AccionConfiguracion.Creado, AccionConfiguracion.Modificado]);
        JsonSerializer.Serialize(registros[1].Antes).ShouldContain("https://srm.cliente.test\"");
        JsonSerializer.Serialize(registros[1].Despues).ShouldContain("http://localhost:5173");

        // Un sistema desactivado ya no edita sus orígenes.
        await EnviarAsync("POST", $"/api/v1/sistemas/{sistema.Id}/desactivar", e.Admin, new { }, 200);
        (await CodigoAsync(await EnviarAsync("PUT", $"/api/v1/sistemas/{sistema.Id}/origenes", e.Admin, new FijarOrigenesDto([]), 409)))
            .ShouldBe("sistema-inactivo");
    }

    [Fact]
    public async Task Frame_ancestors_y_el_endpoint_publico_traen_los_origenes_registrados_y_cambian_al_editarlos()
    {
        var e = await TenantAsync();
        var primero = OrigenUnico("srm");
        var segundo = OrigenUnico("srm-nuevo");
        var sistema = await SistemaAsync(e, "SRM", primero);

        var pagina = await EnviarAsync("GET", "/app/", null, null, 200);
        var politica = pagina.Context.Response.Headers.ContentSecurityPolicy.ToString();
        politica.ShouldStartWith("frame-ancestors 'self' ");
        politica.ShouldContain(" " + primero);
        (await pagina.ReadAsTextAsync()).ShouldContain("<title>web</title>");

        // La app del tercero solo admite a esos orígenes en /embed/{componente} (IO-5); en el resto de sus rutas sigue siendo 'self'.
        var tercero = await EnviarAsync("GET", "/embed/boveda", null, null, 200);
        (await tercero.ReadAsTextAsync()).ShouldContain("<title>public</title>");
        var politicaDelTercero = tercero.Context.Response.Headers.ContentSecurityPolicy.ToString();
        politicaDelTercero.ShouldStartWith("frame-ancestors 'self' ");
        politicaDelTercero.ShouldContain(" " + primero);
        (await EnviarAsync("GET", "/e/token-de-prueba", null, null, 200)).Context.Response.Headers.ContentSecurityPolicy.ToString()
            .ShouldStartWith("frame-ancestors 'self'; ");

        var publico = await EnviarAsync("GET", "/public/v1/embed/origenes-permitidos", null, null, 200);
        publico.Context.Response.Headers.CacheControl.ToString().ShouldBe("public, max-age=60");
        var lista = await publico.ReadAsJsonAsync<OrigenesEmbebidoDto>();
        lista!.Origenes.ShouldContain(primero);
        lista.VigenciaSegundos.ShouldBe(60);
        // Solo orígenes: ni tenant, ni sistema, ni nombres.
        var cuerpo = await (await EnviarAsync("GET", "/public/v1/embed/origenes-permitidos", null, null, 200)).ReadAsJsonAsync<JsonElement>();
        cuerpo.EnumerateObject().Select(p => p.Name).ShouldBe(["origenes", "vigenciaSegundos"]);
        cuerpo.GetProperty("origenes").EnumerateArray().ShouldAllBe(o => o.ValueKind == JsonValueKind.String);

        await EnviarAsync<SistemaIntegradoDto>("PUT", $"/api/v1/sistemas/{sistema.Id}/origenes", e.Admin, new FijarOrigenesDto([segundo]), 200);
        var tras = (await EnviarAsync("GET", "/app/vinculos", null, null, 200)).Context.Response.Headers.ContentSecurityPolicy.ToString();
        tras.ShouldContain(" " + segundo);
        tras.ShouldNotContain(primero);
        (await (await EnviarAsync("GET", "/public/v1/embed/origenes-permitidos", null, null, 200)).ReadAsJsonAsync<OrigenesEmbebidoDto>())!
            .Origenes.ShouldContain(segundo);

        await EnviarAsync("POST", $"/api/v1/sistemas/{sistema.Id}/desactivar", e.Admin, new { }, 200);
        (await EnviarAsync("GET", "/app/", null, null, 200)).Context.Response.Headers.ContentSecurityPolicy.ToString().ShouldNotContain(segundo);
        (await EnviarAsync("GET", "/embed/boveda", null, null, 200)).Context.Response.Headers.ContentSecurityPolicy.ToString().ShouldNotContain(segundo);
    }

    [Fact]
    public async Task La_API_sirve_las_apps_con_fallback_a_index_html()
    {
        var sinBarra = await EnviarAsync("GET", "/app", null, null, 301);
        sinBarra.Context.Response.Headers.Location.ToString().ShouldBe("/app/");

        var ruta = await EnviarAsync("GET", "/app/expedientes/123", null, null, 200);
        ruta.Context.Response.ContentType.ShouldBe("text/html; charset=utf-8");
        ruta.Context.Response.Headers.CacheControl.ToString().ShouldBe("no-cache");
        (await ruta.ReadAsTextAsync()).ShouldContain("<title>web</title>");

        var asset = await EnviarAsync("GET", "/app/assets/web-abc123.js", null, null, 200);
        (await asset.ReadAsTextAsync()).ShouldBe("console.log('web');");
        asset.Context.Response.Headers.CacheControl.ToString().ShouldContain("immutable");

        // La app del tercero en la raíz (rutas con historial: /e/{token}) solo se deja embeber por Validasoft.
        var tercero = await EnviarAsync("GET", "/e/token-de-prueba", null, null, 200);
        (await tercero.ReadAsTextAsync()).ShouldContain("<title>public</title>");
        tercero.Context.Response.Headers.ContentSecurityPolicy.ToString().ShouldStartWith("frame-ancestors 'self'; ");
        (await (await EnviarAsync("GET", "/", null, null, 200)).ReadAsTextAsync()).ShouldContain("<title>public</title>");
        (await (await EnviarAsync("GET", "/assets/public-abc123.js", null, null, 200)).ReadAsTextAsync()).ShouldBe("console.log('public');");

        // Las rutas de la API no caen en el fallback.
        var api = await EnviarAsync("GET", "/api/v1/no-existe", null, null, null);
        api.Context.Response.StatusCode.ShouldBeOneOf(401, 404);
        (await (await EnviarAsync("GET", "/health/live", null, null, 200)).ReadAsTextAsync()).ShouldContain("vivo");
    }

    [Fact]
    public async Task Cors_solo_responde_a_los_origenes_configurados_y_no_admite_la_api_key()
    {
        var permitido = await EnviarCrudoAsync("OPTIONS", "/api/v1/empresas", null, c =>
        {
            c.Request.Headers.Origin = ApiFixture.OrigenCors;
            c.Request.Headers.AccessControlRequestMethod = "GET";
            c.Request.Headers.AccessControlRequestHeaders = "authorization,x-empresa";
        });
        permitido.Response.StatusCode.ShouldBe(204);
        permitido.Response.Headers.AccessControlAllowOrigin.ToString().ShouldBe(ApiFixture.OrigenCors);
        permitido.Response.Headers.AccessControlAllowCredentials.ToString().ShouldBeEmpty();

        var conApiKey = await EnviarCrudoAsync("OPTIONS", "/api/v1/empresas", null, c =>
        {
            c.Request.Headers.Origin = ApiFixture.OrigenCors;
            c.Request.Headers.AccessControlRequestMethod = "GET";
            c.Request.Headers.AccessControlRequestHeaders = "x-api-key";
        });
        conApiKey.Response.Headers.AccessControlAllowHeaders.ToString().ShouldNotContain("x-api-key", Case.Insensitive);

        var ajeno = await EnviarCrudoAsync("OPTIONS", "/api/v1/empresas", null, c =>
        {
            c.Request.Headers.Origin = "https://atacante.test";
            c.Request.Headers.AccessControlRequestMethod = "GET";
        });
        ajeno.Response.Headers.AccessControlAllowOrigin.ToString().ShouldBeEmpty();

        var simple = await EnviarCrudoAsync("GET", "/health/live", null, c => c.Request.Headers.Origin = ApiFixture.OrigenCors);
        simple.Response.StatusCode.ShouldBe(200);
        simple.Response.Headers.AccessControlAllowOrigin.ToString().ShouldBe(ApiFixture.OrigenCors);
    }

    [Fact]
    public async Task X_Forwarded_For_solo_cuenta_desde_proxies_confiables()
    {
        const string ruta = "/public/v1/embed/origenes-permitidos";
        var cliente = $"198.51.100.{Random.Shared.Next(1, 250)}";

        // Desde el proxy confiable, el límite es por la IP real del cliente.
        for (var i = 0; i < ApiFixture.LimitePublico; i++)
            (await EnviarCrudoAsync("GET", ruta, ApiFixture.ProxyConfiable, c => c.Request.Headers["X-Forwarded-For"] = cliente)).Response.StatusCode.ShouldBe(200);
        (await EnviarCrudoAsync("GET", ruta, ApiFixture.ProxyConfiable, c => c.Request.Headers["X-Forwarded-For"] = cliente)).Response.StatusCode.ShouldBe(429);

        // Otro cliente detrás del mismo proxy no comparte el límite; el mismo cliente por otro proxy confiable (red CIDR), sí.
        (await EnviarCrudoAsync("GET", ruta, ApiFixture.ProxyConfiable, c => c.Request.Headers["X-Forwarded-For"] = "198.51.100.251"))
            .Response.StatusCode.ShouldBe(200);
        (await EnviarCrudoAsync("GET", ruta, "10.99.7.7", c => c.Request.Headers["X-Forwarded-For"] = cliente)).Response.StatusCode.ShouldBe(429);
        // Lo que el cliente antepone no cuenta: solo la última IP, la que agregó el proxy.
        (await EnviarCrudoAsync("GET", ruta, ApiFixture.ProxyConfiable, c => c.Request.Headers["X-Forwarded-For"] = $"6.6.6.6, {cliente}"))
            .Response.StatusCode.ShouldBe(429);

        // Desde una IP que no es proxy, la cabecera se ignora: cambiarla en cada petición no evade el límite.
        var directo = $"192.0.2.{Random.Shared.Next(1, 250)}";
        for (var i = 0; i < ApiFixture.LimitePublico; i++)
        {
            var falsa = $"203.0.113.{i + 1}";
            (await EnviarCrudoAsync("GET", ruta, directo, c => c.Request.Headers["X-Forwarded-For"] = falsa)).Response.StatusCode.ShouldBe(200);
        }

        (await EnviarCrudoAsync("GET", ruta, directo, c => c.Request.Headers["X-Forwarded-For"] = "203.0.113.200")).Response.StatusCode.ShouldBe(429);
    }

    // ---------- utilidades ----------

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<T> EnviarAsync<T>(string metodo, string url, string? apiKey, object? cuerpo, int esperado) =>
        (await (await EnviarAsync(metodo, url, apiKey, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> EnviarAsync(string metodo, string url, string? apiKey, object? cuerpo, int? esperado) =>
        Host.Scenario(s =>
        {
            switch (metodo)
            {
                case "POST":
                    s.Post.Json(cuerpo ?? new { }).ToUrl(url);
                    s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
                    break;
                case "PUT":
                    s.Put.Json(cuerpo!).ToUrl(url);
                    break;
                default:
                    s.Get.Url(url);
                    break;
            }

            if (apiKey is not null)
                s.WithRequestHeader("X-Api-Key", apiKey);
            if (esperado is null)
                s.IgnoreStatusCode();
            else
                s.StatusCodeShouldBe(esperado.Value);
        });

    /// <summary>Petición directa al <c>TestServer</c> para fijar la IP remota (el proxy) o usar OPTIONS.</summary>
    private Task<HttpContext> EnviarCrudoAsync(string metodo, string ruta, string? ipRemota, Action<HttpContext> configurar) =>
        Host.GetTestServer().SendAsync(c =>
        {
            c.Request.Method = metodo;
            c.Request.Path = ruta;
            if (ipRemota is not null)
                c.Connection.RemoteIpAddress = IPAddress.Parse(ipRemota);
            configurar(c);
        });
}
