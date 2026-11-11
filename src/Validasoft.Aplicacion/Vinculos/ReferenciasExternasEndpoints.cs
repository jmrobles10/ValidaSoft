using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Referencias externas de un tercero (IO-2): la clave con la que cada sistema integrador conoce al tercero, para que cualquier ERP,
/// portal o CRM lo busque por su propia clave en lugar de por la identificación. Las del vínculo (<c>/vinculos/{id}/referencias</c>)
/// y las del tercero comparten forma, <c>{sistema, id}</c>, y regla: el sistema lo fija la API key (ADR-0011) y registrar la misma
/// referencia otra vez es idempotente. Solo las ve el tenant que las registró: el tercero es global, sus claves no.
/// </summary>
public static class ReferenciasDeTerceroEndpoints
{
    /// <summary>Las referencias del tercero en el tenant de la credencial (404 si el tercero no es visible para él).</summary>
    [WolverineGet("/api/v1/terceros/{id}/referencias")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<IReadOnlyList<ReferenciaExternaDto>> Listar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        await using var sesion = sesiones.DelTenant();
        await TercerosEndpoints.CargarVisibleAsync(sesion, id, contexto, ct);
        return await ListarAsync(sesion, id, ct);
    }

    /// <summary>
    /// Registra la clave del tercero en el sistema de la credencial. Idempotente: la misma referencia responde 200; nueva, 201. Una
    /// clave que ya identifica a otro tercero es 409 <c>referencia-duplicada</c>. Un tercero puede tener varias claves en un mismo
    /// sistema (p. ej. una por instancia); cada clave identifica a un solo tercero.
    /// </summary>
    [WolverinePost("/api/v1/terceros/{id}/referencias")]
    [Authorize(Policy = Permisos.TercerosEscribir)]
    public static async Task<IResult> Agregar(Guid id, ReferenciaExternaDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var referencia = VinculosEndpoints.ReferenciaDeLaCredencial(dto, contexto)!;
        await using var sesion = sesiones.DelTenant();
        await TercerosEndpoints.CargarVisibleAsync(sesion, id, contexto, ct);
        var creada = await RegistrarAsync(sesion, id, referencia.Sistema, referencia.Id, reloj.GetUtcNow(), ct);
        await sesion.SaveChangesAsync(ct);
        var lista = await ListarAsync(sesion, id, ct);
        return creada ? Results.Created($"/api/v1/terceros/{id}/referencias", lista) : Results.Ok(lista);
    }

    /// <summary>Busca el tercero por la clave que le dio un sistema integrador (404 si no la hay en el tenant o el tercero no es visible).</summary>
    [WolverineGet("/api/v1/terceros/por-referencia")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<TerceroDto> PorReferencia(string sistema, string clave, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sistema) || string.IsNullOrWhiteSpace(clave))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "Indica el sistema y la clave de la referencia.");

        await using var sesion = sesiones.DelTenant();
        var referencia = await sesion.LoadAsync<ReferenciaDeTercero>(ReferenciaDeTercero.IdPara(sistema, clave), ct)
                         ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No hay un tercero con esa referencia.");
        return (await TercerosEndpoints.CargarVisibleAsync(sesion, referencia.TerceroId, contexto, ct)).ADto();
    }

    internal static async Task<IReadOnlyList<ReferenciaExternaDto>> ListarAsync(IQuerySession sesion, Guid terceroId, CancellationToken ct) =>
        [.. (await sesion.Query<ReferenciaDeTercero>().Where(r => r.TerceroId == terceroId).ToListAsync(ct))
            .OrderBy(r => r.RegistradaEn).ThenBy(r => r.Id, StringComparer.Ordinal).Select(r => new ReferenciaExternaDto(r.Sistema, r.Clave))];

    /// <summary>
    /// Anota la referencia en la sesión (la confirma quien llama). <c>true</c> si es nueva; <c>false</c> si ya estaba para ese tercero
    /// (idempotente). Si la clave es de otro tercero, 409 <c>referencia-duplicada</c>.
    /// </summary>
    internal static async Task<bool> RegistrarAsync(IDocumentSession sesion, Guid terceroId, string sistema, string clave, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var nueva = ReferenciaDeTercero.Crear(terceroId, sistema, clave, ahora);
        var existente = await sesion.LoadAsync<ReferenciaDeTercero>(nueva.Id, ct);
        if (existente is null)
        {
            sesion.Insert(nueva);
            return true;
        }

        return existente.TerceroId == terceroId
            ? false
            : throw ReglaDeNegocioException.Conflicto("referencia-duplicada",
                $"La clave {nueva.Clave} de {nueva.Sistema} ya identifica a otro tercero.");
    }
}

/// <summary>Referencias externas del vínculo (IO-2): agregar la clave de otro sistema a un vínculo que ya existe.</summary>
public static class ReferenciasDeVinculoEndpoints
{
    /// <summary>
    /// Agrega al vínculo la referencia del sistema de la credencial (I-0): comparte la validación y recibe sus eventos. Idempotente
    /// (200 con el vínculo); el mismo sistema con otra clave es 409 <c>referencia-en-conflicto</c>; una clave que ya identifica otro
    /// vínculo de la empresa, 409 <c>referencia-duplicada</c>.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/referencias")]
    [Authorize(Policy = Permisos.VinculosEscribir)]
    public static async Task<VinculoDto> Agregar(Guid id, ReferenciaExternaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var referencia = VinculosEndpoints.ReferenciaDeLaCredencial(dto, contexto)!;
        var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
        var agregada = stream.Aggregate!.AgregarReferencia(referencia, reloj.GetUtcNow());
        if (agregada is null)
        {
            var actual = await empresa.CargarVinculoAsync(id, ct);
            return actual.ADto(await empresa.Sesion.LoadAsync<Dominio.Configuracion.TipoVinculo>(actual.TipoVinculoId, ct));
        }

        await VinculosEndpoints.AsegurarReferenciaLibreAsync(empresa, referencia, ct);
        return await VinculosEndpoints.GuardarAsync(empresa, stream, agregada, ct);
    }
}
