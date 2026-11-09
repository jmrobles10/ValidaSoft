using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>Catálogo canónico de la plataforma (solo lectura; lo administra la plataforma en código, P-2-1).</summary>
public static class CatalogoDocumentalEndpoints
{
    [WolverineGet("/api/v1/catalogos/tipos-documento")]
    [Authorize]
    public static IReadOnlyList<TipoDocumentoCanonicoDto> TiposDocumento() => [.. CatalogoCanonico.Todos.Select(t => t.ADto())];
}

/// <summary>
/// Tipos de documento del tenant (docs/05 §2.2): defaults del grupo, enlace al canónico y, desde TD-1…TD-6, reglas de
/// archivo, modo de captura con sus campos y la vigencia (frescura, avisos, gracia). Qué se pide a cada vínculo lo deciden
/// los requisitos de su flujo (RQ-3). Todo cambio queda en la auditoría de configuración (R-1).
/// </summary>
public static class TiposDocumentoEndpoints
{
    public const string Entidad = "TipoDocumentoTenant";

    /// <summary>
    /// 201 con el tipo. 409 <c>tipo-documento-duplicado</c>; 422 <c>codigo-invalido</c>, <c>ambito-no-ampliable</c>,
    /// <c>formato-invalido</c>, <c>formato-mas-laxo</c>, <c>limite-invalido</c>, <c>limite-mas-laxo</c>,
    /// <c>limite-sobre-el-global</c>, <c>modo-invalido</c>, <c>campos-requeridos</c>, <c>campo-invalido</c>,
    /// <c>campo-duplicado</c>, <c>campo-reservado</c>, <c>formulario-solo-por-vinculo</c>.
    /// </summary>
    [WolverinePost("/api/v1/tipos-documento")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<IResult> Crear(CrearTipoDocumentoDto dto, ISesiones sesiones, IContextoSolicitud contexto, ReglasDeCarga reglas,
        TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();

        await using var sesion = sesiones.DelTenant();
        var tipo = TipoDocumentoTenant.Crear(tenantId, dto.Codigo, dto.Nombre, dto.TipoCanonico,
            MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia, dto.DiasRevalidacion,
            dto.TextoAyuda, ahora);
        tipo.ConfigurarCaptura(dto.FormatosPermitidos, dto.TamanoMaximoMb, dto.PaginasMaximas, MapeosDocumentos.AModo(dto.Modo),
            dto.Campos?.Select(c => c.ADominio()));
        ValidarContraGlobales(tipo, reglas);

        var codigo = tipo.Codigo;
        if (await sesion.Query<TipoDocumentoTenant>().AnyAsync(t => t.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("tipo-documento-duplicado", $"Ya existe el tipo de documento {codigo}.");

        sesion.Store(tipo);
        var creado = tipo.ADto(reglas);
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Creado, null, creado, ahora);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/tipos-documento/{tipo.Id}", creado);
    }

    [WolverineGet("/api/v1/tipos-documento")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<TipoDocumentoTenantDto>> Listar(ISesiones sesiones, ReglasDeCarga reglas, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipos = await sesion.Query<TipoDocumentoTenant>().OrderBy(t => t.Codigo).ToListAsync(ct);
        return [.. tipos.Select(t => t.ADto(reglas))];
    }

    [WolverineGet("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<TipoDocumentoTenantDto> Obtener(Guid id, ISesiones sesiones, ReglasDeCarga reglas, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await CargarAsync(sesion, id, ct);
        return tipo.ADto(reglas);
    }

    /// <summary>
    /// Código y canónico son inmutables; los campos nulos conservan su valor. Si cambian la frescura, los avisos o la gracia,
    /// las vigencias de los documentos del tipo se recalculan por mensaje (<see cref="RecalcularVigenciasDelTipo"/>).
    /// </summary>
    [WolverinePut("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<TipoDocumentoTenantDto> Actualizar(Guid id, ActualizarTipoDocumentoDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, IMessageBus mensajes, ReglasDeCarga reglas, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant(mensajes);
        var tipo = await CargarAsync(sesion, id, ct);
        var antes = tipo.ADto(reglas);

        tipo.Actualizar(dto.Nombre, MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia,
            dto.DiasRevalidacion, dto.TextoAyuda, dto.Activo);
        tipo.ConfigurarCaptura(dto.FormatosPermitidos, dto.TamanoMaximoMb, dto.PaginasMaximas, MapeosDocumentos.AModo(dto.Modo),
            dto.Campos?.Select(c => c.ADominio()));
        ValidarContraGlobales(tipo, reglas);

        sesion.Store(tipo);
        var despues = tipo.ADto(reglas);
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Modificado, antes, despues, reloj.GetUtcNow());
        if (antes.ReglaFrescura != despues.ReglaFrescura || !antes.DiasAviso.SequenceEqual(despues.DiasAviso) || antes.DiasGracia != despues.DiasGracia ||
            antes.Ambito != despues.Ambito)
            await mensajes.PublishAsync(new RecalcularVigenciasDelTipo(tipo.TenantId, tipo.Id));
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    internal static async Task<TipoDocumentoTenant> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<TipoDocumentoTenant>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("tipo-documento-no-encontrado", "No existe ese tipo de documento.");

    /// <summary>Un tipo nunca supera los límites globales de la plataforma (<c>Validasoft:Boveda</c>): 422 <c>limite-sobre-el-global</c>.</summary>
    private static void ValidarContraGlobales(TipoDocumentoTenant tipo, ReglasDeCarga reglas)
    {
        if (tipo.TamanoMaximoMb is { } mb && mb * ReglasDeArchivo.Mega > reglas.TamanoMaximoBytes)
            throw ReglaDeNegocioException.Validacion("limite-sobre-el-global",
                $"El peso máximo ({mb} MB) supera el de la plataforma ({FormatosDeArchivo.Tamano(reglas.TamanoMaximoBytes)}).");
        if (tipo.PaginasMaximas is { } paginas && paginas > reglas.MaximoPaginas)
            throw ReglaDeNegocioException.Validacion("limite-sobre-el-global",
                $"El máximo de páginas ({paginas}) supera el de la plataforma ({reglas.MaximoPaginas}).");
    }
}
