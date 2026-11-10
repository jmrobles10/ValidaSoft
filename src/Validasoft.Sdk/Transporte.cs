using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Validasoft.Sdk;

/// <summary>Respuesta de un GET condicional: <see cref="NoModificado"/> = 304 (sigue valiendo la copia de <see cref="ETag"/>).</summary>
public sealed record RespuestaCondicional<T>(T? Datos, string? ETag, bool NoModificado) where T : class;

/// <summary>Opciones de una petición concreta.</summary>
internal sealed record Peticion(
    HttpMethod Metodo,
    string Ruta,
    object? Cuerpo = null,
    string? ClaveIdempotencia = null,
    string? SiNoCoincide = null,
    bool Reintentable = false,
    bool ConEmpresa = true);

/// <summary>
/// HTTP de bajo nivel: cabeceras (<c>X-Api-Key</c>, <c>X-Empresa</c>, <c>X-Usuario-Delegado</c> firmada en cada intento,
/// <c>Idempotency-Key</c> en todo POST), reintentos solo de lo idempotente y traducción de errores a
/// <see cref="ExcepcionValidasoft"/>.
/// </summary>
internal sealed class Transporte
{
    public static readonly JsonSerializerOptions Json = CrearJson();

    private readonly HttpClient _http;
    private readonly OpcionesValidasoft _opciones;

    public Transporte(HttpClient http, OpcionesValidasoft opciones, Guid? empresaId, UsuarioDelegado? usuario)
    {
        _http = http;
        _opciones = opciones;
        EmpresaId = empresaId;
        Usuario = usuario;
    }

    public Guid? EmpresaId { get; }

    public UsuarioDelegado? Usuario { get; }

    public Transporte Con(Guid? empresaId, UsuarioDelegado? usuario) => new(_http, _opciones, empresaId, usuario);

    public async Task<T> EnviarAsync<T>(Peticion peticion, CancellationToken ct)
    {
        var (datos, _, _) = await EnviarCondicionalAsync<T>(peticion, ct);
        return datos!;
    }

    public async Task<(T? Datos, string? ETag, bool NoModificado)> EnviarCondicionalAsync<T>(Peticion peticion, CancellationToken ct)
    {
        // La misma clave en todos los intentos: si el primero llegó y se cortó la respuesta, el reintento repite la respuesta
        // guardada en lugar de crear otro recurso.
        var clave = peticion.Metodo == HttpMethod.Post ? peticion.ClaveIdempotencia ?? Guid.NewGuid().ToString() : null;
        var reintentable = peticion.Metodo == HttpMethod.Get || peticion.Metodo == HttpMethod.Put || peticion.Reintentable;
        var cuerpo = peticion.Cuerpo is null ? null : JsonSerializer.Serialize(peticion.Cuerpo, peticion.Cuerpo.GetType(), Json);

        for (var intento = 0; ; intento++)
        {
            using var mensaje = Construir(peticion, clave, cuerpo);
            HttpResponseMessage respuesta;
            try
            {
                respuesta = await _http.SendAsync(mensaje, HttpCompletionOption.ResponseContentRead, ct);
            }
            catch (HttpRequestException) when (reintentable && intento < _opciones.Reintentos)
            {
                await EsperarAsync(intento, ct);
                continue;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && reintentable && intento < _opciones.Reintentos)
            {
                await EsperarAsync(intento, ct);
                continue;
            }

            using (respuesta)
            {
                if (reintentable && intento < _opciones.Reintentos && EsTransitorio(respuesta.StatusCode))
                {
                    await EsperarAsync(intento, ct);
                    continue;
                }

                var etag = respuesta.Headers.ETag?.ToString();
                if (respuesta.StatusCode == HttpStatusCode.NotModified)
                    return (default, etag ?? peticion.SiNoCoincide, true);

                var texto = await respuesta.Content.ReadAsStringAsync(ct);
                if (!respuesta.IsSuccessStatusCode)
                    throw Error(respuesta, texto);

                if (typeof(T) == typeof(SinContenido) || string.IsNullOrEmpty(texto))
                    return (default, etag, false);
                return (JsonSerializer.Deserialize<T>(texto, Json), etag, false);
            }
        }
    }

    private HttpRequestMessage Construir(Peticion peticion, string? clave, string? cuerpo)
    {
        var mensaje = new HttpRequestMessage(peticion.Metodo, Url(peticion.Ruta));
        mensaje.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", _opciones.ApiKey);
        if (peticion.ConEmpresa && EmpresaId is { } empresa)
            mensaje.Headers.TryAddWithoutValidation("X-Empresa", empresa.ToString());
        if (Usuario is { } usuario)
        {
            var firmante = _opciones.Firmante
                           ?? throw new InvalidOperationException("Para actuar como usuario configura OpcionesValidasoft.Firmante con la clave privada del sistema.");
            // Una aserción por intento: Validasoft rechaza un jti repetido (SE-4).
            mensaje.Headers.TryAddWithoutValidation("X-Usuario-Delegado", firmante.Firmar(usuario));
        }

        if (clave is not null)
            mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", clave);
        if (peticion.SiNoCoincide is not null)
            mensaje.Headers.TryAddWithoutValidation("If-None-Match", peticion.SiNoCoincide);
        if (cuerpo is not null)
            mensaje.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
        return mensaje;
    }

    private Uri Url(string ruta)
    {
        var origen = _opciones.UrlBase ?? _http.BaseAddress
                     ?? throw new InvalidOperationException("Configura OpcionesValidasoft.UrlBase (o HttpClient.BaseAddress).");
        // Con una ruta base (https://host/validasoft) la relativa se resuelve debajo de ella, no en la raíz del host.
        var conBarra = origen.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? origen : new Uri(origen.AbsoluteUri + "/");
        return new Uri(conBarra, ruta.TrimStart('/'));
    }

    private static bool EsTransitorio(HttpStatusCode codigo) =>
        codigo is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)codigo >= 500;

    private Task EsperarAsync(int intento, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromTicks(_opciones.EsperaEntreReintentos.Ticks * (1L << Math.Min(intento, 6))), ct);

    private static ExcepcionValidasoft Error(HttpResponseMessage respuesta, string texto)
    {
        string? codigo = null;
        string? detalle = null;
        try
        {
            using var json = JsonDocument.Parse(texto);
            if (json.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (json.RootElement.TryGetProperty("codigo", out var c) && c.ValueKind == JsonValueKind.String)
                    codigo = c.GetString();
                if (json.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                    detalle = d.GetString();
                else if (json.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                    detalle = t.GetString();
            }
        }
        catch (JsonException)
        {
            detalle = texto.Length > 300 ? texto.Substring(0, 300) : texto;
        }

        var correlacion = respuesta.Headers.TryGetValues("X-Correlation-Id", out var valores) ? valores.FirstOrDefault() : null;
        return new ExcepcionValidasoft(respuesta.StatusCode, codigo, detalle, correlacion);
    }

    private static JsonSerializerOptions CrearJson()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new JsonStringEnumConverter());
        return opciones;
    }
}

/// <summary>Marca de una respuesta sin cuerpo (204).</summary>
internal sealed class SinContenido
{
}
