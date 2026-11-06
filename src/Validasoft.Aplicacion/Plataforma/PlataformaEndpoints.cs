using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>Alta y consulta de tenants (rol Plataforma).</summary>
public static class TenantsEndpoints
{
    /// <summary>
    /// Crea el tenant, siembra sus tipos de vínculo y emite la primera API key de administración, todo en
    /// una sola transacción. El valor de la llave solo aparece en esta respuesta.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/tenants")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Crear(CrearTenantDto dto, ISesiones sesiones, IGeneradorDeApiKeys llaves,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var tenant = Tenant.Crear(dto.Slug, dto.Nombre, dto.Region, dto.ZonaHoraria, ahora);

        await using (var consulta = sesiones.ConsultaGlobal())
        {
            if (await consulta.Query<Tenant>().AnyAsync(t => t.Slug == tenant.Slug, ct))
                throw ReglaDeNegocioException.Conflicto("slug-duplicado", $"Ya existe un tenant con el slug '{tenant.Slug}'.");
        }

        await using var sesion = sesiones.ParaTenant(tenant.Id);
        sesion.Store(tenant);
        Auditoria.Registrar(sesion, contexto, "Tenant", tenant.Id.ToString(), AccionConfiguracion.Creado, null, tenant.ADto(), ahora);

        foreach (var tipo in TipoVinculo.Semilla())
        {
            sesion.Store(tipo);
            Auditoria.Registrar(sesion, contexto, "TipoVinculo", tipo.Id.ToString(), AccionConfiguracion.Creado, null, tipo.ADto(), ahora);
        }

        // Tipos de documento más comunes enlazados a su canónico; ninguna empresa los exige hasta que se configure (BO-3).
        foreach (var tipo in TipoDocumentoTenant.Semilla(tenant.Id, ahora))
        {
            sesion.Store(tipo);
            Auditoria.Registrar(sesion, contexto, TiposDocumentoEndpoints.Entidad, tipo.Id.ToString(), AccionConfiguracion.Creado, null,
                tipo.ADto(new Dictionary<Guid, TipoVinculo>()), ahora);
        }

        var (llave, valor) = llaves.Generar(tenant.Slug, "Administración inicial", Permisos.DelTenant, ["*"], null, false, ahora);
        sesion.Store(llave);
        Auditoria.Registrar(sesion, contexto, "ApiKey", llave.Id.ToString(), AccionConfiguracion.Creado, null, llave.ADto(), ahora);

        await sesion.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/plataforma/tenants/{tenant.Id}", new TenantCreadoDto(tenant.ADto(), llave.ADto(valor)));
    }

    [WolverineGet("/api/v1/plataforma/tenants")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<TenantDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var tenants = await consulta.Query<Tenant>().OrderBy(t => t.Slug).ToListAsync(ct);
        return [.. tenants.Select(t => t.ADto())];
    }

    [WolverineGet("/api/v1/plataforma/tenants/{id}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<TenantDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var tenant = await consulta.LoadAsync<Tenant>(id, ct)
            ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
        return tenant.ADto();
    }
}

/// <summary>Catálogos de plataforma de solo lectura.</summary>
public static class CatalogosEndpoints
{
    [WolverineGet("/api/v1/catalogos/paises")]
    [Authorize]
    public static IReadOnlyList<PaisDto> Paises() =>
        [.. CatalogoIdentificacion.Paises.Select(p => new PaisDto(p.Codigo, p.Nombre, [.. CatalogoIdentificacion.TiposDe(p.Codigo).Select(t => t.ADto())]))];

    /// <summary>Tipos de identificación del país, incluidos los genéricos (pasaporte, id fiscal).</summary>
    [WolverineGet("/api/v1/catalogos/tipos-identificacion")]
    [Authorize]
    public static IReadOnlyList<TipoIdentificacionDto> TiposIdentificacion(string pais) =>
        [.. CatalogoIdentificacion.TiposDe(pais).Select(t => t.ADto())];
}
