using System.Globalization;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Aplicacion.Informes;
using Validasoft.Dominio.Ficha;
using Validasoft.Infraestructura.Certificados;

namespace Validasoft.Infraestructura.Ficha;

/// <summary>
/// PDF de la declaración jurada PEP (HU 49093) con el mismo lienzo del certificado y de la evidencia de listas (PDF 1.7, fuentes
/// estándar, sin dependencias): la empresa, los cuatro bloques del formulario con lo que la persona firmó, el juramento y la imagen
/// de la firma con su huella, la fecha y la IP desde la que se firmó. Los textos son los de la plantilla de la HU.
/// </summary>
public sealed class GeneradorPdfDeDeclaracionPep : IGeneradorDePdfDeDeclaracionPep
{
    public const string Juramento = "Declaro bajo juramento que la información suministrada es veraz y completa, y me comprometo a informar cualquier cambio en mi " +
                                    "condición de PEP o en mi vínculo con una PEP.";

    private const double Ancho = 595.28;
    private const double Alto = 841.89;
    private const double Margen = 48;
    private const double AnchoUtil = Ancho - 2 * Margen;
    private const double AnchoEtiqueta = 170;
    private const double AltoDelLogo = 34;
    private const double AnchoMaximoDelLogo = 110;
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    private static readonly Dictionary<string, string> NombresDePaises = new(StringComparer.Ordinal)
    {
        ["AR"] = "Argentina", ["BO"] = "Bolivia", ["BR"] = "Brasil", ["CA"] = "Canadá", ["CH"] = "Suiza", ["CL"] = "Chile", ["CN"] = "China", ["CO"] = "Colombia",
        ["CR"] = "Costa Rica", ["CU"] = "Cuba", ["DE"] = "Alemania", ["DO"] = "República Dominicana", ["EC"] = "Ecuador", ["ES"] = "España",
        ["FR"] = "Francia", ["GB"] = "Reino Unido", ["GT"] = "Guatemala", ["HN"] = "Honduras", ["IT"] = "Italia", ["JP"] = "Japón", ["MX"] = "México",
        ["NI"] = "Nicaragua", ["PA"] = "Panamá", ["PE"] = "Perú", ["PR"] = "Puerto Rico", ["PT"] = "Portugal", ["PY"] = "Paraguay", ["SV"] = "El Salvador",
        ["US"] = "Estados Unidos", ["UY"] = "Uruguay", ["VE"] = "Venezuela", ["AE"] = "Emiratos Árabes Unidos", ["KY"] = "Islas Caimán", ["BS"] = "Bahamas",
        ["NL"] = "Países Bajos", ["LU"] = "Luxemburgo", ["IE"] = "Irlanda", ["BM"] = "Bermudas", ["VG"] = "Islas Vírgenes Británicas", ["IN"] = "India"
    };

    public byte[] Generar(DatosDelPdfDeDeclaracionPep d)
    {
        var declaracion = d.Declaracion;
        var pdf = new LienzoPdf();
        var y = Alto - Margen;
        // Encabezado: el logo de la empresa (o el de Validasoft) a la izquierda y, a su lado, el nombre y el título del documento.
        var logo = LogosDePdf.Resolver(d.LogoUrl);
        var anchoLogo = Math.Min(AnchoMaximoDelLogo, AltoDelLogo * logo.Proporcion);
        logo.Dibujar(pdf, Margen, y - AltoDelLogo - 2, anchoLogo, AltoDelLogo);
        var xTexto = Margen + anchoLogo + 12;
        var (empresa, tamano) = AjustarNombre(d.Empresa, Ancho - Margen - xTexto);
        pdf.Texto(xTexto, y - 14, Fuente.HelveticaBold, tamano, empresa);
        pdf.Texto(xTexto, y - 30, Fuente.Helvetica, 9, "Declaración jurada de persona expuesta políticamente (PEP)", 0.35);
        y -= 42;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.6);
        y -= 4;

        y = Seccion(pdf, y, "1. Datos básicos de la contraparte");
        y = Dato(pdf, y, "Nombre completo o razón social", declaracion.Nombre);
        y = Dato(pdf, y, "Documento", Documento(declaracion.TipoIdentificacion, declaracion.Numero));
        y = Dato(pdf, y, "Nacionalidad", Pais(declaracion.Nacionalidad));
        y = Dato(pdf, y, "Dirección", declaracion.Direccion);
        y = Dato(pdf, y, "Relación con la empresa", Relacion(declaracion));
        if (d.Rol is { } rol)
            y = Dato(pdf, y, "Rol en el proveedor", $"{rol} de {d.Tercero}");

        y = Seccion(pdf, y, "2. Declaración de condición PEP");
        y = Dato(pdf, y, "Cargo desempeñado", declaracion.Cargo);
        y = Dato(pdf, y, "Institución", declaracion.Institucion);
        y = Dato(pdf, y, "País de la institución", Pais(declaracion.Pais));
        y = Dato(pdf, y, "Fecha de vinculación", Fecha(declaracion.FechaVinculacion));
        y = Dato(pdf, y, "Fecha de desvinculación", declaracion.Vigente == true ? "Vigente" : Fecha(declaracion.FechaDesvinculacion));
        y = Dato(pdf, y, "Poder de firma u otro derecho sobre cuentas financieras en el exterior",
            declaracion.CuentasExterior == true
                ? $"Sí: {string.Join(", ", (declaracion.PaisesCuentasExterior ?? []).Select(Pais))}"
                : SiNo(declaracion.CuentasExterior));
        y = Dato(pdf, y, "Cónyuge o compañero(a) permanente",
            declaracion.Conyuge is { } c ? $"{c.Nombre} · {Documento(c.TipoIdentificacion, c.Numero)}" : "No registra");

        y = Subtitulo(pdf, y, "Familiares hasta el 2.º grado de consanguinidad o afinidad y 1.º civil");
        if (declaracion.Familiares is { Count: > 0 } familiares)
        {
            y = Fila(pdf, y, true, ("Nombre completo", 190), ("Parentesco", 110), ("Documento", AnchoUtil - 300));
            foreach (var f in familiares)
                y = Fila(pdf, y, false, (f.Nombre ?? "—", 190), (f.Parentesco ?? "—", 110), (Documento(f.TipoIdentificacion, f.Numero), AnchoUtil - 300));
        }
        else
        {
            y = Fila(pdf, y, false, ("No registra", AnchoUtil));
        }

        y = Subtitulo(pdf, y, "Sociedades, fiducias o patrimonios autónomos en los que participa");
        if (declaracion.Sociedades is { Count: > 0 } sociedades)
        {
            y = Fila(pdf, y, true, ("Nombre o razón social", 200), ("Identificación", 110), ("En calidad de", AnchoUtil - 310));
            foreach (var s in sociedades)
                y = Fila(pdf, y, false, (s.Nombre ?? "—", 200), (s.Identificacion ?? "—", 110), (Calidad(s), AnchoUtil - 310));
        }
        else
        {
            y = Fila(pdf, y, false, ("No registra", AnchoUtil));
        }

        y = Seccion(pdf, y, "3. Vínculo familiar o asociados cercanos a una PEP");
        var vinculo = declaracion.VinculoConPep;
        y = Dato(pdf, y, "¿Tiene algún vínculo familiar o societario con una PEP?", SiNo(vinculo?.Tiene));
        if (vinculo is { Tiene: true })
        {
            y = Dato(pdf, y, "Nombre de la PEP", vinculo.Nombre);
            y = Dato(pdf, y, "Vínculo o grado de parentesco", vinculo.Parentesco);
            y = Dato(pdf, y, "Cargo e institución", vinculo.CargoInstitucion);
            y = Dato(pdf, y, "Fecha de vinculación", Fecha(vinculo.FechaVinculacion));
            y = Dato(pdf, y, "Fecha de desvinculación", vinculo.Vigente == true ? "Vigente" : Fecha(vinculo.FechaDesvinculacion));
        }

        // El juramento, quién firma y la firma van juntos: si no caben en lo que queda de la página, empiezan en la siguiente.
        y = Espacio(pdf, y, 360);
        y = Seccion(pdf, y, "4. Juramento y firma");
        y = Parrafo(pdf, y, Juramento, 9.5);
        y -= 4;
        y = Dato(pdf, y, "Firma en calidad de", declaracion.FirmaEnCalidad == FirmaEnCalidad.RepresentanteLegal ? "Representante legal" : "Nombre propio");
        y = Dato(pdf, y, "Firmante", $"{declaracion.FirmanteNombre} · {Documento(declaracion.FirmanteTipoIdentificacion, declaracion.FirmanteNumero)}");
        y = Firma(pdf, y, declaracion);

        var momento = Exportaciones.Momento(d.Generado);
        return pdf.Construir($"Declaración jurada PEP {declaracion.Numero}", $"{declaracion.Nombre} · {d.Tercero}", d.Generado, (lienzo, n) =>
            lienzo.Texto(Margen, Margen - 14, Fuente.Helvetica, 7,
                $"{d.Seccion}, versión {d.Version} del {Fecha(DateOnly.FromDateTime(Exportaciones.Local(d.FechaVersion)))} · generado el {momento} · página {n.Item1} de {n.Item2}", 0.45));
    }

    /// <summary>El nombre en la línea del encabezado: a 16 pt si cabe, si no a 12 y, si aun así no cabe, cortado con puntos suspensivos.</summary>
    private static (string Texto, double Tamano) AjustarNombre(string nombre, double disponible)
    {
        foreach (var tamano in new[] { 16.0, 12.0 })
        {
            if (Medidas.Ancho(nombre, Fuente.HelveticaBold, tamano) <= disponible)
                return (nombre, tamano);
        }

        var corte = nombre.Length;
        while (corte > 1 && Medidas.Ancho(nombre[..corte] + "…", Fuente.HelveticaBold, 12) > disponible)
            corte--;
        return (nombre[..corte].TrimEnd() + "…", 12);
    }

    private static string Relacion(DeclaracionPep d) => d.RelacionEmpresa switch
    {
        RelacionConLaEmpresa.Proveedor => "Proveedor",
        RelacionConLaEmpresa.Socio => "Socio",
        RelacionConLaEmpresa.Otro => $"Otro: {d.RelacionOtra}",
        _ => "—"
    };

    private static string Calidad(SociedadPep s) => s.Calidad switch
    {
        CalidadEnSociedad.Otra => $"Otra: {s.CalidadOtra}",
        { } calidad => calidad.ToString(),
        _ => "—"
    };

    private static string SiNo(bool? valor) => valor switch { true => "Sí", false => "No", _ => "—" };

    private static string Documento(string? tipo, string? numero) => string.Join(' ', new[] { tipo, numero }.Where(x => !string.IsNullOrWhiteSpace(x))) is { Length: > 0 } t ? t : "—";

    private static string Fecha(DateOnly? fecha) => fecha is { } f ? f.ToString("dd/MM/yyyy", Invariante) : "—";

    private static string Pais(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return "—";
        return NombresDePaises.TryGetValue(codigo, out var nombre) ? $"{nombre} ({codigo})" : codigo;
    }

    /// <summary>La imagen de la firma sobre su línea, con la huella y los sellos de quien firmó.</summary>
    private static double Firma(LienzoPdf pdf, double y, DeclaracionPep d)
    {
        var imagen = d.FirmaPng is { } png && FirmaPng.Bytes(png) is { } bytes && FirmaPng.Decodificar(bytes, out var decodificada) is null ? decodificada : null;
        var alto = 70.0;
        y = Espacio(pdf, y, alto + 54);
        if (imagen is not null)
        {
            // Sobre su línea, a 200 pt de ancho como máximo y sin pasar de la altura de la franja.
            var escala = Math.Min(200.0 / imagen.Ancho, 62.0 / imagen.Alto);
            pdf.Imagen(Margen, y - 6 - imagen.Alto * escala, imagen.Ancho * escala, imagen.Alto * escala, imagen.Ancho, imagen.Alto, imagen.Rgb);
        }
        else
        {
            pdf.Texto(Margen, y - 40, Fuente.Helvetica, 9, "Firma registrada en el sistema de origen (sin imagen en esta copia).", 0.35);
        }

        y -= alto + 4;
        pdf.Linea(Margen, y, Margen + 210, y, 0.6);
        y -= 12;
        pdf.Texto(Margen, y, Fuente.Helvetica, 8, "Firma", 0.35);
        y -= 12;
        var sellos = new List<string>();
        if (d.FechaFirma is { } fecha)
            sellos.Add($"Firmada el {Fecha(fecha)}");
        if (!string.IsNullOrWhiteSpace(d.FirmaIp))
            sellos.Add($"desde la IP {d.FirmaIp}");
        if (sellos.Count > 0)
            y = Parrafo(pdf, y, string.Join(" ", sellos), 8, 0.35);
        if (d.FirmaHuella is { } huella)
            y = Parrafo(pdf, y, $"Huella de la firma (SHA-256): {huella}", 7, 0.45);
        return y;
    }

    private static double Seccion(LienzoPdf pdf, double y, string titulo)
    {
        y = Espacio(pdf, y, 48);
        y -= 24;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 10.5, titulo.ToUpperInvariant());
        y -= 5;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.3);
        return y - 4;
    }

    private static double Subtitulo(LienzoPdf pdf, double y, string titulo)
    {
        y = Espacio(pdf, y, 40);
        y -= 22;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 9, titulo, 0.2);
        return y - 4;
    }

    private static double Dato(LienzoPdf pdf, double y, string etiqueta, string? valor)
    {
        var etiquetas = Medidas.Partir(etiqueta, Fuente.HelveticaBold, 8.5, AnchoEtiqueta - 8);
        var valores = Medidas.Partir(string.IsNullOrWhiteSpace(valor) ? "—" : valor, Fuente.Helvetica, 9.5, AnchoUtil - AnchoEtiqueta, cortarPalabras: false);
        var lineas = Math.Max(etiquetas.Count, valores.Count);
        y = Espacio(pdf, y, 13 * lineas + 4);
        for (var i = 0; i < etiquetas.Count; i++)
            pdf.Texto(Margen, y - 12 - 13 * i, Fuente.HelveticaBold, 8.5, etiquetas[i], 0.25);
        for (var i = 0; i < valores.Count; i++)
            pdf.Texto(Margen + AnchoEtiqueta, y - 12 - 13 * i, Fuente.Helvetica, 9.5, valores[i]);
        return y - 13 * lineas - 4;
    }

    private static double Parrafo(LienzoPdf pdf, double y, string texto, double tamano, double gris = 0)
    {
        foreach (var linea in Medidas.Partir(texto, Fuente.Helvetica, tamano, AnchoUtil))
        {
            y = Espacio(pdf, y, tamano + 4);
            y -= tamano + 3;
            pdf.Texto(Margen, y, Fuente.Helvetica, tamano, linea, gris);
        }

        return y;
    }

    private static double Fila(LienzoPdf pdf, double y, bool encabezado, params (string Texto, double Ancho)[] celdas)
    {
        var fuente = encabezado ? Fuente.HelveticaBold : Fuente.Helvetica;
        const double tamano = 8.5;
        var lineas = celdas.Select(c => Medidas.Partir(c.Texto, fuente, tamano, c.Ancho - 6, cortarPalabras: true)).ToList();
        var alto = 11 * lineas.Max(l => Math.Max(1, l.Count)) + 5;
        y = Espacio(pdf, y, alto);
        var x = Margen;
        for (var i = 0; i < celdas.Length; i++)
        {
            var linea = y - 10;
            foreach (var texto in lineas[i])
            {
                pdf.Texto(x + 2, linea, fuente, tamano, texto, encabezado ? 0.25 : 0);
                linea -= 11;
            }

            x += celdas[i].Ancho;
        }

        y -= alto;
        pdf.Linea(Margen, y + 2, Ancho - Margen, y + 2, encabezado ? 0.5 : 0.15);
        return y;
    }

    private static double Espacio(LienzoPdf pdf, double y, double necesario)
    {
        if (y - necesario >= Margen + 20)
            return y;
        pdf.NuevaPagina();
        return Alto - Margen;
    }
}
