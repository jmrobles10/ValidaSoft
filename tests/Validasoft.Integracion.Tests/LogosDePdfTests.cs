using System.IO.Compression;
using System.Text;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Dominio.Ficha;
using Validasoft.Infraestructura.Ficha;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// El logo del PDF de la declaración PEP (DP-18), sin base ni red: el decodificador de PNG (todos los tipos de color, 16 bits, paleta con
/// transparencia y Adam7), el dibujo de SVG como vectores (formas, trazados, arcos, degradado, estilos, <c>use</c>), la elección del logo
/// (el de la empresa si se puede dibujar; si no, el de Validasoft) y que el PDF lo lleve en su encabezado. Con
/// <c>VALIDASOFT_PDF_SALIDA</c> los PDF generados quedan en esa carpeta para verlos como imagen.
/// </summary>
public sealed class LogosDePdfTests
{
    // ---------- Un codificador de PNG mínimo para armar las entradas ----------

    private static byte[] Trozo(string tipo, byte[] datos)
    {
        var salida = new List<byte> { (byte)(datos.Length >> 24), (byte)(datos.Length >> 16), (byte)(datos.Length >> 8), (byte)datos.Length };
        salida.AddRange(Encoding.ASCII.GetBytes(tipo));
        salida.AddRange(datos);
        salida.AddRange(new byte[4]); // el CRC no se comprueba
        return [.. salida];
    }

    /// <summary>PNG de <paramref name="ancho"/> × <paramref name="alto"/>; <paramref name="muestras"/> da los canales (ya a la profundidad pedida) de cada píxel.</summary>
    private static byte[] Png(int ancho, int alto, int tipoColor, int profundidad, Func<int, int, int[]> muestras, bool entrelazado = false, byte[]? paleta = null, byte[]? trns = null)
    {
        var canales = tipoColor switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        byte[] Fila(IEnumerable<(int X, int Y)> puntos)
        {
            var bits = new List<bool>();
            foreach (var (x, y) in puntos)
            {
                foreach (var muestra in muestras(x, y))
                {
                    for (var b = profundidad - 1; b >= 0; b--)
                        bits.Add(((muestra >> b) & 1) == 1);
                }
            }

            var bytes = new byte[(bits.Count + 7) / 8];
            for (var i = 0; i < bits.Count; i++)
            {
                if (bits[i])
                    bytes[i / 8] |= (byte)(0x80 >> (i % 8));
            }

            return bytes;
        }

        var crudo = new List<byte>();
        (int X, int Y, int DX, int DY)[] pasadas = entrelazado ? [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)] : [(0, 0, 1, 1)];
        foreach (var (px, py, dx, dy) in pasadas)
        {
            for (var y = py; y < alto; y += dy)
            {
                var puntos = Enumerable.Range(0, ancho).Where(x => x >= px && (x - px) % dx == 0).Select(x => (x, y)).ToList();
                if (puntos.Count == 0)
                    continue;
                crudo.Add(0);
                crudo.AddRange(Fila(puntos));
            }
        }

        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write([.. crudo]);

        _ = canales;
        var cabecera = new byte[13];
        cabecera[3] = (byte)ancho;
        cabecera[7] = (byte)alto;
        cabecera[8] = (byte)profundidad;
        cabecera[9] = (byte)tipoColor;
        cabecera[12] = (byte)(entrelazado ? 1 : 0);
        var png = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        png.AddRange(Trozo("IHDR", cabecera));
        if (paleta is not null)
            png.AddRange(Trozo("PLTE", paleta));
        if (trns is not null)
            png.AddRange(Trozo("tRNS", trns));
        png.AddRange(Trozo("IDAT", comprimido.ToArray()));
        png.AddRange(Trozo("IEND", []));
        return [.. png];
    }

    private static string DataUri(string tipo, byte[] bytes) => $"data:{tipo};base64,{Convert.ToBase64String(bytes)}";

    private static string DataUriSvg(string svg) => DataUri("image/svg+xml", Encoding.UTF8.GetBytes(svg));

    // ---------- Decodificador de PNG ----------

    [Fact]
    public void Un_png_con_alfa_compone_la_transparencia_sobre_blanco()
    {
        int[][] pixeles = [[255, 0, 0, 255], [0, 0, 255, 128], [0, 255, 0, 0], [0, 0, 0, 255]];
        var imagen = DecodificadorPng.Decodificar(Png(2, 2, 6, 8, (x, y) => pixeles[y * 2 + x])).ShouldNotBeNull();
        (imagen.Ancho, imagen.Alto).ShouldBe((2, 2));
        imagen.Rgb.Take(3).ShouldBe(new byte[] { 255, 0, 0 });
        imagen.Rgb.Skip(3).Take(3).ShouldBe(new byte[] { 127, 127, 255 }); // azul al 50 % sobre blanco
        imagen.Rgb.Skip(6).Take(3).ShouldBe(new byte[] { 255, 255, 255 }); // transparente = blanco
        imagen.Rgb.Skip(9).Take(3).ShouldBe(new byte[] { 0, 0, 0 });
    }

    [Fact]
    public void La_paleta_de_4_bits_con_transparencia_y_el_gris_de_16_bits_se_leen()
    {
        byte[] paleta = [255, 0, 0, 0, 128, 0, 0, 0, 255];
        var conPaleta = DecodificadorPng.Decodificar(Png(4, 1, 3, 4, (x, _) => [x % 3], paleta: paleta, trns: [255, 0])).ShouldNotBeNull();
        conPaleta.Rgb.Take(3).ShouldBe(new byte[] { 255, 0, 0 });   // índice 0, opaco
        conPaleta.Rgb.Skip(3).Take(3).ShouldBe(new byte[] { 255, 255, 255 }); // índice 1, alfa 0
        conPaleta.Rgb.Skip(6).Take(3).ShouldBe(new byte[] { 0, 0, 255 });
        conPaleta.Rgb.Skip(9).Take(3).ShouldBe(new byte[] { 255, 0, 0 });

        var gris = DecodificadorPng.Decodificar(Png(1, 1, 0, 16, (_, _) => [0x8000])).ShouldNotBeNull();
        gris.Rgb.ShouldBe(new byte[] { 128, 128, 128 });

        var bit = DecodificadorPng.Decodificar(Png(3, 1, 0, 1, (x, _) => [x % 2])).ShouldNotBeNull();
        bit.Rgb.ShouldBe(new byte[] { 0, 0, 0, 255, 255, 255, 0, 0, 0 });
    }

    [Fact]
    public void El_png_entrelazado_Adam7_queda_igual_al_no_entrelazado()
    {
        static int[] Muestra(int x, int y) => [x * 40 % 256, y * 50 % 256, (x + y) * 20 % 256];
        var plano = DecodificadorPng.Decodificar(Png(9, 7, 2, 8, Muestra)).ShouldNotBeNull();
        var entrelazado = DecodificadorPng.Decodificar(Png(9, 7, 2, 8, Muestra, entrelazado: true)).ShouldNotBeNull();
        entrelazado.Rgb.ShouldBe(plano.Rgb);
        plano.Rgb.Skip(((2 * 9) + 3) * 3).Take(3).ShouldBe(new byte[] { 3 * 40, 2 * 50, 5 * 20 });
    }

    [Fact]
    public void Un_png_danado_truncado_o_enorme_no_se_decodifica()
    {
        var bueno = Png(4, 4, 2, 8, (x, y) => [x, y, 0]);
        DecodificadorPng.Decodificar(bueno).ShouldNotBeNull();
        DecodificadorPng.Decodificar(bueno[..(bueno.Length - 30)]).ShouldBeNull(); // truncado
        DecodificadorPng.Decodificar(Encoding.ASCII.GetBytes("esto no es un png, ni de cerca, ni de lejos")).ShouldBeNull();
        var malaProfundidad = Png(4, 4, 2, 8, (x, y) => [x, y, 0]);
        malaProfundidad[24] = 5; // IHDR: profundidad de 5 bits, que PNG no admite
        DecodificadorPng.Decodificar(malaProfundidad).ShouldBeNull();
    }

    // ---------- SVG como vectores ----------

    private static string Contenido(SvgDibujable svg, double ancho = 100, double alto = 40)
    {
        var pdf = new Validasoft.Infraestructura.Certificados.LienzoPdf();
        svg.Dibujar(pdf, 10, 20, ancho, alto);
        var bytes = pdf.Construir("t", "s", DateTimeOffset.UnixEpoch, (_, _) => { });
        return Encoding.Latin1.GetString(bytes);
    }

    [Fact]
    public void El_svg_con_formas_estilos_de_clase_transformaciones_y_use_se_dibuja_como_vectores()
    {
        const string svg = """
            <?xml version="1.0"?>
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" viewBox="0 0 200 80">
              <style>.azul { fill: #0000FF } .borde { stroke: rgb(255,0,0); stroke-width: 4; fill: none }</style>
              <defs><rect id="caja" width="20" height="10"/></defs>
              <g transform="translate(10 10) scale(2)" opacity="0.5">
                <circle class="azul" cx="20" cy="20" r="10"/>
                <rect class="borde" x="50" y="5" width="30" height="20" rx="4"/>
                <polygon points="0,0 10,0 5,10" fill="green"/>
                <use xlink:href="#caja" x="60" y="40" fill="orange"/>
              </g>
              <path d="M10 70 h20 v-5 l-10 -5 z" fill="#333" fill-rule="evenodd"/>
              <line x1="0" y1="79" x2="200" y2="79" stroke="black" stroke-dasharray="4 2"/>
            </svg>
            """;
        var dibujo = SvgDibujable.Leer(svg).ShouldNotBeNull();
        (dibujo.Ancho, dibujo.Alto).ShouldBe((200.0, 80.0));
        var pdf = Contenido(dibujo);
        pdf.ShouldContain("0 0 1 rg");          // clase .azul
        pdf.ShouldContain("1 0 0 RG");          // clase .borde (rgb())
        pdf.ShouldContain("0 0.501961 0 rg");   // green
        pdf.ShouldContain("1 0.647059 0 rg");   // orange, heredado por el use
        pdf.ShouldContain("/GS1 gs");           // opacidad del grupo
        pdf.ShouldContain("ExtGState");
        pdf.ShouldContain("f*");                // fill-rule evenodd
        pdf.ShouldContain("[4 2] 0 d");         // guiones
        pdf.ShouldContain(" c\n");              // curvas del círculo y del rectángulo redondeado
        // La escala lleva el SVG (200×80) a la caja (100×40): 0,5, con el eje y invertido.
        pdf.ShouldContain("0.5 0 0 -0.5 10 60 cm");
    }

    [Fact]
    public void El_degradado_lineal_y_el_arco_del_isotipo_de_Validasoft_se_dibujan()
    {
        var isotipo = SvgDibujable.Leer(LogosDePdf.IsotipoDeValidasoft).ShouldNotBeNull();
        var pdf = Contenido(isotipo, 34, 34);
        pdf.ShouldContain("/ShadingType 2");
        pdf.ShouldContain("/Pattern cs /P1 scn");
        pdf.ShouldContain("/C0 [0.239216 0.419608 1]");   // #3D6BFF
        pdf.ShouldContain("/C1 [0.133333 0.827451 0.541176]"); // #22D38A
        pdf.ShouldContain("1 1 1 RG");                    // la «V» en blanco
        pdf.ShouldContain("3.25 w 1 J 1 j");              // trazo redondeado
    }

    [Fact]
    public void Un_svg_con_texto_imagenes_o_entidades_externas_no_se_dibuja()
    {
        SvgDibujable.Leer("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="5" height="5"/><text x="1" y="9">Hola</text></svg>""").ShouldBeNull();
        SvgDibujable.Leer("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><image href="data:image/png;base64,AAAA" width="5" height="5"/></svg>""").ShouldBeNull();
        SvgDibujable.Leer("""<!DOCTYPE svg [<!ENTITY x SYSTEM "file:///etc/passwd">]><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><rect width="5" height="5"/></svg>""").ShouldBeNull();
        SvgDibujable.Leer("no es xml").ShouldBeNull();
        SvgDibujable.Leer("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"></svg>""").ShouldBeNull(); // no dibuja nada
        SvgDibujable.Leer("""<svg xmlns="http://www.w3.org/2000/svg"><rect width="5" height="5"/></svg>""").ShouldBeNull(); // sin medidas
    }

    // ---------- Elección del logo ----------

    [Fact]
    public void El_logo_es_el_de_la_empresa_si_se_puede_dibujar_y_si_no_el_de_Validasoft()
    {
        var png = DataUri("image/png", Png(30, 10, 6, 8, (x, _) => [x * 8, 100, 200, 255]));
        LogosDePdf.DeLaEmpresa(png).ShouldBeOfType<LogoRaster>().Proporcion.ShouldBe(3.0);
        var svg = DataUriSvg("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 25"><rect width="100" height="25" fill="#E07A1F"/></svg>""");
        LogosDePdf.DeLaEmpresa(svg).ShouldBeOfType<LogoVectorial>().Proporcion.ShouldBe(4.0);

        // Sin logo, por URL (no se descarga), dañado o con texto: no hay logo de la empresa y se usa el de Validasoft.
        foreach (var sinLogo in new string?[] { null, "", "   ", "https://ejemplo.co/logo.png", "data:image/png;base64,AAAA", DataUriSvg("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 9 9"><text>X</text></svg>""") })
        {
            LogosDePdf.DeLaEmpresa(sinLogo).ShouldBeNull();
            LogosDePdf.Resolver(sinLogo).ShouldBeSameAs(LogosDePdf.Validasoft);
        }

        LogosDePdf.Validasoft.Proporcion.ShouldBe(1.0);
    }

    // ---------- El PDF ----------

    private static DeclaracionPep Declaracion() => new()
    {
        RelacionEmpresa = RelacionConLaEmpresa.Proveedor,
        Nombre = "Ana María Pérez Gómez",
        TipoIdentificacion = "CC",
        Numero = "52111333",
        Nacionalidad = "CO",
        Direccion = "Carrera 7 # 32-16",
        Cargo = "Alcalde municipal",
        Institucion = "Alcaldía de Sopó",
        Pais = "CO",
        FechaVinculacion = new DateOnly(2018, 1, 1),
        Vigente = false,
        FechaDesvinculacion = new DateOnly(2022, 1, 1),
        CuentasExterior = false,
        VinculoConPep = new VinculoConPep { Tiene = false },
        AceptaJuramento = true,
        FirmaEnCalidad = FirmaEnCalidad.NombrePropio,
        FirmanteNombre = "Ana María Pérez Gómez",
        FirmanteTipoIdentificacion = "CC",
        FirmanteNumero = "52111333",
        FechaFirma = new DateOnly(2026, 11, 12)
    };

    private static byte[] GenerarPdf(string empresa, string? logo, string nombreDeMuestra)
    {
        var bytes = new GeneradorPdfDeDeclaracionPep().Generar(new DatosDelPdfDeDeclaracionPep(empresa, "Ana María Pérez Gómez", "Información general", 3,
            new DateTimeOffset(2026, 11, 12, 15, 0, 0, TimeSpan.Zero), null, Declaracion(), new DateTimeOffset(2026, 11, 12, 16, 0, 0, TimeSpan.Zero), logo));
        if (Environment.GetEnvironmentVariable("VALIDASOFT_PDF_SALIDA") is { Length: > 0 } carpeta)
        {
            Directory.CreateDirectory(carpeta);
            File.WriteAllBytes(Path.Combine(carpeta, nombreDeMuestra + ".pdf"), bytes);
        }

        return bytes;
    }

    [Fact]
    public void El_pdf_lleva_el_logo_png_de_la_empresa_su_logo_svg_o_el_de_Validasoft()
    {
        // PNG de la empresa: una imagen en el encabezado (y solo esa: esta declaración no trae firma).
        var png = DataUri("image/png", Png(120, 40, 6, 8, (x, y) => [x * 2, 90, 200 - y * 3, x > 4 && x < 115 && y > 4 && y < 35 ? 255 : 0]));
        var conPng = Encoding.Latin1.GetString(GenerarPdf("Constructora Norte S.A.S.", png, "logo-png"));
        conPng.ShouldContain("/Subtype /Image");
        conPng.ShouldContain("/Width 120 /Height 40");
        conPng.ShouldNotContain("/Pattern");
        conPng.ShouldContain("(Constructora Norte S.A.S.)");

        // SVG de la empresa: vectores, sin imagen.
        var svg = DataUriSvg("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 30"><rect width="120" height="30" rx="6" fill="#E07A1F"/><circle cx="15" cy="15" r="8" fill="#fff"/><path d="M32 8h12l-6 14z" fill="#fff"/></svg>""");
        var conSvg = Encoding.Latin1.GetString(GenerarPdf("Constructora Sur S.A.S.", svg, "logo-svg"));
        conSvg.ShouldNotContain("/Subtype /Image");
        conSvg.ShouldContain("0.878431 0.478431 0.121569 rg"); // #E07A1F

        // Sin logo, o con uno que no se puede dibujar: el de Validasoft (el degradado de marca).
        foreach (var (logo, nombre) in new (string?, string)[] { (null, "logo-validasoft"), ("https://ejemplo.co/logo.png", "logo-url") })
        {
            var texto = Encoding.Latin1.GetString(GenerarPdf("Validasoft", logo, nombre));
            texto.ShouldContain("/ShadingType 2");
            texto.ShouldNotContain("/Subtype /Image");
        }
    }

    [Fact]
    public void Un_nombre_largo_de_empresa_se_achica_o_se_corta_para_no_pasarse_del_encabezado()
    {
        var largo = "Consorcio Internacional de Ingeniería, Construcción y Mantenimiento de Infraestructura Vial del Caribe Colombiano S.A.S.";
        var texto = Encoding.Latin1.GetString(GenerarPdf(largo, null, "nombre-largo"));
        texto.ShouldNotContain(largo);
        texto.ShouldContain("Consorcio Internacional");
        texto.ShouldContain("\\205"); // los puntos suspensivos (WinAnsi 0x85)
    }
}
