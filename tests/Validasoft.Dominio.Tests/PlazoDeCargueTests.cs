using Shouldly;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Plazo de cargue del expediente inicial (P-4-2, LT-1…LT-3): corre mientras el expediente está en manos del tercero, recuerda en
/// los días configurados (sin repetir), caduca al vencer y se reabre con un plazo nuevo.
/// </summary>
public sealed class PlazoDeCargueTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
    private readonly Guid _compras = Guid.NewGuid();
    private readonly Guid _rut = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);

    private FlujoParaExpediente Flujo(PlazoDeCargue? plazo) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "PROV", "Proveedores", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_compras, "COMPRAS", "Compras", 1, false, false, null)])], PoliticaDevolucion.Inmediata, false, PlazoCargue: plazo);

    private static DatosDeApertura Datos(TipoExpediente tipo = TipoExpediente.Inicial) =>
        new(Guid.NewGuid(), Guid.NewGuid(), tipo, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456", Guid.NewGuid(),
            "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private RequisitoCalculado Rut(VersionDeRequisito? version = null) =>
        new(_rut, Guid.NewGuid(), "RUT", "Registro Único Tributario", "RUT", AmbitoDocumento.Global, true, [], null, version);

    private Expediente Abrir(PlazoDeCargue? plazo = null, TipoExpediente tipo = TipoExpediente.Inicial) =>
        Expediente.Reproducir(null, Expediente.Abrir(Datos(tipo), Flujo(plazo ?? PlazoDeCargue.PorDefecto), Ahora, [Rut()]));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    [Fact]
    public void El_inicial_abierto_tiene_plazo_de_30_dias_y_su_primera_revision_es_el_recordatorio_de_15()
    {
        var e = Abrir();

        e.Estado.ShouldBe(EstadoExpediente.Abierto);
        e.PlazoCargueHasta.ShouldBe(Ahora.AddDays(30));
        e.ProximaRevisionDePlazo.ShouldBe(Ahora.AddDays(15));
        e.RevisarPlazo(Ahora.AddDays(14)).ShouldBeEmpty();
    }

    [Fact]
    public void Recuerda_una_sola_vez_por_dia_configurado_y_el_que_llega_tarde_cubre_los_anteriores()
    {
        var e = Abrir();

        var a15 = e.RevisarPlazo(Ahora.AddDays(15)).ShouldHaveSingleItem().ShouldBeOfType<RecordatorioDeCargueEnviado>();
        a15.DiasAntes.ShouldBe(15);
        e = Aplicar(e, [a15]);
        e.RevisarPlazo(Ahora.AddDays(16)).ShouldBeEmpty(); // no se repite
        e.ProximaRevisionDePlazo.ShouldBe(Ahora.AddDays(23));

        // El job estuvo detenido: a un día de vencer sale solo el de 1 (cubre el de 7).
        var a1 = e.RevisarPlazo(Ahora.AddDays(29.5)).ShouldHaveSingleItem().ShouldBeOfType<RecordatorioDeCargueEnviado>();
        a1.DiasAntes.ShouldBe(1);
        e = Aplicar(e, [a1]);
        e.RecordatoriosEnviados.Order().ShouldBe([1, 7, 15]);
        e.ProximaRevisionDePlazo.ShouldBe(Ahora.AddDays(30));
    }

    [Fact]
    public void Al_vencer_caduca_y_al_reabrir_vuelve_al_estado_en_que_caduco_con_plazo_nuevo()
    {
        var e = Abrir(new PlazoDeCargue(10, [3]));
        var caducado = e.RevisarPlazo(Ahora.AddDays(10)).ShouldHaveSingleItem().ShouldBeOfType<ExpedienteCaducado>();
        caducado.EstadoPrevio.ShouldBe(EstadoExpediente.Abierto);
        e = Aplicar(e, [caducado]);

        e.Estado.ShouldBe(EstadoExpediente.Caducado);
        e.EstaAbierto.ShouldBeFalse();
        e.ProximaRevisionDePlazo.ShouldBeNull();
        e.RevisarPlazo(Ahora.AddDays(40)).ShouldBeEmpty();

        var reabrirEn = Ahora.AddDays(12);
        var reabierto = e.Reabrir("El proveedor pidió más tiempo.", reabrirEn);
        e = Aplicar(e, [reabierto]);
        e.Estado.ShouldBe(EstadoExpediente.Abierto);
        e.PlazoCargueHasta.ShouldBe(reabrirEn.AddDays(10));
        e.RecordatoriosEnviados.ShouldBeEmpty();
        e.Reaperturas.ShouldBe(1);
        e.CerradoEn.ShouldBeNull();
        e.ProximaRevisionDePlazo.ShouldBe(reabrirEn.AddDays(7));

        Should.Throw<ReglaDeNegocioException>(() => e.Reabrir(null, reabrirEn)).Codigo.ShouldBe("estado-invalido");
    }

    [Fact]
    public void El_plazo_se_detiene_en_revision_y_vuelve_a_correr_completo_al_observarse()
    {
        var e = Abrir(new PlazoDeCargue(10, [5]));
        var cargado = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = new(Guid.NewGuid(), 1, "sha", Ahora) }, Ahora.AddDays(2));
        e = Aplicar(e, cargado);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.PlazoCargueHasta.ShouldBeNull();
        e.RevisarPlazo(Ahora.AddDays(30)).ShouldBeEmpty(); // en revisión no caduca

        var observadoEn = Ahora.AddDays(3);
        e = Aplicar(e, e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Observado, 1,
            [new MotivoAplicado("ilegible", "No se lee.")], null, observadoEn));
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.PlazoCargueHasta.ShouldBe(observadoEn.AddDays(10));

        var caducado = e.RevisarPlazo(observadoEn.AddDays(10)).ShouldHaveSingleItem().ShouldBeOfType<ExpedienteCaducado>();
        caducado.EstadoPrevio.ShouldBe(EstadoExpediente.Observado);
        e = Aplicar(e, [caducado]);
        e = Aplicar(e, [e.Reabrir(null, observadoEn.AddDays(11))]);
        e.Estado.ShouldBe(EstadoExpediente.Observado); // sigue esperando la corrección
    }

    [Fact]
    public void Sin_plazo_o_en_una_renovacion_no_hay_nada_que_vigilar()
    {
        Abrir(new PlazoDeCargue(0, [])).PlazoCargueHasta.ShouldBeNull();
        Abrir(tipo: TipoExpediente.Renovacion).PlazoCargueHasta.ShouldBeNull();
        Abrir(tipo: TipoExpediente.Renovacion).RevisarPlazo(Ahora.AddDays(400)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-1, new int[0], "plazo-cargue-invalido")]
    [InlineData(400, new int[0], "plazo-cargue-invalido")]
    [InlineData(10, new[] { 10 }, "recordatorio-invalido")]
    [InlineData(10, new[] { 0 }, "recordatorio-invalido")]
    [InlineData(30, new[] { 1, 2, 3, 4, 5, 6 }, "recordatorio-invalido")]
    public void El_plazo_del_flujo_se_valida(int dias, int[] recordatorios, string codigo) =>
        Should.Throw<ReglaDeNegocioException>(() => new PlazoDeCargue(dias, recordatorios).Validar()).Codigo.ShouldBe(codigo);

    [Fact]
    public void El_plazo_se_normaliza_y_sin_plazo_no_lleva_recordatorios()
    {
        new PlazoDeCargue(20, [1, 7, 7, 3]).Validar().Recordatorios.ShouldBe([7, 3, 1]);
        new PlazoDeCargue(0, [5]).Validar().Recordatorios.ShouldBeEmpty();
    }

    [Fact]
    public void Caducar_cierra_el_expediente_en_el_vinculo_y_reabrir_lo_vuelve_a_vincular()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, new(), OrigenVinculo.Invitacion, null, "t@x.co", Ahora));
        var expedienteId = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(expedienteId, TipoExpediente.Inicial, "F-1", Ahora));
        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);

        foreach (var evento in vinculo.CerrarExpediente(expedienteId, ResultadoExpediente.Caducado, Ahora.AddDays(30)))
            vinculo.Apply((ExpedienteCerrado)evento);
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        vinculo.ExpedienteAbiertoId.ShouldBeNull();
        vinculo.UltimoResultado.ShouldBe(ResultadoExpediente.Caducado);

        vinculo.Apply(vinculo.VincularExpediente(expedienteId, TipoExpediente.Inicial, "F-1", Ahora.AddDays(31)));
        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        vinculo.ExpedienteAbiertoId.ShouldBe(expedienteId);
    }
}
