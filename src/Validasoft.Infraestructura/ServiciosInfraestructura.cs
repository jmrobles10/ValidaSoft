using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Persistencia;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Infraestructura;

public static class ServiciosInfraestructura
{
    public static IServiceCollection AgregarValidasoft(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddMemoryCache();
        // Registro por tipo (sin fábricas lambda): Wolverine genera el código de los endpoints y no admite service location.
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IContextoSolicitud, ContextoDesdeHttp>();
        servicios.AddSingleton<IGeneradorDeApiKeys, GeneradorDeApiKeys>();
        servicios.AgregarPersistencia(configuracion);

        servicios.AddAuthentication(AutenticacionPorApiKey.Esquema)
            .AddScheme<AuthenticationSchemeOptions, AutenticacionPorApiKey>(AutenticacionPorApiKey.Esquema, null);

        servicios.AddAuthorization(opciones =>
        {
            // Todo exige credencial salvo lo marcado [AllowAnonymous] (salud, documentación en desarrollo).
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            foreach (var permiso in Permisos.Todos)
                opciones.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireClaim(Reclamos.Scope, permiso));
        });

        servicios.AddProblemDetails();
        servicios.AddExceptionHandler<ManejadorDeErrores>();
        return servicios;
    }
}
