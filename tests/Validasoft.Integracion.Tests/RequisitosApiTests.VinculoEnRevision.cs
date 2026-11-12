using System.Text.Json;
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

    /// <summary>La verificación pública del certificado (anónima) por su folio raíz y su huella.</summary>
    private async Task<CertificadoPublicoDto> PublicoCertificadoAsync(string folioRaiz, string huella)
    {
        var respuesta = await Host.Scenario(s =>
        {
            s.Get.Url($"/public/v1/verificar/{folioRaiz}?huella={Uri.EscapeDataString(huella)}");
            IpAleatoria(s);
            s.StatusCodeShouldBe(200);
        });
        return (await respuesta.ReadAsJsonAsync<CertificadoPublicoDto>())!;
    }

    /// <summary>
    /// Norte con el flujo CLIENTE en una etapa (Compras y Cumplimiento, y Cumplimiento puede rechazar) que pide el RUT, y un tercero ya aprobado por las dos áreas.
    /// </summary>
    private async Task<(Escenario E, VinculoDto Vinculo, SesionTerceroDto Sesion, string Correo, Guid Inicial)> AprobadoConRechazoPosibleAsync()
    {
        var e = await PrepararAsync();
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e.Norte), new CrearFlujoDto("HAB_NR_" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(),
            "Habilitación", "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Compras.Id), new AreaDeEtapaDto(e.Cumplimiento.Id, PuedeRechazarExpediente: true)])],
            Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])]), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e.Norte), new { }, 200);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await AceptarAsync(sesion, vinculo.Id);
        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var revision = await EsperarAsync(e.Norte, inicial, x => x.Estado == "EnRevision");
        var versiones = new Dictionary<Guid, int> { [Requisito(revision, "RUT").Id] = 1 };
        await AreaAsync<ExpedienteDto>(e, inicial, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: versiones));
        (await AreaAsync<ExpedienteDto>(e, inicial, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", Versiones: versiones))).Estado.ShouldBe("Aprobado");
        return (e, vinculo, sesion, correo, inicial);
    }

    [Fact]
    public async Task Una_actualizacion_rechazada_deja_el_vinculo_como_estaba_con_el_certificado_vigente_y_avisa_al_tercero_y_a_la_empresa_hasta_la_siguiente_actualizacion()
    {
        var (e, vinculo, sesion, correo, inicial) = await AprobadoConRechazoPosibleAsync();

        // El RUT nuevo: el tercero recibe la confirmación (campana) de que la empresa lo está revisando, no el «necesita que actualices» de una petición de la empresa.
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacion = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        var enRevision = await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        var recibida = (await EsperarBandejaAsync(sesion, b => b.Datos.Any(n => n.Tipo == "tercero.actualizacion"))).Datos.First(n => n.Tipo == "tercero.actualizacion");
        (recibida.Titulo, recibida.Mensaje).ShouldBe(("Recibimos tu actualización",
            "Recibimos tu actualización de Registro Único Tributario; Norte S.A.S. la está revisando y te avisamos si necesita algo."));
        var certificadoEnRevision = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!;
        (certificadoEnRevision.EnVerificacion, certificadoEnRevision.Estado).ShouldBe((true, "ConPendientes"));

        // Cumplimiento rechaza lo que el tercero actualizó: el vínculo conserva su estado (RV-2) y el certificado vuelve a ser vigente a secas.
        var rechazada = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{actualizacion}/rechazar", Delegado(e.Norte, e.Carlos),
            new RechazarExpedienteDto(e.Cumplimiento.Id, ["no-corresponde-al-tercero"], "Interno: el NIT del RUT no es el del tercero."), 200);
        rechazada.Estado.ShouldBe("Rechazado");
        var sinAprobar = await VinculoDeAsync(e.Norte, vinculo.Id);
        (sinAprobar.Estado, sinAprobar.EnRevision, sinAprobar.ExpedienteAbiertoId, sinAprobar.UltimoResultado).ShouldBe(("Activo", false, (Guid?)null, "Rechazado"));
        sinAprobar.ActualizacionNoAprobada.ShouldNotBeNull().ShouldSatisfyAllConditions(a => a.Resultado.ShouldBe("Rechazado"), a => a.ExpedienteId.ShouldBe(actualizacion),
            a => a.Fecha.ShouldNotBeNull());
        var certificado = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!;
        (certificado.Estado, certificado.EnVerificacion, certificado.Bloqueante).ShouldBe(("Vigente", false, false));
        var consulta = await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}", Admin(e.Norte), null, 200);
        (consulta.EstadoVinculo, consulta.Estado, consulta.Bloqueado, consulta.EnRevision, consulta.EnVerificacion).ShouldBe(("Activo", "Vigente", false, false, false));
        consulta.RevalidacionPendiente.ShouldBeEmpty();
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", Admin(e.Norte), null, 200);
        // El historial del vínculo trae el cierre con el tipo de expediente (para decir «Actualización rechazada») y el certificado deja constancia de que volvió a Vigente.
        historial.Where(h => h.Tipo == "expediente_cerrado").ShouldContain(h => h.Datos.EnumerateObject().Any(p => string.Equals(p.Name, "tipo", StringComparison.OrdinalIgnoreCase) &&
            (p.Value.ToString() == "2" || p.Value.ToString() == "Actualizacion")));
        (await EventosAsync(e.Norte.Datos.TenantId, Validasoft.Dominio.Certificados.Certificado.IdPara(vinculo.Id))).OfType<Validasoft.Dominio.Certificados.PendientesDeRevalidacionResueltos>().ShouldHaveSingleItem();

        // El tercero ve que no se aprobó y por qué (los textos que se le comunican; nunca la justificación interna ni las áreas), y que lo ya aprobado sigue vigente.
        var texto = await (await TerceroAsync("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).ReadAsTextAsync();
        texto.ShouldNotContain("Interno");
        texto.ShouldNotContain("CUMPLIMIENTO");
        var publico = (JsonSerializer.Deserialize<List<VinculoPublicoDto>>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Single(v => v.Id == vinculo.Id);
        (publico.Estado, publico.EnRevision).ShouldBe(("Activo", false));
        var aviso = publico.ActualizacionNoAprobada.ShouldNotBeNull();
        aviso.Resultado.ShouldBe("Rechazado");
        aviso.Motivos.Select(m => m.Codigo).ShouldBe(["no-corresponde-al-tercero"]);
        aviso.Motivos.ShouldHaveSingleItem().Texto.ShouldNotBeNullOrWhiteSpace();
        var campana = (await EsperarBandejaAsync(sesion, b => b.Datos.Any(n => n.Titulo == "Tu actualización no se aprobó"))).Datos.First(n => n.Titulo == "Tu actualización no se aprobó");
        campana.Mensaje.ShouldStartWith("Norte S.A.S. no aprobó tu actualización ");
        campana.Mensaje.ShouldContain("lo que ya tenías aprobado sigue vigente.");
        var mensaje = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.StartsWith("Norte S.A.S. no aprobó tu actualización"));
        mensaje.Texto.ShouldContain("Lo que ya tenías aprobado sigue vigente.");
        mensaje.Texto.ShouldNotContain("Interno");

        // La siguiente actualización quita el aviso: el vínculo vuelve a estar en revisión y, aprobada, ya no hay nada que avisar.
        await SubirAsync(sesion, "RUT", Pdf("RUT v3"));
        var siguiente = await AbiertoEnAsync(e.Norte, vinculo.Id);
        siguiente.ShouldNotBe(actualizacion);
        var enRevisionOtraVez = await EsperarAsync(e.Norte, siguiente, x => x.Estado == "EnRevision");
        var conAviso = await VinculoDeAsync(e.Norte, vinculo.Id);
        (conAviso.EnRevision, conAviso.ActualizacionNoAprobada).ShouldBe((true, (ActualizacionNoAprobadaDto?)null));
        (await VinculoPublicoAsync(sesion, vinculo.Id)).ActualizacionNoAprobada.ShouldBeNull();
        var versiones = new Dictionary<Guid, int> { [Requisito(enRevisionOtraVez, "RUT").Id] = 3 };
        await AreaAsync<ExpedienteDto>(e, siguiente, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: versiones));
        (await AreaAsync<ExpedienteDto>(e, siguiente, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", Versiones: versiones))).Estado.ShouldBe("Aprobado");
        var aprobada = await VinculoDeAsync(e.Norte, vinculo.Id);
        (aprobada.EnRevision, aprobada.ActualizacionNoAprobada, aprobada.UltimoResultado).ShouldBe((false, (ActualizacionNoAprobadaDto?)null, "Aprobado"));
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!.Estado.ShouldBe("Vigente");
    }

    [Fact]
    public async Task Una_actualizacion_anulada_tambien_deja_el_vinculo_como_estaba_y_avisa_sin_motivos_al_tercero()
    {
        var (e, vinculo, sesion, _, inicial) = await AprobadoConRechazoPosibleAsync();
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacion = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");
        (await VinculoDeAsync(e.Norte, vinculo.Id)).EnRevision.ShouldBeTrue();

        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{actualizacion}/anular", Admin(e.Norte), new MotivoDto("La subió por error."), 200)).Estado.ShouldBe("Anulado");

        var anulada = await VinculoDeAsync(e.Norte, vinculo.Id);
        (anulada.Estado, anulada.EnRevision, anulada.UltimoResultado).ShouldBe(("Activo", false, "Anulado"));
        anulada.ActualizacionNoAprobada.ShouldNotBeNull().Resultado.ShouldBe("Anulado");
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!
            .ShouldSatisfyAllConditions(c => c.Estado.ShouldBe("Vigente"), c => c.EnVerificacion.ShouldBeFalse());

        // Una anulación no lleva motivos al tercero (son internos), pero igual ve que esa actualización no se aprobó.
        var texto = await (await TerceroAsync("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).ReadAsTextAsync();
        texto.ShouldNotContain("por error");
        var aviso = (JsonSerializer.Deserialize<List<VinculoPublicoDto>>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Single(v => v.Id == vinculo.Id).ActualizacionNoAprobada;
        aviso!.Resultado.ShouldBe("Anulado");
        aviso.Motivos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Terminar_el_vinculo_con_una_actualizacion_abierta_no_deja_un_aviso_de_actualizacion_no_aprobada_al_reintegrarlo_sin_revalidar()
    {
        var (e, vinculo, sesion, _, inicial) = await AprobadoConRechazoPosibleAsync();
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacion = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        await EsperarAsync(e.Norte, actualizacion, x => x.Estado == "EnRevision");

        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e.Norte), new MotivoDto("Fin del contrato."), 200);
        var terminado = await VinculoDeAsync(e.Norte, vinculo.Id);
        (terminado.Estado, terminado.EnRevision, terminado.ActualizacionNoAprobada).ShouldBe(("Desvinculado", false, (ActualizacionNoAprobadaDto?)null));
        (await VinculoPublicoAsync(sesion, vinculo.Id)).ActualizacionNoAprobada.ShouldBeNull();
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

            // El certificado sigue siendo válido y se ve «En verificación» (VR-11): su estado de dominio sigue siendo ConPendientes y nada se bloquea ni se suspende.
            var certificado = await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(empresa), null, 200);
            (certificado.Actual!.Estado, certificado.Actual.Bloqueante, certificado.Actual.EnVerificacion).ShouldBe(("ConPendientes", false, true));
            var consulta = await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}", Admin(empresa), null, 200);
            (consulta.EstadoVinculo, consulta.Estado, consulta.Certificado, consulta.Bloqueado, consulta.EnRevision, consulta.EnVerificacion)
                .ShouldBe(("Activo", "ConPendientes", true, false, true, true));
            consulta.RevalidacionPendiente.ShouldHaveSingleItem().Tipo.ShouldBe("RUT");

            // La lista de certificados lo filtra como «En verificación» (y «Con pendientes» no lo repite) y las cifras lo cuentan aparte.
            (await ApiAsync<PaginaDto<FilaCertificadoDto>>("GET", "/api/v1/certificados?enVerificacion=true", Admin(empresa), null, 200)).Datos
                .ShouldHaveSingleItem().ShouldSatisfyAllConditions(f => f.Estado.ShouldBe("ConPendientes"), f => f.EnVerificacion.ShouldBeTrue(), f => f.Bloqueante.ShouldBeFalse());
            (await ApiAsync<PaginaDto<FilaCertificadoDto>>("GET", "/api/v1/certificados?estado=ConPendientes&enVerificacion=false", Admin(empresa), null, 200)).Datos.ShouldBeEmpty();
            (await ApiAsync<PaginaDto<FilaCertificadoDto>>("GET", "/api/v1/certificados?estado=ConPendientes", Admin(empresa), null, 200)).Datos.ShouldHaveSingleItem(); // compatibilidad
            (cifras.CertificadosEnVerificacion, cifras.CertificadosConPendientes).ShouldBe((1, 0));

            // Quien lo verifica en la página pública ve que sigue vigente y puede guardar la copia; el estado sigue siendo ConPendientes.
            var publicoCert = await PublicoCertificadoAsync(certificado.Actual.FolioRaiz, certificado.Actual.Huella);
            (publicoCert.Estado, publicoCert.Bloqueante, publicoCert.EnVerificacion, publicoCert.PdfDescargable).ShouldBe(("ConPendientes", false, true, true));

            // El informe de vínculos lo cuenta y lo filtra como «En revisión», con su estado de dominio intacto.
            var informe = await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos", Admin(empresa), null, 200);
            var fila = informe.Datos.ShouldHaveSingleItem();
            (fila.Estado, fila.EnRevision, fila.ExpedienteEnCurso!.Tipo, fila.Certificado!.EnVerificacion).ShouldBe(("Activo", true, "Actualizacion", true));
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
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{enNorte.Id}/certificado", Admin(norte), null, 200)).Actual!
            .ShouldSatisfyAllConditions(c => c.Estado.ShouldBe("Vigente"), c => c.EnVerificacion.ShouldBeFalse());
        (await VinculosAsync(norte, "enRevision=true")).ShouldBeEmpty();
        (await VinculosAsync(norte, "estado=Activo&enRevision=false")).ShouldBe([enNorte.Id]);
        var cifrasNorte = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores", Admin(norte), null, 200);
        (cifrasNorte.VinculosEnRevision, cifrasNorte.VinculosActivos, cifrasNorte.CertificadosEnVerificacion).ShouldBe((0, 1, 0));
        (await ApiAsync<PaginaDto<FilaCertificadoDto>>("GET", "/api/v1/certificados?enVerificacion=true", Admin(norte), null, 200)).Datos.ShouldBeEmpty();
        (await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={enNorte.Id}", Admin(norte), null, 200))
            .ShouldSatisfyAllConditions(c => c.EnRevision.ShouldBeFalse(), c => c.EnVerificacion.ShouldBeFalse(), c => c.Estado.ShouldBe("Vigente"), c => c.RevalidacionPendiente.ShouldBeEmpty());

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
