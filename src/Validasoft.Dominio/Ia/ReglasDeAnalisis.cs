using System.Globalization;
using System.Text;
using System.Text.Json;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Ia;

/// <summary>
/// Límites de lo que se envía a OpenAI por tipo canónico (IA-5): páginas máximas, detalle de las imágenes (<c>high</c>
/// solo donde hay letra pequeña), tokens de salida y tamaño máximo enviado. Lo que excede no se envía y queda anotado.
/// </summary>
public sealed record LimitesDeAnalisis(int PaginasMaximas, string DetalleImagen, int MaxTokensSalida, long TamanoMaximoBytes)
{
    public const string DetalleAlto = "high";
    public const string DetalleBajo = "low";
    public const long TamanoMaximoEnviado = 20L * 1024 * 1024;

    public static LimitesDeAnalisis Para(string? tipoCanonico) => (tipoCanonico ?? string.Empty).ToUpperInvariant() switch
    {
        "RUT" => new(4, DetalleAlto, 2000, TamanoMaximoEnviado),
        "CEDULA" => new(2, DetalleAlto, 2000, TamanoMaximoEnviado),
        "CERTIFICACION_BANCARIA" => new(2, DetalleBajo, 2000, TamanoMaximoEnviado),
        "CAMARA_COMERCIO" => new(10, DetalleAlto, 3000, TamanoMaximoEnviado),
        "ESTADOS_FINANCIEROS" => new(6, DetalleAlto, 2000, TamanoMaximoEnviado),
        _ => new(4, DetalleBajo, 2000, TamanoMaximoEnviado)
    };

    /// <summary>Páginas que se envían y las que se omiten de un documento de <paramref name="paginas"/> páginas (nulo si no se sabe).</summary>
    public (int? Enviadas, int Omitidas) Recorte(int? paginas) =>
        paginas is not { } total ? (null, 0) : total > PaginasMaximas ? (PaginasMaximas, total - PaginasMaximas) : (total, 0);
}

/// <summary>Precio de un modelo en dólares por millón de tokens (entrada, entrada en caché y salida), configurable (IA-1).</summary>
public sealed record TarifaDeModelo(decimal Entrada, decimal EntradaEnCache, decimal Salida)
{
    /// <summary>
    /// Costo real de una llamada: los tokens de entrada incluyen los de caché, que se cobran a su tarifa; el resto a la de
    /// entrada; la salida a la suya.
    /// </summary>
    public decimal Costo(long tokensEntrada, long tokensEnCache, long tokensSalida)
    {
        var cache = Math.Clamp(tokensEnCache, 0, Math.Max(0, tokensEntrada));
        return ((tokensEntrada - cache) * Entrada + cache * EntradaEnCache + tokensSalida * Salida) / 1_000_000m;
    }
}

/// <summary>Una llamada a OpenAI (la del modelo por defecto o la de escalamiento) con su consumo y costo.</summary>
public sealed record LlamadaDeIa(string Modelo, decimal Confianza, long TokensEntrada, long TokensEntradaEnCache, long TokensSalida, decimal CostoUsd);

/// <summary>Resultado de comparar los campos clave de dos análisis (sin otra llamada a OpenAI).</summary>
public sealed record ComparacionDeHechos(Comparacion Comparacion, IReadOnlyList<string> CamposCambiados, IReadOnlyList<string> CamposCriticosCambiados);

/// <summary>Reglas puras de la revisión automática (IA-1, IA-6): cuándo escalar, cuándo pedir revisión humana y la comparación entre versiones.</summary>
public static class ReglasDeAnalisis
{
    /// <summary>
    /// Se escala al modelo superior si la confianza quedó bajo el umbral, el documento no es legible, hay señales de
    /// manipulación o cambió un campo crítico respecto de la versión anterior (IA-1).
    /// </summary>
    public static bool DebeEscalar(decimal confianza, bool legible, int alertas, decimal umbral, int camposCriticosCambiados = 0) =>
        confianza < umbral || !legible || alertas > 0 || camposCriticosCambiados > 0;

    /// <summary>Con el resultado final: confianza bajo el umbral, ilegible o con alertas ⇒ revisión humana.</summary>
    public static bool PendienteRevisionHumana(decimal confianza, bool legible, int alertas, decimal umbral) =>
        confianza < umbral || !legible || alertas > 0;

    /// <summary>
    /// Compara los campos clave del análisis anterior con el nuevo: textos sin distinguir mayúsculas, tildes ni espacios
    /// repetidos; números por valor; listas y objetos por su contenido normalizado. Sin análisis anterior ⇒ nulo (la
    /// comparación queda la de los hechos básicos).
    /// </summary>
    public static ComparacionDeHechos? Comparar(IReadOnlyDictionary<string, JsonElement>? anterior, IReadOnlyDictionary<string, JsonElement> nuevo,
        IReadOnlyList<string> camposClave, IReadOnlyList<string> camposCriticos)
    {
        if (anterior is null)
            return null;

        var cambiados = camposClave
            .Where(c => Normalizar(anterior.TryGetValue(c, out var a) ? a : (JsonElement?)null) !=
                        Normalizar(nuevo.TryGetValue(c, out var n) ? n : (JsonElement?)null))
            .ToList();
        var criticos = cambiados.Where(camposCriticos.Contains).ToList();
        return new ComparacionDeHechos(cambiados.Count == 0 ? Comparacion.SinCambios : Comparacion.CambiosDetectados, cambiados, criticos);
    }

    /// <summary>Forma canónica de un valor para comparar.</summary>
    public static string Normalizar(JsonElement? valor)
    {
        if (valor is not { } v)
            return string.Empty;
        return v.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            JsonValueKind.String => Texto(v.GetString()),
            JsonValueKind.Number => v.TryGetDecimal(out var d) ? d.ToString("G29", CultureInfo.InvariantCulture) : v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Array => "[" + string.Join("|", v.EnumerateArray().Select(e => Normalizar(e))) + "]",
            _ => "{" + string.Join("|", v.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => p.Name + "=" + Normalizar(p.Value))) + "}"
        };
    }

    private static string Texto(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;
        var sinTildes = new StringBuilder();
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sinTildes.Append(c);
        }

        return string.Join(' ', sinTildes.ToString().ToUpperInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
