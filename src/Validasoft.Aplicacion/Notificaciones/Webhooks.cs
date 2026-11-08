using System.Globalization;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>Configuración de las entregas (<c>Validasoft:Webhooks:*</c>).</summary>
public sealed class OpcionesDeWebhooks(IConfiguration configuracion)
{
    /// <summary><c>http</c> hacia <c>localhost</c>/<c>127.0.0.1</c>/<c>[::1]</c> (solo desarrollo, como SE-1).</summary>
    public bool PermitirHttpLocal { get; } = configuracion.GetValue("Validasoft:Webhooks:PermitirHttpLocal", false);

    /// <summary>Destinos en redes privadas (solo desarrollo u on-premise con red controlada).</summary>
    public bool PermitirDestinosPrivados { get; } = configuracion.GetValue("Validasoft:Webhooks:PermitirDestinosPrivados", false);

    /// <summary>Tiempo máximo de cada intento (5 s por defecto; el receptor debe responder 2xx rápido y procesar después).</summary>
    public TimeSpan Timeout { get; } = TimeSpan.FromSeconds(Math.Clamp(configuracion.GetValue("Validasoft:Webhooks:TimeoutSegundos", 5), 1, 30));

    /// <summary>Fallos seguidos (entre todas sus entregas) que deshabilitan la suscripción.</summary>
    public int FallosParaDeshabilitar { get; } = Math.Max(1, configuracion.GetValue("Validasoft:Webhooks:FallosParaDeshabilitar", 20));

    /// <summary>Tiempo en que el secreto anterior sigue firmando tras una rotación.</summary>
    public TimeSpan SolapamientoRotacion { get; } =
        TimeSpan.FromHours(Math.Clamp(configuracion.GetValue("Validasoft:Webhooks:HorasSolapamientoRotacion", 24), 0, 168));

    /// <summary>Esperas entre intentos (<c>Validasoft:Webhooks:Reintentos:0..n</c> como <c>hh:mm:ss</c>); por defecto 1 min, 5 min, 30 min, 2 h y 6 h.</summary>
    public PoliticaDeReintentos Reintentos { get; } = new(
    [
        .. configuracion.GetSection("Validasoft:Webhooks:Reintentos").GetChildren()
            .Select(c => TimeSpan.TryParse(c.Value, CultureInfo.InvariantCulture, out var espera) ? espera : TimeSpan.Zero)
            .Where(e => e > TimeSpan.Zero)
    ]);
}

/// <summary>Petición HTTP de una entrega, ya firmada.</summary>
public sealed record SolicitudDeWebhook(string Url, string Cuerpo, IReadOnlyDictionary<string, string> Cabeceras);

/// <summary>Resultado de un intento: éxito = respuesta 2xx dentro del timeout. <see cref="Error"/> sin datos sensibles.</summary>
public sealed record ResultadoDeWebhook(bool Exitoso, int? CodigoHttp, long DuracionMs, string? Error);

/// <summary>Envío HTTP de las entregas; la implementación (con protección SSRF al conectar) vive en infraestructura.</summary>
public interface IEnviadorDeWebhooks
{
    Task<ResultadoDeWebhook> EnviarAsync(SolicitudDeWebhook solicitud, CancellationToken ct);
}

/// <summary>
/// Mensaje de la cola durable y secuencial <c>webhooks</c>: intentar la entrega número <see cref="Intento"/>. Lo publica la
/// suscripción de eventos (intento 1), el propio manejador al programar un reintento y el reenvío manual.
/// </summary>
public sealed record EnviarEntregaWebhook(Guid TenantId, Guid EntregaId, int Intento, bool Manual = false);

public static class EnviarEntregaWebhookHandler
{
    public const string Cabecera = "Validasoft-Signature";

    /// <summary>
    /// Firma y envía el sobre del evento, registra el intento (código, duración, error) y decide: entregada, reintento
    /// programado con backoff (mensaje programado en la misma transacción) o fallida. Al llegar a N fallos seguidos
    /// deshabilita la suscripción, lo audita y avisa a los administradores (campana y correo). Idempotente: un mensaje de un
    /// intento ya registrado no hace nada (reintento programado y reenvío manual no se duplican).
    /// </summary>
    public static async Task Handle(EnviarEntregaWebhook mensaje, ISesiones sesiones, IMessageContext mensajes, IEnviadorDeWebhooks enviador,
        IProtectorDeSecretos protector, OpcionesDeWebhooks opciones, TimeProvider reloj, CancellationToken ct)
    {
        // TD-8: la sesión del outbox toma el tenant del contexto del mensaje.
        mensajes.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        var entrega = await sesion.LoadAsync<EntregaWebhook>(mensaje.EntregaId, ct);
        if (entrega is null || entrega.Intentos.Count >= mensaje.Intento || (entrega.Estado == EstadoEntrega.Entregada && !mensaje.Manual))
            return;

        var suscripcion = await sesion.LoadAsync<SuscripcionWebhook>(entrega.SuscripcionId, ct);
        await using var global = sesiones.ConsultaGlobal();
        var evento = await global.LoadAsync<EventoPublico>(entrega.EventoId, ct);
        if (suscripcion is null || evento is null || (!suscripcion.Activa && !mensaje.Manual))
        {
            entrega.Cancelar();
            sesion.Store(entrega);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        var ahora = reloj.GetUtcNow();
        var intentoId = Guid.CreateVersion7();
        var cuerpo = JsonEventos.Cuerpo(evento.ADto());
        var timestamp = ahora.ToUnixTimeSeconds();
        var secretos = suscripcion.SecretosVigentes(ahora).Select(protector.Desproteger).ToList();
        var cabeceras = new Dictionary<string, string>
        {
            ["Validasoft-Event-Id"] = evento.Id.ToString(),
            ["Validasoft-Event-Type"] = evento.Tipo,
            ["Validasoft-Delivery"] = intentoId.ToString(),
            ["Validasoft-Timestamp"] = timestamp.ToString(CultureInfo.InvariantCulture),
            [Cabecera] = FirmaDeWebhook.Cabecera(secretos, timestamp, cuerpo)
        };

        var resultado = await enviador.EnviarAsync(new SolicitudDeWebhook(suscripcion.Url, cuerpo, cabeceras), ct);
        var despues = reloj.GetUtcNow();
        var espera = resultado.Exitoso || mensaje.Manual ? null : opciones.Reintentos.EsperaTras(mensaje.Intento);
        var deshabilitada = false;
        if (resultado.Exitoso)
            suscripcion.RegistrarExito(despues);
        else
        {
            deshabilitada = suscripcion.RegistrarFallo(opciones.FallosParaDeshabilitar, despues);
            if (!suscripcion.Activa)
                espera = null;
        }

        entrega.Registrar(new IntentoDeEntrega(mensaje.Intento, intentoId, ahora, resultado.CodigoHttp, resultado.DuracionMs, resultado.Exitoso,
            resultado.Error, mensaje.Manual), espera is { } e ? despues + e : null);
        sesion.Store(entrega);
        sesion.Store(suscripcion);

        if (espera is { } demora)
            await mensajes.ScheduleAsync(mensaje with { Intento = mensaje.Intento + 1, Manual = false }, despues + demora);

        if (deshabilitada)
            await AvisarDeshabilitacionAsync(sesion, mensajes, mensaje.TenantId, suscripcion, despues, ct);

        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>Auditoría (actor: el sistema) y aviso a los administradores del tenant (NW-7).</summary>
    private static async Task AvisarDeshabilitacionAsync(IDocumentSession sesion, IMessageBus mensajes, Guid tenantId, SuscripcionWebhook suscripcion,
        DateTimeOffset ahora, CancellationToken ct)
    {
        sesion.Store(new RegistroDeCambioDeConfiguracion
        {
            Id = Guid.CreateVersion7(),
            Entidad = "Webhook",
            EntidadId = suscripcion.Id.ToString(),
            Accion = AccionConfiguracion.Desactivado,
            Despues = WebhooksEndpoints.ADto(suscripcion),
            ActorTipo = ActorTipo.Sistema,
            ActorId = "validasoft-webhooks",
            Justificacion = $"Deshabilitada tras {suscripcion.FallosSeguidos} entregas fallidas seguidas.",
            Fecha = ahora
        });

        var usuarios = await sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
        var ids = usuarios.Select(u => u.Id).ToArray();
        var preferencias = (await sesion.LoadManyAsync<PreferenciasNotificacion>(ct, ids)).ToDictionary(p => p.Id);
        var plan = PlanificadorDeNotificaciones.WebhookDeshabilitado(tenantId, suscripcion, usuarios,
            id => preferencias.GetValueOrDefault(id), ahora, ahora.UtcTicks);
        foreach (var (_, notificacion) in plan.Campana)
            sesion.Store(notificacion);
        foreach (var correo in plan.Correos)
            await mensajes.PublishAsync(correo);
    }
}

/// <summary>
/// Suscripciones de webhooks por sistema integrado (docs/05 §2.8, NW-4): una llave de un sistema solo ve y gestiona las
/// de su sistema; una llave de administración (sin sistema) las de todo el tenant e indica el sistema al crear. Todo
/// cambio queda en la auditoría de configuración; el secreto nunca se audita ni se guarda en claro.
/// </summary>
public static class WebhooksEndpoints
{
    [WolverineGet("/api/v1/webhooks")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<IReadOnlyList<WebhookDto>> Listar(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var todas = await sesion.Query<SuscripcionWebhook>().OrderBy(s => s.CreadaEn).ToListAsync(ct);
        return [.. todas.Where(s => contexto.Sistema is null || s.Sistema == contexto.Sistema).Select(s => ADto(s))];
    }

    [WolverineGet("/api/v1/webhooks/{id}")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<WebhookDto> Obtener(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        return ADto(await CargarAsync(sesion, contexto, id, ct));
    }

    /// <summary>
    /// Crea la suscripción y devuelve el secreto una sola vez (201). URL https (http solo hacia localhost en desarrollo; nunca
    /// a IPs privadas), tipos del catálogo o <c>*</c>, empresas dentro del alcance de la credencial (422
    /// <c>empresas-fuera-de-alcance</c>).
    /// </summary>
    [WolverinePost("/api/v1/webhooks")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<IResult> Crear(CrearWebhookDto dto, ISesiones sesiones, IContextoSolicitud contexto, CatalogoDeEventos catalogo,
        IProtectorDeSecretos protector, OpcionesDeWebhooks opciones, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var url = DestinoDeWebhook.Validar(dto.Url, opciones.PermitirHttpLocal, opciones.PermitirDestinosPrivados);
        ValidarTipos(dto.Tipos, catalogo);

        await using var sesion = sesiones.DelTenant();
        var sistema = await SistemaAsync(sesion, contexto, dto.SistemaId, ct);
        var empresas = await EmpresasAsync(sesiones, contexto, tenantId, dto.Empresas, ct);

        var ahora = reloj.GetUtcNow();
        var secreto = FirmaDeWebhook.GenerarSecreto();
        var suscripcion = SuscripcionWebhook.Crear(sistema.Id, sistema.Codigo, url, dto.Tipos, empresas, dto.Descripcion, protector.Proteger(secreto), ahora);
        sesion.Store(suscripcion);
        Auditoria.Registrar(sesion, contexto, "Webhook", suscripcion.Id.ToString(), AccionConfiguracion.Creado, null, ADto(suscripcion), ahora);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/webhooks/{suscripcion.Id}", ADto(suscripcion, secreto));
    }

    [WolverinePut("/api/v1/webhooks/{id}")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<WebhookDto> Editar(Guid id, EditarWebhookDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        CatalogoDeEventos catalogo, OpcionesDeWebhooks opciones, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var url = dto.Url is null ? null : DestinoDeWebhook.Validar(dto.Url, opciones.PermitirHttpLocal, opciones.PermitirDestinosPrivados);
        if (dto.Tipos is not null)
            ValidarTipos(dto.Tipos, catalogo);

        await using var sesion = sesiones.DelTenant();
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        var empresas = dto.Empresas is null ? null : await EmpresasAsync(sesiones, contexto, tenantId, dto.Empresas, ct);
        var antes = ADto(suscripcion);
        var ahora = reloj.GetUtcNow();
        suscripcion.Editar(url, dto.Tipos, empresas, dto.Descripcion, ahora);
        sesion.Store(suscripcion);
        Auditoria.Registrar(sesion, contexto, "Webhook", suscripcion.Id.ToString(), AccionConfiguracion.Modificado, antes, ADto(suscripcion), ahora);
        await sesion.SaveChangesAsync(ct);
        return ADto(suscripcion);
    }

    /// <summary>Secreto nuevo (se muestra una vez); el anterior sigue firmando durante el solapamiento (24 h por defecto).</summary>
    [WolverinePost("/api/v1/webhooks/{id}/rotar-secreto")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<WebhookDto> RotarSecreto(Guid id, ISesiones sesiones, IContextoSolicitud contexto, IProtectorDeSecretos protector,
        OpcionesDeWebhooks opciones, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        var ahora = reloj.GetUtcNow();
        var secreto = FirmaDeWebhook.GenerarSecreto();
        suscripcion.RotarSecreto(protector.Proteger(secreto), opciones.SolapamientoRotacion, ahora);
        sesion.Store(suscripcion);
        Auditoria.Registrar(sesion, contexto, "Webhook", suscripcion.Id.ToString(), AccionConfiguracion.Rotado, null, ADto(suscripcion), ahora);
        await sesion.SaveChangesAsync(ct);
        return ADto(suscripcion, secreto);
    }

    /// <summary>Reactiva (también una deshabilitada por fallos) y reinicia la cuenta de fallos.</summary>
    [WolverinePost("/api/v1/webhooks/{id}/activar")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static Task<WebhookDto> Activar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct) =>
        CambiarAsync(id, sesiones, contexto, reloj, AccionConfiguracion.Modificado, (s, ahora) => s.Activar(ahora), ct);

    [WolverinePost("/api/v1/webhooks/{id}/desactivar")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static Task<WebhookDto> Desactivar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct) =>
        CambiarAsync(id, sesiones, contexto, reloj, AccionConfiguracion.Desactivado, (s, ahora) => s.Desactivar("manual", ahora), ct);

    /// <summary>Entregas de la suscripción, más recientes primero (<c>?estado=Fallida&amp;limite=50&amp;cursor=</c>).</summary>
    [WolverineGet("/api/v1/webhooks/{id}/entregas")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<PaginaDto<EntregaWebhookDto>> Entregas(Guid id, string? estado, int? limite, string? cursor, ISesiones sesiones,
        IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        var suscripcionId = suscripcion.Id;
        var consulta = sesion.Query<EntregaWebhook>().Where(e => e.SuscripcionId == suscripcionId);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoEntrega>(estado, true, out var filtro))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de entrega.");
            consulta = consulta.Where(e => e.Estado == filtro);
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            var antesDe = LeerCursor(cursor);
            consulta = consulta.Where(e => e.Cursor < antesDe);
        }

        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var pagina = await consulta.OrderByDescending(e => e.Cursor).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? pagina[tamano - 1].Cursor.ToString(CultureInfo.InvariantCulture) : null;
        return new PaginaDto<EntregaWebhookDto>([.. pagina.Take(tamano).Select(ADto)], siguiente);
    }

    [WolverineGet("/api/v1/webhooks/{id}/entregas/{entregaId}")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<EntregaWebhookDto> Entrega(Guid id, Guid entregaId, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        return ADto(await CargarEntregaAsync(sesion, suscripcion, entregaId, ct));
    }

    /// <summary>
    /// Reenvío manual (202): un intento más, ahora, aunque la entrega ya se haya entregado, fallado o cancelado y aunque la
    /// suscripción esté desactivada. Si falla no programa reintentos automáticos.
    /// </summary>
    [WolverinePost("/api/v1/webhooks/{id}/entregas/{entregaId}/reenviar")]
    [Authorize(Policy = Permisos.WebhooksAdministrar)]
    public static async Task<IResult> Reenviar(Guid id, Guid entregaId, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var sesion = sesiones.DelTenant(mensajes);
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        var entrega = await CargarEntregaAsync(sesion, suscripcion, entregaId, ct);
        entrega.Estado = EstadoEntrega.Pendiente;
        entrega.ProximoIntentoEn = null;
        sesion.Store(entrega);
        await mensajes.PublishAsync(new EnviarEntregaWebhook(tenantId, entrega.Id, entrega.Intentos.Count + 1, Manual: true));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/webhooks/{id}/entregas/{entrega.Id}", ADto(entrega));
    }

    // ---------- utilidades ----------

    private static async Task<WebhookDto> CambiarAsync(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        AccionConfiguracion accion, Action<SuscripcionWebhook, DateTimeOffset> cambio, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var suscripcion = await CargarAsync(sesion, contexto, id, ct);
        var antes = ADto(suscripcion);
        var ahora = reloj.GetUtcNow();
        cambio(suscripcion, ahora);
        sesion.Store(suscripcion);
        Auditoria.Registrar(sesion, contexto, "Webhook", suscripcion.Id.ToString(), accion, antes, ADto(suscripcion), ahora);
        await sesion.SaveChangesAsync(ct);
        return ADto(suscripcion);
    }

    /// <summary>La de otro sistema (con una llave de sistema) o de otro tenant responde 404 (T-4).</summary>
    private static async Task<SuscripcionWebhook> CargarAsync(IQuerySession sesion, IContextoSolicitud contexto, Guid id, CancellationToken ct)
    {
        var suscripcion = await sesion.LoadAsync<SuscripcionWebhook>(id, ct);
        return suscripcion is not null && (contexto.Sistema is null || suscripcion.Sistema == contexto.Sistema)
            ? suscripcion
            : throw ReglaDeNegocioException.NoEncontrado("webhook-no-encontrado", "No existe esa suscripción.");
    }

    private static async Task<EntregaWebhook> CargarEntregaAsync(IQuerySession sesion, SuscripcionWebhook suscripcion, Guid entregaId, CancellationToken ct)
    {
        var entrega = await sesion.LoadAsync<EntregaWebhook>(entregaId, ct);
        return entrega is not null && entrega.SuscripcionId == suscripcion.Id
            ? entrega
            : throw ReglaDeNegocioException.NoEncontrado("entrega-no-encontrada", "No existe esa entrega en la suscripción.");
    }

    /// <summary>El de la credencial; con una llave sin sistema, el indicado (422 <c>sistema-requerido</c> / <c>sistema-invalido</c>).</summary>
    private static async Task<SistemaIntegrado> SistemaAsync(IQuerySession sesion, IContextoSolicitud contexto, Guid? indicado, CancellationToken ct)
    {
        if (contexto.Sistema is { } codigo)
        {
            return await sesion.Query<SistemaIntegrado>().Where(s => s.Codigo == codigo).FirstOrDefaultAsync(ct) is { Activo: true } propio
                ? propio
                : throw ReglaDeNegocioException.Validacion("sistema-invalido", "El sistema integrado de la credencial está desactivado.");
        }

        if (indicado is not { } sistemaId)
            throw ReglaDeNegocioException.Validacion("sistema-requerido", "Indica el sistema integrado dueño de la suscripción (sistemaId).");
        return await sesion.LoadAsync<SistemaIntegrado>(sistemaId, ct) is { Activo: true } sistema
            ? sistema
            : throw ReglaDeNegocioException.Validacion("sistema-invalido", "El sistema integrado no existe en el tenant o está desactivado.");
    }

    /// <summary>
    /// Alcance efectivo = empresas pedidas ∩ empresas de la credencial (docs/07 §3.4). <c>*</c> con una credencial limitada
    /// se reduce a sus empresas; una empresa fuera del alcance ⇒ 422 <c>empresas-fuera-de-alcance</c>.
    /// </summary>
    private static async Task<IReadOnlyList<string>> EmpresasAsync(ISesiones sesiones, IContextoSolicitud contexto, Guid tenantId,
        IReadOnlyList<string>? pedidas, CancellationToken ct)
    {
        var lista = (pedidas ?? []).Select(e => (e ?? string.Empty).Trim()).Where(e => e.Length > 0).ToList();
        var todas = lista.Count == 0 || lista.Contains(SuscripcionWebhook.Todos);
        if (todas)
            return contexto.Empresas.Contains("*") ? [SuscripcionWebhook.Todos] : [.. contexto.Empresas];

        if (lista.Any(e => !Guid.TryParse(e, out _)))
            throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Las empresas se indican por su id (o \"*\" para todas).");
        var ids = lista.Select(Guid.Parse).Distinct().ToList();

        await using var consulta = sesiones.ConsultaGlobal();
        var propias = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync(ct);
        if (ids.Any(i => !propias.Contains(i)))
            throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Alguna empresa no pertenece al tenant.");
        if (!contexto.Empresas.Contains("*") && ids.Any(i => !contexto.Empresas.Contains(i.ToString())))
            throw ReglaDeNegocioException.Validacion("empresas-fuera-de-alcance", "La credencial no tiene acceso a alguna de esas empresas.");
        return [.. ids.Select(i => i.ToString())];
    }

    private static void ValidarTipos(IReadOnlyList<string>? tipos, CatalogoDeEventos catalogo)
    {
        var conocidos = catalogo.Tipos.Select(t => t.Tipo).ToHashSet();
        var invalidos = (tipos ?? []).Select(t => (t ?? string.Empty).Trim().ToLowerInvariant())
            .Where(t => t.Length > 0 && t != SuscripcionWebhook.Todos && !conocidos.Contains(t)).ToList();
        if (invalidos.Count > 0)
            throw ReglaDeNegocioException.Validacion("tipos-invalidos", $"Tipos de evento desconocidos: {string.Join(", ", invalidos)}.");
    }

    private static long LeerCursor(string cursor) =>
        long.TryParse(cursor, NumberStyles.None, CultureInfo.InvariantCulture, out var valor)
            ? valor
            : throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");

    public static WebhookDto ADto(SuscripcionWebhook s, string? secreto = null) =>
        new(s.Id, s.SistemaId, s.Sistema, s.Url, s.Descripcion, s.Tipos, s.Empresas, s.Version, s.Activa, s.FallosSeguidos, s.DeshabilitadaEn,
            s.CausaDeshabilitacion, s.CreadaEn, s.UltimaEntregaEn, s.SecretoAnteriorVence, secreto);

    public static EntregaWebhookDto ADto(EntregaWebhook e) =>
        new(e.Id, e.SuscripcionId, e.EventoId, e.TipoEvento, e.Cursor.ToString(CultureInfo.InvariantCulture), e.EmpresaId, e.Estado.ToString(),
            e.Intentos.Count, e.CreadaEn, e.EntregadaEn, e.ProximoIntentoEn,
            [.. e.Intentos.Select(i => new IntentoEntregaDto(i.Numero, i.IntentoId, i.Fecha, i.CodigoHttp, i.DuracionMs, i.Exitoso ? "Exitoso" : "Fallido",
                i.Error, i.Manual))]);
}
