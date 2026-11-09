using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Revalidación por versión nueva (RV-1…RV-3, H6). Cuando la bóveda comparte con un vínculo ya aprobado y sin expediente
/// abierto una versión distinta de un documento que la empresa aprobó en su último expediente, se abre un expediente de
/// <c>Actualizacion</c> solo con esos documentos (nacen cargados, así que pasa directo a revisión), el vínculo conserva su
/// estado y el certificado vigente queda «Con pendientes» hasta que la actualización se apruebe y se reemita.
/// </summary>
public static class Revalidacion
{
    /// <summary>
    /// Abre la actualización si hace falta (idempotente: con un expediente abierto o sin cambios no hace nada). Escribe en
    /// <paramref name="sesion"/> sin guardar. Devuelve el expediente abierto, o nulo.
    /// </summary>
    public static async Task<Expediente?> AbrirSiCorrespondeAsync(IDocumentSession sesion, Guid vinculoId, ActorDeExpediente actor,
        EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct)
    {
        var vinculoStream = await sesion.Events.FetchForWriting<Vinculo>(vinculoId, ct);
        if (vinculoStream.Aggregate is not { } vinculo || vinculo.ExpedienteAbiertoId is not null || !vinculo.TuvoAprobacion ||
            vinculo.Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
            return null;

        var aprobado = (await sesion.Query<Expediente>().Where(e => e.VinculoId == vinculoId && e.Estado == EstadoExpediente.Aprobado).ToListAsync(ct))
            .OrderByDescending(e => e.ActualizadoEn)
            .FirstOrDefault();
        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        if (aprobado is null || flujo is null)
            return null;

        var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct), ct);
        var terceroId = vinculo.TerceroId;
        var documentos = await sesion.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        var afectados = pedidos
            .Where(p => RequisitosDeExpediente.Cubre(p.Tipo.Id, p.Tipo.TipoCanonico, p.Tipo.Ambito, documentos, vinculoId) is { } actual &&
                        AprobadoAntes(aprobado, p) is { } previa &&
                        (previa.DocumentoId != actual.DocumentoId || previa.Numero != actual.Numero))
            .ToList();
        if (afectados.Count == 0)
            return null;

        var expediente = await ProcesoDeValidacion.AbrirAsync(sesion, vinculoStream, flujo, null, certificados, ahora, ct,
            tipoExpediente: TipoExpediente.Actualizacion, soloRequisitos: afectados.Select(p => p.Requisito.Id).ToHashSet(), actor: actor);

        var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        if (expediente.EstaAbierto && certificado.Aggregate?.MarcarConPendientes(MotivoDePendientes(afectados), expediente.Id, ahora) is { } pendientes)
            certificado.AppendOne(pendientes);
        return expediente;
    }

    /// <summary>«Revalidación de Registro Único Tributario y Cámara de comercio».</summary>
    public static string MotivoDePendientes(IReadOnlyList<RequisitoPedido> afectados)
    {
        var nombres = afectados.Select(p => p.Tipo.Nombre).ToList();
        var lista = nombres.Count == 1 ? nombres[0] : $"{string.Join(", ", nombres.Take(nombres.Count - 1))} y {nombres[^1]}";
        return $"Revalidación de {lista}";
    }

    /// <summary>La versión que la empresa aprobó para ese tipo en el expediente (no eximida); nula si no lo aprobó.</summary>
    private static VersionDeRequisito? AprobadoAntes(Expediente aprobado, RequisitoPedido pedido) =>
        aprobado.Requisitos
            .Where(r => !r.Eximido && r.Estado == EstadoRequisito.Aprobado && r.VersionActual is not null &&
                        (r.TipoDocumentoTenantId == pedido.Tipo.Id ||
                         (r.Ambito == AmbitoDocumento.Global && pedido.Tipo.Ambito == AmbitoDocumento.Global &&
                          r.TipoCanonico is not null && r.TipoCanonico == pedido.Tipo.TipoCanonico)))
            .Select(r => r.VersionActual)
            .FirstOrDefault();
}
