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
/// Consola de Plataforma (PT-1…PT-10): operadores por identidad delegada sin tenant, alta de un grupo operable de punta a
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
        var grupos = (await EnviarAsync<PaginaDto<GrupoDto>>("GET", $"/api/v1/plataforma/grupos?q={slug}", operador, esperado: 200)).Datos;
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
        var grupos = (await EnviarAsync<PaginaDto<GrupoDto>>("GET", $"/api/v1/plataforma/grupos?q={slug}", operador, esperado: 200)).Datos;
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

    [Fact]
    public async Task Las_credenciales_de_listas_de_una_empresa_se_cifran_con_la_clave_del_grupo_y_el_recifrado_migra_lo_guardado_con_la_clave_de_la_instalacion()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-5");
        var operador = new Credencial(null, Bearer: token);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, RSA.Create(2048), $"c.{slug}@a.co"));
        var grupoId = alta.Tenant.Id;
        var empresa = new Credencial(alta.ApiKeyAdministrador.Valor, alta.Empresa!.Id);
        var protector = Host.Services.GetRequiredService<Validasoft.Aplicacion.Comun.IProtectorDeSecretos>();

        // Lo que la empresa guarda por la API ya queda con la clave de su grupo y la API nunca devuelve la clave.
        var respuesta = await EnviarAsync("PUT", "/api/v1/configuracion/listas-restrictivas", empresa,
            new ActualizarListasRestrictivasDto(Credenciales: new CredencialesListasDto("Stradata", "sinco-norte", "clave-super-secreta-9876")), esperado: 200);
        (await respuesta.ReadAsTextAsync()).ShouldNotContain("clave-super-secreta");
        await using (var consulta = Store.QuerySession(grupoId.ToString("N")))
        {
            var guardada = (await consulta.LoadAsync<Validasoft.Dominio.Expedientes.ListasRestrictivasEmpresa>(alta.Empresa.Id))!.Credenciales["Stradata"].ClaveProtegida;
            guardada.ShouldStartWith("vs2.");
            guardada.ShouldNotContain("secreta");
            protector.DesprotegerDeGrupo(grupoId, guardada).ShouldBe("clave-super-secreta-9876");
            Should.Throw<CryptographicException>(() => protector.DesprotegerDeGrupo(Guid.NewGuid(), guardada)); // otro grupo no la abre
        }

        // Una guardada antes con la clave de la instalación se sigue leyendo y el recifrado la pasa a la del grupo.
        await using (var sesion = Store.LightweightSession(grupoId.ToString("N")))
        {
            var configuracion = (await sesion.LoadAsync<Validasoft.Dominio.Expedientes.ListasRestrictivasEmpresa>(alta.Empresa.Id))!;
            configuracion.Credenciales["Stradata"].ClaveProtegida = protector.Proteger("clave-legada-1234");
            sesion.Store(configuracion);
            await sesion.SaveChangesAsync();
        }

        var antes = await EnviarAsync<EstadoDeSecretosDto>("GET", "/api/v1/plataforma/secretos", operador, esperado: 200);
        var tipoAntes = antes.PorTipo.Single(t => t.Tipo == "credenciales-listas");
        (tipoAntes.Total - tipoAntes.Vigentes).ShouldBeGreaterThanOrEqualTo(1);
        var despues = await EnviarAsync<EstadoDeSecretosDto>("POST", "/api/v1/plataforma/secretos/recifrar", operador, esperado: 200);
        despues.PorTipo.Single(t => t.Tipo == "credenciales-listas").Recifrados.ShouldBeGreaterThanOrEqualTo(1);

        await using var releida = Store.QuerySession(grupoId.ToString("N"));
        var migrada = (await releida.LoadAsync<Validasoft.Dominio.Expedientes.ListasRestrictivasEmpresa>(alta.Empresa.Id))!.Credenciales["Stradata"].ClaveProtegida;
        migrada.ShouldStartWith($"vs2.{despues.ClaveVigente}.");
        protector.DesprotegerDeGrupo(grupoId, migrada).ShouldBe("clave-legada-1234");
    }

    [Fact]
    public async Task La_cuenta_de_Infolaft_es_de_la_plataforma_se_cifra_con_la_clave_maestra_la_empresa_no_la_ve_y_la_migracion_recoge_la_que_tenia_una_empresa()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-6");
        var operador = new Credencial(null, Bearer: token);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, RSA.Create(2048), $"c.{slug}@a.co"));
        var grupoId = alta.Tenant.Id;
        var empresa = new Credencial(alta.ApiKeyAdministrador.Valor, alta.Empresa!.Id);
        var protector = Host.Services.GetRequiredService<Validasoft.Aplicacion.Comun.IProtectorDeSecretos>();
        const string url = "/api/v1/plataforma/listas-restrictivas";
        var quitar = new ActualizarListasDePlataformaDto(new CredencialesListasDto("Infolaft", Clave: ""));

        try
        {
            // La plataforma parte sin cuenta de Infolaft; la empresa la ve como «de la plataforma» y sin configurar.
            await EnviarAsync<ListasDePlataformaDto>("PUT", url, operador, quitar, esperado: 200);
            var vacia = (await EnviarAsync<ListasDePlataformaDto>("GET", url, operador, esperado: 200)).Proveedores.Single();
            (vacia.Nombre, vacia.Configurado, vacia.Usuario).ShouldBe(("Infolaft", false, (string?)null));
            var deLaEmpresa = (await EnviarAsync<ListasRestrictivasConfigDto>("GET", "/api/v1/configuracion/listas-restrictivas", empresa, esperado: 200)).Proveedores.First();
            (deLaEmpresa.Nombre, deLaEmpresa.DeLaPlataforma, deLaEmpresa.Configurado).ShouldBe(("Infolaft", true, false));

            // Una empresa con la cuenta que cargó antes de G-2 (cifrada con la clave de su grupo, con una cuota propia).
            await using (var sesion = Store.LightweightSession(grupoId.ToString("N")))
            {
                var configuracion = Validasoft.Dominio.Expedientes.ListasRestrictivasEmpresa.Nueva(alta.Empresa.Id);
                configuracion.Credenciales["Infolaft"] = new Validasoft.Dominio.Expedientes.CredencialesDeListas
                {
                    Usuario = "sinco-norte", ClaveProtegida = protector.ProtegerDeGrupo(grupoId, "clave-heredada-5555"), UltimosCuatroClave = "5555", CuotaMensual = 300,
                    ModoBusqueda = Validasoft.Dominio.Expedientes.ModoBusquedaListas.PorDocumento
                };
                sesion.Store(configuracion);
                await sesion.SaveChangesAsync();
            }

            (await EnviarAsync<ListasDePlataformaDto>("GET", url, operador, esperado: 200)).Proveedores.Single().EmpresasConCredencialesPropias.ShouldBeGreaterThanOrEqualTo(1);

            // La migración toma la primera cuenta válida, la cifra con la clave maestra y quita las propias de las empresas.
            var migracion = await EnviarAsync<ResultadoMigracionListasDto>("POST", url + "/migrar-credenciales", operador, esperado: 200);
            (migracion.CuentaTomada, migracion.EmpresaDeOrigen, migracion.NoLegibles).ShouldBe((true, (Guid?)alta.Empresa.Id, 0));
            migracion.EmpresasLimpiadas.ShouldBeGreaterThanOrEqualTo(1);
            migracion.CuotasQueNoSeHeredaron.ShouldBeGreaterThanOrEqualTo(1);
            var configurada = await EnviarAsync<ListasDePlataformaDto>("GET", url, operador, esperado: 200);
            var infolaft = configurada.Proveedores.Single();
            (infolaft.Configurado, infolaft.Usuario, infolaft.UltimosCuatroClave, infolaft.CuotaMensualPorEmpresa, infolaft.ModoBusqueda, infolaft.EmpresasConCredencialesPropias)
                .ShouldBe((true, "sinco-norte", "5555", (int?)null, "PorDocumento", 0));
            configurada.ActualizadaPor.ShouldNotBeNullOrWhiteSpace();

            await using (var consulta = Store.QuerySession())
            {
                var guardada = (await consulta.LoadAsync<Validasoft.Dominio.Expedientes.ListasRestrictivasDePlataforma>(Validasoft.Dominio.Expedientes.ListasRestrictivasDePlataforma.IdUnico))!
                    .Credenciales["Infolaft"].ClaveProtegida;
                guardada.ShouldStartWith($"vs1.{protector.ClaveVigente}.");
                guardada.ShouldNotContain("heredada");
                protector.Desproteger(guardada).ShouldBe("clave-heredada-5555");
            }

            await using (var propia = Store.QuerySession(grupoId.ToString("N")))
                (await propia.LoadAsync<Validasoft.Dominio.Expedientes.ListasRestrictivasEmpresa>(alta.Empresa.Id))!.Credenciales.ShouldNotContainKey("Infolaft");

            // La empresa solo sabe que está disponible (y su cuota); nada de usuario, clave, URL, plan ni modo, ni puede cargar los suyos.
            var configuracionDeLaEmpresa = await EnviarAsync("GET", "/api/v1/configuracion/listas-restrictivas", empresa, esperado: 200);
            var textoDeLaEmpresa = await configuracionDeLaEmpresa.ReadAsTextAsync();
            textoDeLaEmpresa.ShouldNotContain("sinco-norte");
            textoDeLaEmpresa.ShouldNotContain("5555");
            var visto = JsonSerializer.Deserialize<ListasRestrictivasConfigDto>(textoDeLaEmpresa, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Proveedores.First();
            (visto.Nombre, visto.DeLaPlataforma, visto.Configurado, visto.Usuario, visto.UltimosCuatroClave, visto.UrlBase, visto.Plan, visto.ModoBusqueda)
                .ShouldBe(("Infolaft", true, true, (string?)null, (string?)null, (string?)null, (string?)null, (string?)null));
            var rechazo = await EnviarAsync("PUT", "/api/v1/configuracion/listas-restrictivas", empresa,
                new ActualizarListasRestrictivasDto(Credenciales: new CredencialesListasDto("Infolaft", "otro", "clave-otra-1234")), esperado: 422);
            (await rechazo.ReadAsTextAsync()).ShouldContain("credenciales-de-plataforma");

            // Repetir la migración no cambia nada.
            var otraVez = await EnviarAsync<ResultadoMigracionListasDto>("POST", url + "/migrar-credenciales", operador, esperado: 200);
            (otraVez.CuentaTomada, otraVez.EmpresasConCredenciales, otraVez.EmpresasLimpiadas).ShouldBe((false, 0, 0));

            // Plataforma la cambia (clave nueva y la cuota por empresa): la clave nunca vuelve y cada empresa ve su cuota.
            var cambio = await EnviarAsync("PUT", url, operador,
                new ActualizarListasDePlataformaDto(new CredencialesListasDto("Infolaft", Clave: "clave-nueva-7777", Plan: "Empresarial", CuotaMensual: 800)), esperado: 200);
            (await cambio.ReadAsTextAsync()).ShouldNotContain("clave-nueva");
            var cambiada = (await EnviarAsync<ListasDePlataformaDto>("GET", url, operador, esperado: 200)).Proveedores.Single();
            (cambiada.Usuario, cambiada.UltimosCuatroClave, cambiada.Plan, cambiada.CuotaMensualPorEmpresa).ShouldBe(("sinco-norte", "7777", "Empresarial", 800));
            (await EnviarAsync<ListasRestrictivasConfigDto>("GET", "/api/v1/configuracion/listas-restrictivas", empresa, esperado: 200)).Proveedores.First().CuotaMensual.ShouldBe(800);
            (await EnviarAsync("PUT", url, operador, new ActualizarListasDePlataformaDto(new CredencialesListasDto("Stradata", "x", "clave-1234")), esperado: 422)).ShouldNotBeNull();

            // El re-cifrado de la clave maestra la cuenta entre los secretos y la deja intacta.
            var secretos = await EnviarAsync<EstadoDeSecretosDto>("GET", "/api/v1/plataforma/secretos", operador, esperado: 200);
            var tipo = secretos.PorTipo.Single(t => t.Tipo == "credenciales-listas");
            tipo.Vigentes.ShouldBeGreaterThanOrEqualTo(1);

            // Nada de esto dejó una clave en la auditoría.
            await using var auditoria = Store.QuerySession();
            var registros = await auditoria.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "ListasRestrictivasDePlataforma").ToListAsync();
            registros.Count.ShouldBeGreaterThanOrEqualTo(2);
            var serializado = JsonSerializer.Serialize(registros);
            serializado.ShouldNotContain("heredada");
            serializado.ShouldNotContain("clave-nueva");
        }
        finally
        {
            await EnviarAsync<ListasDePlataformaDto>("PUT", url, operador, quitar, esperado: 200);
        }
    }

    private Task<PaginaDto<GrupoDto>> GruposAsync(Credencial credencial, string? q = null, int? limite = null, string? cursor = null)
    {
        var consulta = new List<string>();
        if (q is not null)
            consulta.Add("q=" + Uri.EscapeDataString(q));
        if (limite is not null)
            consulta.Add($"limite={limite}");
        if (cursor is not null)
            consulta.Add("cursor=" + Uri.EscapeDataString(cursor));
        return EnviarAsync<PaginaDto<GrupoDto>>("GET", "/api/v1/plataforma/grupos" + (consulta.Count > 0 ? "?" + string.Join('&', consulta) : string.Empty),
            credencial, esperado: 200);
    }

    [Fact]
    public async Task La_consola_lista_los_grupos_por_paginas_con_cursor_y_los_busca_por_grupo_empresa_o_NIT()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-5");
        var operador = new Credencial(null, Bearer: token);
        var marca = Guid.NewGuid().ToString("N")[..8];
        var nits = new Dictionary<char, string>();
        var altas = new Dictionary<char, TenantCreadoDto>();
        foreach (var letra in "ABCDE")
        {
            var nit = "8" + Random.Shared.Next(10_000_000, 99_999_999).ToString();
            nits[letra] = nit;
            altas[letra] = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, new CrearTenantDto(
                $"g{marca}{char.ToLowerInvariant(letra)}", $"Zeta {marca} {letra}", "co", "America/Bogota",
                new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", nit), $"Obras {marca} {letra} S.A.S.", $"O{letra}", null, null),
                new AdministradorInicialDto("Camila Admin", $"camila.{marca}.{letra}@andina.co", null)));
        }

        // El grupo B tiene una segunda empresa: sale con las dos, ordenadas por nombre.
        await EnviarAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(altas['B'].ApiKeyAdministrador.Valor),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), $"Aliada {marca} S.A.S.", "ALIADA", null, null));

        // Páginas de 2: A-B, C-D, E; el cursor solo viene mientras queda algo, y todo sale una sola vez, por nombre del grupo.
        var primera = await GruposAsync(operador, marca, limite: 2);
        primera.Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} A", $"Zeta {marca} B"]);
        primera.SiguienteCursor.ShouldNotBeNull();
        primera.Datos[1].Empresas.Select(e => e.Nombre).ShouldBe([$"Aliada {marca} S.A.S.", $"Obras {marca} B S.A.S."]);
        var segunda = await GruposAsync(operador, marca, limite: 2, cursor: primera.SiguienteCursor);
        segunda.Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} C", $"Zeta {marca} D"]);
        segunda.SiguienteCursor.ShouldNotBeNull();
        var tercera = await GruposAsync(operador, marca, limite: 2, cursor: segunda.SiguienteCursor);
        tercera.Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} E"]);
        tercera.SiguienteCursor.ShouldBeNull();
        var todos = await GruposAsync(operador, marca, limite: 5);
        todos.Datos.Count.ShouldBe(5);
        todos.SiguienteCursor.ShouldBeNull();

        // Cada grupo trae solo sus empresas (no las de todo el sistema) y el estado de su plan de IA.
        todos.Datos.Where(g => g.Tenant.Slug != $"g{marca}b").ShouldAllBe(g => g.Empresas.Count == 1);
        todos.Datos[0].Empresas.Single().PlanIaConfigurado.ShouldBeFalse();

        // Por el nombre o el identificador del grupo (sin distinguir mayúsculas), por la razón social y por el NIT (con o sin formato).
        (await GruposAsync(operador, $"zeta {marca} c")).Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} C"]);
        (await GruposAsync(operador, $"G{marca}D")).Datos.Select(g => g.Tenant.Slug).ShouldBe([$"g{marca}d"]);
        (await GruposAsync(operador, $"obras {marca} e")).Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} E"]);
        (await GruposAsync(operador, $"Aliada {marca}")).Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} B"]);
        var nitA = nits['A'];
        (await GruposAsync(operador, nitA)).Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} A"]);
        var conFormato = $"{nitA[..3]}.{nitA[3..6]}.{nitA[6..]}-{altas['A'].Empresa!.Identificacion.DigitoVerificacion}";
        (await GruposAsync(operador, conFormato)).Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Zeta {marca} A"]);
        (await GruposAsync(operador, nitA[..7])).Datos.ShouldContain(g => g.Tenant.Nombre == $"Zeta {marca} A");

        // Sin coincidencias: página vacía sin cursor. Los comodines de LIKE no abren la búsqueda.
        var ninguno = await GruposAsync(operador, $"no-existe-{marca}");
        (ninguno.Datos.Count, ninguno.SiguienteCursor).ShouldBe((0, null));
        (await GruposAsync(operador, $"%{marca}")).Datos.ShouldBeEmpty();
        (await GruposAsync(operador, $"{marca[..3]}_{marca[4..]}")).Datos.ShouldBeEmpty();

        // Un cursor que no es nuestro ⇒ 422.
        var invalido = await EnviarAsync("GET", "/api/v1/plataforma/grupos?cursor=no-es-un-cursor", operador, esperado: 422);
        (await invalido.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString().ShouldBe("cursor-invalido");
    }

    [Fact]
    public async Task Un_grupo_se_consulta_por_su_id_con_sus_empresas_y_el_plan_de_IA_sin_recorrer_la_lista()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-7");
        var operador = new Credencial(null, Bearer: token);
        var slug = "g" + Guid.NewGuid().ToString("N")[..10];
        var alta = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador, AltaCompleta(slug, RSA.Create(2048), $"c.{slug}@a.co"));
        await EnviarAsync("PUT", $"/api/v1/plataforma/tenants/{alta.Tenant.Id}/empresas/{alta.Empresa!.Id}/plan-ia", operador,
            new ActualizarPlanIaEmpresaDto(10, false, null, "sk-proj-" + Guid.NewGuid().ToString("N") + "ABCD"), esperado: 200);

        var grupo = await EnviarAsync<GrupoDto>("GET", $"/api/v1/plataforma/grupos/{alta.Tenant.Id}", operador, esperado: 200);
        grupo.Tenant.Slug.ShouldBe(slug);
        var empresa = grupo.Empresas.ShouldHaveSingleItem();
        (empresa.Id, empresa.LlaveConfigurada, empresa.UltimosCuatro).ShouldBe((alta.Empresa.Id, true, "ABCD"));

        var inexistente = await EnviarAsync("GET", $"/api/v1/plataforma/grupos/{Guid.NewGuid()}", operador, esperado: 404);
        (await inexistente.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString().ShouldBe("tenant-no-encontrado");
        await EnviarAsync("GET", $"/api/v1/plataforma/grupos/{alta.Tenant.Id}", new Credencial(alta.ApiKeyAdministrador.Valor), esperado: 403);
    }

    [Fact]
    public async Task La_pagina_de_grupos_es_de_20_por_omision_con_tope_de_100_y_solo_la_ve_Plataforma()
    {
        var (token, _, _) = await SesionDeOperadorAsync("op-6");
        var operador = new Credencial(null, Bearer: token);
        var marca = Guid.NewGuid().ToString("N")[..8];
        TenantCreadoDto? ultimo = null;
        for (var i = 0; i < 21; i++)
            ultimo = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", operador,
                new CrearTenantDto($"p{marca}{i:00}", $"Pagina {marca} {i:00}", "co", null));

        var primera = await GruposAsync(operador, marca);
        primera.Datos.Count.ShouldBe(20);
        primera.SiguienteCursor.ShouldNotBeNull();
        primera.Datos.ShouldAllBe(g => g.Empresas.Count == 0);
        var resto = await GruposAsync(operador, marca, cursor: primera.SiguienteCursor);
        resto.Datos.Select(g => g.Tenant.Nombre).ShouldBe([$"Pagina {marca} 20"]);
        resto.SiguienteCursor.ShouldBeNull();

        (await GruposAsync(operador, marca, limite: 500)).Datos.Count.ShouldBe(21);
        (await GruposAsync(operador, marca, limite: 0)).Datos.Count.ShouldBe(1);
        (await GruposAsync(Bootstrap, marca, limite: 3)).Datos.Count.ShouldBe(3);

        // La llave de un grupo no ve la consola; sin credencial, tampoco.
        await EnviarAsync("GET", "/api/v1/plataforma/grupos", new Credencial(ultimo!.ApiKeyAdministrador.Valor), esperado: 403);
        await EnviarAsync("GET", "/api/v1/plataforma/grupos", new Credencial(null), esperado: 401);
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
