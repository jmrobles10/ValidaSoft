using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validasoft.Dominio.Ia;

namespace Validasoft.Aplicacion.Ia;

/// <summary>
/// Lo que se le pide al analizador: el archivo de una versión (PDF o imagen, ya leído de la bóveda), su tipo canónico,
/// el modelo y los límites del tipo (IA-5). El contenido nunca se registra en logs ni se guarda después del análisis.
/// </summary>
public sealed record SolicitudDeAnalisis(
    Guid DocumentoId,
    int Numero,
    string TipoCanonico,
    string Mime,
    string NombreArchivo,
    byte[] Contenido,
    int? Paginas,
    string Modelo,
    LimitesDeAnalisis Limites);

/// <summary>Tokens que reporta OpenAI: la entrada incluye los de caché.</summary>
public sealed record UsoDeTokens(long Entrada, long EntradaEnCache, long Salida);

/// <summary>Hechos extraídos que cumplen el esquema del tipo: comunes y <see cref="Campos"/> propios del tipo.</summary>
public sealed record HechosExtraidos(
    string TipoDetectado,
    bool Legible,
    decimal Confianza,
    IReadOnlyList<string> Alertas,
    string? Observaciones,
    IReadOnlyDictionary<string, JsonElement> Campos);

/// <summary>Resultado de una llamada: el modelo que respondió, los hechos, los tokens y las páginas enviadas y omitidas.</summary>
public sealed record ResultadoDeAnalisis(string Modelo, HechosExtraidos Hechos, UsoDeTokens Uso, int? PaginasEnviadas, int? PaginasOmitidas);

/// <summary>
/// Falla controlada del análisis (fail-closed, IA-6): error HTTP, tiempo agotado, respuesta incompleta, JSON inválido o
/// fuera del esquema, archivo demasiado grande. <see cref="Motivo"/> es un código estable en kebab-case que va al evento
/// <c>AnalisisDeIaFallido</c>; el mensaje no lleva contenido del documento ni la llave.
/// </summary>
public sealed class AnalisisDeIaException(string motivo, string mensaje, Exception? interna = null) : Exception(mensaje, interna)
{
    public string Motivo { get; } = motivo;
}

/// <summary>
/// Análisis de documentos con un proveedor de IA (ADR-0012: OpenAI, API Responses con salida estructurada). La llave es la
/// de la empresa que paga la llamada. Lanza <see cref="AnalisisDeIaException"/> ante cualquier falla controlada.
/// </summary>
public interface IAnalizadorDeDocumentos
{
    Task<ResultadoDeAnalisis> AnalizarAsync(SolicitudDeAnalisis solicitud, string apiKey, CancellationToken ct);

    /// <summary>
    /// Verifica la llave con una consulta que no consume tokens (PT-6). Solo cuando Plataforma lo pide («Probar llave»).
    /// Nunca lanza por la respuesta del proveedor: la traduce a <see cref="ResultadoDePruebaDeLlave"/>.
    /// </summary>
    Task<ResultadoDePruebaDeLlave> ProbarLlaveAsync(string apiKey, CancellationToken ct);
}

/// <summary>
/// Resultado de probar una llave: <c>valida</c>, <c>invalida</c> (401/403), <c>limite</c> (429) o <c>error</c> (red, tiempo
/// agotado u otra respuesta). El mensaje no lleva la llave ni el cuerpo de la respuesta.
/// </summary>
public sealed record ResultadoDePruebaDeLlave(string Resultado, string Mensaje)
{
    public const string Valida = "valida";
    public const string Invalida = "invalida";
    public const string Limite = "limite";
    public const string Error = "error";
    public const string SinLlave = "sin-llave";
}

/// <summary>
/// Configuración de la IA (<c>Validasoft:IA</c>): interruptor general, URL base de la API Responses, tiempo máximo por
/// llamada y precios por modelo (dólares por millón de tokens de entrada, entrada en caché y salida). Sin secretos: cada
/// empresa trae su propia llave (IA-2).
/// </summary>
public sealed class OpcionesDeIa(IConfiguration configuracion)
{
    private static readonly IReadOnlyDictionary<string, TarifaDeModelo> PreciosPorDefecto = new Dictionary<string, TarifaDeModelo>(StringComparer.OrdinalIgnoreCase)
    {
        [PlanDeIaEmpresa.ModeloPorDefectoInicial] = new(0.10m, 0.01m, 0.50m),
        [PlanDeIaEmpresa.ModeloEscalamientoInicial] = new(1.00m, 0.05m, 5.00m)
    };

    private readonly IConfigurationSection _seccion = configuracion.GetSection("Validasoft:IA");

    /// <summary>Apagada ⇒ ninguna versión se analiza (nada se consume).</summary>
    public bool Habilitada => _seccion.GetValue("Habilitada", true);

    /// <summary>
    /// <c>OpenAi</c> (por defecto) llama a la API real con la llave de cada empresa; <c>Simulado</c> no sale a la red (pruebas
    /// de punta a punta y demos): el análisis falla de forma controlada y la prueba de la llave la acepta salvo que contenga
    /// <c>rechazada</c> (PT-6).
    /// </summary>
    public string Proveedor => _seccion["Proveedor"] is { Length: > 0 } proveedor ? proveedor : "OpenAi";

    public string UrlBase => (_seccion["UrlBase"] is { Length: > 0 } url ? url : "https://api.openai.com/v1").TrimEnd('/');

    public TimeSpan Timeout => TimeSpan.FromSeconds(Math.Clamp(_seccion.GetValue("TimeoutSegundos", 60), 5, 600));

    /// <summary>
    /// Tarifa del modelo: la configurada en <c>Validasoft:IA:Precios:{modelo}</c> (cada valor por separado) o la por
    /// defecto; un modelo desconocido se cobra con la tarifa del modelo de escalamiento por defecto (la más alta conocida).
    /// </summary>
    public TarifaDeModelo Tarifa(string modelo)
    {
        var tarifaBase = PreciosPorDefecto.TryGetValue(modelo, out var conocida) ? conocida : PreciosPorDefecto[PlanDeIaEmpresa.ModeloEscalamientoInicial];
        var seccion = _seccion.GetSection("Precios").GetChildren()
            .FirstOrDefault(s => string.Equals(s.Key, modelo, StringComparison.OrdinalIgnoreCase));
        if (seccion is null)
            return tarifaBase;
        return new TarifaDeModelo(Decimal(seccion["Entrada"]) ?? tarifaBase.Entrada, Decimal(seccion["EntradaEnCache"]) ?? tarifaBase.EntradaEnCache,
            Decimal(seccion["Salida"]) ?? tarifaBase.Salida);
    }

    private static decimal? Decimal(string? valor) =>
        decimal.TryParse(valor, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && d >= 0 ? d : null;
}
