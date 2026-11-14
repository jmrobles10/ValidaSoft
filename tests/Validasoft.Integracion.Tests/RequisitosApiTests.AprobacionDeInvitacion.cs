using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Aprobación antes de invitar (decisión AI, equivale al estado -5 del SRM) con PostgreSQL y la mensajería real: con el tipo de vínculo en «requiere aprobación para invitar» la
/// invitación queda retenida (sin correo, enlace ni vista del tercero) hasta que otra persona del área o rol designado la apruebe (sale la invitación de siempre) o la rechace con
/// un motivo (el vínculo queda anulado); los lotes se deciden completos; la API por llave respeta la misma regla y los eventos públicos y la campana reflejan cada paso.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static readonly AprobadoresDeInvitacionDto ApruebaCumplimiento = new("Area", "CUMPLIMIENTO");

    /// <summary>
    /// El escenario de siempre, con la llave del SRM además con <c>vinculos:escribir</c> (la de las demás pruebas solo lee): una persona actúa con la intersección de lo que da su
    /// rol y lo que permite la llave, y crear o aprobar invitaciones necesita ese permiso.
    /// </summary>
    private async Task<Escenario> PrepararConEscrituraAsync()
    {
        var e = await PrepararAsync();
        var sistemas = await ApiAsync<List<SistemaIntegradoDto>>("GET", "/api/v1/sistemas", new Credencial(e.Norte.Admin), null, 200);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Norte.Admin),
            new CrearApiKeyDto("Integración SRM con escritura", [.. ScopesDeSistema, "vinculos:escribir"], null, null, SistemaId: sistemas.Single(s => s.Codigo == "SRM").Id), 201);
        return e with { Norte = e.Norte with { LlaveSrm = llave.Valor! } };
    }

    /// <summary>Configura el tipo para exigir (o no) la aprobación previa a invitar, conservando lo demás de su configuración.</summary>
    private async Task ExigirAprobacionDeInvitacionAsync(Empresa empresa, string codigo, AprobadoresDeInvitacionDto? aprobadores)
    {
        var tipos = await ApiAsync<List<TipoVinculoDto>>("GET", "/api/v1/tipos-vinculo", new Credencial(empresa.Admin), null, 200);
        var tipo = tipos.Single(t => t.Codigo == codigo);
        var actualizado = await ApiAsync<TipoVinculoDto>("PUT", $"/api/v1/tipos-vinculo/{tipo.Id}", new Credencial(empresa.Admin),
            new CrearTipoVinculoDto(tipo.Codigo, tipo.Nombre, tipo.AtributosPermitidos, tipo.ExigeCuentaDesignada, tipo.ExigeOtpEnEnlace, aprobadores is not null, aprobadores), 200);
        actualizado.RequiereAprobacionParaInvitar.ShouldBe(aprobadores is not null);
    }

    private Task<VinculoDto> CrearVinculoComoAsync(Credencial credencial, string tipo, string correo, bool enviarInvitacion = true, string? nit = null)
    {
        nit ??= Nit();
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", credencial, new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null, [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null,
            EnviarInvitacion: enviarInvitacion), 201);
    }

    private async Task<EventoPublico> EsperarEventoPublicoAsync(Empresa empresa, string tipo, Func<EventoPublico, bool>? condicion = null)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        var empresaId = empresa.Datos.Id;
        while (true)
        {
            await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
            var eventos = await consulta.Query<EventoPublico>().Where(x => x.EmpresaId == empresaId && x.Tipo == tipo).ToListAsync();
            if (eventos.FirstOrDefault(x => condicion is null || condicion(x)) is { } encontrado)
                return encontrado;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No se registró el evento público {tipo}.");
            await Task.Delay(200);
        }
    }

    private async Task<NotificacionDto> EsperarNotificacionAsync(Func<Credencial> persona, string tipo, Func<NotificacionDto, bool>? condicion = null)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var bandeja = await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", persona(), null, 200);
            if (bandeja.Datos.FirstOrDefault(n => n.Tipo == tipo && (condicion is null || condicion(n))) is { } encontrada)
                return encontrada;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No llegó la notificación {tipo}.");
            await Task.Delay(200);
        }
    }

    private async Task<bool> TieneNotificacionAsync(Func<Credencial> persona, string tipo) =>
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", persona(), null, 200)).Datos.Any(n => n.Tipo == tipo);

    /// <summary>Otra persona del área de Cumplimiento de Norte (para decidir lo que pidió o decidió Carlos).</summary>
    private async Task<Persona> OtroDeCumplimientoAsync(Escenario e, string nombre)
    {
        var persona = await OficialAsync(e, nombre);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{e.Cumplimiento.Id}/miembros", Admin(e.Norte), new MiembrosAreaDto([e.Carlos.Usuario.Id, persona.Usuario.Id]), 200);
        return persona;
    }

    /// <summary>La sesión que recibiría el tercero con ese correo (los vínculos que se le muestran), como la calcula el canje del enlace y cada renovación.</summary>
    private async Task<SesionTerceroDto> SesionDelTerceroAsync(Guid terceroId, string correo)
    {
        using var alcance = Host.Services.CreateScope();
        var sesiones = alcance.ServiceProvider.GetRequiredService<ISesiones>();
        await using var consulta = sesiones.ConsultaGlobal();
        return await EnlacesDeAcceso.EmitirAccesoAsync(sesiones, consulta, alcance.ServiceProvider.GetRequiredService<IEmisorDeSesionesTercero>(), terceroId, correo, true,
            DateTimeOffset.UtcNow, Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task Con_aprobacion_previa_la_invitacion_queda_retenida_y_solo_sale_cuando_otra_persona_del_area_la_aprueba()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        Credencial carlos() => Delegado(e.Norte, e.Carlos);
        var correo = CorreoNuevo();

        // Laura (Compras) crea el vínculo: nace Invitado, pero la invitación queda retenida; no hay enlace ni se le avisa a nadie del tercero.
        var retenido = await CrearVinculoComoAsync(laura(), "PROVEEDOR", correo);
        retenido.Estado.ShouldBe("Invitado");
        retenido.Invitacion.ShouldBeNull();
        var pendiente = retenido.InvitacionPorAprobar.ShouldNotBeNull();
        (pendiente.Estado, pendiente.CorreoDestino, pendiente.VigenciaHoras).ShouldBe(("Pendiente", correo, 72));
        pendiente.SolicitadaPor.ShouldBe($"Usuario:{e.Laura.Usuario.Id}");
        pendiente.SolicitadaPorNombre.ShouldBe("Laura Compras");
        pendiente.PuedesDecidir.ShouldBe(false);

        var eventos = await EventosAsync(e.Norte.Datos.TenantId, retenido.Id);
        eventos.OfType<InvitacionPorAprobarSolicitada>().ShouldHaveSingleItem().SolicitadaPor.Id.ShouldBe(e.Laura.Usuario.Id.ToString());
        eventos.OfType<InvitacionEnviada>().ShouldBeEmpty();

        // El evento público lo refleja (sin el correo) y la campana avisa a quien aprueba —no a quien pidió—.
        var publico = await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_por_aprobar");
        publico.Datos.GetRawText().ShouldNotContain(correo);
        publico.Datos.GetProperty("vigenciaHoras").GetInt32().ShouldBe(72);
        var aviso = await EsperarNotificacionAsync(carlos, "invitaciones.por_aprobar");
        aviso.Enlace.ShouldBe($"/e/{e.Norte.Datos.Id}/invitaciones/por-aprobar");
        aviso.Mensaje.ShouldContain("Proveedor");
        (await TieneNotificacionAsync(laura, "invitaciones.por_aprobar")).ShouldBeFalse();
        fixture.Correos.Para(correo).ShouldBeEmpty();

        // Mientras está retenida nadie la envía ni abre un expediente, y el tercero no ve el vínculo.
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{retenido.Id}/invitaciones", laura(), new InvitacionDto(null), 409))).ShouldBe("invitacion-por-aprobar");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{retenido.Id}/expedientes", Admin(e.Norte), new { }, 409))).ShouldBe("invitacion-por-aprobar");
        (await SesionDelTerceroAsync(retenido.TerceroId, correo)).Vinculos.ShouldBeEmpty();

        // La bandeja: Carlos la decide; Laura la ve, pero no la decide (y con soloLasMias no aparece).
        var deCarlos = await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar?soloLasMias=true", carlos(), null, 200);
        var item = deCarlos.Datos.ShouldHaveSingleItem();
        (item.VinculoId, item.PuedesDecidir, item.SolicitadaPorNombre, item.CorreoDestino).ShouldBe((retenido.Id, true, "Laura Compras", correo));
        item.TipoVinculo.Codigo.ShouldBe("PROVEEDOR");
        item.TerceroIdentificacion.Numero.ShouldNotBeNullOrEmpty();
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar?soloLasMias=true", laura(), null, 200)).Datos.ShouldBeEmpty();
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar", laura(), null, 200)).Datos.ShouldHaveSingleItem().PuedesDecidir.ShouldBeFalse();
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{retenido.Id}", carlos(), null, 200)).InvitacionPorAprobar!.PuedesDecidir.ShouldBe(true);

        // Quién decide: una persona del área (Laura es de Compras) y no una llave de integración.
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{retenido.Id}/invitacion/aprobar", laura(), new { }, 403))).ShouldBe("no-es-aprobador-de-invitaciones");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{retenido.Id}/invitacion/aprobar", Admin(e.Norte), new { }, 422))).ShouldBe("usuario-delegado-requerido");
        fixture.Correos.Para(correo).ShouldBeEmpty();

        // Carlos la aprueba: sale la invitación de siempre.
        var aprobado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{retenido.Id}/invitacion/aprobar", carlos(), new { }, 200);
        aprobado.Estado.ShouldBe("Invitado");
        aprobado.Invitacion.ShouldNotBeNull().CorreoDestino.ShouldBe(correo);
        var decidida = aprobado.InvitacionPorAprobar.ShouldNotBeNull();
        (decidida.Estado, decidida.DecididaPorNombre, decidida.PuedesDecidir).ShouldBe(("Aprobada", "Carlos Cumplimiento", null));
        var invitacion = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"));
        invitacion.Texto.ShouldNotBeNullOrEmpty();
        (await SesionDelTerceroAsync(retenido.TerceroId, correo)).Vinculos.ShouldHaveSingleItem().Id.ShouldBe(retenido.Id);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{retenido.Id}/invitacion/aprobar", carlos(), new { }, 409))).ShouldBe("sin-invitacion-por-aprobar");
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar", carlos(), null, 200)).Datos.ShouldBeEmpty();

        // Sus eventos, el aviso a quien la pidió y la auditoría lo registran; el tercero acepta como en cualquier otra invitación.
        var eventosFinales = await EventosAsync(e.Norte.Datos.TenantId, retenido.Id);
        eventosFinales.OfType<InvitacionAprobada>().ShouldHaveSingleItem().AprobadaPor.Id.ShouldBe(e.Carlos.Usuario.Id.ToString());
        eventosFinales.OfType<InvitacionEnviada>().ShouldHaveSingleItem();
        (await EsperarNotificacionAsync(laura, "invitaciones.decidida")).Titulo.ShouldBe("Invitación aprobada");
        await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_aprobada");
        await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_enviada");
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{retenido.Id}/historial", carlos(), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("invitacion_por_aprobar_solicitada");
        historial.Select(h => h.Tipo).ShouldContain("invitacion_aprobada");
        historial.Single(h => h.Tipo == "invitacion_aprobada").ActorId.ShouldBe(e.Carlos.Usuario.Id.ToString());
        var auditoria = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?entidad=InvitacionDeVinculo&entidadId={retenido.Id}",
            new Credencial(e.Norte.Admin, e.Norte.Datos.Id), null, 200);
        auditoria.Datos.Select(a => a.Accion).Order().ShouldBe(["Creado", "Modificado"]);
        auditoria.Datos.Single(a => a.Accion == "Modificado").Actor.Id.ShouldBe(e.Carlos.Usuario.Id.ToString());
        await AceptarAsync(await SesionAsync(correo), retenido.Id);
    }

    [Fact]
    public async Task Quien_pidio_la_invitacion_no_la_decide_aunque_sea_del_area_y_otra_persona_del_area_si()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        var olga = await OtroDeCumplimientoAsync(e, "Olga Cumplimiento");
        Credencial carlos() => Delegado(e.Norte, e.Carlos);

        // Carlos mismo pide la invitación: es de Cumplimiento, pero no puede decidir lo que pidió.
        var vinculo = await CrearVinculoComoAsync(carlos(), "PROVEEDOR", CorreoNuevo());
        vinculo.InvitacionPorAprobar!.PuedesDecidir.ShouldBe(false);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", carlos(), new { }, 403))).ShouldBe("separacion-de-funciones");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/rechazar", carlos(), new MotivoDto("No la quiero"), 403))).ShouldBe("separacion-de-funciones");
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar?soloLasMias=true", carlos(), null, 200)).Datos.ShouldBeEmpty();
        (await EventosAsync(e.Norte.Datos.TenantId, vinculo.Id)).OfType<InvitacionAprobada>().ShouldBeEmpty();

        // Otra persona de Cumplimiento sí.
        Credencial delegadaOlga() => Delegado(e.Norte, olga);
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar?soloLasMias=true", delegadaOlga(), null, 200)).Datos
            .ShouldHaveSingleItem().PuedesDecidir.ShouldBeTrue();
        (await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", delegadaOlga(), new { }, 200)).InvitacionPorAprobar!.Estado.ShouldBe("Aprobada");
    }

    [Fact]
    public async Task Rechazar_la_invitacion_anula_el_vinculo_con_el_motivo_y_nunca_sale_el_correo()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        Credencial carlos() => Delegado(e.Norte, e.Carlos);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoComoAsync(laura(), "PROVEEDOR", correo);

        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/rechazar", carlos(), new MotivoDto("  "), 422))).ShouldBe("motivo-requerido");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/rechazar", laura(), new MotivoDto("No procede"), 403))).ShouldBe("no-es-aprobador-de-invitaciones");

        var rechazado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/rechazar", carlos(), new MotivoDto("El proveedor no está autorizado"), 200);
        rechazado.Estado.ShouldBe("Anulado");
        rechazado.Invitacion.ShouldBeNull();
        var decision = rechazado.InvitacionPorAprobar.ShouldNotBeNull();
        (decision.Estado, decision.MotivoDeRechazo, decision.DecididaPorNombre).ShouldBe(("Rechazada", "El proveedor no está autorizado", "Carlos Cumplimiento"));

        var eventos = await EventosAsync(e.Norte.Datos.TenantId, vinculo.Id);
        eventos.OfType<InvitacionRechazada>().ShouldHaveSingleItem().Motivo.ShouldBe("El proveedor no está autorizado");
        eventos.OfType<VinculoAnulado>().ShouldHaveSingleItem().Motivo.ShouldBe("El proveedor no está autorizado");
        eventos.OfType<InvitacionEnviada>().ShouldBeEmpty();

        // Quien la pidió lo sabe en su campana, con el motivo; el integrador, por los eventos públicos; y ya no está en la bandeja.
        var aviso = await EsperarNotificacionAsync(laura, "invitaciones.decidida");
        (aviso.Titulo, aviso.Mensaje.Contains("El proveedor no está autorizado")).ShouldBe(("Invitación rechazada", true));
        var publico = await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_rechazada");
        publico.Datos.GetProperty("motivo").GetString().ShouldBe("El proveedor no está autorizado");
        await EsperarEventoPublicoAsync(e.Norte, "vinculo.anulado");
        (await ApiAsync<PaginaDto<InvitacionPorAprobarItemDto>>("GET", "/api/v1/invitaciones/por-aprobar", carlos(), null, 200)).Datos.ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", carlos(), new { }, 409))).ShouldBe("sin-invitacion-por-aprobar");
        await Task.Delay(500);
        fixture.Correos.Para(correo).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sin_alguien_que_pueda_aprobar_no_se_retiene_la_invitacion_y_la_configuracion_se_valida()
    {
        var e = await PrepararConEscrituraAsync();
        var tipos = await ApiAsync<List<TipoVinculoDto>>("GET", "/api/v1/tipos-vinculo", new Credencial(e.Norte.Admin), null, 200);
        var proveedor = tipos.Single(t => t.Codigo == "PROVEEDOR");
        async Task<IScenarioResult> Guardar(bool requiere, AprobadoresDeInvitacionDto? aprobadores, int esperado) =>
            await ApiAsync("PUT", $"/api/v1/tipos-vinculo/{proveedor.Id}", new Credencial(e.Norte.Admin),
                new CrearTipoVinculoDto(proveedor.Codigo, proveedor.Nombre, proveedor.AtributosPermitidos, proveedor.ExigeCuentaDesignada, proveedor.ExigeOtpEnEnlace, requiere, aprobadores),
                esperado);

        (await CodigoAsync(await Guardar(true, null, 422))).ShouldBe("aprobadores-requeridos");
        (await CodigoAsync(await Guardar(true, new AprobadoresDeInvitacionDto("Rol", "Auditor"), 422))).ShouldBe("aprobadores-invalidos");
        (await CodigoAsync(await Guardar(true, new AprobadoresDeInvitacionDto("Equipo", "X"), 422))).ShouldBe("aprobadores-invalidos");
        var guardado = await ApiAsync<TipoVinculoDto>("PUT", $"/api/v1/tipos-vinculo/{proveedor.Id}", new Credencial(e.Norte.Admin),
            new CrearTipoVinculoDto(proveedor.Codigo, proveedor.Nombre, proveedor.AtributosPermitidos, proveedor.ExigeCuentaDesignada, proveedor.ExigeOtpEnEnlace, true,
                new AprobadoresDeInvitacionDto("rol", "oficialcumplimiento")), 200);
        guardado.AprobadoresDeInvitacion.ShouldBe(new AprobadoresDeInvitacionDto("Rol", "OficialCumplimiento"));

        // Un área que Norte no tiene: la invitación quedaría retenida para siempre, así que ni se crea el vínculo.
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", new AprobadoresDeInvitacionDto("Area", "NO_EXISTE"));
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/vinculos", Delegado(e.Norte, e.Laura), new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Sin aprobadores", null, null), "PROVEEDOR", null, CorreoNuevo(), null), 422)))
            .ShouldBe("sin-aprobadores-de-invitacion");

        // El único que aprueba es quien pide: tampoco hay quien decida.
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", new AprobadoresDeInvitacionDto("Rol", "OficialCumplimiento"));
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/vinculos", Delegado(e.Norte, e.Carlos), new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Solo yo apruebo", null, null), "PROVEEDOR", null, CorreoNuevo(), null), 422)))
            .ShouldBe("sin-aprobadores-de-invitacion");

        // Sin invitación que enviar (enviarInvitacion = false) no hay nada que retener.
        var sinEnviar = await CrearVinculoComoAsync(Delegado(e.Norte, e.Carlos), "PROVEEDOR", CorreoNuevo(), enviarInvitacion: false);
        sinEnviar.InvitacionPorAprobar.ShouldBeNull();
    }

    [Fact]
    public async Task La_primera_invitacion_y_la_de_otro_correo_piden_aprobacion_pero_reenviar_al_correo_aprobado_no()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        Credencial carlos() => Delegado(e.Norte, e.Carlos);
        var (correo, otroCorreo) = (CorreoNuevo(), CorreoNuevo());

        // Creado sin enviar la invitación; la primera que se pide queda retenida (202).
        var vinculo = await CrearVinculoComoAsync(laura(), "PROVEEDOR", correo, enviarInvitacion: false);
        vinculo.InvitacionPorAprobar.ShouldBeNull();
        var retenida = await ApiAsync<InvitacionPorAprobarDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", laura(), new InvitacionDto(null, 24), 202);
        (retenida.Estado, retenida.CorreoDestino, retenida.VigenciaHoras).ShouldBe(("Pendiente", correo, 24));
        fixture.Correos.Para(correo).ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", laura(), new InvitacionDto(null), 409))).ShouldBe("invitacion-por-aprobar");

        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", carlos(), new { }, 200);
        var enviada = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"));
        enviada.Para.ShouldBe(correo);
        (await EventosAsync(e.Norte.Datos.TenantId, vinculo.Id)).OfType<InvitacionEnviada>().ShouldHaveSingleItem()
            .Expira.ShouldBeLessThan(DateTimeOffset.UtcNow.AddHours(25)); // la vigencia pedida (24 h) corre desde que sale

        // Reenviar al mismo correo no vuelve a pedirla; invitar a otro sí.
        var reenvio = await ApiAsync<EnlaceInvitacionDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", laura(), new InvitacionDto(null), 201);
        (reenvio.CorreoDestino, reenvio.Envios).ShouldBe((correo, 2));
        var otro = await ApiAsync<InvitacionPorAprobarDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitaciones", laura(), new InvitacionDto(otroCorreo), 202);
        (otro.Estado, otro.CorreoDestino).ShouldBe(("Pendiente", otroCorreo));
        fixture.Correos.Para(otroCorreo).ShouldBeEmpty();
        var actual = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", laura(), null, 200);
        actual.CorreoTercero.ShouldBe(correo); // el correo del vínculo cambia cuando sale la invitación, no antes
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", carlos(), new { }, 200);
        (await fixture.Correos.EsperarAsync(otroCorreo, m => m.Asunto.Contains("te invita"))).Para.ShouldBe(otroCorreo);
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", laura(), null, 200)).CorreoTercero.ShouldBe(otroCorreo);
    }

    [Fact]
    public async Task La_api_por_llave_respeta_la_aprobacion_y_el_evento_publico_refleja_la_invitacion_retenida()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        var correo = CorreoNuevo();

        // La integración (llave del SRM) crea el vínculo con su referencia: queda retenido como el de una persona.
        var vinculo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", new Credencial(e.Norte.LlaveSrm, e.Norte.Datos.Id), new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Aceros por API", null, [new ContactoDto("Ana", correo, null, "Comercial")]), "PROVEEDOR",
            null, correo, new ReferenciaExternaDto("SRM", "ADP-AI-1")), 201);
        vinculo.Estado.ShouldBe("Invitado");
        vinculo.Invitacion.ShouldBeNull();
        var pendiente = vinculo.InvitacionPorAprobar.ShouldNotBeNull();
        pendiente.Estado.ShouldBe("Pendiente");
        pendiente.SolicitadaPor.ShouldStartWith("Integracion:");
        pendiente.SolicitadaPorNombre.ShouldBeNull();

        var evento = await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_por_aprobar");
        evento.Datos.GetProperty("referencias")[0].GetProperty("id").GetString().ShouldBe("ADP-AI-1");
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=SRM&clave=ADP-AI-1", new Credencial(e.Norte.LlaveSrm, e.Norte.Datos.Id), null, 200))
            .InvitacionPorAprobar!.Estado.ShouldBe("Pendiente");
        fixture.Correos.Para(correo).ShouldBeEmpty();

        // La integración no aprueba (no es una persona); una persona de Cumplimiento sí, y el integrador se entera por el webhook.
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", new Credencial(e.Norte.LlaveSrm, e.Norte.Datos.Id), new { }, 422)))
            .ShouldBe("usuario-delegado-requerido");
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/invitacion/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        await EsperarEventoPublicoAsync(e.Norte, "vinculo.invitacion_aprobada");
        await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"));
        var catalogo = await ApiAsync<List<TipoEventoPublicoDto>>("GET", "/api/v1/eventos/tipos", Admin(e.Norte), null, 200);
        catalogo.Select(t => t.Tipo).ShouldContain("vinculo.invitacion_por_aprobar");
        catalogo.Select(t => t.Tipo).ShouldContain("vinculo.invitacion_aprobada");
        catalogo.Select(t => t.Tipo).ShouldContain("vinculo.invitacion_rechazada");
    }

    [Fact]
    public async Task Un_lote_con_un_tipo_que_exige_aprobacion_espera_una_sola_decision_antes_de_procesar_y_al_aprobarlo_invita_sin_pedir_una_por_vinculo()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        Credencial carlos() => Delegado(e.Norte, e.Carlos);
        var (c1, c2, c3) = (CorreoNuevo(), CorreoNuevo(), CorreoNuevo());
        var filas = new[] { c1, c2, c3 }.Select(c => new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: Nit(), Nombre: "Proveedor del lote", Correo: c,
            TipoVinculo: "PROVEEDOR")).ToList();

        // Se crea (202) por aprobar: ninguna fila se procesa, ningún tercero recibe nada.
        var creado = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", laura(), new CrearLoteDto("Proveedores de noviembre", Filas: filas), 202);
        creado.Estado.ShouldBe("PorAprobar");
        creado.TiposPorAprobar.ShouldBe(["PROVEEDOR"]);
        creado.PuedesDecidir.ShouldBe(false);
        var eventos = await EventosAsync(e.Norte.Datos.TenantId, creado.Id);
        eventos.OfType<LoteDeInvitacionCreado>().ShouldHaveSingleItem().PorAprobar.ShouldBeTrue();
        await Task.Delay(1500);
        var enEspera = await ApiAsync<LoteDto>("GET", $"/api/v1/invitaciones/lotes/{creado.Id}", laura(), null, 200);
        (enEspera.Estado, enEspera.Totales.Pendientes, enEspera.Totales.Exitosas).ShouldBe(("PorAprobar", 3, 0));
        (await FilasAsync(e.Norte, creado.Id)).ShouldAllBe(f => f.Estado == "Pendiente");
        new[] { c1, c2, c3 }.ShouldAllBe(c => fixture.Correos.Para(c).Count == 0);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/pausar", laura(), null, 409))).ShouldBe("estado-invalido");

        // Avisos y bandeja: Carlos lo decide como lote completo; Laura, que lo creó, no.
        var avisoDeLote = await EsperarNotificacionAsync(carlos, "invitaciones.por_aprobar", n => n.Enlace!.Contains("/invitaciones/lotes/"));
        avisoDeLote.Enlace.ShouldBe($"/e/{e.Norte.Datos.Id}/invitaciones/lotes/{creado.Id}");
        await EsperarEventoPublicoAsync(e.Norte, "invitaciones.lote_por_aprobar");
        var porAprobar = await ApiAsync<PaginaDto<LoteDto>>("GET", "/api/v1/invitaciones/lotes?estado=PorAprobar", carlos(), null, 200);
        porAprobar.Datos.ShouldHaveSingleItem().PuedesDecidir.ShouldBe(true);
        (await ApiAsync<PaginaDto<LoteDto>>("GET", "/api/v1/invitaciones/lotes?estado=Procesando", carlos(), null, 200)).Datos.ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/invitaciones/lotes?estado=Inventado", carlos(), null, 422))).ShouldBe("estado-invalido");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/aprobar", laura(), new { }, 403))).ShouldBe("no-es-aprobador-de-invitaciones");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/aprobar", Admin(e.Norte), new { }, 422))).ShouldBe("usuario-delegado-requerido");

        // Aprobado, se procesa: las invitaciones salen (el lote ya fue aprobado: no se pide una aprobación por vínculo).
        var aprobado = await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/aprobar", carlos(), new { }, 200);
        (aprobado.Estado, aprobado.AprobadoPor).ShouldBe(("Procesando", $"Usuario:{e.Carlos.Usuario.Id}"));
        var terminado = await EsperarLoteAsync(e.Norte, creado.Id, l => l.Estado is "Completado" or "CompletadoConErrores");
        terminado.Estado.ShouldBe("Completado");
        terminado.Totales.Exitosas.ShouldBe(3);
        foreach (var correo in new[] { c1, c2, c3 })
            await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"));
        var filasListas = await FilasAsync(e.Norte, creado.Id);
        var vinculo = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{filasListas[0].VinculoId}", laura(), null, 200);
        vinculo.InvitacionPorAprobar.ShouldBeNull();
        vinculo.Invitacion.ShouldNotBeNull();
        (await EventosAsync(e.Norte.Datos.TenantId, vinculo.Id)).OfType<InvitacionEnviada>().ShouldHaveSingleItem().LoteId.ShouldBe(creado.Id);

        (await EsperarNotificacionAsync(laura, "invitaciones.decidida")).Titulo.ShouldBe("Lote de invitaciones aprobado");
        await EsperarEventoPublicoAsync(e.Norte, "invitaciones.lote_aprobado");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/aprobar", carlos(), new { }, 409))).ShouldBe("sin-lote-por-aprobar");
    }

    [Fact]
    public async Task Rechazar_un_lote_lo_cancela_sin_crear_vinculos_ni_enviar_nada_y_quien_lo_creo_no_lo_decide()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        Credencial carlos() => Delegado(e.Norte, e.Carlos);
        var correo = CorreoNuevo();
        var nit = Nit();
        await OtroDeCumplimientoAsync(e, "Olga Cumplimiento"); // con Carlos como creador del segundo lote, alguien más del área debe poder decidirlo
        var fila = new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: nit, Nombre: "Proveedor rechazado", Correo: correo, TipoVinculo: "PROVEEDOR");

        var lote = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", laura(), new CrearLoteDto("Lote dudoso", Filas: [fila]), 202);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{lote.Id}/rechazar", carlos(), new MotivoDto(" "), 422))).ShouldBe("motivo-requerido");
        var rechazado = await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{lote.Id}/rechazar", carlos(), new MotivoDto("No están autorizados por Compras"), 200);
        (rechazado.Estado, rechazado.MotivoDeRechazo).ShouldBe(("Cancelado", "No están autorizados por Compras"));
        rechazado.Totales.Pendientes.ShouldBe(1);
        (await FilasAsync(e.Norte, lote.Id)).ShouldHaveSingleItem().Estado.ShouldBe("Cancelada");
        (await ApiAsync<PaginaDto<VinculoDto>>("GET", "/api/v1/vinculos", laura(), null, 200)).Datos.ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{lote.Id}/aprobar", carlos(), new { }, 409))).ShouldBe("sin-lote-por-aprobar");

        var aviso = await EsperarNotificacionAsync(laura, "invitaciones.decidida");
        (aviso.Titulo, aviso.Mensaje.Contains("No están autorizados por Compras")).ShouldBe(("Lote de invitaciones rechazado", true));
        var publico = await EsperarEventoPublicoAsync(e.Norte, "invitaciones.lote_rechazado");
        publico.Datos.GetProperty("motivo").GetString().ShouldBe("No están autorizados por Compras");
        await Task.Delay(500);
        fixture.Correos.Para(correo).ShouldBeEmpty();

        // Quien lo creó no lo decide aunque sea de Cumplimiento.
        var deCarlos = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", carlos(), new CrearLoteDto("Lote de Carlos", Filas: [fila with { NumeroIdentificacion = Nit() }]), 202);
        deCarlos.PuedesDecidir.ShouldBe(false);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{deCarlos.Id}/aprobar", carlos(), new { }, 403))).ShouldBe("separacion-de-funciones");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{deCarlos.Id}/rechazar", carlos(), new MotivoDto("x"), 403))).ShouldBe("separacion-de-funciones");
        // Puede retirarlo él mismo mientras espera.
        (await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{deCarlos.Id}/cancelar", carlos(), null, 200)).Estado.ShouldBe("Cancelado");
    }

    [Fact]
    public async Task Un_lote_sin_tipos_que_exijan_aprobacion_o_cuyas_filas_no_invitan_nada_se_procesa_como_siempre()
    {
        var e = await PrepararConEscrituraAsync();
        await ExigirAprobacionDeInvitacionAsync(e.Norte, "PROVEEDOR", ApruebaCumplimiento);
        Credencial laura() => Delegado(e.Norte, e.Laura);
        var correo = CorreoNuevo();

        var clientes = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", laura(), new CrearLoteDto("Clientes", Filas:
            [new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: Nit(), Nombre: "Cliente", Correo: correo, TipoVinculo: "CLIENTE")]), 202);
        clientes.Estado.ShouldBe("Procesando");
        clientes.TiposPorAprobar.ShouldBeNull();
        (await EsperarLoteAsync(e.Norte, clientes.Id, l => l.Estado == "Completado")).Totales.Exitosas.ShouldBe(1);
        await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"));

        // Un vínculo ya retenido de una fila no se reinvita por lote: la fila se rechaza en la vista previa con su código.
        var retenido = await CrearVinculoComoAsync(laura(), "PROVEEDOR", CorreoNuevo(), nit: "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var identificacion = retenido.TerceroId; // el tercero ya existe: se busca su NIT por el vínculo
        var tercero = await ApiAsync<TerceroDto>("GET", $"/api/v1/terceros/{identificacion}", Admin(e.Norte), null, 200);
        var vista = await ApiAsync<VistaPreviaLoteDto>("POST", "/api/v1/invitaciones/lotes/vista-previa", laura(), new CrearLoteDto(Filas:
            [new FilaDeLoteEntradaDto(TipoIdentificacion: tercero.Identificacion.Tipo, NumeroIdentificacion: tercero.Identificacion.Numero, Nombre: "Retenido",
                Correo: CorreoNuevo(), TipoVinculo: "PROVEEDOR")]), 200);
        vista.Filas.ShouldHaveSingleItem().Errores.ShouldHaveSingleItem().Codigo.ShouldBe("invitacion-por-aprobar");
    }
}
