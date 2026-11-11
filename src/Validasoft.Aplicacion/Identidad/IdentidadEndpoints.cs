using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Plataforma;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine;
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
    public static async Task<SistemaIntegradoDto> RotarClave(Guid id, RotarClavePublicaDto dto, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "rotar-clave-del-sistema", ct);
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

    /// <summary>
    /// Desactiva el sistema (PI-7): <b>se revocan todas sus API keys</b> (también las de prueba), sus sesiones —las de identidad delegada y las embebidas del tercero— dejan de
    /// valer en segundos en todos los servidores, sus aserciones se rechazan y sus orígenes ya no pueden embeber. Reactivarlo no restaura las llaves revocadas: hay que emitir
    /// otras. Exige reautenticar con la passkey a quien entró directo (403 <c>reautenticacion-requerida</c>).
    /// </summary>
    [WolverinePost("/api/v1/sistemas/{id}/desactivar")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> Desactivar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeCredenciales cacheLlaves,
        ICacheDeSistemas cacheSistemas, OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-sistema", ct);
        return (await GestionDeSistemas.DesactivarIntegradorAsync(contexto.TenantRequerido(), id, sesiones, contexto, cacheLlaves, cacheSistemas, embebido, reloj, ct)).ADto();
    }

    /// <summary>Vuelve a activar el sistema (PI-7). Sus llaves se revocaron al desactivarlo y no se restauran: emite otras. 409 <c>sistema-activo</c> si ya estaba activo.</summary>
    [WolverinePost("/api/v1/sistemas/{id}/reactivar")]
    [Authorize(Policy = Permisos.SistemasAdministrar)]
    public static async Task<SistemaIntegradoDto> Reactivar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSistemas cacheSistemas,
        OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "reactivar-sistema", ct);
        return (await GestionDeSistemas.ReactivarIntegradorAsync(contexto.TenantRequerido(), id, sesiones, contexto, cacheSistemas, embebido, reloj, ct)).ADto();
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
    public static async Task<IResult> Crear(CrearUsuarioDto dto, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var tenantId = contexto.TenantRequerido();
        var usuario = Usuario.Crear(dto.Nombre, dto.Correo, dto.Identificacion?.ADominio(), ahora, tenantId);
        var roles = (dto.Roles ?? []).Select(r => r.ADominio()).ToList();
        await ValidarRolesAsync(roles, [], sesiones, contexto, ct);
        usuario.AsignarRoles(roles);

        Tenant? tenant = null;
        await using (var global = sesiones.ConsultaGlobal())
            tenant = await global.LoadAsync<Tenant>(tenantId, ct);
        if (dto.Invitar == true && tenant is not null && !tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo))
            throw ReglaDeNegocioException.Validacion("ingreso-directo-apagado", "Este grupo no admite el ingreso directo: actívalo en Configuración › Ingreso para invitar por correo.");

        await using var sesion = sesiones.DelTenant(mensajes);
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

        if (dto.Invitar == true && tenant is not null)
            await InvitacionesDeUsuarios.EnviarAsync(mensajes, protector, tenant, usuario, ahora);

        sesion.Store(usuario);
        Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Creado, null, usuario.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/usuarios/{usuario.Id}", usuario.ADto(tenant?.PasskeyParaTodos ?? false));
    }

    [WolverineGet("/api/v1/usuarios")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IReadOnlyList<UsuarioDto>> Listar(string? correo, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        IQueryable<Usuario> consulta = sesion.Query<Usuario>();
        if (!string.IsNullOrWhiteSpace(correo))
        {
            var normalizado = correo.Trim().ToLowerInvariant();
            consulta = consulta.Where(u => u.Correo == normalizado);
        }

        var usuarios = await consulta.OrderBy(u => u.Nombre).ToListAsync(ct);
        var (paraTodos, activas) = await IngresoDelGrupoAsync(sesiones, contexto, usuarios, ct);
        return [.. usuarios.Select(u => u.ADto(paraTodos, activas.GetValueOrDefault(u.Id.ToString())))];
    }

    /// <summary>Si el grupo exige passkey a todos y cuántas passkeys activas tiene cada usuario (para la lista y el detalle).</summary>
    private static async Task<(bool ParaTodos, Dictionary<string, int> Activas)> IngresoDelGrupoAsync(ISesiones sesiones, IContextoSolicitud contexto,
        IReadOnlyList<Usuario> usuarios, CancellationToken ct)
    {
        await using var global = sesiones.ConsultaGlobal();
        var paraTodos = (await global.LoadAsync<Tenant>(contexto.TenantRequerido(), ct))?.PasskeyParaTodos ?? false;
        var ids = usuarios.Select(u => u.Id.ToString()).ToArray();
        if (ids.Length == 0)
            return (paraTodos, []);
        var passkeys = await global.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Usuario && p.RevocadaEn == null && p.SujetoId.IsOneOf(ids)).ToListAsync(ct);
        return (paraTodos, passkeys.GroupBy(p => p.SujetoId).ToDictionary(g => g.Key, g => g.Count()));
    }

    [WolverineGet("/api/v1/usuarios/{id}")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> Obtener(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var usuario = await CargarAsync(sesion, id, ct);
        var (paraTodos, activas) = await IngresoDelGrupoAsync(sesiones, contexto, [usuario], ct);
        return usuario.ADto(paraTodos, activas.GetValueOrDefault(usuario.Id.ToString()));
    }

    /// <summary>Reemplaza los roles del usuario. Cada empresa debe ser del tenant y estar al alcance de quien asigna.</summary>
    [WolverinePut("/api/v1/usuarios/{id}/roles")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> AsignarRoles(Guid id, AsignarRolesDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "cambiar-roles", ct);
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
    public static async Task<UsuarioDto> Desactivar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSesionesDirectas cache,
        ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-usuario", ct);
        await using var sesion = sesiones.DelTenant();
        var usuario = await CargarAsync(sesion, id, ct);
        await ValidarRolesAsync([], usuario.Roles, sesiones, contexto, ct);

        var ahora = reloj.GetUtcNow();
        var antes = usuario.ADto();
        usuario.Desactivar();
        sesion.Store(usuario);
        Auditoria.Registrar(sesion, contexto, "Usuario", usuario.Id.ToString(), AccionConfiguracion.Desactivado, antes, usuario.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);

        // Desactivada la cuenta, sus sesiones directas dejan de valer de inmediato (ING-5).
        await using var global = sesiones.Global();
        var cerradas = await GestionDeSesiones.CerrarTodasAsync(global, TipoDePropietario.Usuario, usuario.Id.ToString(), SesionDeUsuario.MotivoUsuarioDesactivado,
            $"{contexto.ActorTipo}:{contexto.ActorId}", ContextoDeCliente.De(http, contexto.CorrelationId), contexto.ActorTipo, contexto.ActorId, ahora, ct);
        if (cerradas.Count > 0)
            await global.SaveChangesAsync(ct);
        foreach (var cerrada in cerradas)
            cache.Olvidar(cerrada);
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
