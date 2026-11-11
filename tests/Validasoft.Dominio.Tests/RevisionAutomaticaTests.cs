using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Revision;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Revisión automática con IA (ADR-0012, ítems 13 y 14): la tabla de decisión del motor (Manual, Asistido y Automático, con sus
/// excepciones, el tope mensual y el muestreo), la configuración por empresa y el expediente (registro, cascada hasta el cierre,
/// confirmar o revertir un muestreo).
/// </summary>
public class RevisionAutomaticaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Documento = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ---------- motor ----------

    private static EntradaDeRevision Limpia() =>
        new(Documento, 1, "RUT", 0, true, false, new AnalisisParaRevisar(0.97m, true, 0, "RUT", 0), false, false, false);

    private static PoliticaDeRevision Automatica(int muestreo = 0, int aprobadas = 0, int? tope = null, string[]? tipos = null) =>
        new(NivelDeRevisionAutomatica.Automatico, 0.95m, muestreo, tipos ?? ["RUT"], aprobadas, tope);

    private static PoliticaDeRevision Asistida() =>
        new(NivelDeRevisionAutomatica.Asistido, 0.95m, 0, ["RUT"], 0, null);

    public static IEnumerable<object[]> TablaDelMotor()
    {
        // (nombre, entrada, política, resultado esperado, sugerencia, códigos de motivo)
        var limpia = Limpia();
        var analisis = limpia.Analisis!;
        object[] Fila(string nombre, EntradaDeRevision e, PoliticaDeRevision p, ResultadoDeRevision r, SugerenciaDeRevision? s, params string[] motivos) =>
            [nombre, e, p, r, s!, motivos];

        // Asistido: nunca emite veredictos.
        yield return Fila("asistido sin motivos sugiere aprobar", limpia, Asistida(), ResultadoDeRevision.Sugerida, SugerenciaDeRevision.Aprobar);
        yield return Fila("asistido con baja confianza sugiere revisar", limpia with { Analisis = analisis with { Confianza = 0.80m } }, Asistida(),
            ResultadoDeRevision.Sugerida, SugerenciaDeRevision.Revisar, "confianza-baja");
        yield return Fila("asistido sin análisis sugiere revisar", limpia with { Analisis = null }, Asistida(), ResultadoDeRevision.Sugerida,
            SugerenciaDeRevision.Revisar, "sin-analisis-ia");
        yield return Fila("asistido no mira tipos ni áreas autorizados", limpia with { TipoCanonico = "CEDULA", AreaAutorizada = false, Analisis = analisis with { TipoDetectado = "CEDULA" } },
            Asistida(), ResultadoDeRevision.Sugerida, SugerenciaDeRevision.Aprobar);

        // Automático sin motivos.
        yield return Fila("automático limpio aprueba", limpia, Automatica(), ResultadoDeRevision.AprobadaAutomaticamente, null);
        yield return Fila("automático con muestreo 100 % aprueba y queda por confirmar", limpia, Automatica(muestreo: 100), ResultadoDeRevision.AprobadaConMuestreo, null);
        yield return Fila("la confianza igual al umbral no es baja", limpia with { Analisis = analisis with { Confianza = 0.95m } }, Automatica(),
            ResultadoDeRevision.AprobadaAutomaticamente, null);

        // Fuera del alcance de la revisión automática: se revisa como siempre.
        yield return Fila("tipo no autorizado", limpia with { TipoCanonico = "CEDULA" }, Automatica(), ResultadoDeRevision.SinAlcance, null, "tipo-no-autorizado");
        yield return Fila("tipo sin canónico", limpia with { TipoCanonico = null }, Automatica(), ResultadoDeRevision.SinAlcance, null, "tipo-no-autorizado");
        yield return Fila("área no autorizada", limpia with { AreaAutorizada = false }, Automatica(), ResultadoDeRevision.SinAlcance, null, "area-no-autorizada");
        yield return Fila("área que exige varias aprobaciones", limpia with { AreaExigeVariasAprobaciones = true }, Automatica(), ResultadoDeRevision.SinAlcance, null,
            "area-con-varias-aprobaciones");

        // Excepciones (van a la bandeja de una persona).
        yield return Fila("sin análisis de IA", limpia with { Analisis = null }, Automatica(), ResultadoDeRevision.Excepcion, null, "sin-analisis-ia");
        yield return Fila("confianza bajo el umbral", limpia with { Analisis = analisis with { Confianza = 0.90m } }, Automatica(), ResultadoDeRevision.Excepcion, null,
            "confianza-baja");
        yield return Fila("ilegible", limpia with { Analisis = analisis with { Legible = false } }, Automatica(), ResultadoDeRevision.Excepcion, null, "ilegible");
        yield return Fila("señales de manipulación", limpia with { Analisis = analisis with { Alertas = 2 } }, Automatica(), ResultadoDeRevision.Excepcion, null,
            "alertas-de-manipulacion");
        yield return Fila("tipo detectado distinto", limpia with { Analisis = analisis with { TipoDetectado = "CAMARA_COMERCIO" } }, Automatica(),
            ResultadoDeRevision.Excepcion, null, "tipo-detectado-distinto");
        yield return Fila("campo crítico cambiado", limpia with { Analisis = analisis with { CamposCriticosCambiados = 1 } }, Automatica(),
            ResultadoDeRevision.Excepcion, null, "campo-critico-cambiado");
        yield return Fila("el cruce con la ficha aún no existe", limpia with { CrucePendiente = true }, Automatica(), ResultadoDeRevision.Excepcion, null, "cruce-pendiente");
        yield return Fila("discrepancia crítica con la ficha (F5)", limpia with { DiscrepanciaCritica = true, ConDiscrepancias = true }, Automatica(),
            ResultadoDeRevision.Excepcion, null, "discrepancia-critica-ficha");
        yield return Fila("discrepancias con la ficha (F5)", limpia with { ConDiscrepancias = true }, Automatica(), ResultadoDeRevision.Excepcion, null,
            "discrepancias-con-la-ficha");
        yield return Fila("versión reenviada tras una observación", limpia with { Reenvios = 1 }, Automatica(), ResultadoDeRevision.Excepcion, null, "version-reenviada");
        yield return Fila("varios motivos, en orden", limpia with
            {
                Analisis = analisis with { Confianza = 0.5m, Legible = false, Alertas = 1 },
                DiscrepanciaCritica = true,
                Reenvios = 2
            }, Automatica(), ResultadoDeRevision.Excepcion, null,
            "confianza-baja", "ilegible", "alertas-de-manipulacion", "discrepancia-critica-ficha", "version-reenviada");

        // Presupuesto: el tope mensual de aprobaciones automáticas.
        yield return Fila("tope mensual alcanzado", limpia, Automatica(aprobadas: 5, tope: 5), ResultadoDeRevision.Excepcion, null, "sin-presupuesto");
        yield return Fila("tope mensual superado", limpia, Automatica(aprobadas: 9, tope: 5), ResultadoDeRevision.Excepcion, null, "sin-presupuesto");
        yield return Fila("bajo el tope mensual aprueba", limpia, Automatica(aprobadas: 4, tope: 5), ResultadoDeRevision.AprobadaAutomaticamente, null);
        yield return Fila("el tope no cambia lo que ya es excepción", limpia with { Analisis = null }, Automatica(aprobadas: 5, tope: 5), ResultadoDeRevision.Excepcion, null,
            "sin-analisis-ia");
    }

    [Theory]
    [MemberData(nameof(TablaDelMotor))]
    public void El_motor_decide_segun_la_tabla(string nombre, EntradaDeRevision entrada, PoliticaDeRevision politica, ResultadoDeRevision resultado,
        SugerenciaDeRevision? sugerencia, string[] motivos)
    {
        var decision = MotorDeRevision.Decidir(entrada, politica).ShouldNotBeNull(nombre);

        decision.Resultado.ShouldBe(resultado, nombre);
        decision.Sugerencia.ShouldBe(sugerencia, nombre);
        decision.Motivos.Select(m => m.Codigo).ShouldBe(motivos, nombre);
        decision.Motivos.ShouldAllBe(m => m.Texto.Length > 10, nombre);
        decision.Aprueba.ShouldBe(resultado is ResultadoDeRevision.AprobadaAutomaticamente or ResultadoDeRevision.AprobadaConMuestreo, nombre);
    }

    [Fact]
    public void En_Manual_el_motor_no_hace_nada()
    {
        MotorDeRevision.Decidir(Limpia(), Automatica() with { Nivel = NivelDeRevisionAutomatica.Manual }).ShouldBeNull();
    }

    [Fact]
    public void Los_tipos_autorizados_no_distinguen_mayusculas()
    {
        MotorDeRevision.Decidir(Limpia(), Automatica(tipos: ["rut"]))!.Resultado.ShouldBe(ResultadoDeRevision.AprobadaAutomaticamente);
    }

    [Fact]
    public void El_texto_de_la_confianza_baja_dice_la_confianza_y_el_umbral()
    {
        var decision = MotorDeRevision.Decidir(Limpia() with { Analisis = new AnalisisParaRevisar(0.8m, true, 0, "RUT", 0) }, Automatica())!;

        decision.Motivos.Single().Texto.ShouldBe("La confianza del análisis (80 %) está bajo el umbral de la empresa (95 %).");
        decision.Confianza.ShouldBe(0.8m);
    }

    [Fact]
    public void El_muestreo_es_determinista_y_respeta_el_porcentaje()
    {
        MotorDeRevision.EnMuestra(Documento, 1, 0).ShouldBeFalse();
        MotorDeRevision.EnMuestra(Documento, 1, 100).ShouldBeTrue();
        MotorDeRevision.EnMuestra(Documento, 3, 37).ShouldBe(MotorDeRevision.EnMuestra(Documento, 3, 37));

        var muestreadas = Enumerable.Range(0, 5000).Count(i => MotorDeRevision.EnMuestra(Guid.CreateVersion7(), 1 + i % 3, 10));
        muestreadas.ShouldBeInRange(350, 650); // ~10 %
    }

    // ---------- configuración ----------

    [Fact]
    public void Sin_configurar_la_empresa_esta_en_Manual_con_los_valores_por_omision()
    {
        var c = RevisionAutomaticaEmpresa.PorDefecto(Guid.NewGuid());

        (c.Nivel, c.UmbralConfianza, c.PorcentajeMuestreo, c.TopeMensualAprobaciones, c.EsperaIaMinutos).ShouldBe((NivelDeRevisionAutomatica.Manual, 0.95m, 10, null, 10));
        c.TiposAutorizados.ShouldBeEmpty();
    }

    [Fact]
    public void Actualizar_normaliza_los_tipos_y_guarda_todo()
    {
        var area = Guid.NewGuid();
        var c = RevisionAutomaticaEmpresa.PorDefecto(Guid.NewGuid());

        c.Actualizar(new CambiosDeRevisionAutomatica("automatico", 0.9m, 25, 100, [" rut ", "RUT", "cedula"], [area, area], 15), Ahora);

        (c.Nivel, c.UmbralConfianza, c.PorcentajeMuestreo, c.TopeMensualAprobaciones, c.EsperaIaMinutos, c.ActualizadaEn).ShouldBe(
            (NivelDeRevisionAutomatica.Automatico, 0.9m, 25, 100, 15, Ahora));
        c.TiposAutorizados.ShouldBe(["CEDULA", "RUT"]);
        c.AreasAutorizadas.ShouldBe([area]);
    }

    public static IEnumerable<object[]> ConfiguracionesInvalidas()
    {
        var area = Guid.NewGuid();
        yield return ["nivel-invalido", new CambiosDeRevisionAutomatica("Total", null, null, null, null, null, null)];
        yield return ["umbral-invalido", new CambiosDeRevisionAutomatica("Asistido", 0.80m, null, null, null, null, null)];
        yield return ["umbral-invalido", new CambiosDeRevisionAutomatica("Asistido", 1.01m, null, null, null, null, null)];
        yield return ["muestreo-invalido", new CambiosDeRevisionAutomatica("Asistido", null, 101, null, null, null, null)];
        yield return ["muestreo-invalido", new CambiosDeRevisionAutomatica("Asistido", null, -1, null, null, null, null)];
        yield return ["tope-invalido", new CambiosDeRevisionAutomatica("Asistido", null, null, 0, null, null, null)];
        yield return ["espera-invalida", new CambiosDeRevisionAutomatica("Asistido", null, null, null, null, null, 0)];
        yield return ["automatico-sin-alcance", new CambiosDeRevisionAutomatica("Automatico", null, null, null, null, [area], null)];
        yield return ["automatico-sin-alcance", new CambiosDeRevisionAutomatica("Automatico", null, null, null, ["RUT"], null, null)];
    }

    [Theory]
    [MemberData(nameof(ConfiguracionesInvalidas))]
    public void La_configuracion_invalida_se_rechaza_con_su_codigo(string codigo, CambiosDeRevisionAutomatica cambios)
    {
        var c = RevisionAutomaticaEmpresa.PorDefecto(Guid.NewGuid());

        Should.Throw<ReglaDeNegocioException>(() => c.Actualizar(cambios, Ahora)).Codigo.ShouldBe(codigo);
        c.Nivel.ShouldBe(NivelDeRevisionAutomatica.Manual); // nada quedó a medias
    }

    [Fact]
    public void El_umbral_efectivo_nunca_baja_del_del_plan()
    {
        var c = RevisionAutomaticaEmpresa.PorDefecto(Guid.NewGuid());
        c.Actualizar(new CambiosDeRevisionAutomatica("Asistido", 0.90m, null, null, null, null, null), Ahora);

        c.UmbralEfectivo(0.85m).ShouldBe(0.90m);
        c.UmbralEfectivo(0.93m).ShouldBe(0.93m);
    }

    [Fact]
    public void El_uso_cuenta_aprobaciones_muestreo_excepciones_y_sugerencias_y_respeta_el_tope()
    {
        var uso = UsoRevisionAutomatica.Nuevo(Guid.NewGuid(), "2026-11");
        uso.Registrar(ResultadoDeRevision.AprobadaAutomaticamente);
        uso.Registrar(ResultadoDeRevision.AprobadaConMuestreo);
        uso.Registrar(ResultadoDeRevision.Excepcion);
        uso.Registrar(ResultadoDeRevision.Sugerida);
        uso.Registrar(ResultadoDeRevision.SinAlcance);

        (uso.Aprobadas, uso.Muestreadas, uso.Excepciones, uso.Sugerencias).ShouldBe((2, 1, 1, 1));
        uso.Cabe(null).ShouldBeTrue();
        uso.Cabe(3).ShouldBeTrue();
        uso.Cabe(2).ShouldBeFalse();
        UsoRevisionAutomatica.MesDe(new DateOnly(2026, 11, 9)).ShouldBe("2026-11");
    }

    // ---------- expediente ----------

    private readonly Guid _compras = Guid.NewGuid();
    private readonly Guid _cumplimiento = Guid.NewGuid();
    private readonly Guid _rut = Guid.NewGuid();
    private readonly Guid _camara = Guid.NewGuid();
    private readonly Guid _general = Guid.NewGuid();
    private readonly Guid _docRut = Guid.NewGuid();
    private readonly Guid _docCamara = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);

    private static VersionDeRequisito V(Guid documento, int n) => new(documento, n, $"sha-{documento:N}-{n}", Ahora);

    private RequisitoCalculado Rut() =>
        new(_rut, Guid.NewGuid(), "RUT", "Registro Único Tributario", "RUT", AmbitoDocumento.Global, true, [], null, V(_docRut, 1));

    private RequisitoCalculado Camara() =>
        new(_camara, Guid.NewGuid(), "CAMARA_COMERCIO", "Cámara de comercio", "CAMARA_COMERCIO", AmbitoDocumento.Global, true, [_cumplimiento], null, V(_docCamara, 1));

    private RequisitoCalculado General() =>
        new(_general, Guid.NewGuid(), "FICHA_GENERAL", "Información general", null, AmbitoDocumento.Global, true, [_cumplimiento], null, V(Guid.NewGuid(), 1),
            TipoRequisito: TipoDeRequisito.Seccion, Seccion: "FICHA_GENERAL");

    private FlujoParaExpediente Flujo() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "PROV", "Proveedores", 1, OrigenFlujo.PorDefecto,
            [
                new EtapaDelFlujo(1,
                [
                    new AreaDelFlujo(_compras, "COMPRAS", "Compras", 1, false, false, null),
                    new AreaDelFlujo(_cumplimiento, "CUMPLIMIENTO", "Cumplimiento", 1, true, false, null)
                ])
            ], PoliticaDevolucion.Inmediata, false);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    /// <summary>En revisión con el RUT (Compras y Cumplimiento), la Cámara (Cumplimiento) y la sección general (Cumplimiento).</summary>
    private Expediente EnRevision() =>
        Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(), Ahora, [Rut(), Camara(), General()]));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private static PropuestaDeRevision Propuesta(Guid requisito, Guid area, ResultadoDeRevision resultado, int version = 1, params string[] motivos) =>
        new(requisito, area, version, new DecisionDeRevision(resultado, resultado == ResultadoDeRevision.Sugerida ? SugerenciaDeRevision.Aprobar : null,
            [.. motivos.Select(MotivosDeRevision.De)], 0.97m));

    private static Expediente Registrar(Expediente e, NivelDeRevisionAutomatica nivel, params PropuestaDeRevision[] propuestas) =>
        Aplicar(e, e.RegistrarRevisionesAutomaticas(propuestas, nivel, Ahora));

    [Fact]
    public void Las_candidatas_son_los_documentos_de_la_etapa_activa_sin_decidir_y_nunca_las_secciones()
    {
        var e = EnRevision();

        e.PorRevisarAutomaticamente.ShouldBeTrue();
        e.CandidatasDeRevisionAutomatica().Select(c => (c.Requisito.Id, c.Area.AreaId, c.Version)).ShouldBe(
            [(_rut, _compras, 1), (_rut, _cumplimiento, 1), (_camara, _cumplimiento, 1)], ignoreOrder: true);
        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
    }

    [Fact]
    public void Si_el_expediente_no_esta_en_revision_no_hay_candidatas()
    {
        var abierto = Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(), Ahora, [Rut() with { Version = null }, Camara()]));

        abierto.Estado.ShouldBe(EstadoExpediente.Abierto);
        abierto.PorRevisarAutomaticamente.ShouldBeFalse();
        abierto.CandidatasDeRevisionAutomatica().ShouldBeEmpty();
    }

    [Fact]
    public void Aprobar_solo_los_documentos_que_si_se_revisan_deja_la_seccion_a_una_persona_y_la_cascada_hace_el_resto()
    {
        var e = EnRevision();

        e = Registrar(e, NivelDeRevisionAutomatica.Automatico,
            Propuesta(_rut, _compras, ResultadoDeRevision.AprobadaAutomaticamente),
            Propuesta(_rut, _cumplimiento, ResultadoDeRevision.AprobadaAutomaticamente),
            Propuesta(_camara, _cumplimiento, ResultadoDeRevision.AprobadaAutomaticamente));

        // Compras revisaba solo el RUT: queda aprobada y su etapa espera a Cumplimiento (la sección general, de una persona).
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        var veredictos = e.BuscarRequisito(_rut)!.Veredictos.Where(v => v.Vigente).ToList();
        veredictos.Count.ShouldBe(2);
        veredictos.ShouldAllBe(v => v.Origen == OrigenVeredicto.Sistema && v.RevisorId == Expediente.RevisorAutomatico && v.Veredicto == Veredicto.Aprobado &&
                                    v.SobreVersion == 1 && v.Justificacion!.StartsWith("Aprobado por la revisión automática (confianza 97 %)"));
        e.BuscarRequisito(_general)!.Veredictos.ShouldBeEmpty();
        e.PorRevisarAutomaticamente.ShouldBeFalse(); // la sección no es candidata: nada más por revisar

        // Una persona aprueba la sección y el expediente se cierra solo.
        e = Aplicar(e, e.EmitirVeredictoDeRequisito(_general, _cumplimiento, _ana, Veredicto.Aprobado, 1, [], null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Registrar_dos_veces_o_con_una_version_superada_no_duplica_ni_emite_nada()
    {
        var e = EnRevision();
        var propuesta = Propuesta(_rut, _compras, ResultadoDeRevision.AprobadaAutomaticamente);
        e = Registrar(e, NivelDeRevisionAutomatica.Automatico, propuesta);

        e.RegistrarRevisionesAutomaticas([propuesta], NivelDeRevisionAutomatica.Automatico, Ahora).ShouldBeEmpty();
        e.RegistrarRevisionesAutomaticas([Propuesta(_camara, _cumplimiento, ResultadoDeRevision.AprobadaAutomaticamente, version: 7)],
            NivelDeRevisionAutomatica.Automatico, Ahora).ShouldBeEmpty();
        e.RegistrarRevisionesAutomaticas([Propuesta(_general, _cumplimiento, ResultadoDeRevision.AprobadaAutomaticamente)],
            NivelDeRevisionAutomatica.Automatico, Ahora).ShouldBeEmpty(); // una sección no es candidata
        e.RegistrarRevisionesAutomaticas([], NivelDeRevisionAutomatica.Automatico, Ahora).ShouldBeEmpty();
        e.BuscarRequisito(_rut)!.Revisiones.ShouldHaveSingleItem();
    }

    [Fact]
    public void Una_sugerencia_no_emite_veredictos_y_una_sin_alcance_solo_se_anota()
    {
        var e = EnRevision();

        var eventos = e.RegistrarRevisionesAutomaticas(
            [Propuesta(_rut, _compras, ResultadoDeRevision.Sugerida), Propuesta(_camara, _cumplimiento, ResultadoDeRevision.SinAlcance, motivos: "tipo-no-autorizado")],
            NivelDeRevisionAutomatica.Asistido, Ahora);

        eventos.ShouldAllBe(x => x is RevisionAutomaticaRegistrada);
        e = Aplicar(e, eventos);
        e.BuscarRequisito(_rut)!.Veredictos.ShouldBeEmpty();
        e.BuscarRequisito(_rut)!.Revisiones.Single().ShouldSatisfyAllConditions(r => r.Resultado.ShouldBe(ResultadoDeRevision.Sugerida),
            r => r.Sugerencia.ShouldBe(SugerenciaDeRevision.Aprobar), r => r.Nivel.ShouldBe(NivelDeRevisionAutomatica.Asistido));
        e.ExcepcionesDeRevisionPendientes.ShouldBe(0); // ni la sugerencia ni «sin alcance» piden nada a nadie
        e.ConRevisionAutomatica.ShouldBeFalse();
        e.CandidatasDeRevisionAutomatica().Select(c => (c.Requisito.Id, c.Area.AreaId)).ShouldBe([(_rut, _cumplimiento)]);
    }

    [Fact]
    public void Una_excepcion_queda_abierta_hasta_que_una_persona_decide_el_documento_y_no_se_vuelve_a_evaluar()
    {
        var e = EnRevision();

        e = Registrar(e, NivelDeRevisionAutomatica.Automatico,
            Propuesta(_camara, _cumplimiento, ResultadoDeRevision.Excepcion, motivos: ["confianza-baja", "ilegible"]));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(1);
        e.ConRevisionAutomatica.ShouldBeTrue();
        e.BuscarRequisito(_camara)!.Veredictos.ShouldBeEmpty();
        e.CandidatasDeRevisionAutomatica().ShouldNotContain(c => c.Requisito.Id == _camara);
        var revision = e.BuscarRequisito(_camara)!.Revisiones.Single();
        revision.Motivos.Select(m => m.Codigo).ShouldBe(["confianza-baja", "ilegible"]);
        e.RevisionAbierta(revision, e.BuscarRequisito(_camara)!).ShouldBeTrue();

        // Una persona la aprueba: la excepción se cierra sola.
        e = Aplicar(e, e.EmitirVeredictoDeRequisito(_camara, _cumplimiento, _ana, Veredicto.Aprobado, 1, [], null, Ahora));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
        e.ConRevisionAutomatica.ShouldBeTrue(); // sigue en el histórico de la bandeja
        e.RevisionAbierta(revision, e.BuscarRequisito(_camara)!).ShouldBeFalse();
    }

    [Fact]
    public void Una_version_nueva_cierra_la_excepcion_y_el_documento_vuelve_a_ser_candidato()
    {
        var e = EnRevision();
        e = Registrar(e, NivelDeRevisionAutomatica.Automatico, Propuesta(_camara, _cumplimiento, ResultadoDeRevision.Excepcion, motivos: "sin-analisis-ia"));
        e.ExcepcionesDeRevisionPendientes.ShouldBe(1);

        e = Aplicar(e, e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_camara] = V(_docCamara, 2) }, Ahora));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
        e.CandidatasDeRevisionAutomatica().ShouldContain(c => c.Requisito.Id == _camara && c.Version == 2);
        e.PorRevisarAutomaticamente.ShouldBeTrue();
    }

    private Expediente ConMuestreoAprobado(out Guid veredictoId)
    {
        var e = EnRevision();
        e = Registrar(e, NivelDeRevisionAutomatica.Automatico, Propuesta(_rut, _compras, ResultadoDeRevision.AprobadaConMuestreo));
        veredictoId = e.BuscarRequisito(_rut)!.Revisiones.Single().VeredictoId!.Value;
        return e;
    }

    [Fact]
    public void Un_muestreo_queda_pendiente_y_confirmarlo_lo_cierra_sin_tocar_el_veredicto()
    {
        var e = ConMuestreoAprobado(out var veredictoId);
        e.ExcepcionesDeRevisionPendientes.ShouldBe(1);
        e.BuscarRequisito(_rut)!.Veredictos.Single().Id.ShouldBe(veredictoId);

        e = Aplicar(e, e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Confirmada, _ana, null, Ahora));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
        var revision = e.BuscarRequisito(_rut)!.Revisiones.Single();
        revision.Resolucion.ShouldNotBeNull().ShouldSatisfyAllConditions(r => r.Decision.ShouldBe(DecisionHumanaDeRevision.Confirmada),
            r => r.Por.ShouldBe(_ana.UsuarioId.ToString()));
        e.BuscarRequisito(_rut)!.Veredictos.Single().Vigente.ShouldBeTrue();
        Codigo(() => e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Confirmada, _ana, null, Ahora)).ShouldBe("revision-ya-resuelta");
    }

    [Fact]
    public void Revertir_un_muestreo_anula_el_veredicto_del_sistema_y_devuelve_el_documento_a_una_persona()
    {
        var e = ConMuestreoAprobado(out var veredictoId);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);

        Codigo(() => e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Revertida, _ana, "  ", Ahora)).ShouldBe("justificacion-requerida");
        e = Aplicar(e, e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Revertida, _ana, "El NIT no es el del tercero.", Ahora));

        var requisito = e.BuscarRequisito(_rut)!;
        var veredicto = requisito.Veredictos.Single(v => v.Id == veredictoId);
        (veredicto.Vigente, veredicto.Anulado, veredicto.AnuladoPor).ShouldBe((false, true, _ana.UsuarioId.ToString()));
        veredicto.JustificacionAnulacion.ShouldBe("Revertido tras el muestreo: El NIT no es el del tercero.");
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        requisito.Revisiones.Single().Resolucion!.Decision.ShouldBe(DecisionHumanaDeRevision.Revertida);
        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
        e.CandidatasDeRevisionAutomatica().ShouldNotContain(c => c.Requisito.Id == _rut && c.Area.AreaId == _compras); // lo decide una persona
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        Codigo(() => e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Revertida, _beto, "otra vez", Ahora)).ShouldBe("revision-ya-resuelta");
    }

    [Fact]
    public void Con_el_expediente_cerrado_se_confirma_pero_no_se_revierte()
    {
        var e = EnRevision();
        e = Registrar(e, NivelDeRevisionAutomatica.Automatico,
            Propuesta(_rut, _compras, ResultadoDeRevision.AprobadaConMuestreo),
            Propuesta(_rut, _cumplimiento, ResultadoDeRevision.AprobadaConMuestreo),
            Propuesta(_camara, _cumplimiento, ResultadoDeRevision.AprobadaAutomaticamente));
        e = Aplicar(e, e.EmitirVeredictoDeRequisito(_general, _cumplimiento, _ana, Veredicto.Aprobado, 1, [], null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.ExcepcionesDeRevisionPendientes.ShouldBe(2); // los muestreos siguen pidiendo confirmación aunque el expediente ya cerró

        Codigo(() => e.ResolverRevisionAutomatica(_rut, _cumplimiento, DecisionHumanaDeRevision.Revertida, _beto, "Debió ir a una persona.", Ahora)).ShouldBe("estado-invalido");
        e = Aplicar(e, e.ResolverRevisionAutomatica(_rut, _cumplimiento, DecisionHumanaDeRevision.Confirmada, _beto, null, Ahora));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(1);
    }

    [Fact]
    public void Resolver_lo_que_no_es_un_muestreo_pendiente_falla_con_su_codigo()
    {
        var e = EnRevision();
        e = Registrar(e, NivelDeRevisionAutomatica.Automatico, Propuesta(_camara, _cumplimiento, ResultadoDeRevision.Excepcion, motivos: "ilegible"));

        Codigo(() => e.ResolverRevisionAutomatica(_camara, _cumplimiento, DecisionHumanaDeRevision.Confirmada, _ana, null, Ahora)).ShouldBe("revision-no-encontrada");
        Codigo(() => e.ResolverRevisionAutomatica(Guid.NewGuid(), _cumplimiento, DecisionHumanaDeRevision.Confirmada, _ana, null, Ahora)).ShouldBe("requisito-no-encontrado");
    }

    [Fact]
    public void El_conflicto_de_intereses_impide_resolver_un_muestreo()
    {
        var e = ConMuestreoAprobado(out _);

        Codigo(() => e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Confirmada, new Revisor(Guid.NewGuid(), "CO|NIT|900123456"), null, Ahora))
            .ShouldBe("conflicto-de-intereses");
    }

    [Fact]
    public void Un_muestreo_cuyo_veredicto_ya_no_esta_vigente_no_se_revierte_y_deja_de_pedir_confirmacion()
    {
        var e = ConMuestreoAprobado(out _);

        // Llega una versión nueva: invalida el veredicto del sistema.
        e = Aplicar(e, e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 2) }, Ahora));

        e.ExcepcionesDeRevisionPendientes.ShouldBe(0);
        Codigo(() => e.ResolverRevisionAutomatica(_rut, _compras, DecisionHumanaDeRevision.Revertida, _ana, "x", Ahora)).ShouldBe("veredicto-no-vigente");
    }

    [Fact]
    public void Un_requisito_eximido_o_ya_decidido_por_una_persona_no_es_candidato()
    {
        var e = EnRevision();
        e = Aplicar(e, e.EmitirVeredictoDeRequisito(_rut, _compras, _ana, Veredicto.Aprobado, 1, [], null, Ahora));
        e = Aplicar(e, e.EximirRequisito(_camara, "admin", "No aplica a este tercero.", Ahora));

        e.CandidatasDeRevisionAutomatica().Select(c => (c.Requisito.Id, c.Area.AreaId)).ShouldBe([(_rut, _cumplimiento)]);
    }
}
