using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Lecturas de identidad para las pantallas (RQ-9): quién hace la petición (<c>GET /yo</c>) y el nombre visible de los
/// usuarios que aparecen en expedientes y bandejas (<c>GET /usuarios/resumen</c>), sin exponer correos ni roles.
/// </summary>
public static class QuienSoyEndpoints
{
    public const int MaximoIds = 100;

    /// <summary>
    /// Actor de la petición, su usuario (si es una persona: identidad delegada o sesión de Validasoft), sus roles en las
    /// empresas a su alcance, los permisos efectivos en la empresa activa (<c>X-Empresa</c>), el sistema integrado de la
    /// credencial y las empresas que puede usar. Cualquier credencial de un tenant.
    /// </summary>
    [WolverineGet("/api/v1/yo")]
    [Authorize]
    public static async Task<YoDto> Yo(IContextoSolicitud contexto, ISesiones sesiones, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        Usuario? usuario = null;
        if (contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuarioId))
        {
            await using var sesion = sesiones.DelTenant();
            usuario = await sesion.LoadAsync<Usuario>(usuarioId, ct);
        }

        await using var consulta = sesiones.ConsultaGlobal();
        var empresas = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).OrderBy(e => e.Nombre).ToListAsync(ct);
        var alcance = empresas.Where(e => contexto.Empresas.Contains("*") || contexto.Empresas.Contains(e.Id.ToString())).ToList();

        var roles = usuario is null
            ? []
            : usuario.Roles.Where(r => r.EmpresaId is null || alcance.Any(e => e.Id == r.EmpresaId)).Select(r => r.ADto()).ToList();
        return new YoDto(contexto.ActorTipo.ToString(), contexto.ActorId, tenantId,
            usuario is null ? null : new UsuarioResumenDto(usuario.Id, usuario.Nombre), roles, [.. contexto.Scopes.Order(StringComparer.Ordinal)],
            contexto.Sistema, [.. alcance.Select(e => new EmpresaResumenDto(e.Id, e.Nombre))], contexto.EmpresaId);
    }

    /// <summary>
    /// <c>ids</c> separados por coma (hasta <see cref="MaximoIds"/>): <c>{ id, nombre }</c> de los usuarios del tenant que
    /// existan (activos o no: pueden haber decidido antes); los demás se omiten. Para cualquier credencial con
    /// <c>expedientes:leer</c> o <c>vinculos:leer</c> (si no, 403 <c>permiso-insuficiente</c>).
    /// </summary>
    [WolverineGet("/api/v1/usuarios/resumen")]
    [Authorize]
    public static async Task<IReadOnlyList<UsuarioResumenDto>> Resumen(string? ids, IContextoSolicitud contexto, ISesiones sesiones,
        CancellationToken ct)
    {
        if (!contexto.Scopes.Contains(Permisos.ExpedientesLeer) && !contexto.Scopes.Contains(Permisos.VinculosLeer))
            throw ReglaDeNegocioException.Prohibido("permiso-insuficiente", "Se necesita expedientes:leer o vinculos:leer.");

        var lista = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(i => Guid.TryParse(i, out var id)
                ? id
                : throw ReglaDeNegocioException.Validacion("ids-invalidos", $"'{i}' no es un identificador de usuario."))
            .Distinct()
            .ToArray();
        if (lista.Length > MaximoIds)
            throw ReglaDeNegocioException.Validacion("ids-invalidos", $"Pide como máximo {MaximoIds} usuarios por consulta.");
        if (lista.Length == 0)
            return [];

        await using var sesion = sesiones.DelTenant();
        var usuarios = await sesion.LoadManyAsync<Usuario>(ct, lista);
        return [.. usuarios.OrderBy(u => u.Nombre).Select(u => new UsuarioResumenDto(u.Id, u.Nombre))];
    }
}
