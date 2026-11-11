using System.Text.Json;
using JasperFx.Events;
using Shouldly;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Campana del tercero (NT-1): qué hecho produce qué aviso en su portal, siempre de la empresa que lo origina (su tenant, su marca) y
/// sin nombres de revisores ni de áreas; sin correo propio (los siguen mandando sus disparadores). Además, el aviso a las áreas
/// cuando el tercero responde llega a las que revisan lo corregido (NT-4).
/// </summary>
public class NotificacionesDelTerceroTests
{
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid EmpresaA = Guid.NewGuid();
    private static readonly Guid EmpresaB = Guid.NewGuid();
    private static readonly Guid VinculoA = Guid.NewGuid();
    private static readonly Guid VinculoB = Guid.NewGuid();
    private static readonly Guid ExpedienteId = Guid.NewGuid();
    private static readonly Guid TerceroId = Guid.NewGuid();
    private static readonly Guid DocumentoId = Guid.NewGuid();
    private static readonly Guid RequisitoId = Guid.NewGuid();
    private static readonly Guid AreaCompras = Guid.NewGuid();
    private static readonly Guid AreaCumplimiento = Guid.NewGuid();
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 15, 0, 0, TimeSpan.Zero);
    private const string Revisor = "usuario-revisor-7f3a";

    private static async Task<PlanDeNotificaciones> PlanearAsync(object datos, Guid stream, ContextoFalso contexto, string? tenant = null) =>
        await PlanificadorDeNotificacionesDelTercero.PlanearAsync(Evento(datos, stream, tenant ?? TenantA.ToString("N")), contexto, 7, CancellationToken.None);

    private static Notificacion Unica(PlanDeNotificaciones plan, Guid? tenant = null)
    {
        plan.Correos.ShouldBeEmpty(); // el correo al tercero lo siguen mandando sus disparadores de siempre
        var (tenantId, notificacion) = plan.Campana.ShouldHaveSingleItem();
        tenantId.ShouldBe(tenant ?? TenantA);
        notificacion.TenantId.ShouldBe(tenantId);
        notificacion.UsuarioId.ShouldBe(TerceroId);
        notificacion.Leida.ShouldBeFalse();
        notificacion.Orden.ShouldBe(7);
        return notificacion;
    }

    [Fact]
    public async Task La_observacion_avisa_al_tercero_en_la_campana_de_su_empresa_sin_revisores_ni_areas()
    {
        var contexto = new ContextoFalso();
        var plan = await PlanearAsync(new ExpedienteObservado([AreaCumplimiento], Ahora, [RequisitoId]), ExpedienteId, contexto);

        var n = Unica(plan);
        n.Tipo.ShouldBe(TiposNotificacionTercero.Observado);
        n.Titulo.ShouldBe("Tienes observaciones por corregir");
        n.Mensaje.ShouldBe("Norte S.A.S. revisó tu validación NORTE-E-2026-000001 y te pidió corregir 1 punto.");
        n.EmpresaId.ShouldBe(EmpresaA);
        n.VinculoId.ShouldBe(VinculoA);
        n.ExpedienteId.ShouldBe(ExpedienteId);
        n.Folio.ShouldBe("NORTE-E-2026-000001");
        n.Enlace.ShouldBe($"/expediente/{ExpedienteId}");
        n.CreadaEn.ShouldBe(Ahora);
        foreach (var interno in new[] { Revisor, "Cumplimiento", "CUMPLIMIENTO", "Compras" })
        {
            n.Mensaje.ShouldNotContain(interno);
            n.Titulo.ShouldNotContain(interno);
        }

        // Reprocesar el mismo evento da el mismo id (no duplica); otro evento, otro id.
        var evento = Evento(new ExpedienteObservado([AreaCumplimiento], Ahora, [RequisitoId]), ExpedienteId);
        var uno = await PlanificadorDeNotificacionesDelTercero.PlanearAsync(evento, contexto, 7, CancellationToken.None);
        var otraVez = await PlanificadorDeNotificacionesDelTercero.PlanearAsync(evento, contexto, 7, CancellationToken.None);
        otraVez.Campana.Single().Notificacion.Id.ShouldBe(uno.Campana.Single().Notificacion.Id);
        (await PlanearAsync(new ExpedienteObservado([AreaCumplimiento], Ahora, [RequisitoId]), ExpedienteId, contexto)).Campana.Single().Notificacion.Id
            .ShouldNotBe(uno.Campana.Single().Notificacion.Id);
    }

    [Fact]
    public async Task El_recordatorio_dice_cuantos_dias_lleva_devuelto()
    {
        var contexto = new ContextoFalso();
        var n = Unica(await PlanearAsync(new RecordatorioDeObservacionesEnviado("usuario-1", false, 3, Ahora), ExpedienteId, contexto));
        n.Tipo.ShouldBe(TiposNotificacionTercero.Recordatorio);
        n.Titulo.ShouldBe("Recordatorio: observaciones pendientes");
        n.Mensaje.ShouldBe("Norte S.A.S. te recuerda que tu validación NORTE-E-2026-000001 lleva 3 días con observaciones por corregir.");
        n.Enlace.ShouldBe($"/expediente/{ExpedienteId}");
        n.Mensaje.ShouldNotContain("usuario-1");

        Unica(await PlanearAsync(new RecordatorioDeObservacionesEnviado("recordatorio-automatico", true, 0, Ahora), ExpedienteId, contexto))
            .Mensaje.ShouldBe("Norte S.A.S. te recuerda que tu validación NORTE-E-2026-000001 tiene observaciones por corregir.");
    }

    [Fact]
    public async Task El_resultado_aprobado_o_rechazado_avisa_con_los_motivos_al_tercero_y_nunca_la_justificacion_interna()
    {
        var contexto = new ContextoFalso();
        var aprobado = Unica(await PlanearAsync(new ExpedienteAprobado(Ahora), ExpedienteId, contexto));
        aprobado.Tipo.ShouldBe(TiposNotificacionTercero.Resultado);
        aprobado.Titulo.ShouldBe("Validación aprobada");
        aprobado.Mensaje.ShouldContain("tu vínculo está activo y ya tienes tu certificado");

        var rechazado = Unica(await PlanearAsync(new ExpedienteRechazado(AreaCumplimiento, Revisor,
            [new MotivoAplicado("listas", "Aparece en una lista restrictiva."), new MotivoAplicado("incompleto", "Falta información.")],
            "Coincidencia confirmada con la lista OFAC.", Ahora), ExpedienteId, contexto));
        rechazado.Titulo.ShouldBe("Validación cerrada sin aprobar");
        rechazado.Mensaje.ShouldBe("Norte S.A.S. cerró tu validación NORTE-E-2026-000001 sin aprobarla. Motivos: Aparece en una lista restrictiva. Falta información.");
        rechazado.Mensaje.ShouldNotContain("OFAC");
        rechazado.Mensaje.ShouldNotContain(Revisor);
    }

    [Fact]
    public async Task El_plazo_de_cargue_recuerda_caduca_y_reabre_en_la_campana()
    {
        var contexto = new ContextoFalso();
        var hasta = Ahora.AddDays(7);
        Unica(await PlanearAsync(new RecordatorioDeCargueEnviado(7, hasta, Ahora), ExpedienteId, contexto)).Mensaje
            .ShouldBe("Te quedan 7 días para completar tu validación NORTE-E-2026-000001 con Norte S.A.S.");
        Unica(await PlanearAsync(new RecordatorioDeCargueEnviado(1, hasta, hasta.AddHours(-6)), ExpedienteId, contexto)).Mensaje.ShouldContain("Te quedan 1 día ");
        var caducado = Unica(await PlanearAsync(new ExpedienteCaducado(EstadoExpediente.Abierto, hasta, hasta), ExpedienteId, contexto));
        caducado.Titulo.ShouldBe("Tu validación caducó");
        caducado.Tipo.ShouldBe(TiposNotificacionTercero.PlazoDeCargue);
        var reabierto = Unica(await PlanearAsync(new ExpedienteReabierto(EstadoExpediente.Abierto, hasta.AddDays(30), null, Ahora), ExpedienteId, contexto));
        reabierto.Titulo.ShouldBe("Reabrieron tu validación");
        reabierto.Mensaje.ShouldContain("Tienes hasta el 15 de diciembre de 2026 para completarla.");
    }

    [Fact]
    public async Task Una_actualizacion_o_su_ampliacion_avisa_que_hay_informacion_por_actualizar_y_la_inicial_no()
    {
        var contexto = new ContextoFalso { TipoDeExpediente = TipoExpediente.Actualizacion };
        var abierta = Unica(await PlanearAsync(AperturaDe(TipoExpediente.Actualizacion), ExpedienteId, contexto));
        abierta.Tipo.ShouldBe(TiposNotificacionTercero.Actualizacion);
        abierta.Titulo.ShouldBe("Hay información por actualizar");
        abierta.Mensaje.ShouldBe("Norte S.A.S. necesita que actualices Registro Único Tributario y Cámara de comercio.");

        var ampliada = Unica(await PlanearAsync(new RequisitosAgregados([Calculado(RequisitoId, "RUT", "Registro Único Tributario")], Ahora), ExpedienteId, contexto));
        ampliada.Titulo.ShouldBe("Se sumó algo a tu actualización");
        ampliada.Mensaje.ShouldBe("Norte S.A.S. sumó Registro Único Tributario a la actualización que tienes abierta.");

        (await PlanearAsync(AperturaDe(TipoExpediente.Inicial), ExpedienteId, contexto)).Campana.ShouldBeEmpty();
        (await PlanearAsync(AperturaDe(TipoExpediente.Renovacion), ExpedienteId, contexto)).Campana.ShouldBeEmpty();
    }

    [Fact]
    public async Task Un_documento_por_vencer_o_vencido_avisa_en_la_empresa_del_vinculo_y_no_en_las_otras()
    {
        var contexto = new ContextoFalso();
        var stream = TenantsTecnicos.Boveda;

        // El mismo documento está compartido con dos empresas (dos tenants): cada hecho avisa solo en la del vínculo al que se refiere.
        var deA = Unica(await PlanearAsync(new VencimientoAvisado(2, VinculoA, new DateOnly(2026, 12, 8), 30, Ahora), DocumentoId, contexto, stream));
        deA.Tipo.ShouldBe(TiposNotificacionTercero.DocumentoPorVencer);
        deA.Titulo.ShouldBe("Documento por vencer");
        deA.Mensaje.ShouldBe("Tu RUT vence el 8 de diciembre de 2026 para Norte S.A.S. (30 días).");
        deA.EmpresaId.ShouldBe(EmpresaA);
        deA.VinculoId.ShouldBe(VinculoA);
        deA.Enlace.ShouldBe("/boveda");

        var deB = Unica(await PlanearAsync(new VencimientoAvisado(2, VinculoB, new DateOnly(2026, 12, 20), 1, Ahora), DocumentoId, contexto, stream), TenantB);
        deB.Mensaje.ShouldBe("Tu RUT vence el 20 de diciembre de 2026 para Sur Ltda. (1 día).");
        deB.EmpresaId.ShouldBe(EmpresaB);
        deB.VinculoId.ShouldBe(VinculoB);
        deB.Mensaje.ShouldNotContain("Norte");

        var vencido = Unica(await PlanearAsync(new VersionVencida(2, VinculoA, new DateOnly(2026, 11, 1), 5, Ahora), DocumentoId, contexto, stream));
        vencido.Tipo.ShouldBe(TiposNotificacionTercero.DocumentoVencido);
        vencido.Mensaje.ShouldBe("Tu RUT venció para Norte S.A.S. el 1 de noviembre de 2026. Sube la versión nueva para seguir al día.");

        // Un vínculo con el que nunca se compartió el documento no existe para él.
        (await PlanearAsync(new VencimientoAvisado(2, Guid.NewGuid(), new DateOnly(2026, 12, 8), 30, Ahora), DocumentoId, contexto, stream)).Campana.ShouldBeEmpty();
    }

    [Fact]
    public async Task El_certificado_revocado_o_suspendido_avisa_sin_el_motivo()
    {
        var certificadoId = Guid.NewGuid();
        var emitido = new CertificadoEmitido(certificadoId, TenantA, EmpresaA, VinculoA, TerceroId, ExpedienteId, "NORTE-E-2026-000001",
            "NORTE-C-2026-000001", 1, "NORTE-C-2026-000001-1", "{}", "ab12", "sello", "vs-kid", null, "https://valida.test/verificar/x", Ahora);
        var contexto = new ContextoFalso { Certificado = Certificado.Create(emitido) };

        var revocado = Unica(await PlanearAsync(new CertificadoRevocado(1, "Documento adulterado.", OrigenRevocacion.Administrador, Revisor, Ahora), certificadoId, contexto));
        revocado.Tipo.ShouldBe(TiposNotificacionTercero.Certificado);
        revocado.Titulo.ShouldBe("Certificado revocado");
        revocado.Mensaje.ShouldContain("NORTE-C-2026-000001-1");
        revocado.Mensaje.ShouldNotContain("adulterado");
        revocado.Enlace.ShouldBe("/empresas");

        Unica(await PlanearAsync(new CertificadoSuspendido(1, "Lista restrictiva.", EstadoCertificado.Vigente, Ahora), certificadoId, contexto))
            .Titulo.ShouldBe("Certificado suspendido");
    }

    [Fact]
    public async Task Una_invitacion_nueva_avisa_con_la_empresa_y_el_tipo_de_vinculo()
    {
        var contexto = new ContextoFalso();
        var n = Unica(await PlanearAsync(new InvitacionEnviada("correo", "ana@aceros.co", Ahora.AddDays(7), null, Ahora), VinculoA, contexto));
        n.Tipo.ShouldBe(TiposNotificacionTercero.Invitacion);
        n.Mensaje.ShouldBe("Norte S.A.S. te invitó a validar tu información como proveedor.");
        n.Enlace.ShouldBe($"/empresas/{VinculoA}");
        n.ExpedienteId.ShouldBeNull();
        n.Mensaje.ShouldNotContain("ana@aceros.co");
    }

    [Fact]
    public async Task La_lectura_de_un_documento_con_datos_distintos_propone_actualizar_la_informacion_sin_ser_de_ninguna_empresa()
    {
        var campos = new Dictionary<string, JsonElement> { ["razonSocial"] = JsonSerializer.SerializeToElement("ACEROS DEL NORTE S.A.S.") };
        var analisis = new AnalisisDeIaRegistrado(1, "modelo", false, 0.95m, true, "RUT", campos, [], null, null, 10, 10, 0m, TenantA, EmpresaA, Ahora);
        var contexto = new ContextoFalso();
        var plan = await PlanearAsync(analisis, DocumentoId, contexto, TenantsTecnicos.Boveda);

        // Va en el tenant del último vínculo con el que se compartió el documento, pero sin empresa ni vínculo: es del tercero.
        var n = Unica(plan, TenantB);
        n.Tipo.ShouldBe(TiposNotificacionTercero.Propuesta);
        n.Titulo.ShouldBe("Tu información puede actualizarse");
        n.Mensaje.ShouldStartWith("Leímos tu RUT y trae datos distintos a los de tu información");
        n.EmpresaId.ShouldBeNull();
        n.VinculoId.ShouldBeNull();
        n.Enlace.ShouldBe("/ficha");
        n.Mensaje.ShouldNotContain("Norte");
        n.Mensaje.ShouldNotContain("Sur");

        // Sin lectura legible, con un documento que no respalda secciones o ya versionado de otra manera, no hay propuesta.
        var ilegible = analisis with { Legible = false };
        (await PlanearAsync(ilegible, DocumentoId, contexto, TenantsTecnicos.Boveda)).Campana.ShouldBeEmpty();
        (await PlanearAsync(analisis with { Numero = 3 }, DocumentoId, contexto, TenantsTecnicos.Boveda)).Campana.ShouldBeEmpty();
        contexto.TipoCanonico = "REFERENCIA_COMERCIAL";
        (await PlanearAsync(analisis, DocumentoId, contexto, TenantsTecnicos.Boveda)).Campana.ShouldBeEmpty();
    }

    [Fact]
    public async Task Los_hechos_que_no_son_del_tercero_no_producen_nada()
    {
        var contexto = new ContextoFalso();
        (await PlanearAsync(new EtapaActivada(1, [AreaCompras], Ahora), ExpedienteId, contexto)).Campana.ShouldBeEmpty();
        (await PlanearAsync(new ExpedienteEnviadoARevision(Ahora), ExpedienteId, contexto)).Campana.ShouldBeEmpty();
    }

    [Fact]
    public void El_correo_se_apaga_por_tipo_pero_la_campana_no_y_lo_que_no_va_por_correo_no_se_puede_apagar()
    {
        TiposNotificacionTercero.QuiereCorreo(null, TiposNotificacionTercero.Observado).ShouldBeTrue();
        var apagado = new PreferenciasDelTercero { Id = TerceroId, Correo = new() { [TiposNotificacionTercero.Observado] = false } };
        TiposNotificacionTercero.QuiereCorreo(apagado, TiposNotificacionTercero.Observado).ShouldBeFalse();
        TiposNotificacionTercero.QuiereCorreo(apagado, TiposNotificacionTercero.Recordatorio).ShouldBeTrue();
        TiposNotificacionTercero.QuiereCorreo(apagado, TiposNotificacionTercero.Actualizacion).ShouldBeFalse(); // solo campana
        TiposNotificacionTercero.ConCorreo.ShouldBeSubsetOf(TiposNotificacionTercero.Todos);
        TiposNotificacionTercero.Todos.Distinct().Count().ShouldBe(TiposNotificacionTercero.Todos.Count);
    }

    [Fact]
    public void El_recordatorio_se_redacta_con_las_observaciones_el_tiempo_pendiente_y_sin_revisores()
    {
        var redactor = new RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var correo = new Aplicacion.Expedientes.CorreoDeRecordatorioDeObservacionesPendiente("ana@aceros.co", "Norte S.A.S.", "NORTE-E-2026-000001",
            [new ObservacionParaCorreo(["El documento no se puede leer con claridad."], "La página 2 está cortada.", "Registro Único Tributario")], 4,
            EnlaceExpira: Ahora.AddDays(3));

        var mensaje = redactor.RedactarRecordatorioDeObservaciones(correo, "token-123");
        mensaje.Para.ShouldBe("ana@aceros.co");
        mensaje.Asunto.ShouldBe("Norte S.A.S. te recuerda las observaciones de tu validación NORTE-E-2026-000001");
        mensaje.Texto.ShouldContain("lleva 4 días con observaciones pendientes");
        mensaje.Texto.ShouldContain("- Registro Único Tributario: El documento no se puede leer con claridad. — La página 2 está cortada.");
        mensaje.Texto.ShouldContain("/e/token-123");
        mensaje.Html.ShouldContain("cortada.");
        mensaje.Html.ShouldNotContain("<script");

        redactor.RedactarRecordatorioDeObservaciones(correo with { DiasPendiente = 1 }).Texto.ShouldContain("lleva 1 día con observaciones pendientes");
        redactor.RedactarRecordatorioDeObservaciones(correo with { DiasPendiente = 0 }).Texto.ShouldContain("tiene observaciones pendientes");
        redactor.RedactarRecordatorioDeObservaciones(correo).Texto.ShouldContain("pide un enlace con tu correo"); // sin enlace de acceso

        // La plantilla de la empresa personaliza asunto, cabecera y pie como en los demás correos (PL-1).
        var ejemplo = EjemplosDeCorreo.Redactar(redactor, CatalogoDePlantillas.RecordatorioObservaciones, "Norte S.A.S.", "NORTE", Ahora);
        ejemplo.Variables["folio"].ShouldBe("NORTE-E-2026-000001");
        CatalogoDePlantillas.Buscar("recordatorio-observaciones")!.Variables.ShouldBe(["empresa", "folio"]);
    }

    // ---------- el tercero respondió: a qué áreas se avisa (NT-4) ----------

    [Fact]
    public async Task Cuando_el_tercero_responde_se_avisa_tambien_a_las_areas_que_revisan_lo_corregido_aunque_no_observaran()
    {
        var contexto = new ContextoFalso();
        var planificador = new PlanificadorDeNotificaciones();

        // Cumplimiento observó el RUT, que también revisa Compras (ya lo había aprobado): al corregirlo, ambas vuelven a revisar.
        var conRequisitos = await planificador.PlanearAsync(Evento(new ExpedienteReenviado(OrigenReenvio.Tercero, "Listo.", [AreaCumplimiento], Ahora,
            [RequisitoId]), ExpedienteId), contexto, 1, CancellationToken.None);
        var deRespuesta = conRequisitos.Campana.Where(c => c.Notificacion.Tipo == TiposNotificacion.TerceroRespondio).ToList();
        deRespuesta.Select(c => c.Notificacion.UsuarioId).ShouldBe([contexto.Carlos.Id, contexto.Laura.Id], ignoreOrder: true);
        deRespuesta.ShouldAllBe(c => c.Notificacion.Enlace == $"/e/{EmpresaA}/expedientes/{ExpedienteId}");
        conRequisitos.Campana.Where(c => c.Notificacion.Tipo.StartsWith("tercero.")).ShouldBeEmpty(); // «respondió» es de las áreas, no del tercero

        // Si lo corregido solo lo revisa Cumplimiento, Compras no se entera.
        var soloCumplimiento = await planificador.PlanearAsync(Evento(new ExpedienteReenviado(OrigenReenvio.Tercero, "Listo.", [AreaCumplimiento], Ahora,
            [Guid.Parse("00000000-0000-0000-0000-0000000000c1")]), ExpedienteId), contexto, 1, CancellationToken.None);
        soloCumplimiento.Campana.Select(c => c.Notificacion.UsuarioId).ShouldBe([contexto.Carlos.Id]);

        // Sin requisitos reabiertos (flujo sin requisitos) vale lo de siempre: solo las áreas que observaron.
        var sinRequisitos = await planificador.PlanearAsync(Evento(new ExpedienteReenviado(OrigenReenvio.Tercero, null, [AreaCumplimiento], Ahora), ExpedienteId),
            contexto, 1, CancellationToken.None);
        sinRequisitos.Campana.Select(c => c.Notificacion.UsuarioId).ShouldBe([contexto.Carlos.Id]);
    }

    [Fact]
    public async Task El_planificador_une_lo_de_las_personas_y_lo_del_tercero_en_un_solo_plan()
    {
        var contexto = new ContextoFalso();
        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(Evento(new ExpedienteAprobado(Ahora), ExpedienteId), contexto, 1, CancellationToken.None);

        plan.Campana.Select(c => c.Notificacion.Tipo).Order().ShouldBe(
            [TiposNotificacion.Aprobado, TiposNotificacion.Aprobado, TiposNotificacionTercero.Resultado], ignoreOrder: true); // Laura y Carlos + el tercero
        plan.Campana.Count(c => c.Notificacion.UsuarioId == TerceroId).ShouldBe(1);
        plan.Campana.Where(c => c.Notificacion.UsuarioId != TerceroId).ShouldAllBe(c => c.Notificacion.TenantId == null);
    }

    // ---------- cuenta designada (NT-12) ----------

    [Fact]
    public async Task La_cuenta_propuesta_avisa_solo_a_quienes_la_aprueban_en_esa_empresa()
    {
        var contexto = new ContextoFalso();
        var cuenta = new CuentaParaDesignar(Guid.NewGuid(), "Bancolombia", "Ahorros", "protegido", "5678", "Aceros del Norte", "NIT 900123456", "COP");
        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(Evento(new CuentaDesignadaPropuesta(cuenta, null, Ahora), VinculoA), contexto, 9, CancellationToken.None);

        // Oficial de la empresa y administrador de todo el grupo; no el revisor, ni el inactivo, ni el oficial de otra empresa.
        var deLaCuenta = plan.Campana.Where(c => c.Notificacion.Tipo == TiposNotificacion.CuentaPropuesta).ToList();
        deLaCuenta.Select(c => c.Notificacion.UsuarioId).ShouldBe([contexto.Oficial.Id, contexto.Administrador.Id], ignoreOrder: true);
        deLaCuenta.ShouldAllBe(c => c.TenantId == TenantA);
        var n = deLaCuenta[0].Notificacion;
        n.Titulo.ShouldBe("Cuenta bancaria por aprobar");
        n.Mensaje.ShouldBe("Aceros del Norte propuso la cuenta Bancolombia ****5678 para cobrarle a Norte S.A.S. La aprueban 2 personas distintas.");
        n.Enlace.ShouldBe($"/e/{EmpresaA}/vinculos/{VinculoA}");
        n.VinculoId.ShouldBe(VinculoA);
        plan.Correos.ShouldBeEmpty();
        plan.Campana.Where(c => c.Notificacion.UsuarioId == TerceroId).ShouldBeEmpty(); // el tercero propuso: no necesita aviso
    }

    [Fact]
    public async Task La_cuenta_aprobada_o_rechazada_avisa_al_tercero_con_su_empresa_y_lo_lleva_a_sus_cuentas()
    {
        var contexto = new ContextoFalso();
        var cuenta = new CuentaParaDesignar(Guid.NewGuid(), "Bancolombia", "Ahorros", "protegido", "5678", "Aceros del Norte", "NIT 900123456", "COP");

        var aprobada = Unica(await PlanearAsync(new CuentaDesignadaAprobada(cuenta.CuentaId, ["a", "b"], Ahora, cuenta), VinculoA, contexto));
        aprobada.Tipo.ShouldBe(TiposNotificacionTercero.Cuenta);
        aprobada.Titulo.ShouldBe("Cuenta aprobada");
        aprobada.Mensaje.ShouldBe("Norte S.A.S. aprobó la cuenta Bancolombia ****5678 con la que le cobras.");
        aprobada.Enlace.ShouldBe("/cuentas");

        var rechazada = Unica(await PlanearAsync(new CuentaDesignadaRechazada(cuenta.CuentaId, "usuario-7f3a", "El titular no coincide.", Ahora, cuenta), VinculoA, contexto));
        rechazada.Titulo.ShouldBe("Cuenta rechazada");
        rechazada.Mensaje.ShouldBe("Norte S.A.S. rechazó la cuenta Bancolombia ****5678: El titular no coincide. Elige otra cuenta para esta empresa.");
        rechazada.Mensaje.ShouldNotContain("usuario-7f3a");
    }

    // ---------- desaprobar y reintegrar (DS-10) ----------

    [Fact]
    public async Task Desaprobar_un_expediente_aprobado_avisa_al_tercero_en_la_campana_y_por_correo_sin_la_justificacion_interna()
    {
        var contexto = new ContextoFalso();
        var desaprobado = new ExpedienteDesaprobado(1, false, "usuario-admin-9d", "Interno: el RUT no cuadra con la Cámara.", Ahora);

        var n = Unica(await PlanearAsync(desaprobado, ExpedienteId, contexto));
        n.Tipo.ShouldBe(TiposNotificacionTercero.Desaprobado);
        n.Titulo.ShouldBe("Tu validación volvió a revisión");
        n.Mensaje.ShouldBe("Norte S.A.S. volvió a revisar tu validación NORTE-E-2026-000001: tu vínculo queda en validación mientras la revisa y te avisamos cuando termine.");
        n.EmpresaId.ShouldBe(EmpresaA);
        n.VinculoId.ShouldBe(VinculoA);
        n.Enlace.ShouldBe($"/expediente/{ExpedienteId}");
        foreach (var interno in new[] { "usuario-admin-9d", "Interno", "RUT no cuadra" })
            (n.Titulo + n.Mensaje).ShouldNotContain(interno);

        // El correo (plantilla `desaprobacion`) lo planifica el planificador de las personas: al correo de la invitación, de esa empresa.
        var plan = await new PlanificadorDeNotificaciones().PlanearAsync(Evento(desaprobado, ExpedienteId), contexto, 9, CancellationToken.None);
        var correo = plan.Correos.OfType<CorreoDeDesaprobacionPendiente>().ShouldHaveSingleItem();
        (correo.Para, correo.Empresa, correo.Folio).ShouldBe(("ana@aceros.co", "Norte S.A.S.", "NORTE-E-2026-000001"));
        (correo.TenantId, correo.EmpresaId, correo.TerceroId).ShouldBe((TenantA, EmpresaA, TerceroId));
        plan.Campana.Count(c => c.Notificacion.UsuarioId == TerceroId).ShouldBe(1);

        var redactor = new RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var mensaje = redactor.RedactarDesaprobacion(correo);
        mensaje.Asunto.ShouldBe("Norte S.A.S. volvió a revisar tu validación NORTE-E-2026-000001");
        mensaje.Texto.ShouldContain("que ya estaba aprobada");
        mensaje.Texto.ShouldContain("no tienes que hacer nada por ahora");
        (mensaje.Texto + mensaje.Html).ShouldNotContain("usuario-admin-9d");
        (mensaje.Texto + mensaje.Html).ShouldNotContain("Interno");
    }

    [Fact]
    public async Task Reintegrar_avisa_al_tercero_segun_el_modo_con_el_enlace_a_lo_que_sigue_y_sin_la_justificacion()
    {
        var contexto = new ContextoFalso { ExpedienteDelVinculo = ExpedienteId };
        var planificador = new PlanificadorDeNotificaciones();
        var redactor = new RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        // Sin revalidar: queda como estaba y su certificado se emitió de nuevo; no hay nada que hacer.
        var sin = new VinculoReintegrado(EstadoVinculo.Activo, "Interno: lo pidió gerencia.", Ahora, SinRevalidar: true);
        var n = Unica(await PlanearAsync(sin, VinculoA, contexto));
        n.Tipo.ShouldBe(TiposNotificacionTercero.Reintegrado);
        n.Titulo.ShouldBe("Tu vínculo se reintegró");
        n.Mensaje.ShouldBe("Norte S.A.S. reintegró tu vínculo sin pedirte documentos nuevos: quedó como estaba antes y tu certificado se emitió de nuevo.");
        n.Enlace.ShouldBe("/empresas");
        n.VinculoId.ShouldBe(VinculoA);
        (n.Titulo + n.Mensaje).ShouldNotContain("Interno");
        var correoSin = (await planificador.PlanearAsync(Evento(sin, VinculoA), contexto, 9, CancellationToken.None)).Correos
            .OfType<CorreoDeReintegroPendiente>().ShouldHaveSingleItem();
        (correoSin.Para, correoSin.Modo, correoSin.Folio).ShouldBe(("ana@aceros.co", ModoDeReintegro.SinRevalidar, "NORTE-E-2026-000001"));
        var textoSin = redactor.RedactarReintegro(correoSin);
        textoSin.Asunto.ShouldBe("Norte S.A.S. reintegró tu vínculo");
        textoSin.Texto.ShouldContain("tu certificado se emitió de nuevo");
        (textoSin.Texto + textoSin.Html).ShouldNotContain("Interno");

        // Revalidando con una renovación abierta: hay que subir o confirmar los documentos; el enlace va a esa validación.
        var con = new VinculoReintegrado(EstadoVinculo.EnValidacion, "Interno: renovó el contrato.", Ahora);
        var nCon = Unica(await PlanearAsync(con, VinculoA, contexto));
        nCon.Titulo.ShouldBe("Tu vínculo se reintegró: falta validar");
        nCon.Mensaje.ShouldBe("Norte S.A.S. reintegró tu vínculo y abrió una nueva validación (NORTE-E-2026-000001): sube o confirma tus documentos para completarla.");
        nCon.Enlace.ShouldBe($"/expediente/{ExpedienteId}");
        nCon.ExpedienteId.ShouldBe(ExpedienteId);
        var correoCon = (await planificador.PlanearAsync(Evento(con, VinculoA), contexto, 9, CancellationToken.None)).Correos
            .OfType<CorreoDeReintegroPendiente>().ShouldHaveSingleItem();
        correoCon.Modo.ShouldBe(ModoDeReintegro.ConRenovacion);
        redactor.RedactarReintegro(correoCon).Texto.ShouldContain("abrió una nueva validación (NORTE-E-2026-000001)");

        // Revalidando un vínculo que nunca se aprobó: vuelve a la invitación y hay que completar la validación.
        var sinExpediente = new ContextoFalso();
        var nuevo = new VinculoReintegrado(EstadoVinculo.Invitado, "Interno.", Ahora);
        var nNuevo = Unica(await PlanearAsync(nuevo, VinculoA, sinExpediente));
        nNuevo.Mensaje.ShouldBe("Norte S.A.S. reintegró tu vínculo: completa la validación de tu información para dejarlo activo.");
        nNuevo.Enlace.ShouldBe($"/empresas/{VinculoA}");
        var correoNuevo = (await planificador.PlanearAsync(Evento(nuevo, VinculoA), sinExpediente, 9, CancellationToken.None)).Correos
            .OfType<CorreoDeReintegroPendiente>().ShouldHaveSingleItem();
        (correoNuevo.Modo, correoNuevo.Folio).ShouldBe((ModoDeReintegro.PorValidar, (string?)null));
        redactor.RedactarReintegro(correoNuevo).Texto.ShouldContain("falta que completes la validación");

        // Un vínculo sin correo de invitación no recibe correo (la campana sí).
        sinExpediente.SinCorreo = true;
        (await planificador.PlanearAsync(Evento(nuevo, VinculoA), sinExpediente, 9, CancellationToken.None)).Correos.ShouldBeEmpty();
    }

    [Fact]
    public void Los_correos_de_desaprobar_y_reintegrar_se_apagan_por_tipo_y_tienen_su_plantilla_con_ejemplo()
    {
        foreach (var tipo in new[] { TiposNotificacionTercero.Desaprobado, TiposNotificacionTercero.Reintegrado })
        {
            TiposNotificacionTercero.ConCorreo.ShouldContain(tipo);
            TiposNotificacionTercero.QuiereCorreo(null, tipo).ShouldBeTrue();
            var apagado = new PreferenciasDelTercero { Id = TerceroId, Correo = new() { [tipo] = false } };
            TiposNotificacionTercero.QuiereCorreo(apagado, tipo).ShouldBeFalse();
            TiposNotificacionTercero.QuiereCorreo(apagado, TiposNotificacionTercero.Observado).ShouldBeTrue();
        }

        CatalogoDePlantillas.Buscar("desaprobacion")!.Variables.ShouldBe(["empresa", "folio"]);
        CatalogoDePlantillas.Buscar("reintegro")!.Variables.ShouldBe(["empresa", "folio"]);
        // Cada evento del catálogo se puede redactar con datos de ejemplo (el asunto por defecto y la vista previa de Plantillas).
        var redactor = new RedactorDeCorreos(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        foreach (var evento in CatalogoDePlantillas.Eventos)
            EjemplosDeCorreo.Redactar(redactor, evento.Evento, "Norte S.A.S.", "NORTE", Ahora).Mensaje.Asunto.ShouldNotBeNullOrWhiteSpace();
        EjemplosDeCorreo.Redactar(redactor, CatalogoDePlantillas.Desaprobacion, "Norte S.A.S.", "NORTE", Ahora).Mensaje.Asunto
            .ShouldBe("Norte S.A.S. volvió a revisar tu validación NORTE-E-2026-000001");
    }

    // ---------- avisos de cambio para las pantallas abiertas (NT-12) ----------

    private static async Task<IReadOnlyList<AvisoDeCambio>> CambiosAsync(object datos, Guid stream, ContextoFalso contexto, string? tenant = null) =>
        await PlanificadorDeCambios.PlanearAsync(Evento(datos, stream, tenant ?? TenantA.ToString("N")), contexto, 11, CancellationToken.None);

    [Fact]
    public async Task Un_hecho_del_expediente_avisa_a_su_empresa_y_a_su_tercero_y_a_nadie_mas()
    {
        var avisos = await CambiosAsync(new ExpedienteObservado([AreaCumplimiento], Ahora, [RequisitoId]), ExpedienteId, new ContextoFalso());

        var deLaEmpresa = avisos.Single(a => a.Destino == DestinoDeCambio.Empresa);
        deLaEmpresa.TenantId.ShouldBe(TenantA);
        deLaEmpresa.EmpresaId.ShouldBe(EmpresaA);
        deLaEmpresa.VinculoId.ShouldBe(VinculoA);
        deLaEmpresa.TerceroId.ShouldBeNull();
        var delTercero = avisos.Single(a => a.Destino == DestinoDeCambio.Tercero);
        delTercero.TerceroId.ShouldBe(TerceroId);
        delTercero.VinculoId.ShouldBe(VinculoA);
        avisos.ShouldAllBe(a => a.Recurso == RecursosDeCambio.Expediente && a.RecursoId == ExpedienteId && a.Orden == 11);
        avisos.Select(a => a.Id).Distinct().Count().ShouldBe(2);
        avisos.ShouldAllBe(a => a.ADto().Id == ExpedienteId); // el aviso no lleva datos: solo tipo, id y vínculo
    }

    [Fact]
    public async Task Los_hechos_del_vinculo_y_de_su_cuenta_avisan_del_vinculo_y_de_la_cuenta()
    {
        var cuenta = new CuentaParaDesignar(Guid.NewGuid(), "Bancolombia", "Ahorros", "protegido", "5678", "Aceros", "NIT 9", "COP");
        var avisos = await CambiosAsync(new CuentaDesignadaPropuesta(cuenta, null, Ahora), VinculoA, new ContextoFalso());

        avisos.Where(a => a.Recurso == RecursosDeCambio.Vinculo).Select(a => a.Destino).ShouldBe([DestinoDeCambio.Empresa, DestinoDeCambio.Tercero], ignoreOrder: true);
        avisos.Where(a => a.Recurso == RecursosDeCambio.Cuenta).Select(a => (a.Destino, a.RecursoId)).ShouldBe(
            [(DestinoDeCambio.Empresa, cuenta.CuentaId), (DestinoDeCambio.Tercero, cuenta.CuentaId)], ignoreOrder: true);
        avisos.ShouldAllBe(a => a.VinculoId == VinculoA);
        (await CambiosAsync(new VinculoActivado(ExpedienteId, Ahora), VinculoA, new ContextoFalso())).Select(a => a.Recurso).Distinct().ShouldBe([RecursosDeCambio.Vinculo]);
    }

    [Fact]
    public async Task Un_documento_avisa_al_tercero_y_a_cada_empresa_con_la_que_se_compartio_en_su_tenant()
    {
        var avisos = await CambiosAsync(new VersionVencida(2, VinculoA, new DateOnly(2026, 11, 1), 5, Ahora), DocumentoId, new ContextoFalso(), TenantsTecnicos.Boveda);

        avisos.ShouldAllBe(a => a.Recurso == RecursosDeCambio.Documento && a.RecursoId == DocumentoId);
        avisos.Single(a => a.Destino == DestinoDeCambio.Tercero).TerceroId.ShouldBe(TerceroId);
        var empresas = avisos.Where(a => a.Destino == DestinoDeCambio.Empresa).Select(a => (a.TenantId, a.EmpresaId, a.VinculoId)).ToList();
        empresas.ShouldBe([(TenantA, EmpresaA, VinculoA), (TenantB, EmpresaB, VinculoB)], ignoreOrder: true);
    }

    [Fact]
    public async Task La_ficha_del_tercero_avisa_a_todas_las_empresas_con_las_que_tiene_un_vinculo()
    {
        var seccionId = SeccionDeFicha.IdPara(TerceroId, "general", null);
        var contexto = new ContextoFalso { Seccion = new SeccionDeFicha { Id = seccionId, TerceroId = TerceroId, Seccion = "general" } };
        var avisos = await CambiosAsync(new SeccionDeFichaIniciada(seccionId, TerceroId, "general", null, Ahora), seccionId, contexto, TenantsTecnicos.Boveda);

        avisos.ShouldAllBe(a => a.Recurso == RecursosDeCambio.Ficha);
        avisos.Single(a => a.Destino == DestinoDeCambio.Tercero).TerceroId.ShouldBe(TerceroId);
        avisos.Where(a => a.Destino == DestinoDeCambio.Empresa).Select(a => (a.TenantId, a.EmpresaId)).ShouldBe([(TenantA, EmpresaA), (TenantB, EmpresaB)], ignoreOrder: true);

        // Sin la sección (o con otra), no hay a quién avisar.
        (await CambiosAsync(new SeccionDeFichaIniciada(seccionId, TerceroId, "general", null, Ahora), Guid.NewGuid(), contexto, TenantsTecnicos.Boveda)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Lo_que_no_cambia_una_pantalla_de_datos_no_produce_avisos()
    {
        var contexto = new ContextoFalso();
        (await CambiosAsync(new FolioConsumido(1, Ahora), Guid.NewGuid(), contexto)).ShouldBeEmpty();
        (await CambiosAsync(new Dominio.Invitaciones.LoteTerminado(Dominio.Invitaciones.EstadoLote.Completado, new Dominio.Invitaciones.TotalesDeLote(0, 0, 0, 0, 0, 0), Ahora), Guid.NewGuid(), contexto)).ShouldBeEmpty();
        (await CambiosAsync(new ExpedienteObservado([], Ahora), Guid.NewGuid(), contexto)).ShouldBeEmpty(); // expediente que el contexto no conoce
    }

    [Fact]
    public void Un_lote_avisa_una_sola_vez_cada_recurso_a_cada_destinatario_y_el_cursor_no_repite_lo_que_ya_mando()
    {
        AvisoDeCambio Aviso(long orden, Guid? id = null) => new()
        {
            Id = id ?? Guid.NewGuid(), Orden = orden, Destino = DestinoDeCambio.Empresa, TenantId = TenantA, EmpresaId = EmpresaA, Recurso = RecursosDeCambio.Expediente, RecursoId = ExpedienteId
        };

        PlanificadorDeCambios.Coalescer([Aviso(1), Aviso(3), Aviso(2)]).ShouldHaveSingleItem().Orden.ShouldBe(3);
        var otroRecurso = Aviso(4);
        otroRecurso.Recurso = RecursosDeCambio.Vinculo;
        PlanificadorDeCambios.Coalescer([Aviso(1), otroRecurso]).Count.ShouldBe(2);

        var cursor = new CursorDeCambios(Ahora);
        var primero = Aviso(Ahora.UtcTicks + 10);
        cursor.Nuevos([primero]).ShouldHaveSingleItem();
        cursor.Nuevos([primero]).ShouldBeEmpty(); // el solape lo vuelve a leer: no se repite
        // Un aviso que llegó con un orden algo menor que el último mandado (el procesador escribe por secuencia) sí se manda.
        var tarde = Aviso(Ahora.UtcTicks + 5);
        cursor.Desde.ShouldBeLessThan(tarde.Orden);
        cursor.Nuevos([primero, tarde]).ShouldHaveSingleItem().ShouldBe(tarde);
        cursor.Desde.ShouldBe(primero.Orden - CursorDeCambios.Solape.Ticks);
    }

    // ---------- utilidades ----------

    private static ExpedienteAbierto AperturaDe(TipoExpediente tipo) =>
        new(ExpedienteId, EmpresaA, VinculoA, tipo, "NORTE-E-2026-000001", TerceroId, "Aceros del Norte", "CO|NIT|900123456", Guid.NewGuid(), "PROVEEDOR",
            "Proveedor", new ActorDeExpediente(ActorTipo.Usuario, Revisor), [Revisor], Ahora);

    private static RequisitoCalculado Calculado(Guid id, string codigo, string nombre) =>
        new(id, Guid.NewGuid(), codigo, nombre, codigo, AmbitoDocumento.Global, true, [], null, null);

    private static IEvent Evento(object datos, Guid stream, string? tenant = null)
    {
        var tipo = typeof(Event<>).MakeGenericType(datos.GetType());
        var evento = (IEvent)Activator.CreateInstance(tipo, datos)!;
        evento.StreamId = stream;
        evento.TenantId = tenant ?? TenantA.ToString("N");
        evento.Id = Guid.NewGuid();
        evento.Sequence = 4242;
        evento.Timestamp = Ahora;
        return evento;
    }

    /// <summary>Instantáneas en memoria: dos empresas en dos tenants, un vínculo en cada una, el expediente de Norte y el documento compartido con ambas.</summary>
    private sealed class ContextoFalso : IContextoDeEventos
    {
        public Usuario Laura { get; } = Persona("Laura Compras");
        public Usuario Carlos { get; } = Persona("Carlos Cumplimiento");
        public Usuario Oficial { get; } = Persona("Olga Oficial", Rol.OficialCumplimiento);
        public Usuario Administrador { get; } = Persona("Ana Administradora", Rol.Administrador, empresa: null, todoElTenant: true);
        private Usuario OficialDeOtra { get; } = Persona("Otto Otra", Rol.OficialCumplimiento, empresa: Guid.NewGuid());
        private Usuario Inactiva { get; } = Persona("Ines Inactiva", Rol.OficialCumplimiento, activo: false);
        public Area Compras { get; }
        public Area Cumplimiento { get; }
        public Certificado? Certificado { get; set; }

        public SeccionDeFicha? Seccion { get; set; }

        /// <summary>Expediente que el vínculo de Norte tiene abierto y como último (reintegrar, DS-10); ninguno por omisión.</summary>
        public Guid? ExpedienteDelVinculo { get; set; }

        /// <summary>El vínculo de Norte no tiene correo de invitación.</summary>
        public bool SinCorreo { get; set; }
        public TipoExpediente TipoDeExpediente { get; set; } = TipoExpediente.Inicial;
        public string TipoCanonico { get; set; } = "RUT";

        public ContextoFalso()
        {
            Compras = new Area { Id = AreaCompras, EmpresaId = EmpresaA, Codigo = "COMPRAS", Nombre = "Compras", Miembros = [Laura.Id] };
            Cumplimiento = new Area { Id = AreaCumplimiento, EmpresaId = EmpresaA, Codigo = "CUMPLIMIENTO", Nombre = "Cumplimiento", Miembros = [Carlos.Id] };
        }

        private static Usuario Persona(string nombre, Rol rol = Rol.Revisor, bool activo = true, Guid? empresa = null, bool todoElTenant = false)
        {
            var usuario = Usuario.Crear(nombre, nombre.Split(' ')[0].ToLowerInvariant() + "@norte.co", null, Ahora);
            usuario.Roles = [new RolAsignado(rol, todoElTenant ? null : empresa ?? EmpresaA)];
            usuario.Activo = activo;
            return usuario;
        }

        private Expediente Expediente()
        {
            var observado = new VeredictoRegistrado
            {
                Id = Guid.NewGuid(), AreaId = AreaCumplimiento, Veredicto = Veredicto.Observado, Vigente = true, RequisitoId = RequisitoId,
                Motivos = [new MotivoAplicado("ilegible", "No se lee.")], Fecha = Ahora
            };
            return new Expediente
            {
                Id = ExpedienteId, EmpresaId = EmpresaA, VinculoId = VinculoA, Folio = "NORTE-E-2026-000001", TerceroId = TerceroId, Tipo = TipoDeExpediente,
                TerceroNombre = "Aceros del Norte", TipoVinculoNombre = "Proveedor", Estado = EstadoExpediente.Observado, AreaIds = [AreaCompras, AreaCumplimiento],
                Etapas = [new EtapaDeExpediente { Orden = 1, Estado = EstadoEtapa.Activa, Areas = [new AreaDeExpediente { AreaId = AreaCumplimiento, Codigo = "CUMPLIMIENTO" }] }],
                Requisitos =
                [
                    new RequisitoDeExpediente
                    {
                        Id = RequisitoId, Codigo = "RUT", Nombre = "Registro Único Tributario", AreasQueRevisan = [AreaCompras, AreaCumplimiento], Veredictos = [observado]
                    },
                    new RequisitoDeExpediente { Id = Guid.Parse("00000000-0000-0000-0000-0000000000c1"), Codigo = "CAMARA_COMERCIO", Nombre = "Cámara de comercio",
                        AreasQueRevisan = [AreaCumplimiento] }
                ]
            };
        }

        public Task<Vinculo?> VinculoAsync(Guid tenantId, Guid id, CancellationToken ct)
        {
            Vinculo? vinculo = (tenantId, id) switch
            {
                _ when tenantId == TenantA && id == VinculoA => new Vinculo
                {
                    Id = VinculoA, EmpresaId = EmpresaA, TerceroId = TerceroId, TipoVinculoId = Guid.NewGuid(), Estado = EstadoVinculo.EnValidacion,
                    CorreoTercero = SinCorreo ? string.Empty : "ana@aceros.co", ExpedienteAbiertoId = ExpedienteDelVinculo, UltimoExpedienteId = ExpedienteDelVinculo
                },
                _ when tenantId == TenantB && id == VinculoB => new Vinculo
                {
                    Id = VinculoB, EmpresaId = EmpresaB, TerceroId = TerceroId, TipoVinculoId = Guid.NewGuid(), Estado = EstadoVinculo.EnValidacion,
                    CorreoTercero = "ana@aceros.co"
                },
                _ => null
            };
            return Task.FromResult(vinculo);
        }

        public Task<Expediente?> ExpedienteAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(tenantId == TenantA && id == ExpedienteId ? Expediente() : null);

        public Task<TipoVinculo?> TipoVinculoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult<TipoVinculo?>(new TipoVinculo { Id = id, Codigo = "PROVEEDOR", Nombre = "Proveedor" });

        public Task<TipoDocumentoTenant?> TipoDocumentoAsync(Guid tenantId, Guid id, CancellationToken ct) => Task.FromResult<TipoDocumentoTenant?>(null);

        public Task<Tercero?> TerceroAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Tercero?>(id == TerceroId
                ? new Tercero { Id = id, Identificacion = new Identificacion("CO", "NIT", "900123456", "7"), Nombre = "Aceros del Norte", TipoPersona = TipoPersona.Juridica }
                : null);

        public Task<Documento?> DocumentoAsync(Guid id, CancellationToken ct)
        {
            if (id != DocumentoId)
                return Task.FromResult<Documento?>(null);
            return Task.FromResult<Documento?>(new Documento
            {
                Id = DocumentoId, TerceroId = TerceroId, Ambito = AmbitoDocumento.Global, TipoCanonico = TipoCanonico, NombreTipo = "RUT", VersionActual = 1,
                Comparticiones =
                [
                    new Comparticion(2, TenantA, EmpresaA, VinculoA, OrigenComparticion.AlAceptarInvitacion, Ahora),
                    new Comparticion(2, TenantB, EmpresaB, VinculoB, OrigenComparticion.Automatica, Ahora)
                ]
            });
        }

        public Task<SeccionDeFicha?> SeccionDeFichaAsync(Guid id, CancellationToken ct) => Task.FromResult(Seccion?.Id == id ? Seccion : null);

        public Task<ICatalogosDeFicha> CatalogosDeFichaAsync(IEnumerable<ReferenciaACatalogo> referencias, CancellationToken ct) =>
            Task.FromResult<ICatalogosDeFicha>(CatalogosEnMemoria.Vacios);

        public Task<Certificado?> CertificadoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
            Task.FromResult(Certificado is not null && Certificado.Id == id && tenantId == TenantA ? Certificado : null);

        public Task<Empresa?> EmpresaAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<Empresa?>(id == EmpresaB
                ? new Empresa { Id = id, TenantId = TenantB, Nombre = "Sur Ltda." }
                : new Empresa { Id = id, TenantId = TenantA, Nombre = "Norte S.A.S." });

        public Task<IReadOnlyList<Area>> AreasAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Area>>([.. new[] { Compras, Cumplimiento }.Where(a => ids.Contains(a.Id))]);

        public Task<IReadOnlyList<Usuario>> UsuariosAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Usuario>>([.. new[] { Laura, Carlos }.Where(u => ids.Contains(u.Id))]);

        public Task<IReadOnlyList<Usuario>> UsuariosDelTenantAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Usuario>>([.. new[] { Laura, Carlos, Oficial, Administrador, OficialDeOtra, Inactiva }.Where(u => u.Activo)]);

        public Task<IReadOnlyList<Vinculo>> VinculosDelTerceroAsync(Guid terceroId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Vinculo>>([.. new[] { VinculoA, VinculoB }.Select(id => VinculoAsync(id == VinculoA ? TenantA : TenantB, id, ct).Result!)]);

        public Task<PreferenciasNotificacion?> PreferenciasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct) => Task.FromResult<PreferenciasNotificacion?>(null);

        public Task<IReadOnlyList<TextoLegal>> TextosLegalesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<TextoLegal>>([]);

        public Task<IReadOnlyList<SuscripcionWebhook>> SuscripcionesActivasAsync(Guid tenantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<SuscripcionWebhook>>([]);

        public Task<IReadOnlySet<Guid>> ExistentesAsync<T>(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) where T : notnull =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    }
}
