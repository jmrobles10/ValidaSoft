using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Quién hace la petición cuando entró directo: su sesión (por el reclamo de la familia) y su cuenta al día.</summary>
public static class SesionPropia
{
    /// <summary>
    /// La sesión directa de la petición y su cuenta. Con otra credencial (API key, identidad delegada) ⇒ 403 <c>solo-ingreso-directo</c>; con una
    /// sesión ya cerrada o vencida ⇒ 401 <c>sesion-revocada</c>.
    /// </summary>
    public static async Task<(SesionDeUsuario Sesion, CuentaResuelta Cuenta)> CargarAsync(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas,
        TimeProvider reloj, CancellationToken ct)
    {
        if (!Guid.TryParse(http.User.FindFirstValue(ReclamosDeIngreso.SesionId), out var id))
            throw ReglaDeNegocioException.Prohibido("solo-ingreso-directo", "Esta pantalla es de quienes entraron directo a Validasoft (no desde un sistema integrado).");

        await using var consulta = sesiones.ConsultaGlobal();
        var sesion = await consulta.LoadAsync<SesionDeUsuario>(id, ct);
        if (sesion is null || !sesion.VigenteEn(reloj.GetUtcNow()))
            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "Tu sesión se cerró. Ingresa de nuevo.");

        var cuenta = await cuentas.CargarAsync(new CuentaDeIngreso(sesion.Propietario, sesion.TenantId, sesion.SujetoId), ct)
                     ?? throw ReglaDeNegocioException.NoAutenticado("cuenta-no-disponible", "Tu cuenta ya no puede ingresar directo.");
        return (sesion, cuenta);
    }
}

/// <summary>
/// Mi cuenta (ING-3): la persona que entró directo ve y cierra sus sesiones y registra, renombra y elimina sus passkeys. Solo con una sesión directa
/// (<c>validasoft:sesion-id</c>); con otra credencial, 403 <c>solo-ingreso-directo</c>.
/// </summary>
public static class MiCuentaEndpoints
{
    [WolverineGet("/api/v1/mi-cuenta")]
    [Authorize]
    public static async Task<MiCuentaDto> Obtener(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, TimeProvider reloj, CancellationToken ct)
    {
        var (sesion, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return new MiCuentaDto(cuenta.Cuenta.Tipo.ToString(), cuenta.Cuenta.SujetoId, cuenta.Nombre, cuenta.Correo, cuenta.Grupo,
            [.. (cuenta.Usuario?.Roles ?? []).Select(r => r.ADto())], cuenta.ExigePasskey, sesion.Metodo.ToString(), sesion.Id);
    }

    /// <summary>Mis passkeys activas, de la más antigua a la más nueva.</summary>
    [WolverineGet("/api/v1/mi-cuenta/passkeys")]
    [Authorize]
    public static async Task<IReadOnlyList<PasskeyDto>> ListarPasskeys(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, TimeProvider reloj,
        CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return [.. (await cuentas.PasskeysAsync(cuenta.Cuenta, ct)).Select(p => p.ADto())];
    }

    /// <summary>Opciones de WebAuthn para registrar otra passkey (excluye las que ya tengo en ese dispositivo).</summary>
    [WolverinePost("/api/v1/mi-cuenta/passkeys/opciones")]
    [Authorize]
    public static async Task<OpcionesDePasskeyDto> OpcionesDeRegistro(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, ServicioDeIngreso servicio,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return await servicio.PrepararRegistroAsync(cuenta, ContextoDeCliente.De(http, contexto.CorrelationId), ct);
    }

    /// <summary>Verifica la respuesta del navegador y guarda la passkey. 401 <c>passkey-invalida</c>; 409 <c>passkey-duplicada</c> o <c>passkeys-excesivas</c>.</summary>
    [WolverinePost("/api/v1/mi-cuenta/passkeys")]
    [Authorize]
    public static async Task<PasskeyDto> RegistrarPasskey(RegistrarPasskeyDto dto, HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas,
        ServicioDeIngreso servicio, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return (await servicio.ConfirmarRegistroAsync(cuenta, dto, ContextoDeCliente.De(http, contexto.CorrelationId), ct)).ADto();
    }

    [WolverinePut("/api/v1/mi-cuenta/passkeys/{id}")]
    [Authorize]
    public static async Task<PasskeyDto> Renombrar(Guid id, RenombrarPasskeyDto dto, HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas,
        TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        await using var sesion = sesiones.Global();
        var passkey = await PropiaAsync(sesion, cuenta, id, ct);
        passkey.Renombrar(dto.Nombre);
        sesion.Store(passkey);
        await sesion.SaveChangesAsync(ct);
        return passkey.ADto();
    }

    /// <summary>
    /// Elimina (revoca) una de mis passkeys. Si mi cuenta exige passkey no puedo quedarme sin ninguna: 409 <c>passkey-obligatoria</c> (registra otra antes).
    /// </summary>
    [WolverineDelete("/api/v1/mi-cuenta/passkeys/{id}")]
    [Authorize]
    public static async Task<IResult> Eliminar(Guid id, HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        await using var sesion = sesiones.Global();
        var passkey = await PropiaAsync(sesion, cuenta, id, ct);
        if (cuenta.ExigePasskey && (await cuentas.PasskeysAsync(cuenta.Cuenta, ct)).Count <= 1)
            throw ReglaDeNegocioException.Conflicto("passkey-obligatoria",
                "Tu cuenta exige passkey: registra otra antes de eliminar la única que tienes.");

        var ahora = reloj.GetUtcNow();
        passkey.Revocar($"{cuenta.Cuenta.Tipo}:{cuenta.Cuenta.SujetoId}", "Eliminada por su dueño", ahora);
        sesion.Store(passkey);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.PasskeyEliminada, true, ContextoDeCliente.De(http, contexto.CorrelationId), ahora,
            cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId, cuenta.Correo, passkeyId: passkey.Id, detalle: passkey.Nombre));
        await sesion.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    /// <summary>Mis sesiones abiertas (la actual marcada), de la más reciente a la más antigua.</summary>
    [WolverineGet("/api/v1/mi-cuenta/sesiones")]
    [Authorize]
    public static async Task<IReadOnlyList<SesionDeUsuarioDto>> ListarSesiones(HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, TimeProvider reloj,
        CancellationToken ct)
    {
        var (actual, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        return await SesionesActivasAsync(sesiones, cuenta.Cuenta, actual.Id, reloj.GetUtcNow(), ct);
    }

    /// <summary>Cierra una de mis sesiones (en otro dispositivo, o esta misma).</summary>
    [WolverinePost("/api/v1/mi-cuenta/sesiones/{id}/cerrar")]
    [Authorize]
    public static async Task<IResult> CerrarSesion(Guid id, HttpContext http, ISesiones sesiones, CuentasDeIngreso cuentas, IContextoSolicitud contexto,
        ICacheDeSesionesDirectas cache, TimeProvider reloj, CancellationToken ct)
    {
        var (_, cuenta) = await SesionPropia.CargarAsync(http, sesiones, cuentas, reloj, ct);
        await using var sesion = sesiones.Global();
        var objetivo = await sesion.LoadAsync<SesionDeUsuario>(id, ct);
        if (objetivo is null || objetivo.Propietario != cuenta.Cuenta.Tipo || objetivo.SujetoId != cuenta.Cuenta.SujetoId)
            throw ReglaDeNegocioException.NoEncontrado("sesion-no-encontrada", "No existe esa sesión.");

        await GestionDeSesiones.CerrarAsync(sesion, objetivo, SesionDeUsuario.MotivoCierre, $"{cuenta.Cuenta.Tipo}:{cuenta.Cuenta.SujetoId}",
            ContextoDeCliente.De(http, contexto.CorrelationId), ActorTipo.Usuario, cuenta.Cuenta.SujetoId, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar(id);
        return Results.NoContent();
    }

    private static async Task<Passkey> PropiaAsync(IDocumentSession sesion, CuentaResuelta cuenta, Guid id, CancellationToken ct)
    {
        var passkey = await sesion.LoadAsync<Passkey>(id, ct);
        return passkey is { Activa: true } && passkey.Propietario == cuenta.Cuenta.Tipo && passkey.SujetoId == cuenta.Cuenta.SujetoId
            ? passkey
            : throw ReglaDeNegocioException.NoEncontrado("passkey-no-encontrada", "No existe esa passkey.");
    }

    /// <summary>Sesiones abiertas de una cuenta, de la más reciente a la más antigua.</summary>
    public static async Task<IReadOnlyList<SesionDeUsuarioDto>> SesionesActivasAsync(ISesiones sesiones, CuentaDeIngreso cuenta, Guid? actual, DateTimeOffset ahora,
        CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var sujeto = cuenta.SujetoId;
        var tipo = cuenta.Tipo;
        var lista = await consulta.Query<SesionDeUsuario>().Where(s => s.Propietario == tipo && s.SujetoId == sujeto && s.RevocadaEn == null)
            .OrderByDescending(s => s.UltimaActividad).ToListAsync(ct);
        return [.. lista.Where(s => s.VigenteEn(ahora)).Select(s => s.ADto(actual, ahora))];
    }
}

/// <summary>Cierre y revocación de sesiones directas, con su auditoría, para su dueño, un administrador del grupo o Plataforma.</summary>
public static class GestionDeSesiones
{
    /// <summary>Revoca la sesión y deja el registro; el llamador guarda y olvida la caché.</summary>
    public static void CerrarSinGuardar(IDocumentSession sesion, SesionDeUsuario objetivo, string motivo, string por, ContextoDeCliente cliente, ActorTipo actorTipo,
        string actorId, DateTimeOffset ahora)
    {
        objetivo.Revocar(motivo, por, ahora);
        sesion.Store(objetivo);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(
            motivo == SesionDeUsuario.MotivoCierre ? EventoDeIngreso.SesionCerrada : EventoDeIngreso.SesionRevocada, true, cliente, ahora, objetivo.TenantId,
            objetivo.Propietario, objetivo.SujetoId, objetivo.Correo, objetivo.Metodo, motivo, actorTipo, actorId, objetivo.Id));
    }

    public static Task CerrarAsync(IDocumentSession sesion, SesionDeUsuario objetivo, string motivo, string por, ContextoDeCliente cliente, ActorTipo actorTipo,
        string actorId, DateTimeOffset ahora)
    {
        if (!objetivo.Revocada)
            CerrarSinGuardar(sesion, objetivo, motivo, por, cliente, actorTipo, actorId, ahora);
        return Task.CompletedTask;
    }

    /// <summary>Revoca todas las sesiones abiertas de una cuenta (desactivación, passkey revocada, modo apagado).</summary>
    public static async Task<IReadOnlyList<Guid>> CerrarTodasAsync(IDocumentSession sesion, TipoDePropietario tipo, string sujetoId, string motivo, string por,
        ContextoDeCliente cliente, ActorTipo actorTipo, string actorId, DateTimeOffset ahora, CancellationToken ct)
    {
        var abiertas = await sesion.Query<SesionDeUsuario>().Where(s => s.Propietario == tipo && s.SujetoId == sujetoId && s.RevocadaEn == null).ToListAsync(ct);
        foreach (var s in abiertas)
            CerrarSinGuardar(sesion, s, motivo, por, cliente, actorTipo, actorId, ahora);
        return [.. abiertas.Select(s => s.Id)];
    }
}
