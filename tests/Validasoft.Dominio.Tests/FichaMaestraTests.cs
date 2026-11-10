using System.Text.Json;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Ficha maestra (docs/14, FM-16…): la regla única de cada sección (formato y completitud), vacío ≠ 0, el soporte del
/// cambio, la huella, el DV DIAN, los catálogos y la siguiente sección del wizard.
/// </summary>
public class FichaMaestraTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 9);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);

    private static readonly ContextoDeSeccion Juridica = new(TipoPersona.Juridica, Identificacion.Crear("CO", "NIT", "900373913"), null, Hoy);
    private static readonly ContextoDeSeccion Natural = new(TipoPersona.Natural, Identificacion.Crear("CO", "CC", "79123456"), null, Hoy);

    private static readonly ICatalogosDeFicha Catalogos = CatalogosDeSemilla();

    private const string GeneralCompletaJuridica = """
        { "razon_social": "Aceros del Norte S.A.S.", "correo": "Compras@Aceros.co", "telefono": "+57 601 555 1234", "pais": "co",
          "ciudad": "11001", "direccion": "  Calle 45   # 12-08 ", "ciiu_principal": "4663" }
        """;

    private static ICatalogosDeFicha CatalogosDeSemilla()
    {
        var entradas = new List<EntradaDeCatalogo>();
        foreach (var (catalogo, filas) in SemillaDeCatalogos.Todas)
        {
            var niveles = new Dictionary<string, int>();
            foreach (var fila in filas.OrderBy(f => f.Nivel ?? 0))
            {
                var (entrada, rechazo) = ReglasDeCatalogo.Validar(catalogo, fila, p => niveles.TryGetValue(p, out var n) ? n : null, Ahora);
                rechazo.ShouldBeNull();
                if (entrada!.Nivel is { } nivel)
                    niveles[entrada.Codigo] = nivel;
                entradas.Add(entrada);
            }
        }

        return new CatalogosEnMemoria(entradas);
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static object Leer(string seccion, string json, ContextoDeSeccion contexto) =>
        ReglasDeFicha.Leer(CatalogoDeSecciones.Requerida(seccion), Json(json), contexto);

    private static ResultadoDeRegla Evaluar(string seccion, string json, ContextoDeSeccion contexto, ICatalogosDeFicha? catalogos = null)
    {
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        return ReglasDeFicha.Evaluar(definicion, Leer(seccion, json, contexto), contexto, catalogos ?? Catalogos, false, 0);
    }

    private static IEnumerable<string?> Faltantes(ResultadoDeRegla r) => r.Completitud.Faltantes.Select(f => f.Campo ?? f.Regla);

    // ---------- DV DIAN (módulo 11, pesos 3, 7, 13, … 71) ----------

    [Theory]
    [InlineData("900373913", "4")]
    [InlineData("800197268", "4")] // DIAN
    [InlineData("860002964", "4")]
    [InlineData("890903938", "8")] // Bancolombia
    [InlineData("860034313", "7")] // Davivienda
    public void El_dv_se_calcula_con_el_modulo_11_de_la_DIAN(string nit, string dv) =>
        DigitoVerificacion.NitColombia(nit).ShouldBe(dv);

    [Fact]
    public void El_dv_de_la_ficha_general_lo_calcula_validasoft_y_no_se_edita()
    {
        var datos = (DatosGenerales)Leer(CodigosDeSeccion.General, """{ "numero": "900.373.913-4", "dv": "4" }""", Juridica);
        datos.Numero.ShouldBe("900373913");
        datos.Dv.ShouldBe("4");
        datos.TipoIdentificacion.ShouldBe("NIT");
        datos.TipoPersona.ShouldBe(TipoPersona.Juridica);

        Codigo(() => Leer(CodigosDeSeccion.General, """{ "dv": "7" }""", Juridica)).ShouldBe(("dv-no-editable", "dv"));
        Codigo(() => Leer(CodigosDeSeccion.General, """{ "numero": "900373913-1" }""", Juridica)).ShouldBe(("dv-no-editable", "numero"));
        Codigo(() => Leer(CodigosDeSeccion.General, """{ "numero": "800197268" }""", Juridica)).ShouldBe(("identificacion-no-editable", "numero"));
        Codigo(() => Leer(CodigosDeSeccion.General, """{ "tipo_persona": "Natural" }""", Juridica)).ShouldBe(("identificacion-no-editable", "tipo_persona"));
        ((DatosGenerales)Leer(CodigosDeSeccion.General, "{}", Natural)).Dv.ShouldBeNull(); // la cédula no lleva DV
    }

    // ---------- formato JSON ----------

    [Fact]
    public void Una_clave_desconocida_o_un_tipo_invalido_responden_422_con_el_campo()
    {
        Codigo(() => Leer(CodigosDeSeccion.General, """{ "razon_social": "X", "color": "azul" }""", Juridica)).ShouldBe(("campo-desconocido", "color"));
        Codigo(() => Leer(CodigosDeSeccion.Financiera, """{ "activos": "1000" }""", Juridica with { Periodo = 2025 })).ShouldBe(("valor-invalido", "activos"));
        Codigo(() => Leer(CodigosDeSeccion.Financiera, """{ "fecha_corte": "31/12/2025" }""", Juridica with { Periodo = 2025 }))
            .ShouldBe(("valor-invalido", "fecha_corte"));
        Codigo(() => Leer(CodigosDeSeccion.Legal, """{ "personas": [ { "rol": "Gerente" } ] }""", Juridica)).Item2.ShouldBe("personas[0].rol");
        Codigo(() => Leer(CodigosDeSeccion.Sst, """{ "politica_hseq": "si" }""", Juridica)).ShouldBe(("valor-invalido", "politica_hseq"));
        Codigo(() => Leer(CodigosDeSeccion.General, "[1, 2]", Juridica)).Item1.ShouldBe("datos-invalidos");
    }

    // ---------- FICHA_GENERAL ----------

    [Fact]
    public void General_juridica_completa_y_normalizada()
    {
        var r = Evaluar(CodigosDeSeccion.General, GeneralCompletaJuridica, Juridica);
        r.Errores.ShouldBeEmpty();
        r.Completitud.Completa.ShouldBeTrue();
        r.Completitud.ReglaVersion.ShouldBe(1);

        var datos = (DatosGenerales)Leer(CodigosDeSeccion.General, GeneralCompletaJuridica, Juridica);
        datos.Correo.ShouldBe("compras@aceros.co");
        datos.Pais.ShouldBe("CO");
        datos.Direccion.ShouldBe("Calle 45 # 12-08");
    }

    public static TheoryData<string, string, string[]> CasosGeneral => new()
    {
        // Vacío: todo lo obligatorio de una jurídica.
        { "Juridica", "{}", ["razon_social", "correo", "telefono", "pais", "direccion", "ciiu_principal"] },
        // La natural pide nombres y apellidos, no razón social.
        { "Natural", "{}", ["nombres", "apellidos", "correo", "telefono", "pais", "direccion", "ciiu_principal"] },
        // En Colombia la ciudad es obligatoria.
        { "Juridica", """{ "razon_social": "A", "correo": "a@b.co", "telefono": "6015551234", "pais": "CO", "direccion": "Cl 1", "ciiu_principal": "4111" }""", ["ciudad"] },
        // En el exterior no.
        { "Juridica", """{ "razon_social": "A", "correo": "a@b.co", "telefono": "6015551234", "pais": "US", "direccion": "Main St", "ciiu_principal": "4111" }""", [] },
        // Texto vacío = sin diligenciar.
        { "Natural", """{ "nombres": "  ", "apellidos": "Pérez", "correo": "a@b.co", "telefono": "3001234567", "pais": "US", "direccion": "x", "ciiu_principal": "4111" }""", ["nombres"] }
    };

    [Theory]
    [MemberData(nameof(CasosGeneral))]
    public void General_faltantes_por_tipo_de_persona(string tipo, string json, string[] faltantes)
    {
        var r = Evaluar(CodigosDeSeccion.General, json, tipo == "Natural" ? Natural : Juridica);
        r.Errores.ShouldBeEmpty();
        Faltantes(r).ShouldBe(faltantes);
        r.Completitud.Completa.ShouldBe(faltantes.Length == 0);
    }

    [Theory]
    [InlineData("""{ "correo": "no-es-correo" }""", "correo", "valor-invalido")]
    [InlineData("""{ "telefono": "12" }""", "telefono", "valor-invalido")]
    [InlineData("""{ "pais": "COL" }""", "pais", "valor-invalido")]
    [InlineData("""{ "pais": "CO", "ciudad": "99999" }""", "ciudad", "codigo-invalido")]
    [InlineData("""{ "pais": "CO", "ciudad": "1100" }""", "ciudad", "codigo-invalido")]
    [InlineData("""{ "ciiu_principal": "1234" }""", "ciiu_principal", "codigo-invalido")]
    [InlineData("""{ "ciiu_principal": "4111", "ciiu_secundarias": ["4111"] }""", "ciiu_secundarias[0]", "valor-invalido")]
    [InlineData("""{ "anio_inicio": 2030 }""", "anio_inicio", "valor-invalido")]
    [InlineData("""{ "pagina_web": "no es web" }""", "pagina_web", "valor-invalido")]
    [InlineData("""{ "direccion_estructurada": { "tipo_via": "Callejón", "numero_via": "45" } }""", "direccion_estructurada.tipo_via", "valor-invalido")]
    public void General_formato_invalido_impide_guardar(string json, string campo, string codigo)
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var r = ReglasDeFicha.Evaluar(definicion, Leer(CodigosDeSeccion.General, json, Juridica), Juridica, Catalogos, false, 0);
        r.Errores.ShouldContain(e => e.Campo == campo && e.Codigo == codigo);
        var error = Should.Throw<ReglaDeNegocioException>(() => ReglasDeFicha.AsegurarFormato(r));
        error.Tipo.ShouldBe(TipoFalla.Validacion);
        error.Datos!["campo"].ShouldBe(r.Errores[0].Campo);
    }

    // ---------- FICHA_TRIBUTARIA ----------

    public static TheoryData<string, string[]> CasosTributaria => new()
    {
        { "{}", ["responsabilidades", "responsable_ica"] },
        { """{ "responsabilidades": ["5", "7", "48"], "responsable_ica": false }""", [] },
        // Gran contribuyente (13) y autorretenedor (15) exigen número y fecha de la resolución.
        { """{ "responsabilidades": ["13", "15"], "responsable_ica": false }""",
            ["resolucion_gran_contribuyente.numero", "resolucion_gran_contribuyente.fecha", "resolucion_autorretenedor.numero", "resolucion_autorretenedor.fecha"] },
        { """{ "responsabilidades": ["13"], "resolucion_gran_contribuyente": { "numero": "012635", "fecha": "2024-12-18" }, "responsable_ica": false }""", [] },
        // Contribuyente de ICA sin municipios.
        { """{ "responsabilidades": ["48"], "responsable_ica": true }""", ["ica-requerido"] },
        { """{ "responsabilidades": ["48"], "responsable_ica": true, "ica": [ { "municipio": "11001", "ciiu": "4663", "tarifa_por_mil": 11.04 } ] }""", [] },
        { """{ "responsabilidades": ["48", "49"], "responsable_ica": false }""", ["responsabilidades-incompatibles"] },
        // No responsable de ICA (false) cuenta como respuesta: no es lo mismo que vacío.
        { """{ "responsabilidades": ["49"], "responsable_ica": false, "ica": [ { "municipio": "11001", "ciiu": "4663" } ] }""", ["ica-sin-responsabilidad"] }
    };

    [Theory]
    [MemberData(nameof(CasosTributaria))]
    public void Tributaria_faltantes_y_coherencias(string json, string[] faltantes)
    {
        var r = Evaluar(CodigosDeSeccion.Tributaria, json, Juridica);
        r.Errores.ShouldBeEmpty();
        Faltantes(r).ShouldBe(faltantes);
    }

    [Fact]
    public void Tributaria_normaliza_y_valida_contra_el_catalogo_dian()
    {
        var datos = (DatosTributarios)Leer(CodigosDeSeccion.Tributaria, """{ "responsabilidades": ["48", "5", "05", " 7 "] }""", Juridica);
        datos.Responsabilidades.ShouldBe(["05", "07", "48"]);

        var r = Evaluar(CodigosDeSeccion.Tributaria, """{ "responsabilidades": ["99"], "resolucion_autorretenedor": { "fecha": "2027-01-01" } }""", Juridica);
        r.Errores.Select(e => (e.Campo, e.Codigo)).ShouldBe(
            [("responsabilidades[0]", "codigo-invalido"), ("resolucion_autorretenedor.fecha", "valor-invalido")]);
    }

    // ---------- FICHA_FINANCIERA ----------

    private static readonly ContextoDeSeccion Periodo2025 = Juridica with { Periodo = 2025 };

    private static string Financiera(decimal activos, decimal pasivos, decimal patrimonio, string extra = "") => $$"""
        { "fecha_corte": "2025-12-31", "activos": {{activos}}, "pasivos": {{pasivos}}, "patrimonio": {{patrimonio}}, "ingresos_brutos": 500,
          "ingresos_netos": 450, "gastos_operacionales": 100, "gastos_no_operacionales": 0, "otros_ingresos": 0,
          "moneda_extranjera": { "realiza": false } {{extra}} }
        """;

    [Theory]
    [InlineData(1000, 600, 400, true)]
    [InlineData(1000, 600, 396, true)]   // 0,4 % de diferencia: dentro de la tolerancia
    [InlineData(1000, 600, 394, false)]  // 0,6 %: fuera
    [InlineData(0, 0, 0, true)]          // ceros declarados son valores, no vacío
    [InlineData(1000, 1200, -200, true)] // patrimonio negativo
    public void Financiera_ecuacion_contable_con_tolerancia_de_medio_por_ciento(decimal activos, decimal pasivos, decimal patrimonio, bool completa)
    {
        var r = Evaluar(CodigosDeSeccion.Financiera, Financiera(activos, pasivos, patrimonio), Periodo2025);
        r.Errores.ShouldBeEmpty();
        r.Completitud.Completa.ShouldBe(completa);
        if (!completa)
            Faltantes(r).ShouldBe(["ecuacion-contable"]);
    }

    [Fact]
    public void Financiera_vacio_no_es_cero_y_valida_formato()
    {
        var vacia = Evaluar(CodigosDeSeccion.Financiera, "{}", Periodo2025);
        Faltantes(vacia).ShouldBe(["fecha_corte", "activos", "pasivos", "patrimonio", "ingresos_brutos", "ingresos_netos", "gastos_operacionales",
            "gastos_no_operacionales", "otros_ingresos", "moneda_extranjera.realiza"]);
        ((DatosFinancieros)Leer(CodigosDeSeccion.Financiera, "{}", Periodo2025)).Activos.ShouldBeNull();

        var invalida = Evaluar(CodigosDeSeccion.Financiera, """{ "fecha_corte": "2024-12-31", "activos": -1, "pasivos": 10.123 }""", Periodo2025);
        invalida.Errores.Select(e => e.Campo).ShouldBe(["fecha_corte", "activos", "pasivos"]);

        Codigo(() => Leer(CodigosDeSeccion.Financiera, """{ "periodo": 2024 }""", Periodo2025)).ShouldBe(("periodo-no-coincide", "periodo"));

        var moneda = Evaluar(CodigosDeSeccion.Financiera, Financiera(10, 5, 5).Replace("\"realiza\": false", "\"realiza\": true"), Periodo2025);
        Faltantes(moneda).ShouldBe(["moneda_extranjera.tipos", "moneda_extranjera.paises"]);
    }

    [Fact]
    public void Financiera_ingresos_netos_no_superan_los_brutos() =>
        Faltantes(Evaluar(CodigosDeSeccion.Financiera, Financiera(10, 5, 5).Replace("\"ingresos_netos\": 450", "\"ingresos_netos\": 600"), Periodo2025))
            .ShouldBe(["ingresos-netos-mayores"]);

    // ---------- FICHA_LEGAL ----------

    private const string Representante = """{ "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "79.123.456", "nombres": "Ana", "apellidos": "Gómez", "pep": false, "vinculo_pep": false }""";

    private static string Socio(string numero, decimal porcentaje) =>
        $$"""{ "rol": "Socio", "tipo_identificacion": "NIT", "numero": "{{numero}}", "nombres": "Inversiones S.A.S.", "porcentaje": {{porcentaje}}, "pep": false, "vinculo_pep": false }""";

    [Fact]
    public void Legal_filas_minimas_por_tipo_de_persona()
    {
        Faltantes(Evaluar(CodigosDeSeccion.Legal, "{}", Juridica)).ShouldBe(["representante-legal-requerido", "socio-requerido"]);
        Faltantes(Evaluar(CodigosDeSeccion.Legal, "{}", Natural)).ShouldBe(["representante-legal-requerido"]);
        Faltantes(Evaluar(CodigosDeSeccion.Legal, $$"""{ "personas": [{{Representante}}] }""", Juridica)).ShouldBe(["socio-requerido"]);
        Evaluar(CodigosDeSeccion.Legal, $$"""{ "personas": [{{Representante}}, {{Socio("900373913", 60)}}] }""", Juridica).Completitud.Completa.ShouldBeTrue();
    }

    [Fact]
    public void Legal_participacion_obligatoria_y_suma_hasta_cien()
    {
        var mayor = Evaluar(CodigosDeSeccion.Legal, $$"""{ "personas": [{{Representante}}, {{Socio("900373913", 60)}}, {{Socio("800197268", 45)}}] }""", Juridica);
        Faltantes(mayor).ShouldBe(["participacion-mayor-a-100"]);

        var sinPorcentaje = Evaluar(CodigosDeSeccion.Legal,
            $$"""{ "personas": [{{Representante}}, { "rol": "Socio", "tipo_identificacion": "CC", "numero": "1020304050", "nombres": "Luis", "apellidos": "Ruiz" }] }""",
            Juridica);
        Faltantes(sinPorcentaje).ShouldBe(["personas[1].pep", "personas[1].vinculo_pep", "personas[1].porcentaje"]);
    }

    [Fact]
    public void Legal_identificaciones_dv_fechas_y_duplicados()
    {
        var datos = (DatosLegales)Leer(CodigosDeSeccion.Legal, $$"""{ "personas": [{{Socio("900.373.913-4", 100)}}] }""", Juridica);
        datos.Personas![0].Numero.ShouldBe("900373913");
        datos.Personas[0].Dv.ShouldBe("4");

        var r = Evaluar(CodigosDeSeccion.Legal, $$"""
            { "personas": [ {{Representante}}, {{Representante}},
              { "rol": "Contador", "tipo_identificacion": "XX", "numero": "1", "nombres": "A", "apellidos": "B", "pep": false, "vinculo_pep": false },
              { "rol": "Socio", "tipo_identificacion": "NIT", "numero": "900373913", "dv": "9", "nombres": "X", "porcentaje": 101, "pep": true, "vinculo_pep": false },
              { "rol": "Apoderado", "tipo_identificacion": "CC", "numero": "1020", "nombres": "C", "apellidos": "D", "pep": false, "vinculo_pep": false,
                "fecha_expedicion": "2010-01-01", "fecha_nacimiento": "2011-01-01", "nacionalidad": "Colombia" } ] }
            """, Juridica);
        r.Errores.Select(e => (e.Campo, e.Codigo)).ShouldBe(
        [
            ("personas[1]", "persona-duplicada"),
            ("personas[2].tipo_identificacion", "valor-invalido"),
            ("personas[3].dv", "dv-no-editable"),
            ("personas[3].porcentaje", "valor-invalido"),
            ("personas[4].fecha_nacimiento", "valor-invalido"),
            ("personas[4].nacionalidad", "valor-invalido")
        ]);
    }

    // ---------- FICHA_SST y FICHA_ESPECIALIDADES ----------

    [Fact]
    public void Sst_un_no_es_respuesta_y_vacio_no()
    {
        const string TodasNo = """
            { "programa_salud_ocupacional": false, "identifica_factores_de_riesgo": false, "comite_paritario": false, "programa_higiene_y_seguridad": false,
              "gestion_ambiental": false, "reglamento_interno": false, "politica_hseq": false, "licencias_y_permisos": false }
            """;
        Evaluar(CodigosDeSeccion.Sst, TodasNo, Juridica).Completitud.Completa.ShouldBeTrue();
        Faltantes(Evaluar(CodigosDeSeccion.Sst, TodasNo.Replace("\"politica_hseq\": false", "\"politica_hseq\": null"), Juridica)).ShouldBe(["politica_hseq"]);
        Evaluar(CodigosDeSeccion.Sst, "{}", Juridica).Completitud.Faltantes.Count.ShouldBe(8);
    }

    [Fact]
    public void Especialidades_al_menos_una_de_nivel_tres()
    {
        Faltantes(Evaluar(CodigosDeSeccion.Especialidades, """{ "especialidades": [] }""", Juridica)).ShouldBe(["especialidad-requerida"]);
        Evaluar(CodigosDeSeccion.Especialidades, """{ "especialidades": ["og.con.pre", "OG.CON.PRE"] }""", Juridica).Completitud.Completa.ShouldBeTrue();
        ((DatosEspecialidades)Leer(CodigosDeSeccion.Especialidades, """{ "especialidades": ["og.con.pre", "OG.CON.PRE", "AC.PIN.INT"] }""", Juridica))
            .Especialidades.ShouldBe(["AC.PIN.INT", "OG.CON.PRE"]);
        var r = Evaluar(CodigosDeSeccion.Especialidades, """{ "especialidades": ["OG.CON", "ZZ.ZZ.ZZ"] }""", Juridica);
        r.Errores.Select(e => e.Campo).ShouldBe(["especialidades[0]", "especialidades[1]"]);
    }

    // ---------- huella, versiones y soporte del cambio ----------

    [Fact]
    public void La_huella_no_depende_del_orden_de_las_claves_ni_de_los_ceros_decimales()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera);
        string Huella(string json) => JsonDeFicha.Huella(definicion.Esquema.Canonico(Leer(CodigosDeSeccion.Financiera, json, Periodo2025)));

        Huella("""{ "activos": 100.50, "pasivos": 1 }""").ShouldBe(Huella("""{ "pasivos": 1.00, "activos": 100.5 }"""));
        Huella("""{ "activos": 0 }""").ShouldNotBe(Huella("{}"));
        definicion.Esquema.Canonico(Leer(CodigosDeSeccion.Financiera, """{ "activos": 100.50 }""", Periodo2025)).GetRawText()
            .ShouldBe("""{"periodo":2025,"activos":100.5}""");
    }

    [Fact]
    public void Versiones_de_una_seccion_con_soporte_del_cambio()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var terceroId = Guid.NewGuid();
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(terceroId, definicion, null, Ahora));
        seccion.Id.ShouldBe(SeccionDeFicha.IdPara(terceroId, CodigosDeSeccion.General, null));

        IReadOnlyList<object> Registrar(string json, params SoporteDeSeccion[] soportes) =>
            seccion.Registrar(definicion, Leer(CodigosDeSeccion.General, json, Juridica), soportes, Juridica, Catalogos, OrigenDeSeccion.Tercero,
                "tercero", Ahora);

        // La primera versión no necesita soporte.
        var primera = Registrar(GeneralCompletaJuridica).ShouldHaveSingleItem().ShouldBeOfType<VersionDeSeccionRegistrada>();
        primera.Completitud.Completa.ShouldBeTrue();
        primera.HuellaDatosAnterior.ShouldBeNull();
        seccion = seccion.Proyectar([primera]);

        // Mismos datos (aunque con otro formato) y sin soportes ⇒ sin versión nueva.
        Registrar(GeneralCompletaJuridica.Replace("Compras@Aceros.co", "compras@aceros.co")).ShouldBeEmpty();

        // Cambio sin soporte ⇒ versión 2 incompleta por el soporte.
        var cambio = GeneralCompletaJuridica.Replace("Calle 45", "Carrera 7");
        var sinSoporte = Registrar(cambio).ShouldHaveSingleItem().ShouldBeOfType<VersionDeSeccionRegistrada>();
        sinSoporte.Numero.ShouldBe(2);
        sinSoporte.Completitud.Completa.ShouldBeFalse();
        sinSoporte.Completitud.Faltantes.ShouldBe([new FaltanteDeSeccion(null, ReglasDeFicha.ReglaSoporte, ReglasDeFicha.MensajeSoporte)]);
        seccion = seccion.Proyectar([sinSoporte]);

        // Los mismos datos con el RUT como soporte ⇒ versión 3 (cambian los soportes); comparada con la 2, los datos no cambian.
        var rut = new SoporteDeSeccion(Guid.NewGuid(), 1, "RUT");
        var conSoporte = Registrar(cambio, rut).ShouldHaveSingleItem().ShouldBeOfType<VersionDeSeccionRegistrada>();
        conSoporte.Numero.ShouldBe(3);
        conSoporte.Completitud.Completa.ShouldBeTrue();
        conSoporte.HuellaDatos.ShouldBe(sinSoporte.HuellaDatos);
        seccion = seccion.Proyectar([conSoporte]);
        seccion.VersionVigente.ShouldBe(3);
        seccion.Vigente!.Soportes.ShouldBe([rut]);

        // Un dato con formato inválido no crea versión.
        Should.Throw<ReglaDeNegocioException>(() => Registrar(cambio.Replace("compras@aceros.co", "x").Replace("Compras@Aceros.co", "x")))
            .Codigo.ShouldBe("valor-invalido");
    }

    [Fact]
    public void Completar_un_borrador_no_exige_soporte_pero_cambiar_lo_ya_completo_si()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        VersionDeSeccionRegistrada Registrar(string json) =>
            (VersionDeSeccionRegistrada)seccion.Registrar(definicion, Leer(CodigosDeSeccion.General, json, Juridica), [], Juridica, Catalogos,
                OrigenDeSeccion.Tercero, "t", Ahora).Single();

        var borrador = Registrar("""{ "razon_social": "Aceros del Norte S.A.S." }""");
        borrador.Completitud.Completa.ShouldBeFalse();
        seccion = seccion.Proyectar([borrador]);
        var completa = Registrar(GeneralCompletaJuridica);
        completa.Completitud.Completa.ShouldBeTrue();
        completa.HuellaDatosAnterior.ShouldBeNull();
        seccion = seccion.Proyectar([completa]);

        // Cambio sin soporte (incompleta) y luego otro cambio: la referencia sigue siendo la última completa.
        seccion = seccion.Proyectar([Registrar(GeneralCompletaJuridica.Replace("Calle 45", "Carrera 7"))]);
        var otro = Registrar(GeneralCompletaJuridica.Replace("Calle 45", "Carrera 9"));
        otro.Completitud.Faltantes.ShouldHaveSingleItem().Regla.ShouldBe(ReglasDeFicha.ReglaSoporte);
        otro.HuellaDatosAnterior.ShouldBe(completa.HuellaDatos);
        seccion = seccion.Proyectar([otro]);

        // Volver a los datos completos no es un cambio.
        Registrar(GeneralCompletaJuridica).Completitud.Completa.ShouldBeTrue();
    }

    [Fact]
    public void Solo_los_tipos_del_mapa_de_docs14_respaldan_cada_seccion()
    {
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.General).AdmiteSoporte("RUT").ShouldBeTrue();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.General).AdmiteSoporte("CAMARA_COMERCIO").ShouldBeTrue();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Tributaria).AdmiteSoporte("CAMARA_COMERCIO").ShouldBeFalse();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Tributaria).AdmiteSoporte(CodigosDeSeccion.SoporteFicha).ShouldBeTrue();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal).AdmiteSoporte("CAMARA_COMERCIO").ShouldBeTrue();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera).AdmiteSoporte("ESTADOS_FINANCIEROS").ShouldBeTrue();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera).AdmiteSoporte("RUT").ShouldBeFalse();
        CatalogoDeSecciones.Requerida(CodigosDeSeccion.Sst).AdmiteSoporte("POLIZA").ShouldBeTrue();
        Documentos.CatalogoCanonico.Requerido(CodigosDeSeccion.SoporteFicha).Ambito.ShouldBe(Documentos.AmbitoDocumento.Global);
    }

    [Fact]
    public void Recalcular_refleja_un_codigo_que_salio_del_catalogo_y_es_idempotente()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        var version = seccion.Registrar(definicion, Leer(CodigosDeSeccion.General, GeneralCompletaJuridica, Juridica), [], Juridica, Catalogos,
            OrigenDeSeccion.Tercero, "t", Ahora);
        seccion = seccion.Proyectar(version);

        seccion.Recalcular(definicion, Juridica, Catalogos, Ahora).ShouldBeNull();
        var sinBogota = new CatalogosEnMemoria([]);
        var recalculada = seccion.Recalcular(definicion, Juridica, sinBogota, Ahora).ShouldNotBeNull();
        recalculada.Completitud.Completa.ShouldBeFalse();
        recalculada.Completitud.Faltantes.Select(f => f.Campo).ShouldBe(["ciudad", "ciiu_principal"]);
        seccion = seccion.Proyectar([recalculada]);
        seccion.Recalcular(definicion, Juridica, sinBogota, Ahora).ShouldBeNull();

        // Si el tercero resultó persona natural, la misma versión pide nombres y apellidos.
        seccion.Recalcular(definicion, Natural, Catalogos, Ahora)!.Completitud.Faltantes.Select(f => f.Campo).ShouldBe(["nombres", "apellidos"]);
    }

    // ---------- EstadoDeFicha y el wizard ----------

    private static SeccionDeFicha Seccion(string codigo, bool completa, int? periodo = null)
    {
        var definicion = CatalogoDeSecciones.Requerida(codigo);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.Empty, definicion, periodo, Ahora));
        seccion.Apply(new VersionDeSeccionRegistrada(1, Json("{}"), "h", "h", [],
            new CompletitudDeSeccion(completa, completa ? [] : [new FaltanteDeSeccion("x", null, "Falta x")], 1), 1, null, OrigenDeSeccion.Tercero, "t", Ahora));
        return seccion;
    }

    [Fact]
    public void Siguiente_seccion_del_wizard_por_tipo_de_persona()
    {
        CatalogoDeSecciones.OrdenDelWizard(TipoPersona.Juridica).Select(s => s.Codigo).ShouldBe(
            [CodigosDeSeccion.General, CodigosDeSeccion.Tributaria, CodigosDeSeccion.Legal, CodigosDeSeccion.Financiera, CodigosDeSeccion.Sst,
                CodigosDeSeccion.Especialidades]);
        CatalogoDeSecciones.OrdenDelWizard(TipoPersona.Natural).Select(s => s.Codigo).ShouldBe(
            [CodigosDeSeccion.General, CodigosDeSeccion.Tributaria, CodigosDeSeccion.Sst, CodigosDeSeccion.Especialidades]);

        var estado = EstadoDeFicha.Nuevo(Guid.Empty);
        estado.SiguienteSeccion(TipoPersona.Juridica).ShouldBe(CodigosDeSeccion.General);

        estado.Reflejar(Seccion(CodigosDeSeccion.General, completa: false)).ShouldBeTrue();
        estado.SiguienteSeccion(TipoPersona.Juridica, CodigosDeSeccion.General).ShouldBe(CodigosDeSeccion.General); // borrador: no avanza

        estado.Reflejar(Seccion(CodigosDeSeccion.General, completa: true));
        estado.SiguienteSeccion(TipoPersona.Juridica, CodigosDeSeccion.General).ShouldBe(CodigosDeSeccion.Tributaria);
        estado.SiguienteSeccion(TipoPersona.Natural, CodigosDeSeccion.General).ShouldBe(CodigosDeSeccion.Tributaria);

        estado.Reflejar(Seccion(CodigosDeSeccion.Tributaria, completa: true));
        estado.SiguienteSeccion(TipoPersona.Juridica, CodigosDeSeccion.Tributaria).ShouldBe(CodigosDeSeccion.Legal);
        estado.SiguienteSeccion(TipoPersona.Natural, CodigosDeSeccion.Tributaria).ShouldBe(CodigosDeSeccion.Sst); // salta Legal y Financiera

        // Especialidades completa con SST pendiente ⇒ vuelve a la primera incompleta.
        estado.Reflejar(Seccion(CodigosDeSeccion.Especialidades, completa: true));
        estado.SiguienteSeccion(TipoPersona.Natural, CodigosDeSeccion.Especialidades).ShouldBe(CodigosDeSeccion.Sst);

        // La financiera cuenta por su periodo más reciente.
        estado.Reflejar(Seccion(CodigosDeSeccion.Financiera, completa: true, periodo: 2024));
        estado.Reflejar(Seccion(CodigosDeSeccion.Financiera, completa: false, periodo: 2025));
        estado.Completa(CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera)).ShouldBeFalse();
        estado.Secciones.Where(s => s.Seccion == CodigosDeSeccion.Financiera).Select(s => s.Periodo).ShouldBe([2025, 2024]);

        estado.Reflejar(Seccion(CodigosDeSeccion.Sst, completa: true));
        estado.FichaCompleta(TipoPersona.Natural).ShouldBeTrue();
        estado.SiguienteSeccion(TipoPersona.Natural, CodigosDeSeccion.Sst).ShouldBeNull();
        estado.FichaCompleta(TipoPersona.Juridica).ShouldBeFalse();
        estado.Reflejar(Seccion(CodigosDeSeccion.Sst, completa: true)).ShouldBeFalse(); // idéntico: no cambia
    }

    [Fact]
    public void El_periodo_solo_aplica_a_la_financiera()
    {
        var financiera = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Financiera);
        Should.Throw<ReglaDeNegocioException>(() => CatalogoDeSecciones.PeriodoValido(financiera, null, Hoy)).Codigo.ShouldBe("periodo-requerido");
        Should.Throw<ReglaDeNegocioException>(() => CatalogoDeSecciones.PeriodoValido(financiera, 2027, Hoy)).Codigo.ShouldBe("periodo-invalido");
        CatalogoDeSecciones.PeriodoValido(financiera, 2025, Hoy).ShouldBe(2025);
        Should.Throw<ReglaDeNegocioException>(() => CatalogoDeSecciones.PeriodoValido(CatalogoDeSecciones.Requerida("ficha_general"), 2025, Hoy))
            .Codigo.ShouldBe("periodo-no-aplica");
        Should.Throw<ReglaDeNegocioException>(() => CatalogoDeSecciones.Requerida("FICHA_BANCARIA")).Codigo.ShouldBe("seccion-no-encontrada");
    }

    // ---------- catálogos ----------

    [Theory]
    [InlineData(CatalogoDePlataforma.Ciudades, "05001", "05001")]
    [InlineData(CatalogoDePlataforma.Ciudades, "5001", null)]
    [InlineData(CatalogoDePlataforma.ActividadesEconomicas, "4111", "4111")]
    [InlineData(CatalogoDePlataforma.ActividadesEconomicas, "41110", null)]
    [InlineData(CatalogoDePlataforma.ResponsabilidadesDian, "5", "05")]
    [InlineData(CatalogoDePlataforma.ResponsabilidadesDian, "105", null)]
    [InlineData(CatalogoDePlataforma.Bancos, "1007", "1007")]
    [InlineData(CatalogoDePlataforma.Especialidades, "og.con", "OG.CON")]
    public void Codigos_de_catalogo_normalizados(CatalogoDePlataforma catalogo, string codigo, string? esperado) =>
        ReglasDeCatalogo.NormalizarCodigo(catalogo, codigo).ShouldBe(esperado);

    [Fact]
    public void Filas_de_catalogo_validadas()
    {
        var bogota = ReglasDeCatalogo.Validar(CatalogoDePlataforma.Ciudades, new FilaDeCatalogo("11001", " Bogotá, D.C. ", "Bogotá, D.C."), _ => null, Ahora).Entrada!;
        bogota.Padre.ShouldBe("11");
        bogota.Pais.ShouldBe("CO");
        bogota.NombreNormalizado.ShouldBe("bogota, d.c.");
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Ciudades, new FilaDeCatalogo("11001", "Bogotá"), _ => null, Ahora).Rechazo.ShouldNotBeNull();
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Especialidades, new FilaDeCatalogo("OG.X", "X", Padre: "OG", Nivel: 2), _ => null, Ahora)
            .Rechazo!.ShouldContain("no existe");
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Especialidades, new FilaDeCatalogo("OG.X", "X", Padre: "OG", Nivel: 2), _ => 1, Ahora)
            .Entrada!.Nivel.ShouldBe(2);
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Especialidades, new FilaDeCatalogo("OG.X.Y", "Y", Padre: "OG", Nivel: 3), _ => 1, Ahora)
            .Rechazo.ShouldNotBeNull();
        Should.Throw<ReglaDeNegocioException>(() => ReglasDeCatalogo.DeRuta("monedas")).Codigo.ShouldBe("catalogo-no-encontrado");
    }

    [Fact]
    public void La_semilla_trae_las_capitales_y_las_responsabilidades_pedidas()
    {
        SemillaDeCatalogos.Ciudades.Count(c => c.Codigo!.EndsWith("001", StringComparison.Ordinal)).ShouldBe(32);
        string[] pedidas = ["05", "07", "09", "10", "11", "13", "14", "15", "18", "19", "22", "42", "47", "48", "49", "52", "53", "54", "55"];
        pedidas.Except(SemillaDeCatalogos.ResponsabilidadesDian.Select(r => r.Codigo!)).ShouldBeEmpty();
        Catalogos.Buscar(CatalogoDePlataforma.Especialidades, "OG.CON.PRE")!.Padre.ShouldBe("OG.CON");
    }

    private static (string, string?) Codigo(Func<object> accion)
    {
        var error = Should.Throw<ReglaDeNegocioException>(() => accion());
        return (error.Codigo, error.Datos?.GetValueOrDefault("campo") as string);
    }
}
