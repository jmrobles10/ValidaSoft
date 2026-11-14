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

    /// <summary>Otra sesión de la misma persona (o de un contacto del tercero) la cerró desde «Mis sesiones».</summary>
    public const string MotivoCierreIndividual = "cierre-individual";

    /// <summary>Los contactos del tercero rechazaron el acceso del correo de la sesión: se cortan todas las suyas (acceso aislado).</summary>
    public const string MotivoAccesoRechazado = "acceso-rechazado";

    /// <summary>Otro contacto del tercero se verificó y el correo de la sesión, sin verificar, pasó a acceso aislado: se cortan las suyas para que vuelva a entrar aislado.</summary>
    public const string MotivoContactoDesplazado = "contacto-desplazado";

    public const int LongitudMaximaDelDispositivo = 200;

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

    /// <summary><see cref="MotivoReuso"/>, <see cref="MotivoCierre"/>, <see cref="MotivoCierreIndividual"/> o <see cref="MotivoAccesoRechazado"/>.</summary>
    public string? MotivoRevocacion { get; set; }

    /// <summary>Navegador (<c>User-Agent</c>, recortado) con el que se abrió la sesión: para reconocerla en «Mis sesiones».</summary>
    public string? Dispositivo { get; set; }

    /// <summary>Dirección IP desde la que se abrió la sesión.</summary>
    public string? Ip { get; set; }

    /// <summary>Se usó por última vez (la apertura y cada renovación): lo que muestra «Mis sesiones».</summary>
    public DateTimeOffset? UltimoUsoEn { get; set; }

    /// <summary>¿Sirve en <paramref name="ahora"/>? No está revocada ni vencida (lo comprueba cada petición con su token de acceso).</summary>
    public bool VigenteEn(DateTimeOffset ahora) => !Revocada && ExpiraEn > ahora;

    public bool Revocada => RevocadaEn is not null;

    /// <summary>Abre la familia al canjear el enlace y devuelve el primer token (va solo en la cookie).</summary>
    public static (SesionDeRefresco Sesion, string Token) Abrir(Guid terceroId, string correo, bool otpVerificado, DateTimeOffset ahora,
        string? dispositivo = null, string? ip = null)
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
            ExpiraEn = ahora + VidaMaxima,
            Dispositivo = Recortar(dispositivo, LongitudMaximaDelDispositivo),
            Ip = Recortar(ip, 64),
            UltimoUsoEn = ahora
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
        UltimoUsoEn = ahora;
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

    private static string? Recortar(string? texto, int maximo)
    {
        var limpio = texto?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio.Length > maximo ? limpio[..maximo] : limpio;
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

    /// <summary>Códigos de ingreso directo por correo y hora (ING-1): más holgado que el enlace del tercero, que llega a su vínculo.</summary>
    public const int MaximoDeIngreso = 5;

    public string Id { get; set; } = string.Empty;

    /// <summary>Envíos dentro de la ventana (los más viejos se descartan al registrar uno nuevo).</summary>
    public List<DateTimeOffset> Envios { get; set; } = [];

    /// <summary>Con <paramref name="uso"/> el contador es otro (p. ej. <c>ingreso</c>): el mismo correo tiene un cupo por cada uso.</summary>
    public static string IdDe(string correo, string? uso = null) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes((uso is null ? string.Empty : uso + ":") + (correo ?? string.Empty).Trim().ToLowerInvariant())));

    public static LimiteDeAccesoPorCorreo Nuevo(string correo, string? uso = null) => new() { Id = IdDe(correo, uso) };

    /// <summary>
    /// Registra un envío si cabe en la ventana; <c>false</c> si ya se enviaron <paramref name="maximo"/> (por omisión
    /// <see cref="Maximo"/>) en la última hora.
    /// </summary>
    public bool IntentarRegistrar(DateTimeOffset ahora, int maximo = Maximo)
    {
        Envios.RemoveAll(e => e <= ahora - Ventana);
        if (Envios.Count >= maximo)
            return false;

        Envios.Add(ahora);
        return true;
    }
}
