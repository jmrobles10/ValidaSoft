using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Marten;
using Microsoft.AspNetCore.Http;
using Validasoft.Dominio.Comun;

namespace Validasoft.Infraestructura.Http;

/// <summary>Clave de idempotencia ya usada (docs/05 §3.1). Global; vence a las 24 horas.</summary>
public sealed class RegistroIdempotencia
{
    public string Id { get; set; } = string.Empty;

    public string HashPeticion { get; set; } = string.Empty;

    public int Codigo { get; set; }

    public string? TipoContenido { get; set; }

    /// <summary>La respuesta que se repite. Si la original traía un secreto, aquí ya no está (<see cref="SecretosOmitidos"/>, PI-10).</summary>
    public string Cuerpo { get; set; } = string.Empty;

    /// <summary>Campos con un secreto (<c>valor</c>, <c>token</c>, <c>secreto</c>) que se quitaron de <see cref="Cuerpo"/> antes de guardarlo.</summary>
    public List<string> SecretosOmitidos { get; set; } = [];

    public string? Location { get; set; }

    public DateTimeOffset ExpiraEn { get; set; }
}

/// <summary>
/// <c>Idempotency-Key</c>: obligatorio en los POST que crean recursos; misma clave y mismo cuerpo repiten la
/// respuesta (<c>Idempotent-Replayed: true</c>); misma clave con otro cuerpo responde 422.
/// <para>
/// <b>Respuestas con secretos (PI-10).</b> La respuesta que muestra una llave, un token o un secreto (API key nueva o rotada, secreto de un webhook,
/// llaves del alta de un grupo, sesiones) <b>no se guarda 24 horas con el secreto</b>: se guarda la misma respuesta con esos campos en <c>null</c> y una
/// lista <c>secretosOmitidos</c>. Quien repite la petición recibe el mismo estado y los mismos datos —el recurso que se creó— sin el secreto, con el
/// encabezado <c>Validasoft-Secretos-Omitidos</c> que dice cuáles faltan: el secreto se vio una sola vez, en la respuesta original.
/// </para>
/// </summary>
public sealed class MiddlewareDeIdempotencia(RequestDelegate siguiente)
{
    public const string Cabecera = "Idempotency-Key";

    /// <summary>Encabezado de la respuesta repetida: los campos que la respuesta original traía y esta ya no (el secreto se muestra una sola vez).</summary>
    public const string CabeceraSecretosOmitidos = "Validasoft-Secretos-Omitidos";

    /// <summary>Campos que llevan un secreto, por ruta (sin distinguir mayúsculas). Una ruta que no está aquí se guarda tal cual.</summary>
    private static readonly (Regex Ruta, string[] Campos)[] RutasConSecreto =
    [
        (RegexRuta("^/api/v1/webhooks(/[^/]+/rotar-secreto)?$"), ["secreto"]),
        (RegexRuta("^/api/v1/api-keys(/[^/]+/rotar)?$"), ["valor"]),
        (RegexRuta("^/api/v1/plataforma/tenants$"), ["valor"]),
        (RegexRuta("^/api/v1/(plataforma/sesiones|sesiones-usuario|sesiones-tercero)$"), ["token"])
    ];

    private static readonly string[] RutasQueCrean =
    [
        "/api/v1/plataforma/tenants", "/api/v1/empresas", "/api/v1/api-keys", "/api/v1/sistemas", "/api/v1/usuarios", "/api/v1/tipos-vinculo",
        "/api/v1/areas", "/api/v1/terceros", "/api/v1/vinculos", "/api/v1/motivos", "/api/v1/flujos", "/api/v1/tipos-documento"
    ];

    private static Regex RegexRuta(string patron) => new(patron, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>Los campos con secreto de la ruta, o <c>null</c> si su respuesta no lleva ninguno.</summary>
    public static string[]? CamposSecretosDe(string ruta) => RutasConSecreto.FirstOrDefault(r => r.Ruta.IsMatch(ruta)).Campos;

    public async Task InvokeAsync(HttpContext http, IDocumentStore store, TimeProvider reloj)
    {
        var contexto = Seguridad.ContextoDesdeHttp.De(http);
        var ruta = http.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var clave = http.Request.Headers[Cabecera].ToString();
        var esPost = HttpMethods.IsPost(http.Request.Method) && ruta.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase);

        if (!esPost || http.User.Identity?.IsAuthenticated != true)
        {
            await siguiente(http);
            return;
        }

        var camposSecretos = CamposSecretosDe(ruta);
        if (string.IsNullOrWhiteSpace(clave))
        {
            // Las rutas con secreto nunca exigieron la clave (rotar, alta de grupo, sesiones): sin ella simplemente no se repite nada.
            if (camposSecretos is null && EsRutaQueCrea(ruta))
                throw ReglaDeNegocioException.Validacion("idempotency-key-requerida", "Este POST crea un recurso: envía la cabecera Idempotency-Key.");
            await siguiente(http);
            return;
        }

        if (clave.Length > 100)
            throw ReglaDeNegocioException.Validacion("idempotency-key-invalida", "Idempotency-Key admite hasta 100 caracteres.");

        http.Request.EnableBuffering();
        using var lector = new StreamReader(http.Request.Body, Encoding.UTF8, leaveOpen: true);
        var cuerpo = await lector.ReadToEndAsync(http.RequestAborted);
        http.Request.Body.Position = 0;

        var ambito = contexto.TenantId?.ToString("N") ?? "plataforma";
        var id = $"{ambito}:{contexto.EmpresaId?.ToString("N") ?? "-"}:{clave}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{http.Request.Method} {ruta}\n{cuerpo}")));

        await using (var consulta = store.QuerySession())
        {
            var previo = await consulta.LoadAsync<RegistroIdempotencia>(id, http.RequestAborted);
            if (previo is not null && previo.ExpiraEn > reloj.GetUtcNow())
            {
                if (previo.HashPeticion != hash)
                    throw ReglaDeNegocioException.Validacion("clave-idempotencia-reutilizada",
                        "Esa Idempotency-Key ya se usó con otra petición.");

                http.Response.StatusCode = previo.Codigo;
                http.Response.ContentType = previo.TipoContenido;
                if (previo.Location is not null)
                    http.Response.Headers.Location = previo.Location;
                http.Response.Headers["Idempotent-Replayed"] = "true";
                if (previo.SecretosOmitidos is { Count: > 0 })
                    http.Response.Headers[CabeceraSecretosOmitidos] = string.Join(',', previo.SecretosOmitidos);
                await http.Response.WriteAsync(previo.Cuerpo, http.RequestAborted);
                return;
            }
        }

        var original = http.Response.Body;
        await using var buffer = new MemoryStream();
        http.Response.Body = buffer;
        try
        {
            await siguiente(http);
        }
        finally
        {
            http.Response.Body = original;
        }

        buffer.Position = 0;
        var respuesta = await new StreamReader(buffer).ReadToEndAsync(http.RequestAborted);
        if (http.Response.StatusCode is >= 200 and < 300)
        {
            // Lo que se guarda para repetir nunca lleva el secreto; si no se puede quitar con certeza, no se guarda nada.
            (string? cuerpoGuardado, IReadOnlyList<string> omitidos) = (respuesta, []);
            if (camposSecretos is not null)
                (cuerpoGuardado, omitidos) = RedactorDeSecretos.Redactar(respuesta, camposSecretos);
            if (cuerpoGuardado is not null)
            {
                await using var sesion = store.LightweightSession();
                sesion.Store(new RegistroIdempotencia
                {
                    Id = id,
                    HashPeticion = hash,
                    Codigo = http.Response.StatusCode,
                    TipoContenido = http.Response.ContentType,
                    Cuerpo = cuerpoGuardado,
                    SecretosOmitidos = [.. omitidos],
                    Location = http.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
                    ExpiraEn = reloj.GetUtcNow().AddHours(24)
                });
                await sesion.SaveChangesAsync(http.RequestAborted);
            }
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, http.RequestAborted);
    }

    private static bool EsRutaQueCrea(string ruta) =>
        RutasQueCrean.Contains(ruta, StringComparer.OrdinalIgnoreCase) ||
        RegexRuta("^/api/v1/plataforma/tenants/[^/]+/empresas$").IsMatch(ruta);
}

/// <summary>
/// Quita de un cuerpo JSON los campos que llevan un secreto (PI-10): donde estén, a cualquier profundidad, los deja en <c>null</c> y agrega al objeto raíz
/// <c>secretosOmitidos</c> con sus nombres. Sin secretos que quitar el cuerpo queda igual.
/// </summary>
public static class RedactorDeSecretos
{
    /// <summary>
    /// El cuerpo sin secretos y los campos que se quitaron; <c>Cuerpo</c> es <c>null</c> si no se pudo redactar con certeza (no es JSON válido): quien
    /// llama no debe guardarlo.
    /// </summary>
    public static (string? Cuerpo, IReadOnlyList<string> Omitidos) Redactar(string cuerpo, IReadOnlyCollection<string> campos)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
            return (cuerpo, []);

        JsonNode? raiz;
        try
        {
            raiz = JsonNode.Parse(cuerpo);
        }
        catch (JsonException)
        {
            return (null, []);
        }

        var omitidos = new SortedSet<string>(StringComparer.Ordinal);
        Quitar(raiz, campos, omitidos);
        if (omitidos.Count == 0)
            return (cuerpo, []);

        if (raiz is JsonObject objeto)
        {
            var lista = new JsonArray();
            foreach (var campo in omitidos)
                lista.Add(campo);
            objeto["secretosOmitidos"] = lista;
        }

        return (raiz!.ToJsonString(), [.. omitidos]);
    }

    private static void Quitar(JsonNode? nodo, IReadOnlyCollection<string> campos, ISet<string> omitidos)
    {
        switch (nodo)
        {
            case JsonObject objeto:
                foreach (var propiedad in objeto.ToList())
                {
                    if (campos.Contains(propiedad.Key, StringComparer.OrdinalIgnoreCase) && propiedad.Value is not null)
                    {
                        objeto[propiedad.Key] = null;
                        omitidos.Add(propiedad.Key);
                    }
                    else
                        Quitar(propiedad.Value, campos, omitidos);
                }

                break;
            case JsonArray arreglo:
                foreach (var elemento in arreglo)
                    Quitar(elemento, campos, omitidos);
                break;
        }
    }
}
