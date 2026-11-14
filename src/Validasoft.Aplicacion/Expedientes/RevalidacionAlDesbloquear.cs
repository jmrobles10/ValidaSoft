using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Un vínculo bloqueado volvió a su estado (desbloqueo manual o fin de un bloqueo temporal): hay que ponerlo al día con lo que
/// pasó mientras estuvo congelado (PP-9, B-5, VR-10 (1)). Viaja por el outbox de Wolverine (cola local durable <c>requisitos</c>) en
/// la misma transacción del desbloqueo, como <see cref="SincronizarRequisitosDelVinculo"/>. Lleva quién desbloqueó para el historial.
/// </summary>
public sealed record RevalidarVinculoDesbloqueado(Guid TenantId, Guid VinculoId, string ActorTipo, string ActorId);

/// <summary>
/// Al desbloquear un vínculo, (1) se le restituye la compartición de la bóveda con la <b>versión actual</b> de lo que su flujo pide
/// (mientras estuvo bloqueado la compartición hacia adelante estaba congelada, B-5: una versión nueva no le llegó) y (2) se sincroniza
/// como cuando llega una versión nueva (<see cref="SincronizarRequisitosDelVinculoHandler"/>): con un expediente abierto se adjuntan
/// las versiones al día; sin él, <see cref="Revalidacion.AbrirSiCorrespondeAsync"/> abre la actualización si lo que la empresa aprobó
/// (o aceptó) difiere de lo actual. Así el vínculo queda «En revisión» y el certificado «En verificación» (VR-3, VR-11). Idempotente:
/// sin diferencias no cambia nada, y un mensaje repetido no duplica comparticiones ni expedientes.
/// </summary>
public static class RevalidarVinculoDesbloqueadoHandler
{
    public static async Task Handle(RevalidarVinculoDesbloqueado mensaje, IDocumentStore store, EmisorDeCertificados certificados, TimeProvider reloj,
        IMessageContext mensajes, CancellationToken ct)
    {
        await Peticiones.ConReintentosAsync(async () =>
        {
            await RestituirComparticionAsync(mensaje, store, reloj.GetUtcNow(), ct);
            return true;
        });
        await SincronizarRequisitosDelVinculoHandler.Handle(new SincronizarRequisitosDelVinculo(mensaje.TenantId, mensaje.VinculoId, mensaje.ActorTipo, mensaje.ActorId),
            store, certificados, reloj, mensajes, ct);
    }

    /// <summary>
    /// Comparte con el vínculo la versión actual de cada documento global que pide su flujo (y de sus <c>PorVinculo</c>). Si el vínculo
    /// se volvió a bloquear, terminó o no aceptó la invitación, no hace nada. Cada intento abre su propia sesión (concurrencia
    /// optimista sobre los streams de la bóveda, BO-6).
    /// </summary>
    private static async Task RestituirComparticionAsync(RevalidarVinculoDesbloqueado mensaje, IDocumentStore store, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(mensaje.TenantId.ToString("N"));
        sesion.SetHeader("actorTipo", mensaje.ActorTipo);
        sesion.SetHeader("actorId", mensaje.ActorId);

        var vinculo = await sesion.LoadAsync<Vinculo>(mensaje.VinculoId, ct);
        if (vinculo is not { InvitacionAceptada: true, BovedaDisponible: true } || vinculo.Estado is EstadoVinculo.Bloqueado or EstadoVinculo.Desvinculado or EstadoVinculo.Anulado)
            return;

        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        if (flujo is null)
            return;

        var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
        var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, contexto, ct);
        var compartidos = await ComparticionDeDocumentos.CompartirConVinculoAsync(sesion, vinculo, mensaje.TenantId,
            RequisitosDeExpediente.CodigosGlobales(pedidos), OrigenComparticion.Automatica, ahora, ct);
        if (compartidos.Compartidos.Count > 0)
            await sesion.SaveChangesAsync(ct);
    }
}
