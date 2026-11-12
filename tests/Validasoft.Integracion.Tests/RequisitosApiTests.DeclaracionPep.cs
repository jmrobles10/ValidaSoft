using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- declaración jurada PEP en la ficha (HU 49093, DP-1…DP-14) ----------

    /// <summary>Un PNG RGB de 8 bits de 60×20 con un trazo oscuro (el CRC no se comprueba): lo que el navegador envía, en pequeño.</summary>
    private static string FirmaPngBase64()
    {
        const int ancho = 60, alto = 20;
        var crudo = new List<byte>();
        for (var y = 0; y < alto; y++)
        {
            crudo.Add(0);
            for (var x = 0; x < ancho; x++)
                crudo.AddRange(y is >= 8 and <= 11 && x is >= 5 and <= 50 ? [20, 20, 90] : [255, 255, 255]);
        }

        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write([.. crudo]);

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        void Trozo(string tipo, byte[] datos)
        {
            png.Write([(byte)(datos.Length >> 24), (byte)(datos.Length >> 16), (byte)(datos.Length >> 8), (byte)datos.Length]);
            png.Write(Encoding.ASCII.GetBytes(tipo));
            png.Write(datos);
            png.Write([0, 0, 0, 0]);
        }

        Trozo("IHDR", [0, 0, 0, ancho, 0, 0, 0, alto, 8, 2, 0, 0, 0]);
        Trozo("IDAT", comprimido.ToArray());
        Trozo("IEND", []);
        return Convert.ToBase64String(png.ToArray());
    }

    private static string DeclaracionPepJson(string cargo = "Alcalde municipal", string? direccion = null) => $$"""
        {
          {{(direccion is null ? string.Empty : $"\"direccion\": \"{direccion}\",")}}
          "nacionalidad": "CO", "cargo": "{{cargo}}", "institucion": "Alcaldía de Sopó", "pais": "CO", "fecha_vinculacion": "2018-01-01",
          "vigente": false, "fecha_desvinculacion": "2022-01-01", "cuentas_exterior": true, "paises_cuentas_exterior": ["US", "PA"],
          "conyuge": { "nombre": "Luis Ruiz", "tipo_identificacion": "CC", "numero": "80111222" },
          "familiares": [ { "nombre": "Marta Pérez", "parentesco": "Hermana", "tipo_identificacion": "CC", "numero": "52111222" } ],
          "sociedades": [ { "nombre": "Inversiones SAS", "identificacion": "900111222", "calidad": "Socio" } ],
          "vinculo_con_pep": { "tiene": false }, "acepta_juramento": true, "firma_en_calidad": "NombrePropio", "firma_png": "{{FirmaPngBase64()}}"
        }
        """;

    private static string GeneralNaturalConPep(bool pep, string? declaracion) => $$"""
        { "nombres": "Ana", "apellidos": "Pérez", "correo": "ana@correo.co", "telefono": "3001234567", "pais": "CO", "ciudad": "11001",
          "direccion": "Carrera 7 # 32-16", "ciiu_principal": "4663", "pep": {{(pep ? "true" : "false")}}, "vinculo_pep": false,
          "fecha_expedicion": "2012-03-20", "lugar_expedicion": "Bogotá"{{(declaracion is null ? string.Empty : $", \"declaracion_pep\": {declaracion}")}} }
        """;

    private async Task<(Escenario E, VinculoDto Vinculo, SesionTerceroDto Sesion)> NaturalConSesionAsync()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(e.Norte), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "CC", "10" + Random.Shared.Next(10_000_000, 99_999_999)), "Natural", "Ana Pérez", null,
                [new ContactoDto("Ana Pérez", correo, null, "Titular")]),
            TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: correo, ReferenciaExterna: null), 201);
        return (e, vinculo, await SesionAsync(correo));
    }

    [Fact]
    public async Task La_natural_con_si_a_pep_no_guarda_sin_declaracion_y_con_ella_queda_sellada_y_la_empresa_descarga_el_pdf()
    {
        var (e, vinculo, sesion) = await NaturalConSesionAsync();
        var terceroId = vinculo.TerceroId;
        var rutaGeneral = $"{RutaFicha}/FICHA_GENERAL";

        // «Sí» sin declaración: 422 al guardar y también en la vista previa; no queda nada guardado.
        (await ErrorDeSeccionAsync(sesion, rutaGeneral, GeneralNaturalConPep(true, null))).ShouldBe(("declaracion-pep-requerida", "declaracion_pep"));
        var previa = await TerceroAsync("POST", $"{rutaGeneral}/evaluacion", sesion.AccessToken, new GuardarSeccionDto(Datos(GeneralNaturalConPep(true, null))), 422);
        (await CodigoAsync(previa)).ShouldBe("declaracion-pep-requerida");
        (await TerceroAsync<SeccionFichaDto>("GET", rutaGeneral, sesion.AccessToken, null, 200)).Version.ShouldBeNull();

        // Una declaración incompleta tampoco: el campo que falta va en su ruta.
        (await ErrorDeSeccionAsync(sesion, rutaGeneral, GeneralNaturalConPep(true, DeclaracionPepJson().Replace("\"cargo\": \"Alcalde municipal\", ", string.Empty))))
            .ShouldBe(("valor-invalido", "declaracion_pep.cargo"));

        // «Sí» con la declaración completa: se guarda y el servidor fija los sellos y los datos básicos.
        var guardada = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson(direccion: "Otra dirección que se ignora")));
        guardada.Creada.ShouldBeTrue();
        var declaracion = guardada.Seccion.Datos!.Value.GetProperty("declaracion_pep");
        declaracion.GetProperty("nombre").GetString().ShouldBe("Ana Pérez");
        declaracion.GetProperty("direccion").GetString().ShouldBe("Carrera 7 # 32-16");
        declaracion.GetProperty("relacion_empresa").GetString().ShouldBe("Proveedor");
        declaracion.GetProperty("firma_huella").GetString().ShouldNotBeNullOrEmpty();
        declaracion.GetProperty("fecha_firma").GetString().ShouldNotBeNullOrEmpty();
        declaracion.GetProperty("firma_ip").GetString().ShouldNotBeNullOrEmpty();
        var sello = (declaracion.GetProperty("firma_huella").GetString(), declaracion.GetProperty("fecha_firma").GetString());

        // Guardar otra vez lo mismo (el cliente devuelve lo que recibió): no hay versión nueva y el sello es el mismo.
        var igual = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", JsonSerializer.Serialize(guardada.Seccion.Datos));
        igual.Creada.ShouldBeFalse();
        igual.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("fecha_firma").GetString().ShouldBe(sello.Item2);

        // La empresa descarga el PDF de su proveedor: con la imagen de la firma, el cargo y el nombre; queda como exportación.
        var url = $"/api/v1/terceros/{terceroId}/declaraciones-pep/pdf?seccion=FICHA_GENERAL";
        var pdf = await DescargarAsync(url, Admin(e.Norte));
        pdf.Estado.ShouldBe(HttpStatusCode.OK);
        pdf.Tipo.ShouldBe("application/pdf");
        pdf.Nombre.ShouldNotBeNull().ShouldStartWith("declaracion-pep-ana-perez-");
        var texto = Encoding.Latin1.GetString(pdf.Contenido);
        texto.ShouldStartWith("%PDF-1.7");
        texto.ShouldContain("/Subtype /Image");
        texto.ShouldContain("(Alcalde municipal)");
        texto.ShouldContain("Ana P");
        texto.ShouldContain(sello.Item1!);

        // Sin vínculo con el tercero no existe para la empresa; la sección que no la lleva y una versión inexistente, tampoco.
        var sur = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(e.Norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null), 201);
        (await DescargarAsync(url, new Credencial(e.Norte.Admin, sur.Id))).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarAsync($"/api/v1/terceros/{terceroId}/declaraciones-pep/pdf?seccion=FICHA_SST", Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await DescargarAsync($"{url}&version=7", Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarAsync($"/api/v1/terceros/{Guid.NewGuid()}/declaraciones-pep/pdf?seccion=FICHA_GENERAL", Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Pasar_a_no_conserva_la_declaracion_inactiva_sin_pdf_y_volver_a_si_la_reactiva_y_un_cambio_es_una_version_nueva()
    {
        var (e, vinculo, sesion) = await NaturalConSesionAsync();
        var url = $"/api/v1/terceros/{vinculo.TerceroId}/declaraciones-pep/pdf?seccion=FICHA_GENERAL";
        var v1 = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson()));
        (await DescargarAsync(url, Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.OK);

        // «No»: la declaración se conserva en los datos, pero ya no se entrega (la empresa solo ve la activa).
        var declaracion = DeclaracionPepJson();
        var v2 = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(false, declaracion));
        v2.Seccion.Version.ShouldBe(v1.Seccion.Version + 1);
        v2.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("cargo").GetString().ShouldBe("Alcalde municipal");
        (await DescargarAsync(url, Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);

        // «Sí» otra vez: versión nueva y el PDF vuelve. La versión anterior, que la empresa no ve, no se entrega.
        var v3 = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson()));
        v3.Seccion.Version.ShouldBe(v2.Seccion.Version + 1);
        (await DescargarAsync(url, Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.OK);
        (await DescargarAsync($"{url}&version={v1.Seccion.Version}", Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);

        // Cambiar el cargo es una versión nueva con la declaración nueva.
        var v4 = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson(cargo: "Concejal")));
        v4.Seccion.Version.ShouldBe(v3.Seccion.Version + 1);
        var texto = Encoding.Latin1.GetString((await DescargarAsync(url, Admin(e.Norte))).Contenido);
        texto.ShouldContain("(Concejal)");
    }

    [Fact]
    public async Task En_la_juridica_cada_persona_pep_lleva_su_declaracion_el_pdf_es_por_persona_y_el_informe_dice_si_la_tiene()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, "9" + Random.Shared.Next(10_000_000, 99_999_999));
        var sesion = await SesionAsync(correo);
        const string Representante = """{ "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "1020304050", "nombres": "Marta Lucía", "apellidos": "Ríos Duarte", "pep": false, "vinculo_pep": false }""";
        string Socio(string? declaracion) => $$"""
            { "rol": "Socio", "tipo_identificacion": "CC", "numero": "79555111", "nombres": "Jorge Iván", "apellidos": "Castro Pinto", "porcentaje": 100,
              "pep": true, "vinculo_pep": false{{(declaracion is null ? string.Empty : $", \"declaracion_pep\": {declaracion}")}} }
            """;

        // La declaración es de la persona: sin ella el 422 señala su fila.
        var sinDeclaracion = $$"""{ "personas": [ {{Representante}}, {{Socio(null)}} ] }""";
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_LEGAL", sinDeclaracion)).ShouldBe(("declaracion-pep-requerida", "personas[1].declaracion_pep"));

        // La dirección de una persona de la legal no está en la fila: la escribe quien declara.
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_LEGAL", $$"""{ "personas": [ {{Representante}}, {{Socio(DeclaracionPepJson())}} ] }"""))
            .ShouldBe(("valor-invalido", "personas[1].declaracion_pep.direccion"));

        var guardada = await GuardarSeccionAsync(sesion, "FICHA_LEGAL", $$"""{ "personas": [ {{Representante}}, {{Socio(DeclaracionPepJson(direccion: "Calle 80 # 12-34"))}} ] }""");
        var declaracion = guardada.Seccion.Datos!.Value.GetProperty("personas")[1].GetProperty("declaracion_pep");
        declaracion.GetProperty("relacion_empresa").GetString().ShouldBe("Socio");
        declaracion.GetProperty("nombre").GetString().ShouldBe("Jorge Iván Castro Pinto");

        // PDF de la persona 1 (socio, PEP); la 0 no es PEP y no tiene declaración; sin índice no se sabe de quién.
        var terceroId = vinculo.TerceroId;
        var url = $"/api/v1/terceros/{terceroId}/declaraciones-pep/pdf?seccion=FICHA_LEGAL";
        var pdf = await DescargarAsync($"{url}&persona=1", Admin(e.Norte));
        pdf.Estado.ShouldBe(HttpStatusCode.OK);
        Encoding.Latin1.GetString(pdf.Contenido).ShouldContain("Calle 80 # 12-34");
        (await DescargarAsync($"{url}&persona=0", Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarAsync(url, Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);

        // El informe SAGRILAFT de la empresa muestra la persona PEP con su declaración y su posición.
        var personas = await ApiAsync<PersonasDelTerceroDto>("GET", $"/api/v1/informes/sagrilaft/terceros/{terceroId}/personas", Admin(e.Norte), null, 200);
        var pep = personas.Personas.Single(p => p.EsPep);
        (pep.DeclaracionPep, pep.Indice, pep.Rol).ShouldBe((true, 1, "Socio"));
        personas.Personas.Single(p => !p.EsPep).DeclaracionPep.ShouldBeFalse();
    }
}
