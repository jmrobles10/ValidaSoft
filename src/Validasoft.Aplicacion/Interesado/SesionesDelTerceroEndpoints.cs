using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// «Mis sesiones»: las sesiones abiertas del tercero (una por navegador y correo) y el cierre individual de cada una. Cerrar una la revoca de inmediato: su token de
/// acceso deja de valer en segundos y en todos los servidores, no al vencer los 30 minutos (<see cref="ICacheDeSesionesDeTercero"/>), y su cookie de refresco ya no
/// renueva. Solo una sesión con acceso completo ve y cierra sesiones: una aislada no toca las del tercero entero.
/// </summary>
public static class SesionesDelTerceroEndpoints
{
    /// <summary>Las sesiones abiertas del tercero (todas sus personas), la más reciente primero. <c>esActual</c> marca la de este token.</summary>
    [WolverineGet("/public/v1/sesiones")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<SesionActivaDto>> Listar(HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var terceroId = tercero.TerceroId;
        var ahora = reloj.GetUtcNow();
        await using var consulta = sesiones.ConsultaGlobal();
        var abiertas = await consulta.Query<SesionDeRefresco>()
            .Where(s => s.TerceroId == terceroId && s.RevocadaEn == null && s.ExpiraEn > ahora)
            .OrderByDescending(s => s.CreadaEn)
            .ToListAsync(ct);
        return
        [
            .. abiertas.Select(s => new SesionActivaDto(s.Id, s.Correo, s.CreadaEn, s.ExpiraEn, s.UltimoUsoEn ?? s.RotadaEn ?? s.CreadaEn, s.Dispositivo, s.Ip,
                s.Id == tercero.SesionId))
        ];
    }

    /// <summary>
    /// Cierra una sesión del tercero (204). Una que no es de este tercero responde 404; una ya cerrada o vencida, también (no hay nada que cerrar). Cerrar la propia
    /// sesión borra además su cookie.
    /// </summary>
    [WolverinePost("/public/v1/sesiones/{id}/cerrar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Cerrar(Guid id, HttpContext http, ISesiones sesiones, ICacheDeSesionesDeTercero cache, CookieDeSesionTercero cookie,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Cuenta);
        var ahora = reloj.GetUtcNow();
        for (var intento = 1; ; intento++)
        {
            await using var sesion = sesiones.Global();
            var refresco = await sesion.LoadAsync<SesionDeRefresco>(id, ct);
            if (refresco is null || refresco.TerceroId != tercero.TerceroId || !refresco.VigenteEn(ahora))
                throw ReglaDeNegocioException.NoEncontrado("sesion-no-encontrada", "No existe esa sesión abierta.");

            refresco.Revocar(SesionDeRefresco.MotivoCierreIndividual, ahora);
            sesion.Store(refresco);
            try
            {
                await sesion.SaveChangesAsync(ct);
            }
            catch (Exception e) when (intento < 5 && Peticiones.EsCarrera(e))
            {
                // Una renovación simultánea rotó el token: se revoca sobre la versión actual.
                continue;
            }

            break;
        }

        cache.OlvidarSesion(id);
        if (id == tercero.SesionId)
            cookie.Borrar(http.Response);
        return Results.NoContent();
    }
}
