using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Dominio.Tests;

/// <summary>Un proceso periódico corre solo en el nodo líder: arranca al ganar el liderazgo, se detiene al perderlo y no corre en un nodo que nunca lo tuvo (OC-4).</summary>
public class ProcesoDelLiderTests
{
    private static ProcesoDelLider Envolver(ProcesoDePrueba proceso, LiderManual lider) => new(proceso, lider, NullLogger<ProcesoDelLider>.Instance);

    [Fact]
    public async Task Un_nodo_que_no_es_lider_nunca_arranca_el_proceso()
    {
        var proceso = new ProcesoDePrueba();
        var envoltura = Envolver(proceso, new LiderManual(esLider: false));

        await envoltura.StartAsync(CancellationToken.None);
        await Task.Delay(100);

        proceso.Arranques.ShouldBe(0);
        envoltura.Corriendo.ShouldBeFalse();
        await envoltura.StopAsync(CancellationToken.None);
        proceso.Detenciones.ShouldBe(0);
    }

    [Fact]
    public async Task Un_nodo_que_ya_es_lider_al_arrancar_corre_el_proceso_y_lo_detiene_al_apagar()
    {
        var proceso = new ProcesoDePrueba();
        var envoltura = Envolver(proceso, new LiderManual(esLider: true));

        await envoltura.StartAsync(CancellationToken.None);

        proceso.Arranques.ShouldBe(1);
        envoltura.Corriendo.ShouldBeTrue();
        await envoltura.StopAsync(CancellationToken.None);
        proceso.Detenciones.ShouldBe(1);
        envoltura.Corriendo.ShouldBeFalse();
    }

    [Fact]
    public async Task Al_ganar_el_liderazgo_arranca_y_al_perderlo_se_detiene_y_puede_volver_a_arrancar()
    {
        var proceso = new ProcesoDePrueba();
        var lider = new LiderManual(esLider: false);
        var envoltura = Envolver(proceso, lider);
        await envoltura.StartAsync(CancellationToken.None);

        lider.Cambiar(true);
        await Esperar(() => proceso.Arranques == 1);
        envoltura.Corriendo.ShouldBeTrue();

        lider.Cambiar(false);
        await Esperar(() => proceso.Detenciones == 1);
        envoltura.Corriendo.ShouldBeFalse();

        lider.Cambiar(true);
        await Esperar(() => proceso.Arranques == 2);
        await envoltura.StopAsync(CancellationToken.None);
        proceso.Detenciones.ShouldBe(2);
    }

    [Fact]
    public async Task Despues_de_apagar_un_cambio_de_liderazgo_ya_no_arranca_nada()
    {
        var proceso = new ProcesoDePrueba();
        var lider = new LiderManual(esLider: false);
        var envoltura = Envolver(proceso, lider);
        await envoltura.StartAsync(CancellationToken.None);
        await envoltura.StopAsync(CancellationToken.None);

        lider.Cambiar(true);
        await Task.Delay(150);

        proceso.Arranques.ShouldBe(0);
    }

    [Fact]
    public async Task El_proceso_se_arranca_sin_el_token_del_arranque_del_host()
    {
        var proceso = new ProcesoDePrueba();
        using var arranque = new CancellationTokenSource();
        var envoltura = Envolver(proceso, new LiderManual(esLider: true));

        await envoltura.StartAsync(arranque.Token);
        arranque.Cancel(); // el host terminó de arrancar: el proceso periódico no debe cancelarse con ese token

        proceso.TokenDeArranque.CanBeCanceled.ShouldBeFalse();
        await envoltura.StopAsync(CancellationToken.None);
    }

    private static async Task Esperar(Func<bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(5);
        while (!condicion())
        {
            DateTime.UtcNow.ShouldBeLessThan(limite, "el proceso no se ajustó al cambio de liderazgo");
            await Task.Delay(10);
        }
    }

    private sealed class LiderManual(bool esLider) : ILiderDeProcesos
    {
        public bool EsLider { get; private set; } = esLider;

        public event Action<bool>? LiderazgoCambio;

        public void Cambiar(bool nuevo)
        {
            EsLider = nuevo;
            LiderazgoCambio?.Invoke(nuevo);
        }
    }

    private sealed class ProcesoDePrueba : IHostedService
    {
        private int _arranques;
        private int _detenciones;

        public int Arranques => Volatile.Read(ref _arranques);

        public int Detenciones => Volatile.Read(ref _detenciones);

        public CancellationToken TokenDeArranque { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _arranques);
            TokenDeArranque = cancellationToken;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _detenciones);
            return Task.CompletedTask;
        }
    }
}
