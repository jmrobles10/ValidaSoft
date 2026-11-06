using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Configuracion;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Expedientes de la empresa activa (<c>X-Empresa</c>): apertura manual, consulta, veredictos de área, rechazo,
/// reenvío, anulación y acciones del administrador. Cada expediente es un stream <c>Expediente-{id}</c>; la lectura sale
/// de su proyección inline. Las escrituras aceptan <c>If-Match</c> con la versión y devuelven <c>ETag</c>.
/// </summary>
public static class ExpedientesEndpoints
{
    /// <summary>
    /// Abre manualmente el expediente inicial del vínculo (administrador o integrador), con el flujo indicado o el
    /// resuelto (del vínculo o por defecto). No exige que el tercero haya aceptado. Reintenta si pierde la carrera del folio.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/expedientes")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<IResult> Abrir(Guid id, AbrirExpedienteDto dto, ISesiones sesiones, IContextoSolicitud contexto, HttpContext http,
        TimeProvider reloj, CancellationToken ct)
    {
        var version = Peticiones.VersionEsperada(http);
        var resultado = await Peticiones.ConReintentosAsync(async () =>
        {
            await using var empresa = new SesionDeEmpresa(sesiones, contexto);
            var ahora = reloj.GetUtcNow();
            var vinculoStream = await empresa.VinculoParaEscribirAsync(id, version, ct);
            var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(empresa.Sesion, vinculoStream.Aggregate!, dto.FlujoId, ct)
                        ?? throw ReglaDeNegocioException.Validacion("falta-flujo-por-defecto",
                            "El vínculo no tiene flujo y su tipo no tiene flujo por defecto publicado en la empresa.");

            var abierto = await ProcesoDeValidacion.AbrirAsync(empresa.Sesion, vinculoStream, flujo, contexto, ahora, ct);
            await empresa.Sesion.SaveChangesAsync(ct);
            return await DetalleAsync(empresa, abierto.Id, ct);
        }, version is null ? 10 : 1);

        Peticiones.EscribirETag(http, resultado.Version);
        return Results.Created($"/api/v1/expedientes/{resultado.Id}", resultado);
    }

    /// <summary>Lista con filtros por estado, área y vínculo; cursor opaco.</summary>
    [WolverineGet("/api/v1/expedientes")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<PaginaDto<ExpedienteDto>> Listar(SesionDeEmpresa empresa, string? estado, Guid? area, Guid? vinculoId, int? limite,
        string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var consulta = empresa.Expedientes();

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoExpediente>(estado, true, out var estadoExpediente) || !Enum.IsDefined(estadoExpediente))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de expediente.");
            consulta = consulta.Where(e => e.Estado == estadoExpediente);
        }

        if (area is { } areaId)
            consulta = consulta.Where(e => e.AreaIds.Contains(areaId));
        if (vinculoId is { } vinculo)
            consulta = consulta.Where(e => e.VinculoId == vinculo);

        var pagina = await consulta.OrderBy(e => e.AbiertoEn).ThenBy(e => e.Id).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null;
        return new PaginaDto<ExpedienteDto>([.. pagina.Take(tamano).Select(e => e.ADto(new Dictionary<Guid, List<Guid>>()))], siguiente);
    }

    /// <summary>Estado, etapas y áreas con sus veredictos, aprobaciones y miembros que faltan por pronunciarse.</summary>
    [WolverineGet("/api/v1/expedientes/{id}")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<ExpedienteDto> Obtener(Guid id, SesionDeEmpresa empresa, HttpContext http, CancellationToken ct)
    {
        var detalle = await DetalleAsync(empresa, id, ct);
        Peticiones.EscribirETag(http, detalle.Version);
        return detalle;
    }

    [WolverineGet("/api/v1/expedientes/{id}/historial")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IReadOnlyList<EventoHistorialDto>> Historial(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        await empresa.CargarExpedienteAsync(id, ct);
        return Peticiones.Historial(await empresa.Sesion.Events.FetchStreamAsync(id, token: ct));
    }

    /// <summary>
    /// Veredicto de un miembro del área sobre el expediente completo. <c>Observado</c> exige motivos; con política
    /// <c>Inmediata</c> devuelve el expediente al tercero (correo por el outbox) y bloquea los demás veredictos (409).
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/areas/{areaId}/veredicto")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<ExpedienteDto> Veredicto(Guid id, Guid areaId, VeredictoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        if (!Enum.TryParse<Veredicto>(dto.Veredicto, true, out var veredicto) || !Enum.IsDefined(veredicto))
            throw ReglaDeNegocioException.Validacion("veredicto-invalido", "El veredicto es Aprobado u Observado (el rechazo es del expediente).");

        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        await AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
        var revisor = await Revisores.DelAreaAsync(empresa, contexto, areaId, ct);
        var motivos = await CatalogoDeMotivos.ResolverAsync(empresa.Sesion, dto.Motivos, AplicaMotivo.Observacion, ct);
        var eventos = stream.Aggregate!.EmitirVeredicto(areaId, revisor, veredicto, motivos, dto.Observacion, reloj.GetUtcNow());
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>Rechazo terminal por un área con <c>puedeRechazarExpediente</c> (motivos y justificación).</summary>
    [WolverinePost("/api/v1/expedientes/{id}/rechazar")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<ExpedienteDto> Rechazar(Guid id, RechazarExpedienteDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        await AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
        var revisor = await Revisores.DelAreaAsync(empresa, contexto, dto.AreaId, ct);
        var motivos = await CatalogoDeMotivos.ResolverAsync(empresa.Sesion, dto.Motivos, AplicaMotivo.Rechazo, ct);
        var eventos = stream.Aggregate!.Rechazar(dto.AreaId, revisor, motivos, dto.Justificacion, reloj.GetUtcNow());
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>El integrador responde a la observación en nombre del tercero; solo las áreas que observaron vuelven a revisar.</summary>
    [WolverinePost("/api/v1/expedientes/{id}/reenviar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<ExpedienteDto> Reenviar(Guid id, ReenviarExpedienteDto dto, SesionDeEmpresa empresa, HttpContext http,
        IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        await AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
        var eventos = stream.Aggregate!.Reenviar(OrigenReenvio.Integrador, dto.Respuesta, reloj.GetUtcNow());
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>Anula el expediente abierto (el vínculo vuelve a <c>Invitado</c> si estaba en validación).</summary>
    [WolverinePost("/api/v1/expedientes/{id}/anular")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<ExpedienteDto> Anular(Guid id, MotivoDto dto, SesionDeEmpresa empresa, HttpContext http, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        var eventos = new object[] { stream.Aggregate!.Anular(dto.Motivo, reloj.GetUtcNow()) };
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>Aprobación en nombre del área (origen <c>Administrador</c>), con justificación.</summary>
    [WolverinePost("/api/v1/expedientes/{id}/areas/{areaId}/aprobar-como-administrador")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<ExpedienteDto> AprobarComoAdministrador(Guid id, Guid areaId, JustificacionDto dto, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        await AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
        var eventos = stream.Aggregate!.AprobarComoAdministrador(areaId, contexto.ActorId, dto.Justificacion, reloj.GetUtcNow());
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>Anula un veredicto vigente del área (el indicado o el más reciente), con justificación. No revierte un rechazo.</summary>
    [WolverinePost("/api/v1/expedientes/{id}/areas/{areaId}/anular-veredicto")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<ExpedienteDto> AnularVeredicto(Guid id, Guid areaId, AnularVeredictoDto dto, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
        var eventos = stream.Aggregate!.AnularVeredicto(areaId, dto.VeredictoId, contexto.ActorId, dto.Justificacion, reloj.GetUtcNow());
        return await GuardarAsync(empresa, stream, eventos, mensajes, http, reloj, ct);
    }

    /// <summary>Un vínculo bloqueado no recibe decisiones sobre su expediente (B-5).</summary>
    private static async Task AsegurarVinculoNoBloqueadoAsync(SesionDeEmpresa empresa, Expediente expediente, CancellationToken ct)
    {
        var vinculo = await empresa.Sesion.LoadAsync<Vinculo>(expediente.VinculoId, ct);
        if (vinculo is { Estado: EstadoVinculo.Bloqueado })
            throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "El vínculo está bloqueado: su expediente no recibe decisiones.");
    }

    private static async Task<ExpedienteDto> GuardarAsync(SesionDeEmpresa empresa, JasperFx.Events.IEventStream<Expediente> stream,
        IReadOnlyList<object> eventos, IMessageBus mensajes, HttpContext http, TimeProvider reloj, CancellationToken ct)
    {
        await ProcesoDeValidacion.AnexarAsync(empresa.Sesion, stream, eventos, mensajes, reloj.GetUtcNow(), ct);
        await empresa.Sesion.SaveChangesAsync(ct);
        var detalle = await DetalleAsync(empresa, stream.Id, ct);
        Peticiones.EscribirETag(http, detalle.Version);
        return detalle;
    }

    internal static async Task<ExpedienteDto> DetalleAsync(SesionDeEmpresa empresa, Guid id, CancellationToken ct)
    {
        var expediente = await empresa.CargarExpedienteAsync(id, ct);
        var areas = expediente.AreaIds.Count == 0 ? [] : await empresa.Sesion.LoadManyAsync<Area>(ct, expediente.AreaIds.ToArray());
        return expediente.ADto(areas.ToDictionary(a => a.Id, a => a.Miembros));
    }
}

/// <summary>
/// Bandeja de revisión (<c>BandejaRevision</c>): una fila por expediente × área de los expedientes en revisión u
/// observados. Un usuario delegado ve solo las áreas donde es miembro; una integración ve todas.
/// </summary>
public static class BandejaEndpoints
{
    [WolverineGet("/api/v1/bandeja")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<PaginaDto<FilaBandejaDto>> Listar(SesionDeEmpresa empresa, IContextoSolicitud contexto, Guid? area, string? estado,
        int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var empresaId = empresa.EmpresaId;
        var consulta = empresa.Sesion.Query<FilaBandeja>()
            .Where(f => f.EmpresaId == empresaId &&
                        (f.EstadoExpediente == EstadoExpediente.EnRevision || f.EstadoExpediente == EstadoExpediente.Observado));

        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoAreaExpediente>(estado, true, out var estadoArea) || !Enum.IsDefined(estadoArea))
                throw ReglaDeNegocioException.Validacion("estado-invalido",
                    $"'{estado}' no es un estado de área ({string.Join(", ", Enum.GetNames<EstadoAreaExpediente>())}).");
            consulta = consulta.Where(f => f.EstadoArea == estadoArea);
        }
        else
        {
            consulta = consulta.Where(f => f.EstadoArea != EstadoAreaExpediente.Esperando);
        }

        if (area is { } areaId)
            consulta = consulta.Where(f => f.AreaId == areaId);

        if (contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuarioId))
        {
            var mias = await empresa.Areas().Where(a => a.Miembros.Contains(usuarioId)).Select(a => a.Id).ToListAsync(ct);
            if (mias.Count == 0)
                return new PaginaDto<FilaBandejaDto>([], null);
            var ids = mias.ToArray();
            consulta = consulta.Where(f => f.AreaId.IsOneOf(ids));
        }

        var pagina = await consulta.OrderBy(f => f.Desde).ThenBy(f => f.Id).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        var siguiente = pagina.Count > tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null;
        return new PaginaDto<FilaBandejaDto>([.. pagina.Take(tamano).Select(f => f.ADto())], siguiente);
    }

    /// <summary>
    /// Vínculos cuyo tercero aceptó la invitación pero no tienen expediente porque faltaba un flujo publicado (FL-6): el
    /// administrador lo abre con <c>POST /vinculos/{id}/expedientes</c>.
    /// </summary>
    [WolverineGet("/api/v1/bandeja/aperturas-pendientes")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<IReadOnlyList<VinculoDto>> AperturasPendientes(SesionDeEmpresa empresa, CancellationToken ct)
    {
        var vinculos = await empresa.Vinculos()
            .Where(v => v.InvitacionAceptada && v.Estado == EstadoVinculo.Invitado && v.ExpedienteAbiertoId == null && v.UltimoExpedienteId == null)
            .OrderBy(v => v.InvitacionAceptadaEn)
            .ToListAsync(ct);
        var tipos = await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct);
        return [.. vinculos.Select(v => v.ADto(tipos.FirstOrDefault(t => t.Id == v.TipoVinculoId)))];
    }
}

/// <summary>El tercero ve sus expedientes (solo los de los vínculos de su sesión) y responde a las observaciones.</summary>
public static class ExpedientesPublicosEndpoints
{
    [WolverineGet("/public/v1/expedientes/{id}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ExpedientePublicoDto> Obtener(Guid id, HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaGlobal();
        var expediente = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).SingleOrDefaultAsync(ct);
        if (expediente is null || expediente.TerceroId != tercero.TerceroId || tercero.Vinculos.All(v => v.VinculoId != expediente.VinculoId))
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");

        return expediente.APublico(await consulta.LoadAsync<Empresa>(expediente.EmpresaId, ct));
    }

    /// <summary>Responde a la observación (<c>{ respuesta }</c>): las áreas que observaron vuelven a revisar.</summary>
    [WolverinePost("/public/v1/expedientes/{id}/reenviar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ExpedientePublicoDto> Reenviar(Guid id, ReenviarExpedienteDto dto, HttpContext http, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        Expediente? ubicado;
        await using (var consulta = sesiones.ConsultaGlobal())
            ubicado = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).SingleOrDefaultAsync(ct);

        var enSesion = ubicado is null || ubicado.TerceroId != tercero.TerceroId
            ? null
            : tercero.Vinculos.FirstOrDefault(v => v.VinculoId == ubicado.VinculoId);
        if (enSesion is null)
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");

        await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(id, ct); // FL-12
        var expediente = stream.Aggregate;
        if (expediente is null || expediente.VinculoId != enSesion.VinculoId)
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");

        if (await sesion.LoadAsync<Vinculo>(expediente.VinculoId, ct) is { Estado: EstadoVinculo.Bloqueado })
            throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "La empresa suspendió temporalmente esta validación.");

        var ahora = reloj.GetUtcNow();
        var estado = await ProcesoDeValidacion.AnexarAsync(sesion, stream, expediente.Reenviar(OrigenReenvio.Tercero, dto.Respuesta, ahora), null,
            ahora, ct);
        await sesion.SaveChangesAsync(ct);
        return estado.APublico(await sesion.LoadAsync<Empresa>(estado.EmpresaId, ct));
    }
}
