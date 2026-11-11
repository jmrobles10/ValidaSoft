using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Campanias;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Campanias;

/// <summary>
/// Campañas de renovación de la empresa activa (OP-1…OP-6): se elige a los vínculos por filtro (vencen en N días, tipo de vínculo y estado), la <b>vista
/// previa</b> dice a quién afecta antes de confirmar y la campaña abre su renovación en lote, por tandas en segundo plano, reanudable y con informe por vínculo
/// en Excel. Leer pide <c>expedientes:leer</c>; crear, pausar, reanudar, cancelar y reintentar, <c>expedientes:administrar</c> (abre expedientes).
/// </summary>
public static class CampaniasEndpoints
{
    public const int FilasDeVistaPrevia = 200;

    /// <summary>A quién afectaría el filtro, sin crear nada: por vínculo, qué se haría (abrir su renovación, recordarla u omitirlo y por qué).</summary>
    [WolverinePost("/api/v1/campanias/vista-previa")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<VistaPreviaCampaniaDto> VistaPrevia(FiltroDeCampaniaDto dto, SesionDeEmpresa empresa, IConfiguration configuracion, CancellationToken ct)
    {
        var filtro = dto.ADominio().Validar();
        var candidatos = await SeleccionarAsync(empresa, filtro, configuracion, ct);
        return new VistaPreviaCampaniaDto(filtro.ADto(), candidatos.Count, candidatos.Count(c => c.Accion == "Abrir"), candidatos.Count(c => c.Accion == "Recordar"),
            candidatos.Count(c => c.Accion == "Omitir"), candidatos.Count(c => c.Accion != "Omitir" && !string.IsNullOrWhiteSpace(c.Vinculo.CorreoTercero)),
            candidatos.Count > FilasDeVistaPrevia,
            [.. candidatos.Take(FilasDeVistaPrevia).Select((c, i) => new FilaVistaPreviaCampaniaDto(i + 1, c.Vinculo.Id,
                new TerceroResumenDto(c.Vinculo.TerceroId, c.Tercero?.Nombre ?? string.Empty, c.Tercero?.Identificacion.Clave ?? string.Empty),
                new TipoVinculoResumenDto(c.Vinculo.TipoVinculoId, c.Tipo?.Codigo ?? string.Empty, c.Tipo?.Nombre ?? string.Empty), c.Vinculo.Estado.ToString(), c.VenceEn,
                c.Accion, c.Motivo, !string.IsNullOrWhiteSpace(c.Vinculo.CorreoTercero)))]);
    }

    /// <summary>
    /// Crea la campaña con todas sus filas (las que no aplican quedan <c>Omitida</c> con su motivo) y encola la primera tanda en la misma transacción (202).
    /// 422 <c>campania-sin-vinculos</c>, <c>campania-demasiado-grande</c>, <c>dias-invalidos</c>, <c>estado-invalido</c>.
    /// </summary>
    [WolverinePost("/api/v1/campanias")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static async Task<IResult> Crear(CrearCampaniaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, IConfiguration configuracion,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        empresa.UsarMensajeria(mensajes);
        var filtro = (dto.Filtro ?? new FiltroDeCampaniaDto()).ADominio().Validar();
        var candidatos = await SeleccionarAsync(empresa, filtro, configuracion, ct);
        var pendientes = candidatos.Count(c => c.Accion != "Omitir");
        var totales = new TotalesDeCampania(candidatos.Count, pendientes, 0, 0, candidatos.Count - pendientes);
        var creada = CampaniaDeRenovacion.Crear(empresa.EmpresaId, dto.Nombre, filtro, totales, new ActorDeCampania(contexto.ActorTipo, contexto.ActorId), ahora);

        empresa.Sesion.Events.StartStream<CampaniaDeRenovacion>(creada.CampaniaId, creada);
        for (var i = 0; i < candidatos.Count; i++)
        {
            var c = candidatos[i];
            empresa.Sesion.Store(new FilaDeCampania
            {
                Id = FilaDeCampania.Clave(creada.CampaniaId, i + 1),
                CampaniaId = creada.CampaniaId,
                EmpresaId = empresa.EmpresaId,
                N = i + 1,
                VinculoId = c.Vinculo.Id,
                TerceroId = c.Vinculo.TerceroId,
                TerceroNombre = c.Tercero?.Nombre ?? string.Empty,
                TerceroIdentificacion = c.Tercero?.Identificacion.Clave ?? string.Empty,
                TipoVinculoNombre = c.Tipo?.Nombre ?? string.Empty,
                EstadoDelVinculo = c.Vinculo.Estado,
                VenceEn = c.VenceEn,
                Estado = c.Accion == "Omitir" ? EstadoFilaCampania.Omitida : EstadoFilaCampania.Pendiente,
                Detalle = c.Accion == "Omitir" ? c.Motivo : null
            });
        }

        await mensajes.PublishAsync(new ProcesarTandaDeCampania(contexto.TenantRequerido(), creada.CampaniaId, creada.Ejecucion));
        await empresa.Sesion.SaveChangesAsync(ct);

        var campania = await CargarAsync(empresa, creada.CampaniaId, ct);
        return Results.Accepted($"/api/v1/campanias/{campania.Id}", campania.ADto());
    }

    /// <summary>Campañas de la empresa, de la más reciente a la más antigua; cursor opaco.</summary>
    [WolverineGet("/api/v1/campanias")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<PaginaDto<CampaniaDto>> Listar(SesionDeEmpresa empresa, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 20, 1, 100);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var empresaId = empresa.EmpresaId;
        var pagina = await empresa.Sesion.Query<CampaniaDeRenovacion>().Where(c => c.EmpresaId == empresaId).OrderByDescending(c => c.CreadaEn)
            .Skip(desde).Take(tamano + 1).ToListAsync(ct);
        return new PaginaDto<CampaniaDto>([.. pagina.Take(tamano).Select(c => c.ADto())], pagina.Count > tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    [WolverineGet("/api/v1/campanias/{id}")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<CampaniaDto> Obtener(Guid id, SesionDeEmpresa empresa, CancellationToken ct) => (await CargarAsync(empresa, id, ct)).ADto();

    /// <summary>Filas en orden, con filtro opcional por estado (<c>Pendiente</c>, <c>Exitosa</c>, <c>Fallida</c>, <c>Omitida</c>); el cursor es el número de la última fila leída.</summary>
    [WolverineGet("/api/v1/campanias/{id}/filas")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<PaginaDto<FilaCampaniaDto>> Filas(Guid id, SesionDeEmpresa empresa, string? estado, int? limite, string? cursor, CancellationToken ct)
    {
        var campania = await CargarAsync(empresa, id, ct);
        var tamano = Math.Clamp(limite ?? 50, 1, 500);
        var despues = string.IsNullOrWhiteSpace(cursor) ? 0
            : int.TryParse(cursor, out var n) && n >= 0 ? n
            : throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");
        var consulta = empresa.Sesion.Query<FilaDeCampania>().Where(f => f.CampaniaId == id && f.N > despues);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoFilaCampania>(estado, true, out var filtro) || !Enum.IsDefined(filtro))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de fila.");
            consulta = consulta.Where(f => f.Estado == filtro);
        }

        var pagina = await consulta.OrderBy(f => f.N).Take(tamano + 1).ToListAsync(ct);
        return new PaginaDto<FilaCampaniaDto>([.. pagina.Take(tamano).Select(f => f.ADto(campania))],
            pagina.Count > tamano ? pagina[tamano - 1].N.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
    }

    /// <summary>Pausa el procesamiento (el vínculo en curso termina). 409 <c>estado-invalido</c> o <c>campania-en-estado-terminal</c>.</summary>
    [WolverinePost("/api/v1/campanias/{id}/pausar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static Task<CampaniaDto> Pausar(Guid id, SesionDeEmpresa empresa, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, (campania, _) => Task.FromResult<IReadOnlyList<object>>([campania.Pausar(reloj.GetUtcNow())]), ct);

    /// <summary>Reanuda una campaña pausada: sigue con los vínculos pendientes.</summary>
    [WolverinePost("/api/v1/campanias/{id}/reanudar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static Task<CampaniaDto> Reanudar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (campania, _) =>
        {
            var reanudada = campania.Reanudar(reloj.GetUtcNow());
            await mensajes.PublishAsync(new ProcesarTandaDeCampania(contexto.TenantRequerido(), campania.Id, reanudada.Ejecucion));
            return [reanudada];
        }, ct);

    /// <summary>Cancela la campaña: lo pendiente ya no se procesa. Lo ya abierto sigue su curso. Terminal.</summary>
    [WolverinePost("/api/v1/campanias/{id}/cancelar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static Task<CampaniaDto> Cancelar(Guid id, SesionDeEmpresa empresa, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (campania, sesion) =>
            [campania.Cancelar(await ProcesarTandaDeCampaniaHandler.ContarAsync(sesion, campania.Id, ct), reloj.GetUtcNow())], ct);

    /// <summary>Vuelve a procesar las filas fallidas de una campaña terminada (solo ellas). 409 <c>sin-filas-fallidas</c>, <c>estado-invalido</c> o <c>campania-en-estado-terminal</c>.</summary>
    [WolverinePost("/api/v1/campanias/{id}/reintentar")]
    [Authorize(Policy = Permisos.ExpedientesAdministrar)]
    public static Task<CampaniaDto> Reintentar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (campania, sesion) =>
        {
            var fallidas = await sesion.Query<FilaDeCampania>().Where(f => f.CampaniaId == id && f.Estado == EstadoFilaCampania.Fallida).ToListAsync(ct);
            var reintento = campania.ReintentarFallidas(fallidas.Count, reloj.GetUtcNow());
            foreach (var fila in fallidas)
            {
                fila.Estado = EstadoFilaCampania.Pendiente;
                sesion.Store(fila);
            }

            await mensajes.PublishAsync(new ProcesarTandaDeCampania(contexto.TenantRequerido(), campania.Id, reintento.Ejecucion));
            return [reintento];
        }, ct);

    /// <summary>Informe por vínculo en Excel: tercero, tipo, estado, vencimiento, resultado, expediente abierto y detalle.</summary>
    [WolverineGet("/api/v1/campanias/{id}/informe")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IResult> Informe(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var campania = await CargarAsync(empresa, id, ct);
        var filas = new List<IReadOnlyList<object?>>();
        var despues = 0;
        while (true)
        {
            var desde = despues;
            var pagina = await empresa.Sesion.Query<FilaDeCampania>().Where(f => f.CampaniaId == id && f.N > desde).OrderBy(f => f.N).Take(1000).ToListAsync(ct);
            if (pagina.Count == 0)
                break;
            foreach (var f in pagina.Select(f => f.ADto(campania)))
                filas.Add([f.N, f.Identificacion, f.Tercero, f.TipoVinculo, f.EstadoVinculo, f.VenceEn, f.Estado, f.Accion,
                    f.Errores.Count > 0 ? string.Join(" ", f.Errores.Select(e => e.Mensaje)) : f.Detalle, f.Folio, f.ProcesadaEn is { } fecha ? Dominio.Documentos.CalculadoraDeVigencia.Fecha(fecha) : null]);
            despues = pagina[^1].N;
        }

        var contenido = HojaDeCalculo.Crear("Informe", ["Fila", "Identificación", "Tercero", "Tipo de vínculo", "Estado del vínculo", "Vence", "Estado", "Acción", "Detalle",
            "Expediente", "Procesada"], filas);
        return Results.File(contenido, HojaDeCalculo.TipoContenido, $"informe-campania-{campania.Id:N}.xlsx");
    }

    private static async Task<IReadOnlyList<CandidatoDeCampania>> SeleccionarAsync(SesionDeEmpresa empresa, FiltroDeCampania filtro, IConfiguration configuracion, CancellationToken ct)
    {
        await using var boveda = empresa.ConsultaBoveda();
        return await SeleccionDeCampania.SeleccionarAsync(empresa.Sesion, boveda, empresa.EmpresaId, filtro, empresa.Hoy,
            configuracion.GetValue("Validasoft:Campanias:MaxFilas", SeleccionDeCampania.MaximoPorOmision), ct);
    }

    private static async Task<CampaniaDeRenovacion> CargarAsync(SesionDeEmpresa empresa, Guid id, CancellationToken ct)
    {
        var campania = await empresa.Sesion.LoadAsync<CampaniaDeRenovacion>(id, ct);
        return campania is not null && campania.EmpresaId == empresa.EmpresaId
            ? campania
            : throw ReglaDeNegocioException.NoEncontrado("campania-no-encontrada", "No existe esa campaña en la empresa.");
    }

    /// <summary>Decisión sobre la campaña con su stream bloqueado y sus mensajes en el outbox de la petición.</summary>
    private static async Task<CampaniaDto> EscribirAsync(SesionDeEmpresa empresa, IMessageBus mensajes, Guid id,
        Func<CampaniaDeRenovacion, IDocumentSession, Task<IReadOnlyList<object>>> decidir, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<CampaniaDeRenovacion>(id, ct);
        if (stream.Aggregate is not { } campania || campania.EmpresaId != empresa.EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("campania-no-encontrada", "No existe esa campaña en la empresa.");
        stream.AppendMany(await decidir(campania, empresa.Sesion));
        await empresa.Sesion.SaveChangesAsync(ct);
        return (await CargarAsync(empresa, id, ct)).ADto();
    }

    // ---------- Mapeos ----------

    public static FiltroDeCampania ADominio(this FiltroDeCampaniaDto f) =>
        new(f.DiasParaVencer, f.TipoVinculoId, [.. (f.Estados ?? []).Select(e =>
            Enum.TryParse<EstadoVinculo>(e, true, out var estado) && Enum.IsDefined(estado)
                ? estado
                : throw ReglaDeNegocioException.Validacion("estado-invalido", $"«{e}» no es un estado de vínculo."))]);

    public static FiltroDeCampaniaDto ADto(this FiltroDeCampania f) => new(f.DiasParaVencer, f.TipoVinculoId, [.. f.Estados.Select(e => e.ToString())]);

    public static TotalesCampaniaDto ADto(this TotalesDeCampania t) => new(t.Filas, t.Exitosas + t.Fallidas + t.Omitidas, t.Pendientes, t.Exitosas, t.Fallidas, t.Omitidas);

    public static CampaniaDto ADto(this CampaniaDeRenovacion c) =>
        new(c.Id, c.Nombre, c.Filtro.ADto(), c.Estado.ToString(), c.Totales.ADto(), $"{c.CreadaPor.Tipo}:{c.CreadaPor.Id}", c.CreadaEn, c.ActualizadaEn, c.TerminadaEn, c.Reintentos,
            c.Version);

    public static FilaCampaniaDto ADto(this FilaDeCampania f, CampaniaDeRenovacion campania) =>
        new(f.N, f.VinculoId, f.TerceroId, f.TerceroNombre, string.Join(" ", f.TerceroIdentificacion.Split('|').Skip(1)), f.TipoVinculoNombre, f.EstadoDelVinculo.ToString(), f.VenceEn,
            f.Estado == EstadoFilaCampania.Pendiente && campania.Estado == EstadoCampania.Cancelada ? "Cancelada" : f.Estado.ToString(), f.Accion?.ToString(), f.Detalle, f.ExpedienteId, f.Folio,
            f.ErrorCodigo is null ? [] : [new ErrorDeFilaDto(null, f.ErrorCodigo, f.ErrorMensaje ?? string.Empty)], f.Intentos, f.ProcesadaEn);
}
