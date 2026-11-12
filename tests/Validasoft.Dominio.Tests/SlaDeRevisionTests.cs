using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>SLA de revisión (OP-9…OP-14): el plazo corre por ronda, avisa al acercarse y al vencer una sola vez, se reinicia al volver a llegarle algo al área.</summary>
public sealed class SlaDeRevisionTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly MotivoAplicado Incompleto = new("incompleto", "La información está incompleta.");

    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    /// <summary>A con plazo de 10 h; B sin plazo.</summary>
    private FlujoParaExpediente Flujo() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "A", "Área A", 1, false, false, 10), new AreaDelFlujo(_b, "B", "Área B", 1, false, false, null)])],
            PoliticaDevolucion.Inmediata, false);

    private Expediente EnRevision() => Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(), Inicio));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    [Fact]
    public void Corre_desde_que_el_area_recibe_el_expediente_y_vence_al_cumplirse_su_plazo()
    {
        var e = EnRevision();
        var a = e.BuscarArea(_a)!;
        a.PendienteDesde.ShouldBe(Inicio);
        a.SlaVence.ShouldBe(Inicio.AddHours(10));
        e.BuscarArea(_b)!.SlaVence.ShouldBeNull();

        Expediente.EstadoDelSla(e, a, Inicio.AddHours(1), 80).ShouldBe(EstadoDeSla.EnTiempo);
        Expediente.EstadoDelSla(e, a, Inicio.AddHours(8), 80).ShouldBe(EstadoDeSla.PorVencer);
        Expediente.EstadoDelSla(e, a, Inicio.AddHours(10), 80).ShouldBe(EstadoDeSla.Vencido);
        Expediente.EstadoDelSla(e, e.BuscarArea(_b)!, Inicio.AddHours(500), 80).ShouldBeNull();
    }

    [Fact]
    public void Avisa_al_acercarse_y_al_vencer_una_sola_vez_por_ronda()
    {
        var e = EnRevision();
        e.RevisarSla(Inicio.AddHours(7), 80).ShouldBeEmpty();

        var aviso = e.RevisarSla(Inicio.AddHours(8), 80);
        aviso.ShouldHaveSingleItem().ShouldBeOfType<SlaPorVencer>().ShouldSatisfyAllConditions(
            x => x.AreaId.ShouldBe(_a), x => x.Vence.ShouldBe(Inicio.AddHours(10)), x => x.PorcentajeConsumido.ShouldBe(80));
        e = Aplicar(e, aviso);
        e.RevisarSla(Inicio.AddHours(9), 80).ShouldBeEmpty();

        var vencido = e.RevisarSla(Inicio.AddHours(11), 80);
        vencido.ShouldHaveSingleItem().ShouldBeOfType<SlaIncumplido>().AreaId.ShouldBe(_a);
        e = Aplicar(e, vencido);
        e.RevisarSla(Inicio.AddHours(40), 80).ShouldBeEmpty();
        e.BuscarArea(_a)!.SlaIncumplidoEn.ShouldNotBeNull();
    }

    [Fact]
    public void Un_barrido_tardio_registra_solo_el_vencimiento_y_el_umbral_depende_de_la_empresa()
    {
        var e = EnRevision();
        e.RevisarSla(Inicio.AddHours(20), 80).ShouldHaveSingleItem().ShouldBeOfType<SlaIncumplido>();
        // Con 95 % el aviso previo sale a las 9 h 30 min.
        e.RevisarSla(Inicio.AddHours(9), 95).ShouldBeEmpty();
        e.RevisarSla(Inicio.AddHours(9.6), 95).ShouldHaveSingleItem().ShouldBeOfType<SlaPorVencer>();
    }

    [Fact]
    public void Devuelto_al_tercero_el_plazo_no_corre_y_al_reenviar_empieza_una_ronda_nueva_con_su_plazo_completo()
    {
        var e = EnRevision();
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Observado, [Incompleto], "Falta.", Inicio.AddHours(2)));
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.RevisarSla(Inicio.AddHours(100), 80).ShouldBeEmpty();
        e.ProximaRevisionDeSla.ShouldBeNull();

        e = Aplicar(e, e.Reenviar(OrigenReenvio.Tercero, "Listo", Inicio.AddHours(30)));
        var a = e.BuscarArea(_a)!;
        a.PendienteDesde.ShouldBe(Inicio.AddHours(30));
        a.SlaVence.ShouldBe(Inicio.AddHours(40));
        a.SlaAvisadoEn.ShouldBeNull();
        a.SlaIncumplidoEn.ShouldBeNull();
    }

    [Fact]
    public void El_indice_de_barrido_apunta_al_primer_momento_con_algo_por_registrar_y_se_apaga_al_decidir_el_area()
    {
        var e = EnRevision();
        // Umbral más bajo (50 %): 5 h después de recibirlo.
        e.ProximaRevisionDeSla.ShouldBe(Inicio.AddHours(5));

        e = Aplicar(e, e.RevisarSla(Inicio.AddHours(6), 80)); // aún no cruzó el 80 %: nada
        e = Aplicar(e, e.RevisarSla(Inicio.AddHours(8), 80)); // aviso
        e.ProximaRevisionDeSla.ShouldBe(Inicio.AddHours(10));
        e = Aplicar(e, e.RevisarSla(Inicio.AddHours(11), 80));
        e.ProximaRevisionDeSla.ShouldBeNull();

        var otro = EnRevision();
        otro = Aplicar(otro, otro.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Inicio.AddHours(1)));
        otro.ProximaRevisionDeSla.ShouldBeNull();
    }

    [Fact]
    public void Medir_tiempos_cuenta_las_rondas_con_plazo_y_las_que_pasaron_de_el()
    {
        var historia = new List<object>(Expediente.Abrir(Datos(), Flujo(), Inicio));
        var e = Expediente.Reproducir(null, historia);
        IReadOnlyList<object> Paso(IReadOnlyList<object> eventos)
        {
            historia.AddRange(eventos);
            e = Expediente.Reproducir(e, eventos);
            return eventos;
        }

        Paso(e.EmitirVeredicto(_a, _ana, Veredicto.Observado, [Incompleto], "Falta.", Inicio.AddHours(12))); // ronda 1: 12 h > 10 h
        Paso(e.Reenviar(OrigenReenvio.Tercero, "Listo", Inicio.AddHours(20)));
        Paso(e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Inicio.AddHours(24))); // ronda 2: 4 h

        var a = Expediente.MedirTiempos(historia, Inicio.AddDays(5)).Areas.Single(x => x.Codigo == "A");
        a.Rondas.ShouldBe(2);
        a.RondasConSla.ShouldBe(2);
        a.RondasFueraDeSla.ShouldBe(1);
        var b = Expediente.MedirTiempos(historia, Inicio.AddDays(5)).Areas.Single(x => x.Codigo == "B");
        b.RondasConSla.ShouldBe(0);
    }

    [Theory]
    [InlineData(49, true)]
    [InlineData(50, false)]
    [InlineData(95, false)]
    [InlineData(96, true)]
    public void El_porcentaje_de_aviso_va_de_50_a_95(int porcentaje, bool invalido)
    {
        var accion = () => ConfiguracionDeSla.Validar(porcentaje, []);
        if (invalido)
            Should.Throw<ReglaDeNegocioException>(accion).Codigo.ShouldBe("porcentaje-invalido");
        else
            accion();
    }

    [Fact]
    public void El_plazo_por_defecto_de_un_area_va_de_una_hora_a_noventa_dias()
    {
        Should.Throw<ReglaDeNegocioException>(() => ConfiguracionDeSla.Validar(80, [new SlaDeAreaDto(Guid.NewGuid(), 0)])).Codigo.ShouldBe("sla-invalido");
        Should.Throw<ReglaDeNegocioException>(() => ConfiguracionDeSla.Validar(80, [new SlaDeAreaDto(Guid.NewGuid(), 2161)])).Codigo.ShouldBe("sla-invalido");
        ConfiguracionDeSla.Validar(80, [new SlaDeAreaDto(Guid.NewGuid(), 1), new SlaDeAreaDto(Guid.NewGuid(), null), new SlaDeAreaDto(Guid.NewGuid(), 2160)]);
    }

    [Theory]
    [InlineData(null, false, 3, 0)]
    [InlineData(null, true, 3, 3)]
    [InlineData(0, true, 3, 0)]
    [InlineData(7, false, 3, 7)]
    [InlineData(7, true, 3, 7)]
    public void El_recordatorio_automatico_del_flujo_manda_sobre_el_de_la_empresa(int? flujo, bool empresaActiva, int empresaDias, int esperado)
    {
        var e = Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo() with { RecordatorioObservacionesDias = flujo }, Inicio));
        e.RecordatorioObservacionesDias.ShouldBe(flujo);
        e.RecordatorioAutomaticoEfectivo(empresaActiva, empresaDias).ShouldBe(esperado);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    [InlineData(-1, false)]
    public void El_recordatorio_del_flujo_es_nulo_cero_o_de_1_a_60_dias(int? dias, bool valido)
    {
        var accion = () => DefinicionDeFlujo.ValidarRecordatorio(dias);
        if (valido)
            accion().ShouldBe(dias);
        else
            Should.Throw<ReglaDeNegocioException>(() => accion()).Codigo.ShouldBe("dias-invalidos");
    }
}
