using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Ia;

/// <summary>Cambios que Plataforma pide sobre el plan de IA de una empresa (IA-2). Un modelo o umbral nulo conserva el actual.</summary>
/// <param name="ApiKeyOpenAi">Nula conserva la llave actual; vacía la quita; con valor la reemplaza (se cifra antes de guardarla).</param>
public sealed record CambiosDePlanIa(
    int ValidacionesIncluidasMes,
    bool AdicionalHabilitado,
    int? TopeAdicionalMes,
    string? ApiKeyOpenAi,
    string? ModeloPorDefecto,
    string? ModeloEscalamiento,
    decimal? UmbralConfianza,
    bool Activo,
    decimal? PrecioAdicional = null);

/// <summary>
/// Plan de IA de una empresa (ADR-0012, IA-2). Esquema <c>validacion</c>, multi-tenant; uno por empresa (id = empresa).
/// Lo configura Plataforma: validaciones incluidas por mes, adicional (con tope opcional), la API key de OpenAI de la
/// empresa (solo cifrada; se expone únicamente si está configurada y sus últimos cuatro caracteres), modelos y umbral de
/// confianza para escalar.
/// </summary>
public sealed partial class PlanDeIaEmpresa
{
    public const string ModeloPorDefectoInicial = "gpt-6-luna";
    public const string ModeloEscalamientoInicial = "gpt-6.1-terra";
    public const decimal UmbralConfianzaInicial = 0.85m;
    public const int MaximoValidacionesMes = 1_000_000;

    /// <summary>La empresa dueña del plan.</summary>
    public Guid Id { get; set; }

    public int ValidacionesIncluidasMes { get; set; }

    public bool AdicionalHabilitado { get; set; }

    /// <summary>Máximo de validaciones adicionales al mes; nulo = sin tope.</summary>
    public int? TopeAdicionalMes { get; set; }

    /// <summary>
    /// Lo que se cobra a esta empresa por cada validación adicional (PI-12), en la moneda de la tarifa general; nulo = el precio de la tarifa general
    /// (<c>TarifaDeIa</c>).
    /// </summary>
    public decimal? PrecioAdicional { get; set; }

    /// <summary>Llave de OpenAI cifrada con el protector de secretos; nunca se guarda ni se devuelve en claro.</summary>
    public string? ApiKeyOpenAiProtegida { get; set; }

    public string? UltimosCuatroLlave { get; set; }

    public string ModeloPorDefecto { get; set; } = ModeloPorDefectoInicial;

    public string ModeloEscalamiento { get; set; } = ModeloEscalamientoInicial;

    public decimal UmbralConfianza { get; set; } = UmbralConfianzaInicial;

    public bool Activo { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public Guid EmpresaId => Id;

    public bool LlaveConfigurada => !string.IsNullOrEmpty(ApiKeyOpenAiProtegida);

    /// <summary>Puede pagar una llamada a OpenAI con su llave (IA-3): plan activo y llave configurada.</summary>
    public bool PuedePagar => Activo && LlaveConfigurada;

    /// <summary>Plan sin configurar (inactivo, sin llave): lo que se muestra antes de que Plataforma lo cree.</summary>
    public static PlanDeIaEmpresa PorDefecto(Guid empresaId) => new() { Id = empresaId };

    public static PlanDeIaEmpresa Nuevo(Guid empresaId, DateTimeOffset ahora) => new() { Id = empresaId, CreadoEn = ahora, ActualizadoEn = ahora };

    /// <summary>
    /// Aplica los cambios validados. 422 <c>validaciones-incluidas-invalidas</c>, <c>tope-adicional-invalido</c>,
    /// <c>modelo-invalido</c>, <c>umbral-confianza-invalido</c> o <c>api-key-invalida</c>. <paramref name="proteger"/> cifra
    /// la llave nueva (no se llama si la llave no cambia).
    /// </summary>
    public void Actualizar(CambiosDePlanIa cambios, Func<string, string> proteger, DateTimeOffset ahora)
    {
        if (cambios.ValidacionesIncluidasMes is < 0 or > MaximoValidacionesMes)
            throw ReglaDeNegocioException.Validacion("validaciones-incluidas-invalidas",
                $"Las validaciones incluidas por mes deben estar entre 0 y {MaximoValidacionesMes}.");
        if (cambios.TopeAdicionalMes is < 0 or > MaximoValidacionesMes)
            throw ReglaDeNegocioException.Validacion("tope-adicional-invalido",
                $"El tope de validaciones adicionales debe estar entre 0 y {MaximoValidacionesMes}, o vacío para no tener tope.");
        var umbral = cambios.UmbralConfianza ?? UmbralConfianza;
        if (umbral is < 0m or > 1m)
            throw ReglaDeNegocioException.Validacion("umbral-confianza-invalido", "El umbral de confianza debe estar entre 0 y 1.");
        if (cambios.PrecioAdicional is < 0m or > TarifaDeIa.PrecioMaximo)
            throw ReglaDeNegocioException.Validacion("precio-adicional-invalido", $"El precio de cada validación adicional debe estar entre 0 y {TarifaDeIa.PrecioMaximo:0}, o vacío para usar la tarifa general.");
        var modelo = ValidarModelo(cambios.ModeloPorDefecto) ?? ModeloPorDefecto;
        var escalamiento = ValidarModelo(cambios.ModeloEscalamiento) ?? ModeloEscalamiento;

        if (cambios.ApiKeyOpenAi is { } llave)
        {
            var limpia = llave.Trim();
            if (limpia.Length == 0)
            {
                ApiKeyOpenAiProtegida = null;
                UltimosCuatroLlave = null;
            }
            else
            {
                ValidarLlave(limpia);
                ApiKeyOpenAiProtegida = proteger(limpia);
                UltimosCuatroLlave = limpia[^4..];
            }
        }

        ValidacionesIncluidasMes = cambios.ValidacionesIncluidasMes;
        AdicionalHabilitado = cambios.AdicionalHabilitado;
        TopeAdicionalMes = cambios.TopeAdicionalMes;
        PrecioAdicional = cambios.PrecioAdicional;
        ModeloPorDefecto = modelo;
        ModeloEscalamiento = escalamiento;
        UmbralConfianza = umbral;
        Activo = cambios.Activo;
        ActualizadoEn = ahora;
    }

    /// <summary>Forma de una API key de OpenAI: 20 a 512 caracteres sin espacios. Devuelve la llave recortada o 422 <c>api-key-invalida</c>.</summary>
    public static string ValidarLlave(string llave)
    {
        var limpia = (llave ?? string.Empty).Trim();
        if (limpia.Length is < 20 or > 512 || limpia.Any(char.IsWhiteSpace))
            throw ReglaDeNegocioException.Validacion("api-key-invalida",
                "La API key de OpenAI no parece válida: debe tener entre 20 y 512 caracteres y no llevar espacios.");
        return limpia;
    }

    private static string? ValidarModelo(string? modelo)
    {
        if (modelo is null)
            return null;
        var limpio = modelo.Trim();
        return RegexModelo().IsMatch(limpio)
            ? limpio
            : throw ReglaDeNegocioException.Validacion("modelo-invalido",
                $"'{modelo}' no es un nombre de modelo válido (letras, números, puntos, guiones; hasta 100 caracteres).");
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._:-]{0,99}$")]
    private static partial Regex RegexModelo();
}
