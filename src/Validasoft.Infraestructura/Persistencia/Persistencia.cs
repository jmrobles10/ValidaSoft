using JasperFx;
using JasperFx.Events;
using JasperFx.Events.Projections;
using JasperFx.MultiTenancy;
using Marten;
using Marten.Schema;
using Marten.Schema.Indexing.Unique;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using JasperFx.Events.Daemon;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Integraciones;
using Validasoft.Infraestructura.Http;
using Validasoft.Infraestructura.Notificaciones;
using Validasoft.Infraestructura.Seguridad;
using Weasel.Core;
using Weasel.Postgresql.Tables;
using Wolverine;
using Wolverine.Marten;
using Wolverine.Marten.Publishing;
using Wolverine.Runtime;

namespace Validasoft.Infraestructura.Persistencia;

public static class ConfiguracionMarten
{
    /// <summary>
    /// Un solo <c>DocumentStore</c> con eventos <i>conjoined</i> (T-2, ADR-0004). Esquemas: <c>plataforma</c>
    /// (tenants y empresas, globales), <c>boveda</c> (terceros, globales), <c>identidad</c> (API keys, sistemas integrados
    /// y usuarios por tenant; idempotencia), <c>validacion</c> (todo lo demás, por tenant) y <c>eventos</c> (event store).
    /// </summary>
    public static IServiceCollection AgregarPersistencia(this IServiceCollection servicios, IConfiguration configuracion)
    {
        var cadena = configuracion.GetConnectionString("Validasoft")
                     ?? throw new InvalidOperationException("Falta ConnectionStrings:Validasoft.");
        var crearEsquema = configuracion.GetValue("Validasoft:Persistencia:CrearEsquemaAlIniciar", false);

        servicios.AddMarten(opts =>
            {
                opts.Connection(cadena);
                opts.DatabaseSchemaName = "validacion";
                opts.UseSystemTextJsonForSerialization(EnumStorage.AsString, Casing.CamelCase);
                opts.AutoCreateSchemaObjects = crearEsquema ? AutoCreate.CreateOrUpdate : AutoCreate.None;

                opts.Events.DatabaseSchemaName = "eventos";
                opts.Events.TenancyStyle = TenancyStyle.Conjoined;
                opts.Events.StreamIdentity = StreamIdentity.AsGuid;
                opts.Events.MetadataConfig.HeadersEnabled = true;
                opts.Events.MetadataConfig.CorrelationIdEnabled = true;
                opts.Events.MetadataConfig.CausationIdEnabled = true;

                // plataforma (global)
                opts.Schema.For<Tenant>().DatabaseSchemaName("plataforma")
                    .UniqueIndex(UniqueIndexType.Computed, "ux_tenant_slug", x => x.Slug);
                opts.Schema.For<Empresa>().DatabaseSchemaName("plataforma")
                    .UniqueIndex(UniqueIndexType.Computed, "ux_empresa_identificacion", x => x.TenantId, x => x.ClaveIdentificacion)
                    .UniqueIndex(UniqueIndexType.Computed, "ux_empresa_prefijo", x => x.TenantId, x => x.PrefijoFolio);

                // boveda (global)
                opts.Schema.For<Tercero>().DatabaseSchemaName("boveda")
                    .UniqueIndex(UniqueIndexType.Computed, "ux_tercero_identificacion", x => x.ClaveIdentificacion);

                // Documentos de la bóveda: streams Documento-{id} en el tenant técnico "boveda" (ambos ámbitos; el PorVinculo
                // guarda su tenant como dato). Clave natural única: (tercero, canónico) o (vínculo, tipo del tenant).
                opts.Schema.For<Documento>().DatabaseSchemaName("boveda").MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_documento_clave", TenancyScope.PerTenant, x => x.ClaveNatural)
                    .Index(x => x.TerceroId);
                // Cuentas bancarias del tercero (CB-1): en la bóveda, como sus documentos globales.
                opts.Schema.For<Validasoft.Dominio.Terceros.CuentaBancaria>().DatabaseSchemaName("boveda").MultiTenanted().Index(x => x.TerceroId);
                opts.Schema.For<CargaDeDocumento>().DatabaseSchemaName("boveda")
                    .Index(x => x.DocumentoId);
                // Avisos de vencimiento programados (TD-6): uno por tercero y fecha, en el tenant técnico "boveda" para
                // guardarse en la misma transacción que el evento de vigencia y el mensaje programado (outbox).
                opts.Schema.For<Aplicacion.Documentos.AvisoDeVencimiento>().DatabaseSchemaName("boveda").MultiTenanted()
                    .Index(x => x.TerceroId);

                // identidad
                opts.Schema.For<ApiKey>().DatabaseSchemaName("identidad").MultiTenanted().Index(x => x.Prefijo);
                opts.Schema.For<AccesoSoporte>().DatabaseSchemaName("identidad").MultiTenanted().Index(x => x.ExpiraEn);
                opts.Schema.For<RegistroIdempotencia>().DatabaseSchemaName("identidad");
                opts.Schema.For<SistemaIntegrado>().DatabaseSchemaName("identidad").MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_sistema_codigo", TenancyScope.PerTenant, x => x.Codigo);
                opts.Schema.For<Usuario>().DatabaseSchemaName("identidad").MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_usuario_correo", TenancyScope.PerTenant, x => x.Correo);
                opts.Schema.For<IndiceIdentidadExterna>().DatabaseSchemaName("identidad").MultiTenanted();
                // Enlaces de acceso del tercero: globales (el tercero no pertenece a un tenant). Concurrencia optimista para
                // que dos intentos de OTP simultáneos no pierdan la cuenta de fallos.
                opts.Schema.For<EnlaceDeAcceso>().DatabaseSchemaName("identidad").UseOptimisticConcurrency(true)
                    .Index(x => x.VinculoId)
                    .Index(x => x.Expira);
                // Sesiones de refresco del tercero (RE-3): globales; la rotación usa concurrencia optimista para que dos
                // renovaciones simultáneas con el mismo token no dejen dos tokens válidos.
                opts.Schema.For<SesionDeRefresco>().DatabaseSchemaName("identidad").UseOptimisticConcurrency(true)
                    .Index(x => x.TerceroId)
                    .Index(x => x.ExpiraEn);
                // Aserciones delegadas ya usadas (SE-4): la llave primaria {sistema}:{jti} rechaza la segunda; se borran al vencer.
                opts.Schema.For<AsercionUsada>().DatabaseSchemaName("identidad").Index(x => x.ExpiraEn);
                // Límite de enlaces pedidos con el correo (RE-2): un documento por correo (hash), con concurrencia optimista.
                opts.Schema.For<LimiteDeAccesoPorCorreo>().DatabaseSchemaName("identidad").UseOptimisticConcurrency(true);

                // Textos legales versionados (aviso de la plataforma y textos de cada empresa): globales e inmutables.
                opts.Schema.For<TextoLegal>().DatabaseSchemaName("plataforma")
                    .UniqueIndex(UniqueIndexType.Computed, "ux_texto_legal_version", x => x.Serie, x => x.Version);

                // validacion (por tenant)
                opts.Schema.For<TipoVinculo>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_tipo_vinculo_codigo", TenancyScope.PerTenant, x => x.Codigo);
                opts.Schema.For<RegistroDeCambioDeConfiguracion>().MultiTenanted().Index(x => x.Fecha);
                opts.Schema.For<Area>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_area_codigo", TenancyScope.PerTenant, x => x.EmpresaId, x => x.Codigo);
                opts.Schema.For<Vinculo>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_vinculo_natural", TenancyScope.PerTenant,
                        x => x.EmpresaId, x => x.TerceroId, x => x.TipoVinculoId)
                    .Index(x => x.EmpresaId)
                    .Index(x => x.CreadoEn);

                // Motivos propios del tenant (los de la plataforma viven en código) y flujos aprobatorios versionados.
                opts.Schema.For<Motivo>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_motivo_codigo", TenancyScope.PerTenant, x => x.Codigo);
                opts.Schema.For<Flujo>().MultiTenanted().UseOptimisticConcurrency(true)
                    .UniqueIndex(UniqueIndexType.Computed, "ux_flujo_codigo", TenancyScope.PerTenant, x => x.EmpresaId, x => x.Codigo)
                    .Index(x => x.EmpresaId);
                opts.Schema.For<FlujoPorDefecto>().MultiTenanted().Index(x => x.EmpresaId);

                // Tipos de documento del tenant (defaults del grupo); qué se pide a cada vínculo lo dicen los requisitos del flujo (RQ-3).
                opts.Schema.For<TipoDocumentoTenant>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_tipo_documento_codigo", TenancyScope.PerTenant, x => x.Codigo)
                    .Index(x => x.TipoCanonico);

                // Folios: contador por (empresa, tipo, año) con concurrencia optimista; el folio además es único por tenant.
                opts.Schema.For<ContadorDeFolio>().MultiTenanted().UseOptimisticConcurrency(true);
                opts.Schema.For<Expediente>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_expediente_folio", TenancyScope.PerTenant, x => x.Folio)
                    .Index(x => x.EmpresaId)
                    .Index(x => x.VinculoId)
                    .Index(x => x.AbiertoEn);
                opts.Schema.For<FilaBandeja>().MultiTenanted()
                    .Index(x => x.EmpresaId)
                    .Index(x => x.ExpedienteId)
                    .Index(x => x.AreaId);

                // Certificados (H5): uno por vínculo (id determinista); el folio raíz es único en el tenant y se busca entre
                // tenants en la verificación pública (no es único entre tenants: lo desambigua la huella, CE-8).
                // Último recordatorio de vencimiento por vínculo y documento (VE-3), aislado por empresa.
                opts.Schema.For<Validasoft.Aplicacion.Vinculos.RecordatorioDeVencimiento>().MultiTenanted().Index(x => x.EmpresaId);
                opts.Schema.For<Certificado>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_certificado_folio", TenancyScope.PerTenant, x => x.FolioRaiz)
                    .Index(x => x.EmpresaId)
                    .Index(x => x.VinculoId);
                // Secuencias de folio como streams que se bloquean al consumir (CE-3).
                opts.Schema.For<SecuenciaDeFolios>().MultiTenanted();

                // Proyecciones inline: Vinculo y Expediente son la lectura tras la escritura (VinculoResumen y
                // ExpedienteDetalle, docs/03 §14).
                // Eventos públicos (global, con su tenant), webhooks y notificaciones (NW-1…NW-11). El cursor es único por tenant.
                opts.Schema.For<EventoPublico>()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_evento_publico_cursor", x => x.TenantId, x => x.Cursor);
                // Concurrencia optimista: el manejador de entregas (fallos, deshabilitación) y la administración no se pisan.
                opts.Schema.For<SuscripcionWebhook>().MultiTenanted().UseOptimisticConcurrency(true);
                opts.Schema.For<EntregaWebhook>().MultiTenanted()
                    .Index(x => x.SuscripcionId)
                    .Index(x => x.Cursor);
                opts.Schema.For<Notificacion>().MultiTenanted()
                    .Index(x => x.UsuarioId)
                    .Index(x => x.Orden);
                opts.Schema.For<PreferenciasNotificacion>().MultiTenanted();

                opts.Projections.Snapshot<Vinculo>(SnapshotLifecycle.Inline);
                opts.Projections.Snapshot<Expediente>(SnapshotLifecycle.Inline);
                opts.Projections.Snapshot<Documento>(SnapshotLifecycle.Inline);
                opts.Projections.Snapshot<Certificado>(SnapshotLifecycle.Inline);
                opts.Projections.Snapshot<SecuenciaDeFolios>(SnapshotLifecycle.Inline);
            })
            .UseLightweightSessions()
            // Daemon asíncrono con un solo nodo líder (bloqueo consultivo de PostgreSQL): corre la suscripción de eventos
            // públicos (NW-3). Con varias réplicas, solo el líder procesa; si cae, otra toma el relevo.
            .AddAsyncDaemon(DaemonMode.HotCold)
            .IntegrateWithWolverine()
            .SubscribeToEventsWithServices<SuscripcionDeEventosPublicos>(ServiceLifetime.Singleton);

        servicios.AddScoped<ISesiones, Sesiones>();
        servicios.AddScoped<SesionDeEmpresa>();
        return servicios;
    }
}

/// <summary>
/// Abre sesiones con el tenant de la credencial y escribe actor, sistema y correlación en los metadatos de los eventos.
/// Con un <see cref="IMessageBus"/> la sesión sale de <see cref="OutboxedSessionFactory"/>: los mensajes publicados en ese
/// bus se guardan en la misma transacción (outbox de Marten) y se despachan después del commit.
/// </summary>
public sealed class Sesiones(IDocumentStore store, IContextoSolicitud contexto, OutboxedSessionFactory outbox) : ISesiones
{
    public IDocumentSession DelTenant() => ParaTenant(contexto.TenantRequerido());

    public IDocumentSession DelTenant(IMessageBus mensajes) => ParaTenant(contexto.TenantRequerido(), mensajes);

    public IDocumentSession ParaTenant(Guid tenantId) => ParaTenant(tenantId, null);

    public IDocumentSession ParaTenant(Guid tenantId, IMessageBus? mensajes) =>
        ConMetadatos(mensajes is null
            ? store.LightweightSession(tenantId.ToString("N"))
            : outbox.OpenSession(Contexto(mensajes), tenantId.ToString("N")));

    public IDocumentSession Global(IMessageBus? mensajes = null) =>
        ConMetadatos(mensajes is null ? store.LightweightSession() : outbox.OpenSession(Contexto(mensajes)));

    public IQuerySession ConsultaGlobal() => store.QuerySession();

    public IDocumentSession Boveda(IMessageBus? mensajes = null) =>
        ConMetadatos(mensajes is null
            ? store.LightweightSession(TenantsTecnicos.Boveda)
            : outbox.OpenSession(Contexto(mensajes), TenantsTecnicos.Boveda));

    public IQuerySession ConsultaBoveda() => store.QuerySession(TenantsTecnicos.Boveda);

    private IDocumentSession ConMetadatos(IDocumentSession sesion)
    {
        sesion.CorrelationId = contexto.CorrelationId;
        sesion.SetHeader("actorTipo", contexto.ActorTipo.ToString());
        sesion.SetHeader("actorId", contexto.ActorId);
        if (contexto.Sistema is not null)
            sesion.SetHeader("sistema", contexto.Sistema);
        return sesion;
    }

    /// <summary>En los endpoints de Wolverine el <see cref="IMessageBus"/> de la petición es su <see cref="MessageContext"/>.</summary>
    private static MessageContext Contexto(IMessageBus mensajes) =>
        mensajes as MessageContext
        ?? throw new InvalidOperationException("El outbox necesita el MessageContext de Wolverine de la petición.");
}
