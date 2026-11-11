using Marten;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Lo que la bóveda necesita saber de los expedientes de un vínculo para decirle al tercero qué tiene cada empresa de cada
/// documento (RT-5) y qué le pasaría si retira una versión (RT-1): el último expediente aprobado y el abierto.
/// </summary>
public sealed record ExpedientesDelVinculo(Expediente? Aprobado, Expediente? Abierto);

public static class ExpedientesDeLaBoveda
{
    private static readonly EstadoExpediente[] Relevantes =
        [EstadoExpediente.Abierto, EstadoExpediente.EnRevision, EstadoExpediente.Observado, EstadoExpediente.Aprobado];

    /// <summary>
    /// Por vínculo, su último expediente aprobado y el abierto. Lee de todos los tenants (<c>AnyTenant</c>): el tercero no tiene
    /// tenant y solo recibe lo de sus propios vínculos, que el llamador ya validó contra su sesión.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ExpedientesDelVinculo>> CargarAsync(IQuerySession consulta, IEnumerable<Guid> vinculoIds,
        CancellationToken ct)
    {
        var ids = vinculoIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, ExpedientesDelVinculo>();

        var expedientes = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.VinculoId.IsOneOf(ids) && e.Estado.IsOneOf(Relevantes))
            .ToListAsync(ct);
        return expedientes.GroupBy(e => e.VinculoId).ToDictionary(g => g.Key, g => new ExpedientesDelVinculo(
            g.Where(e => e.Estado == EstadoExpediente.Aprobado).OrderByDescending(e => e.ActualizadoEn).FirstOrDefault(),
            g.Where(e => e.EstaAbierto).OrderByDescending(e => e.AbiertoEn).FirstOrDefault()));
    }

    /// <summary>El requisito del expediente que cubre una versión de este documento (no una sección con el mismo id), si lo hay.</summary>
    public static RequisitoDeExpediente? RequisitoDe(Expediente? expediente, Guid documentoId) =>
        expediente?.Requisitos.FirstOrDefault(r => !r.EsSeccion && !r.Eximido && r.VersionActual?.DocumentoId == documentoId);

    /// <summary>
    /// Los hechos de un documento con un vínculo para elegir su situación (<see cref="SituacionDelDocumento"/>): lo que recibió, lo
    /// que aprobó y lo que revisa hoy. Una empresa revisa solo con el expediente en revisión u observado (en <c>Abierto</c> aún
    /// espera al tercero).
    /// </summary>
    public static HechosConEmpresa HechosDe(Documento documento, Guid vinculoId, ExpedientesDelVinculo? expedientes)
    {
        var recibidas = documento.Comparticiones.Where(c => c.VinculoId == vinculoId).Select(c => c.Numero).Distinct().ToList();
        int? recibida = recibidas.Where(n => documento.Buscar(n)?.Estado != EstadoVersion.Retirada).Select(n => (int?)n).Max();
        int? retirada = recibidas.Where(n => documento.Buscar(n)?.Estado == EstadoVersion.Retirada).Select(n => (int?)n).Max();

        var abierto = RequisitoDe(expedientes?.Abierto, documento.Id);
        var enAprobado = RequisitoDe(expedientes?.Aprobado, documento.Id);
        var aprobada = abierto is { Estado: EstadoRequisito.Aprobado } ? abierto.VersionActual?.Numero
            : enAprobado is { Estado: EstadoRequisito.Aprobado } ? enAprobado.VersionActual?.Numero
            : null;

        var revisando = expedientes?.Abierto is { Estado: EstadoExpediente.EnRevision or EstadoExpediente.Observado } &&
                        abierto is { Estado: not (EstadoRequisito.Aprobado or EstadoRequisito.Pendiente or EstadoRequisito.NoAplica) };
        return new HechosConEmpresa(documento.VersionActual, recibida, retirada, aprobada,
            aprobada is { } n && documento.Buscar(n)?.Estado == EstadoVersion.Retirada,
            revisando ? abierto!.VersionActual?.Numero : null, revisando && abierto!.Estado == EstadoRequisito.Observado);
    }
}
