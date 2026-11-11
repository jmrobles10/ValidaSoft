using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Desaprobar (DS-1, DS-2), reintegrar sin revalidar (DS-3) y reinicio tras un rechazo (DS-4): reabrir una etapa aprobada, desaprobar
/// un expediente aprobado (el vínculo vuelve a validación y su certificado se revoca), volver a la fase inicial, reintegrar con y sin
/// revalidar y el «Volver a postularme» del tercero rechazado.
/// </summary>
public sealed partial class ExpedientesApiTests
{
    /// <summary>Aprobador inicial en la etapa 1 y Cumplimiento en la 2, con el flujo ya publicado.</summary>
    private async Task CrearFlujoEnDosEtapasAsync(Escenario e, string tipo) =>
        await CrearFlujoAsync(e, tipo, [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id)]), new EtapaDto(2, [new AreaDeEtapaDto(e.Cumplimiento.Id)])]);

    private Task<ExpedienteDto> AbrirManualAsync(Escenario e, VinculoDto vinculo) =>
        ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

    private Task<ExpedienteDto> AprobarAreaAsync(Escenario e, Guid expedienteId, AreaDto area, Persona persona) =>
        ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{area.Id}/veredicto", Delegado(e, persona), new VeredictoDto("Aprobado"), 200);

    /// <summary>Vínculo con el expediente aprobado por las dos áreas (Laura y Carlos): queda Activo y con su certificado.</summary>
    private async Task<(VinculoDto Vinculo, ExpedienteDto Expediente)> AprobadoEnDosEtapasAsync(Escenario e)
    {
        await CrearFlujoEnDosEtapasAsync(e, "CLIENTE");
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await AbrirManualAsync(e, vinculo);
        await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura);
        var aprobado = await AprobarAreaAsync(e, expediente.Id, e.Cumplimiento, e.Carlos);
        aprobado.Estado.ShouldBe("Aprobado");
        return (vinculo, aprobado);
    }

    private Task<VinculoDto> VinculoAsync(Escenario e, Guid id) => ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{id}", Admin(e), null, 200);

    private Task<CertificadoDelVinculoDto> CertificadoAsync(Escenario e, Guid vinculoId) =>
        ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}/certificado", Admin(e), null, 200);

    // ---------- reabrir una etapa ----------

    [Fact]
    public async Task Reabrir_una_etapa_aprobada_la_devuelve_a_pendiente_y_las_posteriores_esperan()
    {
        var e = await PrepararAsync();
        await CrearFlujoEnDosEtapasAsync(e, "CLIENTE");
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await AbrirManualAsync(e, vinculo);
        (await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura)).EtapaActual.ShouldBe(2);

        // Solo un administrador, con justificación, y solo sobre una etapa ya aprobada.
        await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/1/reabrir", Delegado(e, e.Laura), new JustificacionDto("Revisar de nuevo."), 403);
        var sinTexto = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/1/reabrir", Admin(e), new JustificacionDto(" "), 422);
        (await CodigoAsync(sinTexto)).ShouldBe("justificacion-requerida");
        var activa = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/2/reabrir", Admin(e), new JustificacionDto("x"), 409);
        (await CodigoAsync(activa)).ShouldBe("etapa-no-aprobada");
        var inexistente = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/7/reabrir", Admin(e), new JustificacionDto("x"), 404);
        (await CodigoAsync(inexistente)).ShouldBe("etapa-no-encontrada");

        var reabierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/1/reabrir", Admin(e),
            new JustificacionDto("Laura aprobó sin ver el RUT."), 200);
        reabierto.Estado.ShouldBe("EnRevision");
        reabierto.EtapaActual.ShouldBe(1);
        reabierto.Etapas.Select(x => x.Estado).ShouldBe(["Activa", "Esperando"]);
        reabierto.Desaprobaciones.ShouldBe(1);
        var ultima = reabierto.UltimaDesaprobacion.ShouldNotBeNull();
        ultima.Alcance.ShouldBe("Etapa");
        ultima.Etapa.ShouldBe(1);
        ultima.Justificacion.ShouldBe("Laura aprobó sin ver el RUT.");
        ultima.Usuario.ShouldNotBeNullOrWhiteSpace();
        var area = reabierto.Etapas[0].Areas.ShouldHaveSingleItem();
        area.Estado.ShouldBe("Pendiente");
        var historico = area.Veredictos.ShouldHaveSingleItem();
        historico.Vigente.ShouldBeFalse();
        historico.Revertido.ShouldBeTrue();

        // La bandeja del aprobador vuelve a mostrarlo y la de Cumplimiento ya no.
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Laura), null, 200)).Datos
            .ShouldHaveSingleItem().EstadoArea.ShouldBe("Pendiente");
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Carlos), null, 200)).Datos.ShouldBeEmpty();

        // El flujo sigue desde ahí: Cumplimiento no puede decidir hasta que el aprobador vuelva a aprobar.
        var antes = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
            new VeredictoDto("Aprobado"), 409);
        (await CodigoAsync(antes)).ShouldBe("area-no-activa");
        await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura);
        (await AprobarAreaAsync(e, expediente.Id, e.Cumplimiento, e.Carlos)).Estado.ShouldBe("Aprobado");

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expediente.Id}/historial", Admin(e), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("etapa_reabierta");
        var evento = historial.Single(h => h.Tipo == "etapa_reabierta");
        evento.ActorTipo.ShouldNotBeNull();
        evento.Datos.GetProperty("justificacion").GetString().ShouldBe("Laura aprobó sin ver el RUT.");
    }

    [Fact]
    public async Task Reabrir_una_etapa_con_el_expediente_devuelto_al_tercero_reanuda_la_revision()
    {
        var e = await PrepararAsync();
        await CrearFlujoEnDosEtapasAsync(e, "CLIENTE");
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await AbrirManualAsync(e, vinculo);
        await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura);
        var observado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Observado", ["incompleto"], "Falta el RUT."), 200);
        observado.Estado.ShouldBe("Observado");

        var reabierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/etapas/1/reabrir", Admin(e),
            new JustificacionDto("La primera etapa no revisó el RUT."), 200);

        reabierto.Estado.ShouldBe("EnRevision");
        reabierto.EtapaActual.ShouldBe(1);
        reabierto.Etapas[1].Areas.ShouldHaveSingleItem().Veredictos.ShouldAllBe(v => !v.Vigente);
    }

    // ---------- desaprobar un expediente aprobado ----------

    [Fact]
    public async Task Desaprobar_un_expediente_aprobado_lo_reabre_el_vinculo_vuelve_a_validacion_y_el_certificado_se_revoca()
    {
        var e = await PrepararAsync();
        var (vinculo, aprobado) = await AprobadoEnDosEtapasAsync(e);
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("Activo");
        var certificado = (await CertificadoAsync(e, vinculo.Id)).Actual.ShouldNotBeNull();
        certificado.Estado.ShouldBe("Vigente");
        certificado.Emision.ShouldBe(1);

        // Solo un administrador, con justificación, y solo un expediente aprobado.
        await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Delegado(e, e.Laura), new DesaprobarExpedienteDto("Apareció una inhabilidad."), 403);
        var sinTexto = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto(""), 422);
        (await CodigoAsync(sinTexto)).ShouldBe("justificacion-requerida");
        var inexistente = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("x", 9), 404);
        (await CodigoAsync(inexistente)).ShouldBe("etapa-no-encontrada");
        (await ApiAsync<PaginaDto<ExpedienteDto>>("GET", "/api/v1/expedientes?desaprobados=true", Admin(e), null, 200)).Datos.ShouldBeEmpty();

        var desaprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e),
            new DesaprobarExpedienteDto("Apareció una inhabilidad."), 200);

        // El mismo expediente (mismo folio) vuelve a revisión desde la última etapa; la primera sigue aprobada.
        desaprobado.Id.ShouldBe(aprobado.Id);
        desaprobado.Folio.ShouldBe(aprobado.Folio);
        desaprobado.Estado.ShouldBe("EnRevision");
        desaprobado.CerradoEn.ShouldBeNull();
        desaprobado.EtapaActual.ShouldBe(2);
        desaprobado.Etapas.Select(x => x.Estado).ShouldBe(["Aprobada", "Activa"]);
        desaprobado.Desaprobaciones.ShouldBe(1);
        desaprobado.UltimaDesaprobacion!.Alcance.ShouldBe("ExpedienteAprobado");

        // El vínculo vuelve a validación con ese expediente abierto y el certificado queda revocado, en la misma transacción.
        var enValidacion = await VinculoAsync(e, vinculo.Id);
        enValidacion.Estado.ShouldBe("EnValidacion");
        enValidacion.ExpedienteAbiertoId.ShouldBe(aprobado.Id);
        var revocado = (await CertificadoAsync(e, vinculo.Id)).Actual.ShouldNotBeNull();
        revocado.Estado.ShouldBe("Revocado");
        revocado.Bloqueante.ShouldBeTrue();
        revocado.Revocacion!.Motivo.ShouldContain("desaprobado");

        // Aparece en la lista de desaprobados y en la bandeja de Cumplimiento.
        (await ApiAsync<PaginaDto<ExpedienteDto>>("GET", "/api/v1/expedientes?desaprobados=true", Admin(e), null, 200)).Datos
            .ShouldHaveSingleItem().Id.ShouldBe(aprobado.Id);
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Carlos), null, 200)).Datos
            .ShouldHaveSingleItem().EstadoExpediente.ShouldBe("EnRevision");

        // Quien ya no tenga el expediente aprobado no se puede desaprobar de nuevo.
        var otraVez = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("x"), 409);
        (await CodigoAsync(otraVez)).ShouldBe("estado-invalido");

        // Se aprueba de nuevo: el vínculo vuelve a Activo y se emite un certificado nuevo (emisión 2); la anterior sigue revocada.
        (await AprobarAreaAsync(e, aprobado.Id, e.Cumplimiento, e.Carlos)).Estado.ShouldBe("Aprobado");
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("Activo");
        var despues = await CertificadoAsync(e, vinculo.Id);
        despues.Actual!.Emision.ShouldBe(2);
        despues.Actual.Estado.ShouldBe("Vigente");
        despues.Emisiones.Select(x => (x.Emision, x.Estado)).ShouldBe([(2, "Vigente"), (1, "Revocado")]); // de la más reciente a la primera

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{aprobado.Id}/historial", Admin(e), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("expediente_desaprobado");
        (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", Admin(e), null, 200))
            .Select(h => h.Tipo).ShouldContain("vinculo_desaprobado");
    }

    [Fact]
    public async Task Desaprobar_no_se_puede_con_el_vinculo_bloqueado_ni_dos_veces_seguidas()
    {
        var e = await PrepararAsync();
        var (vinculo, aprobado) = await AprobadoEnDosEtapasAsync(e);

        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e), new BloquearVinculoDto("Sanción", null), 200);
        var bloqueado = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("x"), 409);
        (await CodigoAsync(bloqueado)).ShouldBe("vinculo-bloqueado");
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desbloquear", Admin(e), new JustificacionDto("Levantada"), 200);

        // Con una desaprobación hecha, el expediente vuelve a revisión: ya no está aprobado para desaprobarlo otra vez.
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("Primera."), 200);
        var segunda = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("Segunda."), 409);
        (await CodigoAsync(segunda)).ShouldBe("estado-invalido");
    }

    [Fact]
    public async Task Desaprobar_con_el_vinculo_desvinculado_responde_409()
    {
        var e = await PrepararAsync();
        var (vinculo, aprobado) = await AprobadoEnDosEtapasAsync(e);

        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e), new MotivoDto("Fin"), 200);
        var terminado = await ApiAsync("POST", $"/api/v1/expedientes/{aprobado.Id}/desaprobar", Admin(e), new DesaprobarExpedienteDto("x"), 409);
        (await CodigoAsync(terminado)).ShouldBe("estado-terminal");
    }

    // ---------- volver a la fase inicial ----------

    [Fact]
    public async Task Volver_a_la_fase_inicial_repite_el_flujo_desde_la_primera_etapa_y_no_aplica_si_ya_esta_ahi()
    {
        var e = await PrepararAsync();
        await CrearFlujoEnDosEtapasAsync(e, "CLIENTE");
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await AbrirManualAsync(e, vinculo);

        var yaInicial = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/fase-inicial", Admin(e), new JustificacionDto("x"), 409);
        (await CodigoAsync(yaInicial)).ShouldBe("ya-esta-en-fase-inicial");

        await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura);
        await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/fase-inicial", Delegado(e, e.Laura), new JustificacionDto("x"), 403);
        var sinTexto = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/fase-inicial", Admin(e), new JustificacionDto(""), 422);
        (await CodigoAsync(sinTexto)).ShouldBe("justificacion-requerida");

        var reiniciado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/fase-inicial", Admin(e),
            new JustificacionDto("Cambió la política de contratación."), 200);

        reiniciado.EtapaActual.ShouldBe(1);
        reiniciado.Etapas.Select(x => x.Estado).ShouldBe(["Activa", "Esperando"]);
        reiniciado.UltimaDesaprobacion!.Alcance.ShouldBe("FaseInicial");
        reiniciado.Etapas.SelectMany(x => x.Areas).SelectMany(a => a.Veredictos).ShouldAllBe(v => !v.Vigente && v.Revertido);

        // Aprobado de punta a punta, también se puede volver a la fase inicial: es una desaprobación.
        await AprobarAreaAsync(e, expediente.Id, e.Aprobador, e.Laura);
        var aprobado = await AprobarAreaAsync(e, expediente.Id, e.Cumplimiento, e.Carlos);
        aprobado.Estado.ShouldBe("Aprobado");
        var desdeCero = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/fase-inicial", Admin(e),
            new JustificacionDto("Empezar de cero."), 200);
        desdeCero.Estado.ShouldBe("EnRevision");
        desdeCero.EtapaActual.ShouldBe(1);
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("EnValidacion");
        (await CertificadoAsync(e, vinculo.Id)).Actual!.Estado.ShouldBe("Revocado");
    }

    // ---------- reintegrar ----------

    [Fact]
    public async Task Reintegrar_sin_revalidar_restituye_el_vinculo_y_emite_el_certificado_de_nuevo_sin_abrir_expediente()
    {
        var e = await PrepararAsync();
        var (vinculo, aprobado) = await AprobadoEnDosEtapasAsync(e);

        // Aún activo: no hay nada que reintegrar.
        await ApiAsync("GET", $"/api/v1/vinculos/{vinculo.Id}/reintegro", Admin(e), null, 200);
        var noDesvinculado = await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e), new ReintegrarVinculoDto("x", false), 409);
        (await CodigoAsync(noDesvinculado)).ShouldBe("estado-invalido");

        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e), new MotivoDto("Fin del contrato"), 200);
        (await CertificadoAsync(e, vinculo.Id)).Actual!.Estado.ShouldBe("Revocado");

        var viabilidad = await ApiAsync<ViabilidadDeReintegroDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/reintegro", Admin(e), null, 200);
        viabilidad.PuedeSinRevalidar.ShouldBeTrue();
        viabilidad.Motivos.ShouldBeEmpty();

        var sinTexto = await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e), new ReintegrarVinculoDto(" ", false), 422);
        (await CodigoAsync(sinTexto)).ShouldBe("justificacion-requerida");
        var reintegrado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e),
            new ReintegrarVinculoDto("Se renovó el contrato.", false), 200);

        reintegrado.Estado.ShouldBe("Activo");
        reintegrado.ExpedienteAbiertoId.ShouldBeNull();
        // Sin revalidar no se abre ningún expediente: el certificado se emite de nuevo con lo que ya se había aprobado.
        (await ApiAsync<PaginaDto<ExpedienteDto>>("GET", $"/api/v1/expedientes?vinculoId={vinculo.Id}", Admin(e), null, 200)).Datos
            .ShouldHaveSingleItem().Id.ShouldBe(aprobado.Id);
        var certificados = await CertificadoAsync(e, vinculo.Id);
        certificados.Actual!.Emision.ShouldBe(2);
        certificados.Actual.Estado.ShouldBe("Vigente");
        certificados.Actual.ExpedienteId.ShouldBe(aprobado.Id);
        certificados.Emisiones.Select(x => x.Estado).ShouldBe(["Vigente", "Revocado"]);
        (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", Admin(e), null, 200))
            .Single(h => h.Tipo == "vinculo_reintegrado").Datos.GetProperty("sinRevalidar").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Reintegrar_revalidando_abre_una_renovacion_con_todo_el_flujo_y_al_aprobarse_emite_otro_certificado()
    {
        var e = await PrepararAsync();
        var (vinculo, aprobado) = await AprobadoEnDosEtapasAsync(e);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e), new MotivoDto("Fin del contrato"), 200);

        // Por omisión revalida (documental = 1 del SRM): vuelve a validación con una renovación abierta.
        var reintegrado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e),
            new ReintegrarVinculoDto("Retoma la relación."), 200);

        reintegrado.Estado.ShouldBe("EnValidacion");
        var renovacionId = reintegrado.ExpedienteAbiertoId.ShouldNotBeNull();
        renovacionId.ShouldNotBe(aprobado.Id);
        var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{renovacionId}", Admin(e), null, 200);
        renovacion.Tipo.ShouldBe("Renovacion");
        renovacion.Estado.ShouldBe("EnRevision");
        renovacion.EtapaActual.ShouldBe(1);

        await AprobarAreaAsync(e, renovacionId, e.Aprobador, e.Laura);
        (await AprobarAreaAsync(e, renovacionId, e.Cumplimiento, e.Carlos)).Estado.ShouldBe("Aprobado");
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("Activo");
        var certificados = await CertificadoAsync(e, vinculo.Id);
        certificados.Actual!.Emision.ShouldBe(2);
        certificados.Actual.ExpedienteId.ShouldBe(renovacionId);
    }

    [Fact]
    public async Task Reintegrar_sin_revalidar_un_vinculo_que_no_estaba_aprobado_explica_por_que_no_se_puede()
    {
        var e = await PrepararAsync();
        await CrearFlujoEnDosEtapasAsync(e, "CLIENTE");
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await AbrirManualAsync(e, vinculo);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e), new MotivoDto("Se canceló"), 200);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expediente.Id}", Admin(e), null, 200)).Estado.ShouldBe("Anulado");

        var viabilidad = await ApiAsync<ViabilidadDeReintegroDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/reintegro", Admin(e), null, 200);
        viabilidad.PuedeSinRevalidar.ShouldBeFalse();
        viabilidad.Motivos.ShouldHaveSingleItem().ShouldContain("nunca fue aprobado");

        var rechazado = await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e), new ReintegrarVinculoDto("x", false), 409);
        (await CodigoAsync(rechazado)).ShouldBe("reintegro-sin-aprobacion-vigente");

        // Sin aprobación previa, reintegrar (revalidando) lo devuelve a Invitado para empezar de nuevo.
        (await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/reintegrar", Admin(e), new ReintegrarVinculoDto("Nuevo contrato."), 200))
            .Estado.ShouldBe("Invitado");
    }

    // ---------- reinicio tras un rechazo ----------

    /// <summary>Un tercero que aceptó la invitación y cuyo expediente rechazó Cumplimiento; el flujo trae la política de reintento.</summary>
    private async Task<(Escenario Escenario, VinculoDto Vinculo, SesionTerceroDto Sesion, ExpedienteDto Rechazado)> RechazadoAsync(ReintentoDto? reintento)
    {
        var e = await PrepararAsync();
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e),
            new CrearFlujoDto("HAB_RECHAZO_" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Habilitación", "CLIENTE",
                [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id), new AreaDeEtapaDto(e.Cumplimiento.Id, PuedeRechazarExpediente: true)])],
                Reintento: reintento), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200);
        await PublicarTextosAsync(e);

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", correo);
        var sesion = await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await VinculoAsync(e, vinculo.Id)).ExpedienteAbiertoId.ShouldNotBeNull();
        var rechazado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/rechazar", Delegado(e, e.Carlos),
            new RechazarExpedienteDto(e.Cumplimiento.Id, ["no-corresponde-al-tercero"], "Aparece en listas restrictivas."), 200);
        rechazado.Estado.ShouldBe("Rechazado");
        return (e, vinculo, sesion, rechazado);
    }

    [Fact]
    public async Task Un_tercero_rechazado_ve_los_motivos_y_vuelve_a_postularse_si_el_flujo_lo_permite()
    {
        var (e, vinculo, sesion, rechazado) = await RechazadoAsync(new ReintentoDto(true, 0));
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("Invitado");

        // El portal ve el rechazo con los textos al tercero (nunca la justificación interna ni las áreas) y que puede reintentar.
        var cuerpo = await (await TerceroAsync("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).ReadAsTextAsync();
        cuerpo.ShouldNotContain("listas restrictivas");
        cuerpo.ShouldNotContain("OFICIAL_CUMPLIMIENTO");
        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200);
        var reintento = vinculos.ShouldHaveSingleItem().Reintento.ShouldNotBeNull();
        reintento.Permitido.ShouldBeTrue();
        reintento.Disponible.ShouldBeTrue();
        reintento.Desde.ShouldNotBeNull();
        reintento.Motivos.ShouldHaveSingleItem().Texto.ShouldBe("El documento no corresponde a la persona o empresa que se está validando.");

        var nuevo = await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/vinculos/{vinculo.Id}/reintentar", sesion.AccessToken, null, 200);
        nuevo.Id.ShouldNotBe(rechazado.Id);
        nuevo.Estado.ShouldBe("EnRevision");
        nuevo.Tipo.ShouldBe("Inicial");
        nuevo.Folio.ShouldNotBe(rechazado.Folio);

        // El vínculo vuelve a validación con el expediente nuevo y ya no hay rechazo que reintentar.
        var enValidacion = await VinculoAsync(e, vinculo.Id);
        enValidacion.Estado.ShouldBe("EnValidacion");
        enValidacion.ExpedienteAbiertoId.ShouldBe(nuevo.Id);
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).ShouldHaveSingleItem().Reintento.ShouldBeNull();
        var otraVez = await TerceroAsync("POST", $"/public/v1/vinculos/{vinculo.Id}/reintentar", sesion.AccessToken, null, 409);
        (await CodigoAsync(otraVez)).ShouldBe("reintento-no-disponible");

        // El expediente rechazado queda en el historial de la empresa.
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{rechazado.Id}", Admin(e), null, 200)).Estado.ShouldBe("Rechazado");
    }

    [Fact]
    public async Task Sin_permiso_del_flujo_el_tercero_rechazado_no_puede_volver_a_postularse_pero_la_empresa_si_puede_reabrir()
    {
        var (e, vinculo, sesion, _) = await RechazadoAsync(null);

        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200);
        var reintento = vinculos.ShouldHaveSingleItem().Reintento.ShouldNotBeNull();
        reintento.Permitido.ShouldBeFalse();
        reintento.Disponible.ShouldBeFalse();
        reintento.Motivos.ShouldNotBeEmpty();
        var prohibido = await TerceroAsync("POST", $"/public/v1/vinculos/{vinculo.Id}/reintentar", sesion.AccessToken, null, 403);
        (await CodigoAsync(prohibido)).ShouldBe("reintento-no-permitido");

        // La empresa abre un expediente nuevo cuando quiera, sin depender del flujo.
        var nuevo = await AbrirManualAsync(e, vinculo);
        nuevo.Estado.ShouldBe("EnRevision");
        (await VinculoAsync(e, vinculo.Id)).Estado.ShouldBe("EnValidacion");
    }

    [Fact]
    public async Task El_reintento_espera_los_dias_del_flujo_y_dice_desde_cuando()
    {
        var (_, vinculo, sesion, rechazado) = await RechazadoAsync(new ReintentoDto(true, 30));

        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200);
        var reintento = vinculos.ShouldHaveSingleItem().Reintento.ShouldNotBeNull();
        reintento.Permitido.ShouldBeTrue();
        reintento.Disponible.ShouldBeFalse();
        reintento.RechazadoEn.ShouldBe(rechazado.CerradoEn!.Value, TimeSpan.FromSeconds(1));
        reintento.Desde!.Value.ShouldBe(reintento.RechazadoEn.AddDays(30), TimeSpan.FromSeconds(1));

        var enEspera = await TerceroAsync("POST", $"/public/v1/vinculos/{vinculo.Id}/reintentar", sesion.AccessToken, null, 409);
        (await CodigoAsync(enEspera)).ShouldBe("reintento-en-espera");
    }
}
