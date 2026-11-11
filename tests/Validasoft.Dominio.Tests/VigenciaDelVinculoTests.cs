using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Vigencia periódica del vínculo completo (VV-1…VV-5) con el reloj simulado: la configuración del flujo, el cálculo de la fecha
/// (periódica y la de cada sección), el ciclo aviso → <c>PorVencer</c> → <c>Vencido</c> → renovación → <c>Activo</c> y el fin de los
/// bloqueos con fecha. Aprobado el 6/11/2026 (hora de Bogotá): con 365 días vale hasta el 6/11/2027.
/// </summary>
public class VigenciaDelVinculoTests
{
    private static readonly DateTimeOffset Aprobacion = new(2026, 11, 6, 14, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hasta = new(2027, 11, 6);

    private static DateTimeOffset Dia(DateOnly fecha, int hora = 14) => new(fecha.ToDateTime(new TimeOnly(hora, 0)), TimeSpan.Zero);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- configuración ----------

    [Fact]
    public void Sin_vigencia_por_omision_y_los_avisos_quedan_de_mayor_a_menor()
    {
        VigenciaDelVinculo.Sin.Activa.ShouldBeFalse();
        VigenciaDelVinculo.Sin.Validar().DiasAviso.ShouldBe([30, 15, 5]);

        var vigencia = new VigenciaDelVinculo(365, [5, 30, 15, 30], 10).Validar();
        vigencia.Activa.ShouldBeTrue();
        vigencia.DiasAviso.ShouldBe([30, 15, 5]);
        vigencia.DiasGracia.ShouldBe(10);
    }

    [Theory]
    [InlineData(-1, 0, "vigencia-vinculo-invalida")]
    [InlineData(3651, 0, "vigencia-vinculo-invalida")]
    [InlineData(100, -1, "gracia-vigencia-invalida")]
    [InlineData(100, 366, "gracia-vigencia-invalida")]
    public void La_vigencia_y_la_gracia_tienen_limites(int dias, int gracia, string codigo) =>
        Codigo(() => new VigenciaDelVinculo(dias, [], gracia).Validar()).ShouldBe(codigo);

    [Fact]
    public void Los_avisos_son_positivos_menores_que_la_vigencia_y_hasta_cinco()
    {
        Codigo(() => new VigenciaDelVinculo(30, [30], 0).Validar()).ShouldBe("aviso-vigencia-invalido");
        Codigo(() => new VigenciaDelVinculo(30, [0], 0).Validar()).ShouldBe("aviso-vigencia-invalido");
        Codigo(() => new VigenciaDelVinculo(365, [1, 2, 3, 4, 5, 6], 0).Validar()).ShouldBe("aviso-vigencia-invalido");
        new VigenciaDelVinculo(30, [29, 1], 0).Validar().DiasAviso.ShouldBe([29, 1]);
        // Sin vigencia periódica los avisos no tienen contra qué medirse, pero siguen acotados.
        Codigo(() => new VigenciaDelVinculo(0, [400], 0).Validar()).ShouldBe("aviso-vigencia-invalido");
    }

    // ---------- flujo ----------

    private readonly Guid _empresa = Guid.NewGuid();

    private IReadOnlyList<Area> Areas() => [Area.Crear(_empresa, "COMPRAS", "Compras")];

    private DefinicionDeFlujo Definicion(IReadOnlyList<Area> areas, params RequisitoDelFlujo[] requisitos) =>
        DefinicionDeFlujo.TodasEnParalelo(areas) with { Requisitos = requisitos };

    private static RequisitoDelFlujo Seccion(string codigo, int? vigencia = null, int? revalidacion = null) =>
        new(Guid.Empty, Guid.Empty, codigo, true, [], 1, Seccion: codigo, VigenciaDias: vigencia, DiasRevalidacion: revalidacion);

    [Fact]
    public void Un_flujo_con_vigencia_del_vinculo_debe_pedir_al_menos_una_seccion()
    {
        var areas = Areas();
        var conVigencia = new VigenciaDelVinculo(365, [30, 5], 15);

        var definicion = Definicion(areas, Seccion("FICHA_GENERAL")) with { VigenciaVinculo = conVigencia };
        definicion.Validar(_empresa, [.. areas]).VigenciaVinculo.ShouldBe(conVigencia with { DiasAviso = [30, 5] }, new VigenciaIgual());

        Codigo(() => (DefinicionDeFlujo.TodasEnParalelo(areas) with { VigenciaVinculo = conVigencia }).Validar(_empresa, [.. areas]))
            .ShouldBe("vigencia-sin-secciones");
        // Sin vigencia periódica no hace falta ninguna sección.
        DefinicionDeFlujo.TodasEnParalelo(areas).Validar(_empresa, [.. areas]).VigenciaVinculo!.Activa.ShouldBeFalse();
    }

    [Fact]
    public void La_vigencia_y_el_plazo_de_revalidacion_de_una_seccion_se_validan_y_no_aplican_a_un_documento()
    {
        var areas = Areas();
        var valida = Definicion(areas, Seccion("FICHA_GENERAL", vigencia: 180, revalidacion: 7)).Validar(_empresa, [.. areas]);
        valida.Requisitos!.Single().ShouldSatisfyAllConditions(r => r.VigenciaDias.ShouldBe(180), r => r.DiasRevalidacion.ShouldBe(7));

        Codigo(() => Definicion(areas, Seccion("FICHA_GENERAL", vigencia: 0)).Validar(_empresa, [.. areas])).ShouldBe("vigencia-seccion-invalida");
        Codigo(() => Definicion(areas, Seccion("FICHA_GENERAL", vigencia: 4000)).Validar(_empresa, [.. areas])).ShouldBe("vigencia-seccion-invalida");
        Codigo(() => Definicion(areas, Seccion("FICHA_GENERAL", revalidacion: 0)).Validar(_empresa, [.. areas])).ShouldBe("dias-revalidacion-invalidos");
        Codigo(() => Definicion(areas, Seccion("FICHA_GENERAL", revalidacion: 366)).Validar(_empresa, [.. areas])).ShouldBe("dias-revalidacion-invalidos");

        var documento = new RequisitoDelFlujo(Guid.Empty, Guid.NewGuid(), "RUT", true, [], 1, VigenciaDias: 30);
        Codigo(() => Definicion(areas, documento, Seccion("FICHA_GENERAL")).Validar(_empresa, [.. areas])).ShouldBe("vigencia-no-aplica");
    }

    [Fact]
    public void Publicar_congela_la_vigencia_del_vinculo_y_una_version_sin_ella_no_vence_periodicamente()
    {
        var areas = Areas();
        var flujo = Flujo.Crear(_empresa, "PROV", "Proveedores", Guid.NewGuid(),
            Definicion(areas, Seccion("FICHA_GENERAL")) with { VigenciaVinculo = new VigenciaDelVinculo(365, [30], 5) }, [.. areas], Aprobacion);
        var v1 = flujo.Publicar([.. areas], Aprobacion);
        v1.VigenciaEfectiva().ShouldBe(new VigenciaDelVinculo(365, [30], 5), new VigenciaIgual());

        // Editarla crea la versión siguiente; la publicada no cambia (y una versión guardada antes de VV-1 no la tiene).
        var borrador = flujo.EditarBorrador(null, Definicion(areas, Seccion("FICHA_GENERAL")), [.. areas], Aprobacion);
        borrador.VigenciaEfectiva().Activa.ShouldBeFalse();
        v1.VigenciaEfectiva().Activa.ShouldBeTrue();
        new FlujoVersion().VigenciaEfectiva().ShouldBe(VigenciaDelVinculo.Sin);
    }

    // ---------- cálculo de la fecha ----------

    private static RequisitoDelFlujo ConVigencia(string codigo, int dias) => Seccion(codigo, vigencia: dias);

    [Fact]
    public void La_vigencia_es_la_aprobacion_mas_los_dias_y_la_de_una_seccion_la_acorta()
    {
        var vigencia = new VigenciaDelVinculo(365, [30], 0);
        VigenciaDeAprobacion.VigenteHasta(vigencia, [], [], Aprobacion).ShouldBe(Hasta);
        // En zona del negocio: a las 8 p. m. de Bogotá del 6/11 todavía es 6/11 aunque en UTC ya sea el 7.
        VigenciaDeAprobacion.VigenteHasta(vigencia, [], [], new DateTimeOffset(2026, 11, 7, 1, 0, 0, TimeSpan.Zero)).ShouldBe(Hasta);

        var general = ConVigencia("FICHA_GENERAL", 180);
        var aceptada = new SeccionAceptadaEn("FICHA_GENERAL", null, Aprobacion);
        VigenciaDeAprobacion.VigenteHasta(vigencia, [general], [aceptada], Aprobacion).ShouldBe(new DateOnly(2027, 5, 5));
        // Una sección con una vigencia mayor, sin aceptar o sin vigencia propia no acorta nada.
        VigenciaDeAprobacion.VigenteHasta(vigencia, [ConVigencia("FICHA_GENERAL", 400)], [aceptada], Aprobacion).ShouldBe(Hasta);
        VigenciaDeAprobacion.VigenteHasta(vigencia, [general], [], Aprobacion).ShouldBe(Hasta);
        VigenciaDeAprobacion.VigenteHasta(vigencia, [Seccion("FICHA_GENERAL")], [aceptada], Aprobacion).ShouldBe(Hasta);
        // Sin vigencia periódica, la de la sección es la única.
        VigenciaDeAprobacion.VigenteHasta(VigenciaDelVinculo.Sin, [general], [aceptada], Aprobacion).ShouldBe(new DateOnly(2027, 5, 5));
        VigenciaDeAprobacion.VigenteHasta(VigenciaDelVinculo.Sin, [], [], Aprobacion).ShouldBeNull();
    }

    [Fact]
    public void Se_sabe_si_vencio_la_vigencia_periodica_o_la_de_alguna_seccion()
    {
        var vigencia = new VigenciaDelVinculo(365, [], 0);
        VigenciaDeAprobacion.PeriodicaVencida(vigencia, Aprobacion, Hasta).ShouldBeFalse(); // inclusive: el último día todavía vale
        VigenciaDeAprobacion.PeriodicaVencida(vigencia, Aprobacion, Hasta.AddDays(1)).ShouldBeTrue();
        VigenciaDeAprobacion.PeriodicaVencida(VigenciaDelVinculo.Sin, Aprobacion, Hasta.AddDays(500)).ShouldBeFalse();
        VigenciaDeAprobacion.PeriodicaVencida(vigencia, null, Hasta.AddDays(500)).ShouldBeFalse();

        var general = ConVigencia("FICHA_GENERAL", 180);
        var tributaria = ConVigencia("FICHA_TRIBUTARIA", 400);
        var aceptadas = new[] { new SeccionAceptadaEn("FICHA_GENERAL", null, Aprobacion), new SeccionAceptadaEn("FICHA_TRIBUTARIA", null, Aprobacion) };
        VigenciaDeAprobacion.SeccionesVencidas([general, tributaria], aceptadas, new DateOnly(2027, 5, 5)).ShouldBeEmpty();
        VigenciaDeAprobacion.SeccionesVencidas([general, tributaria], aceptadas, new DateOnly(2027, 5, 6)).ShouldBe([general]);
        VigenciaDeAprobacion.SeccionesVencidas([general, tributaria], aceptadas, new DateOnly(2027, 12, 20)).Count.ShouldBe(2);
    }

    // ---------- ciclo del vínculo ----------

    private static Vinculo Nuevo() =>
        Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "compras@proveedor.co", Aprobacion));

    /// <summary>Vínculo aprobado en <paramref name="cuando"/> con una vigencia de 365 días, avisos a 30, 15 y 5 y la gracia indicada.</summary>
    private static Vinculo Activo(int gracia = 0, DateTimeOffset? cuando = null, VigenciaDelVinculo? vigencia = null)
    {
        var fecha = cuando ?? Aprobacion;
        var config = vigencia ?? new VigenciaDelVinculo(365, [30, 15, 5], gracia);
        var vinculo = Nuevo();
        var expediente = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(expediente, TipoExpediente.Inicial, "N-E-1", fecha));
        Aprobar(vinculo, expediente, fecha, config);
        return vinculo;
    }

    /// <summary>Cierra el expediente abierto como aprobado y fija la vigencia, como lo hace el proceso de validación.</summary>
    private static void Aprobar(Vinculo vinculo, Guid expediente, DateTimeOffset cuando, VigenciaDelVinculo config)
    {
        foreach (var evento in vinculo.CerrarExpediente(expediente, ResultadoExpediente.Aprobado, cuando))
        {
            if (evento is ExpedienteCerrado cerrado) vinculo.Apply(cerrado);
            if (evento is VinculoActivado activado) vinculo.Apply(activado);
        }

        var hasta = VigenciaDeAprobacion.VigenteHasta(config, [], [], cuando);
        if (vinculo.FijarVigencia(hasta, config.DiasAviso, config.DiasGracia, cuando) is { } fijada)
            vinculo.Apply(fijada);
    }

    [Fact]
    public void Al_aprobarse_queda_fijada_la_vigencia_y_el_job_sabe_cuando_mirarlo()
    {
        var vinculo = Activo();

        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.VigenteHasta.ShouldBe(Hasta);
        vinculo.UltimaAprobacionEn.ShouldBe(Aprobacion);
        vinculo.DiasAvisoVigencia.ShouldBe([30, 15, 5]);
        // El primer aviso: 30 días antes del 6/11/2027, a las 13:00 UTC (8:00 en Colombia).
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-30), 13));
    }

    [Fact]
    public void Sin_vigencia_periodica_no_se_genera_ningun_evento_ni_hay_nada_que_revisar()
    {
        var vinculo = Activo(vigencia: VigenciaDelVinculo.Sin);

        vinculo.VigenteHasta.ShouldBeNull();
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull();
        vinculo.FijarVigencia(null, [], 0, Aprobacion).ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(500))).ShouldBeNull();
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(500)), "Vigencia").ShouldBeNull();
    }

    [Fact]
    public void Con_el_reloj_simulado_avisa_en_cada_dia_de_aviso_pasa_a_por_vencer_y_no_repite()
    {
        var vinculo = Activo();

        // Un día antes del primer aviso no pasa nada.
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-31))).ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-30), 12)).ShouldBeNull(); // antes de las 13:00 UTC del día de aviso

        var primero = vinculo.AvisarVigencia(Dia(Hasta.AddDays(-30), 13)).ShouldNotBeNull();
        primero.ShouldSatisfyAllConditions(
            e => e.VigenteHasta.ShouldBe(Hasta),
            e => e.DiasRestantes.ShouldBe(30),
            e => e.AvisosCumplidos.ShouldBe([30]));
        vinculo.Apply(primero);
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-15), 13));
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-30), 15)).ShouldBeNull(); // idempotente: el mismo aviso no sale dos veces

        // Los siguientes avisos mantienen PorVencer y se cumplen uno a uno.
        var segundo = vinculo.AvisarVigencia(Dia(Hasta.AddDays(-15), 20)).ShouldNotBeNull();
        segundo.AvisosCumplidos.ShouldBe([15]);
        vinculo.Apply(segundo);
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.Apply(vinculo.AvisarVigencia(Dia(Hasta.AddDays(-5), 13)).ShouldNotBeNull());
        vinculo.AvisosDeVigenciaCumplidos.ShouldBe([30, 15, 5], ignoreOrder: true);
        // Ya no hay más avisos: lo próximo es el vencimiento, al día siguiente a las 05:00 UTC.
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(1), 5));
    }

    [Fact]
    public void Un_job_atrasado_cumple_juntos_los_avisos_que_ya_pasaron_con_un_solo_evento()
    {
        var vinculo = Activo();

        var evento = vinculo.AvisarVigencia(Dia(Hasta.AddDays(-4))).ShouldNotBeNull(); // pasaron los de 30, 15 y 5 días
        evento.AvisosCumplidos.ShouldBe([5, 15, 30]);
        evento.DiasRestantes.ShouldBe(4);
        vinculo.Apply(evento);
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-3))).ShouldBeNull();
    }

    [Fact]
    public void Vence_al_terminar_la_vigencia_y_su_gracia_y_queda_pendiente_la_renovacion()
    {
        var vinculo = Activo(gracia: 10);
        vinculo.VencimientoDeVigencia.ShouldBe(Dia(Hasta.AddDays(11), 5));

        // Pasada la fecha pero dentro de la gracia sigue valiendo.
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(1)), "Vigencia").ShouldBeNull();
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(10), 23), "Vigencia").ShouldBeNull();

        var vencido = vinculo.VencerPorVigencia(Dia(Hasta.AddDays(11), 5), "Vigencia del vínculo").ShouldNotBeNull();
        vencido.Causa.ShouldBe(CausaDeVencimiento.Vigencia);
        vinculo.Apply(vencido);
        vinculo.Estado.ShouldBe(EstadoVinculo.Vencido);
        vinculo.RenovacionPorVigenciaPendiente.ShouldBeTrue();
        // Si no se pudo abrir la renovación el job la reintenta: el vínculo sigue «debiendo» una revisión.
        vinculo.ProximaRevisionDeVigencia.ShouldBe(vinculo.VencimientoDeVigencia);
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(12)), "Vigencia").ShouldBeNull(); // ya venció
    }

    [Fact]
    public void Abierta_la_renovacion_el_vinculo_sigue_vencido_y_al_aprobarse_vuelve_a_activo_con_vigencia_nueva()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.AvisarVigencia(Dia(Hasta.AddDays(-5))).ShouldNotBeNull());
        var vence = Dia(Hasta.AddDays(1), 5);
        vinculo.Apply(vinculo.VencerPorVigencia(vence, "Vigencia").ShouldNotBeNull());

        var renovacion = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(renovacion, TipoExpediente.Renovacion, "N-E-2", vence));
        vinculo.Estado.ShouldBe(EstadoVinculo.Vencido);
        vinculo.RenovacionPorVigenciaPendiente.ShouldBeFalse();
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull(); // el tercero tiene la pelota

        // El tercero ratifica y la renovación se aprueba 3 días después: activo, y la vigencia corre desde esa aprobación.
        var aprobada = vence.AddDays(3);
        Aprobar(vinculo, renovacion, aprobada, new VigenciaDelVinculo(365, [30, 15, 5], 0));
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.VigenteHasta.ShouldBe(new DateOnly(2028, 11, 9));
        vinculo.AvisosDeVigenciaCumplidos.ShouldBeEmpty();
        vinculo.UltimaAprobacionEn.ShouldBe(aprobada);
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(new DateOnly(2028, 11, 9).AddDays(-30), 13));
    }

    // ---------- renovación en el pre-vencimiento (VV-3) ----------

    /// <summary>Vínculo que llegó a su primer aviso (30 días antes) y al que se le abrió la renovación, como lo hace el job.</summary>
    private static Vinculo EnPreVencimiento(out Guid renovacion)
    {
        var vinculo = Activo(gracia: 5);
        var primerAviso = Dia(Hasta.AddDays(-30), 13);
        vinculo.Apply(vinculo.AvisarVigencia(primerAviso).ShouldNotBeNull());
        renovacion = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(renovacion, TipoExpediente.Renovacion, "N-E-2", primerAviso));
        return vinculo;
    }

    [Fact]
    public void Con_el_primer_aviso_se_abre_la_renovacion_y_el_vinculo_sigue_por_vencer_con_los_avisos_en_pie()
    {
        var vinculo = EnPreVencimiento(out var renovacion);

        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.ExpedienteAbiertoId.ShouldBe(renovacion);
        vinculo.TieneRenovacionAbierta.ShouldBeTrue();
        vinculo.RenovacionPorVigenciaPendiente.ShouldBeFalse();
        // La renovación abierta no apaga el reloj: los avisos siguientes recuerdan y el vencimiento llega con ella abierta.
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-15), 13));

        // Un aviso posterior sigue saliendo (recuerda la renovación pendiente) y mantiene el estado y el expediente.
        var recordatorio = vinculo.AvisarVigencia(Dia(Hasta.AddDays(-15), 14)).ShouldNotBeNull();
        recordatorio.AvisosCumplidos.ShouldBe([15]);
        vinculo.Apply(recordatorio);
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.ExpedienteAbiertoId.ShouldBe(renovacion);
        Codigo(() => vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Renovacion, "N-E-3", Dia(Hasta.AddDays(-15), 14)))
            .ShouldBe("expediente-abierto-existente"); // nunca dos abiertas
    }

    [Fact]
    public void Aprobada_la_renovacion_antes_de_la_fecha_la_vigencia_corre_desde_la_aprobacion_y_el_vinculo_nunca_vence()
    {
        var vinculo = EnPreVencimiento(out var renovacion);

        // El tercero ratifica y la empresa aprueba 10 días antes de la fecha.
        var aprobada = Dia(Hasta.AddDays(-10));
        Aprobar(vinculo, renovacion, aprobada, new VigenciaDelVinculo(365, [30, 15, 5], 5));

        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.ExpedienteAbiertoId.ShouldBeNull();
        vinculo.TieneRenovacionAbierta.ShouldBeFalse();
        // VV-1: los días se cuentan desde la última aprobación, no desde el vencimiento anterior.
        vinculo.UltimaAprobacionEn.ShouldBe(aprobada);
        vinculo.VigenteHasta.ShouldBe(Hasta.AddDays(-10).AddDays(365));
        vinculo.AvisosDeVigenciaCumplidos.ShouldBeEmpty();
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(vinculo.VigenteHasta!.Value.AddDays(-30), 13));
        // La fecha vieja pasa sin efecto: no vence, ni siquiera tras la gracia.
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(6), 6), "Vigencia").ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(6), 6)).ShouldBeNull();
    }

    [Fact]
    public void Si_vence_con_la_renovacion_abierta_vence_el_vinculo_y_esa_misma_renovacion_sigue_su_curso()
    {
        var vinculo = EnPreVencimiento(out var renovacion);
        vinculo.Apply(vinculo.AvisarVigencia(Dia(Hasta.AddDays(-5), 13)).ShouldNotBeNull()); // los recordatorios no abren otra

        // Termina la gracia (5 días) con la renovación todavía abierta.
        var vence = Dia(Hasta.AddDays(6), 5);
        var vencido = vinculo.VencerPorVigencia(vence, "Vigencia").ShouldNotBeNull();
        vencido.Causa.ShouldBe(CausaDeVencimiento.Vigencia);
        vinculo.Apply(vencido);

        vinculo.Estado.ShouldBe(EstadoVinculo.Vencido);
        vinculo.ExpedienteAbiertoId.ShouldBe(renovacion);
        vinculo.TieneRenovacionAbierta.ShouldBeTrue();
        vinculo.RenovacionPorVigenciaPendiente.ShouldBeFalse(); // no hay otra por abrir: el job no reintenta
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull();

        // Se aprueba ya vencido: vuelve a Activo y la vigencia corre desde esa aprobación.
        var aprobada = vence.AddDays(4);
        Aprobar(vinculo, renovacion, aprobada, new VigenciaDelVinculo(365, [30, 15, 5], 5));
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.VigenteHasta.ShouldBe(Hasta.AddDays(10).AddDays(365));
    }

    [Fact]
    public void Una_renovacion_de_pre_vencimiento_rechazada_deja_el_vinculo_por_vencer_y_los_avisos_o_el_vencimiento_siguen()
    {
        var vinculo = EnPreVencimiento(out var renovacion);

        vinculo.Apply((ExpedienteCerrado)vinculo.CerrarExpediente(renovacion, ResultadoExpediente.Rechazado, Dia(Hasta.AddDays(-20))).Single());

        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
        vinculo.TieneRenovacionAbierta.ShouldBeFalse();
        vinculo.ExpedienteAbiertoTipo.ShouldBeNull();
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-15), 13)); // el siguiente aviso abrirá otra renovación
        vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Renovacion, "N-E-3", Dia(Hasta.AddDays(-15), 13)).ShouldNotBeNull();
    }

    [Fact]
    public void Una_actualizacion_abierta_no_cuenta_como_la_renovacion_y_al_vencer_deja_la_renovacion_por_abrir()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Actualizacion, "N-E-2", Aprobacion.AddDays(10)));
        vinculo.TieneRenovacionAbierta.ShouldBeFalse();

        vinculo.Apply(vinculo.VencerPorVigencia(Dia(Hasta.AddDays(1), 5), "Vigencia").ShouldNotBeNull());

        vinculo.RenovacionPorVigenciaPendiente.ShouldBeTrue();
    }

    [Fact]
    public void Una_renovacion_rechazada_deja_el_vinculo_vencido_sin_reabrirla_sola()
    {
        var vinculo = Activo();
        var vence = Dia(Hasta.AddDays(1), 5);
        vinculo.Apply(vinculo.VencerPorVigencia(vence, "Vigencia").ShouldNotBeNull());
        var renovacion = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(renovacion, TipoExpediente.Renovacion, "N-E-2", vence));

        vinculo.Apply((ExpedienteCerrado)vinculo.CerrarExpediente(renovacion, ResultadoExpediente.Rechazado, vence.AddDays(2)).Single());

        vinculo.Estado.ShouldBe(EstadoVinculo.Vencido);
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull(); // la empresa decide (bloquear, desvincular, volver a abrir)
    }

    [Fact]
    public void Un_vinculo_vencido_con_otro_expediente_abierto_reintenta_la_renovacion_al_cerrarse_ese_expediente()
    {
        var vinculo = Activo();
        var actualizacion = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(actualizacion, TipoExpediente.Actualizacion, "N-E-2", Aprobacion.AddDays(10)));
        var vence = Dia(Hasta.AddDays(1), 5);
        vinculo.Apply(vinculo.VencerPorVigencia(vence, "Vigencia").ShouldNotBeNull());
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull(); // la actualización ocupa el lugar

        vinculo.Apply((ExpedienteCerrado)vinculo.CerrarExpediente(actualizacion, ResultadoExpediente.Rechazado, vence.AddDays(1)).Single());
        vinculo.ProximaRevisionDeVigencia.ShouldBe(vinculo.VencimientoDeVigencia);
    }

    [Fact]
    public void Una_vigencia_nueva_deja_al_vinculo_por_vencer_otra_vez_al_dia()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.AvisarVigencia(Dia(Hasta.AddDays(-5))).ShouldNotBeNull());
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);

        // Nada que ratificar: la vigencia se fija de nuevo desde ahora y el vínculo vuelve a Activo sin expediente.
        var fijada = vinculo.FijarVigencia(Hasta.AddDays(365), [30, 15, 5], 0, Dia(Hasta)).ShouldNotBeNull();
        vinculo.Apply(fijada);
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.VigenteHasta.ShouldBe(Hasta.AddDays(365));
        vinculo.AvisosDeVigenciaCumplidos.ShouldBeEmpty();
    }

    [Fact]
    public void Un_vinculo_bloqueado_no_vence_y_al_desbloquearse_se_evalua_con_la_fecha_de_ese_momento()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.Bloquear("Sanción", null, Dia(Hasta.AddDays(-60))));

        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(5)), "Vigencia").ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-4))).ShouldBeNull();
        vinculo.ProximaRevisionDeVigencia.ShouldBeNull(); // bloqueo sin fecha

        // Desbloqueado después de la fecha: debe vencer ya.
        vinculo.Apply(vinculo.Desbloquear("Levantada", Dia(Hasta.AddDays(5))));
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.ProximaRevisionDeVigencia!.Value.ShouldBeLessThan(Dia(Hasta.AddDays(5)));
        vinculo.VencerPorVigencia(Dia(Hasta.AddDays(5)), "Vigencia").ShouldNotBeNull();
    }

    [Fact]
    public void Terminar_el_vinculo_apaga_las_revisiones()
    {
        var vinculo = Activo();
        vinculo.ProximaRevisionDeVigencia.ShouldNotBeNull();

        vinculo.Apply(vinculo.Desvincular("Fin del contrato", Aprobacion.AddDays(5)));

        vinculo.ProximaRevisionDeVigencia.ShouldBeNull();
        vinculo.AvisarVigencia(Dia(Hasta.AddDays(-4))).ShouldBeNull();
    }

    // ---------- bloqueos con fecha ----------

    [Fact]
    public void Un_bloqueo_con_fecha_se_levanta_solo_al_llegar_y_restituye_el_estado_previo()
    {
        var vinculo = Activo();
        var antes = Aprobacion.AddDays(40);
        var hasta = antes.AddDays(7);
        vinculo.Apply(vinculo.Bloquear("Revisión de sanciones", hasta, antes));

        vinculo.Estado.ShouldBe(EstadoVinculo.Bloqueado);
        vinculo.ProximaRevisionDeVigencia.ShouldBe(hasta);
        vinculo.DesbloquearPorFecha(hasta.AddMinutes(-1)).ShouldBeNull();

        var evento = vinculo.DesbloquearPorFecha(hasta).ShouldNotBeNull();
        evento.EstadoRestituido.ShouldBe(EstadoVinculo.Activo);
        evento.Justificacion.ShouldContain("bloqueo temporal terminó");
        vinculo.Apply(evento);
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.Bloqueo.ShouldBeNull();
        vinculo.ProximaRevisionDeVigencia.ShouldBe(Dia(Hasta.AddDays(-30), 13));
    }

    [Fact]
    public void Si_se_desbloquea_a_mano_antes_o_se_bloquea_de_nuevo_la_fecha_vieja_no_hace_nada()
    {
        var vinculo = Activo();
        var hasta = Aprobacion.AddDays(47);
        vinculo.Apply(vinculo.Bloquear("Temporal", hasta, Aprobacion.AddDays(40)));
        vinculo.Apply(vinculo.Desbloquear("Resuelto antes", Aprobacion.AddDays(42)));

        vinculo.DesbloquearPorFecha(hasta.AddDays(1)).ShouldBeNull();

        // Bloqueado otra vez con una fecha posterior: la anterior (ya vencida) no lo levanta.
        vinculo.Apply(vinculo.Bloquear("Otra vez", hasta.AddDays(30), Aprobacion.AddDays(44)));
        vinculo.DesbloquearPorFecha(hasta.AddDays(1)).ShouldBeNull();
        vinculo.DesbloquearPorFecha(hasta.AddDays(30)).ShouldNotBeNull();
    }

    [Fact]
    public void Un_bloqueo_sin_fecha_no_se_levanta_solo_y_un_vinculo_bloqueado_por_vencer_vuelve_por_vencer()
    {
        var vinculo = Activo();
        vinculo.Apply(vinculo.AvisarVigencia(Dia(Hasta.AddDays(-20))).ShouldNotBeNull());
        vinculo.Apply(vinculo.Bloquear("Sin fecha", null, Dia(Hasta.AddDays(-19))));
        vinculo.DesbloquearPorFecha(Dia(Hasta.AddDays(300))).ShouldBeNull();

        vinculo.Apply(vinculo.Desbloquear("Levantada", Dia(Hasta.AddDays(-10))));
        vinculo.Estado.ShouldBe(EstadoVinculo.PorVencer);
    }

    /// <summary>Compara las vigencias por valor (los avisos son listas).</summary>
    private sealed class VigenciaIgual : IEqualityComparer<VigenciaDelVinculo?>
    {
        public bool Equals(VigenciaDelVinculo? x, VigenciaDelVinculo? y) =>
            x is not null && y is not null && x.Dias == y.Dias && x.DiasGracia == y.DiasGracia && x.DiasAviso.SequenceEqual(y.DiasAviso);

        public int GetHashCode(VigenciaDelVinculo? obj) => obj?.Dias ?? 0;
    }
}
