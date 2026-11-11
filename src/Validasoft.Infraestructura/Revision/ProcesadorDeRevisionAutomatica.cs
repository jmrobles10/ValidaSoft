using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Revision;

namespace Validasoft.Infraestructura.Revision;

/// <summary>
/// Cada <c>Validasoft:RevisionAutomatica:SegundosProceso</c> (30 por omisión; 0 lo apaga) revisa los expedientes de las empresas con
/// revisión automática (ADR-0012). Lo pendiente vive en los propios expedientes (campo plano indexado), así que una caída no pierde
/// nada y cada paso es idempotente.
/// </summary>
public sealed class ProcesadorDeRevisionAutomatica(TareasDeRevisionAutomatica tareas, OpcionesDeRevisionAutomatica opciones, TimeProvider reloj,
    ILogger<ProcesadorDeRevisionAutomatica> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!opciones.Habilitada)
            return;

        using var temporizador = new PeriodicTimer(opciones.Intervalo, reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await tareas.ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "No se pudo procesar la revisión automática; se reintenta en {Intervalo}", opciones.Intervalo);
            }
        }
    }
}
