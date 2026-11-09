using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>
/// Sistema que integra a Validasoft (SRM, Marco, Portal, sistemas del cliente) — ADR-0011. Validasoft solo guarda
/// su <b>clave pública</b> para verificar las aserciones de identidad delegada; nunca un secreto que permita
/// suplantarlo. Esquema <c>identidad</c>, multi-tenant.
/// </summary>
public sealed class SistemaIntegrado
{
    public Guid Id { get; set; }

    /// <summary>Mayúsculas, único en el tenant; es el <c>iss</c> de las aserciones y el sistema de las referencias externas.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>SPKI en PEM (<c>-----BEGIN PUBLIC KEY-----</c>): RSA ≥ 2048 bits o EC P-256.</summary>
    public string ClavePublicaPem { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>
    /// Orígenes web (<c>https://host[:puerto]</c>) desde los que el sistema aloja las pantallas embebidas de Validasoft:
    /// entran en <c>frame-ancestors</c> y en la lista con la que la app embebida acepta <c>postMessage</c> (SE-1).
    /// </summary>
    public List<string> OrigenesPermitidos { get; set; } = [];

    public static SistemaIntegrado Crear(string codigo, string nombre, string clavePublicaPem, DateTimeOffset ahora)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNormalizado, "^[A-Z][A-Z0-9_]{1,19}$"))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código del sistema debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 20).");

        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del sistema es obligatorio.");

        return new SistemaIntegrado
        {
            Id = Guid.CreateVersion7(),
            Codigo = codigoNormalizado,
            Nombre = nombre.Trim(),
            ClavePublicaPem = ClavePublica.Validar(clavePublicaPem),
            CreadoEn = ahora
        };
    }

    /// <summary>Rotación: las aserciones firmadas con la clave anterior dejan de valer de inmediato.</summary>
    public void RotarClavePublica(string clavePublicaPem)
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("sistema-inactivo", "El sistema está desactivado.");
        ClavePublicaPem = ClavePublica.Validar(clavePublicaPem);
    }

    public void Desactivar()
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("sistema-inactivo", "El sistema ya está desactivado.");
        Activo = false;
    }

    /// <summary>
    /// Reemplaza los orígenes que pueden alojar las pantallas embebidas. Cada uno se valida y normaliza con
    /// <see cref="OrigenWeb.Normalizar"/>; los repetidos se descartan. Lista vacía = nadie puede embeber (salvo Validasoft).
    /// </summary>
    public void FijarOrigenes(IEnumerable<string>? origenes, bool permitirHttpLocal)
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("sistema-inactivo", "El sistema está desactivado.");

        var normalizados = (origenes ?? []).Select(o => OrigenWeb.Normalizar(o, permitirHttpLocal)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizados.Count > OrigenWeb.MaximoPorSistema)
            throw ReglaDeNegocioException.Validacion("origenes-excesivos",
                $"Un sistema admite hasta {OrigenWeb.MaximoPorSistema} orígenes.");
        OrigenesPermitidos = normalizados;
    }
}

/// <summary>
/// Origen web en la forma que el navegador reporta en <c>MessageEvent.origin</c> y que entiende
/// <c>frame-ancestors</c>: <c>esquema://host[:puerto]</c>, sin ruta, consulta, fragmento, usuario ni comodines.
/// </summary>
public static class OrigenWeb
{
    public const int MaximoPorSistema = 20;

    private static readonly string[] HostsLocales = ["localhost", "127.0.0.1", "[::1]"];

    /// <summary>
    /// Devuelve el origen normalizado (esquema y host en minúsculas, host IDN en punycode, sin el puerto por defecto) o
    /// lanza <c>origen-invalido</c>. Solo <c>https</c>; <c>http</c> únicamente hacia <c>localhost</c>/<c>127.0.0.1</c>/
    /// <c>[::1]</c> (cualquier puerto) y solo si <paramref name="permitirHttpLocal"/> (desarrollo).
    /// </summary>
    public static string Normalizar(string? origen, bool permitirHttpLocal)
    {
        var texto = (origen ?? string.Empty).Trim();
        if (texto.EndsWith('/'))
            texto = texto[..^1];

        if (texto.Length is 0 or > 200 || texto.Contains('*') ||
            !Uri.TryCreate(texto, UriKind.Absolute, out var uri) ||
            uri.UserInfo.Length > 0 || uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            throw Invalido(origen);

        var host = uri.HostNameType == UriHostNameType.Dns ? uri.IdnHost.ToLowerInvariant() : uri.Host.ToLowerInvariant();
        var local = HostsLocales.Contains(host);
        var esquemaValido = uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && local && permitirHttpLocal);
        if (!esquemaValido)
            throw ReglaDeNegocioException.Validacion("origen-invalido",
                $"El origen «{origen}» debe usar https (http solo se admite hacia localhost en desarrollo).");

        return uri.IsDefaultPort ? $"{uri.Scheme}://{host}" : $"{uri.Scheme}://{host}:{uri.Port}";
    }

    private static ReglaDeNegocioException Invalido(string? origen) =>
        ReglaDeNegocioException.Validacion("origen-invalido",
            $"«{origen}» no es un origen válido: usa https://host[:puerto], sin ruta, consulta, comodines ni usuario.");
}

/// <summary>Validación de claves públicas para firmas RS256 / ES256.</summary>
public static class ClavePublica
{
    private const string OidP256 = "1.2.840.10045.3.1.7";

    /// <summary>Devuelve el PEM normalizado o lanza <c>clave-publica-invalida</c>.</summary>
    public static string Validar(string? pem)
    {
        var texto = (pem ?? string.Empty).Trim();
        if (!PemEncoding.TryFind(texto, out var campos) || texto[campos.Label] is not ("PUBLIC KEY" or "RSA PUBLIC KEY"))
            throw Invalida("Envía la clave pública en PEM (-----BEGIN PUBLIC KEY-----); nunca la privada.");

        if (TryRsa(texto, out var bitsRsa))
            return bitsRsa >= 2048 ? texto : throw Invalida($"La clave RSA tiene {bitsRsa} bits; el mínimo es 2048.");

        if (TryEc(texto, out var esP256))
            return esP256 ? texto : throw Invalida("La clave EC debe usar la curva P-256 (ES256).");

        throw Invalida("La clave no es RSA ni EC.");
    }

    private static bool TryRsa(string pem, out int bits)
    {
        bits = 0;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            bits = rsa.KeySize;
            return true;
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static bool TryEc(string pem, out bool esP256)
    {
        esP256 = false;
        try
        {
            using var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            var curva = ec.ExportParameters(false).Curve;
            esP256 = curva.Oid.Value == OidP256 || curva.Oid.FriendlyName is "nistP256" or "ECDSA_P256" or "secp256r1";
            return true;
        }
        catch (Exception e) when (e is CryptographicException or ArgumentException)
        {
            return false;
        }
    }

    private static ReglaDeNegocioException Invalida(string mensaje) =>
        ReglaDeNegocioException.Validacion("clave-publica-invalida", mensaje);
}
