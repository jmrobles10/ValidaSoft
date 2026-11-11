using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Desaprobar (DS-1, DS-2), lo que el SRM hace con <c>DesaprobarTercero</c>, <c>DesaprobarAFlujo</c> y <c>DesaprobarAEstadoInicial</c>,
/// y reintegrar sin revalidar (DS-3), en la fase sin requisitos: cada área decide sobre el expediente completo.
/// </summary>
public class DesaprobacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));
    private const string Admin = "admin-1";

    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Guid _c = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);
    private readonly Revisor _carla = new(Guid.NewGuid(), null);

    private static readonly MotivoAplicado Incompleto = new("incompleto", "La información está incompleta.");

    private static AreaDelFlujo Area(Guid id, string codigo, int aprobadores = 1, bool automatica = false) =>
        new(id, codigo, codigo, aprobadores, false, automatica, null);

    private static Expediente Abrir(params EtapaDelFlujo[] etapas) =>
        Expediente.Reproducir(null, Expediente.Abrir(
            new DatosDeApertura(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.",
                "CO|NIT|900123456", Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []),
            new FlujoParaExpediente(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto, etapas,
                PoliticaDevolucion.Inmediata, false), Ahora));

    /// <summary>Tres etapas en secuencia: A, B y C.</summary>
    private Expediente EnTresEtapas() =>
        Abrir(new EtapaDelFlujo(1, [Area(_a, "A")]), new EtapaDelFlujo(2, [Area(_b, "B")]), new EtapaDelFlujo(3, [Area(_c, "C")]));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private static Expediente Aprobar(Expediente e, Guid area, Revisor revisor) =>
        Aplicar(e, e.EmitirVeredicto(area, revisor, Veredicto.Aprobado, [], null, Ahora));

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private Expediente Aprobado()
    {
        var e = EnTresEtapas();
        e = Aprobar(e, _a, _ana);
        e = Aprobar(e, _b, _beto);
        e = Aprobar(e, _c, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        return e;
    }

    // ---------- (a) reabrir una etapa ya aprobada ----------

    [Fact]
    public void Reabrir_una_etapa_aprobada_devuelve_esa_etapa_y_las_posteriores_a_esperar_y_sus_veredictos_dejan_de_contar()
    {
        var e = Aprobar(Aprobar(EnTresEtapas(), _a, _ana), _b, _beto); // A y B aprobadas; C activa

        var eventos = e.ReabrirEtapa(2, Admin, "Faltó revisar la garantía.", Ahora.AddDays(1));
        eventos.Select(x => x.GetType().Name).ShouldBe(["EtapaReabierta", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Aprobada, EstadoEtapa.Activa, EstadoEtapa.Esperando]);
        e.Etapas[1].AprobadaEn.ShouldBeNull();
        // La etapa 1 no se toca; la 2 vuelve a pendiente y su aprobación queda en el historial como revertida.
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        var b = e.BuscarArea(_b)!;
        b.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        b.PendienteDesde.ShouldBe(Ahora.AddDays(1));
        var historico = b.Veredictos.ShouldHaveSingleItem();
        historico.Vigente.ShouldBeFalse();
        historico.Revertido.ShouldBeTrue();
        e.BuscarArea(_c)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);

        e.Desaprobaciones.ShouldBe(1);
        e.UltimaDesaprobacion.ShouldNotBeNull().ShouldSatisfyAllConditions(
            d => d.Etapa.ShouldBe(2),
            d => d.FaseInicial.ShouldBeFalse(),
            d => d.ExpedienteAprobado.ShouldBeFalse(),
            d => d.AdministradorId.ShouldBe(Admin),
            d => d.Justificacion.ShouldBe("Faltó revisar la garantía."));
    }

    [Fact]
    public void Tras_reabrir_la_etapa_el_flujo_sigue_desde_ahi_y_las_etapas_posteriores_deciden_de_nuevo()
    {
        var e = Aprobar(Aprobar(EnTresEtapas(), _a, _ana), _b, _beto);
        e = Aplicar(e, e.ReabrirEtapa(2, Admin, "Revisar de nuevo.", Ahora));

        // C no puede decidir hasta que B vuelva a aprobar; el mismo revisor puede aprobar otra vez (su veredicto anterior ya no cuenta).
        Codigo(() => e.EmitirVeredicto(_c, _carla, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("area-no-activa");
        e = Aprobar(e, _b, _beto);
        e.EtapaActiva!.Orden.ShouldBe(3);
        e = Aprobar(e, _c, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Reabrir_una_etapa_exige_justificacion_y_que_la_etapa_exista_y_este_aprobada()
    {
        var e = Aprobar(EnTresEtapas(), _a, _ana); // A aprobada; B activa; C esperando

        Codigo(() => e.ReabrirEtapa(1, Admin, " ", Ahora)).ShouldBe("justificacion-requerida");
        Codigo(() => e.ReabrirEtapa(2, Admin, "x", Ahora)).ShouldBe("etapa-no-aprobada"); // la activa se corrige anulando veredictos
        Codigo(() => e.ReabrirEtapa(3, Admin, "x", Ahora)).ShouldBe("etapa-no-aprobada"); // la que aún espera
        Codigo(() => e.ReabrirEtapa(9, Admin, "x", Ahora)).ShouldBe("etapa-no-encontrada");
    }

    [Fact]
    public void Solo_se_reabre_una_etapa_de_un_expediente_en_revision_o_devuelto()
    {
        var aprobado = Aprobado();
        Codigo(() => aprobado.ReabrirEtapa(1, Admin, "x", Ahora)).ShouldBe("estado-invalido"); // aprobado: se desaprueba
        var abierto = EnTresEtapas();
        var anulado = Aplicar(abierto, [abierto.Anular("Error", Ahora)]);
        Codigo(() => anulado.ReabrirEtapa(1, Admin, "x", Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Reabrir_una_etapa_anterior_con_el_expediente_devuelto_anula_la_observacion_y_reanuda_la_revision()
    {
        var e = Aprobar(EnTresEtapas(), _a, _ana);
        e = Aplicar(e, e.EmitirVeredicto(_b, _beto, Veredicto.Observado, [Incompleto], "Falta el RUT.", Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var eventos = e.ReabrirEtapa(1, Admin, "La etapa 1 aprobó sin ver el RUT.", Ahora.AddHours(2));
        eventos.Select(x => x.GetType().Name).ShouldBe(["EtapaReabierta", "EtapaActivada", "RevisionReanudada"]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.ObservacionesVigentes.ShouldBeEmpty();
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_b)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);
        e.BuscarArea(_b)!.Veredictos.ShouldHaveSingleItem().Revertido.ShouldBeTrue();
    }

    [Fact]
    public void Las_areas_automaticas_de_la_etapa_reabierta_aprueban_de_nuevo_solas_y_siguen_la_cascada()
    {
        var e = Abrir(new EtapaDelFlujo(1, [Area(_a, "A")]), new EtapaDelFlujo(2, [Area(_b, "B", automatica: true)]), new EtapaDelFlujo(3, [Area(_c, "C")]));
        e = Aprobar(e, _a, _ana); // B aprueba sola y C queda activa
        e.EtapaActiva!.Orden.ShouldBe(3);

        e = Aplicar(e, e.ReabrirEtapa(2, Admin, "Revisar la etapa automática.", Ahora));

        // B (automática) vuelve a aprobar en el acto y C se activa otra vez: el expediente espera a C.
        e.BuscarArea(_b)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarArea(_b)!.Veredictos.Count(v => v.Vigente).ShouldBe(1);
        e.EtapaActiva!.Orden.ShouldBe(3);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
    }

    // ---------- (b) desaprobar un expediente aprobado ----------

    [Fact]
    public void Desaprobar_un_expediente_aprobado_lo_devuelve_a_revision_desde_la_ultima_etapa()
    {
        var e = Aprobado();
        e.CerradoEn.ShouldNotBeNull();

        var eventos = e.Desaprobar(null, Admin, "Apareció una inhabilidad.", Ahora.AddDays(30));
        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteDesaprobado", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.CerradoEn.ShouldBeNull();
        e.EtapaActiva!.Orden.ShouldBe(3);
        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Aprobada, EstadoEtapa.Aprobada, EstadoEtapa.Activa]);
        e.BuscarArea(_c)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_c)!.Veredictos.ShouldHaveSingleItem().Revertido.ShouldBeTrue();
        e.UltimaDesaprobacion!.ExpedienteAprobado.ShouldBeTrue();
        e.Desaprobaciones.ShouldBe(1);

        // Se aprueba de nuevo: el mismo expediente (mismo folio) vuelve a quedar aprobado.
        e = Aprobar(e, _c, _carla);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.Folio.ShouldBe("NORTE-E-2026-000001");
    }

    [Fact]
    public void Desaprobar_desde_una_etapa_intermedia_deja_pendientes_esa_etapa_y_las_posteriores()
    {
        var e = Aprobado();

        e = Aplicar(e, e.Desaprobar(2, Admin, "Revisar de nuevo.", Ahora));

        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Aprobada, EstadoEtapa.Activa, EstadoEtapa.Esperando]);
        e.UltimaDesaprobacion!.Etapa.ShouldBe(2);
        e.UltimaDesaprobacion.FaseInicial.ShouldBeFalse();
    }

    [Fact]
    public void Solo_se_desaprueba_un_expediente_aprobado_con_justificacion_y_una_etapa_existente()
    {
        var enRevision = EnTresEtapas();
        Codigo(() => enRevision.Desaprobar(null, Admin, "x", Ahora)).ShouldBe("estado-invalido");

        var aprobado = Aprobado();
        Codigo(() => aprobado.Desaprobar(null, Admin, null, Ahora)).ShouldBe("justificacion-requerida");
        Codigo(() => aprobado.Desaprobar(7, Admin, "x", Ahora)).ShouldBe("etapa-no-encontrada");
    }

    [Fact]
    public void Si_todas_las_areas_son_automaticas_el_expediente_se_aprueba_de_nuevo_en_el_acto()
    {
        var e = Abrir(new EtapaDelFlujo(1, [Area(_a, "A", automatica: true)]));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);

        var eventos = e.Desaprobar(null, Admin, "Probar.", Ahora);

        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteDesaprobado", "EtapaActivada", "VeredictoDeAreaEmitido", "EtapaAprobada", "ExpedienteAprobado"]);
        Aplicar(e, eventos).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    // ---------- (c) volver a la fase inicial ----------

    [Fact]
    public void Volver_a_la_fase_inicial_repite_todo_el_flujo_desde_la_primera_etapa()
    {
        var e = Aprobar(Aprobar(EnTresEtapas(), _a, _ana), _b, _beto);

        var eventos = e.VolverAFaseInicial(Admin, "Cambió la política de contratación.", Ahora.AddDays(2));
        eventos.Select(x => x.GetType().Name).ShouldBe(["EtapaReabierta", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.EtapaActiva!.Orden.ShouldBe(1);
        e.Etapas.Select(x => x.Estado).ShouldBe([EstadoEtapa.Activa, EstadoEtapa.Esperando, EstadoEtapa.Esperando]);
        e.Etapas.SelectMany(x => x.Areas).SelectMany(a => a.Veredictos).ShouldAllBe(v => !v.Vigente && v.Revertido);
        e.UltimaDesaprobacion!.FaseInicial.ShouldBeTrue();
        e.UltimaDesaprobacion.Etapa.ShouldBe(1);
    }

    [Fact]
    public void Volver_a_la_fase_inicial_con_una_aprobacion_parcial_en_la_primera_etapa_tambien_la_descarta()
    {
        var e = Abrir(new EtapaDelFlujo(1, [Area(_a, "A", aprobadores: 2)]));
        e = Aprobar(e, _a, _ana); // 1 de 2 aprobaciones
        e.BuscarArea(_a)!.Aprobaciones.ShouldBe(1);

        e = Aplicar(e, e.VolverAFaseInicial(Admin, "Empezar de cero.", Ahora));

        e.BuscarArea(_a)!.Aprobaciones.ShouldBe(0);
        // La misma persona puede votar otra vez: su veredicto anterior quedó revertido.
        Aprobar(e, _a, _ana).BuscarArea(_a)!.Aprobaciones.ShouldBe(1);
    }

    [Fact]
    public void Volver_a_la_fase_inicial_sin_nada_decidido_no_cambia_nada_y_se_rechaza()
    {
        var e = EnTresEtapas();

        Codigo(() => e.VolverAFaseInicial(Admin, "x", Ahora)).ShouldBe("ya-esta-en-fase-inicial");
        Codigo(() => Aprobado().VolverAFaseInicial(Admin, " ", Ahora)).ShouldBe("justificacion-requerida");
    }

    [Fact]
    public void Volver_a_la_fase_inicial_de_un_expediente_aprobado_es_tambien_una_desaprobacion()
    {
        var e = Aprobado();

        var eventos = e.VolverAFaseInicial(Admin, "Empezar de cero.", Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteDesaprobado", "EtapaActivada"]);
        ((ExpedienteDesaprobado)eventos[0]).FaseInicial.ShouldBeTrue();
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EtapaActiva!.Orden.ShouldBe(1);
        e.UltimaDesaprobacion!.ExpedienteAprobado.ShouldBeTrue();
        e.UltimaDesaprobacion.FaseInicial.ShouldBeTrue();
    }

    [Fact]
    public void Un_expediente_cerrado_sin_aprobar_no_vuelve_a_la_fase_inicial()
    {
        var abierto = EnTresEtapas();
        var anulado = Aplicar(abierto, [abierto.Anular("Error", Ahora)]);
        Codigo(() => anulado.VolverAFaseInicial(Admin, "x", Ahora)).ShouldBe("estado-invalido");
    }

    // ---------- tiempos ----------

    [Fact]
    public void Los_tiempos_se_miden_con_la_desaprobacion_y_no_cuentan_el_tiempo_cerrado()
    {
        var todos = new List<object>(Expediente.Abrir(
            new DatosDeApertura(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "N-E-1", Guid.NewGuid(), "Aceros", "CO|NIT|9",
                Guid.NewGuid(), "P", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "t"), []),
            new FlujoParaExpediente(Guid.NewGuid(), Guid.NewGuid(), "HAB", "H", 1, OrigenFlujo.PorDefecto,
                [new EtapaDelFlujo(1, [Area(_a, "A")])], PoliticaDevolucion.Inmediata, false), Ahora));
        var actual = Expediente.Reproducir(null, todos);
        var aprobar = actual.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora.AddHours(1));
        todos.AddRange(aprobar);
        actual = Aplicar(actual, aprobar);
        actual.Estado.ShouldBe(EstadoExpediente.Aprobado);
        var desaprobar = actual.Desaprobar(null, Admin, "Revisar.", Ahora.AddHours(11)); // 10 horas cerrado
        todos.AddRange(desaprobar);

        var tiempos = Expediente.MedirTiempos(todos, Ahora.AddHours(12));

        tiempos.HorasEnRevision.ShouldBe(2, 0.001); // 1 h antes de aprobar y 1 h después de desaprobar; las 10 h cerrado no cuentan
        tiempos.Areas.ShouldHaveSingleItem().Rondas.ShouldBe(2);
    }

    // ---------- vínculo: desaprobar y reintegrar ----------

    private static Vinculo VinculoActivo(out Guid expedienteId)
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null,
            "compras@proveedor.co", Ahora));
        expedienteId = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(expedienteId, TipoExpediente.Inicial, "N-E-1", Ahora));
        foreach (var evento in vinculo.CerrarExpediente(expedienteId, ResultadoExpediente.Aprobado, Ahora))
        {
            if (evento is ExpedienteCerrado cerrado) vinculo.Apply(cerrado);
            if (evento is VinculoActivado activado) vinculo.Apply(activado);
        }

        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        return vinculo;
    }

    [Fact]
    public void Desaprobar_deja_al_vinculo_en_validacion_con_el_mismo_expediente_abierto()
    {
        var vinculo = VinculoActivo(out var expediente);

        var evento = vinculo.Desaprobar(expediente, "Apareció una inhabilidad.", Ahora.AddDays(30));
        evento.EstadoPrevio.ShouldBe(EstadoVinculo.Activo);
        vinculo.Apply(evento);

        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        vinculo.ExpedienteAbiertoId.ShouldBe(expediente);
        vinculo.TuvoAprobacion.ShouldBeTrue();

        // Aprobado otra vez, vuelve a Activo; rechazado o anulado, a Invitado (y entonces se abre un expediente inicial nuevo).
        foreach (var e in vinculo.CerrarExpediente(expediente, ResultadoExpediente.Aprobado, Ahora.AddDays(31)))
        {
            if (e is ExpedienteCerrado cerrado) vinculo.Apply(cerrado);
            if (e is VinculoActivado activado) vinculo.Apply(activado);
        }

        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
    }

    [Fact]
    public void Un_expediente_desaprobado_que_luego_se_rechaza_deja_al_vinculo_invitado_y_se_puede_abrir_uno_inicial_nuevo()
    {
        var vinculo = VinculoActivo(out var expediente);
        vinculo.Apply(vinculo.Desaprobar(expediente, "x", Ahora));

        vinculo.Apply((ExpedienteCerrado)vinculo.CerrarExpediente(expediente, ResultadoExpediente.Rechazado, Ahora.AddDays(1)).Single());

        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        var nuevo = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(nuevo, TipoExpediente.Inicial, "N-E-2", Ahora.AddDays(2)));
        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        vinculo.ExpedienteAbiertoId.ShouldBe(nuevo);
    }

    [Fact]
    public void Desaprobar_exige_el_ultimo_expediente_aprobado_sin_otro_abierto_y_un_vinculo_vigente()
    {
        var vinculo = VinculoActivo(out var expediente);

        Should.Throw<ReglaDeNegocioException>(() => vinculo.Desaprobar(expediente, " ", Ahora)).Codigo.ShouldBe("justificacion-requerida");
        Should.Throw<ReglaDeNegocioException>(() => vinculo.Desaprobar(Guid.NewGuid(), "x", Ahora)).Codigo.ShouldBe("no-es-el-ultimo-aprobado");

        var abierto = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(abierto, TipoExpediente.Actualizacion, "N-E-2", Ahora));
        Should.Throw<ReglaDeNegocioException>(() => vinculo.Desaprobar(expediente, "x", Ahora)).Codigo.ShouldBe("expediente-abierto-existente");
        vinculo.Apply((ExpedienteCerrado)vinculo.CerrarExpediente(abierto, ResultadoExpediente.Anulado, Ahora).Single());

        // Anulada la actualización, el último resultado es ese: ya no es el aprobado más reciente del vínculo.
        Should.Throw<ReglaDeNegocioException>(() => vinculo.Desaprobar(expediente, "x", Ahora)).Codigo.ShouldBe("no-es-el-ultimo-aprobado");

        var bloqueado = VinculoActivo(out var otro);
        bloqueado.Apply(bloqueado.Bloquear("Sanción", null, Ahora));
        Should.Throw<ReglaDeNegocioException>(() => bloqueado.Desaprobar(otro, "x", Ahora)).Codigo.ShouldBe("vinculo-bloqueado");

        var terminado = VinculoActivo(out var ultimo);
        terminado.Apply(terminado.Desvincular("Fin", Ahora));
        Should.Throw<ReglaDeNegocioException>(() => terminado.Desaprobar(ultimo, "x", Ahora)).Codigo.ShouldBe("estado-terminal");
    }

    [Fact]
    public void Reintegrar_sin_revalidar_restituye_el_estado_que_tenia_al_desvincularse()
    {
        var vinculo = VinculoActivo(out _);
        vinculo.Apply(vinculo.Desvincular("Fin de contrato", Ahora.AddDays(10)));
        vinculo.EstadoAlDesvincular.ShouldBe(EstadoVinculo.Activo);

        var evento = vinculo.Reintegrar("Se renovó el contrato.", Ahora.AddDays(20), sinRevalidar: true);
        evento.SinRevalidar.ShouldBeTrue();
        vinculo.Apply(evento);

        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.EstadoAlDesvincular.ShouldBeNull();
    }

    [Fact]
    public void Reintegrar_sin_revalidar_un_vinculo_bloqueado_al_desvincularse_usa_el_estado_previo_al_bloqueo()
    {
        var vinculo = VinculoActivo(out _);
        vinculo.Apply(vinculo.Bloquear("Sanción", null, Ahora));
        vinculo.Apply(vinculo.Desvincular("Fin de contrato", Ahora.AddDays(1)));

        vinculo.EstadoAlDesvincular.ShouldBe(EstadoVinculo.Activo);
        vinculo.Apply(vinculo.Reintegrar("Ok", Ahora.AddDays(2), sinRevalidar: true));
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
    }

    [Fact]
    public void Reintegrar_sin_revalidar_solo_si_estaba_aprobado_y_vigente_al_desvincularse()
    {
        // Nunca aprobado.
        var nuevo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        nuevo.Apply(nuevo.Desvincular("Fin", Ahora));
        Should.Throw<ReglaDeNegocioException>(() => nuevo.Reintegrar("x", Ahora, sinRevalidar: true)).Codigo.ShouldBe("reintegro-sin-aprobacion-vigente");

        // Aprobado pero vencido.
        var vencido = VinculoActivo(out _);
        vencido.Apply(vencido.Vencer("RUT", Ahora.AddDays(400))!);
        vencido.Apply(vencido.Desvincular("Fin", Ahora.AddDays(401)));
        vencido.EstadoAlDesvincular.ShouldBe(EstadoVinculo.Vencido);
        Should.Throw<ReglaDeNegocioException>(() => vencido.Reintegrar("x", Ahora.AddDays(402), sinRevalidar: true)).Codigo
            .ShouldBe("reintegro-sin-aprobacion-vigente");

        // Revalidando, un vínculo que fue aprobado vuelve a validación y se le abre una renovación con todo.
        vencido.Apply(vencido.Reintegrar("x", Ahora.AddDays(402)));
        vencido.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        var renovacion = Guid.NewGuid();
        vencido.Apply(vencido.VincularExpediente(renovacion, TipoExpediente.Renovacion, "N-E-2", Ahora.AddDays(402)));
        vencido.ExpedienteAbiertoId.ShouldBe(renovacion);
        vencido.Estado.ShouldBe(EstadoVinculo.EnValidacion);
    }
}
