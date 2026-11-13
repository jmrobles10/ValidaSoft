using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Ficha;
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

        // La vista previa de lo mismo no pide un documento de soporte: declarar PEP no es «cambiar» (DP-17).
        guardada.Completitud.Completa.ShouldBeTrue();
        (await TerceroAsync<CompletitudSeccionDto>("POST", $"{rutaGeneral}/evaluacion", sesion.AccessToken,
            new GuardarSeccionDto(Datos(JsonSerializer.Serialize(guardada.Seccion.Datos))), 200)).Completa.ShouldBeTrue();

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
        v2.Completitud.Completa.ShouldBeTrue(); // pasar a «No» y volver a «Sí» tampoco pide soporte (DP-17)
        v2.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("cargo").GetString().ShouldBe("Alcalde municipal");
        (await DescargarAsync(url, Admin(e.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);

        // «Sí» otra vez: versión nueva y el PDF vuelve. La versión anterior, que la empresa no ve, no se entrega.
        var v3 = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson()));
        v3.Seccion.Version.ShouldBe(v2.Seccion.Version + 1);
        v3.Completitud.Completa.ShouldBeTrue();
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

        // El propio tercero baja el PDF de cada persona suya con «Sí» y declaración (por su posición en la legal), y nada más.
        var urlDelTercero = string.Format(RutaPdfDelTercero, "FICHA_LEGAL");
        var propio = await DescargarComoTerceroAsync($"{urlDelTercero}?persona=1", sesion.AccessToken);
        propio.Estado.ShouldBe(HttpStatusCode.OK);
        var propioTexto = Encoding.Latin1.GetString(propio.Contenido);
        propioTexto.ShouldContain("Calle 80 # 12-34");
        propioTexto.ShouldContain("Socio o accionista de ");
        (await DescargarComoTerceroAsync($"{urlDelTercero}?persona=0", sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync($"{urlDelTercero}?persona=7", sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync($"{urlDelTercero}?persona=-1", sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync(urlDelTercero, sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync(string.Format(RutaPdfDelTercero, "FICHA_GENERAL"), sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await AccesosADeclaracionesAsync(terceroId)).ShouldHaveSingleItem().Persona.ShouldBe(1);

        // El informe SAGRILAFT de la empresa muestra la persona PEP con su declaración y su posición.
        var personas = await ApiAsync<PersonasDelTerceroDto>("GET", $"/api/v1/informes/sagrilaft/terceros/{terceroId}/personas", Admin(e.Norte), null, 200);
        var pep = personas.Personas.Single(p => p.EsPep);
        (pep.DeclaracionPep, pep.Indice, pep.Rol).ShouldBe((true, 1, "Socio"));
        personas.Personas.Single(p => !p.EsPep).DeclaracionPep.ShouldBeFalse();
    }

    // ---------- el tercero descarga su declaración y el logo del PDF (DP-16, DP-18) ----------

    private const string RutaPdfDelTercero = "/public/v1/ficha/{0}/declaracion-pep/pdf";

    /// <summary>GET con la sesión del tercero (sin sesión si el token es nulo): el PDF o el error.</summary>
    private async Task<Descarga> DescargarComoTerceroAsync(string url, string? token)
    {
        using var cliente = Host.Server.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        if (token is not null)
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var respuesta = await cliente.SendAsync(pedido);
        return new Descarga(respuesta.StatusCode, respuesta.Content.Headers.ContentType?.MediaType,
            respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName,
            await respuesta.Content.ReadAsByteArrayAsync());
    }

    private async Task<List<AccesoADeclaracionPep>> AccesosADeclaracionesAsync(Guid terceroId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        return [.. (await consulta.Query<AccesoADeclaracionPep>().Where(a => a.TerceroId == terceroId).ToListAsync()).OrderBy(a => a.Fecha)];
    }

    [Fact]
    public async Task El_tercero_descarga_su_propia_declaracion_solo_con_si_a_pep_y_declaracion_y_cada_descarga_queda_registrada()
    {
        var (_, vinculo, sesion) = await NaturalConSesionAsync();
        var url = string.Format(RutaPdfDelTercero, "FICHA_GENERAL");

        // Sin sesión no hay PDF; con sesión pero sin declaración, tampoco.
        (await DescargarComoTerceroAsync(url, null)).Estado.ShouldBe(HttpStatusCode.Unauthorized);
        (await DescargarComoTerceroAsync(url, sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        var sin = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(false, null));
        (await DescargarComoTerceroAsync(url, sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);

        // Con «Sí» y la declaración guardada: el PDF de la versión vigente, a nombre de Validasoft y con su logo (no está dirigido a una empresa).
        var guardada = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson()));
        guardada.Seccion.Version.ShouldBe(sin.Seccion.Version + 1);
        var pdf = await DescargarComoTerceroAsync(url, sesion.AccessToken);
        pdf.Estado.ShouldBe(HttpStatusCode.OK);
        pdf.Tipo.ShouldBe("application/pdf");
        pdf.Nombre.ShouldNotBeNull().ShouldStartWith("declaracion-pep-ana-perez-");
        var texto = Encoding.Latin1.GetString(pdf.Contenido);
        texto.ShouldStartWith("%PDF-1.7");
        texto.ShouldContain("(Validasoft)");
        texto.ShouldContain("(alida) Tj");            // el logotipo de Validasoft: la «V» verde y «alida» «Soft»
        texto.ShouldContain("0.133333 0.827451 0.541176 rg");
        texto.ShouldContain("/Subtype /Image");       // la firma
        texto.ShouldContain("(Alcalde municipal)");
        texto.ShouldContain(guardada.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("firma_huella").GetString()!);

        // Cada descarga queda registrada: quién, cuándo, desde dónde, de qué versión y la huella del archivo.
        (await DescargarComoTerceroAsync(url, sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.OK);
        var accesos = await AccesosADeclaracionesAsync(vinculo.TerceroId);
        accesos.Count.ShouldBe(2);
        accesos.ShouldAllBe(a => a.Seccion == "FICHA_GENERAL" && a.Persona == null && a.Version == guardada.Seccion.Version && a.ActorTipo == "Tercero" && a.Sha256.Length == 64 && a.Fecha > DateTimeOffset.UtcNow.AddMinutes(-5));

        // Lo que no existe: otra sección con declaración, una sección que no la lleva, una persona de la legal (la natural no la diligencia).
        (await DescargarComoTerceroAsync(string.Format(RutaPdfDelTercero, "FICHA_LEGAL") + "?persona=0", sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync(string.Format(RutaPdfDelTercero, "FICHA_SST"), sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await DescargarComoTerceroAsync(string.Format(RutaPdfDelTercero, "FICHA_INEXISTENTE"), sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);

        // «No» la deja inactiva y con ella desaparece la descarga (como para la empresa).
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(false, DeclaracionPepJson()));
        (await DescargarComoTerceroAsync(url, sesion.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await AccesosADeclaracionesAsync(vinculo.TerceroId)).Count.ShouldBe(2); // lo que no se entregó no se registra
    }

    [Fact]
    public async Task Cada_tercero_solo_baja_su_propia_declaracion_y_el_pdf_sale_de_sus_datos_no_de_lo_que_pida_el_cliente()
    {
        var (_, _, sesionA) = await NaturalConSesionAsync();
        var (_, vinculoB, sesionB) = await NaturalConSesionAsync();
        var url = string.Format(RutaPdfDelTercero, "FICHA_GENERAL");

        var a = await GuardarSeccionAsync(sesionA, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson(cargo: "Alcalde de A")));
        // B no tiene declaración: A tiene una y B no puede obtenerla (la sesión fija de quién es), ni con índices ni con versiones de por medio.
        (await DescargarComoTerceroAsync(url, sesionB.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);
        (await DescargarComoTerceroAsync(url + "?persona=0&version=1&terceroId=" + Guid.NewGuid(), sesionB.AccessToken)).Estado.ShouldBe(HttpStatusCode.NotFound);

        var b = await GuardarSeccionAsync(sesionB, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson(cargo: "Concejal de B")));
        var pdfB = Encoding.Latin1.GetString((await DescargarComoTerceroAsync(url, sesionB.AccessToken)).Contenido);
        pdfB.ShouldContain("(Concejal de B)");
        pdfB.ShouldNotContain("Alcalde de A");
        var numeroA = a.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("numero").GetString()!;
        pdfB.ShouldNotContain(numeroA);
        pdfB.ShouldContain(b.Seccion.Datos!.Value.GetProperty("declaracion_pep").GetProperty("numero").GetString()!);
        (await AccesosADeclaracionesAsync(vinculoB.TerceroId)).ShouldHaveSingleItem().TerceroId.ShouldBe(vinculoB.TerceroId);
    }

    /// <summary>Un PNG RGB de 8 bits de 80×24, naranja: el logo de una empresa (distinto de la firma, para contarlos por separado).</summary>
    private static string LogoPngBase64()
    {
        const int ancho = 80, alto = 24;
        var crudo = new List<byte>();
        for (var y = 0; y < alto; y++)
        {
            crudo.Add(0);
            for (var x = 0; x < ancho; x++)
                crudo.AddRange(x < 24 ? [255, 255, 255] : [224, 122, 31]);
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

    [Fact]
    public async Task El_pdf_de_la_empresa_lleva_su_logo_png_o_svg_y_sin_logo_o_con_uno_que_no_se_puede_dibujar_el_de_Validasoft()
    {
        var (e, vinculo, sesion) = await NaturalConSesionAsync();
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralNaturalConPep(true, DeclaracionPepJson()));
        var url = $"/api/v1/terceros/{vinculo.TerceroId}/declaraciones-pep/pdf?seccion=FICHA_GENERAL";
        async Task<string> PdfAsync() => Encoding.Latin1.GetString((await DescargarAsync(url, Admin(e.Norte))).Contenido);
        async Task FijarLogoAsync(string logo) =>
            await ApiAsync("PUT", $"/api/v1/empresas/{e.Norte.Datos.Id}", Admin(e.Norte), new ActualizarEmpresaDto(LogoUrl: logo), 200);

        // Sin logo configurado: el logotipo de Validasoft (vectores: la «V» verde y «alida» «Soft») y la firma como única imagen.
        var sinLogo = await PdfAsync();
        sinLogo.ShouldContain("(alida) Tj");
        sinLogo.Split("/Subtype /Image").Length.ShouldBe(2);

        // Un logo PNG: su imagen (80×24) en el encabezado, además de la firma, y ya no el de Validasoft.
        await FijarLogoAsync($"data:image/png;base64,{LogoPngBase64()}");
        var conPng = await PdfAsync();
        conPng.ShouldContain("/Width 80 /Height 24");
        conPng.Split("/Subtype /Image").Length.ShouldBe(3);
        conPng.ShouldNotContain("(alida) Tj");

        // Un logo SVG: vectores (sin imagen nueva), con el color del logo.
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 30"><rect width="100" height="30" rx="5" fill="#E07A1F"/><circle cx="15" cy="15" r="8" fill="#FFFFFF"/></svg>""";
        await FijarLogoAsync($"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}");
        var conSvg = await PdfAsync();
        conSvg.ShouldContain("0.878431 0.478431 0.121569 rg");
        conSvg.Split("/Subtype /Image").Length.ShouldBe(2);
        conSvg.ShouldNotContain("(alida) Tj");

        // Un SVG con texto no se puede dibujar fielmente: el PDF lleva el de Validasoft en vez de un logo a medias. Y por URL (no se descarga) también.
        await FijarLogoAsync($"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10"><text>Norte</text></svg>"""))}");
        (await PdfAsync()).ShouldContain("(alida) Tj");
        await FijarLogoAsync("https://cdn.norte.example/logo.png");
        (await PdfAsync()).ShouldContain("(alida) Tj");

        // Quitar el logo (cadena vacía) devuelve al de Validasoft.
        await FijarLogoAsync(string.Empty);
        (await PdfAsync()).ShouldContain("(alida) Tj");
    }
}
