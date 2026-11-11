using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Operación de la revisión (OP-1…OP-19) con PostgreSQL y las colas durables: SLA por área con alertas y bandeja, delegación de revisión con sus reglas,
/// campañas de renovación (vista previa, proceso por tandas, plantilla y preferencias del tercero), ayuda por documento por empresa y recordatorio por flujo.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static readonly Credencial PlataformaOp = new(ApiFixture.ClavePlataforma);

    private async Task<VinculoDto> VinculoEnRevisionAsync(Escenario e, string? nit = null, Guid? invitadoPor = null)
    {
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, nit);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);
        await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision" && x.Requisitos!.All(r => r.VersionActual is not null));
        return vinculo with { ExpedienteAbiertoId = expedienteId };
    }

    /// <summary>El veredicto de aprobación de lo que el área tiene por decidir, visto por <paramref name="como"/>.</summary>
    private async Task<VeredictoDto> AprobarLoDeAsync(Escenario e, Guid expedienteId, string codigoArea, Persona como)
    {
        var leido = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Delegado(e.Norte, como), null, 200);
        var porDecidir = Area(leido, codigoArea).DocumentosPorDecidir ?? [];
        return new VeredictoDto("Aprobado", Versiones: porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente));
    }

    private async Task<Persona> UsuarioAsync(Empresa empresa, string nombre, string rol, IdentificacionDto? identificacion = null)
    {
        var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
        var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(empresa.Admin),
            new CrearUsuarioDto(nombre, correo, identificacion, [new RolAsignadoDto(rol, empresa.Datos.Id)], null), 201);
        return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
    }

    private async Task<List<FilaBandejaDto>> BandejaDeAsync(Escenario e, Persona persona) =>
        [.. (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e.Norte, persona), null, 200)).Datos];

    // ---------- SLA (OP-9…OP-14) ----------

    [Fact]
    public async Task El_sla_avisa_al_acercarse_y_al_vencer_marca_la_bandeja_y_el_informe_mide_el_cumplimiento()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var ada = await UsuarioAsync(e.Norte, "Ada Administradora", "Administrador");

        // Configuración con su validación y su auditoría: Compras tiene 10 horas por defecto.
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/configuracion/sla", Admin(e.Norte), new ActualizarConfiguracionSlaDto(30, true, []), 422))).ShouldBe("porcentaje-invalido");
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/configuracion/sla", Admin(e.Norte),
            new ActualizarConfiguracionSlaDto(80, true, [new SlaDeAreaDto(e.Compras.Id, 0)]), 422))).ShouldBe("sla-invalido");
        await ApiAsync("PUT", "/api/v1/configuracion/sla", Delegado(e.Norte, e.Laura), new ActualizarConfiguracionSlaDto(80, true, []), 403);
        var configuracion = await ApiAsync<ConfiguracionSlaDto>("PUT", "/api/v1/configuracion/sla", Admin(e.Norte),
            new ActualizarConfiguracionSlaDto(80, true, [new SlaDeAreaDto(e.Compras.Id, 10)]), 200);
        configuracion.Areas.Single(a => a.AreaId == e.Compras.Id).Horas.ShouldBe(10);
        configuracion.Areas.Single(a => a.AreaId == e.Cumplimiento.Id).Horas.ShouldBeNull();
        (await ApiAsync<ConfiguracionSlaDto>("GET", "/api/v1/configuracion/sla", Delegado(e.Norte, e.Laura), null, 200)).PorcentajeAviso.ShouldBe(80);
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?entidad=SlaDeRevision", Admin(e.Norte), null, 200)).Datos.ShouldNotBeEmpty();

        // Un expediente que se abre con el plazo por defecto de Compras (Cumplimiento no tiene).
        var vinculo = await VinculoEnRevisionAsync(e);
        var expedienteId = vinculo.ExpedienteAbiertoId!.Value;
        var detalle = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        var compras = Area(detalle, "COMPRAS");
        var vence = compras.SlaVence.ShouldNotBeNull();
        Area(detalle, "CUMPLIMIENTO").SlaVence.ShouldBeNull();
        var inicio = vence.AddHours(-10);
        var aprobar = await AprobarLoDeAsync(e, expedienteId, "COMPRAS", e.Laura);

        // Las aserciones de las personas se firman con la hora real: con el reloj adelantado solo se leen cosas con la llave de la empresa.
        async Task<FilaBandejaDto> DeCompras() =>
            (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Admin(e.Norte), null, 200)).Datos.Single(f => f.ExpedienteId == expedienteId && f.AreaCodigo == "COMPRAS");

        try
        {
            // A 9 horas (90 % del plazo): aviso previo, una sola vez.
            IrA(inicio.AddHours(9));
            var barrido = await ApiAsync<ResultadoRevisionSlaDto>("POST", "/api/v1/plataforma/sla/revisar", PlataformaOp, new RevisarSlaDto(tenantId), 200);
            (barrido.Avisos, barrido.Incumplidos).ShouldBe((1, 0));
            (await ApiAsync<ResultadoRevisionSlaDto>("POST", "/api/v1/plataforma/sla/revisar", PlataformaOp, new RevisarSlaDto(tenantId), 200)).Revisados.ShouldBe(0);
            var enLaBandeja = await DeCompras();
            (enLaBandeja.SlaHoras, enLaBandeja.Sla).ShouldBe((10, "PorVencer"));

            // A 11 horas: venció.
            IrA(inicio.AddHours(11));
            var vencido = await ApiAsync<ResultadoRevisionSlaDto>("POST", "/api/v1/plataforma/sla/revisar", PlataformaOp, new RevisarSlaDto(tenantId), 200);
            (vencido.Avisos, vencido.Incumplidos).ShouldBe((0, 1));
            (await DeCompras()).Sla.ShouldBe("Vencido");
            var informeAhora = await ApiAsync<InformeSlaDto>("GET", "/api/v1/informes/sla", Admin(e.Norte), null, 200);
            informeAhora.Resumen.VencidosAhora.ShouldBe(1);
            informeAhora.Vencidos.ShouldHaveSingleItem().HorasDeRetraso.ShouldBeInRange(0.9, 1.1);

            // Compras decide tarde (con su aserción firmada a la hora del reloj de la prueba): la ronda queda fuera de plazo.
            await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Compras.Id}/veredicto", DelegadoEn(e.Norte, e.Laura, fixture.Reloj.GetUtcNow().UtcDateTime), aprobar, 200);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        // Al acercarse: a quienes revisan por Compras, no a Cumplimiento (sin plazo). Al vencer: también a los administradores.
        var avisoLaura = (await EsperarCampanaDeAsync(e, e.Laura, x => x.Datos.Any(n => n.Tipo == "expediente.sla_por_vencer"))).Datos.Single(n => n.Tipo == "expediente.sla_por_vencer");
        avisoLaura.Titulo.ShouldBe("Plazo de revisión por vencer");
        avisoLaura.ExpedienteId.ShouldBe(expedienteId);
        await fixture.Correos.EsperarAsync(e.Laura.Usuario.Correo, m => m.Asunto.StartsWith("Plazo de revisión por vencer", StringComparison.Ordinal));
        await EsperarCampanaDeAsync(e, e.Laura, x => x.Datos.Any(n => n.Tipo == "expediente.sla_incumplido"));
        var deAda = (await EsperarCampanaDeAsync(e, ada, x => x.Datos.Any(n => n.Tipo == "expediente.sla_incumplido"))).Datos.Single(n => n.Tipo == "expediente.sla_incumplido");
        deAda.Titulo.ShouldBe("Plazo de revisión vencido");
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldNotContain(n => n.Tipo.StartsWith("expediente.sla_", StringComparison.Ordinal));

        // El informe de cumplimiento mide la ronda de Compras: una ronda con plazo, fuera de él. Cumplimiento no tiene plazo que cumplir.
        var informe = await ApiAsync<InformeSlaDto>("GET", "/api/v1/informes/sla", Admin(e.Norte), null, 200);
        var delArea = informe.PorArea.Single(a => a.AreaId == e.Compras.Id);
        (delArea.Rondas, delArea.ADentroDelPlazo, delArea.FueraDelPlazo, delArea.Cumplimiento).ShouldBe((1, 0, 1, 0d));
        informe.PorArea.ShouldNotContain(a => a.AreaId == e.Cumplimiento.Id);
        informe.Resumen.VencidosAhora.ShouldBe(0);
        (await ApiAsync("GET", "/api/v1/informes/sla/excel", Admin(e.Norte), null, 200)).Context.Response.ContentType!.ShouldContain("spreadsheetml");

        var eventos = await EventosAsync(tenantId, expedienteId);
        eventos.OfType<Dominio.Expedientes.SlaPorVencer>().ShouldHaveSingleItem().AreaId.ShouldBe(e.Compras.Id);
        eventos.OfType<Dominio.Expedientes.SlaIncumplido>().ShouldHaveSingleItem().AreaId.ShouldBe(e.Compras.Id);
    }

    /// <summary>La persona actúa desde el SRM con una aserción firmada a la hora <paramref name="ahora"/> (para pruebas con el reloj adelantado).</summary>
    private static Credencial DelegadoEn(Empresa empresa, Persona p, DateTime ahora)
    {
        var asercion = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "SRM",
            Audience = "validasoft",
            Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", p.Sub), new System.Security.Claims.Claim("name", p.Usuario.Nombre),
                new System.Security.Claims.Claim("email", p.Usuario.Correo), new System.Security.Claims.Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora.AddSeconds(-5),
            NotBefore = ahora.AddSeconds(-5),
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new Microsoft.IdentityModel.Tokens.SigningCredentials(new Microsoft.IdentityModel.Tokens.RsaSecurityKey(empresa.ClaveSrm),
                Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256)
        });
        return new Credencial(empresa.LlaveSrm, empresa.Datos.Id, asercion);
    }


    // ---------- Delegación (OP-7, OP-8) ----------

    [Fact]
    public async Task La_delegacion_hace_revisar_a_otra_persona_el_area_entre_las_fechas_con_sus_validaciones_y_las_reglas_siguen_valiendo()
    {
        var e = await PrepararAsync();
        var beto = await UsuarioAsync(e.Norte, "Beto Suplente", "Revisor");
        var aura = await UsuarioAsync(e.Norte, "Aura Auditora", "Auditor");
        var nit = Nit();
        var cora = await UsuarioAsync(e.Norte, "Cora Conflicto", "Revisor", new IdentificacionDto("CO", "NIT", nit));
        var vinculo = await VinculoEnRevisionAsync(e, nit);
        var expedienteId = vinculo.ExpedienteAbiertoId!.Value;
        var hoy = Hoy();
        var ruta = "/api/v1/delegaciones";

        // Sin delegación, Beto no revisa Compras ni la ve en su bandeja.
        var aprobar = await AprobarLoDeAsync(e, expedienteId, "COMPRAS", e.Laura);
        (await CodigoAsync(await AreaAsync(e, expedienteId, e.Compras, beto, aprobar, 403))).ShouldBe("no-es-miembro-del-area");
        (await BandejaDeAsync(e, beto)).ShouldBeEmpty();

        // Validaciones.
        async Task<string?> Rechazo(Persona quien, CrearDelegacionDto dto, int estado) => await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e.Norte, quien), dto, estado));
        (await Rechazo(e.Laura, new CrearDelegacionDto(e.Laura.Usuario.Id, hoy, hoy), 422)).ShouldBe("delegacion-a-si-mismo");
        (await Rechazo(e.Laura, new CrearDelegacionDto(aura.Usuario.Id, hoy, hoy), 422)).ShouldBe("delegado-sin-permiso-de-revision");
        (await Rechazo(e.Laura, new CrearDelegacionDto(beto.Usuario.Id, hoy.AddDays(3), hoy), 422)).ShouldBe("rango-invalido");
        (await Rechazo(e.Laura, new CrearDelegacionDto(beto.Usuario.Id, hoy.AddDays(-5), hoy.AddDays(-1)), 422)).ShouldBe("delegacion-vencida");
        (await Rechazo(e.Laura, new CrearDelegacionDto(beto.Usuario.Id, hoy, hoy.AddDays(1), [e.Cumplimiento.Id]), 422)).ShouldBe("no-es-miembro-del-area");
        (await Rechazo(e.Carlos, new CrearDelegacionDto(beto.Usuario.Id, hoy, hoy.AddDays(1), DeUsuarioId: e.Laura.Usuario.Id), 403)).ShouldBe("delegacion-ajena");

        // Laura delega Compras a Beto hasta dentro de tres días: queda vigente y auditada.
        var delegacion = await ApiAsync<DelegacionDto>("POST", ruta, Delegado(e.Norte, e.Laura),
            new CrearDelegacionDto(beto.Usuario.Id, hoy, hoy.AddDays(3), [e.Compras.Id], Motivo: "Vacaciones"), 200);
        (delegacion.Estado, delegacion.De, delegacion.A, delegacion.TodasLasAreas).ShouldBe(("Vigente", "Laura Compras", "Beto Suplente", false));
        delegacion.Areas.ShouldHaveSingleItem().Codigo.ShouldBe("COMPRAS");
        (await Rechazo(e.Laura, new CrearDelegacionDto(beto.Usuario.Id, hoy.AddDays(2), hoy.AddDays(6), [e.Compras.Id]), 409)).ShouldBe("delegacion-solapada");
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?entidad=DelegacionDeRevision&entidadId={delegacion.Id:N}", Admin(e.Norte), null, 200))
            .Datos.Select(r => r.Accion).ShouldBe(["Creado"]);

        // Beto ve la bandeja de Compras, recibe sus avisos y la decide; Cumplimiento no es suyo.
        (await BandejaDeAsync(e, beto)).ShouldContain(f => f.ExpedienteId == expedienteId && f.AreaCodigo == "COMPRAS");
        (await BandejaDeAsync(e, beto)).ShouldNotContain(f => f.AreaCodigo == "CUMPLIMIENTO");
        (await CodigoAsync(await AreaAsync(e, expedienteId, e.Cumplimiento, beto, await AprobarLoDeAsync(e, expedienteId, "CUMPLIMIENTO", e.Carlos), 403))).ShouldBe("no-es-miembro-del-area");
        (await ApiAsync<MisPendientesDto>("GET", "/api/v1/mis-pendientes", Delegado(e.Norte, beto), null, 200)).PorArea.ShouldContain(p => p.Codigo == "COMPRAS" && p.Cantidad == 1);
        var decidido = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Compras, beto, aprobar);
        Area(decidido, "COMPRAS").Estado.ShouldBe("Aprobada");

        // Queda en el historial en nombre de quién revisó.
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(e.Norte.Datos.TenantId.ToString("N")))
        {
            var veredicto = (await consulta.Events.FetchStreamAsync(expedienteId)).Single(x => x.Data is Dominio.Expedientes.VeredictoDeAreaEmitido);
            veredicto.Headers!["delegadoDe"].ShouldBe(e.Laura.Usuario.Id.ToString());
            veredicto.Headers["delegacionId"].ShouldBe(delegacion.Id.ToString());
        }

        // Solo quien delegó, quien recibe o un administrador de expedientes la ven y la revocan; una sola vez.
        (await ApiAsync<List<DelegacionDto>>("GET", ruta, Delegado(e.Norte, e.Carlos), null, 200)).ShouldBeEmpty();
        (await ApiAsync<List<DelegacionDto>>("GET", ruta, Delegado(e.Norte, beto), null, 200)).ShouldHaveSingleItem().Id.ShouldBe(delegacion.Id);
        (await ApiAsync<List<DelegacionDto>>("GET", ruta, Admin(e.Norte), null, 200)).ShouldHaveSingleItem();
        await ApiAsync("POST", $"{ruta}/{delegacion.Id}/revocar", Delegado(e.Norte, e.Carlos), new RevocarDelegacionDto(), 404);
        var revocada = await ApiAsync<DelegacionDto>("POST", $"{ruta}/{delegacion.Id}/revocar", Delegado(e.Norte, e.Laura), new RevocarDelegacionDto("Volví antes"), 200);
        (revocada.Estado, revocada.MotivoDeRevocacion).ShouldBe(("Revocada", "Volví antes"));
        (await CodigoAsync(await ApiAsync("POST", $"{ruta}/{delegacion.Id}/revocar", Delegado(e.Norte, e.Laura), new RevocarDelegacionDto(), 409))).ShouldBe("delegacion-no-revocable");
        (await BandejaDeAsync(e, beto)).ShouldBeEmpty();
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria?entidad=DelegacionDeRevision&entidadId={delegacion.Id:N}", Admin(e.Norte), null, 200))
            .Datos.Select(r => r.Accion).ShouldBe(["Modificado", "Creado"]);

        // El conflicto de intereses se evalúa sobre quien recibe la delegación: Cora tiene la identificación del tercero.
        await ApiAsync<DelegacionDto>("POST", ruta, Delegado(e.Norte, e.Carlos), new CrearDelegacionDto(cora.Usuario.Id, hoy, hoy.AddDays(1), [e.Cumplimiento.Id]), 200);
        (await CodigoAsync(await AreaAsync(e, expedienteId, e.Cumplimiento, cora, await AprobarLoDeAsync(e, expedienteId, "CUMPLIMIENTO", e.Carlos), 403))).ShouldBe("conflicto-de-intereses");

        // Un administrador de expedientes delega por otra persona.
        var porAdmin = await ApiAsync<DelegacionDto>("POST", ruta, Admin(e.Norte),
            new CrearDelegacionDto(beto.Usuario.Id, hoy.AddDays(10), hoy.AddDays(12), DeUsuarioId: e.Carlos.Usuario.Id), 200);
        (porAdmin.Estado, porAdmin.TodasLasAreas, porAdmin.De).ShouldBe(("Programada", true, "Carlos Cumplimiento"));
        (await ApiAsync<List<DelegacionDto>>("GET", $"{ruta}?estado=Programada", Admin(e.Norte), null, 200)).ShouldHaveSingleItem().Id.ShouldBe(porAdmin.Id);
        (await ApiAsync<List<CandidatoDeDelegacionDto>>("GET", $"{ruta}/candidatos", Delegado(e.Norte, e.Laura), null, 200)).Select(c => c.Nombre)
            .ShouldBe(["Beto Suplente", "Carlos Cumplimiento", "Cora Conflicto", "Laura Compras"], ignoreOrder: true);
    }

    // ---------- Campañas de renovación (OP-1…OP-6) y avisos con plantilla y preferencias (OP-17) ----------

    private async Task<CampaniaDto> EsperarCampaniaAsync(Empresa empresa, Guid id, Func<CampaniaDto, bool> condicion, int segundos = 40)
    {
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (true)
        {
            var campania = await ApiAsync<CampaniaDto>("GET", $"/api/v1/campanias/{id}", Admin(empresa), null, 200);
            if (condicion(campania))
                return campania;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La campaña {id} no llegó al estado esperado (está {campania.Estado}, {campania.Totales}).");
            await Task.Delay(150);
        }
    }

    [Fact]
    public async Task La_campania_de_renovacion_previsualiza_abre_la_renovacion_avisa_con_plantilla_y_preferencias_y_la_siguiente_solo_recuerda()
    {
        var e = await PrepararAsync();
        var flujo = await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e), new VigenciaVinculoDto(100, [30, 5], 10));
        flujo.Publicada.ShouldNotBeNull();
        var cliente = await ClienteAprobadoAsync(e);
        var hasta = (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).VigenteHasta.ShouldNotBeNull();

        // Vista previa: a quién afecta, sin crear nada.
        (await ApiAsync<VistaPreviaCampaniaDto>("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(10), 200)).Total.ShouldBe(0);
        var previa = await ApiAsync<VistaPreviaCampaniaDto>("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(120), 200);
        (previa.Total, previa.Abririan, previa.Recordarian, previa.Omitidos, previa.ConCorreo, previa.Truncada).ShouldBe((1, 1, 0, 0, 1, false));
        var fila = previa.Filas.ShouldHaveSingleItem();
        (fila.Accion, fila.EstadoVinculo, fila.VenceEn).ShouldBe(("Abrir", "Activo", hasta));
        fila.Tercero.Nombre.ShouldStartWith("Tercero ");
        (await ApiAsync<PaginaDto<CampaniaDto>>("GET", "/api/v1/campanias", Admin(e.Norte), null, 200)).Datos.ShouldBeEmpty();

        // Validaciones y permisos.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(400), 422))).ShouldBe("dias-invalidos");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(30, Estados: ["Bloqueado"]), 422))).ShouldBe("estado-invalido");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/campanias", Admin(e.Norte), new CrearCampaniaDto(new FiltroDeCampaniaDto(30, Guid.NewGuid())), 422))).ShouldBe("campania-sin-vinculos");
        await ApiAsync("POST", "/api/v1/campanias", Delegado(e.Norte, e.Laura), new CrearCampaniaDto(new FiltroDeCampaniaDto(120)), 403);
        await ApiAsync("GET", "/api/v1/campanias", Delegado(e.Norte, e.Laura), null, 200);

        // La empresa personaliza el aviso de renovación (PL-1).
        await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/renovacion/correo", Admin(e.Norte),
            new ActualizarPlantillaNotificacionDto("{{empresa}} te pide renovar antes del {{fecha}}", "Hola desde Compras", null), 200);
        (await ApiAsync("PUT", $"{RutaPlantillas}/renovacion/correo", Admin(e.Norte), new ActualizarPlantillaNotificacionDto("{{folio}}", null, null), 422))
            .Context.Response.StatusCode.ShouldBe(422); // la variable no está permitida en este correo

        // Crear: 202, se procesa por tandas y abre la renovación del vínculo.
        var creada = await ApiAsync<CampaniaDto>("POST", "/api/v1/campanias", Admin(e.Norte), new CrearCampaniaDto(new FiltroDeCampaniaDto(120), "Renovación de clientes"), 202);
        creada.Nombre.ShouldBe("Renovación de clientes");
        var terminada = await EsperarCampaniaAsync(e.Norte, creada.Id, c => c.Estado == "Completada");
        (terminada.Totales.Filas, terminada.Totales.Exitosas, terminada.Totales.Fallidas, terminada.Totales.Omitidas).ShouldBe((1, 1, 0, 0));
        var filas = (await ApiAsync<PaginaDto<FilaCampaniaDto>>("GET", $"/api/v1/campanias/{creada.Id}/filas", Admin(e.Norte), null, 200)).Datos.ShouldHaveSingleItem();
        (filas.Estado, filas.Accion, filas.VinculoId).ShouldBe(("Exitosa", "RenovacionAbierta", cliente.Vinculo.Id));
        filas.Folio.ShouldNotBeNullOrEmpty();
        var vinculo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
        var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{vinculo.ExpedienteAbiertoId.ShouldNotBeNull()}", Admin(e.Norte), null, 200);
        (renovacion.Tipo, renovacion.Estado, renovacion.Folio).ShouldBe(("Renovacion", "Abierto", filas.Folio));
        renovacion.Requisitos!.ShouldHaveSingleItem().TipoDocumento.Codigo.ShouldBe("FICHA_GENERAL"); // lo que el tercero ratifica o actualiza
        vinculo.Estado.ShouldBe("Activo"); // la renovación se abre antes de que venza: el vínculo sigue activo

        // El tercero recibe el correo con la plantilla de la empresa y el enlace, y el aviso en su campana.
        var correo = await fixture.Correos.EsperarAsync(cliente.Correo, m => m.Asunto.StartsWith("Norte S.A.S. te pide renovar antes del", StringComparison.Ordinal));
        correo.Texto.ShouldStartWith("Hola desde Compras");
        RegexToken().IsMatch(correo.Texto).ShouldBeTrue();
        (await EsperarCampanaDelTerceroAsync(cliente.Sesion, x => x.Datos.Any(n => n.Tipo == "tercero.vigencia"))).Datos
            .Single(n => n.Tipo == "tercero.vigencia").Enlace.ShouldBe($"/expediente/{renovacion.Id}");

        // Informe y listado.
        (await ApiAsync("GET", $"/api/v1/campanias/{creada.Id}/informe", Admin(e.Norte), null, 200)).Context.Response.ContentType!.ShouldContain("spreadsheetml");
        (await ApiAsync<PaginaDto<CampaniaDto>>("GET", "/api/v1/campanias", Admin(e.Norte), null, 200)).Datos.ShouldHaveSingleItem().Id.ShouldBe(creada.Id);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/campanias/{creada.Id}/pausar", Admin(e.Norte), new { }, 409))).ShouldBe("campania-en-estado-terminal");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/campanias/{creada.Id}/reintentar", Admin(e.Norte), new { }, 409))).ShouldBe("sin-filas-fallidas");
        await ApiAsync("GET", $"/api/v1/campanias/{Guid.NewGuid()}", Admin(e.Norte), null, 404);

        // Otra campaña con el mismo filtro: el vínculo ya tiene su renovación abierta, solo se le recuerda... y el tercero apagó este correo (NT-5).
        await TerceroAsync<PreferenciasNotificacionDto>("PUT", "/public/v1/preferencias-notificacion", cliente.Sesion.AccessToken,
            new ActualizarPreferenciasNotificacionDto(new Dictionary<string, bool> { ["tercero.vigencia"] = false }), 200);
        var segunda = await ApiAsync<VistaPreviaCampaniaDto>("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(120), 200);
        (segunda.Abririan, segunda.Recordarian).ShouldBe((0, 1));
        var recordada = await ApiAsync<CampaniaDto>("POST", "/api/v1/campanias", Admin(e.Norte), new CrearCampaniaDto(new FiltroDeCampaniaDto(120)), 202);
        await EsperarCampaniaAsync(e.Norte, recordada.Id, c => c.Estado == "Completada");
        (await ApiAsync<PaginaDto<FilaCampaniaDto>>("GET", $"/api/v1/campanias/{recordada.Id}/filas", Admin(e.Norte), null, 200)).Datos.ShouldHaveSingleItem().Accion.ShouldBe("Recordada");
        await Task.Delay(1500);
        fixture.Correos.Para(cliente.Correo).Count(m => m.Asunto.StartsWith("Norte S.A.S. te pide renovar", StringComparison.Ordinal)).ShouldBe(1); // el correo apagado no sale
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{cliente.Vinculo.Id}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId.ShouldBe(vinculo.ExpedienteAbiertoId);
    }

    private async Task<BandejaNotificacionesDto> EsperarCampanaDelTerceroAsync(SesionTerceroDto sesion, Func<BandejaNotificacionesDto, bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var bandeja = await TerceroAsync<BandejaNotificacionesDto>("GET", "/public/v1/notificaciones", sesion.AccessToken, null, 200);
            if (condicion(bandeja))
                return bandeja;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("La campana del tercero no recibió lo esperado.");
            await Task.Delay(200);
        }
    }

    // ---------- Ayuda por documento por empresa (OP-15, OP-16) ----------

    [Fact]
    public async Task La_empresa_sobrescribe_la_ayuda_del_documento_y_solo_la_ve_su_tercero_el_resto_del_grupo_sigue_con_la_del_grupo()
    {
        var e = await PrepararAsync();
        var rut = (await ApiAsync<List<TipoDocumentoTenantDto>>("GET", "/api/v1/tipos-documento", Admin(e.Norte), null, 200)).Single(t => t.Codigo == "RUT");
        await ApiAsync<TipoDocumentoTenantDto>("PUT", $"/api/v1/tipos-documento/{rut.Id}", Admin(e.Norte), new ActualizarTipoDocumentoDto(TextoAyuda: "Ayuda del grupo"), 200);
        var sur = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(e.Norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "900123456-8"), "Sur S.A.S.", "SUR", null, null), 201);

        async Task<AyudaDeDocumentoDto> Ayuda(Credencial credencial) =>
            (await ApiAsync<List<AyudaDeDocumentoDto>>("GET", "/api/v1/ayudas-de-documentos", credencial, null, 200)).Single(a => a.TipoDocumentoId == rut.Id);

        var inicial = await Ayuda(Admin(e.Norte));
        (inicial.AyudaDelGrupo, inicial.AyudaDeLaEmpresa, inicial.AyudaEfectiva).ShouldBe(("Ayuda del grupo", null, "Ayuda del grupo"));

        // Norte escribe la suya; permisos y largo.
        await ApiAsync("PUT", $"/api/v1/ayudas-de-documentos/{rut.Id}", Delegado(e.Norte, e.Laura), new ActualizarAyudaDeDocumentoDto("x"), 403);
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/ayudas-de-documentos/{rut.Id}", Admin(e.Norte), new ActualizarAyudaDeDocumentoDto(new string('a', 2001)), 422)))
            .ShouldBe("texto-demasiado-largo");
        await ApiAsync("PUT", $"/api/v1/ayudas-de-documentos/{Guid.NewGuid()}", Admin(e.Norte), new ActualizarAyudaDeDocumentoDto("x"), 404);
        var propia = await ApiAsync<AyudaDeDocumentoDto>("PUT", $"/api/v1/ayudas-de-documentos/{rut.Id}", Admin(e.Norte), new ActualizarAyudaDeDocumentoDto("  Ayuda de Norte\r\ncon el RUT vigente  "), 200);
        (propia.AyudaDeLaEmpresa, propia.AyudaEfectiva, propia.AyudaDelGrupo).ShouldBe(("Ayuda de Norte\ncon el RUT vigente", "Ayuda de Norte\ncon el RUT vigente", "Ayuda del grupo"));
        (await Ayuda(Admin(e.Norte))).AyudaDeLaEmpresa.ShouldBe("Ayuda de Norte\ncon el RUT vigente");
        var deSur = await Ayuda(new Credencial(e.Norte.Admin, sur.Id));
        (deSur.AyudaDeLaEmpresa, deSur.AyudaEfectiva).ShouldBe((null, "Ayuda del grupo"));

        // El tercero de Norte ve la de Norte en su expediente y en el del revisor.
        var vinculo = await VinculoEnRevisionAsync(e);
        var expedienteId = vinculo.ExpedienteAbiertoId!.Value;
        var enEmpresa = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        Requisito(enEmpresa, "RUT").TipoDocumento.TextoAyuda.ShouldBe("Ayuda de Norte\ncon el RUT vigente");
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Requisitos!
            .Single(r => r.TipoDocumento.Codigo == "CAMARA_COMERCIO").TipoDocumento.TextoAyuda.ShouldBeNull();

        // Vaciarla devuelve la del grupo y deja la auditoría.
        var vacia = await ApiAsync<AyudaDeDocumentoDto>("PUT", $"/api/v1/ayudas-de-documentos/{rut.Id}", Admin(e.Norte), new ActualizarAyudaDeDocumentoDto(" "), 200);
        (vacia.AyudaDeLaEmpresa, vacia.AyudaEfectiva).ShouldBe((null, "Ayuda del grupo"));
        Requisito(await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200), "RUT").TipoDocumento.TextoAyuda.ShouldBe("Ayuda del grupo");
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=AyudaDeDocumento", Admin(e.Norte), null, 200)).Datos.Count.ShouldBe(2);
    }

    // ---------- Recordatorio automático por flujo (OP-19) ----------

    [Fact]
    public async Task El_flujo_puede_fijar_el_recordatorio_automatico_de_observaciones_y_manda_sobre_el_de_la_empresa()
    {
        var e = await PrepararAsync();
        var flujo = await ApiAsync<FlujoDto>("GET", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte), null, 200);
        flujo.Publicada!.RecordatorioObservaciones.ShouldBe(new RecordatorioDeFlujoDto("Empresa"));

        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte),
            new EditarFlujoDto(RecordatorioObservaciones: new RecordatorioDeFlujoDto("CadaDias", 61)), 422))).ShouldBe("dias-invalidos");
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte),
            new EditarFlujoDto(RecordatorioObservaciones: new RecordatorioDeFlujoDto("Cada3")), 422))).ShouldBe("modo-invalido");
        var borrador = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte),
            new EditarFlujoDto(RecordatorioObservaciones: new RecordatorioDeFlujoDto("CadaDias", 1)), 200);
        borrador.Borrador!.RecordatorioObservaciones.ShouldBe(new RecordatorioDeFlujoDto("CadaDias", 1));
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{e.Flujo.Id}/publicar", Admin(e.Norte), new { }, 200);

        // La empresa no tiene el recordatorio encendido, pero el flujo manda cada día: el expediente devuelto al tercero lo recibe.
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);
        var camara = Requisito(await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision"), "CAMARA_COMERCIO");
        await VeredictoAsync<ExpedienteDto>(e, expedienteId, camara.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Observado", ["ilegible"], "La cámara está cortada.", SobreVersion: 1));
        (await ApiAsync<RecordatorioAutomaticoDto>("GET", "/api/v1/configuracion/recordatorio-observaciones", Admin(e.Norte), null, 200)).Activo.ShouldBeFalse();

        IrA(DateTimeOffset.UtcNow.AddDays(2));
        var revision = await ApiAsync<ResultadoRevisionRecordatoriosDto>("POST", "/api/v1/plataforma/recordatorios-observaciones/revisar", PlataformaOp,
            new RevisarRecordatoriosDto(e.Norte.Datos.TenantId), 200);
        fixture.Reloj.Desfase = TimeSpan.Zero; // las aserciones y los enlaces de las personas se firman con la hora real
        revision.Enviados.ShouldBe(1);
        await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("recordatorio", StringComparison.OrdinalIgnoreCase) || m.Texto.Contains("observaciones", StringComparison.OrdinalIgnoreCase));

        // Apagado en el flujo: aunque la empresa lo encienda, este flujo no recuerda solo.
        await ApiAsync<RecordatorioAutomaticoDto>("PUT", "/api/v1/configuracion/recordatorio-observaciones", Admin(e.Norte), new ActualizarRecordatorioAutomaticoDto(true, 1), 200);
        await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte), new EditarFlujoDto(RecordatorioObservaciones: new RecordatorioDeFlujoDto("Apagado")), 200);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{e.Flujo.Id}/publicar", Admin(e.Norte), new { }, 200);
        var correo2 = CorreoNuevo();
        var vinculo2 = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo2);
        var sesion2 = await SesionAsync(correo2);
        await SubirAsync(sesion2, "RUT", Pdf("RUT"));
        await SubirAsync(sesion2, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion2, vinculo2.Id);
        var expediente2 = await ExpedienteAbiertoAsync(e, vinculo2.Id);
        var camara2 = Requisito(await EsperarAsync(e.Norte, expediente2, x => x.Estado == "EnRevision"), "CAMARA_COMERCIO");
        await VeredictoAsync<ExpedienteDto>(e, expediente2, camara2.Id, e.Cumplimiento, e.Carlos, new VeredictoDto("Observado", ["ilegible"], "Cortada.", SobreVersion: 1));
        IrA(DateTimeOffset.UtcNow.AddDays(5));
        try
        {
            var apagado = await ApiAsync<ResultadoRevisionRecordatoriosDto>("POST", "/api/v1/plataforma/recordatorios-observaciones/revisar", PlataformaOp,
                new RevisarRecordatoriosDto(e.Norte.Datos.TenantId), 200);
            // Solo el primer expediente (flujo anterior, cada día) vuelve a tocar; el segundo, del flujo apagado, no.
            apagado.Enviados.ShouldBe(1);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        (await EventosAsync(e.Norte.Datos.TenantId, expediente2)).OfType<Dominio.Expedientes.RecordatorioDeObservacionesEnviado>().ShouldBeEmpty();
    }
}
