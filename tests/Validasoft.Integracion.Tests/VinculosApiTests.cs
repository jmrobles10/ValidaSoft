using System.Net.Http.Json;
using Alba;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>Levanta PostgreSQL 17 en Docker (Testcontainers) y la API completa en memoria (Alba).</summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string ClavePlataforma = "dev-plataforma-solo-local-cambiar-0001";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Host = await AlbaHost.For<Program>(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Validasoft", _postgres.GetConnectionString());
        });
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("api")]
public sealed class ColeccionApi : ICollectionFixture<ApiFixture>;

[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class VinculosApiTests(ApiFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private sealed record Escenario(string ApiKey, EmpresaDto EmpresaA, EmpresaDto EmpresaB);

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await PostAsync<TenantCreadoDto>("/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Constructora Demo", "co", "America/Bogota"));
        var clave = tenant.ApiKeyAdministrador.Valor!;

        var a = await PostAsync<EmpresaDto>("/api/v1/empresas", clave, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null));
        var b = await PostAsync<EmpresaDto>("/api/v1/empresas", clave, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null));
        return new Escenario(clave, a, b);
    }

    private static CrearVinculoDto Proveedor(string nit) => new(
        TerceroId: null,
        Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Aceros del Norte S.A.S.", null,
            [new ContactoDto("Ana Gómez", "ana@aceros.co", null, "Comercial")]),
        TipoVinculo: "PROVEEDOR",
        Atributos: null,
        CorreoInvitacion: "ana@aceros.co",
        ReferenciaExterna: new ReferenciaExternaDto("SRM", "ADP-00123"));

    [Fact]
    public async Task Crea_un_vinculo_con_identificador_invitacion_e_historial()
    {
        var e = await PrepararAsync();

        var vinculo = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900123456"));

        vinculo.Id.ShouldNotBe(Guid.Empty);
        vinculo.Estado.ShouldBe("Invitado");
        vinculo.TipoVinculo.Codigo.ShouldBe("PROVEEDOR");
        vinculo.Invitacion!.Envios.ShouldBe(1);

        var leido = await GetAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}", e.ApiKey, e.EmpresaA.Id);
        leido.Id.ShouldBe(vinculo.Id);

        var historial = await GetAsync<List<EventoHistorialDto>>($"/api/v1/vinculos/{vinculo.Id}/historial", e.ApiKey, e.EmpresaA.Id);
        historial.Select(h => h.Tipo).ShouldBe(["vinculo_creado", "invitacion_enviada"]);
        historial[0].ActorTipo.ShouldBe("Integracion");
    }

    [Fact]
    public async Task No_permite_dos_vinculos_del_mismo_tipo_con_el_mismo_tercero_en_la_empresa()
    {
        var e = await PrepararAsync();
        await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900555111"));

        var respuesta = await Host.Scenario(s =>
        {
            s.Post.Json(Proveedor("900555111")).ToUrl("/api/v1/vinculos");
            Cabeceras(s, e.ApiKey, e.EmpresaA.Id, Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(409);
        });
        (await respuesta.ReadAsTextAsync()).ShouldContain("vinculo-duplicado");
    }

    [Fact]
    public async Task Otra_empresa_del_mismo_tenant_no_ve_el_vinculo_y_puede_crear_el_suyo()
    {
        var e = await PrepararAsync();
        var deA = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900777222"));

        await Host.Scenario(s =>
        {
            s.Get.Url($"/api/v1/vinculos/{deA.Id}");
            Cabeceras(s, e.ApiKey, e.EmpresaB.Id);
            s.StatusCodeShouldBe(404);
        });

        var deB = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaB.Id, Proveedor("900777222"));
        deB.TerceroId.ShouldBe(deA.TerceroId); // mismo tercero en la bóveda
        deB.Id.ShouldNotBe(deA.Id);            // vínculos distintos por empresa
    }

    [Fact]
    public async Task Idempotency_key_repite_la_respuesta_y_rechaza_otro_cuerpo()
    {
        var e = await PrepararAsync();
        var clave = Guid.NewGuid().ToString();

        var primero = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900888333"), clave);
        var repetido = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900888333"), clave);
        repetido.Id.ShouldBe(primero.Id);

        await Host.Scenario(s =>
        {
            s.Post.Json(Proveedor("900888444")).ToUrl("/api/v1/vinculos");
            Cabeceras(s, e.ApiKey, e.EmpresaA.Id, clave);
            s.StatusCodeShouldBe(422);
        });
    }

    [Fact]
    public async Task Bloquear_y_desbloquear_restituye_el_estado()
    {
        var e = await PrepararAsync();
        var vinculo = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900999444"));

        var bloqueado = await PostAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}/bloquear", e.ApiKey, e.EmpresaA.Id,
            new BloquearVinculoDto("Sanción contractual", null), esperado: 200);
        bloqueado.Estado.ShouldBe("Bloqueado");

        var desbloqueado = await PostAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}/desbloquear", e.ApiKey, e.EmpresaA.Id,
            new JustificacionDto("Sanción levantada"), esperado: 200);
        desbloqueado.Estado.ShouldBe("Invitado");
    }

    [Fact]
    public async Task Sin_credencial_responde_401() =>
        await Host.Scenario(s =>
        {
            s.Get.Url("/api/v1/vinculos");
            s.StatusCodeShouldBe(401);
        });

    // ---------- utilidades ----------

    private async Task<T> PostAsync<T>(string url, string apiKey, Guid? empresa, object cuerpo, string? idempotencia = null, int esperado = 201)
    {
        var resultado = await Host.Scenario(s =>
        {
            s.Post.Json(cuerpo).ToUrl(url);
            Cabeceras(s, apiKey, empresa, idempotencia ?? Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });
        return (await resultado.ReadAsJsonAsync<T>())!;
    }

    private async Task<T> GetAsync<T>(string url, string apiKey, Guid? empresa)
    {
        var resultado = await Host.Scenario(s =>
        {
            s.Get.Url(url);
            Cabeceras(s, apiKey, empresa);
            s.StatusCodeShouldBeOk();
        });
        return (await resultado.ReadAsJsonAsync<T>())!;
    }

    private static void Cabeceras(Scenario s, string apiKey, Guid? empresa, string? idempotencia = null)
    {
        s.WithRequestHeader("X-Api-Key", apiKey);
        if (empresa is not null)
            s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
        if (idempotencia is not null)
            s.WithRequestHeader("Idempotency-Key", idempotencia);
    }
}
