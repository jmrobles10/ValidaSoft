using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Validación por el interesado: enlace de un solo uso (con OTP para proveedores), sesión del tercero entre tenants,
/// textos legales versionados, aceptación con consentimientos, autorregistro y límite de peticiones.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class InteresadoApiTests(ApiFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private CorreosCapturados Correos => fixture.Correos;

    private sealed record Tenant(string ApiKey, EmpresaDto Empresa);

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex RegexCodigo();

    // ---------- enlace y canje ----------

    [Fact]
    public async Task Crear_un_vinculo_envia_el_enlace_que_sin_otp_abre_la_sesion_una_sola_vez()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");

        var correoInvitacion = await Correos.EsperarAsync(correo);
        correoInvitacion.Asunto.ShouldContain("Norte S.A.S.");
        correoInvitacion.Asunto.ShouldContain("cliente");
        correoInvitacion.Texto.ShouldContain($"{ApiFixture.UrlPublica}/e/");

        var sesion = await CanjearAsync(TokenDe(correoInvitacion));
        sesion.OtpVerificado.ShouldBeFalse();
        sesion.TerceroId.ShouldBe(vinculo.TerceroId);
        sesion.Vinculos.ShouldHaveSingleItem().Id.ShouldBe(vinculo.Id);
        sesion.Vinculos[0].Empresa.Nombre.ShouldBe("Norte S.A.S.");

        var segundo = await PublicoAsync("POST", $"/public/v1/enlaces/{TokenDe(correoInvitacion)}/canjear", null, esperado: 410);
        (await CodigoAsync(segundo)).ShouldBe("enlace-usado");

        var tipos = await ApiAsync<List<TipoVinculoDto>>("GET", "/api/v1/tipos-vinculo", t.ApiKey, null, null, 200);
        tipos.Single(x => x.Codigo == "PROVEEDOR").ExigeOtpEnEnlace.ShouldBeTrue();
        tipos.Single(x => x.Codigo == "CLIENTE").ExigeOtpEnEnlace.ShouldBeFalse();
    }

    [Fact]
    public async Task Un_proveedor_exige_otp_por_correo_antes_de_abrir_la_sesion()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        await CrearVinculoAsync(t, Nit(), correo, "PROVEEDOR");
        var token = TokenDe(await Correos.EsperarAsync(correo));

        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, esperado: 202);
        var pendiente = (await canje.ReadAsJsonAsync<OtpRequeridoDto>())!;
        pendiente.RequiereOtp.ShouldBeTrue();
        pendiente.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        var correoOtp = await Correos.EsperarAsync(correo, m => m.Asunto.Contains("código"));
        var codigo = RegexCodigo().Match(correoOtp.Texto).Groups[1].Value;
        codigo.Length.ShouldBe(6);
        correoOtp.Texto.ShouldNotContain(token);

        var malo = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo == "000000" ? "111111" : "000000"),
            esperado: 422);
        var problema = await malo.ReadAsJsonAsync<JsonElement>();
        problema.GetProperty("codigo").GetString().ShouldBe("otp-invalido");
        problema.GetProperty("intentosRestantes").GetInt32().ShouldBe(4);
        problema.GetProperty("instance").GetString()!.ShouldNotContain(token); // el token no sale en respuestas de error

        var bueno = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo), esperado: 200);
        var sesion = (await bueno.ReadAsJsonAsync<SesionTerceroDto>())!;
        sesion.OtpVerificado.ShouldBeTrue();
        sesion.Vinculos.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Reenviar_la_invitacion_invalida_el_enlace_anterior()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");
        var anterior = TokenDe(await Correos.EsperarAsync(correo));

        var reenvio = await ApiAsync<EnlaceInvitacionDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", t.ApiKey, t.Empresa.Id,
            new InvitacionDto(null, 48), 201);
        reenvio.Envios.ShouldBe(2); // el conteo real, no el del agregado ya actualizado + 1
        var leido = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", t.ApiKey, t.Empresa.Id, null, 200);
        leido.Invitacion!.Envios.ShouldBe(2);

        var nuevo = TokenDe(await Correos.EsperarAsync(correo, cuantos: 2));
        nuevo.ShouldNotBe(anterior);

        var viejo = await PublicoAsync("POST", $"/public/v1/enlaces/{anterior}/canjear", null, esperado: 404);
        (await CodigoAsync(viejo)).ShouldBe("enlace-no-encontrado");
        (await CanjearAsync(nuevo)).Vinculos.ShouldHaveSingleItem().Id.ShouldBe(vinculo.Id);
    }

    // ---------- sesión del tercero ----------

    [Fact]
    public async Task La_sesion_ve_sus_vinculos_de_dos_tenants_y_no_uno_ajeno()
    {
        var a = await TenantAsync("Norte S.A.S.");
        var b = await TenantAsync("Sur Global S.A.S.");
        var correo = CorreoNuevo();
        var nit = Nit();

        var deA = await CrearVinculoAsync(a, nit, correo, "CLIENTE");
        var deB = await CrearVinculoAsync(b, nit, correo, "CLIENTE");
        deB.TerceroId.ShouldBe(deA.TerceroId);
        var ajeno = await CrearVinculoAsync(a, Nit(), CorreoNuevo(), "CLIENTE");

        var token = TokenDe(await Correos.EsperarAsync(correo, m => m.Asunto.Contains("Norte S.A.S.")));
        var sesion = await CanjearAsync(token);
        sesion.Vinculos.Select(v => v.Id).ShouldBe([deA.Id, deB.Id], ignoreOrder: true);

        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200);
        vinculos.Select(v => v.Empresa.Nombre).ShouldBe(["Norte S.A.S.", "Sur Global S.A.S."], ignoreOrder: true);
        vinculos.ShouldAllBe(v => v.TipoVinculo.Codigo == "CLIENTE" && v.Estado == "Invitado" && !v.InvitacionAceptada);

        var deOtroTenant = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{deB.Id}", sesion.AccessToken, null, 200);
        deOtroTenant.Vinculo.Empresa.Id.ShouldBe(b.Empresa.Id);

        await TerceroAsync("GET", $"/public/v1/invitaciones/{ajeno.Id}", sesion.AccessToken, null, 404);
        await TerceroAsync("POST", $"/public/v1/invitaciones/{ajeno.Id}/aceptar", sesion.AccessToken, new AceptarInvitacionDto([]), 404);
    }

    [Fact]
    public async Task El_token_del_tercero_no_sirve_en_api_v1_ni_la_api_key_en_public_v1()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");
        var sesion = await CanjearAsync(TokenDe(await Correos.EsperarAsync(correo)));

        await Host.Scenario(s =>
        {
            s.Get.Url("/api/v1/vinculos");
            s.WithRequestHeader("Authorization", "Bearer " + sesion.AccessToken);
            s.WithRequestHeader("X-Empresa", t.Empresa.Id.ToString());
            s.StatusCodeShouldBe(401);
        });

        await Host.Scenario(s =>
        {
            s.Get.Url("/public/v1/vinculos");
            s.WithRequestHeader("X-Api-Key", t.ApiKey);
            IpAleatoria(s);
            s.StatusCodeShouldBe(401);
        });

        await PublicoAsync("GET", "/public/v1/vinculos", null, esperado: 401);
    }

    // ---------- textos legales y aceptación ----------

    [Fact]
    public async Task Aceptar_exige_los_textos_vigentes_y_queda_en_el_historial_con_actor_tercero()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");
        var sesion = await CanjearAsync(TokenDe(await Correos.EsperarAsync(correo)));
        var url = $"/public/v1/invitaciones/{vinculo.Id}";

        // Sin textos de la empresa: la vista lo indica y aceptar es 409.
        var sinTextos = await TerceroAsync<InvitacionPublicaDto>("GET", url, sesion.AccessToken, null, 200);
        sinTextos.PuedeAceptar.ShouldBeFalse();
        sinTextos.TextosFaltantes.ShouldBe(["AutorizacionEmpresa", "TerminosEmpresa"], ignoreOrder: true);
        var aviso = sinTextos.TextosPorAceptar.ShouldHaveSingleItem();
        aviso.Tipo.ShouldBe("AvisoPlataforma");
        aviso.Contenido.ShouldContain("inteligencia artificial");
        aviso.HashTexto.Length.ShouldBe(64);

        var faltan = await TerceroAsync("POST", $"{url}/aceptar", sesion.AccessToken, new AceptarInvitacionDto([]), 409);
        (await CodigoAsync(faltan)).ShouldBe("textos-legales-faltantes");

        // La empresa publica sus textos (con auditoría).
        await PublicarTextoAsync(t, "autorizacion", "Autorizo el tratamiento de mis datos por Norte S.A.S.");
        var terminos1 = await PublicarTextoAsync(t, "TerminosEmpresa", "Términos de Norte v1");
        terminos1.Version.ShouldBe(1);
        var textosEmpresa = await ApiAsync<TextosLegalesEmpresaDto>("GET", $"/api/v1/empresas/{t.Empresa.Id}/textos-legales", t.ApiKey, null, null, 200);
        textosEmpresa.Faltantes.ShouldBeEmpty();
        textosEmpresa.Vigentes.Count.ShouldBe(2);

        var conTextos = await TerceroAsync<InvitacionPublicaDto>("GET", url, sesion.AccessToken, null, 200);
        conTextos.PuedeAceptar.ShouldBeTrue();
        conTextos.TextosPorAceptar.Select(x => x.Tipo).ShouldBe(["AvisoPlataforma", "AutorizacionEmpresa", "TerminosEmpresa"]);

        // aceptado = false ⇒ 422.
        var rechazo = await TerceroAsync("POST", $"{url}/aceptar", sesion.AccessToken,
            Aceptar(conTextos.TextosPorAceptar, terminosAceptados: false), 422);
        (await CodigoAsync(rechazo)).ShouldBe("consentimiento-rechazado");

        // Términos cambian después de leerlos ⇒ 409 texto-desactualizado.
        var terminos2 = await PublicarTextoAsync(t, "terminos", "Términos de Norte v2");
        terminos2.Version.ShouldBe(2);
        var desactualizado = await TerceroAsync("POST", $"{url}/aceptar", sesion.AccessToken, Aceptar(conTextos.TextosPorAceptar), 409);
        (await CodigoAsync(desactualizado)).ShouldBe("texto-desactualizado");

        // Con las versiones vigentes ⇒ 200.
        var vigentes = await TerceroAsync<InvitacionPublicaDto>("GET", url, sesion.AccessToken, null, 200);
        var aceptada = await Host.Scenario(s =>
        {
            s.Post.Json(Aceptar(vigentes.TextosPorAceptar)).ToUrl($"{url}/aceptar");
            s.WithRequestHeader("Authorization", "Bearer " + sesion.AccessToken);
            s.WithRequestHeader("X-Canal", "Embed");
            s.WithRequestHeader("User-Agent", "PruebasValidasoft/1.0");
            IpAleatoria(s);
            s.StatusCodeShouldBe(200);
        });
        var resultado = (await aceptada.ReadAsJsonAsync<InvitacionAceptadaDto>())!;
        resultado.Vinculo.InvitacionAceptada.ShouldBeTrue();
        resultado.Aceptacion.Select(x => x.Tipo).ShouldBe(["AvisoPlataforma", "AutorizacionEmpresa", "TerminosEmpresa"]);
        var deTerminos = resultado.Aceptacion.Single(x => x.Tipo == "TerminosEmpresa");
        deTerminos.TextoVersion.ShouldBe(2);
        deTerminos.HashTexto.ShouldBe(terminos2.Hash);
        deTerminos.Canal.ShouldBe("Embed");
        deTerminos.UserAgent.ShouldBe("PruebasValidasoft/1.0");
        deTerminos.AceptadoPor.ShouldBe(new AceptadoPorDto(sesion.TerceroId, correo));

        // Repetir ⇒ 409; ya no hay textos por aceptar.
        var repetida = await TerceroAsync("POST", $"{url}/aceptar", sesion.AccessToken, Aceptar(vigentes.TextosPorAceptar), 409);
        (await CodigoAsync(repetida)).ShouldBe("invitacion-ya-aceptada");

        // La empresa ve los eventos con el tercero como actor.
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", t.ApiKey, t.Empresa.Id, null, 200);
        historial.Select(h => h.Tipo).ShouldBe(["vinculo_creado", "invitacion_enviada", "consentimientos_registrados", "invitacion_aceptada"]);
        foreach (var evento in historial.Skip(2))
        {
            evento.ActorTipo.ShouldBe("Tercero");
            evento.ActorId.ShouldBe(sesion.TerceroId.ToString());
        }

        var consentimientos = historial[2].Datos;
        consentimientos.GetProperty("evidencia").GetProperty("canal").GetString().ShouldBe("Embed");
        consentimientos.GetProperty("evidencia").GetProperty("otpVerificado").GetBoolean().ShouldBeFalse();
        consentimientos.GetProperty("consentimientos").GetArrayLength().ShouldBe(3);

        var leido = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", t.ApiKey, t.Empresa.Id, null, 200);
        leido.InvitacionAceptadaEn.ShouldNotBeNull();
    }

    [Fact]
    public async Task El_aviso_de_la_plataforma_lo_publica_solo_plataforma_y_crea_una_version_nueva()
    {
        var t = await TenantAsync("Norte S.A.S.");
        await ApiAsync<TextoLegalDto>("PUT", "/api/v1/plataforma/textos-legales/aviso", t.ApiKey, null, new PublicarTextoLegalDto("x"), 403);

        var vigente = await ApiAsync<TextoLegalDto>("GET", "/api/v1/plataforma/textos-legales/aviso", ApiFixture.ClavePlataforma, null, null, 200);
        var nuevo = await ApiAsync<TextoLegalDto>("PUT", "/api/v1/plataforma/textos-legales/aviso", ApiFixture.ClavePlataforma, null,
            new PublicarTextoLegalDto(vigente.Contenido + "\n\nRevisión de prueba."), 200);
        nuevo.Version.ShouldBe(vigente.Version + 1);
        nuevo.Ambito.ShouldBe("Plataforma");
        nuevo.Hash.ShouldNotBe(vigente.Hash);
    }

    // ---------- autorregistro ----------

    [Fact]
    public async Task El_autorregistro_responde_igual_para_un_tercero_nuevo_y_uno_existente()
    {
        // Nuevo: se crea y recibe un enlace sin vínculos.
        var correoNuevo = CorreoNuevo();
        var nuevo = await AutorregistroAsync(Nit(), correoNuevo);
        var sesion = await CanjearAsync(TokenDe(await Correos.EsperarAsync(correoNuevo)));
        sesion.Vinculos.ShouldBeEmpty();

        // Existente con el mismo correo de contacto: misma respuesta y su enlace.
        var t = await TenantAsync("Norte S.A.S.");
        var correoExistente = CorreoNuevo();
        var nit = Nit();
        await CrearVinculoAsync(t, nit, correoExistente, "CLIENTE");
        await Correos.EsperarAsync(correoExistente);
        var existente = await AutorregistroAsync(nit, correoExistente);
        var acceso = await Correos.EsperarAsync(correoExistente, m => m.Asunto.Contains("Tu enlace de acceso"));
        (await CanjearAsync(TokenDe(acceso))).Vinculos.ShouldHaveSingleItem();

        // Existente con un correo que no es de sus contactos: misma respuesta y ningún correo.
        var ajeno = CorreoNuevo();
        var sinCorreo = await AutorregistroAsync(nit, ajeno);

        nuevo.ShouldBe(existente);
        sinCorreo.ShouldBe(nuevo);
        await Task.Delay(1500);
        Correos.Para(ajeno).ShouldBeEmpty();
    }

    // ---------- límite de peticiones ----------

    [Fact]
    public async Task Las_rutas_publicas_limitan_las_peticiones_por_ip()
    {
        const string ip = "203.0.113.77";
        for (var i = 0; i < ApiFixture.LimitePublico; i++)
            await PublicoAsync("POST", "/public/v1/enlaces/no-existe/canjear", null, esperado: 404, ip: ip);

        var excedida = await PublicoAsync("POST", "/public/v1/enlaces/no-existe/canjear", null, esperado: 429, ip: ip);
        (await CodigoAsync(excedida)).ShouldBe("demasiadas-peticiones");

        await PublicoAsync("POST", "/public/v1/enlaces/no-existe/canjear", null, esperado: 404, ip: "203.0.113.78");
    }

    // ---------- utilidades ----------

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static string Nit() => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TokenDe(MensajeDeCorreo correo)
    {
        var coincidencia = RegexToken().Match(correo.Texto);
        coincidencia.Success.ShouldBeTrue($"El correo '{correo.Asunto}' no trae enlace.");
        return coincidencia.Groups[1].Value;
    }

    private static AceptarInvitacionDto Aceptar(IReadOnlyList<TextoLegalPublicoDto> textos, bool terminosAceptados = true) =>
        new([.. textos.Select(x => new ConsentimientoDto(x.Tipo, x.TextoVersion, terminosAceptados || x.Tipo != "TerminosEmpresa"))]);

    private async Task<Tenant> TenantAsync(string empresa)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Grupo " + empresa, "co", "America/Bogota"), 201);
        var clave = tenant.ApiKeyAdministrador.Valor!;
        var creada = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", clave, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), empresa, "EMP", null, null), 201);
        return new Tenant(clave, creada);
    }

    private Task<VinculoDto> CrearVinculoAsync(Tenant t, string nit, string correo, string tipo) =>
        ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", t.ApiKey, t.Empresa.Id, new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);

    private Task<TextoLegalDto> PublicarTextoAsync(Tenant t, string tipo, string contenido) =>
        ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{t.Empresa.Id}/textos-legales/{tipo}", t.ApiKey, null,
            new PublicarTextoLegalDto(contenido), 200);

    private async Task<SesionTerceroDto> CanjearAsync(string token)
    {
        var resultado = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, esperado: 200);
        var sesion = (await resultado.ReadAsJsonAsync<SesionTerceroDto>())!;
        sesion.AccessToken.ShouldNotBeNullOrWhiteSpace();
        sesion.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(25));
        return sesion;
    }

    private async Task<string> AutorregistroAsync(string nit, string correo)
    {
        var resultado = await PublicoAsync("POST", "/public/v1/autorregistro",
            new AutorregistroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Autorregistrada " + nit, correo, true), esperado: 202);
        return await resultado.ReadAsTextAsync();
    }

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    /// <summary>Cada petición pública sale de una IP distinta para no tropezar con el límite (salvo la prueba del límite).</summary>
    private static void IpAleatoria(Scenario s, string? ip = null)
    {
        var direccion = ip is null
            ? new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)])
            : IPAddress.Parse(ip);
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress = direccion);
    }

    private Task<IScenarioResult> PublicoAsync(string metodo, string url, object? cuerpo, int esperado, string? ip = null) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            IpAleatoria(s, ip);
            s.StatusCodeShouldBe(esperado);
        });

    private Task<IScenarioResult> TerceroAsync(string metodo, string url, string token, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("Authorization", "Bearer " + token);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> TerceroAsync<T>(string metodo, string url, string token, object? cuerpo, int esperado) =>
        (await (await TerceroAsync(metodo, url, token, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private async Task<T> ApiAsync<T>(string metodo, string url, string apiKey, Guid? empresa, object? cuerpo, int esperado)
    {
        var resultado = await Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", apiKey);
            if (empresa is not null)
                s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });
        return esperado is >= 200 and < 300 ? (await resultado.ReadAsJsonAsync<T>())! : default!;
    }

    private static void Preparar(Scenario s, string metodo, string url, object? cuerpo)
    {
        switch (metodo)
        {
            case "GET":
                s.Get.Url(url);
                break;
            case "PUT":
                s.Put.Json(cuerpo ?? new { }).ToUrl(url);
                break;
            case "POST" when cuerpo is null:
                s.Post.Url(url);
                break;
            default:
                s.Post.Json(cuerpo!).ToUrl(url);
                break;
        }
    }
}
