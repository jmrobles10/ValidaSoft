using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;
using Wolverine.Tracking;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Vigencia periódica del vínculo (VV-1…VV-5), plazo de revalidación configurable (RV-4) y desbloqueo automático con el reloj
/// simulado: el vínculo avisa (<c>PorVencer</c>), vence con su certificado, se abre la renovación y el tercero ratifica o actualiza
/// las secciones de su ficha; el plazo de la actualización sale del requisito más corto; y un bloqueo con fecha se levanta solo.
/// </summary>
public sealed partial class RequisitosApiTests
{
    /// <summary>Corre el barrido del job de vigencias para el tenant (como lo pide el programador) y espera lo que encadene.</summary>
    private Task BarrerVigenciasAsync(Guid tenantId) =>
        Host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).SendMessageAndWaitAsync(new RevisarVigenciasDeVinculos(tenantId));

    /// <summary>Lleva el reloj de las pruebas a <paramref name="momento"/> (las pruebas lo devuelven a cero al terminar).</summary>
    private void IrA(DateTimeOffset momento) => fixture.Reloj.Desfase = momento - DateTimeOffset.UtcNow;

    private Task<VinculoDto> VinculoAsync(Empresa empresa, Guid id) => ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{id}", Admin(empresa), null, 200);

    private async Task<CertificadoDto> CertificadoActualAsync(Empresa empresa, Guid vinculoId) =>
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}/certificado", Admin(empresa), null, 200)).Actual!;

    /// <summary>Flujo CLIENTE publicado con los requisitos y la vigencia del vínculo indicados.</summary>
    private async Task<FlujoDto> FlujoConVigenciaAsync(Empresa empresa, IReadOnlyList<RequisitoDelFlujoDto> requisitos, VigenciaVinculoDto? vigencia)
    {
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(empresa),
            new CrearFlujoDto("HAB_CLIENTE", "Habilitación CLIENTE", "CLIENTE", Requisitos: requisitos, VigenciaVinculo: vigencia), 201);
        return await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(empresa), new { }, 200);
    }

    private sealed record ClienteAprobado(VinculoDto Vinculo, SesionTerceroDto Sesion, string Correo, ConfirmacionCargaDto Rut);

    /// <summary>
    /// Cliente de Norte con su ficha general y el RUT aprobados (Compras decide la general y el RUT; Cumplimiento, el RUT) con el
    /// flujo CLIENTE que ya publicó la prueba.
    /// </summary>
    private async Task<ClienteAprobado> ClienteAprobadoAsync(Escenario e)
    {
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await AceptarAsync(sesion, vinculo.Id);
        var rut = await SubirAsync(sesion, "RUT", Pdf("RUT v1"));

        var inicial = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var revision = await EsperarAsync(e.Norte, inicial, x => x.Estado == "EnRevision" && x.Requisitos!.All(r => r.VersionActual is not null));
        await AreaAsync<ExpedienteDto>(e, inicial, e.Compras, e.Laura, new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int>
        {
            [Requisito(revision, "RUT").Id] = 1, [Requisito(revision, "FICHA_GENERAL").Id] = 1
        }));
        (await AreaAsync<ExpedienteDto>(e, inicial, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(revision, "RUT").Id] = 1 }))).Estado.ShouldBe("Aprobado");
        return new ClienteAprobado(vinculo, sesion, correo, rut);
    }

    private RequisitoDelFlujoDto[] RutYGeneral(Escenario e, int? vigenciaGeneral = null, int? revalidacionGeneral = null) =>
    [
        new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id]),
        new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id], VigenciaDias: vigenciaGeneral, DiasRevalidacion: revalidacionGeneral)
    ];

    private static DateOnly Hoy(DateTimeOffset? ahora = null) => CalculadoraDeVigencia.Fecha(ahora ?? DateTimeOffset.UtcNow);

    // ---------- vigencia periódica: aviso, vencimiento y ratificación (VV-1…VV-3) ----------

    [Fact]
    public async Task La_vigencia_del_vinculo_avisa_vence_con_su_certificado_abre_la_renovacion_y_el_tercero_ratifica_su_ficha_para_volver_a_activo()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var flujo = await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e), new VigenciaVinculoDto(100, [30, 5], 10));
        var configurada = flujo.Publicada!.VigenciaVinculo.ShouldNotBeNull();
        (configurada.Dias, configurada.DiasGracia).ShouldBe((100, 10));
        configurada.DiasAviso.ShouldBe([30, 5]);
        var cliente = await ClienteAprobadoAsync(e);

        try
        {
            // Aprobado: queda fijada la vigencia (100 días) y el certificado no vence después que el vínculo.
            var activo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            activo.Estado.ShouldBe("Activo");
            var hasta = activo.VigenteHasta.ShouldNotBeNull();
            hasta.ShouldBeInRange(Hoy().AddDays(99), Hoy().AddDays(100));
            var emitido = await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id);
            emitido.VigenteHasta.ShouldBe(hasta);

            // Llega el primer aviso (30 días antes): PorVencer, un correo y solo uno aunque el job vuelva a pasar.
            IrA(CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-30)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            await BarrerVigenciasAsync(tenantId);
            (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("PorVencer");
            await fixture.Correos.EsperarAsync(cliente.Correo, m => m.Asunto.StartsWith("Tu validación con Norte S.A.S. vence", StringComparison.Ordinal));
            fixture.Correos.Para(cliente.Correo).Count(m => m.Asunto.Contains(" vence el ", StringComparison.Ordinal)).ShouldBe(1);
            (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VinculoPorVencer>().ShouldHaveSingleItem().AvisosCumplidos.ShouldBe([30]);
            (await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("Vigente");

            // El segundo aviso (5 días antes) mantiene PorVencer.
            IrA(CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-5)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("PorVencer");
            (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VinculoPorVencer>().Count().ShouldBe(2);

            // Pasada la fecha pero dentro de la gracia (10 días) todavía no vence.
            IrA(CalculadoraDeVigencia.MomentoDeVencimiento(hasta.AddDays(10)).AddHours(-3));
            await BarrerVigenciasAsync(tenantId);
            (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("PorVencer");

            // Terminada la gracia: Vencido, certificado vencido y la renovación abierta por el sistema con la ficha por ratificar.
            IrA(CalculadoraDeVigencia.MomentoDeVencimiento(hasta.AddDays(10)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            var vencido = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            vencido.Estado.ShouldBe("Vencido");
            var renovacionId = vencido.ExpedienteAbiertoId.ShouldNotBeNull();
            (await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("Vencido");
            var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{renovacionId}", Admin(e.Norte), null, 200);
            renovacion.Tipo.ShouldBe("Renovacion");
            renovacion.Estado.ShouldBe("Abierto"); // espera al tercero: la ficha ya aceptada no lo cubre sola
            var general = renovacion.Requisitos!.ShouldHaveSingleItem(); // el RUT sigue vigente: no entra
            (general.TipoDocumento.Codigo, general.Estado).ShouldBe(("FICHA_GENERAL", "Pendiente"));
            var evento = (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VinculoVencido>().ShouldHaveSingleItem();
            evento.Causa.ShouldBe(CausaDeVencimiento.Vigencia);
            var aviso = await fixture.Correos.EsperarAsync(cliente.Correo, m => m.Asunto.Contains("venció: ratifica tu información", StringComparison.Ordinal));
            RegexToken().IsMatch(aviso.Texto).ShouldBeTrue(); // con el enlace de acceso a la renovación

            // El tercero ve la sección como ratificable y la ratifica: no cambia ningún dato y la renovación se aprueba sin revisión.
            var vista = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{renovacionId}", cliente.Sesion.AccessToken, null, 200);
            var porRatificar = vista.Requisitos!.ShouldHaveSingleItem();
            (vista.Tipo, porRatificar.TipoRequisito, porRatificar.PuedeRatificar).ShouldBe(("Renovacion", "Seccion", true));
            var ratificada = await TerceroAsync<ExpedientePublicoDto>("POST",
                $"/public/v1/expedientes/{renovacionId}/requisitos/{porRatificar.Id}/ratificar", cliente.Sesion.AccessToken, new { }, 200);
            ratificada.Estado.ShouldBe("Aprobado");
            ratificada.Requisitos!.Single().RatificadoEn.ShouldNotBeNull();

            // Activo otra vez, con vigencia nueva desde esa aprobación y el certificado reemitido (el mismo ratificó, no se editó nada).
            var renovado = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            renovado.Estado.ShouldBe("Activo");
            renovado.ExpedienteAbiertoId.ShouldBeNull();
            renovado.VigenteHasta!.Value.ShouldBeInRange(Hoy(DateTimeOffset.UtcNow + fixture.Reloj.Desfase).AddDays(99), Hoy(DateTimeOffset.UtcNow + fixture.Reloj.Desfase).AddDays(100));
            var reemitido = await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id);
            (reemitido.Estado, reemitido.Emision, reemitido.VigenteHasta).ShouldBe(("Vigente", emitido.Emision + 1, renovado.VigenteHasta));
            var eventos = await EventosAsync(tenantId, renovacionId);
            eventos.OfType<RequisitoRatificado>().ShouldHaveSingleItem().Por.ShouldStartWith("tercero:");
            (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VigenciaDelVinculoFijada>().Count().ShouldBe(2);
            (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{cliente.Vinculo.Id}/historial", Admin(e.Norte), null, 200))
                .Select(h => h.Tipo).ShouldContain("vinculo_por_vencer");

            // Y la ficha del tercero no se tocó: la sección sigue en su versión 1 (aceptada de nuevo por la empresa).
            Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{cliente.Vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200), "FICHA_GENERAL")
                .VersionAceptada.ShouldBe(1);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task El_job_de_Plataforma_con_fecha_simulada_hace_avanzar_la_vigencia_y_el_tercero_puede_actualizar_en_vez_de_ratificar()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        // Sin vigencia periódica; solo la sección general vale 30 días desde que la empresa la acepta.
        await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e, vigenciaGeneral: 30), new VigenciaVinculoDto(0, [5]));
        var cliente = await ClienteAprobadoAsync(e);
        var hasta = (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).VigenteHasta.ShouldNotBeNull();
        hasta.ShouldBeInRange(Hoy().AddDays(29), Hoy().AddDays(30));

        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        // Antes del aviso no hay nada.
        var antes = await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar", plataforma,
            new RevisarVigenciasDto(tenantId, CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-6))), 200);
        antes.ShouldBe(new ResultadoRevisionVigenciasDto(0, 0, 0, 0));

        var aviso = await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar", plataforma,
            new RevisarVigenciasDto(tenantId, CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-5)).AddMinutes(5)), 200);
        aviso.ShouldBe(new ResultadoRevisionVigenciasDto(1, 1, 0, 0));
        (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).Estado.ShouldBe("PorVencer");

        var vence = await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar", plataforma,
            new RevisarVigenciasDto(tenantId, CalculadoraDeVigencia.MomentoDeVencimiento(hasta).AddMinutes(5)), 200);
        vence.ShouldBe(new ResultadoRevisionVigenciasDto(1, 0, 1, 0));
        var vencido = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
        vencido.Estado.ShouldBe("Vencido");

        // Venció solo la vigencia propia de la sección: la renovación pide solo esa sección (el RUT sigue vigente).
        var renovacionId = vencido.ExpedienteAbiertoId.ShouldNotBeNull();
        var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{renovacionId}", Admin(e.Norte), null, 200);
        (renovacion.Tipo, renovacion.Requisitos!.Single().TipoDocumento.Codigo).ShouldBe(("Renovacion", "FICHA_GENERAL"));
        // El job no vuelve a tocarlo.
        (await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar", plataforma,
            new RevisarVigenciasDto(tenantId, CalculadoraDeVigencia.MomentoDeVencimiento(hasta).AddDays(2)), 200)).Revisados.ShouldBe(0);

        // El tercero actualiza la dirección en vez de ratificar: la versión nueva cubre el requisito y Compras la revisa.
        await GuardarSeccionAsync(cliente.Sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"),
            new SoporteSeccionDto(cliente.Rut.Version.DocumentoId, cliente.Rut.Version.N));
        var enRevision = await EsperarAsync(e.Norte, renovacionId, x => Requisito(x, "FICHA_GENERAL").VersionActual?.N == 2);
        (enRevision.Estado, Requisito(enRevision, "FICHA_GENERAL").Estado).ShouldBe(("EnRevision", "EnRevision"));
        // Ya no se puede ratificar: tiene una versión nueva.
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/expedientes/{renovacionId}/requisitos/{Requisito(enRevision, "FICHA_GENERAL").Id}/ratificar",
            cliente.Sesion.AccessToken, new { }, 409))).ShouldBe("estado-invalido");
        await AreaAsync<ExpedienteDto>(e, renovacionId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [Requisito(enRevision, "FICHA_GENERAL").Id] = 2 }));

        var activo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
        activo.Estado.ShouldBe("Activo");
        // La vigencia de la sección corre otra vez desde esta aceptación (la fecha simulada solo movió al job, no el reloj real).
        activo.VigenteHasta!.Value.ShouldBeInRange(Hoy().AddDays(29), Hoy().AddDays(30));
        Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{cliente.Vinculo.TerceroId}/ficha", Admin(e.Norte), null, 200), "FICHA_GENERAL")
            .VersionAceptada.ShouldBe(2);
    }

    [Fact]
    public async Task Un_flujo_con_la_vigencia_mal_configurada_se_rechaza_y_uno_sin_vigencia_no_genera_trabajo_para_el_job()
    {
        var e = await PrepararAsync();
        // Un flujo con vigencia debe pedir una sección; el RUT de otro flujo sin vigencia no se ve afectado.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(e.Norte),
            new CrearFlujoDto("SIN_SECCIONES", "Sin secciones", "CLIENTE",
                Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id])],
                VigenciaVinculo: new VigenciaVinculoDto(365)), 422))).ShouldBe("vigencia-sin-secciones");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(e.Norte),
            new CrearFlujoDto("MAL_AVISO", "Mal aviso", "CLIENTE", Requisitos: RutYGeneral(e), VigenciaVinculo: new VigenciaVinculoDto(30, [30])), 422)))
            .ShouldBe("aviso-vigencia-invalido");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(e.Norte),
            new CrearFlujoDto("MAL_DOC", "Vigencia en un documento", "CLIENTE",
                Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [e.Compras.Id, e.Cumplimiento.Id], VigenciaDias: 10),
                    new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id])]), 422))).ShouldBe("vigencia-no-aplica");

        var proveedor = await ProveedorAprobadoAsync(); // flujo PROVEEDOR sin vigencia periódica
        (await VinculoAsync(proveedor.E.Norte, proveedor.Vinculo.Id)).VigenteHasta.ShouldBeNull();
        var resultado = await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar",
            new Credencial(ApiFixture.ClavePlataforma), new RevisarVigenciasDto(proveedor.E.Norte.Datos.TenantId, DateTimeOffset.UtcNow.AddYears(10)), 200);
        resultado.Revisados.ShouldBe(0);
        (await VinculoAsync(proveedor.E.Norte, proveedor.Vinculo.Id)).Estado.ShouldBe("Activo");
    }

    [Fact]
    public async Task Si_al_vencer_el_flujo_ya_no_tiene_nada_que_ratificar_la_vigencia_se_renueva_sola_sin_abrir_expediente()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var flujo = await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e), new VigenciaVinculoDto(100, [30], 0));
        var cliente = await ClienteAprobadoAsync(e);
        var hasta = (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).VigenteHasta.ShouldNotBeNull();

        // La empresa publica una versión del flujo sin vigencia periódica: al llegar la fecha no hay qué pedirle al tercero.
        await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{flujo.Id}", Admin(e.Norte), new EditarFlujoDto(VigenciaVinculo: new VigenciaVinculoDto(0)), 200);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e.Norte), new { }, 200);

        var resultado = await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar",
            new Credencial(ApiFixture.ClavePlataforma),
            new RevisarVigenciasDto(tenantId, CalculadoraDeVigencia.MomentoDeVencimiento(hasta).AddMinutes(5)), 200);

        resultado.Revisados.ShouldBe(1);
        var vinculo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
        (vinculo.Estado, vinculo.ExpedienteAbiertoId, vinculo.VigenteHasta).ShouldBe(("Activo", null, null));
        (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VinculoVencido>().ShouldBeEmpty();
        (await EventosAsync(tenantId, cliente.Vinculo.Id)).OfType<VigenciaDelVinculoFijada>().Count().ShouldBe(2);
    }

    // ---------- plazo de revalidación configurable (RV-4) ----------

    [Fact]
    public async Task El_plazo_de_revalidacion_sale_del_tipo_o_de_la_seccion_el_menor_de_los_requisitos_y_15_por_omision()
    {
        var e = await PrepararAsync();
        // La sección general da 7 días y el RUT, por su tipo, 30.
        await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e, revalidacionGeneral: 7), null);
        var rutTipo = (await ApiAsync<List<TipoDocumentoTenantDto>>("GET", "/api/v1/tipos-documento", Admin(e.Norte), null, 200)).Single(t => t.Codigo == "RUT");
        rutTipo.DiasRevalidacion.ShouldBe(15);
        (await ApiAsync<TipoDocumentoTenantDto>("PUT", $"/api/v1/tipos-documento/{rutTipo.Id}", Admin(e.Norte),
            new ActualizarTipoDocumentoDto(DiasRevalidacion: 30), 200)).DiasRevalidacion.ShouldBe(30);
        var cliente = await ClienteAprobadoAsync(e);

        // Un RUT nuevo abre la actualización solo con el RUT: 30 días.
        await SubirAsync(cliente.Sesion, "RUT", Pdf("RUT v2"));
        var actualizacionId = await AbiertoEnAsync(e.Norte, cliente.Vinculo.Id, null);
        var porElRut = await EsperarAsync(e.Norte, actualizacionId, x => x.Estado == "EnRevision");
        porElRut.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT"]);
        var fila = (await ApiAsync<PaginaDto<RevalidacionDto>>("GET", "/api/v1/revalidaciones", Admin(e.Norte), null, 200)).Datos
            .Single(r => r.ExpedienteId == actualizacionId);
        (fila.PlazoHasta - fila.Desde).TotalDays.ShouldBe(30, 0.01);
        fila.DiasRestantes.ShouldBeInRange(29, 30);

        // Después cambia la general (7 días) y entra al mismo expediente: rige el menor.
        await GuardarSeccionAsync(cliente.Sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"),
            new SoporteSeccionDto(cliente.Rut.Version.DocumentoId, cliente.Rut.Version.N));
        await EsperarAsync(e.Norte, actualizacionId, x => x.Requisitos!.Count == 2);
        var filas = (await ApiAsync<PaginaDto<RevalidacionDto>>("GET", "/api/v1/revalidaciones", Admin(e.Norte), null, 200)).Datos
            .Where(r => r.ExpedienteId == actualizacionId).ToList();
        filas.Count.ShouldBe(2);
        filas.ShouldAllBe(r => Math.Abs((r.PlazoHasta - r.Desde).TotalDays - 7) < 0.01 && r.DiasRestantes <= 7);

        // El certificado bloquea al vencer ese plazo (el mensaje programado a los 7 días, aquí invocado).
        using (var alcance = Host.Services.CreateScope())
            await alcance.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>()
                .InvokeAsync(new VencerPlazoDeRevalidacion(e.Norte.Datos.TenantId, cliente.Vinculo.Id, actualizacionId));
        (await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id)).Bloqueante.ShouldBeTrue();
    }

    // ---------- desbloqueo automático (VV-5) ----------

    [Fact]
    public async Task Un_bloqueo_con_fecha_se_levanta_solo_con_el_mismo_efecto_del_manual_y_uno_ya_levantado_no_cambia()
    {
        var a = await ProveedorAprobadoAsync();
        var tenantId = a.E.Norte.Datos.TenantId;
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        try
        {
            // Bloqueo con fecha dentro de 2 días: certificado suspendido y el job aún no hace nada.
            var bloqueado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/bloquear", Admin(a.E.Norte),
                new BloquearVinculoDto("Revisión de sanciones", DateTimeOffset.UtcNow.AddDays(2)), 200);
            (bloqueado.Estado, bloqueado.Bloqueo!.Hasta).ShouldBe(("Bloqueado", bloqueado.Bloqueo.Hasta));
            (await CertificadoActualAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Suspendido");
            await BarrerVigenciasAsync(tenantId);
            (await VinculoAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Bloqueado");

            // Al llegar la fecha el job lo restituye: certificado restablecido, evento con su motivo y el vínculo como estaba.
            IrA(bloqueado.Bloqueo.Hasta!.Value.AddMinutes(5));
            await BarrerVigenciasAsync(tenantId);
            var activo = await VinculoAsync(a.E.Norte, a.Vinculo.Id);
            (activo.Estado, activo.Bloqueo).ShouldBe(("Activo", null));
            (await CertificadoActualAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Vigente");
            var desbloqueo = (await EventosAsync(tenantId, a.Vinculo.Id)).OfType<VinculoDesbloqueado>().ShouldHaveSingleItem();
            (desbloqueo.EstadoRestituido, desbloqueo.Justificacion).ShouldBe((EstadoVinculo.Activo, desbloqueo.Justificacion));
            desbloqueo.Justificacion.ShouldContain("bloqueo temporal terminó");
            // Repetir el job no lo vuelve a tocar.
            await BarrerVigenciasAsync(tenantId);
            (await EventosAsync(tenantId, a.Vinculo.Id)).OfType<VinculoDesbloqueado>().Count().ShouldBe(1);

            // Un bloqueo con fecha que se levanta a mano antes: la fecha vieja no hace nada después.
            fixture.Reloj.Desfase = TimeSpan.Zero;
            var otra = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/bloquear", Admin(a.E.Norte),
                new BloquearVinculoDto("Otra revisión", DateTimeOffset.UtcNow.AddDays(1)), 200);
            await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desbloquear", Admin(a.E.Norte), new JustificacionDto("Resuelto antes"), 200);
            IrA(otra.Bloqueo!.Hasta!.Value.AddHours(1));
            (await ApiAsync<ResultadoRevisionVigenciasDto>("POST", "/api/v1/plataforma/vigencias-de-vinculos/revisar", plataforma,
                new RevisarVigenciasDto(tenantId), 200)).Desbloqueados.ShouldBe(0);
            (await EventosAsync(tenantId, a.Vinculo.Id)).OfType<VinculoDesbloqueado>().Count().ShouldBe(2); // el automático y el manual
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task El_mensaje_programado_al_bloquear_levanta_el_bloqueo_sin_esperar_al_barrido()
    {
        var a = await ProveedorAprobadoAsync();
        // La fecha de fin es futura al pedirla y llega en segundos: solo el mensaje programado de Wolverine puede levantarlo.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/bloquear", Admin(a.E.Norte),
            new BloquearVinculoDto("Revisión rápida", DateTimeOffset.UtcNow.AddSeconds(3)), 200);

        var limite = DateTime.UtcNow.AddSeconds(60);
        VinculoDto vinculo;
        while ((vinculo = await VinculoAsync(a.E.Norte, a.Vinculo.Id)).Estado == "Bloqueado")
        {
            DateTime.UtcNow.ShouldBeLessThan(limite, "el mensaje programado no levantó el bloqueo");
            await Task.Delay(500);
        }

        vinculo.Estado.ShouldBe("Activo");
        (await CertificadoActualAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Vigente");
        (await EventosAsync(a.E.Norte.Datos.TenantId, a.Vinculo.Id)).OfType<VinculoDesbloqueado>().ShouldHaveSingleItem();
    }
}
