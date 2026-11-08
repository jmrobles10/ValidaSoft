using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;

namespace Validasoft.Infraestructura.Certificados;

/// <summary>
/// Cada <c>Validasoft:Certificados:SegundosProceso</c> (10 por defecto) genera los PDF pendientes y vence los certificados
/// cuya vigencia pasó (CE-5, CE-7). Lo pendiente está en los propios streams, así que una caída no pierde nada: la siguiente
/// pasada lo retoma. Con varias réplicas cada operación toma el stream con bloqueo de fila y revisa el estado antes de
/// actuar (idempotente). Se prefirió a un mensaje de Wolverine porque varios caminos que aprueban expedientes no tienen
/// una sesión enlistada en el outbox (aceptación, reenvío del tercero, sincronización por mensaje, apertura con reintentos).
/// </summary>
public sealed class ProcesadorDeCertificados(ArchivoDeCertificados archivo, TimeProvider reloj, IConfiguration configuracion,
    ILogger<ProcesadorDeCertificados> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, configuracion.GetValue("Validasoft:Certificados:SegundosProceso", 10)));
        using var temporizador = new PeriodicTimer(intervalo, reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await archivo.ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "No se pudieron procesar los certificados pendientes; se reintenta en {Intervalo}", intervalo);
            }
        }
    }
}
