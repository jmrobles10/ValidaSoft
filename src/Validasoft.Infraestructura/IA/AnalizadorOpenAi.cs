using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;
using Validasoft.Aplicacion.Ia;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ia;

namespace Validasoft.Infraestructura.IA;

/// <summary>
/// Análisis con OpenAI (ADR-0012, IA-1): <c>POST {UrlBase}/responses</c> con el PDF como <c>input_file</c> (o la imagen como
/// <c>input_image</c>), las instrucciones del tipo y su esquema estricto (<c>text.format = json_schema</c>), sin guardar la
/// conversación en OpenAI (<c>store: false</c>). Fail-closed (IA-6): error HTTP, tiempo agotado, respuesta incompleta o
/// fuera del esquema ⇒ <see cref="AnalisisDeIaException"/>. Nunca registra el contenido del documento ni la llave.
/// </summary>
public sealed class AnalizadorOpenAi(IHttpClientFactory fabrica, OpcionesDeIa opciones, ILogger<AnalizadorOpenAi> logger) : IAnalizadorDeDocumentos
{
    public const string Cliente = "validasoft-openai";

    public async Task<ResultadoDeAnalisis> AnalizarAsync(SolicitudDeAnalisis solicitud, string apiKey, CancellationToken ct)
    {
        var esquema = EsquemasDeAnalisis.Para(solicitud.TipoCanonico)
                      ?? throw new AnalisisDeIaException("tipo-no-soportado", $"{solicitud.TipoCanonico} no tiene esquema de análisis.");
        var archivo = PreparacionDeArchivo.Preparar(solicitud.Contenido, solicitud.Mime, solicitud.Paginas, solicitud.Limites);
        var cuerpo = SolicitudOpenAi.Cuerpo(solicitud, esquema, archivo.Contenido);

        using var peticion = new HttpRequestMessage(HttpMethod.Post, opciones.UrlBase + "/responses")
        {
            Content = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json")
        };
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(opciones.Timeout);
        try
        {
            using var respuesta = await fabrica.CreateClient(Cliente).SendAsync(peticion, limite.Token);
            var texto = await respuesta.Content.ReadAsStringAsync(limite.Token);
            if (!respuesta.IsSuccessStatusCode)
            {
                // El cuerpo del error no se registra: puede repetir parte de la solicitud.
                logger.LogWarning("OpenAI respondió {Codigo} al analizar el documento {DocumentoId} v{Numero} con {Modelo}.", (int)respuesta.StatusCode,
                    solicitud.DocumentoId, solicitud.Numero, solicitud.Modelo);
                throw new AnalisisDeIaException(Motivo(respuesta.StatusCode), $"OpenAI respondió {(int)respuesta.StatusCode}.");
            }

            var (hechos, uso, modelo) = RespuestaDeOpenAi.Interpretar(texto, esquema);
            return new ResultadoDeAnalisis(modelo ?? solicitud.Modelo, hechos, uso, archivo.PaginasEnviadas, archivo.PaginasOmitidas);
        }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            throw new AnalisisDeIaException("tiempo-agotado", "OpenAI no respondió a tiempo.", e);
        }
        catch (HttpRequestException e)
        {
            throw new AnalisisDeIaException("error-de-conexion", "No se pudo conectar con OpenAI.", e);
        }
    }

    /// <summary>
    /// <c>GET {UrlBase}/models</c> con la llave (PT-6): no consume tokens. 200 ⇒ válida; 401/403 ⇒ inválida; 429 ⇒ sin cuota
    /// o límite; otra respuesta, red o tiempo agotado ⇒ error. El cuerpo de la respuesta no se lee ni se registra.
    /// </summary>
    public async Task<ResultadoDePruebaDeLlave> ProbarLlaveAsync(string apiKey, CancellationToken ct)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, opciones.UrlBase + "/models");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TimeSpan.FromSeconds(Math.Min(15, opciones.Timeout.TotalSeconds)));
        try
        {
            using var respuesta = await fabrica.CreateClient(Cliente).SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            return respuesta.StatusCode switch
            {
                HttpStatusCode.OK => new(ResultadoDePruebaDeLlave.Valida, "OpenAI aceptó la llave."),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new(ResultadoDePruebaDeLlave.Invalida,
                    "OpenAI rechazó la llave: revisa que esté completa y activa en la organización."),
                HttpStatusCode.TooManyRequests => new(ResultadoDePruebaDeLlave.Limite,
                    "OpenAI aceptó la llave pero respondió que no hay cuota o se superó el límite de uso."),
                _ => new(ResultadoDePruebaDeLlave.Error, $"OpenAI respondió {(int)respuesta.StatusCode}; intenta de nuevo más tarde.")
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(ResultadoDePruebaDeLlave.Error, "OpenAI no respondió a tiempo.");
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("No se pudo conectar con OpenAI para probar una llave.");
            return new(ResultadoDePruebaDeLlave.Error, "No se pudo conectar con OpenAI.");
        }
    }

    private static string Motivo(HttpStatusCode codigo) => codigo switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "llave-invalida",
        HttpStatusCode.TooManyRequests => "limite-del-proveedor",
        _ => $"http-{(int)codigo}"
    };
}

/// <summary>
/// Prueba de la llave del analizador sin red (<c>Validasoft:IA:Proveedor = Simulado</c>, PT-6): la acepta salvo que contenga
/// «rechazada» (inválida) o «limite» (sin cuota). El análisis simulado (F5) está en AnalizadorSimulado.cs.
/// </summary>
public sealed partial class AnalizadorSimulado
{
    public Task<ResultadoDePruebaDeLlave> ProbarLlaveAsync(string apiKey, CancellationToken ct) =>
        Task.FromResult(apiKey.Contains("rechazada", StringComparison.OrdinalIgnoreCase)
            ? new ResultadoDePruebaDeLlave(ResultadoDePruebaDeLlave.Invalida, "La llave fue rechazada (analizador simulado).")
            : apiKey.Contains("limite", StringComparison.OrdinalIgnoreCase)
                ? new ResultadoDePruebaDeLlave(ResultadoDePruebaDeLlave.Limite, "La llave no tiene cuota (analizador simulado).")
                : new ResultadoDePruebaDeLlave(ResultadoDePruebaDeLlave.Valida, "Llave aceptada (analizador simulado: no se llamó a OpenAI)."));
}

/// <summary>Archivo listo para enviar: el PDF recortado a las páginas permitidas (o el original) y lo enviado/omitido.</summary>
public sealed record ArchivoPreparado(byte[] Contenido, int? PaginasEnviadas, int PaginasOmitidas);

/// <summary>
/// Aplica los límites del tipo al archivo (IA-5): un PDF con más páginas que las permitidas se recorta a las primeras con
/// PdfPig (las demás no se envían y quedan anotadas); una imagen va entera. Más grande que el máximo ⇒
/// <c>archivo-demasiado-grande</c>.
/// </summary>
public static class PreparacionDeArchivo
{
    public static ArchivoPreparado Preparar(byte[] contenido, string mime, int? paginas, LimitesDeAnalisis limites)
    {
        var preparado = new ArchivoPreparado(contenido, null, 0);
        if (mime == InspectorDeArchivos.Pdf)
        {
            preparado = paginas is { } conocidas && conocidas <= limites.PaginasMaximas
                ? new ArchivoPreparado(contenido, conocidas, 0)
                : RecortarSiExcede(contenido, limites.PaginasMaximas);
        }

        if (preparado.Contenido.LongLength > limites.TamanoMaximoBytes)
            throw new AnalisisDeIaException("archivo-demasiado-grande",
                $"El archivo supera el máximo que se envía al análisis ({limites.TamanoMaximoBytes / (1024 * 1024)} MB).");
        return preparado;
    }

    /// <summary>Cuenta las páginas con PdfPig y, si pasan de <paramref name="maximo"/>, arma un PDF con las primeras.</summary>
    public static ArchivoPreparado RecortarSiExcede(byte[] pdf, int maximo)
    {
        try
        {
            using var documento = PdfDocument.Open(pdf);
            var total = documento.NumberOfPages;
            if (total <= maximo)
                return new ArchivoPreparado(pdf, total, 0);

            using var constructor = new PdfDocumentBuilder();
            for (var pagina = 1; pagina <= maximo; pagina++)
                constructor.AddPage(documento, pagina);
            return new ArchivoPreparado(constructor.Build(), maximo, total - maximo);
        }
        catch (Exception e) when (e is not AnalisisDeIaException)
        {
            throw new AnalisisDeIaException("pdf-no-procesable", "No se pudo leer el PDF para recortar sus páginas.", e);
        }
    }
}

/// <summary>Cuerpo de <c>POST /v1/responses</c> (API Responses con salida estructurada).</summary>
public static class SolicitudOpenAi
{
    public static JsonObject Cuerpo(SolicitudDeAnalisis solicitud, EsquemaDeAnalisis esquema, byte[] contenido)
    {
        var base64 = Convert.ToBase64String(contenido);
        // Nombre neutro: el original del tercero puede tener datos personales y no aporta al análisis.
        JsonObject archivo = solicitud.Mime == InspectorDeArchivos.Pdf
            ? new JsonObject
            {
                ["type"] = "input_file",
                ["filename"] = $"{esquema.TipoCanonico.ToLowerInvariant()}-v{solicitud.Numero}.pdf",
                ["file_data"] = "data:application/pdf;base64," + base64
            }
            : new JsonObject
            {
                ["type"] = "input_image",
                ["image_url"] = $"data:{solicitud.Mime};base64,{base64}",
                ["detail"] = solicitud.Limites.DetalleImagen
            };

        return new JsonObject
        {
            ["model"] = solicitud.Modelo,
            ["store"] = false,
            ["reasoning"] = new JsonObject { ["effort"] = "low" },
            ["max_output_tokens"] = solicitud.Limites.MaxTokensSalida,
            ["input"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonArray(archivo, new JsonObject { ["type"] = "input_text", ["text"] = esquema.Instrucciones() })
            }),
            ["text"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["name"] = esquema.NombreFormato,
                    ["strict"] = true,
                    ["schema"] = esquema.Json()
                }
            }
        };
    }
}

/// <summary>
/// Lee la respuesta de la API Responses: estado <c>completed</c> (si no ⇒ <c>respuesta-incompleta</c>), el texto de
/// <c>output[].content[]</c> con <c>type: output_text</c> (una negativa ⇒ <c>respuesta-rechazada</c>; sin texto ⇒
/// <c>respuesta-sin-texto</c>), los hechos según el esquema y el uso de tokens (<c>usage</c>).
/// </summary>
public static class RespuestaDeOpenAi
{
    public static (HechosExtraidos Hechos, UsoDeTokens Uso, string? Modelo) Interpretar(string json, EsquemaDeAnalisis esquema)
    {
        JsonElement raiz;
        try
        {
            using var documento = JsonDocument.Parse(json);
            raiz = documento.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new AnalisisDeIaException("respuesta-invalida", "La respuesta de OpenAI no es JSON válido.", e);
        }

        if (raiz.ValueKind != JsonValueKind.Object)
            throw new AnalisisDeIaException("respuesta-invalida", "La respuesta de OpenAI no tiene la forma esperada.");

        var estado = Texto(raiz, "status");
        if (estado != "completed")
        {
            var razon = raiz.TryGetProperty("incomplete_details", out var detalle) && detalle.ValueKind == JsonValueKind.Object ? Texto(detalle, "reason") : null;
            throw new AnalisisDeIaException("respuesta-incompleta",
                $"OpenAI no completó la respuesta (estado {estado ?? "desconocido"}{(razon is null ? string.Empty : $", {razon}")}).");
        }

        var textos = new List<string>();
        if (raiz.TryGetProperty("output", out var salidas) && salidas.ValueKind == JsonValueKind.Array)
        {
            foreach (var salida in salidas.EnumerateArray())
            {
                if (!salida.TryGetProperty("content", out var contenidos) || contenidos.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var contenido in contenidos.EnumerateArray())
                {
                    switch (Texto(contenido, "type"))
                    {
                        case "refusal":
                            throw new AnalisisDeIaException("respuesta-rechazada", "El modelo se negó a analizar el documento.");
                        case "output_text" when Texto(contenido, "text") is { } texto:
                            textos.Add(texto);
                            break;
                    }
                }
            }
        }

        if (textos.Count == 0)
            throw new AnalisisDeIaException("respuesta-sin-texto", "La respuesta de OpenAI no trae texto de salida.");

        var hechos = esquema.Interpretar(string.Concat(textos));
        var uso = raiz.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object
            ? new UsoDeTokens(Numero(usage, "input_tokens"),
                usage.TryGetProperty("input_tokens_details", out var entrada) && entrada.ValueKind == JsonValueKind.Object ? Numero(entrada, "cached_tokens") : 0,
                Numero(usage, "output_tokens"))
            : new UsoDeTokens(0, 0, 0);
        return (hechos, uso, Texto(raiz, "model"));
    }

    private static string? Texto(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    private static long Numero(JsonElement objeto, string propiedad) =>
        objeto.TryGetProperty(propiedad, out var valor) && valor.ValueKind == JsonValueKind.Number && valor.TryGetInt64(out var n) ? n : 0;
}
