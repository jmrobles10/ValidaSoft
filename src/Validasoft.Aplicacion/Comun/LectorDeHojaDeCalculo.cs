using System.IO.Compression;
using System.Text;
using System.Xml;
using Validasoft.Dominio.Comun;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Lee la primera hoja de un libro de Excel (.xlsx) como filas de texto, sin dependencias: lo justo para la plantilla de
/// invitación masiva (LT-6). Textos compartidos, en línea y fórmulas con su valor; los números tal como los guarda Excel.
/// Rechaza (422 <c>archivo-invalido</c>) lo que no es un .xlsx y, contra las bombas de compresión, partes de más de 64 MB.
/// </summary>
public static class LectorDeHojaDeCalculo
{
    private const long TamanoMaximoParte = 64L * 1024 * 1024;
    private const string Principal = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Relaciones = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static IReadOnlyList<IReadOnlyList<string>> Leer(byte[] contenido)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(contenido), ZipArchiveMode.Read);
            var compartidos = zip.GetEntry("xl/sharedStrings.xml") is { } textos ? TextosCompartidos(Abrir(textos)) : [];
            var hoja = zip.GetEntry(RutaPrimeraHoja(zip)) ?? throw Invalido();
            return Filas(Abrir(hoja), compartidos);
        }
        catch (Exception e) when (e is InvalidDataException or XmlException or IOException)
        {
            throw Invalido();
        }
    }

    private static ReglaDeNegocioException Invalido() =>
        ReglaDeNegocioException.Validacion("archivo-invalido", "El archivo no es un libro de Excel (.xlsx) válido.");

    private static XmlReader Abrir(ZipArchiveEntry parte)
    {
        if (parte.Length > TamanoMaximoParte)
            throw ReglaDeNegocioException.Validacion("archivo-demasiado-grande", "El libro de Excel es demasiado grande.");
        return XmlReader.Create(parte.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, IgnoreWhitespace = false });
    }

    /// <summary>La primera <c>&lt;sheet&gt;</c> de <c>workbook.xml</c> y su destino en las relaciones del libro.</summary>
    private static string RutaPrimeraHoja(ZipArchive zip)
    {
        string? relacion = null;
        using (var libro = Abrir(zip.GetEntry("xl/workbook.xml") ?? throw Invalido()))
        {
            while (libro.Read())
                if (libro.NodeType == XmlNodeType.Element && libro.LocalName == "sheet")
                {
                    relacion = libro.GetAttribute("id", Relaciones);
                    break;
                }
        }

        if (relacion is not null && zip.GetEntry("xl/_rels/workbook.xml.rels") is { } rels)
        {
            using var lector = Abrir(rels);
            while (lector.Read())
                if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "Relationship" && lector.GetAttribute("Id") == relacion)
                {
                    var destino = lector.GetAttribute("Target") ?? string.Empty;
                    return destino.StartsWith('/') ? destino.TrimStart('/') : "xl/" + destino;
                }
        }

        return "xl/worksheets/sheet1.xml";
    }

    private static List<string> TextosCompartidos(XmlReader lector)
    {
        var textos = new List<string>();
        using (lector)
        {
            StringBuilder? actual = null;
            while (lector.Read())
            {
                if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "si" && lector.NamespaceURI == Principal)
                    actual = new StringBuilder();
                else if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "t" && actual is not null)
                    actual.Append(lector.ReadElementContentAsString());
                else if (lector.NodeType == XmlNodeType.EndElement && lector.LocalName == "si" && actual is not null)
                {
                    textos.Add(actual.ToString());
                    actual = null;
                }
            }
        }

        return textos;
    }

    private static List<IReadOnlyList<string>> Filas(XmlReader lector, List<string> compartidos)
    {
        var filas = new List<IReadOnlyList<string>>();
        using (lector)
        {
            List<string>? fila = null;
            var numeroFila = 0;
            while (lector.Read())
            {
                if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "row")
                {
                    // Excel omite las filas vacías: se rellenan para conservar la numeración de la hoja.
                    var r = int.TryParse(lector.GetAttribute("r"), out var n) ? n : numeroFila + 1;
                    while (numeroFila < r - 1)
                    {
                        filas.Add([]);
                        numeroFila++;
                    }

                    fila = [];
                    numeroFila = r;
                    if (lector.IsEmptyElement)
                    {
                        filas.Add(fila);
                        fila = null;
                    }
                }
                else if (lector.NodeType == XmlNodeType.Element && lector.LocalName == "c" && fila is not null)
                {
                    var columna = Columna(lector.GetAttribute("r")) ?? fila.Count;
                    var valor = Celda(lector, compartidos);
                    while (fila.Count < columna)
                        fila.Add(string.Empty);
                    fila.Add(valor);
                }
                else if (lector.NodeType == XmlNodeType.EndElement && lector.LocalName == "row" && fila is not null)
                {
                    filas.Add(fila);
                    fila = null;
                }
            }
        }

        return filas;
    }

    /// <summary>Valor de la celda: <c>s</c> (compartido), <c>inlineStr</c>, <c>str</c>/<c>b</c>/<c>n</c> (su <c>&lt;v&gt;</c>).</summary>
    private static string Celda(XmlReader lector, List<string> compartidos)
    {
        var tipo = lector.GetAttribute("t");
        if (lector.IsEmptyElement)
            return string.Empty;

        var profundidad = lector.Depth;
        string? valor = null;
        var enLinea = new StringBuilder();
        while (lector.Read() && !(lector.NodeType == XmlNodeType.EndElement && lector.Depth == profundidad))
        {
            if (lector.NodeType != XmlNodeType.Element)
                continue;
            if (lector.LocalName == "v")
                valor = lector.ReadElementContentAsString();
            else if (lector.LocalName == "t")
                enLinea.Append(lector.ReadElementContentAsString());
            // ReadElementContentAsString deja el lector en el nodo siguiente: si es el cierre de la celda, termina.
            if (lector.NodeType == XmlNodeType.EndElement && lector.Depth == profundidad)
                break;
        }

        return tipo switch
        {
            "s" => int.TryParse(valor, out var i) && i >= 0 && i < compartidos.Count ? compartidos[i] : string.Empty,
            "inlineStr" => enLinea.ToString(),
            _ => valor ?? enLinea.ToString()
        };
    }

    /// <summary><c>B12</c> → 1 (índice desde 0).</summary>
    private static int? Columna(string? referencia)
    {
        if (string.IsNullOrEmpty(referencia))
            return null;
        var indice = 0;
        var letras = 0;
        foreach (var c in referencia)
        {
            if (c is < 'A' or > 'Z')
                break;
            indice = indice * 26 + (c - 'A' + 1);
            letras++;
        }

        return letras == 0 ? null : indice - 1;
    }
}
