using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>Tiempos del expediente para el informe de estado (IF-2): por fase y por área, con rondas y SLA.</summary>
public class TiemposDeExpedienteTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 2, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly MotivoAplicado Incompleto = new("incompleto", "La información está incompleta.");

    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private FlujoParaExpediente Flujo(int? slaB = null, params int[] etapas) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto,
            etapas.Length == 0
                ? [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "A", "Área A", 1, false, false, 24), new AreaDelFlujo(_b, "B", "Área B", 1, false, false, slaB)])]
                : [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "A", "Área A", 1, false, false, null)]), new EtapaDelFlujo(2, [new AreaDelFlujo(_b, "B", "Área B", 1, false, false, slaB)])],
            PoliticaDevolucion.Inmediata, false);

    /// <summary>Aplica la decisión y la agrega a la historia.</summary>
    private static Expediente Paso(List<object> historia, Expediente e, IReadOnlyList<object> eventos)
    {
        historia.AddRange(eventos);
        return Expediente.Reproducir(e, eventos);
    }

    [Fact]
    public void Areas_en_paralelo_cada_una_cuenta_su_tiempo_y_el_total_es_de_la_apertura_a_la_aprobacion()
    {
        var historia = new List<object>(Expediente.Abrir(Datos(), Flujo(), Inicio));
        var e = Expediente.Reproducir(null, historia);
        e = Paso(historia, e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Inicio.AddHours(10)));
        e = Paso(historia, e, e.EmitirVeredicto(_b, _beto, Veredicto.Aprobado, [], null, Inicio.AddHours(30)));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);

        var t = Expediente.MedirTiempos(historia, Inicio.AddDays(10));
        t.HorasTotales.ShouldBe(30);
        t.HorasHastaAprobacion.ShouldBe(30);
        t.HorasEnRevision.ShouldBe(30);
        t.HorasConTercero.ShouldBe(0);
        t.HorasEnCarga.ShouldBe(0);
        t.Devoluciones.ShouldBe(0);
        t.Areas.Select(a => (a.Codigo, a.Horas, a.Rondas, a.Estado)).ShouldBe(
            [("A", 10d, 1, EstadoAreaExpediente.Aprobada), ("B", 30d, 1, EstadoAreaExpediente.Aprobada)]);
        t.Areas.ShouldAllBe(a => !a.SlaIncumplido);
    }

    [Fact]
    public void La_devolucion_cuenta_como_tiempo_con_el_tercero_y_el_reenvio_abre_otra_ronda_del_area_que_observo()
    {
        var historia = new List<object>(Expediente.Abrir(Datos(), Flujo(slaB: 8), Inicio));
        var e = Expediente.Reproducir(null, historia);
        e = Paso(historia, e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Inicio.AddHours(2)));
        // B observa a las 12 h (pasó su SLA de 8 h): se devuelve al tercero, que responde 48 h después.
        e = Paso(historia, e, e.EmitirVeredicto(_b, _beto, Veredicto.Observado, [Incompleto], "Falta.", Inicio.AddHours(12)));
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e = Paso(historia, e, e.Reenviar(OrigenReenvio.Tercero, "Listo", Inicio.AddHours(60)));
        e = Paso(historia, e, e.EmitirVeredicto(_b, _beto, Veredicto.Aprobado, [], null, Inicio.AddHours(64)));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);

        var t = Expediente.MedirTiempos(historia, Inicio.AddDays(10));
        t.HorasTotales.ShouldBe(64);
        t.HorasConTercero.ShouldBe(48);
        t.HorasEnRevision.ShouldBe(16);
        t.Devoluciones.ShouldBe(1);
        var b = t.Areas.Single(a => a.Codigo == "B");
        b.Horas.ShouldBe(16); // 12 h de la primera ronda + 4 h de la segunda
        b.Rondas.ShouldBe(2);
        b.SlaIncumplido.ShouldBeTrue();
        t.Areas.Single(a => a.Codigo == "A").Rondas.ShouldBe(1);
    }

    [Fact]
    public void Abierto_cuenta_hasta_ahora_y_las_etapas_en_secuencia_no_suman_tiempo_al_area_que_aun_espera()
    {
        var historia = new List<object>(Expediente.Abrir(Datos(), Flujo(null, 1, 2), Inicio));
        var e = Expediente.Reproducir(null, historia);
        e = Paso(historia, e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Inicio.AddHours(5)));

        var t = Expediente.MedirTiempos(historia, Inicio.AddHours(20));
        t.HorasTotales.ShouldBe(20);
        t.HorasHastaAprobacion.ShouldBeNull();
        t.Areas.Select(a => (a.Codigo, a.Etapa, a.Horas, a.Estado)).ShouldBe(
            [("A", 1, 5d, EstadoAreaExpediente.Aprobada), ("B", 2, 15d, EstadoAreaExpediente.Pendiente)]);
    }

    [Fact]
    public void Eventos_ajenos_al_expediente_se_ignoran_y_sin_apertura_no_hay_tiempos()
    {
        Expediente.MedirTiempos([new object()], Inicio).HorasTotales.ShouldBe(0);
        var historia = new List<object>(Expediente.Abrir(Datos(), Flujo(), Inicio)) { "otro evento" };
        Expediente.MedirTiempos(historia, Inicio.AddHours(3)).HorasEnRevision.ShouldBe(3);
    }
}
