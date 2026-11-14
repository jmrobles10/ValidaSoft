using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Marten;
using Microsoft.Extensions.Caching.Memory;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;

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
        DateTimeOffset ahora, EmbebidoDeSesion? embebido = null, bool aislada = false, Guid? sesionId = null)
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
        if (aislada)
            reclamos.Add(new Claim(ReclamosTercero.Aislado, "true"));
        if (sesionId is { } sesion)
            reclamos.Add(new Claim(ReclamosTercero.SesionId, sesion.ToString("N")));
        if (embebido is not null)
        {
            // Un id propio por sesión: dos emitidas en el mismo segundo no son el mismo token (queda rastro de cuál se usó).
            reclamos.Add(new Claim("jti", Guid.NewGuid().ToString("N")));
            reclamos.Add(new Claim(ReclamosTercero.Embebido, "true"));
            reclamos.Add(new Claim(ReclamosTercero.Sistema, embebido.Sistema));
            reclamos.Add(new Claim(ReclamosTercero.Origen, embebido.Origen));
            reclamos.Add(new Claim(ReclamosTercero.Componente, embebido.Componente));
            reclamos.Add(new Claim(ReclamosTercero.GeneracionDelSistema, embebido.Generacion.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var expira = ahora + (embebido?.Duracion ?? Duracion);
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
/// Comprueba en cada petición con un token del tercero que su sesión de refresco siga vigente y que su acceso aislado no se haya rechazado: cerrar una
/// sesión o rechazar un correo corta sus tokens de acceso en segundos, en todos los servidores (PI-8), sin esperar a que venza el de 30 minutos. Se
/// guarda <see cref="Vigencia"/> en memoria.
/// </summary>
public sealed class VerificadorDeSesionesDeTercero(IDocumentStore store, IMemoryCache cache, TimeProvider reloj, IBusDeInvalidaciones? bus = null)
    : ICacheDeSesionesDeTercero, IReceptorDeInvalidaciones
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    private const string PrefijoSesion = "s|";
    private const string PrefijoAcceso = "a|";

    public string Tipo => TiposDeInvalidacion.SesionTercero;

    private static string ClaveSesion(Guid sesionId) => "sesion-tercero:" + sesionId.ToString("N");

    private static string ClaveAcceso(Guid terceroId, string correo) => $"acceso-tercero:{terceroId:N}:{SolicitudDeAcceso.Normalizar(correo)}";

    public async Task<bool> VigenteAsync(Guid sesionId, CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveSesion(sesionId), out bool vigente))
            return vigente;

        await using var consulta = store.QuerySession();
        var sesion = await consulta.LoadAsync<SesionDeRefresco>(sesionId, ct);
        vigente = sesion is not null && sesion.VigenteEn(reloj.GetUtcNow());
        cache.Set(ClaveSesion(sesionId), vigente, Vigencia);
        return vigente;
    }

    public async Task<bool> AccesoVigenteAsync(Guid terceroId, string correo, CancellationToken ct)
    {
        var clave = ClaveAcceso(terceroId, correo);
        if (cache.TryGetValue(clave, out bool vigente))
            return vigente;

        await using var consulta = store.QuerySession(TenantsTecnicos.Boveda);
        var solicitud = await consulta.LoadAsync<SolicitudDeAcceso>(SolicitudDeAcceso.IdPara(terceroId, correo), ct);
        vigente = solicitud is not { Estado: EstadoSolicitudDeAcceso.Rechazada };
        cache.Set(clave, vigente, Vigencia);
        return vigente;
    }

    public void OlvidarSesion(Guid sesionId)
    {
        cache.Remove(ClaveSesion(sesionId));
        bus?.Publicar(Tipo, PrefijoSesion + sesionId.ToString("N"));
    }

    public void OlvidarAcceso(Guid terceroId, string correo)
    {
        cache.Remove(ClaveAcceso(terceroId, correo));
        bus?.Publicar(Tipo, $"{PrefijoAcceso}{terceroId:N}|{SolicitudDeAcceso.Normalizar(correo)}");
    }

    public void Recibir(string id)
    {
        if (id.StartsWith(PrefijoSesion, StringComparison.Ordinal) && Guid.TryParseExact(id[PrefijoSesion.Length..], "N", out var sesionId))
            cache.Remove(ClaveSesion(sesionId));
        else if (id.StartsWith(PrefijoAcceso, StringComparison.Ordinal) && id[PrefijoAcceso.Length..].Split('|', 2) is [var tercero, var correo] &&
                 Guid.TryParseExact(tercero, "N", out var terceroId))
            cache.Remove(ClaveAcceso(terceroId, correo));
    }

    /// <summary>Se perdió la escucha de avisos: lo guardado vence solo en segundos.</summary>
    public void Reiniciar()
    {
    }
}
