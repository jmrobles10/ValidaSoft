using Marten;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// La última actualización del vínculo no se aprobó (VR-13). Rechazada o anulada, el vínculo conserva su estado (RV-2): el tercero y la empresa
/// deben ver que esa actualización no se aprobó. El aviso sale de <see cref="Dominio.Vinculos.Vinculo.ActualizacionNoAprobada"/> y dura hasta que se
/// abre el siguiente expediente.
/// </summary>
public static class ActualizacionNoAprobada
{
    /// <summary>
    /// Lo que el tercero ve de cada vínculo suyo con una actualización no aprobada (por id de vínculo): cuándo, si se rechazó o se anuló y,
    /// al rechazarse, los motivos tal como se le comunicaron (sin áreas ni revisores, FL-10). Una anulación no lleva motivos: son internos.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ActualizacionNoAprobadaPublicaDto>> CargarAsync(IQuerySession consulta,
        IReadOnlyList<VinculoVisto> vistos, CancellationToken ct)
    {
        var sinAprobar = vistos.Select(v => v.Vinculo).Where(v => v.ActualizacionNoAprobada && v.UltimoExpedienteId is not null).ToList();
        if (sinAprobar.Count == 0)
            return new Dictionary<Guid, ActualizacionNoAprobadaPublicaDto>();

        var ids = sinAprobar.Select(v => v.UltimoExpedienteId!.Value).Distinct().ToArray();
        var expedientes = (await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id.IsOneOf(ids)).ToListAsync(ct)).ToDictionary(e => e.Id);

        var resultado = new Dictionary<Guid, ActualizacionNoAprobadaPublicaDto>();
        foreach (var vinculo in sinAprobar)
        {
            expedientes.TryGetValue(vinculo.UltimoExpedienteId!.Value, out var expediente);
            var motivos = expediente?.Rechazo is { } rechazo && vinculo.UltimoResultado == ResultadoExpediente.Rechazado
                ? [.. rechazo.Motivos.Select(m => new MotivoPublicoDto(m.Codigo, m.TextoAlTercero))]
                : Array.Empty<MotivoPublicoDto>();
            resultado[vinculo.Id] = new ActualizacionNoAprobadaPublicaDto(vinculo.UltimoResultado!.Value.ToString(),
                vinculo.UltimoExpedienteCerradoEn ?? expediente?.Rechazo?.Fecha, motivos);
        }

        return resultado;
    }
}
