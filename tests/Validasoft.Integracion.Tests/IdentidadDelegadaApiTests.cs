using System.Security.Claims;
using System.Security.Cryptography;
using Alba;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Identidad delegada (ADR-0011): sistemas integrados con clave pública, usuarios JIT sin roles, intersección de
/// permisos, sesiones embebidas y una sola validación compartida entre sistemas (I-0).
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class IdentidadDelegadaApiTests(ApiFixture fixture)
{
    private static readonly string[] ScopesDeSistema =
        ["configuracion:leer", "terceros:leer", "terceros:escribir", "vinculos:leer", "vinculos:escribir", "vinculos:bloquear"];

    private IAlbaHost Host => fixture.Host;

    /// <summary>Un sistema integrado con su clave privada (solo la conoce la prueba) y su API key.</summary>
    private sealed record Sistema(string Codigo, Guid Id, AsymmetricAlgorithm ClavePrivada, string ApiKey);

    private sealed record Escenario(string Admin, EmpresaDto EmpresaA, EmpresaDto EmpresaB, Sistema Srm, Sistema Marco);

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await EnviarAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Constructora Demo", "co", "America/Bogota"));
        var admin = new Credencial(tenant.ApiKeyAdministrador.Valor!);

        var a = await EnviarAsync<EmpresaDto>("POST", "/api/v1/empresas", admin,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null));
        var b = await EnviarAsync<EmpresaDto>("POST", "/api/v1/empresas", admin,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null));

        var srm = await RegistrarSistemaAsync(admin, "SRM", RSA.Create(2048));
        var marco = await RegistrarSistemaAsync(admin, "MARCO", ECDsa.Create(ECCurve.NamedCurves.nistP256));
        return new Escenario(admin.ApiKey!, a, b, srm, marco);
    }

    private async Task<Sistema> RegistrarSistemaAsync(Credencial admin, string codigo, AsymmetricAlgorithm clave)
    {
        var pem = clave is RSA rsa ? rsa.ExportSubjectPublicKeyInfoPem() : ((ECDsa)clave).ExportSubjectPublicKeyInfoPem();
        var sistema = await EnviarAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", admin, new CrearSistemaIntegradoDto(codigo, codigo + " del cliente", pem));
        var llave = await EnviarAsync<ApiKeyDto>("POST", "/api/v1/api-keys", admin,
            new CrearApiKeyDto("Integración " + codigo, ScopesDeSistema, null, null, SistemaId: sistema.Id));
        llave.SistemaId.ShouldBe(sistema.Id);
        return new Sistema(codigo, sistema.Id, clave, llave.Valor!);
    }

    /// <summary>Aserción como la firmaría el sistema integrado con su clave privada.</summary>
    private static string Asercion(Sistema sistema, string sub, string correo, AsymmetricAlgorithm? otraClave = null,
        TimeSpan? venceEn = null, string nombre = "Laura Pérez")
    {
        var clave = otraClave ?? sistema.ClavePrivada;
        var credenciales = clave is RSA rsa
            ? new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256)
            : new SigningCredentials(new ECDsaSecurityKey((ECDsa)clave), SecurityAlgorithms.EcdsaSha256);
        var ahora = DateTime.UtcNow;
        var expira = ahora + (venceEn ?? TimeSpan.FromMinutes(2));

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = sistema.Codigo,
            Audience = "validasoft",
            Subject = new ClaimsIdentity([new Claim("sub", sub), new Claim("name", nombre), new Claim("email", correo)]),
            IssuedAt = expira.AddMinutes(-3),
            NotBefore = expira.AddMinutes(-3),
            Expires = expira,
            SigningCredentials = credenciales
        });
    }

    private static CrearVinculoDto Proveedor(string nit, ReferenciaExternaDto? referencia) => new(
        TerceroId: null,
        Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Aceros del Norte S.A.S.", null,
            [new ContactoDto("Ana Gómez", "ana@aceros.co", null, "Comercial")]),
        TipoVinculo: "PROVEEDOR",
        Atributos: null,
        CorreoInvitacion: "ana@aceros.co",
        ReferenciaExterna: referencia);

    private async Task<UsuarioDto> UsuarioPorCorreoAsync(Escenario e, string correo)
    {
        var usuarios = await EnviarAsync<List<UsuarioDto>>("GET", $"/api/v1/usuarios?correo={correo}", new Credencial(e.Admin), esperado: 200);
        return usuarios.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Un_usuario_nuevo_nace_sin_roles_y_con_rol_de_revisor_en_A_solo_trabaja_en_A()
    {
        var e = await PrepararAsync();
        var laura = new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-42", "laura@constructora.co"));

        // (a) Sin roles: la empresa no está a su alcance.
        var sinRoles = await EnviarAsync("POST", "/api/v1/vinculos", laura with { Empresa = e.EmpresaA.Id },
            Proveedor("900100200", new ReferenciaExternaDto("SRM", "ADP-1")), esperado: null);
        sinRoles.Context.Response.StatusCode.ShouldBeOneOf(403, 404);

        var usuario = await UsuarioPorCorreoAsync(e, "laura@constructora.co");
        usuario.Nombre.ShouldBe("Laura Pérez");
        usuario.Roles.ShouldBeEmpty();
        usuario.Identidades.ShouldBe([new IdentidadExternaDto(e.Srm.Id, "u-42")]);

        // (b) Revisor en A: crea en A y no ve B.
        await EnviarAsync<UsuarioDto>("PUT", $"/api/v1/usuarios/{usuario.Id}/roles", new Credencial(e.Admin),
            new AsignarRolesDto([new RolAsignadoDto("Revisor", e.EmpresaA.Id)]), esperado: 200);

        var vinculo = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", laura with { Empresa = e.EmpresaA.Id },
            Proveedor("900100200", new ReferenciaExternaDto("", "ADP-1")));
        vinculo.Referencias.ShouldBe([new ReferenciaExternaDto("SRM", "ADP-1")]);

        await EnviarAsync("POST", "/api/v1/vinculos", laura with { Empresa = e.EmpresaB.Id }, Proveedor("900100200", null), esperado: 404);

        // Revisor no bloquea aunque la llave del sistema sí pueda (intersección).
        await EnviarAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", laura with { Empresa = e.EmpresaA.Id },
            new BloquearVinculoDto("Prueba", null), esperado: 403);

        // (c) El historial registra a la persona y al sistema.
        var historial = await EnviarAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial",
            laura with { Empresa = e.EmpresaA.Id }, esperado: 200);
        historial[0].ActorTipo.ShouldBe("Usuario");
        historial[0].ActorId.ShouldBe(usuario.Id.ToString());
        historial[0].Sistema.ShouldBe("SRM");
    }

    [Fact]
    public async Task Rechaza_con_401_la_firma_de_otra_clave_la_asercion_vencida_y_el_usuario_desactivado()
    {
        var e = await PrepararAsync();

        using var intrusa = RSA.Create(2048);
        var firmaAjena = await EnviarAsync("GET", "/api/v1/empresas",
            new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-1", "a@x.co", otraClave: intrusa)), esperado: 401);
        (await firmaAjena.ReadAsTextAsync()).ShouldContain("asercion-firma-invalida");

        var vencida = await EnviarAsync("GET", "/api/v1/empresas",
            new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-1", "a@x.co", venceEn: TimeSpan.FromMinutes(-2))), esperado: 401);
        (await vencida.ReadAsTextAsync()).ShouldContain("asercion-vencida");

        var larga = await EnviarAsync("GET", "/api/v1/empresas",
            new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-1", "a@x.co", venceEn: TimeSpan.FromHours(1))), esperado: 401);
        (await larga.ReadAsTextAsync()).ShouldContain("asercion-vigencia-excesiva");

        // La aserción de SRM no vale con la llave de MARCO (iss y clave distintos).
        await EnviarAsync("GET", "/api/v1/empresas", new Credencial(e.Marco.ApiKey, Delegado: Asercion(e.Srm, "u-1", "a@x.co")), esperado: 401);

        var valida = new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-7", "pedro@x.co"));
        await EnviarAsync("GET", "/api/v1/empresas", valida, esperado: 200);
        var pedro = await UsuarioPorCorreoAsync(e, "pedro@x.co");
        await EnviarAsync<UsuarioDto>("POST", $"/api/v1/usuarios/{pedro.Id}/desactivar", new Credencial(e.Admin), new { }, esperado: 200);

        var inactivo = await EnviarAsync("GET", "/api/v1/empresas", valida, esperado: 401);
        (await inactivo.ReadAsTextAsync()).ShouldContain("usuario-inactivo");
    }

    [Fact]
    public async Task La_sesion_embebida_es_un_token_Bearer_con_el_permiso_efectivo()
    {
        var e = await PrepararAsync();
        var admin = new Credencial(e.Admin);
        var usuario = await EnviarAsync<UsuarioDto>("POST", "/api/v1/usuarios", admin,
            new CrearUsuarioDto("Marta Ruiz", "marta@constructora.co", null, [new RolAsignadoDto("Revisor", e.EmpresaA.Id)], null));
        var creado = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", new Credencial(e.Srm.ApiKey, e.EmpresaA.Id),
            Proveedor("900300400", new ReferenciaExternaDto("SRM", "ADP-3")));

        // Alta anticipada por correo: la primera aserción la enlaza en lugar de crear otro usuario.
        var sesion = await EnviarAsync<SesionUsuarioDto>("POST", "/api/v1/sesiones-usuario",
            new Credencial(e.Srm.ApiKey, Delegado: Asercion(e.Srm, "u-88", "Marta@Constructora.co", nombre: "Marta Ruiz")), new { }, esperado: 200);
        sesion.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(25));
        (await UsuarioPorCorreoAsync(e, "marta@constructora.co")).Identidades.ShouldBe([new IdentidadExternaDto(e.Srm.Id, "u-88")]);

        var bearer = new Credencial(null, e.EmpresaA.Id, Bearer: sesion.Token);
        var pagina = await EnviarAsync<PaginaDto<VinculoDto>>("GET", "/api/v1/vinculos", bearer, esperado: 200);
        pagina.Datos.Select(v => v.Id).ShouldBe([creado.Id]);
        await EnviarAsync("GET", "/api/v1/vinculos", bearer with { Empresa = e.EmpresaB.Id }, esperado: 404);
        await EnviarAsync("POST", "/api/v1/usuarios", bearer with { Empresa = null },
            new CrearUsuarioDto("Otro", "otro@x.co", null, null, null), esperado: 403);

        // Sin delegación no hay sesión.
        await EnviarAsync("POST", "/api/v1/sesiones-usuario", new Credencial(e.Srm.ApiKey), new { }, esperado: 422);
        usuario.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task Dos_sistemas_comparten_una_sola_validacion_del_vinculo()
    {
        var e = await PrepararAsync();
        var srm = new Credencial(e.Srm.ApiKey, e.EmpresaA.Id);
        var marco = new Credencial(e.Marco.ApiKey, e.EmpresaA.Id);

        // (g) SRM crea; MARCO pide el mismo vínculo y recibe el existente con las dos referencias.
        var deSrm = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", srm, Proveedor("900500600", new ReferenciaExternaDto("SRM", "ADP-5")));
        var deMarco = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", marco,
            Proveedor("900500600", new ReferenciaExternaDto("MARCO", "PRV-9")), esperado: 200);

        deMarco.Id.ShouldBe(deSrm.Id);
        deMarco.Referencias.ShouldBe([new ReferenciaExternaDto("SRM", "ADP-5"), new ReferenciaExternaDto("MARCO", "PRV-9")]);

        var repetido = await EnviarAsync<VinculoDto>("POST", "/api/v1/vinculos", marco,
            Proveedor("900500600", new ReferenciaExternaDto("MARCO", "PRV-9")), esperado: 200);
        repetido.Version.ShouldBe(deMarco.Version);

        var conflicto = await EnviarAsync("POST", "/api/v1/vinculos", srm, Proveedor("900500600", new ReferenciaExternaDto("SRM", "ADP-6")), esperado: 409);
        (await conflicto.ReadAsTextAsync()).ShouldContain("referencia-en-conflicto");

        var ajena = await EnviarAsync("POST", "/api/v1/vinculos", marco, Proveedor("900500601", new ReferenciaExternaDto("SRM", "ADP-7")), esperado: 422);
        (await ajena.ReadAsTextAsync()).ShouldContain("referencia-sistema-invalido");

        // (h) Cada sistema lo encuentra por su referencia.
        var porMarco = await EnviarAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=MARCO&id=PRV-9", srm, esperado: 200);
        porMarco.Id.ShouldBe(deSrm.Id);
        await EnviarAsync("GET", "/api/v1/vinculos/por-referencia?sistema=MARCO&id=PRV-9", srm with { Empresa = e.EmpresaB.Id }, esperado: 404);

        var historial = await EnviarAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{deSrm.Id}/historial", srm, esperado: 200);
        historial.Select(h => h.Tipo).ShouldContain("referencia_externa_agregada");
        historial.Last().Sistema.ShouldBe("MARCO");
    }

    [Fact]
    public async Task Los_miembros_de_un_area_deben_tener_rol_en_su_empresa()
    {
        var e = await PrepararAsync();
        var admin = new Credencial(e.Admin);
        var deA = await EnviarAsync<UsuarioDto>("POST", "/api/v1/usuarios", admin,
            new CrearUsuarioDto("Ana", "ana@constructora.co", null, [new RolAsignadoDto("Revisor", e.EmpresaA.Id)], null));
        var deB = await EnviarAsync<UsuarioDto>("POST", "/api/v1/usuarios", admin,
            new CrearUsuarioDto("Beto", "beto@constructora.co", null, [new RolAsignadoDto("Revisor", e.EmpresaB.Id)], null));

        var area = await EnviarAsync<AreaDto>("POST", "/api/v1/areas", admin with { Empresa = e.EmpresaA.Id }, new CrearAreaDto("juridica", "Jurídica"));
        area.Codigo.ShouldBe("JURIDICA");

        var conMiembro = await EnviarAsync<AreaDto>("PUT", $"/api/v1/areas/{area.Id}/miembros", admin with { Empresa = e.EmpresaA.Id },
            new MiembrosAreaDto([deA.Id]), esperado: 200);
        conMiembro.Miembros.ShouldBe([deA.Id]);

        var invalido = await EnviarAsync("PUT", $"/api/v1/areas/{area.Id}/miembros", admin with { Empresa = e.EmpresaA.Id },
            new MiembrosAreaDto([deB.Id]), esperado: 422);
        (await invalido.ReadAsTextAsync()).ShouldContain("miembros-invalidos");

        await EnviarAsync("GET", $"/api/v1/areas/{area.Id}", admin with { Empresa = e.EmpresaB.Id }, esperado: 404);
        await EnviarAsync("POST", "/api/v1/usuarios", admin,
            new CrearUsuarioDto("Ana bis", "ANA@constructora.co", null, null, null), esperado: 409);
    }

    // ---------- utilidades ----------

    private sealed record Credencial(string? ApiKey, Guid? Empresa = null, string? Delegado = null, string? Bearer = null);

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
                s.WithRequestHeader("X-Usuario-Delegado", credencial.Delegado);
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());

            if (esperado is null)
                s.IgnoreStatusCode();
            else
                s.StatusCodeShouldBe(esperado.Value);
        });
}
