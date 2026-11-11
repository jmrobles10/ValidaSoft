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
        // Los sandbox (SB-1) no son clientes: no salen en la consola ni en sus cuentas.
        var tenants = await consulta.Query<Tenant>().Where(t => t.TenantDeProduccionId == null).OrderBy(t => t.Slug).ToListAsync(ct);
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

    private const int TamanoPaginaGrupos = 20;
    private const int MaximoPaginaGrupos = 100;

    /// <summary>
    /// Una página de grupos con sus empresas y el estado del plan de IA de cada una (configurado, activo, llave configurada
    /// y sus últimos cuatro), para la consola de Plataforma. Ordenados por nombre del grupo y su id (orden estable); las
    /// empresas de cada grupo, por nombre. <paramref name="q"/>: parte del nombre o del identificador del grupo, o de la
    /// razón social o el número de identificación (NIT) de alguna de sus empresas (el grupo sale con todas las suyas).
    /// <paramref name="limite"/> por omisión 20 (máximo 100); <paramref name="cursor"/> es el opaco de la página anterior
    /// (<c>siguienteCursor</c>). Solo se cargan las empresas y los planes de los grupos de la página.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/grupos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PaginaDto<GrupoDto>> Grupos(ISesiones sesiones, string? q, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? TamanoPaginaGrupos, 1, MaximoPaginaGrupos);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var texto = q?.Trim();

        List<Tenant> tenants;
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            IQueryable<Tenant> consultaTenants = consulta.Query<Tenant>().Where(t => t.TenantDeProduccionId == null); // sin los sandbox (SB-1)
            if (!string.IsNullOrEmpty(texto))
            {
                var numero = NumeroBuscado(texto);
                var deEmpresas = (await consulta.Query<Empresa>()
                        .Where(e => e.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                                    e.Identificacion.Numero.Contains(numero, StringComparison.OrdinalIgnoreCase))
                        .Select(e => e.TenantId)
                        .ToListAsync(ct))
                    .Distinct().ToArray();
                consultaTenants = consultaTenants.Where(t => t.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                                                             t.Slug.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                                                             t.Id.IsOneOf(deEmpresas));
            }

            tenants = [.. await consultaTenants.OrderBy(t => t.Nombre).ThenBy(t => t.Id).Skip(desde).Take(tamano + 1).ToListAsync(ct)];
        }

        var hayMas = tenants.Count > tamano;
        if (hayMas)
            tenants.RemoveAt(tenants.Count - 1);

        return new PaginaDto<GrupoDto>(await ConSusEmpresasAsync(sesiones, tenants, ct), hayMas ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    /// <summary>
    /// Un grupo con sus empresas y el estado del plan de IA de cada una: lo que la consola necesita para rotular una
    /// empresa (la pantalla del plan) sin recorrer la lista de grupos. 404 <c>tenant-no-encontrado</c>.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/grupos/{id}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<GrupoDto> Grupo(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        Tenant tenant;
        await using (var consulta = sesiones.ConsultaGlobal())
            tenant = await consulta.LoadAsync<Tenant>(id, ct)
                ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");

        return (await ConSusEmpresasAsync(sesiones, [tenant], ct)).Single();
    }

    /// <summary>Las empresas de esos grupos (una sola consulta) y sus planes de IA, que viven en el tenant de cada grupo.</summary>
    private static async Task<List<GrupoDto>> ConSusEmpresasAsync(ISesiones sesiones, IReadOnlyList<Tenant> tenants, CancellationToken ct)
    {
        var ids = tenants.Select(t => t.Id).ToArray();
        List<Empresa> empresas = [];
        if (ids.Length > 0)
        {
            await using var consulta = sesiones.ConsultaGlobal();
            empresas = [.. await consulta.Query<Empresa>().Where(e => e.TenantId.IsOneOf(ids)).ToListAsync(ct)];
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

    /// <summary>
    /// El número de identificación como se guarda: sin puntos ni espacios y sin el dígito de verificación
    /// (<c>900.123.456-7</c> busca <c>900123456</c>).
    /// </summary>
    private static string NumeroBuscado(string texto)
    {
        var sinSeparadores = texto.Replace(".", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        var corte = sinSeparadores.LastIndexOf('-');
        var numero = corte > 0 && corte == sinSeparadores.Length - 2 ? sinSeparadores[..corte] : sinSeparadores;
        return numero.Length > 0 ? numero : texto;
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
