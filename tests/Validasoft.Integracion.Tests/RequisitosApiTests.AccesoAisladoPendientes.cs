using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Los cuatro pendientes del acceso aislado (decisión AV): quien registra primero un NIT ajeno queda como contacto sin verificar y no decide; los contactos se verifican con
/// el correo del RUT o de la Cámara, por corroboración de dos grupos o por la plataforma; un tercero sin contactos verificados deja sus solicitudes en la cola de la consola; el
/// token embebido de un expediente no toca las demás cuentas del tercero; y los vínculos aceptados antes del acceso aislado se migran.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private Task<ContactoDelTerceroDto> VerificarContactoDePlataformaAsync(Guid terceroId, string correo) =>
        ApiAsync<ContactoDelTerceroDto>("POST", $"/api/v1/plataforma/terceros/{terceroId}/contactos/verificar", Plataforma(), new VerificarContactoDto(correo, "Verificado en la prueba"), 200);

    private Task<List<SolicitudDeAccesoDePlataformaDto>> SolicitudesDePlataformaAsync(string estado = "cola") =>
        ApiAsync<List<SolicitudDeAccesoDePlataformaDto>>("GET", $"/api/v1/plataforma/solicitudes-de-acceso?estado={estado}", Plataforma(), null, 200);

    private Task<ContactosDelTerceroDto> ContactosAsync(SesionTerceroDto sesion) =>
        TerceroAsync<ContactosDelTerceroDto>("GET", "/public/v1/contactos-del-tercero", sesion.AccessToken, null, 200);

    private async Task SincronizarAsync(Guid terceroId)
    {
        using var alcance = Host.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new SincronizarAccesoDelTercero(terceroId));
    }

    private async Task<Tercero> TerceroDeBovedaAsync(Guid terceroId) =>
        (await CargarAsync<Tercero>(terceroId)).ShouldNotBeNull();

    private async Task<RegistroDeCambioDeConfiguracion[]> AuditoriaDeAsync(string entidad, string entidadId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return [.. await consulta.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == entidad && r.EntidadId == entidadId).ToListAsync()];
    }

    private async Task<Empresa> EmpresaConFlujoAsync(string nombre, string prefijo)
    {
        var empresa = await EmpresaNuevaAsync(nombre, prefijo);
        await FlujoConAsync(empresa, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        return empresa;
    }

    // ---------- 1. secuestro del NIT ----------

    [Fact]
    public async Task AV_Secuestro_del_NIT_el_primer_correo_no_decide_y_cuando_se_verifica_el_de_B_el_de_A_pasa_a_aislado_con_sus_versiones_en_revision()
    {
        var a = await EmpresaConFlujoAsync("Secuestradora S.A.S.", "SECU");
        var b = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        var (secuestrador, legitimo, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());

        // A registra primero el NIT con su propio correo: contacto sin verificar que entra completo a lo de su tercero y sube un RUT a la bóveda.
        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", secuestrador, nit);
        var sesionA = await SesionAsync(secuestrador, "Secuestradora");
        sesionA.Aislada.ShouldBeFalse();
        var rutA = await SubirAsync(sesionA, "RUT", Pdf("RUT de A"));
        await AceptarAsync(sesionA, vinculoA.Id);
        var inicial = await ContactosAsync(sesionA);
        var propio = inicial.Contactos.ShouldHaveSingleItem();
        (propio.Verificacion, propio.Medio, propio.Desplazado, propio.EsTuCorreo).ShouldBe(("SinVerificar", null, false, true));
        (inicial.PuedesDecidir, inicial.DecideLaPlataforma).ShouldBe((false, true));
        inicial.Historial.ShouldHaveSingleItem().Tipo.ShouldBe("registrado");
        (await VinculoDelAislamientoAsync(a, vinculoA.Id)).VerificacionDelCorreo!.Estado.ShouldBe("SinVerificar");

        // B invita al correo del RUT: no es contacto, entra aislado y, como ningún contacto está verificado, decide la plataforma: el correo de A ni lo aprueba ni lo rechaza.
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", legitimo, nit);
        vinculoB.TerceroId.ShouldBe(vinculoA.TerceroId);
        vinculoB.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await VinculoDelAislamientoAsync(b, vinculoB.Id)).VerificacionDelCorreo!.Estado.ShouldBe("NoEsContacto");
        var solicitud = (await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", sesionA.AccessToken, null, 200)).ShouldHaveSingleItem();
        (solicitud.Correo, solicitud.PuedesDecidir, solicitud.DecideLaPlataforma).ShouldBe((legitimo, false, true));
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/rechazar", sesionA.AccessToken, new RechazarAccesoDto("No lo conozco"), 403)))
            .ShouldBe("contacto-sin-verificar");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/aprobar", sesionA.AccessToken, null, 403))).ShouldBe("contacto-sin-verificar");
        await Task.Delay(500);
        fixture.Correos.Para(secuestrador).Any(EsAvisoDeAcceso).ShouldBeFalse(); // no decide: no recibe el aviso

        // La solicitud espera en la cola de la consola con la evidencia: quién invitó, los contactos y su verificación.
        var enCola = (await SolicitudesDePlataformaAsync()).Single(s => s.Id == solicitud.Id);
        (enCola.Correo, enCola.EnCola, enCola.Estado, enCola.TerceroId).ShouldBe((legitimo, true, "Pendiente", vinculoA.TerceroId));
        enCola.Empresas.ShouldHaveSingleItem().Empresa.ShouldBe("Norte S.A.S.");
        enCola.Contactos.ShouldHaveSingleItem().Verificacion.ShouldBe("SinVerificar");
        enCola.CorreosOficiales.ShouldBeEmpty();

        // Un operador aprueba el correo de B: queda como contacto verificado por la plataforma y, al sincronizar, el de A (sin verificar) pasa a aislado.
        var aprobada = await ApiAsync<SolicitudDeAccesoDePlataformaDto>("POST", $"/api/v1/plataforma/solicitudes-de-acceso/{solicitud.Id}/aprobar", Plataforma(), new { }, 200);
        (aprobada.Estado, aprobada.DecididoVia, aprobada.EnCola).ShouldBe(("Aprobada", "Plataforma", false));
        await SincronizarAsync(vinculoA.TerceroId);
        var sesionB = await SesionAsync(legitimo, "Norte");
        sesionB.Aislada.ShouldBeFalse();
        var despues = await ContactosAsync(sesionB);
        (despues.PuedesDecidir, despues.DecideLaPlataforma).ShouldBe((true, false));
        var delLegitimo = despues.Contactos.Single(c => c.Correo == legitimo);
        (delLegitimo.Verificacion, delLegitimo.Medio, delLegitimo.Desplazado).ShouldBe(("Verificado", "Plataforma", false));
        var delSecuestrador = despues.Contactos.Single(c => c.Correo == secuestrador);
        (delSecuestrador.Verificacion, delSecuestrador.Desplazado).ShouldBe(("SinVerificar", true));
        despues.Historial.Select(h => h.Tipo).ShouldContain("verificado");
        despues.Historial.ShouldContain(h => h.Tipo == "paso_a_aislado" && h.Correo == secuestrador);

        // El de A perdió la bóveda global: su sesión se cortó, su vínculo quedó pendiente (con el motivo) y lo que subió está en revisión.
        await TerceroAsync("GET", "/public/v1/vinculos", sesionA.AccessToken, null, 401);
        var vinculoDesplazado = await VinculoDelAislamientoAsync(a, vinculoA.Id);
        (vinculoDesplazado.AccesoDelCorreo!.Estado, vinculoDesplazado.AccesoDelCorreo.Motivo).ShouldBe(("Pendiente", "ContactoDesplazado"));
        (await DocumentoDeBovedaAsync(rutA.Version.DocumentoId))!.Versiones.Single(v => v.Numero == 1).EnRevisionDesde.ShouldNotBeNull();
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(secuestrador))).Context.Response.StatusCode.ShouldBe(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted);
        (await SesionPorAvisoAsync(secuestrador, m => m.Asunto.Contains("para entrar", StringComparison.Ordinal))).Aislada.ShouldBeTrue();
        var aviso = await fixture.Correos.EsperarAsync(legitimo, m => m.Asunto.Contains(secuestrador, StringComparison.Ordinal) && m.Asunto.Contains("acceso aislado", StringComparison.Ordinal));
        aviso.Texto.ShouldContain("marcados para revisión");

        // Ahora decide un contacto verificado (el de B): su solicitud nació del desplazamiento y puede devolverle el acceso, con lo que las versiones dejan de estar en revisión.
        var solicitudes = await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", sesionB.AccessToken, null, 200);
        var pendiente = solicitudes.SingleOrDefault(s => s.Correo == secuestrador)
                        ?? throw new InvalidOperationException("Solicitudes: " + string.Join("; ", solicitudes.Select(s => $"{s.Correo}:{s.Estado}")));
        (pendiente.ContactoDesplazado, pendiente.PuedesDecidir, pendiente.Estado).ShouldBe((true, true, "Pendiente"));
        var devuelta = await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{pendiente.Id}/aprobar", sesionB.AccessToken, null, 200);
        (devuelta.Estado, devuelta.DecididoPor, devuelta.DecididoVia).ShouldBe(("Aprobada", legitimo, "Contacto"));
        (await ContactosAsync(sesionB)).Contactos.Single(c => c.Correo == secuestrador).ShouldSatisfyAllConditions(
            c => c.Verificacion.ShouldBe("Verificado"), c => c.Medio.ShouldBe("AprobadoPorContacto"), c => c.Desplazado.ShouldBeFalse());
        (await VinculoDelAislamientoAsync(a, vinculoA.Id)).AccesoDelCorreo!.Estado.ShouldBe("Aprobado");
        (await DocumentoDeBovedaAsync(rutA.Version.DocumentoId))!.Versiones.Single(v => v.Numero == 1).EnRevisionDesde.ShouldBeNull();
        var historialVinculo = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculoA.Id}/historial", Admin(a), null, 200);
        historialVinculo.Select(h => h.Tipo).ShouldContain("acceso_aislado_solicitado");
        historialVinculo.Select(h => h.Tipo).ShouldContain("acceso_aislado_aprobado");
    }

    // ---------- 2. la cola de plataforma ----------

    [Fact]
    public async Task AV_La_cola_de_plataforma_la_decide_un_operador_con_auditoria_y_solo_cuando_el_tercero_no_tiene_contactos_verificados()
    {
        var a = await EmpresaConFlujoAsync("Primera S.A.S.", "PRIM");
        var b = await EmpresaConFlujoAsync("Segunda S.A.S.", "SEGU");
        var c = await EmpresaConFlujoAsync("Tercera S.A.S.", "TERC");
        var (primero, extrano, otro, nit) = (CorreoNuevo(), CorreoNuevo(), CorreoNuevo(), Nit());

        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", primero, nit);
        var terceroId = vinculoA.TerceroId;
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", extrano, nit);
        var sesionExtrano = await SesionAsync(extrano, "Segunda");
        sesionExtrano.Aislada.ShouldBeTrue();
        var solicitud = (await SolicitudesDePlataformaAsync()).Single(s => s.TerceroId == terceroId);
        (solicitud.Correo, solicitud.EnCola).ShouldBe((extrano, true));
        (solicitud.Tercero, solicitud.Identificacion).ShouldBe(($"Tercero {nit}", $"NIT {nit}"));

        // Nada de lo que no existe, ni un estado raro.
        await ApiAsync("POST", $"/api/v1/plataforma/solicitudes-de-acceso/{Guid.NewGuid()}/aprobar", Plataforma(), new { }, 404);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/plataforma/solicitudes-de-acceso?estado=cualquiera", Plataforma(), null, 422))).ShouldBe("estado-invalido");
        await ApiAsync("GET", "/api/v1/plataforma/solicitudes-de-acceso", Admin(a), null, 403); // solo la consola de Plataforma

        // El operador rechaza con motivo: se corta la sesión, el vínculo ve el rechazo y queda en la auditoría y en el historial de la solicitud.
        var rechazada = await ApiAsync<SolicitudDeAccesoDePlataformaDto>("POST", $"/api/v1/plataforma/solicitudes-de-acceso/{solicitud.Id}/rechazar", Plataforma(),
            new RechazarAccesoDto("El NIT no es de esa persona"), 200);
        (rechazada.Estado, rechazada.DecididoVia, rechazada.Motivo, rechazada.EnCola).ShouldBe(("Rechazada", "Plataforma", "El NIT no es de esa persona", false));
        rechazada.DecididoPor.ShouldStartWith("plataforma:");
        await TerceroAsync("GET", "/public/v1/vinculos", sesionExtrano.AccessToken, null, 401);
        (await VinculoDelAislamientoAsync(b, vinculoB.Id)).AccesoDelCorreo!.Estado.ShouldBe("Rechazado");
        (await SolicitudesDePlataformaAsync()).ShouldNotContain(s => s.Id == solicitud.Id);
        (await SolicitudesDePlataformaAsync("resueltas")).ShouldContain(s => s.Id == solicitud.Id);
        var auditoria = (await AuditoriaDeAsync("SolicitudDeAccesoATercero", solicitud.Id.ToString())).ShouldHaveSingleItem();
        auditoria.Accion.ShouldBe(AccionConfiguracion.Modificado);
        await using (var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
            (await boveda.Events.FetchStreamAsync(solicitud.Id)).Select(x => x.Data.GetType()).ShouldBe(
                [typeof(SolicitudDeAccesoCreada), typeof(AccesoSolicitadoPorEmpresa), typeof(AccesoDeCorreoRechazado)]);

        // Si el operador verifica un contacto, la decisión vuelve a los contactos: el operador ya no decide (409) y el contacto verificado sí.
        var verificado = await VerificarContactoDePlataformaAsync(terceroId, primero);
        (verificado.Verificacion, verificado.Medio).ShouldBe(("Verificado", "Plataforma"));
        (await VerificarContactoDePlataformaAsync(terceroId, primero)).Verificacion.ShouldBe("Verificado"); // idempotente
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/plataforma/terceros/{terceroId}/contactos/verificar", Plataforma(), new VerificarContactoDto("nadie@prueba.co"), 404)))
            .ShouldBe("contacto-no-encontrado");
        var vinculoC = await CrearVinculoAsync(c, "CLIENTE", otro, nit);
        vinculoC.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        var deContacto = (await SolicitudesDePlataformaAsync("pendientes")).Single(s => s.TerceroId == terceroId);
        deContacto.EnCola.ShouldBeFalse();
        (await SolicitudesDePlataformaAsync()).ShouldNotContain(s => s.Id == deContacto.Id);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/plataforma/solicitudes-de-acceso/{deContacto.Id}/aprobar", Plataforma(), new { }, 409))).ShouldBe("decide-un-contacto");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/plataforma/solicitudes-de-acceso/{deContacto.Id}/rechazar", Plataforma(), new RechazarAccesoDto(), 409))).ShouldBe("decide-un-contacto");
        var sesionContacto = await SesionAsync(primero, "Primera");
        var aprobada = await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{deContacto.Id}/aprobar", sesionContacto.AccessToken, null, 200);
        (aprobada.Estado, aprobada.DecididoPor).ShouldBe(("Aprobada", primero));
        (await TerceroDeBovedaAsync(terceroId)).Contactos.Single(x => x.Correo == otro).Medio.ShouldBe(MedioDeVerificacion.AprobadoPorContacto);
    }

    // ---------- 3. corroboración ----------

    [Fact]
    public async Task AV_Dos_grupos_que_invitan_por_su_cuenta_el_mismo_correo_lo_verifican_y_desplazan_al_primer_contacto()
    {
        var a = await EmpresaConFlujoAsync("Primera S.A.S.", "PRIM");
        var b = await EmpresaConFlujoAsync("Segunda S.A.S.", "SEGU");
        var c = await EmpresaConFlujoAsync("Tercera S.A.S.", "TERC");
        var (primero, comun, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());

        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", primero, nit);
        var terceroId = vinculoA.TerceroId;
        await AceptarAsync(await SesionAsync(primero, "Primera"), vinculoA.Id);

        // Un grupo solo no basta: el correo queda aislado esperando a la plataforma.
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", comun, nit);
        vinculoB.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await SolicitudesDePlataformaAsync()).ShouldContain(s => s.TerceroId == terceroId && s.Correo == comun);

        // Otro grupo distinto invita el mismo correo por su cuenta: queda verificado por corroboración y entra completo.
        var vinculoC = await CrearVinculoAsync(c, "CLIENTE", comun, nit);
        vinculoC.AccesoDelCorreo.ShouldBeNull();
        (await VinculoDelAislamientoAsync(c, vinculoC.Id)).VerificacionDelCorreo!.ShouldBe(new VerificacionDelCorreoDto("Verificado", "Corroborado"));
        await SincronizarAsync(terceroId);

        // La solicitud de B ya no espera a nadie: se aprobó sola con esa verificación; y el primer contacto (sin verificar) quedó desplazado.
        (await VinculoDelAislamientoAsync(b, vinculoB.Id)).AccesoDelCorreo!.Estado.ShouldBe("Aprobado");
        (await SolicitudesDePlataformaAsync()).ShouldNotContain(s => s.TerceroId == terceroId);
        var resuelta = (await SolicitudesDePlataformaAsync("resueltas")).Single(s => s.TerceroId == terceroId && s.Correo == comun);
        (resuelta.Estado, resuelta.DecididoVia, resuelta.DecididoPor).ShouldBe(("Aprobada", "Verificacion", "verificacion"));
        var contactos = (await TerceroDeBovedaAsync(terceroId)).Contactos;
        (contactos.Single(x => x.Correo == comun).Verificacion, contactos.Single(x => x.Correo == comun).Medio).ShouldBe((EstadoDeVerificacion.Verificado, MedioDeVerificacion.Corroborado));
        AccesoDelTercero.EstaDesplazado(await TerceroDeBovedaAsync(terceroId), primero).ShouldBeTrue();
        (await VinculoDelAislamientoAsync(a, vinculoA.Id)).AccesoDelCorreo!.Motivo.ShouldBe("ContactoDesplazado");

        // La corroboración no desplaza a un contacto ya verificado: un tercer grupo que invita a otro correo ve a su solicitud esperando al verificado.
        var d = await EmpresaConFlujoAsync("Cuarta S.A.S.", "CUAR");
        var e = await EmpresaConFlujoAsync("Quinta S.A.S.", "QUIN");
        var otro = CorreoNuevo();
        (await CrearVinculoAsync(d, "CLIENTE", otro, nit)).AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await CrearVinculoAsync(e, "CLIENTE", otro, nit)).AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await TerceroDeBovedaAsync(terceroId)).BuscarContacto(otro).ShouldBeNull();
    }

    // ---------- 4. correo del RUT ----------

    [Fact]
    public async Task AV_El_correo_del_RUT_verifica_al_contacto_que_lo_trae_y_a_quien_lo_invita_y_el_verificado_prevalece()
    {
        var a = await EmpresaConFlujoAsync("Secuestradora S.A.S.", "SECU");
        var b = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        await PlanIaAsync(a, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: LlaveIa()));
        var (secuestrador, legitimo, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());

        // A sube a la bóveda un RUT cuyo correo (el que lee la IA) es el del dueño del NIT; el suyo, que lo registró, sigue sin verificar.
        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", secuestrador, nit);
        var sesionA = await SesionAsync(secuestrador, "Secuestradora");
        var rut = await SubirAsync(sesionA, "RUT", Pdf($"RUT [[correo={legitimo}]]"));
        await AceptarAsync(sesionA, vinculoA.Id);
        await EsperarVersionAsync(a, vinculoA.Id, "RUT", 1, v => v.Hechos!.ConIa);
        await AsentarIaAsync(rut.Version.DocumentoId, 1);
        (await ContactosAsync(sesionA)).Contactos.Single().Verificacion.ShouldBe("SinVerificar");

        // B invita al correo del RUT: coincide con el documento, queda verificado (correo del RUT) y entra completo, sin esperar a nadie.
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", legitimo, nit);
        vinculoB.AccesoDelCorreo.ShouldBeNull();
        (await VinculoDelAislamientoAsync(b, vinculoB.Id)).VerificacionDelCorreo!.ShouldBe(new VerificacionDelCorreoDto("Verificado", "CorreoDelRut"));
        var sesionB = await SesionAsync(legitimo, "Norte");
        sesionB.Aislada.ShouldBeFalse();
        await SincronizarAsync(vinculoA.TerceroId);

        // El verificado prevalece: el correo de A pasó a aislado y no puede decidir.
        var contactos = await ContactosAsync(sesionB);
        contactos.Contactos.Single(c => c.Correo == legitimo).Medio.ShouldBe("CorreoDelRut");
        contactos.Contactos.Single(c => c.Correo == secuestrador).Desplazado.ShouldBeTrue();
        (await VinculoDelAislamientoAsync(a, vinculoA.Id)).AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await SolicitudesDePlataformaAsync("pendientes")).Single(s => s.TerceroId == vinculoA.TerceroId).CorreosOficiales
            .ShouldHaveSingleItem().ShouldBe(new CorreoOficialDto(legitimo, "RUT", 1));

        // El hecho nuevo del análisis también verifica a un contacto que ya estaba: el que sube su RUT con su propio correo se verifica solo (la IA avisa a la sincronización).
        var propio = CorreoNuevo();
        var nit2 = Nit();
        var vinculo2 = await CrearVinculoAsync(a, "CLIENTE", propio, nit2);
        var sesion2 = await SesionAsync(propio, "Secuestradora");
        var rut2 = await SubirAsync(sesion2, "RUT", Pdf($"RUT [[correo={propio}]]"));
        await AceptarAsync(sesion2, vinculo2.Id);
        await EsperarVersionAsync(a, vinculo2.Id, "RUT", 1, v => v.Hechos!.ConIa);
        await AsentarIaAsync(rut2.Version.DocumentoId, 1);
        await EsperarAsync(async () => (await ContactosAsync(sesion2)).Contactos.Single().Verificacion == "Verificado", "la verificación por el correo del RUT");
        (await ContactosAsync(sesion2)).Contactos.Single().Medio.ShouldBe("CorreoDelRut");
        (await ContactosAsync(sesion2)).PuedesDecidir.ShouldBeTrue();
    }

    // ---------- 2 (importación). contactos del Portal ----------

    [Fact]
    public async Task AV_El_correo_que_trae_el_Portal_es_contacto_verificado_por_importacion_y_un_tercero_sin_correo_deja_sus_solicitudes_en_la_cola()
    {
        var norte = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        var otra = await EmpresaConFlujoAsync("Otra S.A.S.", "OTRA");
        var f = NuevoFixturePortal();
        // Norte ya tenía al jurídico (su correo es un contacto sin verificar); la importación lo encuentra por su NIT.
        var correoDeNorte = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correoDeNorte, f.NitJ);

        // La importación crea las especialidades del Portal en el catálogo global: otra prueba cuenta cuántas crearía y espera que todavía no existan. Se quitan al terminar.
        var especialidadesAntes = await EspecialidadesAsync();
        try
        {
            await ProbarContactosImportadosAsync(norte, otra, f, correoDeNorte, vinculo);
        }
        finally
        {
            await using var limpieza = Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
            foreach (var id in (await EspecialidadesAsync()).Except(especialidadesAntes))
                limpieza.Delete<EntradaDeCatalogo>(id);
            await limpieza.SaveChangesAsync();
        }
    }

    private async Task<IReadOnlyList<string>> EspecialidadesAsync()
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return [.. (await consulta.Query<EntradaDeCatalogo>().Where(e => e.Catalogo == CatalogoDePlataforma.Especialidades).ToListAsync()).Select(e => e.Id)];
    }

    private async Task ProbarContactosImportadosAsync(Empresa norte, Empresa otra, FixturePortal f, string correoDeNorte, VinculoDto vinculo)
    {
        await ImportarPortalAsync(f.Json(), dryRun: false);

        var natural = (await CargarAsync<Tercero>(IdDeLegado.Tercero("portal", f.TeridN.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ShouldNotBeNull();
        var contactoNatural = natural.Contactos.ShouldHaveSingleItem();
        (contactoNatural.Correo, contactoNatural.Verificacion, contactoNatural.Medio).ShouldBe(("maria@correo.co", EstadoDeVerificacion.Verificado, MedioDeVerificacion.Importacion));

        // El jurídico suma el correo del origen como verificado y el de Norte (sin verificar) queda desplazado al sincronizar.
        await SincronizarAsync(vinculo.TerceroId);
        var juridico = await TerceroDeBovedaAsync(vinculo.TerceroId);
        (juridico.BuscarContacto(f.CorreoJ)!.Medio).ShouldBe(MedioDeVerificacion.Importacion);
        AccesoDelTercero.EstaDesplazado(juridico, correoDeNorte).ShouldBeTrue();
        (await VinculoDelAislamientoAsync(norte, vinculo.Id)).AccesoDelCorreo!.Estado.ShouldBe("Pendiente");

        // Importar otra vez no repite nada (idempotente).
        await ImportarPortalAsync(f.Json(), dryRun: false);
        (await TerceroDeBovedaAsync(vinculo.TerceroId)).Contactos.Count.ShouldBe(2);

        // El tercero del Portal cuyo correo no sirve se importó sin contactos: nadie lo decide, así que su solicitud va a la cola de la plataforma (no queda aislado para siempre).
        var sinCorreo = await TerceroDeBovedaAsync((await CargarAsync<ReferenciaDeTercero>($"PORTAL|{f.TeridE}", TenantsTecnicos.Boveda)).ShouldNotBeNull().TerceroId);
        sinCorreo.Contactos.ShouldBeEmpty();
        var vinculoSinCorreo = await CrearVinculoAsync(otra, "CLIENTE", CorreoNuevo(), f.NitE);
        vinculoSinCorreo.TerceroId.ShouldBe(sinCorreo.Id);
        vinculoSinCorreo.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        var enCola = (await SolicitudesDePlataformaAsync()).Single(s => s.TerceroId == sinCorreo.Id);
        enCola.EnCola.ShouldBeTrue();
        enCola.Contactos.ShouldBeEmpty();
    }

    // ---------- 3. token embebido de un expediente y las cuentas ----------

    private static RegistrarCuentaBancariaDto CuentaNueva(string banco, string numero) => new(banco, "Ahorros", numero, "Aceros del Norte S.A.S.", "NIT 900123456");

    [Fact]
    public async Task AV_El_token_embebido_de_un_expediente_solo_ve_y_designa_la_cuenta_de_su_vinculo_y_nunca_las_demas_del_tercero()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await ExigirCuentaDelProveedorAsync(norte.Admin, true);
        await FlujoConAsync(norte, "PROVEEDOR", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var erp = await SistemaNuevoConOrigenesAsync(norte, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "PROVEEDOR", correo);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        var sesion = await SesionAsync(correo, "Norte"); // PROVEEDOR exige OTP: SesionAsync lo verifica con el código que llega al mismo correo
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación"));
        // Dos cuentas del tercero registradas desde su cuenta (no desde el expediente).
        var propia1 = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, CuentaNueva("Bancolombia", "031-123456-78"), 201);
        var propia2 = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, CuentaNueva("Davivienda", "4700 1234 9999"), 201);

        var token = (await EmbebidaAsync(erp, vinculo.Id, "expediente")).Token;

        // El expediente exige la cuenta: puede ver las de su vínculo —ninguna todavía— pero no lista las demás del tercero ni designa una que no registró para él.
        (await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", token, null, 200)).ShouldBeEmpty();
        var designar = $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada";
        foreach (var ajena in new[] { propia1, propia2 })
            (await CodigoAsync(await TerceroAsync("POST", designar, token, new DesignarCuentaDto(ajena.Id), 403))).ShouldBe("alcance-de-sesion");

        // Sí puede registrar la cuenta que pide el expediente y designarla; después solo ve esa.
        var delExpediente = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", token, CuentaNueva("Bogotá", "1111 2222 3333"), 201);
        var propuesta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, token, new DesignarCuentaDto(delExpediente.Id), 200);
        propuesta.DesignadaEn.ShouldHaveSingleItem().Estado.ShouldBe("Propuesta");
        var vistas = await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", token, null, 200);
        vistas.ShouldHaveSingleItem().Id.ShouldBe(delExpediente.Id);
        vistas.ShouldNotContain(c => c.Id == propia1.Id || c.Id == propia2.Id);
        // No retira cuentas (ni las ajenas ni la propia): eso es de la cuenta del tercero.
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/cuentas-bancarias/{propia1.Id}/retirar", token, new { }, 403))).ShouldBe("alcance-de-sesion");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/cuentas-bancarias/{delExpediente.Id}/retirar", token, new { }, 403))).ShouldBe("alcance-de-sesion");
        // Una cuenta ya designada o propuesta con su vínculo vuelve a estar a su alcance aunque la haya registrado la cuenta del tercero.
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, sesion.AccessToken, new DesignarCuentaDto(propia1.Id), 200);
        (await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", token, null, 200)).Select(c => c.Id).Order()
            .ShouldBe(new[] { delExpediente.Id, propia1.Id }.Order());

        // Las demás sesiones no se limitan: la de la cuenta del tercero y la del componente de la bóveda ven todas.
        (await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, null, 200)).Count.ShouldBe(3);
        var deLaBoveda = (await EmbebidaAsync(erp, vinculo.Id, "boveda")).Token;
        (await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", deLaBoveda, null, 200)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task AV_El_token_embebido_de_un_expediente_que_no_pide_cuenta_no_toca_ninguna_cuenta_del_tercero()
    {
        var norte = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        var erp = await SistemaNuevoConOrigenesAsync(norte, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        var sesion = await SesionAsync(correo, "Norte");
        await AceptarAsync(sesion, vinculo.Id);
        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, CuentaNueva("Bancolombia", "031-123456-78"), 201);
        var token = (await EmbebidaAsync(erp, vinculo.Id, "expediente")).Token;

        (await CodigoAsync(await TerceroAsync("GET", "/public/v1/boveda/cuentas-bancarias", token, null, 403))).ShouldBe("alcance-de-sesion");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", token, CuentaNueva("Davivienda", "4700 1234 9999"), 403))).ShouldBe("alcance-de-sesion");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada", token, new DesignarCuentaDto(cuenta.Id), 403))).ShouldBe("alcance-de-sesion");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/cuentas-bancarias/{cuenta.Id}/retirar", token, new { }, 403))).ShouldBe("alcance-de-sesion");
        // La cuenta del tercero sigue intacta y su sesión sí la ve.
        (await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, null, 200)).ShouldHaveSingleItem().Estado.ShouldBe("Activa");
    }

    // ---------- 4. migración de los vínculos aceptados antes del acceso aislado ----------

    // Así se arma el estado de antes del acceso aislado: el correo es contacto al invitar (el vínculo nace sin acceso aislado y se acepta) y después se quita de los contactos, como
    // quedaron los vínculos aceptados cuyo correo nunca fue contacto del tercero.

    private async Task AgregarContactoDirectoAsync(Guid terceroId, string correo)
    {
        await using var sesion = Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var tercero = (await sesion.LoadAsync<Tercero>(terceroId)).ShouldNotBeNull();
        tercero.AgregarContacto(correo, null, "Contacto").ShouldBeTrue();
        sesion.Store(tercero);
        await sesion.SaveChangesAsync();
    }

    private async Task QuitarContactosAsync(Guid terceroId, params string[] correos)
    {
        await using var sesion = Host.Services.GetRequiredService<IDocumentStore>().LightweightSession();
        var tercero = (await sesion.LoadAsync<Tercero>(terceroId)).ShouldNotBeNull();
        tercero.Contactos = [.. tercero.Contactos.Where(c => !correos.Contains(c.Correo))];
        sesion.Store(tercero);
        await sesion.SaveChangesAsync();
    }

    private async Task<ResultadoDeMigracionDeAcceso> MigrarAsync()
    {
        using var alcance = Host.Services.CreateScope();
        var p = alcance.ServiceProvider;
        return await MigracionDeAccesoAislado.EjecutarAsync(p.GetRequiredService<ISesiones>(), p.GetRequiredService<Wolverine.Runtime.IWolverineRuntime>(),
            p.GetRequiredService<IProtectorDeSecretos>(), p.GetRequiredService<ICacheDeSesionesDeTercero>(), p.GetRequiredService<IAlmacenDeArchivos>(),
            p.GetRequiredService<ReglasDeCarga>(), p.GetRequiredService<IContextoSolicitud>(), DateTimeOffset.UtcNow, CancellationToken.None);
    }

    [Fact]
    public async Task AV_La_migracion_convierte_en_contacto_el_correo_legitimo_de_un_vinculo_aceptado_y_deja_el_ajeno_en_acceso_aislado_pendiente_una_sola_vez()
    {
        var norte = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        var intrusa = await EmpresaConFlujoAsync("Intrusa S.A.S.", "INTRUSA");
        var (primero, intruso, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());

        // Un tercero de Norte con su primer contacto y el vínculo de una empresa ajena con otro correo, aceptado (así quedó antes del acceso aislado).
        var vinculoNorte = await CrearVinculoAsync(norte, "CLIENTE", primero, nit);
        await AceptarAsync(await SesionAsync(primero, "Norte"), vinculoNorte.Id);
        await AgregarContactoDirectoAsync(vinculoNorte.TerceroId, intruso);
        var vinculoIntrusa = await CrearVinculoAsync(intrusa, "CLIENTE", intruso, nit);
        vinculoIntrusa.AccesoDelCorreo.ShouldBeNull();
        await AceptarAsync(await SesionAsync(intruso, "Intrusa"), vinculoIntrusa.Id);
        await QuitarContactosAsync(vinculoNorte.TerceroId, intruso);
        (await VinculoDelAislamientoAsync(intrusa, vinculoIntrusa.Id)).AccesoDelCorreo.ShouldBeNull();

        // Otro tercero cuyo vínculo aceptado trae un correo que no figura como contacto: es el primer contacto real (el tercero no tiene ninguno y es el del grupo que lo creó).
        var (propio, nit2) = (CorreoNuevo(), Nit());
        var vinculoPropio = await CrearVinculoAsync(norte, "CLIENTE", propio, nit2);
        await AceptarAsync(await SesionAsync(propio, "Norte"), vinculoPropio.Id);
        await QuitarContactosAsync(vinculoPropio.TerceroId, propio);
        (await TerceroDeBovedaAsync(vinculoPropio.TerceroId)).Contactos.ShouldBeEmpty();

        var resultado = await MigrarAsync();
        resultado.TercerosRevisados.ShouldBeGreaterThanOrEqualTo(2);
        resultado.ContactosSinVerificar.ShouldBeGreaterThanOrEqualTo(1);
        resultado.VinculosAislados.ShouldBeGreaterThanOrEqualTo(1);

        // El primer contacto real quedó como contacto (sin verificar) y su vínculo no cambió.
        var contactoPropio = (await TerceroDeBovedaAsync(vinculoPropio.TerceroId)).Contactos.ShouldHaveSingleItem();
        (contactoPropio.Correo, contactoPropio.Verificacion).ShouldBe((propio, EstadoDeVerificacion.SinVerificar));
        (await VinculoDelAislamientoAsync(norte, vinculoPropio.Id)).AccesoDelCorreo.ShouldBeNull();

        // El correo ajeno ya no se reconoce solo: su vínculo quedó en acceso aislado pendiente, su solicitud espera en la cola y el primer contacto no lo decide.
        var aislado = await VinculoDelAislamientoAsync(intrusa, vinculoIntrusa.Id);
        aislado.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        (await TerceroDeBovedaAsync(vinculoIntrusa.TerceroId)).BuscarContacto(intruso).ShouldBeNull();
        var enCola = (await SolicitudesDePlataformaAsync()).Single(s => s.TerceroId == vinculoIntrusa.TerceroId);
        (enCola.Correo, enCola.EnCola).ShouldBe((intruso, true));
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(intruso))).Context.Response.StatusCode.ShouldBe(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted);
        (await SesionPorAvisoAsync(intruso, m => m.Asunto.Contains("para entrar", StringComparison.Ordinal))).Aislada.ShouldBeTrue();

        // Repetirla no cambia nada.
        var otra = await MigrarAsync();
        otra.VinculosAislados.ShouldBe(0);
        otra.ContactosSinVerificar.ShouldBe(0);
        otra.ContactosVerificados.ShouldBe(0);
        (await TerceroDeBovedaAsync(vinculoPropio.TerceroId)).Contactos.Count.ShouldBe(1);
        (await SolicitudesDePlataformaAsync()).Count(s => s.TerceroId == vinculoIntrusa.TerceroId).ShouldBe(1);
    }

    [Fact]
    public async Task AV_La_migracion_verifica_el_correo_que_dos_grupos_tenian_aceptado_y_los_contactos_con_con_que_hacerlo()
    {
        var norte = await EmpresaConFlujoAsync("Norte S.A.S.", "NORTE");
        var sur = await EmpresaConFlujoAsync("Sur S.A.S.", "SUR");
        var oriente = await EmpresaConFlujoAsync("Oriente S.A.S.", "ORIENTE");
        var (primero, comun, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());

        var vinculoNorte = await CrearVinculoAsync(norte, "CLIENTE", primero, nit);
        await AceptarAsync(await SesionAsync(primero, "Norte"), vinculoNorte.Id);
        await AgregarContactoDirectoAsync(vinculoNorte.TerceroId, comun);
        var deSur = await CrearVinculoAsync(sur, "CLIENTE", comun, nit);
        var deOriente = await CrearVinculoAsync(oriente, "CLIENTE", comun, nit);
        await AceptarAsync(await SesionAsync(comun, "Sur"), deSur.Id);
        await AceptarAsync(await SesionAsync(comun, "Oriente"), deOriente.Id);
        // Antes del acceso aislado el correo común no era contacto del tercero y los dos vínculos estaban aceptados (y la corroboración todavía no existía).
        await QuitarContactosAsync(vinculoNorte.TerceroId, comun);
        (await TerceroDeBovedaAsync(vinculoNorte.TerceroId)).Contactos.Select(c => c.Correo).ShouldBe([primero]);

        var resultado = await MigrarAsync();

        // Dos grupos distintos lo habían invitado por su cuenta: queda como contacto verificado por corroboración y desplaza al primer contacto, que pasa a aislado.
        resultado.ContactosVerificados.ShouldBeGreaterThanOrEqualTo(1);
        var tercero = await TerceroDeBovedaAsync(vinculoNorte.TerceroId);
        (tercero.BuscarContacto(comun)!.Verificacion, tercero.BuscarContacto(comun)!.Medio).ShouldBe((EstadoDeVerificacion.Verificado, MedioDeVerificacion.Corroborado));
        AccesoDelTercero.EstaDesplazado(tercero, primero).ShouldBeTrue();
        (await VinculoDelAislamientoAsync(sur, deSur.Id)).AccesoDelCorreo.ShouldBeNull();
        (await VinculoDelAislamientoAsync(oriente, deOriente.Id)).AccesoDelCorreo.ShouldBeNull();
        (await VinculoDelAislamientoAsync(norte, vinculoNorte.Id)).AccesoDelCorreo!.Motivo.ShouldBe("ContactoDesplazado");
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(comun))).Context.Response.StatusCode.ShouldBe(Microsoft.AspNetCore.Http.StatusCodes.Status202Accepted);
        var sesionComun = await SesionPorAvisoAsync(comun, m => m.Asunto.Contains("para entrar", StringComparison.Ordinal));
        sesionComun.Aislada.ShouldBeFalse();
        var historial = await ContactosAsync(sesionComun);
        historial.Historial.ShouldContain(h => h.Tipo == "verificado" && h.Correo == comun);
        historial.Historial.ShouldContain(h => h.Tipo == "paso_a_aislado" && h.Correo == primero);
    }
}
