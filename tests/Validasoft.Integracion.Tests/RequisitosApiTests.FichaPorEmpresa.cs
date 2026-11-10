using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- validación por empresa de la ficha maestra (F2: FM-25…) ----------

    private const string TributariaCompleta =
        """{ "responsabilidades": ["5", "7", "48", "52"], "responsable_ica": true, "ica": [ { "municipio": "11001", "ciiu": "4663" } ] }""";

    private static AceptacionDeSeccionDto Aceptacion(FichaTerceroDto ficha, string seccion) =>
        ficha.Secciones.ShouldNotBeNull().Single(s => s.Seccion == seccion).Aceptaciones.ShouldNotBeNull().ShouldHaveSingleItem();

    /// <summary>Expediente abierto del vínculo en la empresa (la apertura de una actualización llega por el outbox).</summary>
    private async Task<Guid> AbiertoEnAsync(Empresa empresa, Guid vinculoId, Guid? distintoDe = null)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            if ((await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}", Admin(empresa), null, 200)).ExpedienteAbiertoId is { } id && id != distintoDe)
                return id;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("No se abrió el expediente.");
            await Task.Delay(200);
        }
    }

    [Fact]
    public async Task Secciones_como_requisitos_observacion_correccion_aprobacion_y_actualizacion_solo_de_la_seccion_que_cambio()
    {
        var e = await PrepararAsync();

        // El flujo valida las secciones al guardarse.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(e.Norte), new CrearFlujoDto("FICHA_MALA", "Ficha mala", "CLIENTE",
            Requisitos: [new RequisitoDelFlujoDto(Seccion: "FICHA_BANCARIA")]), 422))).ShouldBe("seccion-invalida");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(e.Norte), new CrearFlujoDto("FICHA_MALA", "Ficha mala", "CLIENTE",
            Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT", Seccion: "FICHA_GENERAL")]), 422))).ShouldBe("requisito-invalido");

        // Compras revisa la general y Cumplimiento la tributaria, que exige soporte (el «responsable por sector» del SRM).
        var flujo = await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(Seccion: "ficha_tributaria", AreasQueRevisan: [e.Cumplimiento.Id], ExigeSoporte: true)
        ]);
        flujo.Publicada!.Requisitos!.Select(r => (r.Seccion, r.TipoDocumento, r.ExigeSoporte)).ShouldBe(
            [("FICHA_GENERAL", (string?)null, false), ("FICHA_TRIBUTARIA", null, true)]);

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var url = $"/api/v1/expedientes/{expedienteId}";

        // Sin ficha diligenciada: ambas secciones pendientes con su motivo legible (guardado, no deducido).
        var abierto = await ApiAsync<ExpedienteDto>("GET", url, Admin(e.Norte), null, 200);
        abierto.Estado.ShouldBe("Abierto");
        var general = Requisito(abierto, "FICHA_GENERAL");
        (general.TipoRequisito, general.Estado, general.TipoDocumento.Nombre, general.TipoDocumento.Modo).ShouldBe(
            ("Seccion", "Pendiente", "Información general", "Formulario"));
        general.Seccion!.Completitud!.Faltantes.ShouldHaveSingleItem().Mensaje.ShouldBe("Falta diligenciar la información general.");
        general.Areas.ShouldHaveSingleItem().Codigo.ShouldBe("COMPRAS");
        var tributaria = Requisito(abierto, "FICHA_TRIBUTARIA");
        tributaria.Seccion!.ExigeSoporte.ShouldBeTrue();
        tributaria.Areas.ShouldHaveSingleItem().Codigo.ShouldBe("CUMPLIMIENTO");

        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        publico.RequisitosFaltantes.ShouldBe(2);
        publico.Requisitos!.Select(r => (r.TipoRequisito, r.Seccion!.Seccion)).ShouldBe([("Seccion", "FICHA_GENERAL"), ("Seccion", "FICHA_TRIBUTARIA")]);

        // Diligencia la general en el wizard ⇒ queda disponible para la empresa (outbox) y el requisito, cargado.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        var conGeneral = await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "FICHA_GENERAL").Estado == "Cargado");
        Requisito(conGeneral, "FICHA_GENERAL").VersionActual!.N.ShouldBe(1);
        Requisito(conGeneral, "FICHA_GENERAL").Seccion!.Datos!.Value.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");

        // La tributaria completa pero sin el soporte que exige la empresa ⇒ sigue pendiente con su motivo.
        (await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA", TributariaCompleta)).Completitud.Completa.ShouldBeTrue();
        var sinSoporte = await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "FICHA_TRIBUTARIA").Seccion!.VersionEvaluada == 1);
        sinSoporte.Estado.ShouldBe("Abierto");
        var pendiente = Requisito(sinSoporte, "FICHA_TRIBUTARIA");
        pendiente.Estado.ShouldBe("Pendiente");
        pendiente.VersionActual.ShouldBeNull();
        pendiente.Seccion!.Completitud!.Faltantes.ShouldHaveSingleItem()
            .ShouldBe(new FaltanteSeccionDto(null, "soporte-requerido", "Falta el soporte de la información tributaria."));

        // Con el soporte ⇒ completa para la empresa: el expediente pasa a revisión.
        var adhoc = (await SubirAsync(sesion, "SOPORTE_FICHA", Pdf("Soporte tributario"))).Version;
        var soporte = new SoporteSeccionDto(adhoc.DocumentoId, adhoc.N);
        await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA", TributariaCompleta, soporte);
        var enRevision = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        Requisito(enRevision, "FICHA_TRIBUTARIA").VersionActual!.N.ShouldBe(2);
        Requisito(enRevision, "FICHA_TRIBUTARIA").Seccion!.Soportes.ShouldHaveSingleItem().TipoDocumento.ShouldBe("SOPORTE_FICHA");

        // Compras aprueba la general («Aprobar área»); Cumplimiento observa la tributaria con su motivo y el campo a corregir.
        var deLaura = Area(await ApiAsync<ExpedienteDto>("GET", url, Delegado(e.Norte, e.Laura), null, 200), "COMPRAS").DocumentosPorDecidir!;
        deLaura.ShouldBe([new DocumentoPorDecidirDto(general.Id, 1, "Seccion", "FICHA_GENERAL")]);
        await AreaAsync<ExpedienteDto>(e, expedienteId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: deLaura.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)));
        var observado = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Cumplimiento, e.Carlos, new VeredictoDto("Observado", Requisitos:
            [new ObservacionDeRequisitoDto(tributaria.Id, 2, ["datos-no-coinciden"], "El CIIU de ICA no coincide con el RUT.", ["ica[0].ciiu"])]));
        observado.Estado.ShouldBe("Observado");
        Requisito(observado, "FICHA_TRIBUTARIA").Areas.Single().Veredictos.ShouldHaveSingleItem().Campos.ShouldBe(["ica[0].ciiu"]);
        Area(observado, "COMPRAS").Estado.ShouldBe("Aprobada");

        var vista = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        var observacion = vista.Requisitos!.Single(r => r.TipoDocumento.Codigo == "FICHA_TRIBUTARIA").Observaciones.ShouldHaveSingleItem();
        observacion.Motivos.ShouldHaveSingleItem().Codigo.ShouldBe("datos-no-coinciden");
        observacion.Campos.ShouldBe(["ica[0].ciiu"]);

        // El tercero corrige la sección en el wizard (con su soporte) ⇒ versión 3 ⇒ reenvía.
        await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA", TributariaCompleta.Replace("4663", "4290"), soporte);
        var corregido = await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "FICHA_TRIBUTARIA").VersionActual!.N == 3);
        Requisito(corregido, "FICHA_TRIBUTARIA").Estado.ShouldBe("Cargado");
        (await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken,
            new ReenviarExpedienteDto(), 200)).Estado.ShouldBe("EnRevision");

        // Cumplimiento aprueba la versión corregida ⇒ expediente aprobado, vínculo activo con certificado.
        var aprobado = await VeredictoAsync<ExpedienteDto>(e, expedienteId, tributaria.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", SobreVersion: 3));
        aprobado.Estado.ShouldBe("Aprobado");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Activo");
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual.ShouldNotBeNull();
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"{url}/historial", Admin(e.Norte), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("completitud_de_seccion_evaluada");
        historial.Count(h => h.Tipo == "seccion_aceptada_por_empresa").ShouldBe(2);

        // Tercero 360: lo aceptado por la empresa, sección por sección.
        var ficha = await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200);
        var generalAceptada = Aceptacion(ficha, "FICHA_GENERAL");
        (generalAceptada.VinculoId, generalAceptada.VersionAceptada, generalAceptada.ExpedienteId, generalAceptada.VersionNuevaPendiente)
            .ShouldBe((vinculo.Id, (int?)1, (Guid?)expedienteId, false));
        generalAceptada.Revalidacion.ShouldBeNull();
        Aceptacion(ficha, "FICHA_TRIBUTARIA").VersionAceptada.ShouldBe(3);

        // El tercero cambia la general (con el RUT de soporte) ⇒ la actualización se abre solo con la general.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"),
            new SoporteSeccionDto(rut.DocumentoId, rut.N));
        var actualizacionId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var actualizacion = await EsperarAsync(e.Norte, actualizacionId, x => x.Estado == "EnRevision");
        actualizacion.Tipo.ShouldBe("Actualizacion");
        var cambio = actualizacion.Requisitos!.ShouldHaveSingleItem();
        (cambio.TipoDocumento.Codigo, cambio.VersionActual!.N, cambio.Seccion!.VersionAceptada).ShouldBe(("FICHA_GENERAL", 2, (int?)1));
        cambio.Seccion.Cambios.ShouldBe([new CambioDeCampoDto("direccion", "Dirección", "Calle 45 # 12-08", "Carrera 7 # 80-12", "Modificado")]);
        cambio.Seccion.Soportes.ShouldHaveSingleItem().TipoDocumento.ShouldBe("RUT");

        var pendienteEn360 = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200),
            "FICHA_GENERAL");
        (pendienteEn360.VersionAceptada, pendienteEn360.VersionNuevaPendiente).ShouldBe(((int?)1, true));
        (pendienteEn360.Revalidacion!.ExpedienteId, pendienteEn360.Revalidacion.Estado, pendienteEn360.Revalidacion.Version).ShouldBe(
            (actualizacionId, "EnRevision", (int?)2));

        // Compras la aprueba ⇒ cambia la versión aceptada.
        var porDecidir = Area(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{actualizacionId}", Delegado(e.Norte, e.Laura), null, 200),
            "COMPRAS").DocumentosPorDecidir!;
        (await AreaAsync<ExpedienteDto>(e, actualizacionId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)))).Estado.ShouldBe("Aprobado");
        var actualizada = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200),
            "FICHA_GENERAL");
        (actualizada.VersionAceptada, actualizada.ExpedienteId, actualizada.VersionNuevaPendiente, actualizada.Revalidacion).ShouldBe(
            ((int?)2, (Guid?)actualizacionId, false, (RevalidacionDeSeccionDto?)null));
    }

    [Fact]
    public async Task Dos_empresas_aceptan_cada_una_su_vez_y_sin_el_soporte_exigido_el_requisito_sigue_pendiente()
    {
        var a = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var b = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        await FlujoConAsync(a, "CLIENTE", [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", ExigeSoporte: true)]);
        await FlujoConAsync(b, "CLIENTE", [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL")]);

        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", correo, nit);
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, a.Datos.Nombre);

        // Diligencia la general (sin soporte) antes de aceptar; al aceptar, cada empresa la evalúa con lo que exige su flujo.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculoA.Id);
        await AceptarAsync(sesion, vinculoB.Id);

        var enA = await AbiertoEnAsync(a, vinculoA.Id);
        var expedienteA = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{enA}", Admin(a), null, 200);
        expedienteA.Estado.ShouldBe("Abierto");
        var requisitoA = expedienteA.Requisitos!.ShouldHaveSingleItem();
        (requisitoA.Estado, requisitoA.VersionActual).ShouldBe(("Pendiente", (VersionRequisitoDto?)null));
        requisitoA.Seccion!.Completitud!.Faltantes.ShouldHaveSingleItem().Mensaje.ShouldBe("Falta el soporte de la información general.");

        var enB = await AbiertoEnAsync(b, vinculoB.Id);
        var expedienteB = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{enB}", Admin(b), null, 200);
        expedienteB.Estado.ShouldBe("EnRevision");
        expedienteB.Requisitos!.ShouldHaveSingleItem().VersionActual!.N.ShouldBe(1);

        // Sur acepta la versión 1; Norte no tiene nada aceptado.
        var areaB = expedienteB.Etapas.Single().Areas.Single().AreaId;
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{enB}/areas/{areaB}/aprobar-como-administrador", Admin(b),
            new JustificacionDto("Revisada con el área de compras."), 200)).Estado.ShouldBe("Aprobado");
        var deB = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculoA.TerceroId}/ficha", Admin(b), null, 200), "FICHA_GENERAL");
        (deB.VinculoId, deB.VersionAceptada).ShouldBe((vinculoB.Id, (int?)1));
        var deA = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculoA.TerceroId}/ficha", Admin(a), null, 200), "FICHA_GENERAL");
        (deA.VinculoId, deA.VersionAceptada, deA.VersionNuevaPendiente).ShouldBe((vinculoA.Id, (int?)null, false));

        // Con el RUT como soporte (mismos datos) ⇒ versión 2: completa para Norte, que la revisa; Sur la revalida aparte.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta, new SoporteSeccionDto(rut.DocumentoId, rut.N));
        var revisionA = await EsperarAsync(a, enA, x => x.Estado == "EnRevision");
        revisionA.Requisitos!.Single().VersionActual!.N.ShouldBe(2);
        var actualizacionB = await AbiertoEnAsync(b, vinculoB.Id);
        actualizacionB.ShouldNotBe(enB);
        var revalidacionB = await EsperarAsync(b, actualizacionB, x => x.Estado == "EnRevision");
        revalidacionB.Tipo.ShouldBe("Actualizacion");
        revalidacionB.Requisitos!.Single().Seccion!.Cambios.ShouldBeEmpty(); // solo cambió el soporte

        // Norte acepta la versión 2: lo de Sur no cambia (FM-3).
        var areaA = revisionA.Etapas.Single().Areas.Single().AreaId;
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{enA}/areas/{areaA}/aprobar-como-administrador", Admin(a),
            new JustificacionDto("Revisada con soporte."), 200)).Estado.ShouldBe("Aprobado");
        Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculoA.TerceroId}/ficha", Admin(a), null, 200), "FICHA_GENERAL")
            .VersionAceptada.ShouldBe(2);
        var surSigue = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculoA.TerceroId}/ficha", Admin(b), null, 200),
            "FICHA_GENERAL");
        (surSigue.VersionAceptada, surSigue.VersionNuevaPendiente, surSigue.Revalidacion!.ExpedienteId).ShouldBe(((int?)1, true, actualizacionB));
    }
}
