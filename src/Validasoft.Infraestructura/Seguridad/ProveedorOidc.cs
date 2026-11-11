using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Cliente HTTP con el que Validasoft habla con el proveedor de identidad de un cliente (OIDC, WorkOS). Como el grupo escribe la dirección del proveedor,
/// la conexión se niega a salir hacia loopback, redes privadas o direcciones locales de enlace (anti-SSRF: p. ej. el servicio de metadatos de la nube) salvo
/// que <see cref="OpcionesDeSso.PermitirHttpLocal"/> (desarrollo y pruebas), y no sigue redirecciones.
/// </summary>
public static class ClienteSso
{
    public const string Nombre = "sso";

    public static SocketsHttpHandler Manejador(OpcionesDeSso opciones) => new()
    {
        AllowAutoRedirect = false,
        ConnectTimeout = TimeSpan.FromSeconds(8),
        ConnectCallback = async (contexto, ct) =>
        {
            var direcciones = await Dns.GetHostAddressesAsync(contexto.DnsEndPoint.Host, ct);
            var permitidas = opciones.PermitirHttpLocal ? direcciones : direcciones.Where(d => !EsDireccionInterna(d)).ToArray();
            if (permitidas.Length == 0)
                throw new HttpRequestException($"El proveedor de identidad resuelve a una dirección interna ({contexto.DnsEndPoint.Host}): no se conecta.");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(permitidas, contexto.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };

    /// <summary>Loopback, privadas (RFC 1918 y ULA), de enlace local (incluye 169.254.169.254) y sin especificar.</summary>
    public static bool EsDireccionInterna(IPAddress direccion)
    {
        if (direccion.IsIPv4MappedToIPv6)
            direccion = direccion.MapToIPv4();
        if (IPAddress.IsLoopback(direccion) || direccion.Equals(IPAddress.Any) || direccion.Equals(IPAddress.IPv6Any) || direccion.IsIPv6LinkLocal || direccion.IsIPv6SiteLocal)
            return true;

        var bytes = direccion.GetAddressBytes();
        if (direccion.AddressFamily == AddressFamily.InterNetworkV6)
            return (bytes[0] & 0xFE) == 0xFC; // fc00::/7
        return bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) || (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254) || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
    }

    public static bool EsDireccionPermitida(string url, bool permitirHttpLocal) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && permitirHttpLocal && uri.IsLoopback));
}

/// <summary>
/// Proveedor de identidad OIDC genérico (PI-3): Microsoft Entra ID (Azure AD), Google Workspace o cualquier proveedor que publique su descubrimiento
/// (<c>/.well-known/openid-configuration</c>). Flujo de código de autorización con PKCE (S256), <c>state</c> y <c>nonce</c>; el token de identidad se valida
/// (firma con las llaves del proveedor, emisor, audiencia, vigencia y nonce) y de él salen el correo, el nombre, los grupos y los factores usados (<c>amr</c>).
/// No sabe de grupos, roles ni sesiones de Validasoft: solo dice quién es la persona (ING-7).
/// </summary>
public sealed class ProveedorOidc(ConfiguracionesDeSso configuraciones, OpcionesDeSso opciones, IProtectorDeSecretos protector, IHttpClientFactory http,
    ILogger<ProveedorOidc> logger) : IProveedorDeIdentidad
{
    public const string Codigo = ProveedoresSso.Oidc;

    public const string MensajeDeRedireccion = "Te llevamos al inicio de sesión de tu empresa.";

    private static readonly JsonWebTokenHandler Manejador = new() { MapInboundClaims = false };

    private static readonly string[] Algoritmos =
    [
        SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512, SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.EcdsaSha384,
        SecurityAlgorithms.RsaSsaPssSha256
    ];

    /// <summary>Factores que, con otro distinto, o solos si el proveedor ya dice <c>mfa</c>, dan un segundo factor (RFC 8176).</summary>
    private static readonly HashSet<string> FactoresFuertes = new(StringComparer.OrdinalIgnoreCase)
        { "mfa", "otp", "hwk", "swk", "sms", "tel", "fpt", "face", "retina", "sc", "pop", "pin", "vbm" };

    private static readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> Descubrimientos = new(StringComparer.Ordinal);

    public string Id => Codigo;

    // ---------- iniciar ----------

    public async Task<InicioDeIngreso> IniciarAsync(IDocumentSession sesion, IMessageBus mensajes, string correo, bool conCuenta, ContextoDeCliente cliente,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var configuracion = await configuraciones.DeCorreoAsync(correo, ct);
        if (configuracion is not { Proveedor: Codigo, Autoridad: { } autoridad, ClientId: { } clientId })
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa no está disponible para ese correo.");

        var descubierto = await DescubrirAsync(autoridad, ct);
        var verificador = Aleatorio(48);
        var nonce = Aleatorio(24);
        var (desafio, estado, secretoDeNavegador) = DesafioDeIngreso.ParaSso(configuracion.Id, Codigo, correo, nonce, protector.Proteger(verificador),
            opciones.UrlDeRetorno, ahora, cliente.Ip, cliente.Dispositivo);
        sesion.Store(desafio);

        var parametros = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = opciones.UrlDeRetorno,
            ["scope"] = string.Join(' ', configuracion.Alcances),
            ["state"] = estado,
            ["nonce"] = nonce,
            ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verificador))),
            ["code_challenge_method"] = "S256",
            ["login_hint"] = correo
        };
        var url = QueryHelpers.AddQueryString(descubierto.AuthorizationEndpoint, parametros);
        return new InicioDeIngreso(desafio.Id, desafio.Expira, MensajeDeRedireccion, url, secretoDeNavegador);
    }

    // ---------- completar ----------

    public async Task<IdentidadVerificada?> CompletarAsync(DesafioDeIngreso desafio, string respuesta, DateTimeOffset ahora, CancellationToken ct)
    {
        var sso = desafio.Sso ?? throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión no es de SSO.");
        var configuracion = await configuraciones.DeGrupoAsync(sso.TenantId, ct);
        if (configuracion is not { Activo: true, Proveedor: Codigo, Autoridad: { } autoridad, ClientId: { } clientId })
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa dejó de estar disponible.");

        var descubierto = await DescubrirAsync(autoridad, ct);
        var idToken = await CanjearAsync(configuracion, clientId, descubierto, sso, respuesta, ct);
        var jwt = await ValidarAsync(idToken, descubierto, clientId, autoridad, ct);

        var carga = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload)).RootElement;
        if (!string.Equals(Texto(carga, "nonce"), sso.Nonce, StringComparison.Ordinal))
            throw ReglaDeNegocioException.NoAutenticado("sso-nonce-invalido", "La respuesta del proveedor no corresponde a este inicio de sesión. Empieza de nuevo.");

        var correo = new[] { Texto(carga, "email"), Texto(carga, "preferred_username"), Texto(carga, "upn") }
            .FirstOrDefault(c => c is { Length: > 3 } && c.Contains('@', StringComparison.Ordinal));
        if (correo is null)
            throw ReglaDeNegocioException.NoAutenticado("sso-sin-correo", "El proveedor de identidad no entregó el correo de la persona. Revisa los alcances (email, profile).");
        if (carga.TryGetProperty("email_verified", out var verificado) &&
            (verificado.ValueKind == JsonValueKind.False || (verificado.ValueKind == JsonValueKind.String && verificado.GetString() == "false")))
            throw ReglaDeNegocioException.NoAutenticado("sso-correo-no-verificado", "El proveedor de identidad no ha verificado ese correo.");

        var grupos = LeerLista(carga, configuracion.ReclamoDeGrupos, out var declarados);
        var amr = LeerLista(carga, "amr", out _);
        return new IdentidadVerificada(Codigo, correo, Texto(carga, "name"), EsMultifactor(amr), jwt.Subject, grupos, declarados);
    }

    public async Task<PruebaDeSsoDto> ProbarAsync(ConfiguracionDeSso configuracion, CancellationToken ct)
    {
        if (configuracion.Autoridad is not { Length: > 0 } autoridad)
            return new PruebaDeSsoDto(false, "Falta el emisor (issuer) del proveedor.", null, null);
        var descubierto = await DescubrirAsync(autoridad, ct);
        return new PruebaDeSsoDto(true, "El proveedor respondió y publica su descubrimiento con el emisor esperado.", descubierto.Issuer, descubierto.AuthorizationEndpoint);
    }

    // ---------- piezas ----------

    internal async Task<OpenIdConnectConfiguration> DescubrirAsync(string autoridad, CancellationToken ct)
    {
        var manager = Descubrimientos.GetOrAdd(autoridad, a =>
            new ConfigurationManager<OpenIdConnectConfiguration>($"{a.TrimEnd('/')}/.well-known/openid-configuration", new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(http.CreateClient(ClienteSso.Nombre)) { RequireHttps = !opciones.PermitirHttpLocal })
            {
                AutomaticRefreshInterval = TimeSpan.FromHours(6),
                RefreshInterval = TimeSpan.FromSeconds(30)
            });

        OpenIdConnectConfiguration descubierto;
        try
        {
            descubierto = await manager.GetConfigurationAsync(ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "No se pudo leer el descubrimiento OIDC de {Autoridad}", autoridad);
            throw ReglaDeNegocioException.NoDisponible("sso-proveedor-no-responde", "El proveedor de identidad de tu empresa no respondió. Intenta de nuevo en un momento.");
        }

        // El emisor que publica el descubrimiento debe ser el mismo con el que se configuró (RFC 8414 §3.3): evita tomar la identidad de otro proveedor.
        if (!string.Equals(descubierto.Issuer?.TrimEnd('/'), autoridad.TrimEnd('/'), StringComparison.Ordinal))
            throw ReglaDeNegocioException.NoDisponible("sso-emisor-inesperado", "El proveedor publica un emisor distinto al configurado. Revisa la dirección del emisor.");
        if (!ClienteSso.EsDireccionPermitida(descubierto.AuthorizationEndpoint ?? string.Empty, opciones.PermitirHttpLocal) ||
            !ClienteSso.EsDireccionPermitida(descubierto.TokenEndpoint ?? string.Empty, opciones.PermitirHttpLocal))
            throw ReglaDeNegocioException.NoDisponible("sso-endpoint-invalido", "El proveedor publica direcciones que no son https. Revisa su configuración.");
        return descubierto;
    }

    private async Task<string> CanjearAsync(ConfiguracionDeSso configuracion, string clientId, OpenIdConnectConfiguration descubierto, IngresoSso sso, string codigo,
        CancellationToken ct)
    {
        var campos = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = codigo,
            ["redirect_uri"] = sso.Retorno,
            ["code_verifier"] = protector.Desproteger(sso.VerificadorProtegido)
        };
        using var peticion = new HttpRequestMessage(HttpMethod.Post, descubierto.TokenEndpoint);
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (configuracion.LlaveConfigurada)
        {
            var secreto = protector.Desproteger(configuracion.SecretoProtegido!);
            var metodos = descubierto.TokenEndpointAuthMethodsSupported;
            if (metodos.Count > 0 && !metodos.Contains("client_secret_post") && metodos.Contains("client_secret_basic"))
                peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(secreto)}")));
            else
            {
                campos["client_id"] = clientId;
                campos["client_secret"] = secreto;
            }
        }
        else
            campos["client_id"] = clientId;
        peticion.Content = new FormUrlEncodedContent(campos);

        try
        {
            using var cliente = http.CreateClient(ClienteSso.Nombre);
            cliente.Timeout = TimeSpan.FromSeconds(15);
            using var respuesta = await cliente.SendAsync(peticion, ct);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(ct);
            if (!respuesta.IsSuccessStatusCode)
            {
                logger.LogWarning("El proveedor OIDC {Autoridad} rechazó el canje del código: {Estado}", configuracion.Autoridad, (int)respuesta.StatusCode);
                throw ReglaDeNegocioException.NoAutenticado("sso-canje-fallido", "El proveedor de identidad no aceptó el inicio de sesión. Empieza de nuevo.");
            }

            using var json = JsonDocument.Parse(cuerpo);
            return json.RootElement.TryGetProperty("id_token", out var token) && token.ValueKind == JsonValueKind.String && token.GetString() is { Length: > 0 } idToken
                ? idToken
                : throw ReglaDeNegocioException.NoAutenticado("sso-sin-token", "El proveedor de identidad no entregó el token de identidad (falta el alcance openid).");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "No se pudo canjear el código con {Autoridad}", configuracion.Autoridad);
            throw ReglaDeNegocioException.NoDisponible("sso-proveedor-no-responde", "El proveedor de identidad de tu empresa no respondió. Intenta de nuevo en un momento.");
        }
    }

    private static async Task<JsonWebToken> ValidarAsync(string idToken, OpenIdConnectConfiguration descubierto, string clientId, string autoridad, CancellationToken ct)
    {
        TokenValidationResult resultado = await Manejador.ValidateTokenAsync(idToken, Parametros(descubierto, clientId, autoridad));
        if (!resultado.IsValid && resultado.Exception is SecurityTokenSignatureKeyNotFoundException or SecurityTokenInvalidSignatureException &&
            Descubrimientos.TryGetValue(autoridad, out var manager))
        {
            // El proveedor pudo rotar sus llaves: se vuelve a leer su descubrimiento una vez y se reintenta.
            manager.RequestRefresh();
            var nuevo = await manager.GetConfigurationAsync(ct);
            resultado = await Manejador.ValidateTokenAsync(idToken, Parametros(nuevo, clientId, autoridad));
        }

        return resultado.IsValid && resultado.SecurityToken is JsonWebToken jwt
            ? jwt
            : throw ReglaDeNegocioException.NoAutenticado("sso-token-invalido", "El token de identidad no es válido (firma, emisor, audiencia o vigencia). Empieza de nuevo.");
    }

    private static TokenValidationParameters Parametros(OpenIdConnectConfiguration descubierto, string clientId, string autoridad) => new()
    {
        ValidIssuer = autoridad.TrimEnd('/'),
        ValidIssuers = [autoridad.TrimEnd('/'), autoridad.TrimEnd('/') + "/"],
        ValidAudience = clientId,
        IssuerSigningKeys = descubierto.SigningKeys,
        ValidAlgorithms = Algoritmos,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromMinutes(2)
    };

    // ---------- lectura de reclamos ----------

    internal static string? Texto(JsonElement carga, string nombre) =>
        carga.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    /// <summary>La lista (o el valor único) de un reclamo; <paramref name="declarado"/> dice si el reclamo venía en el token (aunque fuera vacío).</summary>
    internal static IReadOnlyList<string> LeerLista(JsonElement carga, string nombre, out bool declarado)
    {
        declarado = false;
        if (!carga.TryGetProperty(nombre, out var valor))
            return [];
        declarado = valor.ValueKind != JsonValueKind.Null;
        return valor.ValueKind switch
        {
            JsonValueKind.Array => [.. valor.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)],
            JsonValueKind.String => [valor.GetString()!],
            _ => []
        };
    }

    /// <summary>El proveedor declaró más de un factor: <c>mfa</c>, o dos factores distintos (p. ej. <c>pwd</c> y <c>otp</c>/<c>hwk</c>) en <c>amr</c> (RFC 8176).</summary>
    internal static bool EsMultifactor(IReadOnlyList<string> amr) =>
        amr.Any(m => string.Equals(m, "mfa", StringComparison.OrdinalIgnoreCase)) ||
        (amr.Any(m => FactoresFuertes.Contains(m)) && amr.Any(m => string.Equals(m, "pwd", StringComparison.OrdinalIgnoreCase) || string.Equals(m, "user", StringComparison.OrdinalIgnoreCase))) ||
        amr.Count(m => FactoresFuertes.Contains(m)) >= 2;

    private static string Aleatorio(int bytes) => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(bytes));
}
