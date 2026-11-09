using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Validasoft.Dominio.Certificados;

namespace Validasoft.Aplicacion.Certificados;

/// <summary>
/// Ed25519 (RFC 8032) con BouncyCastle: .NET 10 no expone Ed25519 en <c>System.Security.Cryptography</c> y la librería ya
/// llegaba transitiva por MailKit (MIT, mantenida). La clave privada es la semilla de 32 bytes.
/// </summary>
public static class SelloEd25519
{
    public const string Algoritmo = "Ed25519";

    public static byte[] GenerarSemilla() => RandomNumberGenerator.GetBytes(32);

    public static byte[] ClavePublica(byte[] semilla) => new Ed25519PrivateKeyParameters(semilla, 0).GeneratePublicKey().GetEncoded();

    public static byte[] Firmar(byte[] semilla, byte[] mensaje)
    {
        var firmador = new Ed25519Signer();
        firmador.Init(true, new Ed25519PrivateKeyParameters(semilla, 0));
        firmador.BlockUpdate(mensaje, 0, mensaje.Length);
        return firmador.GenerateSignature();
    }

    public static bool Verificar(byte[] clavePublica, byte[] mensaje, byte[] firma)
    {
        if (clavePublica.Length != 32 || firma.Length != 64)
            return false;
        var verificador = new Ed25519Signer();
        verificador.Init(false, new Ed25519PublicKeyParameters(clavePublica, 0));
        verificador.BlockUpdate(mensaje, 0, mensaje.Length);
        return verificador.VerifySignature(firma);
    }

    /// <summary><c>kid</c> por defecto: <c>vs-</c> + los primeros 16 hexadecimales del SHA-256 de la clave pública.</summary>
    public static string KidPara(byte[] clavePublica) => "vs-" + Convert.ToHexStringLower(SHA256.HashData(clavePublica))[..16];
}

/// <summary>Clave pública publicada en <c>.well-known</c>: la vigente o una anterior (para verificar emisiones viejas).</summary>
public sealed record ClavePublicada(string Kid, byte[] ClavePublica, bool Vigente);

/// <summary>
/// Claves de sellado (<c>Validasoft:Certificados</c>): <c>ClavePrivada</c> = semilla Ed25519 de 32 bytes en base64 (secreto de
/// configuración: <c>VALIDASOFT__CERTIFICADOS__CLAVEPRIVADA</c>, Key Vault…), <c>Kid</c> opcional (por defecto se deriva de la
/// clave pública) y <c>ClavesAnteriores[] {Kid, ClavePublica}</c> para seguir publicando las retiradas tras una rotación
/// (CE-4). Se lee al primer uso: sin clave solo falla la emisión. Registrada por tipo (sin fábricas lambda).
/// </summary>
public sealed class ClavesDeCertificados(IConfiguration configuracion) : ISelladorDeCertificados
{
    private readonly Lazy<(string Kid, byte[] Semilla, byte[] Publica)> _vigente = new(() => Cargar(configuracion));

    public string Kid => _vigente.Value.Kid;

    public byte[] Firmar(byte[] mensaje) => SelloEd25519.Firmar(_vigente.Value.Semilla, mensaje);

    /// <summary>La vigente primero y luego las anteriores configuradas (sin repetir <c>kid</c>).</summary>
    public IReadOnlyList<ClavePublicada> Publicadas()
    {
        var lista = new List<ClavePublicada> { new(_vigente.Value.Kid, _vigente.Value.Publica, true) };
        foreach (var anterior in configuracion.GetSection("Validasoft:Certificados:ClavesAnteriores").GetChildren())
        {
            var kid = anterior["Kid"];
            var publica = anterior["ClavePublica"];
            if (string.IsNullOrWhiteSpace(kid) || string.IsNullOrWhiteSpace(publica) || lista.Any(c => c.Kid == kid))
                continue;
            var bytes = Convert.FromBase64String(publica);
            if (bytes.Length == 32)
                lista.Add(new ClavePublicada(kid, bytes, false));
        }

        return lista;
    }

    /// <summary>Clave pública de un <c>kid</c> publicado; nula si no se conoce.</summary>
    public byte[]? ClavePublica(string kid) => Publicadas().FirstOrDefault(c => c.Kid == kid)?.ClavePublica;

    private static (string, byte[], byte[]) Cargar(IConfiguration configuracion)
    {
        var valor = configuracion["Validasoft:Certificados:ClavePrivada"];
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException(
                "Falta Validasoft:Certificados:ClavePrivada (semilla Ed25519 de 32 bytes en base64) para sellar los certificados.");

        byte[] semilla;
        try
        {
            semilla = Convert.FromBase64String(valor.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Validasoft:Certificados:ClavePrivada no es base64.");
        }

        if (semilla.Length != 32)
            throw new InvalidOperationException("Validasoft:Certificados:ClavePrivada debe tener exactamente 32 bytes (semilla Ed25519).");

        var publica = SelloEd25519.ClavePublica(semilla);
        var kid = configuracion["Validasoft:Certificados:Kid"] is { Length: > 0 } propio ? propio.Trim() : SelloEd25519.KidPara(publica);
        return (kid, semilla, publica);
    }
}
