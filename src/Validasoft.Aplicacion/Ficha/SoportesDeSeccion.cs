using Marten;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Soportes de una sección de la ficha vistos contra la bóveda (RT-4): el tercero puede retirar una versión que respaldaba una
/// sección. La sección no pierde datos ni se recalcula (una versión de sección es inmutable y su completitud ya se evaluó con
/// el soporte que tenía); lo que cambia es la lectura: el soporte sale marcado como retirado y el tercero lo ve como
/// advertencia para elegir otro (docs/14 FM-20).
/// </summary>
public static class SoportesDeSeccion
{
    /// <summary>Los documentos de la bóveda que respaldan la versión vigente de las secciones dadas, por id.</summary>
    public static async Task<IReadOnlyDictionary<Guid, Documento>> DocumentosAsync(IQuerySession boveda, IEnumerable<SeccionDeFicha?> secciones,
        CancellationToken ct)
    {
        var ids = secciones.OfType<SeccionDeFicha>().Select(s => s.Vigente).OfType<VersionDeSeccion>().SelectMany(v => v.Soportes)
            .Select(s => s.DocumentoId).Distinct().ToArray();
        return ids.Length == 0
            ? new Dictionary<Guid, Documento>()
            : (await boveda.LoadManyAsync<Documento>(ct, ids)).ToDictionary(d => d.Id);
    }

    /// <summary>Soportes de la versión vigente de la sección cuya versión el tercero retiró.</summary>
    public static IReadOnlyList<SoporteSeccionDto> Retirados(SeccionDeFicha seccion, IReadOnlyDictionary<Guid, Documento> documentos) =>
        seccion.Vigente is not { } vigente
            ? []
            : [.. vigente.Soportes.Select(s => s.ADto(documentos)).Where(s => s.Retirado)];
}
