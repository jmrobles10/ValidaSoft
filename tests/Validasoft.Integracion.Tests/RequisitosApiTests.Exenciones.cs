using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Exención del vínculo (EX-1…EX-12) con PostgreSQL y MinIO reales: la empresa decide no perseguir lo que vence. La Cámara de comercio vence a los 30 días (es la que
/// se usa para los documentos) y la vigencia periódica del vínculo, con el flujo CLIENTE y el reloj simulado, para lo demás. Cada prueba mira un efecto: los
/// requisitos nacen eximidos, no se avisa, no vence el vínculo, al terminar la exención sí, la revocación, los permisos y que sea privada de la empresa.
/// </summary>
public sealed partial class RequisitosApiTests
{
    // ---------- ayudas ----------

    private async Task<Guid> TipoDeDocumentoAsync(Empresa empresa, string codigo) =>
        (await ApiAsync<List<TipoDocumentoTenantDto>>("GET", "/api/v1/tipos-documento", Admin(empresa), null, 200)).Single(t => t.Codigo == codigo).Id;

    private static EximirVinculoDto EximirDocumento(Guid tipoDocumentoId, string justificacion = "El proveedor la renueva por otro canal.", DateOnly? hasta = null) =>
        new(false, [new ObjetivoDeExencionDto("Documento", TipoDocumentoTenantId: tipoDocumentoId)], justificacion, hasta);

    private static EximirVinculoDto EximirTodo(string justificacion = "Proveedor estratégico: no se persigue su renovación.", DateOnly? hasta = null) =>
        new(true, null, justificacion, hasta);

    private Task<ExencionDeVinculoDto> EximirAsync(Empresa empresa, Guid vinculoId, EximirVinculoDto dto) =>
        ApiAsync<ExencionDeVinculoDto>("POST", $"/api/v1/vinculos/{vinculoId}/exenciones", Admin(empresa), dto, 201);

    private Task<ExencionDeVinculoDto> RevocarExencionAsync(Empresa empresa, Guid vinculoId, Guid exencionId, string justificacion = "Ya no aplica.") =>
        ApiAsync<ExencionDeVinculoDto>("POST", $"/api/v1/vinculos/{vinculoId}/exenciones/{exencionId}/revocar", Admin(empresa), new JustificacionDto(justificacion), 200);

    private Task<List<ExencionDeVinculoDto>> ExencionesAsync(Empresa empresa, Guid vinculoId) =>
        ApiAsync<List<ExencionDeVinculoDto>>("GET", $"/api/v1/vinculos/{vinculoId}/exenciones", Admin(empresa), null, 200);

    private Task<List<VencimientoDto>> VencimientosAsync(Empresa empresa, string filtro = "") =>
        ApiAsync<List<VencimientoDto>>("GET", $"/api/v1/vencimientos?dias=0{filtro}", Admin(empresa), null, 200);

    private async Task InvocarMensajeAsync(object mensaje)
    {
        using var alcance = Host.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(mensaje);
    }

    /// <summary>La vigencia (para el vínculo) de la versión 1 del documento del canónico, esperando a que la bóveda la calcule.</summary>
    private async Task<(VigenciaDeVersion Vigencia, Guid DocumentoId)> VigenciaDeAsync(Aprobado a, string codigo)
    {
        var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{a.Expediente}", Admin(a.E.Norte), null, 200);
        var documentoId = expediente.Requisitos!.Single(r => r.TipoDocumento.Codigo == codigo).VersionActual!.DocumentoId;
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            await using (var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
                if ((await boveda.LoadAsync<Documento>(documentoId))!.Buscar(1)!.VigenciaPara(a.Vinculo.Id) is { VigenteHasta: not null } vigencia)
                    return (vigencia, documentoId);
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"{codigo} no tiene vigencia para el vínculo.");
            await Task.Delay(200);
        }
    }

    /// <summary>Dispara el vencimiento de la versión 1 para el vínculo (el mensaje programado, aquí invocado) con el reloj ya adelantado.</summary>
    private Task VencerVersionAsync(Guid documentoId, Guid vinculoId, VigenciaDeVersion vigencia) =>
        InvocarMensajeAsync(new VencerVersionDeDocumento(documentoId, 1, vinculoId, vigencia.VigenteHasta!.Value, vigencia.DiasGracia));

    private async Task<IReadOnlyList<object>> EventosDelDocumentoAsync(Guid documentoId)
    {
        await using var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda);
        return [.. (await boveda.Events.FetchStreamAsync(documentoId)).Select(x => x.Data)];
    }

    /// <summary>Adelanta el reloj al día siguiente del fin de la gracia (la hora en que vence lo que ya cumplió su plazo) más unos minutos.</summary>
    private void IrAlVencimientoDe(DateOnly finDeGracia) => IrA(CalculadoraDeVigencia.MomentoDeVencimiento(finDeGracia).AddMinutes(10));

    private async Task<VinculoDto> EsperarVinculoAsync(Empresa empresa, Guid vinculoId, Func<VinculoDto, bool> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var vinculo = await VinculoAsync(empresa, vinculoId);
            if (condicion(vinculo))
                return vinculo;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El vínculo no llegó a: {que} (está {vinculo.Estado}).");
            await Task.Delay(200);
        }
    }

    // ---------- EX-3: lo eximido nace eximido y cierra lo abierto ----------

    [Fact]
    public async Task Eximir_lo_que_vencio_aprueba_la_renovacion_abierta_cita_la_exencion_y_el_vinculo_vuelve_a_activo_sin_mostrar_la_justificacion_afuera()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
            var (_, renovacionId) = await VencerAsync(a, "CAMARA_COMERCIO"); // vence a los 30 días: el vínculo queda Vencido con su renovación abierta
            (await VinculoAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Vencido");

            var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo, "La Cámara la gestiona el área legal del proveedor."));
            exencion.ShouldSatisfyAllConditions(
                e => e.Estado.ShouldBe("Vigente"),
                e => e.Todos.ShouldBeFalse(),
                e => e.Objetivos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
                    o => o.Tipo.ShouldBe("Documento"), o => o.Codigo.ShouldBe("CAMARA_COMERCIO"), o => o.TipoDocumentoTenantId.ShouldBe(camaraTipo)),
                e => e.Justificacion.ShouldBe("La Cámara la gestiona el área legal del proveedor."),
                e => e.Hasta.ShouldBeNull(),
                e => e.Por.ShouldNotBeNullOrWhiteSpace());

            // Lo pendiente de la renovación se eximió en el acto: el expediente se aprobó y el vínculo volvió a Activo.
            var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{renovacionId}", Admin(a.E.Norte), null, 200);
            renovacion.Estado.ShouldBe("Aprobado");
            var camara = renovacion.Requisitos!.ShouldHaveSingleItem();
            camara.Estado.ShouldBe("Eximido");
            camara.Exencion!.ShouldSatisfyAllConditions(
                x => x.ExencionDeVinculoId.ShouldBe(exencion.Id),
                x => x.Justificacion.ShouldContain("La Cámara la gestiona el área legal del proveedor."));
            var activo = await VinculoAsync(a.E.Norte, a.Vinculo.Id);
            activo.Estado.ShouldBe("Activo");
            activo.ExpedienteAbiertoId.ShouldBeNull();
            activo.Exenciones.ShouldNotBeNull().ShouldHaveSingleItem().Id.ShouldBe(exencion.Id);

            // El certificado se reemitió con la Cámara «eximida por decisión de la empresa»: la justificación es interna.
            var certificado = await CertificadoActualAsync(a.E.Norte, a.Vinculo.Id);
            certificado.Emision.ShouldBe(a.Emision + 1);
            var enElCertificado = certificado.Requisitos.Single(r => r.Codigo == "CAMARA_COMERCIO");
            (enElCertificado.Eximido, enElCertificado.Justificacion).ShouldBe((true, "Eximido por decisión de la empresa."));

            // Nada de esto llega al tercero: su vista del expediente no dice ni la justificación ni la exención.
            var texto = await (await TerceroAsync("GET", $"/public/v1/expedientes/{renovacionId}", a.Sesion.AccessToken, null, 200)).ReadAsTextAsync();
            texto.ShouldNotContain("área legal del proveedor");
            texto.ShouldNotContain(exencion.Id.ToString());
            texto.ShouldNotContain("exenci", Case.Insensitive);

            // Queda en la historia del vínculo (auditable) con quién la hizo.
            var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}/historial", Admin(a.E.Norte), null, 200);
            historial.Select(h => h.Tipo).ShouldContain("exencion_de_vinculo_registrada");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    // ---------- EX-4: mientras rige, el vencimiento del documento no vence al vínculo; al revocar, sí ----------

    [Fact]
    public async Task Con_una_exencion_vigente_el_documento_vencido_no_vence_al_vinculo_y_la_bandeja_lo_marca_y_al_revocarla_el_vinculo_vence()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
            var (camara, documentoId) = await VigenciaDeAsync(a, "CAMARA_COMERCIO");
            await VigenciaDeAsync(a, "RUT");
            var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo));

            // La bandeja de vencimientos marca la Cámara como exenta y no el RUT; el filtro separa una de otro.
            var filas = await VencimientosAsync(a.E.Norte);
            filas.Count.ShouldBe(2);
            var filaCamara = filas.Single(f => f.Exencion is not null);
            filaCamara.Exencion!.ShouldSatisfyAllConditions(x => x.Id.ShouldBe(exencion.Id), x => x.Todos.ShouldBeFalse(), x => x.Hasta.ShouldBeNull());
            filaCamara.DocumentoId.ShouldBe(documentoId);
            var filaRut = filas.Single(f => f.Exencion is null);
            (await VencimientosAsync(a.E.Norte, "&exento=true")).ShouldHaveSingleItem().DocumentoId.ShouldBe(documentoId);
            (await VencimientosAsync(a.E.Norte, "&exento=false")).ShouldHaveSingleItem().DocumentoId.ShouldBe(filaRut.DocumentoId);

            // Recordar al tercero un documento exento se omite; el que no lo está, sale.
            (await ApiAsync<RecordatorioEnviadoDto>("POST", "/api/v1/vencimientos/recordar", Admin(a.E.Norte),
                new RecordarVencimientosDto([new VencimientoARecordarDto(a.Vinculo.Id, documentoId)]), 200)).ShouldBe(new RecordatorioEnviadoDto(0, 0, 1));
            (await ApiAsync<RecordatorioEnviadoDto>("POST", "/api/v1/vencimientos/recordar", Admin(a.E.Norte),
                new RecordarVencimientosDto([new VencimientoARecordarDto(a.Vinculo.Id, filaRut.DocumentoId)]), 200)).ShouldBe(new RecordatorioEnviadoDto(1, 1, 0));

            // Pasa la gracia de la Cámara: la bóveda no la marca vencida para este vínculo ni se abre su renovación.
            IrAlVencimientoDe(camara.FinDeGracia!.Value);
            await VencerVersionAsync(documentoId, a.Vinculo.Id, camara);
            (await EventosDelDocumentoAsync(documentoId)).OfType<VersionVencida>().ShouldBeEmpty();
            var sigueActivo = await VinculoAsync(a.E.Norte, a.Vinculo.Id);
            (sigueActivo.Estado, sigueActivo.ExpedienteAbiertoId).ShouldBe(("Activo", null));
            (await VencimientosAsync(a.E.Norte, "&exento=true")).Single().Estado.ShouldBe("Vencida"); // la fecha pasó; la exención dice que no se persigue

            // El inicio no la cuenta entre lo vencido de la empresa.
            (await ApiAsync<MisPendientesDto>("GET", "/api/v1/mis-pendientes", Admin(a.E.Norte), null, 200)).DocumentosVencidos.ShouldBe(0);

            // Revocarla vuelve a regir las vigencias: la Cámara vence ya, el vínculo vence y se abre su renovación.
            var revocada = await RevocarExencionAsync(a.E.Norte, a.Vinculo.Id, exencion.Id, "Ya no se exime.");
            (revocada.Estado, revocada.RevocadaPor, revocada.JustificacionDeRevocacion).ShouldBe(("Revocada", revocada.RevocadaPor, "Ya no se exime."));
            revocada.RevocadaPor.ShouldNotBeNullOrWhiteSpace();
            var vencido = await EsperarVinculoAsync(a.E.Norte, a.Vinculo.Id, v => v is { Estado: "Vencido", ExpedienteAbiertoId: not null }, "vencido con su renovación");
            var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{vencido.ExpedienteAbiertoId}", Admin(a.E.Norte), null, 200);
            renovacion.Tipo.ShouldBe("Renovacion");
            renovacion.Requisitos!.ShouldHaveSingleItem().Estado.ShouldBe("Pendiente"); // ya no está eximida: el tercero debe renovarla
            (await EventosDelDocumentoAsync(documentoId)).OfType<VersionVencida>().ShouldHaveSingleItem();

            // Una exención revocada no se revoca de nuevo.
            (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones/{exencion.Id}/revocar", Admin(a.E.Norte),
                new JustificacionDto("Otra vez"), 409))).ShouldBe("exencion-ya-revocada");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Una_exencion_con_fecha_deja_de_regir_sola_el_job_recalcula_el_vinculo_y_este_vence_con_su_renovacion()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
            var (camara, documentoId) = await VigenciaDeAsync(a, "CAMARA_COMERCIO");
            var hasta = camara.FinDeGracia!.Value.AddDays(3);
            var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo, hasta: hasta));
            exencion.Hasta.ShouldBe(hasta);

            // Con la Cámara ya vencida pero la exención vigente: nada cambia, ni con el barrido del job.
            IrAlVencimientoDe(camara.FinDeGracia!.Value);
            await VencerVersionAsync(documentoId, a.Vinculo.Id, camara);
            await BarrerVigenciasAsync(a.E.Norte.Datos.TenantId);
            (await VinculoAsync(a.E.Norte, a.Vinculo.Id)).ShouldSatisfyAllConditions(v => v.Estado.ShouldBe("Activo"), v => v.ExpedienteAbiertoId.ShouldBeNull());
            (await ExencionesAsync(a.E.Norte, a.Vinculo.Id)).Single().Estado.ShouldBe("Vigente");

            // Pasa el último día de la exención (inclusive): el job la registra vencida y, en la misma pasada, el vínculo vence con su renovación.
            IrAlVencimientoDe(hasta);
            await BarrerVigenciasAsync(a.E.Norte.Datos.TenantId);
            var exenciones = await ExencionesAsync(a.E.Norte, a.Vinculo.Id);
            exenciones.Single().ShouldSatisfyAllConditions(e => e.Estado.ShouldBe("Vencida"), e => e.VencidaEn.ShouldNotBeNull());
            var vencido = await EsperarVinculoAsync(a.E.Norte, a.Vinculo.Id, v => v is { Estado: "Vencido", ExpedienteAbiertoId: not null }, "vencido al terminar la exención");
            (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{vencido.ExpedienteAbiertoId}", Admin(a.E.Norte), null, 200)).Tipo.ShouldBe("Renovacion");
            (await EventosAsync(a.E.Norte.Datos.TenantId, a.Vinculo.Id)).OfType<ExencionDeVinculoVencida>().ShouldHaveSingleItem().Hasta.ShouldBe(hasta);

            // El barrido repetido no vuelve a registrar nada.
            await BarrerVigenciasAsync(a.E.Norte.Datos.TenantId);
            (await EventosAsync(a.E.Norte.Datos.TenantId, a.Vinculo.Id)).OfType<ExencionDeVinculoVencida>().Count().ShouldBe(1);
            // Y una exención vencida ya no se revoca.
            (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones/{exencion.Id}/revocar", Admin(a.E.Norte),
                new JustificacionDto("Tarde"), 409))).ShouldBe("exencion-vencida");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Al_terminar_la_fecha_de_una_exencion_los_administradores_de_la_empresa_reciben_la_campana_y_el_correo()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            var administrador = await PersonaConRolAsync(a.E.Norte, "Ana Administradora", "Administrador");
            var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
            var hoy = Hoy();
            var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo, hasta: hoy)); // dura hasta el final de hoy

            // Pasa la medianoche de negocio. (Cerca: las notificaciones más viejas de un día no se envían.)
            IrA(CalculadoraDeVigencia.MomentoDeVencimiento(hoy).AddSeconds(5));
            await BarrerVigenciasAsync(a.E.Norte.Datos.TenantId);
            fixture.Reloj.Desfase = TimeSpan.Zero; // las aserciones de las personas se firman con la hora real

            (await ExencionesAsync(a.E.Norte, a.Vinculo.Id)).Single().Estado.ShouldBe("Vencida");
            (await VinculoAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Activo"); // la Cámara sigue vigente: no había nada que vencer
            var bandeja = await EsperarCampanaDeAsync(a.E, administrador, x => x.Datos.Any(n => n.Tipo == "vinculo.exencion_vencida" && n.VinculoId == a.Vinculo.Id));
            var aviso = bandeja.Datos.Single(n => n.Tipo == "vinculo.exencion_vencida");
            aviso.Titulo.ShouldBe("Terminó una exención");
            aviso.Mensaje.ShouldContain("Certificado de existencia y representación legal");
            aviso.Mensaje.ShouldContain($"terminó el {hoy:dd/MM/yyyy}");
            var correo = await fixture.Correos.EsperarAsync(administrador.Usuario.Correo, m => m.Asunto.Contains("exención", StringComparison.OrdinalIgnoreCase));
            correo.Texto.ShouldContain("Certificado de existencia y representación legal");

            // El tercero no se entera: es una decisión privada de la empresa.
            var delTercero = await EsperarBandejaAsync(a.Sesion, _ => true);
            delTercero.Datos.ShouldNotContain(n => n.Tipo.Contains("exencion", StringComparison.OrdinalIgnoreCase));
            fixture.Correos.Para(a.Vinculo.CorreoTercero).ShouldNotContain(m => m.Asunto.Contains("exención", StringComparison.OrdinalIgnoreCase));

            // Idempotente: otra pasada no repite el aviso.
            await BarrerVigenciasAsync(a.E.Norte.Datos.TenantId);
            (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(a.E.Norte, administrador), null, 200)).Datos
                .Count(n => n.Tipo == "vinculo.exencion_vencida").ShouldBe(1);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    // ---------- EX-2: lo eximido no se avisa ----------

    [Fact]
    public async Task El_aviso_de_vencimiento_de_un_documento_eximido_no_sale_y_al_revocar_el_siguiente_si_sale()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
            var (camara, documentoId) = await VigenciaDeAsync(a, "CAMARA_COMERCIO");
            var fechas = CalculadoraDeVigencia.FechasDeAviso(camara.VigenteHasta!.Value, camara.DiasAviso).Where(f => f > Hoy()).ToList();
            fechas.Count.ShouldBeGreaterThanOrEqualTo(2);
            var (primero, segundo) = (fechas[0], fechas[1]);
            var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo));
            var correoDelTercero = a.Vinculo.CorreoTercero;

            // Llega el primer día de aviso con la Cámara eximida: ni correo al tercero ni constancia para el evento documento.por_vencer (y con él, la campana).
            IrA(CalendarioDeVigencias.MomentoDeAviso(primero).AddMinutes(10));
            var correosAntes = fixture.Correos.Para(correoDelTercero).Count(m => m.Asunto.Contains("vencer"));
            var avisadosAntes = (await EventosDelDocumentoAsync(documentoId)).OfType<VencimientoAvisado>().Count(); // el aviso de hoy (30 días antes) pudo salir al cargar
            await InvocarMensajeAsync(new EnviarAvisosDeVencimiento(a.Sesion.TerceroId, primero));
            await using (var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
                (await boveda.LoadAsync<AvisoDeVencimiento>(AvisoDeVencimiento.Clave(a.Sesion.TerceroId, primero)))!.Estado.ShouldBe(EstadoAviso.SinDocumentos);
            fixture.Correos.Para(correoDelTercero).Count(m => m.Asunto.Contains("vencer")).ShouldBe(correosAntes);
            (await EventosDelDocumentoAsync(documentoId)).OfType<VencimientoAvisado>().Count().ShouldBe(avisadosAntes);

            // Se revoca la exención: el siguiente aviso sale como siempre.
            await RevocarExencionAsync(a.E.Norte, a.Vinculo.Id, exencion.Id);
            IrA(CalendarioDeVigencias.MomentoDeAviso(segundo).AddMinutes(10));
            await InvocarMensajeAsync(new EnviarAvisosDeVencimiento(a.Sesion.TerceroId, segundo));
            await using (var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
                (await boveda.LoadAsync<AvisoDeVencimiento>(AvisoDeVencimiento.Clave(a.Sesion.TerceroId, segundo)))!.Estado.ShouldBe(EstadoAviso.Enviado);
            var correo = await fixture.Correos.EsperarAsync(correoDelTercero, m => m.Asunto.Contains("vencer") && m.Texto.Contains(a.E.Norte.Datos.Nombre));
            correo.Texto.ShouldContain("vence para Norte S.A.S.");
            var avisados = (await EventosDelDocumentoAsync(documentoId)).OfType<VencimientoAvisado>().ToList();
            avisados.Count.ShouldBe(avisadosAntes + 1);
            (avisados[^1].VinculoId, avisados[^1].DiasRestantes).ShouldBe((a.Vinculo.Id, camara.VigenteHasta!.Value.DayNumber - segundo.DayNumber));
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    // ---------- EX-3: una versión nueva de lo eximido no abre una revalidación ----------

    [Fact]
    public async Task Una_version_nueva_de_un_documento_eximido_no_abre_una_actualizacion_y_al_revocar_la_exencion_la_siguiente_si()
    {
        var a = await ProveedorAprobadoAsync();
        var rutTipo = await TipoDeDocumentoAsync(a.E.Norte, "RUT");
        var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(rutTipo));

        // El tercero actualiza su RUT: la bóveda lo comparte, pero la empresa no lo persigue ni lo revisa.
        await SubirAsync(a.Sesion, "RUT", Pdf("RUT v2"));
        await EsperarAsync(async () => (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}/documentos", Admin(a.E.Norte), null, 200))
            .Any(d => d.Tipo.Codigo == "RUT" && d.VersionesCompartidas.Any(v => v.N == 2)), "la versión 2 del RUT compartida con el vínculo");
        await Task.Delay(1500); // lo que llegaría por la sincronización del outbox
        var sinRevision = await VinculoAsync(a.E.Norte, a.Vinculo.Id);
        (sinRevision.Estado, sinRevision.ExpedienteAbiertoId, sinRevision.EnRevision).ShouldBe(("Activo", null, false));
        (await CertificadoActualAsync(a.E.Norte, a.Vinculo.Id)).Estado.ShouldBe("Vigente");

        // Sin la exención, la misma actualización sí se abre (control: el circuito funciona).
        await RevocarExencionAsync(a.E.Norte, a.Vinculo.Id, exencion.Id);
        await SubirAsync(a.Sesion, "RUT", Pdf("RUT v3"));
        var enRevision = await EsperarVinculoAsync(a.E.Norte, a.Vinculo.Id, v => v.ExpedienteAbiertoId is not null, "una actualización abierta");
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{enRevision.ExpedienteAbiertoId}", Admin(a.E.Norte), null, 200)).Tipo.ShouldBe("Actualizacion");
    }

    // ---------- EX-4: la vigencia periódica ----------

    [Fact]
    public async Task Con_una_exencion_total_la_vigencia_periodica_no_avisa_ni_vence_ni_vence_el_certificado_para_la_consulta_y_al_revocar_el_vinculo_vence()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        try
        {
            await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e), new VigenciaVinculoDto(100, [30, 5], 10));
            var cliente = await ClienteAprobadoAsync(e);
            var hasta = (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).VigenteHasta.ShouldNotBeNull();

            // La campaña de renovación lo elegiría; eximido de todo, ya no.
            (await ApiAsync<VistaPreviaCampaniaDto>("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(120), 200))
                .Filas.ShouldContain(f => f.VinculoId == cliente.Vinculo.Id);
            var exencion = await EximirAsync(e.Norte, cliente.Vinculo.Id, EximirTodo());
            exencion.ShouldSatisfyAllConditions(x => x.Todos.ShouldBeTrue(), x => x.Objetivos.ShouldBeEmpty());
            (await ApiAsync<VistaPreviaCampaniaDto>("POST", "/api/v1/campanias/vista-previa", Admin(e.Norte), new FiltroDeCampaniaDto(120), 200))
                .Filas.ShouldNotContain(f => f.VinculoId == cliente.Vinculo.Id);

            // Pasa el primer aviso (30 días antes): ni PorVencer, ni renovación, ni correo de pre-vencimiento.
            IrA(CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-30)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            var sinAviso = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            (sinAviso.Estado, sinAviso.ExpedienteAbiertoId).ShouldBe(("Activo", null));
            fixture.Correos.Para(cliente.Correo).ShouldNotContain(m => m.Asunto.StartsWith("Ratifica tu información", StringComparison.Ordinal));

            // Pasa la fecha y la gracia: tampoco vence.
            IrA(CalculadoraDeVigencia.MomentoDeVencimiento(hasta.AddDays(10)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            var seguiaActivo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            (seguiaActivo.Estado, seguiaActivo.ExpedienteAbiertoId).ShouldBe(("Activo", null));
            var eventos = await EventosAsync(tenantId, cliente.Vinculo.Id);
            eventos.OfType<VinculoPorVencer>().ShouldBeEmpty();
            eventos.OfType<VinculoVencido>().ShouldBeEmpty();

            // El certificado sellado sí venció por su fecha, pero la consulta de certificación no bloquea: solo venció por lo que la empresa eximió.
            await EsperarAsync(async () => (await CertificadoActualAsync(e.Norte, cliente.Vinculo.Id)).Estado == "Vencido", "el certificado vencido por su fecha");
            var exento = await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={cliente.Vinculo.Id}", Admin(e.Norte), null, 200);
            exento.ShouldSatisfyAllConditions(
                c => c.Estado.ShouldBe("Vencido"),
                c => c.EstadoVinculo.ShouldBe("Activo"),
                c => c.Exento.ShouldBeTrue(),
                c => c.Bloqueado.ShouldBeFalse(),
                c => c.MotivoBloqueo.ShouldBeNull());

            // Revocada, la vigencia vuelve a regir de inmediato: el vínculo vence con su renovación y la consulta bloquea.
            await RevocarExencionAsync(e.Norte, cliente.Vinculo.Id, exencion.Id);
            var vencido = await EsperarVinculoAsync(e.Norte, cliente.Vinculo.Id, v => v is { Estado: "Vencido", ExpedienteAbiertoId: not null }, "vencido por su vigencia");
            (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{vencido.ExpedienteAbiertoId}", Admin(e.Norte), null, 200)).Tipo.ShouldBe("Renovacion");
            var bloqueado = await ApiAsync<ConsultaCertificacionDto>("GET", $"/api/v1/consultas/certificacion?vinculoId={cliente.Vinculo.Id}", Admin(e.Norte), null, 200);
            (bloqueado.Exento, bloqueado.Bloqueado, bloqueado.MotivoBloqueo).ShouldBe((false, true, "Vencido"));
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task Eximir_la_unica_seccion_por_ratificar_renueva_la_vigencia_sola_sin_pre_vencimiento_ni_renovacion()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        try
        {
            await FlujoConVigenciaAsync(e.Norte, RutYGeneral(e), new VigenciaVinculoDto(100, [30, 5], 10));
            var cliente = await ClienteAprobadoAsync(e);
            var hasta = (await VinculoAsync(e.Norte, cliente.Vinculo.Id)).VigenteHasta.ShouldNotBeNull();
            var exencion = await EximirAsync(e.Norte, cliente.Vinculo.Id, new EximirVinculoDto(false, [new ObjetivoDeExencionDto("Seccion", Seccion: "ficha_general")],
                "La ficha general la actualiza Compras a mano.", null));
            exencion.Objetivos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(o => o.Tipo.ShouldBe("Seccion"), o => o.Seccion.ShouldBe("FICHA_GENERAL"));

            // En el primer aviso lo único por ratificar sería la ficha general, que está eximida: no hay nada que pedir y la vigencia corre de nuevo desde hoy.
            IrA(CalculadoraDeVigencia.MomentoDeAviso(hasta.AddDays(-30)).AddMinutes(10));
            await BarrerVigenciasAsync(tenantId);
            var vinculo = await VinculoAsync(e.Norte, cliente.Vinculo.Id);
            vinculo.ShouldSatisfyAllConditions(
                v => v.Estado.ShouldBe("Activo"),
                v => v.ExpedienteAbiertoId.ShouldBeNull(),
                v => v.VigenteHasta.ShouldNotBeNull().ShouldBeGreaterThan(hasta));
            var eventos = await EventosAsync(tenantId, cliente.Vinculo.Id);
            eventos.OfType<VinculoPorVencer>().ShouldBeEmpty();
            eventos.OfType<ExpedienteVinculado>().ShouldHaveSingleItem(); // solo el inicial
            fixture.Correos.Para(cliente.Correo).ShouldNotContain(m => m.Asunto.StartsWith("Ratifica tu información", StringComparison.Ordinal));
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    // ---------- EX-5: permisos, reglas del vínculo y privacidad entre empresas ----------

    [Fact]
    public async Task Solo_el_administrador_o_una_llave_con_el_permiso_eximen_y_revocan_y_un_vinculo_no_apto_no_admite_la_exencion()
    {
        var a = await ProveedorAprobadoAsync();
        var e = a.E;
        var camaraTipo = await TipoDeDocumentoAsync(e.Norte, "CAMARA_COMERCIO");
        var url = $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones";
        var cuerpo = EximirDocumento(camaraTipo);

        // Los revisores (y la llave del sistema, sin el permiso) no eximen; sí leen.
        await ApiAsync("POST", url, Delegado(e.Norte, e.Laura), cuerpo, 403);
        await ApiAsync("POST", url, Delegado(e.Norte, e.Carlos), cuerpo, 403);
        await ApiAsync("POST", url, new Credencial(e.Norte.LlaveSrm, e.Norte.Datos.Id), cuerpo, 403);
        (await ApiAsync<List<ExencionDeVinculoDto>>("GET", url, Delegado(e.Norte, e.Laura), null, 200)).ShouldBeEmpty();

        // Una llave a la que se le marca el permiso nuevo sí; el administrador, también.
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Norte.Admin),
            new CrearApiKeyDto("Eximir", ["vinculos:leer", "vinculos:eximir"], null, null), 201);
        var conPermiso = new Credencial(llave.Valor!, e.Norte.Datos.Id);
        var primera = await ApiAsync<ExencionDeVinculoDto>("POST", url, conPermiso, cuerpo, 201);
        await ApiAsync("POST", $"{url}/{primera.Id}/revocar", Delegado(e.Norte, e.Carlos), new JustificacionDto("No me toca"), 403);
        (await ApiAsync<ExencionDeVinculoDto>("POST", $"{url}/{primera.Id}/revocar", Admin(e.Norte), new JustificacionDto("Revoca el administrador"), 200)).Estado
            .ShouldBe("Revocada");

        // Validaciones.
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirDocumento(camaraTipo, " "), 422))).ShouldBe("justificacion-requerida");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), new EximirVinculoDto(false, [], "x", null), 422))).ShouldBe("exencion-sin-alcance");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), new EximirVinculoDto(true, [new ObjetivoDeExencionDto("Documento", camaraTipo)], "x", null), 422)))
            .ShouldBe("exencion-alcance-invalido");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), new EximirVinculoDto(false, [new ObjetivoDeExencionDto("Cosa")], "x", null), 422)))
            .ShouldBe("objetivo-invalido");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), new EximirVinculoDto(false, [new ObjetivoDeExencionDto("Documento", Guid.NewGuid())], "x", null), 422)))
            .ShouldBe("tipo-documento-invalido");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), new EximirVinculoDto(false, [new ObjetivoDeExencionDto("Seccion", Seccion: "FICHA_X")], "x", null), 422)))
            .ShouldBe("seccion-invalida");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirDocumento(camaraTipo, hasta: Hoy().AddDays(-1)), 422))).ShouldBe("fecha-invalida");
        (await CodigoAsync(await ApiAsync("POST", $"{url}/{Guid.NewGuid()}/revocar", Admin(e.Norte), new JustificacionDto("x"), 404))).ShouldBe("exencion-no-encontrada");

        // Una exención por requisito a la vez.
        var vigente = await EximirAsync(e.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo));
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirDocumento(camaraTipo), 409))).ShouldBe("exencion-superpuesta");
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirTodo(), 409))).ShouldBe("exencion-superpuesta");
        (await ExencionesAsync(e.Norte, a.Vinculo.Id)).Select(x => (x.Id, x.Estado)).ShouldBe([(vigente.Id, "Vigente"), (primera.Id, "Revocada")]);
        (await ApiAsync<List<ExencionDeVinculoDto>>("GET", $"{url}?vigentes=true", Admin(e.Norte), null, 200)).ShouldHaveSingleItem().Id.ShouldBe(vigente.Id);

        // Un vínculo bloqueado, terminado o que nunca se aprobó no admite la exención.
        await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/bloquear", Admin(e.Norte), new BloquearVinculoDto("Investigación", null), 200);
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirDocumento(camaraTipo), 409))).ShouldBe("vinculo-bloqueado");
        await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desbloquear", Admin(e.Norte), new JustificacionDto("Se levanta"), 200);
        await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desvincular", Admin(e.Norte), new MotivoDto("Terminó el contrato"), 200);
        (await CodigoAsync(await ApiAsync("POST", url, Admin(e.Norte), EximirTodo(), 409))).ShouldBe("estado-terminal");

        var nuevo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{nuevo.Id}/exenciones", Admin(e.Norte), EximirTodo(), 409))).ShouldBe("vinculo-sin-aprobacion");
    }

    [Fact]
    public async Task La_exencion_es_privada_de_la_empresa_otra_empresa_del_grupo_ni_otro_grupo_la_ven_ni_la_tocan_y_el_tercero_no_la_ve()
    {
        var a = await ProveedorAprobadoAsync();
        var camaraTipo = await TipoDeDocumentoAsync(a.E.Norte, "CAMARA_COMERCIO");
        var exencion = await EximirAsync(a.E.Norte, a.Vinculo.Id, EximirDocumento(camaraTipo, "Texto interno que nadie más debe ver."));

        // Otra empresa del mismo grupo y otro grupo: el vínculo no existe para ellas (404 como cualquier recurso ajeno), así que tampoco su exención.
        var sur = await OtraEmpresaDelTenantAsync(a.E.Norte, "Sur S.A.S.", "SUR", Nit());
        var otroGrupo = await EmpresaNuevaAsync("Oriente S.A.S.", "ORI");
        foreach (var ajena in new[] { sur, otroGrupo })
        {
            await ApiAsync("GET", $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones", Admin(ajena), null, 404);
            await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones", Admin(ajena), EximirTodo(), 404);
            await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/exenciones/{exencion.Id}/revocar", Admin(ajena), new JustificacionDto("No es mía"), 404);
            await ApiAsync("GET", $"/api/v1/vinculos/{a.Vinculo.Id}", Admin(ajena), null, 404);
        }

        // El mismo tercero con otra empresa del grupo no hereda nada: su vínculo no está eximido ni su bandeja marca exención.
        var correoSur = CorreoNuevo();
        var delTercero = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(sur), new CrearVinculoDto(a.Vinculo.TerceroId, null, "PROVEEDOR", null, correoSur, null), 201);
        delTercero.Exenciones.ShouldBeNull();
        (await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos?dias=0&exento=true", Admin(sur), null, 200)).ShouldBeEmpty();

        // El tercero no ve la exención en nada de lo suyo: ni el vínculo, ni su expediente, ni su bóveda.
        foreach (var url in new[] { "/public/v1/vinculos", $"/public/v1/expedientes/{a.Expediente}", "/public/v1/boveda" })
        {
            var texto = await (await TerceroAsync("GET", url, a.Sesion.AccessToken, null, 200)).ReadAsTextAsync();
            texto.ShouldNotContain("Texto interno");
            texto.ShouldNotContain(exencion.Id.ToString());
            texto.ShouldNotContain("exenci", Case.Insensitive);
        }
    }
}
