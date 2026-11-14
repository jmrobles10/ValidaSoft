using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- flujo por reglas (FR-1): el flujo se escoge solo según el tercero y el vínculo ----------

    private static CondicionDto RiesgoEs(params string[] valores) =>
        new("Predicado", Atributo: "vinculo.atributos.nivelRiesgo", Operador: "En", Valores: valores);

    private static CondicionDto MontoMayorA(string monto) =>
        new("Predicado", Atributo: "vinculo.atributos.montoEstimado", Operador: "Mayor", Valores: [monto]);

    private static Dictionary<string, JsonElement> Atributos(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    /// <summary>Crea y publica un flujo de proveedores con su regla de selección (condición y prioridad).</summary>
    private async Task<FlujoDto> FlujoConReglaAsync(Empresa empresa, string codigo, CondicionDto? condicion, int? prioridad, params string[] documentos)
    {
        var creado = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(empresa), new CrearFlujoDto(codigo, "Flujo " + codigo, "PROVEEDOR",
            Requisitos: [.. documentos.Select(d => new RequisitoDelFlujoDto(TipoDocumento: d))],
            Seleccion: condicion is null && prioridad is null ? null : new SeleccionDeFlujoDto(condicion, prioridad)), 201);
        return await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{creado.Id}/publicar", Admin(empresa), new { }, 200);
    }

    private Task<VinculoDto> CrearVinculoConAtributosAsync(Empresa empresa, string atributos, string? correo = null, Guid? flujoId = null)
    {
        var nit = Nit();
        correo ??= CorreoNuevo();
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(empresa), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null, [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: "PROVEEDOR", Atributos: Atributos(atributos), CorreoInvitacion: correo, ReferenciaExterna: null, FlujoId: flujoId), 201);
    }

    private Task<ExpedienteDto> AbrirExpedienteAsync(Empresa empresa, Guid vinculoId, Guid? flujoId = null) =>
        ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculoId}/expedientes", Admin(empresa), new AbrirExpedienteDto(flujoId), 201);

    private Task<SeleccionPrevistaDto> VistaPreviaAsync(Empresa empresa, VistaPreviaDeSeleccionDto consulta) =>
        ApiAsync<SeleccionPrevistaDto>("POST", "/api/v1/flujos/seleccion/vista-previa", Admin(empresa), consulta, 200);

    [Fact]
    public async Task Sin_flujo_explicito_gana_la_primera_regla_por_prioridad_y_si_ninguna_cumple_el_por_defecto()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var defecto = await FlujoConAsync(norte, "PROVEEDOR", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var critico = await FlujoConReglaAsync(norte, "CRITICO", RiesgoEs("Alto"), 1, "RUT", "CAMARA_COMERCIO");
        var grande = await FlujoConReglaAsync(norte, "GRANDE", MontoMayorA("1000"), 2, "RUT");
        defecto.EsPorDefecto.ShouldBeTrue();
        critico.EsPorDefecto.ShouldBeFalse(); // publicar con regla no le quita el por defecto al que ya lo era
        critico.Publicada!.Seleccion!.Prioridad.ShouldBe(1);
        critico.Publicada.Seleccion.Condicion!.Valores.ShouldBe(["Alto"]);

        // Proveedor crítico: el tercero acepta la invitación, el expediente se abre solo y queda anotado qué regla coincidió.
        var correo = CorreoNuevo();
        var alto = await CrearVinculoConAtributosAsync(norte, """{"nivelRiesgo": "Alto", "montoEstimado": 5000}""", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, alto.Id);
        var expedienteAlto = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{alto.Id}", Admin(norte), null, 200)).ExpedienteAbiertoId!.Value;
        var delCritico = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteAlto}", Admin(norte), null, 200);
        delCritico.Flujo!.Codigo.ShouldBe("CRITICO");
        delCritico.Flujo.Origen.ShouldBe("Regla");
        delCritico.Flujo.Id.ShouldBe(critico.Id);
        delCritico.Flujo.VersionId.ShouldBe(critico.Publicada.Id);
        delCritico.Flujo.Regla!.Prioridad.ShouldBe(1);
        delCritico.Flujo.Regla.ReglasEvaluadas.ShouldBe(1);
        delCritico.Flujo.Regla.Condicion.Atributo.ShouldBe("vinculo.atributos.nivelRiesgo");
        delCritico.Flujo.Regla.Condicion.Valores.ShouldBe(["Alto"]);
        delCritico.Requisitos!.Select(r => r.TipoDocumento.Codigo).Order().ShouldBe(["CAMARA_COMERCIO", "RUT"]); // abrió con los documentos del flujo escogido

        // Apertura manual: riesgo bajo pero monto alto ⇒ la segunda regla (2 evaluadas).
        var porMonto = await AbrirExpedienteAsync(norte, (await CrearVinculoConAtributosAsync(norte, """{"nivelRiesgo": "Bajo", "montoEstimado": 5000}""")).Id);
        porMonto.Flujo!.Codigo.ShouldBe("GRANDE");
        porMonto.Flujo.Origen.ShouldBe("Regla");
        porMonto.Flujo.Regla!.Prioridad.ShouldBe(2);
        porMonto.Flujo.Regla.ReglasEvaluadas.ShouldBe(2);

        // Ninguna cumple (o falta el dato): el por defecto, sin regla anotada.
        foreach (var atributos in new[] { """{"nivelRiesgo": "Bajo", "montoEstimado": 10}""", "{}" })
        {
            var ocasional = await AbrirExpedienteAsync(norte, (await CrearVinculoConAtributosAsync(norte, atributos)).Id);
            ocasional.Flujo!.Id.ShouldBe(defecto.Id);
            ocasional.Flujo.Origen.ShouldBe("PorDefecto");
            ocasional.Flujo.Regla.ShouldBeNull();
        }

        // Lo anotado sobrevive a la lectura posterior del expediente (viene del evento guardado).
        var releido = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{porMonto.Id}", Admin(norte), null, 200);
        releido.Flujo!.Regla!.Condicion.Atributo.ShouldBe("vinculo.atributos.montoEstimado");
        releido.Flujo.Regla.Condicion.Operador.ShouldBe("Mayor");
    }

    [Fact]
    public async Task Un_flujo_explicito_gana_siempre_sobre_las_reglas()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var defecto = await FlujoConAsync(norte, "PROVEEDOR", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var critico = await FlujoConReglaAsync(norte, "CRITICO", RiesgoEs("Alto"), 1, "RUT", "CAMARA_COMERCIO");

        // Quien abre el expediente indica el flujo: aunque el tercero cumpla la regla de otro, gana el indicado.
        var alto = await CrearVinculoConAtributosAsync(norte, """{"nivelRiesgo": "Alto"}""");
        var indicado = await AbrirExpedienteAsync(norte, alto.Id, defecto.Id);
        indicado.Flujo!.Id.ShouldBe(defecto.Id);
        indicado.Flujo.Origen.ShouldBe("Explicito");
        indicado.Flujo.Regla.ShouldBeNull();

        // El vínculo creado con un flujo explícito lo conserva: las reglas no lo pisan.
        var conFlujo = await CrearVinculoConAtributosAsync(norte, """{"nivelRiesgo": "Alto"}""", flujoId: defecto.Id);
        conFlujo.FlujoId.ShouldBe(defecto.Id);
        var delVinculo = await AbrirExpedienteAsync(norte, conFlujo.Id);
        delVinculo.Flujo!.Id.ShouldBe(defecto.Id);
        delVinculo.Flujo.Origen.ShouldBe("Explicito");

        // Un flujo indicado de otro tipo de vínculo sigue siendo un error.
        var cliente = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(norte), new CrearFlujoDto("CLI", "Clientes", "CLIENTE"), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{cliente.Id}/publicar", Admin(norte), new { }, 200);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{(await CrearVinculoConAtributosAsync(norte, "{}")).Id}/expedientes", Admin(norte),
            new AbrirExpedienteDto(cliente.Id), 422))).ShouldBe("flujo-invalido");
        critico.Publicada.ShouldNotBeNull();
    }

    [Fact]
    public async Task La_condicion_y_la_prioridad_se_validan_y_se_congelan_con_la_version_publicada()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await FlujoConAsync(norte, "PROVEEDOR", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var critico = await FlujoConReglaAsync(norte, "CRITICO", RiesgoEs("Alto"), 1, "RUT", "CAMARA_COMERCIO");

        // Validaciones al crear: la regla se completa, la prioridad es única en el tipo y la condición usa atributos del tipo.
        async Task<string?> CrearMalAsync(SeleccionDeFlujoDto seleccion) => await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(norte),
            new CrearFlujoDto("MAL", "Mal", "PROVEEDOR", Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT")], Seleccion: seleccion), 422));
        (await CrearMalAsync(new SeleccionDeFlujoDto(RiesgoEs("Medio"), null))).ShouldBe("prioridad-requerida");
        (await CrearMalAsync(new SeleccionDeFlujoDto(null, 3))).ShouldBe("seleccion-sin-condicion");
        (await CrearMalAsync(new SeleccionDeFlujoDto(RiesgoEs("Medio"), 1000))).ShouldBe("prioridad-invalida");
        (await CrearMalAsync(new SeleccionDeFlujoDto(RiesgoEs("Medio"), 1))).ShouldBe("prioridad-duplicada");
        (await CrearMalAsync(new SeleccionDeFlujoDto(new CondicionDto("Predicado", Atributo: "vinculo.atributos.inexistente", Operador: "Igual", Valores: ["x"]), 5)))
            .ShouldBe("condicion-invalida");
        (await CrearMalAsync(new SeleccionDeFlujoDto(RiesgoEs("Crítico"), 5))).ShouldBe("condicion-invalida"); // opción que el atributo no tiene

        // Editar el borrador con la prioridad de otro flujo tampoco se deja; una libre sí.
        var otro = await FlujoConReglaAsync(norte, "OTRO", MontoMayorA("1000"), 2, "RUT");
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/flujos/{otro.Id}", Admin(norte), new EditarFlujoDto(Seleccion: new SeleccionDeFlujoDto(MontoMayorA("1000"), 1)), 422)))
            .ShouldBe("prioridad-duplicada");

        // La condición se congela al publicar: el borrador v2 con «riesgo medio» no rige hasta publicarlo.
        var borrador = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{critico.Id}", Admin(norte),
            new EditarFlujoDto(Seleccion: new SeleccionDeFlujoDto(RiesgoEs("Medio"), 1)), 200);
        borrador.Borrador!.Seleccion!.Condicion!.Valores.ShouldBe(["Medio"]);
        borrador.Publicada!.Seleccion!.Condicion!.Valores.ShouldBe(["Alto"]);
        (await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"nivelRiesgo": "Alto"}"""))))
            .Flujo!.Codigo.ShouldBe("CRITICO");
        (await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"nivelRiesgo": "Medio"}"""))))
            .Flujo!.Origen.ShouldBe("PorDefecto");

        // Un PUT sin «seleccion» conserva la de la última versión.
        var conservada = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{critico.Id}", Admin(norte), new EditarFlujoDto(Nombre: "Proveedor crítico"), 200);
        conservada.Borrador!.Seleccion!.Condicion!.Valores.ShouldBe(["Medio"]);

        var v2 = await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{critico.Id}/publicar", Admin(norte), new { }, 200);
        v2.Publicada!.Version.ShouldBe(2);
        (await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"nivelRiesgo": "Alto"}"""))))
            .Flujo!.Origen.ShouldBe("PorDefecto");
        var conV2 = (await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"nivelRiesgo": "Medio"}"""))));
        conV2.Flujo!.Codigo.ShouldBe("CRITICO");
        conV2.Flujo.Version.ShouldBe(2);

        // La versión 1 conserva su condición en el historial.
        var v1 = await ApiAsync<FlujoVersionDto>("GET", $"/api/v1/flujos/{critico.Id}/versiones/1", Admin(norte), null, 200);
        v1.Seleccion!.Condicion!.Valores.ShouldBe(["Alto"]);

        // Quitar la regla: «seleccion: {}» la borra al publicar y el flujo vuelve a ser solo explícito.
        var sinRegla = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{critico.Id}", Admin(norte), new EditarFlujoDto(Seleccion: new SeleccionDeFlujoDto()), 200);
        sinRegla.Borrador!.Seleccion.ShouldBeNull();
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{critico.Id}/publicar", Admin(norte), new { }, 200);
        (await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"nivelRiesgo": "Medio"}"""))))
            .Reglas.Select(r => r.Codigo).ShouldBe(["OTRO"]);

        // La prioridad que dejó libre se puede reutilizar.
        await FlujoConReglaAsync(norte, "NUEVO", RiesgoEs("Bajo"), 1, "RUT");
    }

    [Fact]
    public async Task La_vista_previa_dice_que_flujo_le_tocaria_a_un_tercero_y_por_que()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var defecto = await FlujoConAsync(norte, "PROVEEDOR", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        await FlujoConReglaAsync(norte, "CRITICO", RiesgoEs("Alto"), 1, "RUT", "CAMARA_COMERCIO");
        await FlujoConReglaAsync(norte, "GRANDE", MontoMayorA("1000"), 2, "RUT");
        await FlujoConReglaAsync(norte, "NATURALES", new CondicionDto("Predicado", Atributo: "tercero.tipoPersona", Operador: "Igual", Valores: ["natural"]), 3, "RUT");

        // Con los atributos del vínculo: todas las reglas en orden de evaluación, cada una con su resultado.
        var critico = await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("proveedor", Atributos: Atributos("""{"nivelRiesgo": "Alto"}""")));
        critico.Flujo!.Codigo.ShouldBe("CRITICO");
        critico.Flujo.Origen.ShouldBe("Regla");
        critico.Flujo.Regla!.ReglasEvaluadas.ShouldBe(1);
        critico.Reglas.Select(r => (r.Codigo, r.Prioridad, r.Cumple)).ShouldBe([("CRITICO", 1, true), ("GRANDE", 2, false), ("NATURALES", 3, false)]);
        critico.Reglas[0].Condicion.Valores.ShouldBe(["Alto"]);
        critico.Reglas[0].Version.ShouldBe(1);

        var grande = await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", Atributos: Atributos("""{"montoEstimado": 5000}""")));
        grande.Flujo!.Codigo.ShouldBe("GRANDE");
        grande.Flujo.Regla!.ReglasEvaluadas.ShouldBe(2);

        // Ninguna aplica: el por defecto, con todas las reglas en falso.
        var ninguna = await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR"));
        ninguna.Flujo!.Id.ShouldBe(defecto.Id);
        ninguna.Flujo.Origen.ShouldBe("PorDefecto");
        ninguna.Flujo.Regla.ShouldBeNull();
        ninguna.Reglas.ShouldAllBe(r => !r.Cumple);

        // Con el tercero (jurídico, nacional): sus datos entran a la condición, y los campos que se indican los sustituyen.
        var vinculo = await CrearVinculoConAtributosAsync(norte, "{}");
        var delTercero = await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", TerceroId: vinculo.TerceroId));
        delTercero.Flujo!.Origen.ShouldBe("PorDefecto");
        var siFueraNatural = await VistaPreviaAsync(norte, new VistaPreviaDeSeleccionDto("PROVEEDOR", TerceroId: vinculo.TerceroId, TipoPersona: "Natural"));
        siFueraNatural.Flujo!.Codigo.ShouldBe("NATURALES");
        siFueraNatural.Flujo.Regla!.ReglasEvaluadas.ShouldBe(3);

        // Errores: tipo de vínculo o persona desconocidos, y un tercero que el tenant no puede ver (anti-cosecha).
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos/seleccion/vista-previa", Admin(norte), new VistaPreviaDeSeleccionDto("NO_EXISTE"), 422)))
            .ShouldBe("tipo-vinculo-invalido");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos/seleccion/vista-previa", Admin(norte),
            new VistaPreviaDeSeleccionDto("PROVEEDOR", TipoPersona: "Robot"), 422))).ShouldBe("tipo-persona-invalido");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var deOtroTenant = await CrearVinculoConAtributosAsync(sur, "{}");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos/seleccion/vista-previa", Admin(norte),
            new VistaPreviaDeSeleccionDto("PROVEEDOR", TerceroId: deOtroTenant.TerceroId), 404))).ShouldBe("tercero-no-encontrado");
    }
}
