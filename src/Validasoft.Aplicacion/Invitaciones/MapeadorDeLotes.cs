using JasperFx.Events;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Invitaciones;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>
/// Eventos públicos del lote de invitación masiva: <c>invitaciones.lote_terminado</c> (docs/05 §7, LT-9), y los de su aprobación previa a invitar (decisión AI):
/// <c>invitaciones.lote_por_aprobar</c>, <c>invitaciones.lote_aprobado</c> e <c>invitaciones.lote_rechazado</c>.
/// </summary>
public sealed class MapeadorDeLotes : IMapeadorDeEventos
{
    public IReadOnlyList<TipoEventoPublicoDto> Catalogo { get; } =
    [
        new("invitaciones.lote_terminado", "Terminó un lote de invitación masiva (con sus totales por estado)."),
        new("invitaciones.lote_por_aprobar", "Un lote de invitación masiva quedó por aprobar: incluye un tipo de vínculo que exige aprobación y no se envía ninguna invitación hasta que otra persona lo decida."),
        new("invitaciones.lote_aprobado", "Otra persona aprobó el lote completo: empieza a procesarse y a enviar sus invitaciones."),
        new("invitaciones.lote_rechazado", "Otra persona rechazó el lote completo, con su motivo: queda cancelado y no se envía ninguna invitación.")
    ];

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (evento.Data is not (LoteTerminado or LoteDeInvitacionCreado { PorAprobar: true } or LoteAprobado or LoteRechazado) || !Guid.TryParse(evento.TenantId, out var tenantId))
            return null;

        var lote = await contexto.LoteAsync(tenantId, evento.StreamId, ct);
        if (lote is null)
            return null;

        // Los totales y el estado son los del hecho (el lote cargado ya pudo avanzar), no los de ahora.
        return evento.Data switch
        {
            LoteTerminado terminado => new EventoPublicoMapeado("invitaciones.lote_terminado", tenantId, lote.EmpresaId,
                new DatosLoteTerminadoDto(lote.Id, lote.Nombre, terminado.Estado.ToString(), terminado.Totales.ADto())),
            LoteDeInvitacionCreado creado => new EventoPublicoMapeado("invitaciones.lote_por_aprobar", tenantId, lote.EmpresaId,
                new DatosLoteDecididoDto(lote.Id, lote.Nombre, nameof(EstadoLote.PorAprobar), creado.Totales.ADto())),
            LoteAprobado => new EventoPublicoMapeado("invitaciones.lote_aprobado", tenantId, lote.EmpresaId,
                new DatosLoteDecididoDto(lote.Id, lote.Nombre, nameof(EstadoLote.Procesando), lote.Totales.ADto())),
            LoteRechazado rechazado => new EventoPublicoMapeado("invitaciones.lote_rechazado", tenantId, lote.EmpresaId,
                new DatosLoteDecididoDto(lote.Id, lote.Nombre, nameof(EstadoLote.Cancelado), rechazado.Totales.ADto(), rechazado.Motivo)),
            _ => null
        };
    }
}
