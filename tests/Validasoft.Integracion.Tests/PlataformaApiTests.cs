using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Ia;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Consola de Plataforma (PT-1…PT-8): operadores por identidad delegada sin tenant, alta de un grupo operable de punta a
/// punta, plan de IA con su llave de solo escritura (nunca vuelve en claro ni va a la auditoría), «Probar llave» y
/// re-cifrado de los secretos con la clave maestra.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class PlataformaApiTests(ApiFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private sealed record Credencial(string? ApiKey, Guid? Empresa = null, Func<string>? Delegado = null, string? Bearer = null);

    private static readonly Credencial Bootstrap = new(ApiFixture.ClavePlataforma);

    /// <summary>Registra un sistema de Plataforma nuevo (código único por prueba) y abre la sesión de un operador.</summary>
    private async Task<(string Token, string Codigo, RSA Clave)> SesionDeOperadorAsync(string sub = "op-1", string nombre = "Paula Plataforma")
    {
        var codigo = "BO" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var clave = RSA.Create(2048);
        await EnviarAsync<SistemaDePlataformaDto>("PUT", $"/api/v1/plataforma/sistemas/{codigo}", Bootstrap,
            new FijarSistemaDePlataformaDto("Backoffice de Plataforma", clave.ExportSubjectPublicKeyInfoPem(), ["https://backoffice.validasoft.test"]),
            esperado: 200);
        var sesion = await EnviarAsync<SesionUsuarioDto>("POST", "/api/v1/plataforma/sesiones",
            Bootstrap with { Delegado = () => Asercion(codigo, clave, sub, nombre, $"{sub}@validasoft.co") }, esperado: 200);
        return (sesion.Token, codigo, clave);
    }

    private static string Asercion(string emisor, RSA clave, string sub, string nombre, string correo)
    {
        var ahora = DateTime.UtcNow;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emisor,
            Audience = "validasoft",
            Subject = new ClaimsIdentity([new("sub", sub), new("name", nombre), new("email", correo), new("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(clave), SecurityAlgorithms.RsaSha256)
        });
    }

    private static CrearTenantDto AltaCompleta(string slug, RSA claveSistema, string correoAdmin) => new(
        slug, "Grupo Andino", "co", "America/Bogota",
        new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Andina de Obras S.A.S.", "ANDINA", null, "soporte@andina.co"),
        new AdministradorInicialDto("Camila Admin", correoAdmin, "camila"),
        new CrearSistemaIntegradoDto("ERP", "ERP de Andina", claveSistema.ExportSubjectPublicKeyInfoPem(), ["https://erp.andina.co"]));

    private IDocumentStore Store => Host.Services.GetRequiredService<IDocumentStore>();

    [Fact]
    public async Task El_operador_da_de_alta_un_grupo_operable_y_su_administrador_invita_un_tercero()
    {
        var (token, codigo, _) = await SesionDeOperadorAsync();
        var operador = new Credencial(null, Bearer: token);
        var claveErp = RSA.Create(2048);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var correoAdmin = $"camila.{slug}@andina.co";

        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, claveErp, correoAdmin));
        alta.Empresa.ShouldNotBeNull().PrefijoFolio.ShouldBe("ANDINA");
        alta.Administrador.ShouldNotBeNull().Roles.ShouldBe([new RolAsignadoDto("Administrador", null)]);
        alta.Sistema.ShouldNotBeNull().Codigo.ShouldBe("ERP");
        alta.Administrador.Identidades.ShouldBe([new IdentidadExternaDto(alta.Sistema.Id, "camila")]);
        var llaveSistema = alta.ApiKeySistema.ShouldNotBeNull();
        llaveSistema.SistemaId.ShouldBe(alta.Sistema.Id);
        llaveSistema.Valor.ShouldNotBeNullOrEmpty();
        // Las llaves no quedan en claro en el registro de idempotencia.
        await using (var global = Store.QuerySession())
            (await global.Query<Validasoft.Infraestructura.Http.RegistroIdempotencia>().AnyAsync(r => r.Cuerpo.Contains(llaveSistema.Valor!))).ShouldBeFalse();

        // El sistema del grupo actúa en nombre de su administradora: invita a un proveedor en la empresa nueva.
        var camila = new Credencial(llaveSistema.Valor, alta.Empresa.Id, () => Asercion("ERP", claveErp, "camila", "Camila Admin", correoAdmin));
        var correoTercero = $"compras.{slug}@proveedor.co";
        var vinculo = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", camila, new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", "900123456"), "Juridica", "Aceros del Norte S.A.S.", null,
                [new ContactoDto("Ana Gómez", correoTercero, null, "Comercial")]),
            "PROVEEDOR", null, correoTercero, new ReferenciaExternaDto("ERP", "PRV-1")));
        vinculo.Estado.ShouldBe("Invitado");
        var invitacion = await fixture.Correos.EsperarAsync(correoTercero);
        invitacion.Asunto.ShouldContain("Andina de Obras");

        // La consola ve el grupo con su empresa; la auditoría del alta lleva al operador como actor Plataforma.
        var grupos = await EnviarAsync<List<GrupoDto>>("GET", "/api/v1/plataforma/grupos", operador, esperado: 200);
        var grupo = grupos.Single(g => g.Tenant.Slug == slug);
        grupo.Empresas.ShouldHaveSingleItem().Nombre.ShouldBe("Andina de Obras S.A.S.");
        grupo.Empresas[0].PlanIaConfigurado.ShouldBeFalse();

        await using var consulta = Store.QuerySession(alta.Tenant.Id.ToString("N"));
        var registros = await consulta.Query<RegistroDeCambioDeConfiguracion>().ToListAsync();
        var delTenant = registros.Single(r => r.Entidad == "Tenant");
        delTenant.ActorTipo.ToString().ShouldBe("Plataforma");
        delTenant.ActorId.ShouldBe($"{codigo}:op-1");
        registros.ShouldContain(r => r.Entidad == "Usuario" && r.EntidadId == alta.Administrador.Id.ToString());
        registros.Count(r => r.Entidad == "ApiKey").ShouldBe(2);
    }

    [Fact]
    public async Task Un_alta_con_datos_invalidos_no_deja_nada_a_medias()
    {
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var invalida = AltaCompleta(slug, RSA.Create(2048), "x@andina.co") with
        {
            Sistema = new CrearSistemaIntegradoDto("ERP", "ERP", "-----BEGIN PUBLIC KEY-----\nno\n-----END PUBLIC KEY-----")
        };
        var respuesta = await EnviarAsync("POST", "/api/v1/plataforma/tenants", Bootstrap, invalida, esperado: 422);
        (await respuesta.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString().ShouldBe("clave-publica-invalida");

        var sinSistema = AltaCompleta(slug, RSA.Create(2048), "x@andina.co") with { Sistema = null };
        var otra = await EnviarAsync("POST", "/api/v1/plataforma/tenants", Bootstrap, sinSistema, esperado: 422);
        (await otra.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString().ShouldBe("sistema-requerido");

        var tenants = await EnviarAsync<List<TenantDto>>("GET", "/api/v1/plataforma/tenants", Bootstrap, esperado: 200);
        tenants.ShouldNotContain(t => t.Slug == slug);
    }

    [Fact]
    public async Task La_sesion_de_plataforma_exige_la_firma_del_sistema_y_no_entra_a_los_tenants()
    {
        var (token, codigo, _) = await SesionDeOperadorAsync("op-2");
        var operador = new Credencial(null, Bearer: token);

        // Firma de otra clave ⇒ 401; sin aserción ⇒ 422; una sesión no abre otra ⇒ 403.
        var otraClave = RSA.Create(2048);
        await EnviarAsync("POST", "/api/v1/plataforma/sesiones", Bootstrap with { Delegado = () => Asercion(codigo, otraClave, "op-2", "X", "x@v.co") },
            esperado: 401);
        await EnviarAsync("POST", "/api/v1/plataforma/sesiones", Bootstrap, esperado: 422);
        var propia = await EnviarAsync("POST", "/api/v1/plataforma/sesiones", operador, esperado: 403);
        (await propia.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString().ShouldBe("credencial-de-plataforma-requerida");

        // Un sistema desconocido o desactivado como emisor ⇒ 401.
        await EnviarAsync("POST", "/api/v1/plataforma/sesiones", Bootstrap with { Delegado = () => Asercion("NOEXISTE", otraClave, "op-2", "X", "x@v.co") },
            esperado: 401);

        // La sesión de Plataforma no tiene tenant: nada de lo de un tenant.
        var vinculos = await EnviarAsync("GET", "/api/v1/vinculos", operador, esperado: null);
        vinculos.Context.Response.StatusCode.ShouldBeOneOf(403, 404);

        // Y la llave de administración de un tenant no puede abrir sesiones de Plataforma.
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador,
            new CrearTenantDto("g" + Guid.NewGuid().ToString("N")[..10], "Grupo Sin Empresa", "co", null));
        alta.Empresa.ShouldBeNull();
        await EnviarAsync("POST", "/api/v1/plataforma/sesiones", new Credencial(alta.ApiKeyAdministrador.Valor), esperado: 403);
    }

    [Fact]
    public async Task La_llave_de_OpenAI_nunca_vuelve_en_claro_ni_va_a_la_auditoria_y_se_prueba_solo_a_pedido()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-3");
        var operador = new Credencial(null, Bearer: token);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, RSA.Create(2048), $"c.{slug}@a.co"));
        var url = $"/api/v1/plataforma/tenants/{alta.Tenant.Id}/empresas/{alta.Empresa!.Id}/plan-ia";
        var llave = "sk-proj-" + Guid.NewGuid().ToString("N") + "WXYZ";

        // Sin llave: «Probar» responde sin-llave y no llama al proveedor.
        var sinLlave = await EnviarAsync<PruebaDeLlaveIaDto>("POST", url + "/probar-llave", operador, new ProbarLlaveIaDto(), esperado: 200);
        sinLlave.Resultado.ShouldBe("sin-llave");

        var guardar = await EnviarAsync("PUT", url, operador,
            new ActualizarPlanIaEmpresaDto(200, true, 50, llave, "gpt-6-luna", "gpt-6.1-terra", 0.9m, true), esperado: 200);
        var cuerpo = await guardar.ReadAsTextAsync();
        cuerpo.ShouldNotContain(llave);
        var plan = JsonSerializer.Deserialize<PlanIaEmpresaDto>(cuerpo, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        plan.LlaveConfigurada.ShouldBeTrue();
        plan.UltimosCuatro.ShouldBe("WXYZ");
        plan.TopeAdicionalMes.ShouldBe(50);
        plan.UmbralConfianza.ShouldBe(0.9m);

        (await (await EnviarAsync("GET", url, operador, esperado: 200)).ReadAsTextAsync()).ShouldNotContain(llave);
        var grupos = await EnviarAsync<List<GrupoDto>>("GET", "/api/v1/plataforma/grupos", operador, esperado: 200);
        var empresa = grupos.Single(g => g.Tenant.Id == alta.Tenant.Id).Empresas.Single();
        (empresa.PlanIaActivo, empresa.LlaveConfigurada, empresa.UltimosCuatro).ShouldBe((true, true, "WXYZ"));

        // En la base queda cifrada con la clave maestra (vs1.{kid}.…) y la auditoría no la lleva.
        await using (var consulta = Store.QuerySession(alta.Tenant.Id.ToString("N")))
        {
            var guardado = await consulta.LoadAsync<PlanDeIaEmpresa>(alta.Empresa.Id);
            guardado!.ApiKeyOpenAiProtegida.ShouldStartWith("vs1.");
            guardado.ApiKeyOpenAiProtegida.ShouldNotContain(llave);
            var auditoria = await consulta.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "PlanIaEmpresa").ToListAsync();
            auditoria.ShouldNotBeEmpty();
            JsonSerializer.Serialize(auditoria).ShouldNotContain(llave);
            JsonSerializer.Serialize(auditoria).ShouldNotContain(guardado.ApiKeyOpenAiProtegida!);
        }

        // «Probar llave» con la guardada y con una nueva (sin guardarla); el resultado no trae la llave.
        var guardada = await EnviarAsync("POST", url + "/probar-llave", operador, new ProbarLlaveIaDto(), esperado: 200);
        var textoPrueba = await guardada.ReadAsTextAsync();
        textoPrueba.ShouldNotContain(llave);
        JsonSerializer.Deserialize<PruebaDeLlaveIaDto>(textoPrueba, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Resultado.ShouldBe("valida");
        fixture.Analizador.PruebasCon(llave).ShouldBe(1);

        var rechazada = "sk-proj-rechazada-" + Guid.NewGuid().ToString("N");
        var otra = await EnviarAsync<PruebaDeLlaveIaDto>("POST", url + "/probar-llave", operador, new ProbarLlaveIaDto(rechazada), esperado: 200);
        otra.Resultado.ShouldBe("invalida");
        otra.UltimosCuatro.ShouldBe(rechazada[^4..]);
        (await EnviarAsync<PlanIaEmpresaDto>("GET", url, operador, esperado: 200)).UltimosCuatro.ShouldBe("WXYZ"); // probar no guarda

        await EnviarAsync("POST", url + "/probar-llave", operador, new ProbarLlaveIaDto("corta"), esperado: 422);

        // Quitar la llave.
        var sinLaLlave = await EnviarAsync<PlanIaEmpresaDto>("PUT", url, operador, new ActualizarPlanIaEmpresaDto(200, true, 50, ""), esperado: 200);
        (sinLaLlave.LlaveConfigurada, sinLaLlave.UltimosCuatro).ShouldBe((false, null));
    }

    [Fact]
    public async Task El_recifrado_pasa_a_la_clave_maestra_lo_cifrado_con_la_clave_de_sesiones_y_conserva_el_secreto()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-4");
        var operador = new Credencial(null, Bearer: token);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, RSA.Create(2048), $"c.{slug}@a.co"));
        var url = $"/api/v1/plataforma/tenants/{alta.Tenant.Id}/empresas/{alta.Empresa!.Id}/plan-ia";
        var llave = "sk-heredada-" + Guid.NewGuid().ToString("N");
        await EnviarAsync("PUT", url, operador, new ActualizarPlanIaEmpresaDto(10, false, null, "sk-temporal-" + Guid.NewGuid().ToString("N")), esperado: 200);

        // Simula un plan guardado antes de la clave maestra: formato sin versión, con la clave derivada de las sesiones.
        await using (var sesion = Store.LightweightSession(alta.Tenant.Id.ToString("N")))
        {
            var plan = (await sesion.LoadAsync<PlanDeIaEmpresa>(alta.Empresa.Id))!;
            plan.ApiKeyOpenAiProtegida = CifrarComoAntes(llave, "dev-sesiones-solo-local-cambiar-000000000001");
            sesion.Store(plan);
            await sesion.SaveChangesAsync();
        }

        var antes = await EnviarAsync<EstadoDeSecretosDto>("GET", "/api/v1/plataforma/secretos", operador, esperado: 200);
        antes.Simulado.ShouldBeTrue();
        antes.ClaveVigente.Length.ShouldBe(8);
        var llavesAntes = antes.PorTipo.Single(t => t.Tipo == "llave-openai");
        (llavesAntes.Total - llavesAntes.Vigentes).ShouldBeGreaterThanOrEqualTo(1);

        var despues = await EnviarAsync<EstadoDeSecretosDto>("POST", "/api/v1/plataforma/secretos/recifrar", operador, esperado: 200);
        despues.Simulado.ShouldBeFalse();
        despues.PorTipo.Single(t => t.Tipo == "llave-openai").Recifrados.ShouldBeGreaterThanOrEqualTo(1);

        await using (var consulta = Store.QuerySession(alta.Tenant.Id.ToString("N")))
            (await consulta.LoadAsync<PlanDeIaEmpresa>(alta.Empresa.Id))!.ApiKeyOpenAiProtegida.ShouldStartWith($"vs1.{despues.ClaveVigente}.");

        // Sigue siendo la misma llave: «Probar» la descifra y se la pasa al proveedor.
        (await EnviarAsync<PruebaDeLlaveIaDto>("POST", url + "/probar-llave", operador, new ProbarLlaveIaDto(), esperado: 200)).UltimosCuatro.ShouldBe(llave[^4..]);
        fixture.Analizador.PruebasCon(llave).ShouldBe(1);

        // Repetirlo es inocuo: ya no queda nada de esta empresa por re-cifrar.
        var otraVez = await EnviarAsync<EstadoDeSecretosDto>("GET", "/api/v1/plataforma/secretos", operador, esperado: 200);
        otraVez.PorTipo.Single(t => t.Tipo == "llave-openai").Fallidos.ShouldBe(0);
    }

    /// <summary>El protector anterior a PT-1: AES-256-GCM con HKDF-SHA256 de la clave de firma de sesiones, sin versión.</summary>
    internal static string CifrarComoAntes(string secreto, string claveFirma)
    {
        var rotulo = Encoding.UTF8.GetBytes("validasoft:secretos-outbox:v1");
        var clave = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(claveFirma), 32, info: rotulo);
        var plano = Encoding.UTF8.GetBytes(secreto);
        var salida = new byte[12 + plano.Length + 16];
        RandomNumberGenerator.Fill(salida.AsSpan(0, 12));
        using var aes = new AesGcm(clave, 16);
        aes.Encrypt(salida.AsSpan(0, 12), plano, salida.AsSpan(12, plano.Length), salida.AsSpan(12 + plano.Length), rotulo);
        return Base64Url.EncodeToString(salida);
    }

    private async Task<T> EnviarAsync<T>(string metodo, string url, Credencial credencial, object? cuerpo = null, int? esperado = 201)
    {
        var resultado = await EnviarAsync(metodo, url, credencial, cuerpo, esperado);
        return (await resultado.ReadAsJsonAsync<T>())!;
    }

    private Task<IScenarioResult> EnviarAsync(string metodo, string url, Credencial credencial, object? cuerpo = null, int? esperado = 201) =>
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

            if (credencial.ApiKey is not null)
                s.WithRequestHeader("X-Api-Key", credencial.ApiKey);
            if (credencial.Bearer is not null)
                s.WithRequestHeader("Authorization", "Bearer " + credencial.Bearer);
            if (credencial.Delegado is not null)
                s.WithRequestHeader("X-Usuario-Delegado", credencial.Delegado());
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());

            if (esperado is null)
                s.IgnoreStatusCode();
            else
                s.StatusCodeShouldBe(esperado.Value);
        });
}
