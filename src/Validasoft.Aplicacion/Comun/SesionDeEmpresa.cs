using Marten;
using JasperFx.Events;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Acceso a los datos de la empresa activa (T-3): todo lo que devuelve ya viene filtrado por
/// <c>EmpresaId</c>, y un recurso de otra empresa responde 404 (T-4), nunca 403.
/// </summary>
public sealed class SesionDeEmpresa(ISesiones sesiones, IContextoSolicitud contexto) : IAsyncDisposable
{
    private IDocumentSession? _sesion;

    public IDocumentSession Sesion => _sesion ??= sesiones.DelTenant();

    public Guid EmpresaId => contexto.EmpresaRequerida();

    public IQueryable<Vinculo> Vinculos() => Sesion.Query<Vinculo>().Where(v => v.EmpresaId == EmpresaId);

    public async Task<Vinculo> CargarVinculoAsync(Guid id, CancellationToken ct)
    {
        var vinculo = await Sesion.LoadAsync<Vinculo>(id, ct);
        return vinculo is not null && vinculo.EmpresaId == EmpresaId
            ? vinculo
            : throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");
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
