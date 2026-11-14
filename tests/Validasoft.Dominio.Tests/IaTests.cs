using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Ia;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Dominio.Tests;

/// <summary>Reglas puras de la IA con OpenAI y el cupo por empresa (ADR-0012, IA-1…IA-6).</summary>
public class IaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateOnly Hoy = new(2026, 11, 9);

    private static PlanDeIaEmpresa Plan(int incluidas, bool adicional = false, int? tope = null, bool activo = true, string? llave = "sk-prueba-0123456789abcd")
    {
        var plan = PlanDeIaEmpresa.Nuevo(Guid.NewGuid(), Ahora);
        plan.Actualizar(new CambiosDePlanIa(incluidas, adicional, tope, llave, null, null, null, activo), s => "protegida:" + s, Ahora);
        return plan;
    }

    private static JsonElement Json(object? valor) => JsonSerializer.SerializeToElement(valor);

    // ---------- límites (IA-5) ----------

    [Theory]
    [InlineData("RUT", 4, "high", 2000)]
    [InlineData("CEDULA", 2, "high", 2000)]
    [InlineData("CERTIFICACION_BANCARIA", 2, "low", 2000)]
    [InlineData("CAMARA_COMERCIO", 10, "high", 3000)]
    [InlineData("ESTADOS_FINANCIEROS", 6, "high", 2000)]
    [InlineData("POLIZA", 4, "low", 2000)]
    [InlineData("SG_SST", 4, "low", 2000)]
    public void Los_limites_dependen_del_tipo_canonico(string tipo, int paginas, string detalle, int tokens)
    {
        var limites = LimitesDeAnalisis.Para(tipo);

        limites.PaginasMaximas.ShouldBe(paginas);
        limites.DetalleImagen.ShouldBe(detalle);
        limites.MaxTokensSalida.ShouldBe(tokens);
        limites.TamanoMaximoBytes.ShouldBe(20L * 1024 * 1024);
    }

    [Fact]
    public void Lo_que_excede_las_paginas_del_tipo_no_se_envia_y_queda_anotado()
    {
        var rut = LimitesDeAnalisis.Para("RUT");

        rut.Recorte(6).ShouldBe((4, 2));
        rut.Recorte(3).ShouldBe((3, 0));
        rut.Recorte(null).ShouldBe(((int?)null, 0));
    }

    // ---------- costo (IA-1) ----------

    [Fact]
    public void El_costo_cobra_la_entrada_en_cache_a_su_tarifa_y_suma_la_salida()
    {
        var luna = new TarifaDeModelo(0.10m, 0.01m, 0.50m);

        // 800 000 de entrada normal, 200 000 en caché y 100 000 de salida.
        luna.Costo(1_000_000, 200_000, 100_000).ShouldBe(0.132m);
        luna.Costo(1000, 0, 300).ShouldBe(0.00025m);
        luna.Costo(100, 500, 0).ShouldBe(0.000001m); // la caché nunca supera la entrada
    }

    [Fact]
    public void Los_precios_son_configurables_por_modelo_y_un_modelo_desconocido_se_cobra_con_la_tarifa_mas_alta()
    {
        var opciones = new OpcionesDeIa(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validasoft:IA:Precios:gpt-6-luna:Entrada"] = "0.20",
            ["Validasoft:IA:UrlBase"] = "http://localhost:9999/v1/"
        }).Build());

        opciones.Tarifa("gpt-6-luna").ShouldBe(new TarifaDeModelo(0.20m, 0.01m, 0.50m));
        opciones.Tarifa("gpt-6.1-terra").ShouldBe(new TarifaDeModelo(1.00m, 0.05m, 5.00m));
        opciones.Tarifa("otro-modelo").ShouldBe(new TarifaDeModelo(1.00m, 0.05m, 5.00m));
        opciones.UrlBase.ShouldBe("http://localhost:9999/v1");
        opciones.Habilitada.ShouldBeTrue();
        opciones.Timeout.ShouldBe(TimeSpan.FromSeconds(60));
    }

    // ---------- plan (IA-2) ----------

    [Fact]
    public void El_plan_guarda_la_llave_cifrada_con_sus_ultimos_cuatro_y_nulo_la_conserva_y_vacio_la_quita()
    {
        var plan = Plan(100, llave: "sk-prueba-0123456789WXYZ");

        plan.ApiKeyOpenAiProtegida.ShouldBe("protegida:sk-prueba-0123456789WXYZ");
        plan.UltimosCuatroLlave.ShouldBe("WXYZ");
        plan.PuedePagar.ShouldBeTrue();
        plan.ModeloPorDefecto.ShouldBe("gpt-6-luna");
        plan.ModeloEscalamiento.ShouldBe("gpt-6.1-terra");
        plan.UmbralConfianza.ShouldBe(0.85m);

        plan.Actualizar(new CambiosDePlanIa(50, true, 10, null, "gpt-6-luna-mini", null, 0.9m, true), _ => throw new InvalidOperationException(), Ahora);
        plan.UltimosCuatroLlave.ShouldBe("WXYZ");
        plan.ModeloPorDefecto.ShouldBe("gpt-6-luna-mini");
        plan.UmbralConfianza.ShouldBe(0.9m);

        plan.Actualizar(new CambiosDePlanIa(50, true, 10, "", null, null, null, true), s => s, Ahora);
        plan.LlaveConfigurada.ShouldBeFalse();
        plan.UltimosCuatroLlave.ShouldBeNull();
        plan.PuedePagar.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1, null, "sk-prueba-0123456789abcd", null, null, "validaciones-incluidas-invalidas")]
    [InlineData(10, -2, "sk-prueba-0123456789abcd", null, null, "tope-adicional-invalido")]
    [InlineData(10, null, "corta", null, null, "api-key-invalida")]
    [InlineData(10, null, "sk-con espacios-0123456789", null, null, "api-key-invalida")]
    [InlineData(10, null, "pk-prueba-0123456789abcdefgh", null, null, "api-key-invalida")]
    [InlineData(10, null, "sk-con.puntos.0123456789abcd", null, null, "api-key-invalida")]
    [InlineData(10, null, null, "modelo con espacios", null, "modelo-invalido")]
    [InlineData(10, null, null, null, 1.5, "umbral-confianza-invalido")]
    public void El_plan_valida_sus_valores(int incluidas, int? tope, string? llave, string? modelo, double? umbral, string codigo)
    {
        var plan = PlanDeIaEmpresa.Nuevo(Guid.NewGuid(), Ahora);

        Should.Throw<ReglaDeNegocioException>(() => plan.Actualizar(
                new CambiosDePlanIa(incluidas, true, tope, llave, modelo, null, umbral is null ? null : (decimal)umbral.Value, true), s => s, Ahora))
            .Codigo.ShouldBe(codigo);
    }

    [Theory]
    [InlineData("sk-0123456789abcdefghijklmnopqrstuvwxyz0123456789AB")] // formato anterior
    [InlineData("sk-proj-AbC123_dEf-456GhI789jKl012mNo345")] // de proyecto (guion y guion bajo)
    [InlineData("sk-svcacct-AbC123_dEf-456GhI789jKl012mNo345")] // de cuenta de servicio
    [InlineData("  sk-admin-AbC123_dEf-456GhI789jKl012mNo345  ")] // con espacios alrededor: se recortan
    public void La_llave_acepta_las_formas_actuales_de_OpenAI_y_la_guarda_recortada(string llave)
    {
        PlanDeIaEmpresa.ValidarLlave(llave).ShouldBe(llave.Trim());

        var plan = Plan(10, llave: llave);
        plan.ApiKeyOpenAiProtegida.ShouldBe("protegida:" + llave.Trim());
        plan.UltimosCuatroLlave.ShouldBe(llave.Trim()[^4..]);
    }

    [Fact]
    public void El_mensaje_de_llave_invalida_no_repite_la_llave_escrita()
    {
        const string escrita = "no-es-una-llave-0123456789abcdef";

        var error = Should.Throw<ReglaDeNegocioException>(() => PlanDeIaEmpresa.ValidarLlave(escrita));

        error.Codigo.ShouldBe("api-key-invalida");
        error.Message.ShouldNotContain(escrita);
    }

    // ---------- cupo (IA-3, IA-4) ----------

    [Fact]
    public void Primero_se_consumen_las_incluidas_luego_las_adicionales_hasta_el_tope_y_despues_no_hay_cupo()
    {
        var plan = Plan(2, adicional: true, tope: 1);
        var uso = UsoMensualIa.Nuevo(plan.EmpresaId, "2026-11");

        uso.Consumir(plan, Hoy).Tipo.ShouldBe(TipoValidacionIa.Incluida);
        uso.Consumir(plan, Hoy).Tipo.ShouldBe(TipoValidacionIa.Incluida);
        uso.Consumir(plan, Hoy).Tipo.ShouldBe(TipoValidacionIa.Adicional);
        uso.Consumir(plan, Hoy).ShouldBe(ConsumoDeValidacion.SinCupo);

        uso.IncluidasConsumidas.ShouldBe(2);
        uso.AdicionalesConsumidas.ShouldBe(1);
        uso.PorDia["2026-11-09"].Incluidas.ShouldBe(2);
        uso.PorDia["2026-11-09"].Adicionales.ShouldBe(1);
    }

    [Fact]
    public void Sin_adicional_sin_plan_o_con_el_plan_inactivo_no_hay_cupo_y_el_adicional_sin_tope_no_se_agota()
    {
        var uso = UsoMensualIa.Nuevo(Guid.NewGuid(), "2026-11");
        var sinAdicional = Plan(1);
        uso.Consumir(sinAdicional, Hoy).Consumida.ShouldBeTrue();
        uso.Consumir(sinAdicional, Hoy).Consumida.ShouldBeFalse();
        uso.Consumir(null, Hoy).Consumida.ShouldBeFalse();
        UsoMensualIa.Nuevo(Guid.NewGuid(), "2026-11").Consumir(Plan(5, activo: false), Hoy).Consumida.ShouldBeFalse();

        var sinTope = Plan(0, adicional: true);
        var otro = UsoMensualIa.Nuevo(sinTope.EmpresaId, "2026-11");
        for (var i = 0; i < 50; i++)
            otro.Consumir(sinTope, Hoy).Tipo.ShouldBe(TipoValidacionIa.Adicional);
        otro.AvisosEnviados.ShouldBeEmpty(); // sin incluidas no hay porcentaje que avisar
    }

    [Fact]
    public void Avisa_una_vez_al_80_y_al_100_por_ciento_de_las_incluidas_y_un_cupo_pequeno_solo_avisa_el_mayor()
    {
        var plan = Plan(5, adicional: true);
        var uso = UsoMensualIa.Nuevo(plan.EmpresaId, "2026-11");

        var avisos = Enumerable.Range(0, 7).Select(_ => uso.Consumir(plan, Hoy).AvisosCruzados).ToList();

        avisos.Select(a => a.Count).ShouldBe([0, 0, 0, 1, 1, 0, 0]);
        avisos[3].ShouldBe([80]);
        avisos[4].ShouldBe([100]);
        uso.AvisosEnviados.ShouldBe([80, 100]);

        var uno = Plan(1);
        var deUno = UsoMensualIa.Nuevo(uno.EmpresaId, "2026-11");
        deUno.Consumir(uno, Hoy).AvisosCruzados.ShouldBe([100]);
        deUno.AvisosEnviados.ShouldBe([80, 100]);
    }

    [Fact]
    public void Revertir_devuelve_la_validacion_y_el_costo_suma_tokens_por_dia()
    {
        var plan = Plan(3, adicional: true, tope: 2);
        var uso = UsoMensualIa.Nuevo(plan.EmpresaId, "2026-11");
        uso.Consumir(plan, Hoy);
        uso.Revertir(TipoValidacionIa.Incluida, Hoy);
        uso.IncluidasConsumidas.ShouldBe(0);
        uso.PorDia["2026-11-09"].Incluidas.ShouldBe(0);

        uso.RegistrarCosto(1000, 200, 300, 0.00025m, Hoy, analisis: true);
        uso.RegistrarCosto(500, 0, 100, 0.001m, Hoy.AddDays(1), analisis: false);
        uso.AnalisisPagados.ShouldBe(1);
        uso.TokensEntrada.ShouldBe(1500);
        uso.TokensEntradaEnCache.ShouldBe(200);
        uso.TokensSalida.ShouldBe(400);
        uso.CostoUsd.ShouldBe(0.00125m);
        uso.PorDia["2026-11-10"].CostoUsd.ShouldBe(0.001m);
    }

    [Fact]
    public void La_validacion_consumida_tiene_id_determinista_por_empresa_documento_y_version()
    {
        var empresa = Guid.NewGuid();
        var documento = Guid.NewGuid();

        ValidacionIaConsumida.Clave(empresa, documento, 1).ShouldBe(ValidacionIaConsumida.Clave(empresa, documento, 1));
        ValidacionIaConsumida.Clave(empresa, documento, 2).ShouldNotBe(ValidacionIaConsumida.Clave(empresa, documento, 1));
        ValidacionIaConsumida.Clave(Guid.NewGuid(), documento, 1).ShouldNotBe(ValidacionIaConsumida.Clave(empresa, documento, 1));
    }

    [Fact]
    public void El_mes_y_el_dia_se_cuentan_en_la_zona_del_tenant()
    {
        var medianocheUtc = new DateTimeOffset(2026, 12, 1, 2, 0, 0, TimeSpan.Zero); // 30 de noviembre, 9 p. m. en Bogotá

        CalendarioDeIa.Dia(medianocheUtc, "America/Bogota").ShouldBe(new DateOnly(2026, 11, 30));
        UsoMensualIa.MesDe(CalendarioDeIa.Dia(medianocheUtc, "America/Bogota")).ShouldBe("2026-11");
        CalendarioDeIa.Dia(medianocheUtc, null).ShouldBe(new DateOnly(2026, 12, 1));
    }

    // ---------- esquemas (IA-1) ----------

    public static TheoryData<string> TiposConEsquema => ["RUT", "CAMARA_COMERCIO", "CEDULA", "CERTIFICACION_BANCARIA", "ESTADOS_FINANCIEROS", "POLIZA", "SG_SST"];

    [Theory]
    [MemberData(nameof(TiposConEsquema))]
    public void El_esquema_de_cada_tipo_es_estricto_todo_obligatorio_y_sin_propiedades_adicionales(string tipo)
    {
        var esquema = EsquemasDeAnalisis.Para(tipo).ShouldNotBeNull();
        var json = esquema.Json();

        esquema.NombreFormato.ShouldBe("hechos_" + tipo.ToLowerInvariant());
        ((JsonObject)json["properties"]!).Select(p => p.Key).ShouldBe(EsquemaDeAnalisis.Comunes, ignoreOrder: true);
        RevisarObjetos(json);
        json["properties"]!["observaciones"]!["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldBe(["string", "null"]);
        json["properties"]!["tipoDetectado"]!["enum"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("OTRO");
        esquema.Instrucciones().ShouldContain("No inventes");
        esquema.CamposClave.ShouldNotBeEmpty();

        // Ida y vuelta: el JSON es serializable tal cual va a la API.
        JsonNode.Parse(json.ToJsonString()).ShouldNotBeNull();
    }

    [Fact]
    public void Los_campos_de_cada_tipo_son_los_acordados_y_los_opcionales_admiten_null()
    {
        Campos("RUT").ShouldBe(["nit", "dv", "razonSocial", "tipoContribuyente", "representanteLegalNombre", "representanteLegalIdentificacion",
            "direccion", "municipio", "codigoMunicipio", "correo", "telefono", "fechaExpedicion", "actividadEconomicaPrincipal", "actividadesSecundarias",
            "responsabilidades"]);
        Campos("CAMARA_COMERCIO").ShouldBe(["razonSocial", "nit", "matricula", "estadoMatricula", "domicilio", "correo", "fechaExpedicion", "representantesLegales"]);
        Campos("CEDULA").ShouldBe(["numero", "nombres", "apellidos", "fechaNacimiento", "fechaExpedicion"]);
        Campos("CERTIFICACION_BANCARIA").ShouldBe(["banco", "tipoCuenta", "numeroCuenta", "titular", "identificacionTitular", "estadoCuenta", "fechaExpedicion"]);
        Campos("ESTADOS_FINANCIEROS").ShouldBe(["razonSocial", "nit", "periodo", "fechaCorte", "activoTotal", "pasivoTotal", "patrimonio", "ingresos",
            "ingresosBrutos", "utilidadNeta", "firmadoPorContador"]);
        var listas = EsquemasDeAnalisis.Para("RUT")!.Json()["properties"]!["campos"]!["properties"]!["responsabilidades"]!;
        (listas["type"]!.GetValue<string>(), listas["items"]!["type"]!.GetValue<string>()).ShouldBe(("array", "string"));
        Campos("POLIZA").ShouldBe(["aseguradora", "numeroPoliza", "tomador", "asegurado", "beneficiario", "vigenciaDesde", "vigenciaHasta", "valorAsegurado"]);

        var financieros = EsquemasDeAnalisis.Para("ESTADOS_FINANCIEROS")!.Json()["properties"]!["campos"]!["properties"]!;
        Tipos(financieros["activoTotal"]!).ShouldBe(["number", "null"]);
        Tipos(financieros["firmadoPorContador"]!).ShouldBe(["boolean", "null"]);
        var camara = EsquemasDeAnalisis.Para("CAMARA_COMERCIO")!.Json()["properties"]!["campos"]!["properties"]!["representantesLegales"]!;
        camara["type"]!.GetValue<string>().ShouldBe("array");
        ((JsonObject)camara["items"]!["properties"]!).Select(p => p.Key).ShouldBe(["nombre", "identificacion", "cargo"]);
        EsquemasDeAnalisis.Para("CUENTA_BANCARIA").ShouldBeNull(); // formulario: no se analiza
        EsquemasDeAnalisis.Para("NO_EXISTE").ShouldBeNull();
    }

    [Fact]
    public void La_respuesta_que_cumple_el_esquema_se_interpreta_y_la_que_no_falla_cerrada()
    {
        var rut = EsquemasDeAnalisis.Para("RUT")!;
        var valida = RespuestaRut();

        var hechos = rut.Interpretar(valida.ToJsonString());
        hechos.TipoDetectado.ShouldBe("RUT");
        hechos.Legible.ShouldBeTrue();
        hechos.Confianza.ShouldBe(0.93m);
        hechos.Alertas.ShouldBeEmpty();
        hechos.Campos["nit"].GetString().ShouldBe("900123456");
        hechos.Campos["dv"].ValueKind.ShouldBe(JsonValueKind.Null);

        Motivo(() => rut.Interpretar("{no es json")).ShouldBe("json-invalido");
        var sobrante = RespuestaRut();
        sobrante["extra"] = 1;
        Motivo(() => rut.Interpretar(sobrante.ToJsonString())).ShouldBe("fuera-de-esquema");
        var faltante = RespuestaRut();
        ((JsonObject)faltante["campos"]!).Remove("nit");
        Motivo(() => rut.Interpretar(faltante.ToJsonString())).ShouldBe("fuera-de-esquema");
        var confianza = RespuestaRut();
        confianza["confianza"] = 1.5;
        Motivo(() => rut.Interpretar(confianza.ToJsonString())).ShouldBe("fuera-de-esquema");
        var tipo = RespuestaRut();
        tipo["tipoDetectado"] = "FACTURA";
        Motivo(() => rut.Interpretar(tipo.ToJsonString())).ShouldBe("fuera-de-esquema");
        var numero = RespuestaRut();
        numero["campos"]!["nit"] = 900123456;
        Motivo(() => rut.Interpretar(numero.ToJsonString())).ShouldBe("fuera-de-esquema");
    }

    [Fact]
    public void Un_tipo_sin_esquema_propio_usa_pares_clave_valor()
    {
        var esquema = EsquemasDeAnalisis.Para("PASAPORTE")!;
        esquema.Generico.ShouldBeTrue();
        var respuesta = new JsonObject
        {
            ["tipoDetectado"] = "PASAPORTE", ["legible"] = true, ["confianza"] = 0.9, ["alertasDeManipulacion"] = new JsonArray(), ["observaciones"] = null,
            ["campos"] = new JsonObject
            {
                ["datos"] = new JsonArray(new JsonObject { ["clave"] = "numero", ["valor"] = "AB123" }, new JsonObject { ["clave"] = "pais", ["valor"] = null })
            }
        };

        var hechos = esquema.Interpretar(respuesta.ToJsonString());
        hechos.Campos.Keys.ShouldBe(["numero", "pais"]);
        hechos.Campos["numero"].GetString().ShouldBe("AB123");
    }

    // ---------- escalamiento y comparación (IA-1) ----------

    [Theory]
    [InlineData(0.95, true, 0, 0, false)]
    [InlineData(0.80, true, 0, 0, true)]
    [InlineData(0.95, false, 0, 0, true)]
    [InlineData(0.95, true, 1, 0, true)]
    [InlineData(0.95, true, 0, 1, true)]
    public void Se_escala_por_confianza_baja_ilegible_alertas_o_campo_critico_cambiado(double confianza, bool legible, int alertas, int criticos,
        bool escala) =>
        ReglasDeAnalisis.DebeEscalar((decimal)confianza, legible, alertas, 0.85m, criticos).ShouldBe(escala);

    [Fact]
    public void Queda_pendiente_de_revision_humana_con_confianza_baja_ilegible_o_alertas()
    {
        ReglasDeAnalisis.PendienteRevisionHumana(0.95m, true, 0, 0.85m).ShouldBeFalse();
        ReglasDeAnalisis.PendienteRevisionHumana(0.84m, true, 0, 0.85m).ShouldBeTrue();
        ReglasDeAnalisis.PendienteRevisionHumana(0.95m, false, 0, 0.85m).ShouldBeTrue();
        ReglasDeAnalisis.PendienteRevisionHumana(0.95m, true, 2, 0.85m).ShouldBeTrue();
    }

    [Fact]
    public void La_comparacion_entre_versiones_ignora_mayusculas_tildes_y_espacios_y_marca_los_criticos()
    {
        var esquema = EsquemasDeAnalisis.Para("RUT")!;
        var anterior = new Dictionary<string, JsonElement>
        {
            ["nit"] = Json("900123456"), ["razonSocial"] = Json("Aceros   del Norte S.A.S."), ["direccion"] = Json("Calle 1"), ["dv"] = Json(null)
        };
        var igual = new Dictionary<string, JsonElement>
        {
            ["nit"] = Json("900123456"), ["razonSocial"] = Json("ACEROS DEL NORTE S.A.S."), ["direccion"] = Json("calle 1"), ["dv"] = Json(null),
            ["fechaExpedicion"] = Json("2026-11-01")
        };
        var distinta = new Dictionary<string, JsonElement>(igual) { ["nit"] = Json("900999999"), ["direccion"] = Json("Carrera 2") };

        ReglasDeAnalisis.Comparar(null, igual, esquema.CamposClave, esquema.CamposCriticos).ShouldBeNull();
        var sinCambios = ReglasDeAnalisis.Comparar(anterior, igual, esquema.CamposClave, esquema.CamposCriticos)!;
        sinCambios.Comparacion.ShouldBe(Comparacion.SinCambios);
        sinCambios.CamposCambiados.ShouldBeEmpty();
        var cambios = ReglasDeAnalisis.Comparar(anterior, distinta, esquema.CamposClave, esquema.CamposCriticos)!;
        cambios.Comparacion.ShouldBe(Comparacion.CambiosDetectados);
        cambios.CamposCambiados.ShouldBe(["nit", "direccion"]);
        cambios.CamposCriticosCambiados.ShouldBe(["nit"]);

        ReglasDeAnalisis.Normalizar(Json(1500.0)).ShouldBe(ReglasDeAnalisis.Normalizar(Json(1500)));
        ReglasDeAnalisis.Normalizar(Json(new[] { new { nombre = "Ana Pérez", cargo = "Gerente" } }))
            .ShouldBe(ReglasDeAnalisis.Normalizar(Json(new[] { new { cargo = "gerente", nombre = "ANA PEREZ" } })));
    }

    // ---------- documento: análisis registrado y visibilidad ----------

    [Fact]
    public void El_analisis_marca_los_hechos_con_ia_una_sola_vez_y_quien_no_lo_consumio_ve_los_basicos()
    {
        var tercero = Guid.NewGuid();
        var rut = Documento.Create(Documento.CrearGlobal(tercero, CatalogoCanonico.Requerido("RUT"), Ahora));
        foreach (var evento in rut.RegistrarVersionArchivo(new ArchivoVerificado(1, "a1", "boveda", "terceros/x/a1", "application/pdf", "application/pdf",
                     1000, "rut.pdf", 1, null, tercero.ToString()), null, Antivirus.NoDisponible, false, Ahora))
            rut.Aplicar(evento);

        var analisis = new AnalisisDeIaRegistrado(1, "gpt-6-luna", false, 0.95m, true, "RUT", new Dictionary<string, JsonElement> { ["nit"] = Json("900123456") },
            [], 1, 0, 1000, 300, 0.00025m, Guid.NewGuid(), Guid.NewGuid(), Ahora, PendienteRevisionHumana: false);
        rut.Aplicar(rut.RegistrarAnalisisDeIa(analisis).ShouldNotBeNull());

        var version = rut.VersionRequerida(1);
        version.Hechos!.ConIa.ShouldBeTrue();
        version.Hechos.PendienteRevisionHumana.ShouldBeFalse();
        version.Ia!.Campos["nit"].GetString().ShouldBe("900123456");
        version.HechosPara(true)!.ConIa.ShouldBeTrue();
        var sinIa = version.HechosPara(false)!;
        sinIa.ConIa.ShouldBeFalse();
        sinIa.PendienteRevisionHumana.ShouldBeTrue();
        sinIa.Comparacion.ShouldBe(Comparacion.SinAnterior);

        rut.RegistrarAnalisisDeIa(analisis).ShouldBeNull(); // una sola vez por versión
        rut.RegistrarFalloDeIa(1, "http-500", null, Ahora).ShouldBeNull();
        rut.AnteriorConIa(2)!.Numero.ShouldBe(1);

        // Ida y vuelta por JSON (como el snapshot de Marten).
        rut.Proyectar([]).VersionRequerida(1).Ia!.Campos["nit"].GetString().ShouldBe("900123456");
    }

    [Fact]
    public void El_fallo_deja_la_version_sin_ia_y_no_se_vuelve_a_analizar()
    {
        var tercero = Guid.NewGuid();
        var rut = Documento.Create(Documento.CrearGlobal(tercero, CatalogoCanonico.Requerido("RUT"), Ahora));
        foreach (var evento in rut.RegistrarVersionArchivo(new ArchivoVerificado(1, "a1", "boveda", "terceros/x/a1", "application/pdf", "application/pdf",
                     1000, "rut.pdf", 1, null, tercero.ToString()), null, Antivirus.NoDisponible, false, Ahora))
            rut.Aplicar(evento);

        rut.Aplicar(rut.RegistrarFalloDeIa(1, "respuesta-incompleta", null, Ahora).ShouldNotBeNull());

        var version = rut.VersionRequerida(1);
        version.FalloIa.ShouldBe("respuesta-incompleta");
        version.IaResuelta.ShouldBeTrue();
        version.Hechos!.ConIa.ShouldBeFalse();
        version.HechosPara(true)!.PendienteRevisionHumana.ShouldBeTrue();
    }

    // ---------- avisos (IA-4) ----------

    [Fact]
    public void El_aviso_de_cupo_va_solo_a_los_administradores_de_la_empresa_por_campana_y_correo()
    {
        var empresa = Guid.NewGuid();
        Usuario Persona(string correo, Rol rol, Guid? de)
        {
            var u = Usuario.Crear(correo, correo, null, Ahora);
            u.AsignarRoles([new RolAsignado(rol, de)]);
            return u;
        }

        var deTenant = Persona("admin@grupo.co", Rol.Administrador, null);
        var deEmpresa = Persona("admin@norte.co", Rol.Administrador, empresa);
        var deOtra = Persona("admin@sur.co", Rol.Administrador, Guid.NewGuid());
        var revisor = Persona("revisor@norte.co", Rol.Revisor, empresa);

        var plan = PlanificadorDeNotificaciones.CupoDeIa(Guid.NewGuid(), empresa, "Norte S.A.S.", 80, "2026-11", 8, 10, false,
            [deTenant, deEmpresa, deOtra, revisor], _ => null, Ahora, 1);

        plan.Campana.Select(c => c.Notificacion.UsuarioId).ShouldBe(new[] { deTenant.Id, deEmpresa.Id }.Order(), ignoreOrder: true);
        plan.Campana.ShouldAllBe(c => c.Notificacion.Tipo == TiposNotificacion.CupoIa && c.Notificacion.Titulo == "Validaciones con IA al 80 %");
        plan.Campana[0].Notificacion.Mensaje.ShouldContain("8 de 10");
        plan.Correos.Count.ShouldBe(2);
        TiposNotificacion.ConCorreo.ShouldContain(TiposNotificacion.CupoIa);
    }

    private static JsonObject RespuestaRut() => new()
    {
        ["tipoDetectado"] = "RUT",
        ["legible"] = true,
        ["confianza"] = 0.93,
        ["alertasDeManipulacion"] = new JsonArray(),
        ["observaciones"] = null,
        ["campos"] = new JsonObject
        {
            ["nit"] = "900123456", ["dv"] = null, ["razonSocial"] = "Aceros del Norte S.A.S.", ["tipoContribuyente"] = "Persona jurídica",
            ["representanteLegalNombre"] = "Ana Gómez", ["representanteLegalIdentificacion"] = "52123456", ["direccion"] = "Calle 1 # 2-3",
            ["municipio"] = "Bogotá", ["codigoMunicipio"] = "11001", ["correo"] = null, ["telefono"] = null, ["fechaExpedicion"] = "2026-10-01",
            ["actividadEconomicaPrincipal"] = "2410", ["actividadesSecundarias"] = new JsonArray(), ["responsabilidades"] = new JsonArray("05", "48")
        }
    };

    private static IReadOnlyList<string> Campos(string tipo) => [.. EsquemasDeAnalisis.Para(tipo)!.Campos.Select(c => c.Nombre)];

    private static IReadOnlyList<string> Tipos(JsonNode propiedad) => [.. propiedad["type"]!.AsArray().Select(t => t!.GetValue<string>())];

    private static string Motivo(Action accion) => Should.Throw<AnalisisDeIaException>(accion).Motivo;

    /// <summary>Todo objeto del esquema: <c>additionalProperties: false</c> y <c>required</c> = todas sus propiedades.</summary>
    private static void RevisarObjetos(JsonNode nodo)
    {
        if (nodo is JsonObject objeto)
        {
            if (objeto["type"]?.GetValueKind() == JsonValueKind.String && objeto["type"]!.GetValue<string>() == "object")
            {
                objeto["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
                var propiedades = ((JsonObject)objeto["properties"]!).Select(p => p.Key).ToList();
                objeto["required"]!.AsArray().Select(r => r!.GetValue<string>()).ShouldBe(propiedades, ignoreOrder: true);
            }

            foreach (var (_, hijo) in objeto)
            {
                if (hijo is not null)
                    RevisarObjetos(hijo);
            }
        }
        else if (nodo is JsonArray lista)
        {
            foreach (var hijo in lista.OfType<JsonNode>())
                RevisarObjetos(hijo);
        }
    }
}
