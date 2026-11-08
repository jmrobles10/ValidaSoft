using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// La bóveda compartió (o restituyó) una versión con el vínculo: su expediente abierto debe adjuntarla a los requisitos
/// de ese tipo (RQ-2). Viaja por el outbox de Wolverine (cola local durable <c>requisitos</c>) en la misma transacción que
/// la compartición: si la confirmación se revierte no sale, y si sale se procesa aunque el proceso caiga (se reintenta).
/// Lleva el actor que cargó el documento para el historial del expediente.
/// </summary>
public sealed record SincronizarRequisitosDelVinculo(Guid TenantId, Guid VinculoId, string ActorTipo, string ActorId);

/// <summary>
/// Pone al día el expediente abierto del vínculo con lo que la bóveda le compartió. Toma el stream con bloqueo de fila
/// (FL-12, en una sesión propia del tenant, así no compite con la del documento) y es <b>idempotente</b>: lee el estado
/// actual de la bóveda y solo adjunta la versión actual si el requisito aún no la tiene, de modo que un mensaje repetido o
/// fuera de orden no duplica eventos ni retrocede versiones.
/// </summary>
public static class SincronizarRequisitosDelVinculoHandler
{
    public static async Task Handle(SincronizarRequisitosDelVinculo mensaje, IDocumentStore store, EmisorDeCertificados certificados, TimeProvider reloj,
        CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(mensaje.TenantId.ToString("N"));
        sesion.SetHeader("actorTipo", mensaje.ActorTipo);
        sesion.SetHeader("actorId", mensaje.ActorId);

        if (await sesion.LoadAsync<Vinculo>(mensaje.VinculoId, ct) is not { ExpedienteAbiertoId: { } expedienteId })
            return;

        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        if (stream.Aggregate is not { EstaAbierto: true } expediente)
            return;

        var ahora = reloj.GetUtcNow();
        var eventos = await RequisitosDeExpediente.SincronizarAsync(sesion, expediente, ahora, ct);
        if (eventos.Count == 0)
            return;

        await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, null, certificados, ahora, ct);
        await sesion.SaveChangesAsync(ct);
    }
}
