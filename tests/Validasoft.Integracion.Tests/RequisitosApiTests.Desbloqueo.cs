using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- desbloquear revalida (PP-9: B-5 y VR-10 (1)) ----------

    /// <summary>
    /// Un vínculo bloqueado no recibe la versión nueva (B-5): sigue bloqueado y sin expediente abierto. Se le da un momento al mensaje de
    /// sincronización (que no debería salir) y se comprueba que nada se abrió.
    /// </summary>
    private async Task AsegurarQueElVinculoBloqueadoNoRecibioNadaAsync(Empresa empresa, Guid vinculoId, Guid inicial)
    {
        await Task.Delay(1500);
        var lista = await ApiAsync<PaginaDto<ExpedienteDto>>("GET", $"/api/v1/expedientes?vinculoId={vinculoId}", Admin(empresa), null, 200);
        lista.Datos.Select(x => x.Id).ShouldBe([inicial]);
        var vinculo = await VinculoDeAsync(empresa, vinculoId);
        (vinculo.Estado, vinculo.EnRevision, vinculo.ExpedienteAbiertoId).ShouldBe(("Bloqueado", false, (Guid?)null));
    }

    [Fact]
    public async Task Desbloquear_un_vinculo_al_que_no_llego_la_version_nueva_le_restituye_la_comparticion_y_abre_la_actualizacion_con_el_certificado_en_verificacion()
    {
        var (e, vinculo, sesion, _, inicial) = await AprobadoConRechazoPosibleAsync();

        // Bloqueado: el certificado queda suspendido y la versión nueva del RUT no le llega (B-5).
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e.Norte), new BloquearVinculoDto("Revisión de sanciones", null), 200);
        await SubirAsync(sesion, "RUT", Pdf("RUT v2 con el vínculo bloqueado"));
        await AsegurarQueElVinculoBloqueadoNoRecibioNadaAsync(e.Norte, vinculo.Id, inicial);
        (await CertificadoActualAsync(e.Norte, vinculo.Id)).Estado.ShouldBe("Suspendido");

        // Al desbloquearlo se pone al día: se le restituye la compartición con la versión actual y se abre la actualización del RUT, porque
        // lo que la empresa aprobó (la versión 1) ya no es lo actual (la 2).
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desbloquear", Admin(e.Norte), new JustificacionDto("Se levantó la sanción."), 200);
        var actualizacion = await ActualizacionDeAsync(e.Norte, vinculo.Id);
        var enRevision = await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        enRevision.Tipo.ShouldBe("Actualizacion");
        Requisito(enRevision, "RUT").VersionActual!.N.ShouldBe(2);

        var revisado = await VinculoDeAsync(e.Norte, vinculo.Id);
        (revisado.Estado, revisado.EnRevision, revisado.ExpedienteAbiertoId).ShouldBe(("Activo", true, (Guid?)actualizacion));
        // El certificado se restableció y, con la actualización abierta, se ve «En verificación» (VR-11): vigente, sin bloquear.
        var certificado = await CertificadoActualAsync(e.Norte, vinculo.Id);
        (certificado.Estado, certificado.EnVerificacion, certificado.Bloqueante).ShouldBe(("ConPendientes", true, false));

        // La compartición quedó restituida con la versión actual y el historial del vínculo cuenta el desbloqueo y la actualización.
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", Admin(e.Norte), null, 200);
        var tipos = historial.Select(h => h.Tipo).ToList();
        tipos.ShouldContain("vinculo_bloqueado");
        tipos.ShouldContain("vinculo_desbloqueado");
        tipos.Count(t => t == "expediente_vinculado").ShouldBe(2);
    }

    [Fact]
    public async Task Desbloquear_un_vinculo_sin_diferencias_con_lo_aprobado_no_abre_ninguna_actualizacion()
    {
        var (e, vinculo, _, _, inicial) = await AprobadoConRechazoPosibleAsync();
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e.Norte), new BloquearVinculoDto("Revisión de sanciones", null), 200);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desbloquear", Admin(e.Norte), new JustificacionDto("Sin hallazgos."), 200);

        await Task.Delay(1500);
        var lista = await ApiAsync<PaginaDto<ExpedienteDto>>("GET", $"/api/v1/expedientes?vinculoId={vinculo.Id}", Admin(e.Norte), null, 200);
        lista.Datos.Select(x => x.Id).ShouldBe([inicial]);
        var activo = await VinculoDeAsync(e.Norte, vinculo.Id);
        (activo.Estado, activo.EnRevision).ShouldBe(("Activo", false));
        (await CertificadoActualAsync(e.Norte, vinculo.Id)).Estado.ShouldBe("Vigente");
    }

    [Fact]
    public async Task El_bloqueo_con_fecha_tambien_revalida_al_levantarse_solo()
    {
        var (e, vinculo, sesion, _, inicial) = await AprobadoConRechazoPosibleAsync();
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e.Norte),
            new BloquearVinculoDto("Revisión rápida", DateTimeOffset.UtcNow.AddSeconds(10)), 200);
        await SubirAsync(sesion, "RUT", Pdf("RUT v2 mientras dura el bloqueo temporal"));

        // Lo levanta el mensaje programado (mismo efecto del manual): el vínculo vuelve y se abre la actualización por sí sola.
        var actualizacion = await ActualizacionDeAsync(e.Norte, vinculo.Id);
        actualizacion.ShouldNotBe(inicial);
        var enRevision = await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        Requisito(enRevision, "RUT").VersionActual!.N.ShouldBe(2);
        var revisado = await VinculoDeAsync(e.Norte, vinculo.Id);
        (revisado.Estado, revisado.EnRevision, revisado.Bloqueo).ShouldBe(("Activo", true, null));
        (await CertificadoActualAsync(e.Norte, vinculo.Id)).EnVerificacion.ShouldBeTrue();
    }
}
