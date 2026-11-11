using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Importacion;
using Validasoft.Dominio.Comun;

namespace Validasoft.Adaptadores.Portal;

/// <summary>
/// Adaptador de origen del Portal de Proveedores (F4, FM-39…FM-43): le da al importador y al sincronizador genéricos lo único que
/// no pueden saber, cómo se lee el Portal (SQL Server o un JSON) y cómo se traduce a la ficha (<see cref="TraductorDelPortal"/>).
/// Configuración <c>Validasoft:Importacion:Portal</c>: <c>CadenaDeConexion</c> (SQL Server del Portal, solo lectura; sin valor por
/// omisión) y <c>Archivo</c> (ruta de un JSON con la forma de <see cref="ArchivoDelPortal"/>, para desarrollo, pruebas o un
/// sincronizador sin red). Si están las dos, manda el archivo. La clave de cada tercero es su <c>Terid</c>; las unidades de
/// aceptación son las constructoras (<c>ConstId</c>) donde el tercero estaba Completada (3) o en Actualizacion (4).
/// </summary>
public sealed class AdaptadorPortal(IConfiguration configuracion) : IAdaptadorDeOrigen
{
    public const string CodigoDelSistema = "portal";

    public const string Seccion = "Validasoft:Importacion:Portal";

    /// <summary>Estados del Portal (<c>EstadoTercero</c>) que se importan como aceptados: Completada (3) y Actualizacion (4).</summary>
    public static readonly IReadOnlySet<int> EstadosAceptados = new HashSet<int> { 3, 4 };

    string IAdaptadorDeOrigen.Codigo => CodigoDelSistema;

    private string? Cadena => configuracion[$"{Seccion}:CadenaDeConexion"] is { Length: > 0 } c ? c : null;

    private string? Archivo => configuracion[$"{Seccion}:Archivo"] is { Length: > 0 } a ? a : null;

    public bool ConectadoConfigurado => Cadena is not null;

    public void ValidarArchivo(string json) => _ = OrigenPortalArchivo.DesdeJson(json);

    public IOrigenDeTerceros DesdeArchivo(string json) => new OrigenDeTercerosPortal(OrigenPortalArchivo.DesdeJson(json));

    public IOrigenDeTerceros Conectado() =>
        new OrigenDeTercerosPortal(new OrigenPortalSqlServer(Cadena ?? throw ReglaDeNegocioException.Conflicto("origen-no-configurado",
            $"No hay cadena de conexión del origen «{CodigoDelSistema}» ({Seccion}:CadenaDeConexion).")));

    public IOrigenDeTerceros Configurado()
    {
        if (Archivo is { } ruta)
        {
            if (!File.Exists(ruta))
                throw ReglaDeNegocioException.Conflicto("origen-no-configurado", $"No existe el archivo del origen «{CodigoDelSistema}» ({Seccion}:Archivo).");
            return DesdeArchivo(File.ReadAllText(ruta));
        }

        return Conectado();
    }
}

/// <summary>El origen del Portal (SQL Server o archivo) visto como un origen genérico de terceros.</summary>
internal sealed class OrigenDeTercerosPortal(IOrigenPortal origen) : IOrigenDeTerceros
{
    private readonly Lazy<Task<CatalogosDelPortal>> _catalogos = new(() => origen.CatalogosAsync(CancellationToken.None));

    public async Task<IReadOnlyList<CatalogoImportable>> CatalogosAsync(CancellationToken ct) =>
        [.. CodigosDelPortal.Filas(await _catalogos.Value.WaitAsync(ct)).Select(c => new CatalogoImportable(c.Catalogo, c.Filas, c.Rechazos))];

    public async Task<IReadOnlyList<TerceroDeOrigen>> TercerosAsync(string? despuesDe, int tamano, CancellationToken ct)
    {
        int? cursor = despuesDe is null ? null : int.Parse(despuesDe, CultureInfo.InvariantCulture);
        var catalogos = await _catalogos.Value.WaitAsync(ct);
        return [.. (await origen.TercerosAsync(cursor, tamano, ct)).Select(t => Mapear(t, catalogos))];
    }

    public async Task<TerceroDeOrigen?> TerceroAsync(string clave, CancellationToken ct)
    {
        if (!int.TryParse(clave, NumberStyles.None, CultureInfo.InvariantCulture, out var terid))
            return null;
        var tercero = await origen.TerceroAsync(terid, ct);
        return tercero is null ? null : Mapear(tercero, await _catalogos.Value.WaitAsync(ct));
    }

    private static TerceroDeOrigen Mapear(TerceroDelPortal t, CatalogosDelPortal catalogos) =>
        new(t.Terid.ToString(CultureInfo.InvariantCulture),
            [.. (t.Constructoras ?? []).Where(c => AdaptadorPortal.EstadosAceptados.Contains(c.Estado))
                .Select(c => c.IdConstructora.ToString(CultureInfo.InvariantCulture)).Distinct(StringComparer.Ordinal)],
            (plataforma, hoy, registrado) => TraductorDelPortal.Traducir(t, catalogos, plataforma, hoy, registrado));
}

public static class AdaptadorPortalServicios
{
    /// <summary>Registra el adaptador del Portal para el importador y el sincronizador genéricos (<c>/api/v1/plataforma/importacion/portal</c>).</summary>
    public static IServiceCollection AgregarAdaptadorPortal(this IServiceCollection servicios) =>
        servicios.AddSingleton<IAdaptadorDeOrigen, AdaptadorPortal>();
}
