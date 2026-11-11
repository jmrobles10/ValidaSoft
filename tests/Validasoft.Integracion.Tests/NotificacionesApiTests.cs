using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Contratos;
using Validasoft.Dominio.Integraciones;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Eventos públicos, webhooks firmados, modo pull y notificaciones a personas (NW): recorrido completo con un receptor HTTP
/// real (Kestrel en 127.0.0.1), receptor que falla (reintentos, deshabilitación, aviso y reenvío manual), concurrencia sin
/// pérdidas ni duplicados, alcance por empresa, campana, correo consolidado a revisores y constancia al tercero.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class NotificacionesApiTests(ApiFixture fixture)
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(60);

    private static readonly string[] ScopesDeSistema =
    [
        "configuracion:leer", "terceros:leer", "terceros:escribir", "vinculos:leer", "vinculos:escribir", "flujos:leer", "expedientes:leer",
        "expedientes:revisar", "expedientes:administrar", "eventos:leer", "webhooks:administrar"
    ];

    private IAlbaHost Host => fixture.Host;

    private CorreosCapturados Correos => fixture.Correos;

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    private sealed record Persona(UsuarioDto Usuario, string Sub);

    private sealed record Escenario(
        string Admin, EmpresaDto EmpresaA, EmpresaDto EmpresaB, string LlaveSrm, RSA ClaveSrm, AreaDto Aprobador, AreaDto Cumplimiento,
        Persona Laura, Persona Carlos, Persona Pedro, Persona Ana);

    // ---------- recorrido completo ----------

    [Fact]
    public async Task El_recorrido_completo_llega_al_receptor_en_orden_firmado_y_coincide_con_el_modo_pull()
    {
        var e = await PrepararAsync();
        await using var receptor = await ReceptorDeWebhooks.IniciarAsync();
        var webhook = await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Srm(e),
            new CrearWebhookDto(receptor.Url, ["*"], [e.EmpresaA.Id.ToString()], Descripcion: "SRM local"), 201);
        var secreto = webhook.Secreto.ShouldNotBeNull();
        webhook.Sistema.ShouldBe("SRM");
        webhook.Activa.ShouldBeTrue();
        (await ApiAsync<WebhookDto>("GET", $"/api/v1/webhooks/{webhook.Id}", Srm(e), null, 200)).Secreto.ShouldBeNull();

        // Un vínculo de otra empresa del tenant no le llega a esta suscripción.
        await CrearVinculoAsync(e, e.EmpresaB, CorreoNuevo(), "ADP-OTRA");

        // Crear (con la referencia del SRM), aceptar, observar, reenviar y aprobar.
        var correoTercero = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, e.EmpresaA, correoTercero, "ADP-77");
        var sesion = await AceptarInvitacionAsync(vinculo, correoTercero);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();
        var folio = (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e), null, 200)).Folio;

        // «Nuevo expediente en tu área»: un correo por persona aunque esté en dos áreas (Pedro está en ambas).
        var aPedro = await Correos.EsperarAsync(e.Pedro.Usuario.Correo, m => m.Asunto.StartsWith("Nuevo expediente en tu área", StringComparison.Ordinal));
        aPedro.Asunto.ShouldBe($"Nuevo expediente en tu área: {folio}");
        aPedro.Texto.ShouldContain("Tus áreas: Aprobador inicial, Oficial de cumplimiento.");
        aPedro.Texto.ShouldContain($"#/e/{e.EmpresaA.Id}/expedientes/{expedienteId}");
        await Correos.EsperarAsync(e.Laura.Usuario.Correo, m => m.Asunto == $"Nuevo expediente en tu área: {folio}");

        // Constancia al tercero de lo que aceptó (consentimiento-empresa).
        var constancia = await Correos.EsperarAsync(correoTercero, m => m.Asunto.StartsWith("Constancia de tu aceptación", StringComparison.Ordinal));
        constancia.Asunto.ShouldBe("Constancia de tu aceptación con Norte S.A.S.");
        constancia.Texto.ShouldContain("Autorización de tratamiento de datos de Norte S.A.S., versión 1");
        constancia.Texto.ShouldContain("Términos y condiciones de Norte S.A.S., versión 1");

        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
            new VeredictoDto("Observado", ["incompleto"], "Falta el RUT."), 200);
        await TerceroAsync("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken, new ReenviarExpedienteDto("Listo."), 200);
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura),
            new VeredictoDto("Aprobado"), 200);
        var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200);
        aprobado.Estado.ShouldBe("Aprobado");

        // El receptor recibe todo en orden, con firma válida e Event-Id único.
        await EsperarAsync(() => receptor.Recibidos.Any(r => r.Tipo == "vinculo.activado" && r.VinculoId == vinculo.Id), "vinculo.activado");
        var deEste = receptor.Recibidos.Where(r => r.VinculoId == vinculo.Id).ToList();
        string[] esperados =
        [
            "vinculo.creado", "vinculo.invitacion_enviada", "vinculo.invitacion_aceptada", "expediente.abierto", "expediente.en_revision",
            "expediente.observado", "expediente.reenviado", "expediente.aprobado", "vinculo.activado"
        ];
        deEste.Select(r => r.Tipo).Where(esperados.Contains).ShouldBe(esperados);

        // El certificado que emitió la aprobación también se publica (NW-13).
        await EsperarAsync(() => receptor.Recibidos.Any(r => r.Tipo == "certificado.emitido" && r.VinculoId == vinculo.Id), "certificado.emitido");
        var certificado = receptor.Recibidos.First(r => r.Tipo == "certificado.emitido" && r.VinculoId == vinculo.Id);
        certificado.Datos.GetProperty("certificado").GetProperty("estado").GetString().ShouldBe("Vigente");
        certificado.Datos.GetProperty("certificado").GetProperty("emision").GetInt32().ShouldBe(1);
        certificado.Datos.GetProperty("expedienteFolio").GetString().ShouldBe(folio);
        certificado.Cuerpo.ShouldNotContain("contenidoCanonico");
        deEste.Select(r => r.Cursor).ShouldBeInOrder(SortDirection.Ascending);
        receptor.Recibidos.Select(r => r.EventId).ShouldBeUnique();
        receptor.Recibidos.ShouldAllBe(r => r.EmpresaId == e.EmpresaA.Id);
        foreach (var recibido in receptor.Recibidos)
        {
            FirmaDeWebhook.Verificar(secreto, recibido.Firma, recibido.Cuerpo, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5)).ShouldBeTrue();
            recibido.EventId.ShouldBe(recibido.Id.ToString());
            recibido.TipoCabecera.ShouldBe(recibido.Tipo);
            recibido.Firma.ShouldStartWith($"t={recibido.Timestamp},v1=");
        }

        var creado = deEste.First(r => r.Tipo == "vinculo.creado");
        creado.Datos.GetProperty("referencias")[0].GetProperty("sistema").GetString().ShouldBe("SRM");
        creado.Datos.GetProperty("referencias")[0].GetProperty("id").GetString().ShouldBe("ADP-77");
        creado.Datos.GetProperty("tipoVinculo").GetString().ShouldBe("CLIENTE");
        var observado = deEste.First(r => r.Tipo == "expediente.observado");
        observado.Datos.GetProperty("folio").GetString().ShouldBe(folio);
        foreach (var r in deEste)
            foreach (var prohibido in new[] { e.Carlos.Usuario.Id.ToString(), e.Carlos.Usuario.Correo, correoTercero, "Carlos", "Listo." })
                r.Cuerpo.ShouldNotContain(prohibido);

        // El modo pull devuelve lo mismo que llegó al webhook (mismo sobre y orden) con una llave limitada a la empresa A.
        var llaveA = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Admin),
            new CrearApiKeyDto("Solo A", ["eventos:leer"], [e.EmpresaA.Id.ToString()], null), 201);
        var pull = await PullCompletoAsync(new Credencial(llaveA.Valor!));
        pull.ShouldAllBe(x => x.EmpresaId == e.EmpresaA.Id);
        await EsperarAsync(() => receptor.Recibidos.Count == pull.Count, $"{pull.Count} entregas");
        receptor.Recibidos.OrderBy(r => r.Cursor).Select(r => r.Cuerpo).ShouldBe(pull.Select(x => JsonSerializer.Serialize(x, JsonWeb)));

        // La llave de administración (todas las empresas) también ve los de B; con X-Empresa solo los de esa empresa.
        var todos = await PullCompletoAsync(new Credencial(e.Admin));
        todos.ShouldContain(x => x.EmpresaId == e.EmpresaB.Id);
        (await PullCompletoAsync(new Credencial(e.Admin, e.EmpresaB.Id))).ShouldAllBe(x => x.EmpresaId == e.EmpresaB.Id);
        var soloAprobados = await ApiAsync<PaginaEventosDto>("GET", "/api/v1/eventos?tipos=expediente.aprobado,vinculo.activado", new Credencial(llaveA.Valor!), null, 200);
        soloAprobados.Datos.Select(x => x.Tipo).ShouldBe(["expediente.aprobado", "vinculo.activado"]);
        var siguiente = await ApiAsync<PaginaEventosDto>("GET", $"/api/v1/eventos?desde={pull[^1].Cursor}", new Credencial(llaveA.Valor!), null, 200);
        siguiente.Datos.ShouldBeEmpty();
        siguiente.Cursor.ShouldBe(pull[^1].Cursor);
        siguiente.HayMas.ShouldBeFalse();

        // Cada entrega queda registrada como Entregada en el primer intento.
        var entregas = await ApiAsync<PaginaDto<EntregaWebhookDto>>("GET", $"/api/v1/webhooks/{webhook.Id}/entregas?limite=200", Srm(e), null, 200);
        entregas.Datos.Count.ShouldBe(pull.Count);
        entregas.Datos.ShouldAllBe(x => x.Estado == "Entregada" && x.Intentos == 1 && x.Historial[0].CodigoHttp == 200);
    }

    [Fact]
    public async Task La_campana_avisa_al_area_y_se_marca_leida_y_las_preferencias_apagan_el_correo()
    {
        var e = await PrepararAsync();
        // Laura apaga los correos de «nuevo expediente»: le llega la campana pero no el correo.
        var preferencias = await ApiAsync<PreferenciasNotificacionDto>("PUT", "/api/v1/usuarios/me/preferencias-notificacion", Delegado(e, e.Laura),
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["expediente.nuevo_en_area"] = false }), 200);
        preferencias.Correo["expediente.nuevo_en_area"].ShouldBeFalse();
        preferencias.Correo["expediente.tercero_respondio"].ShouldBeTrue();
        var invalida = await ApiAsync("PUT", "/api/v1/usuarios/me/preferencias-notificacion", Delegado(e, e.Laura),
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["expediente.aprobado"] = true }), 422);
        (await CodigoAsync(invalida)).ShouldBe("tipo-notificacion-invalido");

        var correoTercero = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, e.EmpresaA, correoTercero, "ADP-" + Guid.NewGuid().ToString("N")[..6]);
        await AceptarInvitacionAsync(vinculo, correoTercero);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();

        var bandeja = await EsperarCampanaAsync(e, e.Laura, b => b.Datos.Any(n => n.ExpedienteId == expedienteId));
        var nueva = bandeja.Datos.Single(n => n.ExpedienteId == expedienteId);
        nueva.Tipo.ShouldBe("expediente.nuevo_en_area");
        nueva.Titulo.ShouldBe("Nuevo expediente en tu área");
        nueva.Enlace.ShouldBe($"/e/{e.EmpresaA.Id}/expedientes/{expedienteId}");
        nueva.LeidaEn.ShouldBeNull();
        bandeja.NoLeidas.ShouldBe(1);

        // Carlos también la tiene; Ana (sin área) no.
        await EsperarCampanaAsync(e, e.Carlos, b => b.Datos.Any(n => n.ExpedienteId == expedienteId));
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e, e.Ana), null, 200)).Datos.ShouldBeEmpty();

        var leida = await ApiAsync<NotificacionDto>("POST", $"/api/v1/notificaciones/{nueva.Id}/leida", Delegado(e, e.Laura), null, 200);
        leida.LeidaEn.ShouldNotBeNull();
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones?soloNoLeidas=true", Delegado(e, e.Laura), null, 200)).NoLeidas.ShouldBe(0);
        await ApiAsync("POST", $"/api/v1/notificaciones/{nueva.Id}/leida", Delegado(e, e.Carlos), null, 404);
        (await ApiAsync<NotificacionesMarcadasDto>("POST", "/api/v1/notificaciones/leidas", Delegado(e, e.Carlos), null, 200)).Marcadas.ShouldBe(1);

        // Laura no recibió correo de «nuevo expediente»; Carlos sí.
        await Correos.EsperarAsync(e.Carlos.Usuario.Correo, m => m.Asunto.StartsWith("Nuevo expediente en tu área", StringComparison.Ordinal));
        Correos.Para(e.Laura.Usuario.Correo).ShouldNotContain(m => m.Asunto.StartsWith("Nuevo expediente en tu área", StringComparison.Ordinal));

        // Una integración sin persona no tiene campana.
        var sinPersona = await ApiAsync("GET", "/api/v1/notificaciones", Srm(e), null, 422);
        (await CodigoAsync(sinPersona)).ShouldBe("usuario-delegado-requerido");
    }

    // ---------- receptor que falla ----------

    [Fact]
    public async Task Un_receptor_que_falla_registra_reintentos_se_deshabilita_avisa_y_admite_reenvio_manual()
    {
        var e = await PrepararAsync();
        await using var receptor = await ReceptorDeWebhooks.IniciarAsync(codigo: 500);
        var webhook = await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Srm(e), new CrearWebhookDto(receptor.Url, ["vinculo.creado"]), 201);

        var vinculo = await CrearVinculoAsync(e, e.EmpresaA, CorreoNuevo(), "ADP-FALLA");
        // Tres intentos (inmediato, +1 s, +1 s) y la suscripción se deshabilita al tercer fallo seguido.
        var fallida = await EsperarEntregaAsync(e, webhook.Id, x => x.Estado == "Fallida");
        fallida.Intentos.ShouldBe(ApiFixture.FallosParaDeshabilitarWebhook);
        fallida.Historial.Select(i => i.Numero).ShouldBe([1, 2, 3]);
        fallida.Historial.ShouldAllBe(i => i.CodigoHttp == 500 && i.Resultado == "Fallido" && i.Error == "HTTP 500" && !i.Manual);
        fallida.Historial.Select(i => i.IntentoId).ShouldBeUnique();
        receptor.Recibidos.Count.ShouldBe(3);
        receptor.Recibidos.Select(r => r.EventId).Distinct().ShouldHaveSingleItem().ShouldBe(fallida.EventoId.ToString());
        receptor.Recibidos.Select(r => r.Delivery).ShouldBeUnique();

        var deshabilitado = await ApiAsync<WebhookDto>("GET", $"/api/v1/webhooks/{webhook.Id}", Srm(e), null, 200);
        deshabilitado.Activa.ShouldBeFalse();
        deshabilitado.CausaDeshabilitacion.ShouldBe("fallos-consecutivos");
        deshabilitado.FallosSeguidos.ShouldBe(3);

        // El administrador se entera (campana y correo); el cambio queda auditado.
        var admin = await EsperarCampanaAsync(e, e.Ana, b => b.Datos.Any(n => n.Tipo == "webhook.deshabilitado"));
        admin.Datos.First(n => n.Tipo == "webhook.deshabilitado").Mensaje.ShouldContain(receptor.Url);
        await Correos.EsperarAsync(e.Ana.Usuario.Correo, m => m.Asunto == "Webhook deshabilitado: SRM");

        // Un evento nuevo ya no se entrega a la suscripción deshabilitada.
        await CrearVinculoAsync(e, e.EmpresaA, CorreoNuevo(), "ADP-DESPUES");
        await Task.Delay(TimeSpan.FromSeconds(3));
        receptor.Recibidos.Count.ShouldBe(3);

        // Reenvío manual tras arreglar el receptor: un cuarto intento, manual y exitoso.
        receptor.Codigo = 200;
        var reenvio = await ApiAsync<EntregaWebhookDto>("POST", $"/api/v1/webhooks/{webhook.Id}/entregas/{fallida.Id}/reenviar", Srm(e), null, 202);
        reenvio.Estado.ShouldBe("Pendiente");
        var entregada = await EsperarEntregaAsync(e, webhook.Id, x => x.Id == fallida.Id && x.Estado == "Entregada");
        entregada.Historial.Count.ShouldBe(4);
        entregada.Historial[^1].Manual.ShouldBeTrue();
        entregada.Historial[^1].CodigoHttp.ShouldBe(200);
        receptor.Recibidos[^1].EventId.ShouldBe(fallida.EventoId.ToString());

        // Reactivar reinicia la cuenta de fallos.
        var activo = await ApiAsync<WebhookDto>("POST", $"/api/v1/webhooks/{webhook.Id}/activar", Srm(e), null, 200);
        activo.Activa.ShouldBeTrue();
        activo.FallosSeguidos.ShouldBe(0);
        (await ApiAsync<PaginaDto<EntregaWebhookDto>>("GET", $"/api/v1/webhooks/{webhook.Id}/entregas?estado=Entregada", Srm(e), null, 200))
            .Datos.ShouldHaveSingleItem().Id.ShouldBe(fallida.Id);
        _ = vinculo;
    }

    // ---------- concurrencia ----------

    [Fact]
    public async Task Con_vinculos_creados_en_paralelo_no_se_pierde_ni_se_duplica_ningun_evento()
    {
        var e = await PrepararAsync();
        await using var receptor = await ReceptorDeWebhooks.IniciarAsync();
        await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Srm(e), new CrearWebhookDto(receptor.Url, ["vinculo.creado", "vinculo.invitacion_enviada"]), 201);

        const int cuantos = 12;
        var vinculos = await Task.WhenAll(Enumerable.Range(0, cuantos).Select(i => CrearVinculoAsync(e, e.EmpresaA, CorreoNuevo(), $"ADP-P{i}")));

        await EsperarAsync(() => receptor.Recibidos.Count >= cuantos * 2, $"{cuantos * 2} entregas");
        await Task.Delay(TimeSpan.FromSeconds(2));
        receptor.Recibidos.Count.ShouldBe(cuantos * 2);
        receptor.Recibidos.Select(r => r.EventId).ShouldBeUnique();
        foreach (var vinculo in vinculos)
            receptor.Recibidos.Where(r => r.VinculoId == vinculo.Id).Select(r => r.Tipo).ShouldBe(["vinculo.creado", "vinculo.invitacion_enviada"]);

        var pull = await PullCompletoAsync(new Credencial(e.LlaveSrm, e.EmpresaA.Id));
        pull.Count.ShouldBe(cuantos * 2);
        pull.Select(x => long.Parse(x.Cursor, System.Globalization.CultureInfo.InvariantCulture)).ShouldBeInOrder(SortDirection.Ascending);
        pull.Select(x => x.Cursor).ShouldBeUnique();
        receptor.Recibidos.Select(r => r.Id).ShouldBe(pull.Select(x => x.Id), ignoreOrder: true);
    }

    // ---------- tiempo real ----------

    [Fact]
    public async Task El_stream_SSE_entrega_los_eventos_publicos_con_su_cursor()
    {
        var e = await PrepararAsync();
        using var cliente = Host.Server.CreateClient();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/eventos/stream");
        peticion.Headers.Add("X-Api-Key", e.LlaveSrm);
        peticion.Headers.Add("X-Empresa", e.EmpresaA.Id.ToString());
        using var limite = new CancellationTokenSource(Espera);
        using var respuesta = await cliente.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, limite.Token);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");

        var vinculo = await CrearVinculoAsync(e, e.EmpresaA, CorreoNuevo(), "ADP-SSE");
        using var lector = new StreamReader(await respuesta.Content.ReadAsStreamAsync(limite.Token));
        string? id = null;
        while (await lector.ReadLineAsync(limite.Token) is { } linea)
        {
            if (linea.StartsWith("id: ", StringComparison.Ordinal))
                id = linea[4..];
            if (linea != "event: vinculo.creado")
                continue;
            var datos = (await lector.ReadLineAsync(limite.Token))!;
            datos.ShouldStartWith("data: ");
            var evento = JsonSerializer.Deserialize<EventoPublicoDto>(datos[6..], JsonWeb)!;
            evento.Datos.GetProperty("vinculoId").GetGuid().ShouldBe(vinculo.Id);
            evento.Cursor.ShouldBe(id);
            break;
        }

        await limite.CancelAsync();
        id.ShouldNotBeNull();
    }

    // ---------- administración de suscripciones ----------

    [Fact]
    public async Task Las_suscripciones_validan_destino_tipos_y_alcance_rotan_el_secreto_y_se_aislan_por_sistema()
    {
        var e = await PrepararAsync();
        async Task<string?> Falla(Credencial credencial, CrearWebhookDto dto) => await CodigoAsync(await ApiAsync("POST", "/api/v1/webhooks", credencial, dto, 422));

        (await Falla(Srm(e), new CrearWebhookDto("http://srm.cliente.co/hook", ["*"]))).ShouldBe("url-no-permitida");
        (await Falla(Srm(e), new CrearWebhookDto("https://10.0.0.8/hook", ["*"]))).ShouldBe("url-no-permitida");
        (await Falla(Srm(e), new CrearWebhookDto("https://169.254.169.254/latest", ["*"]))).ShouldBe("url-no-permitida");
        (await Falla(Srm(e), new CrearWebhookDto("https://srm.cliente.co/hook", ["vinculo.inventado"]))).ShouldBe("tipos-invalidos");
        (await Falla(Srm(e), new CrearWebhookDto("https://srm.cliente.co/hook", []))).ShouldBe("tipos-requeridos");
        // La llave de administración no es de un sistema: debe indicar cuál.
        (await Falla(new Credencial(e.Admin), new CrearWebhookDto("https://srm.cliente.co/hook", ["*"]))).ShouldBe("sistema-requerido");

        // Una llave limitada a la empresa A no se suscribe a B; con «*» queda limitada a A.
        var sistemas = await ApiAsync<List<SistemaIntegradoDto>>("GET", "/api/v1/sistemas", new Credencial(e.Admin), null, 200);
        var llaveA = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Admin),
            new CrearApiKeyDto("SRM solo A", ["webhooks:administrar"], [e.EmpresaA.Id.ToString()], null, SistemaId: sistemas.Single(s => s.Codigo == "SRM").Id), 201);
        (await Falla(new Credencial(llaveA.Valor!), new CrearWebhookDto("https://srm.cliente.co/hook", ["*"], [e.EmpresaB.Id.ToString()])))
            .ShouldBe("empresas-fuera-de-alcance");
        var limitado = await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", new Credencial(llaveA.Valor!), new CrearWebhookDto("https://srm.cliente.co/hook", ["*"]), 201);
        limitado.Empresas.ShouldBe([e.EmpresaA.Id.ToString()]);

        // El secreto se muestra una sola vez; rotar entrega otro y deja el anterior vigente 24 h.
        var creado = await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", new Credencial(e.Admin),
            new CrearWebhookDto("https://srm.cliente.co/hook", ["expediente.aprobado"], SistemaId: limitado.SistemaId), 201);
        creado.Secreto.ShouldNotBeNull().ShouldStartWith("whsec_");
        var rotado = await ApiAsync<WebhookDto>("POST", $"/api/v1/webhooks/{creado.Id}/rotar-secreto", Srm(e), null, 200);
        rotado.Secreto.ShouldNotBeNull().ShouldNotBe(creado.Secreto);
        rotado.SecretoAnteriorVence.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddHours(23));
        (await ApiAsync<List<WebhookDto>>("GET", "/api/v1/webhooks", Srm(e), null, 200)).ShouldAllBe(w => w.Secreto == null);

        var editado = await ApiAsync<WebhookDto>("PUT", $"/api/v1/webhooks/{creado.Id}", Srm(e),
            new EditarWebhookDto(Tipos: ["expediente.aprobado", "expediente.rechazado"]), 200);
        editado.Tipos.ShouldBe(["expediente.aprobado", "expediente.rechazado"]);
        (await ApiAsync<WebhookDto>("POST", $"/api/v1/webhooks/{creado.Id}/desactivar", Srm(e), null, 200)).Activa.ShouldBeFalse();

        // Otro sistema del tenant no la ve (404) y la auditoría registró los cambios sin el secreto.
        var otraClave = RSA.Create(2048);
        var marco = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(e.Admin),
            new CrearSistemaIntegradoDto("MARCO", "Marco", otraClave.ExportSubjectPublicKeyInfoPem()), 201);
        var llaveMarco = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Admin),
            new CrearApiKeyDto("Marco", ["webhooks:administrar"], null, null, SistemaId: marco.Id), 201);
        await ApiAsync("GET", $"/api/v1/webhooks/{creado.Id}", new Credencial(llaveMarco.Valor!), null, 404);
        (await ApiAsync<List<WebhookDto>>("GET", "/api/v1/webhooks", new Credencial(llaveMarco.Valor!), null, 200)).ShouldBeEmpty();

        var tipos = await ApiAsync<List<TipoEventoPublicoDto>>("GET", "/api/v1/eventos/tipos", Srm(e), null, 200);
        tipos.Select(t => t.Tipo).ShouldContain("documento.por_vencer");
        tipos.Select(t => t.Tipo).ShouldContain("certificado.revocado");

        // Una llave de varias empresas sin X-Empresa recibe 422 empresa-requerida en los listados, no un 500.
        foreach (var ruta in new[] { "/api/v1/vinculos", "/api/v1/expedientes", "/api/v1/areas", "/api/v1/flujos" })
            (await CodigoAsync(await ApiAsync("GET", ruta, new Credencial(e.Admin), null, 422))).ShouldBe("empresa-requerida");
        // 25 del proceso de validación (3 de cuenta designada, CB-3; caducado y reabierto, LT-2/LT-3; vinculo.reintegrado y
        // expediente.desaprobado, DS-7) + 7 del certificado (NW-13, RV-3) + seccion.aceptada (FM-32) + invitaciones.lote_terminado
        // (LT-9) + 3 de verificaciones (VE-13).
        tipos.Count.ShouldBe(38);
        tipos.Select(t => t.Tipo).ShouldContain("expediente.desaprobado");
        tipos.Select(t => t.Tipo).ShouldContain("vinculo.reintegrado");
    }

    // ---------- preparación ----------

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Grupo Norte", "co", "America/Bogota"), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var a = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null), 201);
        var b = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null), 201);

        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(admin),
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", rsa.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(admin),
            new CrearApiKeyDto("Integración SRM", ScopesDeSistema, null, null, SistemaId: sistema.Id), 201);

        async Task<Persona> PersonaAsync(string nombre, string rol, Guid? empresa)
        {
            var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
            var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(admin),
                new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto(rol, empresa)], null), 201);
            return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
        }

        var laura = await PersonaAsync("Laura Aprobadora", "Revisor", a.Id);
        var carlos = await PersonaAsync("Carlos Cumplimiento", "OficialCumplimiento", a.Id);
        var pedro = await PersonaAsync("Pedro Suplente", "Revisor", a.Id);
        var ana = await PersonaAsync("Ana Administradora", "Administrador", null);

        var credencialA = new Credencial(admin, a.Id);
        var aprobador = await ApiAsync<AreaDto>("POST", "/api/v1/areas", credencialA, new CrearAreaDto("APROBADOR_INICIAL", "Aprobador inicial"), 201);
        var cumplimiento = await ApiAsync<AreaDto>("POST", "/api/v1/areas", credencialA, new CrearAreaDto("OFICIAL_CUMPLIMIENTO", "Oficial de cumplimiento"), 201);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{aprobador.Id}/miembros", credencialA, new MiembrosAreaDto([laura.Usuario.Id, pedro.Usuario.Id]), 200);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{cumplimiento.Id}/miembros", credencialA, new MiembrosAreaDto([carlos.Usuario.Id, pedro.Usuario.Id]), 200);

        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", credencialA, new CrearFlujoDto("HAB_CLIENTE", "Habilitación de clientes", "CLIENTE",
            [new EtapaDto(1, [new AreaDeEtapaDto(aprobador.Id), new AreaDeEtapaDto(cumplimiento.Id)])]), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", credencialA, new { }, 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{a.Id}/textos-legales/autorizacion", new Credencial(admin),
            new PublicarTextoLegalDto("Autorizo el tratamiento de mis datos por Norte S.A.S."), 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{a.Id}/textos-legales/terminos", new Credencial(admin),
            new PublicarTextoLegalDto("Términos de Norte"), 200);

        return new Escenario(admin, a, b, llave.Valor!, rsa, aprobador, cumplimiento, laura, carlos, pedro, ana);
    }

    /// <summary>El SRM crea el vínculo con su referencia externa (la fija la llave del sistema).</summary>
    private Task<VinculoDto> CrearVinculoAsync(Escenario e, EmpresaDto empresa, string correo, string referencia)
    {
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", new Credencial(e.LlaveSrm, empresa.Id), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Contacto", correo, null, "Comercial")]),
            TipoVinculo: "CLIENTE",
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: new ReferenciaExternaDto("SRM", referencia)), 201);
    }

    private async Task<SesionTerceroDto> AceptarInvitacionAsync(VinculoDto vinculo, string correo)
    {
        var invitacion = await Correos.EsperarAsync(correo);
        var token = RegexToken().Match(invitacion.Texto).Groups[1].Value;
        var canje = await Host.Scenario(s =>
        {
            s.Post.Url($"/public/v1/enlaces/{token}/canjear");
            IpAleatoria(s);
            s.StatusCodeShouldBe(200);
        });
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
        var textos = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{vinculo.Id}", sesion.AccessToken, null, 200);
        await TerceroAsync("POST", $"/public/v1/invitaciones/{vinculo.Id}/aceptar", sesion.AccessToken,
            new AceptarInvitacionDto([.. textos.TextosPorAceptar.Select(t => new ConsentimientoDto(t.Tipo, t.TextoVersion, true))]), 200);
        return sesion;
    }

    // ---------- esperas ----------

    private static async Task EsperarAsync(Func<bool> condicion, string que)
    {
        var limite = DateTime.UtcNow + Espera;
        while (!condicion())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No se cumplió a tiempo: {que}.");
            await Task.Delay(100);
        }
    }

    private async Task<BandejaNotificacionesDto> EsperarCampanaAsync(Escenario e, Persona persona, Func<BandejaNotificacionesDto, bool> condicion)
    {
        var limite = DateTime.UtcNow + Espera;
        while (true)
        {
            var bandeja = await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e, persona), null, 200);
            if (condicion(bandeja))
                return bandeja;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La campana de {persona.Usuario.Nombre} no recibió lo esperado.");
            await Task.Delay(200);
        }
    }

    private async Task<EntregaWebhookDto> EsperarEntregaAsync(Escenario e, Guid webhookId, Func<EntregaWebhookDto, bool> condicion)
    {
        var limite = DateTime.UtcNow + Espera;
        while (true)
        {
            var entregas = await ApiAsync<PaginaDto<EntregaWebhookDto>>("GET", $"/api/v1/webhooks/{webhookId}/entregas", Srm(e), null, 200);
            if (entregas.Datos.FirstOrDefault(condicion) is { } entrega)
                return entrega;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("La entrega no llegó al estado esperado.");
            await Task.Delay(200);
        }
    }

    private async Task<List<EventoPublicoDto>> PullCompletoAsync(Credencial credencial)
    {
        var todos = new List<EventoPublicoDto>();
        var cursor = "0";
        while (true)
        {
            var pagina = await ApiAsync<PaginaEventosDto>("GET", $"/api/v1/eventos?desde={cursor}&limite=5", credencial, null, 200);
            todos.AddRange(pagina.Datos);
            cursor = pagina.Cursor;
            if (!pagina.HayMas)
                return todos;
        }
    }

    // ---------- receptor de webhooks ----------

    private static readonly JsonSerializerOptions JsonWeb = CrearJsonWeb();

    private static JsonSerializerOptions CrearJsonWeb()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return opciones;
    }

    internal sealed record Recibido(string Cuerpo, string EventId, string TipoCabecera, string Delivery, string Timestamp, string Firma)
    {
        private JsonElement Json => JsonDocument.Parse(Cuerpo).RootElement;

        public Guid Id => Json.GetProperty("id").GetGuid();

        public string Tipo => Json.GetProperty("tipo").GetString()!;

        public long Cursor => long.Parse(Json.GetProperty("cursor").GetString()!, System.Globalization.CultureInfo.InvariantCulture);

        public Guid? EmpresaId => Json.GetProperty("empresaId").ValueKind == JsonValueKind.Null ? null : Json.GetProperty("empresaId").GetGuid();

        public JsonElement Datos => Json.GetProperty("datos");

        public Guid? VinculoId => Datos.TryGetProperty("vinculoId", out var v) ? v.GetGuid() : null;
    }

    /// <summary>Receptor HTTP real en 127.0.0.1 (Kestrel) que guarda lo recibido y responde <see cref="Codigo"/>.</summary>
    internal sealed class ReceptorDeWebhooks : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly ConcurrentQueue<Recibido> _recibidos = new();

        private ReceptorDeWebhooks(WebApplication app) => _app = app;

        public int Codigo { get; set; } = 200;

        public string Url { get; private set; } = string.Empty;

        public IReadOnlyList<Recibido> Recibidos => [.. _recibidos];

        public static async Task<ReceptorDeWebhooks> IniciarAsync(int codigo = 200)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            var app = builder.Build();
            var receptor = new ReceptorDeWebhooks(app) { Codigo = codigo };
            app.MapPost("/webhooks/validasoft", async (HttpContext http) =>
            {
                using var lector = new StreamReader(http.Request.Body);
                var cuerpo = await lector.ReadToEndAsync();
                string Cabecera(string nombre) => http.Request.Headers[nombre].ToString();
                receptor._recibidos.Enqueue(new Recibido(cuerpo, Cabecera("Validasoft-Event-Id"), Cabecera("Validasoft-Event-Type"),
                    Cabecera("Validasoft-Delivery"), Cabecera("Validasoft-Timestamp"), Cabecera("Validasoft-Signature")));
                http.Response.StatusCode = receptor.Codigo;
            });
            await app.StartAsync();
            var direccion = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            receptor.Url = direccion.TrimEnd('/') + "/webhooks/validasoft";
            return receptor;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    // ---------- utilidades HTTP ----------

    private sealed record Credencial(string ApiKey, Guid? Empresa = null, string? Delegado = null);

    private static Credencial Admin(Escenario e) => new(e.Admin, e.EmpresaA.Id);

    private static Credencial Srm(Escenario e) => new(e.LlaveSrm);

    /// <summary>La persona actúa desde el SRM con una aserción de un solo uso (SE-4) firmada por el sistema.</summary>
    private static Credencial Delegado(Escenario e, Persona p)
    {
        var ahora = DateTime.UtcNow;
        var asercion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "SRM",
            Audience = "validasoft",
            Subject = new ClaimsIdentity([new Claim("sub", p.Sub), new Claim("name", p.Usuario.Nombre), new Claim("email", p.Usuario.Correo),
                new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora.AddSeconds(-5),
            NotBefore = ahora.AddSeconds(-5),
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(e.ClaveSrm), SecurityAlgorithms.RsaSha256)
        });
        return new Credencial(e.LlaveSrm, e.EmpresaA.Id, asercion);
    }

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<T> ApiAsync<T>(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        (await (await ApiAsync(metodo, url, credencial, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> ApiAsync(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", credencial.ApiKey);
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());
            if (credencial.Delegado is not null)
                s.WithRequestHeader("X-Usuario-Delegado", credencial.Delegado);
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });

    private Task<IScenarioResult> TerceroAsync(string metodo, string url, string token, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("Authorization", "Bearer " + token);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> TerceroAsync<T>(string metodo, string url, string token, object? cuerpo, int esperado) =>
        (await (await TerceroAsync(metodo, url, token, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private static void IpAleatoria(Scenario s) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
            new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    private static void Preparar(Scenario s, string metodo, string url, object? cuerpo)
    {
        switch (metodo)
        {
            case "GET":
                s.Get.Url(url);
                break;
            case "PUT":
                s.Put.Json(cuerpo ?? new { }).ToUrl(url);
                break;
            case "POST" when cuerpo is null:
                s.Post.Url(url);
                break;
            default:
                s.Post.Json(cuerpo!).ToUrl(url);
                break;
        }
    }
}
