using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Reingreso del tercero (H2-reingreso): cookie de refresco <c>vs_tercero</c> al canjear, renovación con rotación,
/// detección de reuso, cierre, defensa CSRF por cabecera y acceso pidiendo un enlace con el correo.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class ReingresoApiTests(ApiFixture fixture)
{
    private const string Cookie = "vs_tercero";

    private IAlbaHost Host => fixture.Host;

    private CorreosCapturados Correos => fixture.Correos;

    private sealed record Tenant(string ApiKey, EmpresaDto Empresa);

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex RegexCodigo();

    // ---------- cookie, renovación, reuso y cierre ----------

    [Fact]
    public async Task Canjear_pone_la_cookie_y_renovar_rota_el_token_hasta_detectar_un_reuso()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");

        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{TokenDe(await Correos.EsperarAsync(correo))}/canjear", null, 200);
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
        var setCookie = SetCookie(canje);
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("samesite=lax", Case.Insensitive);
        setCookie.ShouldContain("path=/public/v1/sesion", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive); // la fixture usa CookieSegura = true, como producción
        setCookie.ShouldContain("expires=", Case.Insensitive);
        var primera = ValorCookie(canje);
        sesion.AccessToken.ShouldNotContain(primera); // el refresco nunca va en el cuerpo

        // Renovar: token de acceso nuevo que sirve y cookie rotada.
        var renovada = await RenovarAsync(primera, 200);
        var nueva = (await renovada.ReadAsJsonAsync<SesionTerceroDto>())!;
        nueva.AccessToken.ShouldNotBeNullOrWhiteSpace(); // puede coincidir con el anterior si se emite en el mismo segundo
        nueva.TerceroId.ShouldBe(sesion.TerceroId);
        nueva.OtpVerificado.ShouldBeFalse();
        nueva.Vinculos.ShouldHaveSingleItem().Id.ShouldBe(vinculo.Id);
        nueva.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(25));
        SetCookie(renovada).ShouldContain("httponly", Case.Insensitive);
        var segunda = ValorCookie(renovada);
        segunda.ShouldNotBe(primera);
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", nueva.AccessToken, 200)).ShouldHaveSingleItem();

        // Reusar la cookie anterior ⇒ 401 sesion-revocada y la familia entera muere (también la nueva).
        (await CodigoAsync(await RenovarAsync(primera, 401))).ShouldBe("sesion-revocada");
        (await CodigoAsync(await RenovarAsync(segunda, 401))).ShouldBe("sesion-revocada");
    }

    [Fact]
    public async Task Cerrar_revoca_la_sesion_y_borra_la_cookie()
    {
        var (_, cookie) = await SesionDeClienteAsync();

        var cierre = await Host.Scenario(s =>
        {
            s.Post.Url("/public/v1/sesion/cerrar");
            s.WithRequestHeader("X-Validasoft-Cliente", "public");
            s.WithRequestHeader("Cookie", $"{Cookie}={cookie}");
            IpAleatoria(s);
            s.StatusCodeShouldBe(204);
        });
        var borrada = SetCookie(cierre);
        borrada.ShouldStartWith($"{Cookie}=;");
        borrada.ShouldContain("path=/public/v1/sesion", Case.Insensitive);
        borrada.ShouldContain("expires=Thu, 01 Jan 1970", Case.Insensitive);

        (await CodigoAsync(await RenovarAsync(cookie, 401))).ShouldBe("sesion-revocada");

        // Cerrar sin cookie también es 204 (idempotente).
        await Host.Scenario(s =>
        {
            s.Post.Url("/public/v1/sesion/cerrar");
            s.WithRequestHeader("X-Validasoft-Cliente", "public");
            IpAleatoria(s);
            s.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task Renovar_exige_la_cabecera_del_cliente_y_una_cookie_vigente()
    {
        var (_, cookie) = await SesionDeClienteAsync();

        foreach (var ruta in new[] { "/public/v1/sesion/renovar", "/public/v1/sesion/cerrar" })
        {
            var sinCabecera = await Host.Scenario(s =>
            {
                s.Post.Url(ruta);
                s.WithRequestHeader("Cookie", $"{Cookie}={cookie}");
                IpAleatoria(s);
                s.StatusCodeShouldBe(403);
            });
            (await CodigoAsync(sinCabecera)).ShouldBe("cliente-invalido");
        }

        (await CodigoAsync(await RenovarAsync(null, 401))).ShouldBe("sesion-expirada");
        (await CodigoAsync(await RenovarAsync("basura", 401))).ShouldBe("sesion-expirada");
        (await CodigoAsync(await RenovarAsync($"{Guid.NewGuid():N}.{new string('a', 43)}", 401))).ShouldBe("sesion-expirada");

        // La cookie sigue sirviendo: los intentos sin cabecera no la tocaron.
        await RenovarAsync(cookie, 200);
    }

    [Fact]
    public async Task Dos_renovaciones_simultaneas_con_la_misma_cookie_no_dejan_dos_tokens_validos()
    {
        var (_, cookie) = await SesionDeClienteAsync();

        var resultados = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Host.Scenario(s =>
        {
            s.Post.Url("/public/v1/sesion/renovar");
            s.WithRequestHeader("X-Validasoft-Cliente", "public");
            s.WithRequestHeader("Cookie", $"{Cookie}={cookie}");
            IpAleatoria(s);
            s.IgnoreStatusCode();
        })));

        var codigos = resultados.Select(r => r.Context.Response.StatusCode).ToList();
        codigos.Count(c => c == 200).ShouldBeLessThanOrEqualTo(1);
        codigos.ShouldAllBe(c => c == 200 || c == 401 || c == 409);
    }

    [Fact]
    public async Task La_renovacion_conserva_el_otp_y_recalcula_los_vinculos()
    {
        var a = await TenantAsync("Norte S.A.S.");
        var b = await TenantAsync("Sur Global S.A.S.");
        var correo = CorreoNuevo();
        var nit = Nit();
        var deA = await CrearVinculoAsync(a, nit, correo, "PROVEEDOR");
        var token = TokenDe(await Correos.EsperarAsync(correo));

        await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, 202);
        var codigo = RegexCodigo().Match((await Correos.EsperarAsync(correo, m => m.Asunto.Contains("código"))).Texto).Groups[1].Value;
        var verificada = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo), 200);
        (await verificada.ReadAsJsonAsync<SesionTerceroDto>())!.Vinculos.ShouldHaveSingleItem();
        var cookie = ValorCookie(verificada);

        // Un vínculo nuevo (otro tenant, mismo correo) entra en la siguiente renovación.
        var deB = await CrearVinculoAsync(b, nit, correo, "CLIENTE");
        var renovada = (await (await RenovarAsync(cookie, 200)).ReadAsJsonAsync<SesionTerceroDto>())!;
        renovada.OtpVerificado.ShouldBeTrue();
        renovada.Vinculos.Select(v => v.Id).ShouldBe([deA.Id, deB.Id], ignoreOrder: true);
    }

    // ---------- acceso con el correo ----------

    [Fact]
    public async Task Pedir_acceso_responde_igual_y_el_enlace_abre_los_vinculos_de_dos_tenants()
    {
        var a = await TenantAsync("Norte S.A.S.");
        var b = await TenantAsync("Sur Global S.A.S.");
        var correo = CorreoNuevo();
        var nit = Nit();
        var deA = await CrearVinculoAsync(a, nit, correo, "CLIENTE");
        var deB = await CrearVinculoAsync(b, nit, correo, "CLIENTE");
        await Correos.EsperarAsync(correo, cuantos: 2); // las dos invitaciones

        var existente = await AccesoAsync(correo.ToUpperInvariant());
        var inexistente = await AccesoAsync(CorreoNuevo());
        existente.ShouldBe(inexistente);
        JsonDocument.Parse(existente).RootElement.GetProperty("mensaje").GetString()
            .ShouldBe("Si tu correo está registrado, te enviamos un enlace para entrar.");

        var acceso = await Correos.EsperarAsync(correo, m => m.Asunto == "Tu enlace para entrar a Validasoft");
        acceso.Texto.ShouldContain($"{ApiFixture.UrlPublica}/e/");
        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{TokenDe(acceso)}/canjear", null, 200);
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
        sesion.Vinculos.Select(v => v.Id).ShouldBe([deA.Id, deB.Id], ignoreOrder: true);
        ValorCookie(canje).ShouldNotBeNullOrWhiteSpace();
        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, 200);
        vinculos.Select(v => v.Empresa.Nombre).ShouldBe(["Norte S.A.S.", "Sur Global S.A.S."], ignoreOrder: true);
    }

    [Fact]
    public async Task Pedir_acceso_envia_como_maximo_tres_enlaces_por_hora_y_exige_otp_si_algun_tipo_lo_pide()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        await CrearVinculoAsync(t, Nit(), correo, "PROVEEDOR");
        await Correos.EsperarAsync(correo);

        var respuestas = new List<string>();
        for (var i = 0; i < 4; i++)
            respuestas.Add(await AccesoAsync(correo));
        respuestas.Distinct().ShouldHaveSingleItem();

        await Correos.EsperarAsync(correo, EsDeAcceso, cuantos: 3);
        await Task.Delay(1500);
        var enviados = Correos.Para(correo).Where(EsDeAcceso).ToList();
        enviados.Count.ShouldBe(3);

        // El tipo PROVEEDOR exige OTP: el enlace pedido con el correo también.
        await PublicoAsync("POST", $"/public/v1/enlaces/{TokenDe(enviados[0])}/canjear", null, 202);
    }

    [Fact]
    public async Task Pedir_acceso_no_envia_a_vinculos_terminados_ni_a_correos_invalidos()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");
        await Correos.EsperarAsync(correo);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/anular", t.ApiKey, t.Empresa.Id, new MotivoDto("Prueba de reingreso"), 200);

        await AccesoAsync(correo);
        await Task.Delay(1500);
        Correos.Para(correo).Where(EsDeAcceso).ShouldBeEmpty();

        var invalido = await PublicoAsync("POST", "/public/v1/acceso", new SolicitarAccesoDto("sin-arroba"), 422);
        (await CodigoAsync(invalido)).ShouldBe("correo-invalido");
    }

    // ---------- utilidades ----------

    private static bool EsDeAcceso(MensajeDeCorreo m) => m.Asunto == "Tu enlace para entrar a Validasoft";

    /// <summary>Tercero con un vínculo CLIENTE (sin OTP): canjea y devuelve la sesión y la cookie de refresco.</summary>
    private async Task<(SesionTerceroDto Sesion, string Cookie)> SesionDeClienteAsync()
    {
        var t = await TenantAsync("Norte S.A.S.");
        var correo = CorreoNuevo();
        await CrearVinculoAsync(t, Nit(), correo, "CLIENTE");
        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{TokenDe(await Correos.EsperarAsync(correo))}/canjear", null, 200);
        return ((await canje.ReadAsJsonAsync<SesionTerceroDto>())!, ValorCookie(canje));
    }

    private Task<IScenarioResult> RenovarAsync(string? cookie, int esperado) =>
        Host.Scenario(s =>
        {
            s.Post.Url("/public/v1/sesion/renovar");
            s.WithRequestHeader("X-Validasoft-Cliente", "public");
            if (cookie is not null)
                s.WithRequestHeader("Cookie", $"{Cookie}={cookie}");
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<string> AccesoAsync(string correo) =>
        await (await PublicoAsync("POST", "/public/v1/acceso", new SolicitarAccesoDto(correo), 202)).ReadAsTextAsync();

    private static string SetCookie(IScenarioResult resultado) =>
        resultado.Context.Response.Headers.SetCookie.Select(v => v ?? string.Empty).SingleOrDefault(v => v.StartsWith(Cookie + "=", StringComparison.Ordinal))
        ?? throw new InvalidOperationException("La respuesta no trae Set-Cookie vs_tercero.");

    private static string ValorCookie(IScenarioResult resultado)
    {
        var valor = SetCookie(resultado)[(Cookie.Length + 1)..].Split(';')[0];
        valor.ShouldNotBeNullOrWhiteSpace();
        return valor;
    }

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static string Nit() => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string TokenDe(MensajeDeCorreo correo)
    {
        var coincidencia = RegexToken().Match(correo.Texto);
        coincidencia.Success.ShouldBeTrue($"El correo '{correo.Asunto}' no trae enlace.");
        return coincidencia.Groups[1].Value;
    }

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

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private static void IpAleatoria(Scenario s) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
            new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    private Task<IScenarioResult> PublicoAsync(string metodo, string url, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> TerceroAsync<T>(string metodo, string url, string token, int esperado) =>
        (await (await Host.Scenario(s =>
        {
            Preparar(s, metodo, url, null);
            s.WithRequestHeader("Authorization", "Bearer " + token);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        })).ReadAsJsonAsync<T>())!;

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
            case "POST" when cuerpo is null:
                s.Post.Url(url);
                break;
            default:
                s.Post.Json(cuerpo!).ToUrl(url);
                break;
        }
    }
}
