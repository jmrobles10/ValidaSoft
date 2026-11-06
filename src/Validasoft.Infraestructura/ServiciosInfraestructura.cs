using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Persistencia;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Infraestructura;

public static class ServiciosInfraestructura
{
    /// <summary>Esquema por defecto: elige API key (<c>X-Api-Key</c>) o sesión de Validasoft (<c>Authorization: Bearer</c>).</summary>
    public const string EsquemaPorDefecto = "ApiKeyOBearer";

    public static IServiceCollection AgregarValidasoft(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddMemoryCache();
        // Registro por tipo (sin fábricas lambda): Wolverine genera el código de los endpoints y no admite service location.
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IContextoSolicitud, ContextoDesdeHttp>();
        servicios.AddSingleton<IGeneradorDeApiKeys, GeneradorDeApiKeys>();
        servicios.AddSingleton<IEmisorDeSesiones, EmisorDeSesiones>();
        servicios.AddScoped<RegistroDeUsuariosDelegados>();
        servicios.AddScoped<VerificadorDeIdentidadDelegada>();
        servicios.AgregarPersistencia(configuracion);

        servicios.AddAuthentication(EsquemaPorDefecto)
            .AddPolicyScheme(EsquemaPorDefecto, "API key o sesión de Validasoft", opciones =>
                opciones.ForwardDefaultSelector = http =>
                    !http.Request.Headers.ContainsKey(AutenticacionPorApiKey.Cabecera) &&
                    http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                        ? JwtBearerDefaults.AuthenticationScheme
                        : AutenticacionPorApiKey.Esquema)
            .AddScheme<AuthenticationSchemeOptions, AutenticacionPorApiKey>(AutenticacionPorApiKey.Esquema, null)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opciones =>
            {
                // La clave se lee al primer uso del esquema: sin Validasoft:Sesiones:ClaveFirma solo falla el Bearer.
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = EmisorDeSesiones.ParametrosDeValidacion(configuracion);
            });

        servicios.AddAuthorization(opciones =>
        {
            // Todo exige credencial salvo lo marcado [AllowAnonymous] (salud, documentación en desarrollo).
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            // El scope se mira en el contexto de la petición, que ya suma los de roles de la empresa activa (X-Empresa).
            foreach (var permiso in Permisos.Todos)
                opciones.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
                    c.Resource is HttpContext http && ContextoDesdeHttp.De(http).Scopes.Contains(permiso)));
        });

        servicios.AddProblemDetails();
        servicios.AddExceptionHandler<ManejadorDeErrores>();
        return servicios;
    }
}
