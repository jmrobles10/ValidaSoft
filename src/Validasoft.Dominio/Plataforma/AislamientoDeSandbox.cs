using System.Text.RegularExpressions;

namespace Validasoft.Dominio.Plataforma;

/// <summary>
/// Reglas del sandbox (SB-1…SB-7): una API key de prueba (<c>vsk_test_…</c>) opera en un <b>tenant hermano</b> del de producción,
/// con sus propias empresas, datos, eventos y buzón de correo. Aquí viven las convenciones que lo mantienen aislado.
/// </summary>
public static partial class AislamientoDeSandbox
{
    /// <summary>Sufijo del slug del tenant sandbox; ningún slug real lo admite (solo letras, números y guion).</summary>
    public const string SufijoDeSlug = "~sandbox";

    /// <summary>Sufijo visible del nombre del grupo y de cada empresa del sandbox: en correos y certificados de prueba se nota.</summary>
    public const string SufijoDeNombre = " (sandbox)";

    /// <summary>Valor de la cabecera <c>Validasoft-Entorno</c> que llevan las respuestas de una credencial de prueba.</summary>
    public const string Entorno = "sandbox";

    public const string CabeceraEntorno = "Validasoft-Entorno";

    /// <summary>
    /// «Llave de OpenAI» de los planes de IA del sandbox: ningún OpenAI la reconoce, y el analizador por entorno envía toda llave
    /// con este prefijo al proveedor simulado, así que el análisis de un documento de prueba nunca sale a la red ni cuesta.
    /// </summary>
    public const string PrefijoDeLlaveDeIa = "sk-sandbox-";

    public const string LlaveDeIa = PrefijoDeLlaveDeIa + "simulado-sin-red-0000";

    public static bool EsLlaveDeIaDeSandbox(string? llave) => llave is not null && llave.StartsWith(PrefijoDeLlaveDeIa, StringComparison.Ordinal);

    /// <summary>
    /// Clave del tercero en el índice único global: en producción, la identificación normalizada; en un sandbox, la misma con el
    /// tenant sandbox por delante (<c>sbx:{tenant}|CO|NIT|900123456</c>). Así un NIT de prueba y el real conviven como terceros
    /// distintos y, con ellos, sus bóvedas, enlaces y sesiones: la bóveda es global y es la única parte que un tenant hermano no
    /// aísla por sí solo.
    /// </summary>
    public static string ClaveDeTercero(string clave, Guid? tenantSandboxId) =>
        tenantSandboxId is { } tenant ? $"sbx:{tenant:N}|{clave}" : clave;

    /// <summary>
    /// Prefijo de folio de la copia de pruebas de una empresa: <c>T</c> + el real (máx. 10), para que un folio de prueba
    /// (<c>TACME-E-…</c>) no se parezca a uno real. Si ya lo usa otra empresa del sandbox, el último carácter pasa a ser un dígito.
    /// </summary>
    public static string PrefijoDeFolio(string prefijoDeProduccion, IReadOnlySet<string> ocupados)
    {
        var candidato = ("T" + prefijoDeProduccion.ToUpperInvariant());
        candidato = candidato.Length > 10 ? candidato[..10] : candidato;
        for (var n = 0; ocupados.Contains(candidato); n++)
            candidato = (candidato.Length >= 10 ? candidato[..9] : candidato) + (n % 10);
        return candidato;
    }

    /// <summary>Enlaces <c>http(s)://</c> del texto de un correo: lo que un integrador necesita para seguir el flujo (el enlace de la invitación).</summary>
    public static IReadOnlyList<string> EnlacesDe(string texto) =>
        [.. RegexEnlace().Matches(texto ?? string.Empty).Select(m => m.Value.TrimEnd('.', ',', ')', ';')).Distinct()];

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex RegexEnlace();
}

/// <summary>
/// Correo que Validasoft habría enviado a un tercero o a una persona del grupo, retenido en el buzón del sandbox (SB-4) en vez
/// de salir a su destinatario. Se consulta por <c>GET /api/v1/sandbox/correos</c>. Esquema <c>validacion</c>, multi-tenant (el
/// del sandbox): un reinicio lo borra.
/// </summary>
public sealed class CorreoDePrueba
{
    public Guid Id { get; set; }

    /// <summary>Destinatario tal como se habría enviado.</summary>
    public string Para { get; set; } = string.Empty;

    public string Asunto { get; set; } = string.Empty;

    public string Texto { get; set; } = string.Empty;

    public string Html { get; set; } = string.Empty;

    /// <summary>Enlaces del texto (el de acceso del tercero, el de la app…), listos para abrirse en una prueba.</summary>
    public List<string> Enlaces { get; set; } = [];

    public DateTimeOffset RecibidoEn { get; set; }

    public static CorreoDePrueba De(string para, string asunto, string texto, string html, DateTimeOffset ahora) => new()
    {
        Id = Guid.CreateVersion7(),
        Para = (para ?? string.Empty).Trim(),
        Asunto = asunto ?? string.Empty,
        Texto = texto ?? string.Empty,
        Html = html ?? string.Empty,
        Enlaces = [.. AislamientoDeSandbox.EnlacesDe(texto ?? string.Empty)],
        RecibidoEn = ahora
    };
}
