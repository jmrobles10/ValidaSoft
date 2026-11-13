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
/// aceptación son las constructoras (<c>ConstId</c>) donde el tercero estaba Completada (3) o en Actualizacion (4). La imagen de la firma de las declaraciones
/// PEP vive en el disco del Portal: <c>Firmas:RutaBase</c> (carpeta con el contenido de <c>DeclaracionesPEP</c>) o <c>Firmas:UrlBase</c> (+ <c>Firmas:Token</c>) dicen de dónde
/// leerla (<see cref="IOrigenDeArchivosDelPortal"/>); sin ninguna, las declaraciones se importan sin imagen y con una advertencia.
/// </summary>
public sealed class AdaptadorPortal(IConfiguration configuracion, IOrigenDeArchivosDelPortal? archivos = null) : IAdaptadorDeOrigen
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

    public IOrigenDeTerceros DesdeArchivo(string json) => new OrigenDeTercerosPortal(OrigenPortalArchivo.DesdeJson(json), archivos);

    public IOrigenDeTerceros Conectado() =>
        new OrigenDeTercerosPortal(new OrigenPortalSqlServer(Cadena ?? throw ReglaDeNegocioException.Conflicto("origen-no-configurado",
            $"No hay cadena de conexión del origen «{CodigoDelSistema}» ({Seccion}:CadenaDeConexion).")), archivos);

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
internal sealed class OrigenDeTercerosPortal(IOrigenPortal origen, IOrigenDeArchivosDelPortal? archivos = null) : IOrigenDeTerceros
{
    private readonly Lazy<Task<CatalogosDelPortal>> _catalogos = new(() => origen.CatalogosAsync(CancellationToken.None));

    public async Task<IReadOnlyList<CatalogoImportable>> CatalogosAsync(CancellationToken ct) =>
        [.. CodigosDelPortal.Filas(await _catalogos.Value.WaitAsync(ct)).Select(c => new CatalogoImportable(c.Catalogo, c.Filas, c.Rechazos))];

    public async Task<IReadOnlyList<TerceroDeOrigen>> TercerosAsync(string? despuesDe, int tamano, CancellationToken ct)
    {
        int? cursor = despuesDe is null ? null : int.Parse(despuesDe, CultureInfo.InvariantCulture);
        var catalogos = await _catalogos.Value.WaitAsync(ct);
        var terceros = new List<TerceroDeOrigen>();
        foreach (var tercero in await origen.TercerosAsync(cursor, tamano, ct))
            terceros.Add(Mapear(await ConFirmasAsync(tercero, ct), catalogos));
        return terceros;
    }

    public async Task<TerceroDeOrigen?> TerceroAsync(string clave, CancellationToken ct)
    {
        if (!int.TryParse(clave, NumberStyles.None, CultureInfo.InvariantCulture, out var terid))
            return null;
        var tercero = await origen.TerceroAsync(terid, ct);
        return tercero is null ? null : Mapear(await ConFirmasAsync(tercero, ct), await _catalogos.Value.WaitAsync(ct));
    }

    /// <summary>
    /// Lee la imagen de la firma de cada declaración PEP que se va a importar (solo la de un contacto que hoy responde «Sí»): el Portal la guarda en su disco y
    /// aquí se pide por el origen de archivos. Lo que no se pueda leer queda sin imagen; el traductor lo avisa y verifica la huella.
    /// </summary>
    private async Task<TerceroDelPortal> ConFirmasAsync(TerceroDelPortal t, CancellationToken ct)
    {
        if (archivos is null || t.DeclaracionesPep is not { Count: > 0 } declaraciones)
            return t;
        var leidas = new List<TerDeclaracionPep>();
        foreach (var d in declaraciones)
        {
            var seImporta = t.CamaraComercio?.Any(c => c.TerCamId == d.TerCamId && c.TerCamEsPep == true) == true;
            leidas.Add(seImporta && d.FirmaImagen is null && !string.IsNullOrWhiteSpace(d.FirmaRuta) ? d with { FirmaImagen = await archivos.LeerAsync(d.FirmaRuta, ct) } : d);
        }

        return t with { DeclaracionesPep = leidas };
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
        servicios
            .AddSingleton<IOrigenDeArchivosDelPortal>(proveedor => ArchivosDelPortal.Configurado(proveedor.GetRequiredService<IConfiguration>()))
            .AddSingleton<IAdaptadorDeOrigen, AdaptadorPortal>();
}
