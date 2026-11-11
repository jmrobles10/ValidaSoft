using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Cookie <c>vs_tercero</c> con el token de refresco (RE-3): <c>HttpOnly; SameSite=Lax; Path=/public/v1/sesion</c> y
/// <c>Secure</c> salvo que <c>Validasoft:SesionTercero:CookieSegura</c> sea falso (desarrollo local por http). Vence con la
/// sesión de refresco (máximo 8 h desde el canje). El navegador solo la envía a renovar y cerrar.
/// </summary>
public sealed class CookieDeSesionTercero(IConfiguration configuracion)
{
    public const string Nombre = "vs_tercero";
    public const string Ruta = "/public/v1/sesion";

    public bool Segura => !bool.TryParse(configuracion["Validasoft:SesionTercero:CookieSegura"], out var segura) || segura;

    public void Poner(HttpResponse respuesta, string token, DateTimeOffset expira) =>
        respuesta.Cookies.Append(Nombre, token, Opciones(expira));

    public void Borrar(HttpResponse respuesta) => respuesta.Cookies.Delete(Nombre, Opciones(null));

    public static string? Leer(HttpRequest peticion) =>
        peticion.Cookies.TryGetValue(Nombre, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;

    private CookieOptions Opciones(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        Secure = Segura,
        SameSite = SameSiteMode.Lax,
        Path = Ruta,
        Expires = expira,
        IsEssential = true
    };
}

/// <summary>Apertura de la sesión de refresco al canjear (misma transacción que marca el enlace usado).</summary>
public static class SesionesDeRefresco
{
    public static (string Token, DateTimeOffset Expira) Abrir(IDocumentSession sesion, EnlaceDeAcceso enlace, bool otpVerificado,
        DateTimeOffset ahora)
    {
        var (refresco, token) = SesionDeRefresco.Abrir(enlace.TerceroId, enlace.CorreoDestino, otpVerificado, ahora);
        sesion.Insert(refresco);
        return (token, refresco.ExpiraEn);
    }
}

/// <summary>
/// Renovación y cierre de la sesión del tercero con la cookie <c>vs_tercero</c> (docs/07 §2.1, RE-3). Anónimas para el
/// esquema Bearer (se autentican con la cookie), con el límite por IP de <c>/public/v1</c> y la cabecera
/// <c>X-Validasoft-Cliente: public</c> como defensa adicional contra CSRF (un formulario de otro sitio no puede ponerla).
/// </summary>
public static class SesionPublicaEndpoints
{
    public const string CabeceraCliente = "X-Validasoft-Cliente";
    public const string ValorCliente = "public";

    /// <summary>
    /// 200 con un <see cref="SesionTerceroDto"/> nuevo (vínculos recalculados, <c>otpVerificado</c> conservado) y rota la
    /// cookie. Sin cookie, vencida o desconocida ⇒ 401 <c>sesion-expirada</c>; token ya rotado ⇒ revoca la familia y 401
    /// <c>sesion-revocada</c>; dos renovaciones simultáneas con el mismo token ⇒ una gana y la otra recibe 409
    /// <c>renovacion-concurrente</c> sin revocar (su navegador ya recibió la cookie nueva).
    /// </summary>
    [WolverinePost("/public/v1/sesion/renovar")]
    [AllowAnonymous]
    public static async Task<SesionTerceroDto> Renovar(HttpContext http, ISesiones sesiones, IEmisorDeSesionesTercero emisor,
        CookieDeSesionTercero cookie, TimeProvider reloj, CancellationToken ct)
    {
        AsegurarCliente(http);
        var token = CookieDeSesionTercero.Leer(http.Request);
        if (SesionDeRefresco.FamiliaDe(token) is not { } familia)
            throw SesionExpirada();

        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var refresco = await sesion.LoadAsync<SesionDeRefresco>(familia, ct) ?? throw SesionExpirada();
        var resultado = refresco.Renovar(token!, ahora);
        sesion.Store(refresco);

        if (!resultado.Renovada)
        {
            // Reuso: la revocación se guarda antes de responder (también invalida el token que tenga quien lo robó).
            try
            {
                await sesion.SaveChangesAsync(ct);
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                await RevocarDeNuevoAsync(sesiones, familia, SesionDeRefresco.MotivoReuso, ahora, ct);
            }

            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada",
                "Detectamos un uso repetido de tu sesión y la cerramos por seguridad. Pide un enlace nuevo para entrar.");
        }

        var acceso = await EnlacesDeAcceso.EmitirAccesoAsync(sesion, emisor, refresco.TerceroId, refresco.Correo, refresco.OtpVerificado, ahora, ct);
        try
        {
            await sesion.SaveChangesAsync(ct);
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            // Concurrencia optimista: otra renovación con el mismo token ganó; nunca quedan dos tokens válidos.
            throw ReglaDeNegocioException.Conflicto("renovacion-concurrente",
                "La sesión se renovó al mismo tiempo en otra pestaña; vuelve a intentarlo.");
        }

        cookie.Poner(http.Response, resultado.Token!, refresco.ExpiraEn);
        return acceso;
    }

    /// <summary>204: revoca la familia de la cookie (si la hay) y la borra. Idempotente.</summary>
    [WolverinePost("/public/v1/sesion/cerrar")]
    [AllowAnonymous]
    public static async Task<IResult> Cerrar(HttpContext http, ISesiones sesiones, CookieDeSesionTercero cookie, TimeProvider reloj,
        CancellationToken ct)
    {
        AsegurarCliente(http);
        if (SesionDeRefresco.FamiliaDe(CookieDeSesionTercero.Leer(http.Request)) is { } familia)
        {
            await using var sesion = sesiones.Global();
            if (await sesion.LoadAsync<SesionDeRefresco>(familia, ct) is { Revocada: false } refresco)
            {
                refresco.Revocar(SesionDeRefresco.MotivoCierre, reloj.GetUtcNow());
                sesion.Store(refresco);
                try
                {
                    await sesion.SaveChangesAsync(ct);
                }
                catch (Exception e) when (Peticiones.EsCarrera(e))
                {
                    // Una renovación simultánea rotó el token: se revoca sobre la versión actual.
                    await RevocarDeNuevoAsync(sesiones, familia, SesionDeRefresco.MotivoCierre, reloj.GetUtcNow(), ct);
                }
            }
        }

        cookie.Borrar(http.Response);
        return Results.NoContent();
    }

    /// <summary>Revoca sobre la versión actual cuando una renovación simultánea ganó la escritura.</summary>
    private static async Task RevocarDeNuevoAsync(ISesiones sesiones, Guid familia, string motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        if (await sesion.LoadAsync<SesionDeRefresco>(familia, ct) is { Revocada: false } refresco)
        {
            refresco.Revocar(motivo, ahora);
            sesion.Store(refresco);
            await sesion.SaveChangesAsync(ct);
        }
    }

    private static void AsegurarCliente(HttpContext http)
    {
        if (!string.Equals(http.Request.Headers[CabeceraCliente].ToString().Trim(), ValorCliente, StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Prohibido("cliente-invalido",
                $"Esta ruta solo la usa la aplicación del tercero (falta la cabecera {CabeceraCliente}).");
    }

    private static ReglaDeNegocioException SesionExpirada() =>
        ReglaDeNegocioException.NoAutenticado("sesion-expirada", "Tu sesión venció. Pide un enlace nuevo para entrar.");
}

/// <summary>
/// Volver a entrar con el correo (RE-1): el tercero pide un enlace y lo recibe si tiene vínculos vigentes con ese correo.
/// La respuesta nunca revela si el correo existe.
/// </summary>
public static class AccesoPorCorreoEndpoints
{
    public const string MensajeEnviado = "Si tu correo está registrado, te enviamos un enlace para entrar.";

    private static readonly EstadoVinculo[] Terminales = [EstadoVinculo.Anulado, EstadoVinculo.Desvinculado];

    /// <summary>
    /// Siempre 202 con el mismo cuerpo. Si hay vínculos no terminales (de cualquier tenant) cuyo correo de invitación es
    /// este, envía un enlace de acceso sin vínculo por cada tercero (con OTP si alguno de sus tipos lo exige), como máximo
    /// 3 por hora por correo. Correo vacío o mal formado ⇒ 422 <c>correo-invalido</c> (no depende de que exista).
    /// </summary>
    [WolverinePost("/public/v1/acceso")]
    [AllowAnonymous]
    public static async Task<IResult> Solicitar(SolicitarAccesoDto dto, ISesiones sesiones, IMessageBus mensajes, IProtectorDeSecretos protector,
        TimeProvider reloj, CancellationToken ct)
    {
        var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();
        var arroba = correo.IndexOf('@', StringComparison.Ordinal);
        if (correo.Length > 254 || arroba <= 0 || arroba == correo.Length - 1 || correo.Any(char.IsWhiteSpace))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "Escribe un correo válido.");

        try
        {
            await EnviarAsync(correo, sesiones, mensajes, protector, reloj.GetUtcNow(), ct);
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            // Dos solicitudes simultáneas del mismo correo: la perdedora no envía nada y responde igual (sin oráculo).
            if (mensajes is MessageContext contexto)
                await contexto.ClearAllAsync();
        }

        return Results.Json(new SolicitudRecibidaDto(MensajeEnviado), statusCode: StatusCodes.Status202Accepted);
    }

    private static async Task EnviarAsync(string correo, ISesiones sesiones, IMessageBus mensajes, IProtectorDeSecretos protector,
        DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global(mensajes);
        var vinculos = (await sesion.Query<Vinculo>()
                .Where(v => v.AnyTenant() && v.CorreoTercero == correo)
                .ToListAsync(ct))
            .Where(v => !Terminales.Contains(v.Estado))
            .ToList();

        // Un tercero de pruebas (SB-4) no entra por aquí: el correo de un sandbox nunca sale a su destinatario, y el enlace de su
        // invitación está en el buzón del sandbox. Así el correo real de alguien que probó una integración no recibe enlaces de pruebas.
        var terceroIds = vinculos.Select(v => v.TerceroId).Distinct().ToArray();
        if (terceroIds.Length > 0)
        {
            var deSandbox = (await sesion.Query<Tercero>().Where(t => t.Id.IsOneOf(terceroIds) && t.SandboxTenantId != null).Select(t => t.Id)
                .ToListAsync(ct)).ToHashSet();
            vinculos = [.. vinculos.Where(v => !deSandbox.Contains(v.TerceroId))];
        }

        if (vinculos.Count == 0)
            return;

        var limite = await sesion.LoadAsync<LimiteDeAccesoPorCorreo>(LimiteDeAccesoPorCorreo.IdDe(correo), ct);
        if (limite is null)
        {
            limite = LimiteDeAccesoPorCorreo.Nuevo(correo);
            if (!limite.IntentarRegistrar(ahora))
                return;
            sesion.Insert(limite);
        }
        else
        {
            if (!limite.IntentarRegistrar(ahora))
                return;
            sesion.Store(limite);
        }

        var tipoIds = vinculos.Select(v => v.TipoVinculoId).Distinct().ToArray();
        var conOtp = (await sesion.Query<TipoVinculo>()
                .Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds))
                .ToListAsync(ct))
            .Where(t => t.ExigeOtpEnEnlace)
            .Select(t => t.Id)
            .ToHashSet();

        foreach (var deTercero in vinculos.GroupBy(v => v.TerceroId))
        {
            var (enlace, token) = EnlaceDeAcceso.Emitir(null, null, deTercero.Key, correo, ahora + EnlaceDeAcceso.VigenciaAccesoPorCorreo,
                deTercero.Any(v => conOtp.Contains(v.TipoVinculoId)), ahora, MotivoEnlace.AccesoPorCorreo);
            sesion.Store(enlace);
            await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.AccesoPorCorreo, enlace.CorreoDestino,
                protector.Proteger(token), enlace.Expira, null, null));
        }

        await sesion.SaveChangesAsync(ct);
    }
}
