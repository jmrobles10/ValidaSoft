using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Ingreso directo sin contraseñas (ING-1…ING-7): código por correo (vencimiento, un solo uso, límites, sin revelar si el correo existe), modos por
/// grupo, passkeys obligatorias por rol, ingreso solo con passkey, renovación con inactividad, Mi cuenta, gestión por administradores y auditoría.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class IngresoApiTests(ApiFixture fixture)
{
    private const string AsuntoCodigo = "Tu código para ingresar a Validasoft";
    private const string Cookie = "vs_usuario";

    private IAlbaHost Host => fixture.Host;

    private CorreosCapturados Correos => fixture.Correos;

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex RegexCodigo();

    private sealed record Grupo(Guid TenantId, string Admin, EmpresaDto Empresa);

    // ---------- código: vencimiento, un solo uso, límites y no enumeración ----------

    [Fact]
    public async Task Pedir_el_codigo_responde_igual_exista_o_no_el_correo_y_solo_envia_a_cuentas_con_ingreso_directo()
    {
        var directo = await GrupoAsync();
        var soloDelegado = await GrupoAsync(["Delegado"]);
        var conCuenta = await UsuarioAsync(directo, "Revisor");
        var enGrupoDelegado = await UsuarioAsync(soloDelegado, "Revisor");
        var inexistente = CorreoNuevo();

        var respuestas = new List<string>();
        foreach (var correo in new[] { conCuenta.Correo, enGrupoDelegado.Correo, inexistente, conCuenta.Correo.ToUpperInvariant() })
            respuestas.Add(await (await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(correo), 200)).ReadAsTextAsync());

        // Misma forma y mismo mensaje; solo cambian el identificador del desafío y la hora.
        var formas = respuestas.Select(r => JsonDocument.Parse(r).RootElement).ToList();
        formas.Select(f => f.EnumerateObject().Select(p => p.Name).Order().ToArray()).Distinct(new SecuenciaIgual()).Count().ShouldBe(1);
        formas.Select(f => f.GetProperty("mensaje").GetString()).Distinct().Count().ShouldBe(1);
        formas.Select(f => f.GetProperty("desafio").GetGuid()).Distinct().Count().ShouldBe(4);

        await Correos.EsperarAsync(conCuenta.Correo, c => c.Asunto == AsuntoCodigo);
        await Task.Delay(1000);
        Correos.Para(enGrupoDelegado.Correo).ShouldBeEmpty(); // el grupo no admite el modo directo
        Correos.Para(inexistente).ShouldBeEmpty();

        var invalido = await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto("sin-arroba"), 422);
        (await CodigoDeErrorAsync(invalido)).ShouldBe("correo-invalido");
    }

    [Fact]
    public async Task Un_codigo_correcto_sin_rol_que_exija_passkey_abre_la_sesion_con_cookie_y_los_permisos_de_sus_roles()
    {
        var g = await GrupoAsync();
        var revisor = await UsuarioAsync(g, "Revisor");

        var (paso, resultado) = await IngresarConCodigoAsync(revisor.Correo);

        paso.Estado.ShouldBe("Sesion");
        paso.Sesion!.Metodo.ShouldBe("Codigo");
        paso.Sesion.SugerirPasskey.ShouldBeTrue();
        paso.Sesion.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(10));

        var cookie = SetCookie(resultado);
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("path=/api/v1/ingreso/sesion", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("expires=", Case.Insensitive);
        paso.Sesion.Token.ShouldNotContain(ValorCookie(resultado)); // el refresco nunca va en el cuerpo

        // El token sirve como cualquier sesión de Validasoft: sus scopes son los del rol Revisor en su empresa.
        var yo = await ObtenerAsync<YoDto>("/api/v1/yo", paso.Sesion.Token);
        yo.ActorTipo.ShouldBe("Usuario");
        yo.ActorId.ShouldBe(revisor.Id.ToString());
        yo.TenantId.ShouldBe(g.TenantId);
        yo.Roles.ShouldHaveSingleItem().Rol.ShouldBe("Revisor");
        var conEmpresa = await ObtenerAsync<YoDto>("/api/v1/yo", paso.Sesion.Token, g.Empresa.Id);
        conEmpresa.Permisos.ShouldContain("vinculos:escribir");
        conEmpresa.Permisos.ShouldNotContain("usuarios:administrar");
        await Anonimo("/api/v1/usuarios", null, 403, metodo: "GET", bearer: paso.Sesion.Token);
    }

    [Fact]
    public async Task Un_codigo_incorrecto_descuenta_intentos_y_al_quinto_el_correcto_tampoco_sirve()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");
        var desafio = await PedirCodigoAsync(usuario.Correo);
        var codigo = await CodigoDelCorreoAsync(usuario.Correo);
        var malo = codigo == "000000" ? "111111" : "000000";

        for (var restantes = 4; restantes >= 0; restantes--)
        {
            var fallo = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, malo), 422);
            var problema = await fallo.ReadAsJsonAsync<JsonElement>();
            problema.GetProperty("codigo").GetString().ShouldBe("codigo-invalido");
            problema.GetProperty("intentosRestantes").GetInt32().ShouldBe(restantes);
        }

        var bloqueado = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, codigo), 429);
        (await CodigoDeErrorAsync(bloqueado)).ShouldBe("codigo-bloqueado");
    }

    [Fact]
    public async Task Un_codigo_solo_sirve_una_vez_y_vence_en_cinco_minutos()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");

        var (_, _) = await IngresarConCodigoAsync(usuario.Correo);
        var desafioUsado = await PedirCodigoAsync(usuario.Correo);
        var codigoUsado = await CodigoDelCorreoAsync(usuario.Correo, 2);
        (await VerificarCodigoAsync(desafioUsado, codigoUsado)).Paso.Estado.ShouldBe("Sesion");
        var segunda = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafioUsado, codigoUsado), 410);
        (await CodigoDeErrorAsync(segunda)).ShouldBe("desafio-usado");

        var desafioVencido = await PedirCodigoAsync(usuario.Correo);
        var codigoVencido = await CodigoDelCorreoAsync(usuario.Correo, 3);
        fixture.Reloj.Desfase = TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(5);
        try
        {
            var vencido = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafioVencido, codigoVencido), 410);
            (await CodigoDeErrorAsync(vencido)).ShouldBe("codigo-vencido");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(Guid.NewGuid(), "123456"), 404))).ShouldBe("desafio-no-encontrado");
    }

    [Fact]
    public async Task Un_correo_sin_cuenta_cuenta_intentos_como_uno_real_y_nunca_acepta_un_codigo()
    {
        var desafio = await PedirCodigoAsync(CorreoNuevo());

        for (var i = 0; i < 5; i++)
        {
            var fallo = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, "123456"), 422);
            (await fallo.ReadAsJsonAsync<JsonElement>()).GetProperty("intentosRestantes").GetInt32().ShouldBe(4 - i);
        }

        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, "123456"), 429))).ShouldBe("codigo-bloqueado");
    }

    [Fact]
    public async Task Se_envian_como_maximo_cinco_codigos_por_correo_y_hora_sin_revelarlo()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");

        var respuestas = new List<string>();
        for (var i = 0; i < 7; i++)
            respuestas.Add(JsonDocument.Parse(await (await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(usuario.Correo), 200)).ReadAsTextAsync())
                .RootElement.GetProperty("mensaje").GetString()!);
        respuestas.Distinct().Count().ShouldBe(1);

        await Correos.EsperarAsync(usuario.Correo, c => c.Asunto == AsuntoCodigo, cuantos: 5);
        await Task.Delay(1500);
        Correos.Para(usuario.Correo).Count(c => c.Asunto == AsuntoCodigo).ShouldBe(5);
    }

    [Fact]
    public async Task El_ingreso_tiene_su_propio_limite_por_ip()
    {
        var ip = "10.77." + Random.Shared.Next(1, 255) + "." + Random.Shared.Next(1, 255);
        for (var i = 0; i < ApiFixture.LimiteIngreso; i++)
            await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(CorreoNuevo()), 200, ip: ip);

        var excedido = await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(CorreoNuevo()), 429, ip: ip);
        (await CodigoDeErrorAsync(excedido)).ShouldBe("demasiadas-peticiones");
        await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(CorreoNuevo()), 200); // otra IP no se afecta
    }

    // ---------- passkeys: obligatoria por rol, solo passkey, revocación ----------

    [Theory]
    [InlineData("Administrador")]
    [InlineData("OficialCumplimiento")]
    [InlineData("Auditor")]
    public async Task Los_roles_que_exigen_passkey_no_entran_con_solo_el_codigo_y_la_registran_en_el_mismo_ingreso(string rol)
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, rol);

        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(usuario.Correo), await CodigoDelCorreoAsync(usuario.Correo))).Paso;

        paso.Estado.ShouldBe("RegistrarPasskey");
        paso.Sesion.ShouldBeNull();
        paso.Opciones!.Value.GetProperty("authenticatorSelection").GetProperty("userVerification").GetString().ShouldBe("required");
        paso.Opciones.Value.GetProperty("authenticatorSelection").GetProperty("residentKey").GetString().ShouldBe("required");

        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var registrada = await RegistrarEnIngresoAsync(paso, autenticador, "Mi teléfono");
        registrada.Sesion!.Metodo.ShouldBe("CodigoYPasskey");
        registrada.Sesion.SugerirPasskey.ShouldBeFalse();
    }

    [Fact]
    public async Task Con_passkey_registrada_se_entra_solo_con_ella_sin_correo_ni_codigo()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);

        var sesion = await EntrarConPasskeyAsync(autenticador);

        sesion.Metodo.ShouldBe("Passkey");
        var yo = await ObtenerAsync<YoDto>("/api/v1/yo", sesion.Token);
        yo.ActorId.ShouldBe(admin.Id.ToString());
        (await ObtenerAsync<YoDto>("/api/v1/yo", sesion.Token, g.Empresa.Id)).Permisos.ShouldContain("usuarios:administrar");
        var usuarios = await ObtenerAsync<List<UsuarioDto>>("/api/v1/usuarios", sesion.Token);
        usuarios.Single(u => u.Id == admin.Id).Passkeys.ShouldBe(1);
        usuarios.Single(u => u.Id == admin.Id).ExigePasskey.ShouldBeTrue();
    }

    [Fact]
    public async Task Con_passkey_el_codigo_solo_no_abre_sesion_y_pide_la_passkey_como_segundo_paso()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador");
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);

        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(admin.Correo), await CodigoDelCorreoAsync(admin.Correo, 2))).Paso;

        paso.Estado.ShouldBe("PasskeyRequerida");
        paso.Sesion.ShouldBeNull();
        paso.Opciones!.Value.GetProperty("allowCredentials").EnumerateArray().Single().GetProperty("id").GetString().ShouldBe(autenticador.CredencialIdBase64Url);
        var final = await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(paso.Desafio, autenticador.Firmar(paso.Opciones.Value)), 200);
        var sesion = (await final.ReadAsJsonAsync<PasoDeIngresoDto>())!.Sesion!;
        sesion.Metodo.ShouldBe("CodigoYPasskey");
        ValorCookie(final).ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Un_grupo_puede_exigir_passkey_a_todos_y_un_revisor_con_passkey_registrada_ya_no_entra_con_solo_el_codigo()
    {
        var g = await GrupoAsync(passkeyParaTodos: true);
        var revisor = await UsuarioAsync(g, "Revisor");
        (await VerificarCodigoAsync(await PedirCodigoAsync(revisor.Correo), await CodigoDelCorreoAsync(revisor.Correo))).Paso.Estado.ShouldBe("RegistrarPasskey");

        // Un revisor de un grupo que no la exige puede registrarla desde Mi cuenta; desde entonces el código solo ya no lo deja entrar.
        var libre = await GrupoAsync();
        var otro = await UsuarioAsync(libre, "Revisor");
        var (paso, _) = await IngresarConCodigoAsync(otro.Correo);
        var token = paso.Sesion!.Token;
        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/passkeys/opciones", null, token);
        await PostJsonAsync<PasskeyDto>("/api/v1/mi-cuenta/passkeys", new RegistrarPasskeyDto(opciones.Desafio, autenticador.Registrar(opciones.Opciones), "Llave"), token);

        var siguiente = (await VerificarCodigoAsync(await PedirCodigoAsync(otro.Correo), await CodigoDelCorreoAsync(otro.Correo, 2))).Paso;
        siguiente.Estado.ShouldBe("PasskeyRequerida");
        siguiente.Sesion.ShouldBeNull();
    }

    [Fact]
    public async Task Una_passkey_sin_verificacion_de_usuario_o_con_contador_que_no_crece_se_rechaza()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador");
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);

        // Sin huella ni PIN (UV falso).
        var sinVerificacion = await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200);
        var opciones = (await sinVerificacion.ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        autenticador.VerificaUsuario = false;
        var rechazada = await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(opciones.Desafio, autenticador.Firmar(opciones.Opciones)), 401);
        (await CodigoDeErrorAsync(rechazada)).ShouldBe("passkey-invalida");
        autenticador.VerificaUsuario = true;

        // Una aserción reutilizada (el contador no avanza) indica una credencial clonada.
        var primera = await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200);
        var o1 = (await primera.ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o1.Desafio, autenticador.Firmar(o1.Opciones)), 200);
        var segunda = await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200);
        var o2 = (await segunda.ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        autenticador.Contador -= 1; // el autenticador clonado repite el contador anterior
        var clonada = await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o2.Desafio, autenticador.Firmar(o2.Opciones)), 401);
        (await CodigoDeErrorAsync(clonada)).ShouldBe("passkey-clonada");
    }

    [Fact]
    public async Task Una_asercion_vale_solo_para_su_desafio_y_una_vez()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador");
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);

        var o = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        var firma = autenticador.Firmar(o.Opciones);
        await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o.Desafio, firma), 200);

        // La misma aserción otra vez: el desafío ya se consumió.
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o.Desafio, firma), 410))).ShouldBe("desafio-usado");

        // Una aserción firmada para otro desafío no sirve en este.
        var a = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        var b = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(b.Desafio, autenticador.Firmar(a.Opciones)), 401))).ShouldBe("passkey-invalida");

        // Un origen distinto (otra página) tampoco.
        var c = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        var otroSitio = new AutenticadorVirtual("https://evil.example") { Contador = autenticador.Contador };
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(c.Desafio, otroSitio.Firmar(c.Opciones)), 401))).ShouldBe("passkey-invalida");
    }

    [Fact]
    public async Task Revocar_la_passkey_la_deja_sin_efecto_cierra_las_sesiones_y_el_usuario_se_recupera_con_el_codigo_y_otra_passkey()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var perdida = await RegistrarPasskeyAsync(admin.Correo);
        var sesion = await EntrarConPasskeyAsync(perdida);
        var listadas = await ApiAsync<List<PasskeyDto>>("GET", $"/api/v1/usuarios/{admin.Id}/passkeys", g.Admin, null, null, 200);
        var passkey = listadas.ShouldHaveSingleItem();

        var revocada = await ApiAsync<PasskeyDto>("POST", $"/api/v1/usuarios/{admin.Id}/passkeys/{passkey.Id}/revocar", g.Admin, null,
            new RevocarPasskeyDto("Perdió el teléfono"), 200);
        revocada.Activa.ShouldBeFalse();
        revocada.MotivoRevocacion.ShouldBe("Perdió el teléfono");
        revocada.RevocadaPor.ShouldNotBeNullOrWhiteSpace();

        // La sesión que ya tenía deja de valer y la passkey no vuelve a entrar.
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesion.Token);
        var o = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o.Desafio, perdida.Firmar(o.Opciones)), 401))).ShouldBe("passkey-revocada");
        (await ApiAsync<List<PasskeyDto>>("GET", $"/api/v1/usuarios/{admin.Id}/passkeys", g.Admin, null, null, 200)).ShouldHaveSingleItem().Activa.ShouldBeFalse();

        // Recuperación: el código del correo solo sirve para registrar otra (el rol la exige).
        var nueva = await RegistrarPasskeyAsync(admin.Correo);
        (await EntrarConPasskeyAsync(nueva)).Metodo.ShouldBe("Passkey");
        (await ApiAsync<List<PasskeyDto>>("GET", $"/api/v1/usuarios/{admin.Id}/passkeys", g.Admin, null, null, 200)).Count(p => p.Activa).ShouldBe(1);
    }

    // ---------- modos por grupo ----------

    [Fact]
    public async Task Cada_grupo_elige_sus_modos_de_ingreso_y_el_delegado_se_apaga_con_el_modo_solo_directo()
    {
        var delegado = await GrupoConSistemaAsync(["Delegado"]);
        var directo = await GrupoConSistemaAsync(["Directo"]);
        var ambos = await GrupoConSistemaAsync(["Delegado", "Directo"]);
        foreach (var (g, _) in new[] { delegado, directo, ambos })
            await UsuarioAsync(g, "Revisor", "laura@constructora.co");

        // Identidad delegada: solo con el modo Delegado.
        (await SesionDelegadaAsync(delegado.Sistema, 200)).Token.ShouldNotBeNullOrWhiteSpace();
        (await SesionDelegadaAsync(ambos.Sistema, 200)).Token.ShouldNotBeNullOrWhiteSpace();
        var rechazada = await Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl("/api/v1/sesiones-usuario");
            s.WithRequestHeader("X-Api-Key", directo.Sistema.ApiKey);
            s.WithRequestHeader("X-Usuario-Delegado", directo.Sistema.Asercion("laura-1", "laura@constructora.co"));
            s.StatusCodeShouldBe(403);
        });
        (await CodigoDeErrorAsync(rechazada)).ShouldBe("modo-de-ingreso-no-admitido");

        // Ingreso directo: solo con el modo Directo (el grupo delegado no recibe códigos).
        var correoDirecto = $"d{Guid.NewGuid():N}@prueba.co";
        await UsuarioAsync(directo.Grupo, "Revisor", correoDirecto);
        await UsuarioAsync(ambos.Grupo, "Revisor", correoDirecto + ".ambos");
        (await IngresarConCodigoAsync(correoDirecto)).Paso.Estado.ShouldBe("Sesion");
        (await IngresarConCodigoAsync(correoDirecto + ".ambos")).Paso.Estado.ShouldBe("Sesion");

        // El administrador del grupo cambia la política: nunca queda sin modo, y apagar el directo cierra las sesiones directas abiertas.
        var abierta = (await IngresarConCodigoAsync(correoDirecto, numero: 2)).Paso.Sesion!;
        await ApiAsync<ConfiguracionDeIngresoDto>("PUT", "/api/v1/configuracion/ingreso", directo.Grupo.Admin, null, new ConfiguracionDeIngresoDto([], 8, 30, false), 422);
        var nueva = await ApiAsync<ConfiguracionDeIngresoDto>("PUT", "/api/v1/configuracion/ingreso", directo.Grupo.Admin, null,
            new ConfiguracionDeIngresoDto(["Delegado"], 8, 30, false), 200);
        nueva.Modos.ShouldBe(["Delegado"]);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: abierta.Token);
        (await SesionDelegadaAsync(directo.Sistema, 200)).Token.ShouldNotBeNullOrWhiteSpace();
        await PedirCodigoAsync(correoDirecto);
        await Task.Delay(800);
        Correos.Para(correoDirecto).Count(c => c.Asunto == AsuntoCodigo).ShouldBe(2); // el tercero no llegó
    }

    [Fact]
    public async Task La_politica_del_grupo_se_valida_y_se_audita_y_solo_la_ve_el_administrador_de_todo_el_grupo()
    {
        var g = await GrupoAsync();
        var revisor = await UsuarioAsync(g, "Revisor");
        var sesionDelRevisor = (await IngresarConCodigoAsync(revisor.Correo)).Paso.Sesion!;
        var obtenida = await ApiAsync<ConfiguracionDeIngresoDto>("GET", "/api/v1/configuracion/ingreso", g.Admin, null, null, 200);
        obtenida.Modos.ShouldBe(["Directo"]);
        obtenida.SesionHoras.ShouldBe(8);

        foreach (var (sesionHoras, inactividad) in new[] { (0, 30), (25, 30), (8, 4), (1, 90) })
            await ApiAsync<ConfiguracionDeIngresoDto>("PUT", "/api/v1/configuracion/ingreso", g.Admin, null,
                new ConfiguracionDeIngresoDto(["Directo"], sesionHoras, inactividad, false), 422);
        await ApiAsync<ConfiguracionDeIngresoDto>("PUT", "/api/v1/configuracion/ingreso", g.Admin, null,
            new ConfiguracionDeIngresoDto(["Directo", "Delegado"], 12, 45, true), 200);

        var cambios = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=IngresoDelGrupo", g.Admin, g.Empresa.Id, null, 200);
        var cambio = cambios.Datos.ShouldHaveSingleItem();
        cambio.Diff.Select(d => d.Campo).ShouldContain("sesionHoras");
        cambio.Diff.Select(d => d.Campo).ShouldContain("passkeyParaTodos");

        // Un revisor (aun con sesión directa) no administra el ingreso del grupo.
        await Anonimo("/api/v1/configuracion/ingreso", null, 403, metodo: "GET", bearer: sesionDelRevisor.Token);
        await Anonimo("/api/v1/configuracion/ingreso", new ConfiguracionDeIngresoDto(["Directo"], 8, 30, false), 403, metodo: "PUT", bearer: sesionDelRevisor.Token);
    }

    // ---------- sesión: renovar, rotar, reuso, inactividad, cierre ----------

    [Fact]
    public async Task Renovar_rota_la_cookie_conserva_la_persona_y_reusar_la_anterior_revoca_la_familia()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");
        var (paso, resultado) = await IngresarConCodigoAsync(usuario.Correo);
        var primera = ValorCookie(resultado);

        var renovada = await RenovarAsync(primera, 200);
        var nueva = (await renovada.ReadAsJsonAsync<SesionDirectaDto>())!;
        nueva.SesionId.ShouldBe(paso.Sesion!.SesionId);
        nueva.Metodo.ShouldBe("Codigo");
        (await ObtenerAsync<YoDto>("/api/v1/yo", nueva.Token)).ActorId.ShouldBe(usuario.Id.ToString());
        var segunda = ValorCookie(renovada);
        segunda.ShouldNotBe(primera);
        SetCookie(renovada).ShouldContain("httponly", Case.Insensitive);

        // Una petición duplicada con la cookie recién rotada no revoca nada (dos pestañas): 409 y la cookie nueva sigue sirviendo.
        (await CodigoDeErrorAsync(await RenovarAsync(primera, 409))).ShouldBe("renovacion-concurrente");
        await Anonimo("/api/v1/yo", null, 200, metodo: "GET", bearer: nueva.Token);

        // Pasada la gracia, reusar la cookie anterior es un robo probable: la familia entera muere (también la vigente y su token).
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromSeconds(11);
            (await CodigoDeErrorAsync(await RenovarAsync(primera, 401))).ShouldBe("sesion-revocada");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        (await CodigoDeErrorAsync(await RenovarAsync(segunda, 401))).ShouldBe("sesion-revocada");
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: nueva.Token);
    }

    [Fact]
    public async Task Renovar_exige_la_cabecera_del_cliente_y_una_cookie_valida_y_cerrar_es_idempotente()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");
        var (paso, resultado) = await IngresarConCodigoAsync(usuario.Correo);
        var cookie = ValorCookie(resultado);

        foreach (var ruta in new[] { "/api/v1/ingreso/sesion/renovar", "/api/v1/ingreso/sesion/cerrar" })
            (await CodigoDeErrorAsync(await Anonimo(ruta, null, 403, cookie: cookie))).ShouldBe("cliente-invalido");
        (await CodigoDeErrorAsync(await RenovarAsync(null, 401))).ShouldBe("sin-sesion");
        (await CodigoDeErrorAsync(await RenovarAsync("basura", 401))).ShouldBe("sin-sesion");
        (await CodigoDeErrorAsync(await RenovarAsync($"{Guid.NewGuid():N}.{new string('a', 43)}", 401))).ShouldBe("sin-sesion");

        var cierre = await Anonimo("/api/v1/ingreso/sesion/cerrar", null, 204, cookie: cookie, web: true);
        SetCookie(cierre).ShouldStartWith(Cookie + "=;");
        (await CodigoDeErrorAsync(await RenovarAsync(cookie, 401))).ShouldBe("sesion-revocada");
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: paso.Sesion!.Token);
        await Anonimo("/api/v1/ingreso/sesion/cerrar", null, 204, web: true); // sin cookie también
    }

    [Fact]
    public async Task Sin_renovar_durante_la_inactividad_la_sesion_se_cierra_y_renovando_dura_hasta_el_maximo_del_grupo()
    {
        var g = await GrupoAsync(sesionHoras: 1, inactividad: 20);
        var usuario = await UsuarioAsync(g, "Revisor");
        var (paso, resultado) = await IngresarConCodigoAsync(usuario.Correo);
        var cookie = ValorCookie(resultado);

        // El token de acceso nunca dura más que la inactividad: renovar a tiempo mantiene la sesión.
        (paso.Sesion!.Expira - DateTimeOffset.UtcNow).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(20.5));
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(15);
            cookie = ValorCookie(await RenovarAsync(cookie, 200));
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(30);
            cookie = ValorCookie(await RenovarAsync(cookie, 200));
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(45);
            cookie = ValorCookie(await RenovarAsync(cookie, 200));

            // Pasado el máximo absoluto (1 h desde el ingreso), ya no se renueva aunque haya actividad.
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(61);
            (await CodigoDeErrorAsync(await RenovarAsync(cookie, 401))).ShouldBe("sesion-expirada");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        var (_, otro) = await IngresarConCodigoAsync((await UsuarioAsync(g, "Revisor")).Correo);
        var cookieInactiva = ValorCookie(otro);
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(21);
            (await CodigoDeErrorAsync(await RenovarAsync(cookieInactiva, 401))).ShouldBe("sesion-inactiva");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Desactivar_al_usuario_o_cambiar_sus_roles_se_aplica_a_su_sesion_en_la_siguiente_renovacion()
    {
        var g = await GrupoAsync();
        var revisor = await UsuarioAsync(g, "Revisor");
        var (paso, resultado) = await IngresarConCodigoAsync(revisor.Correo);

        // Un rol que exige passkey cierra la sesión que solo tenía el código.
        await ApiAsync<UsuarioDto>("PUT", $"/api/v1/usuarios/{revisor.Id}/roles", g.Admin, null,
            new AsignarRolesDto([new RolAsignadoDto("Auditor", g.Empresa.Id)]), 200);
        (await CodigoDeErrorAsync(await RenovarAsync(ValorCookie(resultado), 401))).ShouldBe("passkey-requerida");

        var otro = await UsuarioAsync(g, "Revisor");
        var (sesion, cookie) = await IngresarConCodigoAsync(otro.Correo);
        await ApiAsync<UsuarioDto>("POST", $"/api/v1/usuarios/{otro.Id}/desactivar", g.Admin, null, null, 200);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesion.Sesion!.Token);
        (await CodigoDeErrorAsync(await RenovarAsync(ValorCookie(cookie), 401))).ShouldBe("sesion-revocada");
        await PedirCodigoAsync(otro.Correo);
        await Task.Delay(800);
        Correos.Para(otro.Correo).Count(c => c.Asunto == AsuntoCodigo).ShouldBe(1); // desactivado: ya no recibe códigos
        _ = paso;
    }

    // ---------- Mi cuenta ----------

    [Fact]
    public async Task Mi_cuenta_lista_registra_renombra_y_elimina_passkeys_y_no_deja_a_un_rol_obligado_sin_ninguna()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador");
        var primero = await RegistrarPasskeyAsync(admin.Correo);
        var sesion = await EntrarConPasskeyAsync(primero);
        var token = sesion.Token;

        var yo = await ObtenerAsync<MiCuentaDto>("/api/v1/mi-cuenta", token);
        yo.Tipo.ShouldBe("Usuario");
        yo.Correo.ShouldBe(admin.Correo);
        yo.ExigePasskey.ShouldBeTrue();
        yo.Metodo.ShouldBe("Passkey");
        yo.SesionId.ShouldBe(sesion.SesionId);
        yo.Grupo.ShouldNotBeNullOrWhiteSpace();

        var inicial = await ObtenerAsync<List<PasskeyDto>>("/api/v1/mi-cuenta/passkeys", token);
        var unica = inicial.ShouldHaveSingleItem();
        unica.Nombre.ShouldBe("Mi teléfono");

        // Otra passkey (el navegador excluye la que ya tiene en ese dispositivo).
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/passkeys/opciones", null, token);
        opciones.Opciones.GetProperty("excludeCredentials").EnumerateArray().Single().GetProperty("id").GetString().ShouldBe(primero.CredencialIdBase64Url);
        var segundo = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var creada = await PostJsonAsync<PasskeyDto>("/api/v1/mi-cuenta/passkeys", new RegistrarPasskeyDto(opciones.Desafio, segundo.Registrar(opciones.Opciones), "Llave de la oficina"), token);
        creada.Nombre.ShouldBe("Llave de la oficina");
        creada.Activa.ShouldBeTrue();

        // Registrar con un desafío ajeno o repetido falla.
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/mi-cuenta/passkeys", new RegistrarPasskeyDto(opciones.Desafio, segundo.Registrar(opciones.Opciones), "x"), 410, bearer: token)))
            .ShouldBe("desafio-usado");

        var renombrada = await PutJsonAsync<PasskeyDto>($"/api/v1/mi-cuenta/passkeys/{creada.Id}", new RenombrarPasskeyDto("Llave USB"), token);
        renombrada.Nombre.ShouldBe("Llave USB");

        await Anonimo($"/api/v1/mi-cuenta/passkeys/{unica.Id}", null, 204, bearer: token, metodo: "DELETE");
        var ultima = await Anonimo($"/api/v1/mi-cuenta/passkeys/{creada.Id}", null, 409, bearer: token, metodo: "DELETE");
        (await CodigoDeErrorAsync(ultima)).ShouldBe("passkey-obligatoria");
        (await ObtenerAsync<List<PasskeyDto>>("/api/v1/mi-cuenta/passkeys", token)).ShouldHaveSingleItem().Id.ShouldBe(creada.Id);

        // Lo eliminado ya no entra.
        var o = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(o.Desafio, primero.Firmar(o.Opciones)), 401))).ShouldBe("passkey-revocada");
    }

    [Fact]
    public async Task Mi_cuenta_lista_y_cierra_mis_sesiones_y_no_toca_las_de_otras_personas()
    {
        var g = await GrupoAsync();
        var ana = await UsuarioAsync(g, "Revisor");
        var beto = await UsuarioAsync(g, "Revisor");
        var (primera, _) = await IngresarConCodigoAsync(ana.Correo, ip: "10.5.5.5");
        var (segunda, _) = await IngresarConCodigoAsync(ana.Correo, numero: 2);
        var (deBeto, _) = await IngresarConCodigoAsync(beto.Correo);

        var mias = await ObtenerAsync<List<SesionDeUsuarioDto>>("/api/v1/mi-cuenta/sesiones", primera.Sesion!.Token);
        mias.Select(s => s.Id).ShouldBe([primera.Sesion.SesionId, segunda.Sesion!.SesionId], ignoreOrder: true);
        mias.Single(s => s.Actual).Id.ShouldBe(primera.Sesion.SesionId);
        mias.Single(s => s.Actual).Ip.ShouldBe("10.5.5.5");
        mias.ShouldAllBe(s => s.Dispositivo == "Pruebas/1.0 (Windows)" && s.Metodo == "Codigo");

        await Anonimo($"/api/v1/mi-cuenta/sesiones/{deBeto.Sesion!.SesionId}/cerrar", null, 404, bearer: primera.Sesion.Token);
        await Anonimo($"/api/v1/mi-cuenta/sesiones/{segunda.Sesion.SesionId}/cerrar", null, 204, bearer: primera.Sesion.Token);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: segunda.Sesion.Token);
        (await ObtenerAsync<List<SesionDeUsuarioDto>>("/api/v1/mi-cuenta/sesiones", primera.Sesion.Token)).ShouldHaveSingleItem();
        await Anonimo("/api/v1/yo", null, 200, metodo: "GET", bearer: deBeto.Sesion.Token);

        // Con otra credencial (identidad delegada, API key) no hay Mi cuenta.
        await Anonimo("/api/v1/mi-cuenta", null, 403, metodo: "GET", bearer: null, apiKey: g.Admin);
    }

    // ---------- gestión por el administrador del grupo ----------

    [Fact]
    public async Task El_administrador_invita_ve_y_cierra_las_sesiones_y_revoca_passkeys_de_los_usuarios_de_su_grupo_y_de_nadie_mas()
    {
        var g = await GrupoAsync();
        var otroGrupo = await GrupoAsync();
        var correo = CorreoNuevo();
        var persona = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", g.Admin, null,
            new CrearUsuarioDto("Carla Auditora", correo, null, [new RolAsignadoDto("Auditor", g.Empresa.Id)], null, Invitar: true), 201);
        var invitacion = await Correos.EsperarAsync(correo, c => c.Asunto.Contains("te invitó a Validasoft"));
        invitacion.Texto.ShouldContain("Auditor");
        invitacion.Texto.ShouldContain($"{ApiFixture.UrlPublica}/app/");
        persona.ExigePasskey.ShouldBeTrue();

        var autenticador = await RegistrarPasskeyAsync(correo, numero: 1);
        var sesion = await EntrarConPasskeyAsync(autenticador);
        var passkeys = await ApiAsync<List<PasskeyDto>>("GET", $"/api/v1/usuarios/{persona.Id}/passkeys", g.Admin, null, null, 200);
        var sesiones = await ApiAsync<List<SesionDeUsuarioDto>>("GET", $"/api/v1/usuarios/{persona.Id}/sesiones", g.Admin, null, null, 200);
        sesiones.Select(s => s.Id).ShouldContain(sesion.SesionId);

        await ApiAsync<UsuarioDto>("POST", $"/api/v1/usuarios/{persona.Id}/invitar", g.Admin, null, null, 200);
        await Correos.EsperarAsync(correo, c => c.Asunto.Contains("te invitó a Validasoft"), cuantos: 2);

        // Cerrar la sesión de un dispositivo.
        await Anonimo($"/api/v1/usuarios/{persona.Id}/sesiones/{sesion.SesionId}/cerrar", null, 204, apiKey: g.Admin);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesion.Token);

        // El otro grupo no ve ni toca a esa persona.
        await ApiAsync<List<PasskeyDto>>("GET", $"/api/v1/usuarios/{persona.Id}/passkeys", otroGrupo.Admin, null, null, 404);
        await ApiAsync<List<SesionDeUsuarioDto>>("GET", $"/api/v1/usuarios/{persona.Id}/sesiones", otroGrupo.Admin, null, null, 404);
        await ApiAsync<PasskeyDto>("POST", $"/api/v1/usuarios/{persona.Id}/passkeys/{passkeys[0].Id}/revocar", otroGrupo.Admin, null, new RevocarPasskeyDto(null), 404);
        await ApiAsync<UsuarioDto>("POST", $"/api/v1/usuarios/{persona.Id}/invitar", otroGrupo.Admin, null, null, 404);

        // Y la ceremonia de ingreso de un grupo nunca entra a una cuenta de otro.
        passkeys.ShouldHaveSingleItem().Activa.ShouldBeTrue();
    }

    [Fact]
    public async Task Un_administrador_que_entro_directo_revoca_la_passkey_de_otro_usuario_y_queda_su_nombre_en_la_auditoria()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var auditor = await UsuarioAsync(g, "Auditor");
        var autenticadorAdmin = await RegistrarPasskeyAsync(admin.Correo);
        var deAdmin = await EntrarConPasskeyAsync(autenticadorAdmin);
        await RegistrarPasskeyAsync(auditor.Correo);

        var passkey = (await GetAsync<List<PasskeyDto>>($"/api/v1/usuarios/{auditor.Id}/passkeys", deAdmin.Token)).ShouldHaveSingleItem();
        // Revocar la passkey de otra persona es una acción sensible: se confirma con la propia passkey (PI-13).
        await ReautenticarAsync(autenticadorAdmin, deAdmin.Token);
        await PostJsonAsync<PasskeyDto>($"/api/v1/usuarios/{auditor.Id}/passkeys/{passkey.Id}/revocar", new RevocarPasskeyDto("Perdió la llave"), deAdmin.Token);

        var auditoria = await GetAsync<PaginaDto<RegistroAuditoriaDto>>($"/api/v1/auditoria?tipo=Identidad&entidadId={auditor.Id}&limite=100", deAdmin.Token, g.Empresa.Id);
        var revocacion = auditoria.Datos.Single(r => r.Accion == "PasskeyRevocada");
        revocacion.Actor.Id.ShouldBe(admin.Id.ToString());
        revocacion.Actor.Nombre.ShouldBe(admin.Nombre);
        revocacion.Detalle!.ShouldContain("Perdió la llave");
        auditoria.Datos.Select(r => r.Accion).ShouldContain("PasskeyRegistrada");
    }

    // ---------- auditoría de identidad ----------

    [Fact]
    public async Task La_auditoria_de_identidad_registra_ingresos_exitosos_y_fallidos_con_la_ip_y_el_navegador_y_nunca_el_codigo()
    {
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");
        var desafio = await PedirCodigoAsync(usuario.Correo, ip: "10.9.8.7");
        var codigo = await CodigoDelCorreoAsync(usuario.Correo);
        await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, codigo == "000000" ? "111111" : "000000"), 422, ip: "10.9.8.7");
        var (paso, _) = await IngresarConCodigoAsync(usuario.Correo, numero: 2, ip: "10.9.8.8");
        await Anonimo("/api/v1/ingreso/sesion/cerrar", null, 204, cookie: ValorCookie((await IngresarConCodigoAsync(usuario.Correo, numero: 3)).Resultado), web: true);
        _ = paso;

        var pagina = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?tipo=Identidad&entidadId={usuario.Id}&limite=100", g.Admin, g.Empresa.Id, null, 200);
        var acciones = pagina.Datos.Select(r => r.Accion).ToList();
        acciones.ShouldContain("CodigoEnviado");
        acciones.ShouldContain("CodigoIncorrecto");
        acciones.Count(a => a == "IngresoExitoso").ShouldBe(2);
        acciones.ShouldContain("SesionCerrada");

        var fallido = pagina.Datos.Single(r => r.Accion == "CodigoIncorrecto");
        fallido.Ip.ShouldBe("10.9.8.7");
        fallido.UserAgent.ShouldBe("Pruebas/1.0 (Windows)");
        fallido.Detalle!.ShouldContain("Fallido");
        var exitoso = pagina.Datos.Single(r => r.Accion == "IngresoExitoso" && r.Ip == "10.9.8.8");
        exitoso.Detalle!.ShouldContain("Codigo");
        pagina.Datos.ShouldAllBe(r => r.Detalle == null || !r.Detalle.Contains(codigo)); // jamás el código

        // Solo la ven quienes tienen auditoria:leer y el tipo no se mezcla con el de configuración.
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Identidad", g.Admin, g.Empresa.Id, null, 200)).Datos
            .ShouldAllBe(r => r.Tipo == "Identidad");
        var correoOtro = CorreoNuevo();
        await PedirCodigoAsync(correoOtro);
        var otraVista = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Identidad&limite=200", g.Admin, g.Empresa.Id, null, 200);
        otraVista.Datos.ShouldNotContain(r => r.EntidadCorreo() == correoOtro); // un correo sin cuenta no se atribuye a ningún grupo
    }

    // ---------- operadores de Plataforma ----------

    [Fact]
    public async Task Un_operador_de_plataforma_entra_con_correo_codigo_y_passkey_obligatoria_y_gestiona_a_otros_operadores()
    {
        var correo = $"op{Guid.NewGuid():N}@validasoft.co";
        var creado = await ApiAsync<OperadorDePlataformaDto>("POST", "/api/v1/plataforma/operadores", ApiFixture.ClavePlataforma, null,
            new CrearOperadorDto("Paula Plataforma", correo), 201);
        creado.Directo.ShouldBeTrue();
        creado.Id.ShouldBe("DIRECTO:" + correo);
        await Correos.EsperarAsync(correo, c => c.Asunto.Contains("te invitó a Validasoft"));
        await ApiAsync<OperadorDePlataformaDto>("POST", "/api/v1/plataforma/operadores", ApiFixture.ClavePlataforma, null, new CrearOperadorDto("Otra", correo.ToUpperInvariant()), 409);

        // Siempre exige passkey: el código solo sirve para registrar la primera.
        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(correo), await CodigoDelCorreoAsync(correo))).Paso;
        paso.Estado.ShouldBe("RegistrarPasskey");
        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var sesion = (await RegistrarEnIngresoAsync(paso, autenticador, "Llave de Paula")).Sesion!;
        var token = sesion.Token;

        // Sesión de Plataforma: sin tenant, solo plataforma:tenants.
        var jwt = new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
        jwt.Claims.Single(c => c.Type == "validasoft:actor-tipo").Value.ShouldBe("Plataforma");
        jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ShouldBe(["plataforma:tenants"]);
        jwt.Claims.ShouldNotContain(c => c.Type == "validasoft:tenant");
        jwt.Claims.Single(c => c.Type == "validasoft:metodo").Value.ShouldBe("CodigoYPasskey");
        (await GetAsync<List<TenantDto>>("/api/v1/plataforma/tenants", token)).ShouldNotBeNull();
        await Anonimo("/api/v1/usuarios", null, 403, metodo: "GET", bearer: token);

        // Entra después solo con la passkey y desde su sesión crea, ve y revoca a otro operador (confirmando antes con la passkey: acciones sensibles, PI-13).
        (await EntrarConPasskeyAsync(autenticador)).Metodo.ShouldBe("Passkey");
        await ReautenticarAsync(autenticador, token);
        var correoOtro = $"op{Guid.NewGuid():N}@validasoft.co";
        var otro = await PostJsonAsync<OperadorDePlataformaDto>("/api/v1/plataforma/operadores", new CrearOperadorDto("Oscar Operador", correoOtro), token, 201);
        var operadores = await GetAsync<List<OperadorDePlataformaDto>>("/api/v1/plataforma/operadores", token);
        operadores.Single(o => o.Id == creado.Id).Passkeys.ShouldBe(1);
        operadores.Single(o => o.Id == otro.Id).Passkeys.ShouldBe(0);

        var autenticadorOtro = await RegistrarPasskeyAsync(correoOtro);
        var ruta = Uri.EscapeDataString(otro.Id);
        var passkeys = await GetAsync<List<PasskeyDto>>($"/api/v1/plataforma/operadores/{ruta}/passkeys", token);
        var sesionOtro = await EntrarConPasskeyAsync(autenticadorOtro);
        (await GetAsync<List<SesionDeUsuarioDto>>($"/api/v1/plataforma/operadores/{ruta}/sesiones", token)).Select(s => s.Id).ShouldContain(sesionOtro.SesionId);
        await PostJsonAsync<PasskeyDto>($"/api/v1/plataforma/operadores/{ruta}/passkeys/{passkeys.Single().Id}/revocar", new RevocarPasskeyDto("Se perdió"), token);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesionOtro.Token);

        // Desactivado, no vuelve a recibir códigos ni a entrar.
        await PostJsonAsync<OperadorDePlataformaDto>($"/api/v1/plataforma/operadores/{ruta}/desactivar", null, token);
        var antes = Correos.Para(correoOtro).Count(c => c.Asunto == AsuntoCodigo);
        await PedirCodigoAsync(correoOtro);
        await Task.Delay(800);
        Correos.Para(correoOtro).Count(c => c.Asunto == AsuntoCodigo).ShouldBe(antes);
        await Anonimo("/api/v1/plataforma/operadores/" + Uri.EscapeDataString("DIRECTO:no-existe@x.co") + "/passkeys", null, 404, metodo: "GET", bearer: token);
    }

    [Fact]
    public async Task Un_correo_con_cuentas_en_dos_grupos_elige_con_cual_entra()
    {
        var a = await GrupoAsync();
        var b = await GrupoAsync();
        var correo = CorreoNuevo();
        var enA = await UsuarioAsync(a, "Revisor", correo);
        var enB = await UsuarioAsync(b, "Revisor", correo);

        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(correo), await CodigoDelCorreoAsync(correo))).Paso;

        paso.Estado.ShouldBe("ElegirCuenta");
        paso.Sesion.ShouldBeNull();
        paso.Cuentas!.Count.ShouldBe(2);
        paso.Cuentas.ShouldAllBe(c => c.Tipo == "Usuario" && !c.ExigePasskey && !c.TienePasskey);
        var cuentaB = paso.Cuentas.Single(c => c.Id.Contains(b.TenantId.ToString("N")));
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/cuenta", new ElegirCuentaDto(paso.Desafio, "u:inventada"), 422))).ShouldBe("cuenta-invalida");

        var elegida = await Anonimo("/api/v1/ingreso/cuenta", new ElegirCuentaDto(paso.Desafio, cuentaB.Id), 200);
        var sesion = (await elegida.ReadAsJsonAsync<PasoDeIngresoDto>())!.Sesion!;
        (await ObtenerAsync<YoDto>("/api/v1/yo", sesion.Token)).ActorId.ShouldBe(enB.Id.ToString());
        (await ObtenerAsync<YoDto>("/api/v1/yo", sesion.Token)).TenantId.ShouldBe(b.TenantId);
        _ = enA;
    }

    // ---------- utilidades ----------

    private static string CorreoNuevo() => $"p{Guid.NewGuid():N}@prueba.co";

    private async Task<Grupo> GrupoAsync(string[]? modos = null, bool passkeyParaTodos = false, int sesionHoras = 8, int inactividad = 30)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Grupo " + slug, "co", "America/Bogota", Ingreso: new ConfiguracionDeIngresoDto(modos ?? ["Directo"], sesionHoras, inactividad, passkeyParaTodos)), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var empresa = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", admin, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null), 201);
        return new Grupo(tenant.Tenant.Id, admin, empresa);
    }

    private async Task<UsuarioDto> UsuarioAsync(Grupo g, string rol, string? correo = null, bool deTodoElGrupo = false) =>
        await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", g.Admin, null,
            new CrearUsuarioDto("Persona " + rol, correo ?? CorreoNuevo(), null, [new RolAsignadoDto(rol, deTodoElGrupo ? null : g.Empresa.Id)], null), 201);

    private async Task<Guid> PedirCodigoAsync(string correo, string? ip = null) =>
        (await (await Anonimo("/api/v1/ingreso/codigo", new SolicitarCodigoDto(correo), 200, ip: ip)).ReadAsJsonAsync<CodigoSolicitadoDto>())!.Desafio;

    /// <summary>El código del <paramref name="numero"/>-ésimo correo de código que recibió <paramref name="correo"/>.</summary>
    private async Task<string> CodigoDelCorreoAsync(string correo, int numero = 1)
    {
        var mensaje = await Correos.EsperarAsync(correo, c => c.Asunto == AsuntoCodigo, numero);
        return RegexCodigo().Match(mensaje.Texto).Groups[1].Value;
    }

    private async Task<(PasoDeIngresoDto Paso, IScenarioResult Resultado)> VerificarCodigoAsync(Guid desafio, string codigo)
    {
        var resultado = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, codigo), 200);
        return ((await resultado.ReadAsJsonAsync<PasoDeIngresoDto>())!, resultado);
    }

    /// <summary>Pide y verifica el <paramref name="numero"/>-ésimo código del correo.</summary>
    private async Task<(PasoDeIngresoDto Paso, IScenarioResult Resultado)> IngresarConCodigoAsync(string correo, int numero = 1, string? ip = null)
    {
        var desafio = await PedirCodigoAsync(correo, ip);
        var codigo = await CodigoDelCorreoAsync(correo, numero);
        var resultado = await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, codigo), 200, ip: ip);
        return ((await resultado.ReadAsJsonAsync<PasoDeIngresoDto>())!, resultado);
    }

    private async Task<PasoDeIngresoDto> RegistrarEnIngresoAsync(PasoDeIngresoDto paso, AutenticadorVirtual autenticador, string nombre)
    {
        paso.Estado.ShouldBe("RegistrarPasskey");
        var resultado = await Anonimo("/api/v1/ingreso/passkey/registro", new RegistrarPasskeyDto(paso.Desafio, autenticador.Registrar(paso.Opciones!.Value), nombre), 200);
        return (await resultado.ReadAsJsonAsync<PasoDeIngresoDto>())!;
    }

    /// <summary>
    /// Registra una passkey con el código del correo (alta o recuperación de un rol que la exige). <paramref name="numero"/> es el orden del código
    /// que se espera: cada llamada pide uno nuevo.
    /// </summary>
    private async Task<AutenticadorVirtual> RegistrarPasskeyAsync(string correo, int? numero = null)
    {
        var siguiente = numero ?? Correos.Para(correo).Count(c => c.Asunto == AsuntoCodigo) + 1;
        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(correo), await CodigoDelCorreoAsync(correo, siguiente))).Paso;
        var autenticador = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        (await RegistrarEnIngresoAsync(paso, autenticador, "Mi teléfono")).Sesion.ShouldNotBeNull();
        return autenticador;
    }

    private async Task<SesionDirectaDto> EntrarConPasskeyAsync(AutenticadorVirtual autenticador)
    {
        var opciones = (await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>())!;
        // Credencial detectable: las opciones no listan credenciales, así no revelan quién tiene passkey.
        (opciones.Opciones.TryGetProperty("allowCredentials", out var permitidas) ? permitidas.GetArrayLength() : 0).ShouldBe(0);
        var resultado = await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(opciones.Desafio, autenticador.Firmar(opciones.Opciones)), 200);
        ValorCookie(resultado).ShouldNotBeNullOrWhiteSpace();
        return (await resultado.ReadAsJsonAsync<PasoDeIngresoDto>())!.Sesion!;
    }

    private Task<IScenarioResult> RenovarAsync(string? cookie, int esperado) => Anonimo("/api/v1/ingreso/sesion/renovar", null, esperado, cookie: cookie, web: true);

    private static string SetCookie(IScenarioResult resultado) =>
        resultado.Context.Response.Headers.SetCookie.Select(v => v ?? string.Empty).SingleOrDefault(v => v.StartsWith(Cookie + "=", StringComparison.Ordinal))
        ?? throw new InvalidOperationException("La respuesta no trae Set-Cookie vs_usuario.");

    private static string ValorCookie(IScenarioResult resultado)
    {
        var valor = SetCookie(resultado)[(Cookie.Length + 1)..].Split(';')[0];
        valor.ShouldNotBeNullOrWhiteSpace();
        return valor;
    }

    private static async Task<string?> CodigoDeErrorAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private static void FijarIp(Scenario s, string? ip) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress = ip is not null
            ? IPAddress.Parse(ip)
            : new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    /// <summary>Petición sin credencial de API (o con la que se indique): el cuerpo, la cookie, la IP y el cliente web son opcionales.</summary>
    private Task<IScenarioResult> Anonimo(string url, object? cuerpo, int esperado, string? cookie = null, bool web = false, string? ip = null, string? bearer = null,
        string metodo = "POST", string? apiKey = null, Guid? empresa = null) =>
        Host.Scenario(s =>
        {
            switch (metodo)
            {
                case "GET":
                    s.Get.Url(url);
                    break;
                case "DELETE":
                    s.Delete.Url(url);
                    break;
                case "PUT":
                    s.Put.Json(cuerpo!).ToUrl(url);
                    break;
                default:
                    if (cuerpo is null)
                        s.Post.Url(url);
                    else
                        s.Post.Json(cuerpo).ToUrl(url);
                    break;
            }

            s.WithRequestHeader("User-Agent", "Pruebas/1.0 (Windows)");
            if (web)
                s.WithRequestHeader("X-Validasoft-Cliente", "web");
            if (cookie is not null)
                s.WithRequestHeader("Cookie", $"{Cookie}={cookie}");
            if (bearer is not null)
                s.WithRequestHeader("Authorization", "Bearer " + bearer);
            if (apiKey is not null)
                s.WithRequestHeader("X-Api-Key", apiKey);
            if (empresa is not null)
                s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
            FijarIp(s, ip);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> ObtenerAsync<T>(string url, string token, Guid? empresa = null) =>
        (await (await Anonimo(url, null, 200, metodo: "GET", bearer: token, empresa: empresa)).ReadAsJsonAsync<T>())!;

    private Task<T> GetAsync<T>(string url, string token, Guid? empresa = null) => ObtenerAsync<T>(url, token, empresa);

    private async Task<T> PostJsonAsync<T>(string url, object? cuerpo, string token, int esperado = 200) =>
        (await (await Anonimo(url, cuerpo, esperado, bearer: token)).ReadAsJsonAsync<T>())!;

    private async Task<T> PutJsonAsync<T>(string url, object cuerpo, string token) =>
        (await (await Anonimo(url, cuerpo, 200, metodo: "PUT", bearer: token)).ReadAsJsonAsync<T>())!;

    private async Task<T> ApiAsync<T>(string metodo, string url, string apiKey, Guid? empresa, object? cuerpo, int esperado)
    {
        var resultado = await Host.Scenario(s =>
        {
            switch (metodo)
            {
                case "GET":
                    s.Get.Url(url);
                    break;
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
            if (empresa is not null)
                s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
            s.StatusCodeShouldBe(esperado);
        });
        return esperado is >= 200 and < 300 ? (await resultado.ReadAsJsonAsync<T>())! : default!;
    }

    // ---------- grupos con sistema integrado (para la identidad delegada) ----------

    private sealed record SistemaDePrueba(string Codigo, RSA Clave, string ApiKey)
    {
        public string Asercion(string sub, string correo)
        {
            var ahora = DateTime.UtcNow;
            var credenciales = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.RsaSecurityKey(Clave),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256);
            return new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
            {
                Issuer = Codigo,
                Audience = "validasoft",
                Subject = new System.Security.Claims.ClaimsIdentity(
                    [new("sub", sub), new("name", "Laura Pérez"), new("email", correo), new("jti", Guid.NewGuid().ToString())]),
                IssuedAt = ahora.AddMinutes(-1),
                NotBefore = ahora.AddMinutes(-1),
                Expires = ahora.AddMinutes(2),
                SigningCredentials = credenciales
            });
        }
    }

    private async Task<(Grupo Grupo, SistemaDePrueba Sistema)> GrupoConSistemaAsync(string[] modos)
    {
        var grupo = await GrupoAsync(modos);
        var clave = RSA.Create(2048);
        var codigo = "S" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", grupo.Admin, null,
            new CrearSistemaIntegradoDto(codigo, "Sistema " + codigo, clave.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", grupo.Admin, null,
            new CrearApiKeyDto("Integración " + codigo, ["configuracion:leer", "vinculos:leer"], null, null, SistemaId: sistema.Id), 201);
        return (grupo, new SistemaDePrueba(codigo, clave, llave.Valor!));
    }

    private async Task<SesionUsuarioDto> SesionDelegadaAsync(SistemaDePrueba sistema, int esperado)
    {
        var resultado = await Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl("/api/v1/sesiones-usuario");
            s.WithRequestHeader("X-Api-Key", sistema.ApiKey);
            s.WithRequestHeader("X-Usuario-Delegado", sistema.Asercion("laura-1", "laura@constructora.co"));
            s.StatusCodeShouldBe(esperado);
        });
        return (await resultado.ReadAsJsonAsync<SesionUsuarioDto>())!;
    }

    private sealed class SecuenciaIgual : IEqualityComparer<string[]>
    {
        public bool Equals(string[]? x, string[]? y) => x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(string[] obj) => string.Join('|', obj).GetHashCode(StringComparison.Ordinal);
    }
}

internal static class RegistroAuditoriaExtensiones
{
    public static string? EntidadCorreo(this RegistroAuditoriaDto registro) => registro.Entidad.Nombre;
}
