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
