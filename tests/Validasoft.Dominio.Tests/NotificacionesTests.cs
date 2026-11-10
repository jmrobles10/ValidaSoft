using System.Net;
using System.Text.Json;
using JasperFx.Events;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Eventos públicos, webhooks y notificaciones (NW): mapeo de cada evento de dominio a su evento público sin datos
/// sensibles, firma HMAC con rotación, backoff, deshabilitación, destinos permitidos (SSRF) y consolidación de avisos.
/// </summary>
public class NotificacionesTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid EmpresaId = Guid.NewGuid();
    private static readonly Guid VinculoId = Guid.NewGuid();
    private static readonly Guid ExpedienteId = Guid.NewGuid();
    private static readonly Guid TerceroId = Guid.NewGuid();
    private static readonly Guid DocumentoId = Guid.NewGuid();
    private static readonly Guid RequisitoId = Guid.NewGuid();
    private static readonly Guid TipoVinculoId = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 15, 0, 0, TimeSpan.Zero);

    /// <summary>Datos que nunca deben aparecer en un evento público: revisor, correos, IP, nombres de personas.</summary>
    private const string Revisor = "usuario-revisor-7f3a";
    private const string CorreoTercero = "ana@aceros.co";
    private const string Ip = "181.55.66.77";

    // ---------- mapeo ----------

    public static TheoryData<object, string, Guid> EventosDeDominio()
    {
        var motivo = new MotivoAplicado("incompleto", "La información está incompleta.");
        return new TheoryData<object, string, Guid>
        {
            { new VinculoCreado(VinculoId, EmpresaId, TerceroId, TipoVinculoId, null, [], OrigenVinculo.Api, new ReferenciaExterna("SRM", "ADP-1"), CorreoTercero, Ahora), "vinculo.creado", VinculoId },
            { new InvitacionEnviada("correo", CorreoTercero, Ahora.AddDays(7), null, Ahora), "vinculo.invitacion_enviada", VinculoId },
            { new InvitacionAceptada(CorreoTercero, Ahora), "vinculo.invitacion_aceptada", VinculoId },
            { new VinculoActivado(ExpedienteId, Ahora), "vinculo.activado", VinculoId },
            { new VinculoBloqueado(EstadoVinculo.Activo, "Sanción", null, Ahora), "vinculo.bloqueado", VinculoId },
            { new VinculoDesbloqueado(EstadoVinculo.Activo, "Levantada", Ahora), "vinculo.desbloqueado", VinculoId },
            { new VinculoDesvinculado(EstadoVinculo.Activo, "Fin del contrato", Ahora), "vinculo.desvinculado", VinculoId },
            { new VinculoAnulado(EstadoVinculo.Invitado, "Error de captura", Ahora), "vinculo.anulado", VinculoId },
            { new ReferenciaExternaAgregada("MARCO", "M-9", Ahora), "vinculo.referencia_agregada", VinculoId },
            { new ExpedienteAbierto(ExpedienteId, EmpresaId, VinculoId, TipoExpediente.Inicial, "NORTE-E-2026-000001", TerceroId, "Aceros del Norte",
                "CO|NIT|900123456", TipoVinculoId, "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Usuario, Revisor), [Revisor], Ahora),
                "expediente.abierto", ExpedienteId },
            { new ExpedienteEnviadoARevision(Ahora), "expediente.en_revision", ExpedienteId },
            { new RevisionReanudada(Guid.NewGuid(), Ahora), "expediente.en_revision", ExpedienteId },
            { new ExpedienteObservado([Guid.NewGuid()], Ahora, [RequisitoId]), "expediente.observado", ExpedienteId },
            { new ExpedienteReenviado(OrigenReenvio.Tercero, "Adjunto el RUT", [Guid.NewGuid()], Ahora, [RequisitoId]), "expediente.reenviado", ExpedienteId },
            { new ExpedienteAprobado(Ahora), "expediente.aprobado", ExpedienteId },
            { new ExpedienteRechazado(Guid.NewGuid(), Revisor, [motivo], "Aparece en listas.", Ahora), "expediente.rechazado", ExpedienteId },
            { new VersionCompartida(2, Tenant, EmpresaId, VinculoId, OrigenComparticion.AlAceptarInvitacion, Ahora), "documento.compartido", DocumentoId },
            { new VencimientoAvisado(2, VinculoId, new DateOnly(2026, 12, 8), 30, Ahora), "documento.por_vencer", DocumentoId },
            { new VersionVencida(2, VinculoId, new DateOnly(2026, 12, 8), 5, Ahora), "documento.vencido", DocumentoId }
        };
    }

    [Theory]
    [MemberData(nameof(EventosDeDominio))]
    public async Task Cada_evento_de_dominio_produce_su_evento_publico_con_referencias_y_sin_datos_sensibles(object datos, string tipo, Guid stream)
    {
        var evento = Evento(datos, stream, stream == DocumentoId ? TenantsTecnicos() : Tenant.ToString("N"));

        var mapeado = (await new MapeadorDelProcesoDeValidacion().MapearAsync(evento, new ContextoFalso(), CancellationToken.None)).ShouldNotBeNull();

        mapeado.Tipo.ShouldBe(tipo);
        mapeado.TenantId.ShouldBe(Tenant);
        mapeado.EmpresaId.ShouldBe(EmpresaId);
        CatalogoEventosPublicos.DelProceso.Select(t => t.Tipo).ShouldContain(tipo);

        var publico = EventoPublico.Desde(evento, mapeado, Ahora);
        publico.Id.ShouldBe(evento.Id);
        publico.Cursor.ShouldBe(evento.Sequence);
        var json = JsonEventos.Cuerpo(publico.ADto());
        json.ShouldContain("\"referencias\":[{\"sistema\":\"SRM\",\"id\":\"ADP-1\"}");
        json.ShouldContain("\"vinculoId\":\"" + VinculoId);
        foreach (var prohibido in new[] { Revisor, CorreoTercero, Ip, "Laura", "@", "areasQueObservaron", "respuesta", "Aceros del Norte" })
            json.ShouldNotContain(prohibido, Case.Insensitive);
    }

    [Fact]
    public async Task Los_datos_llevan_lo_que_el_integrador_necesita()
    {
        var contexto = new ContextoFalso();
        var mapeador = new MapeadorDelProcesoDeValidacion();

        var creado = await mapeador.MapearAsync(Evento(new VinculoCreado(VinculoId, EmpresaId, TerceroId, TipoVinculoId, null, [], OrigenVinculo.Api,
            null, CorreoTercero, Ahora), VinculoId), contexto, CancellationToken.None);
        var datosCreado = (Contratos.DatosVinculoCreadoDto)creado!.Datos;
        datosCreado.TipoVinculo.ShouldBe("PROVEEDOR");
        datosCreado.Identificacion!.Numero.ShouldBe("900123456");
        datosCreado.Estado.ShouldBe("Invitado");

        var rechazado = await mapeador.MapearAsync(Evento(new ExpedienteRechazado(Guid.NewGuid(), Revisor,
            [new MotivoAplicado("listas", "Aparece en listas restrictivas.")], "Coincidencia confirmada.", Ahora), ExpedienteId), contexto, CancellationToken.None);
        var datosRechazo = (Contratos.DatosExpedienteDto)rechazado!.Datos;
        datosRechazo.Folio.ShouldBe("NORTE-E-2026-000001");
        datosRechazo.Estado.ShouldBe("Rechazado");
        datosRechazo.Motivos!.ShouldHaveSingleItem().Codigo.ShouldBe("listas");
        datosRechazo.Justificacion.ShouldBe("Coincidencia confirmada.");

        var observado = await mapeador.MapearAsync(Evento(new ExpedienteObservado([], Ahora, [RequisitoId]), ExpedienteId), contexto, CancellationToken.None);
        ((Contratos.DatosExpedienteDto)observado!.Datos).Requisitos!.ShouldHaveSingleItem().Codigo.ShouldBe("RUT");

        var aceptada = await mapeador.MapearAsync(Evento(new InvitacionAceptada(CorreoTercero, Ahora), VinculoId), contexto, CancellationToken.None);
        ((Contratos.DatosInvitacionAceptadaDto)aceptada!.Datos).Consentimientos.ShouldHaveSingleItem().Tipo.ShouldBe("AutorizacionEmpresa");

        var porVencer = await mapeador.MapearAsync(Evento(new VencimientoAvisado(2, VinculoId, new DateOnly(2026, 12, 8), 30, Ahora), DocumentoId,
            TenantsTecnicos()), contexto, CancellationToken.None);
        var datosVencer = (Contratos.DatosDocumentoDto)porVencer!.Datos;
        datosVencer.Tipo.ShouldBe("RUT");
        datosVencer.DiasRestantes.ShouldBe(30);
        datosVencer.VigenteHasta.ShouldBe(new DateOnly(2026, 12, 8));
    }

    [Fact]
    public async Task La_seccion_aceptada_es_publica_con_lo_aceptado_normalizado_para_el_erp_y_sin_soportes_ni_revisores()
    {
        // La tributaria del tercero: versión 1 (la aceptada) y una 2 posterior que no debe colarse en el evento.
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Tributaria);
        var iniciada = SeccionDeFicha.Iniciar(TerceroId, definicion, null, Ahora);
        var seccion = SeccionDeFicha.Create(iniciada);
        VersionDeSeccionRegistrada Version(int numero, string[] codigos) => new(numero,
            definicion.Esquema.Canonico(new DatosTributarios { Responsabilidades = codigos, ResponsableIca = false }), $"h{numero}", $"huella-{numero}",
            [new SoporteDeSeccion(DocumentoId, 1, "RUT")], new CompletitudDeSeccion(true, [], 1), 1, null, OrigenDeSeccion.Tercero, Revisor, Ahora);
        seccion.Apply(Version(1, ["05", "13", "48"]));
        seccion.Apply(Version(2, ["49"]));
        var contexto = new ContextoFalso { Seccion = seccion, Catalogos = FichaIntegracionErpTests.Catalogos };

        var aceptada = new SeccionAceptadaPorEmpresa(RequisitoId, CodigosDeSeccion.Tributaria, null, seccion.Id, 1, "huella-1", Ahora);
        var mapeado = (await new MapeadorDeLaFicha().MapearAsync(Evento(aceptada, ExpedienteId), contexto, CancellationToken.None)).ShouldNotBeNull();

        (mapeado.Tipo, mapeado.TenantId, mapeado.EmpresaId).ShouldBe(("seccion.aceptada", Tenant, (Guid?)EmpresaId));
        MapeadorDeLaFicha.Tipos.ShouldHaveSingleItem().Tipo.ShouldBe("seccion.aceptada");
        var datos = (Contratos.DatosSeccionAceptadaDto)mapeado.Datos;
        (datos.VinculoId, datos.TerceroId, datos.EmpresaId, datos.Seccion, datos.Periodo, datos.Version, datos.AceptadaEn, datos.ExpedienteId, datos.Folio,
            datos.Huella).ShouldBe((VinculoId, TerceroId, EmpresaId, "FICHA_TRIBUTARIA", (int?)null, 1, Ahora, ExpedienteId, "NORTE-E-2026-000001", "huella-1"));
        var tributaria = datos.Contenido.Tributaria.ShouldNotBeNull();
        tributaria.Responsabilidades.Select(r => r.Codigo).ShouldBe(["05", "13", "48"]);
        (tributaria.Calidades.ResponsableIva, tributaria.Calidades.GranContribuyente, tributaria.Calidades.DeclaranteRenta).ShouldBe(((bool?)true, (bool?)true, (bool?)true));
        datos.Contenido.General.ShouldBeNull();

        var json = JsonEventos.Cuerpo(EventoPublico.Desde(Evento(aceptada, ExpedienteId), mapeado, Ahora).ADto());
        json.ShouldContain("\"referencias\":[{\"sistema\":\"SRM\",\"id\":\"ADP-1\"}");
        json.ShouldContain("\"granContribuyente\":true");
        foreach (var prohibido in new[] { Revisor, DocumentoId.ToString(), "soportes", CorreoTercero })
            json.ShouldNotContain(prohibido, Case.Insensitive);

        // Una sección que no existe en la bóveda (o una versión que no tiene) no publica nada.
        (await new MapeadorDeLaFicha().MapearAsync(Evento(aceptada with { Version = 9 }, ExpedienteId), contexto, CancellationToken.None)).ShouldBeNull();
        (await new MapeadorDelProcesoDeValidacion().MapearAsync(Evento(aceptada, ExpedienteId), contexto, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Los_eventos_del_certificado_son_publicos_sin_contenido_ni_revocador()
    {
        var certificadoId = Guid.NewGuid();
        var emitido = new CertificadoEmitido(certificadoId, Tenant, EmpresaId, VinculoId, TerceroId, ExpedienteId, "NORTE-E-2026-000001",
            "NORTE-C-2026-000001", 1, "NORTE-C-2026-000001-1", "{}", "ab12", "sello", "vs-kid", new DateOnly(2027, 5, 1),
            "https://valida.test/verificar/NORTE-C-2026-000001", Ahora);
        var contexto = new ContextoFalso { Certificado = Certificado.Create(emitido) };
        var mapeador = new MapeadorDeCertificados();

        var publicado = await mapeador.MapearAsync(Evento(emitido, certificadoId), contexto, CancellationToken.None);
        publicado!.Tipo.ShouldBe("certificado.emitido");
        publicado.EmpresaId.ShouldBe(EmpresaId);
        var datos = (Contratos.DatosCertificadoDto)publicado.Datos;
        datos.Certificado.Folio.ShouldBe("NORTE-C-2026-000001-1");
        datos.Certificado.Estado.ShouldBe("Vigente");
        datos.ExpedienteFolio.ShouldBe("NORTE-E-2026-000001");
        datos.Referencias.ShouldHaveSingleItem().Sistema.ShouldBe("SRM");

        var revocado = await mapeador.MapearAsync(Evento(new CertificadoRevocado(1, "Documentos falsos.", OrigenRevocacion.Administrador, Revisor, Ahora),
            certificadoId), contexto, CancellationToken.None);
        revocado!.Tipo.ShouldBe("certificado.revocado");
        var datosRevocado = (Contratos.DatosCertificadoDto)revocado.Datos;
        datosRevocado.Certificado.Estado.ShouldBe("Revocado");
        datosRevocado.Motivo.ShouldBe("Documentos falsos.");
        JsonSerializer.Serialize(revocado.Datos, JsonEventos.Opciones).ShouldNotContain(Revisor);

        (await mapeador.MapearAsync(Evento(new VinculoActivado(ExpedienteId, Ahora), VinculoId), contexto, CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task Los_eventos_internos_no_son_publicos()
    {
        var mapeador = new MapeadorDelProcesoDeValidacion();
        foreach (var interno in new object[]
                 {
                     new EtapaActivada(1, [Guid.NewGuid()], Ahora), new ConsentimientosRegistrados([], Evidencia(), Ahora),
                     new DocumentoAccedido(1, Tenant, EmpresaId, VinculoId, "Usuario", Revisor, Ip, "x", "descarga", Ahora)
                 })
            (await mapeador.MapearAsync(Evento(interno, VinculoId), new ContextoFalso(), CancellationToken.None)).ShouldBeNull();
    }

    // ---------- firma ----------

    [Fact]
    public void La_firma_HMAC_se_verifica_y_falla_si_cambia_el_cuerpo_o_el_tiempo()
    {
        var secreto = FirmaDeWebhook.GenerarSecreto();
        secreto.ShouldStartWith("whsec_");
        const string cuerpo = "{\"id\":\"1\",\"tipo\":\"vinculo.creado\"}";
        var ts = Ahora.ToUnixTimeSeconds();
        var cabecera = FirmaDeWebhook.Cabecera([secreto], ts, cuerpo);

        cabecera.ShouldBe($"t={ts},v1={FirmaDeWebhook.Calcular(secreto, ts, cuerpo)}");
        FirmaDeWebhook.Calcular(secreto, ts, cuerpo).Length.ShouldBe(64);
        FirmaDeWebhook.Verificar(secreto, cabecera, cuerpo, Ahora, TimeSpan.FromMinutes(5)).ShouldBeTrue();
        FirmaDeWebhook.Verificar(secreto, cabecera, cuerpo + " ", Ahora, TimeSpan.FromMinutes(5)).ShouldBeFalse();
        FirmaDeWebhook.Verificar(FirmaDeWebhook.GenerarSecreto(), cabecera, cuerpo, Ahora, TimeSpan.FromMinutes(5)).ShouldBeFalse();
        FirmaDeWebhook.Verificar(secreto, cabecera, cuerpo, Ahora.AddMinutes(6), TimeSpan.FromMinutes(5)).ShouldBeFalse();
    }

    [Fact]
    public void Con_rotacion_la_cabecera_lleva_las_dos_firmas_hasta_que_vence_el_solapamiento()
    {
        var suscripcion = SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://srm.cliente.co/hooks", ["*"], null, null, "viejo", Ahora);
        suscripcion.RotarSecreto("nuevo", TimeSpan.FromHours(24), Ahora);

        suscripcion.SecretosVigentes(Ahora.AddHours(1)).ShouldBe(["nuevo", "viejo"]);
        suscripcion.SecretosVigentes(Ahora.AddHours(25)).ShouldBe(["nuevo"]);

        const string cuerpo = "{}";
        var ts = Ahora.ToUnixTimeSeconds();
        var cabecera = FirmaDeWebhook.Cabecera(["s-nuevo", "s-viejo"], ts, cuerpo);
        cabecera.Split(',').Count(p => p.StartsWith("v1=", StringComparison.Ordinal)).ShouldBe(2);
        FirmaDeWebhook.Verificar("s-nuevo", cabecera, cuerpo, Ahora, TimeSpan.FromMinutes(5)).ShouldBeTrue();
        FirmaDeWebhook.Verificar("s-viejo", cabecera, cuerpo, Ahora, TimeSpan.FromMinutes(5)).ShouldBeTrue();
    }

    // ---------- reintentos y deshabilitación ----------

    [Fact]
    public void El_backoff_por_defecto_es_1m_5m_30m_2h_6h_y_luego_se_rinde()
    {
        var politica = new PoliticaDeReintentos([]);
        politica.IntentosMaximos.ShouldBe(6);
        Enumerable.Range(1, 6).Select(politica.EsperaTras).ShouldBe(
            [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6), null]);
        new PoliticaDeReintentos([TimeSpan.FromSeconds(2)]).EsperaTras(2).ShouldBeNull();
    }

    [Fact]
    public void Tras_N_fallos_seguidos_la_suscripcion_se_deshabilita_una_sola_vez_y_un_exito_reinicia_la_cuenta()
    {
        var suscripcion = SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://srm.cliente.co/hooks", ["vinculo.creado"], null, null, "x", Ahora);
        suscripcion.RegistrarFallo(3, Ahora).ShouldBeFalse();
        suscripcion.RegistrarExito(Ahora);
        suscripcion.FallosSeguidos.ShouldBe(0);

        suscripcion.RegistrarFallo(3, Ahora).ShouldBeFalse();
        suscripcion.RegistrarFallo(3, Ahora).ShouldBeFalse();
        suscripcion.RegistrarFallo(3, Ahora).ShouldBeTrue();
        suscripcion.Activa.ShouldBeFalse();
        suscripcion.CausaDeshabilitacion.ShouldBe("fallos-consecutivos");
        suscripcion.RegistrarFallo(3, Ahora).ShouldBeFalse();

        suscripcion.Activar(Ahora);
        suscripcion.Activa.ShouldBeTrue();
        suscripcion.FallosSeguidos.ShouldBe(0);
    }

    [Fact]
    public void La_suscripcion_filtra_por_tipo_y_empresa()
    {
        var otra = Guid.NewGuid();
        var suscripcion = SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://x.co/h", ["vinculo.creado", "VINCULO.ACTIVADO"], [EmpresaId.ToString()], null, "x", Ahora);
        suscripcion.Acepta("vinculo.creado", EmpresaId).ShouldBeTrue();
        suscripcion.Acepta("vinculo.activado", EmpresaId).ShouldBeTrue();
        suscripcion.Acepta("vinculo.creado", otra).ShouldBeFalse();
        suscripcion.Acepta("expediente.aprobado", EmpresaId).ShouldBeFalse();

        var todo = SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://x.co/h", ["*"], null, null, "x", Ahora);
        todo.Acepta("cualquier.cosa", otra).ShouldBeTrue();
        Should.Throw<ReglaDeNegocioException>(() => SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://x.co/h", [], null, null, "x", Ahora))
            .Codigo.ShouldBe("tipos-requeridos");
    }

    [Fact]
    public void La_entrega_tiene_id_determinista_y_registra_cada_intento()
    {
        var suscripcion = SuscripcionWebhook.Crear(Guid.NewGuid(), "SRM", "https://x.co/h", ["*"], null, null, "x", Ahora);
        var eventoId = Guid.NewGuid();
        var entrega = EntregaWebhook.Nueva(suscripcion, eventoId, "vinculo.creado", 42, EmpresaId, Ahora);
        entrega.Id.ShouldBe(EntregaWebhook.IdPara(eventoId, suscripcion.Id));

        entrega.Registrar(new IntentoDeEntrega(1, Guid.NewGuid(), Ahora, 500, 12, false, "HTTP 500", false), Ahora.AddMinutes(1));
        entrega.Estado.ShouldBe(EstadoEntrega.Reintentando);
        entrega.ProximoIntentoEn.ShouldBe(Ahora.AddMinutes(1));
        entrega.Registrar(new IntentoDeEntrega(2, Guid.NewGuid(), Ahora, 200, 8, true, null, false), null);
        entrega.Estado.ShouldBe(EstadoEntrega.Entregada);
        entrega.Intentos.Count.ShouldBe(2);
    }

    // ---------- destinos (SSRF) ----------

    [Theory]
    [InlineData("https://srm.cliente.co/webhooks/validasoft")]
    [InlineData("https://8.8.8.8/hook")]
    [InlineData("https://[2606:4700:4700::1111]/hook")]
    public void Acepta_destinos_https_publicos(string url) =>
        DestinoDeWebhook.Validar(url, permitirHttpLocal: false, permitirPrivados: false).ShouldBe(new Uri(url).AbsoluteUri);

    [Theory]
    [InlineData("http://srm.cliente.co/hook", "url-no-permitida")]
    [InlineData("https://10.0.0.5/hook", "url-no-permitida")]
    [InlineData("https://169.254.169.254/latest/meta-data", "url-no-permitida")]
    [InlineData("https://192.168.1.10/hook", "url-no-permitida")]
    [InlineData("https://127.0.0.1/hook", "url-no-permitida")]
    [InlineData("https://localhost/hook", "url-no-permitida")]
    [InlineData("https://[fd00::1]/hook", "url-no-permitida")]
    [InlineData("https://[::ffff:10.1.2.3]/hook", "url-no-permitida")]
    [InlineData("ftp://srm.cliente.co/hook", "url-no-permitida")]
    [InlineData("https://usuario:clave@srm.cliente.co/hook", "url-invalida")]
    [InlineData("/relativa", "url-invalida")]
    public void Rechaza_destinos_inseguros(string url, string codigo) =>
        Should.Throw<ReglaDeNegocioException>(() => DestinoDeWebhook.Validar(url, permitirHttpLocal: false, permitirPrivados: false)).Codigo.ShouldBe(codigo);

    [Fact]
    public void En_desarrollo_admite_http_hacia_localhost_pero_no_a_otras_redes_privadas()
    {
        DestinoDeWebhook.Validar("http://127.0.0.1:5099/hook", permitirHttpLocal: true, permitirPrivados: false).ShouldBe("http://127.0.0.1:5099/hook");
        DestinoDeWebhook.Validar("http://localhost:5099/hook", permitirHttpLocal: true, permitirPrivados: false).ShouldBe("http://localhost:5099/hook");
        Should.Throw<ReglaDeNegocioException>(() => DestinoDeWebhook.Validar("https://10.0.0.5/hook", permitirHttpLocal: true, permitirPrivados: false));
    }

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("1.1.1.1", true)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.20.0.1", false)]
    [InlineData("100.64.0.9", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("::ffff:192.168.0.1", false)]
    [InlineData("64:ff9b::a00:1", false)]
    [InlineData("2001:db8::1", false)]
    [InlineData("2606:4700:4700::1111", true)]
    public void Clasifica_direcciones_publicas_y_privadas(string ip, bool publica) =>
        DireccionesDeRed.EsPublica(IPAddress.Parse(ip)).ShouldBe(publica);

    // ---------- notificaciones a personas ----------

    [Fact]
    public async Task Nuevo_expediente_en_el_area_notifica_una_vez_por_persona_y_consolida_el_correo()
    {
        var contexto = new ContextoFalso();
        var etapa = Evento(new EtapaActivada(1, [contexto.AreaA.Id, contexto.AreaB.Id], Ahora), ExpedienteId);

        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(etapa, contexto, 1, CancellationToken.None);

        // Laura está en las dos áreas, Pedro solo en A; Inactivo no recibe nada; Ajeno no tiene rol en la empresa.
        plan.Campana.Select(c => c.Notificacion.UsuarioId).ShouldBe([contexto.Laura.Id, contexto.Pedro.Id], ignoreOrder: true);
        plan.Campana.ShouldAllBe(c => c.Notificacion.Tipo == TiposNotificacion.NuevoEnArea && c.Notificacion.Titulo == "Nuevo expediente en tu área");
        plan.Campana.ShouldAllBe(c => c.Notificacion.Enlace == $"/e/{EmpresaId}/expedientes/{ExpedienteId}");

        // Pedro apagó el correo de este tipo; Laura recibe uno solo que nombra sus dos áreas.
        var correo = plan.Correos.Cast<CorreoDeNotificacionPendiente>().ShouldHaveSingleItem();
        correo.Para.ShouldBe(contexto.Laura.Correo);
        correo.Lineas.ShouldContain("Tus áreas: Aprobador inicial, Cumplimiento.");
        correo.Asunto.ShouldBe("Nuevo expediente en tu área: NORTE-E-2026-000001");
        correo.Lineas.ShouldContain("Expediente NORTE-E-2026-000001 de Norte S.A.S."); // sin «S.A.S..»

        // Reprocesar el mismo evento produce los mismos ids (no duplica).
        var otraVez = await new PlanificadorDeNotificaciones().PlanearAsync(etapa, contexto, 1, CancellationToken.None);
        otraVez.Campana.Select(c => c.Notificacion.Id).ShouldBe(plan.Campana.Select(c => c.Notificacion.Id));
    }

    [Fact]
    public async Task El_documento_vencido_avisa_al_tercero_por_correo_y_a_las_areas_en_la_campana()
    {
        var contexto = new ContextoFalso();
        var vencida = Evento(new VersionVencida(2, VinculoId, new DateOnly(2026, 11, 1), 5, Ahora), DocumentoId, TenantsTecnicos());

        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(vencida, contexto, 1, CancellationToken.None);

        var alTercero = plan.Correos.OfType<CorreoDeVencidoPendiente>().ShouldHaveSingleItem();
        alTercero.Para.ShouldBe(CorreoTercero);
        alTercero.Empresa.ShouldBe("Norte S.A.S.");
        alTercero.VigenteHasta.ShouldBe(new DateOnly(2026, 11, 1));
        plan.Campana.ShouldNotBeEmpty();
        plan.Campana.ShouldAllBe(c => c.Notificacion.Tipo == TiposNotificacion.Vencido && c.Notificacion.Titulo == "Documento vencido");

        var mensaje = new Aplicacion.Comun.RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).RedactarVencido(alTercero);
        mensaje.Asunto.ShouldBe($"Tu {alTercero.Documento} venció para Norte S.A.S.");
        mensaje.Texto.ShouldContain("vigente hasta el 1 de noviembre de 2026");
    }

    [Fact]
    public async Task Revocar_o_suspender_el_certificado_avisa_al_tercero_sin_el_motivo()
    {
        var certificadoId = Guid.NewGuid();
        var emitido = new CertificadoEmitido(certificadoId, Tenant, EmpresaId, VinculoId, TerceroId, ExpedienteId, "NORTE-E-2026-000001",
            "NORTE-C-2026-000001", 1, "NORTE-C-2026-000001-1", "{}", "ab12", "sello", "vs-kid", null, "https://valida.test/verificar/x", Ahora);
        var contexto = new ContextoFalso { Certificado = Certificado.Create(emitido) };
        var planificador = new PlanificadorDeNotificaciones();

        var revocado = await planificador.PlanearAsync(Evento(new CertificadoRevocado(1, "Documento adulterado.", OrigenRevocacion.Administrador,
            Revisor, Ahora), certificadoId), contexto, 1, CancellationToken.None);
        var correo = revocado.Correos.OfType<CorreoDeCertificadoPendiente>().ShouldHaveSingleItem();
        correo.Para.ShouldBe(CorreoTercero);
        correo.Folio.ShouldBe("NORTE-C-2026-000001-1");
        correo.Revocado.ShouldBeTrue();
        var mensaje = new Aplicacion.Comun.RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).RedactarCertificado(correo);
        mensaje.Asunto.ShouldBe("Norte S.A.S. revocó tu certificado NORTE-C-2026-000001-1");
        mensaje.Texto.ShouldNotContain("adulterado");

        var suspendido = await planificador.PlanearAsync(Evento(new CertificadoSuspendido(1, "Lista restrictiva.", EstadoCertificado.Vigente, Ahora),
            certificadoId), contexto, 1, CancellationToken.None);
        suspendido.Correos.OfType<CorreoDeCertificadoPendiente>().ShouldHaveSingleItem().Revocado.ShouldBeFalse();
    }

    [Fact]
    public async Task La_constancia_al_tercero_lista_los_textos_aceptados_sin_la_ip()
    {
        var contexto = new ContextoFalso();
        var consentimientos = new ConsentimientosRegistrados(
            [new ConsentimientoRegistrado(TipoTextoLegal.AutorizacionEmpresa, contexto.Texto.Id, 3, contexto.Texto.Hash)], Evidencia(), Ahora);

        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(Evento(consentimientos, VinculoId), contexto, 1, CancellationToken.None);

        plan.Campana.ShouldBeEmpty();
        var correo = plan.Correos.Cast<CorreoDeConstanciaPendiente>().ShouldHaveSingleItem();
        correo.Para.ShouldBe(CorreoTercero);
        correo.Empresa.ShouldBe("Norte S.A.S.");
        var texto = correo.Textos.ShouldHaveSingleItem();
        texto.Titulo.ShouldBe("Autorización de tratamiento de datos de Norte S.A.S.");
        texto.Version.ShouldBe(3);

        var mensaje = new Aplicacion.Comun.RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()).RedactarConstancia(correo);
        mensaje.Texto.ShouldContain("versión 3");
        mensaje.Texto.ShouldNotContain(Ip);
    }

    // ---------- utilidades ----------

    private static string TenantsTecnicos() => Aplicacion.Comun.TenantsTecnicos.Boveda;

    private static EvidenciaDeConsentimiento Evidencia() => new(TerceroId, CorreoTercero, Ip, "Mozilla", true, CanalAceptacion.Web);

    private static IEvent Evento(object datos, Guid stream, string? tenant = null)
    {
        var tipo = typeof(Event<>).MakeGenericType(datos.GetType());
        var evento = (IEvent)Activator.CreateInstance(tipo, datos)!;
        evento.StreamId = stream;
        evento.TenantId = tenant ?? Tenant.ToString("N");
        evento.Id = Guid.NewGuid();
        evento.Sequence = 4242;
        evento.Timestamp = Ahora;
        return evento;
    }

    /// <summary>Instantáneas en memoria de un vínculo con referencia del SRM, su expediente, el documento y las personas.</summary>
    private sealed class ContextoFalso : IContextoDeEventos
    {
        public Usuario Laura { get; } = Persona("Laura Aprobadora", Rol.Revisor);
        public Usuario Pedro { get; } = Persona("Pedro Aprobador", Rol.Revisor);
        public Usuario Inactivo { get; } = Persona("Inactivo", Rol.Revisor, activo: false);
        public Usuario Ajeno { get; } = Persona("Ajeno", Rol.Revisor, empresa: Guid.NewGuid());
        public Area AreaA { get; }
        public Area AreaB { get; }
        public TextoLegal Texto { get; } = new() { Id = Guid.NewGuid(), Tipo = TipoTextoLegal.AutorizacionEmpresa, Version = 3, Hash = new string('a', 64) };

        private readonly Vinculo _vinculo;
        private readonly Expediente _expediente;
        private readonly Documento _documento;

        public ContextoFalso()
        {
            AreaA = new Area { Id = Guid.NewGuid(), EmpresaId = EmpresaId, Codigo = "APROBADOR", Nombre = "Aprobador inicial", Miembros = [Laura.Id, Pedro.Id, Inactivo.Id, Ajeno.Id] };
            AreaB = new Area { Id = Guid.NewGuid(), EmpresaId = EmpresaId, Codigo = "CUMPLIMIENTO", Nombre = "Cumplimiento", Miembros = [Laura.Id] };
            _vinculo = new Vinculo
            {
                Id = VinculoId, EmpresaId = EmpresaId, TerceroId = TerceroId, TipoVinculoId = TipoVinculoId, Estado = EstadoVinculo.EnValidacion,
                Referencias = [new ReferenciaExterna("SRM", "ADP-1")], CorreoTercero = CorreoTercero, ExpedienteAbiertoId = ExpedienteId,
                Consentimientos = [new ConsentimientoRegistrado(TipoTextoLegal.AutorizacionEmpresa, Texto.Id, 3, Texto.Hash)]
            };
            _expediente = new Expediente
            {
                Id = ExpedienteId, EmpresaId = EmpresaId, VinculoId = VinculoId, Folio = "NORTE-E-2026-000001", TerceroId = TerceroId,
                TerceroNombre = "Aceros del Norte", TipoVinculoNombre = "Proveedor", AreaIds = [AreaA.Id, AreaB.Id],
                Requisitos = [new RequisitoDeExpediente { Id = RequisitoId, Codigo = "RUT", Nombre = "RUT" }]
            };
            _documento = new Documento
            {
                Id = DocumentoId, TerceroId = TerceroId, TipoCanonico = "RUT", NombreTipo = "RUT",
                Comparticiones = [new Comparticion(2, Tenant, EmpresaId, VinculoId, OrigenComparticion.AlAceptarInvitacion, Ahora)]
            };
        }

        private static Usuario Persona(string nombre, Rol rol, bool activo = true, Guid? empresa = null)
        {
            var usuario = Usuario.Crear(nombre, nombre.Split(' ')[0].ToLowerInvariant() + "@norte.co", null, Ahora);
            usuario.Roles = [new RolAsignado(rol, empresa ?? EmpresaId)];
            usuario.Activo = activo;
            return usuario;
        }

        public Task<Vinculo?> VinculoAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult(id == VinculoId && tenantId == Tenant ? _vinculo : null);

        public Task<Expediente?> ExpedienteAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(id == ExpedienteId && tenantId == Tenant ? _expediente : null);

        public Task<TipoVinculo?> TipoVinculoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult<TipoVinculo?>(new TipoVinculo { Id = id, Codigo = "PROVEEDOR" });

        public Task<TipoDocumentoTenant?> TipoDocumentoAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<TipoDocumentoTenant?>(null);

        public Task<Tercero?> TerceroAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Tercero?>(new Tercero { Id = id, Identificacion = new Identificacion("CO", "NIT", "900123456", "7"), Nombre = "Aceros del Norte" });

        public Task<Documento?> DocumentoAsync(Guid id, CancellationToken ct) => Task.FromResult(id == DocumentoId ? _documento : null);

        public SeccionDeFicha? Seccion { get; set; }

        public ICatalogosDeFicha Catalogos { get; set; } = CatalogosEnMemoria.Vacios;

        public Task<SeccionDeFicha?> SeccionDeFichaAsync(Guid id, CancellationToken ct) => Task.FromResult(Seccion?.Id == id ? Seccion : null);

        public Task<ICatalogosDeFicha> CatalogosDeFichaAsync(IEnumerable<ReferenciaACatalogo> referencias, CancellationToken ct) => Task.FromResult(Catalogos);

        public Certificado? Certificado { get; set; }

        public Task<Certificado?> CertificadoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Certificado is not null && Certificado.Id == id && tenantId == Tenant ? Certificado : null);

        public Task<Empresa?> EmpresaAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Empresa?>(new Empresa { Id = id, TenantId = Tenant, Nombre = "Norte S.A.S." });

        public Task<IReadOnlyList<Area>> AreasAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Area>>([.. new[] { AreaA, AreaB }.Where(a => ids.Contains(a.Id))]);

        public Task<IReadOnlyList<Usuario>> UsuariosAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Usuario>>([.. new[] { Laura, Pedro, Inactivo, Ajeno }.Where(u => ids.Contains(u.Id))]);

        public Task<PreferenciasNotificacion?> PreferenciasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct) =>
            Task.FromResult(usuarioId == Pedro.Id
                ? new PreferenciasNotificacion { Id = usuarioId, Correo = new() { [TiposNotificacion.NuevoEnArea] = false } }
                : null);

        public Task<IReadOnlyList<TextoLegal>> TextosLegalesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TextoLegal>>(ids.Contains(Texto.Id) ? [Texto] : []);

        public Task<IReadOnlyList<SuscripcionWebhook>> SuscripcionesActivasAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuscripcionWebhook>>([]);

        public Task<IReadOnlySet<Guid>> ExistentesAsync<T>(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) where T : notnull =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }
}
