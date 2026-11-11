using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Dominio.Campanias;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Campanias;

/// <summary>Un vínculo que cumple el filtro de la campaña y qué se haría con él (<see cref="Accion"/>: <c>Abrir</c>, <c>Recordar</c> u <c>Omitir</c>).</summary>
public sealed record CandidatoDeCampania(Vinculo Vinculo, Tercero? Tercero, TipoVinculo? Tipo, DateOnly? VenceEn, string Accion, string? Motivo);

/// <summary>
/// Quiénes entran a una campaña (OP-1): los vínculos aprobados alguna vez de la empresa, en alguno de los estados del filtro, cuyo primer vencimiento —el
/// de la vigencia del vínculo o el de la versión actual de alguno de sus documentos para esta empresa— cae en los próximos N días (lo ya vencido también).
/// Lo que se haría con cada uno se decide por su expediente abierto; si hay <i>algo que renovar</i> lo decide el proceso al abrir cada uno.
/// </summary>
public static class SeleccionDeCampania
{
    public const int MaximoPorOmision = 5000;

    public static async Task<IReadOnlyList<CandidatoDeCampania>> SeleccionarAsync(IQuerySession sesion, IQuerySession boveda, Guid empresaId, FiltroDeCampania filtro,
        DateOnly hoy, int maximo, CancellationToken ct)
    {
        var estados = filtro.Validar().Estados.ToArray();
        var consulta = sesion.Query<Vinculo>().Where(v => v.EmpresaId == empresaId && v.TuvoAprobacion && v.Estado.IsOneOf(estados));
        if (filtro.TipoVinculoId is { } tipoVinculo)
            consulta = consulta.Where(v => v.TipoVinculoId == tipoVinculo);
        var vinculos = await consulta.OrderBy(v => v.Id).Take(maximo * 4 + 1).ToListAsync(ct);
        if (vinculos.Count == 0)
            return [];

        // El primer vencimiento de cada vínculo: la vigencia del propio vínculo o la de un documento compartido con él.
        var porVinculo = new Dictionary<Guid, DateOnly>();
        foreach (var v in vinculos.Where(v => v.VigenteHasta is not null))
            porVinculo[v.Id] = v.VigenteHasta!.Value;
        var terceroIds = vinculos.Select(v => v.TerceroId).Distinct().ToArray();
        var ids = vinculos.Select(v => v.Id).ToHashSet();
        foreach (var lote in terceroIds.Chunk(500))
        {
            foreach (var documento in await boveda.Query<Documento>().Where(d => d.TerceroId.IsOneOf(lote)).ToListAsync(ct))
            {
                if (documento.VersionActual is not { } numero)
                    continue;
                foreach (var vigencia in documento.VersionRequerida(numero).Vigencias.Where(g => g.VigenteHasta is not null && ids.Contains(g.VinculoId)))
                    if (!porVinculo.TryGetValue(vigencia.VinculoId, out var actual) || vigencia.VigenteHasta!.Value < actual)
                        porVinculo[vigencia.VinculoId] = vigencia.VigenteHasta!.Value;
            }
        }

        var elegidos = vinculos.Where(v => porVinculo.TryGetValue(v.Id, out var vence) && vence.DayNumber - hoy.DayNumber <= filtro.DiasParaVencer)
            .OrderBy(v => porVinculo[v.Id]).ThenBy(v => v.Id).ToList();
        if (elegidos.Count > maximo)
            throw ReglaDeNegocioException.Validacion("campania-demasiado-grande",
                $"Una campaña admite hasta {maximo} vínculos y este filtro trae más: acótalo por tipo de vínculo, estado o días.");

        var terceros = (await sesion.LoadManyAsync<Tercero>(ct, elegidos.Select(v => v.TerceroId).Distinct().ToArray())).ToDictionary(t => t.Id);
        var tipos = (await sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);
        return
        [
            .. elegidos.Select(v =>
            {
                var (accion, motivo) = v.ExpedienteAbiertoId is null ? ("Abrir", (string?)null)
                    : v.TieneRenovacionAbierta ? ("Recordar", "Ya tiene su renovación abierta: se le recuerda al tercero.")
                    : ("Omitir", $"Tiene un expediente de {Tipo(v.ExpedienteAbiertoTipo)} abierto: se renueva cuando se cierre.");
                return new CandidatoDeCampania(v, terceros.GetValueOrDefault(v.TerceroId), tipos.GetValueOrDefault(v.TipoVinculoId), porVinculo[v.Id], accion, motivo);
            })
        ];
    }

    private static string Tipo(TipoExpediente? tipo) => tipo switch
    {
        TipoExpediente.Renovacion => "renovación",
        TipoExpediente.Actualizacion => "actualización",
        _ => "validación"
    };
}

/// <summary>
/// Procesa la siguiente tanda de filas pendientes de la campaña (cola local durable y secuencial <c>campanias</c>, OP-3). Lleva la ejecución vigente:
/// si se pausó, se canceló o se reanudó con otra, el mensaje viejo no hace nada.
/// </summary>
public sealed record ProcesarTandaDeCampania(Guid TenantId, Guid CampaniaId, Guid Ejecucion);

/// <summary>
/// Procesa una campaña de renovación por tandas (el patrón del lote de invitación, LT-8): <c>Validasoft:Campanias:TamanoTanda</c> filas pendientes (50), cada
/// una en <b>su propia transacción</b> (el expediente de renovación, el aviso al tercero por el outbox y el estado de la fila juntos: idempotente por fila); al final
/// registra el avance y, si quedan pendientes y sigue en proceso, se encola a sí misma. Una regla de negocio deja la fila <c>Fallida</c> con su código; un error
/// inesperado se reintenta dos veces antes.
/// </summary>
public static class ProcesarTandaDeCampaniaHandler
{
    public const int TandaPorOmision = 50;
    private const int Intentos = 3;

    public static async Task Handle(ProcesarTandaDeCampania mensaje, IDocumentStore store, ISesiones sesiones, IWolverineRuntime runtime, IMessageContext mensajes,
        IProtectorDeSecretos protector, EmisorDeCertificados certificados, IConfiguration configuracion, TimeProvider reloj, ILogger<CampaniaDeRenovacion> logger,
        CancellationToken ct)
    {
        var tamano = Math.Clamp(configuracion.GetValue("Validasoft:Campanias:TamanoTanda", TandaPorOmision), 1, 500);
        CampaniaDeRenovacion? campania;
        IReadOnlyList<FilaDeCampania> filas;
        await using (var consulta = store.QuerySession(mensaje.TenantId.ToString("N")))
        {
            campania = await consulta.LoadAsync<CampaniaDeRenovacion>(mensaje.CampaniaId, ct);
            if (campania is not { Estado: EstadoCampania.Procesando } || campania.Ejecucion != mensaje.Ejecucion)
                return;
            var campaniaId = campania.Id;
            filas = await consulta.Query<FilaDeCampania>().Where(f => f.CampaniaId == campaniaId && f.Estado == EstadoFilaCampania.Pendiente)
                .OrderBy(f => f.N).Take(tamano).ToListAsync(ct);
        }

        for (var i = 0; i < filas.Count; i++)
        {
            // Cada 10 filas mira si la pausaron o cancelaron: la pausa no espera a que termine la tanda.
            if (i > 0 && i % 10 == 0 && !await SigueVigenteAsync(store, mensaje, ct))
                break;
            await ProcesarFilaAsync(sesiones, runtime, protector, certificados, mensaje.TenantId, campania, filas[i], reloj, logger, ct);
        }

        mensajes.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "campania-de-renovacion");
        var stream = await sesion.Events.FetchForExclusiveWriting<CampaniaDeRenovacion>(mensaje.CampaniaId, ct);
        if (stream.Aggregate is not { } actual)
            return;
        var eventos = actual.RegistrarAvance(await ContarAsync(sesion, actual.Id, ct), reloj.GetUtcNow());
        stream.AppendMany(eventos);
        if (eventos.OfType<CampaniaTerminada>().Any() is false && actual is { Estado: EstadoCampania.Procesando } && actual.Ejecucion == mensaje.Ejecucion)
            await mensajes.PublishAsync(mensaje);
        await sesion.SaveChangesAsync(ct);
    }

    private static async Task<bool> SigueVigenteAsync(IDocumentStore store, ProcesarTandaDeCampania mensaje, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(mensaje.TenantId.ToString("N"));
        return await consulta.LoadAsync<CampaniaDeRenovacion>(mensaje.CampaniaId, ct) is { Estado: EstadoCampania.Procesando } c && c.Ejecucion == mensaje.Ejecucion;
    }

    /// <summary>Conteo de filas por estado (sin cargarlas).</summary>
    public static async Task<TotalesDeCampania> ContarAsync(IQuerySession sesion, Guid campaniaId, CancellationToken ct)
    {
        async Task<int> Contar(EstadoFilaCampania estado) => await sesion.Query<FilaDeCampania>().CountAsync(f => f.CampaniaId == campaniaId && f.Estado == estado, ct);
        var pendientes = await Contar(EstadoFilaCampania.Pendiente);
        var exitosas = await Contar(EstadoFilaCampania.Exitosa);
        var fallidas = await Contar(EstadoFilaCampania.Fallida);
        var omitidas = await Contar(EstadoFilaCampania.Omitida);
        return new TotalesDeCampania(pendientes + exitosas + fallidas + omitidas, pendientes, exitosas, fallidas, omitidas);
    }

    private static async Task ProcesarFilaAsync(ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, EmisorDeCertificados certificados,
        Guid tenantId, CampaniaDeRenovacion campania, FilaDeCampania fila, TimeProvider reloj, ILogger logger, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            var ahora = reloj.GetUtcNow();
            try
            {
                // Un contexto de mensajería por fila: el correo al tercero sale solo si su transacción se confirma.
                var bus = new MessageContext(runtime) { TenantId = tenantId.ToString("N") };
                await using var sesion = sesiones.ParaTenant(tenantId, bus);
                // En nombre de quien creó la campaña: el historial del expediente y del vínculo lo ven como su renovación.
                sesion.SetHeader("actorTipo", campania.CreadaPor.Tipo.ToString());
                sesion.SetHeader("actorId", campania.CreadaPor.Id);
                sesion.SetHeader("campaniaId", campania.Id.ToString());
                var actual = await sesion.LoadAsync<FilaDeCampania>(fila.Id, ct);
                if (actual is not { Estado: EstadoFilaCampania.Pendiente })
                    return;

                await RenovarAsync(sesion, bus, protector, certificados, campania, actual, ahora, ct);
                sesion.Store(actual);
                await sesion.SaveChangesAsync(ct);
                return;
            }
            catch (ReglaDeNegocioException e)
            {
                await MarcarFallidaAsync(sesiones, tenantId, fila.Id, e.Codigo, e.Message, ahora, ct);
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException && intento < Intentos)
            {
                // Una carrera (otro proceso escribió en el vínculo): se repite con datos frescos.
                logger.LogDebug(e, "Reintento de la fila {Fila} de la campaña {Campania}.", fila.N, campania.Id);
                await Task.Delay(Random.Shared.Next(20, 100 * intento), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "La fila {Fila} de la campaña {Campania} falló.", fila.N, campania.Id);
                await MarcarFallidaAsync(sesiones, tenantId, fila.Id, "error-inesperado", "No se pudo procesar el vínculo; reinténtalo.", ahora, ct);
                return;
            }
        }
    }

    private static async Task MarcarFallidaAsync(ISesiones sesiones, Guid tenantId, string filaId, string codigo, string mensaje, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(tenantId);
        if (await sesion.LoadAsync<FilaDeCampania>(filaId, ct) is not { Estado: EstadoFilaCampania.Pendiente } fila)
            return;
        fila.MarcarFallida(codigo, mensaje, ahora);
        sesion.Store(fila);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Abre la renovación del vínculo (la misma del pre-vencimiento, VV-4: las secciones que el flujo pide y la empresa ya aceptó, para ratificarlas o
    /// actualizarlas, y los documentos obligatorios que ya no están vigentes) y le avisa al tercero con su enlace; si ya la tenía abierta, solo se lo recuerda.
    /// Lo que ya no aplica (cambió desde la vista previa) queda <c>Omitida</c> con su motivo.
    /// </summary>
    internal static async Task RenovarAsync(IDocumentSession sesion, IMessageBus bus, IProtectorDeSecretos protector, EmisorDeCertificados certificados,
        CampaniaDeRenovacion campania, FilaDeCampania fila, DateTimeOffset ahora, CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForExclusiveWriting<Vinculo>(fila.VinculoId, ct);
        if (stream.Aggregate is not { } vinculo || vinculo.EmpresaId != campania.EmpresaId)
        {
            fila.MarcarOmitida("El vínculo ya no existe.", ahora);
            return;
        }

        if (!vinculo.TuvoAprobacion || vinculo.Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
        {
            fila.MarcarOmitida($"El vínculo está {vinculo.Estado}: no se renueva.", ahora);
            return;
        }

        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var hasta = fila.VenceEn ?? hoy;
        if (vinculo.ExpedienteAbiertoId is { } abierto)
        {
            if (!vinculo.TieneRenovacionAbierta)
            {
                fila.MarcarOmitida($"Tiene un expediente de {vinculo.ExpedienteAbiertoTipo} abierto: se renueva cuando se cierre.", ahora);
                return;
            }

            await VigenciasDeVinculos.AvisarAlTerceroAsync(sesion, bus, protector, vinculo, AvisoDeVigencia.Renovacion, hasta, hasta.DayNumber - hoy.DayNumber, ahora, ct);
            var existente = await sesion.LoadAsync<Expediente>(abierto, ct);
            fila.MarcarExitosa(AccionFilaCampania.Recordada, abierto, existente?.Folio, "Ya tenía su renovación abierta: se le recordó al tercero.", ahora);
            return;
        }

        if (await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct) is not { } flujo)
        {
            fila.MarcarOmitida("No hay un flujo publicado para este vínculo: no se puede abrir su renovación.", ahora);
            return;
        }

        var requisitos = await VigenciasDeVinculos.RequisitosDeRenovacionAsync(sesion, vinculo, flujo.Version, ahora, ct);
        if (requisitos.Count == 0)
        {
            fila.MarcarOmitida("Nada por renovar: sus secciones y documentos obligatorios están al día.", ahora);
            return;
        }

        var expediente = await ProcesoDeValidacion.AbrirAsync(sesion, stream, flujo, null, certificados, ahora, ct, tipoExpediente: TipoExpediente.Renovacion,
            soloRequisitos: requisitos, actor: new ActorDeExpediente(campania.CreadaPor.Tipo, campania.CreadaPor.Id), exigirRatificacion: true);
        if (!expediente.EstaAbierto)
        {
            fila.MarcarExitosa(AccionFilaCampania.RenovacionAbierta, expediente.Id, expediente.Folio, "Se aprobó sola: no había nada que el tercero tuviera que hacer.", ahora);
            return;
        }

        await VigenciasDeVinculos.AvisarAlTerceroAsync(sesion, bus, protector, vinculo, AvisoDeVigencia.Renovacion, hasta, hasta.DayNumber - hoy.DayNumber, ahora, ct);
        fila.MarcarExitosa(AccionFilaCampania.RenovacionAbierta, expediente.Id, expediente.Folio,
            string.IsNullOrWhiteSpace(vinculo.CorreoTercero) ? "El vínculo no tiene correo: solo se le avisó en la campana de su portal." : null, ahora);
    }
}
