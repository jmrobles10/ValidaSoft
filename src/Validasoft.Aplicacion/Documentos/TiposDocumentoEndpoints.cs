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
/// Tipos de documento del tenant (docs/05 §2.2): defaults del grupo, enlace al canónico y qué empresa los exige en qué
/// tipo de vínculo (BO-3). Todo cambio queda en la auditoría de configuración (R-1).
/// </summary>
public static class TiposDocumentoEndpoints
{
    public const string Entidad = "TipoDocumentoTenant";

    [WolverinePost("/api/v1/tipos-documento")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<IResult> Crear(CrearTipoDocumentoDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();

        await using var sesion = sesiones.DelTenant();
        var tiposVinculo = await TiposVinculoAsync(sesion, ct);
        var exigencias = await ExigenciasAsync(dto.Exigencias, tiposVinculo, sesiones, contexto, ct);
        var tipo = TipoDocumentoTenant.Crear(tenantId, dto.Codigo, dto.Nombre, dto.TipoCanonico,
            MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia, dto.DiasRevalidacion,
            dto.TextoAyuda, exigencias, ahora);

        var codigo = tipo.Codigo;
        if (await sesion.Query<TipoDocumentoTenant>().AnyAsync(t => t.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("tipo-documento-duplicado", $"Ya existe el tipo de documento {codigo}.");

        sesion.Store(tipo);
        var creado = tipo.ADto(tiposVinculo);
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Creado, null, creado, ahora);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/tipos-documento/{tipo.Id}", creado);
    }

    [WolverineGet("/api/v1/tipos-documento")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<TipoDocumentoTenantDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipos = await sesion.Query<TipoDocumentoTenant>().OrderBy(t => t.Codigo).ToListAsync(ct);
        var tiposVinculo = await TiposVinculoAsync(sesion, ct);
        return [.. tipos.Select(t => t.ADto(tiposVinculo))];
    }

    [WolverineGet("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<TipoDocumentoTenantDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await CargarAsync(sesion, id, ct);
        return tipo.ADto(await TiposVinculoAsync(sesion, ct));
    }

    /// <summary>
    /// Código y canónico son inmutables. <c>exigencias</c> reemplaza la lista, pero solo las de empresas al alcance de la
    /// credencial: las exigencias de otras empresas del tenant se conservan.
    /// </summary>
    [WolverinePut("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<TipoDocumentoTenantDto> Actualizar(Guid id, ActualizarTipoDocumentoDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await CargarAsync(sesion, id, ct);
        var tiposVinculo = await TiposVinculoAsync(sesion, ct);
        var antes = tipo.ADto(tiposVinculo);

        IReadOnlyList<ExigenciaDeDocumento>? exigencias = null;
        if (dto.Exigencias is not null)
        {
            var nuevas = await ExigenciasAsync(dto.Exigencias, tiposVinculo, sesiones, contexto, ct);
            var ajenas = tipo.Exigencias.Where(e => !AlAlcance(contexto, e.EmpresaId));
            exigencias = [.. ajenas, .. nuevas];
        }

        tipo.Actualizar(dto.Nombre, MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia,
            dto.DiasRevalidacion, dto.TextoAyuda, exigencias, dto.Activo);

        sesion.Store(tipo);
        var despues = tipo.ADto(tiposVinculo);
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Modificado, antes, despues, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    private static async Task<TipoDocumentoTenant> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<TipoDocumentoTenant>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("tipo-documento-no-encontrado", "No existe ese tipo de documento.");

    private static async Task<IReadOnlyDictionary<Guid, TipoVinculo>> TiposVinculoAsync(IDocumentSession sesion, CancellationToken ct) =>
        (await sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);

    private static bool AlAlcance(IContextoSolicitud contexto, Guid empresaId) =>
        contexto.Empresas.Contains("*") || contexto.Empresas.Contains(empresaId.ToString());

    /// <summary>Cada exigencia: una empresa del tenant al alcance de la credencial y un tipo de vínculo del tenant (por código o id).</summary>
    private static async Task<IReadOnlyList<ExigenciaDeDocumento>> ExigenciasAsync(IReadOnlyList<ExigenciaDto>? dtos,
        IReadOnlyDictionary<Guid, TipoVinculo> tiposVinculo, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        if (dtos is null || dtos.Count == 0)
            return [];

        var tenantId = contexto.TenantRequerido();
        var ids = dtos.Select(d => d.EmpresaId).Distinct().ToArray();
        await using var consulta = sesiones.ConsultaGlobal();
        var empresas = await consulta.LoadManyAsync<Empresa>(ct, ids);

        var resultado = new List<ExigenciaDeDocumento>();
        foreach (var dto in dtos)
        {
            if (empresas.FirstOrDefault(e => e.Id == dto.EmpresaId) is not { } empresa || empresa.TenantId != tenantId ||
                !AlAlcance(contexto, empresa.Id))
                throw ReglaDeNegocioException.Validacion("exigencia-invalida", $"La empresa {dto.EmpresaId} no existe en el tenant o no está a tu alcance.");

            var clave = (dto.TipoVinculo ?? string.Empty).Trim();
            var tipo = Guid.TryParse(clave, out var tipoId)
                ? tiposVinculo.GetValueOrDefault(tipoId)
                : tiposVinculo.Values.FirstOrDefault(t => string.Equals(t.Codigo, clave, StringComparison.OrdinalIgnoreCase));
            if (tipo is null)
                throw ReglaDeNegocioException.Validacion("exigencia-invalida", $"'{dto.TipoVinculo}' no es un tipo de vínculo del tenant.");

            resultado.Add(new ExigenciaDeDocumento(empresa.Id, tipo.Id));
        }

        return [.. resultado.Distinct()];
    }
}
