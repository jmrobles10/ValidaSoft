using System.Text.Json;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Adaptadores.Portal;

/// <summary>
/// De dónde lee el importador las filas del Portal (FM-39): los catálogos y, por tercero, sus secciones. Solo lectura. Dos
/// implementaciones: <see cref="OrigenPortalArchivo"/> (un JSON con la forma de <see cref="ArchivoDelPortal"/>; lo usan las
/// pruebas y un dry-run sin red) y <c>OrigenPortalSqlServer</c> (infraestructura: consultas parametrizadas a la BD del Portal).
/// </summary>
public interface IOrigenPortal
{
    Task<CatalogosDelPortal> CatalogosAsync(CancellationToken ct);

    /// <summary>Hasta <paramref name="tamano"/> terceros con <c>Terid</c> mayor que <paramref name="despuesDe"/>, en orden de <c>Terid</c> (el cursor del lote).</summary>
    Task<IReadOnlyList<TerceroDelPortal>> TercerosAsync(int? despuesDe, int tamano, CancellationToken ct);

    /// <summary>Un tercero con todas sus secciones, o nulo si no existe.</summary>
    Task<TerceroDelPortal?> TerceroAsync(int terid, CancellationToken ct);
}

/// <summary>Forma del JSON del origen archivo: los catálogos y los terceros, con los nombres de tabla y columna del Portal (sin distinguir mayúsculas).</summary>
public sealed record ArchivoDelPortal(CatalogosDelPortal? Catalogos, IReadOnlyList<TerceroDelPortal>? Terceros);

/// <summary>Origen en memoria leído de un JSON (<see cref="ArchivoDelPortal"/>). JSON inválido ⇒ 422 <c>archivo-invalido</c>.</summary>
public sealed class OrigenPortalArchivo : IOrigenPortal
{
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly CatalogosDelPortal _catalogos;
    private readonly IReadOnlyList<TerceroDelPortal> _terceros;

    public OrigenPortalArchivo(ArchivoDelPortal archivo)
    {
        _catalogos = archivo.Catalogos ?? CatalogosDelPortal.Vacios;
        var terceros = (archivo.Terceros ?? []).Where(t => t?.Tercero is not null).ToList();
        if (terceros.GroupBy(t => t.Terid).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            throw ReglaDeNegocioException.Validacion("archivo-invalido", $"El Terid {repetido.Key} está repetido en el archivo.");
        _terceros = [.. terceros.OrderBy(t => t.Terid)];
    }

    public static OrigenPortalArchivo DesdeJson(string json)
    {
        try
        {
            return new OrigenPortalArchivo(JsonSerializer.Deserialize<ArchivoDelPortal>(json, Opciones)
                                           ?? throw ReglaDeNegocioException.Validacion("archivo-invalido", "El archivo del Portal está vacío."));
        }
        catch (JsonException e)
        {
            throw ReglaDeNegocioException.Validacion("archivo-invalido", $"El archivo del Portal no tiene la forma esperada: {e.Message}");
        }
    }

    public Task<CatalogosDelPortal> CatalogosAsync(CancellationToken ct) => Task.FromResult(_catalogos);

    public Task<IReadOnlyList<TerceroDelPortal>> TercerosAsync(int? despuesDe, int tamano, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TerceroDelPortal>>([.. _terceros.Where(t => despuesDe is null || t.Terid > despuesDe).Take(tamano)]);

    public Task<TerceroDelPortal?> TerceroAsync(int terid, CancellationToken ct) => Task.FromResult(_terceros.FirstOrDefault(t => t.Terid == terid));
}
