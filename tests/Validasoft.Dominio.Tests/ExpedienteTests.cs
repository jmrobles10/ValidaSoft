using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>Tabla de casos del expediente (docs/03 §6 y §13) en la fase sin requisitos: cada área decide sobre el expediente.</summary>
public class ExpedienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));
    private const string ClaveTercero = "CO|NIT|900123456";

    private readonly Guid _a = Guid.NewGuid();
    private readonly Guid _b = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);
    private readonly Revisor _carla = new(Guid.NewGuid(), null);

    private static readonly MotivoAplicado Incompleto = new("incompleto", "La información está incompleta.");

    private static AreaDelFlujo Area(Guid id, string codigo, int aprobadores = 1, bool rechaza = false, bool automatica = false) =>
        new(id, codigo, codigo, aprobadores, rechaza, automatica, null);

    private static FlujoParaExpediente Flujo(PoliticaDevolucion politica, bool separacion, params EtapaDelFlujo[] etapas) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto, etapas, politica, separacion);

    private FlujoParaExpediente EnParalelo(PoliticaDevolucion politica = PoliticaDevolucion.Inmediata, bool rechazaB = false) =>
        Flujo(politica, false, new EtapaDelFlujo(1, [Area(_a, "A"), Area(_b, "B", rechaza: rechazaB)]));

    private static DatosDeApertura Datos(ActorDeExpediente? abiertoPor = null, IReadOnlyList<string>? invitadoPor = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", ClaveTercero,
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", abiertoPor ?? new ActorDeExpediente(ActorTipo.Tercero, "tercero"), invitadoPor ?? []);

    private static Expediente Abrir(FlujoParaExpediente flujo, out IReadOnlyList<object> eventos, DatosDeApertura? datos = null)
    {
        eventos = Expediente.Abrir(datos ?? Datos(), flujo, Ahora);
        return Expediente.Reproducir(null, eventos);
    }

    private static Expediente Abrir(FlujoParaExpediente flujo, DatosDeApertura? datos = null) => Abrir(flujo, out _, datos);

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private static Expediente Aprobar(Expediente e, Guid area, Revisor revisor) =>
        Aplicar(e, e.EmitirVeredicto(area, revisor, Veredicto.Aprobado, [], null, Ahora));

    private static Expediente Observar(Expediente e, Guid area, Revisor revisor) =>
        Aplicar(e, e.EmitirVeredicto(area, revisor, Veredicto.Observado, [Incompleto], "Falta el RUT.", Ahora));

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- etapas: paralelo y secuencia ----------

    [Fact]
    public void Sin_requisitos_pasa_directo_a_revision_y_todas_las_areas_de_la_etapa_reciben_el_expediente_a_la_vez()
    {
        var e = Abrir(EnParalelo(), out var eventos);

        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteAbierto", "FlujoSeleccionado", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        ((EtapaActivada)eventos[3]).Areas.ShouldBe([_a, _b]);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.Etapas.ShouldHaveSingleItem().Areas.ShouldAllBe(a => a.Estado == EstadoAreaExpediente.Pendiente && a.PendienteDesde == Ahora);
    }

    [Fact]
    public void Las_etapas_van_en_secuencia_y_la_siguiente_se_activa_al_aprobar_la_anterior()
    {
        var e = Abrir(Flujo(PoliticaDevolucion.Inmediata, false, new EtapaDelFlujo(1, [Area(_a, "A")]), new EtapaDelFlujo(2, [Area(_b, "B")])));
        e.BuscarArea(_b)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);
        Codigo(() => e.EmitirVeredicto(_b, _beto, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("area-no-activa");

        var eventos = e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VeredictoDeAreaEmitido", "EtapaAprobada", "EtapaActivada"]);
        e = Aplicar(e, eventos);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.BuscarArea(_b)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);

        Aprobar(e, _b, _beto).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Un_area_con_dos_aprobadores_necesita_dos_usuarios_distintos()
    {
        var e = Abrir(Flujo(PoliticaDevolucion.Inmediata, false, new EtapaDelFlujo(1, [Area(_a, "A", aprobadores: 2)])));

        e = Aprobar(e, _a, _ana);
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_a)!.Aprobaciones.ShouldBe(1);
        Codigo(() => e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("aprobador-repetido");

        e = Aprobar(e, _a, _beto);
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    // ---------- observaciones y reenvío ----------

    [Fact]
    public void Con_politica_inmediata_la_primera_observacion_devuelve_el_expediente_y_bloquea_los_veredictos()
    {
        var e = Abrir(EnParalelo());
        Codigo(() => e.EmitirVeredicto(_a, _ana, Veredicto.Observado, [], null, Ahora)).ShouldBe("motivo-requerido");
        Codigo(() => e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [Incompleto], null, Ahora)).ShouldBe("motivos-solo-al-observar");

        var eventos = e.EmitirVeredicto(_b, _beto, Veredicto.Observado, [Incompleto], "Falta el RUT.", Ahora);
        eventos.OfType<ExpedienteObservado>().ShouldHaveSingleItem().AreasQueObservaron.ShouldBe([_b]);
        e = Aplicar(e, eventos);

        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.ObservacionesVigentes.ShouldHaveSingleItem().Observacion.ShouldBe("Falta el RUT.");
        Codigo(() => e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("expediente-observado");
        // Rechazar sí se puede estando devuelto (FL-14), pero solo un área con ese permiso.
        Codigo(() => e.Rechazar(_b, _beto, [Incompleto], "No.", Ahora)).ShouldBe("area-no-puede-rechazar");
        Codigo(() => e.AprobarComoAdministrador(_a, "admin", "Urgente", Ahora)).ShouldBe("expediente-observado");
    }

    [Fact]
    public void Con_politica_al_cerrar_area_se_devuelve_cuando_todas_las_areas_se_pronunciaron()
    {
        var e = Observar(Abrir(EnParalelo(PoliticaDevolucion.AlCerrarArea)), _b, _beto);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        Codigo(() => e.EmitirVeredicto(_b, _carla, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("area-ya-se-pronuncio");

        e = Aprobar(e, _a, _ana);
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.ObservacionesVigentes.Count.ShouldBe(1);
    }

    [Fact]
    public void Al_reenviar_solo_las_areas_que_observaron_vuelven_a_revisar_y_las_aprobaciones_se_conservan()
    {
        var e = Aprobar(Abrir(EnParalelo(PoliticaDevolucion.AlCerrarArea)), _a, _ana);
        e = Observar(e, _b, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var reenvio = e.Reenviar(OrigenReenvio.Tercero, "Adjunto el RUT.", Ahora.AddHours(1));
        reenvio.OfType<ExpedienteReenviado>().ShouldHaveSingleItem().AreasReabiertas.ShouldBe([_b]);
        e = Aplicar(e, reenvio);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.Reenvios.ShouldBe(1);
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        var b = e.BuscarArea(_b)!;
        b.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        b.PendienteDesde.ShouldBe(Ahora.AddHours(1));
        b.Veredictos.ShouldHaveSingleItem().Superado.ShouldBeTrue();

        // Quien observó puede volver a decidir en la nueva ronda.
        Aprobar(e, _b, _beto).Estado.ShouldBe(EstadoExpediente.Aprobado);
        Codigo(() => e.Reenviar(OrigenReenvio.Tercero, null, Ahora)).ShouldBe("estado-invalido");
    }

    // ---------- rechazo ----------

    [Fact]
    public void Solo_un_area_que_puede_rechazar_lo_hace_y_el_rechazo_es_terminal()
    {
        var e = Abrir(EnParalelo(rechazaB: true));
        Codigo(() => e.Rechazar(_a, _ana, [Incompleto], "No.", Ahora)).ShouldBe("area-no-puede-rechazar");
        Codigo(() => e.Rechazar(_b, _beto, [], "No.", Ahora)).ShouldBe("motivo-requerido");
        Codigo(() => e.Rechazar(_b, _beto, [Incompleto], "  ", Ahora)).ShouldBe("justificacion-requerida");

        e = Aplicar(e, e.Rechazar(_b, _beto, [Incompleto], "Aparece en listas.", Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Rechazado);
        e.Rechazo!.Justificacion.ShouldBe("Aparece en listas.");
        Codigo(() => e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("estado-invalido");
        Codigo(() => e.AnularVeredicto(_a, null, "admin", "Revertir", Ahora)).ShouldBe("estado-invalido");
        Codigo(() => e.Anular("x", Ahora)).ShouldBe("estado-invalido");
    }

    // ---------- automáticas ----------

    [Fact]
    public void Un_area_automatica_aprueba_al_activarse_su_etapa_con_origen_sistema()
    {
        var e = Abrir(Flujo(PoliticaDevolucion.Inmediata, false, new EtapaDelFlujo(1, [Area(_a, "A"), Area(_b, "B", automatica: true)])));

        var b = e.BuscarArea(_b)!;
        b.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        b.Veredictos.ShouldHaveSingleItem().Origen.ShouldBe(OrigenVeredicto.Sistema);
        Codigo(() => e.EmitirVeredicto(_b, _beto, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("area-automatica");
        Aprobar(e, _a, _ana).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Si_todas_las_areas_son_automaticas_el_expediente_se_aprueba_al_abrirse()
    {
        var e = Abrir(Flujo(PoliticaDevolucion.Inmediata, false,
            new EtapaDelFlujo(1, [Area(_a, "A", automatica: true)]), new EtapaDelFlujo(2, [Area(_b, "B", automatica: true)])), out var eventos);

        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        eventos.OfType<EtapaAprobada>().Count().ShouldBe(2);
        eventos.Last().ShouldBeOfType<ExpedienteAprobado>();
    }

    // ---------- administrador ----------

    [Fact]
    public void El_administrador_aprueba_en_nombre_del_area_y_anula_veredictos_siempre_con_justificacion()
    {
        var e = Abrir(EnParalelo());
        Codigo(() => e.AprobarComoAdministrador(_a, "admin", " ", Ahora)).ShouldBe("justificacion-requerida");

        e = Aplicar(e, e.AprobarComoAdministrador(_a, "admin", "El área está de vacaciones.", Ahora));
        var a = e.BuscarArea(_a)!;
        a.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        a.Veredictos.ShouldHaveSingleItem().Origen.ShouldBe(OrigenVeredicto.Administrador);

        Codigo(() => e.AnularVeredicto(_a, null, "admin", null, Ahora)).ShouldBe("justificacion-requerida");
        e = Aplicar(e, e.AnularVeredicto(_a, null, "admin", "Se aprobó por error.", Ahora));
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_a)!.Veredictos.ShouldHaveSingleItem().Anulado.ShouldBeTrue();
        Codigo(() => e.AnularVeredicto(_a, null, "admin", "Otra vez", Ahora)).ShouldBe("veredicto-no-encontrado");
    }

    [Fact]
    public void Anular_la_unica_observacion_reanuda_la_revision_sin_esperar_reenvio()
    {
        var e = Aprobar(Observar(Abrir(EnParalelo(PoliticaDevolucion.AlCerrarArea)), _b, _beto), _a, _ana);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var eventos = e.AnularVeredicto(_b, null, "admin", "La observación no aplica.", Ahora);
        eventos.OfType<RevisionReanudada>().ShouldHaveSingleItem();
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.ObservacionesVigentes.ShouldBeEmpty();
        e.BuscarArea(_b)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_a)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);

        Codigo(() => e.Reenviar(OrigenReenvio.Integrador, null, Ahora)).ShouldBe("estado-invalido");
        Aprobar(e, _b, _carla).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Anular_una_de_dos_observaciones_mantiene_el_expediente_devuelto()
    {
        var e = Observar(Observar(Abrir(EnParalelo(PoliticaDevolucion.AlCerrarArea)), _a, _ana), _b, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        var eventos = e.AnularVeredicto(_b, null, "admin", "La observación no aplica.", Ahora);
        eventos.OfType<RevisionReanudada>().ShouldBeEmpty();
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.Observado);
        e.ObservacionesVigentes.ShouldHaveSingleItem();
    }

    [Fact]
    public void Un_area_con_permiso_rechaza_aunque_el_expediente_este_devuelto()
    {
        var e = Observar(Abrir(Flujo(PoliticaDevolucion.Inmediata, false,
            new EtapaDelFlujo(1, [Area(_a, "A"), Area(_b, "B", rechaza: true)]))), _a, _ana);
        e.Estado.ShouldBe(EstadoExpediente.Observado);

        e = Aplicar(e, e.Rechazar(_b, _beto, [Incompleto], "Aparece en listas.", Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Rechazado);
    }

    // ---------- conflicto de intereses y separación de funciones ----------

    [Fact]
    public void Un_revisor_con_la_identificacion_del_tercero_tiene_conflicto_de_intereses()
    {
        var e = Abrir(EnParalelo(rechazaB: true));
        var mismo = new Revisor(Guid.NewGuid(), ClaveTercero);

        Codigo(() => e.EmitirVeredicto(_a, mismo, Veredicto.Aprobado, [], null, Ahora)).ShouldBe("conflicto-de-intereses");
        Codigo(() => e.Rechazar(_b, mismo, [Incompleto], "No.", Ahora)).ShouldBe("conflicto-de-intereses");
        Should.NotThrow(() => e.EmitirVeredicto(_a, new Revisor(Guid.NewGuid(), "CO|CC|1020304050"), Veredicto.Aprobado, [], null, Ahora));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Con_separacion_de_funciones_quien_invito_o_abrio_no_decide(bool separacion, bool prohibido)
    {
        var invito = Guid.NewGuid();
        var abrio = Guid.NewGuid();
        var datos = Datos(new ActorDeExpediente(ActorTipo.Usuario, abrio.ToString()), [invito.ToString()]);
        var e = Abrir(Flujo(PoliticaDevolucion.Inmediata, separacion, new EtapaDelFlujo(1, [Area(_a, "A", aprobadores: 3)])), datos);

        foreach (var usuario in new[] { invito, abrio })
        {
            Action veredicto = () => e.EmitirVeredicto(_a, new Revisor(usuario, null), Veredicto.Aprobado, [], null, Ahora);
            if (prohibido)
                Codigo(veredicto).ShouldBe("separacion-de-funciones");
            else
                veredicto.ShouldNotThrow();
        }
    }

    // ---------- anulación, vínculo y folio ----------

    [Fact]
    public void Anular_cierra_el_expediente_abierto_con_motivo()
    {
        var e = Abrir(EnParalelo());
        Codigo(() => e.Anular(" ", Ahora)).ShouldBe("motivo-requerido");

        e = Aplicar(e, [e.Anular("Duplicado", Ahora)]);
        e.Estado.ShouldBe(EstadoExpediente.Anulado);
        e.EstaAbierto.ShouldBeFalse();
        Codigo(() => e.Anular("Otra vez", Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void El_vinculo_admite_un_solo_expediente_abierto_y_su_cierre_define_el_estado()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        var primero = Guid.NewGuid();

        vinculo.Apply(vinculo.VincularExpediente(primero, TipoExpediente.Inicial, "F-1", Ahora));
        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        vinculo.ExpedienteAbiertoId.ShouldBe(primero);
        Codigo(() => vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Inicial, "F-2", Ahora)).ShouldBe("expediente-abierto-existente");
        Codigo(() => vinculo.CerrarExpediente(Guid.NewGuid(), ResultadoExpediente.Aprobado, Ahora)).ShouldBe("expediente-no-abierto");

        // Rechazado ⇒ vuelve a Invitado (docs/03 §5.2) y admite un expediente nuevo (P-5-5).
        foreach (var evento in vinculo.CerrarExpediente(primero, ResultadoExpediente.Rechazado, Ahora))
            vinculo.Apply((ExpedienteCerrado)evento);
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        vinculo.ExpedienteAbiertoId.ShouldBeNull();

        var segundo = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(segundo, TipoExpediente.Inicial, "F-2", Ahora));
        var cierre = vinculo.CerrarExpediente(segundo, ResultadoExpediente.Aprobado, Ahora);
        cierre.Select(x => x.GetType().Name).ShouldBe(["ExpedienteCerrado", "VinculoActivado"]);
        vinculo.Apply((ExpedienteCerrado)cierre[0]);
        vinculo.Apply((VinculoActivado)cierre[1]);
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.TuvoAprobacion.ShouldBeTrue();
        Codigo(() => vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Inicial, "F-3", Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Un_vinculo_bloqueado_no_abre_expedientes_y_uno_terminado_suelta_el_abierto()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        vinculo.Apply(vinculo.Bloquear("Revisión", null, Ahora));
        Codigo(() => vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Inicial, "F-1", Ahora)).ShouldBe("vinculo-bloqueado");

        vinculo.Apply(vinculo.Desbloquear("Listo", Ahora));
        var abierto = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(abierto, TipoExpediente.Inicial, "F-1", Ahora));
        vinculo.Apply(vinculo.Anular("Fraude", Ahora));
        vinculo.ExpedienteAbiertoId.ShouldBeNull();
        vinculo.UltimoExpedienteId.ShouldBe(abierto);
        vinculo.UltimoResultado.ShouldBe(ResultadoExpediente.Anulado);
    }

    [Fact]
    public void La_aceptacion_sin_expediente_queda_como_apertura_pendiente()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora));
        vinculo.AperturaDeExpedientePendiente.ShouldBeFalse();

        vinculo.Apply(vinculo.AceptarInvitacion("a@b.co", Ahora));
        vinculo.AperturaDeExpedientePendiente.ShouldBeTrue();

        vinculo.Apply(vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Inicial, "F-1", Ahora));
        vinculo.AperturaDeExpedientePendiente.ShouldBeFalse();
    }

    [Fact]
    public void El_folio_lleva_prefijo_anio_y_secuencia_de_seis_digitos_sin_huecos()
    {
        var contador = ContadorDeFolio.Nuevo(Guid.NewGuid(), "E", 2026);
        var folios = Enumerable.Range(0, 3).Select(_ => Folios.DeExpediente("NORTE", contador.Anio, contador.Siguiente())).ToList();

        folios.ShouldBe(["NORTE-E-2026-000001", "NORTE-E-2026-000002", "NORTE-E-2026-000003"]);
        Folios.DeExpediente("ACME1", 2027, 418).ShouldBe("ACME1-E-2027-000418");
        Should.Throw<ArgumentOutOfRangeException>(() => Folios.DeExpediente("NORTE", 2026, 0));
    }
}
