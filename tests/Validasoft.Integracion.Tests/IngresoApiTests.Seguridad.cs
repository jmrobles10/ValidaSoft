using System.Net;
using System.Text.Json;
using Alba;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Identidad;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Plataforma e identidad, segunda tanda (PI-7…PI-18): reautenticación con la passkey para las acciones sensibles, códigos de recuperación, avisos de seguridad por
/// correo, desactivar y reactivar sistemas y operadores, la clave de arranque, la auditoría de ingresos de los operadores, agregar empresas a un grupo, la idempotencia sin
/// secretos y la invalidación de cachés entre servidores.
/// </summary>
public sealed partial class IngresoApiTests
{
    private const string AsuntoPasskeyRegistrada = "Registraste una passkey en Validasoft";
    private const string AsuntoPasskeyRevocada = "Se revocó una passkey de tu cuenta de Validasoft";
    private const string AsuntoPasskeyEliminada = "Eliminaste una passkey de tu cuenta de Validasoft";
    private const string AsuntoCodigosGenerados = "Generaste códigos de recuperación en Validasoft";
    private const string AsuntoRecuperacionUsada = "Usaste un código de recuperación en Validasoft";

    /// <summary>Confirma con la passkey la sesión del token: las acciones sensibles se aceptan unos minutos.</summary>
    private async Task ReautenticarAsync(AutenticadorVirtual autenticador, string token)
    {
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/reautenticacion/opciones", null, token);
        await PostJsonAsync<ReautenticacionDto>("/api/v1/mi-cuenta/reautenticacion", new VerificarPasskeyDto(opciones.Desafio, autenticador.Firmar(opciones.Opciones)), token);
    }

    /// <summary>Una petición hecha como la persona de una sesión directa (Bearer); los POST llevan su Idempotency-Key.</summary>
    private Task<IScenarioResult> ComoPersonaAsync(string metodo, string url, object? cuerpo, string token, int esperado, Guid? empresa = null) =>
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
                    s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
                    break;
            }

            s.WithRequestHeader("Authorization", "Bearer " + token);
            if (empresa is not null)
                s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
            FijarIp(s, null);
            s.StatusCodeShouldBe(esperado);
        });

    private sealed record AdminConPasskey(Grupo Grupo, UsuarioDto Usuario, AutenticadorVirtual Autenticador, string Token);

    /// <summary>Un grupo de ingreso directo con un administrador de todo el grupo que ya registró su passkey y tiene una sesión abierta.</summary>
    private async Task<AdminConPasskey> AdministradorConPasskeyAsync(string[]? modos = null)
    {
        var g = await GrupoAsync(modos);
        var admin = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);
        return new AdminConPasskey(g, admin, autenticador, (await EntrarConPasskeyAsync(autenticador)).Token);
    }

    private async Task<ApiKeyDto> NuevaLlaveAsync(Grupo g, string nombre = "Integración", Guid? sistemaId = null) =>
        await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", g.Admin, null, new CrearApiKeyDto(nombre, ["configuracion:leer"], null, null, SistemaId: sistemaId), 201);

    // ---------- reautenticación (step-up) con la passkey ----------

    [Fact]
    public async Task La_accion_sensible_de_una_sesion_directa_exige_confirmar_con_la_passkey_y_esa_confirmacion_vale_cinco_minutos()
    {
        var a = await AdministradorConPasskeyAsync();
        var otroAdmin = await UsuarioAsync(a.Grupo, "Revisor");
        var llave = await NuevaLlaveAsync(a.Grupo);
        (await GetAsync<EstadoDeReautenticacionDto>("/api/v1/mi-cuenta/reautenticacion", a.Token)).ShouldBe(new EstadoDeReautenticacionDto(false, null, 5));

        // Rotar, revocar o crear llaves, cambiar roles y desactivar personas: sin confirmar, 403 con la acción y la ventana.
        async Task Exigida(string metodo, string url, object? cuerpo, string accion)
        {
            var resultado = await ComoPersonaAsync(metodo, url, cuerpo, a.Token, 403);
            var problema = await resultado.ReadAsJsonAsync<JsonElement>();
            (problema.GetProperty("codigo").GetString(), problema.GetProperty("accion").GetString(), problema.GetProperty("minutos").GetInt32(), problema.GetProperty("tienePasskey").GetBoolean())
                .ShouldBe(("reautenticacion-requerida", accion, 5, true));
        }

        await Exigida("POST", $"/api/v1/api-keys/{llave.Id}/rotar", new RotarApiKeyDto(), "rotar-api-key");
        await Exigida("DELETE", $"/api/v1/api-keys/{llave.Id}", null, "revocar-api-key");
        await Exigida("POST", "/api/v1/api-keys", new CrearApiKeyDto("Otra", ["configuracion:leer"], null, null), "crear-api-key");
        await Exigida("PUT", $"/api/v1/usuarios/{otroAdmin.Id}/roles", new AsignarRolesDto([new RolAsignadoDto("Auditor", null)]), "cambiar-roles");
        await Exigida("POST", $"/api/v1/usuarios/{otroAdmin.Id}/desactivar", null, "desactivar-usuario");
        await Exigida("PUT", "/api/v1/configuracion/sso", new FijarSsoDto(false, "oidc", "Acme"), "configurar-sso");

        // La credencial de un sistema (API key) no tiene a una persona delante de un navegador: no se le pide.
        await ApiAsync<ApiKeyDto>("POST", $"/api/v1/api-keys/{llave.Id}/rotar", a.Grupo.Admin, null, new RotarApiKeyDto(), 201);

        // Confirmar con la passkey de la cuenta abre la ventana; con la de otra persona, no.
        var ajeno = await UsuarioAsync(a.Grupo, "Administrador", deTodoElGrupo: true);
        var autenticadorAjeno = await RegistrarPasskeyAsync(ajeno.Correo);
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/reautenticacion/opciones", null, a.Token);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/reautenticacion", new VerificarPasskeyDto(opciones.Desafio, autenticadorAjeno.Firmar(opciones.Opciones)), a.Token, 401)))
            .ShouldBe("passkey-invalida");
        var reautenticacion = await PostJsonAsync<ReautenticacionDto>("/api/v1/mi-cuenta/reautenticacion", new VerificarPasskeyDto(opciones.Desafio, a.Autenticador.Firmar(opciones.Opciones)), a.Token);
        reautenticacion.Minutos.ShouldBe(5);
        (await GetAsync<EstadoDeReautenticacionDto>("/api/v1/mi-cuenta/reautenticacion", a.Token)).Vigente.ShouldBeTrue();
        // La confirmación se gasta: no se repite con el mismo desafío.
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/reautenticacion", new VerificarPasskeyDto(opciones.Desafio, a.Autenticador.Firmar(opciones.Opciones)), a.Token, 410)))
            .ShouldBe("desafio-usado");

        // Ahora sí: rotar, desactivar y cambiar roles pasan.
        await ComoPersonaAsync("POST", $"/api/v1/api-keys/{llave.Id}/rotar", new RotarApiKeyDto(), a.Token, 409); // ya la rotó el sistema: la acción pasó el filtro y falló por su regla
        await ComoPersonaAsync("PUT", $"/api/v1/usuarios/{otroAdmin.Id}/roles", new AsignarRolesDto([new RolAsignadoDto("Auditor", null)]), a.Token, 200);
        await ComoPersonaAsync("POST", $"/api/v1/usuarios/{otroAdmin.Id}/desactivar", null, a.Token, 200);

        // Pasados cinco minutos vuelve a pedirla.
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(5).Add(TimeSpan.FromSeconds(30));
            (await GetAsync<EstadoDeReautenticacionDto>("/api/v1/mi-cuenta/reautenticacion", a.Token)).Vigente.ShouldBeFalse();
            await ComoPersonaAsync("POST", "/api/v1/api-keys", new CrearApiKeyDto("Otra", ["configuracion:leer"], null, null), a.Token, 403);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        // Quedó en la auditoría de identidad (la exitosa y la fallida), sin nada del desafío.
        var rastro = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?tipo=Identidad&entidadId={a.Usuario.Id}&limite=200", a.Grupo.Admin, a.Grupo.Empresa.Id, null, 200);
        rastro.Datos.Select(r => r.Accion).ShouldContain("ReautenticacionExitosa");
        rastro.Datos.Select(r => r.Accion).ShouldContain("ReautenticacionFallida");
    }

    [Fact]
    public async Task El_desafio_de_reautenticacion_nunca_abre_una_sesion_y_sin_passkey_no_hay_reautenticacion()
    {
        var a = await AdministradorConPasskeyAsync();

        // Un desafío de reautenticación enviado a la verificación de ingreso se rechaza.
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/reautenticacion/opciones", null, a.Token);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(opciones.Desafio, a.Autenticador.Firmar(opciones.Opciones)), 410))).ShouldBe("desafio-usado");

        // Quien entró solo con el código (un rol sin passkey) no tiene con qué reautenticar: pero tampoco puede hacer acciones sensibles.
        var g = await GrupoAsync();
        var revisor = await UsuarioAsync(g, "Revisor");
        var (paso, _) = await IngresarConCodigoAsync(revisor.Correo);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/reautenticacion/opciones", null, paso.Sesion!.Token, 409))).ShouldBe("sin-passkey");
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/codigos-de-recuperacion", null, paso.Sesion.Token, 403))).ShouldBe("reautenticacion-requerida");
        // Con otra credencial (API key) Mi cuenta no existe.
        await Anonimo("/api/v1/mi-cuenta/reautenticacion", null, 403, metodo: "GET", apiKey: g.Admin);
    }

    // ---------- códigos de recuperación ----------

    [Fact]
    public async Task Los_codigos_de_recuperacion_respaldan_la_passkey_se_muestran_una_vez_y_cada_uno_sirve_una_sola_vez()
    {
        var a = await AdministradorConPasskeyAsync();
        var correo = a.Usuario.Correo;
        (await GetAsync<EstadoDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", a.Token)).Disponibles.ShouldBe(0);

        // Generarlos es una acción sensible; se muestran una sola vez y se avisa por correo.
        await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/codigos-de-recuperacion", null, a.Token, 403);
        await ReautenticarAsync(a.Autenticador, a.Token);
        var generados = await PostJsonAsync<CodigosDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", null, a.Token, 200);
        generados.Codigos.Count.ShouldBe(10);
        generados.Codigos.Distinct().Count().ShouldBe(10);
        var estado = await GetAsync<EstadoDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", a.Token);
        (estado.Disponibles, estado.Total).ShouldBe((10, 10));
        (await (await ComoPersonaAsync("GET", "/api/v1/mi-cuenta/codigos-de-recuperacion", null, a.Token, 200)).ReadAsTextAsync()).ShouldNotContain(generados.Codigos[0]);
        await Correos.EsperarAsync(correo, c => c.Asunto == AsuntoCodigosGenerados);
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession())
        {
            var documento = (await consulta.LoadAsync<CodigosDeRecuperacion>(CodigosDeRecuperacion.IdDe(new CuentaDeIngreso(TipoDePropietario.Usuario, a.Grupo.TenantId, a.Usuario.Id.ToString()))))!;
            JsonSerializer.Serialize(documento).ShouldNotContain(generados.Codigos[0].Replace("-", string.Empty)); // solo hashes
        }

        // Perdió el dispositivo: el código del correo no basta (hay passkey) y un código de recuperación incorrecto no sirve ni cuenta más de cinco.
        var desafio = await PedirCodigoAsync(correo);
        var paso = (await VerificarCodigoAsync(desafio, await CodigoDelCorreoAsync(correo, 2))).Paso; // el 1.º fue el del registro de la passkey
        paso.Estado.ShouldBe("PasskeyRequerida");
        var problema = await (await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(desafio, "AAAAA-BBBBB"), 422)).ReadAsJsonAsync<JsonElement>();
        (problema.GetProperty("codigo").GetString(), problema.GetProperty("intentosRestantes").GetInt32()).ShouldBe(("codigo-de-recuperacion-invalido", 4));

        // El correcto gasta el código y sigue el registro de una passkey nueva, que abre la sesión.
        var registro = await (await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(desafio, generados.Codigos[0].ToLowerInvariant()), 200)).ReadAsJsonAsync<PasoDeIngresoDto>();
        registro!.Estado.ShouldBe("RegistrarPasskey");
        var nueva = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var sesion = (await RegistrarEnIngresoAsync(registro, nueva, "Teléfono nuevo")).Sesion!;
        sesion.Metodo.ShouldBe("CodigoYPasskey");
        (await GetAsync<List<PasskeyDto>>("/api/v1/mi-cuenta/passkeys", sesion.Token)).Count.ShouldBe(2);
        (await GetAsync<EstadoDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", sesion.Token)).Disponibles.ShouldBe(9);
        await Correos.EsperarAsync(correo, c => c.Asunto == AsuntoRecuperacionUsada);

        // El mismo código no sirve otra vez; otro del juego, sí.
        var segundo = await PedirCodigoAsync(correo);
        await VerificarCodigoAsync(segundo, await CodigoDelCorreoAsync(correo, 3));
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(segundo, generados.Codigos[0]), 422))).ShouldBe("codigo-de-recuperacion-invalido");
        (await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(segundo, generados.Codigos[1]), 200)).ShouldNotBeNull();

        // Un juego nuevo anula los anteriores.
        await ReautenticarAsync(nueva, sesion.Token);
        var otros = await PostJsonAsync<CodigosDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", null, sesion.Token, 200);
        otros.Codigos.ShouldNotContain(generados.Codigos[2]);
        var tercero = await PedirCodigoAsync(correo);
        await VerificarCodigoAsync(tercero, await CodigoDelCorreoAsync(correo, 4));
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(tercero, generados.Codigos[2]), 422))).ShouldBe("codigo-de-recuperacion-invalido");
        // Al quinto intento fallido el ingreso se bloquea aunque el código fuera el bueno.
        for (var i = 0; i < 4; i++)
            await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(tercero, "ZZZZZ-ZZZZZ"), 422);
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(tercero, otros.Codigos[0]), 429))).ShouldBe("recuperacion-bloqueada");
    }

    [Fact]
    public async Task Un_codigo_de_recuperacion_no_sirve_sin_haber_verificado_el_correo_ni_para_otra_cuenta()
    {
        var a = await AdministradorConPasskeyAsync();
        var otra = await AdministradorConPasskeyAsync();
        await ReautenticarAsync(a.Autenticador, a.Token);
        var codigos = (await PostJsonAsync<CodigosDeRecuperacionDto>("/api/v1/mi-cuenta/codigos-de-recuperacion", null, a.Token, 200)).Codigos;

        // Sin pasar por el código del correo (un desafío recién pedido) no hay recuperación.
        var sinVerificar = await PedirCodigoAsync(a.Usuario.Correo);
        await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(sinVerificar, codigos[0]), 410);
        await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(Guid.NewGuid(), codigos[0]), 404);

        // Los códigos de una cuenta no valen en el ingreso de otra.
        var deOtra = await PedirCodigoAsync(otra.Usuario.Correo);
        await VerificarCodigoAsync(deOtra, await CodigoDelCorreoAsync(otra.Usuario.Correo, 2));
        (await CodigoDeErrorAsync(await Anonimo("/api/v1/ingreso/recuperacion", new UsarCodigoDeRecuperacionDto(deOtra, codigos[0]), 422))).ShouldBe("codigo-de-recuperacion-invalido");

        // Sin passkey no hay qué respaldar.
        var g = await GrupoAsync();
        var revisor = await UsuarioAsync(g, "Revisor");
        var (paso, _) = await IngresarConCodigoAsync(revisor.Correo);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/mi-cuenta/codigos-de-recuperacion", null, paso.Sesion!.Token, 403))).ShouldBe("reautenticacion-requerida");
    }

    // ---------- avisos de seguridad por correo ----------

    [Fact]
    public async Task Registrar_revocar_o_eliminar_una_passkey_avisa_por_correo_a_su_dueno_sin_secretos()
    {
        var g = await GrupoAsync();
        var admin = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var autenticador = await RegistrarPasskeyAsync(admin.Correo);
        var registrado = await Correos.EsperarAsync(admin.Correo, c => c.Asunto == AsuntoPasskeyRegistrada);
        registrado.Texto.ShouldContain("Mi teléfono");
        registrado.Texto.ShouldContain("Si no fuiste tú");
        registrado.Texto.ShouldContain("/app/");
        registrado.Texto.ShouldNotContain(autenticador.CredencialIdBase64Url);

        // Otra desde Mi cuenta también avisa; eliminarla, también.
        var sesion = await EntrarConPasskeyAsync(autenticador);
        var segundo = new AutenticadorVirtual(ApiFixture.OrigenDeLaApp);
        var opciones = await PostJsonAsync<OpcionesDePasskeyDto>("/api/v1/mi-cuenta/passkeys/opciones", null, sesion.Token);
        var creada = await PostJsonAsync<PasskeyDto>("/api/v1/mi-cuenta/passkeys", new RegistrarPasskeyDto(opciones.Desafio, segundo.Registrar(opciones.Opciones), "Llave de la oficina"), sesion.Token);
        (await Correos.EsperarAsync(admin.Correo, c => c.Asunto == AsuntoPasskeyRegistrada && c.Texto.Contains("Llave de la oficina"))).ShouldNotBeNull();
        await ComoPersonaAsync("DELETE", $"/api/v1/mi-cuenta/passkeys/{creada.Id}", null, sesion.Token, 204);
        (await Correos.EsperarAsync(admin.Correo, c => c.Asunto == AsuntoPasskeyEliminada && c.Texto.Contains("Llave de la oficina"))).Texto.ShouldContain("Se eliminó");

        // Revocada por un administrador: la persona se entera y el aviso dice que lo hizo otra persona.
        var otro = await UsuarioAsync(g, "Administrador", deTodoElGrupo: true);
        var deOtro = await RegistrarPasskeyAsync(otro.Correo);
        var passkey = (await GetAsync<List<PasskeyDto>>($"/api/v1/usuarios/{otro.Id}/passkeys", sesion.Token)).Single();
        await ReautenticarAsync(autenticador, sesion.Token);
        await PostJsonAsync<PasskeyDto>($"/api/v1/usuarios/{otro.Id}/passkeys/{passkey.Id}/revocar", new RevocarPasskeyDto("Perdió la llave"), sesion.Token);
        var revocada = await Correos.EsperarAsync(otro.Correo, c => c.Asunto == AsuntoPasskeyRevocada);
        revocada.Texto.ShouldContain("Lo hizo un administrador");
        revocada.Texto.ShouldNotContain("Perdió la llave");
        _ = deOtro;
    }

    // ---------- sistemas integradores: desactivar y reactivar ----------

    /// <summary>Pide una sesión con la identidad delegada del sistema y devuelve la respuesta tal cual (sin leer el cuerpo: una 401 no lo trae).</summary>
    private Task<IScenarioResult> PedirSesionDelegadaAsync(SistemaDePrueba sistema, int esperado) =>
        Host.Scenario(s =>
        {
            s.Post.Json(new { }).ToUrl("/api/v1/sesiones-usuario");
            s.WithRequestHeader("X-Api-Key", sistema.ApiKey);
            s.WithRequestHeader("X-Usuario-Delegado", sistema.Asercion("laura-1", "laura.co"));
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<SistemaIntegradoDto> SistemaDelGrupoAsync(Grupo g, string codigo) =>
        (await ApiAsync<List<SistemaIntegradoDto>>("GET", "/api/v1/sistemas", g.Admin, null, null, 200)).Single(s => s.Codigo == codigo);

    [Fact]
    public async Task Desactivar_un_sistema_revoca_sus_llaves_y_corta_sus_sesiones_al_instante_y_reactivarlo_no_las_restaura()
    {
        var (g, sistema) = await GrupoConSistemaAsync(["Delegado"]);
        var datos = await SistemaDelGrupoAsync(g, sistema.Codigo);
        var sesion = await SesionDelegadaAsync(sistema, 200);
        (await ObtenerAsync<YoDto>("/api/v1/yo", sesion.Token)).ActorTipo.ShouldBe("Usuario");
        var llaves = await ApiAsync<List<ApiKeyDto>>("GET", "/api/v1/api-keys", g.Admin, null, null, 200);
        var deLaPrueba = llaves.Single(k => k.SistemaId == datos.Id);

        // Desactivarlo: sus API keys quedan revocadas, ya no autentican y la sesión que emitió deja de valer (sin esperar a que venza).
        var desactivado = await ApiAsync<SistemaIntegradoDto>("POST", $"/api/v1/sistemas/{datos.Id}/desactivar", g.Admin, null, null, 200);
        desactivado.Activo.ShouldBeFalse();
        await PedirSesionDelegadaAsync(sistema, 401);
        await Anonimo("/api/v1/api-keys", null, 401, metodo: "GET", apiKey: sistema.ApiKey);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesion.Token);
        (await ApiAsync<List<ApiKeyDto>>("GET", "/api/v1/api-keys", g.Admin, null, null, 200)).ShouldNotContain(k => k.Id == deLaPrueba.Id);
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/sistemas/{datos.Id}/desactivar", g.Admin, 409))).ShouldBe("sistema-inactivo");

        // Cada llave revocada quedó en la auditoría de configuración del grupo.
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(g.TenantId.ToString("N")))
        {
            var registros = await consulta.Query<Validasoft.Dominio.Auditoria.RegistroDeCambioDeConfiguracion>().Where(r => r.EntidadId == deLaPrueba.Id.ToString()).ToListAsync();
            registros.Select(r => r.Accion).ShouldContain(Validasoft.Dominio.Auditoria.AccionConfiguracion.Revocado);
            (await consulta.Query<Validasoft.Dominio.Auditoria.RegistroDeCambioDeConfiguracion>().Where(r => r.EntidadId == datos.Id.ToString()).ToListAsync())
                .Select(r => r.Accion).ShouldContain(Validasoft.Dominio.Auditoria.AccionConfiguracion.Desactivado);
        }

        // Reactivarlo lo deja activo pero las llaves revocadas no vuelven: hay que emitir una nueva.
        var reactivado = await ApiAsync<SistemaIntegradoDto>("POST", $"/api/v1/sistemas/{datos.Id}/reactivar", g.Admin, null, null, 200);
        reactivado.Activo.ShouldBeTrue();
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/sistemas/{datos.Id}/reactivar", g.Admin, 409))).ShouldBe("sistema-activo");
        await PedirSesionDelegadaAsync(sistema, 401);
        await Anonimo("/api/v1/yo", null, 401, metodo: "GET", bearer: sesion.Token);
        var nueva = await NuevaLlaveAsync(g, "Integración nueva", datos.Id);
        (await SesionDelegadaAsync(sistema with { ApiKey = nueva.Valor! }, 200)).Token.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Plataforma_ve_desactiva_y_reactiva_los_sistemas_de_un_grupo_y_las_acciones_de_una_sesion_directa_piden_la_passkey()
    {
        var (g, sistema) = await GrupoConSistemaAsync(["Delegado"]);
        var (token, autenticador, _) = await CrearOperadorAsync();
        var plataforma = await GetAsync<List<SistemaIntegradoDto>>($"/api/v1/plataforma/tenants/{g.TenantId}/sistemas", token);
        var datos = plataforma.Single(s => s.Codigo == sistema.Codigo);
        (await SesionDelegadaAsync(sistema, 200)).ShouldNotBeNull();

        // Con la clave de arranque (una credencial, no una persona) se desactiva sin reautenticar; el grupo de otro no se toca.
        await ApiAsync<SistemaIntegradoDto>("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/desactivar", ApiFixture.ClavePlataforma, null, null, 200);
        await PedirSesionDelegadaAsync(sistema, 401);
        var otroGrupo = await GrupoAsync();
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/plataforma/tenants/{otroGrupo.TenantId}/sistemas/{datos.Id}/reactivar", ApiFixture.ClavePlataforma, 404))).ShouldBe("sistema-no-encontrado");
        await ApiRaw("POST", $"/api/v1/plataforma/tenants/{Guid.NewGuid()}/sistemas/{datos.Id}/reactivar", ApiFixture.ClavePlataforma, 404);
        await ApiAsync<SistemaIntegradoDto>("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/reactivar", ApiFixture.ClavePlataforma, null, null, 200);

        // Un operador con sesión directa sí debe confirmar con su passkey; sin hacerlo, 403.
        await ComoPersonaAsync("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/desactivar", null, token, 403);
        await ReautenticarAsync(autenticador, token);
        (await ComoPersonaAsync("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/desactivar", null, token, 200)).ShouldNotBeNull();
        await ComoPersonaAsync("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/reactivar", null, token, 200);
        // La credencial de un grupo no entra a la consola de Plataforma.
        await ApiRaw("POST", $"/api/v1/plataforma/tenants/{g.TenantId}/sistemas/{datos.Id}/desactivar", g.Admin, 403);
    }

    /// <summary>Un operador de Plataforma de ingreso directo nuevo, con su passkey y la sesión abierta (token de acceso).</summary>
    private async Task<(string Token, AutenticadorVirtual Autenticador, OperadorDePlataformaDto Operador)> CrearOperadorAsync(string? correo = null)
    {
        correo ??= $"op{Guid.NewGuid():N}@validasoft.co";
        var operador = await ApiAsync<OperadorDePlataformaDto>("POST", "/api/v1/plataforma/operadores", ApiFixture.ClavePlataforma, null, new CrearOperadorDto("Operador " + correo[..6], correo), 201);
        var autenticador = await RegistrarPasskeyAsync(correo);
        return ((await EntrarConPasskeyAsync(autenticador)).Token, autenticador, operador);
    }

    // ---------- operadores de Plataforma: desactivar, reactivar, auditoría y clave de arranque ----------

    [Fact]
    public async Task Un_operador_desactivado_pierde_sus_sesiones_y_passkeys_y_al_reactivarlo_entra_con_el_codigo_y_registra_una_nueva()
    {
        var (token, autenticador, yo) = await CrearOperadorAsync();
        var (tokenOtro, autenticadorOtro, otro) = await CrearOperadorAsync();
        var ruta = Uri.EscapeDataString(otro.Id);

        // Sin confirmar con la passkey, 403; nadie se desactiva a sí mismo.
        await ComoPersonaAsync("POST", $"/api/v1/plataforma/operadores/{ruta}/desactivar", null, token, 403);
        await ReautenticarAsync(autenticador, token);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", $"/api/v1/plataforma/operadores/{Uri.EscapeDataString(yo.Id)}/desactivar", null, token, 409))).ShouldBe("auto-desactivacion");

        // Desactivado: ya no puede entrar, sus sesiones (la de acceso incluida) y sus passkeys dejan de valer al instante.
        var desactivado = await PostJsonAsync<OperadorDePlataformaDto>($"/api/v1/plataforma/operadores/{ruta}/desactivar", null, token);
        desactivado.Activo.ShouldBeFalse();
        await Anonimo("/api/v1/plataforma/tenants", null, 401, metodo: "GET", bearer: tokenOtro);
        (await GetAsync<List<PasskeyDto>>($"/api/v1/plataforma/operadores/{ruta}/passkeys", token)).ShouldAllBe(p => !p.Activa);
        var solo = await (await Anonimo("/api/v1/ingreso/passkey/opciones", null, 200)).ReadAsJsonAsync<OpcionesDePasskeyDto>();
        await Anonimo("/api/v1/ingreso/passkey/verificar", new VerificarPasskeyDto(solo!.Desafio, autenticadorOtro.Firmar(solo.Opciones)), 401);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", $"/api/v1/plataforma/operadores/{ruta}/desactivar", null, token, 409))).ShouldBe("operador-inactivo");

        // Reactivarlo envía la invitación; sus passkeys revocadas no vuelven, así que registra una nueva con el código del correo.
        await PostJsonAsync<OperadorDePlataformaDto>($"/api/v1/plataforma/operadores/{ruta}/reactivar", null, token);
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", $"/api/v1/plataforma/operadores/{ruta}/reactivar", null, token, 409))).ShouldBe("operador-activo");
        await Correos.EsperarAsync(otro.Correo, c => c.Asunto.Contains("te invitó a Validasoft"), cuantos: 2);
        var paso = (await VerificarCodigoAsync(await PedirCodigoAsync(otro.Correo), await CodigoDelCorreoAsync(otro.Correo, 2))).Paso;
        paso.Estado.ShouldBe("RegistrarPasskey");
        var nuevo = await RegistrarEnIngresoAsync(paso, new AutenticadorVirtual(ApiFixture.OrigenDeLaApp), "Llave nueva");
        (await GetAsync<List<TenantDto>>("/api/v1/plataforma/tenants", nuevo.Sesion!.Token)).ShouldNotBeNull();
        (await GetAsync<List<OperadorDePlataformaDto>>("/api/v1/plataforma/operadores", token)).Single(o => o.Id == otro.Id).Activo.ShouldBeTrue();
    }

    [Fact]
    public async Task Desactivar_un_sistema_de_plataforma_corta_la_sesion_de_su_operador_y_reactivarlo_deja_abrir_otra()
    {
        var codigo = "PL" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var clave = System.Security.Cryptography.RSA.Create(2048);
        await ApiAsync<SistemaDePlataformaDto>("PUT", $"/api/v1/plataforma/sistemas/{codigo}", ApiFixture.ClavePlataforma, null,
            new FijarSistemaDePlataformaDto("Backoffice", clave.ExportSubjectPublicKeyInfoPem(), ["https://backoffice.validasoft.test"]), 200);
        var sistema = new SistemaDePrueba(codigo, clave, ApiFixture.ClavePlataforma);

        async Task<IScenarioResult> AbrirSesionAsync(int esperado) =>
            await Host.Scenario(s =>
            {
                s.Post.Json(new { }).ToUrl("/api/v1/plataforma/sesiones");
                s.WithRequestHeader("X-Api-Key", ApiFixture.ClavePlataforma);
                s.WithRequestHeader("X-Usuario-Delegado", sistema.Asercion("op-" + codigo, $"op@{codigo.ToLowerInvariant()}.co"));
                s.StatusCodeShouldBe(esperado);
            });

        var sesion = (await (await AbrirSesionAsync(200)).ReadAsJsonAsync<SesionUsuarioDto>())!;
        (await GetAsync<List<TenantDto>>("/api/v1/plataforma/tenants", sesion.Token)).ShouldNotBeNull();

        await ApiAsync<SistemaDePlataformaDto>("POST", $"/api/v1/plataforma/sistemas/{codigo}/desactivar", ApiFixture.ClavePlataforma, null, null, 200);
        await Anonimo("/api/v1/plataforma/tenants", null, 401, metodo: "GET", bearer: sesion.Token);
        await AbrirSesionAsync(401);
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/plataforma/sistemas/{codigo}/desactivar", ApiFixture.ClavePlataforma, 409))).ShouldBe("sistema-inactivo");

        await ApiAsync<SistemaDePlataformaDto>("POST", $"/api/v1/plataforma/sistemas/{codigo}/reactivar", ApiFixture.ClavePlataforma, null, null, 200);
        (await CodigoDeErrorAsync(await ApiRaw("POST", $"/api/v1/plataforma/sistemas/{codigo}/reactivar", ApiFixture.ClavePlataforma, 409))).ShouldBe("sistema-activo");
        var otra = (await (await AbrirSesionAsync(200)).ReadAsJsonAsync<SesionUsuarioDto>())!;
        (await GetAsync<List<TenantDto>>("/api/v1/plataforma/tenants", otra.Token)).ShouldNotBeNull();
        await ApiRaw("POST", $"/api/v1/plataforma/sistemas/NOEXISTE/desactivar", ApiFixture.ClavePlataforma, 404);
    }

    [Fact]
    public async Task La_consola_ve_los_ingresos_de_los_operadores_con_filtros_y_paginacion_y_solo_Plataforma_los_ve()
    {
        var (token, autenticador, operador) = await CrearOperadorAsync();
        await ReautenticarAsync(autenticador, token);
        await EntrarConPasskeyAsync(autenticador);
        await PedirCodigoAsync(operador.Correo); // un código pedido que nadie verifica
        var desafio = await PedirCodigoAsync(operador.Correo);
        await Anonimo("/api/v1/ingreso/codigo/verificar", new VerificarCodigoDto(desafio, "000000"), 422); // un intento fallido

        var url = $"/api/v1/plataforma/auditoria-ingresos?operador={Uri.EscapeDataString(operador.Id)}&limite=100";
        var todos = await GetAsync<PaginaDto<IngresoDeOperadorDto>>(url, token);
        var eventos = todos.Datos.Select(r => r.Evento).ToList();
        eventos.ShouldContain("IngresoExitoso");
        eventos.ShouldContain("PasskeyRegistrada");
        eventos.ShouldContain("ReautenticacionExitosa");
        eventos.ShouldContain("CodigoIncorrecto");
        todos.Datos.ShouldAllBe(r => r.OperadorId == operador.Id);
        todos.Datos.Where(r => r.Evento == "IngresoExitoso").ShouldAllBe(r => r.Exitoso && r.Ip != null && r.Dispositivo != null);
        (todos.Datos.Select(r => r.Fecha).ToList()).ShouldBe([.. todos.Datos.Select(r => r.Fecha).OrderByDescending(f => f)]);
        JsonSerializer.Serialize(todos).ShouldNotContain("000000"); // nunca un código

        // Filtros: por evento, por resultado y por rango; paginación por cursor.
        (await GetAsync<PaginaDto<IngresoDeOperadorDto>>(url + "&evento=PasskeyRegistrada", token)).Datos.ShouldAllBe(r => r.Evento == "PasskeyRegistrada");
        (await GetAsync<PaginaDto<IngresoDeOperadorDto>>(url + "&exitoso=false", token)).Datos.ShouldAllBe(r => !r.Exitoso);
        (await GetAsync<PaginaDto<IngresoDeOperadorDto>>(url + $"&desde={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddHours(1).ToString("O"))}", token)).Datos.ShouldBeEmpty();
        var primera = await GetAsync<PaginaDto<IngresoDeOperadorDto>>($"/api/v1/plataforma/auditoria-ingresos?operador={Uri.EscapeDataString(operador.Id)}&limite=2", token);
        primera.Datos.Count.ShouldBe(2);
        var segunda = await GetAsync<PaginaDto<IngresoDeOperadorDto>>($"/api/v1/plataforma/auditoria-ingresos?operador={Uri.EscapeDataString(operador.Id)}&limite=2&cursor={Uri.EscapeDataString(primera.SiguienteCursor!)}", token);
        segunda.Datos.Select(r => r.Id).ShouldNotContain(primera.Datos[0].Id);
        (await CodigoDeErrorAsync(await Anonimo(url + "&evento=Inventado", null, 422, metodo: "GET", bearer: token))).ShouldBe("evento-invalido");

        // Las personas de un grupo no aparecen aquí y la credencial de un grupo no entra.
        var g = await GrupoAsync();
        var usuario = await UsuarioAsync(g, "Revisor");
        await IngresarConCodigoAsync(usuario.Correo);
        (await GetAsync<PaginaDto<IngresoDeOperadorDto>>("/api/v1/plataforma/auditoria-ingresos?limite=200", token)).Datos.ShouldNotContain(r => r.Correo == usuario.Correo);
        await Anonimo("/api/v1/plataforma/auditoria-ingresos", null, 403, metodo: "GET", apiKey: g.Admin);
    }

    [Fact]
    public async Task La_clave_de_arranque_se_desactiva_desde_la_consola_en_el_acto_y_se_puede_reactivar()
    {
        var (token, autenticador, _) = await CrearOperadorAsync();
        var antes = await GetAsync<ClaveDeArranqueDto>("/api/v1/plataforma/clave-de-arranque", token);
        (antes.Configurada, antes.Desactivada, antes.OperadoresConPasskey >= 1).ShouldBe((true, false, true));

        try
        {
            // Es una acción sensible: la sesión de un operador debe confirmar con su passkey.
            await ComoPersonaAsync("POST", "/api/v1/plataforma/clave-de-arranque/desactivar", null, token, 403);
            await ReautenticarAsync(autenticador, token);
            var apagada = await PostJsonAsync<ClaveDeArranqueDto>("/api/v1/plataforma/clave-de-arranque/desactivar", null, token);
            (apagada.Desactivada, apagada.DesactivadaEn is not null, apagada.DesactivadaPor!.StartsWith("Plataforma:", StringComparison.Ordinal)).ShouldBe((true, true, true));

            // La clave deja de valer al instante y las sesiones de los operadores siguen sirviendo.
            await Anonimo("/api/v1/plataforma/tenants", null, 401, metodo: "GET", apiKey: ApiFixture.ClavePlataforma);
            (await GetAsync<ClaveDeArranqueDto>("/api/v1/plataforma/clave-de-arranque", token)).Desactivada.ShouldBeTrue();
            (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/plataforma/clave-de-arranque/desactivar", null, token, 409))).ShouldBe("clave-de-arranque-desactivada");
        }
        finally
        {
            // Reactivarla (por si el servidor aún la tiene) con la sesión del operador; el resto de las pruebas la necesita.
            var reactivada = await PostJsonAsync<ClaveDeArranqueDto>("/api/v1/plataforma/clave-de-arranque/reactivar", null, token);
            reactivada.Desactivada.ShouldBeFalse();
        }

        (await GetAsync<List<TenantDto>>("/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma)).ShouldNotBeNull();
        (await CodigoDeErrorAsync(await ComoPersonaAsync("POST", "/api/v1/plataforma/clave-de-arranque/reactivar", null, token, 409))).ShouldBe("clave-de-arranque-activa");
        // Con la propia clave de arranque (una credencial, no una persona) también se consulta.
        (await ApiAsync<ClaveDeArranqueDto>("GET", "/api/v1/plataforma/clave-de-arranque", ApiFixture.ClavePlataforma, null, null, 200)).Configurada.ShouldBeTrue();
    }

    private async Task<T> GetAsync<T>(string url, string credencial) where T : class =>
        credencial.StartsWith("vsk_", StringComparison.Ordinal) || credencial == ApiFixture.ClavePlataforma
            ? await ApiAsync<T>("GET", url, credencial, null, null, 200)
            : await ObtenerAsync<T>(url, credencial);

    // ---------- agregar empresas a un grupo ----------

    [Fact]
    public async Task Plataforma_agrega_empresas_a_un_grupo_existente_con_su_auditoria_y_sin_duplicar_al_repetir()
    {
        var g = await GrupoAsync(); // ya trae «Norte» (NIT 800197268-4, prefijo NORTE)
        var dto = new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "900123456-8"), "Sur S.A.S.", "SUR", null, "soporte@sur.co");
        var llave = Guid.NewGuid().ToString();

        async Task<IScenarioResult> Crear(object cuerpo, int esperado, string? idempotencia = null, string? credencial = null, Guid? grupo = null) =>
            await Host.Scenario(s =>
            {
                s.Post.Json(cuerpo).ToUrl($"/api/v1/plataforma/tenants/{grupo ?? g.TenantId}/empresas");
                s.WithRequestHeader("X-Api-Key", credencial ?? ApiFixture.ClavePlataforma);
                s.WithRequestHeader("Idempotency-Key", idempotencia ?? Guid.NewGuid().ToString());
                s.StatusCodeShouldBe(esperado);
            });

        var creada = await Crear(dto, 201, llave);
        var empresa = (await creada.ReadAsJsonAsync<EmpresaDto>())!;
        (empresa.TenantId, empresa.Nombre, empresa.PrefijoFolio, empresa.Activa, empresa.ReplyTo).ShouldBe((g.TenantId, "Sur S.A.S.", "SUR", true, "soporte@sur.co"));
        creada.Context.Response.Headers.Location.ToString().ShouldBe($"/api/v1/empresas/{empresa.Id}");

        // La consola y el administrador del grupo la ven; repetir con la misma Idempotency-Key devuelve la misma sin crear otra.
        var grupo = await ApiAsync<GrupoDto>("GET", $"/api/v1/plataforma/grupos/{g.TenantId}", ApiFixture.ClavePlataforma, null, null, 200);
        grupo.Empresas.Select(e => e.Nombre).ShouldBe(["Norte S.A.S.", "Sur S.A.S."], ignoreOrder: true);
        (await ApiAsync<List<EmpresaDto>>("GET", "/api/v1/empresas", g.Admin, null, null, 200)).Select(e => e.Id).ShouldContain(empresa.Id);
        var repetida = await Crear(dto, 201, llave);
        repetida.Context.Response.Headers["Idempotent-Replayed"].ToString().ShouldBe("true");
        (await repetida.ReadAsJsonAsync<EmpresaDto>())!.Id.ShouldBe(empresa.Id);
        (await ApiAsync<GrupoDto>("GET", $"/api/v1/plataforma/grupos/{g.TenantId}", ApiFixture.ClavePlataforma, null, null, 200)).Empresas.Count.ShouldBe(2);

        // Reglas: identificación y prefijo únicos en el grupo, prefijo válido, grupo existente y activo, solo Plataforma.
        (await CodigoDeErrorAsync(await Crear(dto with { PrefijoFolio = "OTRO" }, 409))).ShouldBe("empresa-duplicada");
        (await CodigoDeErrorAsync(await Crear(dto with { Identificacion = new IdentificacionDto("CO", "NIT", "899999068-1") }, 409))).ShouldBe("prefijo-folio-duplicado");
        (await CodigoDeErrorAsync(await Crear(dto with { Identificacion = new IdentificacionDto("CO", "NIT", "899999068-1"), PrefijoFolio = "s" }, 422))).ShouldBe("prefijo-folio-invalido");
        (await CodigoDeErrorAsync(await Crear(dto with { Identificacion = new IdentificacionDto("CO", "NIT", "899999068-1"), PrefijoFolio = "OTRO", Nombre = " " }, 422))).ShouldBe("nombre-requerido");
        await Crear(dto, 404, grupo: Guid.NewGuid());
        await Crear(dto, 403, credencial: g.Admin);
        await Host.Scenario(s =>
        {
            s.Post.Json(dto).ToUrl($"/api/v1/plataforma/tenants/{g.TenantId}/empresas");
            s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(401);
        });
        // Sin la cabecera Idempotency-Key, un POST que crea no se acepta.
        await Host.Scenario(s =>
        {
            s.Post.Json(dto with { PrefijoFolio = "NUEVA" }).ToUrl($"/api/v1/plataforma/tenants/{g.TenantId}/empresas");
            s.WithRequestHeader("X-Api-Key", ApiFixture.ClavePlataforma);
            s.StatusCodeShouldBe(422);
        });

        // Un grupo desactivado no recibe empresas.
        var almacen = Host.Services.GetRequiredService<IDocumentStore>();
        await using (var sesionDelAlmacen = almacen.LightweightSession())
        {
            var tenant = (await sesionDelAlmacen.LoadAsync<Validasoft.Dominio.Plataforma.Tenant>(g.TenantId))!;
            tenant.Activo = false;
            sesionDelAlmacen.Store(tenant);
            await sesionDelAlmacen.SaveChangesAsync();
        }

        try
        {
            (await CodigoDeErrorAsync(await Crear(dto with { Identificacion = new IdentificacionDto("CO", "NIT", "899999068-1"), PrefijoFolio = "OTRO" }, 409))).ShouldBe("grupo-inactivo");
        }
        finally
        {
            await using var restaurar = almacen.LightweightSession();
            var tenant = (await restaurar.LoadAsync<Validasoft.Dominio.Plataforma.Tenant>(g.TenantId))!;
            tenant.Activo = true;
            restaurar.Store(tenant);
            await restaurar.SaveChangesAsync();
        }

        // Quedó en la auditoría de configuración del grupo, a nombre de Plataforma.
        await using var consulta = almacen.QuerySession(g.TenantId.ToString("N"));
        var registro = (await consulta.Query<Validasoft.Dominio.Auditoria.RegistroDeCambioDeConfiguracion>().Where(r => r.EntidadId == empresa.Id.ToString()).ToListAsync()).Single();
        (registro.Entidad, registro.Accion, registro.ActorTipo).ShouldBe(("Empresa", Validasoft.Dominio.Auditoria.AccionConfiguracion.Creado, Validasoft.Dominio.Comun.ActorTipo.Plataforma));
    }

    // ---------- la idempotencia no guarda secretos ----------

    private async Task<(IScenarioResult Resultado, string Cuerpo)> PostConLlaveAsync(string url, object? cuerpo, string apiKey, string llave, int esperado, IDictionary<string, string>? cabeceras = null)
    {
        var resultado = await Host.Scenario(s =>
        {
            if (cuerpo is null)
                s.Post.Url(url);
            else
                s.Post.Json(cuerpo).ToUrl(url);
            s.WithRequestHeader("X-Api-Key", apiKey);
            s.WithRequestHeader("Idempotency-Key", llave);
            foreach (var (nombre, valor) in cabeceras ?? new Dictionary<string, string>())
                s.WithRequestHeader(nombre, valor);
            s.StatusCodeShouldBe(esperado);
        });
        return (resultado, await resultado.ReadAsTextAsync());
    }

    private async Task NingunRegistroDeIdempotenciaGuardaAsync(params string?[] secretos)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var registros = await consulta.Query<RegistroIdempotencia>().ToListAsync();
        foreach (var secreto in secretos.Where(s => !string.IsNullOrEmpty(s)))
            registros.ShouldNotContain(r => r.Cuerpo.Contains(secreto!, StringComparison.Ordinal), $"un registro de idempotencia guardó el secreto {secreto![..6]}…");
    }

    [Fact]
    public async Task La_respuesta_con_una_llave_se_guarda_sin_el_secreto_y_al_repetir_la_peticion_responde_sin_el_con_una_indicacion_clara()
    {
        var g = await GrupoAsync();
        var peticion = new CrearApiKeyDto("Integración con secreto", ["configuracion:leer"], null, null);
        var llave = Guid.NewGuid().ToString();

        // La primera respuesta trae la llave completa, una sola vez.
        var (primera, textoPrimera) = await PostConLlaveAsync("/api/v1/api-keys", peticion, g.Admin, llave, 201);
        var original = JsonSerializer.Deserialize<ApiKeyDto>(textoPrimera, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        original.Valor.ShouldStartWith("vsk_");
        primera.Context.Response.Headers.ContainsKey("Idempotent-Replayed").ShouldBeFalse();

        // Repetirla devuelve la misma llave creada (mismo id) pero sin el valor, con la indicación en el cuerpo y en un encabezado.
        var (repetida, textoRepetida) = await PostConLlaveAsync("/api/v1/api-keys", peticion, g.Admin, llave, 201);
        var json = JsonDocument.Parse(textoRepetida).RootElement;
        json.GetProperty("id").GetGuid().ShouldBe(original.Id);
        json.GetProperty("prefijo").GetString().ShouldBe(original.Prefijo);
        json.GetProperty("valor").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("secretosOmitidos").EnumerateArray().Select(e => e.GetString()).ShouldBe(["valor"]);
        repetida.Context.Response.Headers["Idempotent-Replayed"].ToString().ShouldBe("true");
        repetida.Context.Response.Headers["Validasoft-Secretos-Omitidos"].ToString().ShouldBe("valor");
        textoRepetida.ShouldNotContain(original.Valor!);
        await NingunRegistroDeIdempotenciaGuardaAsync(original.Valor, original.Valor![^20..]);

        // La misma clave con otro cuerpo sigue siendo un error.
        (await CodigoDeErrorAsync(await Host.Scenario(s =>
        {
            s.Post.Json(peticion with { Nombre = "Otra distinta" }).ToUrl("/api/v1/api-keys");
            s.WithRequestHeader("X-Api-Key", g.Admin);
            s.WithRequestHeader("Idempotency-Key", llave);
            s.StatusCodeShouldBe(422);
        }))).ShouldBe("clave-idempotencia-reutilizada");

        // Rotar con una Idempotency-Key también se repite sin el valor nuevo (antes devolvía 409 ya-rotada).
        var rotacion = Guid.NewGuid().ToString();
        var (_, textoRotada) = await PostConLlaveAsync($"/api/v1/api-keys/{original.Id}/rotar", new RotarApiKeyDto(), g.Admin, rotacion, 201);
        var rotada = JsonSerializer.Deserialize<ApiKeyDto>(textoRotada, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var (repetidaRotacion, textoRepetidaRotacion) = await PostConLlaveAsync($"/api/v1/api-keys/{original.Id}/rotar", new RotarApiKeyDto(), g.Admin, rotacion, 201);
        JsonDocument.Parse(textoRepetidaRotacion).RootElement.GetProperty("valor").ValueKind.ShouldBe(JsonValueKind.Null);
        JsonDocument.Parse(textoRepetidaRotacion).RootElement.GetProperty("id").GetGuid().ShouldBe(rotada.Id);
        repetidaRotacion.Context.Response.Headers["Idempotent-Replayed"].ToString().ShouldBe("true");
        await NingunRegistroDeIdempotenciaGuardaAsync(rotada.Valor);

        // Rotar sin la cabecera sigue como siempre: dos veces la misma, 409.
        (await CodigoDeErrorAsync(await Host.Scenario(s =>
        {
            s.Post.Json(new RotarApiKeyDto()).ToUrl($"/api/v1/api-keys/{original.Id}/rotar");
            s.WithRequestHeader("X-Api-Key", g.Admin);
            s.StatusCodeShouldBe(409);
        }))).ShouldBe("api-key-ya-rotada");

        // Lo que no lleva secretos se repite completo, sin indicación.
        var empresaLlave = Guid.NewGuid().ToString();
        var empresa = new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "900123456-8"), "Sur S.A.S.", "SUR", null, null);
        var (_, textoEmpresa) = await PostConLlaveAsync("/api/v1/empresas", empresa, g.Admin, empresaLlave, 201);
        var (repetidaEmpresa, textoEmpresaRepetida) = await PostConLlaveAsync("/api/v1/empresas", empresa, g.Admin, empresaLlave, 201);
        textoEmpresaRepetida.ShouldBe(textoEmpresa);
        repetidaEmpresa.Context.Response.Headers.ContainsKey("Validasoft-Secretos-Omitidos").ShouldBeFalse();
    }

    [Fact]
    public async Task El_alta_de_un_grupo_y_las_sesiones_se_repiten_sin_sus_llaves_ni_tokens()
    {
        // El alta de un grupo con su sistema: ni la llave del administrador ni la del sistema quedan en el registro.
        using var clave = System.Security.Cryptography.RSA.Create(2048);
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var alta = new CrearTenantDto(slug, "Grupo " + slug, "co", "America/Bogota",
            Empresa: new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null),
            Administrador: new AdministradorInicialDto("Ana Admin", CorreoNuevo(), "ana"),
            Sistema: new CrearSistemaIntegradoDto("SRM", "Sistema", clave.ExportSubjectPublicKeyInfoPem()));
        var llave = Guid.NewGuid().ToString();
        var (_, texto) = await PostConLlaveAsync("/api/v1/plataforma/tenants", alta, ApiFixture.ClavePlataforma, llave, 201);
        var creado = JsonSerializer.Deserialize<TenantCreadoDto>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        creado.ApiKeyAdministrador.Valor.ShouldNotBeNull();
        creado.ApiKeySistema!.Valor.ShouldNotBeNull();

        var (repetido, textoRepetido) = await PostConLlaveAsync("/api/v1/plataforma/tenants", alta, ApiFixture.ClavePlataforma, llave, 201);
        var json = JsonDocument.Parse(textoRepetido).RootElement;
        json.GetProperty("tenant").GetProperty("id").GetGuid().ShouldBe(creado.Tenant.Id);
        json.GetProperty("apiKeyAdministrador").GetProperty("valor").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("apiKeySistema").GetProperty("valor").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("apiKeyAdministrador").GetProperty("prefijo").GetString().ShouldBe(creado.ApiKeyAdministrador.Prefijo);
        json.GetProperty("secretosOmitidos").EnumerateArray().Select(e => e.GetString()).ShouldBe(["valor"]);
        repetido.Context.Response.Headers["Validasoft-Secretos-Omitidos"].ToString().ShouldBe("valor");
        await NingunRegistroDeIdempotenciaGuardaAsync(creado.ApiKeyAdministrador.Valor, creado.ApiKeySistema.Valor);

        // Sin la cabecera el alta no la exige (nunca la exigió): el slug repetido responde 409.
        (await CodigoDeErrorAsync(await Host.Scenario(s =>
        {
            s.Post.Json(alta).ToUrl("/api/v1/plataforma/tenants");
            s.WithRequestHeader("X-Api-Key", ApiFixture.ClavePlataforma);
            s.StatusCodeShouldBe(409);
        }))).ShouldBe("slug-duplicado");

        // La sesión de la identidad delegada: el token de la primera respuesta no se guarda; repetir (con otra aserción: cada una vale una vez) responde sin él.
        var (g, sistema) = await GrupoConSistemaAsync(["Delegado"]);
        var sesionLlave = Guid.NewGuid().ToString();
        async Task<(IScenarioResult Resultado, string Texto)> Sesion() => await PostConLlaveAsync("/api/v1/sesiones-usuario", null, sistema.ApiKey, sesionLlave, 200,
            new Dictionary<string, string> { ["X-Usuario-Delegado"] = sistema.Asercion("laura-1", "laura@constructora.co") });
        var (_, textoSesion) = await Sesion();
        var sesion = JsonSerializer.Deserialize<SesionUsuarioDto>(textoSesion, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var (sesionRepetida, textoSesionRepetida) = await Sesion();
        JsonDocument.Parse(textoSesionRepetida).RootElement.GetProperty("token").ValueKind.ShouldBe(JsonValueKind.Null);
        JsonDocument.Parse(textoSesionRepetida).RootElement.GetProperty("secretosOmitidos").EnumerateArray().Select(e => e.GetString()).ShouldBe(["token"]);
        sesionRepetida.Context.Response.Headers["Validasoft-Secretos-Omitidos"].ToString().ShouldBe("token");
        await NingunRegistroDeIdempotenciaGuardaAsync(sesion.Token);
        _ = g;
    }

    [Fact]
    public void El_redactor_quita_los_secretos_a_cualquier_profundidad_y_no_guarda_lo_que_no_puede_leer()
    {
        var (cuerpo, omitidos) = RedactorDeSecretos.Redactar("""{"id":1,"Valor":"vsk_secreto","hijo":{"valor":"otro","lista":[{"VALOR":"x"},{"nombre":"ok"}]},"nulo":null}""", ["valor"]);

        omitidos.ShouldBe(["VALOR", "Valor", "valor"], ignoreOrder: true);
        var json = JsonDocument.Parse(cuerpo!).RootElement;
        json.GetProperty("Valor").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("hijo").GetProperty("valor").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("hijo").GetProperty("lista")[0].GetProperty("VALOR").ValueKind.ShouldBe(JsonValueKind.Null);
        json.GetProperty("hijo").GetProperty("lista")[1].GetProperty("nombre").GetString().ShouldBe("ok");
        json.GetProperty("id").GetInt32().ShouldBe(1);
        var texto = cuerpo!;
        texto.ShouldNotContain("vsk_secreto");
        texto.ShouldNotContain("otro");

        // Sin el campo, o con él en null, el cuerpo queda igual.
        var limpio = """{"id":1,"valor":null}""";
        RedactorDeSecretos.Redactar(limpio, ["valor"]).ShouldBe((limpio, (IReadOnlyList<string>)[]));
        RedactorDeSecretos.Redactar("""{"id":1}""", ["valor"]).Omitidos.ShouldBeEmpty();
        // Lo que no es JSON no se puede redactar con certeza: no se guarda.
        RedactorDeSecretos.Redactar("<html>valor</html>", ["valor"]).Cuerpo.ShouldBeNull();
        RedactorDeSecretos.Redactar("", ["valor"]).Cuerpo.ShouldBe(string.Empty);
        MiddlewareDeIdempotencia.CamposSecretosDe("/api/v1/api-keys").ShouldBe(["valor"]);
        MiddlewareDeIdempotencia.CamposSecretosDe("/api/v1/webhooks/abc/rotar-secreto").ShouldBe(["secreto"]);
        MiddlewareDeIdempotencia.CamposSecretosDe("/api/v1/sesiones-tercero").ShouldBe(["token"]);
        MiddlewareDeIdempotencia.CamposSecretosDe("/api/v1/empresas").ShouldBeNull();
    }

    // ---------- las cachés de los servidores se invalidan entre sí ----------

    [Fact]
    public async Task Revocar_una_llave_en_otro_servidor_la_corta_aqui_de_inmediato_sin_esperar_los_cinco_minutos_de_la_cache()
    {
        var g = await GrupoAsync();
        var llave = await NuevaLlaveAsync(g, "Llave de otro servidor");
        (await Anonimo("/api/v1/empresas", null, 200, metodo: "GET", apiKey: llave.Valor)).ShouldNotBeNull(); // la verificación queda en la caché de este servidor

        // «Otro servidor» la revoca directamente en la base: este no se entera, la caché la sigue dando por buena.
        var almacen = Host.Services.GetRequiredService<IDocumentStore>();
        await using (var sesion = almacen.LightweightSession(g.TenantId.ToString("N")))
        {
            var documento = (await sesion.LoadAsync<ApiKey>(llave.Id))!;
            documento.RevocadaEn = DateTimeOffset.UtcNow;
            sesion.Store(documento);
            await sesion.SaveChangesAsync();
        }

        await Anonimo("/api/v1/empresas", null, 200, metodo: "GET", apiKey: llave.Valor);

        // Y le avisa por el canal compartido de la base (PostgreSQL LISTEN/NOTIFY): este servidor descarta su verificación y la rechaza.
        var bus = Host.Services.GetRequiredService<BusDeInvalidaciones>();
        await bus.EnviarAsync("otro-servidor", TiposDeInvalidacion.ApiKey, llave.Id.ToString(), CancellationToken.None);
        await EsperarAsync(async () =>
        {
            var respuesta = await Host.Scenario(s =>
            {
                s.Get.Url("/api/v1/empresas");
                s.WithRequestHeader("X-Api-Key", llave.Valor!);
                s.IgnoreStatusCode();
            });
            return respuesta.Context.Response.StatusCode == 401;
        }, "que la llave revocada en otro servidor deje de valer aquí");
    }

    [Fact]
    public async Task Cada_servidor_recibe_el_aviso_de_los_demas_y_no_el_propio_y_al_perder_la_conexion_descarta_todo()
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Validasoft"] = fixture.CadenaDeConexion }).Build();
        var busA = new BusDeInvalidaciones(configuracion, NullLogger<BusDeInvalidaciones>.Instance);
        var busB = new BusDeInvalidaciones(configuracion, NullLogger<BusDeInvalidaciones>.Instance);
        var espiaA = new ReceptorEspia("prueba-pi");
        var espiaB = new ReceptorEspia("prueba-pi");
        var otroTipo = new ReceptorEspia("otro-tipo");
        using var escuchaA = new EscuchaDeInvalidaciones(busA, [espiaA, otroTipo], configuracion, NullLogger<EscuchaDeInvalidaciones>.Instance);
        using var escuchaB = new EscuchaDeInvalidaciones(busB, [espiaB], configuracion, NullLogger<EscuchaDeInvalidaciones>.Instance);
        await escuchaA.StartAsync(CancellationToken.None);
        await escuchaB.StartAsync(CancellationToken.None);
        try
        {
            await EsperarAsync(async () => await EscuchandoAsync(configuracion) >= 2, "que los dos servidores escuchen el canal");

            busA.Publicar("prueba-pi", "abc");
            await EsperarAsync(() => Task.FromResult(espiaB.Recibidos.Contains("abc")), "el aviso del servidor A en el B");
            await Task.Delay(500);
            espiaA.Recibidos.ShouldBeEmpty(); // lo que un servidor publica ya lo olvidó por su lado: no se lo reenvía a sí mismo
            otroTipo.Recibidos.ShouldBeEmpty();
            await busB.PublicarAsync("prueba-pi", "def");
            await EsperarAsync(() => Task.FromResult(espiaA.Recibidos.Contains("def")), "el aviso del servidor B en el A");
            espiaB.Recibidos.ShouldBe(["abc"]);

            // Se cae la conexión de escucha del servidor B: al reconectar descarta todo lo guardado (pudo perder avisos).
            await using (var conexion = new NpgsqlConnection(configuracion.GetConnectionString("Validasoft")))
            {
                await conexion.OpenAsync();
                await using var comando = new NpgsqlCommand("select count(pg_terminate_backend(pid)) from pg_stat_activity where query like 'listen validasoft_invalidaciones%' and pid <> pg_backend_pid()", conexion);
                var terminadas = (long)(await comando.ExecuteScalarAsync())!;
                terminadas.ShouldBeGreaterThanOrEqualTo(2L);
            }

            await EsperarAsync(() => Task.FromResult(espiaA.Reinicios >= 1 && espiaB.Reinicios >= 1), "que cada servidor reconecte y descarte lo guardado");
            // Y vuelven a oírse.
            await EsperarAsync(async () => await EscuchandoAsync(configuracion) >= 2, "que vuelvan a escuchar");
            await busA.PublicarAsync("prueba-pi", "ghi");
            await EsperarAsync(() => Task.FromResult(espiaB.Recibidos.Contains("ghi")), "el aviso tras reconectar");
        }
        finally
        {
            await escuchaA.StopAsync(CancellationToken.None);
            await escuchaB.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<long> EscuchandoAsync(IConfiguration configuracion)
    {
        await using var conexion = new NpgsqlConnection(configuracion.GetConnectionString("Validasoft"));
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("select count(*) from pg_stat_activity where query like 'listen validasoft_invalidaciones%'", conexion);
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    private sealed class ReceptorEspia(string tipo) : IReceptorDeInvalidaciones
    {
        private int _reinicios;

        public string Tipo => tipo;

        public System.Collections.Concurrent.ConcurrentBag<string> Recibidos { get; } = [];

        public int Reinicios => _reinicios;

        public void Recibir(string id) => Recibidos.Add(id);

        public void Reiniciar() => Interlocked.Increment(ref _reinicios);
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(15);
        while (!await condicion())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No se cumplió a tiempo: {que}.");
            await Task.Delay(100);
        }
    }
}
