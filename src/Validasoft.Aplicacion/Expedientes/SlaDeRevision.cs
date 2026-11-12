using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Informes;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

// SLA de revisión (OP-10…OP-14). El plazo de cada área ya existía en el flujo (`SlaHoras` por área de cada etapa) y se medía en el informe;
// ahora además hay un plazo por defecto por área en la empresa (cuando el flujo no pone uno), alertas al acercarse y al vencer
// (eventos del expediente que recogen la campana, el correo y los webhooks), un indicador en la bandeja y el informe de cumplimiento.

/// <summary>
/// Configuración del SLA de una empresa (id = empresa): cuánto del plazo debe consumirse para el aviso previo, si los administradores se
/// enteran de los incumplimientos y el plazo por defecto de cada área (se copia al expediente al abrirse; el flujo manda sobre él).
/// </summary>
public sealed class ConfiguracionDeSla
{
    public const int MaximoHoras = 24 * 90;

    public Guid Id { get; set; }

    public int PorcentajeAviso { get; set; } = Expediente.PorcentajeDeAvisoPorOmision;

    public bool AvisarAdministradores { get; set; } = true;

    /// <summary>Horas por defecto de cada área (id del área → horas).</summary>
    public Dictionary<Guid, int> HorasPorArea { get; set; } = [];

    public DateTimeOffset ActualizadaEn { get; set; }

    public int? HorasDe(Guid areaId) => HorasPorArea.TryGetValue(areaId, out var horas) ? horas : null;

    /// <summary>422 <c>porcentaje-invalido</c> (50 a 95) o <c>sla-invalido</c> (1 hora a 90 días).</summary>
    public static void Validar(int porcentaje, IEnumerable<SlaDeAreaDto> areas)
    {
        if (porcentaje is < Expediente.PorcentajeDeAvisoMinimo or > Expediente.PorcentajeDeAvisoMaximo)
            throw ReglaDeNegocioException.Validacion("porcentaje-invalido",
                $"El aviso previo sale cuando se consume entre el {Expediente.PorcentajeDeAvisoMinimo} y el {Expediente.PorcentajeDeAvisoMaximo} % del plazo.");
        foreach (var area in areas)
            if (area.Horas is { } horas and (< 1 or > MaximoHoras))
                throw ReglaDeNegocioException.Validacion("sla-invalido", $"El plazo de un área va de 1 a {MaximoHoras} horas (90 días).");
    }

    public ConfiguracionSlaDto ADto(IEnumerable<Area> areas) =>
        new(PorcentajeAviso, AvisarAdministradores, [.. areas.Where(a => a.Activa).OrderBy(a => a.Nombre).Select(a => new SlaDeAreaDto(a.Id, HorasDe(a.Id)))],
            ActualizadaEn == default ? null : ActualizadaEn);
}

/// <summary>Plazo por defecto y umbral de aviso de la empresa, y el efecto de abrir un expediente (el flujo manda sobre el plazo por defecto).</summary>
public static class SlaDeLaEmpresa
{
    /// <summary>Configuración de la empresa o la de por omisión.</summary>
    public static async Task<ConfiguracionDeSla> CargarAsync(IQuerySession sesion, Guid empresaId, CancellationToken ct) =>
        await sesion.LoadAsync<ConfiguracionDeSla>(empresaId, ct) ?? new ConfiguracionDeSla { Id = empresaId };
}

/// <summary>Plazos de revisión de la empresa activa: plazo por defecto por área y umbral de aviso (con auditoría de configuración).</summary>
public static class ConfiguracionDeSlaEndpoints
{
    [WolverineGet("/api/v1/configuracion/sla")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<ConfiguracionSlaDto> Obtener(SesionDeEmpresa empresa, CancellationToken ct) =>
        (await SlaDeLaEmpresa.CargarAsync(empresa.Sesion, empresa.EmpresaId, ct)).ADto(await empresa.Areas().ToListAsync(ct));

    /// <summary>422 <c>porcentaje-invalido</c>, <c>sla-invalido</c>; 404 <c>area-no-encontrada</c> si un área no es de la empresa.</summary>
    [WolverinePut("/api/v1/configuracion/sla")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<ConfiguracionSlaDto> Actualizar(ActualizarConfiguracionSlaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var areasDto = dto.Areas ?? [];
        ConfiguracionDeSla.Validar(dto.PorcentajeAviso, areasDto);
        var areas = await empresa.Areas().ToListAsync(ct);
        foreach (var area in areasDto.Where(a => areas.All(x => x.Id != a.AreaId)))
            throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", $"El área {area.AreaId} no existe en la empresa.");

        var actual = await SlaDeLaEmpresa.CargarAsync(empresa.Sesion, empresa.EmpresaId, ct);
        var antes = actual.ActualizadaEn == default ? null : actual.ADto(areas);
        var ahora = reloj.GetUtcNow();
        actual.PorcentajeAviso = dto.PorcentajeAviso;
        actual.AvisarAdministradores = dto.AvisarAdministradores;
        // Lo que no se manda queda como estaba: se reemplaza lo de las áreas que llegan (horas nulas = sin plazo por defecto).
        foreach (var area in areasDto)
        {
            if (area.Horas is { } horas)
                actual.HorasPorArea[area.AreaId] = horas;
            else
                actual.HorasPorArea.Remove(area.AreaId);
        }

        actual.ActualizadaEn = ahora;
        var despues = actual.ADto(areas);
        empresa.Sesion.Store(actual);
        Auditoria.Registrar(empresa.Sesion, contexto, "SlaDeRevision", empresa.EmpresaId.ToString("N"),
            antes is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }
}

// ---------- Barrido de alertas ----------

/// <summary>Barre los expedientes con un aviso o un vencimiento de SLA por registrar a la hora <see cref="Ahora"/> (o la actual), de todos los tenants o de uno.</summary>
public sealed record RevisarSlas(Guid? TenantId = null, DateTimeOffset? Ahora = null);

/// <summary>Registra los avisos y vencimientos de SLA de un expediente (idempotente: lo ya registrado en la ronda no se repite).</summary>
public sealed record RevisarSlaDelExpediente(Guid TenantId, Guid ExpedienteId, DateTimeOffset? Ahora = null);

public static class SlasDeRevision
{
    /// <summary>Actor de los eventos que emite el barrido (historial del expediente).</summary>
    public const string Actor = "sla-revision";

    public const int MaximoPorBarrido = 2000;

    /// <summary>Expedientes con algo por registrar a <paramref name="ahora"/>, los más atrasados primero (el tenant de cada uno sale de su empresa).</summary>
    public static async Task<IReadOnlyList<RevisarSlaDelExpediente>> PendientesAsync(IDocumentStore store, Guid? tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        IReadOnlyList<Expediente> expedientes;
        if (tenantId is { } tenant)
        {
            await using var consulta = store.QuerySession(tenant.ToString("N"));
            expedientes = await consulta.Query<Expediente>()
                .Where(e => e.ProximaRevisionDeSla != null && e.ProximaRevisionDeSla <= ahora)
                .OrderBy(e => e.ProximaRevisionDeSla).Take(MaximoPorBarrido).ToListAsync(ct);
        }
        else
        {
            await using var consulta = store.QuerySession();
            expedientes = await consulta.Query<Expediente>()
                .Where(e => e.AnyTenant() && e.ProximaRevisionDeSla != null && e.ProximaRevisionDeSla <= ahora)
                .OrderBy(e => e.ProximaRevisionDeSla).Take(MaximoPorBarrido).ToListAsync(ct);
        }

        if (expedientes.Count == 0)
            return [];

        await using var global = store.QuerySession();
        var empresas = (await global.LoadManyAsync<Empresa>(ct, expedientes.Select(e => e.EmpresaId).Distinct().ToArray())).ToDictionary(e => e.Id);
        return [.. expedientes.Where(e => empresas.ContainsKey(e.EmpresaId)).Select(e => new RevisarSlaDelExpediente(empresas[e.EmpresaId].TenantId, e.Id))];
    }

    /// <summary>
    /// Revisa un expediente con su stream bloqueado y anota lo que toque: <see cref="SlaPorVencer"/> al cruzar el umbral de aviso de la
    /// empresa y <see cref="SlaIncumplido"/> al pasar el plazo. Devuelve los eventos anotados (vacío si no había nada).
    /// </summary>
    public static async Task<IReadOnlyList<object>> RevisarAsync(ISesiones sesiones, RevisarSlaDelExpediente mensaje, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", Actor);
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(mensaje.ExpedienteId, ct);
        if (stream.Aggregate is not { } expediente)
            return [];

        var configuracion = await SlaDeLaEmpresa.CargarAsync(sesion, expediente.EmpresaId, ct);
        var eventos = expediente.RevisarSla(ahora, configuracion.PorcentajeAviso);
        if (eventos.Count == 0)
            return [];
        stream.AppendMany(eventos);
        await sesion.SaveChangesAsync(ct);
        return eventos;
    }
}

public static class RevisarSlasHandler
{
    /// <summary>Pide la revisión de cada expediente pendiente por la cola durable (cada uno en su transacción, con sus reintentos).</summary>
    public static async Task Handle(RevisarSlas mensaje, IDocumentStore store, IMessageContext mensajes, TimeProvider reloj, CancellationToken ct)
    {
        foreach (var pendiente in await SlasDeRevision.PendientesAsync(store, mensaje.TenantId, mensaje.Ahora ?? reloj.GetUtcNow(), ct))
            await mensajes.PublishAsync(pendiente with { Ahora = mensaje.Ahora });
    }
}

public static class RevisarSlaDelExpedienteHandler
{
    public static async Task Handle(RevisarSlaDelExpediente mensaje, ISesiones sesiones, TimeProvider reloj, CancellationToken ct) =>
        await SlasDeRevision.RevisarAsync(sesiones, mensaje, mensaje.Ahora ?? reloj.GetUtcNow(), ct);
}

/// <summary>
/// Dispara el barrido de SLA cada <c>Validasoft:SlaDeRevision:Intervalo</c> (por omisión, 15 minutos; <c>00:00:00</c> lo apaga). Con varias
/// réplicas cada una lo pide: el barrido es idempotente (cada expediente se revisa con su stream bloqueado y cada alerta sale una vez por ronda).
/// </summary>
public sealed class ProgramadorDeSlaDeRevision(IServiceProvider servicios, IConfiguration configuracion, ILogger<ProgramadorDeSlaDeRevision> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:SlaDeRevision:Intervalo", TimeSpan.FromMinutes(15));
        if (intervalo <= TimeSpan.Zero)
            return;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1) < intervalo ? TimeSpan.FromMinutes(1) : intervalo, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new RevisarSlas());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir el barrido de SLA de revisión.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Ejecución inmediata del barrido de SLA (Plataforma), con fecha simulada solo en Development.</summary>
public static class SlasDeRevisionEndpoints
{
    /// <summary>
    /// Corre ya el barrido (Plataforma) y espera su resultado: de un tenant o de todos. <c>fecha</c> simula «ahora» y solo se admite en
    /// Development (pruebas de punta a punta; 422 <c>fecha-simulada-no-permitida</c> en otro entorno).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/sla/revisar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoRevisionSlaDto> Revisar(RevisarSlaDto dto, IDocumentStore store, ISesiones sesiones, IHostEnvironment entorno, TimeProvider reloj,
        CancellationToken ct)
    {
        if (dto.Fecha is not null && !entorno.IsDevelopment())
            throw ReglaDeNegocioException.Validacion("fecha-simulada-no-permitida", "La fecha simulada solo se admite en Development.");

        var ahora = dto.Fecha ?? reloj.GetUtcNow();
        var pendientes = await SlasDeRevision.PendientesAsync(store, dto.TenantId, ahora, ct);
        int avisos = 0, incumplidos = 0;
        foreach (var pendiente in pendientes)
        {
            var eventos = await SlasDeRevision.RevisarAsync(sesiones, pendiente, ahora, ct);
            avisos += eventos.OfType<SlaPorVencer>().Count();
            incumplidos += eventos.OfType<SlaIncumplido>().Count();
        }

        return new ResultadoRevisionSlaDto(pendientes.Count, avisos, incumplidos);
    }
}

// ---------- Informe de cumplimiento ----------

/// <summary>
/// Informe de cumplimiento del SLA (OP-14, docs/11 §H8): por área, cuántas rondas de revisión tenían plazo, cuántas se resolvieron a tiempo y
/// cuántas fuera, con el promedio de días y las revisiones que hoy están vencidas. Se calcula al consultar sobre los eventos de los
/// expedientes (como el informe de estado: mismos filtros de fechas, hasta 5 000 expedientes) y se descarga en Excel.
/// </summary>
public static class InformeSlaEndpoints
{
    [WolverineGet("/api/v1/informes/sla")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<InformeSlaDto> Consultar(SesionDeEmpresa empresa, TimeProvider reloj, DateOnly? desde, DateOnly? hasta, Guid? areaId,
        Guid? tipoVinculoId, CancellationToken ct) =>
        await CalcularAsync(empresa, reloj.GetUtcNow(), desde, hasta, areaId, tipoVinculoId, ct);

    [WolverineGet("/api/v1/informes/sla/excel")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IResult> Excel(SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj, DateOnly? desde, DateOnly? hasta,
        Guid? areaId, Guid? tipoVinculoId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var informe = await CalcularAsync(empresa, ahora, desde, hasta, areaId, tipoVinculoId, ct);
        var porArea = new Hoja("Cumplimiento por área",
            ["Área", "Plazo (horas)", "Rondas con plazo", "A tiempo", "Fuera de plazo", "Cumplimiento (%)", "Promedio (días)", "Vencidos ahora"],
            informe.PorArea.Select(a => (IReadOnlyList<object?>)[a.Nombre, a.SlaHoras, a.Rondas, a.ADentroDelPlazo, a.FueraDelPlazo, a.Cumplimiento, a.PromedioDias, a.VencidosAhora]));
        var vencidos = new Hoja("Vencidos ahora", ["Folio", "Tercero", "Área", "Vencía", "Horas de retraso"],
            informe.Vencidos.Select(v => (IReadOnlyList<object?>)[v.Folio, v.Tercero, v.AreaNombre, Exportaciones.Local(v.Vence), v.HorasDeRetraso]));
        var empresaDatos = await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct);
        var nombre = $"cumplimiento-sla-{Exportaciones.Slug(empresaDatos?.Nombre ?? "empresa")}-{InformeEstadoExpedientesEndpoints.CalculadoraDeVigenciaFecha(ahora)}.xlsx";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado("InformeSla", "Excel", HojaDeCalculo.CrearLibro(porArea, vencidos), HojaDeCalculo.TipoContenido, nombre, informe.PorArea.Count), ct);
    }

    internal static async Task<InformeSlaDto> CalcularAsync(SesionDeEmpresa empresa, DateTimeOffset ahora, DateOnly? desde, DateOnly? hasta, Guid? areaId,
        Guid? tipoVinculoId, CancellationToken ct)
    {
        var filtros = InformeEstadoExpedientesEndpoints.Leer(desde, hasta, null, null, null, areaId, null, tipoVinculoId, null);
        var filas = await InformeEstadoExpedientesEndpoints.CalcularAsync(empresa, filtros, ahora, ct);
        var configuracion = await SlaDeLaEmpresa.CargarAsync(empresa.Sesion, empresa.EmpresaId, ct);

        var vencidos = new List<SlaVencidoDto>();
        foreach (var fila in filas.Where(f => f.Expediente.Estado == EstadoExpediente.EnRevision))
            foreach (var area in fila.Expediente.EtapaActiva?.Areas ?? [])
                if (Expediente.EstadoDelSla(fila.Expediente, area, ahora, configuracion.PorcentajeAviso) == EstadoDeSla.Vencido && (areaId is null || areaId == area.AreaId))
                    vencidos.Add(new SlaVencidoDto(fila.Expediente.Id, fila.Expediente.Folio, area.AreaId, area.Nombre, fila.Expediente.TerceroNombre, area.SlaVence!.Value,
                        Math.Round((ahora - area.SlaVence.Value).TotalHours, 1)));

        var porArea = filas.SelectMany(f => f.Tiempos.Areas)
            .Where(a => areaId is null || a.AreaId == areaId)
            .GroupBy(a => a.AreaId)
            .Select(g =>
            {
                var rondas = g.Sum(a => a.RondasConSla);
                var fuera = g.Sum(a => a.RondasFueraDeSla);
                var revisadas = g.Where(a => a.Rondas > 0).Select(a => a.Horas / 24).ToList();
                var primero = g.First();
                return new CumplimientoSlaDeAreaDto(g.Key, primero.Codigo, primero.Nombre, g.Select(a => a.SlaHoras).FirstOrDefault(h => h is not null), rondas, rondas - fuera, fuera,
                    Porcentaje(rondas - fuera, rondas), Estadistica.Promedio(revisadas), vencidos.Count(v => v.AreaId == g.Key));
            })
            .Where(a => a.Rondas > 0 || a.VencidosAhora > 0)
            .OrderBy(a => a.Nombre, StringComparer.CurrentCulture)
            .ToList();

        var totalRondas = porArea.Sum(a => a.Rondas);
        var totalFuera = porArea.Sum(a => a.FueraDelPlazo);
        return new InformeSlaDto(
            new ResumenSlaDto(filas.Count, totalRondas, totalRondas - totalFuera, totalFuera, Porcentaje(totalRondas - totalFuera, totalRondas), vencidos.Count),
            porArea, [.. vencidos.OrderBy(v => v.Vence)]);
    }

    private static double? Porcentaje(int parte, int total) => total == 0 ? null : Math.Round(100d * parte / total, 1);
}
