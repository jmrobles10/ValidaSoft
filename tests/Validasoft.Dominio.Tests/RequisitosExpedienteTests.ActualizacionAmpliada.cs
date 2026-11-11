using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Ampliación de la actualización abierta (FM-48): con un expediente de <c>Actualizacion</c> ya abierto, lo que cambia después
/// en otro requisito (otra sección o documento) entra al mismo expediente (<see cref="RequisitosAgregados"/>) y las áreas que lo
/// revisan vuelven a quedar pendientes. Mismo escenario: RUT (ambas áreas) y la sección general.
/// </summary>
public partial class RequisitosExpedienteTests
{
    private readonly Guid _general = Guid.NewGuid();
    private readonly Guid _docGeneral = Guid.NewGuid();

    private RequisitoCalculado General(VersionDeRequisito? version, params Guid[] areas) =>
        new(_general, Guid.Empty, "FICHA_GENERAL", "Información general", "FICHA_GENERAL", AmbitoDocumento.Global, true, areas, null, version,
            TipoRequisito: TipoDeRequisito.Seccion, Seccion: "FICHA_GENERAL", Aceptada: V(_docGeneral, 1));

    private static DatosDeApertura DatosDeActualizacion() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Actualizacion, "NORTE-E-2026-000002", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    /// <summary>Actualización que se abrió por el RUT v2 (lo revisan las dos áreas).</summary>
    private Expediente ActualizacionPorElRut(FlujoParaExpediente flujo, out List<object> historia)
    {
        historia = [.. Expediente.Abrir(DatosDeActualizacion(), flujo, Ahora, [Rut(V(_docRut, 2))])];
        return Expediente.Reproducir(null, historia);
    }

    [Fact]
    public void Con_una_actualizacion_abierta_el_requisito_nuevo_entra_al_mismo_expediente_y_el_area_que_ya_habia_decidido_vuelve_a_pendiente()
    {
        var e = ActualizacionPorElRut(Flujo(), out _);
        e = Aprobar(e, _rut, _compras, _ana, version: 2); // Compras ya decidió lo único que tenía
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        // La sección general cambió después de abrirse: la revisa Compras.
        var eventos = e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(2));

        var agregados = eventos.OfType<RequisitosAgregados>().ShouldHaveSingleItem();
        agregados.Requisitos.Single().RequisitoId.ShouldBe(_general);
        e = Aplicar(e, eventos);
        e.Requisitos.Select(r => r.Id).ShouldBe([_rut, _general]);
        e.BuscarRequisito(_general)!.ShouldSatisfyAllConditions(
            r => r.EsSeccion.ShouldBeTrue(),
            r => r.VersionActual!.Numero.ShouldBe(2),
            r => r.VersionAceptadaPrevia!.Numero.ShouldBe(1),
            r => r.Estado.ShouldBe(EstadoRequisito.EnRevision));
        var compras = e.BuscarArea(_compras)!;
        (compras.Estado, compras.RequisitosPorDecidir).ShouldBe((EstadoAreaExpediente.Pendiente, 1));
        compras.PendienteDesde.ShouldBe(Ahora.AddHours(2));
        compras.Veredictos.Where(v => v.Derivado).ShouldAllBe(v => !v.Vigente); // lo derivado deja de valer: ya no está aprobada
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.Tipo.ShouldBe(TipoExpediente.Actualizacion);
        // El RUT que Compras ya aprobó sigue aprobado: solo falta lo nuevo.
        e.BuscarRequisito(_rut)!.Veredictos.ShouldContain(v => v.Vigente && v.AreaId == _compras);

        // Al decidir la general, Compras queda aprobada otra vez y Cumplimiento sigue con lo suyo (el RUT).
        e = Aprobar(e, _general, _compras, _ana, version: 2);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
    }

    [Fact]
    public void Un_mensaje_repetido_no_agrega_dos_veces_el_mismo_requisito()
    {
        var e = ActualizacionPorElRut(Flujo(), out _);
        var nuevo = General(V(_docGeneral, 2), _cumplimiento);

        e = Aplicar(e, e.AgregarRequisitos([nuevo], Ahora.AddHours(1)));
        e.Requisitos.Count.ShouldBe(2);

        e.AgregarRequisitos([nuevo], Ahora.AddHours(2)).ShouldBeEmpty();
        e.AgregarRequisitos([nuevo, nuevo with { RequisitoId = _general }], Ahora.AddHours(2)).ShouldBeEmpty();
        e.Requisitos.Count.ShouldBe(2);
    }

    [Fact]
    public void Un_requisito_sin_areas_indicadas_lo_revisan_todas_las_del_flujo()
    {
        var e = ActualizacionPorElRut(Flujo(), out _);

        e = Aplicar(e, e.AgregarRequisitos([General(V(_docGeneral, 2))], Ahora.AddHours(1)));

        e.BuscarRequisito(_general)!.AreasQueRevisan.ShouldBe([_compras, _cumplimiento], ignoreOrder: true);
    }

    [Fact]
    public void Solo_se_amplian_las_actualizaciones_abiertas_un_expediente_inicial_o_cerrado_no_cambia()
    {
        var inicial = EnRevision();
        inicial.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(1)).ShouldBeEmpty();

        var renovacion = Expediente.Reproducir(null,
            Expediente.Abrir(DatosDeActualizacion() with { Tipo = TipoExpediente.Renovacion }, Flujo(), Ahora, [Rut(V(_docRut, 2))]));
        renovacion.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(1)).ShouldBeEmpty();

        var cerrada = ActualizacionPorElRut(Flujo(), out _);
        cerrada = Aprobar(Aprobar(cerrada, _rut, _compras, _ana, 2), _rut, _cumplimiento, _beto, 2);
        cerrada.Estado.ShouldBe(EstadoExpediente.Aprobado);
        cerrada.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(3)).ShouldBeEmpty();
    }

    [Fact]
    public void Si_la_etapa_del_area_ya_estaba_aprobada_se_reabre_y_las_posteriores_esperan_a_que_se_apruebe_de_nuevo()
    {
        // Compras revisa primero y Cumplimiento después; el RUT lo revisan las dos.
        var e = ActualizacionPorElRut(Flujo(enSecuencia: true), out _);
        e = Aprobar(e, _rut, _compras, _ana, version: 2);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);

        var eventos = e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(3));
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EtapaActiva!.Orden.ShouldBe(1);
        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Activa, EstadoEtapa.Esperando]);
        e.Etapas[0].AprobadaEn.ShouldBeNull();
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);

        // Compras decide lo nuevo: la etapa 1 se aprueba de nuevo y Cumplimiento recibe el expediente.
        e = Aprobar(e, _general, _compras, _ana, version: 2);
        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Aprobada, EstadoEtapa.Activa]);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarRequisito(_rut)!.Veredictos.ShouldContain(v => v.Vigente && v.AreaId == _compras);
    }

    [Fact]
    public void Si_la_actualizacion_estaba_devuelta_al_tercero_y_se_reabre_una_etapa_anterior_la_revision_se_reanuda()
    {
        var e = ActualizacionPorElRut(Flujo(enSecuencia: true), out _);
        e = Aprobar(e, _rut, _compras, _ana, version: 2);
        e = Observar(e, _rut, _cumplimiento, _beto, version: 2);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var eventos = e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(4));

        eventos.Select(x => x.GetType().Name).ShouldBe(["RequisitosAgregados", "RevisionReanudada"]);
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EtapaActiva!.Orden.ShouldBe(1);
        e.RequisitosObservados.ShouldBeEmpty();
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
    }

    [Fact]
    public void Si_la_actualizacion_esta_devuelta_y_la_etapa_del_requisito_sigue_abierta_continua_devuelta()
    {
        // En paralelo: Cumplimiento observa el RUT; el requisito nuevo lo revisa Compras, que aún está pendiente.
        var e = ActualizacionPorElRut(Flujo(), out _);
        e = Observar(e, _rut, _cumplimiento, _beto, version: 2);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var eventos = e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(4));

        eventos.Select(x => x.GetType().Name).ShouldBe(["RequisitosAgregados"]);
        Aplicar(e, eventos).Estado.ShouldBe(EstadoExpediente.Observado);
    }

    [Fact]
    public void El_area_automatica_aprueba_sola_lo_que_se_le_agrega()
    {
        var e = ActualizacionPorElRut(Flujo(comprasAutomatica: true), out _);

        e = Aplicar(e, e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(1)));

        e.BuscarRequisito(_general)!.Veredictos.Single().ShouldSatisfyAllConditions(
            v => v.AreaId.ShouldBe(_compras), v => v.Origen.ShouldBe(OrigenVeredicto.Sistema), v => v.Vigente.ShouldBeTrue());
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
    }

    [Fact]
    public void Los_tiempos_cuentan_otra_ronda_del_area_que_vuelve_a_pendiente_y_no_pierden_el_evento()
    {
        var historia = new List<object>(Expediente.Abrir(DatosDeActualizacion(), Flujo(), Ahora, [Rut(V(_docRut, 2))]));
        var e = Expediente.Reproducir(null, historia);
        var aprobacion = e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 2, [], null, Ahora.AddHours(2));
        historia.AddRange(aprobacion);
        e = Aplicar(e, aprobacion);
        var ampliacion = e.AgregarRequisitos([General(V(_docGeneral, 2), _compras)], Ahora.AddHours(5));
        historia.AddRange(ampliacion);
        e = Aplicar(e, ampliacion);
        var cierre = e.EmitirVeredictoDeRequisito(_general, _compras, _ana, Veredicto.Aprobado, 2, [], null, Ahora.AddHours(9));
        historia.AddRange(cierre);

        var tiempos = Expediente.MedirTiempos(historia, Ahora.AddDays(1));

        var compras = tiempos.Areas.Single(a => a.Codigo == "COMPRAS");
        (compras.Rondas, compras.Horas).ShouldBe((2, 2d + 4d)); // abierta 0–2 h y de nuevo 5–9 h
        tiempos.Areas.Single(a => a.Codigo == "CUMPLIMIENTO").Rondas.ShouldBe(1);
    }
}
