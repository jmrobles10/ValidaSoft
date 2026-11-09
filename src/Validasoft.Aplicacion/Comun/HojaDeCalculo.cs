using System.IO.Compression;
using System.Security;
using System.Text;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Libro de Excel (.xlsx, Office Open XML) de una sola hoja con encabezado y filas de texto, número o fecha, sin
/// dependencias: lo justo para exportar informes (VE-2). La primera fila queda en negrita y fija al desplazarse.
/// </summary>
public static class HojaDeCalculo
{
    public const string TipoContenido = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Celdas: <see cref="string"/> (texto), números (<see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>,
    /// <see cref="double"/>), <see cref="DateOnly"/> (fecha con formato dd/mm/aaaa) o <c>null</c> (vacía).
    /// </summary>
    public static byte[] Crear(string nombreHoja, IReadOnlyList<string> encabezado, IEnumerable<IReadOnlyList<object?>> filas)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escribir(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            Escribir(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            Escribir(zip, "xl/workbook.xml", $"""
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                <sheets><sheet name="{Xml(Recortar(nombreHoja))}" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            Escribir(zip, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            // Estilos: 0 normal, 1 encabezado en negrita, 2 fecha dd/mm/yyyy.
            Escribir(zip, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                <numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy"/></numFmts>
                <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts>
                <fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills>
                <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                <cellXfs count="3"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>
                <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
                </styleSheet>
                """);

            var hoja = new StringBuilder();
            hoja.Append("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>""")
                .Append("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""")
                .Append("""<sheetViews><sheetView workbookViewId="0"><pane ySplit="1" topLeftCell="A2" activePane="bottomLeft" state="frozen"/></sheetView></sheetViews>""")
                .Append("<sheetData>");
            Fila(hoja, 1, encabezado.Cast<object?>().ToList(), encabezado: true);
            var numero = 2;
            foreach (var fila in filas)
                Fila(hoja, numero++, fila, encabezado: false);
            hoja.Append("</sheetData></worksheet>");
            Escribir(zip, "xl/worksheets/sheet1.xml", hoja.ToString());
        }

        return memoria.ToArray();
    }

    private static void Fila(StringBuilder hoja, int numero, IReadOnlyList<object?> celdas, bool encabezado)
    {
        hoja.Append($"<row r=\"{numero}\">");
        for (var i = 0; i < celdas.Count; i++)
        {
            var referencia = $"{Columna(i)}{numero}";
            switch (celdas[i])
            {
                case null:
                    break;
                case DateOnly fecha:
                    // Serial de Excel: días desde el 30/12/1899.
                    hoja.Append($"<c r=\"{referencia}\" s=\"2\"><v>{fecha.DayNumber - new DateOnly(1899, 12, 30).DayNumber}</v></c>");
                    break;
                case int or long or decimal or double:
                    hoja.Append($"<c r=\"{referencia}\"><v>{Convert.ToString(celdas[i], System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                    break;
                default:
                    hoja.Append($"<c r=\"{referencia}\" t=\"inlineStr\"{(encabezado ? " s=\"1\"" : string.Empty)}><is><t xml:space=\"preserve\">{Xml(celdas[i]!.ToString() ?? string.Empty)}</t></is></c>");
                    break;
            }
        }

        hoja.Append("</row>");
    }

    /// <summary>A, B, …, Z, AA, AB…</summary>
    public static string Columna(int indice)
    {
        var nombre = string.Empty;
        for (var n = indice + 1; n > 0; n = (n - 1) / 26)
            nombre = (char)('A' + (n - 1) % 26) + nombre;
        return nombre;
    }

    private static string Recortar(string nombre) => nombre.Length <= 31 ? nombre : nombre[..31];

    private static string Xml(string texto) => SecurityElement.Escape(new string([.. texto.Where(c => c is '\t' or '\n' or '\r' || c >= ' ')]));

    private static void Escribir(ZipArchive zip, string ruta, string contenido)
    {
        using var flujo = zip.CreateEntry(ruta, CompressionLevel.Optimal).Open();
        var bytes = new UTF8Encoding(false).GetBytes(contenido);
        flujo.Write(bytes);
    }
}
