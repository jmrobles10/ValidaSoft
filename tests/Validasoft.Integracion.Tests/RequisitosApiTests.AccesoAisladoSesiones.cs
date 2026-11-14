using Alba;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Alcance de la sesión del tercero: el token embebido de un expediente ya no sirve para toda la bóveda, cada sesión se puede cerrar por separado y su token
/// deja de valer al instante, y la invitación masiva también decide el acceso del correo de cada fila.
/// </summary>
public sealed partial class RequisitosApiTests
{
    // ---------- token embebido limitado a su componente ----------

    private async Task<SesionEmbebidaDto> EmbebidaAsync(Credencial sistema, Guid vinculoId, string componente) =>
        await ApiAsync<SesionEmbebidaDto>("POST", "/api/v1/sesiones-tercero", sistema, new CrearSesionEmbebidaDto(vinculoId, componente, OrigenAnfitrion), 201);

    [Fact]
    public async Task El_token_embebido_de_un_expediente_solo_sirve_para_su_vinculo_y_no_para_toda_la_boveda()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var erp = await SistemaNuevoConOrigenesAsync(norte, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var otro = await CrearVinculoAsync(norte, "CLIENTE", CorreoNuevo());
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        var sesion = await SesionAsync(correo, "Norte");
        await AceptarAsync(sesion, vinculo.Id);

        // Un documento del tercero que el flujo no pide (la empresa no lo recibe): solo lo ve la sesión normal.
        var camara = await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara de comercio"));
        var embebida = await EmbebidaAsync(erp, vinculo.Id, "expediente");
        var token = embebida.Token;

        // Lo que no es del expediente: la bóveda entera, las cuentas, los derechos de datos, las preferencias y el resumen de la ficha.
        foreach (var url in new[]
                 {
                     "/public/v1/boveda", $"/public/v1/boveda/documentos/{camara.Version.DocumentoId}", "/public/v1/exportaciones", "/public/v1/solicitudes-habeas",
                     "/public/v1/preferencias-notificacion", "/public/v1/sesiones", "/public/v1/ficha/propuestas", "/public/v1/ficha/FICHA_GENERAL"
                 })
            (await CodigoAsync(await TerceroAsync("GET", url, token, null, 403))).ShouldBe("alcance-de-sesion", url);
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/documentos/{camara.Version.DocumentoId}/versiones/1/retirar", token, new RetirarVersionDto("x"), 403)))
            .ShouldBe("alcance-de-sesion");
        // Ni siquiera descarga lo que no está compartido con su vínculo.
        await TerceroAsync("POST", $"/public/v1/boveda/documentos/{camara.Version.DocumentoId}/versiones/1/descarga", token, null, 404);

        // Lo del vínculo para el que se emitió sí: su empresa, su expediente y subir lo que le piden (para ese vínculo y no para otro).
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", token, null, 200)).Select(v => v.Id).ShouldBe([vinculo.Id]);
        var rut = Pdf("RUT desde el embebido");
        var subido = await SubirParaVinculoComoAsync(token, "RUT", vinculo.Id, rut);
        subido.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(vinculo.Id);
        var descarga = await TerceroAsync<DescargaDto>("POST", $"/public/v1/boveda/documentos/{subido.Version.DocumentoId}/versiones/1/descarga", token, null, 200);
        (await Http.GetByteArrayAsync(descarga.Url)).ShouldBe(rut);
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/documentos", token,
            new IniciarCargaDto("rut.pdf", 10, "application/pdf", TipoCanonico: "RUT", VinculoId: otro.Id), 404))).ShouldBe("vinculo-no-encontrado");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/documentos", token, new IniciarCargaDto("rut.pdf", 10, "application/pdf", TipoCanonico: "RUT"), 404)))
            .ShouldBe("vinculo-no-encontrado");

        // El componente de la bóveda sí trae la bóveda (pero no la ficha ni los derechos de datos) y el de la ficha, la ficha (pero no la bóveda).
        var deLaBoveda = await EmbebidaAsync(erp, vinculo.Id, "boveda");
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", deLaBoveda.Token, null, 200)).Documentos.Count.ShouldBe(2);
        (await CodigoAsync(await TerceroAsync("GET", "/public/v1/ficha", deLaBoveda.Token, null, 403))).ShouldBe("alcance-de-sesion");
        await TerceroAsync("GET", "/public/v1/exportaciones", deLaBoveda.Token, null, 403);
        var deLaFicha = await EmbebidaAsync(erp, vinculo.Id, "ficha");
        await TerceroAsync("GET", "/public/v1/ficha", deLaFicha.Token, null, 200);
        (await CodigoAsync(await TerceroAsync("GET", "/public/v1/boveda", deLaFicha.Token, null, 403))).ShouldBe("alcance-de-sesion");
    }

    [Fact]
    public async Task La_sesion_embebida_de_un_vinculo_con_acceso_aislado_nace_aislada()
    {
        var e = await AislamientoAsync();
        var erp = await SistemaNuevoConOrigenesAsync(e.Intrusa, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{e.VinculoIntrusa.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        await AceptarAsync(e.SesionIntruso, e.VinculoIntrusa.Id);

        var embebida = await EmbebidaAsync(erp, e.VinculoIntrusa.Id, "boveda");
        var actual = await TerceroAsync<SesionTerceroDto>("GET", "/public/v1/sesion/actual", embebida.Token, null, 200);
        actual.Aislada.ShouldBeTrue();
        actual.TerceroEnmascarado.ShouldNotBeNullOrEmpty();
        (await CodigoAsync(await TerceroAsync("GET", "/public/v1/boveda", embebida.Token, null, 403))).ShouldBe("acceso-aislado");
        (await CodigoAsync(await TerceroAsync("GET", $"/public/v1/boveda/documentos/{e.RutId}", embebida.Token, null, 403))).ShouldBe("acceso-aislado");
        var cuarentena = await SubirParaVinculoComoAsync(embebida.Token, "RUT", e.VinculoIntrusa.Id, Pdf("RUT desde el embebido aislado"));
        cuarentena.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(e.VinculoIntrusa.Id);
        (await DocumentosDeAsync(e.Norte, e.VinculoNorte.Id)).ShouldHaveSingleItem().VersionActual.ShouldBe(1);

        // Con el acceso rechazado, el token de esa sesión deja de valer al instante.
        var contacto = await SesionPorAvisoAsync(e.Contacto, EsAvisoDeAcceso);
        var solicitud = (await TerceroAsync<List<AccesoSolicitadoDto>>("GET", "/public/v1/accesos-solicitados", contacto.AccessToken, null, 200)).Single();
        await TerceroAsync<AccesoSolicitadoDto>("POST", $"/public/v1/accesos-solicitados/{solicitud.Id}/rechazar", contacto.AccessToken, new RechazarAccesoDto(), 200);
        await TerceroAsync("GET", "/public/v1/sesion/actual", embebida.Token, null, 401);
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/sesiones-tercero", erp, new CrearSesionEmbebidaDto(e.VinculoIntrusa.Id, "boveda", OrigenAnfitrion), 409)))
            .ShouldBe("acceso-rechazado");
    }

    // ---------- sesiones del tercero ----------

    private async Task<(SesionTerceroDto Sesion, string Cookie)> CanjearConCookieAsync(MensajeDeCorreo correo)
    {
        var canje = await PublicoAsync($"/public/v1/enlaces/{RegexToken().Match(correo.Texto).Groups[1].Value}/canjear", null);
        canje.Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        var cookie = canje.Context.Response.Headers.SetCookie.Select(c => c!).Single(c => c.StartsWith($"{CookieDeSesionTercero.Nombre}=", StringComparison.Ordinal)).Split(';')[0];
        return ((await canje.ReadAsJsonAsync<SesionTerceroDto>())!, cookie);
    }

    private async Task<IScenarioResult> RenovarAsync(string cookie) =>
        await Host.Scenario(s =>
        {
            s.Post.Url("/public/v1/sesion/renovar");
            s.WithRequestHeader("Cookie", cookie);
            s.WithRequestHeader(SesionPublicaEndpoints.CabeceraCliente, SesionPublicaEndpoints.ValorCliente);
            IpAleatoria(s);
            s.IgnoreStatusCode();
        });

    [Fact]
    public async Task Cada_sesion_del_tercero_se_lista_y_se_cierra_por_separado_y_su_token_deja_de_servir_al_instante()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var correo = CorreoNuevo();
        await CrearVinculoAsync(norte, "CLIENTE", correo);
        var (primera, cookiePrimera) = await CanjearConCookieAsync(await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("Norte")));

        // Una segunda sesión (otro navegador) entra con el enlace que pide con su correo.
        (await PublicoAsync("/public/v1/acceso", new SolicitarAccesoDto(correo))).Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        var (segunda, cookieSegunda) = await CanjearConCookieAsync(await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("para entrar")));

        var lista = await TerceroAsync<List<SesionActivaDto>>("GET", "/public/v1/sesiones", primera.AccessToken, null, 200);
        lista.Count.ShouldBe(2);
        lista.ShouldAllBe(s => s.Correo == correo && s.ExpiraEn > DateTimeOffset.UtcNow);
        var actual = lista.Single(s => s.EsActual);
        var otra = lista.Single(s => !s.EsActual);
        actual.Id.ShouldNotBe(otra.Id);
        (await TerceroAsync<List<SesionActivaDto>>("GET", "/public/v1/sesiones", segunda.AccessToken, null, 200)).Single(s => s.EsActual).Id.ShouldBe(otra.Id);

        // Una sesión que no existe o es de otro tercero: 404.
        await TerceroAsync("POST", $"/public/v1/sesiones/{Guid.NewGuid()}/cerrar", primera.AccessToken, null, 404);
        var correoAjeno = CorreoNuevo();
        await CrearVinculoAsync(norte, "CLIENTE", correoAjeno);
        var sesionAjena = await SesionAsync(correoAjeno, "Norte");
        var idAjeno = (await TerceroAsync<List<SesionActivaDto>>("GET", "/public/v1/sesiones", sesionAjena.AccessToken, null, 200)).Single().Id;
        await TerceroAsync("POST", $"/public/v1/sesiones/{idAjeno}/cerrar", primera.AccessToken, null, 404);

        // Cierra la segunda desde la primera: su token ya no sirve (al instante, sin esperar a que venza) ni renueva; la primera sigue.
        await TerceroAsync("GET", "/public/v1/vinculos", segunda.AccessToken, null, 200);
        await TerceroAsync("POST", $"/public/v1/sesiones/{otra.Id}/cerrar", primera.AccessToken, null, 204);
        await TerceroAsync("GET", "/public/v1/vinculos", segunda.AccessToken, null, 401);
        var renovacion = await RenovarAsync(cookieSegunda);
        renovacion.Context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        (await CodigoAsync(renovacion)).ShouldBe("sesion-revocada");
        await TerceroAsync("POST", $"/public/v1/sesiones/{otra.Id}/cerrar", primera.AccessToken, null, 404); // ya cerrada
        (await TerceroAsync<List<SesionActivaDto>>("GET", "/public/v1/sesiones", primera.AccessToken, null, 200)).ShouldHaveSingleItem().Id.ShouldBe(actual.Id);
        (await RenovarAsync(cookiePrimera)).Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);

        // Cerrar la propia sesión también corta su token.
        await TerceroAsync("POST", $"/public/v1/sesiones/{actual.Id}/cerrar", primera.AccessToken, null, 204);
        await TerceroAsync("GET", "/public/v1/vinculos", primera.AccessToken, null, 401);
    }

    // ---------- invitación masiva ----------

    [Fact]
    public async Task La_invitacion_masiva_decide_el_acceso_de_cada_correo_igual_que_un_vinculo_creado_a_mano()
    {
        var e = await AislamientoAsync();
        var (nitNuevo, correoNuevo, correoPropio) = (Nit(), CorreoNuevo(), CorreoNuevo());
        var nitExistente = (await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{e.VinculoIntrusa.TerceroId}", Admin(e.Norte), null, 200)).Identificacion.Numero;
        var csv = CsvDeLote(
        [
            $"NIT;{nitNuevo};Tercero nuevo del lote;{correoNuevo};PROVEEDOR",
            $"NIT;{nitExistente};Tercero que ya existía;{correoPropio};PROVEEDOR"
        ]);
        var creado = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", Admin(e.Intrusa), new CrearLoteDto("Proveedores", Archivo(csv)), 202);
        var lote = await EsperarLoteAsync(e.Intrusa, creado.Id, l => l.Estado is "Completado" or "CompletadoConErrores");
        lote.Estado.ShouldBe("Completado");

        var filas = await FilasAsync(e.Intrusa, creado.Id);
        filas.Select(f => f.Estado).ShouldBe(["Exitosa", "Exitosa"]);
        filas[0].TerceroCreado.ShouldBeTrue();
        filas[1].TerceroCreado.ShouldBeFalse();

        // El tercero que creó el lote toma el correo como contacto; el que ya existía deja el correo ajeno aislado, con el aviso a sus contactos.
        (await VinculoDelAislamientoAsync(e.Intrusa, filas[0].VinculoId!.Value)).AccesoDelCorreo.ShouldBeNull();
        var aislado = await VinculoDelAislamientoAsync(e.Intrusa, filas[1].VinculoId!.Value);
        aislado.AccesoDelCorreo!.Estado.ShouldBe("Pendiente");
        aislado.AccesoDelCorreo.Correo.ShouldBe(correoPropio);
        var aviso = await fixture.Correos.EsperarAsync(e.Contacto, m => EsAvisoDeAcceso(m) && m.Asunto.Contains(correoPropio));
        aviso.Asunto.ShouldContain("Intrusa S.A.S.");
        (await SesionAsync(correoNuevo, "Intrusa")).Aislada.ShouldBeFalse();
        (await SesionAsync(correoPropio, "Intrusa")).Aislada.ShouldBeTrue();
    }
}
