using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>Verificaciones externas (H7, VE-5…VE-12): bloqueo del cierre, coincidencias, estudio SAGRILAFT, vigencias y monitoreo.</summary>
public class VerificacionesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(-5));
    private const string ClaveTercero = "CO|NIT|900123456";
    private const string ClaveRepresentante = "CO|CC|1020304050";

    private readonly Guid _a = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _oficial = new(Guid.NewGuid(), "CO|CC|79000001");

    private FlujoParaExpediente Flujo(params VerificacionRequeridaDelFlujo[] verificaciones) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "COMPRAS", "Compras", 1, true, false, null)])], PoliticaDevolucion.Inmediata, false, verificaciones);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", ClaveTercero,
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private Expediente Abrir(params VerificacionRequeridaDelFlujo[] verificaciones) =>
        Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(verificaciones), Ahora));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private static ResultadoDeListas Resultado(string referencia, params CoincidenciaHallada[] coincidencias) =>
        new("Simulado", referencia,
            [new PersonaConsultada(ClaveTercero, "Tercero", "Aceros S.A.S.", "NIT 900123456"), new PersonaConsultada(ClaveRepresentante, "RepresentanteLegal", "Ana Coincidencia", "CC 1020304050")],
            coincidencias, null, Ahora.AddMonths(12));

    private static CoincidenciaHallada EnOfac() => new(ClaveRepresentante, "OFAC (SDN)", "ANA COINCIDENCIA", null, 100m);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private static readonly VerificacionRequeridaDelFlujo Listas = new(TipoVerificacion.ListasRestrictivas);
    private static readonly VerificacionRequeridaDelFlujo Estudio = new(TipoVerificacion.EstudioDeCumplimiento);

    private static DatosDeEstudio Favorable(int riesgo = 1) => new(riesgo, false, riesgo == 3 ? DebidaDiligencia.Ampliada : DebidaDiligencia.Simple, null, [],
        ConceptoCumplimiento.Favorable);

    [Fact]
    public void El_flujo_copia_sus_verificaciones_al_expediente_y_las_listas_quedan_para_consulta_al_pasar_a_revision()
    {
        var e = Abrir(Listas, Estudio);

        e.Verificaciones.Select(v => (v.Tipo, v.Estado, v.Bloqueante)).ShouldBe(
            [(TipoVerificacion.ListasRestrictivas, EstadoVerificacion.Pendiente, true), (TipoVerificacion.EstudioDeCumplimiento, EstadoVerificacion.Pendiente, true)]);
        e.ProximaConsultaDeListas.ShouldBe(Ahora);
        e.PendienteDeCumplimiento.ShouldBeTrue();
    }

    [Fact]
    public void Una_verificacion_bloqueante_pendiente_impide_aprobar_aunque_todas_las_areas_aprueben_y_al_satisfacerse_se_cierra()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EsperandoVerificaciones.ShouldBeTrue();

        var eventos = e.RegistrarConsultaDeListas(Resultado("ref-1"), "sistema", Ahora);
        eventos.Select(x => x.GetType().Name).ShouldBe(["VerificacionExternaRegistrada", "ExpedienteAprobado"]);
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.ProximaConsultaDeListas.ShouldBeNull();
        e.PendienteDeCumplimiento.ShouldBeFalse();
    }

    [Fact]
    public void Una_verificacion_no_bloqueante_no_frena_el_cierre()
    {
        var e = Abrir(Listas with { Bloqueante = false });
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Una_coincidencia_queda_por_analizar_y_descartada_con_justificacion_satisface_la_verificacion()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));

        var listas = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        listas.Estado.ShouldBe(EstadoVerificacion.CoincidenciaPorAnalizar);
        listas.Resultado.ShouldBe(ResultadoVerificacion.Coincidencias);
        var coincidencia = listas.Coincidencias.ShouldHaveSingleItem();
        coincidencia.NombrePersona.ShouldBe("Ana Coincidencia");
        coincidencia.Rol.ShouldBe("RepresentanteLegal");
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        Codigo(() => e.ResolverCoincidencia(coincidencia.Id, DecisionCoincidencia.Descartada, " ", _oficial, Ahora)).ShouldBe("justificacion-requerida");
        Codigo(() => e.ResolverCoincidencia(Guid.NewGuid(), DecisionCoincidencia.Descartada, "x", _oficial, Ahora)).ShouldBe("coincidencia-no-encontrada");

        var eventos = e.ResolverCoincidencia(coincidencia.Id, DecisionCoincidencia.Descartada, "Homónimo: otra fecha de nacimiento.", _oficial, Ahora);
        ((CoincidenciaResuelta)eventos[0]).VigenteHasta.ShouldBe(Ahora.AddMonths(12));
        e = Aplicar(e, eventos);
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.Satisfecha);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        Codigo(() => e.ResolverCoincidencia(coincidencia.Id, DecisionCoincidencia.Confirmada, "x", _oficial, Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Una_coincidencia_confirmada_deja_la_verificacion_fallida_el_expediente_no_cierra_y_el_area_puede_rechazarlo()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));
        var id = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id;

        e = Aplicar(e, e.ResolverCoincidencia(id, DecisionCoincidencia.Confirmada, "Es la misma persona: coincide la cédula.", _oficial, Ahora));

        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.Fallida);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EsperandoVerificaciones.ShouldBeTrue();
        // Nunca automático: la etapa ya no está activa, pero un área que puede rechazar cierra el expediente.
        var rechazo = e.Rechazar(_a, _ana, [new MotivoAplicado("listas", "Coincidencia en listas restrictivas.")], "Coincidencia confirmada por Cumplimiento.", Ahora);
        Aplicar(e, rechazo).Estado.ShouldBe(EstadoExpediente.Rechazado);
    }

    [Fact]
    public void Quien_decide_no_puede_ser_una_persona_consultada()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));
        var id = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id;

        Codigo(() => e.ResolverCoincidencia(id, DecisionCoincidencia.Descartada, "x", new Revisor(Guid.NewGuid(), ClaveRepresentante), Ahora))
            .ShouldBe("conflicto-de-intereses");
    }

    [Fact]
    public void La_consulta_es_idempotente_por_referencia_y_la_reconsulta_hereda_lo_descartado_vigente()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));
        e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora).ShouldBeEmpty();

        var id = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id;
        e = Aplicar(e, e.ResolverCoincidencia(id, DecisionCoincidencia.Descartada, "Homónimo.", _oficial, Ahora));

        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-2", EnOfac()), "sistema", Ahora.AddDays(1)));
        var listas = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        listas.Estado.ShouldBe(EstadoVerificacion.Satisfecha);
        listas.Coincidencias.ShouldHaveSingleItem().Heredada.ShouldBeTrue();
        listas.Anteriores.ShouldHaveSingleItem().ReferenciaExterna.ShouldBe("ref-1");

        // Pasados los 12 meses la decisión ya no se hereda: vuelve a pedirse.
        var tarde = Ahora.AddMonths(13);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-3", EnOfac()), "sistema", tarde));
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.CoincidenciaPorAnalizar);
    }

    [Fact]
    public void Una_coincidencia_confirmada_nunca_se_hereda_como_resuelta()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));
        var id = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id;
        e = Aplicar(e, e.ResolverCoincidencia(id, DecisionCoincidencia.Confirmada, "Es la persona.", _oficial, Ahora));

        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-2", EnOfac()), "sistema", Ahora.AddDays(1)));
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.CoincidenciaPorAnalizar);
    }

    [Fact]
    public void El_estudio_de_cumplimiento_valida_riesgo_debida_diligencia_y_observaciones()
    {
        var e = Abrir(Estudio);
        var vence = Ahora.AddDays(30);

        Codigo(() => e.RegistrarEstudio(Favorable() with { NivelRiesgo = 4 }, _oficial, vence, Ahora)).ShouldBe("nivel-riesgo-invalido");
        Codigo(() => e.RegistrarEstudio(Favorable() with { NivelRiesgo = 3 }, _oficial, vence, Ahora)).ShouldBe("debida-diligencia-ampliada-requerida");
        Codigo(() => e.RegistrarEstudio(Favorable() with { ConflictoDeInteres = true }, _oficial, vence, Ahora)).ShouldBe("observaciones-requeridas");
        Codigo(() => e.RegistrarEstudio(Favorable() with { Concepto = ConceptoCumplimiento.Desfavorable }, _oficial, vence, Ahora))
            .ShouldBe("observaciones-requeridas");
        Codigo(() => e.RegistrarEstudio(Favorable(), new Revisor(Guid.NewGuid(), ClaveTercero), vence, Ahora)).ShouldBe("conflicto-de-intereses");
    }

    [Fact]
    public void El_estudio_favorable_satisface_hasta_su_vigencia_y_al_vencer_vuelve_a_pedirse()
    {
        var e = Abrir(Estudio);
        e = Aplicar(e, e.RegistrarEstudio(Favorable(3), _oficial, Ahora.AddDays(30), Ahora));
        var estudio = e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!;
        estudio.Estado.ShouldBe(EstadoVerificacion.Satisfecha);
        estudio.Estudio!.Datos.DebidaDiligencia.ShouldBe(DebidaDiligencia.Ampliada);
        e.ProximoVencimientoDeVerificacion.ShouldBe(Ahora.AddDays(30));

        e.ExpirarVerificaciones(Ahora.AddDays(29)).ShouldBeEmpty();
        var expira = e.ExpirarVerificaciones(Ahora.AddDays(31));
        expira.ShouldHaveSingleItem().ShouldBeOfType<VerificacionExpirada>();
        e = Aplicar(e, expira);
        e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!.Estado.ShouldBe(EstadoVerificacion.Expirada);
        e.PendienteDeCumplimiento.ShouldBeTrue();

        // Vencido no cuenta para el cierre aunque nadie lo haya expirado todavía.
        var otro = Abrir(Estudio);
        otro = Aplicar(otro, otro.RegistrarEstudio(Favorable(), _oficial, Ahora.AddDays(1), Ahora));
        otro.VerificacionesSatisfechas(Ahora.AddDays(2)).ShouldBeFalse();
    }

    [Fact]
    public void El_estudio_desfavorable_deja_la_verificacion_fallida()
    {
        var e = Abrir(Estudio);
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e = Aplicar(e, e.RegistrarEstudio(Favorable() with { Concepto = ConceptoCumplimiento.Desfavorable, Observaciones = "Riesgo reputacional." },
            _oficial, Ahora.AddDays(30), Ahora));
        e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)!.Estado.ShouldBe(EstadoVerificacion.Fallida);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
    }

    [Fact]
    public void Las_listas_vencidas_con_el_expediente_abierto_se_reconsultan_solas()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1") with { VigenteHasta = Ahora.AddDays(1) }, "sistema", Ahora));
        e.ProximaConsultaDeListas.ShouldBeNull();

        e = Aplicar(e, e.ExpirarVerificaciones(Ahora.AddDays(2)));
        e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Estado.ShouldBe(EstadoVerificacion.Expirada);
        e.ProximaConsultaDeListas.ShouldBe(Ahora.AddDays(2));
    }

    [Fact]
    public void Una_consulta_fallida_se_reintenta_mas_tarde()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarFalloDeConsulta(TipoVerificacion.ListasRestrictivas, "Infolaft", "HTTP 503", Ahora.AddMinutes(5), Ahora));
        var listas = e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        listas.Fallos.ShouldBe(1);
        listas.UltimoError.ShouldBe("HTTP 503");
        e.ProximaConsultaDeListas.ShouldBe(Ahora.AddMinutes(5));
    }

    [Fact]
    public void El_flujo_valida_sus_verificaciones()
    {
        var area = new Configuracion.Area { Id = Guid.NewGuid(), EmpresaId = Guid.NewGuid(), Codigo = "A", Nombre = "A", Activa = true };
        DefinicionDeFlujo Definicion(params VerificacionRequeridaDelFlujo[] v) =>
            new([new EtapaDeFlujo(1, [new AreaDeEtapa(area.Id, 1, false, false, null)])], PoliticaDevolucion.Inmediata, false, null, v);

        Codigo(() => Definicion(Listas, Listas).Validar(area.EmpresaId, [area])).ShouldBe("verificacion-duplicada");
        Codigo(() => Definicion(Estudio with { VigenciaDias = 0 }).Validar(area.EmpresaId, [area])).ShouldBe("vigencia-invalida");
        Codigo(() => Definicion(Estudio with { FrecuenciaReconsultaMeses = 6 }).Validar(area.EmpresaId, [area])).ShouldBe("reconsulta-no-aplica");
        Definicion(Listas with { FrecuenciaReconsultaMeses = 6 }, Estudio with { VigenciaDias = 45 }).Validar(area.EmpresaId, [area])
            .Verificaciones!.Count.ShouldBe(2);
    }

    [Fact]
    public void El_monitoreo_hereda_las_decisiones_del_expediente_y_avisa_solo_lo_nuevo()
    {
        var e = Abrir(Listas);
        e = Aplicar(e, e.RegistrarConsultaDeListas(Resultado("ref-1", EnOfac()), "sistema", Ahora));
        e = Aplicar(e, e.ResolverCoincidencia(e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!.Coincidencias[0].Id, DecisionCoincidencia.Descartada,
            "Homónimo.", _oficial, Ahora));
        e = Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);

        var monitoreo = MonitoreoDeListas.Iniciar(e, null, 30, Ahora)!;
        monitoreo.ProximaConsulta.ShouldBe(Ahora.AddDays(30));
        monitoreo.PendienteDeCumplimiento.ShouldBeFalse();

        var nueva = new CoincidenciaHallada(ClaveTercero, "ONU", "ACEROS SAS", null, 90m);
        var nuevas = monitoreo.RegistrarConsulta(Resultado("ref-m1", EnOfac(), nueva), "sistema", Ahora.AddDays(30));
        nuevas.ShouldHaveSingleItem().Lista.ShouldBe("ONU");
        monitoreo.PendienteDeCumplimiento.ShouldBeTrue();
        monitoreo.ProximaConsulta.ShouldBe(Ahora.AddDays(60));
        monitoreo.RegistrarConsulta(Resultado("ref-m1"), "sistema", Ahora.AddDays(31)).ShouldBeEmpty();

        monitoreo.ResolverCoincidencia(nuevas[0].Id, DecisionCoincidencia.Descartada, "Otra empresa.", _oficial, Ahora.AddDays(31));
        monitoreo.PendienteDeCumplimiento.ShouldBeFalse();
        MonitoreoDeListas.Iniciar(Abrir(), null, 30, Ahora).ShouldBeNull();
    }

    [Fact]
    public void La_configuracion_de_listas_guarda_la_clave_cifrada_y_exige_credenciales_para_un_proveedor_real()
    {
        var config = ListasRestrictivasEmpresa.Nueva(Guid.NewGuid());
        Codigo(() => config.ElegirProveedor("Infolaft", Ahora)).ShouldBe("credenciales-requeridas");
        Codigo(() => config.ElegirProveedor("Otro", Ahora)).ShouldBe("proveedor-invalido");
        Codigo(() => config.CambiarCredenciales(new CambioDeCredencialesDeListas("Infolaft", "sinco", null, null, null, null, null), s => s, Ahora))
            .ShouldBe("clave-requerida");
        Codigo(() => config.CambiarCredenciales(new CambioDeCredencialesDeListas("Infolaft", "sinco", "secreta-123", "http://x.co", null, null, null), s => s, Ahora))
            .ShouldBe("url-invalida");

        config.CambiarCredenciales(new CambioDeCredencialesDeListas("infolaft", "sinco", "secreta-1234", null, "Empresarial", 500, ModoBusquedaListas.PorNombre),
            s => $"cifrado({s})", Ahora);
        config.ElegirProveedor("INFOLAFT", Ahora);
        config.Proveedor.ShouldBe("Infolaft");
        var c = config.CredencialesDe("Infolaft")!;
        c.ClaveProtegida.ShouldBe("cifrado(secreta-1234)");
        c.UltimosCuatroClave.ShouldBe("1234");
        c.CuotaMensual.ShouldBe(500);

        // Cambiar el usuario conserva la clave; vaciar la clave quita las credenciales y el proveedor elegido.
        config.CambiarCredenciales(new CambioDeCredencialesDeListas("Infolaft", "otro", null, null, null, null, null), _ => throw new InvalidOperationException(), Ahora);
        config.CredencialesDe("Infolaft")!.ClaveProtegida.ShouldBe("cifrado(secreta-1234)");
        config.CambiarCredenciales(new CambioDeCredencialesDeListas("Infolaft", null, "", null, null, null, null), s => s, Ahora);
        config.CredencialesDe("Infolaft").ShouldBeNull();
        config.Proveedor.ShouldBeNull();
    }

    [Fact]
    public void El_consumo_de_listas_es_idempotente_por_referencia_y_respeta_la_cuota()
    {
        var uso = UsoMensualListas.Nuevo(Guid.NewGuid(), "Infolaft", "2026-11");
        uso.Registrar("ref-1", 3, true).ShouldBeTrue();
        uso.Registrar("ref-1", 3, true).ShouldBeFalse();
        uso.Registrar("ref-2", 2, false).ShouldBeTrue();
        (uso.Consultas, uso.Busquedas, uso.ConCoincidencias).ShouldBe((2, 5, 1));
        uso.Cabe(6, 1).ShouldBeTrue();
        uso.Cabe(6, 2).ShouldBeFalse();
        uso.Cabe(null, 1000).ShouldBeTrue();
    }

    [Fact]
    public void El_lector_de_infolaft_tolera_respuestas_paginadas_con_nombres_distintos()
    {
        // JSON inventado (el formato real no está documentado públicamente): el lector busca la lista y los campos habituales.
        const string json = """
            { "totalElements": 2, "page": 0,
              "content": [
                { "fullName": "ANA COINCIDENCIA", "listName": "OFAC - SDN", "description": "Narcotráfico", "score": 98.5 },
                { "nombre": "ANA M. COINCIDENCIA", "fuente": ["ONU", "UE"], "porcentaje": "91%" }
              ] }
            """;
        var persona = new PersonaAConsultar(ClaveRepresentante, "RepresentanteLegal", "Ana Coincidencia", "CO", "CC", "1020304050");
        var leidas = LectorDeRespuestaDeListas.Leer(json, persona);
        leidas.Count.ShouldBe(2);
        leidas[0].ShouldBe(new CoincidenciaHallada(ClaveRepresentante, "OFAC - SDN", "ANA COINCIDENCIA", "Narcotráfico", 98.5m));
        leidas[1].Lista.ShouldBe("ONU, UE");
        leidas[1].Puntaje.ShouldBe(91m);

        LectorDeRespuestaDeListas.Leer("""{ "data": { "results": [] } }""", persona).ShouldBeEmpty();
        LectorDeRespuestaDeListas.Leer("""[ { "name": "X" } ]""", persona).ShouldHaveSingleItem().Lista.ShouldBe("Infolaft");
        LectorDeRespuestaDeListas.Leer("""{ "mensaje": "sin resultados" }""", persona).ShouldBeEmpty();
    }
}
