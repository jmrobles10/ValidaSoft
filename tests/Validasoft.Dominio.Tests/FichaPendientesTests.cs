using System.Text.Json;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Pendientes de la ficha (FD-1…FD-8): la General prellenada, un cambio sin soporte (FM-15), la cédula que respalda también la legal, la
/// certificación bancaria que respalda las cuentas y lo que difiere entre persona natural y jurídica.
/// </summary>
public class FichaPendientesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 11);
    private static readonly ICatalogosDeFicha Catalogos = FichaIntegracionErpTests.CatalogosDeSemilla();

    private static readonly ContextoDeSeccion Juridica = new(TipoPersona.Juridica, Identificacion.Crear("CO", "NIT", "900373913"), null, Hoy);
    private static readonly ContextoDeSeccion Natural = new(TipoPersona.Natural, Identificacion.Crear("CO", "CC", "79123456"), null, Hoy);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static Dictionary<string, JsonElement> Hechos(object campos) =>
        JsonSerializer.SerializeToElement(campos).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    private static object Leer(string seccion, string json, ContextoDeSeccion contexto) =>
        ReglasDeFicha.Leer(CatalogoDeSecciones.Requerida(seccion), Json(json), contexto);

    private static Tercero TerceroDe(TipoPersona tipo, string nombre) =>
        Tercero.Crear(tipo == TipoPersona.Natural ? Natural.Identificacion : Juridica.Identificacion, tipo, nombre, null, null, Ahora);

    // ---------- FD-1: la General prellenada ----------

    [Fact]
    public void La_general_de_una_juridica_se_prellena_con_la_razon_social_la_identificacion_y_el_correo_de_la_invitacion()
    {
        var prellenado = PrellenadoDeGeneral.Armar(TerceroDe(TipoPersona.Juridica, "Aceros del Norte S.A.S."), "  Compras@Aceros.co ", Juridica).ShouldNotBeNull();

        prellenado.GetProperty("razon_social").GetString().ShouldBe("Aceros del Norte S.A.S.");
        prellenado.GetProperty("correo").GetString().ShouldBe("compras@aceros.co");
        // La identificación la fija el registro del tercero, con su DV calculado.
        prellenado.GetProperty("tipo_persona").GetString().ShouldBe("Juridica");
        prellenado.GetProperty("tipo_identificacion").GetString().ShouldBe("NIT");
        prellenado.GetProperty("numero").GetString().ShouldBe("900373913");
        prellenado.GetProperty("dv").GetString().ShouldNotBeNullOrEmpty();
        prellenado.TryGetProperty("nombres", out _).ShouldBeFalse();
        prellenado.TryGetProperty("apellidos", out _).ShouldBeFalse();
    }

    [Fact]
    public void La_de_una_natural_parte_el_nombre_en_nombres_y_apellidos_y_no_lleva_razon_social()
    {
        var prellenado = PrellenadoDeGeneral.Armar(TerceroDe(TipoPersona.Natural, "Juan Carlos Pérez Gómez"), "juan@correo.co", Natural).ShouldNotBeNull();

        prellenado.GetProperty("nombres").GetString().ShouldBe("Juan Carlos");
        prellenado.GetProperty("apellidos").GetString().ShouldBe("Pérez Gómez");
        prellenado.TryGetProperty("razon_social", out _).ShouldBeFalse();
        prellenado.GetProperty("numero").GetString().ShouldBe("79123456");
        prellenado.TryGetProperty("dv", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Juan", "Juan", null)]
    [InlineData("Juan Pérez", "Juan", "Pérez")]
    [InlineData("Juan Pérez Gómez", "Juan", "Pérez Gómez")]
    [InlineData("Juan Carlos Pérez Gómez", "Juan Carlos", "Pérez Gómez")]
    [InlineData("María De La Cruz Pérez", "María", "De La Cruz Pérez")]
    [InlineData("  Ana   María  del Río  Soto ", "Ana María", "del Río Soto")]
    public void El_nombre_se_divide_con_la_regla_documentada_y_las_particulas_quedan_con_el_apellido(string completo, string nombres, string? apellidos)
    {
        PrellenadoDeGeneral.DividirNombre(completo).ShouldBe((nombres, apellidos));
    }

    [Fact]
    public void Un_correo_sin_forma_de_correo_no_se_sugiere()
    {
        var prellenado = PrellenadoDeGeneral.Armar(TerceroDe(TipoPersona.Juridica, "Aceros del Norte S.A.S."), "no-es-un-correo", Juridica).ShouldNotBeNull();
        prellenado.TryGetProperty("correo", out _).ShouldBeFalse();
    }

    // ---------- FD-8: lo que difiere entre persona natural y jurídica ----------

    [Fact]
    public void La_natural_declara_su_PEP_y_la_expedicion_de_su_documento_y_la_juridica_no()
    {
        var natural = Leer(CodigosDeSeccion.General,
            """{ "nombres": "Juan", "apellidos": "Pérez", "pep": false, "vinculo_pep": true, "fecha_expedicion": "2001-05-20", "lugar_expedicion": "  Bogotá D.C. " }""",
            Natural).ShouldBeOfType<DatosGenerales>();
        (natural.Pep, natural.VinculoPep, natural.FechaExpedicion, natural.LugarExpedicion).ShouldBe((false, true, new DateOnly(2001, 5, 20), "Bogotá D.C."));

        foreach (var (campo, valor) in new[]
                 {
                     ("pep", "true"), ("vinculo_pep", "false"), ("fecha_expedicion", "\"2001-05-20\""), ("lugar_expedicion", "\"Bogotá\""),
                     ("nombres", "\"Juan\""), ("apellidos", "\"Pérez\"")
                 })
        {
            var error = Should.Throw<ReglaDeNegocioException>(() => Leer(CodigosDeSeccion.General, $$"""{ "razon_social": "Aceros", "{{campo}}": {{valor}} }""", Juridica));
            error.Codigo.ShouldBe("campo-no-aplica");
            error.Datos!["campo"].ShouldBe(campo);
        }
    }

    [Fact]
    public void La_razon_social_no_aplica_a_una_natural()
    {
        var error = Should.Throw<ReglaDeNegocioException>(() => Leer(CodigosDeSeccion.General, """{ "nombres": "Juan", "razon_social": "Aceros" }""", Natural));
        (error.Codigo, error.Datos!["campo"]).ShouldBe(("campo-no-aplica", "razon_social"));
    }

    [Fact]
    public void La_expedicion_no_puede_ser_futura_ni_el_lugar_tener_mas_de_cien_caracteres()
    {
        var futura = (DatosGenerales)Leer(CodigosDeSeccion.General, """{ "nombres": "Juan", "apellidos": "Pérez", "fecha_expedicion": "2030-01-01" }""", Natural);
        var resultado = ReglasDeFicha.Evaluar(CatalogoDeSecciones.Requerida(CodigosDeSeccion.General), futura, Natural, Catalogos, false, 0);
        resultado.Errores.ShouldContain(e => e.Campo == "fecha_expedicion");

        var largo = (DatosGenerales)Leer(CodigosDeSeccion.General, $$"""{ "nombres": "Juan", "lugar_expedicion": "{{new string('x', 101)}}" }""", Natural);
        ReglasDeFicha.Evaluar(CatalogoDeSecciones.Requerida(CodigosDeSeccion.General), largo, Natural, Catalogos, false, 0).Errores
            .ShouldContain(e => e.Campo == "lugar_expedicion");
    }

    [Fact]
    public void El_representante_de_una_natural_lleva_su_expedicion_y_su_PEP_al_ERP()
    {
        var general = (DatosGenerales)Leer(CodigosDeSeccion.General,
            """{ "nombres": "Juan", "apellidos": "Pérez", "correo": "juan@correo.co", "pep": true, "fecha_expedicion": "2001-05-20", "lugar_expedicion": "Bogotá" }""",
            Natural);

        var representante = RepresentanteLegalPrincipal.Elegir(TipoPersona.Natural, general, null).ShouldNotBeNull();
        representante.Origen.ShouldBe(OrigenDelRepresentante.General);
        (representante.Persona.FechaExpedicion, representante.Persona.LugarExpedicion, representante.Persona.Pep)
            .ShouldBe((new DateOnly(2001, 5, 20), "Bogotá", true));

        var dto = ContenidoParaErp.Representante(representante).ShouldNotBeNull();
        (dto.FechaExpedicion, dto.LugarExpedicion, dto.Pep).ShouldBe((new DateOnly(2001, 5, 20), "Bogotá", true));
    }

    // ---------- FD-6: la cédula respalda también la información legal ----------

    [Fact]
    public void El_mapa_de_respaldo_dice_que_la_cedula_respalda_la_general_y_la_legal_y_la_certificacion_las_cuentas()
    {
        MapaDeRespaldo.Secciones("CEDULA").ShouldBe([CodigosDeSeccion.General, CodigosDeSeccion.Legal]);
        MapaDeRespaldo.Respalda("cedula", CodigosDeSeccion.Legal).ShouldBeTrue();
        MapaDeRespaldo.TiposPara(CodigosDeSeccion.Legal).ShouldBe(["RUT", "CAMARA_COMERCIO", "CEDULA"], ignoreOrder: true);

        // La certificación bancaria no respalda ninguna sección de la ficha: respalda las cuentas (FD-5).
        MapaDeRespaldo.Secciones("CERTIFICACION_BANCARIA").ShouldBeEmpty();
        MapaDeRespaldo.RespaldaCuentas("certificacion_bancaria").ShouldBeTrue();
        MapaDeRespaldo.RespaldaAlguna("CERTIFICACION_BANCARIA").ShouldBeTrue();
        MapaDeRespaldo.Respalda("CERTIFICACION_BANCARIA", CodigosDeSeccion.CuentasBancarias).ShouldBeTrue();
        MapaDeRespaldo.TiposPara(CodigosDeSeccion.CuentasBancarias).ShouldBe(["CERTIFICACION_BANCARIA"]);
        MapaDeRespaldo.RespaldaCuentas("RUT").ShouldBeFalse();
        // Solo secciones de la ficha: la de cuentas no está en el catálogo de secciones.
        CatalogoDeSecciones.Buscar(CodigosDeSeccion.CuentasBancarias).ShouldBeNull();
    }

    private static readonly JsonElement LegalConRepresentante = Json("""
        { "personas": [
            { "rol": "RepresentanteLegal", "tipo_representacion": "Principal", "tipo_identificacion": "CC", "numero": "52123456",
              "nombres": "Ana", "apellidos": "Gómez", "fecha_expedicion": "1999-03-02" },
            { "rol": "Socio", "tipo_identificacion": "CC", "numero": "80111222", "nombres": "Luis", "apellidos": "Ruiz", "porcentaje": 100 } ] }
        """);

    private static CruceDeSeccion? CruceLegalConCedula(object cedula) =>
        CruceConDocumento.Cruzar(CodigosDeSeccion.Legal, LegalConRepresentante, TipoPersona.Juridica, null, "CEDULA", Guid.NewGuid(), 1, Hechos(cedula), 0.95m, true, Ahora);

    [Fact]
    public void La_cedula_del_representante_coincide_con_su_fila_de_la_legal_y_cruza_sus_fechas()
    {
        var cruce = CruceLegalConCedula(new { numero = "52.123.456", nombres = "ANA", apellidos = "GOMEZ", fechaExpedicion = "1999-03-02", fechaNacimiento = "1980-01-01" })
            .ShouldNotBeNull();

        cruce.Campos.Single(c => c.Campo == "personas[0].numero").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        cruce.Campos.Single(c => c.Campo == "personas[0]").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        cruce.Campos.Single(c => c.Campo == "personas[0].fecha_expedicion").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        // La persona no declaró su nacimiento: no se compara (la propuesta es la que invita a llenarlo).
        cruce.Campos.ShouldNotContain(c => c.Campo == "personas[0].fecha_nacimiento");
        cruce.DiscrepanciaCritica.ShouldBeFalse();
        cruce.Campos.First().Etiqueta.ShouldStartWith("Representante legal");
    }

    [Fact]
    public void Una_cedula_con_otro_nombre_o_de_alguien_que_no_figura_es_una_discrepancia_critica()
    {
        var otroNombre = CruceLegalConCedula(new { numero = "52123456", nombres = "Marta", apellidos = "Gómez" }).ShouldNotBeNull();
        otroNombre.Campos.Single(c => c.Campo == "personas[0]").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        otroNombre.DiscrepanciaCritica.ShouldBeTrue();

        var ajena = CruceLegalConCedula(new { numero = "11222333", nombres = "Pedro", apellidos = "Soto" }).ShouldNotBeNull();
        ajena.Campos.ShouldHaveSingleItem().Campo.ShouldBe("personas");
        ajena.DiscrepanciaCritica.ShouldBeTrue();

        // Sin nada que leer de la cédula no hay cruce.
        CruceLegalConCedula(new { }).ShouldBeNull();
    }

    [Fact]
    public void La_cedula_de_la_natural_tambien_cruza_la_expedicion_si_la_declaro()
    {
        var general = Json("""{ "nombres": "Juan", "apellidos": "Pérez", "numero": "79123456", "fecha_expedicion": "2001-05-20" }""");
        var cruce = CruceConDocumento.Cruzar(CodigosDeSeccion.General, general, TipoPersona.Natural, null, "CEDULA", Guid.NewGuid(), 1,
            Hechos(new { numero = "79123456", nombres = "Juan", apellidos = "Perez", fechaExpedicion = "2001-05-21" }), 0.9m, true, Ahora).ShouldNotBeNull();
        var campo = cruce.Campos.Single(c => c.Campo == "fecha_expedicion");
        (campo.Resultado, campo.Critico).ShouldBe((ResultadoDeCruce.Discrepa, false));
    }

    [Fact]
    public void La_cedula_propone_las_fechas_de_la_persona_de_la_legal_con_ese_numero_y_nunca_agrega_personas()
    {
        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Legal, LegalConRepresentante, TipoPersona.Juridica, null, "CEDULA",
            Hechos(new { numero = "52123456", nombres = "Ana", apellidos = "Gómez", fechaExpedicion = "1999-03-05", fechaNacimiento = "1980-01-01" }));

        propuesta.Campos.Select(c => (c.Campo, c.Actual, c.Propuesto)).ShouldBe(
        [
            ("personas[0].fecha_expedicion", "1999-03-02", "1999-03-05"),
            ("personas[0].fecha_nacimiento", null, "1980-01-01")
        ]);
        propuesta.Datos.GetProperty("personas").GetArrayLength().ShouldBe(2);

        // Una cédula de alguien que no figura no propone nada.
        PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Legal, LegalConRepresentante, TipoPersona.Juridica, null, "CEDULA",
            Hechos(new { numero = "11222333", fechaExpedicion = "1999-03-05" })).HayCambios.ShouldBeFalse();
    }

    [Fact]
    public void La_cedula_de_la_natural_propone_su_fecha_de_expedicion()
    {
        var actuales = Json("""{ "nombres": "Juan", "apellidos": "Pérez" }""");
        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, actuales, TipoPersona.Natural, null, "CEDULA",
            Hechos(new { numero = "79123456", nombres = "Juan", apellidos = "Pérez", fechaExpedicion = "2001-05-20" }));
        propuesta.Campos.ShouldHaveSingleItem().Campo.ShouldBe("fecha_expedicion");
        propuesta.Datos.GetProperty("fecha_expedicion").GetString().ShouldBe("2001-05-20");
    }

    // ---------- FD-5: las cuentas contra la certificación bancaria ----------

    private static readonly DatosDeCuentaParaCruce Cuenta = new("Bancolombia", "Ahorros", "123-456789-01", "Aceros del Norte S.A.S.", "900373913");

    private static CruceDeSeccion CruceDeCuenta(object certificacion, DatosDeCuentaParaCruce? cuenta = null, bool legible = true) =>
        CruceConDocumento.CruzarCuenta(cuenta ?? Cuenta, "Aceros del Norte S.A.S.", "900373913", Guid.NewGuid(), 2, Hechos(certificacion), 0.93m, legible, Ahora);

    private static readonly object CertificacionBuena = new
    {
        banco = "Bancolombia S.A.", tipoCuenta = "Cuenta de ahorros", numeroCuenta = "12345678901", titular = "ACEROS DEL NORTE SAS",
        identificacionTitular = "900.373.913-4", estadoCuenta = "Activa"
    };

    [Fact]
    public void Una_cuenta_que_coincide_con_su_certificacion_no_discrepa_y_el_numero_va_siempre_enmascarado()
    {
        var cruce = CruceDeCuenta(CertificacionBuena);

        cruce.Campos.Where(c => c.Resultado != ResultadoDeCruce.Coincide).ShouldBeEmpty();
        cruce.DiscrepanciaCritica.ShouldBeFalse();
        cruce.TipoDocumento.ShouldBe("CERTIFICACION_BANCARIA");
        var numero = cruce.Campos.Single(c => c.Campo == "numero");
        (numero.Declarado, numero.Extraido, numero.Critico).ShouldBe(("****8901", "****8901", true));
        // Ni en el cruce ni en ningún campo aparece el número completo.
        cruce.Campos.ShouldAllBe(c => (c.Declarado ?? string.Empty).Replace("12345678901", "").Length == (c.Declarado ?? string.Empty).Length);
        cruce.Campos.Select(c => c.Declarado + c.Extraido).ShouldAllBe(texto => !texto.Contains("12345678901") && !texto.Contains("123-456789"));
    }

    [Fact]
    public void Otro_numero_otro_titular_o_una_cuenta_inactiva_son_discrepancias_criticas()
    {
        var otroNumero = CruceDeCuenta(new { numeroCuenta = "99999999999", titular = "Aceros del Norte SAS", identificacionTitular = "900373913", estadoCuenta = "Activa" });
        otroNumero.Campos.Single(c => c.Campo == "numero").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        otroNumero.DiscrepanciaCritica.ShouldBeTrue();

        // La cuenta es de otra persona: la certificación no dice que el titular sea el tercero.
        var ajena = CruceDeCuenta(new
        {
            numeroCuenta = "12345678901", titular = "Pedro Soto", identificacionTitular = "79123456", estadoCuenta = "Activa"
        }, Cuenta with { Titular = "Pedro Soto", IdentificacionTitular = "79123456" });
        ajena.Campos.Single(c => c.Campo == "identificacion_titular").Resultado.ShouldBe(ResultadoDeCruce.Coincide);
        var delTercero = ajena.Campos.Single(c => c.Campo == "titular_es_el_tercero");
        (delTercero.Resultado, delTercero.Critico).ShouldBe((ResultadoDeCruce.Discrepa, true));

        var inactiva = CruceDeCuenta(new
        {
            numeroCuenta = "12345678901", titular = "Aceros del Norte SAS", identificacionTitular = "900373913", estadoCuenta = "Cuenta inactiva"
        });
        inactiva.Campos.Single(c => c.Campo == "estado_cuenta").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        inactiva.DiscrepanciaCritica.ShouldBeTrue();
    }

    [Fact]
    public void El_banco_y_el_tipo_de_cuenta_son_evidencia_no_criticos_y_lo_que_no_trae_el_documento_no_es_verificable()
    {
        var cruce = CruceDeCuenta(new
        {
            banco = "Davivienda", tipoCuenta = "Corriente", numeroCuenta = "12345678901", titular = "Aceros del Norte SAS",
            identificacionTitular = "900373913", estadoCuenta = "Activa"
        });
        cruce.Campos.Single(c => c.Campo == "banco").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        cruce.Campos.Single(c => c.Campo == "tipo_cuenta").Resultado.ShouldBe(ResultadoDeCruce.Discrepa);
        cruce.DiscrepanciaCritica.ShouldBeFalse();

        var incompleta = CruceDeCuenta(new { banco = "Bancolombia" });
        incompleta.Campos.Single(c => c.Campo == "numero").Resultado.ShouldBe(ResultadoDeCruce.NoVerificable);
        incompleta.Campos.Single(c => c.Campo == "estado_cuenta").Resultado.ShouldBe(ResultadoDeCruce.NoVerificable);
        incompleta.DiscrepanciaCritica.ShouldBeFalse();

        var ilegible = CruceDeCuenta(CertificacionBuena, legible: false);
        ilegible.Campos.ShouldAllBe(c => c.Resultado == ResultadoDeCruce.NoVerificable);
    }

    [Fact]
    public void Enmascarar_deja_solo_los_ultimos_cuatro_digitos()
    {
        CruceConDocumento.Enmascarar("123-456 789-01").ShouldBe("****8901");
        CruceConDocumento.Enmascarar("12").ShouldBe("****12");
    }

    // ---------- FD-4: un cambio sin documento nuevo se marca «sin soporte» (FM-15) ----------

    private const string GeneralJuridica = """
        { "razon_social": "Aceros del Norte S.A.S.", "correo": "compras@aceros.co", "telefono": "+57 601 555 1234", "pais": "CO",
          "ciudad": "11001", "direccion": "Calle 45 # 12-08", "ciiu_principal": "4663" }
        """;

    /// <summary>La sección general de una jurídica con las versiones registradas en orden: (datos, soportes, origen).</summary>
    private static SeccionDeFicha SeccionCon(params (string Json, SoporteDeSeccion[] Soportes, OrigenDeSeccion Origen)[] versiones)
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        foreach (var (json, soportes, origen) in versiones)
        {
            var eventos = seccion.Registrar(definicion, Leer(CodigosDeSeccion.General, json, Juridica), soportes, Juridica, Catalogos, origen, "t", Ahora);
            seccion = seccion.Proyectar(eventos);
        }

        return seccion;
    }

    private static readonly SoporteDeSeccion Rut1 = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, "RUT");
    private static readonly SoporteDeSeccion Rut2 = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), 2, "RUT");

    [Fact]
    public void Un_cambio_con_el_mismo_soporte_de_antes_queda_sin_soporte_y_uno_con_un_documento_nuevo_no()
    {
        var cambiada = GeneralJuridica.Replace("Calle 45", "Carrera 7");
        var seccion = SeccionCon(
            (GeneralJuridica, [], OrigenDeSeccion.Tercero),
            (cambiada, [Rut1], OrigenDeSeccion.Tercero),
            (cambiada.Replace("Carrera 7", "Carrera 9"), [Rut1], OrigenDeSeccion.Tercero),
            (cambiada.Replace("Carrera 7", "Carrera 11"), [Rut2], OrigenDeSeccion.Tercero));

        // 1: la primera versión no tiene con qué compararse. 2: cambió con un documento que la anterior no tenía.
        seccion.SinSoporte(1).ShouldBeNull();
        seccion.SinSoporte(2).ShouldBeNull();
        // 3: cambió la dirección y trae el mismo RUT que la versión 2: sin soporte.
        var sinSoporte = seccion.SinSoporte(3).ShouldNotBeNull();
        sinSoporte.Cambios.ShouldHaveSingleItem().Campo.ShouldBe("direccion");
        sinSoporte.Critico.ShouldBeFalse();
        // 4: la misma dirección que cambia, pero con una versión nueva del RUT: respaldada.
        seccion.SinSoporte(4).ShouldBeNull();
    }

    [Fact]
    public void Sin_soporte_se_mide_contra_lo_que_la_empresa_acepto_cuando_se_indica()
    {
        var v2 = GeneralJuridica.Replace("Calle 45", "Carrera 7");
        var seccion = SeccionCon(
            (GeneralJuridica, [], OrigenDeSeccion.Tercero),
            (v2, [Rut1], OrigenDeSeccion.Tercero),
            (v2.Replace("Carrera 7", "Carrera 9"), [Rut2], OrigenDeSeccion.Tercero),
            (v2.Replace("Carrera 7", "Carrera 11"), [Rut2], OrigenDeSeccion.Tercero));

        // La versión 4 trae el mismo RUT (v2) de la 3, pero frente a lo aceptado (la 2) ese RUT es nuevo: respaldada.
        seccion.SinSoporte(4).ShouldNotBeNull();
        seccion.SinSoporte(4, desdeVersion: 2).ShouldBeNull();
        // Frente a la propia versión no hay cambio que medir.
        seccion.SinSoporte(4, desdeVersion: 4).ShouldBeNull();
    }

    [Fact]
    public void Es_critico_si_toca_la_razon_social_y_no_si_toca_la_direccion()
    {
        var seccion = SeccionCon(
            (GeneralJuridica, [], OrigenDeSeccion.Tercero),
            (GeneralJuridica.Replace("Aceros del Norte S.A.S.", "Aceros del Sur S.A.S."), [Rut1], OrigenDeSeccion.Tercero),
            (GeneralJuridica.Replace("Aceros del Norte S.A.S.", "Aceros del Sur S.A.S.").Replace("Calle 45", "Carrera 7"), [Rut1], OrigenDeSeccion.Tercero),
            (GeneralJuridica.Replace("Aceros del Norte S.A.S.", "Aceros del Centro S.A.S.").Replace("Calle 45", "Carrera 7"), [Rut1], OrigenDeSeccion.Tercero));

        seccion.SinSoporte(3).ShouldNotBeNull().Critico.ShouldBeFalse();
        seccion.SinSoporte(4).ShouldNotBeNull().Critico.ShouldBeTrue();
    }

    [Fact]
    public void Lo_que_llega_de_un_sistema_de_origen_no_se_marca_sin_soporte()
    {
        var seccion = SeccionCon(
            (GeneralJuridica, [], OrigenDeSeccion.Tercero),
            (GeneralJuridica.Replace("Calle 45", "Carrera 7"), [], OrigenDeSeccion.SistemaExterno));
        seccion.SinSoporte(2).ShouldBeNull();
    }

    [Fact]
    public void Un_cambio_a_una_persona_legal_es_critico_solo_si_es_representante_o_suplente()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal);
        const string Base = """
            { "personas": [
                { "rol": "RepresentanteLegal", "tipo_representacion": "Principal", "tipo_identificacion": "CC", "numero": "52123456", "nombres": "Ana", "apellidos": "Gómez", "pep": false, "vinculo_pep": false },
                { "rol": "Socio", "tipo_identificacion": "CC", "numero": "80111222", "nombres": "Luis", "apellidos": "Ruiz", "porcentaje": 100, "pep": false, "vinculo_pep": false } ] }
            """;

        SeccionDeFicha Con(params string[] versiones)
        {
            var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
            for (var i = 0; i < versiones.Length; i++)
            {
                SoporteDeSeccion[] soportes = i == 0 ? [] : [Rut1];
                seccion = seccion.Proyectar(seccion.Registrar(definicion, Leer(CodigosDeSeccion.Legal, versiones[i], Juridica), soportes, Juridica, Catalogos,
                    OrigenDeSeccion.Tercero, "t", Ahora));
            }

            return seccion;
        }

        var porcentaje = Con(Base, Base.Replace("\"porcentaje\": 100", "\"porcentaje\": 60"), Base.Replace("\"porcentaje\": 100", "\"porcentaje\": 55"));
        porcentaje.SinSoporte(3).ShouldNotBeNull().Critico.ShouldBeFalse();

        var cargo = Con(Base, Base.Replace("\"apellidos\": \"Gómez\"", "\"apellidos\": \"Gómez Ruiz\""),
            Base.Replace("\"apellidos\": \"Gómez\"", "\"apellidos\": \"Gómez Soto\""));
        cargo.SinSoporte(3).ShouldNotBeNull().Critico.ShouldBeTrue();
    }
}
