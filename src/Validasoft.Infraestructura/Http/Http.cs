using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    public async Task InvokeAsync(HttpContext http, IDocumentStore store)
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

            var cabeceraEmpresa = http.Request.Headers[CabeceraEmpresa].ToString();
            if (!string.IsNullOrWhiteSpace(cabeceraEmpresa))
            {
                var empresa = Guid.TryParse(cabeceraEmpresa, out var empresaId) && contexto.TenantId is not null
                    ? await CargarEmpresaAsync(store, empresaId, http.RequestAborted)
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

    private static async Task<Empresa?> CargarEmpresaAsync(IDocumentStore store, Guid id, CancellationToken ct)
    {
        await using var sesion = store.QuerySession();
        return await sesion.LoadAsync<Empresa>(id, ct);
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
public sealed class MiddlewareDeIdempotencia(RequestDelegate siguiente)
{
    public const string Cabecera = "Idempotency-Key";

    private static readonly string[] RutasQueCrean =
    [
        "/api/v1/plataforma/tenants", "/api/v1/empresas", "/api/v1/api-keys", "/api/v1/sistemas", "/api/v1/usuarios", "/api/v1/tipos-vinculo",
        "/api/v1/areas", "/api/v1/terceros", "/api/v1/vinculos"
    ];

    public async Task InvokeAsync(HttpContext http, IDocumentStore store, TimeProvider reloj)
    {
        var contexto = ContextoDesdeHttp.De(http);
        var ruta = http.Request.Path.Value?.TrimEnd('/') ?? string.Empty;
        var clave = http.Request.Headers[Cabecera].ToString();
        var esPost = HttpMethods.IsPost(http.Request.Method) && ruta.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase);

        if (!esPost || http.User.Identity?.IsAuthenticated != true)
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
        if (excepcion is not ReglaDeNegocioException)
            logger.LogError(excepcion, "Error no controlado en {Ruta} (traceId {TraceId})", http.Request.Path, http.TraceIdentifier);

        var (estado, codigo, detalle) = excepcion switch
        {
            ReglaDeNegocioException r => (r.Tipo switch
            {
                TipoFalla.Validacion => StatusCodes.Status422UnprocessableEntity,
                TipoFalla.NoEncontrado => StatusCodes.Status404NotFound,
                TipoFalla.Prohibido => StatusCodes.Status403Forbidden,
                TipoFalla.NoAutenticado => StatusCodes.Status401Unauthorized,
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
            Instance = http.Request.Path
        };
        problema.Extensions["codigo"] = codigo;
        problema.Extensions["traceId"] = http.TraceIdentifier;
        if (estado == StatusCodes.Status500InternalServerError && entorno.IsDevelopment())
            problema.Extensions["excepcion"] = excepcion.ToString();

        return await problemas.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = problema, Exception = excepcion });
    }
}
