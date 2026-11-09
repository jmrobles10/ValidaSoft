using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Infraestructura.Http;

/// <summary>
/// Lo que hace falta para embeber Validasoft desde otro dominio y desplegarla detrás de un proxy (SE-1…SE-6):
/// proxies confiables para <c>X-Forwarded-For</c>, CORS opcional con lista explícita, y las apps estáticas
/// (<c>apps/web</c> en <c>/app/</c>, <c>apps/public</c> en <c>/</c>) servidas por la API con su cabecera
/// <c>frame-ancestors</c>. Todo está apagado por defecto y se enciende por configuración.
/// </summary>
public static class PlataformaWeb
{
    public const string PoliticaCors = "validasoft-cors";

    /// <summary>Ruta de la app embebible (<c>apps/web</c>, rutas con <c>#</c>).</summary>
    public const string RutaAppWeb = "/app";

    /// <summary>Cabeceras que un navegador puede enviar con CORS. Nunca <c>X-Api-Key</c> ni <c>X-Usuario-Delegado</c> (docs/07 §4).</summary>
    public static readonly string[] CabecerasCors =
        ["Accept", "Accept-Language", "Authorization", "Content-Type", "If-Match", "Idempotency-Key", "X-Correlation-Id", "X-Empresa", "X-Validasoft-Cliente"];

    public static readonly string[] CabecerasExpuestas = ["ETag", "Idempotent-Replayed", "Location", "Retry-After", "X-Correlation-Id"];

    public static IServiceCollection AgregarPlataformaWeb(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var (proxies, redes) = ProxiesConfiables(configuracion);
        var saltos = Math.Max(1, configuracion.GetValue("Validasoft:Proxies:Saltos", 1));
        servicios.Configure<ForwardedHeadersOptions>(opciones =>
        {
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opciones.ForwardLimit = saltos;
            // Sin los valores por defecto (loopback): solo cuenta lo que diga la configuración.
            opciones.KnownProxies.Clear();
            opciones.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
                opciones.KnownProxies.Add(proxy);
            foreach (var red in redes)
                opciones.KnownIPNetworks.Add(red);
        });

        var origenesCors = OrigenesCors(configuracion);
        if (origenesCors.Count > 0)
        {
            var credenciales = configuracion.GetValue("Validasoft:Cors:PermitirCredenciales", false);
            servicios.AddCors(opciones => opciones.AddPolicy(PoliticaCors, politica =>
            {
                politica.WithOrigins([.. origenesCors])
                    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
                    .WithHeaders(CabecerasCors)
                    .WithExposedHeaders(CabecerasExpuestas)
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
                if (credenciales)
                    politica.AllowCredentials();
            }));
        }

        return servicios;
    }

    /// <summary>
    /// Primer middleware: con proxies configurados, <c>RemoteIpAddress</c> pasa a ser la IP real del cliente
    /// (<c>X-Forwarded-For</c>) solo si la petición llega desde uno de ellos. Sin configuración no se lee la cabecera.
    /// </summary>
    public static WebApplication UsarProxiesConfiables(this WebApplication app)
    {
        var (proxies, redes) = ProxiesConfiables(app.Configuration);
        if (proxies.Count + redes.Count > 0)
            app.UseForwardedHeaders();
        return app;
    }

    /// <summary>Apps estáticas (si están configuradas) y CORS (si hay orígenes); antes del límite y la autenticación.</summary>
    public static WebApplication UsarAppsYCors(this WebApplication app)
    {
        var apps = AppsEstaticas.Desde(app.Configuration, app.Environment);
        if (apps.Web is not null || apps.Publica is not null)
        {
            app.UseMiddleware<MiddlewareDeApps>(apps);
            if (apps.Web is not null)
                app.UseStaticFiles(new StaticFileOptions { FileProvider = apps.Web, RequestPath = RutaAppWeb, OnPrepareResponse = Cachear });
            if (apps.Publica is not null)
                app.UseStaticFiles(new StaticFileOptions { FileProvider = apps.Publica, OnPrepareResponse = Cachear });
        }

        if (OrigenesCors(app.Configuration).Count > 0)
            app.UseCors(PoliticaCors);
        return app;
    }

    /// <summary>Los archivos de <c>assets/</c> llevan hash en el nombre: se guardan un año.</summary>
    private static void Cachear(StaticFileResponseContext contexto)
    {
        contexto.Context.Response.Headers.XContentTypeOptions = "nosniff";
        if (contexto.Context.Request.Path.Value?.Contains("/assets/", StringComparison.Ordinal) == true)
            contexto.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
    }

    /// <summary><c>Validasoft:Cors:OrigenesPermitidos</c>: orígenes exactos (sin comodines), validados como los de los sistemas.</summary>
    public static IReadOnlyList<string> OrigenesCors(IConfiguration configuracion)
    {
        var permitirHttpLocal = configuracion.GetValue("Validasoft:Embebido:PermitirHttpLocal", false);
        var configurados = configuracion.GetSection("Validasoft:Cors:OrigenesPermitidos").Get<string[]>() ?? [];
        try
        {
            return [.. configurados.Where(o => !string.IsNullOrWhiteSpace(o)).Select(o => OrigenWeb.Normalizar(o, permitirHttpLocal)).Distinct()];
        }
        catch (ReglaDeNegocioException e)
        {
            throw new InvalidOperationException($"Validasoft:Cors:OrigenesPermitidos: {e.Message}", e);
        }
    }

    /// <summary><c>Validasoft:Proxies:Confiables</c>: IP (<c>10.0.0.5</c>) o red CIDR (<c>10.0.0.0/24</c>) de cada proxy.</summary>
    public static (IReadOnlyList<IPAddress> Proxies, IReadOnlyList<System.Net.IPNetwork> Redes) ProxiesConfiables(IConfiguration configuracion)
    {
        var proxies = new List<IPAddress>();
        var redes = new List<System.Net.IPNetwork>();
        foreach (var entrada in configuracion.GetSection("Validasoft:Proxies:Confiables").Get<string[]>() ?? [])
        {
            var texto = entrada.Trim();
            if (texto.Length == 0)
                continue;
            if (texto.Contains('/') && System.Net.IPNetwork.TryParse(texto, out var red))
                redes.Add(red);
            else if (IPAddress.TryParse(texto, out var ip))
                proxies.Add(ip);
            else
                throw new InvalidOperationException($"Validasoft:Proxies:Confiables: «{texto}» no es una IP ni una red CIDR.");
        }

        return (proxies, redes);
    }
}

/// <summary>
/// <c>Validasoft:Apps:Web</c> y <c>Validasoft:Apps:Publica</c>: carpetas con el <c>dist/</c> de cada app (relativas a
/// la raíz de contenido). Vacías = la API no sirve esa app. Si están configuradas y no existen, la API no arranca.
/// </summary>
public sealed record AppsEstaticas(IFileProvider? Web, IFileProvider? Publica)
{
    public static AppsEstaticas Desde(IConfiguration configuracion, IHostEnvironment entorno) =>
        new(Proveedor(configuracion["Validasoft:Apps:Web"], entorno), Proveedor(configuracion["Validasoft:Apps:Publica"], entorno));

    private static PhysicalFileProvider? Proveedor(string? ruta, IHostEnvironment entorno)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return null;
        var completa = Path.GetFullPath(ruta, entorno.ContentRootPath);
        return File.Exists(Path.Combine(completa, "index.html"))
            ? new PhysicalFileProvider(completa)
            : throw new InvalidOperationException($"Validasoft:Apps: no existe {Path.Combine(completa, "index.html")}.");
    }
}

/// <summary>
/// Entrega el <c>index.html</c> de cada app para las rutas que no son archivos (fallback de una SPA) con su
/// <c>Content-Security-Policy</c>: la app embebible (<c>/app/</c>) admite como <c>frame-ancestors</c> a Validasoft y a
/// los orígenes de los sistemas activos (SE-1); la del tercero, solo a Validasoft. Los demás archivos los sirve
/// <c>UseStaticFiles</c>.
/// </summary>
public sealed class MiddlewareDeApps(RequestDelegate siguiente, AppsEstaticas apps)
{
    /// <summary>Rutas de la API que la app del tercero (en la raíz) nunca atiende.</summary>
    private static readonly string[] RutasReservadas = ["/api", "/public", "/health", "/openapi", "/docs", PlataformaWeb.RutaAppWeb];

    public async Task InvokeAsync(HttpContext http, OrigenesDeEmbebido origenes)
    {
        var peticion = http.Request;
        if (!HttpMethods.IsGet(peticion.Method) && !HttpMethods.IsHead(peticion.Method))
        {
            await siguiente(http);
            return;
        }

        if (apps.Web is not null && peticion.Path.StartsWithSegments(PlataformaWeb.RutaAppWeb, out var resto))
        {
            if (!resto.HasValue)
            {
                // Sin la barra final las rutas relativas de los assets no resolverían dentro de /app/.
                http.Response.Redirect(PlataformaWeb.RutaAppWeb + "/" + peticion.QueryString, permanent: true);
                return;
            }

            if (EsDocumento(resto))
            {
                var ancestros = await origenes.ObtenerAsync(http.RequestAborted);
                await EntregarIndiceAsync(http, apps.Web, Politica(ancestros));
                return;
            }
        }
        else if (apps.Publica is not null && EsDocumento(peticion.Path) &&
                 !RutasReservadas.Any(r => peticion.Path.StartsWithSegments(r, StringComparison.OrdinalIgnoreCase)))
        {
            await EntregarIndiceAsync(http, apps.Publica, Politica([]));
            return;
        }

        await siguiente(http);
    }

    /// <summary><c>frame-ancestors 'self' {orígenes}</c> más dos directivas sin riesgo para las apps.</summary>
    public static string Politica(IEnumerable<string> ancestros) =>
        $"frame-ancestors 'self'{string.Concat(ancestros.Select(o => " " + o))}; base-uri 'self'; object-src 'none'";

    /// <summary>Ruta de una página de la SPA: sin extensión en el último segmento, o un <c>.html</c>.</summary>
    private static bool EsDocumento(PathString ruta)
    {
        var valor = ruta.Value ?? "/";
        var ultimo = valor[(valor.LastIndexOf('/') + 1)..];
        return !ultimo.Contains('.') || ultimo.EndsWith(".html", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EntregarIndiceAsync(HttpContext http, IFileProvider proveedor, string politica)
    {
        var indice = proveedor.GetFileInfo("index.html");
        var respuesta = http.Response;
        respuesta.StatusCode = StatusCodes.Status200OK;
        respuesta.ContentType = "text/html; charset=utf-8";
        respuesta.ContentLength = indice.Length;
        respuesta.Headers.CacheControl = "no-cache";
        respuesta.Headers.ContentSecurityPolicy = politica;
        respuesta.Headers.XContentTypeOptions = "nosniff";
        if (!HttpMethods.IsHead(http.Request.Method))
            await respuesta.SendFileAsync(indice, http.RequestAborted);
    }
}
