using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- pendientes de la ficha (FD-1…FD-8) ----------

    /// <summary>Las dos áreas del flujo (Compras y Cumplimiento) aprueban lo que les queda por decidir (con una justificación si el caso la pide).</summary>
    private async Task<ExpedienteDto> AreasApruebanAsync(Escenario e, Guid expedienteId, string? justificacion = null)
    {
        ExpedienteDto? resultado = null;
        foreach (var (area, persona) in new[] { (e.Compras, e.Laura), (e.Cumplimiento, e.Carlos) })
        {
            var porDecidir = Area(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Delegado(e.Norte, persona), null, 200), area.Codigo)
                .DocumentosPorDecidir!;
            resultado = await AreaAsync<ExpedienteDto>(e, expedienteId, area, persona,
                new VeredictoDto("Aprobado", Versiones: porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente), Justificacion: justificacion));
        }

        return resultado!;
    }

    [Fact]
    public async Task La_general_se_ofrece_prellenada_y_las_observaciones_de_las_empresas_llegan_con_el_estado_de_la_ficha()
    {
        var e = await PrepararAsync();
        await FlujoConAsync(e.Norte, "CLIENTE", [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);

        // Sin ninguna versión guardada: razón social, identificación (con DV) y el correo de la invitación, solo como sugerencia.
        var vacia = await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200);
        vacia.Version.ShouldBeNull();
        var prellenado = vacia.Prellenado.ShouldNotBeNull();
        prellenado.GetProperty("razon_social").GetString().ShouldStartWith("Tercero 9");
        prellenado.GetProperty("correo").GetString().ShouldBe(correo);
        (prellenado.GetProperty("tipo_identificacion").GetString(), prellenado.GetProperty("tipo_persona").GetString()).ShouldBe(("NIT", "Juridica"));
        prellenado.GetProperty("numero").GetString().ShouldNotBeNullOrEmpty();
        prellenado.GetProperty("dv").GetString().ShouldNotBeNullOrEmpty();
        // Las demás secciones no se prellenan.
        (await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_TRIBUTARIA", sesion.AccessToken, null, 200)).Prellenado.ShouldBeNull();

        // Guardada la primera versión, ya no se ofrece.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        (await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200)).Prellenado.ShouldBeNull();
        (await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200)).Observaciones.ShouldNotBeNull().ShouldBeEmpty();

        // La empresa observa la sección con un motivo y un campo: llega en el mismo estado de la ficha (sin pedir cada expediente).
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var enRevision = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        var general = Requisito(enRevision, "FICHA_GENERAL");
        await AreaAsync<ExpedienteDto>(e, expedienteId, e.Compras, e.Laura, new VeredictoDto("Observado", Requisitos:
            [new ObservacionDeRequisitoDto(general.Id, 1, ["datos-no-coinciden"], "El teléfono no responde.", ["telefono"])]));

        var estado = await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200);
        var observacion = estado.Observaciones.ShouldNotBeNull().ShouldHaveSingleItem();
        (observacion.Empresa, observacion.ExpedienteId, observacion.Seccion, observacion.Periodo).ShouldBe((e.Norte.Datos.Nombre, expedienteId, "FICHA_GENERAL", (int?)null));
        observacion.Observacion.Motivos.ShouldHaveSingleItem().Codigo.ShouldBe("datos-no-coinciden");
        observacion.Observacion.Observacion.ShouldBe("El teléfono no responde.");
        observacion.Observacion.Campos.ShouldBe(["telefono"]);
        // Es lo mismo que dice el expediente público, y nunca trae áreas ni revisores.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        publico.Requisitos!.Single().Observaciones.ShouldHaveSingleItem().Motivos.Select(m => m.Codigo).ShouldBe(["datos-no-coinciden"]);

        // Corregida (con el RUT como documento nuevo) y reenviada, la observación sale del estado.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("555 1234", "555 9999"), new SoporteSeccionDto(rut.DocumentoId, rut.N));
        await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "FICHA_GENERAL").VersionActual!.N == 2);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken, new ReenviarExpedienteDto(), 200);
        (await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200)).Observaciones.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task La_ficha_aceptada_separa_lo_aceptado_de_lo_que_el_tercero_cambio_despues_y_un_cambio_critico_sin_documento_nuevo_pide_justificacion()
    {
        var e = await PrepararAsync();
        await FlujoConAsync(e.Norte, "CLIENTE", [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        var urlFicha = $"/api/v1/vinculos/{vinculo.Id}/ficha-aceptada";

        // Sin nada aceptado, la ficha aceptada está vacía (lo que la empresa no aceptó no aparece).
        (await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200)).Secciones.ShouldBeEmpty();

        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        var primero = await AbiertoEnAsync(e.Norte, vinculo.Id);
        await EsperarAsync(e.Norte, primero, x => x.Estado == "EnRevision");
        (await AreasApruebanAsync(e, primero)).Estado.ShouldBe("Aprobado");

        // Lo aceptado: versión 1, con su expediente, sus datos y, sin soporte, la lista vacía; nada posterior.
        var inicial = await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200);
        (inicial.VinculoId, inicial.TipoPersona).ShouldBe((vinculo.Id, "Juridica"));
        var aceptada1 = inicial.Secciones.ShouldHaveSingleItem();
        (aceptada1.Seccion, aceptada1.Nombre, aceptada1.Aceptada.Version, aceptada1.Aceptada.ExpedienteId, aceptada1.CambioPosterior)
            .ShouldBe(("FICHA_GENERAL", "Información general", 1, (Guid?)primero, (CambioPosteriorDelTerceroDto?)null));
        aceptada1.Aceptada.Folio.ShouldNotBeNullOrEmpty();
        aceptada1.Aceptada.Origen.ShouldBe("Expediente");
        aceptada1.Aceptada.Datos.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");
        aceptada1.Aceptada.Soportes.ShouldBeEmpty();

        // El tercero cambia la razón social con el RUT como documento nuevo ⇒ la empresa sigue viendo lo aceptado y, aparte, el cambio.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        var soporteRut = new SoporteSeccionDto(rut.DocumentoId, rut.N);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Aceros del Norte", "Aceros del Sur"), soporteRut);
        var segundo = await AbiertoEnAsync(e.Norte, vinculo.Id, primero);
        var actualizacion = await EsperarAsync(e.Norte, segundo, x => x.Estado == "EnRevision");
        // Con un documento nuevo, el cambio no queda «sin soporte».
        Requisito(actualizacion, "FICHA_GENERAL").Seccion!.SinSoporte.ShouldBeNull();

        var pendiente = (await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200)).Secciones.ShouldHaveSingleItem();
        pendiente.Aceptada.Version.ShouldBe(1);
        pendiente.Aceptada.Datos.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");
        var posterior = pendiente.CambioPosterior.ShouldNotBeNull();
        posterior.Version.ShouldBe(2);
        posterior.Cambios.ShouldHaveSingleItem().ShouldBe(new CambioDeCampoDto("razon_social", "Razón social", "Aceros del Norte S.A.S.", "Aceros del Sur S.A.S.", "Modificado"));
        posterior.Soportes.ShouldHaveSingleItem().TipoDocumento.ShouldBe("RUT");
        posterior.SinSoporte.ShouldBeNull();
        (posterior.Revalidacion!.ExpedienteId, posterior.Revalidacion.Estado).ShouldBe((segundo, "EnRevision"));

        // Compras la acepta (con el RUT): ahora lo aceptado es la versión 2, con su soporte, y ya no hay nada posterior.
        (await AreasApruebanAsync(e, segundo)).Estado.ShouldBe("Aprobado");
        var aceptada2 = (await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200)).Secciones.ShouldHaveSingleItem();
        (aceptada2.Aceptada.Version, aceptada2.Aceptada.ExpedienteId, aceptada2.CambioPosterior).ShouldBe((2, (Guid?)segundo, (CambioPosteriorDelTerceroDto?)null));
        aceptada2.Aceptada.Soportes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(s => s.TipoDocumento.ShouldBe("RUT"), s => s.Retirado.ShouldBeFalse());

        // El tercero vuelve a cambiar la razón social con el mismo RUT de antes: sin documento nuevo ⇒ «sin soporte» y crítico.
        var guardada = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Aceros del Norte", "Aceros del Centro"), soporteRut);
        guardada.Completitud.Completa.ShouldBeTrue();
        var sinSoporte = guardada.Seccion.SinSoporte.ShouldNotBeNull();
        sinSoporte.Critico.ShouldBeTrue();
        sinSoporte.Cambios.Select(c => c.Campo).ShouldBe(["razon_social"]);
        var tercero = await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200);
        tercero.SinSoporte.ShouldNotBeNull().Critico.ShouldBeTrue();

        var tercero3 = await AbiertoEnAsync(e.Norte, vinculo.Id, segundo);
        var revision = await EsperarAsync(e.Norte, tercero3, x => x.Estado == "EnRevision");
        var requisito = Requisito(revision, "FICHA_GENERAL");
        var marca = requisito.Seccion!.SinSoporte.ShouldNotBeNull();
        (marca.Critico, marca.Cambios.ShouldHaveSingleItem().Campo).ShouldBe((true, "razon_social"));
        // El cambio se mide contra lo que aceptó la empresa (la versión 2, con ese RUT): la razón social cambió y el soporte es el mismo.
        requisito.Seccion.VersionAceptada.ShouldBe(2);

        // En la vista de la ficha aceptada también se ve, aparte de lo aceptado.
        var conCambio = (await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200)).Secciones.ShouldHaveSingleItem();
        conCambio.Aceptada.Version.ShouldBe(2);
        conCambio.CambioPosterior.ShouldNotBeNull().SinSoporte.ShouldNotBeNull().Critico.ShouldBeTrue();

        // Aprobarlo sin justificación: 422; con ella, queda escrita en el veredicto. Tampoco se aprueba en lote.
        var porDecidir = Area(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{tercero3}", Delegado(e.Norte, e.Laura), null, 200), "COMPRAS").DocumentosPorDecidir!;
        var versiones = porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente);
        (await CodigoAsync(await AreaAsync(e, tercero3, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: versiones), 422)))
            .ShouldBe("justificacion-por-discrepancias");
        var conJustificacion = await AreasApruebanAsync(e, tercero3, "Cambio de razón social por fusión; verificado con la Cámara de Comercio.");
        conJustificacion.Estado.ShouldBe("Aprobado");
        Requisito(conJustificacion, "FICHA_GENERAL").Areas.Single(a => a.Codigo == "COMPRAS").Veredictos.ShouldHaveSingleItem().Justificacion
            .ShouldBe("Cambio de razón social por fusión; verificado con la Cámara de Comercio.");
        (await ApiAsync<FichaAceptadaDelVinculoDto>("GET", urlFicha, Admin(e.Norte), null, 200)).Secciones.ShouldHaveSingleItem().Aceptada.Version.ShouldBe(3);
    }

    /// <summary>Certificación bancaria con marcas que lee el analizador falso (como el simulado de la API).</summary>
    private static byte[] CertificacionCon(string numeroCuenta, string identificacionTitular, string estado = "Activa") =>
        Pdf($"Certificación [[banco=Bancolombia S.A.]][[tipoCuenta=Cuenta de ahorros]][[numeroCuenta={numeroCuenta}]][[titular=ACEROS DEL NORTE SAS]]" +
            $"[[identificacionTitular={identificacionTitular}]][[estadoCuenta={estado}]]");

    [Fact]
    public async Task La_certificacion_bancaria_respalda_las_cuentas_y_su_cruce_va_enmascarado_y_pide_justificacion_si_discrepa()
    {
        var e = await PrepararAsync();
        await PlanIaAsync(e.Norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        var flujo = await FlujoConAsync(e.Norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "CERTIFICACION_BANCARIA", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])]);
        flujo.Publicada!.Requisitos!.Single().UsarComoSoporte.ShouldBeNull(); // activo por omisión: la certificación respalda las cuentas

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var nit = (await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200)).Tercero.Identificacion.Numero;
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        // El tercero registra su cuenta (el número se escribe como quiera) y sube la certificación que dice lo mismo.
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", $"NIT {nit}"), 201);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", CertificacionCon("03112345678", nit));

        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var conCruce = await EsperarAsync(e.Norte, expedienteId,
            x => Requisito(x, "CERTIFICACION_BANCARIA").SoporteDeSecciones?.Secciones.Any(s => s.Cruce is not null) == true);
        var soporte = Requisito(conCruce, "CERTIFICACION_BANCARIA").SoporteDeSecciones!;
        soporte.SinAnalisis.ShouldBeFalse();
        var fila = soporte.Secciones.ShouldHaveSingleItem();
        (fila.Seccion, fila.Nombre).ShouldBe(("CUENTAS_BANCARIAS", "Cuenta de Bancolombia ****5678"));
        var cruce = fila.Cruce.ShouldNotBeNull();
        cruce.TipoDocumento.ShouldBe("CERTIFICACION_BANCARIA");
        cruce.DiscrepanciaCritica.ShouldBeFalse();
        cruce.Campos.Select(c => (c.Campo, c.Resultado)).ShouldBe(
        [
            ("numero", "Coincide"), ("titular", "Coincide"), ("identificacion_titular", "Coincide"), ("titular_es_el_tercero", "Coincide"),
            ("estado_cuenta", "Coincide"), ("banco", "Coincide"), ("tipo_cuenta", "Coincide")
        ]);
        var numero = cruce.Campos.Single(c => c.Campo == "numero");
        (numero.Declarado, numero.Extraido, numero.Critico).ShouldBe(("****5678", "****5678", true));
        // El número completo nunca sale en lo que ve la empresa.
        JsonSerializer.Serialize(conCruce).ShouldNotContain("03112345678");
        soporte.DiscrepanciaCritica.ShouldBeFalse();

        // Una certificación nueva con otro número de cuenta: discrepancia crítica ⇒ no se aprueba sin justificación.
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", CertificacionCon("99999999999", nit));
        var discrepa = await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "CERTIFICACION_BANCARIA").VersionActual?.N == 2 &&
                                                                       Requisito(x, "CERTIFICACION_BANCARIA").SoporteDeSecciones?.Secciones.Any(s => s.Cruce is not null) == true);
        var requisito = Requisito(discrepa, "CERTIFICACION_BANCARIA");
        requisito.SoporteDeSecciones!.DiscrepanciaCritica.ShouldBeTrue();
        requisito.SoporteDeSecciones.Secciones.Single().Cruce!.Campos.Single(c => c.Campo == "numero").ShouldSatisfyAllConditions(
            c => c.Resultado.ShouldBe("Discrepa"), c => c.Extraido.ShouldBe("****9999"));
        JsonSerializer.Serialize(discrepa).ShouldNotContain("99999999999");
        (await CodigoAsync(await VeredictoAsync(e, expedienteId, requisito.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 2), 422)))
            .ShouldBe("justificacion-por-discrepancias");
    }
}
