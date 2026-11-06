using JasperFx.Events;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Compartición de la bóveda con las empresas (B-1, ADR-0009, A-8). Nada se comparte antes de que el tercero acepte la
/// invitación; al aceptarla, la empresa recibe la versión actual de cada tipo global que exige para ese tipo de vínculo
/// (y los <c>PorVinculo</c> ya cargados para ese vínculo); cuando una versión nueva pasa a <c>Actual</c>, se comparte con
/// <b>todos</b> los vínculos aceptados del tercero cuya empresa exige el tipo. Las reglas viven en
/// <see cref="Documento.Compartir"/>; aquí solo se resuelven los candidatos entre tenants.
/// </summary>
public static class ComparticionDeDocumentos
{
    /// <summary>
    /// Al aceptar la invitación, en la misma transacción que <c>InvitacionAceptada</c>: la sesión es la del tenant del
    /// vínculo y los streams de la bóveda se escriben con <c>ForTenant("boveda")</c> sobre la misma unidad de trabajo.
    /// Cada documento se escribe con concurrencia optimista; el llamador reintenta la aceptación si pierde la carrera (BO-6).
    /// </summary>
    public static async Task<IReadOnlyList<CompartidoDto>> AlAceptarInvitacionAsync(IDocumentSession sesionDelTenant, Vinculo vinculo,
        Guid tenantId, DateTimeOffset ahora, CancellationToken ct)
    {
        var tipos = await sesionDelTenant.Query<TipoDocumentoTenant>().Where(t => t.Activo).ToListAsync(ct);
        var exigidos = tipos
            .Where(t => t.Ambito == AmbitoDocumento.Global && t.TipoCanonico != null && t.Exige(vinculo.EmpresaId, vinculo.TipoVinculoId))
            .Select(t => t.TipoCanonico!)
            .ToHashSet(StringComparer.Ordinal);

        var boveda = sesionDelTenant.ForTenant(TenantsTecnicos.Boveda);
        var terceroId = vinculo.TerceroId;
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);

        var compartidos = new List<CompartidoDto>();
        foreach (var candidato in documentos.Where(d => d.VersionActual is not null).OrderBy(d => d.CreadoEn))
        {
            var esDelVinculo = candidato.Ambito == AmbitoDocumento.PorVinculo && candidato.VinculoId == vinculo.Id;
            var exigido = candidato.Ambito == AmbitoDocumento.Global && candidato.TipoCanonico is { } codigo && exigidos.Contains(codigo);
            if (!esDelVinculo && !exigido)
                continue;

            // Concurrencia optimista (no FetchForExclusiveWriting: la operación por tenant tomaría el bloqueo en otra conexión
            // y el guardado de esta sesión esperaría por él). Si una confirmación cambió el documento entretanto, la aceptación
            // entera se reintenta y comparte la versión que quedó actual.
            var stream = await boveda.Events.FetchForWriting<Documento>(candidato.Id, ct);
            var documento = stream.Aggregate!;
            var destino = new VinculoCandidato(vinculo.Id, tenantId, vinculo.EmpresaId, terceroId, InvitacionAceptada: true, vinculo.Estado,
                ExigeElTipo: exigido);
            if (documento.Compartir(destino, OrigenComparticion.AlAceptarInvitacion, ahora) is { } evento)
            {
                stream.AppendOne(evento);
                compartidos.Add(evento.ADto(documento));
            }
        }

        return compartidos;
    }

    /// <summary>
    /// La versión actual de <paramref name="proyectado"/> (el documento con los eventos nuevos ya aplicados) se comparte con
    /// cada vínculo aceptado que corresponda. Se llama con el stream ya tomado con bloqueo: los vínculos se leen después,
    /// así una aceptación simultánea o la ve en su propia transacción o la ve esta.
    /// </summary>
    public static async Task<IReadOnlyList<CompartidoDto>> CompartirActualAsync(IDocumentSession sesionBoveda, IEventStream<Documento> stream,
        Documento proyectado, OrigenComparticion origen, DateTimeOffset ahora, CancellationToken ct)
    {
        if (proyectado.VersionActual is null)
            return [];

        var candidatos = await CandidatosAsync(sesionBoveda, proyectado, ct);
        var eventos = proyectado.CompartirActual(candidatos, origen, ahora);
        foreach (var evento in eventos)
            stream.AppendOne(evento);
        return [.. eventos.Select(e => e.ADto(proyectado))];
    }

    private static async Task<IReadOnlyList<VinculoCandidato>> CandidatosAsync(IQuerySession consulta, Documento documento, CancellationToken ct)
    {
        var terceroId = documento.TerceroId;
        if (documento.Ambito == AmbitoDocumento.PorVinculo)
        {
            var vinculoId = documento.VinculoId!.Value;
            var propio = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == vinculoId).FirstOrDefaultAsync(ct);
            return propio is null
                ? []
                : [new VinculoCandidato(propio.Id, documento.TenantId!.Value, propio.EmpresaId, propio.TerceroId, propio.InvitacionAceptada,
                    propio.Estado, ExigeElTipo: false)];
        }

        var vinculos = await consulta.Query<Vinculo>()
            .Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.InvitacionAceptada)
            .ToListAsync(ct);
        if (vinculos.Count == 0)
            return [];

        var empresaIds = vinculos.Select(v => v.EmpresaId).Distinct().ToArray();
        var empresas = (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);
        var codigo = documento.TipoCanonico;
        var tipos = await consulta.Query<TipoDocumentoTenant>()
            .Where(t => t.AnyTenant() && t.TipoCanonico == codigo && t.Activo)
            .ToListAsync(ct);

        return
        [
            .. vinculos
                .Where(v => empresas.ContainsKey(v.EmpresaId))
                .Select(v =>
                {
                    var tenantId = empresas[v.EmpresaId].TenantId;
                    var exige = tipos.Any(t => t.TenantId == tenantId && t.Ambito == AmbitoDocumento.Global && t.Exige(v.EmpresaId, v.TipoVinculoId));
                    return new VinculoCandidato(v.Id, tenantId, v.EmpresaId, v.TerceroId, v.InvitacionAceptada, v.Estado, exige);
                })
        ];
    }
}
