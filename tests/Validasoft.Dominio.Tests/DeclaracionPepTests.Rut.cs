using System.Text.Json;
using System.Text.Json.Nodes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// «Actualizar desde mi RUT» y la declaración PEP (HU 49093, DP-19). El RUT no trae datos de PEP (el formulario 001 no los tiene): no crea contactos con
/// «Sí» ni toca la respuesta. Lo que sí hace la HU al registrar por RUT es <b>conservar la respuesta PEP y la declaración guardadas</b> (solo se
/// reescriben si el usuario las cambió); aquí eso vale para la propuesta (parte de lo vigente y solo cambia lo que el documento trae) y para el
/// guardado: un cambio ajeno a la declaración, como la dirección que propone el RUT, no la vuelve a sellar.
/// </summary>
public partial class DeclaracionPepTests
{
    private static Dictionary<string, JsonElement> HechosDelDocumento(object campos) =>
        JsonSerializer.SerializeToElement(campos).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    private static JsonElement DatosDe(IReadOnlyList<object> eventos) => eventos.OfType<VersionDeSeccionRegistrada>().Single().Datos;

    private static JsonObject Objeto(JsonElement json) => JsonNode.Parse(json.GetRawText())!.AsObject();

    private static JsonObject Cambiado(JsonObject objeto, Action<JsonObject> cambio)
    {
        cambio(objeto);
        return objeto;
    }

    private static readonly ContextoDeSeccion OtroDia = Natural with { Hoy = new DateOnly(2026, 11, 20), IpDeFirma = "10.9.9.9" };

    [Fact]
    public void La_propuesta_del_RUT_no_toca_la_respuesta_pep_ni_la_declaracion_de_la_general()
    {
        var (_, guardar) = SeccionNatural();
        var vigente = DatosDe(guardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero));

        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, vigente, TipoPersona.Natural, null, "RUT",
            HechosDelDocumento(new { nit = "79123456", direccion = "CL 100 15 20", correo = "ANA@correo.co", telefono = "+57 300 555 1234" }));

        // Solo cambia lo que el RUT trae; ni la respuesta ni la declaración aparecen entre los cambios y los datos propuestos las llevan intactas.
        propuesta.Campos.Select(c => c.Campo).ShouldBe(["direccion", "correo", "telefono"]);
        propuesta.Campos.ShouldNotContain(c => c.Campo.Contains("pep", StringComparison.Ordinal));
        propuesta.Datos.GetProperty("pep").GetBoolean().ShouldBeTrue();
        propuesta.Datos.GetProperty("vinculo_pep").GetBoolean().ShouldBeFalse();
        propuesta.Datos.GetProperty("declaracion_pep").GetRawText().ShouldBe(vigente.GetProperty("declaracion_pep").GetRawText());
    }

    [Fact]
    public void Un_RUT_sin_cambios_para_una_persona_sin_pep_no_inventa_una_respuesta_ni_una_declaracion()
    {
        var (_, guardar) = SeccionNatural();
        var sinPep = DatosDe(guardar(GeneralNatural(null, pep: false), Natural, OrigenDeSeccion.Tercero));

        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, sinPep, TipoPersona.Natural, null, "RUT",
            HechosDelDocumento(new { nit = "79123456", direccion = "Carrera 7 # 32-16" }));

        propuesta.HayCambios.ShouldBeFalse();
        propuesta.Datos.GetProperty("pep").GetBoolean().ShouldBeFalse();
        propuesta.Datos.TryGetProperty("declaracion_pep", out _).ShouldBeFalse();
    }

    [Fact]
    public void La_camara_agrega_personas_sin_respuesta_pep_y_las_que_ya_estaban_conservan_la_suya_y_su_declaracion()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        var datos = Legal(Persona("Socio", Cambiado(Declaracion(), d => d["direccion"] = "Calle 80 # 12-34")));
        var leidos = ReglasDeFicha.Leer(definicion, JsonDocument.Parse(datos.ToJsonString()).RootElement.Clone(), Juridica);
        var vigente = DatosDe(seccion.Registrar(definicion, leidos, [], Juridica, new CatalogosEnMemoria([]), OrigenDeSeccion.Tercero, "tercero", Ahora));
        var declaradaAntes = vigente.GetProperty("personas")[0].GetProperty("declaracion_pep").GetRawText();

        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.Legal, vigente, TipoPersona.Juridica, null, "CAMARA_COMERCIO",
            HechosDelDocumento(new { representantesLegales = new[] { new { nombre = "ANA GOMEZ", identificacion = "52123456", cargo = "Gerente" } } }));

        propuesta.Campos.ShouldHaveSingleItem().Campo.ShouldBe("personas[1]");
        var personas = propuesta.Datos.GetProperty("personas");
        personas.GetArrayLength().ShouldBe(2);
        personas[0].GetProperty("pep").GetBoolean().ShouldBeTrue();
        personas[0].GetProperty("declaracion_pep").GetRawText().ShouldBe(declaradaAntes);
        // La persona nueva sale del documento sin respuesta a «¿Es PEP?»: no hay nada que declarar hasta que el tercero responda.
        personas[1].TryGetProperty("pep", out _).ShouldBeFalse();
        personas[1].TryGetProperty("declaracion_pep", out _).ShouldBeFalse();
        Rutas(Evaluar(CodigosDeSeccion.Legal, Objeto(propuesta.Datos), Juridica)).ShouldBeEmpty();
    }

    [Fact]
    public void Guardar_la_direccion_que_propone_el_RUT_no_vuelve_a_sellar_la_declaracion_firmada()
    {
        var (_, guardar) = SeccionNatural();
        var primera = guardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero);
        var firmada = Declarada(primera);

        var propuesta = PropuestaDesdeDocumento.Proponer(CodigosDeSeccion.General, DatosDe(primera), TipoPersona.Natural, null, "RUT",
            HechosDelDocumento(new { nit = "79123456", direccion = "CL 100 15 20" }));
        var segunda = guardar(Objeto(propuesta.Datos), OtroDia, OrigenDeSeccion.Tercero);

        // La general tiene la dirección nueva (versión nueva), pero la declaración sigue siendo la que se firmó: mismos datos básicos y mismos sellos.
        var general = JsonDeFicha.Deserializar<DatosGenerales>(DatosDe(segunda));
        general.Direccion.ShouldBe("CL 100 15 20");
        var despues = general.DeclaracionPep.ShouldNotBeNull();
        despues.Direccion.ShouldBe("Carrera 7 # 32-16");
        (despues.FechaFirma, despues.FirmaIp, despues.FirmaHuella, despues.FirmaPng).ShouldBe((Hoy, "10.0.0.7", firmada.FirmaHuella, firmada.FirmaPng));
        despues.ShouldBe(firmada);
    }

    [Fact]
    public void Declarar_de_nuevo_con_otro_contenido_o_con_otra_firma_si_actualiza_los_datos_basicos_y_los_sellos()
    {
        // Cambiar algo de la declaración: la declaración nueva lleva la dirección actual y los sellos del día.
        var (_, guardar) = SeccionNatural();
        guardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero);
        var cambio = Declaracion();
        cambio["cargo"] = "Concejal";
        var editada = Declarada(guardar(GeneralNatural(cambio, direccion: "CL 100 15 20"), OtroDia, OrigenDeSeccion.Tercero));
        (editada.Cargo, editada.Direccion, editada.FechaFirma, editada.FirmaIp).ShouldBe(("Concejal", "CL 100 15 20", OtroDia.Hoy, "10.9.9.9"));

        // Firmar otra vez (otra imagen) con el mismo contenido también vuelve a fijar los datos básicos y los sellos.
        var (_, otroGuardar) = SeccionNatural();
        otroGuardar(GeneralNatural(Declaracion()), Natural, OrigenDeSeccion.Tercero);
        var nuevaFirma = Declaracion();
        nuevaFirma["firma_png"] = Convert.ToBase64String(PngDePrueba.Crear(600, 200, tipoColor: 2));
        var firmadaDeNuevo = Declarada(otroGuardar(GeneralNatural(nuevaFirma, direccion: "CL 100 15 20"), OtroDia, OrigenDeSeccion.Tercero));
        (firmadaDeNuevo.Direccion, firmadaDeNuevo.FechaFirma, firmadaDeNuevo.FirmaIp).ShouldBe(("CL 100 15 20", OtroDia.Hoy, "10.9.9.9"));
    }

    [Fact]
    public void Corregir_el_nombre_de_una_persona_de_la_legal_tampoco_vuelve_a_sellar_su_declaracion()
    {
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal);
        var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(Guid.NewGuid(), definicion, null, Ahora));
        IReadOnlyList<object> Guardar(JsonObject datos, ContextoDeSeccion contexto)
        {
            var leidos = ReglasDeFicha.Leer(definicion, JsonDocument.Parse(datos.ToJsonString()).RootElement.Clone(), contexto);
            var eventos = seccion.Registrar(definicion, leidos, [], contexto, new CatalogosEnMemoria([]), OrigenDeSeccion.Tercero, "tercero", Ahora);
            seccion = seccion.Proyectar(eventos);
            return eventos;
        }

        var primera = Guardar(Legal(Persona("Socio", Cambiado(Declaracion(), d => d["direccion"] = "Calle 80 # 12-34"))), Juridica);
        var antes = JsonDeFicha.Deserializar<DatosLegales>(DatosDe(primera)).Personas![0].DeclaracionPep!;

        var corregida = Persona("Socio", Cambiado(Declaracion(), d => d["direccion"] = "Calle 80 # 12-34"));
        corregida["nombres"] = "Jorge Ivan";
        corregida["apellidos"] = "Castro Pinto";
        var segunda = Guardar(Legal(corregida), Juridica with { Hoy = new DateOnly(2026, 12, 1), IpDeFirma = "10.1.1.1" });

        var persona = JsonDeFicha.Deserializar<DatosLegales>(DatosDe(segunda)).Personas![0];
        persona.Nombres.ShouldBe("Jorge Ivan");
        (persona.DeclaracionPep!.Nombre, persona.DeclaracionPep.FechaFirma, persona.DeclaracionPep.FirmaIp).ShouldBe((antes.Nombre, antes.FechaFirma, antes.FirmaIp));
        persona.DeclaracionPep.Nombre.ShouldBe("Jorge Iván Castro Pinto"); // la que se firmó
        persona.DeclaracionPep.Direccion.ShouldBe("Calle 80 # 12-34");
    }
}
