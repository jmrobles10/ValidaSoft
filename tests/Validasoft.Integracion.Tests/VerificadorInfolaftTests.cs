using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Expedientes;
using Validasoft.Infraestructura.Verificaciones;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Adaptador de Infolaft listo para su respuesta real (CU-21), sin tocar al proveedor: un manejador HTTP de guion hace de
/// Infolaft. Lo único confirmado es la búsqueda por nombre; la búsqueda por documento, el formato de la respuesta y el PDF son
/// supuestos (docs/15) que cambian por configuración o por el lector tolerante, no por código.
/// </summary>
public sealed class VerificadorInfolaftTests
{
    private static readonly PersonaAConsultar Tercero = new("CO|NIT|900123456", "Tercero", "Aceros del Norte S.A.S.", "CO", "NIT", "900123456");
    private static readonly PersonaAConsultar Representante = new("CO|CC|1020304050", "RepresentanteLegal", "Ana Pérez", "CO", "CC", "1020304050");

    private sealed class ManejadorDeGuion(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<(HttpMethod Metodo, Uri Uri, AuthenticationHeaderValue? Autorizacion)> Peticiones { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage peticion, CancellationToken ct)
        {
            Peticiones.Add((peticion.Method, peticion.RequestUri!, peticion.Headers.Authorization));
            return Task.FromResult(responder(peticion));
        }
    }

    private sealed class FabricaDeClientes(HttpMessageHandler manejador) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(manejador, disposeHandler: false);
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode estado = HttpStatusCode.OK) =>
        new(estado) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static VerificadorInfolaft Verificador(ManejadorDeGuion manejador, Dictionary<string, string?>? configuracion = null) =>
        new(new FabricaDeClientes(manejador), NullLogger<VerificadorInfolaft>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(configuracion ?? []).Build());

    private static SolicitudDeListas Solicitud(ModoBusquedaListas modo, params PersonaAConsultar[] personas) =>
        new(Guid.NewGuid(), personas, new CredencialesEnClaro("sinco-norte", "clave-secreta", null, modo));

    private static string Pdf(string texto) => Convert.ToBase64String(Encoding.ASCII.GetBytes($"%PDF-1.4\n{texto}\n%%EOF"));

    [Fact]
    public async Task Busca_por_nombre_con_basic_y_los_parametros_que_confirma_el_srm()
    {
        var manejador = new ManejadorDeGuion(_ => Json("""{ "content": [] }"""));

        var respuesta = await Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero, Representante), CancellationToken.None);

        manejador.Peticiones.Count.ShouldBe(2);
        var primera = manejador.Peticiones[0];
        primera.Metodo.ShouldBe(HttpMethod.Post);
        primera.Uri.AbsoluteUri.ShouldBe("https://infolaftsearch.com/infolaft/Search/searchPaginated?accuracyLevel=EXACTA&forceSearch=true" +
                                         "&queryByName=Aceros%20del%20Norte%20S.A.S.&searchMode=SearchByName");
        primera.Autorizacion!.Scheme.ShouldBe("Basic");
        Encoding.UTF8.GetString(Convert.FromBase64String(primera.Autorizacion.Parameter!)).ShouldBe("sinco-norte:clave-secreta");
        respuesta.Coincidencias.ShouldBeEmpty();
        respuesta.Criterio.ShouldContain("por nombre");
        respuesta.RespuestaCruda!.ShouldContain("\"consultas\"");
        respuesta.PdfDelProveedor.ShouldBeNull();
    }

    [Fact]
    public async Task Busca_por_documento_cuando_la_empresa_lo_elige_y_la_forma_se_cambia_por_configuracion()
    {
        var manejador = new ManejadorDeGuion(_ => Json("""{ "content": [] }"""));

        var respuesta = await Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorDocumento, Tercero), CancellationToken.None);
        manejador.Peticiones.Single().Uri.Query.ShouldBe("?accuracyLevel=EXACTA&forceSearch=true&queryByDocument=900123456&searchMode=SearchByDocument");
        respuesta.Criterio.ShouldContain("número de documento");

        // Cuando Infolaft confirme otros nombres, es configuración (y puede pedir el tipo de documento).
        var otro = new ManejadorDeGuion(_ => Json("""{ "content": [] }"""));
        await Verificador(otro, new Dictionary<string, string?>
        {
            ["Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:RutaBusqueda"] = "v2/buscar",
            ["Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:ParametroDocumento"] = "numero",
            ["Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:ModoDocumento"] = "DOCUMENTO",
            ["Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:ParametroTipoDocumento"] = "tipo",
            ["Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:Exactitud"] = "ALTA"
        }).ConsultarAsync(Solicitud(ModoBusquedaListas.PorDocumento, Representante), CancellationToken.None);
        otro.Peticiones.Single().Uri.PathAndQuery.ShouldBe("/infolaft/v2/buscar?accuracyLevel=ALTA&forceSearch=true&numero=1020304050&tipo=CC&searchMode=DOCUMENTO");
    }

    [Fact]
    public async Task Lee_las_coincidencias_de_cada_persona_con_el_formato_paginado_habitual()
    {
        var manejador = new ManejadorDeGuion(peticion => peticion.RequestUri!.Query.Contains("Aceros")
            ? Json("""{ "content": [] }""")
            : Json("""{ "content": [ { "nombre": "ANA PEREZ", "lista": "OFAC (SDN)", "descripcion": "Posible homónimo", "score": "98.5%" } ] }"""));

        var respuesta = await Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero, Representante), CancellationToken.None);

        var coincidencia = respuesta.Coincidencias.ShouldHaveSingleItem();
        (coincidencia.ClavePersona, coincidencia.Lista, coincidencia.NombreEncontrado, coincidencia.Detalle, coincidencia.Puntaje)
            .ShouldBe((Representante.Clave, "OFAC (SDN)", "ANA PEREZ", "Posible homónimo", 98.5m));
    }

    [Fact]
    public async Task Usa_el_pdf_del_proveedor_si_la_consulta_cubre_a_una_sola_persona_y_lo_trae_en_base64()
    {
        var manejador = new ManejadorDeGuion(_ => Json($$"""{ "content": [], "pdfBase64": "{{Pdf("informe de Infolaft")}}" }"""));

        var sola = await Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorDocumento, Tercero), CancellationToken.None);
        sola.PdfDelProveedor.ShouldNotBeNull().Take(5).ShouldBe("%PDF-"u8.ToArray());

        // Con varias personas hay un PDF por búsqueda y no hay con qué unirlos: Validasoft genera el suyo.
        var varias = await Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorDocumento, Tercero, Representante), CancellationToken.None);
        varias.PdfDelProveedor.ShouldBeNull();
    }

    [Fact]
    public async Task Descarga_el_pdf_de_un_enlace_https_con_las_mismas_credenciales_y_si_no_es_pdf_lo_ignora()
    {
        var bueno = new ManejadorDeGuion(peticion => peticion.RequestUri!.AbsolutePath == "/informes/123.pdf"
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.7 informe")) }
            : Json("""{ "content": [], "data": { "reportUrl": "https://infolaftsearch.com/informes/123.pdf" } }"""));
        var conPdf = await Verificador(bueno).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero), CancellationToken.None);
        conPdf.PdfDelProveedor.ShouldNotBeNull().Take(5).ShouldBe("%PDF-"u8.ToArray());
        bueno.Peticiones[1].Metodo.ShouldBe(HttpMethod.Get);
        bueno.Peticiones[1].Autorizacion!.Scheme.ShouldBe("Basic");

        var malo = new ManejadorDeGuion(peticion => peticion.RequestUri!.AbsolutePath.EndsWith(".pdf", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>no es un pdf</html>") }
            : Json("""{ "content": [], "reportUrl": "https://infolaftsearch.com/informes/123.pdf" }"""));
        (await Verificador(malo).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero), CancellationToken.None)).PdfDelProveedor.ShouldBeNull();

        // Un enlace que no es https nunca se sigue.
        var http = new ManejadorDeGuion(_ => Json("""{ "content": [], "reportUrl": "http://infolaftsearch.com/informes/123.pdf" }"""));
        (await Verificador(http).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero), CancellationToken.None)).PdfDelProveedor.ShouldBeNull();
        http.Peticiones.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "credenciales")]
    [InlineData(HttpStatusCode.Forbidden, "credenciales")]
    [InlineData(HttpStatusCode.TooManyRequests, "límite")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP 500")]
    public async Task Los_errores_del_proveedor_se_dicen_en_claro_y_nunca_son_una_consulta_vacia(HttpStatusCode estado, string texto)
    {
        var manejador = new ManejadorDeGuion(_ => Json("{}", estado));

        var error = await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() =>
            Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero), CancellationToken.None));

        error.Message.ShouldContain(texto);
    }

    [Fact]
    public async Task Una_respuesta_que_no_es_json_no_cuenta_como_sin_coincidencias()
    {
        var manejador = new ManejadorDeGuion(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<html>mantenimiento</html>") });

        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() =>
            Verificador(manejador).ConsultarAsync(Solicitud(ModoBusquedaListas.PorNombre, Tercero), CancellationToken.None));
    }

    [Fact]
    public async Task Sin_credenciales_de_la_empresa_no_llama_a_nadie()
    {
        var manejador = new ManejadorDeGuion(_ => Json("{}"));

        await Should.ThrowAsync<ProveedorDeListasNoDisponibleException>(() =>
            Verificador(manejador).ConsultarAsync(new SolicitudDeListas(Guid.NewGuid(), [Tercero], null), CancellationToken.None));
        manejador.Peticiones.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "pdf": "%%" }""", false)]
    [InlineData("""{ "pdf": "SG9sYQ==" }""", false)]
    [InlineData("""[ { "pdf": "SG9sYQ==" } ]""", false)]
    public void El_lector_no_inventa_un_pdf_donde_no_lo_hay(string json, bool hay)
    {
        var (pdf, enlace) = LectorDeRespuestaDeListas.PdfDe(json);

        (pdf is not null).ShouldBe(hay);
        enlace.ShouldBeNull();
    }

    [Fact]
    public void El_lector_encuentra_el_pdf_en_la_raiz_o_un_nivel_adentro()
    {
        LectorDeRespuestaDeListas.PdfDe($$"""{ "report": "{{Pdf("raíz")}}" }""").Pdf.ShouldNotBeNull();
        LectorDeRespuestaDeListas.PdfDe($$"""{ "data": { "certificado": "{{Pdf("dentro")}}" } }""").Pdf.ShouldNotBeNull();
        LectorDeRespuestaDeListas.PdfDe("""{ "result": { "downloadUrl": "https://x.test/a.pdf" } }""").Enlace.ShouldBe("https://x.test/a.pdf");
    }
}
