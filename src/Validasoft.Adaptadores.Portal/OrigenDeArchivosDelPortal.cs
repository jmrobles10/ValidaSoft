using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;

namespace Validasoft.Adaptadores.Portal;

/// <summary>
/// De dónde se leen los archivos del Portal que no están en su base de datos: hoy, la imagen de la firma de una declaración jurada PEP, que el
/// Portal guarda en el disco de su servidor (<c>dbo.TerDeclaracionPep.FirmaRuta</c>). Solo lectura. Hay tres implementaciones: una carpeta a la que
/// se tenga acceso (un recurso compartido o una copia de <c>DeclaracionesPEP</c>), una URL base (un servidor que la publique) y una en memoria para
/// desarrollo y pruebas. Sin ninguna configurada, las declaraciones se importan sin imagen y con la advertencia correspondiente.
/// </summary>
public interface IOrigenDeArchivosDelPortal
{
    /// <summary>El contenido del archivo que el Portal guardó en <paramref name="ruta"/> (tal cual la tiene <c>FirmaRuta</c>), o nulo si no existe o no se puede leer.</summary>
    Task<byte[]?> LeerAsync(string ruta, CancellationToken ct);
}

/// <summary>
/// El camino de un archivo del Portal dentro de su carpeta <c>DeclaracionesPEP</c>. El Portal guarda la ruta <b>absoluta de su servidor</b>
/// (<c>{raíz}\DeclaracionesPEP\{tercero}\{contacto}\firma_….png</c>), que otro servidor no tiene: lo que sirve es lo que sigue a esa carpeta.
/// </summary>
internal static class RutasDelPortal
{
    public const string Carpeta = "DeclaracionesPEP";

    /// <summary>Los tramos que siguen a <c>DeclaracionesPEP</c> (la última aparición), o nulo si la ruta no está en esa carpeta o intenta salirse de ella.</summary>
    public static string[]? Relativa(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return null;
        var tramos = ruta.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var indice = Array.FindLastIndex(tramos, t => t.Equals(Carpeta, StringComparison.OrdinalIgnoreCase));
        if (indice < 0 || indice == tramos.Length - 1)
            return null;
        var relativa = tramos[(indice + 1)..];
        return relativa.Any(t => t is "." or ".." || t.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ? null : relativa;
    }
}

/// <summary>Sin origen de archivos: nada se lee y las declaraciones se importan sin imagen de la firma.</summary>
public sealed class ArchivosDelPortalNulo : IOrigenDeArchivosDelPortal
{
    public Task<byte[]?> LeerAsync(string ruta, CancellationToken ct) => Task.FromResult<byte[]?>(null);
}

/// <summary>
/// Lee de una carpeta (<c>Validasoft:Importacion:Portal:Firmas:RutaBase</c>) que tiene el contenido de <c>DeclaracionesPEP</c> del Portal: un recurso
/// compartido de solo lectura o una copia. No sigue rutas que salgan de la carpeta y no lee archivos de más de <see cref="MaximoBytes"/>.
/// </summary>
public sealed class ArchivosDelPortalEnDisco(string rutaBase) : IOrigenDeArchivosDelPortal
{
    public const int MaximoBytes = 262_144;

    private readonly string _base = Path.GetFullPath(rutaBase);

    public async Task<byte[]?> LeerAsync(string ruta, CancellationToken ct)
    {
        if (RutasDelPortal.Relativa(ruta) is not { } relativa)
            return null;
        var completa = Path.GetFullPath(Path.Combine([_base, .. relativa]));
        if (!completa.StartsWith(_base + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return null;
        try
        {
            var archivo = new FileInfo(completa);
            return archivo.Exists && archivo.Length <= MaximoBytes ? await File.ReadAllBytesAsync(completa, ct) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>
/// Lee de un servidor web (<c>Validasoft:Importacion:Portal:Firmas:UrlBase</c>) que publique <c>DeclaracionesPEP</c>: pide <c>{UrlBase}/{tercero}/{contacto}/{archivo}</c>,
/// con <c>Authorization: Bearer</c> si hay <c>Token</c> (que viene de la configuración del entorno, nunca del repositorio). Solo http o https, con tiempo
/// de espera, y descarta lo que pese más de <see cref="ArchivosDelPortalEnDisco.MaximoBytes"/>.
/// </summary>
public sealed class ArchivosDelPortalPorHttp(HttpClient http, Uri urlBase, string? token = null) : IOrigenDeArchivosDelPortal
{
    public async Task<byte[]?> LeerAsync(string ruta, CancellationToken ct)
    {
        if (RutasDelPortal.Relativa(ruta) is not { } relativa)
            return null;
        var direccion = new Uri(urlBase.AbsoluteUri.TrimEnd('/') + "/" + string.Join('/', relativa.Select(Uri.EscapeDataString)));
        using var peticion = new HttpRequestMessage(HttpMethod.Get, direccion);
        if (!string.IsNullOrEmpty(token))
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var respuesta = await http.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!respuesta.IsSuccessStatusCode || respuesta.Content.Headers.ContentLength > ArchivosDelPortalEnDisco.MaximoBytes)
                return null;
            await using var flujo = await respuesta.Content.ReadAsStreamAsync(ct);
            using var memoria = new MemoryStream();
            var buffer = new byte[16_384];
            int leidos;
            while ((leidos = await flujo.ReadAsync(buffer, ct)) > 0)
            {
                memoria.Write(buffer, 0, leidos);
                if (memoria.Length > ArchivosDelPortalEnDisco.MaximoBytes)
                    return null;
            }

            return memoria.ToArray();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}

/// <summary>Archivos en memoria, por la ruta tal como la guarda el Portal: la implementación simulada para desarrollo y pruebas (no toca disco ni red).</summary>
public sealed class ArchivosDelPortalSimulado : IOrigenDeArchivosDelPortal
{
    private readonly Dictionary<string, byte[]> _archivos = new(StringComparer.Ordinal);

    /// <summary>Cuántas lecturas se pidieron (las pruebas comprueban que solo se leen las firmas que se van a importar).</summary>
    public int Lecturas { get; private set; }

    public ArchivosDelPortalSimulado Con(string ruta, byte[] contenido)
    {
        _archivos[ruta] = contenido;
        return this;
    }

    public Task<byte[]?> LeerAsync(string ruta, CancellationToken ct)
    {
        Lecturas++;
        return Task.FromResult(_archivos.TryGetValue(ruta, out var contenido) ? contenido : null);
    }
}

/// <summary>Elige el origen de archivos según la configuración <c>Validasoft:Importacion:Portal:Firmas</c>: <c>RutaBase</c> (carpeta) o <c>UrlBase</c> (+ <c>Token</c>); sin ninguna, <see cref="ArchivosDelPortalNulo"/>.</summary>
public static class ArchivosDelPortal
{
    public const string Seccion = $"{AdaptadorPortal.Seccion}:Firmas";

    public static IOrigenDeArchivosDelPortal Configurado(IConfiguration configuracion, HttpClient? http = null)
    {
        if (configuracion[$"{Seccion}:RutaBase"] is { Length: > 0 } ruta)
            return new ArchivosDelPortalEnDisco(ruta);
        if (configuracion[$"{Seccion}:UrlBase"] is { Length: > 0 } url && Uri.TryCreate(url, UriKind.Absolute, out var direccion) &&
            direccion.Scheme is "http" or "https")
            return new ArchivosDelPortalPorHttp(http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, direccion, configuracion[$"{Seccion}:Token"]);
        return new ArchivosDelPortalNulo();
    }
}
