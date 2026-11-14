using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class NotificacionesApiTests
{
    // ---------- colas por tenant (OC-5) ----------

    /// <summary>
    /// El receptor de un tenant no contesta: su entrega ocupa su carril hasta que vence el tiempo de espera (3 s en las pruebas). Las entregas de
    /// otros tenants, que caen en otros carriles, no esperan: llegan mientras la primera sigue sin respuesta. Con la cola secuencial de antes,
    /// todas esperaban al fin de esa llamada. Son tres tenants «sanos»: con 9 carriles, que los tres compartan el del lento es 1 de cada 729.
    /// </summary>
    [Fact]
    public async Task Un_receptor_que_no_responde_de_un_tenant_no_frena_las_entregas_de_otros_tenants()
    {
        var a = await PrepararAsync();
        var sinRespuesta = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var lento = await ReceptorDeWebhooks.IniciarAsync();
        lento.Bloqueo = sinRespuesta.Task;
        await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Srm(a), new CrearWebhookDto(lento.Url, ["vinculo.creado"], [a.EmpresaA.Id.ToString()]), 201);

        var sanos = new List<(Escenario Tenant, ReceptorDeWebhooks Receptor)>();
        try
        {
            for (var n = 0; n < 3; n++)
            {
                var tenant = await PrepararAsync();
                var receptor = await ReceptorDeWebhooks.IniciarAsync();
                sanos.Add((tenant, receptor));
                await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Srm(tenant),
                    new CrearWebhookDto(receptor.Url, ["vinculo.creado"], [tenant.EmpresaA.Id.ToString()]), 201);
            }

            // El primer tenant recibe su entrega y no contesta: su carril queda ocupado.
            await CrearVinculoAsync(a, a.EmpresaA, CorreoNuevo(), "ADP-LENTO");
            await EsperarAsync(() => lento.Recibidos.Count > 0, "la entrega al receptor que no contesta");
            var desde = lento.Recibidos[0].RecibidoEn;

            // Los demás crean su vínculo después: sus entregas llegan mientras el primer receptor sigue sin responder.
            foreach (var (tenant, _) in sanos)
                await CrearVinculoAsync(tenant, tenant.EmpresaA, CorreoNuevo(), "ADP-SANO");
            await EsperarAsync(() => sanos.Any(s => s.Receptor.Recibidos.Any(r => r.Tipo == "vinculo.creado")), "la entrega de otro tenant");
            var primera = sanos.SelectMany(s => s.Receptor.Recibidos).Where(r => r.Tipo == "vinculo.creado").Min(r => r.RecibidoEn);
            (primera - desde).ShouldBeLessThan(TimeSpan.FromSeconds(2.5), "otros tenants esperaron a que venciera el tiempo de la llamada sin respuesta");
        }
        finally
        {
            sinRespuesta.TrySetResult();
            foreach (var (_, receptor) in sanos)
                await receptor.DisposeAsync();
        }
    }
}
