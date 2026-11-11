using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Cumplimiento;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Cumplimiento completo (H7, CU-15…CU-18): la matriz de riesgo configurable por empresa (puntaje, nivel, pisos, debida
/// diligencia), los archivos del oficial y la consulta de listas hecha por fuera, el rechazo directo ante un hallazgo y la
/// expiración de las listas cuando cambian las personas relacionadas.
/// </summary>
public class CumplimientoCompletoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.FromHours(-5));
    private const string ClaveTercero = "CO|NIT|900123456";
    private const string ClaveRepresentante = "CO|CC|1020304050";
    private const string ClaveNueva = "CO|CC|55667788";

    private readonly Guid _a = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _oficial = new(Guid.NewGuid(), "CO|CC|79000001");

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- Matriz de riesgo ----------

    private static MatrizDeRiesgoEmpresa Matriz() => MatrizDeRiesgoEmpresa.PorDefecto(Guid.NewGuid());

    private static IReadOnlyDictionary<string, string> Automaticos(string jurisdiccion = FactoresAutomaticos.Nacional,
        string listas = FactoresAutomaticos.SinCoincidencias, string pep = FactoresAutomaticos.SinPep) =>
        new Dictionary<string, string>
        {
            [FactoresAutomaticos.Jurisdiccion] = jurisdiccion,
            [FactoresAutomaticos.CoincidenciasEnListas] = listas,
            [FactoresAutomaticos.Pep] = pep
        };

    private static IReadOnlyList<RespuestaDeFactor> Manuales(string sector, string operacion) =>
        [new RespuestaDeFactor("sector", sector), new RespuestaDeFactor("operacion", operacion)];

    public static TheoryData<string, string, string, string, string, decimal, int, string?> CasosDeMatriz => new()
    {
        // jurisdicción, listas, pep, sector, operación → puntaje, nivel, forzado por
        { FactoresAutomaticos.Nacional, FactoresAutomaticos.SinCoincidencias, FactoresAutomaticos.SinPep, "bajo", "bajo", 0m, 1, null },
        { FactoresAutomaticos.Nacional, FactoresAutomaticos.SinCoincidencias, FactoresAutomaticos.SinPep, "medio", "medio", 17.5m, 1, null },
        { FactoresAutomaticos.Nacional, FactoresAutomaticos.Descartadas, FactoresAutomaticos.SinPep, "medio", "medio", 26.5m, 1, null },
        { FactoresAutomaticos.Extranjero, FactoresAutomaticos.Descartadas, FactoresAutomaticos.SinPep, "medio", "alto", 44.5m, 2, null },
        { FactoresAutomaticos.Extranjero, FactoresAutomaticos.SinCoincidencias, FactoresAutomaticos.ConPep, "alto", "alto", 61.5m, 2, null },
        { FactoresAutomaticos.Extranjero, FactoresAutomaticos.PorAnalizar, FactoresAutomaticos.ConPep, "alto", "alto", 82.5m, 3, null },
        // Un piso sube el nivel aunque el puntaje sea bajo: con PEP, nunca menos de Medio.
        { FactoresAutomaticos.Nacional, FactoresAutomaticos.SinCoincidencias, FactoresAutomaticos.ConPep, "bajo", "bajo", 16m, 2, "Personas expuestas políticamente (PEP): Con PEP entre sus personas" },
        // Un piso manda aunque el puntaje sea bajo: una coincidencia confirmada ⇒ Alto.
        { FactoresAutomaticos.Nacional, FactoresAutomaticos.Confirmadas, FactoresAutomaticos.SinPep, "bajo", "bajo", 30m, 3, "Coincidencias en listas restrictivas: Coincidencia confirmada" },
    };

    [Theory]
    [MemberData(nameof(CasosDeMatriz))]
    public void La_matriz_por_defecto_da_el_puntaje_el_nivel_y_respeta_los_pisos(string jurisdiccion, string listas, string pep, string sector, string operacion,
        decimal puntaje, int nivel, string? forzadoPor)
    {
        var resultado = Matriz().Calcular(Manuales(sector, operacion), Automaticos(jurisdiccion, listas, pep));

        resultado.Puntaje.ShouldBe(puntaje);
        resultado.Nivel.ShouldBe(nivel);
        resultado.ForzadoPor.ShouldBe(forzadoPor);
        resultado.Desglose.Count.ShouldBe(5);
    }

    [Theory]
    [InlineData(1, DebidaDiligencia.Simple, 24)]
    [InlineData(2, DebidaDiligencia.Ordinaria, 12)]
    [InlineData(3, DebidaDiligencia.Ampliada, 6)]
    public void Cada_nivel_exige_su_debida_diligencia_y_su_periodicidad_de_revision(int nivel, DebidaDiligencia debida, int meses)
    {
        var matriz = Matriz();
        var respuestas = nivel switch
        {
            1 => Manuales("bajo", "bajo"),
            2 => Manuales("medio", "alto"),
            _ => Manuales("alto", "alto")
        };
        var automaticos = nivel switch
        {
            1 => Automaticos(),
            2 => Automaticos(FactoresAutomaticos.Extranjero, FactoresAutomaticos.Descartadas),
            _ => Automaticos(FactoresAutomaticos.Extranjero, FactoresAutomaticos.Confirmadas, FactoresAutomaticos.ConPep)
        };

        var resultado = matriz.Calcular(respuestas, automaticos);

        (resultado.Nivel, resultado.DebidaDiligencia, resultado.MesesRevision).ShouldBe((nivel, debida, meses));
    }

    [Fact]
    public void El_calculo_pide_todos_los_factores_manuales_y_opciones_que_existan()
    {
        var matriz = Matriz();

        Codigo(() => matriz.Calcular([new RespuestaDeFactor("sector", "bajo")], Automaticos())).ShouldBe("respuestas-incompletas");
        Codigo(() => matriz.Calcular(Manuales("bajo", "enorme"), Automaticos())).ShouldBe("opcion-invalida");
    }

    [Fact]
    public void Actualizar_sube_la_version_y_deja_la_matriz_lista_para_calcular()
    {
        var matriz = Matriz();
        var plantilla = Matriz();

        matriz.Actualizar(true, plantilla.Factores, 30, 60, plantilla.Niveles, Ahora);

        (matriz.Activa, matriz.Version, matriz.LimiteBajo, matriz.LimiteMedio, matriz.ActualizadaEn).ShouldBe((true, 1, 30, 60, Ahora));
        matriz.Calcular(Manuales("medio", "medio"), Automaticos()).VersionMatriz.ShouldBe(1);
        matriz.Actualizar(true, plantilla.Factores, 30, 60, plantilla.Niveles, Ahora);
        matriz.Version.ShouldBe(2);
    }

    [Fact]
    public void Actualizar_rechaza_factores_pesos_opciones_y_niveles_invalidos()
    {
        var plantilla = Matriz();
        var factores = plantilla.Factores;
        var niveles = plantilla.Niveles;
        var manual = factores.First(f => f.Origen == OrigenDelFactor.Manual);

        Codigo(() => Matriz().Actualizar(true, [.. factores, factores[0]], 34, 67, niveles, Ahora)).ShouldBe("factores-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual with { Codigo = "Mal Codigo" }], 34, 67, niveles, Ahora)).ShouldBe("factores-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual with { Nombre = " " }], 34, 67, niveles, Ahora)).ShouldBe("factores-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual with { Peso = 0 }], 34, 67, niveles, Ahora)).ShouldBe("peso-invalido");
        Codigo(() => Matriz().Actualizar(true, [manual with { Peso = 101 }], 34, 67, niveles, Ahora)).ShouldBe("peso-invalido");
        Codigo(() => Matriz().Actualizar(true, [manual with { Opciones = [manual.Opciones[0]] }], 34, 67, niveles, Ahora)).ShouldBe("opciones-invalidas");
        Codigo(() => Matriz().Actualizar(true, [manual with { Opciones = [manual.Opciones[0], manual.Opciones[0]] }], 34, 67, niveles, Ahora)).ShouldBe("opciones-invalidas");
        Codigo(() => Matriz().Actualizar(true, [manual with { Opciones = [manual.Opciones[0], manual.Opciones[1] with { Puntaje = 101 }] }], 34, 67, niveles, Ahora))
            .ShouldBe("opciones-invalidas");
        Codigo(() => Matriz().Actualizar(true, [manual with { Opciones = [manual.Opciones[0], manual.Opciones[1] with { NivelMinimo = 1 }] }], 34, 67, niveles, Ahora))
            .ShouldBe("opciones-invalidas");
        Codigo(() => Matriz().Actualizar(true, [], 34, 67, niveles, Ahora)).ShouldBe("factores-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 67, 34, niveles, Ahora)).ShouldBe("limites-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 0, 50, niveles, Ahora)).ShouldBe("limites-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 34, 67, [niveles[0], niveles[1]], Ahora)).ShouldBe("niveles-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 34, 67, [niveles[0], niveles[1], niveles[2] with { DebidaDiligencia = DebidaDiligencia.Ordinaria }], Ahora))
            .ShouldBe("niveles-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 34, 67,
            [niveles[0] with { DebidaDiligencia = DebidaDiligencia.Ampliada }, niveles[1] with { DebidaDiligencia = DebidaDiligencia.Ordinaria }, niveles[2]], Ahora))
            .ShouldBe("niveles-invalidos");
        Codigo(() => Matriz().Actualizar(true, [manual], 34, 67, [niveles[0] with { MesesRevision = 0 }, niveles[1], niveles[2]], Ahora)).ShouldBe("niveles-invalidos");
    }

    [Fact]
    public void Un_factor_automatico_solo_admite_las_opciones_que_el_sistema_calcula_y_un_manual_no_usa_su_codigo()
    {
        var plantilla = Matriz();
        var pep = plantilla.Factores.First(f => f.Codigo == FactoresAutomaticos.Pep);

        Codigo(() => Matriz().Actualizar(true, [pep with { Opciones = [pep.Opciones[0], new OpcionDeFactor("quiza", "Quizá", 50)] }], 34, 67, plantilla.Niveles, Ahora))
            .ShouldBe("factor-automatico-invalido");
        Codigo(() => Matriz().Actualizar(true, [pep with { Origen = OrigenDelFactor.Manual }], 34, 67, plantilla.Niveles, Ahora)).ShouldBe("factores-invalidos");
        // Lo que sí puede cambiar la empresa: el puntaje, el piso y el peso.
        var ajustado = pep with { Peso = 50, Opciones = [pep.Opciones[0], pep.Opciones[1] with { Puntaje = 100, NivelMinimo = 3 }] };
        var matriz = Matriz();
        matriz.Actualizar(true, [ajustado], 34, 67, plantilla.Niveles, Ahora);
        matriz.Calcular([], Automaticos(pep: FactoresAutomaticos.ConPep)).Nivel.ShouldBe(3);
    }

    [Fact]
    public void Los_factores_automaticos_se_evaluan_con_lo_que_ya_hay_en_el_expediente()
    {
        var personas = new[] { new PersonaConsultada(ClaveTercero, "Tercero", "Aceros", "NIT 900123456"), new PersonaConsultada(ClaveRepresentante, "Pep", "Ana", "CC 1020304050") };
        var confirmada = new CoincidenciaDeVerificacion
        {
            Resolucion = new ResolucionDeCoincidencia(DecisionCoincidencia.Confirmada, "Es la persona.", "x", Ahora, Ahora.AddMonths(12))
        };
        var descartada = new CoincidenciaDeVerificacion
        {
            Resolucion = new ResolucionDeCoincidencia(DecisionCoincidencia.Descartada, "Homónimo.", "x", Ahora, Ahora.AddMonths(12))
        };

        FactoresAutomaticos.Evaluar("CO|NIT|9", null, []).ShouldBe(Automaticos());
        FactoresAutomaticos.Evaluar("US|SSN|9", null, personas)[FactoresAutomaticos.Jurisdiccion].ShouldBe(FactoresAutomaticos.Extranjero);
        FactoresAutomaticos.Evaluar("CO|NIT|9", null, personas)[FactoresAutomaticos.Pep].ShouldBe(FactoresAutomaticos.ConPep);
        string EstadoListas(params CoincidenciaDeVerificacion[] c) => FactoresAutomaticos
            .Evaluar("CO|NIT|9", new VerificacionDeExpediente { Coincidencias = [.. c] }, [])[FactoresAutomaticos.CoincidenciasEnListas];
        EstadoListas().ShouldBe(FactoresAutomaticos.SinCoincidencias);
        EstadoListas(descartada).ShouldBe(FactoresAutomaticos.Descartadas);
        EstadoListas(descartada, new CoincidenciaDeVerificacion()).ShouldBe(FactoresAutomaticos.PorAnalizar);
        EstadoListas(descartada, confirmada, new CoincidenciaDeVerificacion()).ShouldBe(FactoresAutomaticos.Confirmadas);
    }

    // ---------- Expediente ----------

    private FlujoParaExpediente Flujo(params VerificacionRequeridaDelFlujo[] verificaciones) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "COMPRAS", "Compras", 1, true, false, null)])], PoliticaDevolucion.Inmediata, false, verificaciones);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", ClaveTercero,
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private Expediente Abrir(params VerificacionRequeridaDelFlujo[] verificaciones) =>
        Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(verificaciones), Ahora));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private static readonly VerificacionRequeridaDelFlujo Listas = new(TipoVerificacion.ListasRestrictivas);
    private static readonly VerificacionRequeridaDelFlujo EstudioRequerido = new(TipoVerificacion.EstudioDeCumplimiento);

    private static ResultadoDeListas Resultado(string referencia, DateTimeOffset desde, params CoincidenciaHallada[] coincidencias) =>
        new("Simulado", referencia, [new PersonaConsultada(ClaveTercero, "Tercero", "Aceros S.A.S.", "NIT 900123456"),
            new PersonaConsultada(ClaveRepresentante, "RepresentanteLegal", "Ana Coincidencia", "CC 1020304050")], coincidencias, null, desde.AddMonths(12));

    private static AdjuntoDeCumplimiento Adjunto(PropositoDeAdjunto proposito, long tamano = 1024) =>
        new(Guid.NewGuid(), "consulta.pdf", "application/pdf", tamano, new string('a', 64), "tenant-x", "empresas/x/cumplimiento/aaa", proposito, "oficial", Ahora);

    [Fact]
    public void Un_estudio_con_matriz_que_ajusta_el_nivel_exige_una_justificacion_y_guarda_el_calculo()
    {
        var e = Abrir(EstudioRequerido);
        var calculo = Matriz().Calcular(Manuales("bajo", "bajo"), Automaticos()); // nivel 1
        var datos = new DatosDeEstudio(2, false, DebidaDiligencia.Ordinaria, null, [], ConceptoCumplimiento.Favorable, calculo);

        Codigo(() => e.RegistrarEstudio(datos, _oficial, Ahora.AddDays(30), Ahora)).ShouldBe("justificacion-de-nivel-requerida");
        Codigo(() => e.RegistrarEstudio(datos with { JustificacionDeNivel = "corto" }, _oficial, Ahora.AddDays(30), Ahora)).ShouldBe("justificacion-de-nivel-requerida");

        e = Aplicar(e, e.RegistrarEstudio(datos with { JustificacionDeNivel = "Sector regulado que la matriz no refleja." }, _oficial, Ahora.AddDays(30), Ahora));
        var estudio = e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!.Estudio!.Datos;
        (estudio.NivelRiesgo, estudio.Matriz!.Nivel, estudio.Matriz.Puntaje).ShouldBe((2, 1, 0m));
        estudio.JustificacionDeNivel.ShouldBe("Sector regulado que la matriz no refleja.");

        // Sin ajuste (el nivel es el calculado) no hace falta justificar.
        var e2 = Abrir(EstudioRequerido);
        Aplicar(e2, e2.RegistrarEstudio(datos with { NivelRiesgo = 1, DebidaDiligencia = DebidaDiligencia.Simple }, _oficial, Ahora.AddDays(30), Ahora))
            .BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!.Estado.ShouldBe(EstadoVerificacion.Satisfecha);
    }

    [Fact]
    public void Los_archivos_del_estudio_deben_ser_del_expediente_y_de_ese_proposito()
    {
        var e = Abrir(EstudioRequerido);
        var soporte = Adjunto(PropositoDeAdjunto.Estudio);
        var deConsulta = Adjunto(PropositoDeAdjunto.ConsultaManual);
        e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(soporte));
        e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(deConsulta));
        var datos = new DatosDeEstudio(1, false, DebidaDiligencia.Simple, null, [], ConceptoCumplimiento.Favorable);

        Codigo(() => e.RegistrarEstudio(datos with { Adjuntos = [Guid.NewGuid()] }, _oficial, Ahora.AddDays(30), Ahora)).ShouldBe("adjunto-invalido");
        Codigo(() => e.RegistrarEstudio(datos with { Adjuntos = [deConsulta.Id] }, _oficial, Ahora.AddDays(30), Ahora)).ShouldBe("adjunto-invalido");
        e = Aplicar(e, e.RegistrarEstudio(datos with { Adjuntos = [soporte.Id, soporte.Id] }, _oficial, Ahora.AddDays(30), Ahora));
        e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!.Estudio!.Datos.Adjuntos.ShouldBe([soporte.Id]);
    }

    [Fact]
    public void Un_archivo_vacio_enorme_o_demasiados_se_rechazan()
    {
        var e = Abrir(EstudioRequerido);

        Codigo(() => e.RegistrarAdjuntoDeCumplimiento(Adjunto(PropositoDeAdjunto.Estudio, 0))).ShouldBe("adjunto-invalido");
        Codigo(() => e.RegistrarAdjuntoDeCumplimiento(Adjunto(PropositoDeAdjunto.Estudio, Expediente.TamanoMaximoDeAdjunto + 1))).ShouldBe("adjunto-invalido");
        for (var i = 0; i < 20; i++)
            e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(Adjunto(PropositoDeAdjunto.Estudio)));
        Codigo(() => e.RegistrarAdjuntoDeCumplimiento(Adjunto(PropositoDeAdjunto.Estudio))).ShouldBe("adjuntos-demasiados");
        e.AdjuntosDeCumplimiento.Count.ShouldBe(20);
    }

    [Fact]
    public void La_consulta_hecha_por_fuera_entra_como_una_consulta_mas_con_su_pdf_de_evidencia()
    {
        var e = Abrir(Listas);
        var pdf = Adjunto(PropositoDeAdjunto.ConsultaManual);
        e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(pdf));
        var consultada = Ahora.AddDays(-2);
        var coincidencia = new CoincidenciaHallada(ClaveRepresentante, "OFAC (SDN)", "ANA COINCIDENCIA", "Homónimo probable.", 91m);

        e = Aplicar(e, e.RegistrarConsultaManualDeListas(Resultado("manual-1", consultada, coincidencia), pdf.Id, consultada, _oficial, Ahora));

        var listas = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        listas.Estado.ShouldBe(EstadoVerificacion.CoincidenciaPorAnalizar);
        listas.RegistradaPor.ShouldBe(_oficial.UsuarioId.ToString());
        listas.VigenteHasta.ShouldBe(consultada.AddMonths(12));
        listas.Coincidencias.ShouldHaveSingleItem().Rol.ShouldBe("RepresentanteLegal");

        // Sin coincidencias, una consulta nueva la reemplaza y queda satisfecha.
        e = Aplicar(e, e.RegistrarConsultaManualDeListas(Resultado("manual-2", Ahora), pdf.Id, Ahora, _oficial, Ahora));
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.Satisfecha);
    }

    [Fact]
    public void La_consulta_por_fuera_exige_el_pdf_correcto_una_fecha_reciente_y_un_oficial_sin_conflicto()
    {
        var e = Abrir(Listas);
        var pdf = Adjunto(PropositoDeAdjunto.ConsultaManual);
        var deEstudio = Adjunto(PropositoDeAdjunto.Estudio);
        e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(pdf));
        e = Aplicar(e, e.RegistrarAdjuntoDeCumplimiento(deEstudio));

        Codigo(() => e.RegistrarConsultaManualDeListas(Resultado("m", Ahora), Guid.NewGuid(), Ahora, _oficial, Ahora)).ShouldBe("adjunto-invalido");
        Codigo(() => e.RegistrarConsultaManualDeListas(Resultado("m", Ahora), deEstudio.Id, Ahora, _oficial, Ahora)).ShouldBe("adjunto-invalido");
        Codigo(() => e.RegistrarConsultaManualDeListas(Resultado("m", Ahora), pdf.Id, Ahora.AddDays(-31), _oficial, Ahora)).ShouldBe("consulta-fuera-de-plazo");
        Codigo(() => e.RegistrarConsultaManualDeListas(Resultado("m", Ahora), pdf.Id, Ahora.AddDays(2), _oficial, Ahora)).ShouldBe("consulta-fuera-de-plazo");
        Codigo(() => e.RegistrarConsultaManualDeListas(Resultado("m", Ahora), pdf.Id, Ahora, new Revisor(Guid.NewGuid(), ClaveTercero), Ahora))
            .ShouldBe("conflicto-de-intereses");
    }

    [Fact]
    public void El_oficial_rechaza_directamente_solo_ante_un_hallazgo_con_motivos_y_justificacion()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        var motivos = new[] { new MotivoAplicado("listas", "Coincidencia en listas restrictivas.") };

        // Sin hallazgo (las listas aún pendientes) no se rechaza.
        Codigo(() => e.RechazarPorCumplimiento(_oficial, motivos, "Es de riesgo.", Ahora)).ShouldBe("rechazo-sin-hallazgo");

        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", Ahora, new CoincidenciaHallada(ClaveRepresentante, "OFAC (SDN)", "ANA COINCIDENCIA", null, 100m)),
            "sistema", Ahora));
        Codigo(() => e.RechazarPorCumplimiento(_oficial, motivos, "Es de riesgo.", Ahora)).ShouldBe("rechazo-sin-hallazgo"); // por analizar no es hallazgo
        var id = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id;
        e = Aplicar(e, e.ResolverCoincidencia(id, DecisionCoincidencia.Confirmada, "Es la misma persona.", _oficial, Ahora));

        Codigo(() => e.RechazarPorCumplimiento(_oficial, [], "Es de riesgo.", Ahora)).ShouldBe("motivo-requerido");
        Codigo(() => e.RechazarPorCumplimiento(_oficial, motivos, " ", Ahora)).ShouldBe("justificacion-requerida");
        Codigo(() => e.RechazarPorCumplimiento(new Revisor(Guid.NewGuid(), ClaveRepresentante), motivos, "Es de riesgo.", Ahora)).ShouldBe("conflicto-de-intereses");

        var eventos = e.RechazarPorCumplimiento(_oficial, motivos, "Coincidencia confirmada en OFAC.", Ahora);
        var rechazo = eventos.ShouldHaveSingleItem().ShouldBeOfType<ExpedienteRechazado>();
        (rechazo.AreaId, rechazo.Origen, rechazo.RevisorId).ShouldBe((Guid.Empty, "Cumplimiento", _oficial.UsuarioId.ToString()));
        e = Aplicar(e, eventos);
        (e.Estado, e.Rechazo!.Origen).ShouldBe((EstadoExpediente.Rechazado, "Cumplimiento"));
        Codigo(() => e.RechazarPorCumplimiento(_oficial, motivos, "Otra vez.", Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Un_estudio_desfavorable_tambien_es_un_hallazgo_para_rechazar()
    {
        var e = Abrir(EstudioRequerido);
        e = Aplicar(e, e.RegistrarEstudio(new DatosDeEstudio(3, false, DebidaDiligencia.Ampliada, "No explica el origen de sus fondos.", [],
            ConceptoCumplimiento.Desfavorable), _oficial, Ahora.AddDays(30), Ahora));

        var eventos = e.RechazarPorCumplimiento(_oficial, [new MotivoAplicado("riesgo", "No cumple la política de riesgo.")], "Estudio desfavorable.", Ahora);

        Aplicar(e, eventos).Estado.ShouldBe(EstadoExpediente.Rechazado);
    }

    [Fact]
    public void Las_listas_se_expiran_cuando_aparece_una_persona_que_la_consulta_no_cubria()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", Ahora), "sistema", Ahora));
        var listas = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        listas.Estado.ShouldBe(EstadoVerificacion.Satisfecha);

        // Las mismas personas, o menos (se retiró un socio), no cambian nada.
        e.ExpirarListasPorCambioDePersonas([ClaveTercero, ClaveRepresentante], Ahora).ShouldBeEmpty();
        e.ExpirarListasPorCambioDePersonas([ClaveTercero], Ahora).ShouldBeEmpty();
        e.ExpirarListasPorCambioDePersonas([ClaveTercero.ToLowerInvariant(), ClaveRepresentante], Ahora).ShouldBeEmpty(); // sin importar mayúsculas

        // Un representante nuevo: la consulta ya no cubre al tercero y se repite sola.
        var expira = e.ExpirarListasPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora.AddDays(1));
        var evento = expira.ShouldHaveSingleItem().ShouldBeOfType<VerificacionExpirada>();
        (evento.Tipo, evento.Motivo).ShouldBe((TipoVerificacion.ListasRestrictivas, "Cambió el representante legal o las personas relacionadas: hay que volver a consultar las listas."));
        e = Aplicar(e, expira);
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.Expirada);
        e.ProximaConsultaDeListas.ShouldNotBeNull();
        e.PendienteDeCumplimiento.ShouldBeTrue();
        // Ya expirada: no se vuelve a expirar.
        e.ExpirarListasPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora.AddDays(1)).ShouldBeEmpty();
    }

    [Fact]
    public void Sin_consulta_hecha_o_con_el_expediente_cerrado_no_hay_nada_que_expirar()
    {
        var sinConsultar = Abrir(Listas);
        sinConsultar.ExpirarListasPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora).ShouldBeEmpty();

        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", Ahora), "sistema", Ahora));
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.ExpirarListasPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora).ShouldBeEmpty();
    }

    [Fact]
    public void El_monitoreo_de_un_vinculo_activo_reconsulta_ya_si_hay_personas_nuevas()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", Ahora), "sistema", Ahora));
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        var monitoreo = MonitoreoDeListas.Iniciar(e, null, 30, Ahora)!;
        monitoreo.ProximaConsulta.ShouldBe(Ahora.AddDays(30));

        monitoreo.ReconsultarPorCambioDePersonas([ClaveTercero, ClaveRepresentante], Ahora.AddDays(1)).ShouldBeFalse();
        monitoreo.ProximaConsulta.ShouldBe(Ahora.AddDays(30));
        monitoreo.ReconsultarPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora.AddDays(1)).ShouldBeTrue();
        monitoreo.ProximaConsulta.ShouldBe(Ahora.AddDays(1));
        monitoreo.Detener(Ahora);
        monitoreo.ReconsultarPorCambioDePersonas([ClaveTercero, ClaveNueva], Ahora.AddDays(2)).ShouldBeFalse(); // detenido: no se reactiva
    }
}
