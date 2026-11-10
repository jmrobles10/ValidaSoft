using JasperFx.Events;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Invitaciones;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>Evento público <c>invitaciones.lote_terminado</c> (docs/05 §7, LT-9): el lote, su estado final y sus totales.</summary>
public sealed class MapeadorDeLotes : IMapeadorDeEventos
{
    public IReadOnlyList<TipoEventoPublicoDto> Catalogo { get; } =
        [new("invitaciones.lote_terminado", "Terminó un lote de invitación masiva (con sus totales por estado).")];

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (evento.Data is not LoteTerminado terminado || !Guid.TryParse(evento.TenantId, out var tenantId))
            return null;
        var lote = await contexto.LoteAsync(tenantId, evento.StreamId, ct);
        return lote is null
            ? null
            : new EventoPublicoMapeado("invitaciones.lote_terminado", tenantId, lote.EmpresaId,
                new DatosLoteTerminadoDto(lote.Id, lote.Nombre, terminado.Estado.ToString(), terminado.Totales.ADto()));
    }
}
