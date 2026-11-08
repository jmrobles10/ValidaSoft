using System.Globalization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Documentos;

/// <summary>
/// Formatos de la lista blanca (T-17) con el nombre que lee el tercero. En la configuración se aceptan el MIME o el nombre
/// corto (<c>pdf</c>, <c>jpg</c>/<c>jpeg</c>, <c>png</c>, <c>webp</c>); se guardan y se devuelven como MIME.
/// </summary>
public static class FormatosDeArchivo
{
    private static readonly Dictionary<string, string> PorNombre = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pdf"] = InspectorDeArchivos.Pdf,
        [InspectorDeArchivos.Pdf] = InspectorDeArchivos.Pdf,
        ["jpg"] = InspectorDeArchivos.Jpeg,
        ["jpeg"] = InspectorDeArchivos.Jpeg,
        [InspectorDeArchivos.Jpeg] = InspectorDeArchivos.Jpeg,
        ["png"] = InspectorDeArchivos.Png,
        [InspectorDeArchivos.Png] = InspectorDeArchivos.Png,
        ["webp"] = InspectorDeArchivos.Webp,
        [InspectorDeArchivos.Webp] = InspectorDeArchivos.Webp
    };

    private static readonly Dictionary<string, string> Etiquetas = new(StringComparer.Ordinal)
    {
        [InspectorDeArchivos.Pdf] = "PDF",
        [InspectorDeArchivos.Jpeg] = "JPG",
        [InspectorDeArchivos.Png] = "PNG",
        [InspectorDeArchivos.Webp] = "WebP"
    };

    /// <summary>MIME del formato; fuera de la lista blanca ⇒ 422 <c>formato-invalido</c>.</summary>
    public static string Normalizar(string? formato) =>
        PorNombre.TryGetValue((formato ?? string.Empty).Trim(), out var mime)
            ? mime
            : throw ReglaDeNegocioException.Validacion("formato-invalido",
                $"'{formato}' no es un formato admitido (PDF, JPG, PNG o WebP).");

    /// <summary>Sin repetidos y en el orden de la lista blanca.</summary>
    public static List<string> NormalizarLista(IEnumerable<string>? formatos)
    {
        var elegidos = (formatos ?? []).Select(Normalizar).ToHashSet(StringComparer.Ordinal);
        return [.. InspectorDeArchivos.Permitidos.Where(elegidos.Contains)];
    }

    public static string Etiqueta(string mime) => Etiquetas.TryGetValue(mime, out var etiqueta) ? etiqueta : mime;

    /// <summary>«PDF», «PDF o JPG», «PDF, JPG o PNG».</summary>
    public static string Enumerar(IReadOnlyList<string> mimes)
    {
        var etiquetas = mimes.Select(Etiqueta).ToList();
        return etiquetas.Count switch
        {
            0 => "ningún formato",
            1 => etiquetas[0],
            _ => string.Join(", ", etiquetas.Take(etiquetas.Count - 1)) + " o " + etiquetas[^1]
        };
    }

    /// <summary>«5 MB», «2,5 MB» o «256 KB».</summary>
    public static string Tamano(long bytes)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        return bytes >= 1024 * 1024
            ? (bytes / (1024d * 1024d)).ToString("0.#", cultura) + " MB"
            : (bytes / 1024d).ToString("0", cultura) + " KB";
    }
}

/// <summary>
/// Límites efectivos de un archivo para un tipo de documento (TD-1): la lista blanca y los límites globales
/// (<c>Validasoft:Boveda</c>) acotados por el canónico y por el tipo del tenant —siempre el más estricto—. Se aplican al
/// iniciar la carga (MIME y tamaño declarados) y al confirmarla (tipo real por magic bytes, tamaño y páginas reales).
/// <see cref="Propias"/>: el tipo restringe algo; entonces los mensajes nombran el tipo y su límite.
/// </summary>
public sealed record ReglasDeArchivo(string NombreTipo, IReadOnlyList<string> Formatos, long TamanoMaximoBytes, int PaginasMaximas, bool Propias)
{
    public const long Mega = 1024 * 1024;

    public static ReglasDeArchivo Globales(long tamanoMaximoBytes, int paginasMaximas) =>
        new("El archivo", InspectorDeArchivos.Permitidos, tamanoMaximoBytes, paginasMaximas, false);

    /// <summary>Lo más estricto entre los límites globales, el canónico y el tipo del tenant (nulos ⇒ no restringen).</summary>
    public static ReglasDeArchivo Efectivas(TipoDocumentoCanonico? canonico, TipoDocumentoTenant? tipo, long tamanoGlobalBytes, int paginasGlobales)
    {
        IEnumerable<string> formatos = InspectorDeArchivos.Permitidos;
        if (canonico is { Formatos.Count: > 0 })
            formatos = formatos.Where(canonico.Formatos.Contains);
        if (tipo is { FormatosPermitidos.Count: > 0 })
            formatos = formatos.Where(tipo.FormatosPermitidos.Contains);
        var lista = formatos.ToList();

        var bytes = Math.Min(tamanoGlobalBytes, Math.Min(Mb(canonico?.TamanoMaximoMb), Mb(tipo?.TamanoMaximoMb)));
        var paginas = Math.Min(paginasGlobales, Math.Min(canonico?.PaginasMaximas ?? int.MaxValue, tipo?.PaginasMaximas ?? int.MaxValue));
        var propias = lista.Count < InspectorDeArchivos.Permitidos.Count || bytes < tamanoGlobalBytes || paginas < paginasGlobales;
        return new ReglasDeArchivo(tipo?.Nombre ?? canonico?.Nombre ?? "El archivo", lista, bytes, paginas, propias);
    }

    /// <summary>«Certificado de existencia… admite solo PDF de hasta 5 MB».</summary>
    public string Descripcion => $"{NombreTipo} admite solo {FormatosDeArchivo.Enumerar(Formatos)} de hasta {FormatosDeArchivo.Tamano(TamanoMaximoBytes)}";

    public bool Admite(string? mime) => mime is not null && Formatos.Contains(mime.Trim().ToLowerInvariant());

    /// <summary>Paso 1: MIME declarado fuera de lo admitido ⇒ 415; tamaño declarado mayor ⇒ 413.</summary>
    public void ValidarDeclarado(string mime, long tamanoBytes)
    {
        if (!Admite(mime))
            throw ReglaDeNegocioException.TipoNoSoportado(Propias
                ? $"{Descripcion}."
                : $"Solo se aceptan {string.Join(", ", InspectorDeArchivos.Permitidos)}.");
        ValidarTamano(tamanoBytes);
    }

    /// <summary>Tamaño (declarado o real) mayor al máximo ⇒ 413.</summary>
    public void ValidarTamano(long tamanoBytes)
    {
        if (tamanoBytes > TamanoMaximoBytes)
            throw ReglaDeNegocioException.DemasiadoGrande(Propias
                ? $"{Descripcion}."
                : $"El archivo supera el máximo de {TamanoMaximoBytes / Mega} MB.");
    }

    /// <summary>Paso 2: el tipo real (magic bytes) debe estar admitido; si no ⇒ 415 (la extensión y el MIME declarado no bastan).</summary>
    public void ValidarTipoReal(string? mimeReal)
    {
        if (mimeReal is null)
            throw ReglaDeNegocioException.TipoNoSoportado(
                $"El contenido no es un {string.Join(", ", InspectorDeArchivos.Permitidos)} válido (la extensión o el tipo declarado no bastan).");
        if (!Admite(mimeReal))
            throw ReglaDeNegocioException.TipoNoSoportado(
                $"{Descripcion}: el archivo es {FormatosDeArchivo.Etiqueta(mimeReal)} (la extensión o el tipo declarado no bastan).");
    }

    /// <summary>Más páginas que el máximo ⇒ la versión se devuelve al tercero con <c>demasiadas-paginas</c> (BO-4).</summary>
    public MotivoDeDevolucion? PorPaginas(int? paginas) =>
        paginas is { } n && n > PaginasMaximas
            ? new MotivoDeDevolucion("demasiadas-paginas", Propias
                ? $"El PDF tiene {n} páginas y {NombreTipo} admite hasta {PaginasMaximas}. Sube solo las páginas necesarias."
                : $"El PDF tiene {n} páginas y el máximo es {PaginasMaximas}. Sube solo las páginas necesarias.")
            : null;

    private static long Mb(int? megas) => megas is { } m ? m * Mega : long.MaxValue;
}
