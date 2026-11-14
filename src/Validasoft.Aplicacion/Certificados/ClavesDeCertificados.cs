using System.Globalization;
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

/// <summary>
/// Clave pública publicada en <c>.well-known</c>: la vigente o una retirada. Una retirada solo vale para verificar emisiones
/// anteriores a su retiro (<see cref="RetiradaEn"/>) y hasta su fecha límite (<see cref="VerificableHasta"/>); pasada esa fecha ya no
/// se publica (CL-2).
/// </summary>
public sealed record ClavePublicada(string Kid, byte[] ClavePublica, bool Vigente, DateTimeOffset? RetiradaEn = null, DateTimeOffset? VerificableHasta = null)
{
    /// <summary>La vigente siempre; una retirada solo mientras no pase su fecha límite.</summary>
    public bool Publicable(DateTimeOffset ahora) => Vigente || ahora <= VerificableHasta;

    /// <summary>
    /// ¿Puede esta clave dar por válido el sello de una emisión hecha en <paramref name="emitidoEn"/>, consultado en
    /// <paramref name="ahora"/>? La retirada, solo si la emisión es anterior a su retiro y su plazo no venció.
    /// </summary>
    public bool Valida(DateTimeOffset emitidoEn, DateTimeOffset ahora) => Vigente || (Publicable(ahora) && emitidoEn <= RetiradaEn);
}

/// <summary>
/// Claves de sellado (<c>Validasoft:Certificados</c>): <c>ClavePrivada</c> = semilla Ed25519 de 32 bytes en base64 (secreto de
/// configuración: <c>VALIDASOFT__CERTIFICADOS__CLAVEPRIVADA</c>, Key Vault…), <c>Kid</c> opcional (por defecto se deriva de la
/// clave pública) y <c>ClavesAnteriores[] {Kid, ClavePublica, RetiradaEn, VerificableHasta?}</c> para seguir publicando las
/// retiradas tras una rotación (CE-4) <b>con fecha límite</b> (CL-2): <c>RetiradaEn</c> es obligatoria (la clave solo valida
/// emisiones anteriores a ella) y <c>VerificableHasta</c>, si falta, es <c>RetiradaEn</c> más
/// <c>DiasDeVerificacionDeClavesRetiradas</c> (730 por omisión). Se lee al primer uso (el servicio de arranque lo fuerza: una
/// configuración inválida impide arrancar): sin clave solo falla la emisión. Registrada por tipo (sin fábricas lambda).
/// </summary>
public sealed class ClavesDeCertificados(IConfiguration configuracion) : ISelladorDeCertificados
{
    public const string SeccionConfiguracion = "Validasoft:Certificados";

    /// <summary>Días que una clave retirada sigue verificando tras su retiro cuando no trae <c>VerificableHasta</c>.</summary>
    public const int DiasPorOmision = 730;

    private sealed record Cargada(string Kid, byte[] Semilla, byte[] Publica, IReadOnlyList<ClavePublicada> Retiradas);

    private readonly Lazy<Cargada> _cargada = new(() => Cargar(configuracion));

    public string Kid => _cargada.Value.Kid;

    public byte[] Firmar(byte[] mensaje) => SelloEd25519.Firmar(_cargada.Value.Semilla, mensaje);

    /// <summary>Todas las retiradas configuradas, vencidas o no (el servicio de arranque las revisa).</summary>
    public IReadOnlyList<ClavePublicada> Retiradas => _cargada.Value.Retiradas;

    /// <summary>La vigente primero y luego las retiradas cuyo plazo no venció en <paramref name="ahora"/>.</summary>
    public IReadOnlyList<ClavePublicada> Publicadas(DateTimeOffset ahora) =>
    [
        new ClavePublicada(_cargada.Value.Kid, _cargada.Value.Publica, true),
        .. _cargada.Value.Retiradas.Where(c => c.Publicable(ahora))
    ];

    /// <summary>Clave pública de un <c>kid</c> publicado en <paramref name="ahora"/>; nula si no se conoce o su plazo venció.</summary>
    public byte[]? ClavePublica(string kid, DateTimeOffset ahora) => Publicadas(ahora).FirstOrDefault(c => c.Kid == kid)?.ClavePublica;

    private static Cargada Cargar(IConfiguration configuracion)
    {
        var valor = configuracion[$"{SeccionConfiguracion}:ClavePrivada"];
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
        var kid = configuracion[$"{SeccionConfiguracion}:Kid"] is { Length: > 0 } propio ? propio.Trim() : SelloEd25519.KidPara(publica);
        return new Cargada(kid, semilla, publica, CargarRetiradas(configuracion, kid));
    }

    /// <summary>Lee y valida <c>ClavesAnteriores</c>; lanza <see cref="InvalidOperationException"/> con la entrada y el motivo.</summary>
    private static List<ClavePublicada> CargarRetiradas(IConfiguration configuracion, string kidVigente)
    {
        var dias = DiasPorOmision;
        if (configuracion[$"{SeccionConfiguracion}:DiasDeVerificacionDeClavesRetiradas"] is { Length: > 0 } texto &&
            (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out dias) || dias is < 1 or > 3650))
            throw new InvalidOperationException("Validasoft:Certificados:DiasDeVerificacionDeClavesRetiradas debe ser un entero de 1 a 3650.");

        var retiradas = new List<ClavePublicada>();
        foreach (var (anterior, i) in configuracion.GetSection($"{SeccionConfiguracion}:ClavesAnteriores").GetChildren().Select((c, i) => (c, i)))
        {
            var nombre = $"{SeccionConfiguracion}:ClavesAnteriores:{i}";
            var kid = anterior["Kid"]?.Trim();
            if (string.IsNullOrEmpty(kid))
                throw new InvalidOperationException($"{nombre}:Kid falta.");
            if (kid == kidVigente || retiradas.Any(c => c.Kid == kid))
                throw new InvalidOperationException($"{nombre}:Kid ({kid}) repite el de otra clave: cada clave necesita su propio kid.");

            var bytes = DecodificarPublica(anterior["ClavePublica"], nombre);
            var retiradaEn = LeerFecha(anterior["RetiradaEn"], $"{nombre}:RetiradaEn")
                ?? throw new InvalidOperationException(
                    $"{nombre}:RetiradaEn falta: una clave retirada necesita la fecha en que dejó de sellar (ISO 8601, p. ej. 2027-01-15T00:00:00Z); solo valida emisiones anteriores a ella.");
            var verificableHasta = LeerFecha(anterior["VerificableHasta"], $"{nombre}:VerificableHasta") ?? retiradaEn.AddDays(dias);
            if (verificableHasta <= retiradaEn)
                throw new InvalidOperationException($"{nombre}:VerificableHasta debe ser posterior a RetiradaEn.");

            retiradas.Add(new ClavePublicada(kid, bytes, false, retiradaEn, verificableHasta));
        }

        return retiradas;
    }

    private static byte[] DecodificarPublica(string? valor, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new InvalidOperationException($"{nombre}:ClavePublica falta.");

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(valor.Trim());
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{nombre}:ClavePublica no es base64.");
        }

        return bytes.Length == 32 ? bytes : throw new InvalidOperationException($"{nombre}:ClavePublica debe tener exactamente 32 bytes.");
    }

    private static DateTimeOffset? LeerFecha(string? valor, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return DateTimeOffset.TryParse(valor.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fecha)
            ? fecha
            : throw new InvalidOperationException($"{nombre} no es una fecha ISO 8601.");
    }
}
