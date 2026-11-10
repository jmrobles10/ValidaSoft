using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Validasoft.Sdk;

/// <summary>Persona del sistema integrado en cuyo nombre se actúa (<c>sub</c> = su id en el sistema, ADR-0011).</summary>
public sealed record UsuarioDelegado(string Sub, string Nombre, string Correo);

/// <summary>
/// Firma la aserción <c>X-Usuario-Delegado</c> (ADR-0011, SE-4): JWT RS256 (clave RSA) o ES256 (clave EC P-256) con
/// <c>iss</c> = código del sistema, <c>aud</c> = <c>validasoft</c>, <c>sub</c>, <c>name</c>, <c>email</c>, un <c>jti</c> nuevo
/// por aserción y vencimiento de como mucho 5 minutos. Validasoft rechaza un <c>jti</c> repetido: se firma <b>una por
/// petición</b> (el cliente lo hace solo en cada intento). La clave pública correspondiente se registra en
/// <c>POST /api/v1/sistemas</c>.
/// </summary>
public sealed class FirmanteDeAsercionesDelegadas : IDisposable
{
    public const string Audiencia = "validasoft";

    /// <summary>Lo máximo que acepta Validasoft.</summary>
    public static readonly TimeSpan VigenciaMaxima = TimeSpan.FromMinutes(5);

    private readonly AsymmetricAlgorithm _clave;
    private readonly string _algoritmo;
    private readonly Func<DateTimeOffset> _reloj;

    /// <param name="codigoSistema">Código del sistema integrado (p. ej. <c>SRM</c>): va en <c>iss</c>.</param>
    /// <param name="clavePrivadaPem">Clave privada PKCS#8 o PKCS#1 (RSA ≥ 2048) o SEC1/PKCS#8 (EC P-256) en PEM.</param>
    /// <param name="reloj">Hora actual (para pruebas); por defecto la del sistema.</param>
    public FirmanteDeAsercionesDelegadas(string codigoSistema, string clavePrivadaPem, Func<DateTimeOffset>? reloj = null)
    {
        if (string.IsNullOrWhiteSpace(codigoSistema))
            throw new ArgumentException("Indica el código del sistema integrado.", nameof(codigoSistema));
        CodigoSistema = codigoSistema.Trim();
        _reloj = reloj ?? (() => DateTimeOffset.UtcNow);
        (_clave, _algoritmo) = Importar(clavePrivadaPem);
    }

    public string CodigoSistema { get; }

    /// <summary><c>RS256</c> o <c>ES256</c>, según la clave.</summary>
    public string Algoritmo => _algoritmo;

    /// <summary>Aserción nueva (con su propio <c>jti</c>) para una sola petición.</summary>
    public string Firmar(UsuarioDelegado usuario, TimeSpan? vigencia = null)
    {
        if (usuario is null)
            throw new ArgumentNullException(nameof(usuario));
        var duracion = vigencia ?? TimeSpan.FromMinutes(2);
        if (duracion <= TimeSpan.Zero || duracion > VigenciaMaxima)
            throw new ArgumentOutOfRangeException(nameof(vigencia), "La aserción debe vencer en 5 minutos o menos.");

        var ahora = _reloj().ToUnixTimeSeconds();
        var cabecera = new Dictionary<string, object> { ["alg"] = _algoritmo, ["typ"] = "JWT" };
        var carga = new Dictionary<string, object>
        {
            ["iss"] = CodigoSistema,
            ["aud"] = Audiencia,
            ["sub"] = usuario.Sub,
            ["name"] = usuario.Nombre,
            ["email"] = usuario.Correo,
            ["jti"] = Guid.NewGuid().ToString(),
            ["iat"] = ahora,
            ["nbf"] = ahora - 5,
            ["exp"] = ahora + (long)duracion.TotalSeconds
        };

        var firmado = Base64Url.Codificar(JsonSerializer.SerializeToUtf8Bytes(cabecera)) + "." +
                      Base64Url.Codificar(JsonSerializer.SerializeToUtf8Bytes(carga));
        var datos = Encoding.ASCII.GetBytes(firmado);
        var firma = _clave switch
        {
            RSA rsa => rsa.SignData(datos, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            // JWS (RFC 7518 §3.4) usa r||s de 64 bytes: es el formato IEEE P1363 que .NET produce por defecto.
            ECDsa ec => ec.SignData(datos, HashAlgorithmName.SHA256),
            _ => throw new InvalidOperationException("Clave no soportada.")
        };
        return firmado + "." + Base64Url.Codificar(firma);
    }

    public void Dispose() => _clave.Dispose();

    private static (AsymmetricAlgorithm Clave, string Algoritmo) Importar(string pem)
    {
        if (string.IsNullOrWhiteSpace(pem))
            throw new ArgumentException("Indica la clave privada en PEM.", nameof(pem));

        if (pem.Contains("EC PRIVATE KEY", StringComparison.Ordinal))
            return (ImportarEc(pem), "ES256");

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            if (rsa.KeySize < 2048)
                throw new ArgumentException("La clave RSA debe tener al menos 2048 bits.", nameof(pem));
            return (rsa, "RS256");
        }
        catch (CryptographicException)
        {
            rsa.Dispose();
            return (ImportarEc(pem), "ES256");
        }
    }

    private static ECDsa ImportarEc(string pem)
    {
        var ec = ECDsa.Create();
        ec.ImportFromPem(pem);
        if (ec.KeySize != 256)
        {
            ec.Dispose();
            throw new ArgumentException("La clave EC debe ser P-256.", nameof(pem));
        }

        return ec;
    }
}

/// <summary>Base64url sin relleno (RFC 4648 §5), disponible también en net7.0.</summary>
internal static class Base64Url
{
    public static string Codificar(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decodificar(string texto)
    {
        var base64 = texto.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
