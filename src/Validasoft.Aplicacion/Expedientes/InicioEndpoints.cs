using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Inicio de la empresa activa (IN-1, IN-2, docs/06 <c>/inicio</c>): lo que le toca a quien consulta y los indicadores de
/// la empresa. Se calculan al consultar sobre la bandeja, los vínculos, los expedientes y los certificados de la empresa
/// (aún no son las proyecciones <c>KpisEmpresa</c>/<c>MisPendientes</c> de docs/03 §14).
/// </summary>
public static class InicioEndpoints
{
    [WolverineGet("/api/v1/mis-pendientes")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<MisPendientesDto> MisPendientes(SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var empresaId = empresa.EmpresaId;
        var usuario = contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var id) ? id : (Guid?)null;
        var areas = await empresa.Areas().Where(a => a.Activa).ToListAsync(ct);
        var efectivas = usuario is { } u ? await Delegaciones.DelegacionesDeRevision.AreasQueRevisaAsync(empresa.Sesion, areas, empresaId, u, empresa.Hoy, ct) : null;
        var mias = efectivas is null ? areas : areas.Where(a => efectivas.Contains(a.Id)).ToList();
        var ids = mias.Select(a => a.Id).ToArray();

        var filas = ids.Length == 0
            ? []
            : await empresa.Sesion.Query<FilaBandeja>()
                .Where(f => f.EmpresaId == empresaId && f.EstadoExpediente == EstadoExpediente.EnRevision && f.EstadoArea == EstadoAreaExpediente.Pendiente &&
                            f.AreaId.IsOneOf(ids))
                .ToListAsync(ct);
        var porArea = mias
            .Select(a =>
            {
                var suyas = filas.Where(f => f.AreaId == a.Id).ToList();
                var antiguo = suyas.Where(f => f.Desde is not null).Select(f => f.Desde!.Value).DefaultIfEmpty(ahora).Min();
                return new PendientesDeAreaDto(a.Id, a.Codigo, a.Nombre, suyas.Count, suyas.Count == 0 ? 0 : (int)Math.Floor((ahora - antiguo).TotalDays));
            })
            .OrderByDescending(p => p.Cantidad).ThenBy(p => p.Nombre)
            .ToList();

        var revalidaciones = (await RevalidacionesEndpoints.Listar(empresa, contexto, reloj, null, 200, null, ct)).Datos;

        var cuentas = 0;
        if (contexto.Scopes.Contains(Permisos.CuentasAprobar) && usuario is { } aprobador)
        {
            var conPropuesta = await empresa.Vinculos().Where(v => v.CuentaPropuesta != null).ToListAsync(ct);
            cuentas = conPropuesta.Count(v => v.CuentaPropuesta!.Aprobaciones.All(a => a.UsuarioId != aprobador.ToString()));
        }

        var aperturas = contexto.Scopes.Contains(Permisos.ExpedientesAdministrar)
            ? await empresa.Vinculos()
                .Where(v => v.InvitacionAceptada && v.Estado == EstadoVinculo.Invitado && v.ExpedienteAbiertoId == null && v.UltimoExpedienteId == null)
                .CountAsync(ct)
            : 0;

        var vencimientos = contexto.Scopes.Contains(Permisos.DocumentosLeer)
            ? await VencimientosEndpoints.Listar(empresa, reloj, 30, null, ct)
            : [];
        return new MisPendientesDto(porArea, revalidaciones.Count, revalidaciones.Count(r => r.Bloqueante), cuentas, aperturas,
            vencimientos.Count(v => v.Estado is nameof(EstadoVigencia.PorVencer) or nameof(EstadoVigencia.EnGracia)),
            vencimientos.Count(v => v.Estado == nameof(EstadoVigencia.Vencida)));
    }

    /// <summary>
    /// <paramref name="dias"/>: ventana de los indicadores de flujo (30 por defecto, de 1 a 365), en días de calendario de la zona
    /// del negocio: hoy y los <paramref name="dias"/> - 1 anteriores. Es el mismo rango del informe de estado con
    /// <c>fecha=Cierre&amp;desde=…</c>, y los tiempos salen de la misma medida (<see cref="Informes.Estadistica"/>, IF-7).
    /// </summary>
    [WolverineGet("/api/v1/indicadores")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IndicadoresDto> Indicadores(SesionDeEmpresa empresa, TimeProvider reloj, int? dias, CancellationToken ct)
    {
        var ventana = Math.Clamp(dias ?? 30, 1, 365);
        var desde = Informes.Exportaciones.InicioDelDia(CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()).AddDays(1 - ventana));
        var empresaId = empresa.EmpresaId;

        // «En revisión» (VR-1) es lo que la pantalla muestra de un vínculo con una actualización abierta: ya no cuenta como Activo, Por vencer
        // ni Vencido (así las cifras suman lo mismo que la lista filtrada), y tiene la suya.
        var vinculos = await empresa.Vinculos().Select(v => v.Estado).ToListAsync(ct);
        var enRevision = await empresa.Vinculos().Where(Vinculo.EsEnRevisionPorActualizacion).Select(v => v.Estado).ToListAsync(ct);
        int Vinculos(EstadoVinculo estado) => vinculos.Count(e => e == estado) - enRevision.Count(e => e == estado);

        var abiertos = await empresa.Expedientes()
            .Where(e => e.Estado == EstadoExpediente.Abierto || e.Estado == EstadoExpediente.EnRevision || e.Estado == EstadoExpediente.Observado)
            .Select(e => e.Estado)
            .ToListAsync(ct);
        var cerrados = await empresa.Expedientes()
            .Where(e => e.CerradoEn != null && e.CerradoEn >= desde && (e.Estado == EstadoExpediente.Aprobado || e.Estado == EstadoExpediente.Rechazado))
            .ToListAsync(ct);
        var tiempos = cerrados.Where(e => e.Estado == EstadoExpediente.Aprobado)
            .Select(e => (e.CerradoEn!.Value - e.AbiertoEn).TotalDays)
            .Order()
            .ToList();

        var certificados = await empresa.Sesion.Query<Certificado>().Where(c => c.EmpresaId == empresaId).ToListAsync(ct);
        return new IndicadoresDto(ventana,
            Vinculos(EstadoVinculo.Activo) + Vinculos(EstadoVinculo.PorVencer), Vinculos(EstadoVinculo.EnValidacion), Vinculos(EstadoVinculo.Invitado),
            Vinculos(EstadoVinculo.Vencido), Vinculos(EstadoVinculo.Bloqueado),
            abiertos.Count(e => e != EstadoExpediente.Observado), abiertos.Count(e => e == EstadoExpediente.Observado),
            cerrados.Count(e => e.Estado == EstadoExpediente.Aprobado), cerrados.Count(e => e.Estado == EstadoExpediente.Rechazado),
            Informes.Estadistica.Promedio(tiempos), Informes.Estadistica.Mediana(tiempos),
            certificados.Count(c => c.Estado == EstadoCertificado.Vigente),
            certificados.Count(c => c.Estado == EstadoCertificado.ConPendientes),
            certificados.Count(c => c.Bloqueante),
            Vinculos(EstadoVinculo.PorVencer), enRevision.Count);
    }
}
