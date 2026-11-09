using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Tabla de casos del expediente con requisitos (RQ-1…RQ-8, docs/03 §6): cada área aprueba u observa documentos
/// concretos de su alcance y su aprobación se deriva de ellos. Escenario base: Compras y Cumplimiento en paralelo; RUT
/// (ambas) y Cámara de comercio (solo Cumplimiento).
/// </summary>
public partial class RequisitosExpedienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _compras = Guid.NewGuid();
    private readonly Guid _cumplimiento = Guid.NewGuid();
    private readonly Guid _rut = Guid.NewGuid();
    private readonly Guid _camara = Guid.NewGuid();
    private readonly Guid _docRut = Guid.NewGuid();
    private readonly Guid _docCamara = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);
    private readonly Revisor _carla = new(Guid.NewGuid(), null);

    private static readonly MotivoAplicado Ilegible = new("ilegible", "El documento no se puede leer.");

    private static VersionDeRequisito V(Guid documento, int n) => new(documento, n, $"sha-{documento:N}-{n}", Ahora);

    private RequisitoCalculado Rut(VersionDeRequisito? version = null, bool obligatorio = true, int? aprobadores = null) =>
        new(_rut, Guid.NewGuid(), "RUT", "Registro Único Tributario", "RUT", AmbitoDocumento.Global, obligatorio, [], aprobadores, version);

    private RequisitoCalculado Camara(VersionDeRequisito? version = null, bool obligatorio = true) =>
        new(_camara, Guid.NewGuid(), "CAMARA_COMERCIO", "Cámara de comercio", "CAMARA_COMERCIO", AmbitoDocumento.Global, obligatorio,
            [_cumplimiento], null, version);

    private FlujoParaExpediente Flujo(PoliticaDevolucion politica = PoliticaDevolucion.Inmediata, bool comprasAutomatica = false,
        bool enSecuencia = false, int aprobadoresCumplimiento = 1)
    {
        var compras = new AreaDelFlujo(_compras, "COMPRAS", "Compras", 1, false, comprasAutomatica, null);
        var cumplimiento = new AreaDelFlujo(_cumplimiento, "CUMPLIMIENTO", "Cumplimiento", aprobadoresCumplimiento, true, false, null);
        IReadOnlyList<EtapaDelFlujo> etapas = enSecuencia
            ? [new EtapaDelFlujo(1, [compras]), new EtapaDelFlujo(2, [cumplimiento])]
            : [new EtapaDelFlujo(1, [compras, cumplimiento])];
        return new FlujoParaExpediente(Guid.NewGuid(), Guid.NewGuid(), "PROV", "Proveedores", 1, OrigenFlujo.PorDefecto, etapas, politica, false);
    }

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private static Expediente Abrir(FlujoParaExpediente flujo, out IReadOnlyList<object> eventos, params RequisitoCalculado[] requisitos)
    {
        eventos = Expediente.Abrir(Datos(), flujo, Ahora, requisitos);
        return Expediente.Reproducir(null, eventos);
    }

    private static Expediente Abrir(FlujoParaExpediente flujo, params RequisitoCalculado[] requisitos) => Abrir(flujo, out _, requisitos);

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    /// <summary>Expediente en revisión con RUT v1 y Cámara v1 ya compartidos.</summary>
    private Expediente EnRevision(PoliticaDevolucion politica = PoliticaDevolucion.Inmediata) =>
        Abrir(Flujo(politica), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));

    private static Expediente Aprobar(Expediente e, Guid requisito, Guid area, Revisor revisor, int version = 1) =>
        Aplicar(e, e.EmitirVeredictoDeRequisito(requisito, area, revisor, Veredicto.Aprobado, version, [], null, Ahora));

    private static Expediente Observar(Expediente e, Guid requisito, Guid area, Revisor revisor, int version = 1) =>
        Aplicar(e, e.EmitirVeredictoDeRequisito(requisito, area, revisor, Veredicto.Observado, version, [Ilegible], "Escanéalo de nuevo.", Ahora));

    private Expediente Adjuntar(Expediente e, Guid requisito, VersionDeRequisito version) =>
        Aplicar(e, e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [requisito] = version }, Ahora));

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- apertura y fase de carga ----------

    [Fact]
    public void Lo_que_la_empresa_ya_aprobo_en_esa_misma_version_se_aprueba_solo_y_lo_adicional_sigue_pendiente()
    {
        // RQ-11: la empresa ya aprobó el RUT v1 como proveedor; ahora es cliente y se le pide además la Cámara.
        var previa = new AprobacionPrevia(Guid.NewGuid(), "NORTE-E-2026-000007", "Proveedor", _docRut, 1);
        var e = Abrir(Flujo(), out var eventos, Rut(V(_docRut, 1)) with { Previa = previa }, Camara(V(_docCamara, 1)));

        var reutilizados = eventos.OfType<VeredictoDeRequisitoEmitido>().Where(v => v.RequisitoId == _rut).ToList();
        reutilizados.Select(v => v.AreaId).ShouldBe([_compras, _cumplimiento], ignoreOrder: true);
        reutilizados.ShouldAllBe(v => v.Origen == OrigenVeredicto.Sistema && v.Veredicto == Veredicto.Aprobado && v.SobreVersion == 1);
        reutilizados[0].Justificacion.ShouldBe("La empresa ya aprobó esta versión en el expediente NORTE-E-2026-000007 (Proveedor).");
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Aprobado);
        // Compras solo revisaba el RUT: queda aprobada; la Cámara es nueva y la decide Cumplimiento.
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.EnRevision);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        // Al aprobar Cumplimiento la Cámara (el RUT ya lo tiene), el expediente se aprueba.
        Aprobar(e, _camara, _cumplimiento, _ana).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Si_todo_lo_que_pide_ya_lo_aprobo_la_empresa_el_expediente_queda_aprobado_al_abrir()
    {
        var previa = new AprobacionPrevia(Guid.NewGuid(), "NORTE-E-2026-000007", "Proveedor", _docRut, 1);
        var e = Abrir(Flujo(), Rut(V(_docRut, 1)) with { Previa = previa });

        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Una_aprobacion_previa_de_otra_version_no_cuenta_y_una_version_nueva_la_anula()
    {
        // La empresa aprobó la v1; el tercero ya tiene la v2: se revisa.
        var previa = new AprobacionPrevia(Guid.NewGuid(), "NORTE-E-2026-000007", "Proveedor", _docRut, 1);
        var otra = Abrir(Flujo(), Rut(V(_docRut, 2)) with { Previa = previa }, Camara(V(_docCamara, 1)));
        otra.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.EnRevision);

        // Abierto con la v1 aprobada antes, el tercero sube la v2: la aprobación heredada deja de valer y se revisa la v2.
        var e = Abrir(Flujo(), Rut(V(_docRut, 1)) with { Previa = previa }, Camara(V(_docCamara, 1)));
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Aprobado);
        var conV2 = Adjuntar(e, _rut, V(_docRut, 2));
        conV2.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.EnRevision);
        conV2.BuscarArea(_compras)!.Estado.ShouldNotBe(EstadoAreaExpediente.Aprobada);
    }

    [Fact]
    public void Un_requisito_nace_cargado_si_ya_esta_compartido_y_pendiente_si_no_y_el_expediente_espera_en_carga()
    {
        var e = Abrir(Flujo(), out var eventos, Rut(V(_docRut, 1)), Camara());

        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteAbierto", "FlujoSeleccionado", "RequisitosCalculados"]);
        e.Estado.ShouldBe(EstadoExpediente.Abierto);
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Cargado);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.Pendiente);
        // Vacío en el flujo ⇒ lo revisan todas las áreas; la Cámara, solo Cumplimiento.
        e.BuscarRequisito(_rut)!.AreasQueRevisan.ShouldBe([_compras, _cumplimiento]);
        e.BuscarRequisito(_camara)!.AreasQueRevisan.ShouldBe([_cumplimiento]);
        e.Etapas.Single().Areas.ShouldAllBe(a => a.Estado == EstadoAreaExpediente.Esperando);
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Con_todos_los_obligatorios_cargados_pasa_a_revision_al_abrir()
    {
        var e = Abrir(Flujo(), out var eventos, Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));

        eventos.Select(x => x.GetType().Name).ShouldBe(
            ["ExpedienteAbierto", "FlujoSeleccionado", "RequisitosCalculados", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.Requisitos.ShouldAllBe(r => r.Estado == EstadoRequisito.EnRevision);
        e.BuscarArea(_compras)!.RequisitosPorDecidir.ShouldBe(1);
        e.BuscarArea(_cumplimiento)!.RequisitosPorDecidir.ShouldBe(2);
    }

    [Fact]
    public void Al_adjuntarse_el_ultimo_obligatorio_pasa_solo_a_revision_y_un_opcional_sin_cargar_no_frena()
    {
        var e = Abrir(Flujo(), Rut(), Camara(obligatorio: false));
        e.Estado.ShouldBe(EstadoExpediente.Abierto);

        var eventos = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 1) }, Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VersionAdjuntadaARequisito", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.Pendiente);
        e.BuscarArea(_cumplimiento)!.RequisitosPorDecidir.ShouldBe(1); // el opcional sin cargar no cuenta

        e = Aprobar(e, _rut, _compras, _ana);
        e = Aprobar(e, _rut, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    // ---------- veredictos por requisito ----------

    [Fact]
    public void El_veredicto_es_de_un_area_del_alcance_sobre_la_version_vigente()
    {
        var e = EnRevision();

        Codigo(() => e.EmitirVeredictoDeRequisito(_camara, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("requisito-no-en-alcance");
        Codigo(() => e.EmitirVeredictoDeRequisito(Guid.NewGuid(), _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora))
            .ShouldBe("requisito-no-encontrado");
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, null, [], null, Ahora)).ShouldBe("sobre-version-requerida");
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 2, [], null, Ahora)).ShouldBe("version-superada");
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Observado, 1, [], null, Ahora)).ShouldBe("motivo-requerido");
        // Con requisitos, la decisión del área pide las versiones de sus documentos o los documentos que observa (DA-1).
        Codigo(() => e.EmitirVeredicto(_compras, _ana, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("versiones-requeridas");
        Codigo(() => e.EmitirVeredicto(_compras, _ana, Veredicto.Observado, [Ilegible], null, Ahora)).ShouldBe("requisitos-requeridos");

        e = Aprobar(e, _rut, _compras, _ana);
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _beto, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("requisito-ya-decidido");
        var veredicto = e.BuscarRequisito(_rut)!.Veredictos.ShouldHaveSingleItem();
        veredicto.AreaId.ShouldBe(_compras);
        veredicto.SobreVersion.ShouldBe(1);
        veredicto.RequisitoId.ShouldBe(_rut);
    }

    [Fact]
    public void Un_area_queda_aprobada_cuando_aprueba_todo_su_alcance_y_el_sistema_emite_su_veredicto_derivado()
    {
        var e = EnRevision();

        var eventos = e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VeredictoDeRequisitoEmitido", "VeredictoDeAreaEmitido"]);
        var derivado = (VeredictoDeAreaEmitido)eventos[1];
        derivado.DerivadoDeRequisitos.ShouldBeTrue();
        derivado.Origen.ShouldBe(OrigenVeredicto.Humano);
        derivado.RevisorId.ShouldBeNull();
        derivado.RequisitoId.ShouldBeNull();
        e = Aplicar(e, eventos);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.EnRevision); // falta Cumplimiento

        e = Aprobar(e, _rut, _cumplimiento, _carla);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente); // le falta la Cámara
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Aprobado);

        eventos = e.EmitirVeredictoDeRequisito(_camara, _cumplimiento, _carla, Veredicto.Aprobado, 1, [], null, Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(
            ["VeredictoDeRequisitoEmitido", "VeredictoDeAreaEmitido", "EtapaAprobada", "ExpedienteAprobado"]);
        Aplicar(e, eventos).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Un_requisito_puede_pedir_varios_aprobadores_distintos_por_area()
    {
        var e = Abrir(Flujo(), Rut(V(_docRut, 1), aprobadores: 2), Camara(V(_docCamara, 1)));

        e = Aprobar(e, _rut, _compras, _ana);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarRequisito(_rut)!.AprobacionesDe(e.BuscarArea(_compras)!).ShouldBe(1);
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("aprobador-repetido");

        e = Aprobar(e, _rut, _compras, _beto);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
    }

    // ---------- observaciones, versión nueva y reenvío ----------

    [Fact]
    public void Una_observacion_de_un_requisito_devuelve_el_expediente_con_politica_inmediata()
    {
        var e = EnRevision();
        e = Aprobar(e, _rut, _compras, _ana);

        var eventos = e.EmitirVeredictoDeRequisito(_camara, _cumplimiento, _carla, Veredicto.Observado, 1, [Ilegible], "Escanéalo de nuevo.", Ahora);
        var observado = eventos.OfType<ExpedienteObservado>().ShouldHaveSingleItem();
        observado.AreasQueObservaron.ShouldBe([_cumplimiento]);
        observado.RequisitosObservados.ShouldBe([_camara]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.Observado);
        e.ObservacionesVigentes.ShouldHaveSingleItem().RequisitoId.ShouldBe(_camara);
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _cumplimiento, _carla, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("expediente-observado");
    }

    [Fact]
    public void Con_politica_al_cerrar_area_se_devuelve_cuando_cada_area_decidio_todo_su_alcance()
    {
        var e = EnRevision(PoliticaDevolucion.AlCerrarArea);

        e = Observar(e, _camara, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision); // a Cumplimiento le falta el RUT y Compras no ha decidido
        e = Aprobar(e, _rut, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        var eventos = e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora);
        eventos.OfType<ExpedienteObservado>().ShouldHaveSingleItem().RequisitosObservados.ShouldBe([_camara]);
    }

    [Fact]
    public void Una_version_nueva_invalida_solo_los_veredictos_de_ese_requisito_y_el_reenvio_reabre_solo_lo_observado()
    {
        var e = EnRevision();
        e = Aprobar(e, _rut, _compras, _ana);
        e = Aprobar(e, _rut, _cumplimiento, _carla);
        e = Observar(e, _camara, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        // Sin versión nueva ni respuesta no se reenvía.
        Codigo(() => e.Reenviar(OrigenReenvio.Tercero, null, Ahora)).ShouldBe("requisitos-sin-corregir");

        var eventos = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>
        {
            [_rut] = V(_docRut, 1), // la misma: no cambia nada
            [_camara] = V(_docCamara, 2)
        }, Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VersionAdjuntadaARequisito", "VeredictosInvalidadosPorNuevaVersion"]);
        ((VeredictosInvalidadosPorNuevaVersion)eventos[1]).AreasAfectadas.ShouldBe([_cumplimiento]);
        e = Aplicar(e, eventos);

        var camara = e.BuscarRequisito(_camara)!;
        camara.Estado.ShouldBe(EstadoRequisito.Cargado); // corregida, espera el reenvío
        camara.VersionActual!.Numero.ShouldBe(2);
        camara.Historial.ShouldHaveSingleItem().Numero.ShouldBe(1);
        camara.Reenvios.ShouldBe(1);
        camara.Veredictos.ShouldHaveSingleItem().Invalidado.ShouldBeTrue();
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Aprobado); // el RUT conserva sus aprobaciones
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.ObservacionesVigentes.ShouldBeEmpty();

        // Repetir la sincronización no emite nada (idempotente).
        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_camara] = V(_docCamara, 2) }, Ahora).ShouldBeEmpty();

        var reenvio = e.Reenviar(OrigenReenvio.Tercero, null, Ahora);
        reenvio.OfType<ExpedienteReenviado>().ShouldHaveSingleItem().RequisitosReabiertos.ShouldBe([_camara]);
        e = Aplicar(e, reenvio);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_cumplimiento)!.RequisitosPorDecidir.ShouldBe(1);

        // La versión superada ya no se puede decidir.
        Codigo(() => e.EmitirVeredictoDeRequisito(_camara, _cumplimiento, _carla, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("version-superada");
        Aprobar(e, _camara, _cumplimiento, _carla, version: 2).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Una_respuesta_escrita_permite_reenviar_sin_version_nueva_y_reabre_el_requisito_observado()
    {
        var e = EnRevision();
        e = Observar(e, _camara, _cumplimiento, _carla);

        e = Aplicar(e, e.Reenviar(OrigenReenvio.Tercero, "La cámara es legible: es el formato de la entidad.", Ahora));

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        var camara = e.BuscarRequisito(_camara)!;
        camara.Estado.ShouldBe(EstadoRequisito.EnRevision);
        camara.Veredictos.ShouldHaveSingleItem().Superado.ShouldBeTrue();
    }

    [Fact]
    public void Una_version_nueva_mientras_se_revisa_quita_la_aprobacion_del_area_y_su_veredicto_derivado()
    {
        var e = EnRevision();
        e = Aprobar(e, _rut, _compras, _ana);
        e.BuscarArea(_compras)!.Veredictos.ShouldContain(v => v.Derivado && v.Vigente);

        e = Adjuntar(e, _rut, V(_docRut, 2));

        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_compras)!.Veredictos.ShouldAllBe(v => !v.Vigente);
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.EnRevision);
    }

    [Fact]
    public void Un_requisito_que_solo_revisan_etapas_cerradas_conserva_su_version()
    {
        // RUT revisado solo por Compras (etapa 1). Cuando esa etapa se cierra, nadie queda para revisar una versión
        // nueva: el requisito conserva la aprobada (RQ-10; la nueva queda para la revalidación).
        var rutSoloCompras = Rut(V(_docRut, 1)) with { AreasQueRevisan = [_compras] };
        var e = Abrir(Flujo(enSecuencia: true), rutSoloCompras, Camara(V(_docCamara, 1)));
        e = Aprobar(e, _rut, _compras, _ana);
        e.EtapaActiva!.Orden.ShouldBe(2);

        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 2) }, Ahora).ShouldBeEmpty();
    }

    // ---------- exención ----------

    [Fact]
    public void Eximir_pide_justificacion_y_si_era_lo_que_faltaba_pasa_a_revision()
    {
        var e = Abrir(Flujo(), Rut(V(_docRut, 1)), Camara());
        Codigo(() => e.EximirRequisito(_camara, "admin", " ", Ahora)).ShouldBe("justificacion-requerida");

        var eventos = e.EximirRequisito(_camara, "admin", "La entidad no está obligada a registrarse.", Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["RequisitoEximido", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        var camara = e.BuscarRequisito(_camara)!;
        camara.Estado.ShouldBe(EstadoRequisito.Eximido);
        camara.JustificacionExencion.ShouldBe("La entidad no está obligada a registrarse.");
        Codigo(() => e.EximirRequisito(_camara, "admin", "otra vez", Ahora)).ShouldBe("requisito-eximido");
        Codigo(() => e.EmitirVeredictoDeRequisito(_camara, _cumplimiento, _carla, Veredicto.Aprobado, 1, [], null, Ahora))
            .ShouldBe("requisito-eximido");

        e = Aprobar(e, _rut, _compras, _ana);
        Aprobar(e, _rut, _cumplimiento, _carla).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Eximir_el_ultimo_requisito_observado_reanuda_la_revision()
    {
        var e = EnRevision();
        e = Observar(e, _camara, _cumplimiento, _carla);

        var eventos = e.EximirRequisito(_camara, "admin", "Se validó por otro medio.", Ahora);
        eventos.OfType<RevisionReanudada>().ShouldHaveSingleItem().RequisitoEximidoId.ShouldBe(_camara);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
    }

    // ---------- automáticas y administrador ----------

    [Fact]
    public void Un_area_automatica_aprueba_sus_requisitos_con_origen_sistema_tambien_al_llegar_una_version_nueva()
    {
        var e = Abrir(Flujo(comprasAutomatica: true), out var eventos, Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));

        var automatico = eventos.OfType<VeredictoDeRequisitoEmitido>().ShouldHaveSingleItem();
        automatico.Origen.ShouldBe(OrigenVeredicto.Sistema);
        automatico.AreaId.ShouldBe(_compras);
        eventos.OfType<VeredictoDeAreaEmitido>().ShouldHaveSingleItem().Origen.ShouldBe(OrigenVeredicto.Sistema);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);

        e = Adjuntar(e, _rut, V(_docRut, 2));
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarRequisito(_rut)!.Veredictos.Count(v => v.Vigente && v.Origen == OrigenVeredicto.Sistema && v.SobreVersion == 2).ShouldBe(1);
    }

    [Fact]
    public void El_administrador_aprueba_los_requisitos_del_area_y_anular_una_observacion_reanuda()
    {
        var e = EnRevision();
        e = Observar(e, _camara, _cumplimiento, _carla);

        var observacion = e.BuscarRequisito(_camara)!.Veredictos.Single();
        e = Aplicar(e, e.AnularVeredicto(_cumplimiento, null, "admin", "Error de lectura del revisor.", Ahora));
        observacion = e.BuscarRequisito(_camara)!.Veredictos.Single(v => v.Id == observacion.Id);
        observacion.Anulado.ShouldBeTrue();
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        e = Aplicar(e, e.AprobarComoAdministrador(_cumplimiento, "admin", "Urgente por contrato.", Ahora));
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarArea(_cumplimiento)!.Veredictos.Single(v => v.Derivado).Origen.ShouldBe(OrigenVeredicto.Administrador);
        e.Requisitos.SelectMany(r => r.Veredictos).Where(v => v.Vigente && v.AreaId == _cumplimiento)
            .ShouldAllBe(v => v.Origen == OrigenVeredicto.Administrador);
    }

    // ---------- compatibilidad FL-1 ----------

    [Fact]
    public void Un_flujo_sin_requisitos_conserva_el_veredicto_sobre_el_expediente_completo()
    {
        var e = Abrir(Flujo(), out var eventos);

        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteAbierto", "FlujoSeleccionado", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        e.ConRequisitos.ShouldBeFalse();
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("sin-requisitos");
        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 1) }, Ahora).ShouldBeEmpty();

        e = Aplicar(e, e.EmitirVeredicto(_compras, _ana, Veredicto.Aprobado, [], null, Ahora));
        Aplicar(e, e.EmitirVeredicto(_cumplimiento, _carla, Veredicto.Aprobado, [], null, Ahora)).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    // ---------- etapas en secuencia (RQ-10) ----------

    [Fact]
    public void Una_etapa_posterior_observa_un_documento_ya_aprobado_y_la_version_nueva_lo_corrige()
    {
        var e = Abrir(Flujo(enSecuencia: true), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        e = Aprobar(e, _rut, _compras, _ana);
        e.Etapas[0].Estado.ShouldBe(EstadoEtapa.Aprobada);
        e = Observar(e, _rut, _cumplimiento, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        // La versión nueva se adjunta aunque Compras (etapa cerrada) ya lo aprobó: invalida solo lo de Cumplimiento.
        var eventos = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 2) }, Ahora);
        eventos.OfType<VeredictosInvalidadosPorNuevaVersion>().ShouldHaveSingleItem().AreasAfectadas.ShouldBe([_cumplimiento]);
        e = Aplicar(e, eventos);
        var rut = e.BuscarRequisito(_rut)!;
        rut.VersionActual!.Numero.ShouldBe(2);
        rut.AprobadoPor(e.BuscarArea(_compras)!).ShouldBeTrue();
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);

        // El tercero reenvía sin respuesta escrita y Cumplimiento decide sobre la v2.
        e = Aplicar(e, e.Reenviar(OrigenReenvio.Tercero, null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        Codigo(() => e.EmitirVeredictoDeRequisito(_rut, _cumplimiento, _carla, Veredicto.Aprobado, 1, [], null, Ahora)).ShouldBe("version-superada");
        e = Aprobar(e, _rut, _cumplimiento, _carla, version: 2);
        Aprobar(e, _camara, _cumplimiento, _carla).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Si_todas_sus_areas_estan_en_etapas_cerradas_la_version_nueva_no_se_adjunta()
    {
        var e = Abrir(Flujo(enSecuencia: true), Rut(V(_docRut, 1), aprobadores: null), Camara(V(_docCamara, 1)));
        e = Aprobar(e, _rut, _compras, _ana);

        // La Cámara solo la revisa Cumplimiento (etapa 2, abierta): se adjunta. El RUT también, porque Cumplimiento lo revisa.
        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_camara] = V(_docCamara, 2) }, Ahora)
            .OfType<VersionAdjuntadaARequisito>().ShouldHaveSingleItem();

        // Con Cumplimiento ya decidido y la etapa 2 cerrada el expediente se aprueba: después nada se adjunta.
        e = Aprobar(Aprobar(e, _rut, _cumplimiento, _carla), _camara, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 3) }, Ahora).ShouldBeEmpty();
    }
}

/// <summary>Requisitos en la versión del flujo (RQ-1): se validan contra los tipos del tenant y las áreas del flujo.</summary>
public class RequisitosDelFlujoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Area _compras;
    private readonly Area _cumplimiento;
    private readonly List<Area> _areas;
    private readonly List<TipoDocumentoTenant> _tipos;

    public RequisitosDelFlujoTests()
    {
        _compras = Area.Crear(_empresa, "COMPRAS", "Compras");
        _cumplimiento = Area.Crear(_empresa, "CUMPLIMIENTO", "Cumplimiento");
        _areas = [_compras, _cumplimiento];
        _tipos = [.. TipoDocumentoTenant.Semilla(Guid.NewGuid(), Ahora)];
    }

    private TipoDocumentoTenant Tipo(string codigo) => _tipos.Single(t => t.Codigo == codigo);

    private static RequisitoDelFlujo Pide(TipoDocumentoTenant tipo, params Guid[] areas) => new(Guid.Empty, tipo.Id, string.Empty, true, areas, null);

    private DefinicionDeFlujo Con(params RequisitoDelFlujo[] requisitos) => DefinicionDeFlujo.TodasEnParalelo(_areas) with { Requisitos = requisitos };

    private string Codigo(DefinicionDeFlujo definicion, IReadOnlyCollection<TipoDocumentoTenant>? tipos = null) =>
        Should.Throw<ReglaDeNegocioException>(() => definicion.Validar(_empresa, _areas, tipos ?? _tipos)).Codigo;

    [Fact]
    public void Valida_tipo_del_tenant_duplicados_areas_del_flujo_y_que_cada_area_revise_algo()
    {
        Codigo(Con(new RequisitoDelFlujo(Guid.Empty, Guid.NewGuid(), "NO_EXISTE", true, [], null))).ShouldBe("requisito-invalido");
        Codigo(Con(Pide(Tipo("RUT")), Pide(Tipo("RUT")))).ShouldBe("requisito-duplicado");
        Codigo(Con(Pide(Tipo("RUT"), Guid.NewGuid()))).ShouldBe("requisito-area-invalida");
        Codigo(Con(Pide(Tipo("RUT"), _cumplimiento.Id))).ShouldBe("area-sin-requisitos");
        Codigo(Con(Pide(Tipo("RUT")) with { AprobadoresRequeridos = 0 })).ShouldBe("aprobadores-invalidos");

        var formulario = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CUENTA", null, "CUENTA_BANCARIA", null, null, null, null, null, null, Ahora);
        Codigo(Con(Pide(formulario)), [.. _tipos, formulario]).ShouldBe("clase-no-soportada");

        var inactivo = Tipo("CEDULA");
        inactivo.Actualizar(null, null, null, null, null, null, null, activo: false);
        Codigo(Con(Pide(inactivo))).ShouldBe("requisito-invalido");
    }

    [Fact]
    public void Se_editan_en_el_borrador_conservan_su_id_y_se_congelan_al_publicar()
    {
        var flujo = Flujo.Crear(_empresa, "PROV", "Proveedores", Guid.NewGuid(),
            Con(Pide(Tipo("RUT")), Pide(Tipo("CAMARA_COMERCIO"), _cumplimiento.Id)), _areas, Ahora, _tipos);

        var borrador = flujo.Borrador!;
        borrador.Requisitos.Select(r => r.CodigoTipo).ShouldBe(["RUT", "CAMARA_COMERCIO"]);
        borrador.Requisitos.ShouldAllBe(r => r.Id != Guid.Empty);
        var idRut = borrador.Requisitos[0].Id;

        var v1 = flujo.Publicar(_areas, Ahora, _tipos);
        v1.Requisitos.Count.ShouldBe(2);

        // Editar crea la v2 en borrador; la publicada no cambia. Reenviar el id lo conserva.
        var v2 = flujo.EditarBorrador(null, v1.Definicion() with { Requisitos = [v1.Requisitos[0]] }, _areas, Ahora.AddDays(1), _tipos);
        v2.Version.ShouldBe(2);
        v2.Requisitos.ShouldHaveSingleItem().Id.ShouldBe(idRut);
        flujo.VersionPublicada!.Requisitos.Count.ShouldBe(2);

        // Publicar revalida: un tipo desactivado entretanto impide publicar.
        Tipo("RUT").Actualizar(null, null, null, null, null, null, null, activo: false);
        Should.Throw<ReglaDeNegocioException>(() => flujo.Publicar(_areas, Ahora.AddDays(2), _tipos)).Codigo.ShouldBe("requisito-invalido");
    }
}
