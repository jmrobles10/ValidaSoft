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
using Validasoft.Aplicacion.Identidad;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>Alta y consulta de tenants (rol Plataforma).</summary>
public static class TenantsEndpoints
{
    /// <summary>
    /// Crea el tenant, siembra sus tipos de vínculo y de documento y emite la primera API key de administración. Con
    /// <c>empresa</c>, <c>administrador</c> y <c>sistema</c> deja además el grupo operable de punta a punta (PT-5): su primera
    /// empresa, su administrador de todo el grupo y el sistema integrado con una API key de todos los permisos del tenant
    /// (que actúa en nombre de las personas con <c>X-Usuario-Delegado</c>). Todo en una sola transacción; los valores de las
    /// llaves solo aparecen en esta respuesta. 409 <c>slug-duplicado</c>; 422 de cada parte (<c>slug-invalido</c>,
    /// <c>prefijo-folio-invalido</c>, <c>clave-publica-invalida</c>, <c>origen-invalido</c>, <c>sistema-requerido</c>…).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/tenants")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Crear(CrearTenantDto dto, ISesiones sesiones, IGeneradorDeApiKeys llaves, IContextoSolicitud contexto,
        OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var tenant = Tenant.Crear(dto.Slug, dto.Nombre, dto.Region, dto.ZonaHoraria, ahora);
        if (dto.Administrador?.IdExterno is { Length: > 0 } && dto.Sistema is null)
            throw ReglaDeNegocioException.Validacion("sistema-requerido",
                "El id externo del administrador es su usuario en el sistema integrado: envía también el sistema, o quita el id externo.");

        // Se valida todo antes de escribir: un dato malo no deja un tenant a medias.
        var empresa = dto.Empresa is { } datosEmpresa
            ? Empresa.Crear(tenant.Id, datosEmpresa.Identificacion.ADominio(), datosEmpresa.Nombre, datosEmpresa.PrefijoFolio, datosEmpresa.LogoUrl,
                datosEmpresa.ReplyTo, ahora)
            : null;
        SistemaIntegrado? sistema = null;
        if (dto.Sistema is { } datosSistema)
        {
            sistema = SistemaIntegrado.Crear(datosSistema.Codigo, datosSistema.Nombre, datosSistema.ClavePublicaPem, ahora);
            sistema.FijarOrigenes(datosSistema.OrigenesPermitidos, embebido.PermiteHttpLocal);
        }

        Usuario? administrador = null;
        if (dto.Administrador is { } datosAdmin)
        {
            administrador = Usuario.Crear(datosAdmin.Nombre, datosAdmin.Correo, null, ahora);
            administrador.AsignarRoles([new RolAsignado(Rol.Administrador, null)]);
            if (datosAdmin.IdExterno is { Length: > 0 } sub && sistema is not null)
                administrador.EnlazarIdentidad(sistema.Id, sub.Trim());
        }

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

        // Tipos de documento más comunes enlazados a su canónico; ninguno se pide hasta que un flujo lo incluya (RQ-3).
        foreach (var tipo in TipoDocumentoTenant.Semilla(tenant.Id, ahora))
        {
            sesion.Store(tipo);
            Auditoria.Registrar(sesion, contexto, TiposDocumentoEndpoints.Entidad, tipo.Id.ToString(), AccionConfiguracion.Creado, null,
                tipo.ADto(), ahora);
        }

        var (llave, valor) = llaves.Generar(tenant.Slug, "Administración inicial", Permisos.DelTenant, ["*"], null, false, ahora);
        sesion.Store(llave);
        Auditoria.Registrar(sesion, contexto, "ApiKey", llave.Id.ToString(), AccionConfiguracion.Creado, null, llave.ADto(), ahora);

        if (empresa is not null)
        {
            sesion.Store(empresa);
            Auditoria.Registrar(sesion, contexto, "Empresa", empresa.Id.ToString(), AccionConfiguracion.Creado, null, empresa.ADto(), ahora, empresa.Id);
        }

        ApiKeyDto? llaveSistema = null;
        if (sistema is not null)
        {
            sesion.Store(sistema);
            Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Creado, null, sistema.ADto(), ahora);
            var (deSistema, valorDeSistema) = llaves.Generar(tenant.Slug, $"Sistema {sistema.Codigo}", Permisos.DelTenant, ["*"], null, false, ahora);
            deSistema.SistemaId = sistema.Id;
            sesion.Store(deSistema);
            Auditoria.Registrar(sesion, contexto, "ApiKey", deSistema.Id.ToString(), AccionConfiguracion.Creado, null, deSistema.ADto(), ahora);
            llaveSistema = deSistema.ADto(valorDeSistema);
        }

        if (administrador is not null)
        {
            foreach (var identidad in administrador.Identidades)
            {
                var clave = IndiceIdentidadExterna.Clave(identidad.SistemaId, identidad.IdExterno);
                sesion.Insert(new IndiceIdentidadExterna { Id = clave, UsuarioId = administrador.Id });
            }

            sesion.Store(administrador);
            Auditoria.Registrar(sesion, contexto, "Usuario", administrador.Id.ToString(), AccionConfiguracion.Creado, null, administrador.ADto(), ahora);
        }

        await sesion.SaveChangesAsync(ct);
        if (sistema is not null)
            embebido.Invalidar();

        return Results.Created($"/api/v1/plataforma/tenants/{tenant.Id}",
            new TenantCreadoDto(tenant.ADto(), llave.ADto(valor), empresa?.ADto(), administrador?.ADto(), sistema?.ADto(), llaveSistema));
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

    /// <summary>
    /// Los grupos con sus empresas y el estado del plan de IA de cada una (configurado, activo, llave configurada y sus
    /// últimos cuatro), para la consola de Plataforma. Ordenados por nombre del grupo y de la empresa.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/grupos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<GrupoDto>> Grupos(ISesiones sesiones, CancellationToken ct)
    {
        List<Tenant> tenants;
        List<Empresa> empresas;
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            tenants = [.. await consulta.Query<Tenant>().OrderBy(t => t.Nombre).ToListAsync(ct)];
            empresas = [.. await consulta.Query<Empresa>().ToListAsync(ct)];
        }

        var grupos = new List<GrupoDto>(tenants.Count);
        foreach (var tenant in tenants)
        {
            var propias = empresas.Where(e => e.TenantId == tenant.Id).OrderBy(e => e.Nombre, StringComparer.CurrentCulture).ToList();
            var planes = new Dictionary<Guid, PlanDeIaEmpresa>();
            if (propias.Count > 0)
            {
                await using var sesion = sesiones.ParaTenant(tenant.Id);
                foreach (var plan in await sesion.LoadManyAsync<PlanDeIaEmpresa>(ct, propias.Select(e => e.Id)))
                    planes[plan.Id] = plan;
            }

            grupos.Add(new GrupoDto(tenant.ADto(), [.. propias.Select(e => EmpresaDeGrupo(e, planes.GetValueOrDefault(e.Id)))]));
        }

        return grupos;
    }

    private static EmpresaDeGrupoDto EmpresaDeGrupo(Empresa e, PlanDeIaEmpresa? plan) =>
        new(e.Id, e.Nombre, e.Identificacion.ADto(), e.PrefijoFolio, e.Activa, plan is not null, plan?.Activo ?? false, plan?.LlaveConfigurada ?? false,
            plan is { LlaveConfigurada: true } ? plan.UltimosCuatroLlave : null);
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
