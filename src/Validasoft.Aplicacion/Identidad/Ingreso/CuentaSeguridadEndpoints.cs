using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Seguridad de la cuenta de quien entró directo (Mi cuenta): reautenticación con la passkey para las acciones sensibles (step-up, PI-13) y códigos de recuperación de
/// un solo uso que respaldan la passkey (PI-18). Solo con una sesión directa; con otra credencial, 403 <c>solo-ingreso-directo</c>.
/// </summary>
public static class MiCuentaSeguridadEndpoints
{
    /// <summary>Si la sesión está reautenticada ahora (y hasta cuándo): lo que la pantalla mira antes de ofrecer una acción sensible.</summary>
    [WolverineGet("/api/v1/mi-cuenta/reautenticacion")]
    [Authorize]
    public static async Task<EstadoDeReautenticacionDto> Estado(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio, TimeProvider reloj,
        CancellationToken ct)
    {
        var (sesion, _) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return servicio.EstadoDeReautenticacion(sesion);
    }

    /// <summary>Opciones de WebAuthn para confirmar con una de mis passkeys. 409 <c>sin-passkey</c> si no tengo ninguna.</summary>
    [WolverinePost("/api/v1/mi-cuenta/reautenticacion/opciones")]
    [Authorize]
    public static async Task<OpcionesDePasskeyDto> Opciones(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var (sesion, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return await servicio.OpcionesDeReautenticacionAsync(cuenta, sesion, ContextoDeCliente.De(http, contexto.CorrelationId), ct);
    }

    /// <summary>
    /// Verifica la passkey y deja la sesión reautenticada unos minutos: las acciones sensibles (rotar llaves, cambiar roles, desactivar…) las aceptan hasta entonces. 401
    /// <c>passkey-invalida</c>, <c>passkey-revocada</c>, <c>passkey-clonada</c>; 410 <c>desafio-usado</c>.
    /// </summary>
    [WolverinePost("/api/v1/mi-cuenta/reautenticacion")]
    [Authorize]
    public static async Task<ReautenticacionDto> Confirmar(VerificarPasskeyDto dto, HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var (sesion, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return await servicio.ConfirmarReautenticacionAsync(cuenta, sesion, dto, ContextoDeCliente.De(http, contexto.CorrelationId), ct);
    }

    /// <summary>Cuántos códigos de recuperación me quedan (nunca los códigos).</summary>
    [WolverineGet("/api/v1/mi-cuenta/codigos-de-recuperacion")]
    [Authorize]
    public static async Task<EstadoDeRecuperacionDto> EstadoDeRecuperacion(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio,
        TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return await servicio.EstadoDeRecuperacionAsync(cuenta, ct);
    }

    /// <summary>
    /// Genera un juego nuevo de códigos de recuperación (los anteriores dejan de valer) y los muestra <b>una sola vez</b>. Exige reautenticar con la passkey (403
    /// <c>reautenticacion-requerida</c>) y tener al menos una passkey (409 <c>sin-passkey</c>); avisa por correo.
    /// </summary>
    [WolverinePost("/api/v1/mi-cuenta/codigos-de-recuperacion")]
    [Authorize]
    public static async Task<CodigosDeRecuperacionDto> Generar(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio, IContextoSolicitud contexto,
        IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        await servicio.ExigirReautenticacionAsync(http.User, "codigos-de-recuperacion", ct);
        return await servicio.GenerarCodigosDeRecuperacionAsync(cuenta, ContextoDeCliente.De(http, contexto.CorrelationId), mensajes, ct);
    }
}
