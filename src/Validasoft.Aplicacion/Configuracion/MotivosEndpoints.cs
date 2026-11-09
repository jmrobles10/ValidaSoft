using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Configuracion;

/// <summary>Motivos de observación y rechazo: los de la plataforma más los propios del tenant (P-5-1), con auditoría.</summary>
public static class MotivosEndpoints
{
    /// <summary>Base de la plataforma primero y luego los del tenant (activos e inactivos).</summary>
    [WolverineGet("/api/v1/motivos")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<IReadOnlyList<MotivoTipificadoDto>> Listar(ISesiones sesiones, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var propios = await sesion.Query<Motivo>().OrderBy(m => m.Codigo).ToListAsync(ct);
        return [.. MotivosBase.Todos.Concat(propios).Select(m => m.ADto())];
    }

    [WolverinePost("/api/v1/motivos")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<IResult> Crear(CrearMotivoDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var motivo = Motivo.Crear(dto.Codigo, dto.Nombre, Categoria(dto.Categoria), Aplica(dto.AplicaA), dto.TextoAlTercero);

        await using var sesion = sesiones.DelTenant();
        var codigo = motivo.Codigo;
        if (await sesion.Query<Motivo>().AnyAsync(m => m.Codigo == codigo, ct))
            throw ReglaDeNegocioException.Conflicto("motivo-duplicado", $"Ya existe el motivo {codigo}.");

        sesion.Store(motivo);
        Auditoria.Registrar(sesion, contexto, "Motivo", motivo.Id.ToString(), AccionConfiguracion.Creado, null, motivo.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/motivos/{motivo.Id}", motivo.ADto());
    }

    /// <summary>El código es inmutable; los motivos de la plataforma no se editan (409 <c>motivo-base</c>).</summary>
    [WolverinePut("/api/v1/motivos/{id}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<MotivoTipificadoDto> Actualizar(Guid id, ActualizarMotivoDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        if (MotivosBase.Todos.Any(m => m.Id == id))
            throw ReglaDeNegocioException.Conflicto("motivo-base", "Los motivos de la plataforma no se modifican.");

        await using var sesion = sesiones.DelTenant();
        var motivo = await sesion.LoadAsync<Motivo>(id, ct)
                     ?? throw ReglaDeNegocioException.NoEncontrado("motivo-no-encontrado", "No existe ese motivo.");
        var antes = motivo.ADto();
        motivo.Actualizar(dto.Nombre, Categoria(dto.Categoria), Aplica(dto.AplicaA), dto.TextoAlTercero, dto.Activo);

        sesion.Store(motivo);
        Auditoria.Registrar(sesion, contexto, "Motivo", motivo.Id.ToString(), AccionConfiguracion.Modificado, antes, motivo.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        return motivo.ADto();
    }

    private static CategoriaMotivo Categoria(string? valor) =>
        Enum.TryParse<CategoriaMotivo>(valor, true, out var categoria) && Enum.IsDefined(categoria)
            ? categoria
            : throw ReglaDeNegocioException.Validacion("categoria-invalida",
                $"'{valor}' no es una categoría ({string.Join(", ", Enum.GetNames<CategoriaMotivo>())}).");

    private static AplicaMotivo Aplica(string? valor) =>
        Enum.TryParse<AplicaMotivo>(valor, true, out var aplica) && Enum.IsDefined(aplica)
            ? aplica
            : throw ReglaDeNegocioException.Validacion("aplica-a-invalido",
                $"'{valor}' no es válido ({string.Join(", ", Enum.GetNames<AplicaMotivo>())}).");
}

/// <summary>Resuelve los códigos que llegan en un veredicto o un rechazo contra el catálogo (base + tenant activos).</summary>
public static class CatalogoDeMotivos
{
    public static async Task<IReadOnlyList<MotivoAplicado>> ResolverAsync(IQuerySession sesion, IEnumerable<string>? codigos, AplicaMotivo decision,
        CancellationToken ct)
    {
        var lista = (codigos ?? []).Select(c => (c ?? string.Empty).Trim().ToLowerInvariant()).Where(c => c.Length > 0).Distinct().ToArray();
        if (lista.Length == 0)
            return [];

        var propios = await sesion.Query<Motivo>().Where(m => m.Codigo.IsOneOf(lista)).ToListAsync(ct);
        var resultado = new List<MotivoAplicado>();
        foreach (var codigo in lista)
        {
            var motivo = MotivosBase.Buscar(codigo) ?? propios.FirstOrDefault(m => m.Codigo == codigo && m.Activo);
            if (motivo is null || !motivo.SirvePara(decision))
                throw ReglaDeNegocioException.Validacion("motivo-invalido",
                    $"'{codigo}' no es un motivo activo para {(decision == AplicaMotivo.Rechazo ? "rechazar" : "observar")}.");
            resultado.Add(new MotivoAplicado(motivo.Codigo, motivo.TextoAlTercero));
        }

        return resultado;
    }
}
