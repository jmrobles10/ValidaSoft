using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Reinicio tras un rechazo (DS-4). El rechazo de un expediente deja el vínculo en <c>Invitado</c>: antes hacía falta que la
/// empresa abriera uno nuevo a mano o enviara otra invitación. Ahora el tercero ve en su portal el rechazo con los motivos que
/// se le comunicaron y, si el flujo lo permite (<see cref="ReintentoTrasRechazo"/>, con su espera en días), puede «Volver a
/// postularme»: se abre un expediente inicial nuevo con el flujo vigente. La empresa puede abrirlo cuando quiera desde la web
/// (<c>POST /vinculos/{id}/expedientes</c>), sin esperar ni depender del flujo.
/// </summary>
public static class ReintentoDelTercero
{
    /// <summary>
    /// ¿Hay un rechazo que reintentar? Vínculo en <c>Invitado</c> que el tercero ya aceptó, sin expediente abierto y cuyo último
    /// expediente se rechazó (si la empresa anuló o dejó caducar el expediente, no hay rechazo que mostrar).
    /// </summary>
    public static bool HayRechazo(Vinculo vinculo) =>
        vinculo is { Estado: EstadoVinculo.Invitado, InvitacionAceptada: true, ExpedienteAbiertoId: null, UltimoResultado: ResultadoExpediente.Rechazado } &&
        vinculo.UltimoExpedienteId is not null;

    /// <summary>
    /// Lo que el tercero ve del rechazo de cada vínculo suyo que lo tenga (por id de vínculo): fecha, motivos tal como se le
    /// comunicaron, si la empresa permite reintentar y desde cuándo. Con la política del flujo vigente de cada vínculo.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ReintentoPublicoDto>> CargarAsync(IQuerySession consulta, IReadOnlyList<VinculoVisto> vistos,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var rechazados = vistos.Select(v => v.Vinculo).Where(HayRechazo).ToList();
        if (rechazados.Count == 0)
            return new Dictionary<Guid, ReintentoPublicoDto>();

        var expedienteIds = rechazados.Select(v => v.UltimoExpedienteId!.Value).Distinct().ToArray();
        var expedientes = (await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id.IsOneOf(expedienteIds)).ToListAsync(ct))
            .ToDictionary(e => e.Id);

        var claves = rechazados.Select(v => FlujoPorDefecto.Clave(v.EmpresaId, v.TipoVinculoId)).Distinct().ToArray();
        var porDefecto = await consulta.Query<FlujoPorDefecto>().Where(p => p.AnyTenant() && p.Id.IsOneOf(claves)).ToListAsync(ct);
        // La política de reintento es la del flujo que el vínculo tendría hoy: el suyo, el que escoja una regla de selección (FR-1) o el por defecto.
        var flujos = await ProcesoDeValidacion.CargarFlujosDeLosVinculosAsync(consulta, rechazados, ct);
        var terceros = (await consulta.LoadManyAsync<Tercero>(ct, rechazados.Select(v => v.TerceroId).Distinct().ToArray())).ToDictionary(t => t.Id);
        var tipoIds = rechazados.Select(v => v.TipoVinculoId).Distinct().ToArray();
        var tiposVinculo = (await consulta.Query<TipoVinculo>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct)).ToDictionary(t => t.Id);

        var resultado = new Dictionary<Guid, ReintentoPublicoDto>();
        foreach (var vinculo in rechazados)
        {
            if (!expedientes.TryGetValue(vinculo.UltimoExpedienteId!.Value, out var expediente) || expediente is not { Estado: EstadoExpediente.Rechazado, Rechazo: { } rechazo })
                continue;
            var contexto = terceros.TryGetValue(vinculo.TerceroId, out var tercero)
                ? ContextoDeExigencia.De(tercero, tiposVinculo.GetValueOrDefault(vinculo.TipoVinculoId)?.Codigo ?? string.Empty, vinculo.Atributos)
                : null;
            var politica = ProcesoDeValidacion.FlujoVigente(vinculo, flujos, porDefecto, contexto)?.ReintentoEfectivo() ?? ReintentoTrasRechazo.PorDefecto;
            var desde = politica.PuedeDesde(rechazo.Fecha);
            resultado[vinculo.Id] = new ReintentoPublicoDto(rechazo.Fecha, [.. rechazo.Motivos.Select(m => new MotivoPublicoDto(m.Codigo, m.TextoAlTercero))],
                politica.Permitido, desde is { } d && d <= ahora, desde);
        }

        return resultado;
    }
}

/// <summary>El tercero rechazado vuelve a postularse (DS-4) desde su portal.</summary>
public static class ReintentoPublicoEndpoints
{
    /// <summary>
    /// «Volver a postularme»: abre un expediente inicial nuevo con el flujo vigente del vínculo, con los documentos que el tercero
    /// ya tiene compartidos. Solo con un rechazo vigente (409 <c>reintento-no-disponible</c>), si la empresa lo permite (403
    /// <c>reintento-no-permitido</c>), cumplida la espera (409 <c>reintento-en-espera</c> con la fecha en <c>desde</c>) y con el
    /// vínculo sin bloquear (409 <c>vinculo-bloqueado</c>). Responde el expediente nuevo.
    /// </summary>
    [WolverinePost("/public/v1/vinculos/{id}/reintentar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ExpedientePublicoDto> Reintentar(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        EmisorDeCertificados certificados, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        var enSesion = tercero.VinculoRequerido(id);
        // Si pierde una carrera (el folio del expediente o el propio vínculo), se repite completa con una sesión nueva.
        var abierto = await Peticiones.ConReintentosAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
            var stream = await sesion.Events.FetchForWriting<Vinculo>(id, ct);
            var vinculo = stream.Aggregate;
            if (vinculo is null || vinculo.TerceroId != tercero.TerceroId || vinculo.EmpresaId != enSesion.EmpresaId)
                throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");

            if (vinculo.Estado == EstadoVinculo.Bloqueado)
                throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "La empresa suspendió temporalmente esta validación.");
            if (!ReintentoDelTercero.HayRechazo(vinculo) || await sesion.LoadAsync<Expediente>(vinculo.UltimoExpedienteId!.Value, ct) is not { Estado: EstadoExpediente.Rechazado, Rechazo: { } rechazo })
                throw ReglaDeNegocioException.Conflicto("reintento-no-disponible", "No hay un rechazo del que volver a postularte con esta empresa.");

            var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
            var politica = flujo?.Version.ReintentoEfectivo() ?? ReintentoTrasRechazo.PorDefecto;
            if (!politica.Permitido)
                throw ReglaDeNegocioException.Prohibido("reintento-no-permitido", "La empresa no permite volver a postularse por tu cuenta: comunícate con ella.");
            if (politica.PuedeDesde(rechazo.Fecha) is { } desde && desde > ahora)
                throw new ReglaDeNegocioException(TipoFalla.Conflicto, "reintento-en-espera", "Aún no puedes volver a postularte con esta empresa.")
                {
                    Datos = new Dictionary<string, object?> { ["desde"] = desde }
                };

            var expediente = await ProcesoDeValidacion.AbrirAsync(sesion, stream, flujo!, contexto, certificados, ahora, ct);
            await sesion.SaveChangesAsync(ct);
            await using var boveda = sesiones.ConsultaBoveda();
            var documentacion = await DocumentacionDelExpediente.CargarAsync(sesion, boveda, expediente, reglas, CalculadoraDeVigencia.Fecha(ahora), ct);
            return expediente.APublico(await sesion.LoadAsync<Empresa>(expediente.EmpresaId, ct), documentacion);
        });

        // Lo compartido al abrir (sin outbox de la bóveda) calcula sus vigencias y su análisis por mensaje, como al aceptar la invitación.
        await mensajes.PublishAsync(new Documentos.CalcularVigenciasDelVinculo(tercero.TerceroId, id));
        await mensajes.PublishAsync(new Ia.AnalizarIaDelVinculo(tercero.TerceroId, id));
        return abierto;
    }
}
