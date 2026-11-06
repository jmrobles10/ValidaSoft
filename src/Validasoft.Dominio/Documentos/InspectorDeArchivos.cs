using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Validasoft.Dominio.Documentos;

/// <summary>Resultado de inspeccionar los bytes de un archivo: tipo real, páginas (PDF) y contenido activo encontrado.</summary>
public sealed record InspeccionDeArchivo(string? MimeReal, int? Paginas, IReadOnlyList<string> ContenidoActivo)
{
    public bool Soportado => MimeReal is not null;
}

/// <summary>
/// Ingesta segura sin IA (docs/07 §6.1): el tipo real sale de los <b>magic bytes</b> (el MIME declarado no basta) y un PDF
/// se revisa por contenido activo —JavaScript, acciones automáticas, lanzadores, formularios que envían datos y adjuntos
/// embebidos—, también dentro de los flujos comprimidos (<c>FlateDecode</c>, incluidos los <c>ObjStm</c>). Es un análisis
/// estático conservador: ante la duda la versión se devuelve al tercero; no reemplaza al antivirus (T-11).
/// </summary>
public static partial class InspectorDeArchivos
{
    public const string Pdf = "application/pdf";
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";

    /// <summary>Lista blanca de tipos aceptados (P-16-2, T-17). HEIC se convierte a PDF en el cliente.</summary>
    public static readonly IReadOnlyList<string> Permitidos = [Pdf, Jpeg, Png, Webp];

    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>Nombres PDF que indican contenido activo o embebido (se comparan tras decodificar los escapes <c>#xx</c>).</summary>
    private static readonly string[] NombresActivos =
        ["JavaScript", "JS", "Launch", "EmbeddedFile", "EmbeddedFiles", "RichMedia", "SubmitForm", "ImportData", "AA", "XFA"];

    [GeneratedRegex(@"/([A-Za-z0-9#]+)")]
    private static partial Regex RegexNombre();

    [GeneratedRegex(@"/OpenAction\s*<<")]
    private static partial Regex RegexOpenActionAccion();

    [GeneratedRegex(@"/Type\s*/Pages\b")]
    private static partial Regex RegexPaginas();

    [GeneratedRegex(@"/Count\s+(\d+)")]
    private static partial Regex RegexCount();

    [GeneratedRegex(@"/Type\s*/Page(?![A-Za-z])")]
    private static partial Regex RegexPagina();

    [GeneratedRegex(@"(?<!end)stream\r?\n")]
    private static partial Regex RegexInicioStream();

    public static bool EsPermitido(string? mime) =>
        mime is not null && Permitidos.Contains(mime.Trim().ToLowerInvariant());

    /// <summary>Tipo real por la firma de los primeros bytes; nulo si no es un tipo permitido.</summary>
    public static string? DetectarMime(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return Png;
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return Jpeg;
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
            return Webp;

        // La cabecera %PDF- puede venir tras basura de hasta 1 KB (ISO 32000-1 §7.5.2).
        var cabecera = bytes[..Math.Min(bytes.Length, 1024)];
        return cabecera.IndexOf("%PDF-"u8) >= 0 ? Pdf : null;
    }

    public static InspeccionDeArchivo Inspeccionar(byte[] contenido)
    {
        var mime = DetectarMime(contenido);
        if (mime != Pdf)
            return new InspeccionDeArchivo(mime, null, []);

        // Se revisan los diccionarios (el PDF sin el cuerpo de sus flujos) y los flujos de objetos comprimidos; el resto de
        // flujos (imágenes, contenido de página) son datos y darían falsos positivos.
        var (diccionarios, objetos) = Separar(contenido);
        var textos = new List<string> { diccionarios };
        textos.AddRange(objetos);

        var activos = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var texto in textos)
        {
            foreach (Match m in RegexNombre().Matches(texto))
            {
                var nombre = DecodificarNombre(m.Groups[1].Value);
                if (NombresActivos.Contains(nombre, StringComparer.Ordinal))
                    activos.Add(nombre);
            }

            // /OpenAction con un diccionario es una acción automática; con un arreglo es solo la vista inicial.
            if (RegexOpenActionAccion().IsMatch(texto))
                activos.Add("OpenAction");
        }

        return new InspeccionDeArchivo(Pdf, ContarPaginas(textos), [.. activos]);
    }

    /// <summary>
    /// Motivo de devolución de un PDF con contenido activo (docs/04 §6: "PDF con JavaScript o formularios activos →
    /// Devuelta"); nulo si está limpio o no es PDF.
    /// </summary>
    public static MotivoDeDevolucion? MotivoDeDevolucion(InspeccionDeArchivo inspeccion, int? maximoPaginas)
    {
        if (inspeccion.ContenidoActivo.Count > 0)
            return new MotivoDeDevolucion("pdf-con-contenido-activo",
                "El PDF trae contenido activo que no aceptamos (" + string.Join(", ", inspeccion.ContenidoActivo) +
                "). Guárdalo de nuevo como PDF simple (\"Imprimir como PDF\") y vuelve a subirlo.");

        if (maximoPaginas is { } maximo && inspeccion.Paginas > maximo)
            return new MotivoDeDevolucion("demasiadas-paginas",
                $"El PDF tiene {inspeccion.Paginas} páginas y el máximo es {maximo}. Sube solo las páginas necesarias.");

        return null;
    }

    private static int? ContarPaginas(IReadOnlyList<string> textos)
    {
        // El nodo raíz /Pages tiene el /Count mayor; si no se encuentra, se cuentan los objetos /Type /Page.
        var maximo = 0;
        foreach (var texto in textos)
        {
            foreach (Match m in RegexPaginas().Matches(texto))
            {
                var inicio = Math.Max(0, m.Index - 400);
                var ventana = texto.Substring(inicio, Math.Min(texto.Length - inicio, 800));
                foreach (Match c in RegexCount().Matches(ventana))
                {
                    if (int.TryParse(c.Groups[1].Value, out var n))
                        maximo = Math.Max(maximo, n);
                }
            }
        }

        if (maximo > 0)
            return maximo;

        var paginas = textos.Sum(t => RegexPagina().Matches(t).Count);
        return paginas > 0 ? paginas : null;
    }

    /// <summary>
    /// Separa el PDF en el texto fuera de los flujos <c>stream … endstream</c> (diccionarios y objetos) y el contenido
    /// descomprimido (zlib, <c>FlateDecode</c>) de los flujos de objetos <c>/Type /ObjStm</c>, donde un PDF moderno guarda
    /// sus diccionarios.
    /// </summary>
    private static (string Diccionarios, List<string> Objetos) Separar(byte[] contenido)
    {
        var texto = Latin1.GetString(contenido);
        var fuera = new StringBuilder(texto.Length);
        var objetos = new List<string>();
        var cursor = 0;

        foreach (Match m in RegexInicioStream().Matches(texto))
        {
            if (m.Index < cursor)
                continue;

            var inicio = m.Index + m.Length;
            var fin = texto.IndexOf("endstream", inicio, StringComparison.Ordinal);
            if (fin < 0)
                break;

            fuera.Append(texto, cursor, inicio - cursor).Append(' ');
            cursor = fin;

            var desde = Math.Max(0, m.Index - 1000);
            var previo = texto[desde..m.Index];
            var objeto = previo.LastIndexOf(" obj", StringComparison.Ordinal);
            var diccionario = objeto >= 0 ? previo[objeto..] : previo;
            if (!diccionario.Contains("/ObjStm", StringComparison.Ordinal))
                continue;

            try
            {
                using var entrada = new MemoryStream(contenido, inicio, fin - inicio);
                using var zlib = new ZLibStream(entrada, CompressionMode.Decompress);
                using var salida = new MemoryStream();
                CopiarConLimite(zlib, salida, 64L * 1024 * 1024);
                objetos.Add(Latin1.GetString(salida.ToArray()));
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                // Un flujo de objetos ilegible no aporta nombres; el antivirus y la revisión humana siguen aplicando.
            }
        }

        fuera.Append(texto, cursor, texto.Length - cursor);
        return (fuera.ToString(), objetos);
    }

    /// <summary>Evita bombas de descompresión: deja de leer al pasar el límite.</summary>
    private static void CopiarConLimite(Stream origen, Stream destino, long limite)
    {
        var buffer = new byte[81920];
        long total = 0;
        int leidos;
        while ((leidos = origen.Read(buffer, 0, buffer.Length)) > 0)
        {
            total += leidos;
            if (total > limite)
                throw new InvalidDataException("Flujo demasiado grande.");
            destino.Write(buffer, 0, leidos);
        }
    }

    /// <summary>Decodifica los escapes de nombres PDF (<c>/J#61vaScript</c> ⇒ <c>JavaScript</c>).</summary>
    private static string DecodificarNombre(string nombre)
    {
        if (!nombre.Contains('#'))
            return nombre;

        var sb = new StringBuilder(nombre.Length);
        for (var i = 0; i < nombre.Length; i++)
        {
            if (nombre[i] == '#' && i + 2 < nombre.Length &&
                byte.TryParse(nombre.AsSpan(i + 1, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            {
                sb.Append((char)b);
                i += 2;
            }
            else
            {
                sb.Append(nombre[i]);
            }
        }

        return sb.ToString();
    }
}
