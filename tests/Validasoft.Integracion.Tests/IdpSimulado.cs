using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Identidad;

namespace Validasoft.Integracion.Tests;

/// <summary>Cómo se equivoca el proveedor simulado en la siguiente respuesta (los negativos del SSO).</summary>
public enum FalloDelIdp
{
    Ninguno,
    NonceDistinto,
    AudienciaDistinta,
    EmisorDistinto,
    FirmaAjena,
    TokenVencido,
    CorreoNoVerificado,
    SinCorreo,
    ErrorEnLaAutorizacion,
    CanjeRechazado
}

/// <summary>Una persona del proveedor de identidad simulado: lo que declara su token.</summary>
public sealed record PerfilDelIdp(string Correo, string Nombre, string Sub, string[]? Grupos = null, string[]? Amr = null, string ReclamoDeGrupos = "groups");

/// <summary>
/// Proveedor de identidad simulado, en proceso, sobre Kestrel en <c>127.0.0.1</c> y un puerto libre (nunca un proveedor real, PI-3): publica el descubrimiento OIDC y
/// sus llaves (RS256), «autoriza» sin pantalla (redirige con el código al <c>redirect_uri</c>), canjea el código verificando PKCE y la credencial del cliente, y firma el
/// token de identidad con el correo, los grupos y los factores del perfil que se le dio. También imita los dos puntos de WorkOS (<c>/workos/sso/authorize</c> y
/// <c>/workos/sso/token</c>). <see cref="Fallo"/> hace que la siguiente respuesta salga mal, para probar lo que Validasoft debe rechazar.
/// </summary>
public sealed class IdpSimulado : IAsyncDisposable
{
    public const string ClientIdDeWorkOs = "client_workos_simulado";
    public const string ApiKeyDeWorkOs = "sk_workos_simulado_0000000000000001";

    private readonly RSA _clave = RSA.Create(2048);
    private readonly RSA _ajena = RSA.Create(2048);
    private readonly string _kid = "idp-simulado-1";
    private WebApplication _app = null!;
    private string _url = string.Empty;

    private sealed record Solicitud(string ClientId, string RedirectUri, string Nonce, string? Reto, string Correo, string? Organizacion, string? Conexion);

    private readonly ConcurrentDictionary<string, Solicitud> _codigos = new();

    /// <summary>Clientes registrados en el proveedor: client id → su secreto (nulo = cliente público, solo PKCE).</summary>
    public ConcurrentDictionary<string, string?> Clientes { get; } = new();

    /// <summary>Personas que pueden iniciar sesión: correo (minúsculas) → perfil.</summary>
    public ConcurrentDictionary<string, PerfilDelIdp> Perfiles { get; } = new();

    public FalloDelIdp Fallo { get; set; }

    /// <summary>Cuántas veces se canjeó un código (para comprobar que PKCE y el secreto viajaron).</summary>
    public ConcurrentQueue<string> Canjes { get; } = new();

    public string Url => _url;

    public string UrlDeWorkOs => _url + "/workos";

    public string Emisor => _url;

    public static async Task<IdpSimulado> IniciarAsync()
    {
        var idp = new IdpSimulado();
        await idp.ArrancarAsync();
        return idp;
    }

    /// <summary>Registra una persona y, si hace falta, el cliente de un grupo; devuelve el perfil.</summary>
    public PerfilDelIdp Persona(string correo, string nombre = "Persona de prueba", string[]? grupos = null, string[]? amr = null, string reclamo = "groups")
    {
        var perfil = new PerfilDelIdp(correo.ToLowerInvariant(), nombre, "sub-" + Guid.NewGuid().ToString("N")[..10], grupos, amr, reclamo);
        Perfiles[perfil.Correo] = perfil;
        return perfil;
    }

    public string RegistrarCliente(string? secreto = "secreto-del-cliente-0001")
    {
        var clientId = "cliente-" + Guid.NewGuid().ToString("N")[..10];
        Clientes[clientId] = secreto;
        return clientId;
    }

    private async Task ArrancarAsync()
    {
        var constructor = WebApplication.CreateSlimBuilder();
        constructor.WebHost.UseUrls("http://127.0.0.1:0");
        constructor.Logging.ClearProviders();
        _app = constructor.Build();

        _app.MapGet("/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer = _url,
            authorization_endpoint = _url + "/authorize",
            token_endpoint = _url + "/token",
            jwks_uri = _url + "/jwks",
            response_types_supported = new[] { "code" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_post", "client_secret_basic" },
            code_challenge_methods_supported = new[] { "S256" }
        }));

        // Un descubrimiento cuyo emisor no es el de su dirección: Validasoft no debe confiar en él.
        _app.MapGet("/emisor-malo/.well-known/openid-configuration", () => Results.Json(new
        {
            issuer = "https://otro-emisor.test",
            authorization_endpoint = _url + "/authorize",
            token_endpoint = _url + "/token",
            jwks_uri = _url + "/jwks"
        }));

        _app.MapGet("/jwks", () =>
        {
            var llave = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_clave.ExportParameters(false)) { KeyId = _kid });
            llave.Use = "sig";
            llave.Alg = "RS256";
            return Results.Json(new { keys = new[] { new { kty = llave.Kty, use = llave.Use, alg = llave.Alg, kid = llave.Kid, n = llave.N, e = llave.E } } });
        });

        _app.MapGet("/authorize", (HttpRequest peticion) => Autorizar(peticion, "client_id", "login_hint", null));
        _app.MapGet("/workos/sso/authorize", (HttpRequest peticion) => Autorizar(peticion, "client_id", "login_hint", "workos"));
        _app.MapPost("/token", async (HttpRequest peticion) => await CanjearAsync(peticion));
        _app.MapPost("/workos/sso/token", async (HttpRequest peticion) => await CanjearWorkOsAsync(peticion));

        await _app.StartAsync();
        _url = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First().TrimEnd('/');
    }

    private IResult Autorizar(HttpRequest peticion, string campoDelCliente, string campoDelCorreo, string? modo)
    {
        var consulta = peticion.Query;
        var redirect = consulta["redirect_uri"].ToString();
        var estado = consulta["state"].ToString();
        if (Fallo == FalloDelIdp.ErrorEnLaAutorizacion)
            return Results.Redirect($"{redirect}?error=access_denied&state={Uri.EscapeDataString(estado)}");

        var correo = consulta[campoDelCorreo].ToString().ToLowerInvariant();
        var codigo = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        _codigos[codigo] = new Solicitud(consulta[campoDelCliente].ToString(), redirect, consulta["nonce"].ToString(), consulta["code_challenge"].ToString() is { Length: > 0 } r ? r : null,
            correo, consulta["organization"].ToString() is { Length: > 0 } o ? o : null, consulta["connection"].ToString() is { Length: > 0 } c ? c : null);
        return Results.Redirect($"{redirect}?code={codigo}&state={Uri.EscapeDataString(estado)}");
    }

    private async Task<IResult> CanjearAsync(HttpRequest peticion)
    {
        var formulario = await peticion.ReadFormAsync();
        string clientId = formulario["client_id"].ToString(), secreto = formulario["client_secret"].ToString();
        var autorizacion = peticion.Headers.Authorization.ToString();
        if (autorizacion.StartsWith("Basic ", StringComparison.Ordinal))
        {
            var partes = Encoding.UTF8.GetString(Convert.FromBase64String(autorizacion[6..])).Split(':', 2);
            clientId = Uri.UnescapeDataString(partes[0]);
            secreto = Uri.UnescapeDataString(partes[1]);
        }

        if (Fallo == FalloDelIdp.CanjeRechazado || !Clientes.TryGetValue(clientId, out var esperado) || (esperado is not null && esperado != secreto))
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);
        if (!_codigos.TryRemove(formulario["code"].ToString(), out var solicitud) || solicitud.ClientId != clientId || solicitud.RedirectUri != formulario["redirect_uri"].ToString())
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);

        // PKCE: el verificador debe ser el del reto con el que se pidió el código.
        if (solicitud.Reto is not null)
        {
            var reto = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(formulario["code_verifier"].ToString())));
            if (reto != solicitud.Reto)
                return Results.Json(new { error = "invalid_grant", error_description = "PKCE" }, statusCode: 400);
        }

        Canjes.Enqueue($"{clientId}|{(string.IsNullOrEmpty(formulario["code_verifier"]) ? "sin-pkce" : "pkce")}|{(autorizacion.StartsWith("Basic ", StringComparison.Ordinal) ? "basic" : "post")}");
        if (!Perfiles.TryGetValue(solicitud.Correo, out var perfil))
            return Results.Json(new { error = "access_denied" }, statusCode: 403);

        return Results.Json(new { access_token = "acceso-simulado", token_type = "Bearer", expires_in = 3600, id_token = IdToken(perfil, clientId, solicitud.Nonce) });
    }

    private string IdToken(PerfilDelIdp perfil, string clientId, string nonce)
    {
        var ahora = DateTime.UtcNow;
        var reclamos = new Dictionary<string, object>
        {
            ["sub"] = perfil.Sub,
            ["name"] = perfil.Nombre,
            ["nonce"] = Fallo == FalloDelIdp.NonceDistinto ? "nonce-ajeno" : nonce
        };
        if (Fallo != FalloDelIdp.SinCorreo)
        {
            reclamos["email"] = perfil.Correo;
            reclamos["email_verified"] = Fallo != FalloDelIdp.CorreoNoVerificado;
        }

        if (perfil.Grupos is not null)
            reclamos[perfil.ReclamoDeGrupos] = perfil.Grupos;
        if (perfil.Amr is not null)
            reclamos["amr"] = perfil.Amr;

        var emisor = Fallo == FalloDelIdp.EmisorDistinto ? "https://otro-emisor.test" : _url;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emisor,
            Audience = Fallo == FalloDelIdp.AudienciaDistinta ? "otra-audiencia" : clientId,
            Claims = reclamos,
            IssuedAt = ahora.AddMinutes(Fallo == FalloDelIdp.TokenVencido ? -30 : 0),
            NotBefore = ahora.AddMinutes(Fallo == FalloDelIdp.TokenVencido ? -30 : 0),
            Expires = ahora.AddMinutes(Fallo == FalloDelIdp.TokenVencido ? -10 : 10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(Fallo == FalloDelIdp.FirmaAjena ? _ajena : _clave) { KeyId = _kid }, SecurityAlgorithms.RsaSha256)
        });
    }

    private async Task<IResult> CanjearWorkOsAsync(HttpRequest peticion)
    {
        var formulario = await peticion.ReadFormAsync();
        if (Fallo == FalloDelIdp.CanjeRechazado || formulario["client_id"] != ClientIdDeWorkOs || formulario["client_secret"] != ApiKeyDeWorkOs)
            return Results.Json(new { error = "invalid_client" }, statusCode: 401);
        if (!_codigos.TryRemove(formulario["code"].ToString(), out var solicitud) || solicitud.ClientId != ClientIdDeWorkOs)
            return Results.Json(new { error = "invalid_grant" }, statusCode: 400);
        if (!Perfiles.TryGetValue(solicitud.Correo, out var perfil))
            return Results.Json(new { error = "access_denied" }, statusCode: 403);

        var nombres = perfil.Nombre.Split(' ', 2);
        return Results.Json(new
        {
            access_token = "acceso-workos-simulado",
            profile = new
            {
                id = perfil.Sub,
                email = perfil.Correo,
                first_name = nombres[0],
                last_name = nombres.Length > 1 ? nombres[1] : null,
                organization_id = solicitud.Organizacion ?? "org_simulada",
                connection_id = solicitud.Conexion ?? "conn_simulada",
                groups = perfil.Grupos,
                raw_attributes = new Dictionary<string, object?> { [perfil.ReclamoDeGrupos] = perfil.Grupos }
            }
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
        _clave.Dispose();
        _ajena.Dispose();
    }
}

/// <summary>Comprobador de dominios de las pruebas: «publicó» el token quien está en <see cref="Publicados"/> (nunca se sale a la red).</summary>
public sealed class ComprobadorDeDominiosDePrueba : IComprobadorDeDominios
{
    public ConcurrentDictionary<string, string> Publicados { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Task<bool> ComprobarAsync(string dominio, string token, CancellationToken ct) =>
        Task.FromResult(Publicados.TryGetValue(dominio, out var publicado) && publicado == token);
}
