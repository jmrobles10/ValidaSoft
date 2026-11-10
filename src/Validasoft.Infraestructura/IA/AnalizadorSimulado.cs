using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Validasoft.Aplicacion.Ia;
using Validasoft.Dominio.Ia;

namespace Validasoft.Infraestructura.IA;

/// <summary>
/// Analizador simulado y determinista (desarrollo y pruebas de punta a punta, F5): nunca llama a OpenAI. Lee del propio
/// archivo marcas <c>[[clave=valor]]</c> (en el texto del PDF o en cualquier parte de los bytes) y arma la respuesta con el
/// esquema del tipo, que pasa por la misma interpretación estricta que la de OpenAI (<see cref="EsquemaDeAnalisis.Interpretar"/>).
/// Los campos sin marca van nulos (las listas, vacías). Marcas especiales: <c>confianza</c> (0–1, por omisión 0,97),
/// <c>legible</c> (true/false), <c>tipoDetectado</c> y <c>alerta</c>. Listas separadas por coma; personas con
/// <c>nombre|identificacion|cargo</c> separadas por punto y coma. Se activa con <c>Validasoft:IA:Proveedor = Simulado</c>.
/// </summary>
public sealed partial class AnalizadorSimulado : IAnalizadorDeDocumentos
{
    public const string Proveedor = "Simulado";

    public Task<ResultadoDeAnalisis> AnalizarAsync(SolicitudDeAnalisis solicitud, string apiKey, CancellationToken ct)
    {
        var esquema = EsquemasDeAnalisis.Para(solicitud.TipoCanonico)
                      ?? throw new AnalisisDeIaException("tipo-no-soportado", $"{solicitud.TipoCanonico} no tiene esquema de análisis.");
        var hechos = Hechos(esquema, solicitud.Contenido);
        return Task.FromResult(new ResultadoDeAnalisis(solicitud.Modelo, hechos, new UsoDeTokens(1000, 0, 300), solicitud.Paginas, 0));
    }

    [GeneratedRegex(@"\[\[([A-Za-z][A-Za-z0-9]*)=([^\]]*)\]\]")]
    private static partial Regex RegexMarca();

    /// <summary>Marcas <c>[[clave=valor]]</c> del archivo (la última gana).</summary>
    public static IReadOnlyDictionary<string, string> Marcas(byte[] contenido)
    {
        var texto = Encoding.Latin1.GetString(contenido);
        var marcas = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match marca in RegexMarca().Matches(texto))
            marcas[marca.Groups[1].Value] = marca.Groups[2].Value.Trim();
        return marcas;
    }

    /// <summary>Hechos del esquema con los valores de las marcas del archivo (interpretados como una respuesta de OpenAI).</summary>
    public static HechosExtraidos Hechos(EsquemaDeAnalisis esquema, byte[] contenido)
    {
        var marcas = Marcas(contenido);
        var campos = new JsonObject();
        foreach (var campo in esquema.Campos)
            campos[campo.Nombre] = Valor(campo, marcas);

        var respuesta = new JsonObject
        {
            ["tipoDetectado"] = marcas.GetValueOrDefault("tipoDetectado") ?? esquema.TipoCanonico,
            ["legible"] = !string.Equals(marcas.GetValueOrDefault("legible"), "false", StringComparison.OrdinalIgnoreCase),
            ["confianza"] = decimal.TryParse(marcas.GetValueOrDefault("confianza"), NumberStyles.Number, CultureInfo.InvariantCulture, out var confianza)
                ? confianza
                : 0.97m,
            ["alertasDeManipulacion"] = marcas.TryGetValue("alerta", out var alerta) ? new JsonArray(alerta) : new JsonArray(),
            ["observaciones"] = null,
            ["campos"] = campos
        };
        return esquema.Interpretar(respuesta.ToJsonString());
    }

    private static JsonNode? Valor(CampoDeEsquema campo, IReadOnlyDictionary<string, string> marcas)
    {
        switch (campo.Tipo)
        {
            case TipoDeCampoIa.Lista:
                return new JsonArray([.. Partes(marcas.GetValueOrDefault(campo.Nombre), ',').Select(p => (JsonNode?)JsonValue.Create(p))]);
            case TipoDeCampoIa.Personas:
                return new JsonArray([
                    .. Partes(marcas.GetValueOrDefault(campo.Nombre), ';').Select(p => p.Split('|')).Select(p => (JsonNode?)new JsonObject
                    {
                        ["nombre"] = Vacio(p.ElementAtOrDefault(0)),
                        ["identificacion"] = Vacio(p.ElementAtOrDefault(1)),
                        ["cargo"] = Vacio(p.ElementAtOrDefault(2))
                    })
                ]);
            case TipoDeCampoIa.Datos:
                return new JsonArray([
                    .. marcas.Where(m => m.Key is not ("confianza" or "legible" or "tipoDetectado" or "alerta"))
                        .Select(m => (JsonNode?)new JsonObject { ["clave"] = m.Key, ["valor"] = m.Value })
                ]);
        }

        if (!marcas.TryGetValue(campo.Nombre, out var texto) || texto.Length == 0)
            return null;
        return campo.Tipo switch
        {
            TipoDeCampoIa.Numero => decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out var numero) ? JsonValue.Create(numero) : null,
            TipoDeCampoIa.Booleano => bool.TryParse(texto, out var booleano) ? JsonValue.Create(booleano) : null,
            _ => JsonValue.Create(texto)
        };
    }

    private static IEnumerable<string> Partes(string? texto, char separador) =>
        (texto ?? string.Empty).Split(separador, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Vacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
