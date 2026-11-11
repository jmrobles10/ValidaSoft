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
using Validasoft.Aplicacion.Ia;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Infraestructura.Almacenamiento;
using Validasoft.Infraestructura.Certificados;
using Validasoft.Infraestructura.Correo;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.IA;
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

    /// <summary>
    /// Cola local durable y <b>secuencial</b> del análisis con IA (ADR-0012): una versión a la vez, así dos mensajes de la
    /// misma versión (carga y aceptación) no llaman dos veces a OpenAI; el segundo encuentra el análisis y solo consume.
    /// </summary>
    public const string ColaIa = "ia";

    /// <summary>
    /// Cola local durable y <b>secuencial</b> del importador genérico (F4): una corrida a la vez, que se reanuda desde su cursor si
    /// el proceso cae.
    /// </summary>
    public const string ColaImportacion = "importacion";

    /// <summary>Cola local durable del job de plazos de cargue: barrido y revisión de cada expediente (LT-4).</summary>
    public const string ColaPlazos = "plazos";

    /// <summary>
    /// Cola local durable y <b>secuencial</b> de la invitación masiva (LT-8): una tanda a la vez, en orden; cada tanda encola la
    /// siguiente, así varios lotes avanzan intercalados y un lote no corre dos tandas a la vez.
    /// </summary>
    public const string ColaLotes = "lotes";

    public static IServiceCollection AgregarValidasoft(this IServiceCollection servicios, IConfiguration configuracion)
    {
        servicios.AddSingleton(TimeProvider.System);
        // Fechas de calendario del negocio (vencimientos, «hoy») en la zona configurada, no en UTC (FE-1).
        Dominio.Documentos.CalculadoraDeVigencia.ZonaDeNegocio = Dominio.Documentos.CalculadoraDeVigencia.Zona(configuracion["Validasoft:ZonaHoraria"] ?? "America/Bogota");
        servicios.AddMemoryCache();
        // Registro por tipo (sin fábricas lambda): Wolverine genera el código de los endpoints y no admite service location.
        servicios.AddHttpContextAccessor();
        servicios.AddScoped<IContextoSolicitud, ContextoDesdeHttp>();
        servicios.AddSingleton<IGeneradorDeApiKeys, GeneradorDeApiKeys>();
        servicios.AddSingleton<ICacheDeCredenciales, CacheDeCredenciales>();
        // Los adaptadores de origen (IAdaptadorDeOrigen) los registra quien compone la aplicación; sin ninguno el importador responde 404.
        servicios.AddSingleton<Aplicacion.Importacion.RegistroDeAdaptadores>();
        servicios.AddSingleton<IEmisorDeSesiones, EmisorDeSesiones>();
        servicios.AddSingleton<IEmisorDeSesionesTercero, EmisorDeSesionesTercero>();
        servicios.AddSingleton<IProtectorDeSecretos, ProtectorDeSecretos>();
        servicios.AddHostedService<VerificacionDeClaveMaestra>(); // sin clave maestra de secretos la API no arranca (PT-1)
        // El correo pasa primero por EnviadorDeCorreo, que retiene en el buzón del sandbox el de un tenant sandbox (SB-4); solo lo
        // que sale va al transporte SMTP.
        servicios.AddSingleton<IEntornos, Sandbox.Entornos>();
        servicios.AddSingleton<CacheDeEmpresas>(); // la empresa de X-Empresa, 30 s por nodo
        servicios.AddSingleton<Aplicacion.Integracion.IndiceDeVinculosConsultados>(); // consulta de certificación en un solo viaje (CN-1)
        servicios.AddSingleton<ITransporteDeCorreo, EnviadorSmtp>();
        servicios.AddSingleton<IEnviadorDeCorreo, Sandbox.EnviadorDeCorreo>();
        servicios.AddScoped<Aplicacion.Sandbox.ServicioDeSandbox>();
        servicios.AddSingleton<RedactorDeCorreos>();
        servicios.AddScoped<PlantillasDeCorreo>(); // plantillas de la empresa al enviar (PL-1)
        servicios.AddSingleton<CookieDeSesionTercero>();
        servicios.AddScoped<RegistroDeUsuariosDelegados>();
        servicios.AddScoped<VerificadorDeIdentidadDelegada>();
        servicios.AddScoped<IVerificadorDeOperadores, VerificadorDeOperadores>(); // operadores de Plataforma (PT-4)
        servicios.AddSingleton<RegistroDeAsercionesUsadas>();
        servicios.AddHostedService<LimpiezaDeAsercionesUsadas>();
        // Embebido desde otros dominios (SE-1…SE-3): orígenes de los sistemas, apps estáticas, CORS opcional y proxies.
        servicios.AddSingleton<OrigenesDeEmbebido>();
        servicios.AddHostedService<SembradoDeTextosLegales>();
        // Catálogos de la ficha maestra: semilla idempotente solo en Development (FM-22).
        servicios.AddHostedService<Aplicacion.Ficha.SembradoDeCatalogosDeDesarrollo>();
        // Bóveda de documentos: almacenamiento S3/MinIO y límites de carga (docs/04 §6).
        servicios.AddSingleton<ReglasDeCarga>();
        servicios.AddSingleton<FabricaClienteS3>();
        servicios.AddSingleton<IAlmacenDeArchivos, AlmacenS3>();
        // Eventos públicos, webhooks y notificaciones (NW-1…NW-13). Cada catálogo es un IMapeadorDeEventos.
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDelProcesoDeValidacion>();
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDeCertificados>(); // certificado.* (NW-13)
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDeLaFicha>(); // seccion.aceptada (FM-32)
        servicios.AddSingleton<IMapeadorDeEventos, MapeadorDeVerificaciones>(); // expediente.verificacion_* (VE-13)
        servicios.AddSingleton<IMapeadorDeEventos, Aplicacion.Invitaciones.MapeadorDeLotes>(); // invitaciones.lote_terminado (LT-9)
        servicios.AddSingleton<CatalogoDeEventos>();
        servicios.AddSingleton<PlanificadorDeNotificaciones>();
        servicios.AddScoped<PreferenciasDeCorreoDelTercero>(); // el tercero puede apagar cada correo (NT-1)
        // Recordatorio automático de observaciones pendientes (NT-3): barrido periódico (Validasoft:RecordatoriosDeObservaciones:Intervalo).
        servicios.AddHostedService<ProgramadorDeRecordatoriosDeObservaciones>();
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
        servicios.AddSingleton<Validasoft.Aplicacion.Informes.IGeneradorDePdfDeInformes, GeneradorPdfDeInformes>();
        servicios.AddSingleton<ArchivoDeCertificados>();
        servicios.AddHostedService<ProcesadorDeCertificados>();
        // Verificaciones externas (H7, VE-5…VE-16): listas restrictivas con Infolaft como principal entre los reales (apagados
        // por omisión; credenciales por empresa), simulado solo con PermitirSimulado, PDF de evidencia y procesador periódico.
        servicios.AddSingleton<OpcionesDeVerificaciones>();
        Aplicacion.Expedientes.ProcesoDeValidacion.DiasMonitoreoDeListas =
            Math.Clamp(configuracion.GetValue("Validasoft:Verificadores:ListasRestrictivas:DiasMonitoreo", 30), 0, 365);
        servicios.AddSingleton<ControlDeProveedores>();
        servicios.AddSingleton<IVerificadorDeListas, Verificaciones.VerificadorInfolaft>();
        servicios.AddSingleton<IVerificadorDeListas, Verificaciones.VerificadorStradata>();
        servicios.AddSingleton<IVerificadorDeListas, Verificaciones.VerificadorCompliance>();
        servicios.AddSingleton<IVerificadorDeListas, Verificaciones.VerificadorSimulado>();
        servicios.AddSingleton<IGeneradorDePdfDeEvidencia, Verificaciones.GeneradorPdfDeEvidencia>();
        servicios.AddSingleton<ConsultaDeListas>();
        servicios.AddSingleton<TareasDeVerificaciones>();
        servicios.AddHostedService<Verificaciones.ProcesadorDeVerificaciones>();
        servicios.AddHttpClient(Verificaciones.VerificadorInfolaft.Cliente);
        servicios.AddHttpClient(Verificaciones.VerificadorStradata.Cliente);
        servicios.AddHttpClient(Verificaciones.VerificadorCompliance.Cliente);
        // Plazos de cargue del expediente inicial (P-4-2, LT-4): barrido periódico (Validasoft:PlazosDeCargue:Intervalo).
        servicios.AddHostedService<ProgramadorDePlazosDeCargue>();
        // Análisis de documentos con OpenAI y cupo por empresa (ADR-0012): cada empresa trae su llave; aquí no hay secretos.
        servicios.AddSingleton<OpcionesDeIa>();
        servicios.AddScoped<CupoDeIa>();
        servicios.AddHttpClient(AnalizadorOpenAi.Cliente, (proveedor, cliente) =>
            cliente.Timeout = proveedor.GetRequiredService<OpcionesDeIa>().Timeout + TimeSpan.FromSeconds(5));
        // Validasoft:IA:Proveedor = Simulado: analizador determinista que lee marcas del propio archivo (desarrollo y e2e, F5; PT-6),
        // sin red. Por omisión, OpenAI con la llave de cada empresa; las llaves de prueba del sandbox van siempre al simulado (SB-4).
        servicios.AddSingleton<AnalizadorOpenAi>();
        servicios.AddSingleton<AnalizadorSimulado>();
        servicios.AddSingleton<IAnalizadorDeDocumentos, Sandbox.AnalizadorPorEntorno>();
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
        opciones.PublishMessage<Aplicacion.Ficha.CruzarFichaConDocumentos>().ToLocalQueue(ColaRequisitos).UseDurableInbox();
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
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeDesaprobacionPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.CorreoDeReintegroPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Notificaciones.EnviarEntregaWebhook>().ToLocalQueue(ColaWebhooks);
        opciones.LocalQueue(ColaWebhooks).Sequential().UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Ia.AnalizarVersionConIa>().ToLocalQueue(ColaIa);
        opciones.PublishMessage<Aplicacion.Ia.AnalizarIaDelVinculo>().ToLocalQueue(ColaIa);
        opciones.LocalQueue(ColaIa).Sequential().UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Invitaciones.ProcesarTandaDeLote>().ToLocalQueue(ColaLotes);
        opciones.LocalQueue(ColaLotes).Sequential().UseDurableInbox();
        opciones.PublishMessage<RevisarPlazosDeCargue>().ToLocalQueue(ColaPlazos).UseDurableInbox();
        opciones.PublishMessage<RevisarPlazoDeCargue>().ToLocalQueue(ColaPlazos).UseDurableInbox();
        opciones.PublishMessage<CorreoDePlazoDeCarguePendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<CorreoDeRecordatorioDeObservacionesPendiente>().ToLocalQueue(ColaCorreo).UseDurableInbox();
        opciones.PublishMessage<RevisarRecordatoriosDeObservaciones>().ToLocalQueue(ColaPlazos).UseDurableInbox();
        opciones.PublishMessage<RecordarObservacionesDelExpediente>().ToLocalQueue(ColaPlazos).UseDurableInbox();
        opciones.PublishMessage<Aplicacion.Importacion.EjecutarImportacion>().ToLocalQueue(ColaImportacion);
        opciones.LocalQueue(ColaImportacion).Sequential().UseDurableInbox();
        opciones.Policies.OnAnyException()
            .RetryWithCooldown(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30))
            .Then.MoveToErrorQueue();
        return opciones;
    }
}
