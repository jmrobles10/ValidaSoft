using Validasoft.Aplicacion.Informes;

namespace Validasoft.Infraestructura.Certificados;

/// <summary>
/// PDF de los informes (IF-3) con el mismo lienzo, fuentes estándar y estilo del certificado: cabecera con la empresa, su
/// identificación y la etiqueta a la derecha; secciones con título y filete; tablas con encabezado gris en negrita y filas
/// que crecen con la celda más alta; pie con el texto del informe y «página n de m». Sin librerías ni fuentes externas.
/// </summary>
public sealed class GeneradorPdfDeInformes : IGeneradorDePdfDeInformes
{
    private const double Margen = 40;
    private const double TopeDelPie = Margen + 24;
    private const double Tamano = 8;

    public byte[] Generar(DocumentoPdf documento)
    {
        var (ancho, alto) = documento.Horizontal ? (841.89, 595.28) : (595.28, 841.89);
        var pdf = new LienzoPdf(ancho, alto);
        var pagina = new Pagina(pdf, ancho, alto);
        var y = Encabezado(pagina, documento);

        foreach (var bloque in documento.Bloques)
        {
            y = bloque switch
            {
                DatosPdf datos => Datos(pagina, y, datos),
                TablaPdf tabla => Tabla(pagina, y, tabla),
                NotaPdf nota => Nota(pagina, y - 6, nota.Texto),
                _ => y
            };
        }

        return pdf.Construir(documento.Titulo, documento.Empresa, documento.GeneradoEn, (lienzo, numeracion) =>
        {
            lienzo.Linea(Margen, Margen + 8, ancho - Margen, Margen + 8, 0.3);
            var numero = $"página {numeracion.Item1} de {numeracion.Item2}";
            lienzo.Texto(Margen, Margen - 4, Fuente.Helvetica, 7, documento.Pie, 0.45);
            lienzo.Texto(ancho - Margen - Medidas.Ancho(numero, Fuente.Helvetica, 7), Margen - 4, Fuente.Helvetica, 7, numero, 0.45);
        });
    }

    private sealed record Pagina(LienzoPdf Pdf, double Ancho, double Alto)
    {
        public double Util => Ancho - 2 * Margen;
    }

    private static double Encabezado(Pagina p, DocumentoPdf d)
    {
        var y = p.Alto - Margen;
        p.Pdf.Texto(Margen, y - 14, Fuente.HelveticaBold, 14, d.Empresa);
        if (d.IdentificacionEmpresa is { } identificacion)
            p.Pdf.Texto(Margen, y - 27, Fuente.Helvetica, 9, identificacion, 0.35);
        var etiqueta = d.Etiqueta.ToUpperInvariant();
        p.Pdf.Texto(p.Ancho - Margen - Medidas.Ancho(etiqueta, Fuente.HelveticaBold, 10), y - 12, Fuente.HelveticaBold, 10, etiqueta, 0.35);
        y -= 36;
        p.Pdf.Linea(Margen, y, p.Ancho - Margen, y, 0.6);
        y -= 22;
        p.Pdf.Texto(Margen, y, Fuente.HelveticaBold, 14, d.Titulo);
        if (d.Subtitulo is { Length: > 0 } subtitulo)
        {
            foreach (var linea in Medidas.Partir(subtitulo, Fuente.Helvetica, 9, p.Util))
            {
                y -= 13;
                p.Pdf.Texto(Margen, y, Fuente.Helvetica, 9, linea, 0.35);
            }
        }

        return y - 6;
    }

    private static double Seccion(Pagina p, double y, string titulo)
    {
        y = Espacio(p, y, 40);
        y -= 18;
        p.Pdf.Texto(Margen, y, Fuente.HelveticaBold, 10.5, titulo);
        y -= 5;
        p.Pdf.Linea(Margen, y, p.Ancho - Margen, y, 0.3);
        return y - 4;
    }

    private static double Datos(Pagina p, double y, DatosPdf datos)
    {
        y = Seccion(p, y, datos.Titulo);
        const double etiqueta = 190;
        foreach (var dato in datos.Datos)
        {
            var lineas = Medidas.Partir(dato.Valor, Fuente.Helvetica, 9, p.Util - etiqueta);
            y = Espacio(p, y, 12 * lineas.Count + 2);
            p.Pdf.Texto(Margen, y - 11, Fuente.HelveticaBold, 8.5, dato.Etiqueta, 0.25);
            foreach (var linea in lineas)
            {
                y -= 12;
                p.Pdf.Texto(Margen + etiqueta, y + 1, Fuente.Helvetica, 9, linea);
            }

            y -= 2;
        }

        return y;
    }

    private static double Tabla(Pagina p, double y, TablaPdf tabla)
    {
        y = Seccion(p, y, tabla.Titulo);
        var total = tabla.Columnas.Sum(c => c.Peso);
        var anchos = tabla.Columnas.Select(c => p.Util * c.Peso / total).ToList();
        y = Fila(p, y, anchos, [.. tabla.Columnas.Select(c => c.Titulo)], encabezado: true);
        if (tabla.Filas.Count == 0)
            return Nota(p, y - 2, tabla.Vacio);
        foreach (var fila in tabla.Filas)
        {
            // En una página nueva se repite el encabezado de la tabla.
            var alto = AltoDeFila(anchos, fila, Fuente.Helvetica);
            if (y - alto < TopeDelPie)
            {
                p.Pdf.NuevaPagina();
                y = Fila(p, p.Alto - Margen, anchos, [.. tabla.Columnas.Select(c => c.Titulo)], encabezado: true);
            }

            y = Fila(p, y, anchos, fila, encabezado: false);
        }

        return y;
    }

    private static double AltoDeFila(IReadOnlyList<double> anchos, IReadOnlyList<string> celdas, Fuente fuente) =>
        10 * anchos.Select((a, i) => Medidas.Partir(i < celdas.Count ? celdas[i] : string.Empty, fuente, Tamano, a - 4).Count).Max() + 5;

    private static double Fila(Pagina p, double y, IReadOnlyList<double> anchos, IReadOnlyList<string> celdas, bool encabezado)
    {
        var fuente = encabezado ? Fuente.HelveticaBold : Fuente.Helvetica;
        var alto = AltoDeFila(anchos, celdas, fuente);
        y = Espacio(p, y, alto);
        var x = Margen;
        for (var i = 0; i < anchos.Count; i++)
        {
            var linea = y - 9;
            foreach (var texto in Medidas.Partir(i < celdas.Count ? celdas[i] : string.Empty, fuente, Tamano, anchos[i] - 4))
            {
                p.Pdf.Texto(x + 2, linea, fuente, Tamano, texto, encabezado ? 0.25 : 0);
                linea -= 10;
            }

            x += anchos[i];
        }

        y -= alto;
        p.Pdf.Linea(Margen, y + 2, p.Ancho - Margen, y + 2, encabezado ? 0.5 : 0.15);
        return y;
    }

    private static double Nota(Pagina p, double y, string texto)
    {
        var lineas = Medidas.Partir(texto, Fuente.Helvetica, 8, p.Util);
        y = Espacio(p, y, 10 * lineas.Count + 4);
        foreach (var linea in lineas)
        {
            y -= 10;
            p.Pdf.Texto(Margen, y, Fuente.Helvetica, 8, linea, 0.35);
        }

        return y - 4;
    }

    private static double Espacio(Pagina p, double y, double necesario)
    {
        if (y - necesario >= TopeDelPie)
            return y;
        p.Pdf.NuevaPagina();
        return p.Alto - Margen;
    }
}
