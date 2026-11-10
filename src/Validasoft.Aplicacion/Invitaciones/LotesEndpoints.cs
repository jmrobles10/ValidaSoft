using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
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
        var creado = LoteDeInvitacion.Crear(empresa.EmpresaId, dto.Nombre, dto.Archivo?.Nombre, dto.VigenciaHoras, totales,
            new ActorDeLote(contexto.ActorTipo, contexto.ActorId), ahora);

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
        await mensajes.PublishAsync(new ProcesarTandaDeLote(contexto.TenantRequerido(), creado.LoteId, creado.Ejecucion));
        await empresa.Sesion.SaveChangesAsync(ct);

        var lote = await CargarAsync(empresa, creado.LoteId, ct);
        return Results.Accepted($"/api/v1/invitaciones/lotes/{lote.Id}", lote.ADto());
    }

    /// <summary>Lotes de la empresa, del más reciente al más antiguo; cursor opaco.</summary>
    [WolverineGet("/api/v1/invitaciones/lotes")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<PaginaDto<LoteDto>> Listar(SesionDeEmpresa empresa, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 20, 1, 100);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var empresaId = empresa.EmpresaId;
        var pagina = await empresa.Sesion.Query<LoteDeInvitacion>().Where(l => l.EmpresaId == empresaId)
            .OrderByDescending(l => l.CreadoEn).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        return new PaginaDto<LoteDto>([.. pagina.Take(tamano).Select(l => l.ADto())],
            pagina.Count > tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    [WolverineGet("/api/v1/invitaciones/lotes/{id}")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<LoteDto> Obtener(Guid id, SesionDeEmpresa empresa, CancellationToken ct) => (await CargarAsync(empresa, id, ct)).ADto();

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
            l.ActualizadoEn, l.TerminadoEn, l.Reintentos, l.Version);

    public static TotalesLoteDto ADto(this TotalesDeLote t) =>
        new(t.Filas, t.Exitosas + t.Fallidas, t.Pendientes, t.Exitosas, t.Fallidas, t.Duplicadas, t.Invalidas);

    public static FilaLoteDto ADto(this FilaDeLote f, LoteDeInvitacion lote) =>
        new(f.N, f.ClaveIdentificacion ?? string.Join(" ", new[] { f.Datos.TipoIdentificacion, f.Datos.NumeroIdentificacion }.OfType<string>()),
            f.Datos.Nombre, f.Datos.Correo, f.Datos.TipoVinculo,
            f.Estado == EstadoFilaLote.Pendiente && lote.Estado == EstadoLote.Cancelado ? "Cancelada" : f.Estado.ToString(), f.Accion?.ToString(), f.Detalle,
            [.. f.Errores.Select(ADto)], f.DuplicadaDe, f.TerceroId, f.VinculoId, f.TerceroCreado, f.Intentos, f.ProcesadaEn);

    public static ErrorDeFilaDto ADto(this ErrorDeFila e) => new(e.Campo, e.Codigo, e.Mensaje);
}
