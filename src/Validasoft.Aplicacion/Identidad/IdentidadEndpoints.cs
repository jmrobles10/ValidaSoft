using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Sistemas integrados del tenant y sus claves públicas (ADR-0011).</summary>
public static class SistemasEndpoints
{
    [WolverinePost("/api/v1/sistemas")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<IResult> Crear(CrearSistemaIntegradoDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var sistema = SistemaIntegrado.Crear(dto.Codigo, dto.Nombre, dto.ClavePublicaPem, ahora);
        sistema.FijarOrigenes(dto.OrigenesPermitidos, embebido.PermiteHttpLocal);

        await using var sesion = sesiones.DelTenant();
        var codigo = sistema.Codigo;
        if (await sesion.Query<SistemaIntegrado>().AnyAsync(s => s.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("sistema-duplicado", $"Ya existe el sistema {codigo}.");

        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Creado, null, sistema.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        embebido.Invalidar();
        return Results.Created($"/api/v1/sistemas/{sistema.Id}", sistema.ADto());
    }

    [WolverineGet("/api/v1/sistemas")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<IReadOnlyList<SistemaIntegradoDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var sistemas = await sesion.Query<SistemaIntegrado>().OrderBy(s => s.Codigo).ToListAsync(ct);
        return [.. sistemas.Select(s => s.ADto())];
    }

    [WolverineGet("/api/v1/sistemas/{id}")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        return (await CargarAsync(sesion, id, ct)).ADto();
    }

    /// <summary>Rotación de la clave pública: desde ya, solo valen las aserciones firmadas con la nueva.</summary>
    [WolverinePut("/api/v1/sistemas/{id}/clave-publica")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> RotarClave(Guid id, RotarClavePublicaDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var sistema = await CargarAsync(sesion, id, ct);
        var antes = sistema.ADto();
        sistema.RotarClavePublica(dto.ClavePublicaPem);

        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Rotado, antes, sistema.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return sistema.ADto();
    }

    /// <summary>
    /// Reemplaza los orígenes desde los que el sistema aloja las pantallas embebidas (SE-1). Cambian
    /// <c>frame-ancestors</c> y <c>GET /public/v1/embed/origenes-permitidos</c> (caché de 60 s por nodo).
    /// </summary>
    [WolverinePut("/api/v1/sistemas/{id}/origenes")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> FijarOrigenes(Guid id, FijarOrigenesDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var sistema = await CargarAsync(sesion, id, ct);
        var antes = sistema.ADto();
        sistema.FijarOrigenes(dto.Origenes, embebido.PermiteHttpLocal);

        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Modificado, antes, sistema.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        embebido.Invalidar();
        return sistema.ADto();
    }

    /// <summary>Sus API keys dejan de autenticar, sus aserciones se rechazan y sus orígenes ya no pueden embeber.</summary>
    [WolverinePost("/api/v1/sistemas/{id}/desactivar")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> Desactivar(Guid id, ISesiones sesiones, IContextoSolicitud contexto,
        OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var sistema = await CargarAsync(sesion, id, ct);
        var antes = sistema.ADto();
        sistema.Desactivar();

        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Desactivado, antes, sistema.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        embebido.Invalidar();
        return sistema.ADto();
    }

    private static async Task<SistemaIntegrado> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<SistemaIntegrado>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("sistema-no-encontrado", "No existe ese sistema integrado.");
}

/// <summary>
/// Usuarios del tenant (ADR-0011): roles y áreas se asignan aquí, iguales para todos los sistemas (ID-2). Los usuarios
/// de sistemas integrados se crean solos en su primer acceso delegado, sin roles.
/// </summary>
public static class UsuariosEndpoints
{
    [WolverinePost("/api/v1/usuarios")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IResult> Crear(CrearUsuarioDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var usuario = Usuario.Crear(dto.Nombre, dto.Correo, dto.Identificacion?.ADominio(), ahora);
        var roles = (dto.Roles ?? []).Select(r => r.ADominio()).ToList();
        await ValidarRolesAsync(roles, [], sesiones, contexto, ct);
        usuario.AsignarRoles(roles);

        await using var sesion = sesiones.DelTenant();
        var correo = usuario.Correo;
        if (await sesion.Query<Usuario>().AnyAsync(u => u.Correo == correo, ct))
            throw ReglaDeNegocioException.Conflicto("usuario-duplicado", $"Ya existe un usuario con el correo {correo}.");

        foreach (var identidad in dto.Identidades ?? [])
        {
            if (await sesion.LoadAsync<SistemaIntegrado>(identidad.SistemaId, ct) is null)
                throw ReglaDeNegocioException.Validacion("sistema-invalido", $"El sistema {identidad.SistemaId} no existe en el tenant.");

            usuario.EnlazarIdentidad(identidad.SistemaId, identidad.IdExterno);
            var clave = IndiceIdentidadExterna.Clave(identidad.SistemaId, identidad.IdExterno);
            if (await sesion.LoadAsync<IndiceIdentidadExterna>(clave, ct) is not null)
                throw ReglaDeNegocioException.Conflicto("identidad-duplicada", "Esa identidad externa ya pertenece a otro usuario.");
            sesion.Insert(new IndiceIdentidadExterna { Id = clave, UsuarioId = usuario.Id });
        }

        sesion.Store(usuario);
        Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Creado, null, usuario.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/usuarios/{usuario.Id}", usuario.ADto());
    }

    [WolverineGet("/api/v1/usuarios")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IReadOnlyList<UsuarioDto>> Listar(string? correo, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        IQueryable<Usuario> consulta = sesion.Query<Usuario>();
        if (!string.IsNullOrWhiteSpace(correo))
        {
            var normalizado = correo.Trim().ToLowerInvariant();
            consulta = consulta.Where(u => u.Correo == normalizado);
        }

        var usuarios = await consulta.OrderBy(u => u.Nombre).ToListAsync(ct);
        return [.. usuarios.Select(u => u.ADto())];
    }

    [WolverineGet("/api/v1/usuarios/{id}")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        return (await CargarAsync(sesion, id, ct)).ADto();
    }

    /// <summary>Reemplaza los roles del usuario. Cada empresa debe ser del tenant y estar al alcance de quien asigna.</summary>
    [WolverinePut("/api/v1/usuarios/{id}/roles")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> AsignarRoles(Guid id, AsignarRolesDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var usuario = await CargarAsync(sesion, id, ct);
        var roles = (dto.Roles ?? []).Select(r => r.ADominio()).ToList();
        await ValidarRolesAsync(roles, usuario.Roles, sesiones, contexto, ct);

        var antes = usuario.ADto();
        usuario.AsignarRoles(roles);
        sesion.Store(usuario);
        Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Modificado, antes, usuario.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return usuario.ADto();
    }

    /// <summary>Un usuario desactivado no puede actuar por delegación (401) aunque su sistema lo autentique.</summary>
    [WolverinePost("/api/v1/usuarios/{id}/desactivar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> Desactivar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var usuario = await CargarAsync(sesion, id, ct);
        await ValidarRolesAsync([], usuario.Roles, sesiones, contexto, ct);

        var antes = usuario.ADto();
        usuario.Desactivar();
        sesion.Store(usuario);
        Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Desactivado, antes, usuario.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return usuario.ADto();
    }

    private static async Task<Usuario> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<Usuario>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("usuario-no-encontrado", "No existe ese usuario.");

    /// <summary>
    /// Las empresas de los roles deben ser del tenant. Una credencial limitada a ciertas empresas solo toca roles de
    /// esas empresas, ni los nuevos ni los que el usuario ya tenía (anti-escalada, como en las API keys).
    /// </summary>
    private static async Task ValidarRolesAsync(IReadOnlyList<RolAsignado> nuevos, IReadOnlyList<RolAsignado> actuales,
        ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ids = nuevos.Where(r => r.EmpresaId is not null).Select(r => r.EmpresaId!.Value).Distinct().ToList();
        if (ids.Count > 0)
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var propias = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync(ct);
            if (ids.Any(i => !propias.Contains(i)))
                throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Alguna empresa de los roles no pertenece al tenant.");
        }

        if (contexto.Empresas.Contains("*"))
            return;

        if (nuevos.Concat(actuales).Any(r => r.EmpresaId is null || !contexto.Empresas.Contains(r.EmpresaId.Value.ToString())))
            throw ReglaDeNegocioException.Prohibido("alcance-insuficiente",
                "Tu credencial no alcanza todas las empresas de esos roles (ni los roles de todo el tenant).");
    }
}

/// <summary>Sesiones cortas para las pantallas embebidas en los sistemas integrados (ID-3).</summary>
public static class SesionesUsuarioEndpoints
{
    /// <summary>
    /// Canjea la identidad delegada (API key + <c>X-Usuario-Delegado</c>) por un token de Validasoft de 30 minutos con
    /// el permiso efectivo de esa persona; el iframe o Web Component lo envía como <c>Authorization: Bearer</c>.
    /// </summary>
    [WolverinePost("/api/v1/sesiones-usuario")]
    [Authorize]
    public static SesionUsuarioDto Crear(HttpContext http, IEmisorDeSesiones emisor, TimeProvider reloj) =>
        emisor.Emitir(http.User, reloj.GetUtcNow());
}
