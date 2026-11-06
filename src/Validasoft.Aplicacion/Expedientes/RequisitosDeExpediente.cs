using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>Requisito de una versión de flujo con su tipo del tenant (código, nombre, canónico y ámbito).</summary>
public sealed record RequisitoPedido(RequisitoDelFlujo Requisito, TipoDocumentoTenant Tipo);

/// <summary>
/// Puente entre la bóveda y los requisitos del expediente (RQ-2, RQ-3): qué tipos pide el flujo, qué versión compartida
/// con el vínculo cubre cada requisito y qué vínculos piden un tipo (para compartir una versión nueva). El expediente
/// decide; aquí solo se leen los documentos y se casan con los requisitos.
/// </summary>
public static class RequisitosDeExpediente
{
    /// <summary>Requisitos de la versión del flujo con su tipo del tenant (en el orden del flujo).</summary>
    public static async Task<IReadOnlyList<RequisitoPedido>> DelFlujoAsync(IQuerySession sesionDelTenant, FlujoVersion version, CancellationToken ct)
    {
        if (version.Requisitos.Count == 0)
            return [];

        var ids = version.Requisitos.Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        var tipos = (await sesionDelTenant.LoadManyAsync<TipoDocumentoTenant>(ct, ids)).ToDictionary(t => t.Id);
        return [.. version.Requisitos.Where(r => tipos.ContainsKey(r.TipoDocumentoTenantId)).Select(r => new RequisitoPedido(r, tipos[r.TipoDocumentoTenantId]))];
    }

    /// <summary>Canónicos de los tipos globales que se piden: lo que la bóveda del tercero comparte con el vínculo.</summary>
    public static IReadOnlySet<string> CodigosGlobales(IEnumerable<RequisitoPedido> pedidos) =>
        pedidos.Where(p => p.Tipo.Ambito == AmbitoDocumento.Global && p.Tipo.TipoCanonico is not null)
            .Select(p => p.Tipo.TipoCanonico!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Foto del requisito para <c>RequisitosCalculados</c>, con la versión que ya lo cubre (si está compartida con el vínculo).</summary>
    public static RequisitoCalculado Calcular(RequisitoPedido pedido, IEnumerable<Documento> documentos, Guid vinculoId)
    {
        var tipo = pedido.Tipo;
        return new RequisitoCalculado(pedido.Requisito.Id, tipo.Id, tipo.Codigo, tipo.Nombre, tipo.TipoCanonico, tipo.Ambito,
            pedido.Requisito.Obligatorio, pedido.Requisito.AreasQueRevisan, pedido.Requisito.AprobadoresRequeridos,
            Cubre(tipo.Id, tipo.TipoCanonico, tipo.Ambito, documentos, vinculoId));
    }

    /// <summary>
    /// Versión que cubre un requisito: la actual del documento global del canónico (o del <c>PorVinculo</c> del vínculo y
    /// ese tipo), siempre que ya esté compartida con el vínculo; si no, nula.
    /// </summary>
    public static VersionDeRequisito? Cubre(Guid tipoDocumentoTenantId, string? tipoCanonico, AmbitoDocumento ambito, IEnumerable<Documento> documentos,
        Guid vinculoId)
    {
        var documento = ambito == AmbitoDocumento.Global
            ? documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.Global && tipoCanonico is not null && d.TipoCanonico == tipoCanonico)
            : documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.PorVinculo && d.VinculoId == vinculoId &&
                                             d.TipoDocumentoTenantId == tipoDocumentoTenantId);
        if (documento?.VersionActual is not { } numero || !documento.EstaCompartida(numero, vinculoId))
            return null;

        var version = documento.VersionRequerida(numero);
        return new VersionDeRequisito(documento.Id, numero, version.Sha256, version.CargadaEn);
    }

    /// <summary>
    /// Eventos que ponen al día el expediente con la bóveda: a cada requisito, la versión actual compartida con su vínculo
    /// (<see cref="Expediente.SincronizarVersiones"/>; idempotente). Lee los documentos con <c>ForTenant("boveda")</c> en la
    /// misma sesión del tenant.
    /// </summary>
    public static async Task<IReadOnlyList<object>> SincronizarAsync(IDocumentSession sesionDelTenant, Expediente expediente, DateTimeOffset ahora,
        CancellationToken ct)
    {
        if (!expediente.EstaAbierto || !expediente.ConRequisitos)
            return [];

        var terceroId = expediente.TerceroId;
        var documentos = await sesionDelTenant.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        var disponibles = new Dictionary<Guid, VersionDeRequisito>();
        foreach (var requisito in expediente.Requisitos)
        {
            if (Cubre(requisito.TipoDocumentoTenantId, requisito.TipoCanonico, requisito.Ambito, documentos, expediente.VinculoId) is { } version)
                disponibles[requisito.Id] = version;
        }

        return expediente.SincronizarVersiones(disponibles, ahora);
    }

    /// <summary>
    /// Vínculos (de cualquier tenant) que piden el canónico global <paramref name="codigo"/>: su expediente abierto tiene
    /// un requisito de ese tipo o su flujo vigente (el del vínculo publicado, si no el por defecto) lo pide (RQ-3).
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> VinculosQuePidenAsync(IQuerySession consulta, IReadOnlyList<Vinculo> vinculos, string codigo,
        CancellationToken ct)
    {
        var piden = new HashSet<Guid>();
        if (vinculos.Count == 0)
            return piden;

        var ids = vinculos.Select(v => v.Id).ToArray();
        var expedientes = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.VinculoId.IsOneOf(ids)).ToListAsync(ct);
        foreach (var expediente in expedientes.Where(e => e.EstaAbierto))
        {
            if (expediente.Requisitos.Any(r => r.Ambito == AmbitoDocumento.Global && r.TipoCanonico == codigo))
                piden.Add(expediente.VinculoId);
        }

        var claves = vinculos.Select(v => FlujoPorDefecto.Clave(v.EmpresaId, v.TipoVinculoId)).Distinct().ToArray();
        var porDefecto = await consulta.Query<FlujoPorDefecto>().Where(p => p.AnyTenant() && p.Id.IsOneOf(claves)).ToListAsync(ct);
        var flujoIds = vinculos.Where(v => v.FlujoId is not null).Select(v => v.FlujoId!.Value).Concat(porDefecto.Select(p => p.FlujoId))
            .Distinct().ToArray();
        if (flujoIds.Length == 0)
            return piden;

        var flujos = await consulta.Query<Flujo>().Where(f => f.AnyTenant() && f.Id.IsOneOf(flujoIds)).ToListAsync(ct);
        var tipoIds = flujos.SelectMany(f => f.VersionPublicada?.Requisitos ?? []).Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        if (tipoIds.Length == 0)
            return piden;

        var tipos = await consulta.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct);
        var delCodigo = tipos.Where(t => t.Ambito == AmbitoDocumento.Global && t.TipoCanonico == codigo).Select(t => t.Id).ToHashSet();
        foreach (var vinculo in vinculos)
        {
            if (ProcesoDeValidacion.FlujoVigente(vinculo, flujos, porDefecto)?.Requisitos.Any(r => delCodigo.Contains(r.TipoDocumentoTenantId)) == true)
                piden.Add(vinculo.Id);
        }

        return piden;
    }
}
