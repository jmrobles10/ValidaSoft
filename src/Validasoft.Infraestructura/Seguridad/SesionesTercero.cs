using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Dominio.Comun;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Sesión del tercero (docs/07 §2.2): JWT HS256 de 30 minutos con la misma clave <c>Validasoft:Sesiones:ClaveFirma</c>
/// que las sesiones embebidas, pero con audiencia <c>validasoft:tercero</c>. Solo lo acepta el esquema <c>Tercero</c>,
/// que se aplica únicamente a <c>/public/v1</c>; el esquema <c>Bearer</c> de <c>/api/v1</c> lo rechaza por audiencia.
/// </summary>
public sealed class EmisorDeSesionesTercero(IConfiguration configuracion) : IEmisorDeSesionesTercero
{
    public const string Esquema = "Tercero";
    public const string Audiencia = "validasoft:tercero";
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(30);

    public (string Token, DateTimeOffset Expira) Emitir(Guid terceroId, string correo, bool otpVerificado, IReadOnlyList<VinculoEnSesion> vinculos,
        DateTimeOffset ahora)
    {
        var reclamos = new List<Claim>
        {
            new(Reclamos.Sujeto, terceroId.ToString()),
            new(Reclamos.ActorTipo, ActorTipo.Tercero.ToString()),
            new(ReclamosTercero.Tipo, ReclamosTercero.ValorTipo),
            new(ReclamosTercero.TerceroId, terceroId.ToString()),
            new(ReclamosTercero.Correo, correo),
            new(ReclamosTercero.OtpVerificado, otpVerificado ? "true" : "false")
        };
        reclamos.AddRange(vinculos.Select(v => new Claim(ReclamosTercero.Vinculos, v.Reclamo())));

        var expira = ahora + Duracion;
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = EmisorDeSesiones.Emisor,
            Audience = Audiencia,
            Subject = new ClaimsIdentity(reclamos),
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(EmisorDeSesiones.ClaveDeFirma(configuracion), SecurityAlgorithms.HmacSha256)
        });
        return (token, expira);
    }

    public static TokenValidationParameters ParametrosDeValidacion(IConfiguration configuracion) => new()
    {
        ValidIssuer = EmisorDeSesiones.Emisor,
        ValidAudience = Audiencia,
        IssuerSigningKey = EmisorDeSesiones.ClaveDeFirma(configuracion),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = Reclamos.Sujeto
    };
}

/// <summary>
/// AES-256-GCM con una clave derivada (HKDF-SHA256) de <c>Validasoft:Sesiones:ClaveFirma</c> con un rótulo propio:
/// protege el token del enlace y el código OTP mientras esperan en el outbox (IN-3). Formato:
/// base64url(nonce ‖ cifrado ‖ etiqueta).
/// </summary>
public sealed class ProtectorDeSecretos(IConfiguration configuracion) : IProtectorDeSecretos
{
    private const int TamanoNonce = 12;
    private const int TamanoEtiqueta = 16;
    private static readonly byte[] Rotulo = Encoding.UTF8.GetBytes("validasoft:secretos-outbox:v1");

    private byte[]? _clave;

    private byte[] Clave => _clave ??= HKDF.DeriveKey(HashAlgorithmName.SHA256,
        EmisorDeSesiones.ClaveDeFirma(configuracion).Key, 32, info: Rotulo);

    public string Proteger(string secreto)
    {
        var plano = Encoding.UTF8.GetBytes(secreto);
        var salida = new byte[TamanoNonce + plano.Length + TamanoEtiqueta];
        var nonce = salida.AsSpan(0, TamanoNonce);
        RandomNumberGenerator.Fill(nonce);

        using var aes = new AesGcm(Clave, TamanoEtiqueta);
        aes.Encrypt(nonce, plano, salida.AsSpan(TamanoNonce, plano.Length), salida.AsSpan(TamanoNonce + plano.Length), Rotulo);
        return Base64Url.EncodeToString(salida);
    }

    public string Desproteger(string protegido)
    {
        var datos = Base64Url.DecodeFromChars(protegido);
        if (datos.Length < TamanoNonce + TamanoEtiqueta)
            throw new CryptographicException("Secreto protegido inválido.");

        var largo = datos.Length - TamanoNonce - TamanoEtiqueta;
        var plano = new byte[largo];
        using var aes = new AesGcm(Clave, TamanoEtiqueta);
        aes.Decrypt(datos.AsSpan(0, TamanoNonce), datos.AsSpan(TamanoNonce, largo), datos.AsSpan(TamanoNonce + largo), plano, Rotulo);
        return Encoding.UTF8.GetString(plano);
    }
}
