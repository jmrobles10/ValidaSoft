using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Configuracion;

/// <summary>
/// Accesos temporales de soporte de la Plataforma (B-6). Los concede y revoca un administrador del tenant
/// (<c>empresas:administrar</c>); cada cambio queda en la auditoría de configuración. La Plataforma ve los suyos en
/// <c>GET /plataforma/accesos-soporte</c> y entra con <c>X-Acceso-Soporte</c> (solo lectura, sin documentos).
/// </summary>
public static class AccesosSoporteEndpoints
{
    [WolverinePost("/api/v1/accesos-soporte")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<IResult> Conceder(ConcederAccesoSoporteDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var empresa = await EmpresasEndpoints.CargarAsync(dto.EmpresaId, sesiones, contexto, ct);
        if (!empresa.Activa)
            throw ReglaDeNegocioException.Validacion("empresa-inactiva", "La empresa está desactivada.");

        var acceso = AccesoSoporte.Conceder(empresa.TenantId, empresa.Id, dto.Horas, dto.Justificacion, contexto.ActorId, ahora);
        await using var sesion = sesiones.DelTenant();
        sesion.Store(acceso);
        var resultado = acceso.ADto(empresa.Nombre, ahora);
        Auditoria.Registrar(sesion, contexto, "AccesoSoporte", acceso.Id.ToString(), AccionConfiguracion.Creado, null, resultado, ahora, empresa.Id);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/accesos-soporte/{acceso.Id}", resultado);
    }

    /// <summary>Los accesos del tenant, el más reciente primero (vigentes, expirados y revocados).</summary>
    [WolverineGet("/api/v1/accesos-soporte")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<IReadOnlyList<AccesoSoporteDto>> Listar(ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        var accesos = await sesion.Query<AccesoSoporte>().OrderByDescending(a => a.ConcedidoEn).Take(100).ToListAsync(ct);
        var nombres = await NombresDeEmpresasAsync(sesion, accesos.Select(a => a.EmpresaId), ct);
        var ahora = reloj.GetUtcNow();
        return [.. accesos.Select(a => a.ADto(nombres.GetValueOrDefault(a.EmpresaId) ?? string.Empty, ahora))];
    }

    [WolverinePost("/api/v1/accesos-soporte/{id}/revocar")]
    [Authorize(Policy = Permisos.EmpresasAdministrar)]
    public static async Task<AccesoSoporteDto> Revocar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.DelTenant();
        var acceso = await sesion.LoadAsync<AccesoSoporte>(id, ct)
                     ?? throw ReglaDeNegocioException.NoEncontrado("acceso-no-encontrado", "No existe ese acceso de soporte.");
        var nombre = (await NombresDeEmpresasAsync(sesion, [acceso.EmpresaId], ct)).GetValueOrDefault(acceso.EmpresaId) ?? string.Empty;
        var antes = acceso.ADto(nombre, ahora);
        acceso.Revocar(contexto.ActorId, ahora);
        sesion.Store(acceso);
        var despues = acceso.ADto(nombre, ahora);
        Auditoria.Registrar(sesion, contexto, "AccesoSoporte", acceso.Id.ToString(), AccionConfiguracion.Modificado, antes, despues, ahora, acceso.EmpresaId);
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>Plataforma: los accesos vigentes que le concedieron los tenants, para entrar con <c>X-Acceso-Soporte</c>.</summary>
    [WolverineGet("/api/v1/plataforma/accesos-soporte")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<AccesoSoporteConcedidoDto>> Concedidos(ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var consulta = sesiones.ConsultaGlobal();
        var vigentes = await consulta.Query<AccesoSoporte>()
            .Where(a => a.AnyTenant() && a.RevocadoEn == null && a.ExpiraEn > ahora)
            .OrderBy(a => a.ExpiraEn)
            .ToListAsync(ct);
        var empresas = (await consulta.LoadManyAsync<Empresa>(ct, vigentes.Select(a => a.EmpresaId).Distinct())).ToDictionary(e => e.Id);
        var tenants = (await consulta.LoadManyAsync<Tenant>(ct, vigentes.Select(a => a.TenantId).Distinct())).ToDictionary(t => t.Id);
        return [.. vigentes.Select(a => new AccesoSoporteConcedidoDto(a.Id, a.TenantId, tenants.GetValueOrDefault(a.TenantId)?.Nombre ?? string.Empty,
            a.EmpresaId, empresas.GetValueOrDefault(a.EmpresaId)?.Nombre ?? string.Empty, a.Justificacion, a.ExpiraEn))];
    }

    private static async Task<Dictionary<Guid, string>> NombresDeEmpresasAsync(IQuerySession sesion, IEnumerable<Guid> ids, CancellationToken ct) =>
        (await sesion.LoadManyAsync<Empresa>(ct, ids.Distinct())).ToDictionary(e => e.Id, e => e.Nombre);

    private static AccesoSoporteDto ADto(this AccesoSoporte a, string empresa, DateTimeOffset ahora) =>
        new(a.Id, a.EmpresaId, empresa, a.Justificacion, a.ConcedidoPor, a.ConcedidoEn, a.ExpiraEn, a.RevocadoEn, a.RevocadoPor, a.VigenteEn(ahora));
}
