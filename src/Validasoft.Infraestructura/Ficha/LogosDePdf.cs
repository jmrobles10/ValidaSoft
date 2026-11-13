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
/// Los logos de los PDF del producto: el de la empresa si tiene uno configurado que se pueda dibujar (PNG o SVG embebido como data URI,
/// que es como la configuración lo guarda) y, si no, el de Validasoft. Un logo por URL <c>https</c> no se descarga: generar un PDF no abre
/// conexiones de salida a direcciones que escribió un administrador (SSRF); se sube el archivo y queda embebido.
/// </summary>
internal static partial class LogosDePdf
{
    /// <summary>El isotipo de Validasoft (el mismo del producto: la «V» redondeada sobre el degradado de marca).</summary>
    public const string IsotipoDeValidasoft =
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">
          <defs><linearGradient id="marca" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#3D6BFF"/><stop offset="1" stop-color="#22D38A"/></linearGradient></defs>
          <rect width="32" height="32" rx="9" fill="url(#marca)"/>
          <path d="M10 11.25 15.1 21a1 1 0 0 0 1.8 0L22 11.25" fill="none" stroke="#FFFFFF" stroke-width="3.25" stroke-linecap="round" stroke-linejoin="round"/>
        </svg>
        """;

    private static readonly Lazy<LogoDePdf> DeValidasoft = new(() =>
        new LogoVectorial(SvgDibujable.Leer(IsotipoDeValidasoft) ?? throw new InvalidOperationException("El isotipo de Validasoft no se pudo leer.")));

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
