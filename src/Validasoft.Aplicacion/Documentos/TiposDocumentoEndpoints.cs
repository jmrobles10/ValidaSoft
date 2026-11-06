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
/// Tipos de documento del tenant (docs/05 §2.2): defaults del grupo y enlace al canónico. Qué se pide a cada vínculo lo
/// deciden los requisitos de su flujo (RQ-3). Todo cambio queda en la auditoría de configuración (R-1).
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
        var tipo = TipoDocumentoTenant.Crear(tenantId, dto.Codigo, dto.Nombre, dto.TipoCanonico,
            MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia, dto.DiasRevalidacion,
            dto.TextoAyuda, ahora);

        var codigo = tipo.Codigo;
        if (await sesion.Query<TipoDocumentoTenant>().AnyAsync(t => t.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("tipo-documento-duplicado", $"Ya existe el tipo de documento {codigo}.");

        sesion.Store(tipo);
        var creado = tipo.ADto();
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
        return [.. tipos.Select(t => t.ADto())];
    }

    [WolverineGet("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<TipoDocumentoTenantDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await CargarAsync(sesion, id, ct);
        return tipo.ADto();
    }

    /// <summary>Código y canónico son inmutables; los campos nulos conservan su valor.</summary>
    [WolverinePut("/api/v1/tipos-documento/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<TipoDocumentoTenantDto> Actualizar(Guid id, ActualizarTipoDocumentoDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var tipo = await CargarAsync(sesion, id, ct);
        var antes = tipo.ADto();

        tipo.Actualizar(dto.Nombre, MapeosDocumentos.AAmbito(dto.Ambito), dto.ReglaFrescura.ADominio(), dto.DiasAviso, dto.DiasGracia,
            dto.DiasRevalidacion, dto.TextoAyuda, dto.Activo);

        sesion.Store(tipo);
        var despues = tipo.ADto();
        Auditoria.Registrar(sesion, contexto, Entidad, tipo.Id.ToString(), AccionConfiguracion.Modificado, antes, despues, reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    private static async Task<TipoDocumentoTenant> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<TipoDocumentoTenant>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("tipo-documento-no-encontrado", "No existe ese tipo de documento.");
}
