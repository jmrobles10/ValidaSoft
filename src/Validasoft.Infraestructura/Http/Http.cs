using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Plataforma;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Infraestructura.Http;

/// <summary>
/// Llena el <see cref="ContextoSolicitud"/>: tenant, actor, sistema, scopes y empresas desde la credencial (API key o
/// sesión Bearer); empresa activa desde <c>X-Empresa</c> (validada contra el tenant y el alcance de la credencial; si
/// no, 404 — T-4); correlación desde <c>X-Correlation-Id</c> (se devuelve en la respuesta). Los scopes de roles por
/// empresa (<see cref="Reclamos.ScopeEmpresa"/>) solo se suman cuando esa es la empresa activa.
/// </summary>
public sealed class MiddlewareDeContexto(RequestDelegate siguiente)
{
    public const string CabeceraEmpresa = "X-Empresa";
    public const string CabeceraCorrelacion = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext http, IDocumentStore store, IEntornos entornos, CacheDeEmpresas empresas)
    {
        var contexto = ContextoDesdeHttp.De(http);
        contexto.CorrelationId = Correlacion(http);
        http.Response.Headers[CabeceraCorrelacion] = contexto.CorrelationId;

        var usuario = http.User;
        if (usuario.Identity?.IsAuthenticated == true)
        {
            contexto.TenantId = Guid.TryParse(usuario.FindFirstValue(Reclamos.Tenant), out var tenant) ? tenant : null;
            contexto.ActorTipo = Enum.TryParse<ActorTipo>(usuario.FindFirstValue(Reclamos.ActorTipo), out var actor) ? actor : ActorTipo.Sistema;
            contexto.ActorId = usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario.FindFirstValue(Reclamos.Sujeto) ?? "desconocido";
            contexto.Sistema = usuario.FindFirstValue(Reclamos.Sistema);
            contexto.Empresas = usuario.FindAll(Reclamos.Empresa).Select(c => c.Value).ToHashSet();

            // Una credencial de prueba opera en el sandbox del grupo (SB-1): toda respuesta lo dice, para que nadie lo confunda con producción.
            if (contexto.TenantId is { } tenantDeLaCredencial && await entornos.EsSandboxAsync(tenantDeLaCredencial, http.RequestAborted))
            {
                contexto.EsSandbox = true;
                http.Response.Headers[AislamientoDeSandbox.CabeceraEntorno] = AislamientoDeSandbox.Entorno;
            }

            var cabeceraEmpresa = http.Request.Headers[CabeceraEmpresa].ToString();
            if (!string.IsNullOrWhiteSpace(cabeceraEmpresa))
            {
                var empresa = Guid.TryParse(cabeceraEmpresa, out var empresaId) && contexto.TenantId is not null
                    ? await empresas.ObtenerAsync(store, empresaId, http.RequestAborted)
                    : null;

                var permitida = empresa is not null && empresa.Activa && empresa.TenantId == contexto.TenantId &&
                                (contexto.Empresas.Contains("*") || contexto.Empresas.Contains(empresa.Id.ToString()));
                if (!permitida)
                    throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "No existe esa empresa o la credencial no tiene acceso a ella.");

                contexto.EmpresaId = empresa!.Id;
            }

            var scopes = usuario.FindAll(Reclamos.Scope).Select(c => c.Value).ToHashSet();
            if (contexto.EmpresaId is { } activa)
            {
                var sufijo = "|" + activa;
                scopes.UnionWith(usuario.FindAll(Reclamos.ScopeEmpresa).Where(c => c.Value.EndsWith(sufijo, StringComparison.Ordinal))
                    .Select(c => c.Value[..^sufijo.Length]));
            }

            contexto.Scopes = scopes;
        }

        await siguiente(http);
    }

    /// <summary><c>X-Correlation-Id</c> del cliente (hasta 100 caracteres) o el identificador de la petición.</summary>
    public static string Correlacion(HttpContext http)
    {
        var correlacion = http.Request.Headers[CabeceraCorrelacion].ToString();
        return string.IsNullOrWhiteSpace(correlacion) || correlacion.Length > 100 ? http.TraceIdentifier : correlacion;
    }

}

/// <summary>Clave de idempotencia ya usada (docs/05 §3.1). Global; vence a las 24 horas.</summary>
public sealed class RegistroIdempotencia
{
    public string Id { get; set; } = string.Empty;

    public string HashPeticion { get; set; } = string.Empty;

    public int Codigo { get; set; }

    public string? TipoContenido { get; set; }

    public string Cuerpo { get; set; } = string.Empty;

    public string? Location { get; set; }

    public DateTimeOffset ExpiraEn { get; set; }
}

/// <summary>
/// <c>Idempotency-Key</c>: obligatorio en los POST que crean recursos; misma clave y mismo cuerpo repiten la
/// respuesta (<c>Idempotent-Replayed: true</c>); misma clave con otro cuerpo responde 422.
/// </summary>
public sealed partial class MiddlewareDeIdempotencia(RequestDelegate siguiente)
{
    /// <summary>
    /// Respuestas con un secreto que no se guardan en el registro: el secreto de un webhook (NW-4), la clave nueva de una API
    /// key rotada (CN-3), las API keys del alta de un grupo y la sesión de un operador de Plataforma (PT-5, PT-4) y las sesiones embebidas (IO-5: tercero y usuario). Repetir el
    /// alta responde 409 <c>slug-duplicado</c>; rotar dos veces, 409 <c>api-key-ya-rotada</c>.
    /// </summary>
    [GeneratedRegex("^/api/v1/(webhooks(/[^/]+/rotar-secreto)?|api-keys/[^/]+/rotar|plataforma/tenants|plataforma/sesiones|sesiones-tercero|sesiones-usuario)$", RegexOptions.IgnoreCase)]
    private static partial Regex RutaConSecreto();

    public const string Cabecera = "Idempotency-Key";

    private static readonly string[] RutasQueCrean =
    [
        "/api/v1/plataforma/tenants", "/api/v1/empresas", "/api/v1/api-keys", "/api/v1/sistemas", "/api/v1/usuarios", "/api/v1/tipos-vinculo",
        "/api/v1/areas", "/api/v1/terceros", "/api/v1/vinculos", "/api/v1/motivos", "/api/v1/flujos", "/api/v1/tipos-documento"
    ];

    public async Task InvokeAsync(HttpContext http, IDocumentStore store, TimeProvider reloj)
    {
        var contexto = ContextoDesdeHttp.De(http);
        var ruta = http.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var clave = http.Request.Headers[Cabecera].ToString();
        var esPost = HttpMethods.IsPost(http.Request.Method) && ruta.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase);

        // Las respuestas que muestran un secreto (webhook, clave nueva de una API key rotada, llaves del alta de un grupo, sesión
        // de Plataforma) no se guardan: el registro de idempotencia lo dejaría en claro 24 h (NW-4, CN-3, PT-5). Esas rutas no
        // repiten respuesta.
        if (!esPost || http.User.Identity?.IsAuthenticated != true || RutaConSecreto().IsMatch(ruta))
        {
            await siguiente(http);
            return;
        }

        if (string.IsNullOrWhiteSpace(clave))
        {
            if (RutasQueCrean.Contains(ruta, StringComparer.OrdinalIgnoreCase))
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
            await using var sesion = store.LightweightSession();
            sesion.Store(new RegistroIdempotencia
            {
                Id = id,
                HashPeticion = hash,
                Codigo = http.Response.StatusCode,
                TipoContenido = http.Response.ContentType,
                Cuerpo = respuesta,
                Location = http.Response.Headers.Location.ToString() is { Length: > 0 } location ? location : null,
                ExpiraEn = reloj.GetUtcNow().AddHours(24)
            });
            await sesion.SaveChangesAsync(http.RequestAborted);
        }

        buffer.Position = 0;
        await buffer.CopyToAsync(original, http.RequestAborted);
    }
}

/// <summary>Traduce excepciones a ProblemDetails RFC 9457 con <c>codigo</c> estable y <c>traceId</c> (docs/05 §3.3).</summary>
public sealed class ManejadorDeErrores(IProblemDetailsService problemas, ILogger<ManejadorDeErrores> logger, IHostEnvironment entorno)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception excepcion, CancellationToken ct)
    {
        // Una regla de negocio envuelta por una librería (p. ej. Marten al evaluar una expresión LINQ) responde como regla.
        excepcion = ReglaEnvuelta(excepcion) ?? excepcion;
        var ruta = RutaSegura(http.Request.Path);
        if (excepcion is not ReglaDeNegocioException)
            logger.LogError(excepcion, "Error no controlado en {Ruta} (traceId {TraceId})", ruta, http.TraceIdentifier);

        var (estado, codigo, detalle) = excepcion switch
        {
            ReglaDeNegocioException r => (r.Tipo switch
            {
                TipoFalla.Validacion => StatusCodes.Status422UnprocessableEntity,
                TipoFalla.NoEncontrado => StatusCodes.Status404NotFound,
                TipoFalla.Prohibido => StatusCodes.Status403Forbidden,
                TipoFalla.NoAutenticado => StatusCodes.Status401Unauthorized,
                TipoFalla.Expirado => StatusCodes.Status410Gone,
                TipoFalla.DemasiadosIntentos => StatusCodes.Status429TooManyRequests,
                TipoFalla.DemasiadoGrande => StatusCodes.Status413PayloadTooLarge,
                TipoFalla.TipoNoSoportado => StatusCodes.Status415UnsupportedMediaType,
                TipoFalla.NoDisponible => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status409Conflict
            }, r.Codigo, r.Message),
            ConcurrencyException or EventStreamUnexpectedMaxEventIdException =>
                (StatusCodes.Status412PreconditionFailed, "version-desactualizada", "El recurso cambió desde que lo leíste; vuelve a cargarlo."),
            DocumentAlreadyExistsException => (StatusCodes.Status409Conflict, "duplicado", "El recurso ya existe."),
            MartenCommandException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "duplicado", "Ya existe un registro con esos datos únicos."),
            BadHttpRequestException b => (StatusCodes.Status400BadRequest, "peticion-invalida", b.Message),
            _ => (StatusCodes.Status500InternalServerError, "error-interno", "Ocurrió un error inesperado. Usa el traceId para reportarlo.")
        };

        http.Response.StatusCode = estado;
        var problema = new ProblemDetails
        {
            Status = estado,
            Type = $"https://docs.validasoft.co/errores/{codigo}",
            Title = codigo,
            Detail = detalle,
            Instance = ruta
        };
        problema.Extensions["codigo"] = codigo;
        problema.Extensions["traceId"] = http.TraceIdentifier;
        if (excepcion is ReglaDeNegocioException { Datos: { } datos })
        {
            foreach (var (clave, valor) in datos)
                problema.Extensions[clave] = valor;
        }
        if (estado == StatusCodes.Status500InternalServerError && entorno.IsDevelopment())
            problema.Extensions["excepcion"] = excepcion.ToString();

        return await problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema, Exception = excepcion });
    }

    private static ReglaDeNegocioException? ReglaEnvuelta(Exception excepcion)
    {
        for (var actual = excepcion.InnerException; actual is not null; actual = actual.InnerException)
            if (actual is ReglaDeNegocioException regla)
                return regla;
        return null;
    }

    /// <summary>Ruta sin secretos: el token de <c>/public/v1/enlaces/{token}/…</c> nunca llega a logs ni a respuestas de error.</summary>
    public static string RutaSegura(PathString ruta) =>
        Regex.Replace(ruta.Value ?? string.Empty, "(?<=/enlaces/)[^/]+", "***", RegexOptions.IgnoreCase);
}

/// <summary>
/// Límite de peticiones por IP en <c>/public/v1</c> (ventana fija; <c>Validasoft:LimitePublico:{Peticiones, VentanaSegundos}</c>,
/// por defecto 60 por minuto). La evaluación de la ficha (<c>…/evaluacion</c>, FM-46) va en otro cupo, cuatro veces mayor: el
/// wizard la pide mientras el tercero escribe y no debe agotar el de las demás. <c>/api/v1</c> aún no tiene límite (llegará por credencial). Excedido ⇒ 429 ProblemDetails
/// <c>demasiadas-peticiones</c> con <c>Retry-After</c>.
/// </summary>
public static class LimiteDePeticionesPublicas
{
    /// <summary>Rutas del ingreso directo: anónimas, con su propio límite por IP.</summary>
    public const string RutaIngreso = "/api/v1/ingreso";

    public static IServiceCollection AgregarLimiteDePeticionesPublicas(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var peticiones = Math.Max(1, configuracion.GetValue("Validasoft:LimitePublico:Peticiones", 60));
        var ventana = TimeSpan.FromSeconds(Math.Max(1, configuracion.GetValue("Validasoft:LimitePublico:VentanaSegundos", 60)));
        // El ingreso directo (/api/v1/ingreso, ING-1) tiene su propio cupo por IP: 30 peticiones por minuto por omisión.
        var peticionesIngreso = Math.Max(1, configuracion.GetValue("Validasoft:LimiteIngreso:Peticiones", 30));
        var ventanaIngreso = TimeSpan.FromSeconds(Math.Max(1, configuracion.GetValue("Validasoft:LimiteIngreso:VentanaSegundos", 60)));

        return servicios.AddRateLimiter(opciones =>
        {
            opciones.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                if (http.Request.Path.StartsWithSegments(RutaIngreso, StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetFixedWindowLimiter("ingreso:" + (http.Connection.RemoteIpAddress?.ToString() ?? "desconocida"),
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = peticionesIngreso, Window = ventanaIngreso, QueueLimit = 0 });
                if (!http.Request.Path.StartsWithSegments(ServiciosInfraestructura.RutaPublica, StringComparison.OrdinalIgnoreCase))
                    return RateLimitPartition.GetNoLimiter("sin-limite");
                var ip = http.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
                var evaluacion = http.Request.Path.Value?.EndsWith("/evaluacion", StringComparison.OrdinalIgnoreCase) == true;
                return RateLimitPartition.GetFixedWindowLimiter((evaluacion ? "evaluacion:" : "ip:") + ip,
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = evaluacion ? peticiones * 4 : peticiones, Window = ventana, QueueLimit = 0 });
            });

            opciones.OnRejected = async (contexto, ct) =>
            {
                var http = contexto.HttpContext;
                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.Headers.RetryAfter = ((int)ventana.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var problema = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Type = "https://docs.validasoft.co/errores/demasiadas-peticiones",
                    Title = "demasiadas-peticiones",
                    Detail = "Hiciste demasiadas peticiones; espera un momento e inténtalo de nuevo.",
                    Instance = ManejadorDeErrores.RutaSegura(http.Request.Path)
                };
                problema.Extensions["codigo"] = "demasiadas-peticiones";
                problema.Extensions["traceId"] = http.TraceIdentifier;
                await http.RequestServices.GetRequiredService<IProblemDetailsService>()
                    .WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema });
            };
        });
    }
}
