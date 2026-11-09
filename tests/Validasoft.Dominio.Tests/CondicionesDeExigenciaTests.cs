using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using C = Validasoft.Dominio.Flujos.CondicionDeExigencia;
using Op = Validasoft.Dominio.Flujos.OperadorCondicion;

namespace Validasoft.Dominio.Tests;

/// <summary>«Se pide cuando» (RQ-12, docs/03 §12): tabla de casos del evaluador, validación al guardar y efecto en el expediente.</summary>
public class CondicionesDeExigenciaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(-5));

    private const string Persona = AtributosDeExigencia.TipoPersona;
    private const string Pais = AtributosDeExigencia.Pais;

    private static ContextoDeExigencia Contexto(TipoPersona persona = TipoPersona.Juridica, string pais = "CO", string tipoId = "NIT",
        bool extranjero = false, string tipoVinculo = "PROVEEDOR", string? atributos = null) =>
        new(persona, pais, tipoId, extranjero, tipoVinculo,
            atributos is null ? new Dictionary<string, JsonElement>() : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(atributos)!);

    private static C Monto(Op op, params string[] valores) => C.Predicado("vinculo.atributos.montoAnual", op, valores);

    public static TheoryData<string, C, ContextoDeExigencia, bool> Casos => new()
    {
        // Tipo de persona: a la jurídica se le pide más que a la natural.
        { "jurídica = jurídica", C.Predicado(Persona, Op.Igual, "Juridica"), Contexto(), true },
        { "natural ≠ jurídica", C.Predicado(Persona, Op.Igual, "Juridica"), Contexto(TipoPersona.Natural), false },
        { "consorcio en {jurídica, consorcio}", C.Predicado(Persona, Op.En, "Juridica", "Consorcio"), Contexto(TipoPersona.Consorcio), true },
        { "natural no en {jurídica, consorcio}", C.Predicado(Persona, Op.NoEn, "Juridica", "Consorcio"), Contexto(TipoPersona.Natural), true },
        // País, tipo de identificación, extranjero y tipo de vínculo.
        { "país distinto de CO", C.Predicado(Pais, Op.Distinto, "CO"), Contexto(pais: "PE"), true },
        { "país CO no es distinto de CO", C.Predicado(Pais, Op.Distinto, "CO"), Contexto(), false },
        { "cédula de extranjería", C.Predicado(AtributosDeExigencia.TipoIdentificacion, Op.Igual, "CE"), Contexto(tipoId: "CE"), true },
        { "extranjero", C.Predicado(AtributosDeExigencia.EsExtranjero, Op.Igual, "true"), Contexto(extranjero: true), true },
        { "no extranjero", C.Predicado(AtributosDeExigencia.EsExtranjero, Op.Igual, "true"), Contexto(), false },
        { "tipo de vínculo cliente", C.Predicado(AtributosDeExigencia.TipoVinculo, Op.Igual, "CLIENTE"), Contexto(tipoVinculo: "CLIENTE"), true },
        // Atributos numéricos del vínculo (el «monto anual estimado» del design system).
        { "monto > 500M", Monto(Op.Mayor, "500000000"), Contexto(atributos: """{"montoAnual": 800000000}"""), true },
        { "monto = 500M no es mayor", Monto(Op.Mayor, "500000000"), Contexto(atributos: """{"montoAnual": 500000000}"""), false },
        { "monto 1.0 = 1", Monto(Op.Igual, "1"), Contexto(atributos: """{"montoAnual": 1.0}"""), true },
        { "monto entre (inclusivo)", Monto(Op.Entre, "100", "200"), Contexto(atributos: """{"montoAnual": 200}"""), true },
        { "monto fuera del rango", Monto(Op.Entre, "100", "200"), Contexto(atributos: """{"montoAnual": 201}"""), false },
        { "monto < 100", Monto(Op.Menor, "100"), Contexto(atributos: """{"montoAnual": 99.5}"""), true },
        // Un dato faltante: «se pide si…» no pide; «se pide salvo que…» sí pide; Existe responde por el dato.
        { "sin monto no es mayor", Monto(Op.Mayor, "0"), Contexto(), false },
        { "sin monto es distinto", Monto(Op.Distinto, "0"), Contexto(), true },
        { "sin monto no existe", Monto(Op.Existe), Contexto(), false },
        { "monto existe", Monto(Op.Existe), Contexto(atributos: """{"montoAnual": 0}"""), true },
        { "no existe monto", C.Negar(Monto(Op.Existe)), Contexto(), true },
        // Listas, fechas y opciones.
        { "categoría alguna", C.Predicado("vinculo.atributos.categorias", Op.ContieneAlguno, "OBRA", "DISENO"),
            Contexto(atributos: """{"categorias": ["SUMINISTRO", "OBRA"]}"""), true },
        { "categoría todas (falta una)", C.Predicado("vinculo.atributos.categorias", Op.ContieneTodos, "OBRA", "DISENO"),
            Contexto(atributos: """{"categorias": ["OBRA"]}"""), false },
        { "fecha posterior", C.Predicado("vinculo.atributos.inicio", Op.Mayor, "2026-01-01"), Contexto(atributos: """{"inicio": "2026-03-15"}"""), true },
        { "opción en", C.Predicado("vinculo.atributos.riesgo", Op.En, "ALTO", "MEDIO"), Contexto(atributos: """{"riesgo": "ALTO"}"""), true },
        // Combinaciones.
        { "jurídica Y colombiana", C.Todas(C.Predicado(Persona, Op.Igual, "Juridica"), C.Predicado(Pais, Op.Igual, "CO")), Contexto(pais: "PE"), false },
        { "jurídica O extranjera", C.Alguna(C.Predicado(Persona, Op.Igual, "Juridica"), C.Predicado(AtributosDeExigencia.EsExtranjero, Op.Igual, "true")),
            Contexto(TipoPersona.Natural, extranjero: true), true },
        { "no (natural)", C.Negar(C.Predicado(Persona, Op.Igual, "Natural")), Contexto(), true },
        { "jurídica Y (monto > 500M O extranjera)",
            C.Todas(C.Predicado(Persona, Op.Igual, "Juridica"), C.Alguna(Monto(Op.Mayor, "500000000"), C.Predicado(AtributosDeExigencia.EsExtranjero, Op.Igual, "true"))),
            Contexto(atributos: """{"montoAnual": 10}"""), false },
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Evalua_la_tabla_de_casos(string caso, C condicion, ContextoDeExigencia contexto, bool aplica) =>
        EvaluadorDeExigencia.Aplica(condicion, contexto).ShouldBe(aplica, caso);

    [Fact]
    public void Sin_condicion_se_pide_siempre_y_filtrar_conserva_el_orden_del_flujo()
    {
        EvaluadorDeExigencia.Aplica(null, Contexto(TipoPersona.Natural)).ShouldBeTrue();

        RequisitoDelFlujo R(string codigo, C? condicion = null) => new(Guid.NewGuid(), Guid.NewGuid(), codigo, true, [], null, condicion);
        var juridica = C.Predicado(Persona, Op.Igual, "Juridica");
        var requisitos = new[] { R("RUT"), R("CAMARA_COMERCIO", juridica), R("CEDULA", C.Predicado(Persona, Op.Igual, "Natural")), R("CERT_BANCARIA") };

        EvaluadorDeExigencia.Filtrar(requisitos, Contexto()).Select(r => r.CodigoTipo).ShouldBe(["RUT", "CAMARA_COMERCIO", "CERT_BANCARIA"]);
        EvaluadorDeExigencia.Filtrar(requisitos, Contexto(TipoPersona.Natural)).Select(r => r.CodigoTipo).ShouldBe(["RUT", "CEDULA", "CERT_BANCARIA"]);
    }

    // ---------- validación al guardar el flujo ----------

    private static readonly TipoVinculo Proveedor = TipoVinculo.Crear("PROVEEDOR", "Proveedor",
    [
        new AtributoPermitido("montoAnual", TipoAtributo.Numero, false, null),
        new AtributoPermitido("categorias", TipoAtributo.ListaOpciones, false, ["OBRA", "SUMINISTRO", "DISENO"]),
        new AtributoPermitido("riesgo", TipoAtributo.Opcion, false, ["ALTO", "MEDIO", "BAJO"]),
        new AtributoPermitido("inicio", TipoAtributo.Fecha, false, null),
        new AtributoPermitido("nota", TipoAtributo.Texto, false, null),
    ], false);

    private static string Invalida(C condicion, TipoVinculo? tipo = null) =>
        Should.Throw<ReglaDeNegocioException>(() => AtributosDeExigencia.Validar(condicion, tipo ?? Proveedor, "Cámara de comercio")).Codigo;

    [Fact]
    public void Normaliza_los_valores_y_un_grupo_vacio_es_siempre()
    {
        AtributosDeExigencia.Validar(null, Proveedor, "RUT").ShouldBeNull();
        AtributosDeExigencia.Validar(new C(TipoNodoCondicion.Y, []), Proveedor, "RUT").ShouldBeNull();

        var normalizada = AtributosDeExigencia.Validar(C.Todas(
            C.Predicado(Persona, Op.En, " juridica ", "CONSORCIO", "Juridica"),
            C.Predicado(Pais, Op.Igual, "co"),
            C.Predicado(AtributosDeExigencia.EsExtranjero, Op.Igual, "True"),
            Monto(Op.Entre, "100.50", "200")), Proveedor, "RUT")!;

        normalizada.Nodos![0].Valores.ShouldBe(["Juridica", "Consorcio"]); // nombre exacto y sin repetidos
        normalizada.Nodos[1].Valores.ShouldBe(["CO"]);
        normalizada.Nodos[2].Valores.ShouldBe(["true"]);
        normalizada.Nodos[3].Valores.ShouldBe(["100.50", "200"]);
    }

    [Fact]
    public void Rechaza_atributos_operadores_y_valores_que_no_corresponden()
    {
        Invalida(C.Predicado("tercero.nombre", Op.Igual, "x")).ShouldBe("condicion-invalida"); // atributo desconocido
        Invalida(C.Predicado("vinculo.atributos.noExiste", Op.Existe)).ShouldBe("condicion-invalida"); // el tipo no lo admite
        Invalida(Monto(Op.Mayor, "1"), TipoVinculo.Crear("CLIENTE", "Cliente", [], false)).ShouldBe("condicion-invalida");
        Invalida(C.Predicado(Persona, Op.Mayor, "Juridica")).ShouldBe("condicion-invalida"); // operador de orden en una opción
        Invalida(C.Predicado(Persona, Op.Existe)).ShouldBe("condicion-invalida"); // del tercero siempre hay valor
        Invalida(C.Predicado(Persona, Op.Igual, "Empresa")).ShouldBe("condicion-invalida"); // no es un tipo de persona
        Invalida(C.Predicado(Persona, Op.Igual)).ShouldBe("condicion-invalida"); // falta el valor
        Invalida(C.Predicado(Persona, Op.Igual, "Natural", "Juridica")).ShouldBe("condicion-invalida"); // Igual compara con uno
        Invalida(Monto(Op.Mayor, "1.000.000")).ShouldBe("condicion-invalida"); // número con separador de miles
        Invalida(Monto(Op.Entre, "200", "100")).ShouldBe("condicion-invalida"); // rango al revés
        Invalida(Monto(Op.Existe, "1")).ShouldBe("condicion-invalida"); // Existe no lleva valores
        Invalida(C.Predicado("vinculo.atributos.riesgo", Op.Igual, "CRITICO")).ShouldBe("condicion-invalida"); // no es una opción
        Invalida(C.Predicado("vinculo.atributos.categorias", Op.Igual, "OBRA")).ShouldBe("condicion-invalida"); // una lista se mira con Contiene*
        Invalida(C.Predicado("vinculo.atributos.inicio", Op.Mayor, "15/03/2026")).ShouldBe("condicion-invalida"); // fecha en yyyy-MM-dd
        Invalida(new C(TipoNodoCondicion.No, [C.Predicado(Pais, Op.Igual, "CO"), C.Predicado(Pais, Op.Igual, "PE")])).ShouldBe("condicion-invalida");
        Invalida(new C(TipoNodoCondicion.O, [])).ShouldBe("condicion-invalida");
        Invalida(new C(TipoNodoCondicion.Predicado, null, Pais, null, ["CO"])).ShouldBe("condicion-invalida"); // sin operador

        var profunda = C.Predicado(Pais, Op.Igual, "CO");
        for (var i = 0; i < AtributosDeExigencia.ProfundidadMaxima; i++)
            profunda = C.Todas(profunda);
        Invalida(profunda).ShouldBe("condicion-invalida");

        Invalida(C.Todas([.. Enumerable.Range(0, AtributosDeExigencia.PredicadosMaximos + 1).Select(_ => C.Predicado(Pais, Op.Igual, "CO"))]))
            .ShouldBe("condicion-invalida");
    }

    [Fact]
    public void El_flujo_valida_las_condiciones_con_su_tipo_de_vinculo_y_pide_algo_siempre()
    {
        var empresa = Guid.NewGuid();
        var compras = Area.Crear(empresa, "COMPRAS", "Compras");
        var tipos = TipoDocumentoTenant.Semilla(Guid.NewGuid(), Ahora).ToList();
        RequisitoDelFlujo Pide(string codigo, C? condicion = null) =>
            new(Guid.Empty, tipos.Single(t => t.Codigo == codigo).Id, string.Empty, true, [], null, condicion);
        DefinicionDeFlujo Con(params RequisitoDelFlujo[] r) => DefinicionDeFlujo.TodasEnParalelo([compras]) with { Requisitos = r };

        var juridica = C.Predicado(Persona, Op.Igual, "juridica");
        var flujo = Flujo.Crear(empresa, "PROV", "Proveedores", Proveedor.Id,
            Con(Pide("RUT"), Pide("CAMARA_COMERCIO", juridica), Pide("CEDULA", Monto(Op.Mayor, "500000000"))), [compras], Ahora, tipos, Proveedor);
        flujo.Borrador!.Requisitos[0].Condicion.ShouldBeNull();
        flujo.Borrador.Requisitos[1].Condicion!.Valores.ShouldBe(["Juridica"]);

        // Todo condicionado: algún tercero quedaría sin requisitos y sus áreas decidirían sobre el expediente completo.
        Should.Throw<ReglaDeNegocioException>(() => Con(Pide("RUT", juridica), Pide("CAMARA_COMERCIO", juridica)).Validar(empresa, [compras], tipos, Proveedor))
            .Codigo.ShouldBe("requisito-siempre-requerido");
        // Un atributo del vínculo exige que el tipo de vínculo del flujo lo tenga.
        Should.Throw<ReglaDeNegocioException>(() => Con(Pide("RUT"), Pide("CEDULA", Monto(Op.Mayor, "1"))).Validar(empresa, [compras], tipos))
            .Codigo.ShouldBe("condicion-invalida");
    }

    // ---------- efecto en el expediente ----------

    [Fact]
    public void Un_area_sin_documentos_para_este_tercero_aprueba_sola_como_sistema_y_dice_por_que()
    {
        // Cumplimiento solo revisaba la Cámara, que no se le pidió a esta persona natural: no tiene nada que revisar.
        var compras = new AreaDelFlujo(Guid.NewGuid(), "COMPRAS", "Compras", 1, false, false, null);
        var cumplimiento = new AreaDelFlujo(Guid.NewGuid(), "CUMPLIMIENTO", "Cumplimiento", 1, true, false, null);
        var flujo = new FlujoParaExpediente(Guid.NewGuid(), Guid.NewGuid(), "PROV", "Proveedores", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [compras, cumplimiento])], PoliticaDevolucion.Inmediata, false);
        var rut = new RequisitoCalculado(Guid.NewGuid(), Guid.NewGuid(), "RUT", "RUT", "RUT", AmbitoDocumento.Global, true, [compras.AreaId], null,
            new VersionDeRequisito(Guid.NewGuid(), 1, "sha", Ahora));
        var datos = new DatosDeApertura(Guid.NewGuid(), Guid.NewGuid(), TipoExpediente.Inicial, "NORTE-E-2026-000009", Guid.NewGuid(), "Ana Pérez",
            "CO|CC|1010", Guid.NewGuid(), "PROVEEDOR", "Proveedor", new ActorDeExpediente(ActorTipo.Tercero, "tercero"), []);

        var e = Expediente.Reproducir(null, Expediente.Abrir(datos, flujo, Ahora, [rut]));

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        var area = e.BuscarArea(cumplimiento.AreaId)!;
        area.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        var derivado = area.Veredictos.ShouldHaveSingleItem();
        derivado.Origen.ShouldBe(OrigenVeredicto.Sistema);
        derivado.Justificacion.ShouldBe(Expediente.SinDocumentosQueRevisar);
        e.BuscarArea(compras.AreaId)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
    }
}
