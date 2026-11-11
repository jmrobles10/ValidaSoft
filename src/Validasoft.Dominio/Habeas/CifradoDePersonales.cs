using System.Security.Cryptography;
using System.Text;

namespace Validasoft.Dominio.Habeas;

/// <summary>
/// Llave de datos de un tercero (OD-4): AES-256 propia de cada persona, guardada envuelta con la clave maestra de secretos. Todo
/// dato personal que va a un evento (que es inmutable) se cifra con ella; destruir la llave (<see cref="DestruidaEn"/>) deja ese
/// dato ilegible para siempre aunque el evento siga en el almacén de eventos: es el <i>crypto-shredding</i> del habeas data. Esquema
/// <c>boveda</c>, global (id = tercero).
/// </summary>
public sealed class LlaveDeDatosDelTercero
{
    /// <summary>El tercero dueño de la llave.</summary>
    public Guid Id { get; set; }

    /// <summary>Identifica esta llave dentro del tercero: si se destruye y se crea otra, lo cifrado con la anterior no se lee.</summary>
    public Guid Kid { get; set; }

    /// <summary>La llave de 32 bytes en base64, cifrada con el protector de secretos (clave maestra).</summary>
    public string LlaveProtegida { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; }
}

/// <summary>
/// Cifrado autenticado (AES-256-GCM) de los textos personales que van a eventos. El texto protegido es
/// <c>vsdp1.{tercero:N}.{llave:N}.{nonce|cifrado|etiqueta en base64url}</c> y lleva atado (como datos asociados) el tercero y la
/// llave: copiarlo a otro tercero o manipularlo hace fallar el descifrado.
/// </summary>
public static class CifradoDePersonales
{
    public const string Prefijo = "vsdp1";
    private const int TamanoLlave = 32;
    private const int TamanoNonce = 12;
    private const int TamanoEtiqueta = 16;

    public static byte[] NuevaLlave() => RandomNumberGenerator.GetBytes(TamanoLlave);

    public static string Cifrar(byte[] llave, Guid terceroId, Guid kid, string texto)
    {
        if (llave.Length != TamanoLlave)
            throw new ArgumentException("La llave de datos debe tener 32 bytes.", nameof(llave));

        var claro = Encoding.UTF8.GetBytes(texto);
        var nonce = RandomNumberGenerator.GetBytes(TamanoNonce);
        var cifrado = new byte[claro.Length];
        var etiqueta = new byte[TamanoEtiqueta];
        using (var aes = new AesGcm(llave, TamanoEtiqueta))
            aes.Encrypt(nonce, claro, cifrado, etiqueta, Asociados(terceroId, kid));

        var union = new byte[TamanoNonce + cifrado.Length + TamanoEtiqueta];
        nonce.CopyTo(union, 0);
        cifrado.CopyTo(union, TamanoNonce);
        etiqueta.CopyTo(union, TamanoNonce + cifrado.Length);
        return $"{Prefijo}.{terceroId:N}.{kid:N}.{Base64Url(union)}";
    }

    /// <summary>El tercero y la llave a los que dice pertenecer el texto; nulo si no tiene el formato de un texto protegido.</summary>
    public static (Guid TerceroId, Guid Kid)? Leer(string? protegido)
    {
        var partes = (protegido ?? string.Empty).Split('.');
        return partes is [Prefijo, var tercero, var llave, _] && Guid.TryParseExact(tercero, "N", out var terceroId) &&
               Guid.TryParseExact(llave, "N", out var kid)
            ? (terceroId, kid)
            : null;
    }

    /// <summary>El texto en claro. <see cref="CryptographicException"/> si la llave no es la que lo cifró o el texto se alteró.</summary>
    public static string Descifrar(byte[] llave, string protegido)
    {
        var (terceroId, kid) = Leer(protegido) ?? throw new CryptographicException("El texto no es un dato personal protegido.");
        var union = DeBase64Url(protegido.Split('.')[3]);
        if (union.Length < TamanoNonce + TamanoEtiqueta)
            throw new CryptographicException("El dato protegido está incompleto.");

        var cifrado = union.AsSpan(TamanoNonce, union.Length - TamanoNonce - TamanoEtiqueta);
        var claro = new byte[cifrado.Length];
        using (var aes = new AesGcm(llave, TamanoEtiqueta))
            aes.Decrypt(union.AsSpan(0, TamanoNonce), cifrado, union.AsSpan(union.Length - TamanoEtiqueta), claro, Asociados(terceroId, kid));
        return Encoding.UTF8.GetString(claro);
    }

    private static byte[] Asociados(Guid terceroId, Guid kid) => Encoding.UTF8.GetBytes($"{terceroId:N}|{kid:N}");

    private static string Base64Url(byte[] datos) => Convert.ToBase64String(datos).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DeBase64Url(string texto)
    {
        var normal = texto.Replace('-', '+').Replace('_', '/');
        normal = normal.PadRight(normal.Length + (4 - normal.Length % 4) % 4, '=');
        try
        {
            return Convert.FromBase64String(normal);
        }
        catch (FormatException)
        {
            throw new CryptographicException("El dato protegido no es base64 válido.");
        }
    }
}
