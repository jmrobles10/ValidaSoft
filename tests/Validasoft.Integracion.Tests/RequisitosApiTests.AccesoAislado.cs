using System.Net;
using Alba;
using System.Net.Http.Headers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Acceso aislado (decisión AA): una empresa que crea un vínculo con el NIT de un tercero ajeno y pone su propio correo ya no entra a la bóveda de ese tercero. Entra
/// aislada (ve y sube solo lo de su relación, en cuarentena), los contactos del tercero reciben el aviso y lo aprueban o rechazan; el token embebido de un expediente
/// ya no sirve para toda la bóveda y cada sesión del tercero se puede cerrar por separado.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private sealed record Aislamiento(Empresa Norte, Empresa Intrusa, VinculoDto VinculoNorte, VinculoDto VinculoIntrusa, string Contacto, string Intruso,
        SesionTerceroDto SesionContacto, SesionTerceroDto SesionIntruso, Guid RutId, byte[] Rut);

    /// <summary>
    /// Norte registró a un tercero (su contacto sube el RUT a la bóveda y acepta la invitación de Norte); Intrusa, otra empresa de otro grupo, crea un vínculo
    /// con el mismo NIT y su propio correo (el del atacante) y abre su enlace.
    /// </summary>
    private async Task<Aislamiento> AislamientoAsync()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var intrusa = await EmpresaNuevaAsync("Intrusa S.A.S.", "INTRUSA");
        foreach (var empresa in new[] { norte, intrusa })
            await FlujoConAsync(empresa, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);

        var (contacto, intruso, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());
        var vinculoNorte = await CrearVinculoAsync(norte, "CLIENTE", contacto, nit);
        vinculoNorte.AccesoDelCorreo.ShouldBeNull();
        var sesionContacto = await SesionAsync(contacto, "Norte");
        sesionContacto.Aislada.ShouldBeFalse();

        var rut = Pdf("RUT del tercero");
        var subido = await SubirAsync(sesionContacto, "RUT", rut);
        await AceptarAsync(sesionContacto, vinculoNorte.Id);

        // El atacante: otra empresa, el mismo NIT, su propio correo.
        var vinculoIntrusa = await CrearVinculoAsync(intrusa, "CLIENTE", intruso, nit);
        vinculoIntrusa.TerceroId.ShouldBe(vinculoNorte.TerceroId);
        var sesionIntruso = await SesionAsync(intruso, "Intrusa");
        return new Aislamiento(norte, intrusa, vinculoNorte, vinculoIntrusa, contacto, intruso, sesionContacto, sesionIntruso, subido.Version.DocumentoId, rut);
    }

    /// <summary>Paso 1, PUT directo a MinIO y paso 2 de una carga hecha para un vínculo (la subida de un documento del que pide la empresa).</summary>
    private async Task<IScenarioResult> SubirParaVinculoAsync(string token, string canonico, Guid? vinculoId, byte[] contenido, int esperado = 200)
    {
        var carga = await TerceroAsync<CargaPrefirmadaDto>("POST", "/public/v1/boveda/documentos", token,
            new IniciarCargaDto(canonico.ToLowerInvariant() + ".pdf", contenido.Length, "application/pdf", TipoCanonico: canonico, VinculoId: vinculoId), 201);
        using var put = new HttpRequestMessage(HttpMethod.Put, carga.Carga!.Url) { Content = new ByteArrayContent(contenido) };
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(carga.Carga.Cabeceras["Content-Type"]);
        using var respuesta = await Http.SendAsync(put);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await TerceroAsync("POST", $"/public/v1/boveda/documentos/{carga.DocumentoId}/versiones/{carga.Version}/confirmar", token, null, esperado);
    }

    private async Task<ConfirmacionCargaDto> SubirParaVinculoComoAsync(string token, string canonico, Guid vinculoId, byte[] contenido) =>
        (await (await SubirParaVinculoAsync(token, canonico, vinculoId, contenido)).ReadAsJsonAsync<ConfirmacionCargaDto>())!;

    private async Task<List<DocumentoVistaEmpresaDto>> DocumentosDeAsync(Empresa empresa, Guid vinculoId) =>
        await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{vinculoId}/documentos", Admin(empresa), null, 200);

    private Task<VinculoDto> VinculoDelAislamientoAsync(Empresa empresa, Guid vinculoId) => ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}", Admin(empresa), null, 200);

    private async Task<SesionTerceroDto> SesionPorAvisoAsync(string correo, Func<MensajeDeCorreo, bool> condicion)
    {
        var aviso = await fixture.Correos.EsperarAsync(correo, condicion);
        var canje = await PublicoAsync($"/public/v1/enlaces/{RegexToken().Match(aviso.Texto).Groups[1].Value}/canjear", null);
        canje.Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK, await canje.ReadAsTextAsync());
        return (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
    }

    /// <summary>El enlace de un correo cuyo vínculo exige OTP (proveedor): se canjea y se verifica con el código más reciente que llega al mismo correo.</summary>
    private async Task<SesionTerceroDto> SesionPorEnlaceConOtpAsync(string correo, Func<MensajeDeCorreo, bool> condicion)
    {
        var codigosAntes = fixture.Correos.Para(correo).Count(m => m.Asunto.Contains("código", StringComparison.Ordinal));
        var enlace = await fixture.Correos.EsperarAsync(correo, condicion);
        var token = RegexToken().Match(enlace.Texto).Groups[1].Value;
        var canje = await PublicoAsync($"/public/v1/enlaces/{token}/canjear", null);
        if (canje.Context.Response.StatusCode == StatusCodes.Status200OK)
            return (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;

        canje.Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        var codigo = RegexCodigo().Match((await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("código", StringComparison.Ordinal), codigosAntes + 1)).Texto).Groups[1].Value;
        var verificado = await PublicoAsync($"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo));
        verificado.Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        return (await verificado.ReadAsJsonAsync<SesionTerceroDto>())!;
    }

    private static bool EsAvisoDeAcceso(MensajeDeCorreo m) => m.Asunto.Contains("invitó a", StringComparison.Ordinal);

    // ---------- el ataque ----------

    [Fact]
    public async Task La_empresa_con_el_NIT_de_un_tercero_ajeno_y_su_propio_correo_entra_aislada_y_no_ve_ni_toca_su_boveda()
    {
        var e = await AislamientoAsync();
        var token = e.SesionIntruso.AccessToken;

        // La sesión nace aislada y solo se le dice el nombre del tercero con las letras tapadas.
        e.SesionIntruso.Aislada.ShouldBeTrue();
        e.SesionIntruso.TerceroEnmascarado.ShouldNotBeNullOrEmpty();
        e.SesionIntruso.TerceroEnmascarado!.ShouldNotContain("ercero");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}", Admin(e.Intrusa), null, 200)).AccesoDelCorreo!.Estado.ShouldBe("Pendiente");

        // La bóveda global, la ficha, las cuentas, los derechos de datos y las sesiones del tercero: 403; un documento global ni se descarga (404) ni se retira (403).
        foreach (var url in new[]
                 {
                     "/public/v1/boveda", $"/public/v1/boveda/documentos/{e.RutId}", $"/public/v1/boveda/documentos/{e.RutId}/versiones/1/retiro/impacto",
                     "/public/v1/boveda/cuentas-bancarias", "/public/v1/ficha", "/public/v1/ficha/propuestas", "/public/v1/exportaciones", "/public/v1/solicitudes-habeas",
                     "/public/v1/preferencias-notificacion", "/public/v1/sesiones", "/public/v1/accesos-solicitados"
                 })
            (await CodigoAsync(await TerceroAsync("GET", url, token, null, 403))).ShouldBe("acceso-aislado", url);
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/documentos/{e.RutId}/versiones/1/retirar", token, new RetirarVersionDto("Lo quiero fuera"), 403)))
            .ShouldBe("acceso-aislado");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/documentos/{e.RutId}/versiones/1/descarga", token, null, 404))).ShouldBe("documento-no-encontrado");

        // Lo suyo sí: sus vínculos y la sesión.
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", token, null, 200)).Select(v => v.Id).ShouldBe([e.VinculoIntrusa.Id]);
        (await TerceroAsync<SesionTerceroDto>("GET", "/public/v1/sesion/actual", token, null, 200)).Aislada.ShouldBeTrue();

        // Acepta la invitación de Intrusa: no se comparte nada de la bóveda del tercero con la empresa y ella no ve de él más que lo que puso.
        var aceptada = await AceptarAsync(e.SesionIntruso, e.VinculoIntrusa.Id);
        aceptada.Compartidos.ShouldBeEmpty();
        (await DocumentosDeAsync(e.Intrusa, e.VinculoIntrusa.Id)).ShouldBeEmpty();
        var vinculo = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        vinculo.InvitacionAceptadaEn.ShouldNotBeNull();
        vinculo.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        vinculo.AccesoDelCorreo.Correo.ShouldBe(e.Intruso);
        (await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}", Admin(e.Intrusa), null, 200)).Contactos.ShouldBeEmpty();
        (await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}", Admin(e.Norte), null, 200)).Contactos.Select(c => c.Correo).ShouldBe([e.Contacto]);
        ((await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(e.Intrusa), null, 200)).Secciones ?? []).ShouldBeEmpty();

        // Subir para su relación: necesita el vínculo (sin él, 422) y lo que sube queda en cuarentena, ligado solo a su vínculo.
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/documentos", token,
            new IniciarCargaDto("rut.pdf", 10, "application/pdf", TipoCanonico: "RUT"), 422))).ShouldBe("vinculo-requerido");
        var intruso = Pdf("RUT falso del intruso");
        var subida = await SubirParaVinculoComoAsync(token, "RUT", e.VinculoIntrusa.Id, intruso);
        subida.Version.Estado.ShouldBe("Actual");
        subida.Version.DocumentoId.ShouldNotBe(e.RutId);
        subida.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(e.VinculoIntrusa.Id);

        // Intrusa ve solo lo suyo (en cuarentena, por vínculo); Norte y la bóveda del tercero no cambian: nada de lo subido se comparte ni es la versión actual global.
        var deIntrusa = (await DocumentosDeAsync(e.Intrusa, e.VinculoIntrusa.Id)).ShouldHaveSingleItem();
        deIntrusa.Ambito.ShouldBe("PorVinculo");
        deIntrusa.Tipo.Codigo.ShouldBe("RUT");
        deIntrusa.VersionActual.ShouldBe(1);
        deIntrusa.VersionesCompartidas.ShouldHaveSingleItem().Sha256.ShouldBe(Sha256(intruso));

        var deNorte = (await DocumentosDeAsync(e.Norte, e.VinculoNorte.Id)).ShouldHaveSingleItem();
        deNorte.DocumentoId.ShouldBe(e.RutId);
        deNorte.VersionActual.ShouldBe(1);
        deNorte.VersionesCompartidas.ShouldHaveSingleItem().Sha256.ShouldBe(Sha256(e.Rut));

        var boveda = await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", e.SesionContacto.AccessToken, null, 200);
        var enBoveda = boveda.Documentos.ShouldHaveSingleItem(); // la cuarentena no se lista en la bóveda del tercero
        enBoveda.Id.ShouldBe(e.RutId);
        enBoveda.Versiones.ShouldBe(1);
        enBoveda.VersionActual!.Sha256.ShouldBe(Sha256(e.Rut));

        // Descarga lo suyo, pero ni siquiera retira lo que subió (eso es de la bóveda del tercero).
        var propia = await TerceroAsync<DescargaDto>("POST", $"/public/v1/boveda/documentos/{subida.Version.DocumentoId}/versiones/1/descarga", token, null, 200);
        (await Http.GetByteArrayAsync(propia.Url)).ShouldBe(intruso);
        await TerceroAsync("POST", $"/public/v1/boveda/documentos/{subida.Version.DocumentoId}/versiones/1/retirar", token, new RetirarVersionDto("x"), 403);

        // El expediente de Intrusa queda cubierto con lo que esa persona subió para ella.
        var expediente = await EsperarAsync(e.Intrusa, vinculo.ExpedienteAbiertoId!.Value, x => Requisito(x, "RUT").VersionActual is not null);
        Requisito(expediente, "RUT").VersionActual!.Sha256.ShouldBe(Sha256(intruso));
    }

    [Fact]
    public async Task Los_contactos_del_tercero_reciben_el_aviso_por_correo_y_campana_y_el_aislado_no_ve_nada_de_eso()
    {
        var e = await AislamientoAsync();

        // Correo al contacto: quién invitó, a qué correo, y el enlace para entrar a decidir. La persona invitada no recibe este aviso.
        var aviso = await fixture.Correos.EsperarAsync(e.Contacto, EsAvisoDeAcceso);
        aviso.Asunto.ShouldContain("Intrusa S.A.S.");
        aviso.Asunto.ShouldContain(e.Intruso);
        aviso.Texto.ShouldContain("acceso aislado");
        aviso.Texto.ShouldContain($"{ApiFixture.UrlPublica}/e/");
        fixture.Correos.Para(e.Intruso).Any(EsAvisoDeAcceso).ShouldBeFalse();

        // El enlace abre su cuenta con acceso completo y ve la solicitud pendiente, con el nombre de la empresa que invitó.
        var sesion = await SesionPorAvisoAsync(e.Contacto, EsAvisoDeAcceso);
        sesion.Aislada.ShouldBeFalse();
        var solicitudes = await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", sesion.AccessToken, null, 200);
        var solicitud = solicitudes.ShouldHaveSingleItem();
        (solicitud.Correo, solicitud.Estado).ShouldBe((e.Intruso, "Pendiente"));
        solicitud.Empresas.ShouldHaveSingleItem().Empresa.ShouldBe("Intrusa S.A.S.");

        // La campana del contacto (la sale el procesador de eventos: se espera) trae el aviso; la de la persona aislada, no.
        var limite = DateTime.UtcNow.AddSeconds(20);
        BandejaNotificacionesTerceroDto bandeja;
        while ((bandeja = await TerceroAsync<BandejaNotificacionesTerceroDto>("GET", "/public/v1/notificaciones", sesion.AccessToken, null, 200))
               .Datos.All(n => n.Tipo != "tercero.acceso_solicitado"))
        {
            DateTime.UtcNow.ShouldBeLessThan(limite, "No llegó el aviso a la campana del contacto.");
            await Task.Delay(150);
        }

        var notificacion = bandeja.Datos.Single(n => n.Tipo == "tercero.acceso_solicitado");
        notificacion.Mensaje.ShouldContain(e.Intruso);
        notificacion.Mensaje.ShouldContain("Intrusa S.A.S.");
        notificacion.Enlace.ShouldBe("/accesos");
        notificacion.VinculoId.ShouldBeNull();
        (await TerceroAsync<BandejaNotificacionesTerceroDto>("GET", "/public/v1/notificaciones", e.SesionIntruso.AccessToken, null, 200))
            .Datos.ShouldNotContain(n => n.Tipo == "tercero.acceso_solicitado");

        // Reenviar la invitación a ese correo no vuelve a avisar a los contactos.
        await ApiAsync<EnlaceInvitacionDto>("POST", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}/invitaciones", Admin(e.Intrusa), new InvitacionDto(null, 48), 201);
        await Task.Delay(500);
        fixture.Correos.Para(e.Contacto).Count(EsAvisoDeAcceso).ShouldBe(1);
    }

    // ---------- aprobar y rechazar ----------

    [Fact]
    public async Task Al_aprobar_el_correo_pasa_a_ser_contacto_la_empresa_recibe_lo_suyo_y_lo_subido_en_cuarentena_sigue_el_camino_normal()
    {
        var e = await AislamientoAsync();
        await AceptarAsync(e.SesionIntruso, e.VinculoIntrusa.Id);
        var intruso = Pdf("RUT que subió la persona aislada");
        await SubirParaVinculoComoAsync(e.SesionIntruso.AccessToken, "RUT", e.VinculoIntrusa.Id, intruso);
        var sesion = await SesionPorAvisoAsync(e.Contacto, EsAvisoDeAcceso);
        var solicitud = (await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", sesion.AccessToken, null, 200)).Single();

        // Nadie decide su propio acceso (la aislada ni siquiera ve las solicitudes), y una solicitud ajena no existe.
        await TerceroAsync("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/aprobar", e.SesionIntruso.AccessToken, null, 403);
        await TerceroAsync("POST", $"/public/v1/accesos-solicitados/{Guid.NewGuid()}/aprobar", sesion.AccessToken, null, 404);

        var aprobada = await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/aprobar", sesion.AccessToken, null, 200);
        (aprobada.Estado, aprobada.DecididoPor).ShouldBe(("Aprobada", e.Contacto));
        aprobada.DecididoEn.ShouldNotBeNull();

        // La empresa ve la aprobación y recibe lo que su flujo pide de la bóveda; lo que la persona subió pasó a ser la versión actual del tercero.
        var vinculo = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        vinculo.AccesoDelCorreo!.Estado.ShouldBe("Aprobado");
        vinculo.AccesoDelCorreo.ResueltoEn.ShouldNotBeNull();
        var global = (await DocumentosDeAsync(e.Intrusa, e.VinculoIntrusa.Id)).Single(d => d.Ambito == "Global");
        global.DocumentoId.ShouldBe(e.RutId);
        global.VersionActual.ShouldBe(2);
        global.VersionesCompartidas.Last().Sha256.ShouldBe(Sha256(intruso));

        var boveda = await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", sesion.AccessToken, null, 200);
        var rut = boveda.Documentos.ShouldHaveSingleItem();
        (rut.Versiones, rut.VersionActual!.N, rut.VersionActual.Sha256).ShouldBe((2, 2, Sha256(intruso)));
        (await DocumentosDeAsync(e.Norte, e.VinculoNorte.Id)).ShouldHaveSingleItem().VersionActual.ShouldBe(2); // Norte recibe la versión nueva, como siempre

        // La cuarentena quedó resuelta (promovida) y repetir la aprobación no cambia nada.
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
        {
            var cuarentena = (await consulta.Query<Documento>().Where(d => d.VinculoId == e.VinculoIntrusa.Id && d.EnCuarentena).ToListAsync()).ShouldHaveSingleItem();
            cuarentena.CuarentenaResueltaEn.ShouldNotBeNull();
            cuarentena.PromovidoAGlobalId.ShouldBe(e.RutId);
        }

        (await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/aprobar", sesion.AccessToken, null, 200)).Estado.ShouldBe("Aprobada");
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", sesion.AccessToken, null, 200)).Documentos.Single().Versiones.ShouldBe(2);
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/rechazar", sesion.AccessToken, new RechazarAccesoDto(), 409))).ShouldBe("solicitud-ya-aprobada");

        // El correo ya es contacto del tercero: al volver a entrar con su correo, la sesión es completa y ve la bóveda.
        var contactos = (await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}", Admin(e.Intrusa), null, 200)).Contactos;
        contactos.Select(c => c.Correo).ShouldContain(e.Intruso);
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(e.Intruso))).Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        var reingreso = await SesionPorAvisoAsync(e.Intruso, m => m.Asunto.Contains("para entrar", StringComparison.Ordinal));
        reingreso.Aislada.ShouldBeFalse();
        reingreso.TerceroEnmascarado.ShouldBeNull();
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", reingreso.AccessToken, null, 200)).Documentos.ShouldHaveSingleItem();

        // Queda en el historial del vínculo (el que ve la empresa) y en el de la solicitud.
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}/historial", Admin(e.Intrusa), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("acceso_aislado_solicitado");
        historial.Select(h => h.Tipo).ShouldContain("acceso_aislado_aprobado");
        await using var boveda2 = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda);
        (await boveda2.Events.FetchStreamAsync(Guid.Parse(solicitud.Id.ToString()))).Select(x => x.Data.GetType()).ShouldBe(
            [typeof(SolicitudDeAccesoCreada), typeof(AccesoSolicitadoPorEmpresa), typeof(AccesoDeCorreoAprobado)]);
    }

    [Fact]
    public async Task Al_rechazar_se_corta_la_sesion_del_correo_la_empresa_ve_el_rechazo_e_invita_a_otro_correo_que_acepta_ella_misma()
    {
        var e = await AislamientoAsync();
        var token = e.SesionIntruso.AccessToken;
        await AceptarAsync(e.SesionIntruso, e.VinculoIntrusa.Id);
        await SubirParaVinculoComoAsync(token, "RUT", e.VinculoIntrusa.Id, Pdf("RUT del intruso"));
        var vinculoAntes = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        var sesion = await SesionPorAvisoAsync(e.Contacto, EsAvisoDeAcceso);
        var solicitud = (await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", sesion.AccessToken, null, 200)).Single();

        var rechazada = await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/rechazar", sesion.AccessToken,
            new RechazarAccesoDto("No conozco a esa persona"), 200);
        (rechazada.Estado, rechazada.DecididoPor, rechazada.Motivo).ShouldBe(("Rechazada", e.Contacto, "No conozco a esa persona"));
        // Repetir el rechazo no cambia nada.
        (await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/rechazar", sesion.AccessToken, new RechazarAccesoDto(), 200))
            .Estado.ShouldBe("Rechazada");

        // La sesión de esa persona deja de servir al instante (su token y su sesión de refresco) y no vuelve a entrar con ese correo.
        await TerceroAsync("GET", "/public/v1/vinculos", token, null, 401);
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(e.Intruso))).Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        await Task.Delay(1000);
        fixture.Correos.Para(e.Intruso).Count(m => m.Asunto.Contains("para entrar", StringComparison.Ordinal)).ShouldBe(0); // sin enlace: el vínculo rechazado no abre sesiones

        // La empresa ve el rechazo (sin el motivo, que es de los contactos), y sigue sin recibir nada de la bóveda del tercero.
        var vinculo = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        vinculo.AccesoDelCorreo!.Estado.ShouldBe("Rechazado");
        vinculo.AccesoDelCorreo.ResueltoEn.ShouldNotBeNull();
        (await DocumentosDeAsync(e.Intrusa, e.VinculoIntrusa.Id)).ShouldHaveSingleItem().Ambito.ShouldBe("PorVinculo");
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", sesion.AccessToken, null, 200)).Documentos.ShouldHaveSingleItem().Versiones.ShouldBe(1);

        // La empresa invita a otro correo (el de un contacto del tercero) aunque ya hubiera aceptado el anterior: empieza de cero y la persona nueva acepta ella misma.
        var reinvitada = await ApiAsync<EnlaceInvitacionDto>("POST", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}/invitaciones", Admin(e.Intrusa),
            new InvitacionDto(e.Contacto, 72), 201);
        reinvitada.CorreoDestino.ShouldBe(e.Contacto);
        var reiniciado = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        reiniciado.CorreoTercero.ShouldBe(e.Contacto);
        reiniciado.InvitacionAceptadaEn.ShouldBeNull();
        reiniciado.AccesoDelCorreo.ShouldBeNull();
        reiniciado.ExpedienteAbiertoId.ShouldBe(vinculoAntes.ExpedienteAbiertoId);

        var contacto = await SesionAsync(e.Contacto, "Intrusa S.A.S. te invita"); // la invitación, no el aviso de acceso (que también nombra a Intrusa)
        contacto.Aislada.ShouldBeFalse();
        var aceptada = await AceptarAsync(contacto, e.VinculoIntrusa.Id);
        aceptada.Compartidos.ShouldContain(c => c.DocumentoId == e.RutId); // ahora sí, con el consentimiento de quien es del tercero
        var final = await VinculoDelAislamientoAsync(e.Intrusa, e.VinculoIntrusa.Id);
        final.ExpedienteAbiertoId.ShouldBe(vinculoAntes.ExpedienteAbiertoId); // no se abrió otro expediente
        (await DocumentosDeAsync(e.Intrusa, e.VinculoIntrusa.Id)).ShouldContain(d => d.DocumentoId == e.RutId);

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}/historial", Admin(e.Intrusa), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("acceso_aislado_rechazado");
    }

    // ---------- quién es el primer contacto ----------

    [Fact]
    public async Task El_primer_correo_de_un_tercero_nuevo_o_sin_historia_se_vuelve_su_contacto_y_entra_completo()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);

        // Un tercero nuevo que crea el vínculo (sin contactos en el alta): su correo pasa a ser contacto, como siempre.
        var (c1, nit) = (CorreoNuevo(), Nit());
        var nuevo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(norte), new CrearVinculoDto(TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null, null), TipoVinculo: "CLIENTE", Atributos: null,
            CorreoInvitacion: c1, ReferenciaExterna: null), 201);
        nuevo.AccesoDelCorreo.ShouldBeNull();
        (await SesionAsync(c1, "Norte")).Aislada.ShouldBeFalse();
        (await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{nuevo.TerceroId}", Admin(norte), null, 200)).Contactos.Select(c => c.Correo).ShouldBe([c1]);

        // Un tercero que la empresa dio de alta antes, sin vínculos ni documentos: igual.
        var nit2 = Nit();
        var registrado = await ApiAsync<TerceroDto>("POST", "/api/v1/terceros", Admin(norte),
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit2), "Juridica", "Tercero " + nit2, null, null), 201);
        var c2 = CorreoNuevo();
        var vinculo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(norte), new CrearVinculoDto(TerceroId: registrado.Id, Tercero: null, TipoVinculo: "CLIENTE",
            Atributos: null, CorreoInvitacion: c2, ReferenciaExterna: null), 201);
        vinculo.AccesoDelCorreo.ShouldBeNull();
        (await SesionAsync(c2, "Norte")).Aislada.ShouldBeFalse();

        // Mientras nadie haya aceptado, un correo equivocado se corrige invitando a otro (sigue sin historia).
        var c3 = CorreoNuevo();
        await ApiAsync<EnlaceInvitacionDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", Admin(norte), new InvitacionDto(c3, 72), 201);
        (await VinculoDelAislamientoAsync(norte, vinculo.Id)).AccesoDelCorreo.ShouldBeNull();
        (await SesionAsync(c3, "Norte")).Aislada.ShouldBeFalse();
    }

    [Fact]
    public async Task Con_un_contacto_probado_otro_correo_de_la_misma_empresa_entra_aislado_hasta_que_lo_apruebe_el_contacto()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var (contacto, otro, nit) = (CorreoNuevo(), CorreoNuevo(), Nit());
        var primero = await CrearVinculoAsync(norte, "CLIENTE", contacto, nit);
        await AceptarAsync(await SesionAsync(contacto, "Norte"), primero.Id); // el contacto queda probado

        // La misma empresa vincula al tercero con otro tipo de vínculo y otro correo: no es contacto conocido.
        var segundo = await CrearVinculoAsync(norte, "PROVEEDOR", otro, nit);
        segundo.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        var sesion = await SesionAsync(otro, "Norte");
        sesion.Aislada.ShouldBeTrue();
        await TerceroAsync("GET", "/public/v1/boveda", sesion.AccessToken, null, 403);

        // Un contacto que ya existía (la persona del primer vínculo) lo aprueba: en la siguiente sesión entra completo.
        var decisor = await SesionPorAvisoAsync(contacto, EsAvisoDeAcceso);
        var solicitud = (await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", decisor.AccessToken, null, 200)).Single();
        solicitud.Correo.ShouldBe(otro);
        await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/aprobar", decisor.AccessToken, null, 200);
        (await VinculoDelAislamientoAsync(norte, segundo.Id)).AccesoDelCorreo!.Estado.ShouldBe("Aprobado");
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(otro))).Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        var reingreso = await SesionPorEnlaceConOtpAsync(otro, m => m.Asunto.Contains("para entrar", StringComparison.Ordinal));
        reingreso.Aislada.ShouldBeFalse();
    }
}
