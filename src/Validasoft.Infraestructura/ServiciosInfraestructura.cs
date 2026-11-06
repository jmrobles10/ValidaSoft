using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Consentimientos;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Infraestructura.Correo;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Persistencia;
using Validasoft.Infraestructura.Seguridad;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Validasoft.Infraestructura;

public static class ServiciosInfraestructura
{
    /// <summary>
    /// Esquema por defecto. En <c>/public/v1</c> solo vale la sesión del tercero (<c>Tercero</c>); en el resto, API key
    /// (<c>X-Api-Key</c>) o sesión de Validasoft (<c>Authorization: Bearer</c>).
    /// </summary>
    public const string EsquemaPorDefecto = "ApiKeyOBearer";

    public const string RutaPublica = "/public/v1";

    /// <summary>Cola local durable de los correos del outbox (IN-2).</summary>
    public const string ColaCorreo = "correo";

    public static IServiceCollection AgregarValidasoft(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton(TimeProvider.System);
        servicios.AddMemoryCache();
        // Registro por tipo (sin fábricas lambda): Wolverine genera el código de los endpoints y no admite service location.
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IContextoSolicitud, ContextoDesdeHttp>();
        servicios.AddSingleton<IGeneradorDeApiKeys, GeneradorDeApiKeys>();
        servicios.AddSingleton<IEmisorDeSesiones, EmisorDeSesiones>();
        servicios.AddSingleton<IEmisorDeSesionesTercero, EmisorDeSesionesTercero>();
        servicios.AddSingleton<IProtectorDeSecretos, ProtectorDeSecretos>();
        servicios.AddSingleton<IEnviadorDeCorreo, EnviadorSmtp>();
        servicios.AddSingleton<RedactorDeCorreos>();
        servicios.AddScoped<RegistroDeUsuariosDelegados>();
        servicios.AddScoped<VerificadorDeIdentidadDelegada>();
        servicios.AddHostedService<SembradoDeTextosLegales>();
        servicios.AgregarPersistencia(configuracion);

        servicios.AddAuthentication(EsquemaPorDefecto)
            .AddPolicyScheme(EsquemaPorDefecto, "Sesión del tercero, API key o sesión de Validasoft", opciones =>
                opciones.ForwardDefaultSelector = http =>
                    http.Request.Path.StartsWithSegments(RutaPublica, StringComparison.OrdinalIgnoreCase)
                        ? EmisorDeSesionesTercero.Esquema
                        : !http.Request.Headers.ContainsKey(AutenticacionPorApiKey.Cabecera) &&
                          http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? JwtBearerDefaults.AuthenticationScheme
                            : AutenticacionPorApiKey.Esquema)
            .AddScheme<AuthenticationSchemeOptions, AutenticacionPorApiKey>(AutenticacionPorApiKey.Esquema, null)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, opciones =>
            {
                // La clave se lee al primer uso del esquema: sin Validasoft:Sesiones:ClaveFirma solo falla el Bearer.
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = EmisorDeSesiones.ParametrosDeValidacion(configuracion);
            })
            .AddJwtBearer(EmisorDeSesionesTercero.Esquema, opciones =>
            {
                opciones.MapInboundClaims = false;
                opciones.TokenValidationParameters = EmisorDeSesionesTercero.ParametrosDeValidacion(configuracion);
            });

        servicios.AddAuthorization(opciones =>
        {
            // Todo exige credencial salvo lo marcado [AllowAnonymous] (salud, documentación, canje y autorregistro).
            opciones.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            // El scope se mira en el contexto de la petición, que ya suma los de roles de la empresa activa (X-Empresa).
            foreach (var permiso in Permisos.Todos)
                opciones.AddPolicy(permiso, p => p.RequireAuthenticatedUser().RequireAssertion(c =>
                    c.Resource is HttpContext http && ContextoDesdeHttp.De(http).Scopes.Contains(permiso)));
            opciones.AddPolicy(ReclamosTercero.Politica, p => p.AddAuthenticationSchemes(EmisorDeSesionesTercero.Esquema)
                .RequireAuthenticatedUser()
                .RequireClaim(ReclamosTercero.Tipo, ReclamosTercero.ValorTipo));
        });

        servicios.AgregarLimiteDePeticionesPublicas(configuracion);
        servicios.AddProblemDetails();
        servicios.AddExceptionHandler<ManejadorDeErrores>();
        return servicios;
    }

    /// <summary>
    /// Mensajería de Wolverine: transacciones automáticas de Marten en los manejadores que usen sesión, y los correos por
    /// una cola local durable (sus sobres se guardan con el outbox en la misma transacción que el cambio que los origina).
    /// Un fallo de SMTP se reintenta con espera y luego va a la cola de errores.
    /// </summary>
    public static WolverineOptions ConfigurarMensajeria(this WolverineOptions opciones)
    {
        opciones.Discovery.IncludeAssembly(typeof(Permisos).Assembly);
        opciones.Policies.AutoApplyTransactions();
        opciones.PublishMessage<CorreoDeAccesoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<CorreoDeObservacionPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.Policies.OnAnyException()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
            .Then.MoveToErrorQueue();
        return opciones;
    }
}
