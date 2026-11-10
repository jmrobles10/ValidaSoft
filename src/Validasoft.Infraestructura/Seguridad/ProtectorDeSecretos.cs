using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Claves de los secretos en reposo (PT-1): la <b>clave maestra</b> propia (<c>Validasoft:Secretos:ClaveMaestra</c>, 32
/// bytes en base64) cifra; las de <c>Validasoft:Secretos:ClavesAnteriores</c> solo descifran (rotación). Cada clave se
/// identifica por su <see cref="Kid"/>: los 8 primeros hex del SHA-256 de la clave, que viaja en el texto cifrado.
/// <para>
/// Compatibilidad con el formato anterior (PT-2): lo cifrado antes de la clave maestra no lleva versión y usa una clave
/// derivada (HKDF-SHA256, rótulo <c>validasoft:secretos-outbox:v1</c>) de <c>Validasoft:Sesiones:ClaveFirma</c>. Se
/// sigue descifrando con la clave de firma vigente y con las de <c>Validasoft:Secretos:ClavesFirmaHeredadas</c> (las de
/// firma de sesión que se hayan rotado), así que rotar la clave de sesiones ya no deja ilegibles los secretos.
/// </para>
/// </summary>
public sealed class ClavesDeSecretos
{
    public const string SeccionConfiguracion = "Validasoft:Secretos";
    public const int TamanoClave = 32;

    private static readonly byte[] RotuloHeredado = Encoding.UTF8.GetBytes("validasoft:secretos-outbox:v1");

    private ClavesDeSecretos(byte[] actual, IReadOnlyDictionary<string, byte[]> porKid, IReadOnlyList<byte[]> heredadas)
    {
        Actual = actual;
        KidActual = Kid(actual);
        PorKid = porKid;
        Heredadas = heredadas;
    }

    public byte[] Actual { get; }

    public string KidActual { get; }

    /// <summary>La actual y las anteriores, por su identificador.</summary>
    public IReadOnlyDictionary<string, byte[]> PorKid { get; }

    /// <summary>Claves derivadas de las claves de firma de sesión (formato sin versión, solo para descifrar).</summary>
    public IReadOnlyList<byte[]> Heredadas { get; }

    public IReadOnlyList<string> KidsAnteriores => [.. PorKid.Keys.Where(k => k != KidActual).Order(StringComparer.Ordinal)];

    public static string Kid(byte[] clave) => Convert.ToHexStringLower(SHA256.HashData(clave))[..8];

    /// <summary>Lee y valida la configuración; lanza <see cref="InvalidOperationException"/> con el motivo (sin la clave).</summary>
    public static ClavesDeSecretos Desde(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(SeccionConfiguracion);
        var actual = Decodificar(seccion["ClaveMaestra"], "Validasoft:Secretos:ClaveMaestra")
                     ?? throw new InvalidOperationException(
                         "Falta Validasoft:Secretos:ClaveMaestra (32 bytes aleatorios en base64; ver docs/07 §6.3). Sin ella no se pueden cifrar secretos.");

        var porKid = new Dictionary<string, byte[]>(StringComparer.Ordinal) { [Kid(actual)] = actual };
        var anteriores = seccion.GetSection("ClavesAnteriores").GetChildren().Select(c => c.Value).Where(v => !string.IsNullOrWhiteSpace(v));
        foreach (var (texto, i) in anteriores.Select((v, i) => (v, i)))
        {
            var clave = Decodificar(texto, $"Validasoft:Secretos:ClavesAnteriores:{i}")!;
            porKid.TryAdd(Kid(clave), clave);
        }

        var firmas = new List<string>();
        if (configuracion["Validasoft:Sesiones:ClaveFirma"] is { Length: > 0 } firma)
            firmas.Add(firma);
        firmas.AddRange(seccion.GetSection("ClavesFirmaHeredadas").GetChildren().Select(c => c.Value).OfType<string>().Where(v => v.Length > 0));
        var heredadas = firmas.Select(Encoding.UTF8.GetBytes).Where(b => b.Length >= 32).Distinct(new ComparadorDeBytes())
            .Select(b => HKDF.DeriveKey(HashAlgorithmName.SHA256, b, TamanoClave, info: RotuloHeredado)).ToList();

        return new ClavesDeSecretos(actual, porKid, heredadas);
    }

    internal static byte[] RotuloDelFormatoHeredado => RotuloHeredado;

    private static byte[]? Decodificar(string? base64, string nombre)
    {
        if (string.IsNullOrWhiteSpace(base64))
            return null;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{nombre} no es base64 válido.");
        }

        return bytes.Length == TamanoClave
            ? bytes
            : throw new InvalidOperationException($"{nombre} debe tener {TamanoClave} bytes (tiene {bytes.Length}).");
    }

    private sealed class ComparadorDeBytes : IEqualityComparer<byte[]>
    {
        public bool Equals(byte[]? x, byte[]? y) => x is not null && y is not null && x.AsSpan().SequenceEqual(y);

        public int GetHashCode(byte[] obj) => obj.Length == 0 ? 0 : HashCode.Combine(obj.Length, obj[0], obj[^1]);
    }
}

/// <summary>
/// Secretos en reposo y en el outbox con AES-256-GCM (PT-1): llaves de OpenAI, secretos de webhooks, números de cuenta,
/// tokens de enlace y códigos OTP. Formato <c>vs1.{kid}.{base64url(nonce ‖ cifrado ‖ etiqueta)}</c>, con
/// <c>validasoft:secretos:v1|{kid}</c> como datos asociados (cambiar el kid del texto invalida la etiqueta). Cifra siempre con
/// la clave maestra actual; descifra con la que diga el kid (actual o anterior) o, sin prefijo, con el formato heredado.
/// </summary>
public sealed class ProtectorDeSecretos(IConfiguration configuracion) : IProtectorDeSecretos
{
    public const string Prefijo = "vs1";
    private const int TamanoNonce = 12;
    private const int TamanoEtiqueta = 16;

    private ClavesDeSecretos? _claves;

    public ClavesDeSecretos Claves => _claves ??= ClavesDeSecretos.Desde(configuracion);

    public string ClaveVigente => Claves.KidActual;

    public IReadOnlyList<string> ClavesAnteriores => Claves.KidsAnteriores;

    public string Proteger(string secreto)
    {
        var claves = Claves;
        var plano = Encoding.UTF8.GetBytes(secreto);
        var salida = new byte[TamanoNonce + plano.Length + TamanoEtiqueta];
        var nonce = salida.AsSpan(0, TamanoNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(claves.Actual, TamanoEtiqueta);
        aes.Encrypt(nonce, plano, salida.AsSpan(TamanoNonce, plano.Length), salida.AsSpan(TamanoNonce + plano.Length), Asociados(claves.KidActual));
        return $"{Prefijo}.{claves.KidActual}.{Base64Url.EncodeToString(salida)}";
    }

    public string Desproteger(string protegido)
    {
        var claves = Claves;
        if (Partir(protegido) is not { } partes)
            return DesprotegerHeredado(claves, protegido);

        if (!claves.PorKid.TryGetValue(partes.Kid, out var clave))
            throw new CryptographicException(
                $"El secreto se cifró con la clave {partes.Kid}, que no está configurada (agrégala a Validasoft:Secretos:ClavesAnteriores).");
        return Descifrar(clave, Base64Url.DecodeFromChars(partes.Cuerpo), Asociados(partes.Kid));
    }

    public bool EstaConClaveActual(string protegido) => Partir(protegido) is { } partes && partes.Kid == Claves.KidActual;

    /// <summary>Kid del texto cifrado, o nulo si está en el formato heredado (sin versión).</summary>
    public static string? KidDe(string protegido) => Partir(protegido)?.Kid;

    private static (string Kid, string Cuerpo)? Partir(string protegido)
    {
        // El formato heredado es base64url puro (sin puntos); el nuevo siempre empieza por «vs1.».
        if (!protegido.StartsWith(Prefijo + ".", StringComparison.Ordinal))
            return null;
        var partes = protegido.Split('.');
        return partes.Length == 3 && partes[1].Length == 8 ? (partes[1], partes[2]) : throw new CryptographicException("Secreto protegido inválido.");
    }

    private static string DesprotegerHeredado(ClavesDeSecretos claves, string protegido)
    {
        var datos = Base64Url.DecodeFromChars(protegido);
        foreach (var clave in claves.Heredadas)
        {
            try
            {
                return Descifrar(clave, datos, ClavesDeSecretos.RotuloDelFormatoHeredado);
            }
            catch (CryptographicException)
            {
                // Se prueba con la siguiente clave de firma heredada.
            }
        }

        throw new CryptographicException(
            "No se pudo descifrar el secreto del formato anterior: su clave de sesión ya no está configurada (Validasoft:Secretos:ClavesFirmaHeredadas).");
    }

    private static string Descifrar(byte[] clave, byte[] datos, byte[] asociados)
    {
        if (datos.Length < TamanoNonce + TamanoEtiqueta)
            throw new CryptographicException("Secreto protegido inválido.");

        var largo = datos.Length - TamanoNonce - TamanoEtiqueta;
        var plano = new byte[largo];
        using var aes = new AesGcm(clave, TamanoEtiqueta);
        aes.Decrypt(datos.AsSpan(0, TamanoNonce), datos.AsSpan(TamanoNonce, largo), datos.AsSpan(TamanoNonce + largo), plano, asociados);
        return Encoding.UTF8.GetString(plano);
    }

    private static byte[] Asociados(string kid) => Encoding.UTF8.GetBytes($"validasoft:secretos:v1|{kid}");
}

/// <summary>Sin una clave maestra válida la API no arranca (como el sello de los certificados, CE-11).</summary>
public sealed class VerificacionDeClaveMaestra(IProtectorDeSecretos protector) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (protector is ProtectorDeSecretos propio)
            _ = propio.Claves; // lanza InvalidOperationException con el motivo
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
