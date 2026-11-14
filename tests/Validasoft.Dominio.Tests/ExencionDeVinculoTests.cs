using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Exención del vínculo (EX-1…EX-12): la empresa decide no perseguir la renovación de un tercero ya aprobado. Cubre las reglas del agregado (quién puede eximir y
/// de qué), la regla única de «está eximido», su efecto sobre la vigencia periódica con el reloj simulado, el vencimiento de la exención y el vínculo que vuelve a
/// <c>Activo</c> cuando no queda nada que renovar. Aprobado el 6/11/2026 (hora de Bogotá): con 365 días vale hasta el 6/11/2027.
/// </summary>
public class ExencionDeVinculoTests
{
    private static readonly DateTimeOffset Aprobacion = new(2026, 11, 6, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hasta = new(2027, 11, 6);
    private static readonly DateTimeOffset Hoy = new(2027, 1, 10, 14, 0, 0, TimeSpan.Zero);

    private static readonly Guid TipoRut = Guid.NewGuid();
    private static readonly Guid TipoCamara = Guid.NewGuid();
    private static readonly ObjetivoDeExencion Rut = ObjetivoDeExencion.DeDocumento(TipoRut, "RUT", "Registro Único Tributario");
    private static readonly ObjetivoDeExencion Camara = ObjetivoDeExencion.DeDocumento(TipoCamara, "CAMARA_COMERCIO", "Cámara de comercio");
    private static readonly ObjetivoDeExencion General = ObjetivoDeExencion.DeSeccion("FICHA_GENERAL", "Información general");

    private static DateTimeOffset Dia(DateOnly fecha, int hora = 14) => new(fecha.ToDateTime(new TimeOnly(hora, 0)), TimeSpan.Zero);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private static Vinculo Nuevo() =>
        Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "compras@proveedor.co", Aprobacion));

    /// <summary>Vínculo aprobado el 6/11/2026 con vigencia de 365 días (avisos a 30, 15 y 5) y la gracia indicada.</summary>
    private static Vinculo Activo(int gracia = 0)
    {
        var vinculo = Nuevo();
        var expediente = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(expediente, TipoExpediente.Inicial, "N-E-1", Aprobacion));
        foreach (var evento in vinculo.CerrarExpediente(expediente, ResultadoExpediente.Aprobado, Aprobacion))
        {
            if (evento is ExpedienteCerrado cerrado) vinculo.Apply(cerrado);
            if (evento is VinculoActivado activado) vinculo.Apply(activado);
        }

        var config = new VigenciaDelVinculo(365, [30, 15, 5], gracia);
        vinculo.Apply(vinculo.FijarVigencia(VigenciaDeAprobacion.VigenteHasta(config, [], [], Aprobacion), config.DiasAviso, config.DiasGracia, Aprobacion)!);
        return vinculo;
    }

    private static ExencionDeVinculoRegistrada Eximir(Vinculo vinculo, bool todos = false, ObjetivoDeExencion[]? objetivos = null, DateOnly? hasta = null,
        string justificacion = "Proveedor estratégico: no se persigue.", DateTimeOffset? cuando = null)
    {
        var evento = vinculo.Eximir(todos, objetivos ?? [], justificacion, hasta, "admin", cuando ?? Hoy);
        vinculo.Apply(evento);
        return evento;
    }

    // ---------- quién puede eximir y de qué ----------

    [Fact]
    public void Se_exime_de_requisitos_concretos_o_de_todo_con_justificacion_y_actor()
    {
        var vinculo = Activo();

        var concreta = vinculo.Eximir(false, [Rut, General, Rut], "El RUT lo renueva Contabilidad.", Hasta, "ana", Hoy);
        concreta.ShouldSatisfyAllConditions(
            e => e.Todos.ShouldBeFalse(),
            e => e.Objetivos.Select(o => o.Clave).ShouldBe([Rut.Clave, General.Clave]), // sin repetir
            e => e.Justificacion.ShouldBe("El RUT lo renueva Contabilidad."),
            e => e.Hasta.ShouldBe(Hasta),
            e => e.Por.ShouldBe("ana"),
            e => e.Fecha.ShouldBe(Hoy));
        vinculo.Apply(concreta);

        var exencion = vinculo.Exenciones.ShouldHaveSingleItem();
        (exencion.Id, exencion.Estado, exencion.Por, exencion.CreadaEn).ShouldBe((concreta.ExencionId, EstadoDeExencion.Vigente, "ana", Hoy));
        vinculo.Apply(concreta); // idempotente: aplicarlo dos veces no la duplica
        vinculo.Exenciones.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void La_justificacion_es_obligatoria_para_eximir_y_para_revocar(string? justificacion)
    {
        var vinculo = Activo();
        Codigo(() => vinculo.Eximir(true, [], justificacion, null, "admin", Hoy)).ShouldBe("justificacion-requerida");

        var evento = Eximir(vinculo, todos: true);
        Codigo(() => vinculo.RevocarExencion(evento.ExencionId, justificacion, "admin", Hoy)).ShouldBe("justificacion-requerida");
    }

    [Fact]
    public void La_justificacion_tiene_un_largo_maximo()
    {
        var vinculo = Activo();
        vinculo.Eximir(true, [], new string('x', Vinculo.MaximoDeJustificacionDeExencion), null, "admin", Hoy).ShouldNotBeNull();
        Codigo(() => vinculo.Eximir(true, [], new string('x', Vinculo.MaximoDeJustificacionDeExencion + 1), null, "admin", Hoy))
            .ShouldBe("justificacion-demasiado-larga");
    }

    [Fact]
    public void Una_exencion_es_de_todo_o_de_algo_concreto_y_no_de_las_dos_ni_de_nada()
    {
        var vinculo = Activo();

        Codigo(() => vinculo.Eximir(false, [], "x", null, "admin", Hoy)).ShouldBe("exencion-sin-alcance");
        Codigo(() => vinculo.Eximir(true, [Rut], "x", null, "admin", Hoy)).ShouldBe("exencion-alcance-invalido");
        var demasiados = Enumerable.Range(0, Vinculo.MaximoDeObjetivosPorExencion + 1)
            .Select(i => ObjetivoDeExencion.DeDocumento(Guid.NewGuid(), $"T{i}", $"Tipo {i}")).ToArray();
        Codigo(() => vinculo.Eximir(false, demasiados, "x", null, "admin", Hoy)).ShouldBe("exencion-alcance-invalido");
        vinculo.Eximir(false, demasiados[..Vinculo.MaximoDeObjetivosPorExencion], "x", null, "admin", Hoy).ShouldNotBeNull();
    }

    [Fact]
    public void Solo_se_exime_un_vinculo_que_ya_fue_aprobado_y_que_no_esta_bloqueado_ni_terminado()
    {
        // Nunca aprobado: eximirlo equivaldría a aprobarlo sin revisión.
        Codigo(() => Nuevo().Eximir(true, [], "x", null, "admin", Hoy)).ShouldBe("vinculo-sin-aprobacion");

        var bloqueado = Activo();
        bloqueado.Apply(bloqueado.Bloquear("Investigación", null, Hoy));
        Codigo(() => bloqueado.Eximir(true, [], "x", null, "admin", Hoy)).ShouldBe("vinculo-bloqueado");

        var desvinculado = Activo();
        desvinculado.Apply(desvinculado.Desvincular("Terminó el contrato", Hoy));
        Codigo(() => desvinculado.Eximir(true, [], "x", null, "admin", Hoy)).ShouldBe("estado-terminal");

        var anulado = Activo();
        anulado.Apply(anulado.Anular("Duplicado", Hoy));
        Codigo(() => anulado.Eximir(true, [], "x", null, "admin", Hoy)).ShouldBe("estado-terminal");

        // También se exime uno por vencer o vencido: es justo el caso para el que existe.
        var porVencer = Activo();
        porVencer.Apply(porVencer.AvisarVigencia(Dia(Hasta.AddDays(-4)))!);
        porVencer.Estado.ShouldBe(EstadoVinculo.PorVencer);
        porVencer.Eximir(true, [], "x", null, "admin", Dia(Hasta.AddDays(-4))).ShouldNotBeNull();
    }

    [Fact]
    public void La_fecha_final_no_puede_ser_anterior_a_hoy_ni_pasar_de_diez_anios()
    {
        var vinculo = Activo();
        var hoy = CalculadoraDeVigencia.Fecha(Hoy);

        Codigo(() => vinculo.Eximir(true, [], "x", hoy.AddDays(-1), "admin", Hoy)).ShouldBe("fecha-invalida");
        Codigo(() => vinculo.Eximir(true, [], "x", hoy.AddYears(Vinculo.MaximoDeAniosDeExencion).AddDays(1), "admin", Hoy)).ShouldBe("fecha-invalida");
        vinculo.Eximir(true, [], "x", hoy, "admin", Hoy).Hasta.ShouldBe(hoy); // hoy mismo vale: dura hasta el final del día
    }

    [Fact]
    public void No_puede_haber_dos_exenciones_vigentes_sobre_lo_mismo_hasta_revocar_la_primera()
    {
        var vinculo = Activo();
        var rut = Eximir(vinculo, objetivos: [Rut]);

        Codigo(() => vinculo.Eximir(false, [Rut, Camara], "otra", null, "admin", Hoy)).ShouldBe("exencion-superpuesta");
        Codigo(() => vinculo.Eximir(true, [], "todo", null, "admin", Hoy)).ShouldBe("exencion-superpuesta");
        vinculo.Eximir(false, [Camara], "la Cámara no se cruza con el RUT", null, "admin", Hoy).ShouldNotBeNull();

        // Una vigente de todo choca con cualquier otra; revocada, deja de chocar.
        var total = Activo();
        var todo = Eximir(total, todos: true);
        Codigo(() => total.Eximir(false, [Camara], "x", null, "admin", Hoy)).ShouldBe("exencion-superpuesta");
        total.Apply(total.RevocarExencion(todo.ExencionId, "Ya no aplica", "admin", Hoy));
        total.Eximir(false, [Camara], "x", null, "admin", Hoy).ShouldNotBeNull();

        vinculo.Apply(vinculo.RevocarExencion(rut.ExencionId, "Ya no aplica", "admin", Hoy));
        vinculo.Eximir(false, [Rut], "de nuevo", null, "admin", Hoy).ShouldNotBeNull();
    }

    [Fact]
    public void Una_exencion_ya_vencida_por_fecha_no_choca_con_una_nueva()
    {
        var vinculo = Activo();
        var hoy = CalculadoraDeVigencia.Fecha(Hoy);
        Eximir(vinculo, objetivos: [Rut], hasta: hoy);

        var manana = Hoy.AddDays(1);
        vinculo.Eximir(false, [Rut], "renovada", null, "admin", manana).ShouldNotBeNull();
    }

    // ---------- la regla única ----------

    [Fact]
    public void La_regla_unica_dice_si_un_documento_una_seccion_o_todo_esta_eximido_y_hasta_cuando()
    {
        var vinculo = Activo();
        var hoy = CalculadoraDeVigencia.Fecha(Hoy);
        vinculo.ExencionDeDocumento(TipoRut, hoy).ShouldBeNull();

        var concreta = Eximir(vinculo, objetivos: [Rut, General], hasta: hoy.AddDays(10));
        vinculo.ExencionDeDocumento(TipoRut, hoy)!.Id.ShouldBe(concreta.ExencionId);
        vinculo.ExencionDeSeccion("FICHA_GENERAL", hoy)!.Id.ShouldBe(concreta.ExencionId);
        vinculo.ExencionDeSeccion("ficha_general", hoy).ShouldNotBeNull(); // el código no distingue mayúsculas
        vinculo.ExencionDeDocumento(TipoCamara, hoy).ShouldBeNull();
        vinculo.ExencionDeSeccion("FICHA_TRIBUTARIA", hoy).ShouldBeNull();
        vinculo.ExencionTotal(hoy).ShouldBeNull();
        vinculo.ExencionDeRequisito(TipoDeRequisito.Documento, TipoRut, null, hoy).ShouldNotBeNull();
        vinculo.ExencionDeRequisito(TipoDeRequisito.Seccion, null, "FICHA_GENERAL", hoy).ShouldNotBeNull();
        // El certificado nombra cada requisito por su código.
        vinculo.ExencionDeCodigo("RUT", hoy).ShouldNotBeNull();
        vinculo.ExencionDeCodigo("CAMARA_COMERCIO", hoy).ShouldBeNull();

        // Hasta es inclusive: el último día todavía vale; al día siguiente ya no.
        vinculo.ExencionDeDocumento(TipoRut, hoy.AddDays(10)).ShouldNotBeNull();
        vinculo.ExencionDeDocumento(TipoRut, hoy.AddDays(11)).ShouldBeNull();
        vinculo.Exenciones.Single().EstadoEn(hoy.AddDays(11)).ShouldBe(EstadoDeExencion.Vencida);
        vinculo.Exenciones.Single().Estado.ShouldBe(EstadoDeExencion.Vigente); // hasta que el job la registre
    }

    [Fact]
    public void La_exencion_total_cubre_cualquier_requisito_y_una_revocada_no_cubre_nada()
    {
        var vinculo = Activo();
        var hoy = CalculadoraDeVigencia.Fecha(Hoy);
        var total = Eximir(vinculo, todos: true);

        vinculo.ExencionTotal(hoy).ShouldNotBeNull();
        vinculo.ExencionDeDocumento(Guid.NewGuid(), hoy).ShouldNotBeNull();
        vinculo.ExencionDeSeccion("FICHA_FINANCIERA", hoy).ShouldNotBeNull();
        vinculo.ExencionDeCodigo("LO_QUE_SEA", hoy).ShouldNotBeNull();

        var revocada = vinculo.RevocarExencion(total.ExencionId, "Cambió la política", "carlos", Hoy.AddDays(1));
        vinculo.Apply(revocada);
        var exencion = vinculo.Exenciones.Single();
        (exencion.Estado, exencion.RevocadaPor, exencion.JustificacionDeRevocacion, exencion.RevocadaEn)
            .ShouldBe((EstadoDeExencion.Revocada, "carlos", "Cambió la política", Hoy.AddDays(1)));
        vinculo.ExencionTotal(hoy).ShouldBeNull();
        vinculo.ExencionDeDocumento(TipoRut, hoy).ShouldBeNull();
    }

    // ---------- revocar ----------

    [Fact]
    public void Revocar_pide_una_exencion_vigente_y_se_puede_en_un_vinculo_bloqueado()
    {
        var vinculo = Activo();
        var exencion = Eximir(vinculo, objetivos: [Rut]);

        Codigo(() => vinculo.RevocarExencion(Guid.NewGuid(), "x", "admin", Hoy)).ShouldBe("exencion-no-encontrada");

        vinculo.Apply(vinculo.Bloquear("Investigación", null, Hoy));
        var revocada = vinculo.RevocarExencion(exencion.ExencionId, "Ya no aplica", "admin", Hoy);
        vinculo.Apply(revocada);

        Codigo(() => vinculo.RevocarExencion(exencion.ExencionId, "otra vez", "admin", Hoy)).ShouldBe("exencion-ya-revocada");
    }

    [Fact]
    public void No_se_revoca_una_exencion_que_ya_vencio_y_un_vinculo_terminado_no_admite_cambios()
    {
        var vinculo = Activo();
        var hoy = CalculadoraDeVigencia.Fecha(Hoy);
        var exencion = Eximir(vinculo, objetivos: [Rut], hasta: hoy);

        Codigo(() => vinculo.RevocarExencion(exencion.ExencionId, "x", "admin", Hoy.AddDays(2))).ShouldBe("exencion-vencida");

        var anulado = Activo();
        var otra = Eximir(anulado, objetivos: [Rut]);
        anulado.Apply(anulado.Anular("Duplicado", Hoy));
        Codigo(() => anulado.RevocarExencion(otra.ExencionId, "x", "admin", Hoy)).ShouldBe("estado-terminal");
    }

    // ---------- la vigencia periódica con la exención ----------

    [Fact]
    public void Con_una_exencion_total_no_avisa_ni_vence_y_el_job_solo_espera_el_fin_de_la_exencion()
    {
        var vinculo = Activo(gracia: 5);
        var finDeLaExencion = new DateOnly(2027, 11, 30); // después de la vigencia y su gracia
        Eximir(vinculo, todos: true, hasta: finDeLaExencion, cuando: Dia(Hasta.AddDays(-40)));

        // Ni el aviso de 30 días ni el de 5 ni el vencimiento salen mientras rija.
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-29))).ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-4))).ShouldBeNull();
        vinculo.AvisarVigencia(Dia(finDeLaExencion)).ShouldBeNull();
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(7), 6), "Vigencia").ShouldBeNull();
        // Lo único que el job tiene que mirar es el fin de la exención: la medianoche del día siguiente (00:00 en Bogotá = 05:00 UTC).
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(finDeLaExencion.AddDays(1), 5));
    }

    [Fact]
    public void Al_terminar_la_exencion_el_estado_se_recalcula_y_el_vinculo_avisa_o_vence_con_la_regla_de_siempre()
    {
        var vinculo = Activo();
        var fin = new DateOnly(2027, 11, 10); // después de la fecha de vigencia
        var exencion = Eximir(vinculo, todos: true, hasta: fin, cuando: Dia(Hasta.AddDays(-40)));
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(3), 6), "Vigencia").ShouldBeNull();

        // Pasa la fecha de la exención: el job la registra y, en la misma pasada, el vínculo ya puede vencer.
        var ahora = Dia(fin.AddDays(1), 5);
        vinculo.VencerExenciones(Dia(fin, 20)).ShouldBeEmpty(); // el último día todavía rige
        var vencidas = vinculo.VencerExenciones(ahora);
        vencidas.ShouldHaveSingleItem().ShouldSatisfyAllConditions(e => e.ExencionId.ShouldBe(exencion.ExencionId), e => e.Hasta.ShouldBe(fin));
        vinculo.Apply(vencidas[0]);

        vinculo.Exenciones.Single().Estado.ShouldBe(EstadoDeExencion.Vencida);
        vinculo.Exenciones.Single().VencidaEn.ShouldBe(ahora);
        vinculo.VencerExenciones(ahora).ShouldBeEmpty(); // idempotente
        var vencido = vinculo.VencerPorVigencia(ahora, "Vigencia del vínculo").ShouldNotBeNull();
        vencido.Causa.ShouldBe(CausaDeVencimiento.Vigencia);
        // Y con los avisos y el vencimiento de la vigencia otra vez en el índice del job (el primer aviso, ya pasado, es lo más próximo).
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-30), 13));
    }

    [Fact]
    public void Al_revocar_una_exencion_total_los_avisos_vuelven_a_salir_con_las_fechas_que_ya_tenia_el_vinculo()
    {
        var vinculo = Activo();
        var exencion = Eximir(vinculo, todos: true, cuando: Dia(Hasta.AddDays(-40)));
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull(); // sin fin ni nada que mirar

        vinculo.Apply(vinculo.RevocarExencion(exencion.ExencionId, "Ya no aplica", "admin", Dia(Hasta.AddDays(-20))));

        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-30), 13)); // el aviso de 30 días ya pasó: el job lo cumple ya
        var aviso = vinculo.AvisarVigencia(Dia(Hasta.AddDays(-20))).ShouldNotBeNull();
        aviso.AvisosCumplidos.ShouldBe([30]);
    }

    [Fact]
    public void Una_exencion_parcial_no_toca_la_vigencia_periodica_del_vinculo()
    {
        var vinculo = Activo();
        Eximir(vinculo, objetivos: [Rut]);

        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-30), 13)).ShouldNotBeNull();
        vinculo.Vencer("RUT", Dia(Hasta.AddDays(2))).ShouldNotBeNull(); // el filtro por tipo lo hace quien llama; el vínculo solo respeta la exención total
    }

    [Fact]
    public void Un_documento_vencido_no_vence_al_vinculo_mientras_rija_una_exencion_total()
    {
        var vinculo = Activo();
        var exencion = Eximir(vinculo, todos: true);

        vinculo.Vencer("RUT", Hoy).ShouldBeNull();
        vinculo.Apply(vinculo.RevocarExencion(exencion.ExencionId, "Ya no aplica", "admin", Hoy));
        vinculo.Vencer("RUT", Hoy).ShouldNotBeNull();
    }

    [Fact]
    public void El_job_mira_el_fin_de_una_exencion_aunque_el_vinculo_no_tenga_vigencia_periodica()
    {
        var activo = Activo();
        activo.Apply(activo.FijarVigencia(null, [], 0, Hoy)!);
        activo.ProximaRevisionDeVigencia.ShouldBeNull();
        var hasta = new DateOnly(2027, 3, 1);
        Eximir(activo, objetivos: [Rut], hasta: hasta);
        activo.ProximaRevisionDeVigencia.ShouldBe(Dia(hasta.AddDays(1), 5));

        // Un vínculo terminado no tiene nada pendiente.
        activo.Apply(activo.Desvincular("Terminó", Hoy));
        activo.ProximaRevisionDeVigencia.ShouldBeNull();
    }

    // ---------- restablecer ----------

    [Fact]
    public void Un_vinculo_por_vencer_o_vencido_sin_expediente_abierto_vuelve_a_activo_con_una_exencion_total()
    {
        // Por vencer sin renovación abierta (se rechazó antes del vencimiento).
        var porVencer = Activo();
        porVencer.Apply(porVencer.AvisarVigencia(Dia(Hasta.AddDays(-4)))!);
        porVencer.RestablecerPorExencion(Hoy).ShouldBeNull(); // sin exención no hay nada que restablecer
        Eximir(porVencer, objetivos: [Rut], cuando: Dia(Hasta.AddDays(-3)));
        porVencer.RestablecerPorExencion(Dia(Hasta.AddDays(-3))).ShouldBeNull(); // parcial: no alcanza

        var vencido = Activo();
        vencido.Apply(vencido.VencerPorVigencia(Dia(Hasta.AddDays(1), 5), "Vigencia")!);
        vencido.RenovacionPorVigenciaPendiente.ShouldBeTrue();
        var exencion = Eximir(vencido, todos: true, cuando: Dia(Hasta.AddDays(2)));
        var restablecido = vencido.RestablecerPorExencion(Dia(Hasta.AddDays(2))).ShouldNotBeNull();
        (restablecido.EstadoPrevio, restablecido.ExencionId).ShouldBe((EstadoVinculo.Vencido, exencion.ExencionId));
        vencido.Apply(restablecido);

        vencido.Estado.ShouldBe(EstadoVinculo.Activo);
        vencido.RenovacionPorVigenciaPendiente.ShouldBeFalse();
        // Mientras la exención rija, el job no lo vuelve a vencer.
        vencido.VencerPorVigencia(Dia(Hasta.AddDays(3), 5), "Vigencia").ShouldBeNull();
        vencido.RestablecerPorExencion(Dia(Hasta.AddDays(3))).ShouldBeNull(); // ya está activo
    }

    [Fact]
    public void Con_un_expediente_abierto_el_cierre_del_expediente_es_el_camino_y_no_se_restablece_solo()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.VencerPorVigencia(Dia(Hasta.AddDays(1), 5), "Vigencia")!);
        vinculo.Apply(vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Renovacion, "N-E-2", Dia(Hasta.AddDays(1), 6)));
        Eximir(vinculo, todos: true, cuando: Dia(Hasta.AddDays(2)));

        vinculo.RestablecerPorExencion(Dia(Hasta.AddDays(2))).ShouldBeNull();
    }
}
