using System.Globalization;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Modo pull (docs/05 §2.8): los mismos eventos públicos que reciben los webhooks, en orden de cursor, solo de las empresas
/// al alcance de la credencial (y solo de la empresa activa si llega <c>X-Empresa</c>).
/// </summary>
public static class EventosEndpoints
{
    /// <summary>
    /// <c>?desde={cursor}&amp;tipos=a,b&amp;empresaId=&amp;limite=50</c> (máx. 200). Sin <c>desde</c> empieza por el primero.
    /// <c>empresaId</c> equivale a <c>X-Empresa</c> (para el pull de un integrador con varias empresas, CN-4); una empresa
    /// fuera del alcance de la credencial responde 403 <c>alcance-insuficiente</c>.
    /// </summary>
    [WolverineGet("/api/v1/eventos")]
    [Authorize(Policy = Permisos.EventosLeer)]
    public static async Task<PaginaEventosDto> Listar(string? desde, string? tipos, Guid? empresaId, int? limite, ISesiones sesiones,
        IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var cursor = string.IsNullOrWhiteSpace(desde) ? 0L : LeerCursor(desde);
        var tamano = Math.Clamp(limite ?? 50, 1, 200);

        await using var sesion = sesiones.ConsultaGlobal();
        var consulta = Filtrar(sesion.Query<EventoPublico>().Where(e => e.TenantId == tenantId && e.Cursor > cursor), tipos, contexto, empresaId);
        var pagina = await consulta.OrderBy(e => e.Cursor).Take(tamano + 1).ToListAsync(ct);
        var datos = pagina.Take(tamano).Select(e => e.ADto()).ToList();
        return new PaginaEventosDto(datos, datos.Count > 0 ? datos[^1].Cursor : cursor.ToString(CultureInfo.InvariantCulture), pagina.Count > tamano);
    }

    /// <summary>Catálogo v1 de tipos de evento público (para elegir al suscribirse).</summary>
    [WolverineGet("/api/v1/eventos/tipos")]
    public static IReadOnlyList<TipoEventoPublicoDto> Tipos(CatalogoDeEventos catalogo) => catalogo.Tipos;

    /// <summary>
    /// SSE (docs/05 §3.4): <c>id</c> = cursor (se reanuda con <c>Last-Event-ID</c> o <c>?desde=</c>), <c>event</c> = tipo,
    /// <c>data</c> = el mismo sobre del modo pull. Consulta cada 2 s y manda un comentario de vida cada 15 s.
    /// </summary>
    [WolverineGet("/api/v1/eventos/stream")]
    [Authorize(Policy = Permisos.EventosLeer)]
    public static async Task<IResult> Stream(HttpContext http, string? desde, string? tipos, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj)
    {
        var tenantId = contexto.TenantRequerido();
        var ultimo = http.Request.Headers["Last-Event-ID"].ToString() is { Length: > 0 } reanudar ? LeerCursor(reanudar)
            : string.IsNullOrWhiteSpace(desde) ? 0L : LeerCursor(desde);
        var ct = http.RequestAborted;
        await Sse.IniciarAsync(http);

        var ultimoLatido = reloj.GetUtcNow();
        while (!ct.IsCancellationRequested)
        {
            await using (var sesion = sesiones.ConsultaGlobal())
            {
                var desdeCursor = ultimo;
                var nuevos = await Filtrar(sesion.Query<EventoPublico>().Where(e => e.TenantId == tenantId && e.Cursor > desdeCursor), tipos, contexto)
                    .OrderBy(e => e.Cursor).Take(200).ToListAsync(ct);
                foreach (var evento in nuevos)
                {
                    await Sse.EscribirAsync(http, evento.Cursor.ToString(CultureInfo.InvariantCulture), evento.Tipo,
                        JsonSerializer.Serialize(evento.ADto(), JsonEventos.Opciones));
                    ultimo = evento.Cursor;
                }
            }

            ultimoLatido = await Sse.LatidoAsync(http, ultimoLatido, reloj);
            await Sse.EsperarAsync(ct);
        }

        return Sse.Fin;
    }

    private static IQueryable<EventoPublico> Filtrar(IQueryable<EventoPublico> consulta, string? tipos, IContextoSolicitud contexto,
        Guid? empresaPedida = null)
    {
        var lista = (tipos ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant()).ToArray();
        if (lista.Length > 0)
            consulta = consulta.Where(e => e.Tipo.IsOneOf(lista));

        if (empresaPedida is { } pedida)
        {
            if ((contexto.EmpresaId is { } conCabecera && conCabecera != pedida) ||
                (!contexto.Empresas.Contains("*") && !contexto.Empresas.Contains(pedida.ToString())))
                throw ReglaDeNegocioException.Prohibido("alcance-insuficiente", "Esa empresa no está al alcance de tu credencial.");
            Guid? empresa = pedida;
            consulta = consulta.Where(e => e.EmpresaId == empresa);
        }
        else if (contexto.EmpresaId is { } activa)
        {
            Guid? empresa = activa;
            consulta = consulta.Where(e => e.EmpresaId == empresa);
        }
        else if (!contexto.Empresas.Contains("*"))
        {
            var empresas = contexto.Empresas.Select(e => Guid.TryParse(e, out var id) ? (Guid?)id : null).Where(e => e is not null).ToArray();
            consulta = consulta.Where(e => e.EmpresaId.IsOneOf(empresas));
        }

        return consulta;
    }

    internal static long LeerCursor(string cursor) =>
        long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");
}

/// <summary>Escritura de Server-Sent Events.</summary>
internal static class Sse
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Latido = TimeSpan.FromSeconds(15);

    /// <summary>Resultado al cerrar el stream: la respuesta ya se escribió, no hay nada más que enviar.</summary>
    public static IResult Fin => Results.Empty;

    public static async Task IniciarAsync(HttpContext http)
    {
        http.Response.StatusCode = StatusCodes.Status200OK;
        http.Response.ContentType = "text/event-stream";
        http.Response.Headers.CacheControl = "no-cache";
        http.Response.Headers["X-Accel-Buffering"] = "no";
        await http.Response.WriteAsync(": conectado\n\n", http.RequestAborted);
        await http.Response.Body.FlushAsync(http.RequestAborted);
    }

    public static async Task EscribirAsync(HttpContext http, string id, string evento, string datos)
    {
        await http.Response.WriteAsync($"id: {id}\nevent: {evento}\ndata: {datos}\n\n", http.RequestAborted);
        await http.Response.Body.FlushAsync(http.RequestAborted);
    }

    /// <summary>Un evento sin <c>id</c>: no mueve el <c>Last-Event-ID</c> con el que se reanuda el stream.</summary>
    public static async Task EscribirSinIdAsync(HttpContext http, string evento, string datos)
    {
        await http.Response.WriteAsync($"event: {evento}\ndata: {datos}\n\n", http.RequestAborted);
        await http.Response.Body.FlushAsync(http.RequestAborted);
    }

    public static async Task<DateTimeOffset> LatidoAsync(HttpContext http, DateTimeOffset ultimo, TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();
        if (ahora - ultimo < Latido)
            return ultimo;
        await http.Response.WriteAsync(": latido\n\n", http.RequestAborted);
        await http.Response.Body.FlushAsync(http.RequestAborted);
        return ahora;
    }

    public static async Task EsperarAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(Intervalo, ct);
        }
        catch (OperationCanceledException)
        {
            // El cliente cerró la conexión.
        }
    }
}

/// <summary>
/// Campana de la persona que consulta (identidad delegada o su sesión; una integración sin persona recibe 422
/// <c>usuario-delegado-requerido</c>) y sus preferencias de correo (NW-8, NW-11). Con <c>X-Empresa</c> solo se ven las de
/// esa empresa y las del tenant (p. ej. webhooks).
/// </summary>
public static class NotificacionesEndpoints
{
    [WolverineGet("/api/v1/notificaciones")]
    public static async Task<BandejaNotificacionesDto> Listar(bool? soloNoLeidas, int? limite, string? cursor, ISesiones sesiones,
        IContextoSolicitud contexto, CancellationToken ct)
    {
        var usuarioId = Persona(contexto);
        await using var sesion = sesiones.DelTenant();
        var consulta = DeLaPersona(sesion, usuarioId, contexto);
        var noLeidas = await consulta.Where(n => !n.Leida).CountAsync(ct);
        if (soloNoLeidas == true)
            consulta = consulta.Where(n => !n.Leida);
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var antesDe = EventosEndpoints.LeerCursor(cursor);
            consulta = consulta.Where(n => n.Orden < antesDe);
        }

        var tamano = Math.Clamp(limite ?? 30, 1, 100);
        var pagina = await consulta.OrderByDescending(n => n.Orden).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? pagina[tamano - 1].Orden.ToString(CultureInfo.InvariantCulture) : null;
        return new BandejaNotificacionesDto([.. pagina.Take(tamano).Select(n => n.ADto())], siguiente, noLeidas);
    }

    [WolverinePost("/api/v1/notificaciones/{id}/leida")]
    public static async Task<NotificacionDto> MarcarLeida(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var usuarioId = Persona(contexto);
        await using var sesion = sesiones.DelTenant();
        var notificacion = await sesion.LoadAsync<Notificacion>(id, ct);
        if (notificacion is null || notificacion.UsuarioId != usuarioId)
            throw ReglaDeNegocioException.NoEncontrado("notificacion-no-encontrada", "No existe esa notificación.");
        notificacion.MarcarLeida(reloj.GetUtcNow());
        sesion.Store(notificacion);
        await sesion.SaveChangesAsync(ct);
        return notificacion.ADto();
    }

    /// <summary>Marca todas las no leídas (de la empresa activa y del tenant si llega <c>X-Empresa</c>).</summary>
    [WolverinePost("/api/v1/notificaciones/leidas")]
    public static async Task<NotificacionesMarcadasDto> MarcarTodas(ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var usuarioId = Persona(contexto);
        await using var sesion = sesiones.DelTenant();
        var pendientes = await DeLaPersona(sesion, usuarioId, contexto).Where(n => !n.Leida).ToListAsync(ct);
        var ahora = reloj.GetUtcNow();
        foreach (var notificacion in pendientes)
        {
            notificacion.MarcarLeida(ahora);
            sesion.Store(notificacion);
        }

        await sesion.SaveChangesAsync(ct);
        return new NotificacionesMarcadasDto(pendientes.Count);
    }

    /// <summary>
    /// SSE de la campana: <c>event: notificacion</c> con cada <see cref="NotificacionDto"/> nueva (id = su orden) y
    /// <c>event: no_leidas</c> con el total cuando cambia. Con <c>X-Empresa</c> también <c>event: cambio</c> (<see cref="CambioEnVivoDto"/>:
    /// tipo de recurso e id) cada vez que cambia algo de esa empresa —un expediente, un vínculo, su cuenta, un documento, la ficha de un tercero,
    /// un certificado— para que la pantalla abierta lo refresque sola (NT-12); solo de la empresa activa y su tenant. Consulta cada 2 s.
    /// </summary>
    [WolverineGet("/api/v1/notificaciones/stream")]
    public static async Task<IResult> Stream(HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj)
    {
        var usuarioId = Persona(contexto);
        var ct = http.RequestAborted;
        var ultimo = http.Request.Headers["Last-Event-ID"].ToString() is { Length: > 0 } reanudar ? EventosEndpoints.LeerCursor(reanudar) : reloj.GetUtcNow().UtcTicks;
        var tenantId = contexto.TenantRequerido();
        Guid? empresaActiva = contexto.EmpresaId;
        var cambios = new CursorDeCambios(CambiosEnVivo.InicioDeUnStream(reloj));
        await Sse.IniciarAsync(http);

        int? noLeidasAnterior = null;
        var ultimoLatido = reloj.GetUtcNow();
        while (!ct.IsCancellationRequested)
        {
            if (empresaActiva is { } empresaId)
            {
                await using var global = sesiones.ConsultaGlobal();
                await CambiosEnVivo.EscribirAsync(http, cambios.Nuevos(await CambiosEnVivo.DeLaEmpresaAsync(global, tenantId, empresaId, cambios.Desde, ct)));
            }

            await using (var sesion = sesiones.DelTenant())
            {
                var desde = ultimo;
                var nuevas = await DeLaPersona(sesion, usuarioId, contexto).Where(n => n.Orden > desde).OrderBy(n => n.Orden).Take(100).ToListAsync(ct);
                foreach (var notificacion in nuevas)
                {
                    await Sse.EscribirAsync(http, notificacion.Orden.ToString(CultureInfo.InvariantCulture), "notificacion",
                        JsonSerializer.Serialize(notificacion.ADto(), JsonEventos.Opciones));
                    ultimo = notificacion.Orden;
                }

                var noLeidas = await DeLaPersona(sesion, usuarioId, contexto).Where(n => !n.Leida).CountAsync(ct);
                if (noLeidas != noLeidasAnterior)
                {
                    await Sse.EscribirAsync(http, ultimo.ToString(CultureInfo.InvariantCulture), "no_leidas",
                        noLeidas.ToString(CultureInfo.InvariantCulture));
                    noLeidasAnterior = noLeidas;
                }
            }

            ultimoLatido = await Sse.LatidoAsync(http, ultimoLatido, reloj);
            await Sse.EsperarAsync(ct);
        }

        return Sse.Fin;
    }

    [WolverineGet("/api/v1/usuarios/me/preferencias-notificacion")]
    public static async Task<PreferenciasNotificacionDto> Preferencias(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var usuarioId = Persona(contexto);
        await using var sesion = sesiones.DelTenant();
        return ADto(await sesion.LoadAsync<PreferenciasNotificacion>(usuarioId, ct));
    }

    /// <summary>Reemplaza el valor de los tipos indicados (los demás conservan el suyo); 422 <c>tipo-notificacion-invalido</c>.</summary>
    [WolverinePut("/api/v1/usuarios/me/preferencias-notificacion")]
    public static async Task<PreferenciasNotificacionDto> ActualizarPreferencias(ActualizarPreferenciasNotificacionDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var usuarioId = Persona(contexto);
        var invalidos = (dto.Correo ?? new Dictionary<string, bool>()).Keys.Where(k => !TiposNotificacion.ConCorreo.Contains(k)).ToList();
        if (invalidos.Count > 0)
            throw ReglaDeNegocioException.Validacion("tipo-notificacion-invalido",
                $"Estos tipos no van por correo: {string.Join(", ", invalidos)}. Los que admiten correo: {string.Join(", ", TiposNotificacion.ConCorreo)}.");

        await using var sesion = sesiones.DelTenant();
        var preferencias = await sesion.LoadAsync<PreferenciasNotificacion>(usuarioId, ct) ?? new PreferenciasNotificacion { Id = usuarioId };
        foreach (var (tipo, valor) in dto.Correo ?? new Dictionary<string, bool>())
            preferencias.Correo[tipo] = valor;
        preferencias.ActualizadasEn = reloj.GetUtcNow();
        sesion.Store(preferencias);
        await sesion.SaveChangesAsync(ct);
        return ADto(preferencias);
    }

    private static PreferenciasNotificacionDto ADto(PreferenciasNotificacion? preferencias) =>
        new(TiposNotificacion.ConCorreo.ToDictionary(t => t, t => TiposNotificacion.QuiereCorreo(preferencias, t)), TiposNotificacion.ConCorreo);

    private static IQueryable<Notificacion> DeLaPersona(IQuerySession sesion, Guid usuarioId, IContextoSolicitud contexto)
    {
        var consulta = sesion.Query<Notificacion>().Where(n => n.UsuarioId == usuarioId);
        if (contexto.EmpresaId is { } activa)
        {
            Guid? empresa = activa;
            consulta = consulta.Where(n => n.EmpresaId == empresa || n.EmpresaId == null);
        }

        return consulta;
    }

    private static Guid Persona(IContextoSolicitud contexto) =>
        contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuarioId)
            ? usuarioId
            : throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "Las notificaciones son de una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");
}
