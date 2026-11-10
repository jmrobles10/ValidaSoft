using System.Security.Claims;
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
