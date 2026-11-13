using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Habeas;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Habeas data (Ley 1581 de 2012, OD-1…OD-8) con PostgreSQL y MinIO reales: consulta, corrección y supresión desde el portal del tercero;
/// plazos de 10 y 15 días hábiles con alertas; respuesta de la empresa y de la plataforma; supresión que conserva lo referenciado por
/// aceptaciones y certificados y elimina el resto; crypto-shredding de lo cifrado en eventos; constancia sellada y auditoría.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static Credencial Plataforma() => new(ApiFixture.ClavePlataforma);

    private static DateTimeOffset AlMediodia(DateOnly fecha) => new(fecha.ToDateTime(new TimeOnly(12, 0)), TimeSpan.FromHours(-5));

    /// <summary>Proveedor aprobado por Compras y Cumplimiento (RUT y Cámara referenciados por la aceptación y el certificado), con su correo.</summary>
    private async Task<(Escenario E, VinculoDto Vinculo, SesionTerceroDto Sesion, string Correo)> TerceroConAprobacionAsync()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT habeas"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara habeas"));
        await AceptarAsync(sesion, vinculo.Id);
        var inicial = await ExpedienteAbiertoAsync(e, vinculo.Id);
        await AprobarAreasAsync(e, inicial);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{inicial}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Aprobado");
        return (e, vinculo, sesion, correo);
    }

    private Task<SolicitudHabeasDto> SolicitarAsync(SesionTerceroDto sesion, string tipo, string? alcance = null, Guid? empresa = null, string? detalle = null) =>
        TerceroAsync<SolicitudHabeasDto>("POST", "/public/v1/solicitudes-habeas", sesion.AccessToken, new CrearSolicitudHabeasDto(tipo, alcance, empresa, detalle), 201);

    private DateOnly Hoy() => CalculadoraDeVigencia.Fecha(fixture.Reloj.GetUtcNow());

    private IDocumentStore Almacen() => Host.Services.GetRequiredService<IDocumentStore>();

    // ---------- a la empresa: consulta, corrección, plazos y alertas ----------

    [Fact]
    public async Task La_empresa_atiende_la_consulta_en_su_plazo_de_10_dias_habiles_con_alertas_constancia_y_auditoria()
    {
        var (e, _, sesion, correo) = await TerceroConAprobacionAsync();
        var empresaId = e.Norte.Datos.Id;

        var solicitud = await SolicitarAsync(sesion, "Consulta", empresa: empresaId, detalle: "Quiero saber qué datos suyos tiene la empresa.");
        solicitud.Estado.ShouldBe("Recibida");
        solicitud.DiasHabiles.ShouldBe(10);
        solicitud.VenceEl.ShouldBe(CalendarioHabil.SumarDiasHabiles(Hoy(), 10));
        solicitud.Radicado.ShouldStartWith("HD-");
        solicitud.Destino.EmpresaId.ShouldBe(empresaId);
        solicitud.Titular.ShouldBeNull(); // el titular no se ve a sí mismo como «titular»: eso es para quien responde
        (await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("Recibimos tu solicitud"))).Texto.ShouldContain(solicitud.Radicado);

        // Una igual en trámite no se duplica; una empresa sin vínculo no existe para el titular.
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/solicitudes-habeas", sesion.AccessToken, new CrearSolicitudHabeasDto("Consulta", null, empresaId), 409)))
            .ShouldBe("solicitud-en-curso");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/solicitudes-habeas", sesion.AccessToken, new CrearSolicitudHabeasDto("Consulta", null, Guid.NewGuid()), 404)))
            .ShouldBe("empresa-no-encontrada");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/solicitudes-habeas", sesion.AccessToken, new CrearSolicitudHabeasDto("Consejo", null, empresaId), 422)))
            .ShouldBe("tipo-invalido");

        // Quien atiende: el administrador del grupo (el oficial de cumplimiento también tiene el permiso, pero desde el SRM solo con una llave que lo incluya).
        var lista = await ApiAsync<PaginaDto<SolicitudHabeasDto>>("GET", "/api/v1/solicitudes-habeas", Admin(e.Norte), null, 200);
        var vista = lista.Datos.Single(s => s.Id == solicitud.Id);
        vista.Titular.ShouldNotBeNull().Correo.ShouldBe(correo);
        vista.Detalle.ShouldBe("Quiero saber qué datos suyos tiene la empresa.");
        (await ApiAsync<ResumenDeSolicitudesHabeasDto>("GET", "/api/v1/solicitudes-habeas/resumen", Admin(e.Norte), null, 200)).Abiertas.ShouldBe(1);

        // El oficial recibe la campana de la solicitud nueva.
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldContain(n => n.Tipo == "habeas.solicitud" && n.Mensaje.Contains(solicitud.Radicado));

        // Alerta «por vencer»: a dos días hábiles del plazo. Una sola vez.
        var dosAntes = solicitud.VenceEl;
        for (var i = 0; i < 2; i++)
        {
            do
            {
                dosAntes = dosAntes.AddDays(-1);
            } while (!CalendarioHabil.EsHabil(dosAntes));
        }

        var avisos = await ApiAsync<ResultadoDeRevisionDeHabeasDto>("POST", "/api/v1/plataforma/habeas/revisar-plazos", Plataforma(), new RevisarPlazosDeHabeasDto(AlMediodia(dosAntes)), 200);
        avisos.PorVencer.ShouldBeGreaterThanOrEqualTo(1);
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldContain(n => n.Tipo == "habeas.plazo" && n.Titulo.Contains("Vence pronto"));
        (await ApiAsync<ResultadoDeRevisionDeHabeasDto>("POST", "/api/v1/plataforma/habeas/revisar-plazos", Plataforma(), new RevisarPlazosDeHabeasDto(AlMediodia(dosAntes)), 200))
            .PorVencer.ShouldBe(0);
        var enAviso = (await ApiAsync<PaginaDto<SolicitudHabeasDto>>("GET", "/api/v1/solicitudes-habeas?estado=abiertas", Admin(e.Norte), null, 200)).Datos.Single();
        enAviso.Id.ShouldBe(solicitud.Id);

        // Respuesta dentro del plazo: constancia sellada y correo al titular.
        var respuesta = "Tenemos tu nombre, tu correo y tus documentos validados; te los enviamos por este medio.";
        var resuelta = await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/solicitudes-habeas/{solicitud.Id}/resolver", Admin(e.Norte),
            new ResolverSolicitudHabeasDto("Atender", respuesta), 200);
        (resuelta.Estado, resuelta.Resultado, resuelta.DentroDelPlazo, resuelta.Plazo).ShouldBe(("Resuelta", "Atendida", true, "Resuelta"));
        resuelta.Respuesta.ShouldBe(respuesta);
        resuelta.ConstanciaHash.ShouldNotBeNullOrEmpty();
        var correoResuelta = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("Respondimos tu solicitud"));
        correoResuelta.Texto.ShouldContain(respuesta);
        correoResuelta.Texto.ShouldContain(resuelta.ConstanciaHash!);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/solicitudes-habeas/{solicitud.Id}/resolver", Admin(e.Norte), new ResolverSolicitudHabeasDto("Atender", respuesta), 409)))
            .ShouldBe("solicitud-ya-resuelta");

        // El titular ve la respuesta y la constancia con la misma huella.
        var propia = await TerceroAsync<SolicitudHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}", sesion.AccessToken, null, 200);
        (propia.Estado, propia.Respuesta, propia.ContenidoSuprimido).ShouldBe(("Resuelta", respuesta, false));
        var constancia = await TerceroAsync<ConstanciaHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}/constancia", sesion.AccessToken, null, 200);
        constancia.Hash.ShouldBe(resuelta.ConstanciaHash);
        (constancia.Resultado, constancia.DentroDelPlazo, constancia.Destino).ShouldBe(("Atendida", true, e.Norte.Datos.Nombre));

        // Todo quedó auditado en el tenant de la empresa: la recepción por el titular y la respuesta de quien la atendió.
        await using var consulta = Almacen().QuerySession(e.Norte.Datos.TenantId.ToString("N"));
        var registros = await consulta.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "SolicitudHabeas" && r.EntidadId == solicitud.Id.ToString()).ToListAsync();
        registros.Select(r => r.Accion).ShouldBe([AccionConfiguracion.Creado, AccionConfiguracion.Modificado], ignoreOrder: true);
        registros.Single(r => r.Accion == AccionConfiguracion.Creado).ActorTipo.ShouldBe(ActorTipo.Tercero);
    }

    [Fact]
    public async Task La_solicitud_sin_responder_a_tiempo_avisa_que_vencio_y_la_correccion_exige_detalle_y_puede_rechazarse_con_su_base_legal()
    {
        var (e, _, sesion, correo) = await TerceroConAprobacionAsync();
        var empresaId = e.Norte.Datos.Id;

        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/solicitudes-habeas", sesion.AccessToken, new CrearSolicitudHabeasDto("Correccion", null, empresaId), 422)))
            .ShouldBe("detalle-requerido");
        var solicitud = await SolicitarAsync(sesion, "Correccion", empresa: empresaId, detalle: "Mi dirección de la ficha está mal: es la Calle 45 # 12-08.");
        solicitud.DiasHabiles.ShouldBe(15);
        solicitud.VenceEl.ShouldBe(CalendarioHabil.SumarDiasHabiles(Hoy(), 15));

        // Pasado el plazo sin respuesta: aviso «venció» (una vez) y la lista la marca vencida.
        var tarde = AlMediodia(solicitud.VenceEl.AddDays(2));
        (await ApiAsync<ResultadoDeRevisionDeHabeasDto>("POST", "/api/v1/plataforma/habeas/revisar-plazos", Plataforma(), new RevisarPlazosDeHabeasDto(tarde), 200))
            .Vencidas.ShouldBeGreaterThanOrEqualTo(1);
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldContain(n => n.Tipo == "habeas.plazo" && n.Titulo.Contains("Venció el plazo"));
        (await ApiAsync<ResultadoDeRevisionDeHabeasDto>("POST", "/api/v1/plataforma/habeas/revisar-plazos", Plataforma(), new RevisarPlazosDeHabeasDto(tarde), 200)).Vencidas.ShouldBe(0);

        // Responder tarde queda registrado como fuera de plazo; rechazar lleva la base legal en la respuesta.
        fixture.Reloj.Desfase = TimeSpan.FromDays(40);
        try
        {
            var rechazada = await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/solicitudes-habeas/{solicitud.Id}/resolver", Admin(e.Norte),
                new ResolverSolicitudHabeasDto("Rechazar", "No podemos modificar la dirección que respalda un certificado ya emitido.", "Art. 15 Ley 1581 de 2012."), 200);
            (rechazada.Resultado, rechazada.DentroDelPlazo).ShouldBe(("Rechazada", false));
            rechazada.Respuesta.ShouldNotBeNull().ShouldContain("Base legal: Art. 15 Ley 1581 de 2012.");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        (await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("Respondimos tu solicitud"))).Texto.ShouldContain("no pudimos atenderla");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/solicitudes-habeas/{solicitud.Id}/resolver", Admin(e.Norte), new ResolverSolicitudHabeasDto("Mañana", "Una respuesta cualquiera", null), 422)))
            .ShouldBe("decision-invalida");
    }

    [Fact]
    public async Task Cada_quien_ve_solo_lo_suyo_el_titular_no_ve_las_de_otros_y_una_empresa_no_ve_las_de_otra()
    {
        var (e, _, sesion, _) = await TerceroConAprobacionAsync();
        var (otraE, _, otraSesion, _) = await TerceroConAprobacionAsync();
        var solicitud = await SolicitarAsync(sesion, "Consulta", empresa: e.Norte.Datos.Id);
        var deOtra = await SolicitarAsync(otraSesion, "Consulta", empresa: otraE.Norte.Datos.Id);

        (await TerceroAsync<List<SolicitudHabeasDto>>("GET", "/public/v1/solicitudes-habeas", sesion.AccessToken, null, 200)).Select(s => s.Id).ShouldBe([solicitud.Id]);
        await TerceroAsync("GET", $"/public/v1/solicitudes-habeas/{deOtra.Id}", sesion.AccessToken, null, 404);
        await ApiAsync("GET", $"/api/v1/solicitudes-habeas/{deOtra.Id}", Admin(e.Norte), null, 404);
        await ApiAsync("POST", $"/api/v1/solicitudes-habeas/{deOtra.Id}/resolver", Admin(e.Norte), new ResolverSolicitudHabeasDto("Atender", "Respuesta de otra empresa"), 404);
        (await ApiAsync<PaginaDto<SolicitudHabeasDto>>("GET", "/api/v1/solicitudes-habeas", Admin(otraE.Norte), null, 200)).Datos.Select(s => s.Id).ShouldBe([deOtra.Id]);
        await TerceroAsync("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}/constancia", sesion.AccessToken, null, 409);
    }

    // ---------- a la plataforma: supresión de la bóveda ----------

    [Fact]
    public async Task La_supresion_elimina_lo_que_ninguna_empresa_referencia_conserva_lo_aceptado_y_certificado_y_destruye_la_llave_del_titular()
    {
        var (e, vinculo, sesion, correo) = await TerceroConAprobacionAsync();
        var terceroId = vinculo.TerceroId;

        // Lo que nadie referencia: un documento que ninguna empresa pide, una cuenta sin designar y una sección de la ficha.
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación bancaria sin uso"));
        await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        // Una descarga previa de su declaración PEP (con su IP y agente): es un acceso suyo al portal y se suprime con los demás (DP-19).
        await using (var escritura = Almacen().LightweightSession())
        {
            escritura.Store(new Validasoft.Aplicacion.Ficha.AccesoADeclaracionPep
            {
                Id = Guid.CreateVersion7(), TerceroId = terceroId, Seccion = "FICHA_GENERAL", Version = 1, ActorTipo = "Tercero", Ip = "10.0.0.7", UserAgent = "navegador",
                Sha256 = new string('a', 64), Fecha = DateTimeOffset.UtcNow
            });
            await escritura.SaveChangesAsync();
        }

        var solicitud = await SolicitarAsync(sesion, "Supresion", "Todo", null, "Pido que supriman todos mis datos personales.");
        solicitud.Destino.EmpresaId.ShouldBeNull();
        solicitud.Destino.Nombre.ShouldBe("Validasoft");
        solicitud.DiasHabiles.ShouldBe(15);

        // La plataforma ve la solicitud con su titular y el plan: qué se elimina y qué se conserva.
        var abiertas = await ApiAsync<PaginaDto<SolicitudHabeasDto>>("GET", "/api/v1/plataforma/solicitudes-habeas?estado=abiertas", Plataforma(), null, 200);
        var vista = abiertas.Datos.Single(s => s.Id == solicitud.Id);
        vista.Titular.ShouldNotBeNull().Correo.ShouldBe(correo);
        vista.Detalle.ShouldBe("Pido que supriman todos mis datos personales.");
        (await ApiAsync<ResumenDeSolicitudesHabeasDto>("GET", "/api/v1/plataforma/solicitudes-habeas/resumen", Plataforma(), null, 200)).Abiertas.ShouldBeGreaterThanOrEqualTo(1);

        var plan = await ApiAsync<PlanDeSupresionDto>("GET", $"/api/v1/plataforma/solicitudes-habeas/{solicitud.Id}/plan", Plataforma(), null, 200);
        plan.Elementos.Where(x => x.Tipo == "documento-version" && x.Accion == "Retener").Select(x => x.Descripcion)
            .ShouldBe(["Registro Único Tributario (versión 1)", "Certificado de existencia y representación legal (versión 1)"], ignoreOrder: true);
        plan.Elementos.Single(x => x.Descripcion.StartsWith("Certificación bancaria")).Accion.ShouldBe("Suprimir");
        plan.Elementos.Single(x => x.Tipo == "cuenta-bancaria").Accion.ShouldBe("Suprimir");
        plan.Elementos.Single(x => x.Tipo == "seccion-ficha").Accion.ShouldBe("Suprimir");
        plan.Elementos.Single(x => x.Tipo == "contactos").Accion.ShouldBe("Anonimizar");
        var retenido = plan.Elementos.First(x => x.Accion == "Retener");
        retenido.BaseLegal.ShouldNotBeNull().ShouldContain("certificado");
        retenido.RetenidaHasta.ShouldNotBeNull().ShouldBeGreaterThan(Hoy().AddYears(9));
        (plan.Suprimir, plan.Retener).ShouldBe((4, 2)); // certificación, cuenta, sección y accesos / RUT y Cámara

        // Un plan solo existe para una supresión (la consulta se atiende antes: con otra solicitud abierta la llave del titular no se destruye).
        var consulta = await SolicitarAsync(sesion, "Consulta");
        (await CodigoAsync(await ApiAsync("GET", $"/api/v1/plataforma/solicitudes-habeas/{consulta.Id}/plan", Plataforma(), null, 409))).ShouldBe("no-es-supresion");
        (await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/plataforma/solicitudes-habeas/{consulta.Id}/resolver", Plataforma(),
            new ResolverSolicitudHabeasDto("Atender", "Te enviamos por correo un resumen de los datos que tenemos."), 200)).Resultado.ShouldBe("Atendida");

        // Antes de ejecutar el titular aún puede leer lo que escribió.
        (await TerceroAsync<SolicitudHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}", sesion.AccessToken, null, 200)).Detalle
            .ShouldBe("Pido que supriman todos mis datos personales.");
        (await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IProtectorDeDatosDelTercero>().TieneLlaveAsync(terceroId, default)).ShouldBeTrue();

        // Atender ejecuta el plan.
        var resuelta = await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/plataforma/solicitudes-habeas/{solicitud.Id}/resolver", Plataforma(),
            new ResolverSolicitudHabeasDto("Atender", "Eliminamos tus datos; conservamos solo lo que la ley nos obliga a guardar."), 200);
        (resuelta.Estado, resuelta.Resultado, resuelta.DentroDelPlazo).ShouldBe(("Resuelta", "Parcial", true));
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/plataforma/solicitudes-habeas/{solicitud.Id}/resolver", Plataforma(), new ResolverSolicitudHabeasDto("Atender", "Otra vez la misma respuesta"), 409)))
            .ShouldBe("solicitud-ya-resuelta");

        // Lo referenciado se conserva íntegro, marcado con su base legal y sin volver a compartirse; el resto desapareció.
        await using var boveda = Almacen().QuerySession("boveda");
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync();
        documentos.Select(d => d.TipoCanonico).OrderBy(c => c).ShouldBe(["CAMARA_COMERCIO", "RUT"]);
        var almacen = Host.Services.GetRequiredService<IAlmacenDeArchivos>();
        foreach (var documento in documentos)
        {
            var version = documento.VersionRequerida(1);
            version.RetenidaPorLey.ShouldBeTrue();
            version.BaseLegalDeRetencion.ShouldNotBeNull().ShouldContain("Código de Comercio");
            version.RetenidaHasta.ShouldNotBeNull();
            version.Suprimida.ShouldBeFalse();
            (await almacen.ExisteAsync(version.Bucket, version.ClaveObjeto, default)).ShouldBeTrue();
        }

        (await boveda.Query<CuentaBancaria>().CountAsync(c => c.TerceroId == terceroId)).ShouldBe(0);
        (await boveda.Query<SeccionDeFicha>().CountAsync(s => s.TerceroId == terceroId)).ShouldBe(0);
        await using var global = Almacen().QuerySession();
        (await global.Query<Validasoft.Aplicacion.Ficha.AccesoADeclaracionPep>().CountAsync(a => a.TerceroId == terceroId)).ShouldBe(0);
        var tercero = (await global.LoadAsync<Tercero>(terceroId))!;
        tercero.Contactos.ShouldBeEmpty();
        tercero.Nombre.ShouldStartWith("Tercero 9"); // sigue con vínculos y certificados: se conserva para identificar lo retenido

        // La constancia dice qué se eliminó y qué se conserva, con su base y su fecha; no lleva datos personales.
        var constancia = await TerceroAsync<ConstanciaHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}/constancia", sesion.AccessToken, null, 200);
        (constancia.Resultado, constancia.Destino).ShouldBe(("Parcial", "Validasoft"));
        constancia.Retenidos.Count.ShouldBe(2);
        constancia.Retenidos.ShouldAllBe(r => r.RetenidaHasta != null && r.BaseLegal.Contains("Código de Comercio"));
        constancia.Suprimidos.Select(s => s.Descripcion).ShouldContain(d => d.StartsWith("Certificación bancaria"));
        constancia.Anonimizados.ShouldContain("Contactos y nombre comercial");
        System.Text.Json.JsonSerializer.Serialize(constancia).ShouldNotContain(correo);
        (await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("solicitud de supresión") && m.Asunto.Contains("Respondimos"))).Texto.ShouldContain("conservando lo que la ley nos obliga a guardar");

        // Crypto-shredding: la llave se destruyó, lo cifrado en el evento (nombre, identificación, correo y detalle) ya no se lee.
        (await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IProtectorDeDatosDelTercero>().TieneLlaveAsync(terceroId, default)).ShouldBeFalse();
        var despues = await TerceroAsync<SolicitudHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{solicitud.Id}", sesion.AccessToken, null, 200);
        (despues.ContenidoSuprimido, despues.Detalle, despues.Respuesta).ShouldBe((true, null, null));
        var eventos = await boveda.Events.FetchStreamAsync(solicitud.Id);
        var recibida = eventos.Select(x => x.Data).OfType<SolicitudHabeasRecibida>().Single();
        recibida.DatosProtegidos.ShouldStartWith("vsdp1.");
        recibida.DatosProtegidos.ShouldNotContain(correo);
        (await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IProtectorDeDatosDelTercero>().RevelarAsync(recibida.DatosProtegidos, default)).ShouldBeNull();
        var resueltaEvento = eventos.Select(x => x.Data).OfType<SolicitudHabeasResuelta>().Single();
        resueltaEvento.RespuestaProtegida.ShouldStartWith("vsdp1.");
        resueltaEvento.ConstanciaHash.ShouldBe(constancia.Hash);

        // Auditoría de la plataforma.
        var registros = await global.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "SolicitudHabeas" && r.EntidadId == solicitud.Id.ToString()).ToListAsync();
        registros.Count.ShouldBe(2);
    }

    [Fact]
    public async Task La_supresion_solo_de_documentos_no_toca_la_ficha_ni_destruye_la_llave_y_rechazarla_no_borra_nada()
    {
        var (_, vinculo, sesion, _) = await TerceroConAprobacionAsync();
        var terceroId = vinculo.TerceroId;
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación bancaria"));
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);

        var soloDocumentos = await SolicitarAsync(sesion, "Supresion", "Documentos");
        var plan = await ApiAsync<PlanDeSupresionDto>("GET", $"/api/v1/plataforma/solicitudes-habeas/{soloDocumentos.Id}/plan", Plataforma(), null, 200);
        plan.Elementos.ShouldAllBe(x => x.Tipo == "documento-version");
        (plan.Suprimir, plan.Retener).ShouldBe((1, 2));

        // Rechazada: no se ejecuta nada y la llave sigue.
        var rechazada = await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/plataforma/solicitudes-habeas/{soloDocumentos.Id}/resolver", Plataforma(),
            new ResolverSolicitudHabeasDto("Rechazar", "Antes necesitamos verificar que eres el titular de esos documentos.", "Art. 12 del Decreto 1377 de 2013."), 200);
        (rechazada.Resultado, rechazada.Respuesta).ShouldBe(("Rechazada", "Antes necesitamos verificar que eres el titular de esos documentos.\n\nBase legal: Art. 12 del Decreto 1377 de 2013."));
        await using (var boveda = Almacen().QuerySession("boveda"))
            (await boveda.Query<Documento>().CountAsync(d => d.TerceroId == terceroId)).ShouldBe(3);
        (await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IProtectorDeDatosDelTercero>().TieneLlaveAsync(terceroId, default)).ShouldBeTrue();

        // Una nueva, ahora atendida: borra la certificación y deja la ficha y la llave.
        var otra = await SolicitarAsync(sesion, "Supresion", "Documentos");
        var atendida = await ApiAsync<SolicitudHabeasDto>("POST", $"/api/v1/plataforma/solicitudes-habeas/{otra.Id}/resolver", Plataforma(),
            new ResolverSolicitudHabeasDto("Atender", "Eliminamos tus documentos no referenciados."), 200);
        atendida.Resultado.ShouldBe("Parcial");
        await using (var boveda = Almacen().QuerySession("boveda"))
        {
            (await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync()).Select(d => d.TipoCanonico).OrderBy(c => c).ShouldBe(["CAMARA_COMERCIO", "RUT"]);
            (await boveda.Query<SeccionDeFicha>().CountAsync(s => s.TerceroId == terceroId)).ShouldBe(1);
        }

        (await Host.Services.CreateAsyncScope().ServiceProvider.GetRequiredService<IProtectorDeDatosDelTercero>().TieneLlaveAsync(terceroId, default)).ShouldBeTrue();
        (await TerceroAsync<SolicitudHabeasDto>("GET", $"/public/v1/solicitudes-habeas/{otra.Id}", sesion.AccessToken, null, 200)).ContenidoSuprimido.ShouldBeFalse();
    }
}
