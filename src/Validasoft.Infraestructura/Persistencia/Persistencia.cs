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
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Validasoft.Infraestructura.Http;
using Weasel.Core;
using Weasel.Postgresql.Tables;
using Wolverine.Marten;

namespace Validasoft.Infraestructura.Persistencia;

public static class ConfiguracionMarten
{
    /// <summary>
    /// Un solo <c>DocumentStore</c> con eventos <i>conjoined</i> (T-2, ADR-0004). Esquemas: <c>plataforma</c>
    /// (tenants y empresas, globales), <c>boveda</c> (terceros, globales), <c>identidad</c> (API keys por tenant,
    /// idempotencia), <c>validacion</c> (todo lo demás, por tenant) y <c>eventos</c> (event store).
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

                // identidad
                opts.Schema.For<ApiKey>().DatabaseSchemaName("identidad").MultiTenanted().Index(x => x.Prefijo);
                opts.Schema.For<RegistroIdempotencia>().DatabaseSchemaName("identidad");

                // validacion (por tenant)
                opts.Schema.For<TipoVinculo>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_tipo_vinculo_codigo", TenancyScope.PerTenant, x => x.Codigo);
                opts.Schema.For<RegistroDeCambioDeConfiguracion>().MultiTenanted().Index(x => x.Fecha);
                opts.Schema.For<Vinculo>().MultiTenanted()
                    .UniqueIndex(UniqueIndexType.Computed, "ux_vinculo_natural", TenancyScope.PerTenant,
                        x => x.EmpresaId, x => x.TerceroId, x => x.TipoVinculoId)
                    .Index(x => x.EmpresaId)
                    .Index(x => x.CreadoEn);

                // Proyección inline: el documento Vinculo es la lectura de tras escritura (VinculoResumen, docs/03 §14).
                opts.Projections.Snapshot<Vinculo>(SnapshotLifecycle.Inline);
            })
            .UseLightweightSessions()
            .IntegrateWithWolverine();

        servicios.AddScoped<ISesiones, Sesiones>();
        servicios.AddScoped<SesionDeEmpresa>();
        return servicios;
    }
}

/// <summary>Abre sesiones con el tenant de la credencial y escribe actor y correlación en los metadatos de los eventos.</summary>
public sealed class Sesiones(IDocumentStore store, IContextoSolicitud contexto) : ISesiones
{
    public IDocumentSession DelTenant() => ParaTenant(contexto.TenantRequerido());

    public IDocumentSession ParaTenant(Guid tenantId)
    {
        var sesion = store.LightweightSession(tenantId.ToString("N"));
        sesion.CorrelationId = contexto.CorrelationId;
        sesion.SetHeader("actorTipo", contexto.ActorTipo.ToString());
        sesion.SetHeader("actorId", contexto.ActorId);
        return sesion;
    }

    public IQuerySession ConsultaGlobal() => store.QuerySession();
}
