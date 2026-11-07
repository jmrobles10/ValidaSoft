using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Decisión por área con requisitos (DA-1…DA-4): «Aprobar área» y «Observar» marcando documentos en un solo paso,
/// conservando por dentro un veredicto por documento y versión. Mismo escenario: RUT (Compras y Cumplimiento) y Cámara
/// de comercio (solo Cumplimiento).
/// </summary>
public partial class RequisitosExpedienteTests
{
    private static Dictionary<Guid, int> Versiones(params (Guid Requisito, int Version)[] versiones) =>
        versiones.ToDictionary(v => v.Requisito, v => v.Version);

    private static ObservacionDeRequisito Marca(Guid requisito, int version = 1, string? observacion = "Escanéalo de nuevo.") =>
        new(requisito, version, [Ilegible], observacion);

    private static IReadOnlyList<DocumentoPorDecidir> DatosRequisitos(Action accion) =>
        (IReadOnlyList<DocumentoPorDecidir>)Should.Throw<ReglaDeNegocioException>(accion).Datos!["requisitos"]!;

    [Fact]
    public void Aprobar_area_aprueba_todos_sus_documentos_pendientes_en_un_paso_y_sigue_la_cascada()
    {
        var e = EnRevision();
        e.DocumentosPorDecidir(_compras, _ana.UsuarioId.ToString()).ShouldBe([new DocumentoPorDecidir(_rut, 1)]);
        e.DocumentosPorDecidir(_cumplimiento).ShouldBe([new DocumentoPorDecidir(_rut, 1), new DocumentoPorDecidir(_camara, 1)]);

        var eventos = e.AprobarArea(_compras, _ana, Versiones((_rut, 1)), Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VeredictoDeRequisitoEmitido", "VeredictoDeAreaEmitido"]);
        e = Aplicar(e, eventos);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);

        eventos = e.AprobarArea(_cumplimiento, _carla, Versiones((_rut, 1), (_camara, 1)), Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(
            ["VeredictoDeRequisitoEmitido", "VeredictoDeRequisitoEmitido", "VeredictoDeAreaEmitido", "EtapaAprobada", "ExpedienteAprobado"]);
        var porDocumento = eventos.OfType<VeredictoDeRequisitoEmitido>().ToList();
        porDocumento.Select(v => v.RequisitoId).ShouldBe([_rut, _camara]);
        porDocumento.ShouldAllBe(v => v.Veredicto == Veredicto.Aprobado && v.Origen == OrigenVeredicto.Humano &&
                                      v.RevisorId == _carla.UsuarioId.ToString() && v.SobreVersion == 1 && v.AreaId == _cumplimiento);
        eventos.OfType<VeredictoDeAreaEmitido>().ShouldHaveSingleItem().DerivadoDeRequisitos.ShouldBeTrue();

        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.Requisitos.ShouldAllBe(r => r.Estado == EstadoRequisito.Aprobado);
    }

    [Fact]
    public void Con_dos_aprobadores_dos_personas_distintas_aprueban_el_area()
    {
        var e = Abrir(Flujo(aprobadoresCumplimiento: 2), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        var todos = Versiones((_rut, 1), (_camara, 1));

        e = Aplicar(e, e.AprobarArea(_cumplimiento, _carla, todos, Ahora));
        var cumplimiento = e.BuscarArea(_cumplimiento)!;
        cumplimiento.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.Requisitos.ShouldAllBe(r => r.AprobacionesDe(cumplimiento) == 1);
        // A Carla no le queda nada; al área, sí (falta otra persona).
        e.DocumentosPorDecidir(_cumplimiento, _carla.UsuarioId.ToString()).ShouldBeEmpty();
        e.DocumentosPorDecidir(_cumplimiento).Count.ShouldBe(2);
        Codigo(() => e.AprobarArea(_cumplimiento, _carla, todos, Ahora)).ShouldBe("area-ya-se-pronuncio");

        e = Aplicar(e, e.AprobarArea(_cumplimiento, _beto, todos, Ahora));
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.DocumentosPorDecidir(_cumplimiento).ShouldBeEmpty();
        Codigo(() => e.AprobarArea(_cumplimiento, _ana, todos, Ahora)).ShouldBe("area-ya-se-pronuncio");
    }

    [Fact]
    public void Con_una_version_desactualizada_no_se_aprueba_nada()
    {
        var e = EnRevision();
        e = Adjuntar(e, _camara, V(_docCamara, 2));

        var accion = () => e.AprobarArea(_cumplimiento, _carla, Versiones((_rut, 1), (_camara, 1)), Ahora);
        Codigo(() => accion()).ShouldBe("version-superada");
        DatosRequisitos(() => accion()).ShouldBe([new DocumentoPorDecidir(_camara, 2)]);
        e.Requisitos.SelectMany(r => r.Veredictos).ShouldBeEmpty();

        // Con la versión vigente sí.
        Aplicar(e, e.AprobarArea(_cumplimiento, _carla, Versiones((_rut, 1), (_camara, 2)), Ahora))
            .BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
    }

    [Fact]
    public void Sin_las_versiones_de_todos_sus_documentos_responde_versiones_requeridas()
    {
        var e = EnRevision();

        Codigo(() => e.AprobarArea(_cumplimiento, _carla, null, Ahora)).ShouldBe("versiones-requeridas");
        var faltantes = DatosRequisitos(() => e.AprobarArea(_cumplimiento, _carla, Versiones((_rut, 1)), Ahora));
        faltantes.ShouldBe([new DocumentoPorDecidir(_camara, 1)]);
        Should.Throw<ReglaDeNegocioException>(() => e.AprobarArea(_cumplimiento, _carla, null, Ahora)).Tipo.ShouldBe(TipoFalla.Validacion);

        // Un documento fuera del alcance o inexistente no se cuela.
        Codigo(() => e.AprobarArea(_compras, _ana, Versiones((_rut, 1), (_camara, 1)), Ahora)).ShouldBe("requisito-no-en-alcance");
        Codigo(() => e.AprobarArea(_compras, _ana, Versiones((_rut, 1), (Guid.NewGuid(), 1)), Ahora)).ShouldBe("requisito-no-encontrado");
    }

    [Fact]
    public void Una_persona_que_aprobo_un_documento_suelto_aprueba_el_resto_con_aprobar_area()
    {
        var e = EnRevision();
        e = Aprobar(e, _rut, _cumplimiento, _carla);
        e.DocumentosPorDecidir(_cumplimiento, _carla.UsuarioId.ToString()).ShouldBe([new DocumentoPorDecidir(_camara, 1)]);

        // La versión del RUT (ya decidido) se ignora: viene de una lectura anterior.
        var eventos = e.AprobarArea(_cumplimiento, _carla, Versiones((_rut, 1), (_camara, 1)), Ahora);
        eventos.OfType<VeredictoDeRequisitoEmitido>().ShouldHaveSingleItem().RequisitoId.ShouldBe(_camara);
        e = Aplicar(e, eventos);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarRequisito(_rut)!.Veredictos.Count(v => v.AreaId == _cumplimiento).ShouldBe(1);
    }

    [Fact]
    public void Observar_varios_documentos_emite_un_veredicto_por_documento_y_un_solo_expediente_observado()
    {
        var e = EnRevision();
        e = Aprobar(e, _rut, _compras, _ana);

        var eventos = e.ObservarArea(_cumplimiento, _carla, [Marca(_rut), Marca(_camara, observacion: "La cámara está cortada.")], Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VeredictoDeRequisitoEmitido", "VeredictoDeRequisitoEmitido", "ExpedienteObservado"]);
        eventos.OfType<VeredictoDeRequisitoEmitido>().ShouldAllBe(v => v.Veredicto == Veredicto.Observado && v.Motivos.Count == 1);
        var observado = eventos.OfType<ExpedienteObservado>().ShouldHaveSingleItem();
        observado.AreasQueObservaron.ShouldBe([_cumplimiento]);
        observado.RequisitosObservados.ShouldBe([_rut, _camara]);

        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.ObservacionesVigentes.Select(v => v.RequisitoId).ShouldBe([_rut, _camara]);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada); // lo de Compras no se toca
    }

    [Fact]
    public void Observar_el_area_pide_documentos_motivos_y_versiones_vigentes()
    {
        var e = EnRevision();
        e = Adjuntar(e, _rut, V(_docRut, 2));

        Codigo(() => e.ObservarArea(_cumplimiento, _carla, [], Ahora)).ShouldBe("requisitos-requeridos");
        Codigo(() => e.ObservarArea(_cumplimiento, _carla, [Marca(_camara), Marca(_camara)], Ahora)).ShouldBe("requisito-duplicado");
        Codigo(() => e.ObservarArea(_cumplimiento, _carla, [new ObservacionDeRequisito(_camara, 1, [], null)], Ahora)).ShouldBe("motivo-requerido");
        Codigo(() => e.ObservarArea(_cumplimiento, _carla, [new ObservacionDeRequisito(_camara, null, [Ilegible], null)], Ahora))
            .ShouldBe("sobre-version-requerida");
        Codigo(() => e.ObservarArea(_compras, _ana, [Marca(_camara)], Ahora)).ShouldBe("requisito-no-en-alcance");
        DatosRequisitos(() => e.ObservarArea(_cumplimiento, _carla, [Marca(_rut), Marca(_camara)], Ahora))
            .ShouldBe([new DocumentoPorDecidir(_rut, 2)]);

        // Solo los documentos marcados: la Cámara queda observada y el RUT sin decidir.
        e = Aplicar(e, e.ObservarArea(_cumplimiento, _carla, [Marca(_camara)], Ahora));
        e.BuscarRequisito(_camara)!.ObservadoPor(_cumplimiento).ShouldBeTrue();
        e.BuscarRequisito(_rut)!.Veredictos.ShouldBeEmpty();
    }

    [Fact]
    public void No_se_aprueba_el_area_encima_de_una_observacion_vigente()
    {
        var e = EnRevision(PoliticaDevolucion.AlCerrarArea);
        e = Observar(e, _camara, _cumplimiento, _carla);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        Codigo(() => e.AprobarArea(_cumplimiento, _beto, Versiones((_rut, 1)), Ahora)).ShouldBe("area-observada");
        // El documento suelto sigue disponible.
        e = Aprobar(e, _rut, _cumplimiento, _beto);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Observada);
    }

    [Fact]
    public void La_decision_de_area_respeta_las_reglas_del_revisor_y_del_estado()
    {
        var conSeparacion = Abrir(Flujo(), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        var tercero = new Revisor(Guid.NewGuid(), "CO|NIT|900123456");
        Codigo(() => conSeparacion.AprobarArea(_compras, tercero, Versiones((_rut, 1)), Ahora)).ShouldBe("conflicto-de-intereses");

        var automatica = Abrir(Flujo(comprasAutomatica: true), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        Codigo(() => automatica.AprobarArea(_compras, _ana, Versiones((_rut, 1)), Ahora)).ShouldBe("area-automatica");

        var observado = Observar(EnRevision(), _camara, _cumplimiento, _carla);
        Codigo(() => observado.AprobarArea(_compras, _ana, Versiones((_rut, 1)), Ahora)).ShouldBe("expediente-observado");

        var sinRequisitos = Abrir(Flujo());
        Codigo(() => sinRequisitos.AprobarArea(_compras, _ana, Versiones((_rut, 1)), Ahora)).ShouldBe("sin-requisitos");
        Codigo(() => sinRequisitos.ObservarArea(_compras, _ana, [Marca(_rut)], Ahora)).ShouldBe("sin-requisitos");
        sinRequisitos.DocumentosPorDecidir(_compras).ShouldBeEmpty();
    }
}
