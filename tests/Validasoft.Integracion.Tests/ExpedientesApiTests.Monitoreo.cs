using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class ExpedientesApiTests
{
    // ---------- monitoreo del vínculo: la fila sale de la bandeja cuando ya no hay nada por decidir (PP-1, HI-5) ----------

    [Fact]
    public async Task Confirmar_la_coincidencia_del_monitoreo_saca_la_fila_de_la_bandeja_del_oficial_y_una_reconsulta_que_la_encuentra_otra_vez_la_vuelve_a_pedir()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Monitoreo S.A.S.", correo);
        var sesion = await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();
        await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Any(v => v.Estado == "Satisfecha"));

        // Se aprueba sin coincidencias: el vínculo queda activo y en monitoreo, sin nada en la bandeja del oficial.
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200))
            .Estado.ShouldBe("Aprobado");
        (await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200)).ShouldBeEmpty();

        // El tercero suma un socio cuyo nombre dispara el proveedor simulado y pasan 31 días: el monitoreo reconsulta y lo encuentra.
        await TerceroAsync("PUT", "/public/v1/ficha/FICHA_LEGAL", sesion.AccessToken, new GuardarSeccionDto(JsonDocument.Parse("""
            { "personas": [ { "rol": "Socio", "tipo_identificacion": "CC", "numero": "1020304099", "nombres": "Roberto", "apellidos": "Coincidencia Prueba",
                              "pep": false, "vinculo_pep": false } ] }
            """).RootElement.Clone()), 200);
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        var fila = await EsperarFilaDeMonitoreoAsync(e, plataforma, vinculo.Id, DateTimeOffset.UtcNow.AddDays(31));
        fila.CoincidenciasPorAnalizar.ShouldBe(1);

        // Cumplimiento la confirma: ya no hay nada por decidir para el oficial (lo que sigue es del administrador), así que la fila sale de su
        // bandeja; el monitoreo conserva el estado «Fallida» y la decisión, y el vínculo sigue activo hasta que un administrador decida.
        var monitoreo = await ApiAsync<MonitoreoListasDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/monitoreo-listas", Delegado(e, e.Carlos), null, 200);
        var coincidencia = monitoreo.Listas.Coincidencias.ShouldHaveSingleItem();
        var ruta = $"/api/v1/vinculos/{vinculo.Id}/monitoreo-listas/coincidencias/{coincidencia.Id}/resolver";
        var confirmado = await ApiAsync<MonitoreoListasDto>("POST", ruta, Delegado(e, e.Carlos), new ResolverCoincidenciaDto("Confirmada", "Es la misma persona del listado."), 200);
        (confirmado.Listas.Estado, confirmado.Listas.Coincidencias.Single().Decision).ShouldBe(("Fallida", "Confirmada"));
        (await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200)).ShouldBeEmpty();
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).Estado.ShouldBe("Activo");

        // Una confirmada nunca se hereda como resuelta: la siguiente reconsulta periódica la vuelve a encontrar y vuelve a pedirse al oficial.
        var otraVez = await EsperarFilaDeMonitoreoAsync(e, plataforma, vinculo.Id, DateTimeOffset.UtcNow.AddDays(62));
        otraVez.CoincidenciasPorAnalizar.ShouldBe(1);
    }

    /// <summary>Corre el barrido de verificaciones con la fecha simulada hasta que la bandeja del oficial trae la fila del monitoreo del vínculo.</summary>
    private async Task<FilaCumplimientoDto> EsperarFilaDeMonitoreoAsync(Escenario e, Credencial plataforma, Guid vinculoId, DateTimeOffset fecha)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            await ApiAsync<ResultadoRevisionVerificacionesDto>("POST", "/api/v1/plataforma/verificaciones/revisar", plataforma, new RevisarVerificacionesDto(e.TenantId, fecha), 200);
            var bandeja = await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200);
            if (bandeja.FirstOrDefault(f => f.Origen == "Monitoreo" && f.VinculoId == vinculoId) is { } fila)
                return fila;
            DateTime.UtcNow.ShouldBeLessThan(limite, "el monitoreo no llevó la novedad a la bandeja del oficial");
            await Task.Delay(300);
        }
    }
}
