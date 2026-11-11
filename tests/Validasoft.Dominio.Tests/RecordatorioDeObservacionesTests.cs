using Shouldly;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Recordatorio de observaciones pendientes (NT-2, NT-3): solo con el expediente devuelto al tercero, a lo sumo uno por día de
/// calendario del negocio (manual o automático) y, en automático, cada N días desde que se devolvió o desde el último recordatorio.
/// </summary>
public sealed class RecordatorioDeObservacionesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero); // 10:00 en Bogotá
    private readonly Guid _compras = Guid.NewGuid();
    private readonly Guid _rut = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);

    private FlujoParaExpediente Flujo() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "PROV", "Proveedores", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_compras, "COMPRAS", "Compras", 1, false, false, null)])], PoliticaDevolucion.Inmediata, false);

    private Expediente EnRevision()
    {
        var datos = new DatosDeApertura(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.",
            "CO|NIT|900123456", Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);
        var abierto = Expediente.Reproducir(null, Expediente.Abrir(datos, Flujo(), Ahora,
            [new RequisitoCalculado(_rut, Guid.NewGuid(), "RUT", "Registro Único Tributario", "RUT", AmbitoDocumento.Global, true, [], null, null)]));
        return Expediente.Reproducir(abierto, abierto.SincronizarVersiones(
            new Dictionary<Guid, VersionDeRequisito> { [_rut] = new(Guid.NewGuid(), 1, "sha", Ahora) }, Ahora));
    }

    private Expediente Observado(DateTimeOffset en)
    {
        var e = EnRevision();
        return Expediente.Reproducir(e, e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Observado, 1,
            [new MotivoAplicado("ilegible", "No se lee.")], null, en));
    }

    [Fact]
    public void Solo_se_recuerda_con_el_expediente_devuelto_al_tercero()
    {
        var e = EnRevision();
        e.SinRecordatorioPorque(Ahora).ShouldBe(MotivoSinRecordatorio.NoObservado);
        Should.Throw<ReglaDeNegocioException>(() => e.RecordarObservaciones("u1", false, Ahora)).Codigo.ShouldBe("expediente-no-observado");

        var observado = Observado(Ahora);
        observado.SinRecordatorioPorque(Ahora).ShouldBeNull();
        var recordatorio = observado.RecordarObservaciones("u1", false, Ahora.AddDays(2));
        recordatorio.Por.ShouldBe("u1");
        recordatorio.Automatico.ShouldBeFalse();
        recordatorio.DiasObservado.ShouldBe(2);
    }

    [Fact]
    public void A_lo_sumo_uno_por_dia_de_calendario_del_negocio_y_el_siguiente_dia_se_puede_de_nuevo()
    {
        var e = Observado(Ahora);
        e = Expediente.Reproducir(e, [e.RecordarObservaciones("u1", false, Ahora)]);
        e.UltimoRecordatorioDeObservaciones.ShouldBe(Ahora);
        e.RecordatoriosDeObservaciones.ShouldBe(1);

        // Más tarde ese mismo día (hora de Bogotá) no; pasada la medianoche de Bogotá sí, aunque en UTC fueran menos de 24 horas.
        e.SinRecordatorioPorque(Ahora.AddHours(8)).ShouldBe(MotivoSinRecordatorio.YaRecordadoHoy);
        Should.Throw<ReglaDeNegocioException>(() => e.RecordarObservaciones("u1", false, Ahora.AddHours(8))).Codigo.ShouldBe("recordatorio-ya-enviado-hoy");
        e.SinRecordatorioPorque(Ahora.AddHours(14)).ShouldBeNull(); // 00:00 del día siguiente en Bogotá (05:00 UTC)

        e = Expediente.Reproducir(e, [e.RecordarObservaciones("u2", true, Ahora.AddDays(1))]);
        e.RecordatoriosDeObservaciones.ShouldBe(2);
    }

    [Fact]
    public void El_automatico_toca_cada_N_dias_desde_que_se_devolvio_o_desde_el_ultimo_recordatorio()
    {
        var e = Observado(Ahora);
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(2)).ShouldBeFalse();
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(3)).ShouldBeTrue();
        e.TocaRecordatorioAutomatico(0, Ahora.AddDays(30)).ShouldBeFalse(); // apagado

        e = Expediente.Reproducir(e, [e.RecordarObservaciones("recordatorio-automatico", true, Ahora.AddDays(3))]);
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(3)).ShouldBeFalse(); // ya se recordó hoy
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(5)).ShouldBeFalse();
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(6)).ShouldBeTrue();
    }

    [Fact]
    public void Una_devolucion_nueva_reinicia_la_cuenta_del_automatico_aunque_hubiera_recordatorios_de_la_anterior()
    {
        var e = Observado(Ahora);
        e = Expediente.Reproducir(e, [e.RecordarObservaciones("recordatorio-automatico", true, Ahora.AddDays(3))]);

        // El tercero responde (versión nueva y reenvío) y se vuelve a observar días después: cuenta desde la nueva devolución.
        e = Expediente.Reproducir(e, e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = new(Guid.NewGuid(), 2, "sha2", Ahora.AddDays(4)) },
            Ahora.AddDays(4)));
        e = Expediente.Reproducir(e, e.Reenviar(OrigenReenvio.Tercero, "Listo.", Ahora.AddDays(4)));
        e = Expediente.Reproducir(e, e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Observado, 2,
            [new MotivoAplicado("ilegible", "No se lee.")], null, Ahora.AddDays(10)));

        e.SinRecordatorioPorque(Ahora.AddDays(10)).ShouldBeNull();
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(12)).ShouldBeFalse();
        e.TocaRecordatorioAutomatico(3, Ahora.AddDays(13)).ShouldBeTrue();
        e.DiasObservado(Ahora.AddDays(13)).ShouldBe(3);
    }
}
