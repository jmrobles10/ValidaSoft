using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>PNG sintéticos para probar la firma: cualquier tipo de color de 8 bits, con los cinco filtros de fila.</summary>
internal static class PngDePrueba
{
    /// <summary>Imagen con un degradado conocido: el píxel (x, y) vale <c>(x * 7 + y * 3)</c> en cada canal (el alfa, 200).</summary>
    public static byte[] Crear(int ancho, int alto, int tipoColor = 6, bool filtrar = true, int profundidad = 8, int entrelazado = 0, bool vacia = false)
    {
        var canales = tipoColor switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        var fila = ancho * canales;
        var crudo = new List<byte>();
        var anterior = new byte[fila];
        for (var y = 0; y < alto; y++)
        {
            var actual = new byte[fila];
            for (var x = 0; x < ancho; x++)
                for (var c = 0; c < canales; c++)
                    actual[x * canales + c] = vacia ? (byte)255 : ValorDe(x, y, c, canales, tipoColor);
            var filtro = filtrar ? y % 5 : 0;
            crudo.Add((byte)filtro);
            crudo.AddRange(Filtrar(filtro, actual, anterior, canales));
            anterior = actual;
        }

        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write([.. crudo]);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new List<byte>();
        ihdr.AddRange(Entero(ancho));
        ihdr.AddRange(Entero(alto));
        ihdr.AddRange([(byte)profundidad, (byte)tipoColor, 0, 0, (byte)entrelazado]);
        Trozo(png, "IHDR", [.. ihdr]);
        if (tipoColor == 3)
            Trozo(png, "PLTE", [.. Enumerable.Range(0, 256).SelectMany(i => new[] { (byte)i, (byte)(255 - i), (byte)(i / 2) })]);
        Trozo(png, "IDAT", comprimido.ToArray());
        Trozo(png, "IEND", []);
        return png.ToArray();
    }

    public static byte ValorDe(int x, int y, int canal, int canales, int tipoColor) =>
        canales == 4 && canal == 3 || tipoColor == 4 && canal == 1 ? (byte)200 : (byte)((x * 7 + y * 3) % 251);

    private static byte[] Entero(int n) => [(byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n];

    private static void Trozo(Stream s, string tipo, byte[] datos)
    {
        s.Write(Entero(datos.Length));
        s.Write(System.Text.Encoding.ASCII.GetBytes(tipo));
        s.Write(datos);
        s.Write([0, 0, 0, 0]); // el CRC no se comprueba
    }

    private static byte[] Filtrar(int filtro, byte[] actual, byte[] anterior, int bpp)
    {
        var salida = new byte[actual.Length];
        for (var i = 0; i < actual.Length; i++)
        {
            int a = i >= bpp ? actual[i - bpp] : 0, b = anterior[i], c = i >= bpp ? anterior[i - bpp] : 0;
            int predictor = filtro switch
            {
                1 => a,
                2 => b,
                3 => (a + b) >> 1,
                4 => Paeth(a, b, c),
                _ => 0
            };
            salida[i] = (byte)(actual[i] - predictor);
        }

        return salida;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }
}

/// <summary>
/// Declaración jurada PEP (HU 49093, DP-1…DP-14): obligatoria con «Sí» a PEP, todos sus campos y las filas todo-o-nada, el máximo de
/// 30 filas, los datos básicos y los sellos que fija el servidor, la firma (PNG decodificable) y lo que conserva cada versión.
/// </summary>
public partial class DeclaracionPepTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 12);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 12, 15, 0, 0, TimeSpan.Zero);

    private static readonly ContextoDeSeccion NaturalSinExigir = new(TipoPersona.Natural, Identificacion.Crear("CO", "CC", "79123456"), null, Hoy);
    private static readonly ContextoDeSeccion Natural = NaturalSinExigir with { ExigeDeclaracionPep = true, IpDeFirma = "10.0.0.7" };
    private static readonly ContextoDeSeccion JuridicaSinExigir = new(TipoPersona.Juridica, Identificacion.Crear("CO", "NIT", "900373913"), null, Hoy);
    private static readonly ContextoDeSeccion Juridica = JuridicaSinExigir with { ExigeDeclaracionPep = true, IpDeFirma = "10.0.0.7" };

    private static readonly string FirmaBase64 = Convert.ToBase64String(PngDePrueba.Crear(600, 200));

    /// <summary>Una declaración completa (en una persona natural): cada prueba le cambia lo que quiere probar.</summary>
    private static JsonObject Declaracion() => JsonNode.Parse($$"""
        {
          "nacionalidad": "co", "cargo": "Alcalde municipal", "institucion": "Alcaldía de Sopó", "pais": "CO", "fecha_vinculacion": "2018-01-01",
          "vigente": false, "fecha_desvinculacion": "2022-01-01", "cuentas_exterior": false, "vinculo_con_pep": { "tiene": false },
          "acepta_juramento": true, "firma_en_calidad": "NombrePropio", "firma_png": "{{FirmaBase64}}"
        }
        """)!.AsObject();

    private static JsonObject GeneralNatural(JsonObject? declaracion, bool? pep = true, string? direccion = "Carrera 7 # 32-16")
    {
        var general = new JsonObject { ["nombres"] = "Ana", ["apellidos"] = "Gómez", ["pep"] = pep, ["vinculo_pep"] = false };
        if (direccion is not null)
            general["direccion"] = direccion;
        if (declaracion is not null)
            general["declaracion_pep"] = declaracion.DeepClone();
        return general;
    }

    private static JsonObject Persona(string rol, JsonObject? declaracion, bool? pep = true, string numero = "79555111")
    {
        var persona = new JsonObject
        {
            ["rol"] = rol, ["tipo_identificacion"] = "CC", ["numero"] = numero, ["nombres"] = "Jorge Iván", ["apellidos"] = "Castro Pinto", ["pep"] = pep,
            ["vinculo_pep"] = false
        };
        if (rol == "Socio")
            persona["porcentaje"] = 100;
        if (declaracion is not null)
            persona["declaracion_pep"] = declaracion.DeepClone();
        return persona;
    }

    private static JsonObject Legal(params JsonObject[] personas) => new() { ["personas"] = new JsonArray([.. personas.Select(p => (JsonNode)p)]) };

    private static (DefinicionDeSeccion Definicion, object Datos) Leer(string seccion, JsonObject json, ContextoDeSeccion contexto)
    {
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        return (definicion, ReglasDeFicha.Leer(definicion, JsonDocument.Parse(json.ToJsonString()).RootElement.Clone(), contexto));
    }

    private static ResultadoDeRegla Evaluar(string seccion, JsonObject json, ContextoDeSeccion contexto)
    {
        var (definicion, datos) = Leer(seccion, json, contexto);
        return ReglasDeFicha.Evaluar(definicion, datos, contexto, new CatalogosEnMemoria([]), false, 0);
    }

    /// <summary>Lo que dice la regla de la declaración: los errores de formato y los faltantes de las rutas <c>…declaracion_pep…</c>.</summary>
    private static List<string> Rutas(ResultadoDeRegla r) =>
    [
        .. r.Errores.Select(e => e.Campo).Concat(r.Completitud.Faltantes.Select(f => f.Campo ?? f.Regla ?? string.Empty)).Where(c => c.Contains("declaracion_pep", StringComparison.Ordinal))
    ];

    private static List<string> RutasDeNatural(JsonObject declaracion, ContextoDeSeccion? contexto = null) =>
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(declaracion), contexto ?? Natural));

    private static DeclaracionPep LeerDeclaracion(JsonObject declaracion, ContextoDeSeccion? contexto = null) =>
        ((DatosGenerales)Leer(CodigosDeSeccion.General, GeneralNatural(declaracion), contexto ?? Natural).Datos).DeclaracionPep!;

    // ---------- obligatoriedad ----------

    [Fact]
    public void Con_si_a_pep_y_sin_declaracion_el_tercero_no_guarda()
    {
        var resultado = Evaluar(CodigosDeSeccion.General, GeneralNatural(null), Natural);
        var error = Should.Throw<ReglaDeNegocioException>(() => ReglasDeFicha.AsegurarFormato(resultado));
        error.Codigo.ShouldBe(DeclaracionPepReglas.CodigoRequerida);
        error.Datos!["campo"].ShouldBe("declaracion_pep");
    }

    [Fact]
    public void Con_si_a_pep_y_sin_declaracion_lo_que_llega_de_un_sistema_de_origen_se_guarda_incompleto()
    {
        var r = Evaluar(CodigosDeSeccion.General, GeneralNatural(null), NaturalSinExigir);
        r.Errores.ShouldBeEmpty();
        r.Completitud.Completa.ShouldBeFalse();
        Rutas(r).ShouldBe(["declaracion_pep"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void Sin_pep_no_se_pide_declaracion(bool? pep) =>
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(null, pep), Natural)).ShouldBeEmpty();

    [Fact]
    public void La_pregunta_de_vinculo_con_pep_no_abre_la_declaracion()
    {
        var general = GeneralNatural(null, pep: false);
        general["vinculo_pep"] = true;
        Rutas(Evaluar(CodigosDeSeccion.General, general, Natural)).ShouldBeEmpty();
    }

    [Fact]
    public void Una_declaracion_completa_no_deja_faltantes_y_con_pep_no_la_inactiva()
    {
        RutasDeNatural(Declaracion()).ShouldBeEmpty();
        // Con «No» a PEP la declaración se conserva (inactiva) y se valida igual.
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(Declaracion(), pep: false), Natural)).ShouldBeEmpty();
        var incompleta = Declaracion();
        incompleta.Remove("cargo");
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(incompleta, pep: false), NaturalSinExigir)).ShouldBe(["declaracion_pep.cargo"]);
    }

    public static TheoryData<string, string?, string> CasosDeObligatorios => new()
    {
        { "cargo", null, "declaracion_pep.cargo" },
        { "institucion", null, "declaracion_pep.institucion" },
        { "pais", null, "declaracion_pep.pais" },
        { "pais", "Colombia", "declaracion_pep.pais" },
        { "nacionalidad", null, "declaracion_pep.nacionalidad" },
        { "fecha_vinculacion", null, "declaracion_pep.fecha_vinculacion" },
        { "cuentas_exterior", null, "declaracion_pep.cuentas_exterior" },
        { "acepta_juramento", null, "declaracion_pep.acepta_juramento" },
        { "firma_png", null, "declaracion_pep.firma_png" },
        { "firma_en_calidad", null, "" } // la natural siempre firma a nombre propio: no falta
    };

    [Theory]
    [MemberData(nameof(CasosDeObligatorios))]
    public void Cada_campo_obligatorio_se_senala_en_su_ruta(string campo, string? valor, string ruta)
    {
        var d = Declaracion();
        d[campo] = valor;
        RutasDeNatural(d).ShouldBe(ruta.Length == 0 ? [] : [ruta]);
    }

    [Fact]
    public void El_juramento_en_falso_no_vale()
    {
        var d = Declaracion();
        d["acepta_juramento"] = false;
        RutasDeNatural(d).ShouldBe(["declaracion_pep.acepta_juramento"]);
    }

    [Fact]
    public void Sin_direccion_en_la_general_no_se_puede_firmar()
    {
        var r = Evaluar(CodigosDeSeccion.General, GeneralNatural(Declaracion(), direccion: null), Natural);
        Rutas(r).ShouldBe(["declaracion_pep.direccion"]);
    }

    // ---------- fechas y vigencia ----------

    [Fact]
    public void Las_fechas_de_vinculacion_y_desvinculacion_siguen_sus_reglas()
    {
        var futura = Declaracion();
        futura["fecha_vinculacion"] = "2026-11-13";
        RutasDeNatural(futura).ShouldContain("declaracion_pep.fecha_vinculacion");

        var anterior = Declaracion();
        anterior["fecha_desvinculacion"] = "2017-12-31";
        RutasDeNatural(anterior).ShouldBe(["declaracion_pep.fecha_desvinculacion"]);

        var desvinculacionFutura = Declaracion();
        desvinculacionFutura["fecha_desvinculacion"] = "2027-01-01";
        RutasDeNatural(desvinculacionFutura).ShouldBe(["declaracion_pep.fecha_desvinculacion"]);

        var sinDesvinculacion = Declaracion();
        sinDesvinculacion.Remove("fecha_desvinculacion");
        RutasDeNatural(sinDesvinculacion).ShouldBe(["declaracion_pep.fecha_desvinculacion"]);

        var mismoDia = Declaracion();
        mismoDia["fecha_desvinculacion"] = "2018-01-01";
        RutasDeNatural(mismoDia).ShouldBeEmpty();
    }

    [Fact]
    public void Una_fecha_de_desvinculacion_sin_la_marca_vigente_vale_como_no_vigente()
    {
        var d = Declaracion();
        d.Remove("vigente"); // la casilla sin marcar no llega como «no»
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).Vigente.ShouldBe(false);

        d.Remove("fecha_desvinculacion");
        RutasDeNatural(d).ShouldBe(["declaracion_pep.vigente"]);
    }

    [Fact]
    public void Vigente_no_pide_desvinculacion_y_la_descarta()
    {
        var d = Declaracion();
        d["vigente"] = true; // trae una fecha de desvinculación de antes de marcar «vigente»
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).FechaDesvinculacion.ShouldBeNull();
    }

    // ---------- cuentas en el exterior, cónyuge, familiares y sociedades ----------

    [Fact]
    public void Cuentas_en_el_exterior_piden_al_menos_un_pais_y_sin_cuentas_se_descartan()
    {
        var d = Declaracion();
        d["cuentas_exterior"] = true;
        RutasDeNatural(d).ShouldBe(["declaracion_pep.paises_cuentas_exterior"]);

        d["paises_cuentas_exterior"] = new JsonArray("us", "PA", "US");
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).PaisesCuentasExterior.ShouldBe(["US", "PA"]);

        d["paises_cuentas_exterior"] = new JsonArray("Estados Unidos");
        RutasDeNatural(d).ShouldBe(["declaracion_pep.paises_cuentas_exterior[0]"]);

        d["cuentas_exterior"] = false;
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).PaisesCuentasExterior.ShouldBeNull();
    }

    [Fact]
    public void El_conyuge_es_todo_o_nada()
    {
        var d = Declaracion();
        d["conyuge"] = new JsonObject { ["nombre"] = "Luis Pérez" };
        RutasDeNatural(d).ShouldBe(["declaracion_pep.conyuge.tipo_identificacion", "declaracion_pep.conyuge.numero"]);

        d["conyuge"] = new JsonObject { ["nombre"] = "Luis Pérez", ["tipo_identificacion"] = "cc", ["numero"] = "52123456" };
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).Conyuge.ShouldBe(new ConyugePep { Nombre = "Luis Pérez", TipoIdentificacion = "CC", Numero = "52123456" });

        d["conyuge"] = new JsonObject { ["nombre"] = "  ", ["numero"] = "" };
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).Conyuge.ShouldBeNull();
    }

    private static JsonObject Familiar(string? nombre, string? parentesco, string? tipo, string? numero) =>
        new() { ["nombre"] = nombre, ["parentesco"] = parentesco, ["tipo_identificacion"] = tipo, ["numero"] = numero };

    [Fact]
    public void Cada_familiar_va_completo_y_la_fila_vacia_se_descarta()
    {
        var d = Declaracion();
        d["familiares"] = new JsonArray(Familiar("Marta Gómez", "Hermana", "CC", "52111222"), Familiar("Pedro Gómez", "Padre", null, "79111222"), Familiar(null, null, null, null));
        RutasDeNatural(d).ShouldBe(["declaracion_pep.familiares[1].tipo_identificacion"]);

        d["familiares"] = new JsonArray(Familiar("Marta Gómez", "Hermana", "CC", "52111222"), Familiar(" ", "", null, null));
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).Familiares.ShouldHaveSingleItem().Nombre.ShouldBe("Marta Gómez");
    }

    [Fact]
    public void Se_admiten_hasta_treinta_familiares_y_treinta_sociedades()
    {
        var d = Declaracion();
        d["familiares"] = new JsonArray([.. Enumerable.Range(1, 30).Select(i => (JsonNode)Familiar($"Familiar {i}", "Primo", "CC", $"5211{i:0000}"))]);
        d["sociedades"] = new JsonArray([.. Enumerable.Range(1, 30).Select(i => (JsonNode)new JsonObject { ["nombre"] = $"Sociedad {i}", ["identificacion"] = $"9001{i:00000}", ["calidad"] = "Socio" })]);
        RutasDeNatural(d).ShouldBeEmpty();

        ((JsonArray)d["familiares"]!).Add(Familiar("Familiar 31", "Primo", "CC", "52119999"));
        ((JsonArray)d["sociedades"]!).Add(new JsonObject { ["nombre"] = "Sociedad 31", ["identificacion"] = "900199999", ["calidad"] = "Socio" });
        RutasDeNatural(d).ShouldBe(["declaracion_pep.familiares", "declaracion_pep.sociedades"]);
    }

    [Fact]
    public void Una_sociedad_pide_su_calidad_y_con_otra_pide_cual()
    {
        var d = Declaracion();
        d["sociedades"] = new JsonArray(
            new JsonObject { ["nombre"] = "Inversiones SAS", ["identificacion"] = "900111222" },
            new JsonObject { ["nombre"] = "Fiducia X", ["identificacion"] = "900333444", ["calidad"] = "Otra" },
            new JsonObject { ["nombre"] = "Fiducia Y", ["identificacion"] = "900555666", ["calidad"] = "Fideicomitente", ["calidad_otra"] = "ignorada" });
        RutasDeNatural(d).ShouldBe(["declaracion_pep.sociedades[0].calidad", "declaracion_pep.sociedades[1].calidad_otra"]);
        LeerDeclaracion(d).Sociedades![2].CalidadOtra.ShouldBeNull();
    }

    // ---------- vínculo con otra PEP ----------

    [Fact]
    public void El_vinculo_con_otra_pep_pide_si_o_no_y_con_si_todos_sus_campos()
    {
        var d = Declaracion();
        d.Remove("vinculo_con_pep");
        RutasDeNatural(d).ShouldBe(["declaracion_pep.vinculo_con_pep.tiene"]);

        d["vinculo_con_pep"] = new JsonObject { ["tiene"] = true };
        RutasDeNatural(d).ShouldBe(
        [
            "declaracion_pep.vinculo_con_pep.nombre", "declaracion_pep.vinculo_con_pep.parentesco", "declaracion_pep.vinculo_con_pep.cargo_institucion",
            "declaracion_pep.vinculo_con_pep.fecha_vinculacion", "declaracion_pep.vinculo_con_pep.vigente"
        ]);

        d["vinculo_con_pep"] = new JsonObject
        {
            ["tiene"] = true, ["nombre"] = "Carlos Ruiz", ["parentesco"] = "Cuñado", ["cargo_institucion"] = "Senador, Senado", ["fecha_vinculacion"] = "2022-08-01",
            ["vigente"] = false
        };
        RutasDeNatural(d).ShouldBe(["declaracion_pep.vinculo_con_pep.fecha_desvinculacion"]);

        d["vinculo_con_pep"]!["vigente"] = true;
        RutasDeNatural(d).ShouldBeEmpty();

        d["vinculo_con_pep"] = new JsonObject { ["tiene"] = false, ["nombre"] = "Se descarta" };
        RutasDeNatural(d).ShouldBeEmpty();
        LeerDeclaracion(d).VinculoConPep.ShouldBe(new VinculoConPep { Tiene = false });
    }

    // ---------- datos básicos que fija el servidor ----------

    [Fact]
    public void Los_datos_basicos_de_la_natural_salen_de_su_general_y_firma_a_nombre_propio()
    {
        var d = Declaracion();
        d["nombre"] = "Otra Persona";
        d["numero"] = "1";
        d["relacion_empresa"] = "Socio";
        d["direccion"] = "Otra dirección";
        d["firma_en_calidad"] = "RepresentanteLegal";
        d["firmante_nombre"] = "Un representante";
        var leida = LeerDeclaracion(d);
        (leida.Nombre, leida.TipoIdentificacion, leida.Numero, leida.Direccion, leida.Nacionalidad).ShouldBe(("Ana Gómez", "CC", "79123456", "Carrera 7 # 32-16", "CO"));
        (leida.RelacionEmpresa, leida.RelacionOtra).ShouldBe((RelacionConLaEmpresa.Proveedor, null));
        (leida.FirmaEnCalidad, leida.FirmanteNombre, leida.FirmanteNumero).ShouldBe((FirmaEnCalidad.NombrePropio, "Ana Gómez", "79123456"));
    }

    [Fact]
    public void Una_persona_juridica_no_declara_pep_en_su_general_sino_en_cada_persona_de_la_legal()
    {
        var general = new JsonObject { ["razon_social"] = "Aceros S.A.S.", ["declaracion_pep"] = Declaracion() };
        var error = Should.Throw<ReglaDeNegocioException>(() => Leer(CodigosDeSeccion.General, general, Juridica));
        (error.Codigo, error.Datos!["campo"]).ShouldBe(("campo-no-aplica", "declaracion_pep"));
    }

    [Theory]
    [InlineData("RepresentanteLegal", RelacionConLaEmpresa.Proveedor, null)]
    [InlineData("Suplente", RelacionConLaEmpresa.Proveedor, null)]
    [InlineData("Apoderado", RelacionConLaEmpresa.Proveedor, null)]
    [InlineData("Socio", RelacionConLaEmpresa.Socio, null)]
    [InlineData("JuntaDirectiva", RelacionConLaEmpresa.Socio, null)]
    [InlineData("RevisorFiscal", RelacionConLaEmpresa.Otro, "Revisor fiscal")]
    [InlineData("Contador", RelacionConLaEmpresa.Otro, "Contador")]
    public void En_la_juridica_la_relacion_con_la_empresa_sale_del_rol_de_cada_persona(string rol, RelacionConLaEmpresa relacion, string? otra)
    {
        var d = Declaracion();
        d["direccion"] = "Calle 80 # 12-34";
        var (_, datos) = Leer(CodigosDeSeccion.Legal, Legal(Persona(rol, d)), Juridica);
        var leida = ((DatosLegales)datos).Personas!.Single().DeclaracionPep!;
        (leida.RelacionEmpresa, leida.RelacionOtra, leida.Nombre, leida.TipoIdentificacion, leida.Numero).ShouldBe((relacion, otra, "Jorge Iván Castro Pinto", "CC", "79555111"));
        leida.Direccion.ShouldBe("Calle 80 # 12-34"); // en la persona la escribe quien declara
        Rutas(Evaluar(CodigosDeSeccion.Legal, Legal(Persona(rol, d)), Juridica)).ShouldBeEmpty();
    }

    [Fact]
    public void En_la_juridica_la_direccion_la_escribe_el_tercero_y_la_nacionalidad_de_la_persona_se_respeta()
    {
        var sinDireccion = Declaracion();
        Rutas(Evaluar(CodigosDeSeccion.Legal, Legal(Persona("Socio", sinDireccion)), Juridica)).ShouldBe(["personas[0].declaracion_pep.direccion"]);

        var persona = Persona("Socio", Declaracion());
        persona["nacionalidad"] = "VE";
        persona["declaracion_pep"]!["direccion"] = "Calle 1";
        var leida = ((DatosLegales)Leer(CodigosDeSeccion.Legal, Legal(persona), Juridica).Datos).Personas!.Single().DeclaracionPep!;
        leida.Nacionalidad.ShouldBe("VE");
    }

    [Fact]
    public void Quien_firma_como_representante_legal_escribe_su_nombre_y_documento()
    {
        var d = Declaracion();
        d["direccion"] = "Calle 1";
        d["firma_en_calidad"] = "RepresentanteLegal";
        Rutas(Evaluar(CodigosDeSeccion.Legal, Legal(Persona("Socio", d)), Juridica))
            .ShouldBe(["personas[0].declaracion_pep.firmante_nombre", "personas[0].declaracion_pep.firmante_tipo_identificacion", "personas[0].declaracion_pep.firmante_numero"]);

        d["firmante_nombre"] = "Marta Ríos";
        d["firmante_tipo_identificacion"] = "cc";
        d["firmante_numero"] = "1020304050";
        Rutas(Evaluar(CodigosDeSeccion.Legal, Legal(Persona("Socio", d)), Juridica)).ShouldBeEmpty();
        var leida = ((DatosLegales)Leer(CodigosDeSeccion.Legal, Legal(Persona("Socio", d)), Juridica).Datos).Personas!.Single().DeclaracionPep!;
        (leida.FirmanteNombre, leida.FirmanteTipoIdentificacion, leida.FirmanteNumero).ShouldBe(("Marta Ríos", "CC", "1020304050"));
    }

    [Fact]
    public void En_la_juridica_cada_persona_pep_necesita_la_suya()
    {
        var con = Declaracion();
        con["direccion"] = "Calle 1";
        var r = Evaluar(CodigosDeSeccion.Legal, Legal(Persona("RepresentanteLegal", con, numero: "1020304050"), Persona("Socio", null)), JuridicaSinExigir);
        Rutas(r).ShouldBe(["personas[1].declaracion_pep"]);
        var error = Should.Throw<ReglaDeNegocioException>(() => ReglasDeFicha.AsegurarFormato(Evaluar(CodigosDeSeccion.Legal, Legal(Persona("RepresentanteLegal", con, numero: "1020304050"), Persona("Socio", null)), Juridica)));
        (error.Codigo, error.Datos!["campo"]).ShouldBe((DeclaracionPepReglas.CodigoRequerida, "personas[1].declaracion_pep"));
    }

    // ---------- la firma ----------

    [Fact]
    public void La_firma_debe_ser_un_png_decodificable_dentro_de_los_limites()
    {
        var d = Declaracion();
        d["firma_png"] = "esto no es base64 !!";
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        d["firma_png"] = Convert.ToBase64String("%PDF-1.7 no es un png"u8.ToArray());
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        d["firma_png"] = Convert.ToBase64String(PngDePrueba.Crear(1300, 10));
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        d["firma_png"] = Convert.ToBase64String(PngDePrueba.Crear(600, 200, profundidad: 16));
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        d["firma_png"] = Convert.ToBase64String(PngDePrueba.Crear(600, 200, entrelazado: 1));
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        // Un PNG recortado (datos dañados) se rechaza al guardar, no al imprimir.
        var corto = PngDePrueba.Crear(600, 200)[..120];
        d["firma_png"] = Convert.ToBase64String(corto);
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        // Pesa demasiado: ruido que no comprime.
        var grande = new byte[FirmaPng.MaximoBytes + 10];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(grande, 0);
        d["firma_png"] = Convert.ToBase64String(grande);
        RutasDeNatural(d).ShouldBe(["declaracion_pep.firma_png"]);

        d["firma_png"] = $"data:image/png;base64,{FirmaBase64}";
        RutasDeNatural(d).ShouldBeEmpty(); // el prefijo se limpia
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(4, true)]
    [InlineData(6, true)]
    [InlineData(6, false)]
    public void Se_decodifican_todos_los_tipos_de_color_y_los_cinco_filtros(int tipoColor, bool filtrar)
    {
        var png = PngDePrueba.Crear(40, 12, tipoColor, filtrar);
        FirmaPng.Decodificar(png, out var imagen).ShouldBeNull();
        imagen!.Ancho.ShouldBe(40);
        imagen.Alto.ShouldBe(12);
        imagen.Rgb.Length.ShouldBe(40 * 12 * 3);

        // El píxel (x, y) vale (x*7 + y*3) % 251; con alfa 200 se compone sobre blanco.
        foreach (var (x, y) in new[] { (0, 0), (5, 3), (39, 11) })
        {
            var v = (x * 7 + y * 3) % 251;
            int esperadoR, esperadoG, esperadoB;
            switch (tipoColor)
            {
                case 3:
                    (esperadoR, esperadoG, esperadoB) = (v, 255 - v, v / 2);
                    break;
                case 4 or 6:
                    esperadoR = esperadoG = esperadoB = (v * 200 + 255 * 55) / 255;
                    break;
                default:
                    esperadoR = esperadoG = esperadoB = v;
                    break;
            }

            var i = (y * 40 + x) * 3;
            ((int)imagen.Rgb[i], (int)imagen.Rgb[i + 1], (int)imagen.Rgb[i + 2]).ShouldBe((esperadoR, esperadoG, esperadoB));
        }
    }

    [Fact]
    public void La_huella_de_la_firma_es_el_sha256_del_png()
    {
        var bytes = PngDePrueba.Crear(600, 200);
        DeclaracionPepReglas.HuellaDeLaFirma(Convert.ToBase64String(bytes)).ShouldBe(Convert.ToHexStringLower(SHA256.HashData(bytes)));
        DeclaracionPepReglas.HuellaDeLaFirma("???").ShouldBeNull();
    }

    // ---------- el sello y las versiones ----------

    private static (SeccionDeFicha Seccion, Func<JsonObject, ContextoDeSeccion, OrigenDeSeccion, IReadOnlyList<object>> Guardar) SeccionNatural()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        var estado = new SeccionDeFicha[] { seccion };
        IReadOnlyList<object> Guardar(JsonObject datos, ContextoDeSeccion contexto, OrigenDeSeccion origen)
        {
            var leidos = ReglasDeFicha.Leer(definicion, JsonDocument.Parse(datos.ToJsonString()).RootElement.Clone(), contexto);
            var eventos = estado[0].Registrar(definicion, leidos, [], contexto, new CatalogosEnMemoria([]), origen, "tercero", Ahora);
            estado[0] = estado[0].Proyectar(eventos);
            return eventos;
        }

        return (seccion, Guardar);
    }

    private static DeclaracionPep Declarada(IReadOnlyList<object> eventos) =>
        JsonDeFicha.Deserializar<DatosGenerales>(eventos.OfType<VersionDeSeccionRegistrada>().Single().Datos).DeclaracionPep!;

    [Fact]
    public void El_servidor_sella_la_firma_con_la_huella_la_fecha_y_la_ip_y_la_conserva_mientras_no_cambie()
    {
        var (_, guardar) = SeccionNatural();
        var primera = Declarada(guardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero));
        (primera.FechaFirma, primera.FirmaIp).ShouldBe((Hoy, "10.0.0.7"));
        primera.FirmaHuella.ShouldBe(DeclaracionPepReglas.HuellaDeLaFirma(FirmaBase64));

        // Lo que el cliente diga de los sellos se ignora.
        var manipulada = Declaracion();
        manipulada["fecha_firma"] = "2000-01-01";
        manipulada["firma_ip"] = "1.1.1.1";
        manipulada["firma_huella"] = "falsa";
        guardar(GeneralNatural(manipulada), Natural with { Hoy = Hoy.AddDays(1), IpDeFirma = "10.9.9.9" }, OrigenDeSeccion.Tercero).ShouldBeEmpty();

        // Otro día, otra IP y los mismos datos: no hay versión nueva y los sellos siguen siendo los primeros.
        var otroDia = Natural with { Hoy = Hoy.AddDays(3), IpDeFirma = "10.8.8.8" };
        guardar(GeneralNatural(Declaracion()), otroDia, OrigenDeSeccion.Tercero).ShouldBeEmpty();

        // Cambia un campo de la declaración: versión nueva con los sellos de ese día.
        var cambio = Declaracion();
        cambio["cargo"] = "Concejal";
        var segunda = Declarada(guardar(GeneralNatural(cambio), otroDia, OrigenDeSeccion.Tercero));
        (segunda.Cargo, segunda.FechaFirma, segunda.FirmaIp).ShouldBe(("Concejal", Hoy.AddDays(3), "10.8.8.8"));

        // Cambiar otra cosa de la sección (no de la declaración) conserva el sello de la declaración.
        var otraCosa = GeneralNatural(cambio);
        otraCosa["telefono"] = "300 555 1234";
        var tercera = Declarada(guardar(otraCosa, Natural with { Hoy = Hoy.AddDays(9), IpDeFirma = "10.7.7.7" }, OrigenDeSeccion.Tercero));
        (tercera.FechaFirma, tercera.FirmaIp).ShouldBe((Hoy.AddDays(3), "10.8.8.8"));
    }

    [Fact]
    public void Volver_a_si_tras_un_no_es_una_declaracion_nueva_con_sellos_del_dia()
    {
        var (_, guardar) = SeccionNatural();
        guardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero);

        // «No»: la declaración se conserva (inactiva) con sus sellos.
        var inactiva = Declarada(guardar(GeneralNatural(Declaracion(), pep: false), Natural with { Hoy = Hoy.AddDays(1) }, OrigenDeSeccion.Tercero));
        inactiva.FechaFirma.ShouldBe(Hoy);

        // «Sí» otra vez con el mismo contenido: se firma de nuevo (el juramento se marca otra vez) y los sellos son los de ese día.
        var reactivada = Declarada(guardar(GeneralNatural(Declaracion(), pep: true), Natural with { Hoy = Hoy.AddDays(5), IpDeFirma = "10.5.5.5" }, OrigenDeSeccion.Tercero));
        (reactivada.FechaFirma, reactivada.FirmaIp).ShouldBe((Hoy.AddDays(5), "10.5.5.5"));
    }

    [Fact]
    public void La_declaracion_importada_conserva_sus_sellos_y_puede_traer_solo_la_huella_de_la_firma()
    {
        var (_, guardar) = SeccionNatural();
        var d = Declaracion();
        d.Remove("firma_png");
        d["firma_huella"] = new string('a', 64);
        d["fecha_firma"] = "2026-10-05";
        d["firma_ip"] = "186.1.2.3";

        var eventos = guardar(GeneralNatural(d), NaturalSinExigir, OrigenDeSeccion.SistemaExterno);
        var guardada = Declarada(eventos);
        (guardada.FirmaHuella, guardada.FechaFirma, guardada.FirmaIp, guardada.FirmaPng).ShouldBe((new string('a', 64), new DateOnly(2026, 10, 5), "186.1.2.3", null));
        // Sin la imagen no hay faltante: la que importó el sistema de origen vale como firma.
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(d), NaturalSinExigir)).ShouldBeEmpty();
        // Pero el tercero, al guardar, debe firmar de nuevo.
        Rutas(Evaluar(CodigosDeSeccion.General, GeneralNatural(d), Natural)).ShouldBe(["declaracion_pep.firma_png"]);
    }


    private static readonly Lazy<ICatalogosDeFicha> CatalogosDeSemilla = new(() =>
    {
        var entradas = new List<EntradaDeCatalogo>();
        foreach (var (catalogo, filas) in SemillaDeCatalogos.Todas)
        {
            var niveles = new Dictionary<string, int>();
            foreach (var fila in filas.OrderBy(f => f.Nivel ?? 0))
            {
                var (entrada, _) = ReglasDeCatalogo.Validar(catalogo, fila, p => niveles.TryGetValue(p, out var n) ? n : null, Ahora);
                if (entrada!.Nivel is { } nivel)
                    niveles[entrada.Codigo] = nivel;
                entradas.Add(entrada);
            }
        }

        return new CatalogosEnMemoria(entradas);
    });

    [Fact]
    public void Declarar_o_cambiar_la_declaracion_no_pide_el_soporte_del_cambio_pero_otro_cambio_si()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));

        VersionDeSeccionRegistrada Guardar(JsonObject datos)
        {
            var leidos = ReglasDeFicha.Leer(definicion, JsonDocument.Parse(datos.ToJsonString()).RootElement.Clone(), Natural);
            var evento = seccion.Registrar(definicion, leidos, [], Natural, CatalogosDeSemilla.Value, OrigenDeSeccion.Tercero, "tercero", Ahora).ShouldHaveSingleItem()
                .ShouldBeOfType<VersionDeSeccionRegistrada>();
            seccion = seccion.Proyectar([evento]);
            return evento;
        }

        JsonObject Completa(bool pep, JsonObject? declaracion, string direccion = "Carrera 7 # 32-16")
        {
            var general = GeneralNatural(declaracion, pep, direccion);
            general["correo"] = "ana@correo.co";
            general["telefono"] = "3001234567";
            general["pais"] = "CO";
            general["ciudad"] = "11001";
            general["ciiu_principal"] = "4663";
            return general;
        }

        // La primera versión (sin PEP) queda completa; declarar PEP con su declaración también: la declaración es su propia evidencia.
        Guardar(Completa(false, null)).Completitud.Completa.ShouldBeTrue();
        var conDeclaracion = Guardar(Completa(true, Declaracion()));
        conDeclaracion.Completitud.Completa.ShouldBeTrue();
        conDeclaracion.Completitud.Faltantes.ShouldBeEmpty();

        // Cambiar el cargo, pasar a «No» y volver a «Sí» tampoco piden soporte.
        var cambio = Declaracion();
        cambio["cargo"] = "Concejal";
        Guardar(Completa(true, cambio)).Completitud.Completa.ShouldBeTrue();
        Guardar(Completa(false, cambio)).Completitud.Completa.ShouldBeTrue();
        Guardar(Completa(true, cambio)).Completitud.Completa.ShouldBeTrue();
        seccion.SinSoporte(seccion.VersionVigente!.Value).ShouldBeNull();

        // La vista previa de los mismos datos, otra vez, tampoco pide soporte (el servidor responde igual que al guardar).
        var previa = SeccionDeFicha.Anticipar(seccion, definicion, ReglasDeFicha.Leer(definicion, JsonDocument.Parse(Completa(true, cambio).ToJsonString()).RootElement.Clone(), Natural),
            0, Natural, CatalogosDeSemilla.Value);
        previa.Completa.ShouldBeTrue();

        // Cambiar otra cosa de la sección (la dirección) sí lo pide, y el aviso solo nombra lo que no es de PEP.
        var direccion = Guardar(Completa(true, cambio, "Calle 80 # 12-34"));
        direccion.Completitud.Faltantes.ShouldBe([new FaltanteDeSeccion(null, ReglasDeFicha.ReglaSoporte, ReglasDeFicha.MensajeSoporte)]);
        var sinSoporte = seccion.SinSoporte(direccion.Numero).ShouldNotBeNull();
        sinSoporte.Cambios.Select(c => c.Campo).ShouldBe(["direccion"]);
    }

    [Fact]
    public void La_huella_para_el_soporte_ignora_lo_de_pep_y_el_orden_de_las_claves_que_devuelve_la_base()
    {
        var uno = JsonDocument.Parse("""{ "nombres": "Ana", "pep": true, "declaracion_pep": { "cargo": "A" }, "personas": [ { "numero": "1", "pep": true, "declaracion_pep": { "cargo": "B" }, "rol": "Socio" } ] }""").RootElement;
        var otro = JsonDocument.Parse("""{ "personas": [ { "rol": "Socio", "numero": "1", "pep": false } ], "nombres": "Ana" }""").RootElement;
        SeccionDeFicha.HuellaParaSoporte(uno).ShouldBe(SeccionDeFicha.HuellaParaSoporte(otro));

        var distinto = JsonDocument.Parse("""{ "personas": [ { "rol": "Socio", "numero": "2" } ], "nombres": "Ana" }""").RootElement;
        SeccionDeFicha.HuellaParaSoporte(distinto).ShouldNotBe(SeccionDeFicha.HuellaParaSoporte(otro));
    }
    [Fact]
    public void Lo_que_cambia_en_la_declaracion_se_lee_en_la_comparacion_sin_la_imagen_de_la_firma()
    {
        var antes = ReglasDeFicha.Leer(CatalogoDeSecciones.Requerida(CodigosDeSeccion.General), JsonDocument.Parse(GeneralNatural(Declaracion()).ToJsonString()).RootElement.Clone(), Natural);
        var cambio = Declaracion();
        cambio["cargo"] = "Concejal";
        cambio["firma_png"] = Convert.ToBase64String(PngDePrueba.Crear(300, 100));
        var despues = ReglasDeFicha.Leer(CatalogoDeSecciones.Requerida(CodigosDeSeccion.General), JsonDocument.Parse(GeneralNatural(cambio).ToJsonString()).RootElement.Clone(), Natural);
        var esquema = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General).Esquema;
        // La huella de la firma sí se sella antes de comparar (como al guardar).
        var sellada = esquema.Sellar(despues, null, Natural, OrigenDeSeccion.Tercero);
        var base0 = esquema.Sellar(antes, null, Natural, OrigenDeSeccion.Tercero);

        var cambios = ComparacionDeSecciones.Comparar(esquema.Canonico(base0), esquema.Canonico(sellada));
        cambios.Select(c => c.Campo).ShouldBe(["declaracion_pep.cargo", "declaracion_pep.firma_huella"]);
        cambios[0].Etiqueta.ShouldBe("Declaración PEP · Cargo");
        cambios.ShouldNotContain(c => c.Campo.Contains("firma_png", StringComparison.Ordinal) || c.Campo.Contains("firma_ip", StringComparison.Ordinal));
    }
}
