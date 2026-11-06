using Marten;
using JasperFx.Events;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Acceso a los datos de la empresa activa (T-3): todo lo que devuelve ya viene filtrado por
/// <c>EmpresaId</c>, y un recurso de otra empresa responde 404 (T-4), nunca 403.
/// </summary>
public sealed class SesionDeEmpresa(ISesiones sesiones, IContextoSolicitud contexto) : IAsyncDisposable
{
    private IDocumentSession? _sesion;
    private IMessageBus? _mensajes;

    public IDocumentSession Sesion => _sesion ??= _mensajes is null ? sesiones.DelTenant() : sesiones.DelTenant(_mensajes);

    /// <summary>
    /// Enlista la sesión en el outbox de <paramref name="mensajes"/> (debe llamarse antes del primer uso de
    /// <see cref="Sesion"/>): los mensajes publicados se despachan solo si la transacción se confirma.
    /// </summary>
    public void UsarMensajeria(IMessageBus mensajes)
    {
        if (_sesion is not null)
            throw new InvalidOperationException("La sesión de la empresa ya se abrió sin mensajería.");
        _mensajes = mensajes;
    }

    public Guid EmpresaId => contexto.EmpresaRequerida();

    public IQueryable<Vinculo> Vinculos() => Sesion.Query<Vinculo>().Where(v => v.EmpresaId == EmpresaId);

    public async Task<Vinculo> CargarVinculoAsync(Guid id, CancellationToken ct)
    {
        var vinculo = await Sesion.LoadAsync<Vinculo>(id, ct);
        return vinculo is not null && vinculo.EmpresaId == EmpresaId
            ? vinculo
            : throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");
    }

    public IQueryable<Area> Areas() => Sesion.Query<Area>().Where(a => a.EmpresaId == EmpresaId);

    public async Task<Area> CargarAreaAsync(Guid id, CancellationToken ct)
    {
        var area = await Sesion.LoadAsync<Area>(id, ct);
        return area is not null && area.EmpresaId == EmpresaId
            ? area
            : throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", "No existe esa área en la empresa.");
    }

    /// <summary>Carga el stream para escribir con concurrencia optimista (<c>If-Match</c> si llega).</summary>
    public async Task<IEventStream<Vinculo>> VinculoParaEscribirAsync(Guid id, long? versionEsperada, CancellationToken ct)
    {
        var stream = versionEsperada is null
            ? await Sesion.Events.FetchForWriting<Vinculo>(id, ct)
            : await Sesion.Events.FetchForWriting<Vinculo>(id, versionEsperada.Value, ct);

        if (stream.Aggregate is null || stream.Aggregate.EmpresaId != EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");

        return stream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sesion is not null)
            await _sesion.DisposeAsync();
    }
}
