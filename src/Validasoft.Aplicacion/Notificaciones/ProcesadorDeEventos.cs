using JasperFx.Events;
using Marten;
using Microsoft.Extensions.Configuration;
using Validasoft.Contratos;
using Validasoft.Dominio.Integraciones;
using Wolverine;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>Los mapeadores registrados y su catálogo unido (valida tipos de las suscripciones y responde <c>GET /eventos/tipos</c>).</summary>
public sealed class CatalogoDeEventos(IEnumerable<IMapeadorDeEventos> mapeadores)
{
    public IReadOnlyList<IMapeadorDeEventos> Mapeadores { get; } = [.. mapeadores];

    public IReadOnlyList<TipoEventoPublicoDto> Tipos => [.. Mapeadores.SelectMany(m => m.Catalogo).DistinctBy(t => t.Tipo)];

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        foreach (var mapeador in Mapeadores)
        {
            if (await mapeador.MapearAsync(evento, contexto, ct) is { } mapeado)
                return mapeado;
        }

        return null;
    }
}

/// <summary>
/// Procesa un lote de eventos de dominio en el orden del event store (NW-3): registra los eventos públicos, crea una
/// entrega por cada suscripción que los acepta y encola su envío, y planea las notificaciones a personas (campana y
/// correos). Todo se escribe con las <paramref name="operaciones"/> y el bus de la suscripción de Marten, que confirma
/// en una sola transacción con el avance de la suscripción (outbox de Wolverine): un lote nunca queda a medias, así que
/// no se pierden ni se duplican eventos, entregas ni correos. Si un lote se reprocesa (reconstrucción manual), las
/// entregas y notificaciones ya existentes no se tocan.
/// </summary>
public sealed class ProcesadorDeEventos(CatalogoDeEventos catalogo, PlanificadorDeNotificaciones planificador, TimeProvider reloj,
    IConfiguration configuracion)
{
    /// <summary>
    /// Hechos más viejos que esto no generan notificaciones a personas (NW-3): al encender la suscripción sobre una base con
    /// historia, los eventos públicos se registran pero nadie recibe correos de hace meses.
    /// </summary>
    private readonly TimeSpan _antiguedadMaxima =
        TimeSpan.FromHours(Math.Max(1, configuracion.GetValue("Validasoft:Notificaciones:HorasAntiguedadMaxima", 24)));

    public async Task ProcesarAsync(IReadOnlyList<IEvent> eventos, IDocumentOperations operaciones, IMessageBus bus, IContextoDeEventos contexto,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var entregas = new List<(Guid TenantId, EntregaWebhook Entrega)>();
        var notificaciones = new List<(Guid TenantId, Notificacion Notificacion)>();
        var correos = new List<object>();
        var cambios = new List<AvisoDeCambio>();

        foreach (var evento in eventos)
        {
            if (await catalogo.MapearAsync(evento, contexto, ct) is { } mapeado)
            {
                var publico = EventoPublico.Desde(evento, mapeado, ahora);
                operaciones.Store(publico);
                foreach (var suscripcion in (await contexto.SuscripcionesActivasAsync(mapeado.TenantId, ct)).Where(s => s.Acepta(publico.Tipo, publico.EmpresaId)))
                    entregas.Add((mapeado.TenantId, EntregaWebhook.Nueva(suscripcion, publico.Id, publico.Tipo, publico.Cursor, publico.EmpresaId, ahora)));
            }

            if (ahora - evento.Timestamp <= _antiguedadMaxima)
            {
                // Orden de la campana: el instante del hecho, desempatado por su secuencia (eventos de una misma transacción).
                var plan = await planificador.PlanearAsync(evento, contexto, evento.Timestamp.UtcTicks + (evento.Sequence % 10_000), ct);
                notificaciones.AddRange(plan.Campana);
                correos.AddRange(plan.Correos);
                cambios.AddRange(plan.Cambios ?? []);
            }
        }

        foreach (var grupo in entregas.GroupBy(e => e.TenantId))
        {
            var existentes = await contexto.ExistentesAsync<EntregaWebhook>(grupo.Key, [.. grupo.Select(e => e.Entrega.Id)], ct);
            var delTenant = operaciones.ForTenant(grupo.Key.ToString("N"));
            foreach (var (_, entrega) in grupo.Where(e => !existentes.Contains(e.Entrega.Id)))
            {
                delTenant.Store(entrega);
                await bus.PublishAsync(new EnviarEntregaWebhook(grupo.Key, entrega.Id, 1));
            }
        }

        foreach (var grupo in notificaciones.GroupBy(n => n.TenantId))
        {
            var existentes = await contexto.ExistentesAsync<Notificacion>(grupo.Key, [.. grupo.Select(n => n.Notificacion.Id)], ct);
            var delTenant = operaciones.ForTenant(grupo.Key.ToString("N"));
            foreach (var (_, notificacion) in grupo.Where(n => !existentes.Contains(n.Notificacion.Id)))
                delTenant.Store(notificacion);
        }

        // Avisos de «cambió X» para las pantallas abiertas (NT-12): uno por destinatario y recurso del lote; solo los recientes (un
        // stream que se conecta ahora empieza desde ahora) y se borran los viejos, que ya nadie leerá.
        foreach (var aviso in PlanificadorDeCambios.Coalescer(cambios).Where(a => ahora - a.CreadaEn <= AvisoDeCambio.Vida))
            operaciones.Store(aviso);
        var vencidos = ahora - AvisoDeCambio.Vida;
        operaciones.DeleteWhere<AvisoDeCambio>(a => a.CreadaEn < vencidos);

        foreach (var correo in correos)
            await bus.PublishAsync(correo);
    }
}
