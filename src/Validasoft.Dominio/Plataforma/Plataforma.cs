using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Plataforma;

/// <summary>
/// Cliente del SaaS = grupo empresarial (ADR-0008). Esquema <c>plataforma</c>. Su <see cref="Id"/> es el
/// identificador de tenant que usa Marten (<c>tenant_id</c> en el esquema <c>validacion</c>).
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; set; }

    /// <summary>Inmutable; forma parte de las API keys (<c>vsk_{entorno}_{slug}_…</c>).</summary>
    public string Slug { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Region { get; set; } = "co";

    public string ZonaHoraria { get; set; } = "America/Bogota";

    public string Idioma { get; set; } = "es-CO";

    public string Plan { get; set; } = "Inicial";

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    public string IdentificadorMarten => Id.ToString("N");

    public static Tenant Crear(string slug, string nombre, string? region, string? zonaHoraria, DateTimeOffset ahora)
    {
        var slugNormalizado = (slug ?? string.Empty).Trim().ToLowerInvariant();
        if (!Regex.IsMatch(slugNormalizado, "^[a-z0-9](?:[a-z0-9-]{1,28})[a-z0-9]$"))
            throw ReglaDeNegocioException.Validacion("slug-invalido",
                "El slug debe tener entre 3 y 30 caracteres: letras minúsculas, números y guiones, sin guion al inicio o al final.");

        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del tenant es obligatorio.");

        var zona = string.IsNullOrWhiteSpace(zonaHoraria) ? "America/Bogota" : zonaHoraria.Trim();
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(zona, out _))
            throw ReglaDeNegocioException.Validacion("zona-horaria-invalida", $"'{zona}' no es una zona horaria IANA válida.");

        return new Tenant
        {
            Id = Guid.CreateVersion7(),
            Slug = slugNormalizado,
            Nombre = nombre.Trim(),
            Region = string.IsNullOrWhiteSpace(region) ? "co" : region.Trim().ToLowerInvariant(),
            ZonaHoraria = zona,
            CreadoEn = ahora
        };
    }
}

/// <summary>Razón social dentro del tenant (ADR-0008). Las decisiones se aíslan por empresa.</summary>
public sealed class Empresa
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Identificacion Identificacion { get; set; } = null!;

    /// <summary>Copia de <see cref="Identificacion.Clave"/> para el índice único por tenant.</summary>
    public string ClaveIdentificacion { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Prefijo de folios de expediente y certificado (<c>{Prefijo}-E-…</c>, T-9). Único en el tenant.</summary>
    public string PrefijoFolio { get; set; } = string.Empty;

    /// <summary>Logo para el portal del tercero y el certificado: URL https o imagen SVG/PNG embebida (data URI, ≤ 200 KB).</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Correo de soporte para terceros: aparece en el pie de sus pantallas y es el Reply-To de los correos.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Color de marca (#RRGGBB). El portal deriva de él una paleta que cumple contraste (marcaEmpresa).</summary>
    public string? ColorMarca { get; set; }

    public bool Activa { get; set; } = true;

    public DateTimeOffset CreadaEn { get; set; }

    public static Empresa Crear(Guid tenantId, Identificacion identificacion, string nombre, string prefijoFolio,
        string? logoUrl, string? replyTo, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "La razón social es obligatoria.");

        var prefijo = (prefijoFolio ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(prefijo, "^[A-Z0-9]{2,10}$"))
            throw ReglaDeNegocioException.Validacion("prefijo-folio-invalido",
                "El prefijo de folio debe tener entre 2 y 10 letras mayúsculas o números.");

        return new Empresa
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Identificacion = identificacion,
            ClaveIdentificacion = identificacion.Clave,
            Nombre = nombre.Trim(),
            PrefijoFolio = prefijo,
            LogoUrl = ValidarLogo(logoUrl),
            ReplyTo = ValidarSoporte(replyTo),
            CreadaEn = ahora
        };
    }

    public const int TamanoMaximoLogo = 200 * 1024;

    /// <summary>
    /// Datos y marca (P2ConfigEmpresa): contacto de soporte, color y logo. La razón social, la identificación y el prefijo
    /// de folio no cambian aquí (el prefijo ya está en folios emitidos). <c>null</c> conserva; cadena vacía quita.
    /// </summary>
    public void ActualizarMarca(string? contactoSoporte, string? colorMarca, string? logoUrl)
    {
        if (contactoSoporte is not null)
            ReplyTo = ValidarSoporte(contactoSoporte);
        if (colorMarca is not null)
            ColorMarca = ValidarColor(colorMarca);
        if (logoUrl is not null)
            LogoUrl = ValidarLogo(logoUrl);
    }

    private static string? ValidarSoporte(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo))
            return null;
        var limpio = correo.Trim();
        if (limpio.Length > 254 || !Regex.IsMatch(limpio, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw ReglaDeNegocioException.Validacion("contacto-soporte-invalido", "El contacto de soporte debe ser un correo válido.");
        return limpio;
    }

    private static string? ValidarColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;
        var limpio = color.Trim().ToUpperInvariant();
        if (!Regex.IsMatch(limpio, "^#[0-9A-F]{6}$"))
            throw ReglaDeNegocioException.Validacion("color-marca-invalido", "El color de marca debe ser hexadecimal, como #E07A1F.");
        return limpio;
    }

    /// <summary>URL https o data URI de SVG/PNG en base64 de hasta <see cref="TamanoMaximoLogo"/> bytes.</summary>
    private static string? ValidarLogo(string? logo)
    {
        if (string.IsNullOrWhiteSpace(logo))
            return null;
        var limpio = logo.Trim();
        if (Uri.TryCreate(limpio, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps && limpio.Length <= 2048)
            return limpio;

        var datos = Regex.Match(limpio, @"^data:(image/svg\+xml|image/png);base64,([A-Za-z0-9+/=]+)$");
        if (!datos.Success)
            throw ReglaDeNegocioException.Validacion("logo-invalido", "El logo debe ser una imagen SVG o PNG (o una URL https).");
        var bytes = datos.Groups[2].Value.Length / 4 * 3;
        if (bytes > TamanoMaximoLogo)
            throw ReglaDeNegocioException.Validacion("logo-demasiado-grande", "El logo pesa más de 200 KB.");
        return limpio;
    }
}
