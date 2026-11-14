using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// El cortacircuito de los proveedores de listas se lleva por empresa y proveedor, con un contador atómico (OC-3): la clave vencida de una
/// empresa no corta a las demás y ningún fallo se pierde cuando varias consultas terminan a la vez.
/// </summary>
public class ControlDeProveedoresTests
{
    private const string Proveedor = "Infolaft";

    private static OpcionesDeProveedor Opciones(int fallosParaAbrir = 3, int concurrencia = 4, int segundosAbierto = 60) =>
        new(Proveedor, true, null, TimeSpan.FromSeconds(30), concurrencia, fallosParaAbrir, TimeSpan.FromSeconds(segundosAbierto));

    private static Task<int> Falla(ControlDeProveedores control, Guid empresa, OpcionesDeProveedor opciones) =>
        control.EjecutarAsync<int>(empresa, opciones, _ => throw new HttpRequestException("401"), CancellationToken.None);

    private static Task<int> Acierta(ControlDeProveedores control, Guid empresa, OpcionesDeProveedor opciones) =>
        control.EjecutarAsync(empresa, opciones, _ => Task.FromResult(7), CancellationToken.None);

    [Fact]
    public async Task La_clave_vencida_de_una_empresa_abre_su_cortacircuito_y_las_demas_siguen_consultando()
    {
        var control = new ControlDeProveedores(new RelojManual());
        var opciones = Opciones(fallosParaAbrir: 3);
        var vencida = Guid.NewGuid();
        var sana = Guid.NewGuid();

        for (var n = 0; n < 3; n++)
            await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, vencida, opciones));

        // La empresa con la clave vencida recibe el corte inmediato (sin llamar al proveedor); la otra empresa consulta con normalidad.
        var llamadas = 0;
        var corte = await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() =>
            control.EjecutarAsync(vencida, opciones, _ => { llamadas++; return Task.FromResult(1); }, CancellationToken.None));
        corte.Message.ShouldContain("falló varias veces seguidas");
        llamadas.ShouldBe(0);
        (await Acierta(control, sana, opciones)).ShouldBe(7);
        control.Estado(sana, Proveedor).AbiertoHasta.ShouldBeNull();
        control.Estado(vencida, Proveedor).AbiertoHasta.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cada_proveedor_de_la_misma_empresa_lleva_su_propio_cortacircuito()
    {
        var control = new ControlDeProveedores(new RelojManual());
        var empresa = Guid.NewGuid();
        for (var n = 0; n < 3; n++)
            await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, Opciones()));

        var otro = Opciones() with { Nombre = "Stradata" };
        (await Acierta(control, empresa, otro)).ShouldBe(7);
        control.Estado(empresa, "Stradata").Fallos.ShouldBe(0);
    }

    [Fact]
    public async Task Los_fallos_simultaneos_de_una_empresa_no_se_pierden()
    {
        const int consultas = 200;
        var control = new ControlDeProveedores(new RelojManual());
        var opciones = Opciones(fallosParaAbrir: 10_000, concurrencia: consultas); // no abre: cuenta todos los fallos
        var empresa = Guid.NewGuid();
        var adentro = 0;
        var salida = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tareas = Enumerable.Range(0, consultas).Select(_ => Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() =>
            control.EjecutarAsync<int>(empresa, opciones, async _ =>
            {
                // Todas esperan aquí y fallan al mismo tiempo: sin contador atómico, leer-sumar-escribir pierde fallos.
                if (Interlocked.Increment(ref adentro) == consultas)
                    salida.SetResult();
                await salida.Task;
                throw new HttpRequestException("500");
            }, CancellationToken.None))).ToArray();
        await Task.WhenAll(tareas);

        control.Estado(empresa, Proveedor).Fallos.ShouldBe(consultas);
    }

    [Fact]
    public async Task Un_exito_reinicia_la_cuenta_y_un_error_de_negocio_no_cuenta()
    {
        var control = new ControlDeProveedores(new RelojManual());
        var opciones = Opciones(fallosParaAbrir: 3);
        var empresa = Guid.NewGuid();

        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, opciones));
        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, opciones));
        await Should.ThrowAsync<ReglaDeNegocioException>(() => control.EjecutarAsync<int>(empresa, opciones,
            _ => throw ReglaDeNegocioException.Validacion("x", "regla"), CancellationToken.None));
        control.Estado(empresa, Proveedor).Fallos.ShouldBe(2);

        (await Acierta(control, empresa, opciones)).ShouldBe(7);
        control.Estado(empresa, Proveedor).Fallos.ShouldBe(0);
        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, opciones));
        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, opciones));
        control.Estado(empresa, Proveedor).AbiertoHasta.ShouldBeNull(); // volvió a contar desde cero: dos fallos no abren
    }

    [Fact]
    public async Task Pasado_el_tiempo_abierto_deja_pasar_una_consulta_y_un_fallo_lo_vuelve_a_abrir()
    {
        var reloj = new RelojManual();
        var control = new ControlDeProveedores(reloj);
        var opciones = Opciones(fallosParaAbrir: 2, segundosAbierto: 60);
        var empresa = Guid.NewGuid();
        for (var n = 0; n < 2; n++)
            await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => Falla(control, empresa, opciones));
        control.Estado(empresa, Proveedor).AbiertoHasta.ShouldBe(reloj.GetUtcNow().AddSeconds(60));

        reloj.Avanzar(TimeSpan.FromSeconds(61));
        var llamadas = 0;
        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() => control.EjecutarAsync<int>(empresa, opciones,
            _ => { llamadas++; throw new HttpRequestException("otra vez"); }, CancellationToken.None));
        llamadas.ShouldBe(1); // pasó al proveedor
        control.Estado(empresa, Proveedor).AbiertoHasta.ShouldBe(reloj.GetUtcNow().AddSeconds(60)); // y quedó abierto otra vez
    }

    private sealed class RelojManual : TimeProvider
    {
        private DateTimeOffset _ahora = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
    }
}
