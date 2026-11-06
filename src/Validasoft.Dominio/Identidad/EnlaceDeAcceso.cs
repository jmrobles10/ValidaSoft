using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>Resultado de canjear un enlace: abre la sesión de una vez o pide el código OTP que se envía al correo.</summary>
public sealed record ResultadoCanje(bool SesionAbierta, string? CodigoOtp, DateTimeOffset? OtpExpira);

/// <summary>
/// Enlace de acceso de un solo uso del tercero (docs/07 §2.2). Global (esquema <c>identidad</c>): el
/// <see cref="Id"/> es el hash SHA-256 hexadecimal del token, que nunca se guarda en claro. El tercero no tiene
/// contraseña: el enlace abre una sesión corta; si <see cref="RequiereOtp"/>, además debe digitar un código de 6
/// dígitos enviado al mismo correo (10 minutos, 5 intentos en total por enlace).
/// </summary>
public sealed class EnlaceDeAcceso
{
    public const int IntentosOtp = 5;
    public static readonly TimeSpan VigenciaOtp = TimeSpan.FromMinutes(10);

    /// <summary>SHA-256 hexadecimal (minúsculas) del token.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Tenant del vínculo que originó el enlace; nulo en los enlaces de autorregistro.</summary>
    public Guid? TenantId { get; set; }

    public Guid? VinculoId { get; set; }

    public Guid TerceroId { get; set; }

    public string CorreoDestino { get; set; } = string.Empty;

    public DateTimeOffset Expira { get; set; }

    public bool RequiereOtp { get; set; }

    public bool Usado { get; set; }

    public DateTimeOffset? UsadoEn { get; set; }

    public string? OtpHash { get; set; }

    public DateTimeOffset? OtpExpira { get; set; }

    /// <summary>Intentos fallidos acumulados en el enlace; no se reinicia al pedir otro código.</summary>
    public int OtpIntentos { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Crea el enlace y devuelve el token opaco (256 bits, base64url) que viaja solo en el correo.</summary>
    public static (EnlaceDeAcceso Enlace, string Token) Emitir(Guid? tenantId, Guid? vinculoId, Guid terceroId, string correoDestino,
        DateTimeOffset expira, bool requiereOtp, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(correoDestino) || !correoDestino.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "El enlace de acceso necesita un correo válido.");

        if (expira <= ahora)
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "El enlace de acceso debe vencer en el futuro.");

        var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        var enlace = new EnlaceDeAcceso
        {
            Id = HashDeToken(token),
            TenantId = tenantId,
            VinculoId = vinculoId,
            TerceroId = terceroId,
            CorreoDestino = correoDestino.Trim().ToLowerInvariant(),
            Expira = expira,
            RequiereOtp = requiereOtp,
            CreadoEn = ahora
        };
        return (enlace, token);
    }

    public static string HashDeToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty)));

    /// <summary>
    /// Sin OTP marca el enlace como usado y abre la sesión. Con OTP genera un código nuevo (el anterior deja de
    /// servir) sin reiniciar los intentos fallidos: si ya se agotaron, el enlace queda bloqueado.
    /// </summary>
    public ResultadoCanje Canjear(DateTimeOffset ahora)
    {
        AsegurarVigente(ahora);

        if (!RequiereOtp)
        {
            MarcarUsado(ahora);
            return new ResultadoCanje(true, null, null);
        }

        AsegurarNoBloqueado();
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        OtpHash = HashDeOtp(codigo);
        OtpExpira = ahora + VigenciaOtp < Expira ? ahora + VigenciaOtp : Expira;
        return new ResultadoCanje(false, codigo, OtpExpira);
    }

    /// <summary>
    /// Verifica el código. Devuelve <c>true</c> si es correcto (y marca el enlace usado) o <c>false</c> si no lo es
    /// (cuenta un intento fallido, que el llamador debe guardar antes de responder).
    /// </summary>
    public bool VerificarOtp(string? codigo, DateTimeOffset ahora)
    {
        AsegurarVigente(ahora);

        if (!RequiereOtp || OtpHash is null)
            throw ReglaDeNegocioException.Conflicto("otp-no-solicitado", "Primero canjea el enlace para recibir el código.");

        AsegurarNoBloqueado();

        if (OtpExpira <= ahora)
            throw ReglaDeNegocioException.Expirado("otp-vencido", "El código venció; vuelve a abrir el enlace para recibir otro.");

        var normalizado = (codigo ?? string.Empty).Trim();
        var esperado = Encoding.ASCII.GetBytes(OtpHash);
        var recibido = Encoding.ASCII.GetBytes(HashDeOtp(normalizado));
        if (normalizado.Length == 6 && CryptographicOperations.FixedTimeEquals(esperado, recibido))
        {
            MarcarUsado(ahora);
            return true;
        }

        OtpIntentos++;
        return false;
    }

    public int IntentosRestantes => Math.Max(0, IntentosOtp - OtpIntentos);

    private void AsegurarVigente(DateTimeOffset ahora)
    {
        if (Usado)
            throw ReglaDeNegocioException.Expirado("enlace-usado",
                "Este enlace ya se usó. Pide uno nuevo a la empresa que te invitó.");

        if (Expira <= ahora)
            throw ReglaDeNegocioException.Expirado("enlace-vencido",
                "Este enlace venció. Pide uno nuevo a la empresa que te invitó.");
    }

    private void AsegurarNoBloqueado()
    {
        if (OtpIntentos >= IntentosOtp)
            throw ReglaDeNegocioException.DemasiadosIntentos("otp-bloqueado",
                "Se agotaron los intentos del código. Pide un enlace nuevo a la empresa que te invitó.");
    }

    private void MarcarUsado(DateTimeOffset ahora)
    {
        Usado = true;
        UsadoEn = ahora;
        OtpHash = null;
        OtpExpira = null;
    }

    /// <summary>El código se guarda con hash ligado al enlace (dos enlaces con el mismo código no comparten hash).</summary>
    private string HashDeOtp(string codigo) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Id}:{codigo}")));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
