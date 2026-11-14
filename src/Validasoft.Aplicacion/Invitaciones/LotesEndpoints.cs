using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Invitaciones;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>
/// Invitación masiva por lote de la empresa activa (H8, paridad con <c>Features/InvitacionLote</c> del SRM; LT-5…LT-10):
/// plantilla descargable, vista previa con la validación por fila, creación (202, se procesa por tandas en segundo plano),
/// consulta con su avance y sus filas, pausa, reanudación, cancelación, reintento de las fallidas e informe en Excel. Leer pide
/// <c>vinculos:leer</c>; todo lo demás, <c>vinculos:escribir</c> (como la invitación individual).
/// </summary>
public static class LotesEndpoints
{
    public const int MaximoFilasPorOmision = 10_000;

    /// <summary>Plantilla vacía con los encabezados (<c>formato=xlsx</c> por omisión, o <c>csv</c> separado por punto y coma).</summary>
    [WolverineGet("/api/v1/invitaciones/lotes/plantilla")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static IResult Plantilla(string? formato)
    {
        var (contenido, tipo, nombre) = ArchivoDeLote.Plantilla(formato);
        return Results.File(contenido, tipo, nombre);
    }

    /// <summary>
    /// Valida el archivo o las filas sin crear nada: por fila, sus errores, si repite una anterior y qué se hará (invitar,
    /// reenviar o nada porque el tercero ya aceptó). 422 si el archivo no se puede leer o le faltan columnas.
    /// </summary>
    [WolverinePost("/api/v1/invitaciones/lotes/vista-previa")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VistaPreviaLoteDto> VistaPrevia(CrearLoteDto dto, SesionDeEmpresa empresa, IConfiguration configuracion, TimeProvider reloj,
        CancellationToken ct)
    {
        var filas = await ValidacionDeLote.ValidarAsync(empresa, Entradas(dto, configuracion), reloj.GetUtcNow(), ct);
        return new VistaPreviaLoteDto(dto.Archivo?.Nombre, filas.Count, filas.Count(f => f.Estado == EstadoFilaLote.Pendiente),
            filas.Count(f => f.Estado == EstadoFilaLote.Invalida), filas.Count(f => f.Estado == EstadoFilaLote.Duplicada),
            [.. filas.Select(f => new FilaVistaPreviaDto(f.N, f.Datos.ADto(), f.Estado == EstadoFilaLote.Pendiente ? "Valida" : f.Estado.ToString(),
                [.. f.Errores.Select(ADto)], f.DuplicadaDe, f.Accion))]);
    }

    /// <summary>
    /// Crea el lote con todas sus filas (las inválidas y duplicadas quedan registradas sin procesarse) y encola la primera tanda
    /// en la misma transacción (202). 422 <c>lote-sin-filas-validas</c>, <c>lote-demasiado-grande</c>, <c>archivo-o-filas</c>.
    /// </summary>
    [WolverinePost("/api/v1/invitaciones/lotes")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<IResult> Crear(CrearLoteDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes,
        IConfiguration configuracion, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        empresa.UsarMensajeria(mensajes);
        var filas = await ValidacionDeLote.ValidarAsync(empresa, Entradas(dto, configuracion), ahora, ct);
        var totales = new TotalesDeLote(filas.Count, filas.Count(f => f.Estado == EstadoFilaLote.Pendiente), 0, 0,
            filas.Count(f => f.Estado == EstadoFilaLote.Duplicada), filas.Count(f => f.Estado == EstadoFilaLote.Invalida));

        // Aprobación antes de invitar (decisión AI): si alguna fila que va a invitar es de un tipo que lo exige, el lote completo espera una sola decisión antes de procesar nada.
        var porAprobar = await TiposPorAprobarAsync(empresa, filas, ct);
        if (porAprobar.Count > 0)
            await AprobadoresDeInvitaciones.ExigirAprobadoresAsync(empresa, porAprobar, contexto, ct);
        var creado = LoteDeInvitacion.Crear(empresa.EmpresaId, dto.Nombre, dto.Archivo?.Nombre, dto.VigenciaHoras, totales,
            new ActorDeLote(contexto.ActorTipo, contexto.ActorId), ahora, [.. porAprobar.Select(t => t.Id)]);

        empresa.Sesion.Events.StartStream<LoteDeInvitacion>(creado.LoteId, creado);
        foreach (var f in filas)
            empresa.Sesion.Store(new FilaDeLote
            {
                Id = FilaDeLote.Clave(creado.LoteId, f.N),
                LoteId = creado.LoteId,
                EmpresaId = empresa.EmpresaId,
                N = f.N,
                Datos = f.Datos,
                ClaveIdentificacion = f.Clave,
                Estado = f.Estado,
                Errores = [.. f.Errores],
                DuplicadaDe = f.DuplicadaDe
            });
        // Un lote por aprobar no encola su primera tanda: la encola quien lo apruebe (con una ejecución nueva).
        if (porAprobar.Count == 0)
            await mensajes.PublishAsync(new ProcesarTandaDeLote(contexto.TenantRequerido(), creado.LoteId, creado.Ejecucion));
        else
            AprobadoresDeInvitaciones.Auditar(empresa, contexto, "InvitacionDeLote", creado.LoteId, AccionConfiguracion.Creado, null,
                new { Estado = nameof(EstadoLote.PorAprobar), creado.CreadoPor, Tipos = porAprobar.Select(t => t.Codigo) }, ahora);
        await empresa.Sesion.SaveChangesAsync(ct);

        var lote = await CargarAsync(empresa, creado.LoteId, ct);
        return Results.Accepted($"/api/v1/invitaciones/lotes/{lote.Id}", (await ConAprobacionAsync(empresa, contexto, [lote], ct))[0]);
    }

    /// <summary>
    /// Lotes de la empresa, del más reciente al más antiguo; cursor opaco. <c>estado</c> filtra por el estado del lote (<c>PorAprobar</c> es la bandeja de quienes aprueban lotes,
    /// decisión AI); cada lote por aprobar dice <c>puedesDecidir</c>.
    /// </summary>
    [WolverineGet("/api/v1/invitaciones/lotes")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<PaginaDto<LoteDto>> Listar(SesionDeEmpresa empresa, IContextoSolicitud contexto, string? estado, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 20, 1, 100);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var empresaId = empresa.EmpresaId;
        var consulta = empresa.Sesion.Query<LoteDeInvitacion>().Where(l => l.EmpresaId == empresaId);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoLote>(estado, true, out var filtro) || !Enum.IsDefined(filtro))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de lote.");
            consulta = consulta.Where(l => l.Estado == filtro);
        }

        var pagina = await consulta.OrderByDescending(l => l.CreadoEn).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        return new PaginaDto<LoteDto>(await ConAprobacionAsync(empresa, contexto, [.. pagina.Take(tamano)], ct),
            pagina.Count > tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    [WolverineGet("/api/v1/invitaciones/lotes/{id}")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<LoteDto> Obtener(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, CancellationToken ct) =>
        (await ConAprobacionAsync(empresa, contexto, [await CargarAsync(empresa, id, ct)], ct))[0];

    /// <summary>
    /// Filas en orden, con filtro opcional por estado (<c>Pendiente</c>, <c>Exitosa</c>, <c>Fallida</c>, <c>Duplicada</c>,
    /// <c>Invalida</c>); el cursor es el número de la última fila leída.
    /// </summary>
    [WolverineGet("/api/v1/invitaciones/lotes/{id}/filas")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<PaginaDto<FilaLoteDto>> Filas(Guid id, SesionDeEmpresa empresa, string? estado, int? limite, string? cursor,
        CancellationToken ct)
    {
        var lote = await CargarAsync(empresa, id, ct);
        var tamano = Math.Clamp(limite ?? 50, 1, 500);
        var despues = string.IsNullOrWhiteSpace(cursor) ? 0
            : int.TryParse(cursor, out var n) && n >= 0 ? n
            : throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");
        var consulta = empresa.Sesion.Query<FilaDeLote>().Where(f => f.LoteId == id && f.N > despues);
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoFilaLote>(estado, true, out var filtro) || !Enum.IsDefined(filtro))
                throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{estado}' no es un estado de fila.");
            consulta = consulta.Where(f => f.Estado == filtro);
        }

        var pagina = await consulta.OrderBy(f => f.N).Take(tamano + 1).ToListAsync(ct);
        return new PaginaDto<FilaLoteDto>([.. pagina.Take(tamano).Select(f => f.ADto(lote))],
            pagina.Count > tamano ? pagina[tamano - 1].N.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
    }

    /// <summary>Pausa el procesamiento (la fila en curso termina). 409 <c>estado-invalido</c> o <c>lote-en-estado-terminal</c>.</summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/pausar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static Task<LoteDto> Pausar(Guid id, SesionDeEmpresa empresa, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, (lote, _) => Task.FromResult<IReadOnlyList<object>>([lote.Pausar(reloj.GetUtcNow())]), ct);

    /// <summary>Reanuda un lote pausado: sigue con las filas pendientes.</summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/reanudar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static Task<LoteDto> Reanudar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj,
        CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (lote, _) =>
        {
            var reanudado = lote.Reanudar(reloj.GetUtcNow());
            await mensajes.PublishAsync(new ProcesarTandaDeLote(contexto.TenantRequerido(), lote.Id, reanudado.Ejecucion));
            return [reanudado];
        }, ct);

    /// <summary>Cancela el lote: lo pendiente ya no se procesa (en el informe queda <c>Cancelada</c>). Terminal.</summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/cancelar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static Task<LoteDto> Cancelar(Guid id, SesionDeEmpresa empresa, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (lote, sesion) =>
            [lote.Cancelar(await ProcesarTandaDeLoteHandler.ContarAsync(sesion, lote.Id, ct), reloj.GetUtcNow())], ct);

    /// <summary>
    /// Vuelve a procesar las filas fallidas de un lote terminado (solo ellas). 409 <c>sin-filas-fallidas</c>,
    /// <c>estado-invalido</c> (aún en proceso) o <c>lote-en-estado-terminal</c> (cancelado).
    /// </summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/reintentar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static Task<LoteDto> Reintentar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj,
        CancellationToken ct) =>
        EscribirAsync(empresa, mensajes, id, async (lote, sesion) =>
        {
            var fallidas = await sesion.Query<FilaDeLote>().Where(f => f.LoteId == id && f.Estado == EstadoFilaLote.Fallida).ToListAsync(ct);
            var reintento = lote.ReintentarFallidas(fallidas.Count, reloj.GetUtcNow());
            foreach (var fila in fallidas)
            {
                fila.Estado = EstadoFilaLote.Pendiente;
                sesion.Store(fila);
            }

            await mensajes.PublishAsync(new ProcesarTandaDeLote(contexto.TenantRequerido(), lote.Id, reintento.Ejecucion));
            return [reintento];
        }, ct);

    /// <summary>
    /// Aprueba el lote completo (una sola decisión para todas sus filas, decisión AI): pasa a <c>Procesando</c> y se encola la primera tanda; las invitaciones de sus filas salen sin
    /// pedir una aprobación por vínculo. Quien aprueba debe ser de quienes designó <b>cada</b> tipo que exige aprobación (403 <c>no-es-aprobador-de-invitaciones</c>), una persona
    /// (422 <c>usuario-delegado-requerido</c>) y no quien creó el lote (403 <c>separacion-de-funciones</c>); 409 <c>sin-lote-por-aprobar</c>.
    /// </summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/aprobar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<LoteDto> Aprobar(Guid id, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<LoteDeInvitacion>(id, ct);
        var lote = Propio(stream.Aggregate, empresa);
        var decisor = await AprobadoresDeInvitaciones.DecisorAsync(empresa, contexto, await TiposAsync(empresa, lote, ct), ct);

        var ahora = reloj.GetUtcNow();
        var aprobado = lote.Aprobar(decisor.UsuarioId, ahora);
        stream.AppendOne(aprobado);
        await mensajes.PublishAsync(new ProcesarTandaDeLote(contexto.TenantRequerido(), lote.Id, aprobado.Ejecucion));
        AprobadoresDeInvitaciones.Auditar(empresa, contexto, "InvitacionDeLote", id, AccionConfiguracion.Modificado, new { Estado = nameof(EstadoLote.PorAprobar) },
            new { Estado = nameof(EstadoLote.Procesando), aprobado.AprobadoPor, lote.CreadoPor }, ahora);
        await empresa.Sesion.SaveChangesAsync(ct);
        return (await ConAprobacionAsync(empresa, contexto, [await CargarAsync(empresa, id, ct)], ct))[0];
    }

    /// <summary>
    /// Rechaza el lote completo con un motivo (422 <c>motivo-requerido</c>): queda <c>Cancelado</c>, ninguna fila se procesa y nunca se le escribió a ningún tercero. Las mismas reglas
    /// de quién decide que al aprobarlo.
    /// </summary>
    [WolverinePost("/api/v1/invitaciones/lotes/{id}/rechazar")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<LoteDto> Rechazar(Guid id, MotivoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes, TimeProvider reloj,
        CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<LoteDeInvitacion>(id, ct);
        var lote = Propio(stream.Aggregate, empresa);
        var decisor = await AprobadoresDeInvitaciones.DecisorAsync(empresa, contexto, await TiposAsync(empresa, lote, ct), ct);

        var ahora = reloj.GetUtcNow();
        var rechazado = lote.Rechazar(decisor.UsuarioId, dto.Motivo, await ProcesarTandaDeLoteHandler.ContarAsync(empresa.Sesion, id, ct), ahora);
        stream.AppendOne(rechazado);
        AprobadoresDeInvitaciones.Auditar(empresa, contexto, "InvitacionDeLote", id, AccionConfiguracion.Modificado, new { Estado = nameof(EstadoLote.PorAprobar) },
            new { Estado = nameof(EstadoLote.Cancelado), rechazado.RechazadoPor, rechazado.Motivo, lote.CreadoPor }, ahora);
        await empresa.Sesion.SaveChangesAsync(ct);
        return (await ConAprobacionAsync(empresa, contexto, [await CargarAsync(empresa, id, ct)], ct))[0];
    }

    private static LoteDeInvitacion Propio(LoteDeInvitacion? lote, SesionDeEmpresa empresa) =>
        lote is not null && lote.EmpresaId == empresa.EmpresaId
            ? lote
            : throw ReglaDeNegocioException.NoEncontrado("lote-no-encontrado", "No existe ese lote en la empresa.");

    /// <summary>Los tipos de vínculo del lote que exigen aprobación (los guardados al crearlo).</summary>
    private static async Task<IReadOnlyList<TipoVinculo>> TiposAsync(SesionDeEmpresa empresa, LoteDeInvitacion lote, CancellationToken ct) =>
        lote.TiposPorAprobar.Count == 0
            ? []
            : [.. (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).Where(t => lote.TiposPorAprobar.Contains(t.Id))];

    /// <summary>
    /// Los tipos de vínculo con aprobación previa a invitar (decisión AI) de las filas que van a invitar o reinvitar (las que no cambian nada, porque el tercero ya aceptó, no cuentan).
    /// </summary>
    private static async Task<IReadOnlyList<TipoVinculo>> TiposPorAprobarAsync(SesionDeEmpresa empresa, IReadOnlyList<FilaValidada> filas, CancellationToken ct)
    {
        var codigos = filas
            .Where(f => f.Estado == EstadoFilaLote.Pendiente && (f.Accion == nameof(AccionFilaLote.Invitado) || f.Accion == nameof(AccionFilaLote.Reinvitado)))
            .Select(f => f.Datos.TipoVinculo)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return codigos.Count == 0
            ? []
            : [.. (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).Where(t => t.Activo && t.RequiereAprobacionParaInvitar && codigos.Contains(t.Codigo))];
    }

    /// <summary>
    /// Los lotes como DTO; los que pasaron por aprobación (decisión AI) con los códigos de sus tipos y, mientras esperan, si quien consulta puede decidirlos (aprueba cada tipo y no
    /// los creó).
    /// </summary>
    internal static async Task<IReadOnlyList<LoteDto>> ConAprobacionAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, IReadOnlyList<LoteDeInvitacion> lotes,
        CancellationToken ct)
    {
        if (lotes.All(l => l.TiposPorAprobar.Count == 0))
            return [.. lotes.Select(l => l.ADto())];

        var tipos = (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);
        var usuarios = await empresa.Sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
        var areas = await empresa.Areas().ToListAsync(ct);
        var yo = AprobadoresDeInvitaciones.UsuarioDe(contexto);

        bool PuedeDecidir(LoteDeInvitacion lote) =>
            yo is { } usuario &&
            !(lote.CreadoPor.Tipo == ActorTipo.Usuario && string.Equals(lote.CreadoPor.Id, usuario.ToString(), StringComparison.OrdinalIgnoreCase)) &&
            lote.TiposPorAprobar.All(id => tipos.TryGetValue(id, out var tipo) &&
                                           AprobadoresDeInvitaciones.Elegibles(tipo, empresa.EmpresaId, usuarios, areas).Any(u => u.Id == usuario));

        return [.. lotes.Select(l => l.TiposPorAprobar.Count == 0
            ? l.ADto()
            : l.ADto() with
            {
                TiposPorAprobar = [.. l.TiposPorAprobar.Select(id => tipos.TryGetValue(id, out var tipo) ? tipo.Codigo : null).OfType<string>()],
                PuedesDecidir = l.Estado == EstadoLote.PorAprobar ? PuedeDecidir(l) : null
            })];
    }

    /// <summary>Informe por fila en Excel: identificación, datos, estado, acción, errores y el vínculo creado o reutilizado.</summary>
    [WolverineGet("/api/v1/invitaciones/lotes/{id}/informe")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<IResult> Informe(Guid id, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var lote = await CargarAsync(empresa, id, ct);
        var filas = new List<IReadOnlyList<object?>>();
        var despues = 0;
        while (true)
        {
            var desde = despues;
            var pagina = await empresa.Sesion.Query<FilaDeLote>().Where(f => f.LoteId == id && f.N > desde).OrderBy(f => f.N).Take(1000).ToListAsync(ct);
            if (pagina.Count == 0)
                break;
            foreach (var f in pagina.Select(f => f.ADto(lote)))
                filas.Add([f.N, f.Identificacion, f.Nombre, f.Correo, f.TipoVinculo, f.Estado, f.Accion,
                    string.Join(" ", f.Errores.Select(e => e.Campo is null ? e.Mensaje : $"{e.Campo}: {e.Mensaje}")) is { Length: > 0 } errores ? errores : f.Detalle,
                    f.DuplicadaDe, f.VinculoId?.ToString(), f.ProcesadaEn is { } fecha ? Dominio.Documentos.CalculadoraDeVigencia.Fecha(fecha) : null]);
            despues = pagina[^1].N;
        }

        var contenido = HojaDeCalculo.Crear("Informe", ["Fila", "Identificación", "Razón social o nombres", "Correo", "Tipo de vínculo", "Estado", "Acción",
            "Detalle", "Duplicada de", "Vínculo", "Procesada"], filas);
        return Results.File(contenido, HojaDeCalculo.TipoContenido, $"informe-lote-{lote.Id:N}.xlsx");
    }

    private static IReadOnlyList<FilaDeLoteEntrada> Entradas(CrearLoteDto dto, IConfiguration configuracion)
    {
        if ((dto.Archivo is null) == (dto.Filas is null))
            throw ReglaDeNegocioException.Validacion("archivo-o-filas", "Envía el archivo de la plantilla o las filas (una de las dos).");
        var filas = dto.Archivo is not null ? ArchivoDeLote.Leer(dto.Archivo) : [.. dto.Filas!.Select(f => f.ADominio())];
        if (filas.Count == 0)
            throw ReglaDeNegocioException.Validacion("lote-sin-filas", "El lote no tiene filas.");
        var maximo = configuracion.GetValue("Validasoft:Lotes:MaxFilas", MaximoFilasPorOmision);
        return filas.Count <= maximo
            ? filas
            : throw ReglaDeNegocioException.Validacion("lote-demasiado-grande", $"Un lote admite hasta {maximo} filas; este tiene {filas.Count}.");
    }

    private static async Task<LoteDeInvitacion> CargarAsync(SesionDeEmpresa empresa, Guid id, CancellationToken ct)
    {
        var lote = await empresa.Sesion.LoadAsync<LoteDeInvitacion>(id, ct);
        return lote is not null && lote.EmpresaId == empresa.EmpresaId
            ? lote
            : throw ReglaDeNegocioException.NoEncontrado("lote-no-encontrado", "No existe ese lote en la empresa.");
    }

    /// <summary>Decisión sobre el lote con su stream bloqueado y sus mensajes en el outbox de la petición.</summary>
    private static async Task<LoteDto> EscribirAsync(SesionDeEmpresa empresa, IMessageBus mensajes, Guid id,
        Func<LoteDeInvitacion, IDocumentSession, Task<IReadOnlyList<object>>> decidir, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var stream = await empresa.Sesion.Events.FetchForExclusiveWriting<LoteDeInvitacion>(id, ct);
        if (stream.Aggregate is not { } lote || lote.EmpresaId != empresa.EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("lote-no-encontrado", "No existe ese lote en la empresa.");
        stream.AppendMany(await decidir(lote, empresa.Sesion));
        await empresa.Sesion.SaveChangesAsync(ct);
        return (await CargarAsync(empresa, id, ct)).ADto();
    }

    public static LoteDto ADto(this LoteDeInvitacion l) =>
        new(l.Id, l.Nombre, l.Archivo, l.Estado.ToString(), l.Totales.ADto(), l.VigenciaHoras, $"{l.CreadoPor.Tipo}:{l.CreadoPor.Id}", l.CreadoEn,
            l.ActualizadoEn, l.TerminadoEn, l.Reintentos, l.Version,
            AprobadoPor: l.AprobadoPor is null ? null : $"{l.AprobadoPor.Tipo}:{l.AprobadoPor.Id}", AprobadoEn: l.AprobadoEn, MotivoDeRechazo: l.MotivoDeRechazo);

    public static TotalesLoteDto ADto(this TotalesDeLote t) =>
        new(t.Filas, t.Exitosas + t.Fallidas, t.Pendientes, t.Exitosas, t.Fallidas, t.Duplicadas, t.Invalidas);

    public static FilaLoteDto ADto(this FilaDeLote f, LoteDeInvitacion lote) =>
        new(f.N, f.ClaveIdentificacion ?? string.Join(" ", new[] { f.Datos.TipoIdentificacion, f.Datos.NumeroIdentificacion }.OfType<string>()),
            f.Datos.Nombre, f.Datos.Correo, f.Datos.TipoVinculo,
            f.Estado == EstadoFilaLote.Pendiente && lote.Estado == EstadoLote.Cancelado ? "Cancelada" : f.Estado.ToString(), f.Accion?.ToString(), f.Detalle,
            [.. f.Errores.Select(ADto)], f.DuplicadaDe, f.TerceroId, f.VinculoId, f.TerceroCreado, f.Intentos, f.ProcesadaEn);

    public static ErrorDeFilaDto ADto(this ErrorDeFila e) => new(e.Campo, e.Codigo, e.Mensaje);
}
