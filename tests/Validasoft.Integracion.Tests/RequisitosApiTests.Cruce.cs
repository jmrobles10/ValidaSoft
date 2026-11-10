using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- F5: cruce sección ↔ documento, «usar como soporte» y actualización desde el documento ----------

    /// <summary>RUT con marcas que lee el analizador falso (como el simulado de la API): NIT del tercero y la razón social de <see cref="GeneralCompleta"/>.</summary>
    private static byte[] RutCon(string nit, string direccion, string razonSocial = "ACEROS DEL NORTE SAS") =>
        Pdf($"RUT [[nit={nit}]][[razonSocial={razonSocial}]][[direccion={direccion}]][[codigoMunicipio=11001]][[actividadEconomicaPrincipal=4663]]");

    /// <summary>Flujo CLIENTE de Norte: el RUT lo revisa Compras y la general, Cumplimiento.</summary>
    private Task<FlujoDto> FlujoRutYGeneralAsync(Escenario e) =>
        FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Cumplimiento.Id])
        ]);

    private static CampoCruzadoDto CampoDe(CruceDeSeccionDto cruce, string campo) => cruce.Campos.Single(c => c.Campo == campo);

    [Fact]
    public async Task El_RUT_requerido_se_cruza_con_la_general_y_cada_empresa_lo_ve_solo_si_recibio_el_analisis()
    {
        var e = await PrepararAsync();
        var sur = await OtraEmpresaDelTenantAsync(e.Norte, "Sur S.A.S.", "SUR", "899999068"); // sin plan de IA
        await PlanIaAsync(e.Norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        var flujo = await FlujoRutYGeneralAsync(e);
        flujo.Publicada!.Requisitos!.First().UsarComoSoporte.ShouldBeNull(); // activo por omisión
        await FlujoConAsync(sur, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT"), new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL")]);

        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculoNorte = await CrearVinculoAsync(e.Norte, "CLIENTE", correo, nit);
        var vinculoSur = await CrearVinculoAsync(sur, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, e.Norte.Datos.Nombre);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculoNorte.Id);
        await AceptarAsync(sesion, vinculoSur.Id);
        await SubirAsync(sesion, "RUT", RutCon(nit, "Cra 10 # 20-30"));

        // Norte pagó el análisis: ve el cruce campo por campo, como hecho de la versión de la sección.
        var enNorte = await AbiertoEnAsync(e.Norte, vinculoNorte.Id);
        var expediente = await EsperarAsync(e.Norte, enNorte, x => Requisito(x, "FICHA_GENERAL").Seccion?.Cruce?.Estado == "Cruzado");
        var cruce = Requisito(expediente, "FICHA_GENERAL").Seccion!.Cruce!;
        cruce.DiscrepanciaCritica.ShouldBeFalse();
        var delRut = cruce.Cruces.ShouldHaveSingleItem();
        (delRut.TipoDocumento, delRut.VersionDocumento, delRut.Confianza).ShouldBe(("RUT", 1, 0.97m));
        CampoDe(delRut, "numero").ShouldBe(new CampoCruzadoDto("numero", "Número de identificación", nit, nit, "Coincide", true, null));
        CampoDe(delRut, "razon_social").Resultado.ShouldBe("Coincide");
        CampoDe(delRut, "ciudad").Resultado.ShouldBe("Coincide");
        var direccion = CampoDe(delRut, "direccion");
        (direccion.Resultado, direccion.Declarado, direccion.Extraido, direccion.Critico).ShouldBe(("Discrepa", "Calle 45 # 12-08", "Cra 10 # 20-30", false));

        // En el requisito del RUT («usar como soporte»): las secciones que respalda, con su cruce.
        var soporte = Requisito(expediente, "RUT").SoporteDeSecciones.ShouldNotBeNull();
        soporte.SinAnalisis.ShouldBeFalse();
        soporte.Secciones.Select(s => (s.Seccion, s.Version, s.Cruce is not null)).ShouldBe(
            [("FICHA_GENERAL", (int?)1, true), ("FICHA_TRIBUTARIA", null, false), ("FICHA_LEGAL", null, false)]);

        // Sur no tiene plan: el mismo RUT y la misma sección, pero revisión manual (fail-closed).
        var enSur = await AbiertoEnAsync(sur, vinculoSur.Id);
        var deSur = await EsperarAsync(sur, enSur, x => Requisito(x, "RUT").VersionActual is not null && Requisito(x, "FICHA_GENERAL").Seccion?.Cruce is not null);
        var manual = Requisito(deSur, "FICHA_GENERAL").Seccion!.Cruce!;
        (manual.Estado, manual.Cruces.Count, manual.DiscrepanciaCritica).ShouldBe(("Manual", 0, false));
        manual.SinCruce.ShouldHaveSingleItem().ShouldSatisfyAllConditions(s => s.TipoDocumento.ShouldBe("RUT"), s => s.Motivo.ShouldBe("sin-analisis"));
        Requisito(deSur, "RUT").SoporteDeSecciones!.SinAnalisis.ShouldBeTrue();
        (await UsoIaAsync(sur)).IncluidasConsumidas.ShouldBe(0);

        // El tercero nunca ve el cruce.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{enNorte}", sesion.AccessToken, null, 200);
        publico.Requisitos!.Single(r => r.TipoDocumento.Codigo == "FICHA_GENERAL").Seccion!.Cruce.ShouldBeNull();

        // Sin discrepancias críticas se aprueba como siempre.
        var aprobado = await EsperarAsync(e.Norte, enNorte, x => x.Estado == "EnRevision");
        await AreaAsync<ExpedienteDto>(e, enNorte, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(aprobado, "RUT").Id] = 1 }));
        (await AreaAsync<ExpedienteDto>(e, enNorte, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(aprobado, "FICHA_GENERAL").Id] = 1 }))).Estado.ShouldBe("Aprobado");
    }

    [Fact]
    public async Task Con_otra_razon_social_en_el_RUT_no_se_aprueba_sin_justificacion_y_apagar_usar_como_soporte_quita_el_cruce()
    {
        var e = await PrepararAsync();
        await PlanIaAsync(e.Norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        await FlujoRutYGeneralAsync(e);
        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "RUT", RutCon(nit, "Calle 45 # 12-08", "FERRETERIA OTRA LTDA"));

        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var expediente = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision" && Requisito(x, "FICHA_GENERAL").Seccion?.Cruce?.Estado == "Cruzado");
        var cruce = Requisito(expediente, "FICHA_GENERAL").Seccion!.Cruce!;
        cruce.DiscrepanciaCritica.ShouldBeTrue();
        CampoDe(cruce.Cruces.Single(), "razon_social").ShouldSatisfyAllConditions(c => c.Resultado.ShouldBe("Discrepa"), c => c.Critico.ShouldBeTrue());
        CampoDe(cruce.Cruces.Single(), "direccion").Resultado.ShouldBe("Coincide");
        Requisito(expediente, "RUT").SoporteDeSecciones!.DiscrepanciaCritica.ShouldBeTrue();

        // Aprobar sin justificación: 422; con ella, queda escrita en el veredicto.
        var general = Requisito(expediente, "FICHA_GENERAL");
        var versiones = new Dictionary<Guid, int> { [general.Id] = 1 };
        (await CodigoAsync(await AreaAsync(e, expedienteId, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", Versiones: versiones), 422)))
            .ShouldBe("justificacion-por-discrepancias");
        var conJustificacion = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: versiones, Justificacion: "La razón social cambió por fusión; verificado con la Cámara."));
        Requisito(conJustificacion, "FICHA_GENERAL").Areas.Single().Veredictos.ShouldHaveSingleItem().Justificacion
            .ShouldBe("La razón social cambió por fusión; verificado con la Cámara.");
        var rut = Requisito(conJustificacion, "RUT");
        (await CodigoAsync(await VeredictoAsync(e, expedienteId, rut.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1), 422)))
            .ShouldBe("justificacion-por-discrepancias");

        // La empresa apaga «usar como soporte» en el RUT: en un expediente nuevo el RUT ya no se cruza ni pide justificación.
        var otro = await EmpresaNuevaAsync("Oriente S.A.S.", "ORIENTE");
        await PlanIaAsync(otro, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        var sinCruce = await FlujoConAsync(otro, "CLIENTE",
            [new RequisitoDelFlujoDto(TipoDocumento: "RUT", UsarComoSoporte: false), new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL")]);
        sinCruce.Publicada!.Requisitos!.First().UsarComoSoporte.ShouldBe(false);
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(otro), new CrearFlujoDto("MAL", "Mal", "CLIENTE",
            Requisitos: [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", UsarComoSoporte: true)]), 422))).ShouldBe("soporte-no-aplica");
        var vinculoOtro = await CrearVinculoAsync(otro, "CLIENTE", correo, nit);
        var sesionOtro = await SesionAsync(correo, otro.Datos.Nombre);
        await AceptarAsync(sesionOtro, vinculoOtro.Id);
        var enOtro = await AbiertoEnAsync(otro, vinculoOtro.Id);
        var apagado = await EsperarAsync(otro, enOtro, x => x.Estado == "EnRevision");
        Requisito(apagado, "RUT").SoporteDeSecciones.ShouldBeNull();
        Requisito(apagado, "FICHA_GENERAL").Seccion!.Cruce!.ShouldSatisfyAllConditions(c => c.Estado.ShouldBe("SinDocumento"), c => c.Cruces.ShouldBeEmpty());
    }

    [Fact]
    public async Task Un_RUT_nuevo_propone_al_tercero_los_cambios_y_al_confirmarlos_la_empresa_revalida_la_general_con_su_cruce()
    {
        var e = await PrepararAsync();
        await PlanIaAsync(e.Norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        await FlujoRutYGeneralAsync(e);
        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "RUT", RutCon(nit, "Calle 45 # 12-08"));

        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var revision = await EsperarAsync(e.Norte, inicial, x => x.Estado == "EnRevision" && Requisito(x, "FICHA_GENERAL").Seccion?.Cruce?.Estado == "Cruzado");
        await AreaAsync<ExpedienteDto>(e, inicial, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revision, "RUT").Id] = 1 }));
        (await AreaAsync<ExpedienteDto>(e, inicial, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revision, "FICHA_GENERAL").Id] = 1 }))).Estado.ShouldBe("Aprobado");

        // Con el RUT de la ficha no hay nada que proponer.
        (await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/propuestas", sesion.AccessToken, null, 200))
            .ShouldNotContain(p => p.Seccion == "FICHA_GENERAL");

        // El tercero sube el RUT nuevo (cambió la dirección): la IA lo lee y la plataforma le propone el cambio; nada se guarda solo.
        var rut2 = (await SubirAsync(sesion, "RUT", RutCon(nit, "Cl 80 # 10-20"))).Version;
        // El RUT nuevo abre la actualización solo con el RUT (aún no hay nada que cambiar en la general).
        var actualizacionId = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        var soloRut = await EsperarAsync(e.Norte, actualizacionId, x => x.Estado == "EnRevision");
        soloRut.Tipo.ShouldBe("Actualizacion");
        soloRut.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT"]);
        List<PropuestaDeSeccionDto> propuestas = [];
        await EsperarAsync(async () =>
        {
            propuestas = await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/propuestas", sesion.AccessToken, null, 200);
            return propuestas.Any(p => p.Seccion == "FICHA_GENERAL" && p.Documento.Version == 2 && p.Disponible);
        }, "la propuesta del RUT v2");
        var resumen = propuestas.Single(p => p.Seccion == "FICHA_GENERAL");
        resumen.Campos.ShouldBe([new CampoPropuestoDto("direccion", "Dirección", "Calle 45 # 12-08", "Cl 80 # 10-20")]);
        resumen.Datos.ShouldBeNull();
        (await TerceroAsync<SeccionFichaDto>("GET", "/public/v1/ficha/FICHA_GENERAL", sesion.AccessToken, null, 200)).Version.ShouldBe(1);

        var detalle = (await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/FICHA_GENERAL/propuestas", sesion.AccessToken, null, 200))
            .ShouldHaveSingleItem();
        var datos = detalle.Datos!.Value;
        datos.GetProperty("direccion").GetString().ShouldBe("Cl 80 # 10-20");
        datos.GetProperty("correo").GetString().ShouldBe("compras@aceros.co");

        // Confirma la propuesta con el RUT v2 como soporte ⇒ versión 2 ⇒ la general entra a la MISMA actualización que abrió el RUT
        // (se amplía la actualización abierta; no se abre otro expediente) y se revalida con su cruce.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", datos.GetRawText(), new SoporteSeccionDto(rut2.DocumentoId, rut2.N));
        var actualizacion = await EsperarAsync(e.Norte, actualizacionId, x =>
            x.Requisitos!.Any(r => r.TipoDocumento.Codigo == "FICHA_GENERAL" && r.VersionActual?.N == 2 &&
                                   r.Seccion?.Cruce?.Cruces.Any(c => c.VersionDocumento == 2) == true));
        (await AbiertoEnAsync(e.Norte, vinculo.Id)).ShouldBe(actualizacionId);
        actualizacion.Tipo.ShouldBe("Actualizacion");
        actualizacion.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT", "FICHA_GENERAL"], ignoreOrder: true);
        var general = Requisito(actualizacion, "FICHA_GENERAL").Seccion!;
        general.Cambios.ShouldBe([new CambioDeCampoDto("direccion", "Dirección", "Calle 45 # 12-08", "Cl 80 # 10-20", "Modificado")]);
        var conRut2 = general.Cruce!.Cruces.Single(c => c.VersionDocumento == 2);
        CampoDe(conRut2, "direccion").ShouldSatisfyAllConditions(c => c.Resultado.ShouldBe("Coincide"), c => c.Declarado.ShouldBe("Cl 80 # 10-20"));
        conRut2.DiscrepanciaCritica.ShouldBeFalse();

        // Ya no hay nada que proponer para la general.
        (await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/propuestas", sesion.AccessToken, null, 200))
            .ShouldNotContain(p => p.Seccion == "FICHA_GENERAL");
    }

    [Fact]
    public async Task Sin_analisis_el_RUT_nuevo_no_propone_valores_y_el_tercero_diligencia_con_el_documento_a_la_vista()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE"); // sin plan de IA
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT"), new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL")]);
        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        var rut = (await SubirAsync(sesion, "RUT", RutCon(nit, "Cl 80 # 10-20"))).Version;

        var propuestas = await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/FICHA_GENERAL/propuestas", sesion.AccessToken, null, 200);
        var sinLectura = propuestas.ShouldHaveSingleItem();
        (sinLectura.Disponible, sinLectura.Motivo, sinLectura.Documento.DocumentoId, sinLectura.Campos.Count, sinLectura.Datos)
            .ShouldBe((false, "sin-lectura-automatica", rut.DocumentoId, 0, (JsonElement?)null));
        (await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/propuestas", sesion.AccessToken, null, 200))
            .Select(p => p.Seccion).ShouldBe(["FICHA_GENERAL", "FICHA_TRIBUTARIA", "FICHA_LEGAL"]);

        // Al guardar con el RUT como soporte el aviso se va.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta, new SoporteSeccionDto(rut.DocumentoId, rut.N));
        (await TerceroAsync<List<PropuestaDeSeccionDto>>("GET", "/public/v1/ficha/FICHA_GENERAL/propuestas", sesion.AccessToken, null, 200)).ShouldBeEmpty();
    }
}
