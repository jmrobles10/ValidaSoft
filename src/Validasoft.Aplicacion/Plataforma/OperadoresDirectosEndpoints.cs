using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>
/// Operadores de Plataforma de ingreso directo (ING-4): entran a la consola con correo, código y passkey (obligatoria) y se gestionan con la credencial
/// de arranque —solo para dar de alta al primero— o desde la propia consola. Un operador revoca las passkeys y cierra las sesiones de otro cuando las
/// pierde; todo queda en la auditoría de identidad.
/// </summary>
public static class OperadoresDirectosEndpoints
{
    public const string Entidad = "OperadorDePlataforma";

    /// <summary>
    /// Da de alta un operador de ingreso directo y le envía el correo para entrar. 409 <c>operador-duplicado</c>; 422 <c>correo-invalido</c>,
    /// <c>nombre-requerido</c>. La primera vez se hace con la credencial de arranque (<c>Validasoft:Plataforma:ClaveBootstrap</c>); después, con la sesión de
    /// un operador, y la clave de arranque se desactiva (docs/07 §2.4).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/operadores")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Crear(CrearOperadorDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IProtectorDeSecretos protector, RegistroDeIngresos registro, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "crear-operador", ct);
        var ahora = reloj.GetUtcNow();
        var operador = OperadorDePlataforma.RegistrarDirecto(dto.Nombre, dto.Correo, ahora);
        await using var sesion = sesiones.Global(mensajes);
        if (await sesion.LoadAsync<OperadorDePlataforma>(operador.Id, ct) is not null)
            throw ReglaDeNegocioException.Conflicto("operador-duplicado", $"Ya existe un operador con el correo {operador.Correo}.");

        sesion.Insert(operador);
        Auditoria.Registrar(sesion, contexto, Entidad, operador.Id, AccionConfiguracion.Creado, null, operador.ADto(0), ahora);
        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.InvitacionDeUsuario, operador.Correo, protector.Proteger("-"), ahora,
            "Plataforma de Validasoft", "Operador de Plataforma"));
        await sesion.SaveChangesAsync(ct);
        await registro.RegistrarAsync(EventoDeIngreso.UsuarioInvitado, true, ContextoDeCliente.De(http, contexto.CorrelationId), null, TipoDePropietario.Operador, operador.Id,
            operador.Correo, actorTipo: contexto.ActorTipo, actorId: contexto.ActorId, ct: ct);
        return Results.Created($"/api/v1/plataforma/operadores/{Uri.EscapeDataString(operador.Id)}", operador.ADto(0));
    }

    /// <summary>Los operadores de Plataforma (de ingreso directo y los de identidad delegada) con cuántas passkeys activas tiene cada uno.</summary>
    [WolverineGet("/api/v1/plataforma/operadores")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<OperadorDePlataformaDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var operadores = await consulta.Query<OperadorDePlataforma>().OrderBy(o => o.Nombre).ToListAsync(ct);
        var activas = await consulta.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Operador && p.RevocadaEn == null).ToListAsync(ct);
        return [.. operadores.Select(o => o.ADto(activas.Count(p => p.SujetoId == o.Id)))];
    }

    /// <summary>
    /// Desactiva al operador (PI-7): ya no entra, <b>sus sesiones se cierran</b> —también las de identidad delegada, en todos los servidores— y <b>sus passkeys se revocan</b>.
    /// Reactivarlo no las restaura: entra con el código del correo y registra una nueva. Nadie puede desactivarse a sí mismo (409 <c>auto-desactivacion</c>). Exige reautenticar
    /// con la passkey (403 <c>reautenticacion-requerida</c>).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/operadores/{id}/desactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<OperadorDePlataformaDto> Desactivar(string id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSesionesDirectas cache,
        ICacheDeSistemas cacheSistemas, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-operador", ct);
        if (string.Equals(contexto.ActorId, id, StringComparison.Ordinal))
            throw ReglaDeNegocioException.Conflicto("auto-desactivacion", "No puedes desactivarte a ti mismo: pídeselo a otro operador.");

        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var operador = await sesion.LoadAsync<OperadorDePlataforma>(id, ct) ?? throw ReglaDeNegocioException.NoEncontrado("operador-no-encontrado", "No existe ese operador.");
        var antes = operador.ADto(0);
        operador.Desactivar();
        sesion.Store(operador);
        Auditoria.Registrar(sesion, contexto, Entidad, operador.Id, AccionConfiguracion.Desactivado, antes, operador.ADto(0), ahora);
        var cliente = ContextoDeCliente.De(http, contexto.CorrelationId);
        var por = $"{contexto.ActorTipo}:{contexto.ActorId}";
        var cerradas = await GestionDeSesiones.CerrarTodasAsync(sesion, TipoDePropietario.Operador, operador.Id, SesionDeUsuario.MotivoUsuarioDesactivado, por, cliente,
            contexto.ActorTipo, contexto.ActorId, ahora, ct);

        // Sus llaves (passkeys) también se revocan: al volver a entrar registra otras.
        var llaves = await sesion.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Operador && p.SujetoId == id && p.RevocadaEn == null).ToListAsync(ct);
        foreach (var llave in llaves)
        {
            llave.Revocar(por, "Operador desactivado", ahora);
            sesion.Store(llave);
            RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.PasskeyRevocada, true, cliente, ahora, null, TipoDePropietario.Operador, id, operador.Correo,
                actorTipo: contexto.ActorTipo, actorId: contexto.ActorId, passkeyId: llave.Id, motivo: "Operador desactivado", detalle: llave.Nombre));
        }

        await sesion.SaveChangesAsync(ct);
        foreach (var cerrada in cerradas)
            cache.Olvidar(cerrada);
        cacheSistemas.OlvidarOperador(id);
        return operador.ADto(0);
    }

    /// <summary>
    /// Vuelve a activar al operador (PI-7) y le envía el correo para entrar: como sus passkeys se revocaron al desactivarlo, entra con el código y registra una nueva. 409
    /// <c>operador-activo</c> si ya estaba activo. Exige reautenticar con la passkey.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/operadores/{id}/reactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<OperadorDePlataformaDto> Reactivar(string id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IProtectorDeSecretos protector, ICacheDeSistemas cacheSistemas, ServicioDeIngreso servicio, RegistroDeIngresos registro, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "reactivar-operador", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global(mensajes);
        var operador = await sesion.LoadAsync<OperadorDePlataforma>(id, ct) ?? throw ReglaDeNegocioException.NoEncontrado("operador-no-encontrado", "No existe ese operador.");
        var antes = operador.ADto(0);
        operador.Reactivar();
        sesion.Store(operador);
        Auditoria.Registrar(sesion, contexto, Entidad, operador.Id, AccionConfiguracion.Reactivado, antes, operador.ADto(0), ahora);
        if (operador.EsDirecto)
            await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.InvitacionDeUsuario, operador.Correo, protector.Proteger("-"), ahora, "Plataforma de Validasoft",
                "Operador de Plataforma"));
        await sesion.SaveChangesAsync(ct);
        cacheSistemas.OlvidarOperador(id);
        await registro.RegistrarAsync(EventoDeIngreso.UsuarioInvitado, true, ContextoDeCliente.De(http, contexto.CorrelationId), null, TipoDePropietario.Operador, operador.Id,
            operador.Correo, actorTipo: contexto.ActorTipo, actorId: contexto.ActorId, detalle: "Operador reactivado", ct: ct);
        return operador.ADto(0);
    }

    [WolverineGet("/api/v1/plataforma/operadores/{id}/passkeys")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<PasskeyDto>> Passkeys(string id, ISesiones sesiones, CancellationToken ct)
    {
        await CargarAsync(sesiones, id, ct);
        await using var consulta = sesiones.ConsultaGlobal();
        return [.. (await consulta.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Operador && p.SujetoId == id).OrderBy(p => p.CreadaEn).ToListAsync(ct))
            .Select(p => p.ADto())];
    }

    /// <summary>Revoca una passkey del operador (la perdió): deja de valer y se cierran sus sesiones; entra con el código y registra otra.</summary>
    [WolverinePost("/api/v1/plataforma/operadores/{id}/passkeys/{passkeyId}/revocar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PasskeyDto> RevocarPasskey(string id, Guid passkeyId, RevocarPasskeyDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSesionesDirectas cache, CuentasDeIngreso cuentas, ServicioDeIngreso servicio, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "revocar-passkey", ct);
        await CargarAsync(sesiones, id, ct);
        return await GestionDeIngresoEndpoints.RevocarAsync(passkeyId, new CuentaDeIngreso(TipoDePropietario.Operador, null, id), dto.Motivo, http, sesiones, contexto, cache,
            cuentas, mensajes, reloj, ct);
    }

    [WolverineGet("/api/v1/plataforma/operadores/{id}/sesiones")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<SesionDeUsuarioDto>> Sesiones(string id, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        await CargarAsync(sesiones, id, ct);
        return await MiCuentaEndpoints.SesionesActivasAsync(sesiones, new CuentaDeIngreso(TipoDePropietario.Operador, null, id), null, reloj.GetUtcNow(), ct);
    }

    [WolverinePost("/api/v1/plataforma/operadores/{id}/sesiones/{sesionId}/cerrar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> CerrarSesion(string id, Guid sesionId, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSesionesDirectas cache,
        TimeProvider reloj, CancellationToken ct)
    {
        await CargarAsync(sesiones, id, ct);
        await using var sesion = sesiones.Global();
        var objetivo = await sesion.LoadAsync<SesionDeUsuario>(sesionId, ct);
        if (objetivo is null || objetivo.Propietario != TipoDePropietario.Operador || objetivo.SujetoId != id)
            throw ReglaDeNegocioException.NoEncontrado("sesion-no-encontrada", "No existe esa sesión.");

        await GestionDeSesiones.CerrarAsync(sesion, objetivo, SesionDeUsuario.MotivoAdministrador, $"{contexto.ActorTipo}:{contexto.ActorId}",
            ContextoDeCliente.De(http, contexto.CorrelationId), contexto.ActorTipo, contexto.ActorId, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(sesionId);
        return Results.NoContent();
    }

    private static async Task CargarAsync(ISesiones sesiones, string id, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        if (await consulta.LoadAsync<OperadorDePlataforma>(id, ct) is null)
            throw ReglaDeNegocioException.NoEncontrado("operador-no-encontrado", "No existe ese operador.");
    }
}
