using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;
using C = Validasoft.Dominio.Flujos.CondicionDeExigencia;
using Op = Validasoft.Dominio.Flujos.OperadorCondicion;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Flujo por reglas (FR-1, docs/03 §12.1): tabla de casos de la elección (el primero que cumple por prioridad, si no el por defecto),
/// la condición congelada con la versión publicada, la validación de condición y prioridad y lo que queda registrado en el expediente.
/// </summary>
public class SeleccionDeFlujoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(-5));

    private const string Riesgo = "vinculo.atributos.nivelRiesgo";
    private const string Monto = "vinculo.atributos.montoEstimado";

    private readonly Guid _empresa = Guid.NewGuid();
    private readonly TipoVinculo _proveedor = TipoVinculo.Semilla().Single(t => t.Codigo == "PROVEEDOR");
    private readonly List<Area> _areas;

    public SeleccionDeFlujoTests() => _areas = [Area.Crear(_empresa, "COMPRAS", "Compras")];

    private static ContextoDeExigencia Contexto(TipoPersona persona = TipoPersona.Juridica, string pais = "CO", bool extranjero = false, string? atributos = null) =>
        new(persona, pais, "NIT", extranjero, "PROVEEDOR",
            atributos is null ? new Dictionary<string, JsonElement>() : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(atributos)!);

    private Flujo Publicado(string codigo, C? condicion = null, int? prioridad = null)
    {
        var definicion = DefinicionDeFlujo.TodasEnParalelo(_areas) with
        {
            Seleccion = condicion is null && prioridad is null ? null : new SeleccionDeFlujo(condicion, prioridad)
        };
        var flujo = Flujo.Crear(_empresa, codigo, "Flujo " + codigo, _proveedor.Id, definicion, _areas, Ahora, null, _proveedor);
        flujo.Publicar(_areas, Ahora, null, _proveedor);
        return flujo;
    }

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- elección: tabla de casos ----------

    public sealed record Regla(string Codigo, int Prioridad, C Condicion);

    private static readonly Regla Critico = new("CRITICO", 1, C.Predicado(Riesgo, Op.En, "Alto"));
    private static readonly Regla Grande = new("GRANDE", 2, C.Predicado(Monto, Op.Mayor, "1000"));

    public static TheoryData<string, Regla[], ContextoDeExigencia, string, OrigenFlujo, int?> Casos => new()
    {
        { "sin reglas: el por defecto", [], Contexto(), "DEFECTO", OrigenFlujo.PorDefecto, null },
        { "proveedor crítico: riesgo alto", [Critico], Contexto(atributos: """{"nivelRiesgo": "Alto"}"""), "CRITICO", OrigenFlujo.Regla, 1 },
        { "ocasional: riesgo bajo cae al por defecto", [Critico], Contexto(atributos: """{"nivelRiesgo": "Bajo"}"""), "DEFECTO", OrigenFlujo.PorDefecto, null },
        { "sin el dato «riesgo» «es alto» no cumple", [Critico], Contexto(), "DEFECTO", OrigenFlujo.PorDefecto, null },
        { "sin el dato «riesgo» «no es alto» sí cumple", [new("ORDINARIO", 1, C.Predicado(Riesgo, Op.Distinto, "Alto"))], Contexto(), "ORDINARIO", OrigenFlujo.Regla, 1 },
        { "gana la de menor prioridad aunque otra también cumpla", [Grande, Critico],
            Contexto(atributos: """{"nivelRiesgo": "Alto", "montoEstimado": 5000}"""), "CRITICO", OrigenFlujo.Regla, 1 },
        { "la primera no cumple y gana la segunda (2 evaluadas)", [Critico, Grande],
            Contexto(atributos: """{"nivelRiesgo": "Bajo", "montoEstimado": 5000}"""), "GRANDE", OrigenFlujo.Regla, 2 },
        { "ninguna cumple: el por defecto", [Critico, Grande], Contexto(atributos: """{"nivelRiesgo": "Medio", "montoEstimado": 10}"""), "DEFECTO", OrigenFlujo.PorDefecto, null },
        { "una prioridad repetida se resuelve por código (la validación la evita al publicar)",
            [new("ZETA", 1, C.Predicado("tercero.tipoPersona", Op.Igual, "Juridica")), new("ALFA", 1, C.Predicado("tercero.pais", Op.Igual, "CO"))],
            Contexto(), "ALFA", OrigenFlujo.Regla, 1 },
        { "jurídica nacional con monto alto (Y)",
            [new("NACIONAL_GRANDE", 1, C.Todas(C.Predicado("tercero.tipoPersona", Op.Igual, "Juridica"), C.Predicado("tercero.pais", Op.Igual, "CO"),
                C.Predicado(Monto, Op.Mayor, "500")))],
            Contexto(atributos: """{"montoEstimado": 800}"""), "NACIONAL_GRANDE", OrigenFlujo.Regla, 1 },
        { "la misma regla Y con otro país cae al por defecto",
            [new("NACIONAL_GRANDE", 1, C.Todas(C.Predicado("tercero.tipoPersona", Op.Igual, "Juridica"), C.Predicado("tercero.pais", Op.Igual, "CO"),
                C.Predicado(Monto, Op.Mayor, "500")))],
            Contexto(pais: "PE", extranjero: true, atributos: """{"montoEstimado": 800}"""), "DEFECTO", OrigenFlujo.PorDefecto, null },
        { "extranjero o persona natural (O)",
            [new("SIMPLIFICADO", 1, C.Alguna(C.Predicado("tercero.esExtranjero", Op.Igual, "true"), C.Predicado("tercero.tipoPersona", Op.Igual, "Natural")))],
            Contexto(TipoPersona.Natural), "SIMPLIFICADO", OrigenFlujo.Regla, 1 },
        { "no (jurídica)", [new("NO_JURIDICA", 1, C.Negar(C.Predicado("tercero.tipoPersona", Op.Igual, "Juridica")))],
            Contexto(TipoPersona.Consorcio), "NO_JURIDICA", OrigenFlujo.Regla, 1 },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Elige_el_flujo_de_la_tabla_de_casos(string caso, Regla[] reglas, ContextoDeExigencia contexto, string esperado, OrigenFlujo origen, int? evaluadas)
    {
        var defecto = Publicado("DEFECTO");
        var flujos = new List<Flujo> { defecto };
        flujos.AddRange(reglas.Select(r => Publicado(r.Codigo, r.Condicion, r.Prioridad)));

        var seleccion = EvaluadorDeSeleccion.Seleccionar(flujos, defecto, contexto);

        seleccion.Flujo!.Codigo.ShouldBe(esperado, caso);
        seleccion.Origen.ShouldBe(origen, caso);
        seleccion.Version.ShouldBe(seleccion.Flujo.VersionPublicada, caso);
        seleccion.Regla?.ReglasEvaluadas.ShouldBe(evaluadas ?? 0, caso);
        (seleccion.Regla is not null).ShouldBe(origen == OrigenFlujo.Regla, caso);
        seleccion.Reglas.Count.ShouldBe(reglas.Length, caso);
    }

    [Fact]
    public void La_regla_registrada_trae_prioridad_y_condicion_de_la_version_publicada()
    {
        var defecto = Publicado("DEFECTO");
        var critico = Publicado("CRITICO", Critico.Condicion, 7);

        var seleccion = EvaluadorDeSeleccion.Seleccionar([defecto, critico], defecto, Contexto(atributos: """{"nivelRiesgo": "Alto"}"""));

        // Las condiciones llevan listas: se comparan por contenido, no por referencia.
        var regla = seleccion.Regla!;
        regla.Prioridad.ShouldBe(7);
        regla.ReglasEvaluadas.ShouldBe(1);
        regla.Condicion.Atributo.ShouldBe(Riesgo);
        regla.Condicion.Operador.ShouldBe(Op.En);
        regla.Condicion.Valores.ShouldBe(["Alto"]);
        seleccion.Reglas.ShouldHaveSingleItem().Cumple.ShouldBeTrue();
    }

    [Fact]
    public void Las_reglas_se_listan_en_orden_de_evaluacion_con_su_resultado_aunque_alguna_no_cumpla()
    {
        var defecto = Publicado("DEFECTO");
        var flujos = new[] { defecto, Publicado("GRANDE", Grande.Condicion, 2), Publicado("CRITICO", Critico.Condicion, 1) };

        var seleccion = EvaluadorDeSeleccion.Seleccionar(flujos, defecto, Contexto(atributos: """{"nivelRiesgo": "Bajo", "montoEstimado": 5000}"""));

        seleccion.Reglas.Select(r => (r.Flujo.Codigo, r.Prioridad, r.Cumple)).ShouldBe([("CRITICO", 1, false), ("GRANDE", 2, true)]);
    }

    [Fact]
    public void Solo_cuentan_los_flujos_publicados_y_la_condicion_es_la_de_la_version_publicada()
    {
        var defecto = Publicado("DEFECTO");
        var alto = Contexto(atributos: """{"nivelRiesgo": "Alto"}""");
        var medio = Contexto(atributos: """{"nivelRiesgo": "Medio"}""");

        // Un flujo que aún solo es borrador no participa, aunque tenga condición.
        var borrador = Flujo.Crear(_empresa, "BORRADOR", "Borrador", _proveedor.Id,
            DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = new SeleccionDeFlujo(Critico.Condicion, 1) }, _areas, Ahora, null, _proveedor);
        EvaluadorDeSeleccion.Seleccionar([defecto, borrador], defecto, alto).Origen.ShouldBe(OrigenFlujo.PorDefecto);

        // Publicada con «riesgo alto»; el borrador siguiente cambia a «riesgo medio» pero no rige hasta publicarlo (la condición se congela).
        var flujo = Publicado("CRITICO", Critico.Condicion, 1);
        flujo.EditarBorrador(null, DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = new SeleccionDeFlujo(C.Predicado(Riesgo, Op.En, "Medio"), 1) },
            _areas, Ahora.AddDays(1), null, _proveedor);
        EvaluadorDeSeleccion.Seleccionar([defecto, flujo], defecto, alto).Flujo!.Codigo.ShouldBe("CRITICO");
        EvaluadorDeSeleccion.Seleccionar([defecto, flujo], defecto, medio).Origen.ShouldBe(OrigenFlujo.PorDefecto);

        flujo.Publicar(_areas, Ahora.AddDays(2), null, _proveedor);
        EvaluadorDeSeleccion.Seleccionar([defecto, flujo], defecto, alto).Origen.ShouldBe(OrigenFlujo.PorDefecto);
        EvaluadorDeSeleccion.Seleccionar([defecto, flujo], defecto, medio).Flujo!.Codigo.ShouldBe("CRITICO");
    }

    [Fact]
    public void Sin_flujo_por_defecto_publicado_y_sin_regla_que_aplique_no_hay_flujo()
    {
        var sinPublicar = Flujo.Crear(_empresa, "DEFECTO", "Defecto", _proveedor.Id, DefinicionDeFlujo.TodasEnParalelo(_areas), _areas, Ahora, null, _proveedor);

        EvaluadorDeSeleccion.Seleccionar([sinPublicar], sinPublicar, Contexto()).Flujo.ShouldBeNull();
        EvaluadorDeSeleccion.Seleccionar([], null, Contexto()).Flujo.ShouldBeNull();
        EvaluadorDeSeleccion.SoloPorDefecto(null).Origen.ShouldBe(OrigenFlujo.PorDefecto);
    }

    [Fact]
    public void El_flujo_por_defecto_tambien_puede_tener_regla_y_entonces_gana_por_regla()
    {
        var defecto = Publicado("DEFECTO", Critico.Condicion, 1);

        var porRegla = EvaluadorDeSeleccion.Seleccionar([defecto], defecto, Contexto(atributos: """{"nivelRiesgo": "Alto"}"""));
        porRegla.Origen.ShouldBe(OrigenFlujo.Regla);

        var sinRegla = EvaluadorDeSeleccion.Seleccionar([defecto], defecto, Contexto());
        sinRegla.Origen.ShouldBe(OrigenFlujo.PorDefecto);
        sinRegla.Flujo.ShouldBe(defecto);
    }

    [Fact]
    public void Un_flujo_de_otro_tipo_de_vinculo_no_se_mezcla_porque_el_llamador_entrega_solo_los_del_tipo()
    {
        // La función no filtra por tipo (lo hace quien carga los flujos); un flujo sin selección nunca es candidato.
        var sinRegla = Publicado("SIN_REGLA");
        EvaluadorDeSeleccion.Reglas([sinRegla]).ShouldBeEmpty();
    }

    // ---------- validación de la condición y la prioridad ----------

    [Fact]
    public void Una_condicion_con_su_prioridad_se_guarda_normalizada_en_la_version()
    {
        var flujo = Publicado("CRITICO", C.Predicado(Riesgo, Op.En, " Alto "), 3);

        var guardada = flujo.VersionPublicada!.Seleccion!;
        guardada.Prioridad.ShouldBe(3);
        guardada.Condicion!.Valores.ShouldBe(["Alto"]); // sin los espacios
        flujo.VersionPublicada.Definicion().Seleccion.ShouldBeSameAs(guardada);
    }

    public static TheoryData<string, SeleccionDeFlujo?, string?> Validaciones => new()
    {
        { "nada que seleccionar", null, null },
        { "sin condición ni prioridad", new SeleccionDeFlujo(null, null), null },
        { "un grupo Y vacío es «sin condición»", new SeleccionDeFlujo(new C(TipoNodoCondicion.Y, []), null), null },
        { "prioridad sin condición", new SeleccionDeFlujo(null, 1), "seleccion-sin-condicion" },
        { "condición sin prioridad", new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), null), "prioridad-requerida" },
        { "prioridad cero", new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), 0), "prioridad-invalida" },
        { "prioridad negativa", new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), -2), "prioridad-invalida" },
        { "prioridad sobre el máximo", new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), 1000), "prioridad-invalida" },
        { "la prioridad máxima vale", new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), 999), null },
        { "atributo desconocido", new SeleccionDeFlujo(C.Predicado("tercero.color", Op.Igual, "rojo"), 1), "condicion-invalida" },
        { "atributo del vínculo que el tipo no tiene", new SeleccionDeFlujo(C.Predicado("vinculo.atributos.inexistente", Op.Igual, "x"), 1), "condicion-invalida" },
        { "opción que no existe en el atributo", new SeleccionDeFlujo(C.Predicado(Riesgo, Op.En, "Crítico"), 1), "condicion-invalida" },
        { "operador que el atributo no admite", new SeleccionDeFlujo(C.Predicado(Riesgo, Op.Mayor, "Alto"), 1), "condicion-invalida" },
    };

    [Theory]
    [MemberData(nameof(Validaciones))]
    public void Valida_la_condicion_y_la_prioridad_al_guardar(string caso, SeleccionDeFlujo? seleccion, string? codigo)
    {
        var definicion = DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = seleccion };

        if (codigo is null)
            Should.NotThrow(() => definicion.Validar(_empresa, _areas, null, _proveedor), caso);
        else
            Codigo(() => definicion.Validar(_empresa, _areas, null, _proveedor)).ShouldBe(codigo, caso);
    }

    [Fact]
    public void Sin_seleccion_la_definicion_queda_sin_regla_y_el_flujo_por_defecto_no_necesita_condicion()
    {
        var defecto = Publicado("DEFECTO");

        defecto.VersionPublicada!.Seleccion.ShouldBeNull();
        EvaluadorDeSeleccion.Reglas([defecto]).ShouldBeEmpty();
    }

    [Fact]
    public void Una_condicion_sobre_atributos_del_vinculo_se_valida_con_el_tipo_del_flujo()
    {
        var cliente = TipoVinculo.Semilla().Single(t => t.Codigo == "CLIENTE");
        var definicion = DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = new SeleccionDeFlujo(C.Predicado(Monto, Op.Mayor, "10"), 1) };

        Should.NotThrow(() => definicion.Validar(_empresa, _areas, null, _proveedor)); // PROVEEDOR tiene montoEstimado
        Codigo(() => definicion.Validar(_empresa, _areas, null, cliente)).ShouldBe("condicion-invalida"); // CLIENTE no
    }

    // ---------- una prioridad no se repite dentro del tipo ----------

    [Fact]
    public void Una_prioridad_no_se_repite_entre_los_flujos_publicados_del_mismo_tipo()
    {
        var critico = Publicado("CRITICO", Critico.Condicion, 1);
        var nuevo = Publicado("OTRO", Grande.Condicion, 2);

        var error = Should.Throw<ReglaDeNegocioException>(() =>
            EvaluadorDeSeleccion.ValidarPrioridadUnica(nuevo, new SeleccionDeFlujo(Grande.Condicion, 1), [critico, nuevo]));
        error.Codigo.ShouldBe("prioridad-duplicada");
        error.Tipo.ShouldBe(TipoFalla.Validacion);
        error.Datos!["flujo"].ShouldBe("CRITICO");
        error.Datos["prioridad"].ShouldBe(1);

        Should.NotThrow(() => EvaluadorDeSeleccion.ValidarPrioridadUnica(nuevo, new SeleccionDeFlujo(Grande.Condicion, 5), [critico, nuevo]));
    }

    [Fact]
    public void La_prioridad_de_un_flujo_no_choca_con_la_suya_ni_con_flujos_sin_publicar_ni_con_otro_tipo_de_vinculo()
    {
        var critico = Publicado("CRITICO", Critico.Condicion, 1);

        // Su propia prioridad publicada se puede conservar al publicar una versión nueva.
        Should.NotThrow(() => EvaluadorDeSeleccion.ValidarPrioridadUnica(critico, new SeleccionDeFlujo(Critico.Condicion, 1), [critico]));

        // Un borrador de otro flujo con esa prioridad todavía no rige: no cuenta.
        var borrador = Flujo.Crear(_empresa, "BORRADOR", "Borrador", _proveedor.Id,
            DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = new SeleccionDeFlujo(Grande.Condicion, 1) }, _areas, Ahora, null, _proveedor);
        Should.NotThrow(() => EvaluadorDeSeleccion.ValidarPrioridadUnica(critico, new SeleccionDeFlujo(Critico.Condicion, 1), [borrador]));

        // Otro tipo de vínculo y otra empresa tienen su propia numeración.
        var otroTipo = Flujo.Crear(_empresa, "OTRO_TIPO", "Otro tipo", Guid.NewGuid(),
            DefinicionDeFlujo.TodasEnParalelo(_areas) with { Seleccion = new SeleccionDeFlujo(C.Predicado("tercero.pais", Op.Igual, "CO"), 1) }, _areas, Ahora);
        otroTipo.Publicar(_areas, Ahora);
        Should.NotThrow(() => EvaluadorDeSeleccion.ValidarPrioridadUnica(borrador, new SeleccionDeFlujo(Grande.Condicion, 1), [otroTipo]));
    }

    [Fact]
    public void Retirar_una_version_libera_su_prioridad()
    {
        var critico = Publicado("CRITICO", Critico.Condicion, 1);
        var nuevo = Publicado("OTRO", Grande.Condicion, 2);

        // CRITICO publica una versión 2 sin selección: la prioridad 1 queda libre.
        critico.EditarBorrador(null, DefinicionDeFlujo.TodasEnParalelo(_areas), _areas, Ahora.AddDays(1), null, _proveedor);
        critico.Publicar(_areas, Ahora.AddDays(1), null, _proveedor);

        Should.NotThrow(() => EvaluadorDeSeleccion.ValidarPrioridadUnica(nuevo, new SeleccionDeFlujo(Grande.Condicion, 1), [critico, nuevo]));
    }

    // ---------- lo que queda registrado en el expediente ----------

    private static FlujoParaExpediente ParaExpediente(OrigenFlujo origen, SeleccionPorRegla? regla) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "CRITICO", "Proveedor crítico", 3, origen,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(Guid.NewGuid(), "COMPRAS", "Compras", 1, false, false, null)])], PoliticaDevolucion.Inmediata, false,
            Regla: regla);

    private static DatosDeApertura Datos() =>
        new(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", "CO|NIT|900123456",
            Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Usuario, "ana"), []);

    [Fact]
    public void El_expediente_abierto_por_regla_registra_la_prioridad_y_la_condicion_que_coincidieron()
    {
        var regla = new SeleccionPorRegla(2, Critico.Condicion, 2);

        var eventos = Expediente.Abrir(Datos(), ParaExpediente(OrigenFlujo.Regla, regla), Ahora);
        var expediente = Expediente.Reproducir(null, eventos);

        var seleccionado = eventos.OfType<FlujoSeleccionado>().Single();
        seleccionado.Origen.ShouldBe(OrigenFlujo.Regla);
        seleccionado.Regla.ShouldBe(regla);
        seleccionado.Version.ShouldBe(3);
        expediente.Flujo!.Origen.ShouldBe(OrigenFlujo.Regla);
        expediente.Flujo.Regla.ShouldBe(regla);
    }

    [Theory]
    [InlineData(OrigenFlujo.Explicito)]
    [InlineData(OrigenFlujo.PorDefecto)]
    public void Un_flujo_explicito_o_por_defecto_no_registra_regla(OrigenFlujo origen)
    {
        var eventos = Expediente.Abrir(Datos(), ParaExpediente(origen, new SeleccionPorRegla(1, Critico.Condicion, 1)), Ahora);

        eventos.OfType<FlujoSeleccionado>().Single().Regla.ShouldBeNull();
        Expediente.Reproducir(null, eventos).Flujo!.Regla.ShouldBeNull();
    }

    [Fact]
    public void Un_evento_guardado_antes_de_las_reglas_se_lee_sin_regla()
    {
        // Los FlujoSeleccionado anteriores no traen «regla»: el campo opcional queda nulo y el expediente sigue su curso.
        var eventos = Expediente.Abrir(Datos(), ParaExpediente(OrigenFlujo.PorDefecto, null), Ahora);
        var antiguo = eventos.OfType<FlujoSeleccionado>().Single() with { Regla = null };

        Expediente.Reproducir(null, [eventos[0], antiguo, .. eventos.Skip(2)]).Flujo!.Regla.ShouldBeNull();
    }
}
