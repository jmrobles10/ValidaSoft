using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
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

// Plazo de cargue del expediente inicial (P-4-2, LT-1…LT-4). El dominio decide (Expediente.RevisarPlazo / Reabrir) y guarda en
// el expediente el próximo momento en que hay algo que hacer (ProximaRevisionDePlazo, indexado). Un job periódico (como el
// «job diario Wolverine que emite comandos idempotentes» de docs/03 §9) barre los expedientes con ese momento cumplido y pide
// por cada uno RevisarPlazoDeCargue en la cola local durable «plazos»: un recordatorio o la caducidad, con su correo al tercero
// por el outbox. La caducidad cierra el expediente en el vínculo (vuelve a Invitado) en la misma transacción; la campana a las
// áreas y el evento público expediente.caducado salen de la suscripción de eventos.

/// <summary>Barre los expedientes cuyo plazo tiene algo pendiente a la hora <see cref="Ahora"/> (o la actual), de todos los tenants o de uno.</summary>
public sealed record RevisarPlazosDeCargue(Guid? TenantId = null, DateTimeOffset? Ahora = null);

/// <summary>Recuerda o caduca un expediente (idempotente: si no hay nada pendiente no hace nada).</summary>
public sealed record RevisarPlazoDeCargue(Guid TenantId, Guid ExpedienteId, DateTimeOffset? Ahora = null);

public enum AvisoDePlazo
{
    Recordatorio,
    Caducado,
    Reabierto
}

/// <summary>Correo al tercero sobre su plazo (cola <c>correo</c>, plantilla <c>caducidad</c>); el enlace de la reapertura va cifrado (IN-3).</summary>
public sealed record CorreoDePlazoDeCarguePendiente(string Para, string Empresa, string Folio, AvisoDePlazo Aviso, DateTimeOffset PlazoHasta, int DiasRestantes,
    string? EnlaceProtegido = null, DateTimeOffset? EnlaceExpira = null, Guid? TenantId = null, Guid? EmpresaId = null, Guid? TerceroId = null);

public static class CorreoDePlazoDeCarguePendienteHandler
{
    public static async Task Handle(CorreoDePlazoDeCarguePendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.PlazoDeCargue, ct))
            return;
        var mensaje = redactor.RedactarPlazoDeCargue(correo, correo.EnlaceProtegido is null ? null : protector.Desproteger(correo.EnlaceProtegido));
        await enviador.EnviarAsync((await plantillas.AplicarAsync(mensaje, CatalogoDePlantillas.Caducidad, correo.TenantId, correo.EmpresaId,
            EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct)).DeTenant(correo.TenantId), ct);
    }
}

public enum ResultadoDePlazo
{
    SinCambios,
    Recordatorio,
    Caducado
}

public static class PlazosDeCargue
{
    /// <summary>Actor de los eventos que emite el job (historial del expediente).</summary>
    public const string Actor = "plazo-cargue";

    /// <summary>Expedientes que revisa un barrido como máximo; el resto queda para el siguiente.</summary>
    public const int MaximoPorBarrido = 2000;

    /// <summary>
    /// Expedientes con algo pendiente a <paramref name="ahora"/>, los más atrasados primero. El tenant de cada uno sale de su
    /// empresa (global): la consulta entre tenants no lo devuelve.
    /// </summary>
    public static async Task<IReadOnlyList<RevisarPlazoDeCargue>> PendientesAsync(IDocumentStore store, Guid? tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        IReadOnlyList<Expediente> expedientes;
        if (tenantId is { } tenant)
        {
            await using var consulta = store.QuerySession(tenant.ToString("N"));
            expedientes = await consulta.Query<Expediente>()
                .Where(e => e.ProximaRevisionDePlazo != null && e.ProximaRevisionDePlazo <= ahora)
                .OrderBy(e => e.ProximaRevisionDePlazo).Take(MaximoPorBarrido).ToListAsync(ct);
        }
        else
        {
            await using var consulta = store.QuerySession();
            expedientes = await consulta.Query<Expediente>()
                .Where(e => e.AnyTenant() && e.ProximaRevisionDePlazo != null && e.ProximaRevisionDePlazo <= ahora)
                .OrderBy(e => e.ProximaRevisionDePlazo).Take(MaximoPorBarrido).ToListAsync(ct);
        }

        if (expedientes.Count == 0)
            return [];

        await using var global = store.QuerySession();
        var empresas = (await global.LoadManyAsync<Empresa>(ct, expedientes.Select(e => e.EmpresaId).Distinct().ToArray())).ToDictionary(e => e.Id);
        return
        [
            .. expedientes.Where(e => empresas.ContainsKey(e.EmpresaId))
                .Select(e => new RevisarPlazoDeCargue(empresas[e.EmpresaId].TenantId, e.Id))
        ];
    }

    /// <summary>
    /// Revisa un expediente con su stream bloqueado (no compite con una carga simultánea): recordatorio (evento y correo) o
    /// caducidad (evento, cierre en el vínculo, bandeja y correo), todo en una transacción con el outbox de <paramref name="mensajes"/>.
    /// </summary>
    public static async Task<ResultadoDePlazo> RevisarAsync(ISesiones sesiones, IMessageBus mensajes, EmisorDeCertificados certificados,
        RevisarPlazoDeCargue mensaje, DateTimeOffset ahora, CancellationToken ct)
    {
        // Dentro de un manejador, Wolverine abre la sesión del outbox con el tenant del contexto del mensaje (ver CalendarioDeVigencias).
        if (mensajes is IMessageContext contexto)
            contexto.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", Actor);

        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(mensaje.ExpedienteId, ct);
        if (stream.Aggregate is not { } expediente)
            return ResultadoDePlazo.SinCambios;

        var hasta = expediente.PlazoCargueHasta;
        var eventos = expediente.RevisarPlazo(ahora);
        if (eventos.Count == 0)
            return ResultadoDePlazo.SinCambios;

        var caduca = eventos.OfType<ExpedienteCaducado>().Any();
        if (caduca)
            await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, null, certificados, ahora, ct);
        else
            stream.AppendMany(eventos);

        var vinculo = await sesion.LoadAsync<Vinculo>(expediente.VinculoId, ct);
        if (vinculo is not null && !string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
        {
            var empresa = await sesion.LoadAsync<Empresa>(expediente.EmpresaId, ct);
            await mensajes.PublishAsync(new CorreoDePlazoDeCarguePendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", expediente.Folio,
                caduca ? AvisoDePlazo.Caducado : AvisoDePlazo.Recordatorio, hasta!.Value, DiasRestantes(hasta.Value, ahora), TenantId: mensaje.TenantId,
                EmpresaId: expediente.EmpresaId, TerceroId: vinculo.TerceroId));
        }

        await sesion.SaveChangesAsync(ct);
        return caduca ? ResultadoDePlazo.Caducado : ResultadoDePlazo.Recordatorio;
    }

    /// <summary>Días que faltan, redondeados hacia arriba (con 6 horas faltantes, «1 día»).</summary>
    public static int DiasRestantes(DateTimeOffset hasta, DateTimeOffset ahora) => Math.Max(0, (int)Math.Ceiling((hasta - ahora).TotalDays));
}

public static class RevisarPlazosDeCargueHandler
{
    /// <summary>Pide la revisión de cada expediente pendiente por la cola durable (cada uno en su transacción, con sus reintentos).</summary>
    public static async Task Handle(RevisarPlazosDeCargue mensaje, IDocumentStore store, IMessageContext mensajes, TimeProvider reloj, CancellationToken ct)
    {
        foreach (var pendiente in await PlazosDeCargue.PendientesAsync(store, mensaje.TenantId, mensaje.Ahora ?? reloj.GetUtcNow(), ct))
            await mensajes.PublishAsync(pendiente with { Ahora = mensaje.Ahora });
    }
}

public static class RevisarPlazoDeCargueHandler
{
    public static Task Handle(RevisarPlazoDeCargue mensaje, ISesiones sesiones, IMessageContext mensajes, EmisorDeCertificados certificados,
        TimeProvider reloj, CancellationToken ct) =>
        PlazosDeCargue.RevisarAsync(sesiones, mensajes, certificados, mensaje, mensaje.Ahora ?? reloj.GetUtcNow(), ct);
}

/// <summary>
/// Dispara el barrido de plazos cada <c>Validasoft:PlazosDeCargue:Intervalo</c> (por omisión, una hora; <c>00:00:00</c> lo
/// apaga). Con varias réplicas cada una lo pide: el barrido es idempotente (cada expediente se revisa con su stream bloqueado).
/// </summary>
public sealed class ProgramadorDePlazosDeCargue(IServiceProvider servicios, IConfiguration configuracion, ILogger<ProgramadorDePlazosDeCargue> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:PlazosDeCargue:Intervalo", TimeSpan.FromHours(1));
        if (intervalo <= TimeSpan.Zero)
            return;

        // El primero al minuto de arrancar (lo que venció mientras la API estaba detenida) y luego cada intervalo.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1) < intervalo ? TimeSpan.FromMinutes(1) : intervalo, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new RevisarPlazosDeCargue());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir el barrido de plazos de cargue.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Reapertura del expediente caducado (empresa) y ejecución inmediata del job (Plataforma).</summary>
public static class PlazosDeCargueEndpoints
{
    /// <summary>
    /// Reabre un expediente caducado (P-4-2, LT-3): vuelve al estado en que caducó con el plazo del flujo completo, el vínculo
    /// vuelve a <c>EnValidacion</c> y el tercero recibe un correo con un enlace de acceso nuevo (reinvitación). Lo que el tercero
    /// subió mientras estaba caducado se adjunta enseguida. 409 <c>estado-invalido</c> si no está caducado.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/reabrir")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<ExpedienteDto> Reabrir(Guid id, ReabrirExpedienteDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var detalle = await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var expediente = stream.Aggregate!;
            var reabierto = expediente.Reabrir(dto.Justificacion, ahora);

            var vinculoStream = await empresa.VinculoParaEscribirAsync(expediente.VinculoId, null, ct);
            var vinculo = vinculoStream.Aggregate!;
            vinculoStream.AppendOne(vinculo.VincularExpediente(expediente.Id, expediente.Tipo, expediente.Folio, ahora));
            stream.AppendOne(reabierto);
            foreach (var fila in ProyeccionBandejaRevision.Filas(Expediente.Reproducir(expediente, [reabierto])))
                empresa.Sesion.Store(fila);

            var datosEmpresa = await empresa.Sesion.LoadAsync<Empresa>(expediente.EmpresaId, ct);
            if (!string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            {
                var tipo = await empresa.Sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
                var (enlace, token) = EnlaceDeAcceso.Emitir(datosEmpresa?.TenantId, vinculo.Id, vinculo.TerceroId, vinculo.CorreoTercero,
                    ahora + EnlaceDeAcceso.VigenciaObservacion, tipo?.ExigeOtpEnEnlace ?? false, ahora);
                empresa.Sesion.Store(enlace);
                await mensajes.PublishAsync(new CorreoDePlazoDeCarguePendiente(enlace.CorreoDestino, datosEmpresa?.Nombre ?? "La empresa", expediente.Folio,
                    AvisoDePlazo.Reabierto, reabierto.PlazoHasta, PlazosDeCargue.DiasRestantes(reabierto.PlazoHasta, ahora), protector.Proteger(token),
                    enlace.Expira, datosEmpresa?.TenantId, expediente.EmpresaId, vinculo.TerceroId));
            }

            await mensajes.PublishAsync(new SincronizarRequisitosDelVinculo(contexto.TenantRequerido(), vinculo.Id, contexto.ActorTipo.ToString(),
                contexto.ActorId));
            await empresa.Sesion.SaveChangesAsync(ct);
            return await ExpedientesEndpoints.DetalleAsync(empresa, id, ct);
        });
        Peticiones.EscribirETag(http, detalle.Version);
        return detalle;
    }

    /// <summary>
    /// Corre ya el job de plazos (Plataforma) y espera su resultado: de un tenant o de todos. <c>fecha</c> simula «ahora» y solo
    /// se admite en Development (pruebas de punta a punta; 422 <c>fecha-simulada-no-permitida</c> en otro entorno).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/plazos-de-cargue/revisar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoRevisionPlazosDto> Revisar(RevisarPlazosDto dto, IDocumentStore store, ISesiones sesiones, IWolverineRuntime runtime,
        EmisorDeCertificados certificados, IHostEnvironment entorno, TimeProvider reloj, CancellationToken ct)
    {
        if (dto.Fecha is not null && !entorno.IsDevelopment())
            throw ReglaDeNegocioException.Validacion("fecha-simulada-no-permitida", "La fecha simulada solo se admite en Development.");

        var ahora = dto.Fecha ?? reloj.GetUtcNow();
        var pendientes = await PlazosDeCargue.PendientesAsync(store, dto.TenantId, ahora, ct);
        int recordatorios = 0, caducados = 0;
        foreach (var pendiente in pendientes)
        {
            // Un contexto de mensajería por expediente: cada uno con su transacción y su outbox (como el importador del Portal).
            var resultado = await PlazosDeCargue.RevisarAsync(sesiones, new MessageContext(runtime), certificados, pendiente, ahora, ct);
            if (resultado == ResultadoDePlazo.Recordatorio)
                recordatorios++;
            else if (resultado == ResultadoDePlazo.Caducado)
                caducados++;
        }

        return new ResultadoRevisionPlazosDto(pendientes.Count, recordatorios, caducados);
    }
}
