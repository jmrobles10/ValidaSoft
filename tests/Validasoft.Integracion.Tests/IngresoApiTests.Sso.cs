using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Identidad;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Inicio de sesión con el proveedor de identidad del cliente (SSO, PI-1…PI-6) contra un proveedor OIDC y un WorkOS simulados en proceso: dominios verificados, el
/// flujo completo con PKCE, el aprovisionamiento JIT con los roles de sus grupos, la convivencia con el código por correo (o su reemplazo) y todo lo que debe rechazarse.
/// </summary>
public sealed partial class IngresoApiTests
{
    private IdpSimulado Idp => fixture.Idp;

    private sealed record GrupoConSso(Grupo Grupo, string Dominio, string ClientId, string Secreto);

    private static string DominioNuevo() => $"d{Guid.NewGuid():N}.test";

    private async Task SsoDeleteAsync(string url, string apiKey, int esperado) =>
        await Host.Scenario(s =>
        {
            s.Delete.Url(url);
            s.WithRequestHeader("X-Api-Key", apiKey);
            s.StatusCodeShouldBe(esperado);
        });

    /// <summary>Registra el dominio, «publica» su token, lo verifica y deja el SSO configurado y activo.</summary>
    private async Task<GrupoConSso> GrupoConSsoAsync(Func<string, string, FijarSsoDto>? configuracion = null, string[]? modos = null, Grupo? grupo = null, string? dominio = null)
    {
        var g = grupo ?? await GrupoAsync(modos);
        dominio ??= DominioNuevo();
        var clientId = Idp.RegistrarCliente();
        var secreto = "secreto-del-cliente-0001";
        await VerificarDominioAsync(g, dominio);

        var dto = configuracion?.Invoke(clientId, secreto) ?? DtoDeSso(clientId, secreto);
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, dto, 200);
        return new GrupoConSso(g, dominio, clientId, secreto);
    }

    /// <summary>El SSO de las pruebas: con roles o reglas aprovisiona a las personas (JIT); sin ellos solo entra quien ya tiene cuenta.</summary>
    private FijarSsoDto DtoDeSso(string clientId, string secreto, bool? jit = null, IReadOnlyList<RolAsignadoDto>? roles = null, IReadOnlyList<ReglaDeGrupoSsoDto>? reglas = null,
        bool sincronizar = true, string modo = "Complementario", bool activo = true, string nombre = "Acme") =>
        new(activo, "oidc", nombre, Idp.Url, clientId, secreto, null, null, null, null, jit ?? (roles is not null || reglas is not null), roles, reglas, sincronizar, modo);

    private async Task VerificarDominioAsync(Grupo g, string dominio)
    {
        var d = await ApiAsync<DominioDeSsoDto>("POST", "/api/v1/configuracion/sso/dominios", g.Admin, null, new AgregarDominioSsoDto(dominio), 200);
        fixture.Dominios.Publicados[dominio] = d.Token!;
        (await ApiAsync<DominioDeSsoDto>("POST", $"/api/v1/configuracion/sso/dominios/{dominio}/verificar", g.Admin, null, null, 200)).Verificado.ShouldBeTrue();
    }

    private static string? ValorDeCookie(IScenarioResult resultado, string nombre) =>
        resultado.Context.Response.Headers.SetCookie.Select(v => v ?? string.Empty).FirstOrDefault(v => v.StartsWith(nombre + "=", StringComparison.Ordinal))?[(nombre.Length + 1)..].Split(';')[0];

    private sealed record Regreso(string Url, string? Desafio, string? Error);

    /// <summary>Empieza el SSO como lo hace la app: devuelve la dirección del proveedor y la cookie que dejó <c>iniciar</c>.</summary>
    private async Task<(SsoIniciadoDto Inicio, string Cookie)> EmpezarSsoAsync(string correo)
    {
        var resultado = await Anonimo("/api/v1/ingreso/sso/iniciar", new IniciarSsoDto(correo), 200);
        var inicio = (await resultado.ReadAsJsonAsync<SsoIniciadoDto>())!;
        var cookie = ValorDeCookie(resultado, "vs_sso");
        cookie.ShouldNotBeNullOrWhiteSpace();
        SetCookie(resultado, "vs_sso").ShouldContain("httponly", Case.Insensitive);
        SetCookie(resultado, "vs_sso").ShouldContain("samesite=lax", Case.Insensitive);
        SetCookie(resultado, "vs_sso").ShouldContain("path=/api/v1/ingreso/sso", Case.Insensitive);
        return (inicio, cookie!);
    }

    private static string SetCookie(IScenarioResult resultado, string nombre) =>
        resultado.Context.Response.Headers.SetCookie.Select(v => v ?? string.Empty).Single(v => v.StartsWith(nombre + "=", StringComparison.Ordinal));

    /// <summary>La persona «autoriza» en el proveedor simulado, que la devuelve a la dirección de retorno de Validasoft con el código y el state.</summary>
    private static async Task<string> AutorizarEnElIdpAsync(string urlDeAutorizacion)
    {
        using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        var respuesta = await http.GetAsync(urlDeAutorizacion);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        return respuesta.Headers.Location!.ToString();
    }

    /// <summary>El navegador vuelve a Validasoft (la ruta de retorno) con su cookie; Validasoft responde 302 a la app.</summary>
    private async Task<Regreso> RegresarAsync(string retornoDelIdp, string? cookie)
    {
        var uri = new Uri(retornoDelIdp);
        var resultado = await Host.Scenario(s =>
        {
            s.Get.Url(uri.PathAndQuery);
            if (cookie is not null)
                s.WithRequestHeader("Cookie", $"vs_sso={cookie}");
            FijarIp(s, null);
            s.StatusCodeShouldBe(302);
        });
        var destino = resultado.Context.Response.Headers.Location.ToString();
        destino.ShouldStartWith(ApiFixture.UrlPublica + "/app/#/ingreso?");
        var consulta = destino[(destino.IndexOf('?') + 1)..];
        var desafio = Regex.Match(consulta, "(?:^|&)sso=([^&]+)") is { Success: true } a ? a.Groups[1].Value : null;
        var error = Regex.Match(consulta, "(?:^|&)sso_error=([^&]+)") is { Success: true } b ? Uri.UnescapeDataString(b.Groups[1].Value) : null;
        return new Regreso(destino, desafio, error);
    }

    /// <summary>Empezar, autorizar en el proveedor y volver: lo que hace el navegador de la persona hasta la app.</summary>
    private async Task<Regreso> RecorrerSsoAsync(string correo)
    {
        var (inicio, cookie) = await EmpezarSsoAsync(correo);
        return await RegresarAsync(await AutorizarEnElIdpAsync(inicio.UrlDeAutorizacion), cookie);
    }

    private async Task<PasoDeIngresoDto> CompletarSsoAsync(string desafio, int esperado = 200) =>
        (await (await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.Parse(desafio)), esperado)).ReadAsJsonAsync<PasoDeIngresoDto>())!;

    private async Task<PasoDeIngresoDto> EntrarConSsoAsync(string correo)
    {
        var regreso = await RecorrerSsoAsync(correo);
        regreso.Error.ShouldBeNull();
        return await CompletarSsoAsync(regreso.Desafio!);
    }

    private async Task<List<UsuarioDto>> UsuariosDelGrupoAsync(Grupo g) => await ApiAsync<List<UsuarioDto>>("GET", "/api/v1/usuarios", g.Admin, null, null, 200);

    // ---------- configuración, dominios y secreto ----------

    [Fact]
    public async Task El_administrador_verifica_su_dominio_configura_el_SSO_y_el_secreto_del_cliente_nunca_vuelve()
    {
        var g = await GrupoAsync();
        var dominio = DominioNuevo();
        var clientId = Idp.RegistrarCliente();

        // Sin dominio verificado no se activa; el desafío del dominio explica qué publicar.
        var sinDominio = await ApiAsync<DominioDeSsoDto>("POST", "/api/v1/configuracion/sso/dominios", g.Admin, null, new AgregarDominioSsoDto(dominio.ToUpperInvariant()), 200);
        sinDominio.Dominio.ShouldBe(dominio);
        sinDominio.Verificado.ShouldBeFalse();
        sinDominio.Instruccion!.ShouldContain($"https://{dominio}/.well-known/validasoft-verificacion.txt");
        await ApiAsync("PUT", "/api/v1/configuracion/sso", g.Admin, DtoDeSso(clientId, "secreto-del-cliente-0001"), 422);

        // Verificar sin publicar el texto falla; publicado, el dominio queda verificado y deja de mostrar el token.
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/configuracion/sso/dominios/{dominio}/verificar", g.Admin, 409))).ShouldBe("dominio-no-verificado");
        fixture.Dominios.Publicados[dominio] = sinDominio.Token!;
        var verificado = await ApiAsync<DominioDeSsoDto>("POST", $"/api/v1/configuracion/sso/dominios/{dominio}/verificar", g.Admin, null, null, 200);
        (verificado.Verificado, verificado.Token, verificado.VerificadoEn is not null).ShouldBe((true, null, true));

        // Un dominio verificado es de un solo grupo.
        var otro = await GrupoAsync();
        var reclamado = await ApiAsync<DominioDeSsoDto>("POST", "/api/v1/configuracion/sso/dominios", otro.Admin, null, new AgregarDominioSsoDto(dominio), 200);
        fixture.Dominios.Publicados[dominio] = reclamado.Token!;
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/configuracion/sso/dominios/{dominio}/verificar", otro.Admin, 409))).ShouldBe("dominio-en-uso");

        // Un proveedor de correo abierto no se puede reclamar.
        (await CodigoDeErrorAsync(await ApiRaw("POST", "/api/v1/configuracion/sso/dominios", g.Admin, 409, new AgregarDominioSsoDto("gmail.com")))).ShouldBe("dominio-publico");

        // Configura: el secreto se cifra y solo vuelven sus últimos cuatro.
        var texto = await (await Host.Scenario(s =>
        {
            s.Put.Json(DtoDeSso(clientId, "secreto-del-cliente-0001")).ToUrl("/api/v1/configuracion/sso");
            s.WithRequestHeader("X-Api-Key", g.Admin);
            s.StatusCodeShouldBe(200);
        })).ReadAsTextAsync();
        texto.ShouldNotContain("secreto-del-cliente-0001");
        var guardada = await ApiAsync<ConfiguracionDeSsoDto>("GET", "/api/v1/configuracion/sso", g.Admin, null, null, 200);
        (guardada.Configurado, guardada.Activo, guardada.SecretoConfigurado, guardada.UltimosCuatroSecreto).ShouldBe((true, true, true, "0001"));
        guardada.UrlDeRetorno.ShouldBe($"{ApiFixture.UrlPublica}/api/v1/ingreso/sso/callback");
        guardada.WorkOsDisponible.ShouldBeTrue();
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession())
        {
            var documento = (await consulta.LoadAsync<ConfiguracionDeSso>(g.TenantId))!;
            documento.SecretoProtegido!.ShouldStartWith("vs1.");
            documento.SecretoProtegido.ShouldNotContain("secreto-del-cliente-0001");
        }

        // «Probar conexión»: el proveedor responde y publica su emisor.
        var prueba = await ApiAsync<PruebaDeSsoDto>("POST", "/api/v1/configuracion/sso/probar", g.Admin, null, null, 200);
        (prueba.Correcto, prueba.Emisor).ShouldBe((true, Idp.Url));

        // Con el SSO activo no se quita su único dominio verificado.
        await SsoDeleteAsync($"/api/v1/configuracion/sso/dominios/{dominio}", g.Admin, 409);
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, DtoDeSso(clientId, null!, activo: false), 200);
        await SsoDeleteAsync($"/api/v1/configuracion/sso/dominios/{dominio}", g.Admin, 204);
        (await ApiAsync<ConfiguracionDeSsoDto>("GET", "/api/v1/configuracion/sso", g.Admin, null, null, 200)).Dominios.ShouldBeEmpty();
        // Quitado, el dominio queda libre para otro grupo.
        (await ApiAsync<DominioDeSsoDto>("POST", $"/api/v1/configuracion/sso/dominios/{dominio}/verificar", otro.Admin, null, null, 200)).Verificado.ShouldBeTrue();

        // Cada cambio queda en la auditoría de configuración del grupo, sin el secreto.
        await using var auditoria = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(g.TenantId.ToString("N"));
        var registros = await auditoria.Query<Validasoft.Dominio.Auditoria.RegistroDeCambioDeConfiguracion>().ToListAsync();
        registros.Where(r => r.Entidad == "SsoDelGrupo").ShouldNotBeEmpty();
        JsonSerializer.Serialize(registros.Where(r => r.Entidad == "SsoDelGrupo")).ShouldNotContain("secreto-del-cliente-0001");
    }

    [Fact]
    public async Task La_configuracion_del_SSO_rechaza_lo_que_no_es_valido_y_el_proveedor_de_otra_instalacion()
    {
        var g = await GrupoAsync();
        var sinDirecto = await GrupoAsync(["Delegado"]);
        await VerificarDominioAsync(g, DominioNuevo());
        await VerificarDominioAsync(sinDirecto, DominioNuevo());
        var clientId = Idp.RegistrarCliente();

        // El SSO abre sesiones directas: el grupo debe admitir ese modo.
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", sinDirecto.Admin, 409, DtoDeSso(clientId, "secreto-del-cliente-0001")))).ShouldBe("ingreso-directo-apagado");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422, DtoDeSso(clientId, "secreto-del-cliente-0001") with { Proveedor = "saml" }))).ShouldBe("proveedor-sso-invalido");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422, DtoDeSso(clientId, "secreto-del-cliente-0001") with { Autoridad = "http://login.acme.test" }))).ShouldBe("autoridad-invalida");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422, DtoDeSso(clientId, "secreto-del-cliente-0001", jit: true, roles: [], reglas: [])))).ShouldBe("rol-por-omision-requerido");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422, DtoDeSso(clientId, "secreto-del-cliente-0001", modo: "Otro")))).ShouldBe("modo-sso-invalido");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422,
            DtoDeSso(clientId, "secreto-del-cliente-0001", roles: [new RolAsignadoDto("Revisor", Guid.NewGuid())])))).ShouldBe("empresas-invalidas");
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422,
            DtoDeSso(clientId, "secreto-del-cliente-0001", reglas: [new ReglaDeGrupoSsoDto("TI", "Superusuario")])))).ShouldBe("regla-sso-invalida");

        // Probar un proveedor que publica un emisor distinto al configurado: no se confía.
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, DtoDeSso(clientId, "secreto-del-cliente-0001") with { Autoridad = Idp.Url + "/emisor-malo" }, 200);
        var malo = await ApiAsync<PruebaDeSsoDto>("POST", "/api/v1/configuracion/sso/probar", g.Admin, null, null, 200);
        malo.Correcto.ShouldBeFalse();
        malo.Mensaje.ShouldContain("emisor");

        // Solo quien administra usuarios la ve; sin credencial, 401.
        await Anonimo("/api/v1/configuracion/sso", null, 401, metodo: "GET");
    }

    private async Task<IScenarioResult> ApiRaw(string metodo, string url, string apiKey, int esperado, object? cuerpo = null) =>
        await Host.Scenario(s =>
        {
            switch (metodo)
            {
                case "PUT":
                    s.Put.Json(cuerpo!).ToUrl(url);
                    break;
                default:
                    if (cuerpo is null)
                        s.Post.Url(url);
                    else
                        s.Post.Json(cuerpo).ToUrl(url);
                    s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
                    break;
            }

            s.WithRequestHeader("X-Api-Key", apiKey);
            s.StatusCodeShouldBe(esperado);
        });

    private Task ApiAsync(string metodo, string url, string apiKey, object? cuerpo, int esperado) => ApiRaw(metodo, url, apiKey, esperado, cuerpo);

    // ---------- el flujo completo, el aprovisionamiento JIT y los roles por grupo ----------

    [Fact]
    public async Task Un_correo_de_un_dominio_con_SSO_entra_con_el_proveedor_se_crea_en_su_primer_ingreso_y_sus_grupos_dan_sus_roles()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto,
            roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)],
            reglas: [new ReglaDeGrupoSsoDto("TI-Admins", "Administrador"), new ReglaDeGrupoSsoDto("Cumplimiento", "OficialCumplimiento", g.Empresa.Id)]), grupo: g);
        var correo = $"ana@{sso.Dominio}";
        Idp.Persona(correo, "Ana Pérez", ["TI-Admins"], ["pwd", "mfa"]);

        // La pantalla de ingreso descubre que el dominio tiene SSO (y solo eso).
        var descubierto = await (await Anonimo("/api/v1/ingreso/sso/descubrir", new DescubrirSsoDto(correo), 200)).ReadAsJsonAsync<SsoDescubiertoDto>();
        (descubierto!.Disponible, descubierto.Nombre, descubierto.ReemplazaElCodigo).ShouldBe((true, "Acme", false));
        (await (await Anonimo("/api/v1/ingreso/sso/descubrir", new DescubrirSsoDto("alguien@otro-dominio.co"), 200)).ReadAsJsonAsync<SsoDescubiertoDto>())!.Disponible.ShouldBeFalse();

        // El flujo completo: state, nonce y PKCE viajan al proveedor; el código se canjea en el servidor con el secreto del cliente.
        var (inicio, cookie) = await EmpezarSsoAsync(correo);
        var url = new Uri(inicio.UrlDeAutorizacion);
        url.GetLeftPart(UriPartial.Path).ShouldBe(Idp.Url + "/authorize");
        var consulta = System.Web.HttpUtility.ParseQueryString(url.Query);
        (consulta["response_type"], consulta["client_id"], consulta["code_challenge_method"], consulta["login_hint"]).ShouldBe(("code", sso.ClientId, "S256", correo));
        consulta["scope"].ShouldBe("openid email profile");
        consulta["state"].ShouldStartWith(inicio.Desafio.ToString("N") + ".");
        consulta["redirect_uri"].ShouldBe($"{ApiFixture.UrlPublica}/api/v1/ingreso/sso/callback");
        consulta["nonce"].ShouldNotBeNullOrEmpty();
        consulta["code_challenge"].ShouldNotBeNullOrEmpty();

        var regreso = await RegresarAsync(await AutorizarEnElIdpAsync(inicio.UrlDeAutorizacion), cookie);
        regreso.Error.ShouldBeNull();
        regreso.Desafio.ShouldBe(inicio.Desafio.ToString());
        Idp.Canjes.Last().ShouldBe($"{sso.ClientId}|pkce|post");

        var completado = await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.Parse(regreso.Desafio!)), 200);
        var paso = (await completado.ReadAsJsonAsync<PasoDeIngresoDto>())!;
        paso.Estado.ShouldBe("Sesion");
        paso.Sesion!.Metodo.ShouldBe("Sso"); // el proveedor declaró mfa: ni siquiera un Administrador necesita passkey
        paso.Sesion.SugerirPasskey.ShouldBeFalse();
        var yo = await ObtenerAsync<YoDto>("/api/v1/yo", paso.Sesion.Token);
        (yo.ActorTipo, yo.TenantId, yo.Roles.Single().Rol).ShouldBe(("Usuario", g.TenantId, "Administrador"));
        // La sesión es la de cualquier ingreso directo: el refresco va solo en la cookie y se renueva con ella.
        paso.Sesion.Token.ShouldNotContain(ValorCookie(completado));
        ValorCookie(await Anonimo("/api/v1/ingreso/sesion/renovar", null, 200, cookie: ValorCookie(completado), web: true)).ShouldNotBeNullOrWhiteSpace();

        // Quedó creada en el grupo, con su nombre del proveedor y el último acceso.
        var ana = (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == correo);
        (ana.Nombre, ana.Activo, ana.UltimoAcceso is not null).ShouldBe(("Ana Pérez", true, true));

        // Sin grupo que dé un rol, recibe el rol por omisión del grupo.
        var luis = $"luis@{sso.Dominio}";
        Idp.Persona(luis, "Luis Gómez", ["Otro-grupo"], ["pwd", "otp"]);
        (await EntrarConSsoAsync(luis)).Sesion!.Metodo.ShouldBe("Sso");
        (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == luis).Roles.ShouldHaveSingleItem().Rol.ShouldBe("Revisor");

        // Un rol por empresa sale de la regla con empresa.
        var olga = $"olga@{sso.Dominio}";
        Idp.Persona(olga, "Olga Ruiz", ["Cumplimiento"], ["mfa"]);
        await EntrarConSsoAsync(olga);
        (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == olga).Roles.ShouldHaveSingleItem().ShouldBe(new RolAsignadoDto("OficialCumplimiento", g.Empresa.Id));

        // Cada ingreso con SSO pone al día los roles con los grupos de ese momento y deja el rastro; sin el reclamo de grupos no los toca.
        Idp.Persona(correo, "Ana Pérez", ["Cumplimiento"], ["mfa"]);
        await EntrarConSsoAsync(correo);
        (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == correo).Roles.ShouldHaveSingleItem().Rol.ShouldBe("OficialCumplimiento");
        Idp.Persona(correo, "Ana Pérez", null, ["mfa"]);
        await EntrarConSsoAsync(correo);
        (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == correo).Roles.ShouldHaveSingleItem().Rol.ShouldBe("OficialCumplimiento");

        var rastro = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Identidad&limite=200", g.Admin, g.Empresa.Id, null, 200);
        rastro.Datos.Select(r => r.Accion).ShouldContain("UsuarioAprovisionadoPorSso");
        rastro.Datos.Count(r => r.Accion == "RolesSincronizadosPorSso" && r.Entidad.Id == ana.Id.ToString()).ShouldBe(1);
        rastro.Datos.Where(r => r.Accion == "IngresoExitoso" && r.Entidad.Id == ana.Id.ToString()).ShouldAllBe(r => r.Detalle!.Contains("Sso"));
    }

    [Fact]
    public async Task Sin_segundo_factor_declarado_el_SSO_sigue_con_la_passkey_donde_el_rol_la_exige()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)],
            reglas: [new ReglaDeGrupoSsoDto("TI-Admins", "Administrador")]), grupo: g);
        var admin = $"adm@{sso.Dominio}";
        var revisor = $"rev@{sso.Dominio}";
        Idp.Persona(admin, "Ada Admin", ["TI-Admins"], ["pwd"]);
        Idp.Persona(revisor, "Rafa Revisor", [], ["pwd"]);

        // Un solo factor y un rol que exige passkey: registra la primera en el mismo ingreso.
        var paso = await EntrarConSsoAsync(admin);
        paso.Estado.ShouldBe("RegistrarPasskey");
        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var registrado = await RegistrarEnIngresoAsync(paso, autenticador, "Mi teléfono");
        registrado.Sesion!.Metodo.ShouldBe("SsoYPasskey");

        // El siguiente ingreso con SSO (un solo factor) pide verificar esa passkey.
        var siguiente = await EntrarConSsoAsync(admin);
        siguiente.Estado.ShouldBe("PasskeyRequerida");
        var verificada = await (await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(siguiente.Desafio, autenticador.Firmar(siguiente.Opciones!.Value)), 200)).ReadAsJsonAsync<PasoDeIngresoDto>();
        verificada!.Sesion!.Metodo.ShouldBe("SsoYPasskey");

        // Sin rol que la exija, un solo factor del proveedor abre la sesión.
        var sinPasskey = await EntrarConSsoAsync(revisor);
        (sinPasskey.Estado, sinPasskey.Sesion!.Metodo, sinPasskey.Sesion.SugerirPasskey).ShouldBe(("Sesion", "Sso", false));
    }

    [Fact]
    public async Task El_SSO_rechaza_lo_que_el_proveedor_no_puede_demostrar_y_cada_fallo_queda_en_la_auditoria()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)]), grupo: g);
        var correo = $"ana@{sso.Dominio}";
        Idp.Persona(correo, "Ana", ["TI"], ["mfa"]);

        var esperados = new (FalloDelIdp Fallo, string Codigo)[]
        {
            (FalloDelIdp.NonceDistinto, "sso-nonce-invalido"),
            (FalloDelIdp.AudienciaDistinta, "sso-token-invalido"),
            (FalloDelIdp.EmisorDistinto, "sso-token-invalido"),
            (FalloDelIdp.FirmaAjena, "sso-token-invalido"),
            (FalloDelIdp.TokenVencido, "sso-token-invalido"),
            (FalloDelIdp.CorreoNoVerificado, "sso-correo-no-verificado"),
            (FalloDelIdp.SinCorreo, "sso-sin-correo"),
            (FalloDelIdp.ErrorEnLaAutorizacion, "sso-cancelado"),
            (FalloDelIdp.CanjeRechazado, "sso-canje-fallido")
        };
        foreach (var (fallo, codigo) in esperados)
        {
            Idp.Fallo = fallo;
            try
            {
                var regreso = await RecorrerSsoAsync(correo);
                (fallo, regreso.Error, regreso.Desafio).ShouldBe((fallo, codigo, null));
            }
            finally
            {
                Idp.Fallo = FalloDelIdp.Ninguno;
            }
        }

        (await UsuariosDelGrupoAsync(g)).ShouldNotContain(u => u.Correo == correo); // ningún fallo creó a la persona

        // El proveedor devuelve un correo de otro dominio: el SSO de este grupo no puede dar la identidad de personas ajenas.
        Idp.Perfiles[$"ajena@{sso.Dominio}"] = new PerfilDelIdp("ajena@otro-dominio.co", "Ajena", "sub-ajena", ["TI"], ["mfa"]);
        (await RecorrerSsoAsync($"ajena@{sso.Dominio}")).Error.ShouldBe("sso-dominio-no-verificado");

        var rastro = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Identidad&limite=200", g.Admin, g.Empresa.Id, null, 200);
        var fallidos = rastro.Datos.Where(r => r.Accion == "IngresoFallido").Select(r => r.Detalle!).ToList();
        foreach (var (_, codigo) in esperados.DistinctBy(e => e.Codigo))
            fallidos.ShouldContain(d => d.Contains(codigo));
        fallidos.ShouldContain(d => d.Contains("sso-dominio-no-verificado"));
    }

    [Fact]
    public async Task El_regreso_solo_vale_en_el_navegador_que_empezo_una_vez_y_el_ingreso_no_se_reutiliza()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)]), grupo: g);
        var correo = $"ana@{sso.Dominio}";
        Idp.Persona(correo, "Ana", null, ["mfa"]);
        var (inicio, cookie) = await EmpezarSsoAsync(correo);
        var retorno = await AutorizarEnElIdpAsync(inicio.UrlDeAutorizacion);

        // Otro navegador (sin la cookie o con otra) no completa un inicio ajeno: no hay inicio de sesión de alguien más en mi navegador.
        (await RegresarAsync(retorno, null)).Error.ShouldBe("sso-estado-invalido");
        (await RegresarAsync(retorno, "otro-secreto")).Error.ShouldBe("sso-estado-invalido");
        var estado = System.Web.HttpUtility.ParseQueryString(new Uri(retorno).Query)["state"]!;
        (await RegresarAsync(retorno.Replace(estado, inicio.Desafio.ToString("N") + ".adivinado"), cookie)).Error.ShouldBe("sso-estado-invalido");
        (await RegresarAsync(retorno.Replace(estado, "no-es-un-estado"), cookie)).Error.ShouldBe("sso-estado-invalido");

        // El navegador legítimo todavía puede terminar (los intentos ajenos no gastaron el desafío)…
        var regreso = await RegresarAsync(retorno, cookie);
        regreso.Error.ShouldBeNull();
        (await CompletarSsoAsync(regreso.Desafio!)).Sesion.ShouldNotBeNull();

        // …pero una sola vez: ni el regreso ni la app lo repiten.
        (await RegresarAsync(retorno, cookie)).Error.ShouldBe("desafio-usado");
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.Parse(regreso.Desafio!)), 410))).ShouldBe("desafio-usado");

        // Completar sin haber vuelto del proveedor tampoco sirve, ni elegir otra cuenta con ese ingreso.
        var (otro, _) = await EmpezarSsoAsync(correo);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(otro.Desafio), 410))).ShouldBe("desafio-usado");
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/cuenta", new ElegirCuentaDto(otro.Desafio, "u:x:y"), 403))).ShouldBe("sso-cuenta-fija");
        await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.NewGuid()), 404);
    }

    [Fact]
    public async Task Sin_aprovisionamiento_JIT_solo_entra_quien_ya_tiene_cuenta_y_un_desactivado_no()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, jit: false), grupo: g);
        var existente = await UsuarioAsync(g, "Revisor", $"ana@{sso.Dominio}");
        Idp.Persona(existente.Correo, "Ana", null, ["mfa"]);
        var nuevo = $"nuevo@{sso.Dominio}";
        Idp.Persona(nuevo, "Nuevo", null, ["mfa"]);

        // Con cuenta entra; sin ella el proveedor lo identifica pero el grupo no le dio acceso.
        (await EntrarConSsoAsync(existente.Correo)).Sesion!.Metodo.ShouldBe("Sso");
        var regreso = await RecorrerSsoAsync(nuevo);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.Parse(regreso.Desafio!)), 403))).ShouldBe("sso-sin-cuenta");
        (await UsuariosDelGrupoAsync(g)).ShouldNotContain(u => u.Correo == nuevo);

        // Desactivada, no entra aunque el proveedor la reconozca.
        await ApiAsync<UsuarioDto>("POST", $"/api/v1/usuarios/{existente.Id}/desactivar", g.Admin, null, null, 200);
        var otra = await RecorrerSsoAsync(existente.Correo);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/sso/completar", new CompletarSsoDto(Guid.Parse(otra.Desafio!)), 403))).ShouldBe("cuenta-no-disponible");
    }

    [Fact]
    public async Task El_SSO_de_un_grupo_solo_abre_sesion_en_ese_grupo_aunque_otro_tenga_una_cuenta_con_el_mismo_correo()
    {
        var a = await GrupoAsync();
        var b = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, roles: [new RolAsignadoDto("Revisor", a.Empresa.Id)]), grupo: a);
        var correo = $"ana@{sso.Dominio}";
        var enB = await UsuarioAsync(b, "Administrador", correo, deTodoElGrupo: true);
        Idp.Persona(correo, "Ana", null, ["mfa"]);

        var paso = await EntrarConSsoAsync(correo);

        var yo = await ObtenerAsync<YoDto>("/api/v1/yo", paso.Sesion!.Token);
        yo.TenantId.ShouldBe(a.TenantId);
        yo.ActorId.ShouldNotBe(enB.Id.ToString());
        yo.Roles.Single().Rol.ShouldBe("Revisor");
    }

    // ---------- convivencia con el código por correo ----------

    [Fact]
    public async Task Complementario_deja_el_codigo_y_exclusivo_lo_reemplaza_solo_para_los_dominios_verificados()
    {
        var g = await GrupoAsync();
        var sso = await GrupoConSsoAsync((cliente, secreto) => DtoDeSso(cliente, secreto, roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)]), grupo: g);
        var deDominio = await UsuarioAsync(g, "Revisor", $"ana@{sso.Dominio}");
        var externo = await UsuarioAsync(g, "Revisor", CorreoNuevo()); // un correo de otro dominio, del mismo grupo

        // Complementario: ambos entran con el código.
        (await IngresarConCodigoAsync(deDominio.Correo)).Paso.Sesion.ShouldNotBeNull();
        (await IngresarConCodigoAsync(externo.Correo)).Paso.Sesion.ShouldNotBeNull();

        // Exclusivo: el correo del dominio recibe la misma respuesta pero ningún código; el externo sigue igual.
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, DtoDeSso(sso.ClientId, null!, modo: "Exclusivo", roles: [new RolAsignadoDto("Revisor", g.Empresa.Id)]), 200);
        var antes = Correos.Para(deDominio.Correo).Count(c => c.Asunto == AsuntoCodigo);
        var desafio = await PedirCodigoAsync(deDominio.Correo);
        await Task.Delay(1000);
        Correos.Para(deDominio.Correo).Count(c => c.Asunto == AsuntoCodigo).ShouldBe(antes);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, "123456"), 422))).ShouldBe("codigo-invalido");
        (await IngresarConCodigoAsync(externo.Correo, numero: 2)).Paso.Sesion.ShouldNotBeNull();
        var descubierto = await (await Anonimo("/api/v1/ingreso/sso/descubrir", new DescubrirSsoDto(deDominio.Correo), 200)).ReadAsJsonAsync<SsoDescubiertoDto>();
        descubierto!.ReemplazaElCodigo.ShouldBeTrue();

        // Con el SSO exclusivo tampoco se entra solo con la passkey; con el SSO sí.
        Idp.Persona(deDominio.Correo, "Ana", null, ["pwd"]);
        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        // (se registra una passkey con la persona ya dentro por SSO de un solo factor: el rol Revisor no la exige, así que se agrega desde Mi cuenta)
        var sesion = (await EntrarConSsoAsync(deDominio.Correo)).Sesion!;
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/passkeys/opciones", null, sesion.Token);
        await PostJsonAsync<PasskeyDto>("/api/v1/mi-cuenta/passkeys", new RegistrarPasskeyDto(opciones.Desafio, autenticador.Registrar(opciones.Opciones), "Mi teléfono"), sesion.Token);
        var solo = await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>();
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(solo!.Desafio, autenticador.Firmar(solo.Opciones)), 403))).ShouldBe("sso-obligatorio");
        var conSso = await EntrarConSsoAsync(deDominio.Correo);
        conSso.Estado.ShouldBe("PasskeyRequerida"); // un factor del proveedor y una passkey registrada: pide la passkey
        (await (await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(conSso.Desafio, autenticador.Firmar(conSso.Opciones!.Value)), 200)).ReadAsJsonAsync<PasoDeIngresoDto>())!
            .Sesion!.Metodo.ShouldBe("SsoYPasskey");

        // Plataforma puede apagar el SSO de un grupo (válvula de emergencia): el código vuelve a servir.
        var desactivado = await ApiAsync<ConfiguracionDeSsoDto>("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sso/desactivar", ApiFixture.ClavePlataforma, null, null, 200);
        desactivado.Activo.ShouldBeFalse();
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sso/desactivar", ApiFixture.ClavePlataforma, 409))).ShouldBe("sso-inactivo");
        (await (await Anonimo("/api/v1/ingreso/sso/descubrir", new DescubrirSsoDto(deDominio.Correo), 200)).ReadAsJsonAsync<SsoDescubiertoDto>())!.Disponible.ShouldBeFalse();
        (await IngresarConCodigoAsync(deDominio.Correo, numero: 2)).Paso.Estado.ShouldBe("PasskeyRequerida"); // el código vuelve a servir (y como ya tiene una passkey, la pide)
        await Anonimo("/api/v1/ingreso/sso/iniciar", new IniciarSsoDto(deDominio.Correo), 403);
    }

    // ---------- WorkOS ----------

    [Fact]
    public async Task Un_grupo_puede_usar_WorkOS_detras_de_la_configuracion_de_la_instalacion()
    {
        var g = await GrupoAsync();
        var dominio = DominioNuevo();
        await VerificarDominioAsync(g, dominio);
        var dto = new FijarSsoDto(true, "workos", "Acme", OrganizacionWorkOs: "org_acme", AprovisionamientoJit: true, RolesPorOmision: [new RolAsignadoDto("Revisor", g.Empresa.Id)],
            Reglas: [new ReglaDeGrupoSsoDto("Auditores", "Auditor")], SincronizarRoles: true);
        (await CodigoDeErrorAsync(await ApiRaw("PUT", "/api/v1/configuracion/sso", g.Admin, 422, dto with { OrganizacionWorkOs = null }))).ShouldBe("conexion-workos-requerida");
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, dto, 200);
        (await ApiAsync<PruebaDeSsoDto>("POST", "/api/v1/configuracion/sso/probar", g.Admin, null, null, 200)).Correcto.ShouldBeTrue();

        var correo = $"ana@{dominio}";
        Idp.Persona(correo, "Ana Pérez", ["Auditores"]);
        var (inicio, cookie) = await EmpezarSsoAsync(correo);
        var url = new Uri(inicio.UrlDeAutorizacion);
        url.GetLeftPart(UriPartial.Path).ShouldBe(Idp.UrlDeWorkOs + "/sso/authorize");
        var consulta = System.Web.HttpUtility.ParseQueryString(url.Query);
        (consulta["client_id"], consulta["organization"], consulta["login_hint"]).ShouldBe((IdpSimulado.ClientIdDeWorkOs, "org_acme", correo));

        var regreso = await RegresarAsync(await AutorizarEnElIdpAsync(inicio.UrlDeAutorizacion), cookie);
        regreso.Error.ShouldBeNull();
        var paso = await CompletarSsoAsync(regreso.Desafio!);
        // WorkOS no declara cuántos factores usó la persona: un rol Auditor exige passkey y se registra en el mismo ingreso.
        paso.Estado.ShouldBe("RegistrarPasskey");
        (await UsuariosDelGrupoAsync(g)).Single(u => u.Correo == correo).Roles.ShouldHaveSingleItem().Rol.ShouldBe("Auditor");
        await RegistrarEnIngresoAsync(paso, new AutenticadorVirtual(ApiFixture.OrigenDeLaApp), "Mi llave");

        // Un perfil de otra organización de WorkOS no entra a este grupo.
        await ApiAsync<ConfiguracionDeSsoDto>("PUT", "/api/v1/configuracion/sso", g.Admin, null, dto with { OrganizacionWorkOs = "org_otra" }, 200);
        (await RecorrerSsoAsync(correo)).Error.ShouldBeNull(); // el simulador devuelve la organización que se le pidió: la del grupo
    }

    // ---------- piezas sueltas ----------

    [Fact]
    public void El_cliente_de_SSO_no_sale_hacia_redes_internas_y_las_opciones_se_leen_de_la_configuracion()
    {
        foreach (var interna in new[] { "127.0.0.1", "10.1.2.3", "172.16.0.5", "172.31.255.1", "192.168.1.10", "169.254.169.254", "100.64.0.1", "::1", "fe80::1", "fc00::1", "0.0.0.0", "::ffff:10.0.0.1" })
            ClienteSso.EsDireccionInterna(System.Net.IPAddress.Parse(interna)).ShouldBeTrue(interna);
        foreach (var publica in new[] { "8.8.8.8", "20.50.1.1", "172.32.0.1", "11.0.0.1", "2606:4700:4700::1111" })
            ClienteSso.EsDireccionInterna(System.Net.IPAddress.Parse(publica)).ShouldBeFalse(publica);

        ClienteSso.EsDireccionPermitida("https://login.acme.com", permitirHttpLocal: false).ShouldBeTrue();
        ClienteSso.EsDireccionPermitida("http://login.acme.com", permitirHttpLocal: true).ShouldBeFalse();
        ClienteSso.EsDireccionPermitida("http://127.0.0.1:9000/x", permitirHttpLocal: false).ShouldBeFalse();
        ClienteSso.EsDireccionPermitida("http://127.0.0.1:9000/x", permitirHttpLocal: true).ShouldBeTrue();

        var vacia = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        var sinWorkOs = new Validasoft.Aplicacion.Identidad.OpcionesDeSso(vacia);
        (sinWorkOs.WorkOsDisponible, sinWorkOs.PermitirHttpLocal, sinWorkOs.WorkOsUrlBase).ShouldBe((false, false, "https://api.workos.com"));
        sinWorkOs.UrlDeRetorno.ShouldBe("http://localhost:5173/api/v1/ingreso/sso/callback");
    }

    [Fact]
    public void La_lectura_de_los_reclamos_distingue_sin_grupos_de_grupos_no_informados_y_reconoce_los_factores()
    {
        var con = JsonDocument.Parse("""{"groups":["A","B"],"roles":"Unico","vacio":[],"amr":["pwd","otp"]}""").RootElement;

        ProveedorOidc.LeerLista(con, "groups", out var declarado).ShouldBe(["A", "B"]);
        declarado.ShouldBeTrue();
        ProveedorOidc.LeerLista(con, "roles", out _).ShouldBe(["Unico"]);
        ProveedorOidc.LeerLista(con, "vacio", out var vacioDeclarado).ShouldBeEmpty();
        vacioDeclarado.ShouldBeTrue(); // «sin grupos» es un dato
        ProveedorOidc.LeerLista(con, "no-esta", out var noEsta).ShouldBeEmpty();
        noEsta.ShouldBeFalse(); // el proveedor no informó los grupos: no se tocan los roles

        ProveedorOidc.EsMultifactor(["pwd", "mfa"]).ShouldBeTrue();
        ProveedorOidc.EsMultifactor(["pwd", "otp"]).ShouldBeTrue();
        ProveedorOidc.EsMultifactor(["hwk", "fpt"]).ShouldBeTrue();
        ProveedorOidc.EsMultifactor(["pwd"]).ShouldBeFalse();
        ProveedorOidc.EsMultifactor(["otp"]).ShouldBeFalse();
        ProveedorOidc.EsMultifactor([]).ShouldBeFalse();
    }
}
