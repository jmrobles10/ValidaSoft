using JasperFx.Events;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Evento público de la ficha maestra (F3, FM-32): <c>seccion.aceptada</c> sale de <see cref="SeccionAceptadaPorEmpresa"/> (stream
/// <c>Expediente-{id}</c>, misma transacción que la aprobación). Se registra como otro <see cref="IMapeadorDeEventos"/> (NW-2).
/// El contenido es el de la versión aceptada, que es inmutable, así que un reproceso publica lo mismo; solo los nombres de
/// los catálogos se leen al mapear.
/// </summary>
public sealed class MapeadorDeLaFicha : IMapeadorDeEventos
{
    public const string SeccionAceptada = "seccion.aceptada";

    public static readonly IReadOnlyList<TipoEventoPublicoDto> Tipos =
    [
        new(SeccionAceptada,
            "La empresa aceptó una versión de una sección de la ficha del tercero (al aprobarse el expediente); trae lo aceptado normalizado para el ERP.")
    ];

    public IReadOnlyList<TipoEventoPublicoDto> Catalogo => Tipos;

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (evento.Data is not SeccionAceptadaPorEmpresa aceptada || !Guid.TryParse(evento.TenantId, out var tenantId))
            return null;

        var expediente = await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct);
        var version = (await contexto.SeccionDeFichaAsync(aceptada.SeccionId, ct))?.Versiones.FirstOrDefault(v => v.Numero == aceptada.Version);
        if (expediente is null || version is null)
            return null;

        var datos = ContenidoParaErp.Datos(aceptada.Seccion, version.Datos);
        var catalogos = await contexto.CatalogosDeFichaAsync(ContenidoParaErp.Referencias(aceptada.Seccion, datos), ct);
        var vinculo = await contexto.VinculoAsync(tenantId, expediente.VinculoId, ct);
        IReadOnlyList<ReferenciaExternaDto> referencias = vinculo is null ? [] : [.. vinculo.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))];

        return new EventoPublicoMapeado(SeccionAceptada, tenantId, expediente.EmpresaId,
            new DatosSeccionAceptadaDto(expediente.VinculoId, referencias, expediente.TerceroId, expediente.EmpresaId, aceptada.Seccion, aceptada.Periodo,
                aceptada.Version, aceptada.Fecha, expediente.Id, expediente.Folio, aceptada.Huella, ContenidoParaErp.Normalizar(datos, catalogos)));
    }
}
