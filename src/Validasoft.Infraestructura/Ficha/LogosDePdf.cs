using System.Text;
using System.Text.RegularExpressions;
using Validasoft.Infraestructura.Certificados;

namespace Validasoft.Infraestructura.Ficha;

/// <summary>Un logo que el PDF sabe dibujar: su proporción y cómo se pinta dentro de un rectángulo sin deformarlo.</summary>
internal abstract class LogoDePdf
{
    /// <summary>Ancho dividido por alto.</summary>
    public abstract double Proporcion { get; }

    /// <summary>Dibuja el logo centrado en el rectángulo cuya esquina inferior izquierda es (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public abstract void Dibujar(LienzoPdf pdf, double x, double y, double ancho, double alto);
}

/// <summary>Un logo PNG ya decodificado (con su transparencia compuesta sobre blanco).</summary>
internal sealed class LogoRaster(ImagenRgb imagen) : LogoDePdf
{
    public override double Proporcion => (double)imagen.Ancho / imagen.Alto;

    public override void Dibujar(LienzoPdf pdf, double x, double y, double ancho, double alto)
    {
        var escala = Math.Min(ancho / imagen.Ancho, alto / imagen.Alto);
        var (w, h) = (imagen.Ancho * escala, imagen.Alto * escala);
        pdf.Imagen(x + (ancho - w) / 2, y + (alto - h) / 2, w, h, imagen.Ancho, imagen.Alto, imagen.Rgb);
    }
}

/// <summary>Un logo SVG dibujado como vectores.</summary>
internal sealed class LogoVectorial(SvgDibujable svg) : LogoDePdf
{
    public override double Proporcion => svg.Ancho / svg.Alto;

    public override void Dibujar(LienzoPdf pdf, double x, double y, double ancho, double alto) => svg.Dibujar(pdf, x, y, ancho, alto);
}

/// <summary>
/// El logotipo de Validasoft: la «V» (el check verde, del alto de la letra) muy pegada a «alida» (peso medio, color de texto) y «Soft» (peso
/// normal, color de marca), con las fuentes estándar del PDF (Helvetica, negrita para «alida»; la tipografía de marca no se incrusta).
/// Todo en puntos: la letra mide el alto disponible y el conjunto se centra en el rectángulo sin deformarse.
/// </summary>
internal sealed class LogoDeValidasoft(SvgDibujable v) : LogoDePdf
{
    /// <summary>Ancho de la «V» en tamaños de letra: su caja es de 64 × 60.</summary>
    private const double AnchoDeLaV = 64.0 / 60.0;

    /// <summary>Margen derecho de la «V» en tamaños de letra (va pegada a la «a»).</summary>
    private const double MargenDeLaV = -0.23;

    private const string Cuerpo = "alida";
    private const string Marca = "Soft";

    // #0A0F1C y #2F55D9.
    private static readonly (double R, double G, double B) ColorDelTexto = (10 / 255.0, 15 / 255.0, 28 / 255.0);
    private static readonly (double R, double G, double B) ColorDeLaMarca = (47 / 255.0, 85 / 255.0, 217 / 255.0);

    /// <summary>Ancho natural con la letra de 1 punto.</summary>
    private static double AnchoNatural => AnchoDeLaV + MargenDeLaV + (Medidas.Ancho(Cuerpo, Fuente.HelveticaBold, 1) + Medidas.Ancho(Marca, Fuente.Helvetica, 1));

    public override double Proporcion => AnchoNatural;

    public override void Dibujar(LienzoPdf pdf, double x, double y, double ancho, double alto)
    {
        var letra = Math.Min(ancho / AnchoNatural, alto);
        var izquierda = x + (ancho - AnchoNatural * letra) / 2;
        var baseY = y + (alto - letra) / 2;
        v.Dibujar(pdf, izquierda, baseY, AnchoDeLaV * letra, letra);
        var xTexto = izquierda + (AnchoDeLaV + MargenDeLaV) * letra;
        pdf.TextoColor(xTexto, baseY, Fuente.HelveticaBold, letra, Cuerpo, ColorDelTexto);
        pdf.TextoColor(xTexto + Medidas.Ancho(Cuerpo, Fuente.HelveticaBold, letra), baseY, Fuente.Helvetica, letra, Marca, ColorDeLaMarca);
    }
}

/// <summary>
/// Los logos de los PDF del producto: el de la empresa si tiene uno configurado que se pueda dibujar (PNG o SVG embebido como data URI,
/// que es como la configuración lo guarda) y, si no, el de Validasoft. Un logo por URL <c>https</c> no se descarga: generar un PDF no abre
/// conexiones de salida a direcciones que escribió un administrador (SSRF); se sube el archivo y queda embebido.
/// </summary>
internal static partial class LogosDePdf
{
    /// <summary>
    /// La «V» del logotipo de Validasoft (MA-1, docs/marca.md): el check caligráfico verde recortado a su caja (<c>viewBox 22 16 64 60</c>). Los
    /// números son los de <c>packages/ui/src/marca/geometria.ts</c>; <c>marca/sincronia.test.ts</c> comprueba que no se separen.
    /// </summary>
    public const string VDeValidasoft =
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="22 16 64 60">
          <path d="M24 38 C26 34 30 34 32 37 L47 61 C56 45 66 32 80 19 C82 17 85 19 83 22 C70 37 60 52 51 71 C49 75 44 75 42 71 Z" fill="#22D38A"/>
        </svg>
        """;

    private static readonly Lazy<LogoDePdf> DeValidasoft = new(() =>
        new LogoDeValidasoft(SvgDibujable.Leer(VDeValidasoft) ?? throw new InvalidOperationException("La «V» de Validasoft no se pudo leer.")));

    /// <summary>El logo de Validasoft.</summary>
    public static LogoDePdf Validasoft => DeValidasoft.Value;

    /// <summary>El logo de la empresa, o el de Validasoft si no tiene uno o no se puede dibujar.</summary>
    public static LogoDePdf Resolver(string? logoUrl) => DeLaEmpresa(logoUrl) ?? Validasoft;

    /// <summary>El logo de la empresa si es una imagen PNG o SVG embebida que se pueda dibujar; nulo en cualquier otro caso (sin logo, URL, archivo dañado).</summary>
    public static LogoDePdf? DeLaEmpresa(string? logoUrl)
    {
        if (string.IsNullOrWhiteSpace(logoUrl) || RegexDataUri().Match(logoUrl.Trim()) is not { Success: true } datos)
            return null;

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(datos.Groups[2].Value);
        }
        catch (FormatException)
        {
            return null;
        }

        if (datos.Groups[1].Value == "image/png")
            return DecodificadorPng.Decodificar(bytes) is { } imagen ? new LogoRaster(imagen) : null;
        return SvgDibujable.Leer(Encoding.UTF8.GetString(bytes)) is { } svg ? new LogoVectorial(svg) : null;
    }

    [GeneratedRegex(@"^data:(image/svg\+xml|image/png);base64,([A-Za-z0-9+/=]+)$")]
    private static partial Regex RegexDataUri();
}
