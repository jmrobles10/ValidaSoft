using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- Inicio: mis pendientes e indicadores (IN-1, IN-2) ----------

    [Fact]
    public async Task El_inicio_muestra_a_cada_revisor_lo_de_sus_areas_y_los_indicadores_siguen_al_expediente()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var antes = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores", Admin(e.Norte), null, 200);
        antes.Dias.ShouldBe(30);
        antes.VinculosInvitados.ShouldBe(1);

        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        await AceptarAsync(sesion, vinculo.Id);
        var expediente = await ExpedienteAbiertoAsync(e, vinculo.Id);

        // Cada revisor ve solo sus áreas; la integración, todas.
        async Task<MisPendientesDto> Pendientes(Credencial credencial) =>
            await ApiAsync<MisPendientesDto>("GET", "/api/v1/mis-pendientes", credencial, null, 200);
        var laura = await Pendientes(Delegado(e.Norte, e.Laura));
        laura.PorArea.ShouldHaveSingleItem().Codigo.ShouldBe("COMPRAS");
        laura.PorArea.Single().Cantidad.ShouldBe(1);
        laura.PorArea.Single().MasAntiguoDias.ShouldBe(0);
        laura.CuentasPorAprobar.ShouldBe(0);
        (await Pendientes(Delegado(e.Norte, e.Carlos))).PorArea.ShouldHaveSingleItem().Codigo.ShouldBe("CUMPLIMIENTO");
        (await Pendientes(Admin(e.Norte))).PorArea.Select(a => (a.Codigo, a.Cantidad)).OrderBy(a => a.Codigo)
            .ShouldBe([("COMPRAS", 1), ("CUMPLIMIENTO", 1)]);

        var enCurso = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores", Admin(e.Norte), null, 200);
        enCurso.VinculosEnValidacion.ShouldBe(1);
        enCurso.ExpedientesEnCurso.ShouldBe(1);
        enCurso.ExpedientesAprobados.ShouldBe(0);
        enCurso.TiempoPromedioDias.ShouldBeNull();

        // Aprobado: sale de los pendientes y entra en los indicadores de flujo.
        await AprobarAreasAsync(e, expediente);
        (await Pendientes(Delegado(e.Norte, e.Laura))).PorArea.Single().Cantidad.ShouldBe(0);
        var despues = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores?dias=7", Admin(e.Norte), null, 200);
        despues.Dias.ShouldBe(7);
        despues.VinculosActivos.ShouldBe(1);
        despues.ExpedientesEnCurso.ShouldBe(0);
        despues.ExpedientesAprobados.ShouldBe(1);
        despues.TiempoPromedioDias.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(0);
        despues.TiempoMedianaDias.ShouldBe(despues.TiempoPromedioDias);
        despues.CertificadosVigentes.ShouldBe(1);
        despues.CertificadosNoValidos.ShouldBe(0);
    }
}
