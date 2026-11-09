using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>Resultado de presentar un token de refresco: el token nuevo, o el reuso de uno ya rotado (familia revocada).</summary>
public sealed record ResultadoRenovacion(bool Renovada, string? Token);

/// <summary>
/// Sesión de refresco del tercero (docs/07 §2.1): una <b>familia</b> de tokens opacos que se rotan en cada renovación.
/// Global (esquema <c>identidad</c>); solo se guarda el hash SHA-256 del token vigente. El token tiene la forma
/// <c>{familia:N}.{secreto}</c> (secreto de 256 bits en base64url): la familia permite cargar el documento y el secreto
/// lo autentica. Presentar un token que no es el vigente ⇒ reuso ⇒ se revoca toda la familia (RE-3). La vida es
/// deslizante (cada renovación entrega un token nuevo) con un máximo absoluto de <see cref="VidaMaxima"/> desde el canje.
/// </summary>
public sealed class SesionDeRefresco
{
    public static readonly TimeSpan VidaMaxima = TimeSpan.FromHours(8);

    public const string MotivoReuso = "reuso";
    public const string MotivoCierre = "cierre";

    /// <summary>Identificador de la familia (viaja dentro del token).</summary>
    public Guid Id { get; set; }

    /// <summary>SHA-256 hexadecimal (minúsculas) del token vigente.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public Guid TerceroId { get; set; }

    /// <summary>Correo con el que se abrió la sesión: decide qué vínculos entran en cada access token.</summary>
    public string Correo { get; set; } = string.Empty;

    /// <summary>La sesión se abrió verificando el OTP; se conserva en cada renovación.</summary>
    public bool OtpVerificado { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    /// <summary>Vencimiento absoluto: <see cref="CreadaEn"/> + 8 h; ninguna renovación lo extiende.</summary>
    public DateTimeOffset ExpiraEn { get; set; }

    public DateTimeOffset? RotadaEn { get; set; }

    public int Rotaciones { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    /// <summary><see cref="MotivoReuso"/> o <see cref="MotivoCierre"/>.</summary>
    public string? MotivoRevocacion { get; set; }

    public bool Revocada => RevocadaEn is not null;

    /// <summary>Abre la familia al canjear el enlace y devuelve el primer token (va solo en la cookie).</summary>
    public static (SesionDeRefresco Sesion, string Token) Abrir(Guid terceroId, string correo, bool otpVerificado, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(correo))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "La sesión necesita el correo con el que se abrió.");

        var sesion = new SesionDeRefresco
        {
            Id = Guid.NewGuid(),
            TerceroId = terceroId,
            Correo = correo.Trim().ToLowerInvariant(),
            OtpVerificado = otpVerificado,
            CreadaEn = ahora,
            ExpiraEn = ahora + VidaMaxima
        };
        return (sesion, sesion.NuevoToken());
    }

    /// <summary>La familia que dice el token, o nulo si no tiene la forma esperada (no se consulta nada).</summary>
    public static Guid? FamiliaDe(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
            return null;

        var punto = token.IndexOf('.', StringComparison.Ordinal);
        return punto == 32 && Guid.TryParseExact(token[..punto], "N", out var familia) && token.Length > punto + 1 ? familia : null;
    }

    public static string HashDeToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));

    /// <summary>
    /// Con el token vigente rota (devuelve el siguiente y el anterior deja de servir). Con cualquier otro token de la
    /// familia revoca la familia y devuelve <c>Renovada = false</c>: el llamador debe guardar antes de responder.
    /// Revocada ⇒ 401 <c>sesion-revocada</c>; vencida ⇒ 401 <c>sesion-expirada</c>.
    /// </summary>
    public ResultadoRenovacion Renovar(string token, DateTimeOffset ahora)
    {
        if (Revocada)
            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "La sesión se cerró. Pide un enlace nuevo para entrar.");

        if (ExpiraEn <= ahora)
            throw ReglaDeNegocioException.NoAutenticado("sesion-expirada", "La sesión venció. Pide un enlace nuevo para entrar.");

        var esperado = Encoding.ASCII.GetBytes(TokenHash);
        var recibido = Encoding.ASCII.GetBytes(HashDeToken(token));
        if (!CryptographicOperations.FixedTimeEquals(esperado, recibido))
        {
            Revocar(MotivoReuso, ahora);
            return new ResultadoRenovacion(false, null);
        }

        RotadaEn = ahora;
        Rotaciones++;
        return new ResultadoRenovacion(true, NuevoToken());
    }

    /// <summary>Revoca la familia (cierre de sesión o reuso); repetir no cambia la primera revocación.</summary>
    public void Revocar(string motivo, DateTimeOffset ahora)
    {
        if (Revocada)
            return;

        RevocadaEn = ahora;
        MotivoRevocacion = motivo;
    }

    private string NuevoToken()
    {
        var token = $"{Id:N}.{Base64Url(RandomNumberGenerator.GetBytes(32))}";
        TokenHash = HashDeToken(token);
        return token;
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// Límite de enlaces de acceso pedidos por correo (RE-2): <see cref="Maximo"/> envíos por <see cref="Ventana"/> para el
/// mismo correo, además del límite por IP de <c>/public/v1</c>. Global (esquema <c>identidad</c>); el <see cref="Id"/>
/// es el SHA-256 del correo normalizado (no guarda el correo).
/// </summary>
public sealed class LimiteDeAccesoPorCorreo
{
    public const int Maximo = 3;
    public static readonly TimeSpan Ventana = TimeSpan.FromHours(1);

    public string Id { get; set; } = string.Empty;

    /// <summary>Envíos dentro de la ventana (los más viejos se descartan al registrar uno nuevo).</summary>
    public List<DateTimeOffset> Envios { get; set; } = [];

    public static string IdDe(string correo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes((correo ?? string.Empty).Trim().ToLowerInvariant())));

    public static LimiteDeAccesoPorCorreo Nuevo(string correo) => new() { Id = IdDe(correo) };

    /// <summary>Registra un envío si cabe en la ventana; <c>false</c> si ya se enviaron <see cref="Maximo"/> en la última hora.</summary>
    public bool IntentarRegistrar(DateTimeOffset ahora)
    {
        Envios.RemoveAll(e => e <= ahora - Ventana);
        if (Envios.Count >= Maximo)
            return false;

        Envios.Add(ahora);
        return true;
    }
}
