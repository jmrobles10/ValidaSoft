using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Revisión automática con IA (ADR-0012, ítems 13 y 14) con el analizador falso (nunca OpenAI) y el procesador a un segundo:
/// configuración por empresa, Automático de punta a punta, excepciones en la bandeja (baja confianza y cruce con la ficha de F5),
/// muestreo (confirmar y revertir), Asistido con sugerencia y tope mensual.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private const string UrlRevision = "/api/v1/configuracion/revision-automatica";

    private Task<RevisionAutomaticaConfigDto> ConfigRevisionAsync(Empresa empresa, ActualizarRevisionAutomaticaDto dto) =>
        ApiAsync<RevisionAutomaticaConfigDto>("PUT", UrlRevision, Admin(empresa), dto, 200);

    private Task<List<ExcepcionDeRevisionDto>> ExcepcionesAsync(Empresa empresa, string estado = "Pendientes") =>
        ApiAsync<List<ExcepcionDeRevisionDto>>("GET", $"/api/v1/revision-automatica/excepciones?estado={estado}", Admin(empresa), null, 200);

    private static RevisionAutomaticaDto? RevisionDe(ExpedienteDto expediente, string codigoDelRequisito, string area) =>
        Requisito(expediente, codigoDelRequisito).RevisionesAutomaticas?.SingleOrDefault(r => r.AreaCodigo == area);

    /// <summary>Plan de IA con llave para la empresa; <paramref name="marcador"/> <c>baja</c> da confianza baja sin escalar (mismo modelo en ambos).</summary>
    private Task<PlanIaEmpresaDto> PlanParaRevisionAsync(Empresa empresa, string marcador = "ok") =>
        PlanIaAsync(empresa, marcador == "baja"
            ? new ActualizarPlanIaEmpresaDto(50, false, ApiKeyOpenAi: LlaveIa("baja"), ModeloPorDefecto: "gpt-6-luna", ModeloEscalamiento: "gpt-6-luna")
            : new ActualizarPlanIaEmpresaDto(50, false, ApiKeyOpenAi: LlaveIa()));

    /// <summary>Tercero (jurídica) invitado con el flujo vigente del tipo que sube sus documentos; devuelve el vínculo, su sesión y el expediente abierto.</summary>
    private async Task<(VinculoDto Vinculo, SesionTerceroDto Sesion, Guid ExpedienteId)> TerceroAceptaAsync(Empresa empresa, string tipoVinculo, string? nit = null)
    {
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(empresa, tipoVinculo, correo, nit);
        var sesion = await SesionAsync(correo, empresa.Datos.Nombre);
        await AceptarAsync(sesion, vinculo.Id);
        return (vinculo, sesion, await AbiertoEnAsync(empresa, vinculo.Id));
    }

    [Fact]
    public async Task La_configuracion_es_Manual_sin_guardar_se_valida_se_audita_y_es_privada_de_cada_empresa()
    {
        var e = await PrepararAsync();
        var otra = await EmpresaNuevaAsync("Oriente S.A.S.", "ORIENTE");

        // Sin configuración: Manual, sin IA disponible; ofrece los tipos que la IA analiza y las áreas de la empresa.
        var inicial = await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Delegado(e.Norte, e.Laura), null, 200);
        (inicial.Configurada, inicial.Nivel, inicial.IaDisponible, inicial.UmbralConfianza, inicial.PorcentajeMuestreo, inicial.EsperaIaMinutos)
            .ShouldBe((false, "Manual", false, 0.95m, 10, 10));
        inicial.TiposAnalizables.Select(t => t.Codigo).ShouldContain("RUT");
        inicial.TiposAnalizables.Select(t => t.Codigo).ShouldContain("CAMARA_COMERCIO");
        inicial.Areas.Select(a => a.Codigo).ShouldBe(["COMPRAS", "CUMPLIMIENTO"]);
        inicial.Uso.ShouldSatisfyAllConditions(u => u.Aprobadas.ShouldBe(0), u => u.Mes.Length.ShouldBe(7));

        // Solo quien administra la configuración la cambia.
        await ApiAsync("PUT", UrlRevision, Delegado(e.Norte, e.Laura), new ActualizarRevisionAutomaticaDto("Asistido"), 403);
        var todas = new[] { e.Compras.Id, e.Cumplimiento.Id };
        string[] tipos = ["RUT"];
        async Task<string?> Falla(ActualizarRevisionAutomaticaDto dto) => await CodigoAsync(await ApiAsync("PUT", UrlRevision, Admin(e.Norte), dto, 422));
        (await Falla(new ActualizarRevisionAutomaticaDto("Total"))).ShouldBe("nivel-invalido");
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", UmbralConfianza: 0.7m))).ShouldBe("umbral-invalido");
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", PorcentajeMuestreo: 120))).ShouldBe("muestreo-invalido");
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", TopeMensualAprobaciones: 0))).ShouldBe("tope-invalido");
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", TiposAutorizados: ["PASTEL"]))).ShouldBe("tipo-invalido");
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", TiposAutorizados: ["CUENTA_BANCARIA"]))).ShouldBe("tipo-invalido"); // un formulario no se analiza
        (await Falla(new ActualizarRevisionAutomaticaDto("Asistido", AreasAutorizadas: [Guid.NewGuid()]))).ShouldBe("area-invalida");
        (await Falla(new ActualizarRevisionAutomaticaDto("Automatico", TiposAutorizados: tipos))).ShouldBe("automatico-sin-alcance");
        (await Falla(new ActualizarRevisionAutomaticaDto("Automatico", TiposAutorizados: tipos, AreasAutorizadas: todas))).ShouldBe("ia-no-disponible"); // sin plan de IA

        // Asistido no exige plan; con plan y llave el Automático sí se puede.
        var asistido = await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Asistido", 0.9m, 20, 100, ["rut"], [e.Compras.Id], 15));
        (asistido.Configurada, asistido.Nivel, asistido.UmbralConfianza, asistido.PorcentajeMuestreo, asistido.TopeMensualAprobaciones, asistido.EsperaIaMinutos)
            .ShouldBe((true, "Asistido", 0.9m, 20, 100, 15));
        asistido.TiposAutorizados.ShouldBe(["RUT"]);
        asistido.ActualizadaEn.ShouldNotBeNull();
        await PlanParaRevisionAsync(e.Norte);
        var automatico = await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", TiposAutorizados: ["RUT", "CAMARA_COMERCIO"], AreasAutorizadas: todas));
        (automatico.Nivel, automatico.IaDisponible, automatico.UmbralConfianza, automatico.PorcentajeMuestreo).ShouldBe(("Automatico", true, 0.95m, 10));
        (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(e.Norte), null, 200)).AreasAutorizadas.ShouldBe(todas, ignoreOrder: true);

        // Cada cambio queda en la auditoría de la empresa.
        var auditoria = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=RevisionAutomaticaEmpresa", Admin(e.Norte), null, 200);
        auditoria.Datos.Select(r => r.Accion).ShouldBe(["Modificado", "Creado"]); // lo más reciente primero (los rechazados no se auditan)

        // Las decisiones de una empresa son privadas: otra empresa del sistema sigue en Manual.
        (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(otra), null, 200)).Nivel.ShouldBe("Manual");
        (await ExcepcionesAsync(otra)).ShouldBeEmpty();
    }

    [Fact]
    public async Task En_Automatico_el_sistema_aprueba_los_documentos_con_IA_y_el_expediente_se_cierra_solo_con_sus_veredictos_del_sistema()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte);
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 0,
            TiposAutorizados: ["RUT", "CAMARA_COMERCIO"], AreasAutorizadas: [e.Compras.Id, e.Cumplimiento.Id]));
        var (vinculo, sesion, expedienteId) = await TerceroAceptaAsync(e.Norte, "PROVEEDOR");

        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));

        // La IA analiza, la empresa recibe el análisis y el procesador decide: aprueba lo que no tiene excepciones.
        var aprobado = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "Aprobado");
        foreach (var (codigo, area) in new[] { ("RUT", "COMPRAS"), ("RUT", "CUMPLIMIENTO"), ("CAMARA_COMERCIO", "CUMPLIMIENTO") })
        {
            var revision = RevisionDe(aprobado, codigo, area).ShouldNotBeNull($"{codigo} en {area}");
            (revision.Nivel, revision.Resultado, revision.PendienteDeAtencion, revision.SobreVersion).ShouldBe(("Automatico", "AprobadaAutomaticamente", false, 1));
            revision.Confianza.ShouldBe(0.97m);
            revision.Motivos.ShouldBeEmpty();
        }

        // Los veredictos son del sistema, con su justificación, y las áreas, etapa y expediente avanzaron por la cascada.
        var veredicto = Requisito(aprobado, "RUT").Areas.Single(a => a.Codigo == "COMPRAS").Veredictos.Single(v => !v.Derivado);
        (veredicto.Origen, veredicto.Veredicto, veredicto.Usuario, veredicto.Vigente).ShouldBe(("Sistema", "Aprobado", "revision-automatica", true));
        veredicto.Justificacion.ShouldBe("Aprobado por la revisión automática (confianza 97 %), sin excepciones.");
        aprobado.Requisitos!.ShouldAllBe(r => r.Estado == "Aprobado");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Activo");

        // El uso del mes cuenta una aprobación por documento y área; nada quedó para una persona.
        var uso = (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(e.Norte), null, 200)).Uso;
        (uso.Aprobadas, uso.Muestreadas, uso.Excepciones).ShouldBe((3, 0, 0));
        (await ExcepcionesAsync(e.Norte)).ShouldBeEmpty();
        (await ExcepcionesAsync(e.Norte, "Resueltas")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Con_baja_confianza_el_documento_va_a_la_bandeja_de_excepciones_y_se_cierra_cuando_una_persona_decide()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte, "baja");
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 0,
            TiposAutorizados: ["RUT", "CAMARA_COMERCIO"], AreasAutorizadas: [e.Compras.Id, e.Cumplimiento.Id]));
        var (_, sesion, expedienteId) = await TerceroAceptaAsync(e.Norte, "PROVEEDOR");
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));

        // Nada se aprueba solo: tres excepciones (RUT en dos áreas y la Cámara), con su motivo, a la espera de una persona.
        var pendientes = await EsperarExcepcionesAsync(e.Norte, 3);
        pendientes.ShouldAllBe(f => f.Tipo == "Excepcion" && f.Resultado == "Excepcion" && !f.Resuelta && f.Folio == pendientes[0].Folio);
        pendientes.ShouldAllBe(f => f.Motivos.Single().Codigo == "confianza-baja" && f.Confianza == 0.5m);
        pendientes[0].Motivos.Single().Texto.ShouldBe("La confianza del análisis (50 %) está bajo el umbral de la empresa (95 %).");
        pendientes.Select(f => (f.Requisito, f.AreaCodigo)).ShouldBe(
            [("Registro Único Tributario", "COMPRAS"), ("Registro Único Tributario", "CUMPLIMIENTO"), ("Certificado de existencia y representación legal", "CUMPLIMIENTO")], ignoreOrder: true);
        var enRevision = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        enRevision.Estado.ShouldBe("EnRevision");
        Requisito(enRevision, "RUT").Areas.SelectMany(a => a.Veredictos).ShouldBeEmpty();
        RevisionDe(enRevision, "RUT", "COMPRAS")!.PendienteDeAtencion.ShouldBeTrue();
        (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(e.Norte), null, 200)).Uso.ShouldSatisfyAllConditions(
            u => u.Excepciones.ShouldBe(3), u => u.Aprobadas.ShouldBe(0));

        // Una persona decide el documento y la excepción se cierra sola; el resto sigue esperándola.
        var rut = Requisito(enRevision, "RUT");
        await VeredictoAsync<ExpedienteDto>(e, expedienteId, rut.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1));
        (await ExcepcionesAsync(e.Norte)).Select(f => (f.Requisito, f.AreaCodigo)).ShouldBe(
            [("Registro Único Tributario", "CUMPLIMIENTO"), ("Certificado de existencia y representación legal", "CUMPLIMIENTO")], ignoreOrder: true);
        var resuelta = (await ExcepcionesAsync(e.Norte, "Resueltas")).ShouldHaveSingleItem();
        (resuelta.Resuelta, resuelta.AreaCodigo, resuelta.Tipo).ShouldBe((true, "COMPRAS", "Excepcion"));

        // El procesador no vuelve a decidir lo que ya registró: sigue habiendo una sola revisión por documento y área.
        await Task.Delay(2500);
        var despues = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        despues.Requisitos!.Sum(r => r.RevisionesAutomaticas?.Count ?? 0).ShouldBe(3);
    }

    private async Task<List<ExcepcionDeRevisionDto>> EsperarExcepcionesAsync(Empresa empresa, int cantidad)
    {
        var filas = new List<ExcepcionDeRevisionDto>();
        await EsperarAsync(async () =>
        {
            filas = await ExcepcionesAsync(empresa);
            return filas.Count >= cantidad;
        }, $"{cantidad} excepciones en la bandeja");
        return filas;
    }

    [Fact]
    public async Task El_cruce_con_la_ficha_manda_a_una_persona_la_discrepancia_y_deja_aprobar_solo_lo_que_coincide()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte);
        await FlujoRutYGeneralAsync(e); // RUT: Compras; ficha general: Cumplimiento (las secciones nunca se aprueban solas)
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 0, TiposAutorizados: ["RUT"],
            AreasAutorizadas: [e.Compras.Id, e.Cumplimiento.Id]));

        // Razón social distinta en el RUT: discrepancia crítica con la ficha ⇒ excepción, no se aprueba sola.
        var nit = Nit();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, e.Norte.Datos.Nombre);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "RUT", RutCon(nit, "Calle 45 # 12-08", "FERRETERIA OTRA LTDA"));
        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);

        var excepcion = (await EsperarExcepcionesAsync(e.Norte, 1)).ShouldHaveSingleItem();
        (excepcion.Folio, excepcion.AreaCodigo, excepcion.Tipo).ShouldBe((excepcion.Folio, "COMPRAS", "Excepcion"));
        excepcion.Motivos.Select(m => m.Codigo).ShouldBe(["discrepancia-critica-ficha"]);
        var conExcepcion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        Requisito(conExcepcion, "RUT").SoporteDeSecciones!.DiscrepanciaCritica.ShouldBeTrue(); // lo mismo que ve el revisor
        Requisito(conExcepcion, "RUT").Areas.SelectMany(a => a.Veredictos).ShouldBeEmpty();
        Requisito(conExcepcion, "FICHA_GENERAL").RevisionesAutomaticas.ShouldBeNull(); // la sección no entra a la revisión automática

        // Otro tercero con el RUT idéntico a su ficha: el cruce coincide y el sistema aprueba el RUT (la sección sigue siendo de una persona).
        var otroNit = Nit();
        var otroCorreo = CorreoNuevo();
        var otroVinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", otroCorreo, otroNit);
        var otraSesion = await SesionAsync(otroCorreo, e.Norte.Datos.Nombre);
        await GuardarSeccionAsync(otraSesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(otraSesion, otroVinculo.Id);
        await SubirAsync(otraSesion, "RUT", RutCon(otroNit, "Calle 45 # 12-08"));
        var otroExpediente = await AbiertoEnAsync(e.Norte, otroVinculo.Id);
        var aprobado = await EsperarAsync(e.Norte, otroExpediente, x => RevisionDe(x, "RUT", "COMPRAS")?.Resultado == "AprobadaAutomaticamente");
        Requisito(aprobado, "RUT").Estado.ShouldBe("Aprobado");
        Requisito(aprobado, "RUT").SoporteDeSecciones!.DiscrepanciaCritica.ShouldBeFalse();
        aprobado.Estado.ShouldBe("EnRevision"); // falta la ficha general de Cumplimiento: una persona
        Area(aprobado, "CUMPLIMIENTO").Estado.ShouldBe("Pendiente");
        (await ExcepcionesAsync(e.Norte)).ShouldHaveSingleItem().Folio.ShouldBe(excepcion.Folio);
    }

    [Fact]
    public async Task Un_muestreo_se_aprueba_solo_y_una_persona_lo_confirma_o_lo_revierte_con_justificacion()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte);
        // RUT: Compras (el sistema); Cámara: Cumplimiento (una persona), así el expediente queda en revisión.
        await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO", AreasQueRevisan: [e.Cumplimiento.Id])
        ]);
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 100, TiposAutorizados: ["RUT", "CAMARA_COMERCIO"],
            AreasAutorizadas: [e.Compras.Id]));

        async Task<(Guid ExpedienteId, ExpedienteDto Expediente)> TerceroConMuestreoAsync()
        {
            var (_, sesion, id) = await TerceroAceptaAsync(e.Norte, "CLIENTE");
            await SubirAsync(sesion, "RUT", Pdf("RUT"));
            await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
            return (id, await EsperarAsync(e.Norte, id, x => RevisionDe(x, "RUT", "COMPRAS")?.Resultado == "AprobadaConMuestreo" &&
                                                           RevisionDe(x, "CAMARA_COMERCIO", "CUMPLIMIENTO") is not null));
        }

        // El área de Cumplimiento no está autorizada: la Cámara se revisa como siempre y no suma ruido a la bandeja.
        var (primero, expediente) = await TerceroConMuestreoAsync();
        RevisionDe(expediente, "CAMARA_COMERCIO", "CUMPLIMIENTO")!.ShouldSatisfyAllConditions(r => r.Resultado.ShouldBe("SinAlcance"),
            r => r.Motivos.Single().Codigo.ShouldBe("area-no-autorizada"), r => r.PendienteDeAtencion.ShouldBeFalse());
        var revision = RevisionDe(expediente, "RUT", "COMPRAS")!;
        (revision.PendienteDeAtencion, revision.Resolucion).ShouldBe((true, null));
        Area(expediente, "COMPRAS").Estado.ShouldBe("Aprobada");
        expediente.Estado.ShouldBe("EnRevision");
        var fila = (await ExcepcionesAsync(e.Norte)).ShouldHaveSingleItem();
        (fila.Tipo, fila.Resultado, fila.Requisito, fila.AreaCodigo, fila.Version, fila.Motivos.Count).ShouldBe(("Muestreo", "AprobadaConMuestreo", "Registro Único Tributario", "COMPRAS", 1, 0));
        var rut = Requisito(expediente, "RUT");
        var url = $"/api/v1/expedientes/{primero}/requisitos/{rut.Id}/revision-automatica/resolver";

        // Solo una persona del área, con una decisión válida, y revertir exige justificación.
        await ApiAsync("POST", url, Delegado(e.Norte, e.Carlos), new ResolverRevisionAutomaticaDto(e.Compras.Id, "Confirmar"), 403);
        (await CodigoAsync(await ApiAsync("POST", url, Delegado(e.Norte, e.Laura), new ResolverRevisionAutomaticaDto(e.Compras.Id, "Quizás"), 422))).ShouldBe("decision-invalida");
        (await CodigoAsync(await ApiAsync("POST", url, Delegado(e.Norte, e.Laura), new ResolverRevisionAutomaticaDto(e.Compras.Id, "Revertir", " "), 422)))
            .ShouldBe("justificacion-requerida");

        // Revertir anula el veredicto del sistema y devuelve el RUT a Compras (una persona).
        var revertido = await ApiAsync<ExpedienteDto>("POST", url, Delegado(e.Norte, e.Laura),
            new ResolverRevisionAutomaticaDto(e.Compras.Id, "Revertir", "El NIT del RUT no es el del tercero."), 200);
        Requisito(revertido, "RUT").Estado.ShouldBe("EnRevision");
        Area(revertido, "COMPRAS").Estado.ShouldBe("Pendiente");
        var anulado = Requisito(revertido, "RUT").Areas.Single().Veredictos.Single();
        (anulado.Origen, anulado.Vigente, anulado.Anulado, anulado.AnuladoPor).ShouldBe(("Sistema", false, true, e.Laura.Usuario.Id.ToString()));
        RevisionDe(revertido, "RUT", "COMPRAS")!.ShouldSatisfyAllConditions(r => r.Resolucion.ShouldBe("Revertida"), r => r.PendienteDeAtencion.ShouldBeFalse(),
            r => r.JustificacionResolucion.ShouldBe("El NIT del RUT no es el del tercero."));
        (await ExcepcionesAsync(e.Norte)).ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("POST", url, Delegado(e.Norte, e.Laura), new ResolverRevisionAutomaticaDto(e.Compras.Id, "Confirmar"), 409))).ShouldBe("revision-ya-resuelta");
        await Task.Delay(2500); // el procesador no vuelve a aprobar lo que una persona revirtió
        Requisito(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{primero}", Admin(e.Norte), null, 200), "RUT").Areas.Single().Veredictos
            .ShouldHaveSingleItem().Vigente.ShouldBeFalse();
        await VeredictoAsync<ExpedienteDto>(e, primero, rut.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1));

        // Otro expediente: la persona confirma y la aprobación del sistema queda como está.
        var (segundo, _) = await TerceroConMuestreoAsync();
        var otroRut = Requisito(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{segundo}", Admin(e.Norte), null, 200), "RUT");
        var confirmado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{segundo}/requisitos/{otroRut.Id}/revision-automatica/resolver",
            Delegado(e.Norte, e.Laura), new ResolverRevisionAutomaticaDto(e.Compras.Id, "Confirmar"), 200);
        RevisionDe(confirmado, "RUT", "COMPRAS")!.ShouldSatisfyAllConditions(r => r.Resolucion.ShouldBe("Confirmada"), r => r.PendienteDeAtencion.ShouldBeFalse());
        Requisito(confirmado, "RUT").Areas.Single().Veredictos.Single().Vigente.ShouldBeTrue();
        Area(confirmado, "COMPRAS").Estado.ShouldBe("Aprobada");
        (await ExcepcionesAsync(e.Norte)).ShouldBeEmpty();
        (await ExcepcionesAsync(e.Norte, "Resueltas")).Select(f => f.Resolucion).ShouldBe(["Confirmada", "Revertida"], ignoreOrder: true);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/revision-automatica/excepciones?estado=Todas", Admin(e.Norte), null, 422))).ShouldBe("estado-invalido");
    }

    [Fact]
    public async Task En_Asistido_el_sistema_sugiere_sin_emitir_veredictos_y_sin_analisis_sugiere_revisar()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte);
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Asistido"));
        var (_, sesion, expedienteId) = await TerceroAceptaAsync(e.Norte, "PROVEEDOR");
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));

        var sugerido = await EsperarAsync(e.Norte, expedienteId, x => x.Requisitos!.All(r => r.RevisionesAutomaticas is { Count: > 0 }));
        foreach (var (codigo, area) in new[] { ("RUT", "COMPRAS"), ("RUT", "CUMPLIMIENTO"), ("CAMARA_COMERCIO", "CUMPLIMIENTO") })
        {
            var r = RevisionDe(sugerido, codigo, area).ShouldNotBeNull();
            (r.Nivel, r.Resultado, r.Sugerencia, r.PendienteDeAtencion).ShouldBe(("Asistido", "Sugerida", "Aprobar", false));
        }

        // Asistido nunca emite veredictos: el expediente espera a las personas y la bandeja de excepciones no recibe nada.
        sugerido.Estado.ShouldBe("EnRevision");
        sugerido.Requisitos!.SelectMany(r => r.Areas).SelectMany(a => a.Veredictos).ShouldBeEmpty();
        (await ExcepcionesAsync(e.Norte)).ShouldBeEmpty();
        (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(e.Norte), null, 200)).Uso.Sugerencias.ShouldBe(3);

        // Sin plan activo la empresa no recibe el análisis: la sugerencia es revisar, sin esperar a un análisis que no va a llegar.
        await PlanIaAsync(e.Norte, new ActualizarPlanIaEmpresaDto(50, false, Activo: false));
        var (_, otraSesion, otroExpediente) = await TerceroAceptaAsync(e.Norte, "PROVEEDOR");
        await SubirAsync(otraSesion, "RUT", Pdf("RUT"));
        await SubirAsync(otraSesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        var sinAnalisis = await EsperarAsync(e.Norte, otroExpediente, x => RevisionDe(x, "CAMARA_COMERCIO", "CUMPLIMIENTO") is not null);
        RevisionDe(sinAnalisis, "CAMARA_COMERCIO", "CUMPLIMIENTO")!.ShouldSatisfyAllConditions(r => r.Sugerencia.ShouldBe("Revisar"),
            r => r.Motivos.Single().Codigo.ShouldBe("sin-analisis-ia"), r => r.Confianza.ShouldBeNull());
    }

    [Fact]
    public async Task Alcanzado_el_tope_mensual_de_aprobaciones_automaticas_el_resto_va_a_una_persona()
    {
        var e = await PrepararAsync();
        await PlanParaRevisionAsync(e.Norte);
        await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])
        ]);
        await ConfigRevisionAsync(e.Norte, new ActualizarRevisionAutomaticaDto("Automatico", PorcentajeMuestreo: 0, TopeMensualAprobaciones: 1,
            TiposAutorizados: ["RUT", "CAMARA_COMERCIO"], AreasAutorizadas: [e.Compras.Id]));
        var (_, sesion, expedienteId) = await TerceroAceptaAsync(e.Norte, "CLIENTE");
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));

        // Se espera la decisión de Compras sobre cada documento, no «alguna revisión»: la Cámara también la ve Cumplimiento, que no está autorizada
        // y se anota «SinAlcance» en la primera pasada sin esperar el análisis; con «alguna revisión» la espera terminaba antes de que Compras decidiera
        // sobre la Cámara (el análisis aún no llegaba) y `sinPresupuesto` era nulo.
        var decidido = await EsperarAsync(e.Norte, expedienteId, x => RevisionDe(x, "RUT", "COMPRAS") is not null && RevisionDe(x, "CAMARA_COMERCIO", "COMPRAS") is not null);
        RevisionDe(decidido, "RUT", "COMPRAS")!.Resultado.ShouldBe("AprobadaAutomaticamente");
        var sinPresupuesto = RevisionDe(decidido, "CAMARA_COMERCIO", "COMPRAS")!;
        (sinPresupuesto.Resultado, sinPresupuesto.PendienteDeAtencion).ShouldBe(("Excepcion", true));
        sinPresupuesto.Motivos.Single().ShouldBe(new MotivoDeRevisionDto("sin-presupuesto", "Se alcanzó el tope mensual de aprobaciones automáticas."));
        decidido.Estado.ShouldBe("EnRevision");

        var uso = (await ApiAsync<RevisionAutomaticaConfigDto>("GET", UrlRevision, Admin(e.Norte), null, 200)).Uso;
        (uso.Aprobadas, uso.Excepciones, uso.Tope).ShouldBe((1, 1, 1));
        (await ExcepcionesAsync(e.Norte)).ShouldHaveSingleItem().Motivos.Single().Codigo.ShouldBe("sin-presupuesto");
    }
}
