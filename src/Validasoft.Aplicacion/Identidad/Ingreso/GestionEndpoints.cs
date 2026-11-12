using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Invitación de una persona a entrar directo (ING-5): un correo con la dirección de la app; no lleva ningún secreto.</summary>
public static class InvitacionesDeUsuarios
{
    /// <summary>
    /// Publica en el outbox de <paramref name="mensajes"/> (misma transacción que el cambio) el correo que invita a <paramref name="usuario"/> a entrar con su
    /// correo a <c>{UrlApp}</c>: allí pedirá un código, sin contraseña. Sale solo si se confirma la transacción.
    /// </summary>
    public static async Task EnviarAsync(IMessageBus mensajes, IProtectorDeSecretos protector, Tenant tenant, Usuario usuario, DateTimeOffset ahora)
    {
        var roles = usuario.Roles.Count == 0
            ? null
            : string.Join(", ", usuario.Roles.Select(r => r.Rol.ToString()).Distinct());
        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.InvitacionDeUsuario, usuario.Correo, protector.Proteger("-"), ahora, tenant.Nombre, roles));
        usuario.InvitadoEn = ahora;
    }
}

/// <summary>
/// Gestión del ingreso de las personas del grupo por sus administradores (ING-5, <c>usuarios:administrar</c>, solo con un rol de todo el grupo): ver
/// y cerrar sus sesiones, ver y revocar sus passkeys (la recuperación cuando pierden una), invitarlas a entrar y configurar los modos de ingreso.
/// Cada acción queda en la auditoría de identidad.
/// </summary>
public static class GestionDeIngresoEndpoints
{
    [WolverineGet("/api/v1/usuarios/{id}/sesiones")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IReadOnlyList<SesionDeUsuarioDto>> Sesiones(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var cuenta = await CuentaDeAsync(id, sesiones, contexto, ct);
        return await MiCuentaEndpoints.SesionesActivasAsync(sesiones, cuenta, null, reloj.GetUtcNow(), ct);
    }

    /// <summary>Cierra una sesión de la persona (p. ej. de un dispositivo perdido).</summary>
    [WolverinePost("/api/v1/usuarios/{id}/sesiones/{sesionId}/cerrar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IResult> CerrarSesion(Guid id, Guid sesionId, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSesionesDirectas cache, TimeProvider reloj, CancellationToken ct)
    {
        var cuenta = await CuentaDeAsync(id, sesiones, contexto, ct);
        await using var sesion = sesiones.Global();
        var objetivo = await sesion.LoadAsync<SesionDeUsuario>(sesionId, ct);
        if (objetivo is null || objetivo.Propietario != cuenta.Tipo || objetivo.SujetoId != cuenta.SujetoId)
            throw ReglaDeNegocioException.NoEncontrado("sesion-no-encontrada", "No existe esa sesión.");

        await GestionDeSesiones.CerrarAsync(sesion, objetivo, SesionDeUsuario.MotivoAdministrador, $"{ActorTipo.Usuario}:{contexto.ActorId}",
            ContextoDeCliente.De(http, contexto.CorrelationId), contexto.ActorTipo, contexto.ActorId, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(sesionId);
        return Results.NoContent();
    }

    /// <summary>Las passkeys de la persona (activas y revocadas), de la más antigua a la más nueva.</summary>
    [WolverineGet("/api/v1/usuarios/{id}/passkeys")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IReadOnlyList<PasskeyDto>> Passkeys(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var cuenta = await CuentaDeAsync(id, sesiones, contexto, ct);
        await using var consulta = sesiones.ConsultaGlobal();
        var sujeto = cuenta.SujetoId;
        return [.. (await consulta.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Usuario && p.SujetoId == sujeto).OrderBy(p => p.CreadaEn).ToListAsync(ct))
            .Select(p => p.ADto())];
    }

    /// <summary>
    /// Revoca una passkey de la persona (la perdió o se la robaron): deja de valer de inmediato y se cierran sus sesiones; para volver a entrar usa el
    /// código del correo y registra otra. 409 <c>passkey-revocada</c> si ya lo estaba.
    /// </summary>
    [WolverinePost("/api/v1/usuarios/{id}/passkeys/{passkeyId}/revocar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<PasskeyDto> RevocarPasskey(Guid id, Guid passkeyId, RevocarPasskeyDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSesionesDirectas cache, CuentasDeIngreso cuentas, ServicioDeIngreso servicio, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "revocar-passkey", ct);
        var cuenta = await CuentaDeAsync(id, sesiones, contexto, ct);
        return await RevocarAsync(passkeyId, cuenta, dto.Motivo, http, sesiones, contexto, cache, cuentas, mensajes, reloj, ct);
    }

    /// <summary>La revocación compartida con la consola de Plataforma: la passkey, el cierre de las sesiones de la cuenta y la auditoría, en una transacción.</summary>
    public static async Task<PasskeyDto> RevocarAsync(Guid passkeyId, CuentaDeIngreso cuenta, string? motivo, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSesionesDirectas cache, CuentasDeIngreso cuentas, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var passkey = await sesion.LoadAsync<Passkey>(passkeyId, ct);
        if (passkey is null || passkey.Propietario != cuenta.Tipo || passkey.SujetoId != cuenta.SujetoId)
            throw ReglaDeNegocioException.NoEncontrado("passkey-no-encontrada", "No existe esa passkey.");

        var por = $"{contexto.ActorTipo}:{contexto.ActorId}";
        passkey.Revocar(por, motivo, ahora);
        sesion.Store(passkey);
        var cliente = ContextoDeCliente.De(http, contexto.CorrelationId);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.PasskeyRevocada, true, cliente, ahora, cuenta.TenantId, cuenta.Tipo, cuenta.SujetoId,
            actorTipo: contexto.ActorTipo, actorId: contexto.ActorId, passkeyId: passkey.Id, motivo: motivo, detalle: passkey.Nombre));
        var cerradas = await GestionDeSesiones.CerrarTodasAsync(sesion, cuenta.Tipo, cuenta.SujetoId, SesionDeUsuario.MotivoPasskeyRevocada, por, cliente,
            contexto.ActorTipo, contexto.ActorId, ahora, ct);
        await sesion.SaveChangesAsync(ct);
        foreach (var cerrada in cerradas)
            cache.Olvidar(cerrada);
        // La persona se entera por correo de que le revocaron una passkey (PI-15), sea quien sea quien lo hizo.
        if (await cuentas.CargarAsync(cuenta, ct) is { } dueno)
            await AvisosDeSeguridad.PasskeyRevocadaAsync(mensajes, dueno.Correo, dueno.Nombre, cuenta.TenantId, passkey.Nombre, porAdministrador: true, cliente, ahora);
        return passkey.ADto();
    }

    /// <summary>
    /// Invita (o vuelve a invitar) a la persona a entrar directo: un correo con la dirección de la app, donde pide un código con su correo.
    /// 409 <c>ingreso-directo-apagado</c> si el grupo no admite el modo directo; 409 <c>usuario-inactivo</c>.
    /// </summary>
    [WolverinePost("/api/v1/usuarios/{id}/invitar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<UsuarioDto> Invitar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IProtectorDeSecretos protector, RegistroDeIngresos registro, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();
        Tenant tenant;
        await using (var consulta = sesiones.ConsultaGlobal())
            tenant = await consulta.LoadAsync<Tenant>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese grupo.");
        if (!tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo))
            throw ReglaDeNegocioException.Conflicto("ingreso-directo-apagado", "Este grupo no admite el ingreso directo: actívalo en Configuración › Ingreso.");

        await using var sesion = sesiones.DelTenant(mensajes);
        var usuario = await sesion.LoadAsync<Usuario>(id, ct) ?? throw ReglaDeNegocioException.NoEncontrado("usuario-no-encontrado", "No existe ese usuario.");
        if (!usuario.Activo)
            throw ReglaDeNegocioException.Conflicto("usuario-inactivo", "No se invita a un usuario desactivado.");

        usuario.TenantId ??= tenantId;
        await InvitacionesDeUsuarios.EnviarAsync(mensajes, protector, tenant, usuario, ahora);
        sesion.Store(usuario);
        await sesion.SaveChangesAsync(ct);
        await registro.RegistrarAsync(EventoDeIngreso.UsuarioInvitado, true, ContextoDeCliente.De(http, contexto.CorrelationId), tenantId, TipoDePropietario.Usuario,
            usuario.Id.ToString(), usuario.Correo, actorTipo: contexto.ActorTipo, actorId: contexto.ActorId, ct: ct);
        return usuario.ADto(tenant.PasskeyParaTodos);
    }

    // ---------- política de ingreso del grupo ----------

    /// <summary>Modos de ingreso, duración e inactividad de la sesión directa y passkey para todos.</summary>
    [WolverineGet("/api/v1/configuracion/ingreso")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<ConfiguracionDeIngresoDto> ObtenerConfiguracion(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return (await consulta.LoadAsync<Tenant>(contexto.TenantRequerido(), ct) ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese grupo.")).IngresoADto();
    }

    /// <summary>
    /// Fija la política de ingreso (auditada). Al habilitar el modo directo se completa el grupo de los usuarios anteriores; al apagarlo se cierran las
    /// sesiones directas abiertas. 422 <c>modos-de-ingreso-invalidos</c>, <c>sesion-invalida</c>, <c>inactividad-invalida</c>.
    /// </summary>
    [WolverinePut("/api/v1/configuracion/ingreso")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static Task<ConfiguracionDeIngresoDto> FijarConfiguracion(ConfiguracionDeIngresoDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        PoliticaDeIngreso politica, ICacheDeSesionesDirectas cache, TimeProvider reloj, CancellationToken ct) =>
        FijarAsync(contexto.TenantRequerido(), dto, http, sesiones, contexto, politica, cache, reloj, ct);

    /// <summary>La misma política desde la consola de Plataforma (<c>plataforma:tenants</c>).</summary>
    [WolverinePut("/api/v1/plataforma/tenants/{id}/ingreso")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static Task<ConfiguracionDeIngresoDto> FijarDesdePlataforma(Guid id, ConfiguracionDeIngresoDto dto, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, PoliticaDeIngreso politica, ICacheDeSesionesDirectas cache, TimeProvider reloj, CancellationToken ct) =>
        FijarAsync(id, dto, http, sesiones, contexto, politica, cache, reloj, ct);

    private static async Task<ConfiguracionDeIngresoDto> FijarAsync(Guid tenantId, ConfiguracionDeIngresoDto dto, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, PoliticaDeIngreso politica, ICacheDeSesionesDirectas cache, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var cliente = ContextoDeCliente.De(http, contexto.CorrelationId);
        await using var sesion = sesiones.ParaTenant(tenantId);
        var tenant = await sesion.LoadAsync<Tenant>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese grupo.");
        var antes = tenant.IngresoADto();
        var teniaDirecto = tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo);
        tenant.FijarIngreso(Mapeos.ModosADominio(dto.Modos), dto.SesionHoras, dto.InactividadMinutos, dto.PasskeyParaTodos);
        sesion.Store(tenant);
        Auditoria.Registrar(sesion, contexto, "IngresoDelGrupo", tenant.Id.ToString(), AccionConfiguracion.Modificado, antes, tenant.IngresoADto(), ahora);

        var cerradas = new List<Guid>();
        if (tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo) && !teniaDirecto)
        {
            foreach (var u in await sesion.Query<Usuario>().Where(x => x.TenantId == null).ToListAsync(ct))
            {
                u.TenantId = tenantId;
                sesion.Store(u);
            }
        }
        else if (!tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo) && teniaDirecto)
        {
            await using var global = sesiones.Global();
            var abiertas = await global.Query<SesionDeUsuario>().Where(s => s.TenantId == tenantId && s.RevocadaEn == null).ToListAsync(ct);
            foreach (var s in abiertas)
                GestionDeSesiones.CerrarSinGuardar(global, s, "modo-directo-apagado", $"{contexto.ActorTipo}:{contexto.ActorId}", cliente, contexto.ActorTipo, contexto.ActorId, ahora);
            await global.SaveChangesAsync(ct);
            cerradas.AddRange(abiertas.Select(s => s.Id));
        }

        await sesion.SaveChangesAsync(ct);
        politica.Olvidar(tenantId);
        foreach (var id in cerradas)
            cache.Olvidar(id);
        return tenant.IngresoADto();
    }

    private static async Task<CuentaDeIngreso> CuentaDeAsync(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var sesion = sesiones.DelTenant();
        _ = await sesion.LoadAsync<Usuario>(id, ct) ?? throw ReglaDeNegocioException.NoEncontrado("usuario-no-encontrado", "No existe ese usuario.");
        return new CuentaDeIngreso(TipoDePropietario.Usuario, tenantId, id.ToString());
    }
}
