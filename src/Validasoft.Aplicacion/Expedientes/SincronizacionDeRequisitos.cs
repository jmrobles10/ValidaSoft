using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Dominio.Certificados;
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
/// revalidación si la versión nueva es de un documento ya aprobado o de una sección ya aceptada (RV-1); con una actualización
/// abierta, además le suma los requisitos que cambiaron después de abrirse y no incluye (<see cref="Revalidacion.AmpliarAsync"/>).
/// Toma el stream con bloqueo de fila
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
        var eventos = (await RequisitosDeExpediente.SincronizarAsync(sesion, expediente, ahora, ct)).ToList();
        // Una actualización abierta amplía su alcance con lo que cambió después de abrirse (la sincronización ya está aplicada).
        var ampliacion = await Revalidacion.AmpliarAsync(sesion, Expediente.Reproducir(expediente, eventos), ahora, ct);
        if (ampliacion is not null)
            eventos.AddRange(ampliacion.Eventos);
        if (eventos.Count == 0)
            return;

        await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, null, certificados, ahora, ct);
        if (ampliacion is not null)
        {
            var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(expediente.VinculoId), ct);
            if (certificado.Aggregate?.ActualizarMotivoDePendientes(ampliacion.Motivo, expediente.Id, ahora) is { } pendientes)
                certificado.AppendOne(pendientes);
            // Un requisito con un plazo de revalidación menor acorta el de toda la actualización (RV-4): el mensaje de la apertura
            // llegaría tarde, así que se programa también uno al nuevo plazo (idempotente, como el primero).
            var ampliado = Expediente.Reproducir(expediente, eventos);
            if (ampliado.PlazoDeRevalidacionHasta < expediente.PlazoDeRevalidacionHasta)
                await mensajes.ScheduleAsync(new VencerPlazoDeRevalidacion(mensaje.TenantId, expediente.VinculoId, expediente.Id), ampliado.PlazoDeRevalidacionHasta);
        }

        await sesion.SaveChangesAsync(ct);
    }
}
