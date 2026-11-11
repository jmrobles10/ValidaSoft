using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Novedades del tercero (NT-1…NT-4): la campana de su portal (lo suyo y nada más, por empresa), sus preferencias de correo, el
/// recordatorio de observaciones pendientes (a mano, en lote y automático, a lo sumo uno al día por expediente) y el aviso a las
/// áreas correctas cuando el tercero responde.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private const string RutaCampanaTercero = "/public/v1/notificaciones";

    private sealed record Observado(Escenario E, VinculoDto Vinculo, SesionTerceroDto Sesion, Guid Expediente, string Correo, RequisitoExpedienteDto Camara);

    /// <summary>
    /// Un proveedor de Norte con RUT y Cámara subidos y el expediente en revisión; con <paramref name="observar"/>, Cumplimiento
    /// observa la Cámara y el expediente queda devuelto al tercero.
    /// </summary>
    private async Task<Observado> ProveedorObservadoAsync(Escenario? escenario = null, string? nit = null, bool observar = true)
    {
        var e = escenario ?? await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, nit);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);
        var enRevision = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        var camara = Requisito(enRevision, "CAMARA_COMERCIO");
        if (observar)
            (await VeredictoAsync<ExpedienteDto>(e, expedienteId, camara.Id, e.Cumplimiento, e.Carlos,
                new VeredictoDto("Observado", ["ilegible"], "La cámara está cortada.", SobreVersion: 1))).Estado.ShouldBe("Observado");
        return new Observado(e, vinculo, sesion, expedienteId, correo, camara);
    }

    private async Task<BandejaNotificacionesTerceroDto> EsperarBandejaAsync(SesionTerceroDto sesion, Func<BandejaNotificacionesTerceroDto, bool> condicion,
        string consulta = "")
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var bandeja = await TerceroAsync<BandejaNotificacionesTerceroDto>("GET", RutaCampanaTercero + consulta, sesion.AccessToken, null, 200);
            if (condicion(bandeja))
                return bandeja;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La campana del tercero no recibió lo esperado (tiene: {string.Join(", ", bandeja.Datos.Select(n => n.Tipo))}).");
            await Task.Delay(200);
        }
    }

    private async Task<BandejaNotificacionesDto> EsperarCampanaDeAsync(Escenario e, Persona persona, Func<BandejaNotificacionesDto, bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var bandeja = await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, persona), null, 200);
            if (condicion(bandeja))
                return bandeja;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La campana de {persona.Usuario.Nombre} no recibió lo esperado.");
            await Task.Delay(200);
        }
    }

    private async Task<Persona> PersonaConRolAsync(Empresa empresa, string nombre, string rol)
    {
        var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
        var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(empresa.Admin),
            new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto(rol, empresa.Datos.Id)], null), 201);
        return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
    }

    // ---------- campana del tercero (NT-1) ----------

    [Fact]
    public async Task La_campana_del_tercero_avisa_la_observacion_con_su_empresa_y_solo_muestra_lo_suyo()
    {
        var c = await ProveedorObservadoAsync(nit: Nit());

        // La observación llega a su campana con la empresa (su marca), el enlace al expediente y sin revisores ni áreas.
        var bandeja = await EsperarBandejaAsync(c.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.observado"));
        var observacion = bandeja.Datos.Single(n => n.Tipo == "tercero.observado");
        observacion.Titulo.ShouldBe("Tienes observaciones por corregir");
        observacion.Mensaje.ShouldContain("Norte S.A.S.");
        observacion.Mensaje.ShouldContain("te pidió corregir 1 punto");
        observacion.Empresa.ShouldNotBeNull().Nombre.ShouldBe("Norte S.A.S.");
        observacion.Empresa!.Id.ShouldBe(c.E.Norte.Datos.Id);
        observacion.Enlace.ShouldBe($"/expediente/{c.Expediente}");
        observacion.ExpedienteId.ShouldBe(c.Expediente);
        observacion.VinculoId.ShouldBe(c.Vinculo.Id);
        observacion.LeidaEn.ShouldBeNull();
        var json = JsonSerializer.Serialize(bandeja, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var interno in new[] { c.E.Carlos.Usuario.Id.ToString(), c.E.Carlos.Usuario.Nombre, "CUMPLIMIENTO", "Cumplimiento" })
            json.ShouldNotContain(interno);

        // La invitación que lo trajo hasta aquí también está (con su vínculo).
        bandeja.Datos.ShouldContain(n => n.Tipo == "tercero.invitacion" && n.VinculoId == c.Vinculo.Id);
        bandeja.NoLeidas.ShouldBe(bandeja.Datos.Count(n => n.LeidaEn is null));

        // Otro tercero (de otra empresa) no ve nada de esto, no lo puede leer y marcar todo lo suyo no toca lo de este.
        var otra = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var correoAjeno = CorreoNuevo();
        var vinculoAjeno = await CrearVinculoAsync(otra, "CLIENTE", correoAjeno);
        var sesionAjena = await SesionAsync(correoAjeno);
        var bandejaAjena = await EsperarBandejaAsync(sesionAjena, b => b.Datos.Any(n => n.Tipo == "tercero.invitacion"));
        bandejaAjena.Datos.ShouldAllBe(n => n.VinculoId == vinculoAjeno.Id && n.Empresa!.Id == otra.Datos.Id);
        bandejaAjena.Datos.ShouldNotContain(n => n.Id == observacion.Id);
        await TerceroAsync("POST", $"{RutaCampanaTercero}/{observacion.Id}/leida", sesionAjena.AccessToken, null, 404);
        (await TerceroAsync<NotificacionesMarcadasDto>("POST", $"{RutaCampanaTercero}/leidas", sesionAjena.AccessToken, null, 200)).Marcadas
            .ShouldBe(bandejaAjena.NoLeidas);
        (await TerceroAsync<BandejaNotificacionesTerceroDto>("GET", RutaCampanaTercero, c.Sesion.AccessToken, null, 200)).Datos
            .Single(n => n.Id == observacion.Id).LeidaEn.ShouldBeNull();

        // Marcar una como leída baja el contador; marcar todas lo deja en cero; lo que no existe da 404.
        var leida = await TerceroAsync<NotificacionTerceroDto>("POST", $"{RutaCampanaTercero}/{observacion.Id}/leida", c.Sesion.AccessToken, null, 200);
        leida.LeidaEn.ShouldNotBeNull();
        (await EsperarBandejaAsync(c.Sesion, b => b.Datos.Single(n => n.Id == observacion.Id).LeidaEn is not null)).NoLeidas.ShouldBe(bandeja.NoLeidas - 1);
        var marcadas = await TerceroAsync<NotificacionesMarcadasDto>("POST", $"{RutaCampanaTercero}/leidas", c.Sesion.AccessToken, null, 200);
        marcadas.Marcadas.ShouldBe(bandeja.NoLeidas - 1);
        (await EsperarBandejaAsync(c.Sesion, b => b.NoLeidas == 0)).Datos.ShouldAllBe(n => n.LeidaEn != null);
        (await TerceroAsync<BandejaNotificacionesTerceroDto>("GET", RutaCampanaTercero + "?soloNoLeidas=true", c.Sesion.AccessToken, null, 200)).Datos.ShouldBeEmpty();
        (await CodigoAsync(await TerceroAsync("POST", $"{RutaCampanaTercero}/{Guid.NewGuid()}/leida", c.Sesion.AccessToken, null, 404)))
            .ShouldBe("notificacion-no-encontrada");
    }

    [Fact]
    public async Task Un_tercero_con_dos_correos_solo_ve_en_cada_sesion_los_vinculos_de_ese_correo()
    {
        var nit = Nit();
        var a = await ProveedorObservadoAsync(nit: nit);
        // El mismo tercero (mismo NIT) tiene otro vínculo con otra empresa, invitado a otro correo: otra sesión, otra campana.
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var correoB = CorreoNuevo();
        var vinculoB = await CrearVinculoAsync(sur, "CLIENTE", correoB, nit);
        var sesionB = await SesionAsync(correoB);

        var bandejaA = await EsperarBandejaAsync(a.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.observado"));
        bandejaA.Datos.ShouldAllBe(n => n.VinculoId == a.Vinculo.Id);
        var bandejaB = await EsperarBandejaAsync(sesionB, b => b.Datos.Any(n => n.Tipo == "tercero.invitacion"));
        bandejaB.Datos.ShouldAllBe(n => n.VinculoId == vinculoB.Id && n.Empresa!.Id == sur.Datos.Id);
        bandejaB.Datos.ShouldNotContain(n => n.Tipo == "tercero.observado");
        // Tampoco puede marcar como leída una notificación del otro vínculo.
        await TerceroAsync("POST", $"{RutaCampanaTercero}/{bandejaA.Datos[0].Id}/leida", sesionB.AccessToken, null, 404);
    }

    [Fact]
    public async Task El_resultado_la_invitacion_y_el_certificado_llegan_a_la_campana_del_tercero()
    {
        var a = await ProveedorAprobadoAsync();
        var bandeja = await EsperarBandejaAsync(a.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.resultado"));
        var resultado = bandeja.Datos.Single(n => n.Tipo == "tercero.resultado");
        resultado.Titulo.ShouldBe("Validación aprobada");
        resultado.Mensaje.ShouldContain("tu vínculo está activo y ya tienes tu certificado");
        resultado.Enlace.ShouldBe($"/expediente/{a.Expediente}");

        // La empresa lo revoca: el tercero lo ve en su campana (sin el motivo, que es interno).
        var certificado = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}/certificado", Admin(a.E.Norte), null, 200)).Actual!;
        await ApiAsync<CertificadoDto>("POST", $"/api/v1/certificados/{certificado.Folio}/revocar", Admin(a.E.Norte), new RevocarCertificadoDto("Fraude interno"), 200);
        var revocado = (await EsperarBandejaAsync(a.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.certificado"))).Datos.Single(n => n.Tipo == "tercero.certificado");
        revocado.Titulo.ShouldBe("Certificado revocado");
        revocado.Mensaje.ShouldContain(certificado.Folio);
        revocado.Mensaje.ShouldNotContain("Fraude");
        revocado.Empresa!.Nombre.ShouldBe("Norte S.A.S.");
    }

    // ---------- preferencias de correo del tercero (NT-1) ----------

    [Fact]
    public async Task El_tercero_apaga_el_correo_de_un_tipo_y_la_campana_sigue_avisando()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);

        var inicial = await TerceroAsync<PreferenciasNotificacionDto>("GET", "/public/v1/preferencias-notificacion", sesion.AccessToken, null, 200);
        inicial.TiposConCorreo.ShouldBe(["tercero.observado", "tercero.recordatorio", "tercero.resultado", "tercero.documento_por_vencer", "tercero.documento_vencido",
            "tercero.certificado", "tercero.plazo_de_cargue", "tercero.desaprobado", "tercero.reintegrado"]);
        inicial.Correo.Values.ShouldAllBe(v => v);
        (await CodigoAsync(await TerceroAsync("PUT", "/public/v1/preferencias-notificacion", sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["tercero.invitacion"] = false }), 422))).ShouldBe("tipo-notificacion-invalido");
        var guardadas = await TerceroAsync<PreferenciasNotificacionDto>("PUT", "/public/v1/preferencias-notificacion", sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["tercero.observado"] = false }), 200);
        guardadas.Correo["tercero.observado"].ShouldBeFalse();
        guardadas.Correo["tercero.recordatorio"].ShouldBeTrue();

        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);
        var camara = Requisito(await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision"), "CAMARA_COMERCIO");
        await VeredictoAsync<ExpedienteDto>(e, expedienteId, camara.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Observado", ["ilegible"], "La cámara está cortada.", SobreVersion: 1));

        // La campana avisa; el correo de observación no sale. El de recordatorio (que no apagó) sí, y llega después del otro.
        await EsperarBandejaAsync(sesion, b => b.Datos.Any(n => n.Tipo == "tercero.observado"));
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/recordar-observaciones", Admin(e.Norte), null, 200);
        await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te recuerda las observaciones"));
        await Task.Delay(1500);
        fixture.Correos.Para(correo).ShouldNotContain(m => m.Asunto.Contains("tiene observaciones sobre tu validación"));

        // Apagar el recordatorio también: el siguiente (otro día) no manda correo pero sí campana.
        await TerceroAsync<PreferenciasNotificacionDto>("PUT", "/public/v1/preferencias-notificacion", sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["tercero.recordatorio"] = false, ["tercero.observado"] = true }), 200);
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromDays(1.2);
            await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/recordar-observaciones", Admin(e.Norte), null, 200);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        await Task.Delay(1500);
        fixture.Correos.Para(correo).Count(m => m.Asunto.Contains("te recuerda las observaciones")).ShouldBe(1);
    }

    // ---------- recordatorio de observaciones pendientes (NT-2) ----------

    [Fact]
    public async Task Recordar_las_observaciones_manda_correo_y_campana_una_vez_al_dia_y_solo_con_el_expediente_devuelto()
    {
        var c = await ProveedorObservadoAsync();
        var auditor = await PersonaConRolAsync(c.E.Norte, "Ana Auditora", "Auditor");
        var ruta = $"/api/v1/expedientes/{c.Expediente}/recordar-observaciones";

        // Quien solo audita no recuerda (necesita vinculos:escribir); un expediente ajeno a la empresa no existe.
        await ApiAsync("POST", ruta, Delegado(c.E.Norte, auditor), null, 403);
        var otra = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        await ApiAsync("POST", ruta, Admin(otra), null, 404);

        var recordado = await ApiAsync<ExpedienteDto>("POST", ruta, Admin(c.E.Norte), null, 200);
        recordado.RecordatorioObservaciones.ShouldNotBeNull().Total.ShouldBe(1);
        recordado.Estado.ShouldBe("Observado");

        // El correo lleva lo que falta corregir y un enlace de acceso nuevo; nunca el área ni el revisor.
        var correo = await fixture.Correos.EsperarAsync(c.Correo, m => m.Asunto == $"Norte S.A.S. te recuerda las observaciones de tu validación {recordado.Folio}");
        correo.Texto.ShouldContain("Norte S.A.S. sigue esperando tu respuesta");
        correo.Texto.ShouldContain("tiene observaciones pendientes");
        correo.Texto.ShouldContain(c.Camara.TipoDocumento.Nombre);
        correo.Texto.ShouldContain("La cámara está cortada.");
        RegexToken().IsMatch(correo.Texto).ShouldBeTrue();
        correo.Texto.ShouldNotContain("Cumplimiento");
        correo.Texto.ShouldNotContain("Carlos");

        // Y la campana de su portal, con el enlace al expediente.
        var aviso = (await EsperarBandejaAsync(c.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.recordatorio"))).Datos.Single(n => n.Tipo == "tercero.recordatorio");
        aviso.Titulo.ShouldBe("Recordatorio: observaciones pendientes");
        aviso.Enlace.ShouldBe($"/expediente/{c.Expediente}");
        aviso.Empresa!.Nombre.ShouldBe("Norte S.A.S.");

        // A lo sumo uno por día: lo repetido hoy se rechaza y mañana se puede de nuevo.
        (await CodigoAsync(await ApiAsync("POST", ruta, Admin(c.E.Norte), null, 409))).ShouldBe("recordatorio-ya-enviado-hoy");
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{c.Expediente}/historial", Admin(c.E.Norte), null, 200);
        historial.Count(h => h.Tipo == "recordatorio_de_observaciones_enviado").ShouldBe(1);
        historial.Last(h => h.Tipo == "recordatorio_de_observaciones_enviado").ActorTipo.ShouldNotBeNull();
        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromDays(1.2);
            (await ApiAsync<ExpedienteDto>("POST", ruta, Admin(c.E.Norte), null, 200)).RecordatorioObservaciones!.Total.ShouldBe(2);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        await fixture.Correos.EsperarAsync(c.Correo, m => m.Asunto.Contains("te recuerda las observaciones"), cuantos: 2);

        // Con el tercero respondiendo, ya no hay nada que recordar.
        await SubirAsync(c.Sesion, "CAMARA_COMERCIO", Pdf("Cámara completa"));
        await EsperarAsync(c.E.Norte, c.Expediente, x => Requisito(x, "CAMARA_COMERCIO").VersionActual!.N == 2);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{c.Expediente}/reenviar", c.Sesion.AccessToken, new ReenviarExpedienteDto(), 200);
        (await CodigoAsync(await ApiAsync("POST", ruta, Admin(c.E.Norte), null, 409))).ShouldBe("expediente-no-observado");
    }

    [Fact]
    public async Task El_recordatorio_en_lote_omite_lo_que_no_aplica_y_todos_los_observados_respeta_el_limite_diario_y_el_area()
    {
        var e = await PrepararAsync();
        var a = await ProveedorObservadoAsync(e);
        var b = await ProveedorObservadoAsync(e);
        var enRevision = await ProveedorObservadoAsync(e, observar: false);
        const string ruta = "/api/v1/expedientes/recordar-observaciones";

        // Plantilla propia del recordatorio (editable en Plantillas): asunto y cabecera con variables.
        var plantilla = await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/recordatorio-observaciones/correo", Admin(e.Norte),
            new ActualizarPlantillaNotificacionDto("{{empresa}}: pendiente {{folio}}", "Hola desde {{empresa}}", null), 200);
        plantilla.Evento.ShouldBe("recordatorio-observaciones");
        plantilla.VariablesPermitidas.ShouldBe(["empresa", "folio"]);

        // Validaciones del lote.
        (await CodigoAsync(await ApiAsync("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto([]), 422))).ShouldBe("lote-vacio");
        (await CodigoAsync(await ApiAsync("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto([.. Enumerable.Range(0, 201).Select(_ => Guid.NewGuid())]), 422)))
            .ShouldBe("lote-demasiado-grande");

        // Una lista: lo observado se recuerda; lo que está en revisión y lo que no existe se omiten con su motivo.
        var primero = await ApiAsync<RecordatoriosEnviadosDto>("POST", ruta, Admin(e.Norte),
            new RecordarObservacionesDto([a.Expediente, enRevision.Expediente, Guid.NewGuid()]), 200);
        primero.Enviados.ShouldBe(1);
        primero.Omitidos.ShouldBe(2);
        primero.Detalle.Select(d => d.Resultado).ShouldBe(["Enviado", "NoObservado", "NoEncontrado"]);
        var correoA = await fixture.Correos.EsperarAsync(a.Correo, m => m.Asunto.StartsWith("Norte S.A.S.: pendiente NORTE-E-", StringComparison.Ordinal));
        correoA.Texto.ShouldStartWith("Hola desde Norte S.A.S.");
        fixture.Correos.Para(enRevision.Correo).ShouldNotContain(m => m.Asunto.Contains("recuerda"));

        // «Todos los observados» con un área por la que no pasan (una nueva, fuera del flujo): nada. Con una por la que sí pasan: solo el que
        // no se recordó hoy (b), nunca a (límite diario).
        var legal = await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(e.Norte), new CrearAreaDto("LEGAL", "Legal"), 201);
        var deLegal = await ApiAsync<RecordatoriosEnviadosDto>("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto(Todos: true, Area: legal.Id), 200);
        deLegal.Detalle.ShouldBeEmpty();
        var todos = await ApiAsync<RecordatoriosEnviadosDto>("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto(Todos: true, Area: e.Compras.Id), 200);
        todos.Enviados.ShouldBe(1);
        todos.Detalle.ShouldHaveSingleItem().ExpedienteId.ShouldBe(b.Expediente);
        await fixture.Correos.EsperarAsync(b.Correo, m => m.Asunto.StartsWith("Norte S.A.S.: pendiente NORTE-E-", StringComparison.Ordinal));
        fixture.Correos.Para(a.Correo).Count(m => m.Asunto.Contains("pendiente NORTE-E-")).ShouldBe(1);
        (await ApiAsync<RecordatoriosEnviadosDto>("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto(Todos: true), 200)).Detalle.ShouldBeEmpty();

        // Pedir por nombre un expediente ya recordado hoy lo omite, sin error.
        var repetido = await ApiAsync<RecordatoriosEnviadosDto>("POST", ruta, Admin(e.Norte), new RecordarObservacionesDto([a.Expediente]), 200);
        repetido.Enviados.ShouldBe(0);
        repetido.Detalle.ShouldHaveSingleItem().Resultado.ShouldBe("YaRecordadoHoy");
    }

    // ---------- recordatorio automático (NT-3) ----------

    [Fact]
    public async Task El_recordatorio_automatico_sale_cada_N_dias_mientras_siga_devuelto_y_se_configura_por_empresa()
    {
        var c = await ProveedorObservadoAsync();
        const string ruta = "/api/v1/configuracion/recordatorio-observaciones";
        var tenantId = c.E.Norte.Datos.TenantId;
        Task<ResultadoRevisionRecordatoriosDto> BarrerAsync(double dias) =>
            ApiAsync<ResultadoRevisionRecordatoriosDto>("POST", "/api/v1/plataforma/recordatorios-observaciones/revisar", new Credencial(ApiFixture.ClavePlataforma),
                new RevisarRecordatoriosDto(tenantId, DateTimeOffset.UtcNow.AddDays(dias)), 200);

        // Apagado por omisión: no sale nada aunque pasen los días.
        (await ApiAsync<RecordatorioAutomaticoDto>("GET", ruta, Admin(c.E.Norte), null, 200)).Activo.ShouldBeFalse();
        (await BarrerAsync(10)).Enviados.ShouldBe(0);

        // Configurarlo pide permiso, días entre 1 y 60 y queda auditado.
        var auditor = await PersonaConRolAsync(c.E.Norte, "Ana Auditora", "Auditor");
        await ApiAsync("PUT", ruta, Delegado(c.E.Norte, auditor), new ActualizarRecordatorioAutomaticoDto(true, 2), 403);
        (await CodigoAsync(await ApiAsync("PUT", ruta, Admin(c.E.Norte), new ActualizarRecordatorioAutomaticoDto(true, 0), 422))).ShouldBe("dias-invalidos");
        (await CodigoAsync(await ApiAsync("PUT", ruta, Admin(c.E.Norte), new ActualizarRecordatorioAutomaticoDto(true, 61), 422))).ShouldBe("dias-invalidos");
        var guardada = await ApiAsync<RecordatorioAutomaticoDto>("PUT", ruta, Admin(c.E.Norte), new ActualizarRecordatorioAutomaticoDto(true, 2), 200);
        guardada.Activo.ShouldBeTrue();
        guardada.CadaDias.ShouldBe(2);
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=RecordatorioDeObservaciones", Admin(c.E.Norte), null, 200))
            .Datos.ShouldHaveSingleItem().Accion.ShouldBe("Creado");

        // Con 2 días: al día siguiente no toca; a los 2 sí (y es del sistema); ese mismo día no se repite; recién recordado, no toca.
        (await BarrerAsync(1)).Enviados.ShouldBe(0);
        var primero = await BarrerAsync(2.1);
        primero.Revisados.ShouldBe(1);
        primero.Enviados.ShouldBe(1);
        (await BarrerAsync(2.1)).Enviados.ShouldBe(0);
        (await BarrerAsync(3)).Enviados.ShouldBe(0);
        await fixture.Correos.EsperarAsync(c.Correo, m => m.Asunto.Contains("te recuerda las observaciones"));
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{c.Expediente}/historial", Admin(c.E.Norte), null, 200);
        var automatico = historial.Single(h => h.Tipo == "recordatorio_de_observaciones_enviado");
        automatico.ActorTipo.ShouldBe("Sistema");
        automatico.ActorId.ShouldBe("recordatorio-automatico");
        automatico.Datos.GetProperty("automatico").GetBoolean().ShouldBeTrue();
        (await EsperarBandejaAsync(c.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.recordatorio"))).Datos.Count(n => n.Tipo == "tercero.recordatorio").ShouldBe(1);

        // Otros dos días después vuelve a salir; apagado, no.
        (await BarrerAsync(4.3)).Enviados.ShouldBe(1);
        await ApiAsync<RecordatorioAutomaticoDto>("PUT", ruta, Admin(c.E.Norte), new ActualizarRecordatorioAutomaticoDto(false, 2), 200);
        (await BarrerAsync(9)).Enviados.ShouldBe(0);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{c.Expediente}", Admin(c.E.Norte), null, 200)).RecordatorioObservaciones!.Total.ShouldBe(2);

        // Si el tercero responde, deja de recordarse aunque siga encendido.
        await ApiAsync<RecordatorioAutomaticoDto>("PUT", ruta, Admin(c.E.Norte), new ActualizarRecordatorioAutomaticoDto(true, 1), 200);
        await SubirAsync(c.Sesion, "CAMARA_COMERCIO", Pdf("Cámara completa"));
        await EsperarAsync(c.E.Norte, c.Expediente, x => Requisito(x, "CAMARA_COMERCIO").VersionActual!.N == 2);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{c.Expediente}/reenviar", c.Sesion.AccessToken, new ReenviarExpedienteDto(), 200);
        (await BarrerAsync(20)).Enviados.ShouldBe(0);
    }

    // ---------- aviso a las áreas cuando el tercero responde (NT-4) ----------

    [Fact]
    public async Task Cuando_el_tercero_responde_se_avisa_a_las_areas_que_revisan_lo_corregido_y_no_a_las_demas()
    {
        var e = await PrepararAsync();

        // A: Cumplimiento observa la Cámara, que solo él revisa. Responde ⇒ le llega a Carlos y no a Laura (Compras no revisa la Cámara).
        var a = await ProveedorObservadoAsync(e);
        await SubirAsync(a.Sesion, "CAMARA_COMERCIO", Pdf("Cámara completa"));
        await EsperarAsync(e.Norte, a.Expediente, x => Requisito(x, "CAMARA_COMERCIO").VersionActual!.N == 2);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{a.Expediente}/reenviar", a.Sesion.AccessToken, new ReenviarExpedienteDto(), 200);

        var deCarlos = await EsperarCampanaDeAsync(e, e.Carlos, x => x.Datos.Any(n => n.Tipo == "expediente.tercero_respondio" && n.ExpedienteId == a.Expediente));
        var aviso = deCarlos.Datos.Single(n => n.Tipo == "expediente.tercero_respondio" && n.ExpedienteId == a.Expediente);
        aviso.Titulo.ShouldBe("El tercero respondió");
        aviso.Enlace.ShouldBe($"/e/{e.Norte.Datos.Id}/expedientes/{a.Expediente}");
        var deLaura = await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Laura), null, 200);
        deLaura.Datos.ShouldNotContain(n => n.Tipo == "expediente.tercero_respondio" && n.ExpedienteId == a.Expediente);

        // B: Compras ya había aprobado el RUT y Cumplimiento lo observa. Al responder con un RUT nuevo, lo que Compras decidió quedó sin
        // efecto: también le llega a Laura, aunque su área no observó.
        var b = await ProveedorObservadoAsync(e, observar: false);
        var rut = Requisito(await EsperarAsync(e.Norte, b.Expediente, x => x.Estado == "EnRevision"), "RUT");
        await VeredictoAsync<ExpedienteDto>(e, b.Expediente, rut.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1));
        (await VeredictoAsync<ExpedienteDto>(e, b.Expediente, rut.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Observado", ["ilegible"], "El RUT está cortado.", SobreVersion: 1))).Estado.ShouldBe("Observado");
        await SubirAsync(b.Sesion, "RUT", Pdf("RUT completo"));
        await EsperarAsync(e.Norte, b.Expediente, x => Requisito(x, "RUT").VersionActual!.N == 2);
        await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{b.Expediente}/reenviar", b.Sesion.AccessToken, new ReenviarExpedienteDto(), 200);

        foreach (var persona in new[] { e.Laura, e.Carlos })
            (await EsperarCampanaDeAsync(e, persona, x => x.Datos.Any(n => n.Tipo == "expediente.tercero_respondio" && n.ExpedienteId == b.Expediente)))
                .Datos.Count(n => n.Tipo == "expediente.tercero_respondio" && n.ExpedienteId == b.Expediente).ShouldBe(1);
    }

    // ---------- desaprobar y reintegrar (DS-10) ----------

    private Task ApagarCorreoAsync(SesionTerceroDto sesion, string tipo) =>
        TerceroAsync("PUT", "/public/v1/preferencias-notificacion", sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { [tipo] = false }), 200);

    private Task EncenderCorreoAsync(SesionTerceroDto sesion, string tipo) =>
        TerceroAsync("PUT", "/public/v1/preferencias-notificacion", sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { [tipo] = true }), 200);

    [Fact]
    public async Task Desaprobar_un_expediente_aprobado_avisa_al_tercero_en_la_campana_y_por_correo_con_la_plantilla()
    {
        const string justificacion = "Interno: el RUT no cuadra con la Cámara.";

        // Con la plantilla de la empresa: la campana y el correo avisan, sin la justificación (es interna).
        var a = await ProveedorAprobadoAsync();
        await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/desaprobacion/correo", Admin(a.E.Norte),
            new ActualizarPlantillaNotificacionDto("{{empresa}} · revisión de {{folio}}", "Hola desde {{empresa}}", null), 200);
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{a.Expediente}/desaprobar", Admin(a.E.Norte), new DesaprobarExpedienteDto(justificacion), 200);

        var bandeja = await EsperarBandejaAsync(a.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.desaprobado"));
        var aviso = bandeja.Datos.Single(n => n.Tipo == "tercero.desaprobado");
        aviso.Titulo.ShouldBe("Tu validación volvió a revisión");
        aviso.Mensaje.ShouldContain("Norte S.A.S. volvió a revisar tu validación");
        aviso.Empresa!.Id.ShouldBe(a.E.Norte.Datos.Id);
        aviso.Enlace.ShouldBe($"/expediente/{a.Expediente}");
        aviso.ExpedienteId.ShouldBe(a.Expediente);
        aviso.LeidaEn.ShouldBeNull();
        JsonSerializer.Serialize(bandeja, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotContain("Interno");

        var correo = await fixture.Correos.EsperarAsync(a.Vinculo.CorreoTercero, m => m.Asunto.StartsWith("Norte S.A.S. · revisión de NORTE-E-", StringComparison.Ordinal));
        correo.Texto.ShouldStartWith("Hola desde Norte S.A.S.");
        correo.Texto.ShouldContain("que ya estaba aprobada");
        correo.Texto.ShouldNotContain("Interno");
        fixture.Correos.Para(a.Vinculo.CorreoTercero).Count(m => m.Asunto.Contains("revisión de NORTE-E-")).ShouldBe(1);
    }

    [Fact]
    public async Task Reintegrar_avisa_al_tercero_segun_el_modo_en_la_campana_y_por_correo_con_la_plantilla()
    {
        const string justificacion = "Interno: lo pidió gerencia.";
        var a = await ProveedorAprobadoAsync();
        var norte = a.E.Norte;
        await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/reintegro/correo", Admin(norte),
            new ActualizarPlantillaNotificacionDto("{{empresa}} · reintegro", "Hola desde {{empresa}}", null), 200);

        // Sin revalidar: la campana avisa que quedó como estaba y sale el correo con la plantilla.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desvincular", Admin(norte), new MotivoDto("Fin del contrato"), 200);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/reintegrar", Admin(norte), new ReintegrarVinculoDto(justificacion, false), 200);
        var primera = await EsperarBandejaAsync(a.Sesion, b => b.Datos.Any(n => n.Tipo == "tercero.reintegrado"));
        var sinRevalidar = primera.Datos.Single(n => n.Tipo == "tercero.reintegrado");
        sinRevalidar.Titulo.ShouldBe("Tu vínculo se reintegró");
        sinRevalidar.Mensaje.ShouldContain("sin pedirte documentos nuevos");
        sinRevalidar.Enlace.ShouldBe("/empresas");
        sinRevalidar.VinculoId.ShouldBe(a.Vinculo.Id);
        sinRevalidar.Empresa!.Id.ShouldBe(norte.Datos.Id);
        var correoSin = await fixture.Correos.EsperarAsync(a.Vinculo.CorreoTercero,
            m => m.Asunto == "Norte S.A.S. · reintegro" && m.Texto.Contains("sin pedirte documentos nuevos", StringComparison.Ordinal));
        correoSin.Texto.ShouldStartWith("Hola desde Norte S.A.S.");

        // Revalidando: renovación abierta, el aviso lleva a ella y el correo dice que hay una validación nueva. Cada correo se reconoce por
        // su texto (la cola de correo no garantiza el orden entre dos mensajes).
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desvincular", Admin(norte), new MotivoDto("Otra vez"), 200);
        var reintegrado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/reintegrar", Admin(norte), new ReintegrarVinculoDto(justificacion), 200);
        reintegrado.Estado.ShouldBe("EnValidacion");
        var renovacion = reintegrado.ExpedienteAbiertoId.ShouldNotBeNull();
        var segunda = await EsperarBandejaAsync(a.Sesion, b => b.Datos.Count(n => n.Tipo == "tercero.reintegrado") == 2);
        var conRenovacion = segunda.Datos.Single(n => n.Tipo == "tercero.reintegrado" && n.Id != sinRevalidar.Id);
        conRenovacion.Titulo.ShouldBe("Tu vínculo se reintegró: falta validar");
        conRenovacion.Mensaje.ShouldContain("abrió una nueva validación");
        conRenovacion.Enlace.ShouldBe($"/expediente/{renovacion}");
        conRenovacion.ExpedienteId.ShouldBe(renovacion);
        JsonSerializer.Serialize(segunda, new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldNotContain("Interno");

        var correoCon = await fixture.Correos.EsperarAsync(a.Vinculo.CorreoTercero,
            m => m.Asunto == "Norte S.A.S. · reintegro" && m.Texto.Contains("abrió una nueva validación (NORTE-E-", StringComparison.Ordinal));
        correoCon.Texto.ShouldStartWith("Hola desde Norte S.A.S.");
        correoCon.Texto.ShouldNotContain("Interno");
        fixture.Correos.Para(a.Vinculo.CorreoTercero).Count(m => m.Asunto == "Norte S.A.S. · reintegro").ShouldBe(2);
    }

    [Fact]
    public async Task El_correo_de_desaprobar_y_el_de_reintegrar_se_omiten_si_el_tercero_los_apago_y_salen_si_los_enciende()
    {
        // Se ejecutan los manejadores de los mensajes directamente, con un enviador propio: así se comprueba la preferencia de forma
        // determinista, sin esperar a ver si un correo «no llega» y sin depender del orden de la cola de correo.
        var a = await ProveedorAprobadoAsync();
        var correo = a.Vinculo.CorreoTercero;
        var (tenant, empresa, tercero) = (a.E.Norte.Datos.TenantId, a.E.Norte.Datos.Id, a.Vinculo.TerceroId);
        var desaprobacion = new CorreoDeDesaprobacionPendiente(correo, "Norte S.A.S.", "NORTE-E-2026-000001", tenant, empresa, tercero);
        var reintegro = new CorreoDeReintegroPendiente(correo, "Norte S.A.S.", "NORTE-E-2026-000001", ModoDeReintegro.ConRenovacion, tenant, empresa, tercero);

        using var alcance = Host.Services.CreateScope();
        var servicios = alcance.ServiceProvider;
        var enviados = new CorreosCapturados();
        async Task EnviarAsync()
        {
            await CorreoDeDesaprobacionPendienteHandler.Handle(desaprobacion, enviados, servicios.GetRequiredService<RedactorDeCorreos>(),
                servicios.GetRequiredService<PlantillasDeCorreo>(), servicios.GetRequiredService<PreferenciasDeCorreoDelTercero>(), CancellationToken.None);
            await CorreoDeReintegroPendienteHandler.Handle(reintegro, enviados, servicios.GetRequiredService<RedactorDeCorreos>(),
                servicios.GetRequiredService<PlantillasDeCorreo>(), servicios.GetRequiredService<PreferenciasDeCorreoDelTercero>(), CancellationToken.None);
        }

        // Sin tocar nada salen los dos.
        await EnviarAsync();
        enviados.Para(correo).Select(m => m.Asunto).ShouldBe(["Norte S.A.S. volvió a revisar tu validación NORTE-E-2026-000001", "Norte S.A.S. reintegró tu vínculo"]);

        // Apagados los dos, ninguno; la preferencia es por tipo, así que encender uno no enciende el otro.
        await ApagarCorreoAsync(a.Sesion, "tercero.desaprobado");
        await ApagarCorreoAsync(a.Sesion, "tercero.reintegrado");
        await EnviarAsync();
        enviados.Para(correo).Count.ShouldBe(2);

        await EncenderCorreoAsync(a.Sesion, "tercero.reintegrado");
        await EnviarAsync();
        enviados.Para(correo).Select(m => m.Asunto).ShouldBe(
            ["Norte S.A.S. volvió a revisar tu validación NORTE-E-2026-000001", "Norte S.A.S. reintegró tu vínculo", "Norte S.A.S. reintegró tu vínculo"]);
    }
}
