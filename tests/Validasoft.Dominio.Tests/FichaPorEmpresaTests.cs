using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Validación por empresa de la ficha maestra (F2, FM-25…): secciones como requisitos del flujo, «completa para la empresa»
/// (soporte exigido, persona natural sin Legal ni Financiera, periodos de la financiera), veredictos de sección con campos
/// señalados, aceptación al aprobar y la comparación anterior → nuevo de las Actualizaciones.
/// </summary>
public class FichaPorEmpresaTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 9);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);
    private static readonly MotivoAplicado NoCoinciden = new("datos-no-coinciden", "Los datos informados no coinciden con los soportes.");

    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Guid _tercero = Guid.NewGuid();
    private readonly Area _compras;
    private readonly Area _contabilidad;
    private readonly Revisor _ana = new(Guid.NewGuid(), null);
    private readonly Revisor _beto = new(Guid.NewGuid(), null);

    public FichaPorEmpresaTests()
    {
        _compras = Area.Crear(_empresa, "COMPRAS", "Compras");
        _contabilidad = Area.Crear(_empresa, "CONTABILIDAD", "Contabilidad");
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static RequisitoDelFlujo Pide(string seccion, Area? area = null, bool exigeSoporte = false, int? periodos = null) =>
        new(Guid.Empty, Guid.Empty, string.Empty, true, area is null ? [] : [area.Id], null, Seccion: seccion, Periodos: periodos, ExigeSoporte: exigeSoporte);

    private DefinicionDeFlujo Definicion(params RequisitoDelFlujo[] requisitos) =>
        new([new EtapaDeFlujo(1, [new AreaDeEtapa(_compras.Id, 1, false, false, null), new AreaDeEtapa(_contabilidad.Id, 1, false, false, null)])],
            PoliticaDevolucion.Inmediata, false, requisitos);

    private string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    /// <summary>Sección con versiones de completitud dada (la base de F1 ya calculada y guardada).</summary>
    private SeccionDeFicha Seccion(string codigo, int? periodo = null, params (bool Completa, int Soportes, string Datos)[] versiones)
    {
        var definicion = CatalogoDeSecciones.Requerida(codigo);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(_tercero, definicion, periodo, Ahora));
        var numero = 0;
        foreach (var (completa, soportes, datos) in versiones)
        {
            numero++;
            seccion.Apply(new VersionDeSeccionRegistrada(numero, Json(datos), $"d{numero}", $"h{numero}",
                [.. Enumerable.Range(0, soportes).Select(_ => new SoporteDeSeccion(Guid.NewGuid(), 1, "RUT"))],
                new CompletitudDeSeccion(completa, completa ? [] : [new FaltanteDeSeccion(null, "socio-requerido", "Registra al menos un socio o accionista.")], 1),
                1, null, OrigenDeSeccion.Tercero, "t", Ahora));
        }

        return seccion;
    }

    // ---------- el flujo pide secciones ----------

    [Fact]
    public void El_flujo_pide_secciones_con_areas_soporte_y_periodos_y_rechaza_las_desconocidas()
    {
        var areas = new[] { _compras, _contabilidad };
        var validada = Definicion(Pide("ficha_general", _compras), Pide(CodigosDeSeccion.Tributaria, _contabilidad, exigeSoporte: true),
            Pide(CodigosDeSeccion.Financiera, _contabilidad)).Validar(_empresa, areas);

        var requisitos = validada.Requisitos!;
        requisitos.Select(r => (r.Seccion, r.CodigoTipo, r.TipoDocumentoTenantId)).ShouldBe(
        [
            ("FICHA_GENERAL", "FICHA_GENERAL", Guid.Empty), ("FICHA_TRIBUTARIA", "FICHA_TRIBUTARIA", Guid.Empty),
            ("FICHA_FINANCIERA", "FICHA_FINANCIERA", Guid.Empty)
        ]);
        requisitos.ShouldAllBe(r => r.Id != Guid.Empty);
        requisitos[1].ExigeSoporte.ShouldBeTrue();
        requisitos[2].Periodos.ShouldBe(1); // por omisión, el último cierre
        requisitos[0].Periodos.ShouldBeNull();

        Codigo(() => Definicion(Pide("FICHA_BANCARIA", _compras), Pide(CodigosDeSeccion.General, _contabilidad)).Validar(_empresa, areas))
            .ShouldBe("seccion-invalida");
        Codigo(() => Definicion(Pide(CodigosDeSeccion.General, _compras, periodos: 2), Pide(CodigosDeSeccion.Sst, _contabilidad)).Validar(_empresa, areas))
            .ShouldBe("periodos-no-aplica");
        Codigo(() => Definicion(Pide(CodigosDeSeccion.Financiera, _compras, periodos: 6), Pide(CodigosDeSeccion.Sst, _contabilidad)).Validar(_empresa, areas))
            .ShouldBe("periodos-invalidos");
        Codigo(() => Definicion(Pide(CodigosDeSeccion.General, _compras), Pide(CodigosDeSeccion.General, _contabilidad)).Validar(_empresa, areas))
            .ShouldBe("requisito-duplicado");
        // El 422 dice en qué requisito está el problema (su posición), para mostrarlo en su campo.
        Should.Throw<ReglaDeNegocioException>(() =>
                Definicion(Pide(CodigosDeSeccion.General, _compras), Pide(CodigosDeSeccion.General, _contabilidad)).Validar(_empresa, areas))
            .Datos!["requisito"].ShouldBe(1);
        // Cada área revisa algo: Contabilidad no tiene secciones en su alcance.
        Codigo(() => Definicion(Pide(CodigosDeSeccion.General, _compras)).Validar(_empresa, areas)).ShouldBe("area-sin-requisitos");
    }

    [Fact]
    public void A_la_persona_natural_no_se_le_piden_legal_ni_financiera()
    {
        IReadOnlyList<RequisitoDelFlujo> requisitos =
        [
            Pide(CodigosDeSeccion.General) with { Id = Guid.NewGuid() }, Pide(CodigosDeSeccion.Legal) with { Id = Guid.NewGuid() },
            Pide(CodigosDeSeccion.Financiera) with { Id = Guid.NewGuid() }, Pide(CodigosDeSeccion.Especialidades) with { Id = Guid.NewGuid() }
        ];
        var natural = new ContextoDeExigencia(TipoPersona.Natural, "CO", "CC", false, "PROVEEDOR", new Dictionary<string, JsonElement>());
        var juridica = natural with { TipoPersona = TipoPersona.Juridica, TipoIdentificacion = "NIT" };
        var consorcio = juridica with { TipoPersona = TipoPersona.Consorcio };

        EvaluadorDeExigencia.Filtrar(requisitos, natural).Select(r => r.Seccion).ShouldBe(["FICHA_GENERAL", "FICHA_ESPECIALIDADES"]);
        EvaluadorDeExigencia.Filtrar(requisitos, juridica).Count.ShouldBe(4);
        EvaluadorDeExigencia.Filtrar(requisitos, consorcio).Count.ShouldBe(4);
    }

    [Fact]
    public void La_financiera_se_pide_por_cada_periodo_exigido_con_ids_deterministas()
    {
        var id = Guid.NewGuid();
        var pedidos = RequisitosDeSeccion.Expandir([Pide(CodigosDeSeccion.Financiera, periodos: 2) with { Id = id }, Pide(CodigosDeSeccion.General) with { Id = Guid.NewGuid() }],
            Hoy);

        pedidos.Select(p => (p.Definicion.Codigo, p.Periodo, p.Nombre)).ShouldBe(
        [
            ("FICHA_FINANCIERA", (int?)2025, "Información financiera 2025"), ("FICHA_FINANCIERA", 2024, "Información financiera 2024"),
            ("FICHA_GENERAL", null, "Información general")
        ]);
        pedidos[0].RequisitoId.ShouldBe(id);
        pedidos[1].RequisitoId.ShouldBe(RequisitosDeSeccion.IdPorPeriodo(id, 2024));
        pedidos[1].RequisitoId.ShouldNotBe(id);
        pedidos[0].SeccionId(_tercero).ShouldBe(SeccionDeFicha.IdPara(_tercero, "FICHA_FINANCIERA", 2025));
    }

    // ---------- completa para la empresa ----------

    [Fact]
    public void Completa_para_la_empresa_es_la_base_de_F1_mas_el_soporte_que_exige_el_requisito()
    {
        var general = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);

        // Sin diligenciar: pendiente con su motivo.
        var vacia = RequisitosDeSeccion.Evaluar(general, null, false, null);
        vacia.Version.ShouldBeNull();
        vacia.Disponible.ShouldBeNull();
        vacia.Completitud.ShouldBe(new CompletitudParaEmpresa(null, false, vacia.Completitud.Faltantes));
        vacia.Completitud.Faltantes.ShouldHaveSingleItem().Mensaje.ShouldBe("Falta diligenciar la información general.");

        // Completa en la base y sin soporte: sirve si el requisito no lo exige; si lo exige, falta el soporte.
        var sinSoporte = Seccion(CodigosDeSeccion.General, null, (true, 0, "{}"));
        RequisitosDeSeccion.Evaluar(general, null, false, sinSoporte).Disponible.ShouldBe(new VersionDeRequisito(sinSoporte.Id, 1, "h1", Ahora));
        var exige = RequisitosDeSeccion.Evaluar(general, null, true, sinSoporte);
        exige.Disponible.ShouldBeNull();
        exige.Version!.Numero.ShouldBe(1);
        exige.Completitud.Version.ShouldBe(1);
        exige.Completitud.Faltantes.ShouldHaveSingleItem().ShouldBe(new FaltanteDeSeccion(null, "soporte-requerido", "Falta el soporte de la información general."));

        // Con soporte: completa.
        var conSoporte = Seccion(CodigosDeSeccion.General, null, (true, 0, "{}"), (true, 1, "{}"));
        var completa = RequisitosDeSeccion.Evaluar(general, null, true, conSoporte);
        completa.Completitud.Completa.ShouldBeTrue();
        completa.Disponible!.Numero.ShouldBe(2);

        // Los faltantes de la base (lo que guardó F1) llegan tal cual.
        var legal = Seccion(CodigosDeSeccion.Legal, null, (false, 1, "{}"));
        RequisitosDeSeccion.Evaluar(CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal), null, false, legal).Completitud.Faltantes
            .ShouldHaveSingleItem().Mensaje.ShouldBe("Registra al menos un socio o accionista.");

        // La financiera habla del periodo.
        RequisitosDeSeccion.Evaluar(CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera), 2025, true, null).Completitud.Faltantes
            .ShouldHaveSingleItem().Mensaje.ShouldBe("Falta diligenciar la información financiera de 2025.");
    }

    // ---------- el expediente con secciones ----------

    private FlujoParaExpediente Flujo() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "CLI", "Clientes", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_compras.Id, "COMPRAS", "Compras", 1, false, false, null),
                new AreaDelFlujo(_contabilidad.Id, "CONTABILIDAD", "Contabilidad", 1, false, false, null)])], PoliticaDevolucion.Inmediata, false);

    private DatosDeApertura Datos() =>
        new(_empresa, Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", _tercero, "Aceros S.A.S.", "CO|NIT|900123456", Guid.NewGuid(),
            "CLIENTE", "Cliente", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

    private (RequisitoDeSeccionPedido General, RequisitoDeSeccionPedido Tributaria) Pedidos() =>
        (new RequisitoDeSeccionPedido(Guid.NewGuid(), Pide(CodigosDeSeccion.General, _compras), CatalogoDeSecciones.Requerida(CodigosDeSeccion.General), null),
            new RequisitoDeSeccionPedido(Guid.NewGuid(), Pide(CodigosDeSeccion.Tributaria, _contabilidad, exigeSoporte: true),
                CatalogoDeSecciones.Requerida(CodigosDeSeccion.Tributaria), null));

    [Fact]
    public void Una_seccion_incompleta_para_la_empresa_deja_el_requisito_pendiente_con_su_motivo_y_completarla_lo_pasa_a_revision()
    {
        var (general, tributaria) = Pedidos();
        var generalV1 = Seccion(CodigosDeSeccion.General, null, (true, 0, "{}"));
        var tributariaV1 = Seccion(CodigosDeSeccion.Tributaria, null, (true, 0, "{}"));
        var eventos = Expediente.Abrir(Datos(), Flujo(), Ahora,
            [general.Calcular(RequisitosDeSeccion.Evaluar(general, generalV1), null), tributaria.Calcular(RequisitosDeSeccion.Evaluar(tributaria, tributariaV1), null)]);
        var e = Expediente.Reproducir(null, eventos);

        e.Estado.ShouldBe(EstadoExpediente.Abierto);
        var rGeneral = e.BuscarRequisito(general.RequisitoId)!;
        (rGeneral.EsSeccion, rGeneral.Seccion, rGeneral.Codigo, rGeneral.Nombre, rGeneral.Estado).ShouldBe(
            (true, "FICHA_GENERAL", "FICHA_GENERAL", "Información general", EstadoRequisito.Cargado));
        var rTributaria = e.BuscarRequisito(tributaria.RequisitoId)!;
        rTributaria.Estado.ShouldBe(EstadoRequisito.Pendiente);
        rTributaria.VersionActual.ShouldBeNull();
        rTributaria.Completitud!.Faltantes.ShouldHaveSingleItem().Mensaje.ShouldBe("Falta el soporte de la información tributaria.");

        // La misma evaluación otra vez no emite nada (idempotente).
        var mismas = new Dictionary<Guid, EvaluacionDeSeccion> { [tributaria.RequisitoId] = RequisitosDeSeccion.Evaluar(tributaria, tributariaV1) };
        e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora, mismas).ShouldBeEmpty();

        // El tercero agrega el soporte ⇒ versión 2 completa: se guarda la completitud, se adjunta y pasa a revisión.
        var tributariaV2 = Seccion(CodigosDeSeccion.Tributaria, null, (true, 0, "{}"), (true, 1, "{}"));
        var sincronizados = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora,
            new Dictionary<Guid, EvaluacionDeSeccion> { [tributaria.RequisitoId] = RequisitosDeSeccion.Evaluar(tributaria, tributariaV2) });
        sincronizados.Select(x => x.GetType().Name).ShouldBe(
            ["CompletitudDeSeccionEvaluada", "VersionAdjuntadaARequisito", "ExpedienteEnviadoARevision", "EtapaActivada"]);
        e = Expediente.Reproducir(e, sincronizados);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.BuscarRequisito(tributaria.RequisitoId)!.Completitud!.Completa.ShouldBeTrue();
        e.DocumentosPorDecidir(_contabilidad.Id).ShouldBe([new DocumentoPorDecidir(tributaria.RequisitoId, 2)]);

        // Una versión nueva incompleta no se adjunta: queda la anterior y la completitud dice qué le falta a la nueva.
        var tributariaV3 = Seccion(CodigosDeSeccion.Tributaria, null, (true, 0, "{}"), (true, 1, "{}"), (false, 1, "{}"));
        var incompleta = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora,
            new Dictionary<Guid, EvaluacionDeSeccion> { [tributaria.RequisitoId] = RequisitosDeSeccion.Evaluar(tributaria, tributariaV3) });
        incompleta.ShouldHaveSingleItem().ShouldBeOfType<CompletitudDeSeccionEvaluada>().Completitud.Version.ShouldBe(3);
        Expediente.Reproducir(e, incompleta).BuscarRequisito(tributaria.RequisitoId)!.VersionActual!.Numero.ShouldBe(2);
    }

    [Fact]
    public void Veredictos_de_seccion_por_area_con_campos_senalados_y_aceptacion_al_aprobar()
    {
        var (general, tributaria) = Pedidos();
        var completa = Seccion(CodigosDeSeccion.Tributaria, null, (true, 1, "{}"));
        var e = Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(), Ahora,
        [
            general.Calcular(RequisitosDeSeccion.Evaluar(general, Seccion(CodigosDeSeccion.General, null, (true, 0, "{}"))), null),
            tributaria.Calcular(RequisitosDeSeccion.Evaluar(tributaria, completa), null)
        ]));
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        // Compras aprueba la general (decisión de área); Contabilidad observa la tributaria señalando un campo.
        e = Expediente.Reproducir(e, e.AprobarArea(_compras.Id, _ana, new Dictionary<Guid, int> { [general.RequisitoId] = 1 }, Ahora));
        e.BuscarArea(_compras.Id)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        Codigo(() => e.EmitirVeredictoDeRequisito(tributaria.RequisitoId, _contabilidad.Id, _beto, Veredicto.Aprobado, 1, [], null, Ahora, ["ica[0].ciiu"]))
            .ShouldBe("campos-solo-al-observar");
        var observado = Expediente.Reproducir(e, e.ObservarArea(_contabilidad.Id, _beto,
            [new ObservacionDeRequisito(tributaria.RequisitoId, 1, [NoCoinciden], "El CIIU de ICA no es el del RUT.", [" ica[0].ciiu ", "ica[0].ciiu"])], Ahora));
        observado.Estado.ShouldBe(EstadoExpediente.Observado);
        var veredicto = observado.ObservacionesVigentes.ShouldHaveSingleItem();
        veredicto.Campos.ShouldBe(["ica[0].ciiu"]);
        veredicto.RequisitoId.ShouldBe(tributaria.RequisitoId);

        // El tercero corrige en el wizard ⇒ versión 2 completa: invalida la observación y, al reenviar, vuelve a revisión.
        var corregida = Seccion(CodigosDeSeccion.Tributaria, null, (true, 1, "{}"), (true, 1, "{}"));
        observado = Expediente.Reproducir(observado, observado.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora,
            new Dictionary<Guid, EvaluacionDeSeccion> { [tributaria.RequisitoId] = RequisitosDeSeccion.Evaluar(tributaria, corregida) }));
        observado.BuscarRequisito(tributaria.RequisitoId)!.Estado.ShouldBe(EstadoRequisito.Cargado);
        var reenviado = Expediente.Reproducir(observado, observado.Reenviar(OrigenReenvio.Tercero, null, Ahora));
        reenviado.Estado.ShouldBe(EstadoExpediente.EnRevision);

        // Contabilidad aprueba la versión 2 ⇒ expediente aprobado y cada sección aceptada por la empresa (punto de F3).
        var cierre = reenviado.EmitirVeredictoDeRequisito(tributaria.RequisitoId, _contabilidad.Id, _beto, Veredicto.Aprobado, 2, [], null, Ahora);
        var aceptadas = cierre.OfType<SeccionAceptadaPorEmpresa>().ToList();
        aceptadas.Select(a => (a.Seccion, a.Version, a.SeccionId)).ShouldBe(
            [("FICHA_GENERAL", 1, SeccionDeFicha.IdPara(_tercero, "FICHA_GENERAL", null)), ("FICHA_TRIBUTARIA", 2, completa.Id)]);
        var orden = cierre.ToList();
        orden.IndexOf(orden.OfType<ExpedienteAprobado>().Single()).ShouldBeLessThan(orden.IndexOf(aceptadas[0]));
        var aprobado = Expediente.Reproducir(reenviado, cierre);
        aprobado.Estado.ShouldBe(EstadoExpediente.Aprobado);
        aprobado.Requisitos.ShouldAllBe(r => r.AceptadaEn == Ahora);
    }

    [Fact]
    public void Los_campos_senalados_solo_aplican_a_secciones()
    {
        var documento = new RequisitoCalculado(Guid.NewGuid(), Guid.NewGuid(), "RUT", "RUT", "RUT", AmbitoDocumento.Global, true, [_compras.Id, _contabilidad.Id],
            null, new VersionDeRequisito(Guid.NewGuid(), 1, "sha", Ahora));
        var e = Expediente.Reproducir(null, Expediente.Abrir(Datos(), Flujo(), Ahora, [documento]));

        Codigo(() => e.EmitirVeredictoDeRequisito(documento.RequisitoId, _compras.Id, _ana, Veredicto.Observado, 1, [NoCoinciden], null, Ahora, ["razon_social"]))
            .ShouldBe("campos-solo-en-secciones");
        // Sin campos, el documento se observa como siempre.
        e.EmitirVeredictoDeRequisito(documento.RequisitoId, _compras.Id, _ana, Veredicto.Observado, 1, [NoCoinciden], null, Ahora)
            .OfType<VeredictoDeRequisitoEmitido>().ShouldHaveSingleItem().Campos.ShouldBeNull();
    }

    // ---------- comparación anterior → nuevo ----------

    [Fact]
    public void Comparacion_de_campos_simples_y_anidados_solo_trae_lo_que_cambio()
    {
        var anterior = Json("""
            { "razon_social": "Aceros S.A.S.", "direccion": "Calle 45 # 12-08", "pagina_web": "https://aceros.co", "certificado_iso": false,
              "direccion_estructurada": { "tipo_via": "Calle", "placa": "08" } }
            """);
        var nuevo = Json("""
            { "razon_social": "Aceros S.A.S.", "direccion": "Carrera 7 # 80-12", "telefono": "+57 601 555 1234", "certificado_iso": true,
              "direccion_estructurada": { "tipo_via": "Carrera", "placa": "08" } }
            """);

        ComparacionDeSecciones.Comparar(anterior, nuevo).ShouldBe(
        [
            new CambioDeCampo("direccion", "Dirección", "Calle 45 # 12-08", "Carrera 7 # 80-12", TipoDeCambio.Modificado),
            new CambioDeCampo("telefono", "Teléfono", null, "+57 601 555 1234", TipoDeCambio.Agregado),
            new CambioDeCampo("certificado_iso", "Certificado ISO", "No", "Sí", TipoDeCambio.Modificado),
            new CambioDeCampo("direccion_estructurada.tipo_via", "Dirección estructurada · Tipo de vía", "Calle", "Carrera", TipoDeCambio.Modificado),
            new CambioDeCampo("pagina_web", "Página web", "https://aceros.co", null, TipoDeCambio.Quitado)
        ]);
        ComparacionDeSecciones.Comparar(nuevo, nuevo).ShouldBeEmpty();
        ComparacionDeSecciones.Comparar(null, Json("""{ "activos": 1000.5 }""")).ShouldHaveSingleItem()
            .ShouldBe(new CambioDeCampo("activos", "Activos", null, "1000.5", TipoDeCambio.Agregado));
    }

    [Fact]
    public void Comparacion_de_filas_personas_ica_y_conjuntos_de_codigos()
    {
        var anterior = Json("""
            { "personas": [
                { "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "79123456", "nombres": "Ana", "apellidos": "Ruiz", "pep": false },
                { "rol": "Socio", "tipo_identificacion": "CC", "numero": "80111222", "nombres": "Luis", "apellidos": "Gil", "porcentaje": 60 },
                { "rol": "Socio", "tipo_identificacion": "CC", "numero": "80333444", "nombres": "Eva", "apellidos": "Paz", "porcentaje": 40 } ],
              "ica": [ { "municipio": "11001", "ciiu": "4663", "tarifa_por_mil": 9.66 } ],
              "responsabilidades": ["05", "07", "48"] }
            """);
        var nuevo = Json("""
            { "personas": [
                { "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "79123456", "nombres": "Ana", "apellidos": "Ruiz", "pep": false },
                { "rol": "Socio", "tipo_identificacion": "CC", "numero": "80111222", "nombres": "Luis", "apellidos": "Gil", "porcentaje": 70 },
                { "rol": "Socio", "tipo_identificacion": "CE", "numero": "555666", "nombres": "Mia", "apellidos": "Sol", "porcentaje": 30 } ],
              "ica": [ { "municipio": "11001", "ciiu": "4663", "tarifa_por_mil": 11.04 }, { "municipio": "05001", "ciiu": "4663" } ],
              "responsabilidades": ["05", "48", "52"] }
            """);

        ComparacionDeSecciones.Comparar(anterior, nuevo).ShouldBe(
        [
            new CambioDeCampo("personas[1].porcentaje", "Socio: Luis Gil (CC 80111222) · Porcentaje de participación", "60", "70", TipoDeCambio.Modificado),
            new CambioDeCampo("personas[2]", "Persona relacionada", null, "Socio: Mia Sol (CE 555666)", TipoDeCambio.Agregado),
            new CambioDeCampo("personas[2]", "Persona relacionada", "Socio: Eva Paz (CC 80333444)", null, TipoDeCambio.Quitado),
            new CambioDeCampo("ica[0].tarifa_por_mil", "11001 · CIIU 4663 · Tarifa por mil", "9.66", "11.04", TipoDeCambio.Modificado),
            new CambioDeCampo("ica[1]", "Municipio de ICA", null, "05001 · CIIU 4663", TipoDeCambio.Agregado),
            new CambioDeCampo("responsabilidades", "Responsabilidades (casilla 53 del RUT)", "07", null, TipoDeCambio.Quitado),
            new CambioDeCampo("responsabilidades", "Responsabilidades (casilla 53 del RUT)", null, "52", TipoDeCambio.Agregado)
        ]);

        // Especialidades: conjunto de códigos.
        ComparacionDeSecciones.Comparar(Json("""{ "especialidades": ["01.01.001"] }"""), Json("""{ "especialidades": ["01.01.001", "02.01.004"] }"""))
            .ShouldHaveSingleItem().ShouldBe(new CambioDeCampo("especialidades", "Especialidad", null, "02.01.004", TipoDeCambio.Agregado));
    }

}
