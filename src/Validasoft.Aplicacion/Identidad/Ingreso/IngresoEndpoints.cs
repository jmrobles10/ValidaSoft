using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Ingreso directo sin contraseñas (ING-1…ING-4, docs/05 §2.10): anónimo, con el límite de peticiones por IP de <c>/api/v1/ingreso</c> y el
/// de códigos por correo. Ninguna respuesta revela si un correo tiene cuenta. El token de refresco viaja solo en la cookie
/// <c>vs_usuario</c>; el de acceso, en el cuerpo (en memoria del navegador).
/// </summary>
public static class IngresoEndpoints
{
    /// <summary>
    /// Pide un código de 6 dígitos al correo (vence en 5 minutos, 5 por correo y hora). Responde siempre igual, tenga o no cuenta con ingreso
    /// directo ese correo; el desafío que devuelve es la capacidad para verificar. 422 <c>correo-invalido</c> solo por el formato.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/codigo")]
    [AllowAnonymous]
    public static Task<CodigoSolicitadoDto> SolicitarCodigo(SolicitarCodigoDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        IMessageBus mensajes, CancellationToken ct) =>
        servicio.SolicitarCodigoAsync(dto.Correo, ContextoDeCliente.De(http, contexto.CorrelationId), mensajes, ct);

    /// <summary>
    /// Verifica el código. Siguiente paso en <c>estado</c>: <c>ElegirCuenta</c>, <c>PasskeyRequerida</c>, <c>RegistrarPasskey</c> o <c>Sesion</c>. 422
    /// <c>codigo-invalido</c> (con <c>intentosRestantes</c>), 410 <c>codigo-vencido</c>, 429 <c>codigo-bloqueado</c>, 404 <c>desafio-no-encontrado</c>.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/codigo/verificar")]
    [AllowAnonymous]
    public static Task<PasoDeIngresoDto> VerificarCodigo(VerificarCodigoDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        CancellationToken ct) =>
        servicio.VerificarCodigoAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct);

    /// <summary>Con varias cuentas para el mismo correo (grupos distintos, operador de Plataforma), elige con cuál continúa el ingreso.</summary>
    [WolverinePost("/api/v1/ingreso/cuenta")]
    [AllowAnonymous]
    public static Task<PasoDeIngresoDto> ElegirCuenta(ElegirCuentaDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        CancellationToken ct) =>
        servicio.ElegirCuentaAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct);

    /// <summary>Opciones de WebAuthn para entrar solo con una passkey (sin correo ni código): el navegador ofrece las que tenga para este sitio.</summary>
    [WolverinePost("/api/v1/ingreso/passkey/opciones")]
    [AllowAnonymous]
    public static Task<OpcionesDePasskeyDto> OpcionesDeIngreso(HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio, CancellationToken ct) =>
        servicio.OpcionesDeIngresoAsync(ContextoDeCliente.De(http, contexto.CorrelationId), ct);

    /// <summary>
    /// Verifica la passkey (ingreso solo con passkey, o el segundo paso tras el código) y abre la sesión. 401 <c>passkey-invalida</c>,
    /// <c>passkey-revocada</c> o <c>passkey-clonada</c>; 403 <c>cuenta-no-disponible</c>.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/passkey/verificar")]
    [AllowAnonymous]
    public static Task<PasoDeIngresoDto> VerificarPasskey(VerificarPasskeyDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        CancellationToken ct) =>
        servicio.VerificarPasskeyAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct);

    /// <summary>
    /// Registra la primera passkey de una cuenta que la exige (alta o recuperación) en el mismo ingreso y abre la sesión. 401
    /// <c>passkey-invalida</c>; 409 <c>passkey-duplicada</c>.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/passkey/registro")]
    [AllowAnonymous]
    public static Task<PasoDeIngresoDto> RegistrarPasskey(RegistrarPasskeyDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        CancellationToken ct) =>
        servicio.RegistrarPasskeyAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct);

    /// <summary>
    /// Renueva el token de acceso con la cookie <c>vs_usuario</c> y la rota. Exige <c>X-Validasoft-Cliente: web</c> (defensa CSRF además de
    /// <c>SameSite=Strict</c>). 401 <c>sin-sesion</c> (no hay cookie), <c>sesion-expirada</c>, <c>sesion-revocada</c> (también por reuso de una cookie ya rotada) o <c>sesion-inactiva</c>;
    /// 409 <c>renovacion-concurrente</c>.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/sesion/renovar")]
    [AllowAnonymous]
    public static Task<SesionDirectaDto> Renovar(HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio, CancellationToken ct) =>
        servicio.RenovarAsync(http, ContextoDeCliente.De(http, contexto.CorrelationId), ct);

    /// <summary>204: revoca la sesión de la cookie (si la hay) y la borra. Idempotente.</summary>
    [WolverinePost("/api/v1/ingreso/sesion/cerrar")]
    [AllowAnonymous]
    public static async Task<IResult> Cerrar(HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio, CancellationToken ct)
    {
        await servicio.CerrarAsync(http, ContextoDeCliente.De(http, contexto.CorrelationId), ct);
        return Results.NoContent();
    }
}
