using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Quién es el nodo líder de los procesos periódicos (OC-4). Con varias réplicas, los barridos, las limpiezas y los procesadores de
/// pendientes corren solo en el líder; si el líder cae, otra réplica toma el relevo. Los procesos que cada nodo necesita para sí (la
/// escucha de invalidaciones, el volcado de su conteo de llamadas) no pasan por aquí.
/// </summary>
public interface ILiderDeProcesos
{
    /// <summary>Este nodo es hoy el líder.</summary>
    bool EsLider { get; }

    /// <summary>Se dispara al ganar (<c>true</c>) o al perder (<c>false</c>) el liderazgo.</summary>
    event Action<bool>? LiderazgoCambio;
}

/// <summary>Un solo nodo (sin elección): siempre es el líder. Para instalaciones de una réplica y para las pruebas.</summary>
public sealed class LiderUnico : ILiderDeProcesos
{
    public bool EsLider => true;

    public event Action<bool>? LiderazgoCambio
    {
        add { }
        remove { }
    }
}

/// <summary>
/// Un proceso periódico que solo corre en el nodo líder (OC-4): lo arranca al ganar el liderazgo y lo detiene al perderlo (o al apagar el
/// nodo). El proceso es el <see cref="BackgroundService"/> de siempre; la decisión de quién lo corre vive aquí, en un solo lugar, y no en el bucle
/// de cada proceso. Se registra con <see cref="LiderDeProcesosExtensiones.AgregarProcesoDelLider{T}"/>.
/// </summary>
public sealed class ProcesoDelLider(IHostedService proceso, ILiderDeProcesos lider, ILogger<ProcesoDelLider> logger) : IHostedService, IDisposable
{
    private readonly SemaphoreSlim _turno = new(1, 1);
    private bool _corriendo;
    private bool _detenido;

    public bool Corriendo => _corriendo;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        lider.LiderazgoCambio += AlCambiarElLiderazgo;
        await ReconciliarAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        lider.LiderazgoCambio -= AlCambiarElLiderazgo;
        await _turno.WaitAsync(cancellationToken);
        try
        {
            _detenido = true;
            await DetenerAsync(cancellationToken);
        }
        finally
        {
            _turno.Release();
        }
    }

    public void Dispose() => _turno.Dispose();

    private void AlCambiarElLiderazgo(bool esLider) =>
        // Sin bloquear a quien notifica (el hilo que vigila el liderazgo): se reconcilia con el liderazgo de ese momento, en segundo plano.
        _ = Task.Run(async () =>
        {
            try
            {
                await ReconciliarAsync(CancellationToken.None);
            }
            catch (Exception e)
            {
                logger.LogError(e, "No se pudo ajustar el proceso periódico {Proceso} al cambio de liderazgo.", proceso.GetType().Name);
            }
        });

    /// <summary>Deja el proceso como toca según el liderazgo de ahora: corriendo si este nodo es el líder, detenido si no.</summary>
    private async Task ReconciliarAsync(CancellationToken ct)
    {
        await _turno.WaitAsync(ct);
        try
        {
            if (_detenido)
                return;
            if (lider.EsLider && !_corriendo)
            {
                // Sin el token del arranque: el proceso no debe cancelarse cuando termine el arranque del host.
                await proceso.StartAsync(CancellationToken.None);
                _corriendo = true;
                logger.LogInformation("Este nodo es el líder: arranca el proceso periódico {Proceso}.", proceso.GetType().Name);
            }
            else if (!lider.EsLider && _corriendo)
            {
                await DetenerAsync(CancellationToken.None);
                logger.LogInformation("Este nodo dejó de ser el líder: se detiene el proceso periódico {Proceso}.", proceso.GetType().Name);
            }
        }
        finally
        {
            _turno.Release();
        }
    }

    private async Task DetenerAsync(CancellationToken ct)
    {
        if (!_corriendo)
            return;
        _corriendo = false;
        await proceso.StopAsync(ct);
    }
}

public static class LiderDeProcesosExtensiones
{
    /// <summary>
    /// Registra un proceso periódico (<see cref="BackgroundService"/>) que corre solo en el nodo líder. Reemplaza a
    /// <c>AddHostedService&lt;T&gt;()</c> en los procesos que con varias réplicas no deben repetirse.
    /// </summary>
    public static IServiceCollection AgregarProcesoDelLider<T>(this IServiceCollection servicios) where T : BackgroundService
    {
        servicios.AddSingleton<T>();
        servicios.AddSingleton<IHostedService>(proveedor => new ProcesoDelLider(proveedor.GetRequiredService<T>(),
            proveedor.GetRequiredService<ILiderDeProcesos>(), proveedor.GetRequiredService<ILogger<ProcesoDelLider>>()));
        return servicios;
    }
}
