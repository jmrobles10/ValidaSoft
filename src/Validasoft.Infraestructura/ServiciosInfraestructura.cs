using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Consentimientos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Infraestructura.Almacenamiento;
using Validasoft.Infraestructura.Certificados;
using Validasoft.Infraestructura.Correo;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Notificaciones;
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

    /// <summary>Cola local durable que adjunta a los expedientes las versiones que comparte la bóveda (RQ-2).</summary>
    public const string ColaRequisitos = "requisitos";

    /// <summary>Cola local durable de vigencias: cálculo, avisos y vencimientos programados de las versiones (TD-6).</summary>
    public const string ColaVigencias = "vigencias";

    /// <summary>
    /// Cola local durable y <b>secuencial</b> de las entregas de webhooks (NW-5): un intento a la vez, en el orden en que se
    /// encolaron, así un receptor sano recibe los eventos en orden. Los reintentos van programados y no bloquean la cola.
    /// </summary>
    public const string ColaWebhooks = "webhooks";

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
        servicios.AddScoped<PlantillasDeCorreo>(); // plantillas de la empresa al enviar (PL-1)
        servicios.AddSingleton<CookieDeSesionTercero>();
        servicios.AddScoped<RegistroDeUsuariosDelegados>();
        servicios.AddScoped<VerificadorDeIdentidadDelegada>();
        servicios.AddSingleton<RegistroDeAsercionesUsadas>();
        servicios.AddHostedService<LimpiezaDeAsercionesUsadas>();
        // Embebido desde otros dominios (SE-1…SE-3): orígenes de los sistemas, apps estáticas, CORS opcional y proxies.
        servicios.AddSingleton<OrigenesDeEmbebido>();
        servicios.AddHostedService<SembradoDeTextosLegales>();
        // Bóveda de documentos: almacenamiento S3/MinIO y límites de carga (docs/04 §6).
        servicios.AddSingleton<ReglasDeCarga>();
        servicios.AddSingleton<FabricaClienteS3>();
        servicios.AddSingleton<IAlmacenDeArchivos, AlmacenS3>();
        // Eventos públicos, webhooks y notificaciones (NW-1…NW-13). Cada catálogo es un IMapeadorDeEventos.
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDelProcesoDeValidacion>();
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDeCertificados>(); // certificado.* (NW-13)
        servicios.AddSingleton<CatalogoDeEventos>();
        servicios.AddSingleton<PlanificadorDeNotificaciones>();
        servicios.AddSingleton<ProcesadorDeEventos>();
        servicios.AddSingleton<OpcionesDeWebhooks>();
        servicios.AddSingleton<IEnviadorDeWebhooks, EnviadorDeWebhooksHttp>();
        // El manejador primario se arma en la configuración del HttpClientFactory (no es un servicio que Wolverine resuelva).
        servicios.AddHttpClient(EnviadorDeWebhooksHttp.Cliente)
            .ConfigurePrimaryHttpMessageHandler(proveedor => EnviadorDeWebhooksHttp.CrearManejador(proveedor.GetRequiredService<OpcionesDeWebhooks>()));
        servicios.AddHostedService<InicializadorDeAlmacen>();
        // Certificados verificables (H5): sello Ed25519, emisión en la transacción de la aprobación, PDF y vencimientos después.
        servicios.AddSingleton<ClavesDeCertificados>();
        servicios.AddHostedService<VerificacionDeClavesDeCertificados>(); // sin clave de sellado la API no arranca (CE-11)
        servicios.AddSingleton<EmisorDeCertificados>();
        servicios.AddSingleton<IGeneradorDePdfDeCertificados, GeneradorPdfDeCertificados>();
        servicios.AddSingleton<ArchivoDeCertificados>();
        servicios.AddHostedService<ProcesadorDeCertificados>();
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
    /// Mensajería de Wolverine: transacciones automáticas de Marten en los manejadores que usen sesión, y los correos y la
    /// sincronización de requisitos por colas locales durables (sus sobres se guardan con el outbox en la misma transacción
    /// que el cambio que los origina). Un fallo se reintenta con espera y luego va a la cola de errores.
    /// </summary>
    public static WolverineOptions ConfigurarMensajeria(this WolverineOptions opciones)
    {
        opciones.Discovery.IncludeAssembly(typeof(Permisos).Assembly);
        opciones.Policies.AutoApplyTransactions();
        opciones.PublishMessage<CorreoDeAccesoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<CorreoDeObservacionPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<CorreoDeResultadoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<SincronizarRequisitosDelVinculo>().ToLocalQueue(ColaRequisitos).UseDurableInbox();
        opciones.PublishMessage<VencerPlazoDeRevalidacion>().ToLocalQueue(ColaRequisitos).UseDurableInbox();
        opciones.PublishMessage<RenovarPorVencimiento>().ToLocalQueue(ColaRequisitos).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.CorreoDeVencimientoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.CalcularVigenciasDelDocumento>().ToLocalQueue(ColaVigencias).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.CalcularVigenciasDelVinculo>().ToLocalQueue(ColaVigencias).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.RecalcularVigenciasDelTipo>().ToLocalQueue(ColaVigencias).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.EnviarAvisosDeVencimiento>().ToLocalQueue(ColaVigencias).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Documentos.VencerVersionDeDocumento>().ToLocalQueue(ColaVigencias).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeNotificacionPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeConstanciaPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeVencidoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeCertificadoPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.EnviarEntregaWebhook>().ToLocalQueue(ColaWebhooks);
        opciones.LocalQueue(ColaWebhooks).Sequential().UseDurableInbox();
        opciones.Policies.OnAnyException()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
            .Then.MoveToErrorQueue();
        return opciones;
    }
}
