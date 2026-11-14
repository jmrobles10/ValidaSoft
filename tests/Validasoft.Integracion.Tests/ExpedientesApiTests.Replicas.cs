using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class ExpedientesApiTests
{
    // ---------- una verificación no se pide ni se paga dos veces (OC-4) ----------

    /// <summary>
    /// Seis pasadas a la vez del monitoreo del mismo vínculo (lo que harían seis réplicas, o dos barridos que se cruzan) a la fecha en que toca la
    /// reconsulta: el proveedor se consulta una sola vez, y por lo tanto el consumo del mes sube en uno. Sin el reclamo, cada pasada pedía y
    /// pagaba su propia consulta.
    /// </summary>
    [Fact]
    public async Task Varias_pasadas_simultaneas_del_monitoreo_consultan_al_proveedor_una_sola_vez()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Relevo S.A.S.", correo);
        await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();
        await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Any(v => v.Estado == "Satisfecha"));
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200))
            .Estado.ShouldBe("Aprobado");

        async Task<int> ConsultasAsync() =>
            (await ApiAsync<ListasRestrictivasConfigDto>("GET", "/api/v1/configuracion/listas-restrictivas", Admin(e), null, 200)).Uso.Sum(u => u.Consultas);
        var antes = await ConsultasAsync();
        antes.ShouldBe(1); // la del expediente

        // Con el reloj 31 días adelante (el monitoreo reconsulta a los 30), seis pasadas al mismo tiempo.
        var tareas = Host.Services.GetRequiredService<TareasDeVerificaciones>().ConReloj(new RelojFijo(DateTimeOffset.UtcNow.AddDays(31)));
        await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(() => tareas.MonitorearAsync(e.TenantId, vinculo.Id, CancellationToken.None))));

        (await ConsultasAsync()).ShouldBe(antes + 1);
        // Y con el monitoreo ya reprogramado no hay nada más que pedir a esa fecha.
        await tareas.MonitorearAsync(e.TenantId, vinculo.Id, CancellationToken.None);
        (await ConsultasAsync()).ShouldBe(antes + 1);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
