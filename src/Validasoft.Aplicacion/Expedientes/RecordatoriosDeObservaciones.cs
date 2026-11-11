using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Expedientes;

// Recordatorio de observaciones pendientes (NT-2, NT-3). El SRM tenía un recordatorio masivo de novedades; aquí la empresa le
// recuerda al tercero, desde el expediente o en lote, lo que sigue sin corregir: un correo (plantilla `recordatorio-observaciones`,
// editable) con las mismas observaciones vigentes y un enlace de acceso nuevo, y el aviso en la campana de su portal. A lo sumo uno
// por día y expediente (como el recordatorio de vencimientos). Opcional por empresa: que salga solo cada N días mientras el
// expediente siga devuelto.

/// <summary>Correo al tercero con el recordatorio de lo que tiene por corregir (cola <c>correo</c>, plantilla <c>recordatorio-observaciones</c>); el enlace va cifrado (IN-3).</summary>
public sealed record CorreoDeRecordatorioDeObservacionesPendiente(
    string Para, string Empresa, string Folio, IReadOnlyList<ObservacionParaCorreo> Observaciones, int DiasPendiente, string? EnlaceProtegido = null,
    DateTimeOffset? EnlaceExpira = null, Guid? TenantId = null, Guid? EmpresaId = null, Guid? TerceroId = null);

public static class CorreoDeRecordatorioDeObservacionesPendienteHandler
{
    public static async Task Handle(CorreoDeRecordatorioDeObservacionesPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.Recordatorio, ct))
            return;
        var mensaje = redactor.RedactarRecordatorioDeObservaciones(correo, correo.EnlaceProtegido is null ? null : protector.Desproteger(correo.EnlaceProtegido));
        await enviador.EnviarAsync((await plantillas.AplicarAsync(mensaje, CatalogoDePlantillas.RecordatorioObservaciones, correo.TenantId, correo.EmpresaId,
            EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct)).DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }
}

/// <summary>Configuración del recordatorio automático de una empresa (NT-3; id = empresa). Sin documento: apagado.</summary>
public sealed class ConfiguracionDeRecordatorios
{
    public const int MinimoDias = 1;
    public const int MaximoDias = 60;

    /// <summary>Cada cuántos días sale solo cuando se enciende sin decir otra cosa.</summary>
    public const int DiasPorOmision = 3;

    public Guid Id { get; set; }

    public bool Activo { get; set; }

    public int CadaDias { get; set; } = DiasPorOmision;

    public DateTimeOffset ActualizadoEn { get; set; }

    public RecordatorioAutomaticoDto ADto() => new(Activo, CadaDias, ActualizadoEn == default ? null : ActualizadoEn);

    /// <summary>422 <c>dias-invalidos</c> fuera de 1 a 60.</summary>
    public static void ValidarDias(int cadaDias)
    {
        if (cadaDias is < MinimoDias or > MaximoDias)
            throw ReglaDeNegocioException.Validacion("dias-invalidos", $"El recordatorio automático sale cada {MinimoDias} a {MaximoDias} días.");
    }
}

public enum ResultadoDeRecordatorio
{
    /// <summary>Correo y aviso en la campana.</summary>
    Enviado,

    /// <summary>El vínculo no tiene correo: solo el aviso en la campana del tercero.</summary>
    SoloCampana,
    NoObservado,
    YaRecordadoHoy
}

public static class RecordatoriosDeObservaciones
{
    /// <summary>Actor de los eventos que emite el job automático (historial del expediente).</summary>
    public const string ActorAutomatico = "recordatorio-automatico";

    /// <summary>Expedientes que recuerda a la vez un lote con una lista (como el recordatorio de vencimientos).</summary>
    public const int MaximoPorLote = 200;

    /// <summary>Expedientes que recuerda «todos los observados» en una llamada; el resto queda para la siguiente.</summary>
    public const int MaximoTodos = 500;

    /// <summary>Expedientes que revisa un barrido automático como máximo por empresa.</summary>
    public const int MaximoPorBarrido = 2000;

    /// <summary>
    /// Registra el recordatorio en el expediente (evento) y, en la misma transacción, lo encola: el correo al tercero (con un
    /// enlace de acceso nuevo, RE-4) por el outbox y el aviso en su campana, que sale del evento. Sin correo en el vínculo solo
    /// queda el aviso. No guarda: lo hace quien llama (un lote lo hace una sola vez).
    /// </summary>
    public static async Task<ResultadoDeRecordatorio> EnviarAsync(IDocumentSession sesion, IEventStream<Expediente> stream, string por, bool automatico,
        IMessageBus mensajes, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct)
    {
        var expediente = stream.Aggregate!;
        switch (expediente.SinRecordatorioPorque(ahora))
        {
            case MotivoSinRecordatorio.NoObservado:
                return ResultadoDeRecordatorio.NoObservado;
            case MotivoSinRecordatorio.YaRecordadoHoy:
                return ResultadoDeRecordatorio.YaRecordadoHoy;
        }

        var recordatorio = expediente.RecordarObservaciones(por, automatico, ahora);
        stream.AppendOne(recordatorio);

        var vinculo = await sesion.LoadAsync<Vinculo>(expediente.VinculoId, ct);
        if (vinculo is null || string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return ResultadoDeRecordatorio.SoloCampana;

        var empresa = await sesion.LoadAsync<Empresa>(expediente.EmpresaId, ct);
        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        var (enlace, token) = EnlaceDeAcceso.Emitir(empresa?.TenantId, vinculo.Id, vinculo.TerceroId, vinculo.CorreoTercero,
            ahora + EnlaceDeAcceso.VigenciaObservacion, tipo?.ExigeOtpEnEnlace ?? false, ahora, MotivoEnlace.Observacion);
        sesion.Store(enlace);
        await mensajes.PublishAsync(new CorreoDeRecordatorioDeObservacionesPendiente(enlace.CorreoDestino, empresa?.Nombre ?? "La empresa", expediente.Folio,
            ObservacionesParaCorreo(expediente), recordatorio.DiasObservado, protector.Proteger(token), enlace.Expira, empresa?.TenantId, expediente.EmpresaId,
            vinculo.TerceroId));
        return ResultadoDeRecordatorio.Enviado;
    }

    /// <summary>Las observaciones vigentes como las lee el tercero: motivos (texto al tercero) y observación escrita, sin revisores ni áreas.</summary>
    public static IReadOnlyList<ObservacionParaCorreo> ObservacionesParaCorreo(Expediente expediente) =>
    [
        .. expediente.ObservacionesVigentes.Select(v => new ObservacionParaCorreo([.. v.Motivos.Select(m => m.TextoAlTercero)], v.Observacion,
            v.RequisitoId is { } requisitoId ? expediente.BuscarRequisito(requisitoId)?.Nombre : null))
    ];

    public static string Texto(ResultadoDeRecordatorio resultado) => resultado.ToString();
}

/// <summary>Recordar al tercero las observaciones de un expediente o de varios (NT-2).</summary>
public static class RecordatoriosDeObservacionesEndpoints
{
    /// <summary>
    /// Recuerda al tercero las observaciones pendientes de un expediente: correo con las observaciones vigentes y un enlace de
    /// acceso nuevo, y aviso en su campana. 409 <c>expediente-no-observado</c> si no está devuelto al tercero y 409
    /// <c>recordatorio-ya-enviado-hoy</c> si ya se le recordó hoy. Responde el expediente con su último recordatorio.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/recordar-observaciones")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<ExpedienteDto> Recordar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes,
        IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var detalle = await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var ahora = reloj.GetUtcNow();
            // La regla vive en el dominio: la lanza si no está observado o ya se recordó hoy.
            stream.Aggregate!.RecordarObservaciones(contexto.ActorId, false, ahora);
            await RecordatoriosDeObservaciones.EnviarAsync(empresa.Sesion, stream, contexto.ActorId, false, mensajes, protector, ahora, ct);
            await empresa.Sesion.SaveChangesAsync(ct);
            return await ExpedientesEndpoints.DetalleAsync(empresa, id, ct);
        });
        Peticiones.EscribirETag(http, detalle.Version);
        return detalle;
    }

    /// <summary>
    /// Recordatorio en lote: los <c>expedientes</c> elegidos (hasta 200) o, con <c>todos</c>, los de la empresa devueltos al
    /// tercero (hasta 500, los observados hace más tiempo primero), opcionalmente solo aquellos por los que pasa el <c>area</c> (los que ve en su
    /// bandeja, como el filtro de la pantalla). Un correo y
    /// un aviso por expediente; lo que no se puede recordar se omite y se informa con su motivo.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/recordar-observaciones")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<RecordatoriosEnviadosDto> RecordarVarios(RecordarObservacionesDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var pedidos = (dto.Expedientes ?? []).Distinct().ToList();
        if (!dto.Todos && pedidos.Count == 0)
            throw ReglaDeNegocioException.Validacion("lote-vacio", "Elige al menos un expediente.");
        if (!dto.Todos && pedidos.Count > RecordatoriosDeObservaciones.MaximoPorLote)
            throw ReglaDeNegocioException.Validacion("lote-demasiado-grande", $"Se recuerdan hasta {RecordatoriosDeObservaciones.MaximoPorLote} expedientes a la vez.");

        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            var objetivos = dto.Todos ? await ObservadosAsync(empresa, dto.Area, ahora, ct) : pedidos;
            var detalle = new List<ResultadoRecordatorioDto>();
            foreach (var id in objetivos)
            {
                if (await empresa.Sesion.LoadAsync<Expediente>(id, ct) is not { } existente || existente.EmpresaId != empresa.EmpresaId)
                {
                    detalle.Add(new ResultadoRecordatorioDto(id, null, "NoEncontrado"));
                    continue;
                }

                var stream = await empresa.ExpedienteParaEscribirAsync(id, null, ct);
                var resultado = await RecordatoriosDeObservaciones.EnviarAsync(empresa.Sesion, stream, contexto.ActorId, false, mensajes, protector, ahora, ct);
                detalle.Add(new ResultadoRecordatorioDto(id, stream.Aggregate!.Folio, RecordatoriosDeObservaciones.Texto(resultado)));
            }

            await empresa.Sesion.SaveChangesAsync(ct);
            var enviados = detalle.Count(d => d.Resultado is nameof(ResultadoDeRecordatorio.Enviado) or nameof(ResultadoDeRecordatorio.SoloCampana));
            return new RecordatoriosEnviadosDto(enviados, detalle.Count - enviados, detalle);
        });
    }

    /// <summary>Los expedientes devueltos al tercero que hoy admiten recordatorio, los observados hace más tiempo primero.</summary>
    private static async Task<IReadOnlyList<Guid>> ObservadosAsync(SesionDeEmpresa empresa, Guid? area, DateTimeOffset ahora, CancellationToken ct)
    {
        var observados = await empresa.Expedientes().Where(e => e.Estado == EstadoExpediente.Observado).OrderBy(e => e.ObservadoEn).Take(RecordatoriosDeObservaciones.MaximoPorBarrido)
            .ToListAsync(ct);
        return
        [
            .. observados
                .Where(e => e.SinRecordatorioPorque(ahora) is null)
                .Where(e => area is not { } a || e.EtapaActiva?.Areas.Any(x => x.AreaId == a) == true)
                .Take(RecordatoriosDeObservaciones.MaximoTodos)
                .Select(e => e.Id)
        ];
    }
}

/// <summary>
/// Recordatorio automático de la empresa activa (NT-3): apagado por omisión; encendido, sale solo cada N días mientras el
/// expediente siga devuelto al tercero. Con auditoría de configuración.
/// </summary>
public static class ConfiguracionDeRecordatoriosEndpoints
{
    [WolverineGet("/api/v1/configuracion/recordatorio-observaciones")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<RecordatorioAutomaticoDto> Obtener(SesionDeEmpresa empresa, CancellationToken ct) =>
        (await empresa.Sesion.LoadAsync<ConfiguracionDeRecordatorios>(empresa.EmpresaId, ct) ?? new ConfiguracionDeRecordatorios { Id = empresa.EmpresaId }).ADto();

    /// <summary>422 <c>dias-invalidos</c> fuera de 1 a 60.</summary>
    [WolverinePut("/api/v1/configuracion/recordatorio-observaciones")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<RecordatorioAutomaticoDto> Actualizar(ActualizarRecordatorioAutomaticoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        ConfiguracionDeRecordatorios.ValidarDias(dto.CadaDias);
        var empresaId = empresa.EmpresaId;
        var actual = await empresa.Sesion.LoadAsync<ConfiguracionDeRecordatorios>(empresaId, ct);
        var antes = actual?.ADto();
        var ahora = reloj.GetUtcNow();
        actual ??= new ConfiguracionDeRecordatorios { Id = empresaId };
        actual.Activo = dto.Activo;
        actual.CadaDias = dto.CadaDias;
        actual.ActualizadoEn = ahora;
        var despues = actual.ADto();

        empresa.Sesion.Store(actual);
        Auditoria.Registrar(empresa.Sesion, contexto, "RecordatorioDeObservaciones", empresaId.ToString("N"),
            antes is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }
}

/// <summary>Barre las empresas con el recordatorio automático encendido a la hora <see cref="Ahora"/> (o la actual), de todos los tenants o de uno.</summary>
public sealed record RevisarRecordatoriosDeObservaciones(Guid? TenantId = null, DateTimeOffset? Ahora = null);

/// <summary>Recuerda las observaciones de un expediente que ya tocaba recordar (idempotente: si ya no toca, no hace nada).</summary>
public sealed record RecordarObservacionesDelExpediente(Guid TenantId, Guid ExpedienteId, DateTimeOffset? Ahora = null);

public static class RecordatoriosAutomaticos
{
    /// <summary>
    /// Expedientes a los que hoy toca el recordatorio automático: los devueltos al tercero de las empresas que lo tienen encendido,
    /// a los que ya pasaron N días desde que se devolvieron o desde el último recordatorio.
    /// </summary>
    public static async Task<IReadOnlyList<RecordarObservacionesDelExpediente>> PendientesAsync(IDocumentStore store, Guid? tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        await using var global = store.QuerySession();
        IReadOnlyList<ConfiguracionDeRecordatorios> configuraciones;
        if (tenantId is { } tenant)
        {
            await using var delTenant = store.QuerySession(tenant.ToString("N"));
            configuraciones = await delTenant.Query<ConfiguracionDeRecordatorios>().Where(c => c.Activo).ToListAsync(ct);
        }
        else
        {
            configuraciones = await global.Query<ConfiguracionDeRecordatorios>().Where(c => c.AnyTenant() && c.Activo).ToListAsync(ct);
        }

        if (configuraciones.Count == 0)
            return [];

        var empresas = (await global.LoadManyAsync<Empresa>(ct, configuraciones.Select(c => c.Id).ToArray())).ToDictionary(e => e.Id);
        var pendientes = new List<RecordarObservacionesDelExpediente>();
        foreach (var configuracion in configuraciones)
        {
            if (!empresas.TryGetValue(configuracion.Id, out var empresa))
                continue;
            await using var consulta = store.QuerySession(empresa.TenantId.ToString("N"));
            var observados = await consulta.Query<Expediente>()
                .Where(e => e.EmpresaId == empresa.Id && e.Estado == EstadoExpediente.Observado)
                .OrderBy(e => e.ObservadoEn).Take(RecordatoriosDeObservaciones.MaximoPorBarrido).ToListAsync(ct);
            pendientes.AddRange(observados.Where(e => e.TocaRecordatorioAutomatico(configuracion.CadaDias, ahora))
                .Select(e => new RecordarObservacionesDelExpediente(empresa.TenantId, e.Id)));
        }

        return pendientes;
    }

    /// <summary>
    /// Recuerda un expediente con su stream bloqueado (no compite con una carga o un reenvío simultáneo): evento, correo por el
    /// outbox y aviso en la campana, en una transacción. Si ya no toca (el tercero respondió, ya se recordó hoy), no hace nada.
    /// </summary>
    public static async Task<bool> RecordarAsync(ISesiones sesiones, IMessageBus mensajes, IProtectorDeSecretos protector, RecordarObservacionesDelExpediente mensaje,
        DateTimeOffset ahora, CancellationToken ct)
    {
        // Dentro de un manejador, Wolverine abre la sesión del outbox con el tenant del contexto del mensaje (ver PlazosDeCargue).
        if (mensajes is IMessageContext contexto)
            contexto.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", RecordatoriosDeObservaciones.ActorAutomatico);

        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(mensaje.ExpedienteId, ct);
        if (stream.Aggregate is not { } expediente)
            return false;
        var configuracion = await sesion.LoadAsync<ConfiguracionDeRecordatorios>(expediente.EmpresaId, ct);
        if (configuracion is not { Activo: true } || !expediente.TocaRecordatorioAutomatico(configuracion.CadaDias, ahora))
            return false;

        var resultado = await RecordatoriosDeObservaciones.EnviarAsync(sesion, stream, RecordatoriosDeObservaciones.ActorAutomatico, true, mensajes, protector, ahora, ct);
        await sesion.SaveChangesAsync(ct);
        return resultado is ResultadoDeRecordatorio.Enviado or ResultadoDeRecordatorio.SoloCampana;
    }
}

public static class RevisarRecordatoriosDeObservacionesHandler
{
    /// <summary>Pide el recordatorio de cada expediente pendiente por la cola durable (cada uno en su transacción, con sus reintentos).</summary>
    public static async Task Handle(RevisarRecordatoriosDeObservaciones mensaje, IDocumentStore store, IMessageContext mensajes, TimeProvider reloj,
        CancellationToken ct)
    {
        foreach (var pendiente in await RecordatoriosAutomaticos.PendientesAsync(store, mensaje.TenantId, mensaje.Ahora ?? reloj.GetUtcNow(), ct))
            await mensajes.PublishAsync(pendiente with { Ahora = mensaje.Ahora });
    }
}

public static class RecordarObservacionesDelExpedienteHandler
{
    public static Task Handle(RecordarObservacionesDelExpediente mensaje, ISesiones sesiones, IMessageContext mensajes, IProtectorDeSecretos protector,
        TimeProvider reloj, CancellationToken ct) =>
        RecordatoriosAutomaticos.RecordarAsync(sesiones, mensajes, protector, mensaje, mensaje.Ahora ?? reloj.GetUtcNow(), ct);
}

/// <summary>
/// Dispara el barrido del recordatorio automático cada <c>Validasoft:RecordatoriosDeObservaciones:Intervalo</c> (por omisión, una
/// hora; <c>00:00:00</c> lo apaga). Con varias réplicas cada una lo pide: el barrido es idempotente (cada expediente se revisa con su
/// stream bloqueado y a lo sumo un recordatorio por día).
/// </summary>
public sealed class ProgramadorDeRecordatoriosDeObservaciones(IServiceProvider servicios, IConfiguration configuracion,
    ILogger<ProgramadorDeRecordatoriosDeObservaciones> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:RecordatoriosDeObservaciones:Intervalo", TimeSpan.FromHours(1));
        if (intervalo <= TimeSpan.Zero)
            return;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(2) < intervalo ? TimeSpan.FromMinutes(2) : intervalo, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new RevisarRecordatoriosDeObservaciones());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir el barrido de recordatorios de observaciones.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Ejecución inmediata del barrido automático (Plataforma), con fecha simulada solo en Development.</summary>
public static class RecordatoriosAutomaticosEndpoints
{
    /// <summary>
    /// Corre ya el barrido (Plataforma) y espera su resultado: de un tenant o de todos. <c>fecha</c> simula «ahora» y solo se admite en
    /// Development (pruebas de punta a punta; 422 <c>fecha-simulada-no-permitida</c> en otro entorno).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/recordatorios-observaciones/revisar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoRevisionRecordatoriosDto> Revisar(RevisarRecordatoriosDto dto, IDocumentStore store, ISesiones sesiones,
        IWolverineRuntime runtime, IProtectorDeSecretos protector, IHostEnvironment entorno, TimeProvider reloj, CancellationToken ct)
    {
        if (dto.Fecha is not null && !entorno.IsDevelopment())
            throw ReglaDeNegocioException.Validacion("fecha-simulada-no-permitida", "La fecha simulada solo se admite en Development.");

        var ahora = dto.Fecha ?? reloj.GetUtcNow();
        var pendientes = await RecordatoriosAutomaticos.PendientesAsync(store, dto.TenantId, ahora, ct);
        var enviados = 0;
        foreach (var pendiente in pendientes)
        {
            // Un contexto de mensajería por expediente: cada uno con su transacción y su outbox.
            if (await RecordatoriosAutomaticos.RecordarAsync(sesiones, new MessageContext(runtime), protector, pendiente, ahora, ct))
                enviados++;
        }

        return new ResultadoRevisionRecordatoriosDto(pendientes.Count, enviados);
    }
}
