using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// La bóveda compartió (o restituyó) una versión con el vínculo: su expediente abierto debe adjuntarla a los requisitos
/// de ese tipo (RQ-2). Viaja por el outbox de Wolverine (cola local durable <c>requisitos</c>) en la misma transacción que
/// la compartición: si la confirmación se revierte no sale, y si sale se procesa aunque el proceso caiga (se reintenta).
/// Lleva el actor que cargó el documento para el historial del expediente.
/// </summary>
public sealed record SincronizarRequisitosDelVinculo(Guid TenantId, Guid VinculoId, string ActorTipo, string ActorId);

/// <summary>
/// Pone al día el expediente abierto del vínculo con lo que la bóveda le compartió; sin expediente abierto, abre la
/// revalidación si la versión nueva es de un documento ya aprobado (RV-1). Toma el stream con bloqueo de fila
/// (FL-12, en una sesión propia del tenant, así no compite con la del documento) y es <b>idempotente</b>: lee el estado
/// actual de la bóveda y solo adjunta la versión actual si el requisito aún no la tiene, de modo que un mensaje repetido o
/// fuera de orden no duplica eventos ni retrocede versiones.
/// </summary>
public static class SincronizarRequisitosDelVinculoHandler
{
    public static async Task Handle(SincronizarRequisitosDelVinculo mensaje, IDocumentStore store, EmisorDeCertificados certificados, TimeProvider reloj,
        IMessageContext mensajes, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(mensaje.TenantId.ToString("N"));
        sesion.SetHeader("actorTipo", mensaje.ActorTipo);
        sesion.SetHeader("actorId", mensaje.ActorId);

        var vinculo = await sesion.LoadAsync<Vinculo>(mensaje.VinculoId, ct);
        if (vinculo is null)
            return;

        // Sin expediente abierto: si es una versión nueva de algo que la empresa ya aprobó, se revalida (RV-1).
        if (vinculo.ExpedienteAbiertoId is not { } expedienteId)
        {
            var actor = new ActorDeExpediente(Enum.TryParse<ActorTipo>(mensaje.ActorTipo, out var tipo) ? tipo : ActorTipo.Sistema, mensaje.ActorId);
            if (await Revalidacion.AbrirSiCorrespondeAsync(sesion, vinculo.Id, actor, certificados, reloj.GetUtcNow(), ct, mensajes) is not null)
                await sesion.SaveChangesAsync(ct);
            return;
        }

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
