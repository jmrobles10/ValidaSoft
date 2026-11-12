using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- vínculo «En revisión» por una actualización (VR-1…VR-6) ----------

    private Task<VinculoDto> VinculoDeAsync(Empresa empresa, Guid vinculoId) =>
        ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}", Admin(empresa), null, 200);

    private async Task<List<Guid>> VinculosAsync(Empresa empresa, string consulta) =>
        [.. (await ApiAsync<PaginaDto<VinculoDto>>("GET", $"/api/v1/vinculos?{consulta}", Admin(empresa), null, 200)).Datos.Select(v => v.Id)];

    /// <summary>
    /// La actualización del vínculo en cualquier estado (con la revisión automática puede aprobarse al segundo de abrirse, antes de que
    /// una consulta del vínculo alcance a verla abierta).
    /// </summary>
    private async Task<Guid> ActualizacionDeAsync(Empresa empresa, Guid vinculoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var lista = await ApiAsync<PaginaDto<ExpedienteDto>>("GET", $"/api/v1/expedientes?vinculoId={vinculoId}", Admin(empresa), null, 200);
            if (lista.Datos.FirstOrDefault(x => x.Tipo == "Actualizacion") is { } actualizacion)
                return actualizacion.Id;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("No se abrió la actualización.");
            await Task.Delay(150);
        }
    }

    private async Task<VinculoPublicoDto> VinculoPublicoAsync(SesionTerceroDto sesion, Guid vinculoId) =>
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).Single(v => v.Id == vinculoId);

    [Fact]
    public async Task Un_tercero_aprobado_en_dos_empresas_sube_una_version_y_ambos_vinculos_quedan_en_revision_sin_cambiar_de_estado_hasta_que_cada_empresa_la_resuelve()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        await FlujoConAsync(sur, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var correo = CorreoNuevo();
        var nit = Nit();
        var enNorte = await CrearVinculoAsync(norte, "CLIENTE", correo, nit);
        var enSur = await CrearVinculoAsync(sur, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, norte.Datos.Nombre);

        // Con el RUT v1 en la bóveda, el tercero acepta a las dos y cada una aprueba lo suyo: dos vínculos Activos, ninguno en revisión.
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await AceptarAsync(sesion, enNorte.Id);
        await AceptarAsync(sesion, enSur.Id);
        var inicialNorte = await AbiertoEnAsync(norte, enNorte.Id);
        var inicialSur = await AbiertoEnAsync(sur, enSur.Id);
        foreach (var (empresa, id) in new[] { (norte, inicialNorte), (sur, inicialSur) })
        {
            var abierto = await EsperarAsync(empresa, id, x => x.Estado == "EnRevision");
            var area = abierto.Etapas.Single().Areas.Single().AreaId;
            (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{id}/areas/{area}/aprobar-como-administrador", Admin(empresa),
                new JustificacionDto("Revisado."), 200)).Estado.ShouldBe("Aprobado");
        }

        foreach (var (empresa, vinculo) in new[] { (norte, enNorte), (sur, enSur) })
        {
            var activo = await VinculoDeAsync(empresa, vinculo.Id);
            (activo.Estado, activo.EnRevision).ShouldBe(("Activo", false));
        }

        (await VinculoPublicoAsync(sesion, enNorte.Id)).EnRevision.ShouldBeFalse();

        // El RUT nuevo abre una actualización en cada empresa que lo exige.
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacionNorte = await AbiertoEnAsync(norte, enNorte.Id, inicialNorte);
        var actualizacionSur = await AbiertoEnAsync(sur, enSur.Id, inicialSur);
        var revisionNorte = await EsperarAsync(norte, actualizacionNorte, x => x.Estado == "EnRevision");
        var revisionSur = await EsperarAsync(sur, actualizacionSur, x => x.Estado == "EnRevision");

        // El documento actualizado está «En revisión» en cada empresa (nivel de revisión Manual: lo decide una persona).
        foreach (var revision in new[] { revisionNorte, revisionSur })
        {
            revision.Tipo.ShouldBe("Actualizacion");
            var rut = Requisito(revision, "RUT");
            (rut.Estado, rut.VersionActual!.N).ShouldBe(("EnRevision", 2));
            rut.Areas.SelectMany(a => a.Veredictos).ShouldBeEmpty();
        }

        // Cada vínculo está «En revisión» sin cambiar de estado: el estado, el certificado y la consulta de certificación siguen como siempre (A-2).
        foreach (var (empresa, vinculo, actualizacion) in new[] { (norte, enNorte, actualizacionNorte), (sur, enSur, actualizacionSur) })
        {
            var enRevision = await VinculoDeAsync(empresa, vinculo.Id);
            (enRevision.Estado, enRevision.EnRevision, enRevision.ExpedienteAbiertoId).ShouldBe(("Activo", true, (Guid?)actualizacion));

            // La lista y su filtro: «En revisión» solo lo que tiene la actualización abierta; «Activo» sin repetirlo en pantalla, y el estado de siempre sigue intacto.
            (await VinculosAsync(empresa, "enRevision=true")).ShouldBe([vinculo.Id]);
            (await VinculosAsync(empresa, "estado=Activo&enRevision=false")).ShouldBeEmpty();
            (await VinculosAsync(empresa, "estado=Activo")).ShouldBe([vinculo.Id]); // compatibilidad: el estado de dominio no cambió
            (await VinculosAsync(empresa, "estado=EnValidacion")).ShouldBeEmpty();

            // Las cifras: cada vínculo está en una sola (En revisión, no Activo).
            var cifras = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores", Admin(empresa), null, 200);
            (cifras.VinculosEnRevision, cifras.VinculosActivos, cifras.VinculosPorVencer, cifras.VinculosVencidos).ShouldBe((1, 0, 0, 0));

            // El certificado sigue siendo válido (Con pendientes) y la consulta lo dice: nada se bloquea ni se suspende por la revisión.
            var certificado = await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(empresa), null, 200);
            certificado.Actual!.Estado.ShouldBe("ConPendientes");
            var consulta = await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}", Admin(empresa), null, 200);
            (consulta.EstadoVinculo, consulta.Estado, consulta.Certificado, consulta.Bloqueado, consulta.EnRevision).ShouldBe(("Activo", "ConPendientes", true, false, true));
            consulta.RevalidacionPendiente.ShouldHaveSingleItem().Tipo.ShouldBe("RUT");

            // El informe de vínculos lo cuenta y lo filtra como «En revisión», con su estado de dominio intacto.
            var informe = await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos", Admin(empresa), null, 200);
            var fila = informe.Datos.ShouldHaveSingleItem();
            (fila.Estado, fila.EnRevision, fila.ExpedienteEnCurso!.Tipo).ShouldBe(("Activo", true, "Actualizacion"));
            informe.Resumen.PorEstado.ShouldBe([new ConteoDto("EnRevision", 1)]);
            (await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos?estado=EnRevision", Admin(empresa), null, 200)).Datos.ShouldHaveSingleItem();
            (await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos?estado=Activo", Admin(empresa), null, 200)).Datos.ShouldBeEmpty();
            (await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos?estado=Activo,EnRevision", Admin(empresa), null, 200)).Datos.ShouldHaveSingleItem();
            (await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos?estado=EnValidacion,EnRevision", Admin(empresa), null, 200)).Datos.ShouldHaveSingleItem();
        }

        // El tercero ve las dos empresas en revisión (sin «Aprobado»): el vínculo sigue Activo pero lo que le toca es lo del expediente.
        foreach (var (vinculo, actualizacion) in new[] { (enNorte, actualizacionNorte), (enSur, actualizacionSur) })
        {
            var publico = await VinculoPublicoAsync(sesion, vinculo.Id);
            (publico.Estado, publico.EnRevision, publico.ExpedienteId).ShouldBe(("Activo", true, (Guid?)actualizacion));
            var expediente = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{actualizacion}", sesion.AccessToken, null, 200);
            (expediente.Tipo, expediente.Estado).ShouldBe(("Actualizacion", "EnRevision"));
            expediente.Requisitos!.Single().Estado.ShouldBe("EnRevision");
        }

        // Norte aprueba su actualización: vuelve a Activo (ya no en revisión), el certificado se reemite y Sur sigue en revisión.
        var areaNorte = revisionNorte.Etapas.Single().Areas.Single().AreaId;
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{actualizacionNorte}/areas/{areaNorte}/aprobar-como-administrador", Admin(norte),
            new JustificacionDto("Revisada."), 200)).Estado.ShouldBe("Aprobado");
        var norteAprobado = await VinculoDeAsync(norte, enNorte.Id);
        (norteAprobado.Estado, norteAprobado.EnRevision, norteAprobado.ExpedienteAbiertoId, norteAprobado.UltimoResultado).ShouldBe(("Activo", false, (Guid?)null, "Aprobado"));
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{enNorte.Id}/certificado", Admin(norte), null, 200)).Actual!.Estado.ShouldBe("Vigente");
        (await VinculosAsync(norte, "enRevision=true")).ShouldBeEmpty();
        (await VinculosAsync(norte, "estado=Activo&enRevision=false")).ShouldBe([enNorte.Id]);
        var cifrasNorte = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores", Admin(norte), null, 200);
        (cifrasNorte.VinculosEnRevision, cifrasNorte.VinculosActivos).ShouldBe((0, 1));
        (await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={enNorte.Id}", Admin(norte), null, 200))
            .ShouldSatisfyAllConditions(c => c.EnRevision.ShouldBeFalse(), c => c.Estado.ShouldBe("Vigente"), c => c.RevalidacionPendiente.ShouldBeEmpty());

        var surSigue = await VinculoDeAsync(sur, enSur.Id);
        (surSigue.Estado, surSigue.EnRevision, surSigue.ExpedienteAbiertoId).ShouldBe(("Activo", true, (Guid?)actualizacionSur));
        var publicoNorte = await VinculoPublicoAsync(sesion, enNorte.Id);
        var publicoSur = await VinculoPublicoAsync(sesion, enSur.Id);
        (publicoNorte.EnRevision, publicoSur.EnRevision).ShouldBe((false, true));
        (await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{actualizacionNorte}", sesion.AccessToken, null, 200)).Estado.ShouldBe("Aprobado");

        // Sur la resuelve a su tiempo: también vuelve a Activo.
        var areaSur = revisionSur.Etapas.Single().Areas.Single().AreaId;
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{actualizacionSur}/areas/{areaSur}/aprobar-como-administrador", Admin(sur),
            new JustificacionDto("Revisada."), 200);
        (await VinculoDeAsync(sur, enSur.Id)).ShouldSatisfyAllConditions(v => v.EnRevision.ShouldBeFalse(), v => v.Estado.ShouldBe("Activo"));
        (await VinculoPublicoAsync(sesion, enSur.Id)).EnRevision.ShouldBeFalse();
    }

    [Fact]
    public async Task Una_empresa_que_no_exige_lo_actualizado_o_que_nunca_lo_aprobo_no_tiene_nada_en_revision_y_un_vinculo_bloqueado_se_ve_bloqueado()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var oriente = await EmpresaNuevaAsync("Oriente S.A.S.", "ORIENTE");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        await FlujoConAsync(sur, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO")]); // no exige el RUT
        await FlujoConAsync(oriente, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]); // lo exige, pero aún no lo aprobó
        var correo = CorreoNuevo();
        var nit = Nit();
        var enNorte = await CrearVinculoAsync(norte, "CLIENTE", correo, nit);
        var enSur = await CrearVinculoAsync(sur, "CLIENTE", correo, nit);
        var enOriente = await CrearVinculoAsync(oriente, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, norte.Datos.Nombre);

        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        await AceptarAsync(sesion, enNorte.Id);
        await AceptarAsync(sesion, enSur.Id);
        var inicialNorte = await AbiertoEnAsync(norte, enNorte.Id);
        var inicialSur = await AbiertoEnAsync(sur, enSur.Id);
        foreach (var (empresa, id) in new[] { (norte, inicialNorte), (sur, inicialSur) })
        {
            var abierto = await EsperarAsync(empresa, id, x => x.Estado == "EnRevision");
            var area = abierto.Etapas.Single().Areas.Single().AreaId;
            await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{id}/areas/{area}/aprobar-como-administrador", Admin(empresa), new JustificacionDto("Revisado."), 200);
        }

        // Oriente aún no se acepta (no hay expediente): nada que revisar. Con Sur, que no exige el RUT, tampoco.
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacion = await AbiertoEnAsync(norte, enNorte.Id, inicialNorte);
        await EsperarAsync(norte, actualizacion, x => x.Estado == "EnRevision");
        await Task.Delay(1500); // lo de Sur y Oriente llegaría por el mismo outbox: se le da tiempo y no ocurre
        (await VinculoDeAsync(sur, enSur.Id)).ShouldSatisfyAllConditions(v => v.EnRevision.ShouldBeFalse(), v => v.ExpedienteAbiertoId.ShouldBeNull());
        (await VinculoDeAsync(oriente, enOriente.Id)).ShouldSatisfyAllConditions(v => v.EnRevision.ShouldBeFalse(), v => v.Estado.ShouldBe("Invitado"));
        (await VinculosAsync(sur, "enRevision=true")).ShouldBeEmpty();
        (await VinculoPublicoAsync(sesion, enSur.Id)).EnRevision.ShouldBeFalse();
        (await VinculoPublicoAsync(sesion, enNorte.Id)).EnRevision.ShouldBeTrue();

        // Un vínculo bloqueado muestra Bloqueado (manda el bloqueo); al desbloquearlo, con la actualización aún abierta, vuelve a verse en revisión.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{enNorte.Id}/bloquear", Admin(norte), new BloquearVinculoDto("Sanción", null), 200);
        (await VinculoDeAsync(norte, enNorte.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Bloqueado"), v => v.EnRevision.ShouldBeFalse());
        (await VinculosAsync(norte, "enRevision=true")).ShouldBeEmpty();
        (await VinculosAsync(norte, "estado=Bloqueado&enRevision=false")).ShouldBe([enNorte.Id]);
        (await VinculoPublicoAsync(sesion, enNorte.Id)).EnRevision.ShouldBeFalse();
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{enNorte.Id}/desbloquear", Admin(norte), new JustificacionDto("Resuelto."), 200);
        (await VinculoDeAsync(norte, enNorte.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeTrue());
    }

    [Fact]
    public async Task Observada_la_actualizacion_el_vinculo_sigue_en_revision_y_el_tercero_ve_sus_observaciones_hasta_que_corrige_y_se_aprueba()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        await AceptarAsync(sesion, vinculo.Id);
        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var revisionInicial = await EsperarAsync(e.Norte, inicial, x => x.Estado == "EnRevision");
        await AreaAsync<ExpedienteDto>(e, inicial, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revisionInicial, "RUT").Id] = 1 }));
        (await AreaAsync<ExpedienteDto>(e, inicial, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int>
        {
            [Requisito(revisionInicial, "RUT").Id] = 1, [Requisito(revisionInicial, "CAMARA_COMERCIO").Id] = 1
        }))).Estado.ShouldBe("Aprobado");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).EnRevision.ShouldBeFalse();

        // RUT nuevo: en revisión. Compras lo observa ⇒ la actualización vuelve al tercero, y el vínculo sigue en revisión (la empresa espera su respuesta).
        await SubirAsync(sesion, "RUT", Pdf("RUT v2 borroso"));
        var actualizacion = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        var enRevision = await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        var rut = Requisito(enRevision, "RUT");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeTrue());
        await VeredictoAsync<ExpedienteDto>(e, actualizacion, rut.Id, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", SobreVersion: 2));
        await VeredictoAsync<ExpedienteDto>(e, actualizacion, rut.Id, e.Compras, e.Laura,
            new VeredictoDto("Observado", ["ilegible"], "El RUT está borroso.", SobreVersion: 2));
        var observada = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{actualizacion}", Admin(e.Norte), null, 200);
        observada.Estado.ShouldBe("Observado");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeTrue());
        (await VinculosAsync(e.Norte, "enRevision=true")).ShouldBe([vinculo.Id]);

        // El tercero ve «Tienes observaciones» (la actualización devuelta), no «Aprobado»: su vínculo no aparece como aprobado mientras tanto.
        var publico = await VinculoPublicoAsync(sesion, vinculo.Id);
        (publico.Estado, publico.EnRevision).ShouldBe(("Activo", true));
        var vista = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{actualizacion}", sesion.AccessToken, null, 200);
        (vista.Tipo, vista.Estado, vista.PuedeReenviar).ShouldBe(("Actualizacion", "Observado", true));
        vista.Requisitos!.Single().Observaciones.ShouldHaveSingleItem().Motivos.ShouldHaveSingleItem().Codigo.ShouldBe("ilegible");

        // Corrige con una versión nueva y reenvía ⇒ vuelve a revisión; aprobada, el vínculo vuelve a Activo sin revisión.
        await SubirAsync(sesion, "RUT", Pdf("RUT v3 legible"));
        await EsperarAsync(e.Norte, actualizacion, x => Requisito(x, "RUT").VersionActual!.N == 3);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{actualizacion}/reenviar", sesion.AccessToken, new ReenviarExpedienteDto(), 200);
        var reenviada = await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).EnRevision.ShouldBeTrue();
        await AreaAsync<ExpedienteDto>(e, actualizacion, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(reenviada, "RUT").Id] = 3 }));
        (await AreaAsync<ExpedienteDto>(e, actualizacion, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(reenviada, "RUT").Id] = 3 }))).Estado.ShouldBe("Aprobado");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeFalse());
        (await VinculoPublicoAsync(sesion, vinculo.Id)).EnRevision.ShouldBeFalse();
    }

    [Fact]
    public async Task El_documento_actualizado_queda_en_revision_en_Asistido_y_en_Automatico_sale_de_revision_por_el_veredicto_del_sistema_con_su_evidencia()
    {
        // Asistido: el sistema solo sugiere, nunca emite veredictos: el RUT nuevo sigue en revisión y el vínculo también.
        var asistido = await PrepararAsync();
        await PlanParaRevisionAsync(asistido.Norte);
        await ConfigRevisionAsync(asistido.Norte, new ActualizarRevisionAutomaticaDto("Asistido"));
        var (vinculoA, sesionA, inicialA) = await TerceroAceptaAsync(asistido.Norte, "PROVEEDOR");
        await SubirAsync(sesionA, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesionA, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        var revisionA = await EsperarAsync(asistido.Norte, inicialA, x => x.Estado == "EnRevision" && x.Requisitos!.All(r => r.RevisionesAutomaticas is { Count: > 0 }));
        await AreaAsync<ExpedienteDto>(asistido, inicialA, asistido.Compras, asistido.Laura,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revisionA, "RUT").Id] = 1 }));
        await AreaAsync<ExpedienteDto>(asistido, inicialA, asistido.Cumplimiento, asistido.Carlos, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int>
        {
            [Requisito(revisionA, "RUT").Id] = 1, [Requisito(revisionA, "CAMARA_COMERCIO").Id] = 1
        }));
        (await VinculoDeAsync(asistido.Norte, vinculoA.Id)).EnRevision.ShouldBeFalse();

        await SubirAsync(sesionA, "RUT", Pdf("RUT v2"));
        var actualizacionA = await AbiertoEnAsync(asistido.Norte, vinculoA.Id, inicialA);
        var sugerida = await EsperarAsync(asistido.Norte, actualizacionA, x => Requisito(x, "RUT").RevisionesAutomaticas is { Count: > 0 });
        Requisito(sugerida, "RUT").ShouldSatisfyAllConditions(r => r.Estado.ShouldBe("EnRevision"), r => r.Areas.SelectMany(a => a.Veredictos).ShouldBeEmpty());
        (await VinculoDeAsync(asistido.Norte, vinculoA.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeTrue());

        // Automático: el RUT nuevo pasa por revisión y sale de ella por el veredicto del sistema (con la confianza y su justificación); el vínculo vuelve a Activo.
        var automatico = await PrepararAsync();
        await PlanParaRevisionAsync(automatico.Norte);
        await ConfigRevisionAsync(automatico.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 0, TiposAutorizados: ["RUT", "CAMARA_COMERCIO"],
            AreasAutorizadas: [automatico.Compras.Id, automatico.Cumplimiento.Id]));
        var (vinculoB, sesionB, inicialB) = await TerceroAceptaAsync(automatico.Norte, "PROVEEDOR");
        await SubirAsync(sesionB, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesionB, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        await EsperarAsync(automatico.Norte, inicialB, x => x.Estado == "Aprobado");

        await SubirAsync(sesionB, "RUT", Pdf("RUT v2"));
        var actualizacionB = await ActualizacionDeAsync(automatico.Norte, vinculoB.Id);
        var aprobada = await EsperarAsync(automatico.Norte, actualizacionB, x => x.Estado == "Aprobado");
        var veredicto = Requisito(aprobada, "RUT").Areas.Single(a => a.Codigo == "COMPRAS").Veredictos.Single(v => !v.Derivado);
        (veredicto.Origen, veredicto.Usuario, veredicto.Vigente).ShouldBe(("Sistema", "revision-automatica", true));
        veredicto.Justificacion.ShouldBe("Aprobado por la revisión automática (confianza 97 %), sin excepciones.");
        RevisionDe(aprobada, "RUT", "COMPRAS")!.ShouldSatisfyAllConditions(r => r.Resultado.ShouldBe("AprobadaAutomaticamente"), r => r.SobreVersion.ShouldBe(2));
        // Antes de ese veredicto el documento pasó por «En revisión» (el expediente se envió a revisión y solo después el sistema lo aprobó).
        var eventos = await EventosAsync(automatico.Norte.Datos.TenantId, actualizacionB);
        var enviado = eventos.ToList().FindIndex(x => x is Validasoft.Dominio.Expedientes.ExpedienteEnviadoARevision);
        var aprobadoPorElSistema = eventos.ToList().FindIndex(x => x is Validasoft.Dominio.Expedientes.VeredictoDeRequisitoEmitido { Origen: Validasoft.Dominio.Expedientes.OrigenVeredicto.Sistema });
        enviado.ShouldBeGreaterThanOrEqualTo(0);
        aprobadoPorElSistema.ShouldBeGreaterThan(enviado);
        (await VinculoDeAsync(automatico.Norte, vinculoB.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.EnRevision.ShouldBeFalse());
    }
}
