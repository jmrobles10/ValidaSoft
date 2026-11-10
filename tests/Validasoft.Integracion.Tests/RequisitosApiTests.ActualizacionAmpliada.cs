using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- ampliación de la actualización abierta (FM-48) ----------

    [Fact]
    public async Task Con_una_actualizacion_abierta_por_un_RUT_nuevo_guardar_la_general_cambiada_la_agrega_al_mismo_expediente_y_Compras_vuelve_a_revisar()
    {
        var e = await PrepararAsync();
        await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id]),
            new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id])
        ]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));

        // Expediente inicial aprobado por las dos áreas.
        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var revision = await EsperarAsync(e.Norte, inicial, x => x.Estado == "EnRevision" && x.Requisitos!.All(r => r.VersionActual is not null));
        await AreaAsync<ExpedienteDto>(e, inicial, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int>
        {
            [Requisito(revision, "RUT").Id] = 1, [Requisito(revision, "FICHA_GENERAL").Id] = 1
        }));
        (await AreaAsync<ExpedienteDto>(e, inicial, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revision, "RUT").Id] = 1 }))).Estado.ShouldBe("Aprobado");

        // El RUT nuevo abre la actualización solo con el RUT; Compras ya lo aprobó, Cumplimiento aún no: el expediente sigue abierto.
        var rut2 = (await SubirAsync(sesion, "RUT", Pdf("RUT v2"))).Version;
        var actualizacionId = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        var porElRut = await EsperarAsync(e.Norte, actualizacionId, x => x.Estado == "EnRevision");
        porElRut.Tipo.ShouldBe("Actualizacion");
        porElRut.Requisitos!.ShouldHaveSingleItem().TipoDocumento.Codigo.ShouldBe("RUT");
        await AreaAsync<ExpedienteDto>(e, actualizacionId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(porElRut, "RUT").Id] = 2 }));
        var comprasListo = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{actualizacionId}", Admin(e.Norte), null, 200);
        (comprasListo.Estado, Area(comprasListo, "COMPRAS").Estado, Area(comprasListo, "CUMPLIMIENTO").Estado).ShouldBe(("EnRevision", "Aprobada", "Pendiente"));

        // Después el tercero cambia la general (con el RUT nuevo de soporte): entra al MISMO expediente (no abre otro) y Compras vuelve a
        // tener algo pendiente.
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"),
            new SoporteSeccionDto(rut2.DocumentoId, rut2.N));
        var ampliada = await EsperarAsync(e.Norte, actualizacionId, x => x.Requisitos!.Count == 2);
        (await AbiertoEnAsync(e.Norte, vinculo.Id)).ShouldBe(actualizacionId);
        (ampliada.Tipo, ampliada.Estado).ShouldBe(("Actualizacion", "EnRevision"));
        var general = Requisito(ampliada, "FICHA_GENERAL");
        (general.Estado, general.VersionActual!.N, general.Seccion!.VersionAceptada).ShouldBe(("EnRevision", 2, (int?)1));
        general.Seccion.Cambios.ShouldBe([new CambioDeCampoDto("direccion", "Dirección", "Calle 45 # 12-08", "Carrera 7 # 80-12", "Modificado")]);
        general.Areas.ShouldHaveSingleItem().Codigo.ShouldBe("COMPRAS");
        var compras = Area(ampliada, "COMPRAS");
        compras.Estado.ShouldBe("Pendiente");
        compras.RequisitosPorDecidir.ShouldBe(1);
        Requisito(ampliada, "RUT").Areas.Single(a => a.Codigo == "COMPRAS").Veredictos.ShouldHaveSingleItem().Veredicto.ShouldBe("Aprobado"); // lo anterior sigue

        // Compras ve la general por decidir (aunque ya había aprobado el RUT) y la bandeja lo refleja.
        var deLaura = Area(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{actualizacionId}", Delegado(e.Norte, e.Laura), null, 200), "COMPRAS")
            .DocumentosPorDecidir!;
        deLaura.ShouldBe([new DocumentoPorDecidirDto(general.Id, 2, "Seccion", "FICHA_GENERAL")]);
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e.Norte, e.Laura), null, 200)).Datos
            .Single(f => f.ExpedienteId == actualizacionId).RequisitosPendientes.ShouldBe(1);
        (await ApiAsync<PaginaDto<RevalidacionDto>>("GET", "/api/v1/revalidaciones", Delegado(e.Norte, e.Laura), null, 200)).Datos
            .Where(r => r.ExpedienteId == actualizacionId).Select(r => r.TipoDocumentoCodigo).ShouldBe(["FICHA_GENERAL", "RUT"], ignoreOrder: true);

        // El certificado sigue con pendientes y el historial deja el evento.
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!.Estado
            .ShouldBe("ConPendientes");
        var eventos = await EventosAsync(e.Norte.Datos.TenantId, actualizacionId);
        eventos.OfType<Validasoft.Dominio.Expedientes.RequisitosAgregados>().ShouldHaveSingleItem().Requisitos.Single().Codigo.ShouldBe("FICHA_GENERAL");
        (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{actualizacionId}/historial", Admin(e.Norte), null, 200))
            .Select(h => h.Tipo).ShouldContain("requisitos_agregados");

        // Repetir el mensaje no agrega dos veces.
        await InvocarAsync(new SincronizarRequisitosDelVinculo(e.Norte.Datos.TenantId, vinculo.Id, "Tercero", sesion.TerceroId.ToString()));
        await InvocarAsync(new SincronizarRequisitosDelVinculo(e.Norte.Datos.TenantId, vinculo.Id, "Tercero", sesion.TerceroId.ToString()));
        (await EventosAsync(e.Norte.Datos.TenantId, actualizacionId)).OfType<Validasoft.Dominio.Expedientes.RequisitosAgregados>().ShouldHaveSingleItem();
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{actualizacionId}", Admin(e.Norte), null, 200)).Requisitos!.Count.ShouldBe(2);

        // Compras decide la general y Cumplimiento el RUT ⇒ la actualización se aprueba con las dos y se acepta la general v2.
        await AreaAsync<ExpedienteDto>(e, actualizacionId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [general.Id] = 2 }));
        (await AreaAsync<ExpedienteDto>(e, actualizacionId, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(ampliada, "RUT").Id] = 2 }))).Estado.ShouldBe("Aprobado");
        Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200), "FICHA_GENERAL")
            .VersionAceptada.ShouldBe(2);
    }

    [Fact]
    public async Task Un_cambio_en_otro_documento_mientras_la_actualizacion_esta_abierta_tambien_entra_a_ella_pero_un_expediente_inicial_no_se_amplia()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await AceptarAsync(sesion, vinculo.Id);

        // Expediente inicial abierto (con RUT y Cámara pedidos): una Cámara nueva lo pone al día, no agrega requisitos.
        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var cantidad = (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{inicial}", Admin(e.Norte), null, 200)).Requisitos!.Count;
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v1"));
        var conCamara = await EsperarAsync(e.Norte, inicial, x => Requisito(x, "CAMARA_COMERCIO").VersionActual is not null);
        conCamara.Requisitos!.Count.ShouldBe(cantidad);
        (await EventosAsync(e.Norte.Datos.TenantId, inicial)).OfType<Validasoft.Dominio.Expedientes.RequisitosAgregados>().ShouldBeEmpty();

        // Se aprueba todo.
        foreach (var (area, persona, codigo) in new[] { (e.Compras, e.Laura, "COMPRAS"), (e.Cumplimiento, e.Carlos, "CUMPLIMIENTO") })
        {
            var porDecidir = Area(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{inicial}", Delegado(e.Norte, persona), null, 200), codigo)
                .DocumentosPorDecidir ?? [];
            if (porDecidir.Count > 0)
                await AreaAsync<ExpedienteDto>(e, inicial, area, persona, new VeredictoDto("Aprobado", Versiones: porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)));
        }

        // El RUT nuevo abre la actualización (solo el RUT) y, con ella abierta, una Cámara nueva se agrega al mismo expediente.
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var actualizacionId = await AbiertoEnAsync(e.Norte, vinculo.Id, inicial);
        (await EsperarAsync(e.Norte, actualizacionId, x => x.Estado == "EnRevision")).Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT"]);

        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v2"));
        var ampliada = await EsperarAsync(e.Norte, actualizacionId, x => x.Requisitos!.Count == 2);
        (await AbiertoEnAsync(e.Norte, vinculo.Id)).ShouldBe(actualizacionId);
        ampliada.Requisitos!.Select(r => (r.TipoDocumento.Codigo, r.VersionActual!.N)).ShouldBe([("RUT", 2), ("CAMARA_COMERCIO", 2)], ignoreOrder: true);
        Requisito(ampliada, "CAMARA_COMERCIO").Estado.ShouldBe("EnRevision");
        (await EventosAsync(e.Norte.Datos.TenantId, actualizacionId)).OfType<Validasoft.Dominio.Expedientes.RequisitosAgregados>().ShouldHaveSingleItem();
    }
}
