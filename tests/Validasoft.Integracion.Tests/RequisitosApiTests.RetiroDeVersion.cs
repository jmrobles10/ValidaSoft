using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- el tercero retira una versión: impacto antes, avisos y soporte de la ficha después (RT-1…RT-4) ----------

    private static string RutaImpacto(Guid documentoId, int numero) => $"/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retiro/impacto";

    private static string RutaRetiro(Guid documentoId, int numero) => $"/public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retirar";

    private Task<ImpactoDeRetiroDto> ImpactoAsync(SesionTerceroDto sesion, Guid documentoId, int numero) =>
        TerceroAsync<ImpactoDeRetiroDto>("GET", RutaImpacto(documentoId, numero), sesion.AccessToken, null, 200);

    private async Task EsperarSinExpedienteAbiertoAsync(Escenario e, Guid vinculoId) =>
        await EsperarAsync(async () => (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId is null);

    [Fact]
    public async Task Retirar_la_version_que_una_empresa_aprobo_muestra_el_impacto_abre_la_revalidacion_de_la_anterior_con_el_motivo_y_avisa()
    {
        var a = await ProveedorAprobadoAsync();
        var e = a.E;

        // Norte puede personalizar el correo de este aviso (plantilla editable, como las demás).
        var textos = new ActualizarPlantillaNotificacionDto("Retiro de {{documento}} en {{folio}}", "Revisa con calma, {{empresa}}.", null);
        (await ApiAsync<VistaPreviaCorreoDto>("POST", "/api/v1/plantillas-notificacion/retiro-version/correo/vista-previa", Admin(e.Norte), textos, 200))
            .Asunto.ShouldStartWith("Retiro de RUT en NORTE-E-");
        await ApiAsync<PlantillaNotificacionDto>("PUT", "/api/v1/plantillas-notificacion/retiro-version/correo", Admin(e.Norte), textos, 200);

        // La v2 abre una revalidación y Norte la aprueba: la v2 es la que Norte tiene aprobada.
        var v2 = await SubirAsync(a.Sesion, "RUT", Pdf("RUT v2"));
        var documentoId = v2.Version.DocumentoId;
        var revalidacion1 = await ExpedienteAbiertoAsync(e, a.Vinculo.Id);
        revalidacion1.ShouldNotBe(a.Expediente);
        await AprobarAreasAsync(e, revalidacion1);
        await EsperarSinExpedienteAbiertoAsync(e, a.Vinculo.Id);

        // Antes de retirar: la v2 es la vigente, queda la v1 y Norte, que la aprobó, tendrá que revalidar. Solo el nombre y lo que hizo.
        var impacto = await ImpactoAsync(a.Sesion, documentoId, 2);
        impacto.EsLaVigente.ShouldBeTrue();
        impacto.VersionQueQuedaVigente.ShouldBe(1);
        var norte = impacto.Empresas.ShouldHaveSingleItem();
        norte.Empresa.Nombre.ShouldBe("Norte S.A.S.");
        (norte.Situacion, norte.Efecto, norte.Folio).ShouldBe(("Aprobo", "Revalidar", null));
        impacto.Secciones.ShouldBeEmpty();

        // La v1 ya no es la vigente: retirarla no mueve nada. Solo lectura: nada se retiró con consultar.
        var impactoDeLaAnterior = await ImpactoAsync(a.Sesion, documentoId, 1);
        impactoDeLaAnterior.EsLaVigente.ShouldBeFalse();
        impactoDeLaAnterior.VersionQueQuedaVigente.ShouldBe(2);
        impactoDeLaAnterior.Empresas.ShouldHaveSingleItem().Efecto.ShouldBe("SinCambios");
        (await TerceroAsync<DocumentoDto>("GET", $"/public/v1/boveda/documentos/{documentoId}", a.Sesion.AccessToken, null, 200)).VersionActual.ShouldBe(2);

        // Retira la v2 con su motivo: vuelve la v1 y Norte recibe la revalidación de la v1.
        var retiro = await TerceroAsync<RetiroDto>("POST", RutaRetiro(documentoId, 2), a.Sesion.AccessToken, new RetirarVersionDto("Subí el RUT de otra empresa"), 200);
        retiro.Documento.VersionActual.ShouldBe(1);
        var revalidacion2 = await ExpedienteAbiertoAsync(e, a.Vinculo.Id);
        revalidacion2.ShouldNotBe(revalidacion1);
        var actualizacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{revalidacion2}", Admin(e.Norte), null, 200);
        actualizacion.Tipo.ShouldBe("Actualizacion");
        var rut = Requisito(actualizacion, "RUT");
        rut.VersionActual!.N.ShouldBe(1);
        rut.Retiro.ShouldNotBeNull().Numero.ShouldBe(2);
        rut.Retiro.Motivo.ShouldBe("Subí el RUT de otra empresa");

        // La bandeja de revalidaciones dice por qué y no deja aprobarla en lote (va hacia atrás: se revisa a conciencia).
        var fila = (await ApiAsync<PaginaDto<RevalidacionDto>>("GET", "/api/v1/revalidaciones", Admin(e.Norte), null, 200)).Datos
            .Single(r => r.ExpedienteId == revalidacion2);
        fila.Retiro.ShouldNotBeNull().Motivo.ShouldBe("Subí el RUT de otra empresa");
        fila.AprobableEnLote.ShouldBeFalse();
        var lote = await ApiAsync<ResultadoDeLoteDto>("POST", "/api/v1/revalidaciones/aprobar", Delegado(e.Norte, e.Laura),
            new AprobarRevalidacionesDto([new RevalidacionAAprobarDto(revalidacion2, rut.Id, 1)]), 200);
        lote.Aprobadas.ShouldBe(0);
        lote.Fallidas.ShouldHaveSingleItem().Codigo.ShouldBe("revalidacion-con-cambios");

        // La línea de tiempo del expediente trae el motivo en los datos del requisito.
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{revalidacion2}/historial", Admin(e.Norte), null, 200);
        var calculados = historial.Single(h => h.Tipo == "requisitos_calculados").Datos.GetProperty("requisitos")[0];
        calculados.GetProperty("retiro").GetProperty("motivo").GetString().ShouldBe("Subí el RUT de otra empresa");
        calculados.GetProperty("retiro").GetProperty("numero").GetInt32().ShouldBe(2);

        // Aviso a las áreas: campana (en lugar de «Nuevo expediente en tu área») y correo con la plantilla de Norte.
        await EsperarAsync(async () => (await CampanaAsync(e, e.Laura)).Any(n => n.ExpedienteId == revalidacion2 && n.Tipo == "documento.version_retirada"));
        var aviso = (await CampanaAsync(e, e.Laura)).Single(n => n.ExpedienteId == revalidacion2 && n.Tipo == "documento.version_retirada");
        aviso.Titulo.ShouldBe("Versión retirada por el tercero");
        aviso.Mensaje.ShouldContain("retiró la versión 2 de Registro Único Tributario (motivo: Subí el RUT de otra empresa), vuelve la 1. Se abrió una actualización");
        (await CampanaAsync(e, e.Laura)).ShouldNotContain(n => n.ExpedienteId == revalidacion2 && n.Tipo == "expediente.nuevo_en_area");
        var correo = await fixture.Correos.EsperarAsync(e.Laura.Usuario.Correo, m => m.Asunto.StartsWith("Retiro de Registro Único Tributario en NORTE-E-"));
        correo.Texto.ShouldStartWith("Revisa con calma, Norte S.A.S.");
        correo.Texto.ShouldContain("Subí el RUT de otra empresa");

        // La tarjeta de la bóveda lo cuenta sin contradicciones: la vigente es la v1 y Norte aprobó la v2 que retiraste.
        var boveda = await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", a.Sesion.AccessToken, null, 200);
        var tarjeta = boveda.Documentos.Single(d => d.Id == documentoId);
        tarjeta.VersionActual!.N.ShouldBe(1);
        var enNorte = tarjeta.PorEmpresa.ShouldHaveSingleItem();
        (enNorte.Version, enNorte.VersionActualCompartida, enNorte.Situacion, enNorte.OtraVersion).ShouldBe((1, true, "RevalidaRetirada", 2));
    }

    [Fact]
    public async Task Retirar_la_version_que_un_expediente_abierto_revisa_la_cambia_a_la_anterior_con_el_motivo_y_avisa_a_las_areas()
    {
        var a = await ProveedorAprobadoAsync();
        var e = a.E;

        // v2 abre una revalidación; v3 llega mientras se revisa y la reemplaza: el expediente revisa la v3.
        var v2 = await SubirAsync(a.Sesion, "RUT", Pdf("RUT v2"));
        var documentoId = v2.Version.DocumentoId;
        var revalidacion = await ExpedienteAbiertoAsync(e, a.Vinculo.Id);
        await SubirAsync(a.Sesion, "RUT", Pdf("RUT v3"));
        await EsperarAsync(e.Norte, revalidacion, x => Requisito(x, "RUT").VersionActual!.N == 3);

        var impacto = await ImpactoAsync(a.Sesion, documentoId, 3);
        impacto.VersionQueQuedaVigente.ShouldBe(2);
        var empresa = impacto.Empresas.ShouldHaveSingleItem();
        (empresa.Situacion, empresa.Efecto).ShouldBe(("Revisa", "VolverARevisar"));
        empresa.Folio.ShouldBe((await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{revalidacion}", Admin(e.Norte), null, 200)).Folio);

        await TerceroAsync<RetiroDto>("POST", RutaRetiro(documentoId, 3), a.Sesion.AccessToken, new RetirarVersionDto("La v3 era de otra empresa"), 200);

        // El mismo expediente vuelve a la v2 y lo dice: la versión retirada, su motivo y cuál vuelve.
        var despues = await EsperarAsync(e.Norte, revalidacion, x => Requisito(x, "RUT").VersionActual!.N == 2);
        var rut = Requisito(despues, "RUT");
        rut.Retiro.ShouldNotBeNull().Numero.ShouldBe(3);
        rut.Retiro.Motivo.ShouldBe("La v3 era de otra empresa");
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{revalidacion}/historial", Admin(e.Norte), null, 200);
        var adjuntada = historial.Last(h => h.Tipo == "version_adjuntada_a_requisito").Datos;
        adjuntada.GetProperty("numeroAnterior").GetInt32().ShouldBe(3);
        adjuntada.GetProperty("retiro").GetProperty("motivo").GetString().ShouldBe("La v3 era de otra empresa");

        // Las dos áreas que revisan el RUT se enteran con el motivo (no como «versión nueva»).
        foreach (var persona in new[] { e.Laura, e.Carlos })
        {
            await EsperarAsync(async () => (await CampanaAsync(e, persona)).Any(n => n.ExpedienteId == revalidacion && n.Tipo == "documento.version_retirada"));
            var aviso = (await CampanaAsync(e, persona)).Single(n => n.ExpedienteId == revalidacion && n.Tipo == "documento.version_retirada");
            aviso.Mensaje.ShouldContain("retiró la versión 3 de Registro Único Tributario (motivo: La v3 era de otra empresa), vuelve la 2. Lo que se había decidido sobre ella quedó sin efecto");
        }

        // La tarjeta: Norte revisa la v2 y la v1 fue lo último que aprobó.
        var tarjeta = (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", a.Sesion.AccessToken, null, 200)).Documentos.Single(d => d.Id == documentoId);
        var enNorte = tarjeta.PorEmpresa.ShouldHaveSingleItem();
        (enNorte.Version, enNorte.Situacion, enNorte.OtraVersion).ShouldBe((2, "Revalida", 1));
    }

    [Fact]
    public async Task Retirar_una_version_que_ninguna_empresa_recibio_es_simple_y_lo_que_no_se_puede_retirar_responde_igual_en_el_impacto()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo); // invitada, sin aceptar: nada se comparte
        var sesion = await SesionAsync(correo);
        var v1 = await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var documentoId = v1.Version.DocumentoId;

        var impacto = await ImpactoAsync(sesion, documentoId, 2);
        (impacto.EsLaVigente, impacto.VersionQueQuedaVigente).ShouldBe((true, 1));
        impacto.Empresas.ShouldBeEmpty();
        impacto.Secciones.ShouldBeEmpty();

        // Retirada: ya no se puede consultar su impacto ni retirar otra vez (409), una inexistente es 404 y el documento de otro tercero también.
        (await TerceroAsync<RetiroDto>("POST", RutaRetiro(documentoId, 2), sesion.AccessToken, new RetirarVersionDto("error"), 200)).BinarioEliminado.ShouldBeTrue();
        await TerceroAsync("GET", RutaImpacto(documentoId, 2), sesion.AccessToken, null, 409);
        await TerceroAsync("GET", RutaImpacto(documentoId, 9), sesion.AccessToken, null, 404);
        var otra = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", otra);
        await TerceroAsync("GET", RutaImpacto(documentoId, 1), (await SesionAsync(otra)).AccessToken, null, 404);

        // Sin sesión del tercero: 401.
        await Host.Scenario(s =>
        {
            s.Get.Url(RutaImpacto(documentoId, 1));
            IpAleatoria(s);
            s.StatusCodeShouldBe(401);
        });
    }

    [Fact]
    public async Task Retirar_el_soporte_de_una_seccion_la_marca_sin_borrar_datos_y_el_impacto_la_nombra()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, Nit()); // un NIT propio: los terceros son globales y su ficha se comparte entre pruebas
        var sesion = await SesionAsync(correo);
        var rut = await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        var soporte = new SoporteSeccionDto(rut.Version.DocumentoId, 1);
        var guardada = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta, soporte);
        guardada.Completitud.Completa.ShouldBeTrue();
        guardada.Seccion.Soportes.ShouldHaveSingleItem().Retirado.ShouldBeFalse();

        // El impacto nombra la sección que usa esa versión como soporte.
        var seccion = (await ImpactoAsync(sesion, rut.Version.DocumentoId, 1)).Secciones.ShouldHaveSingleItem();
        (seccion.Seccion, seccion.Periodo).ShouldBe(("FICHA_GENERAL", null));

        await TerceroAsync<RetiroDto>("POST", RutaRetiro(rut.Version.DocumentoId, 1), sesion.AccessToken, new RetirarVersionDto("Era el RUT equivocado"), 200);

        // La sección conserva sus datos y su completitud; el soporte sale marcado como retirado, con el nombre legible del tipo.
        var despues = await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200);
        despues.Version.ShouldBe(guardada.Version);
        despues.Datos!.Value.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");
        despues.Completitud!.Completa.ShouldBeTrue();
        var retirado = despues.Soportes.ShouldHaveSingleItem();
        (retirado.Retirado, retirado.Nombre, retirado.Version).ShouldBe((true, "Registro Único Tributario", 1));

        // En el resumen de la ficha queda como advertencia de esa sección (no como faltante: la sección no se recalcula).
        var estado = await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200);
        var fila = estado.Secciones.Single(s => s.Seccion == "FICHA_GENERAL");
        fila.SoportesRetirados.ShouldNotBeNull().ShouldHaveSingleItem().Nombre.ShouldBe("Registro Único Tributario");
        fila.Completa.ShouldBeTrue();
        estado.Secciones.Where(s => s.Seccion != "FICHA_GENERAL").ShouldAllBe(s => s.SoportesRetirados == null);

        // Un soporte retirado no se puede volver a elegir.
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", GeneralCompleta, soporte)).Codigo.ShouldBe("soporte-version-invalida");
    }
}
