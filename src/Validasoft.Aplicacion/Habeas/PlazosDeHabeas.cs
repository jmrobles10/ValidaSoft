using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>Pide el barrido de plazos del habeas data: una revisión por cada solicitud abierta (cola durable <c>habeas</c>, OD-3).</summary>
public sealed record RevisarSolicitudesHabeas(DateTimeOffset? Ahora = null);

/// <summary>Revisa el plazo de una solicitud: si faltan dos días hábiles o ya venció, avisa a quien responde (una vez por cada caso).</summary>
public sealed record RevisarSolicitudHabeas(Guid SolicitudId, DateTimeOffset? Ahora = null);

/// <summary>Elimina del almacenamiento los ZIP de exportaciones cuyo plazo de descarga venció (OD-10).</summary>
public sealed record LimpiarExportacionesDeBoveda(DateTimeOffset? Ahora = null);

/// <summary>
/// Alertas de plazo del habeas data (OD-3): <b>PorVencer</b> cuando faltan dos días hábiles o menos y <b>Vencida</b> pasado el plazo
/// legal. Avisa a quien debe responder —los administradores y el oficial de cumplimiento de la empresa, con campana y correo, o los
/// correos de la plataforma (<c>Validasoft:Habeas:CorreosPlataforma</c>)— y deja el aviso en el stream de la solicitud.
/// </summary>
public static class PlazosDeHabeas
{
    /// <summary>Actor de los eventos que emite el barrido.</summary>
    public const string Actor = "plazo-habeas";

    public static async Task<IReadOnlyList<Guid>> AbiertasAsync(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaBoveda();
        return [.. await consulta.Query<SolicitudHabeas>().Where(s => s.Estado == EstadoDeSolicitudHabeas.Recibida).Select(s => s.Id).ToListAsync(ct)];
    }

    /// <summary>Revisa una solicitud y avisa si toca. Devuelve el aviso enviado, o nulo si no había nada que avisar.</summary>
    public static async Task<AvisoDePlazoHabeas?> RevisarAsync(ISesiones sesiones, IWolverineRuntime runtime, IConfiguration configuracion, Guid solicitudId,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        SolicitudHabeas? solicitud;
        await using (var consulta = sesiones.ConsultaBoveda())
            solicitud = await consulta.LoadAsync<SolicitudHabeas>(solicitudId, ct);
        if (solicitud?.AvisoPendiente(hoy) is not { } aviso)
            return null;

        var derecho = RedaccionDeCorreosDeHabeas.Derecho(solicitud.Tipo);
        var titulo = aviso == AvisoDePlazoHabeas.Vencida
            ? $"Venció el plazo de una solicitud de {derecho}"
            : $"Vence pronto una solicitud de {derecho}";
        var mensaje = aviso == AvisoDePlazoHabeas.Vencida
            ? $"La solicitud {solicitud.Radicado} ({derecho}) venció el {solicitud.VenceEl:dd/MM/yyyy}: ya pasó el plazo legal de {solicitud.DiasHabiles} días hábiles. Responde cuanto antes."
            : $"La solicitud {solicitud.Radicado} ({derecho}) vence el {solicitud.VenceEl:dd/MM/yyyy}: {Math.Max(0, solicitud.DiasHabilesRestantes(hoy))} días hábiles para responderla.";

        string? nombreDeLaEmpresa = null;
        if (solicitud.EmpresaId is { } empresaId && solicitud.TenantId is { } tenantId)
        {
            await using (var consulta = sesiones.ConsultaGlobal())
                nombreDeLaEmpresa = (await consulta.LoadAsync<Dominio.Plataforma.Empresa>(empresaId, ct))?.Nombre;
            // Cada transacción con su propio contexto de mensajería: la del aviso en el tenant del grupo y la del stream de la bóveda.
            await AvisosAAdministradores.AvisarAsync(sesiones, new MessageContext(runtime), tenantId, new DestinatariosDeAviso(empresaId, ConOficial: true), TiposNotificacion.HabeasPlazo,
                titulo, mensaje, $"/e/{empresaId}/habeas-data", $"habeas-{aviso}:{solicitud.Id:N}", nombreDeLaEmpresa, ahora, ct);
        }

        var mensajes = new MessageContext(runtime);
        await using var sesion = sesiones.Boveda(mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", Actor);
        var stream = await sesion.Events.FetchForExclusiveWriting<SolicitudHabeas>(solicitudId, ct);
        if (stream.Aggregate?.AvisoPendiente(hoy) != aviso)
            return null; // otro nodo se adelantó
        stream.AppendOne(new AvisoDePlazoDeSolicitudEnviado(aviso, ahora));

        // La plataforma no tiene campana: sus avisos salen por correo a quienes configure.
        if (solicitud.EsPlataforma)
        {
            foreach (var correo in CorreosDeLaPlataforma(configuracion))
                await mensajes.PublishAsync(new CorreoDeNotificacionPendiente(correo, "equipo de Plataforma", titulo, [mensaje], null));
        }

        await sesion.SaveChangesAsync(ct);
        return aviso;
    }

    public static IReadOnlyList<string> CorreosDeLaPlataforma(IConfiguration configuracion) =>
    [
        .. (configuracion["Validasoft:Habeas:CorreosPlataforma"] ?? string.Empty)
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => c.Contains('@')).Distinct(StringComparer.OrdinalIgnoreCase)
    ];
}

public static class RevisarSolicitudesHabeasHandler
{
    public static async Task Handle(RevisarSolicitudesHabeas mensaje, ISesiones sesiones, IMessageContext mensajes, CancellationToken ct)
    {
        foreach (var id in await PlazosDeHabeas.AbiertasAsync(sesiones, ct))
            await mensajes.PublishAsync(new RevisarSolicitudHabeas(id, mensaje.Ahora));
    }
}

public static class RevisarSolicitudHabeasHandler
{
    public static Task Handle(RevisarSolicitudHabeas mensaje, ISesiones sesiones, IWolverineRuntime runtime, IConfiguration configuracion, TimeProvider reloj,
        CancellationToken ct) =>
        PlazosDeHabeas.RevisarAsync(sesiones, runtime, configuracion, mensaje.SolicitudId, mensaje.Ahora ?? reloj.GetUtcNow(), ct);
}

/// <summary>
/// Dispara el barrido de plazos del habeas data y la limpieza de exportaciones vencidas cada <c>Validasoft:Habeas:Intervalo</c> (por
/// omisión, una hora; <c>00:00:00</c> lo apaga). Con varias réplicas cada una lo pide: cada revisión es idempotente.
/// </summary>
public sealed class ProgramadorDeHabeas(IServiceProvider servicios, IConfiguration configuracion, ILogger<ProgramadorDeHabeas> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:Habeas:Intervalo", TimeSpan.FromHours(1));
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
                    var bus = alcance.ServiceProvider.GetRequiredService<IMessageBus>();
                    await bus.PublishAsync(new RevisarSolicitudesHabeas());
                    await bus.PublishAsync(new LimpiarExportacionesDeBoveda());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir el barrido de plazos del habeas data.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Ejecución inmediata del barrido (Plataforma): para operar y para las pruebas de punta a punta.</summary>
public static class PlazosDeHabeasEndpoints
{
    /// <summary>
    /// Revisa ya los plazos de todas las solicitudes abiertas y avisa lo que toque. <c>fecha</c> simula «ahora» y solo se admite en
    /// Development (422 <c>fecha-simulada-no-permitida</c> en otro entorno).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/habeas/revisar-plazos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoDeRevisionDeHabeasDto> Revisar(RevisarPlazosDeHabeasDto dto, ISesiones sesiones, IWolverineRuntime runtime, IConfiguration configuracion,
        IHostEnvironment entorno, TimeProvider reloj, CancellationToken ct)
    {
        if (dto.Fecha is not null && !entorno.IsDevelopment())
            throw ReglaDeNegocioException.Validacion("fecha-simulada-no-permitida", "La fecha simulada solo se admite en Development.");

        var ahora = dto.Fecha ?? reloj.GetUtcNow();
        int revisadas = 0, porVencer = 0, vencidas = 0;
        foreach (var id in await PlazosDeHabeas.AbiertasAsync(sesiones, ct))
        {
            revisadas++;
            var aviso = await PlazosDeHabeas.RevisarAsync(sesiones, runtime, configuracion, id, ahora, ct);
            if (aviso == AvisoDePlazoHabeas.PorVencer)
                porVencer++;
            else if (aviso == AvisoDePlazoHabeas.Vencida)
                vencidas++;
        }

        return new ResultadoDeRevisionDeHabeasDto(revisadas, porVencer, vencidas);
    }
}
