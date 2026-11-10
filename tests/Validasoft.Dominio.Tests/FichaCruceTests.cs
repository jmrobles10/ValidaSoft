using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// F5 (docs/14 §5.2, §5.3, §6): cruce sección ↔ documento con los hechos de IA, la actualización propuesta desde el documento
/// y la opción «Usar como soporte» de los requisitos de documento.
/// </summary>
public class FichaCruceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Rut = Guid.NewGuid();

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static Dictionary<string, JsonElement> Hechos(object campos) =>
        JsonSerializer.SerializeToElement(campos).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    private static readonly JsonElement General = Json("""
        {"razon_social":"Aceros del Norte S.A.S.","tipo_persona":"Juridica","tipo_identificacion":"NIT","numero":"900123456","dv":"8",
         "correo":"compras@aceros.co","telefono":"6015550101","pais":"CO","ciudad":"11001","direccion":"Carrera 10 # 20-30",
         "ciiu_principal":"2410","ciiu_secundarias":["4663"]}
        """);

    private static Dictionary<string, JsonElement> RutDe(string direccion = "KR 10 20 30", string razon = "ACEROS DEL NORTE SAS", string nit = "900123456") =>
        Hechos(new
        {
            nit, dv = "8", razonSocial = razon, tipoContribuyente = "Persona jurídica", direccion, codigoMunicipio = "11001", correo = "COMPRAS@aceros.co",
            telefono = "+57 601 5550101", actividadEconomicaPrincipal = "2410", actividadesSecundarias = new[] { "4663" },
            responsabilidades = new[] { "5", "48", "52" }, representanteLegalNombre = "Ana Gómez", representanteLegalIdentificacion = "52.123.456"
        });

    private static CampoCruzado Campo(CruceDeSeccion cruce, string campo) => cruce.Campos.Single(c => c.Campo == campo);

    [Fact]
    public void El_RUT_coincide_con_la_general_aunque_cambien_mayusculas_tildes_puntuacion_y_abreviaturas()
    {
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.97m, true, Ahora)
            .ShouldNotBeNull();

        cruce.Campos.Where(c => c.Resultado != ResultadoDeCruce.Coincide).ShouldBeEmpty();
        Campo(cruce, "numero").Critico.ShouldBeTrue();
        Campo(cruce, "razon_social").Critico.ShouldBeTrue();
        Campo(cruce, "direccion").Critico.ShouldBeFalse();
        cruce.DiscrepanciaCritica.ShouldBeFalse();
        (cruce.Coincidencias, cruce.Discrepancias, cruce.NoVerificables).ShouldBe((cruce.Campos.Count, 0, 0));
    }

    [Fact]
    public void Una_direccion_distinta_discrepa_sin_ser_critica_y_otro_NIT_es_discrepancia_critica()
    {
        var direccion = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 2,
            RutDe("Cl 45 # 12-08"), 0.97m, true, Ahora)!;
        var campo = Campo(direccion, "direccion");
        (campo.Resultado, campo.Declarado, campo.Extraido).ShouldBe((ResultadoDeCruce.Discrepa, "Carrera 10 # 20-30", "Cl 45 # 12-08"));
        direccion.DiscrepanciaCritica.ShouldBeFalse();

        var nit = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 3, RutDe(nit: "800999111"),
            0.97m, true, Ahora)!;
        Campo(nit, "numero").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        nit.DiscrepanciaCritica.ShouldBeTrue();
    }

    [Fact]
    public void Lo_que_el_documento_no_trae_no_es_verificable_y_un_documento_ilegible_no_verifica_nada()
    {
        var sinCorreo = Hechos(new { nit = "900123456", razonSocial = "Aceros del Norte SAS" });
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 1, sinCorreo, 0.9m, true, Ahora)!;
        Campo(cruce, "correo").Resultado.ShouldBe(ResultadoDeCruce.NoVerificable);
        Campo(cruce, "numero").Resultado.ShouldBe(ResultadoDeCruce.Coincide);

        var ilegible = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.4m, false, Ahora)!;
        ilegible.Campos.ShouldAllBe(c => c.Resultado == ResultadoDeCruce.NoVerificable);
        ilegible.DiscrepanciaCritica.ShouldBeFalse();
    }

    [Fact]
    public void La_tributaria_compara_las_responsabilidades_como_conjunto()
    {
        var tributaria = Json("""{"responsabilidades":["05","48"]}""");
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.Tributaria, tributaria, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.97m, true, Ahora)!;
        var campo = cruce.Campos.ShouldHaveSingleItem();
        (campo.Resultado, campo.Extraido).ShouldBe((ResultadoDeCruce.Discrepa, "05, 48, 52"));
        campo.Detalle.ShouldBe("El documento trae 52.");

        var igual = Json("""{"responsabilidades":["52","48","05"]}""");
        CruceConDocumento.Cruzar(CodigosDeSeccion.Tributaria, igual, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.97m, true, Ahora)!
            .Campos.ShouldHaveSingleItem().Resultado.ShouldBe(ResultadoDeCruce.Coincide);
    }

    [Fact]
    public void En_la_legal_cada_representante_declarado_debe_estar_en_la_camara_y_viceversa()
    {
        var legal = Json("""
            {"personas":[
              {"rol":"RepresentanteLegal","tipo_representacion":"Principal","tipo_identificacion":"CC","numero":"52123456","nombres":"Ana","apellidos":"Gómez"},
              {"rol":"Suplente","tipo_representacion":"Suplente","tipo_identificacion":"CC","numero":"79000111","nombres":"Luis","apellidos":"Rojas"},
              {"rol":"Socio","tipo_identificacion":"CC","numero":"1","nombres":"X","apellidos":"Y","porcentaje":100}]}
            """);
        var camara = Hechos(new
        {
            estadoMatricula = "ACTIVA",
            representantesLegales = new[]
            {
                new { nombre = "ANA GOMEZ", identificacion = "52123456", cargo = "Gerente" },
                new { nombre = "PEDRO PAEZ", identificacion = "80555444", cargo = "Suplente del gerente" }
            }
        });

        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.Legal, legal, TipoPersona.Juridica, null, "CAMARA_COMERCIO", Rut, 1, camara, 0.95m, true, Ahora)!;
        Campo(cruce, "personas[0]").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        Campo(cruce, "personas[1]").Detalle.ShouldBe("No figura en el documento.");
        var sobra = Campo(cruce, "personas");
        (sobra.Resultado, sobra.Extraido).ShouldBe((ResultadoDeCruce.Discrepa, "PEDRO PAEZ (80555444) — Suplente del gerente"));
        Campo(cruce, "estado_matricula").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        cruce.DiscrepanciaCritica.ShouldBeTrue();

        // El RUT respalda al representante principal.
        var delRut = CruceConDocumento.Cruzar(CodigosDeSeccion.Legal, legal, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.95m, true, Ahora)!;
        delRut.Campos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(c => c.Campo.ShouldBe("personas[0]"), c => c.Resultado.ShouldBe(ResultadoDeCruce.Coincide));

        // Una matrícula cancelada es una discrepancia crítica; la persona natural no tiene legal.
        var cancelada = new Dictionary<string, JsonElement>(camara) { ["estadoMatricula"] = JsonSerializer.SerializeToElement("CANCELADA") };
        Campo(CruceConDocumento.Cruzar(CodigosDeSeccion.Legal, legal, TipoPersona.Juridica, null, "CAMARA_COMERCIO", Rut, 1, cancelada, 0.95m, true, Ahora)!,
            "estado_matricula").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        CruceConDocumento.Cruzar(CodigosDeSeccion.Legal, legal, TipoPersona.Natural, null, "RUT", Rut, 1, RutDe(), 0.95m, true, Ahora).ShouldBeNull();
    }

    [Fact]
    public void La_financiera_tolera_medio_por_ciento_y_unos_estados_de_otro_periodo_no_respaldan_las_cifras()
    {
        var financiera = Json("""{"periodo":2025,"activos":1000000,"pasivos":400000,"patrimonio":600000,"ingresos_brutos":2000000}""");
        var estados = Hechos(new { periodo = "2025", activoTotal = 1004000, pasivoTotal = 410000, patrimonio = 600000, ingresosBrutos = 2000000 });
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.Financiera, financiera, TipoPersona.Juridica, 2025, "ESTADOS_FINANCIEROS", Rut, 1, estados,
            0.9m, true, Ahora)!;
        Campo(cruce, "activos").Resultado.ShouldBe(ResultadoDeCruce.Coincide); // 0,4 %
        var pasivos = Campo(cruce, "pasivos");
        (pasivos.Resultado, pasivos.Detalle).ShouldBe((ResultadoDeCruce.Discrepa, "Diferencia de 2,44 %."));

        var otroAnio = Hechos(new { periodo = "2024", activoTotal = 1000000 });
        CruceConDocumento.Cruzar(CodigosDeSeccion.Financiera, financiera, TipoPersona.Juridica, 2025, "ESTADOS_FINANCIEROS", Rut, 1, otroAnio, 0.9m, true,
            Ahora)!.Campos.ShouldHaveSingleItem().Campo.ShouldBe("periodo");
    }

    [Fact]
    public void El_cruce_se_guarda_una_sola_vez_por_version_de_documento()
    {
        var seccion = new SeccionDeFicha { Id = Guid.NewGuid(), Seccion = CodigosDeSeccion.General };
        seccion.Apply(new VersionDeSeccionRegistrada(1, General, "h", "h", [], new CompletitudDeSeccion(true, [], 1), 1, null, OrigenDeSeccion.Tercero,
            "t", Ahora));
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", Rut, 1, RutDe(), 0.97m, true, Ahora)!;

        var evento = seccion.RegistrarCruce(1, cruce).ShouldNotBeNull();
        seccion.Apply(evento);
        seccion.Vigente!.Cruces.ShouldHaveSingleItem();
        seccion.RegistrarCruce(1, cruce).ShouldBeNull();
        seccion.RegistrarCruce(2, cruce).ShouldBeNull(); // la versión no existe
        seccion.RegistrarCruce(1, cruce with { VersionDocumento = 2 }).ShouldNotBeNull();
    }

    [Fact]
    public void El_RUT_nuevo_propone_solo_lo_que_cambio_sobre_los_datos_actuales_y_nunca_la_identificacion()
    {
        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT",
            RutDe("Cl 45 # 12-08", nit: "800999111"));

        propuesta.Campos.ShouldHaveSingleItem().ShouldBe(new CampoPropuesto("direccion", "Dirección", "Carrera 10 # 20-30", "Cl 45 # 12-08"));
        propuesta.Datos.GetProperty("direccion").GetString().ShouldBe("Cl 45 # 12-08");
        propuesta.Datos.GetProperty("numero").GetString().ShouldBe("900123456");
        propuesta.Datos.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");

        // Excluido (no pasa la regla de la sección): queda como estaba.
        PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, General, TipoPersona.Juridica, null, "RUT", RutDe("Cl 45 # 12-08"),
            new HashSet<string> { "direccion" }).HayCambios.ShouldBeFalse();
    }

    [Fact]
    public void Sin_seccion_el_documento_propone_todo_lo_que_trae_y_en_la_tributaria_las_responsabilidades()
    {
        var general = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, null, TipoPersona.Juridica, null, "RUT", RutDe());
        general.Campos.Select(c => c.Campo).ShouldBe(["razon_social", "direccion", "ciudad", "correo", "telefono", "ciiu_principal", "ciiu_secundarias"]);
        general.Datos.GetProperty("correo").GetString().ShouldBe("compras@aceros.co");
        general.Datos.GetProperty("telefono").GetString().ShouldBe("576015550101");

        var tributaria = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Tributaria, Json("""{"responsabilidades":["05"],"responsable_ica":false}"""),
            TipoPersona.Juridica, null, "RUT", RutDe());
        tributaria.Campos.ShouldHaveSingleItem().ShouldBe(new CampoPropuesto("responsabilidades", "Responsabilidades (casilla 53 del RUT)", "05", "05, 48, 52"));
        tributaria.Datos.GetProperty("responsable_ica").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public void La_camara_propone_agregar_los_representantes_que_faltan_y_los_estados_las_cifras_de_su_periodo()
    {
        var camara = Hechos(new { representantesLegales = new[] { new { nombre = "PEDRO PAEZ", identificacion = "80555444", cargo = "Suplente" } } });
        var legal = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Legal, Json("""{"personas":[]}"""), TipoPersona.Juridica, null, "CAMARA_COMERCIO",
            camara);
        legal.Campos.ShouldHaveSingleItem().Campo.ShouldBe("personas[0]");
        var fila = legal.Datos.GetProperty("personas")[0];
        (fila.GetProperty("rol").GetString(), fila.GetProperty("numero").GetString(), fila.GetProperty("nombres").GetString())
            .ShouldBe(("Suplente", "80555444", "PEDRO PAEZ"));

        var estados = Hechos(new { periodo = "2025", activoTotal = 1000000.456m, fechaCorte = "2025-12-31" });
        var financiera = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Financiera, null, TipoPersona.Juridica, 2025, "ESTADOS_FINANCIEROS", estados);
        financiera.Datos.GetProperty("activos").GetDecimal().ShouldBe(1000000.46m);
        financiera.Campos.Select(c => c.Campo).ShouldBe(["activos", "fecha_corte"]);
        PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Financiera, null, TipoPersona.Juridica, 2024, "ESTADOS_FINANCIEROS", estados).HayCambios
            .ShouldBeFalse();
    }

    [Fact]
    public void Usar_como_soporte_es_activo_por_omision_solo_en_documentos_que_respaldan_secciones()
    {
        var rut = new RequisitoDelFlujo(Guid.NewGuid(), Guid.NewGuid(), "RUT", true, [], null);
        rut.UsaComoSoporte("RUT").ShouldBeTrue();
        (rut with { UsarComoSoporte = false }).UsaComoSoporte("RUT").ShouldBeFalse();
        rut.UsaComoSoporte("POLIZA").ShouldBeFalse();
        new RequisitoDelFlujo(Guid.NewGuid(), Guid.Empty, "FICHA_GENERAL", true, [], null, Seccion: "FICHA_GENERAL").UsaComoSoporte("RUT").ShouldBeFalse();
        MapaDeRespaldo.Secciones("RUT").ShouldBe([CodigosDeSeccion.General, CodigosDeSeccion.Tributaria, CodigosDeSeccion.Legal]);
        MapaDeRespaldo.TiposPara(CodigosDeSeccion.Financiera).ShouldBe(["ESTADOS_FINANCIEROS"]);
    }
}
