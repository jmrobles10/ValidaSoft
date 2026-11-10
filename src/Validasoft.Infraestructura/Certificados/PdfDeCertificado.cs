using System.Globalization;
using System.Text;
using QRCoder;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Dominio.Certificados;

namespace Validasoft.Infraestructura.Certificados;

/// <summary>
/// PDF del certificado (docs/03 §7.1) escrito directamente en PDF 1.7 con las fuentes estándar (Helvetica y Courier,
/// WinAnsi): sin librería de maquetación ni fuentes que distribuir, y <b>determinista</b> (mismo contenido ⇒ mismos bytes,
/// así regenerarlo da el mismo SHA-256). El QR (QRCoder, MIT) apunta a la URL pública de verificación y va abajo a la
/// derecha (25 mm). No es PDF/A-2b: las fuentes estándar no se incrustan y no lleva perfil ICC ni XMP (CE-6).
/// </summary>
public sealed class GeneradorPdfDeCertificados : IGeneradorDePdfDeCertificados
{
    private const double Ancho = 595.28;
    private const double Alto = 841.89;
    private const double Margen = 48;
    private const double LadoQr = 70.87; // 25 mm
    private const double TopeDelPie = Margen + 112;

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public byte[] Generar(DatosDelPdf datos)
    {
        var c = datos.Contenido;
        var pdf = new LienzoPdf();
        var y = Encabezado(pdf, c, datos);

        y = Seccion(pdf, y, "Titular y validación");
        y = Dato(pdf, y, "Titular", c.Tercero.Nombre);
        y = Dato(pdf, y, "Identificación", $"{c.Tercero.IdentificacionEnmascarada} ({c.Tercero.Pais}, {Persona(c.Tercero.TipoPersona)})");
        y = Dato(pdf, y, "Tipo de vínculo", c.TipoVinculo.Nombre);
        y = Dato(pdf, y, "Flujo aprobatorio", $"{c.Flujo.Nombre} ({c.Flujo.Codigo}, versión {c.Flujo.Version})");
        y = Dato(pdf, y, "Empresa", $"{c.Empresa.Nombre} ({c.Empresa.Identificacion.Tipo} {c.Empresa.Identificacion.Numero})");
        y = Dato(pdf, y, "Emitido", c.EmitidoEn.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", Invariante));
        y = Dato(pdf, y, "Vigente hasta", c.VigenteHasta?.ToString("yyyy-MM-dd", Invariante) ?? "Sin vencimiento por documentos");

        y = Seccion(pdf, y - 6, "Documentos aceptados");
        y = Fila(pdf, y, true, ("Documento", 170), ("Versión", 46), ("Vigente hasta", 74), ("SHA-256 / exención", 209));
        if (c.Requisitos.Count == 0)
            y = Fila(pdf, y, false, ("El flujo no pide documentos.", 499));
        foreach (var r in c.Requisitos.OrderBy(r => r.Codigo, StringComparer.Ordinal))
            y = Fila(pdf, y, false, ($"{r.Nombre} ({r.Codigo})", 170), (r.Eximido ? "—" : $"v{r.VersionN}", 46),
                (r.VigenteHasta?.ToString("yyyy-MM-dd", Invariante) ?? "—", 74),
                (r.Eximido ? $"Eximido: {r.Justificacion}" : Partir(r.Sha256 ?? string.Empty, 32), 209));

        y = Seccion(pdf, y - 6, "Aprobaciones");
        y = Fila(pdf, y, true, ("Área", 250), ("Forma de aprobación", 130), ("Fecha (UTC)", 119));
        foreach (var a in c.Areas)
            y = Fila(pdf, y, false, (a.Area, 250), (Origen(a.Origen), 130), (a.Fecha.UtcDateTime.ToString("yyyy-MM-dd HH:mm", Invariante), 119));

        if (c.Verificaciones.Count > 0)
        {
            y = Seccion(pdf, y - 6, "Verificaciones de cumplimiento");
            y = Fila(pdf, y, true, ("Verificación", 200), ("Proveedor", 130), ("Resultado", 90), ("Fecha (UTC)", 79));
            foreach (var v in c.Verificaciones)
                y = Fila(pdf, y, false, (v.Tipo == "EstudioDeCumplimiento" ? "Estudio de cumplimiento" : "Listas restrictivas", 200), (v.Proveedor, 130),
                    (v.Resultado switch { "SinCoincidencias" => "Sin coincidencias", "Coincidencias" => "Coincidencias descartadas", "Aprobado" => "Favorable", _ => v.Resultado }, 90),
                    (v.Fecha.UtcDateTime.ToString("yyyy-MM-dd", Invariante), 79));
        }

        y -= 10;
        y = Nota(pdf, y, "La validez de este certificado se consulta en línea: su estado puede cambiar después de emitido (suspensión, " +
                         "revocación, vencimiento o reemplazo por una emisión nueva). No incluye contactos ni la identidad de quienes revisaron.");
        _ = y;

        return pdf.Construir($"Certificado {datos.Folio}", $"SHA-256 {datos.HashContenido}", datos.Contenido.EmitidoEn,
            (lienzo, numeracion) => Pie(lienzo, numeracion, datos));
    }

    private static double Encabezado(LienzoPdf pdf, ContenidoDeCertificado c, DatosDelPdf datos)
    {
        var y = Alto - Margen;
        pdf.Texto(Margen, y - 14, Fuente.HelveticaBold, 16, c.Empresa.Nombre);
        pdf.Texto(Margen, y - 28, Fuente.Helvetica, 9, $"{c.Empresa.Identificacion.Tipo} {c.Empresa.Identificacion.Numero}" +
                                                       (c.Empresa.Identificacion.DigitoVerificacion is { } dv ? $"-{dv}" : string.Empty), 0.35);
        var titulo = "CERTIFICADO VERIFICABLE";
        pdf.Texto(Ancho - Margen - Medidas.Ancho(titulo, Fuente.HelveticaBold, 10), y - 12, Fuente.HelveticaBold, 10, titulo, 0.35);
        y -= 40;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.6);

        y -= 26;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 15, "Certificado de validación de tercero");
        y -= 22;
        pdf.Texto(Margen, y, Fuente.Helvetica, 9, "Folio", 0.35);
        pdf.Texto(Margen + 60, y, Fuente.CourierBold, 12, datos.Folio);
        pdf.Texto(Margen + 300, y, Fuente.Helvetica, 9, "Huella", 0.35);
        pdf.Texto(Margen + 345, y, Fuente.CourierBold, 12, HuellaDeCertificado.Formatear(datos.HashContenido));
        return y - 10;
    }

    private static double Seccion(LienzoPdf pdf, double y, string titulo)
    {
        y = Espacio(pdf, y, 34);
        y -= 18;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 11, titulo);
        y -= 5;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.3);
        return y - 4;
    }

    private static double Dato(LienzoPdf pdf, double y, string etiqueta, string valor)
    {
        var lineas = Medidas.Partir(valor, Fuente.Helvetica, 10, Ancho - 2 * Margen - 120);
        y = Espacio(pdf, y, 14 * lineas.Count + 2);
        pdf.Texto(Margen, y - 12, Fuente.HelveticaBold, 9, etiqueta, 0.25);
        foreach (var linea in lineas)
        {
            y -= 14;
            pdf.Texto(Margen + 120, y + 2, Fuente.Helvetica, 10, linea);
        }

        return y - 2;
    }

    /// <summary>Fila de tabla con columnas de ancho fijo; cada celda se parte en líneas y la fila crece con la más alta.</summary>
    private static double Fila(LienzoPdf pdf, double y, bool encabezado, params (string Texto, double Ancho)[] celdas)
    {
        var fuente = encabezado ? Fuente.HelveticaBold : Fuente.Helvetica;
        const double tamano = 8.5;
        var lineas = celdas.Select(c => c.Texto.Contains('\n')
            ? c.Texto.Split('\n').ToList()
            : Medidas.Partir(c.Texto, fuente, tamano, c.Ancho - 6)).ToList();
        var alto = 11 * lineas.Max(l => l.Count) + 5;
        y = Espacio(pdf, y, alto);

        var x = Margen;
        for (var i = 0; i < celdas.Length; i++)
        {
            var esHash = !encabezado && lineas[i].Count > 0 && lineas[i].All(l => l.Length == 32 && l.All(char.IsAsciiHexDigitLower));
            var linea = y - 10;
            foreach (var texto in lineas[i])
            {
                pdf.Texto(x + 2, linea, esHash ? Fuente.Courier : fuente, esHash ? 7.5 : tamano, texto, encabezado ? 0.25 : 0);
                linea -= 11;
            }

            x += celdas[i].Ancho;
        }

        y -= alto;
        pdf.Linea(Margen, y + 2, Ancho - Margen, y + 2, encabezado ? 0.5 : 0.15);
        return y;
    }

    private static double Nota(LienzoPdf pdf, double y, string texto)
    {
        var lineas = Medidas.Partir(texto, Fuente.Helvetica, 8, Ancho - 2 * Margen);
        y = Espacio(pdf, y, 10 * lineas.Count);
        foreach (var linea in lineas)
        {
            y -= 10;
            pdf.Texto(Margen, y, Fuente.Helvetica, 8, linea, 0.35);
        }

        return y;
    }

    /// <summary>Si lo que sigue no cabe sobre el pie, abre una página nueva.</summary>
    private static double Espacio(LienzoPdf pdf, double y, double necesario)
    {
        if (y - necesario >= TopeDelPie)
            return y;
        pdf.NuevaPagina();
        return Alto - Margen;
    }

    /// <summary>Pie de cada página: URL de verificación, hash completo, sello y QR (abajo a la derecha, 25 mm).</summary>
    private static void Pie(LienzoPdf pdf, (int Pagina, int Total) numeracion, DatosDelPdf datos)
    {
        var anchoTexto = Ancho - 2 * Margen - LadoQr - 14;
        var y = Margen + LadoQr + 26;
        pdf.Linea(Margen, y + 8, Ancho - Margen, y + 8, 0.3);

        pdf.Texto(Margen, y - 4, Fuente.HelveticaBold, 8, "Verifique este certificado en", 0.25);
        y -= 4;
        foreach (var linea in Medidas.Partir(datos.UrlVerificacion, Fuente.Helvetica, 8, anchoTexto, cortarPalabras: true))
        {
            y -= 10;
            pdf.Texto(Margen, y, Fuente.Helvetica, 8, linea);
        }

        y -= 14;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 7.5, "Hash del contenido (SHA-256)", 0.25);
        y -= 9;
        pdf.Texto(Margen, y, Fuente.Courier, 7, datos.HashContenido);
        y -= 13;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 7.5, $"Sello Ed25519 de la plataforma ({datos.Kid})", 0.25);
        foreach (var linea in Medidas.Partir(datos.Sello, Fuente.Courier, 6, anchoTexto, cortarPalabras: true))
        {
            y -= 8;
            pdf.Texto(Margen, y, Fuente.Courier, 6, linea);
        }

        var numero = $"{datos.Folio} · página {numeracion.Pagina} de {numeracion.Total}";
        pdf.Texto(Margen, Margen - 14, Fuente.Helvetica, 7, numero, 0.45);
        pdf.Qr(Ancho - Margen - LadoQr, Margen - 4, LadoQr, datos.UrlVerificacion);
    }

    private static string Persona(string tipo) => tipo switch
    {
        "Natural" => "persona natural",
        "Juridica" => "persona jurídica",
        "Consorcio" => "consorcio o unión temporal",
        _ => tipo
    };

    private static string Origen(string origen) => origen switch
    {
        "Humano" => "Revisión humana",
        "Sistema" => "Automática (sistema)",
        "Administrador" => "Administrador",
        _ => origen
    };

    private static string Partir(string texto, int cada) =>
        string.Join('\n', Enumerable.Range(0, (texto.Length + cada - 1) / cada).Select(i => texto.Substring(i * cada, Math.Min(cada, texto.Length - i * cada))));
}

internal enum Fuente
{
    Helvetica,
    HelveticaBold,
    Courier,
    CourierBold
}

/// <summary>Anchos de las fuentes estándar (métricas AFM de Adobe, por 1000 unidades) para partir líneas.</summary>
internal static class Medidas
{
    private static readonly int[] Helvetica =
    [
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278,
        584, 584, 584, 556, 1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667,
        944, 667, 667, 611, 278, 278, 278, 469, 556, 333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556, 556, 556,
        333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584
    ];

    private static readonly int[] HelveticaBold =
    [
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278, 556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333,
        584, 584, 584, 611, 975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778, 667, 778, 722, 667, 611, 722, 667,
        944, 667, 667, 611, 333, 278, 333, 584, 556, 333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611, 611, 611,
        389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584
    ];

    public static double Ancho(string texto, Fuente fuente, double tamano) => texto.Sum(c => Unidades(c, fuente)) * tamano / 1000;

    /// <summary>Parte en líneas que caben en <paramref name="ancho"/>; una palabra más larga que la línea se corta.</summary>
    public static List<string> Partir(string texto, Fuente fuente, double tamano, double ancho, bool cortarPalabras = false)
    {
        var lineas = new List<string>();
        var actual = new StringBuilder();
        foreach (var palabra in cortarPalabras ? texto.Select(c => c.ToString()) : texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separador = actual.Length > 0 && !cortarPalabras ? " " : string.Empty;
            if (Ancho(actual + separador + palabra, fuente, tamano) <= ancho)
            {
                actual.Append(separador).Append(palabra);
                continue;
            }

            if (actual.Length > 0)
                lineas.Add(actual.ToString());
            actual.Clear();
            var resto = palabra;
            while (Ancho(resto, fuente, tamano) > ancho && resto.Length > 1)
            {
                var corte = resto.Length - 1;
                while (corte > 1 && Ancho(resto[..corte], fuente, tamano) > ancho)
                    corte--;
                lineas.Add(resto[..corte]);
                resto = resto[corte..];
            }

            actual.Append(resto);
        }

        if (actual.Length > 0 || lineas.Count == 0)
            lineas.Add(actual.ToString());
        return lineas;
    }

    private static int Unidades(char c, Fuente fuente)
    {
        if (fuente is Fuente.Courier or Fuente.CourierBold)
            return 600;
        var tabla = fuente == Fuente.HelveticaBold ? HelveticaBold : Helvetica;
        var basico = c is >= ' ' and <= '~' ? c : c.ToString().Normalize(NormalizationForm.FormD)[0];
        return basico is >= ' ' and <= '~' ? tabla[basico - ' '] : 556;
    }
}

/// <summary>Escritor PDF mínimo: páginas A4, texto WinAnsi con las cuatro fuentes estándar, líneas, rectángulos y QR.</summary>
internal sealed class LienzoPdf
{
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;
    private readonly List<StringBuilder> _paginas = [];
    private int _actual;

    private readonly double _ancho;
    private readonly double _alto;

    /// <summary>A4 vertical por defecto; con las medidas al revés, horizontal (los informes anchos, IF-1).</summary>
    public LienzoPdf(double ancho = 595.28, double alto = 841.89)
    {
        _ancho = ancho;
        _alto = alto;
        NuevaPagina();
    }

    private StringBuilder Actual => _paginas[_actual];

    public void NuevaPagina()
    {
        _paginas.Add(new StringBuilder());
        _actual = _paginas.Count - 1;
    }

    /// <summary>Texto en (x, y) de la línea base; <paramref name="gris"/> 0 = negro.</summary>
    public void Texto(double x, double y, Fuente fuente, double tamano, string texto, double gris = 0)
    {
        var nombre = fuente switch
        {
            Fuente.Helvetica => "F1",
            Fuente.HelveticaBold => "F2",
            Fuente.Courier => "F3",
            _ => "F4"
        };
        Actual.Append(Invariante, $"BT {N(gris)} g /{nombre} {N(tamano)} Tf {N(x)} {N(y)} Td (").Append(Cadena(texto)).Append(") Tj ET\n");
    }

    public void Linea(double x1, double y1, double x2, double y2, double grosor) =>
        Actual.Append(Invariante, $"q 0.6 G {N(grosor)} w {N(x1)} {N(y1)} m {N(x2)} {N(y2)} l S Q\n");

    /// <summary>QR de <paramref name="texto"/> (corrección M) como rectángulos negros; incluye la zona de silencio.</summary>
    public void Qr(double x, double y, double lado, string texto)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(texto, QRCodeGenerator.ECCLevel.M);
        var matriz = datos.ModuleMatrix;
        var modulo = lado / matriz.Count;
        Actual.Append("q 0 g\n");
        for (var fila = 0; fila < matriz.Count; fila++)
        {
            var columna = 0;
            while (columna < matriz[fila].Count)
            {
                if (!matriz[fila][columna])
                {
                    columna++;
                    continue;
                }

                var inicio = columna;
                while (columna < matriz[fila].Count && matriz[fila][columna])
                    columna++;
                var arriba = y + lado - (fila + 1) * modulo;
                Actual.Append(Invariante, $"{N(x + inicio * modulo)} {N(arriba)} {N((columna - inicio) * modulo)} {N(modulo)} re\n");
            }
        }

        Actual.Append("f Q\n");
    }

    /// <summary>Arma el archivo del certificado (título con el folio, asunto con el hash).</summary>
    public byte[] Construir(DatosDelPdf datos, Action<LienzoPdf, (int, int)> pie) =>
        Construir($"Certificado {datos.Folio}", $"SHA-256 {datos.HashContenido}", datos.Contenido.EmitidoEn, pie);

    /// <summary>Arma el archivo: catálogo, páginas, fuentes, información y tabla de referencias cruzadas.</summary>
    public byte[] Construir(string titulo, string asunto, DateTimeOffset fecha, Action<LienzoPdf, (int, int)> pie)
    {
        // El pie (con «página n de m») se escribe al final, cuando ya se sabe cuántas páginas hay.
        var total = _paginas.Count;
        for (_actual = 0; _actual < total; _actual++)
            pie(this, (_actual + 1, total));

        var objetos = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /Lang (es-CO) >>",
            string.Empty, // páginas: se completa abajo
            Fuente1("Helvetica"),
            Fuente1("Helvetica-Bold"),
            Fuente1("Courier"),
            Fuente1("Courier-Bold"),
            $"<< /Title ({Cadena(titulo)}) /Subject ({Cadena(asunto)}) " +
            $"/Creator (Validasoft) /Producer (Validasoft) /CreationDate ({Fecha(fecha)}) >>"
        };

        var hijos = new List<string>();
        foreach (var pagina in _paginas)
        {
            var contenido = pagina.ToString();
            objetos.Add($"<< /Length {Encoding.ASCII.GetByteCount(contenido)} >>\nstream\n{contenido}endstream");
            var contenidoId = objetos.Count;
            objetos.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {N(_ancho)} {N(_alto)}] /Contents {contenidoId} 0 R " +
                        "/Resources << /Font << /F1 3 0 R /F2 4 0 R /F3 5 0 R /F4 6 0 R >> >> >>");
            hijos.Add($"{objetos.Count} 0 R");
        }

        objetos[1] = $"<< /Type /Pages /Kids [{string.Join(' ', hijos)}] /Count {hijos.Count} >>";

        using var salida = new MemoryStream();
        void Escribir(string texto) => salida.Write(Encoding.ASCII.GetBytes(texto));
        Escribir("%PDF-1.7\n");
        salida.Write([0x25, 0xE2, 0xE3, 0xCF, 0xD3, 0x0A]); // comentario binario: el archivo no es texto plano
        var posiciones = new List<long>();
        for (var i = 0; i < objetos.Count; i++)
        {
            posiciones.Add(salida.Position);
            Escribir($"{i + 1} 0 obj\n{objetos[i]}\nendobj\n");
        }

        var xref = salida.Position;
        var tabla = new StringBuilder();
        tabla.Append(Invariante, $"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
        foreach (var posicion in posiciones)
            tabla.Append(posicion.ToString("D10", Invariante)).Append(" 00000 n \n");
        tabla.Append(Invariante, $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R /Info 7 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        Escribir(tabla.ToString());
        return salida.ToArray();
    }

    private static string Fuente1(string nombre) => $"<< /Type /Font /Subtype /Type1 /BaseFont /{nombre} /Encoding /WinAnsiEncoding >>";

    private static string Fecha(DateTimeOffset fecha) => "D:" + fecha.UtcDateTime.ToString("yyyyMMddHHmmss", Invariante) + "Z";

    private static string N(double valor) => Math.Round(valor, 2).ToString("0.##", Invariante);

    /// <summary>Cadena literal PDF en WinAnsi: escapa <c>\ ( )</c> y escribe lo no ASCII en octal (el flujo queda en ASCII).</summary>
    private static string Cadena(string texto)
    {
        var salida = new StringBuilder();
        foreach (var c in texto)
        {
            var b = WinAnsi(c);
            if (b is (byte)'\\' or (byte)'(' or (byte)')')
                salida.Append('\\').Append((char)b);
            else if (b is < 0x20 or > 0x7E)
                salida.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
            else
                salida.Append((char)b);
        }

        return salida.ToString();
    }

    private static byte WinAnsi(char c) => c switch
    {
        >= ' ' and <= '~' => (byte)c,
        >= '\u00A0' and <= '\u00FF' => (byte)c,
        '€' => 0x80,
        '…' => 0x85,
        '‘' => 0x91,
        '’' => 0x92,
        '“' => 0x93,
        '”' => 0x94,
        '•' => 0x95,
        '–' => 0x96,
        '—' => 0x97,
        _ => (byte)'?'
    };
}
