using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Validasoft.Aplicacion.Ia;
using Validasoft.Dominio.Ia;
using Validasoft.Infraestructura.IA;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// El cliente de OpenAI sin red (ADR-0012): recorte de páginas con PdfPig, cuerpo de <c>/v1/responses</c>, lectura de la
/// respuesta (incluida una incompleta) y fallas HTTP. Nunca llama a OpenAI: el HTTP es un manejador falso.
/// </summary>
public sealed class AnalizadorOpenAiTests
{
    private const string Llave = "sk-prueba-unitaria-0123456789";

    private static byte[] PdfDePaginas(int paginas)
    {
        using var constructor = new PdfDocumentBuilder();
        var fuente = constructor.AddStandard14Font(Standard14Font.Helvetica);
        for (var i = 1; i <= paginas; i++)
            constructor.AddPage(PageSize.A4).AddText($"Pagina {i}", 12, new PdfPoint(40, 700), fuente);
        return constructor.Build();
    }

    private static SolicitudDeAnalisis Solicitud(byte[] contenido, string mime = "application/pdf", int? paginas = null, string tipo = "RUT") =>
        new(Guid.NewGuid(), 1, tipo, mime, "rut de Ana Gómez.pdf", contenido, paginas, "gpt-6-luna", LimitesDeAnalisis.Para(tipo));

    // ---------- recorte de páginas (IA-5) ----------

    [Fact]
    public void Un_pdf_de_seis_paginas_se_recorta_a_las_cuatro_del_rut_y_las_demas_quedan_omitidas()
    {
        var pdf = PdfDePaginas(6);

        var preparado = PreparacionDeArchivo.Preparar(pdf, "application/pdf", 6, LimitesDeAnalisis.Para("RUT"));

        preparado.PaginasEnviadas.ShouldBe(4);
        preparado.PaginasOmitidas.ShouldBe(2);
        using var recortado = PdfDocument.Open(preparado.Contenido);
        recortado.NumberOfPages.ShouldBe(4);
        recortado.GetPage(4).Text.ShouldContain("Pagina 4");

        // Dentro del límite se envía tal cual; sin conteo previo, PdfPig cuenta.
        var estados = PreparacionDeArchivo.Preparar(pdf, "application/pdf", 6, LimitesDeAnalisis.Para("ESTADOS_FINANCIEROS"));
        estados.Contenido.ShouldBeSameAs(pdf);
        (estados.PaginasEnviadas, estados.PaginasOmitidas).ShouldBe((6, 0));
        var cedula = PreparacionDeArchivo.Preparar(pdf, "application/pdf", null, LimitesDeAnalisis.Para("CEDULA"));
        (cedula.PaginasEnviadas, cedula.PaginasOmitidas).ShouldBe((2, 4));
    }

    [Fact]
    public void Una_imagen_va_entera_y_lo_que_supera_el_tamano_maximo_no_se_envia()
    {
        var imagen = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var preparada = PreparacionDeArchivo.Preparar(imagen, "image/png", null, LimitesDeAnalisis.Para("CEDULA"));
        preparada.Contenido.ShouldBeSameAs(imagen);
        preparada.PaginasEnviadas.ShouldBeNull();

        var limites = LimitesDeAnalisis.Para("CEDULA") with { TamanoMaximoBytes = 3 };
        Should.Throw<AnalisisDeIaException>(() => PreparacionDeArchivo.Preparar(imagen, "image/png", null, limites)).Motivo
            .ShouldBe("archivo-demasiado-grande");
        Should.Throw<AnalisisDeIaException>(() => PreparacionDeArchivo.Preparar([1, 2, 3], "application/pdf", 9, LimitesDeAnalisis.Para("RUT")))
            .Motivo.ShouldBe("pdf-no-procesable");
    }

    // ---------- cuerpo de la solicitud ----------

    [Fact]
    public void El_cuerpo_lleva_el_pdf_como_archivo_el_esquema_estricto_y_no_guarda_la_conversacion()
    {
        var pdf = PdfDePaginas(1);
        var cuerpo = SolicitudOpenAi.Cuerpo(Solicitud(pdf), EsquemasDeAnalisis.Para("RUT")!, pdf);

        cuerpo["model"]!.GetValue<string>().ShouldBe("gpt-6-luna");
        cuerpo["store"]!.GetValue<bool>().ShouldBeFalse();
        cuerpo["reasoning"]!["effort"]!.GetValue<string>().ShouldBe("low");
        cuerpo["max_output_tokens"]!.GetValue<int>().ShouldBe(2000);
        var contenido = cuerpo["input"]![0]!["content"]!.AsArray();
        contenido[0]!["type"]!.GetValue<string>().ShouldBe("input_file");
        contenido[0]!["filename"]!.GetValue<string>().ShouldBe("rut-v1.pdf"); // nunca el nombre original del tercero
        contenido[0]!["file_data"]!.GetValue<string>().ShouldBe("data:application/pdf;base64," + Convert.ToBase64String(pdf));
        contenido[1]!["type"]!.GetValue<string>().ShouldBe("input_text");
        contenido[1]!["text"]!.GetValue<string>().ShouldContain("Registro Único Tributario");
        var formato = cuerpo["text"]!["format"]!;
        formato["type"]!.GetValue<string>().ShouldBe("json_schema");
        formato["name"]!.GetValue<string>().ShouldBe("hechos_rut");
        formato["strict"]!.GetValue<bool>().ShouldBeTrue();
        formato["schema"]!["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();

        var imagen = SolicitudOpenAi.Cuerpo(Solicitud([1, 2, 3], "image/jpeg", tipo: "CEDULA"), EsquemasDeAnalisis.Para("CEDULA")!, [1, 2, 3]);
        var parte = imagen["input"]![0]!["content"]![0]!;
        parte["type"]!.GetValue<string>().ShouldBe("input_image");
        parte["image_url"]!.GetValue<string>().ShouldBe("data:image/jpeg;base64,AQID");
        parte["detail"]!.GetValue<string>().ShouldBe("high");
    }

    // ---------- respuesta ----------

    [Fact]
    public void Lee_el_texto_de_salida_y_el_uso_de_tokens_de_una_respuesta_completa()
    {
        var (hechos, uso, modelo) = RespuestaDeOpenAi.Interpretar(Respuesta(), EsquemasDeAnalisis.Para("RUT")!);

        modelo.ShouldBe("gpt-6-luna-2026-10-01");
        hechos.Confianza.ShouldBe(0.91m);
        hechos.Campos["razonSocial"].GetString().ShouldBe("Aceros del Norte S.A.S.");
        uso.ShouldBe(new UsoDeTokens(1834, 1024, 412));
    }

    [Fact]
    public void Una_respuesta_incompleta_rechazada_sin_texto_o_que_no_es_json_falla_cerrada()
    {
        var rut = EsquemasDeAnalisis.Para("RUT")!;
        var incompleta = JsonNode.Parse(Respuesta())!;
        incompleta["status"] = "incomplete";
        incompleta["incomplete_details"] = new JsonObject { ["reason"] = "max_output_tokens" };
        var excepcion = Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(incompleta.ToJsonString(), rut));
        excepcion.Motivo.ShouldBe("respuesta-incompleta");
        excepcion.Message.ShouldContain("max_output_tokens");

        var rechazada = JsonNode.Parse(Respuesta())!;
        rechazada["output"]![1]!["content"] = new JsonArray(new JsonObject { ["type"] = "refusal", ["refusal"] = "No puedo." });
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(rechazada.ToJsonString(), rut)).Motivo.ShouldBe("respuesta-rechazada");

        var sinTexto = JsonNode.Parse(Respuesta())!;
        sinTexto["output"] = new JsonArray();
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(sinTexto.ToJsonString(), rut)).Motivo.ShouldBe("respuesta-sin-texto");

        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar("<html>", rut)).Motivo.ShouldBe("respuesta-invalida");

        var fueraDeEsquema = JsonNode.Parse(Respuesta())!;
        fueraDeEsquema["output"]![1]!["content"]![0]!["text"] = "{\"tipoDetectado\":\"RUT\"}";
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(fueraDeEsquema.ToJsonString(), rut)).Motivo.ShouldBe("fuera-de-esquema");
    }

    [Fact]
    public void Una_respuesta_que_llega_pero_no_sirve_trae_el_uso_de_tokens_que_OpenAI_facturo()
    {
        var rut = EsquemasDeAnalisis.Para("RUT")!;
        var facturado = new UsoDeTokens(1834, 1024, 412); // el de Respuesta()

        var incompleta = JsonNode.Parse(Respuesta())!;
        incompleta["status"] = "incomplete";
        incompleta["incomplete_details"] = new JsonObject { ["reason"] = "max_output_tokens" };
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(incompleta.ToJsonString(), rut)).Uso.ShouldBe(facturado);

        var rechazada = JsonNode.Parse(Respuesta())!;
        rechazada["output"]![1]!["content"] = new JsonArray(new JsonObject { ["type"] = "refusal", ["refusal"] = "No puedo." });
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(rechazada.ToJsonString(), rut)).Uso.ShouldBe(facturado);

        var sinTexto = JsonNode.Parse(Respuesta())!;
        sinTexto["output"] = new JsonArray();
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(sinTexto.ToJsonString(), rut)).Uso.ShouldBe(facturado);

        var fueraDeEsquema = JsonNode.Parse(Respuesta())!;
        fueraDeEsquema["output"]![1]!["content"]![0]!["text"] = "{\"tipoDetectado\":\"RUT\"}";
        var fuera = Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(fueraDeEsquema.ToJsonString(), rut));
        (fuera.Motivo, fuera.Uso).ShouldBe(("fuera-de-esquema", facturado));

        // Sin respuesta (no es JSON) o sin tokens reportados no hay nada que registrar.
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar("<html>", rut)).Uso.ShouldBeNull();
        var sinUso = JsonNode.Parse(incompleta.ToJsonString())!;
        sinUso.AsObject().Remove("usage");
        Should.Throw<AnalisisDeIaException>(() => RespuestaDeOpenAi.Interpretar(sinUso.ToJsonString(), rut)).Uso.ShouldBeNull();
    }

    // ---------- cliente HTTP ----------

    [Fact]
    public async Task Envia_la_llave_a_la_url_configurada_y_devuelve_hechos_tokens_y_paginas()
    {
        var http = new HttpFalso((_, _) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respuesta(), Encoding.UTF8, "application/json") });
        var analizador = Analizador(http);

        var resultado = await analizador.AnalizarAsync(Solicitud(PdfDePaginas(6), paginas: 6), Llave, CancellationToken.None);

        var (peticion, cuerpo) = http.Peticiones.ShouldHaveSingleItem();
        peticion.RequestUri!.ToString().ShouldBe("http://openai.prueba/v1/responses");
        peticion.Headers.Authorization!.ToString().ShouldBe("Bearer " + Llave);
        JsonNode.Parse(cuerpo)!["text"]!["format"]!["name"]!.GetValue<string>().ShouldBe("hechos_rut");
        resultado.PaginasEnviadas.ShouldBe(4);
        resultado.PaginasOmitidas.ShouldBe(2);
        resultado.Uso.Entrada.ShouldBe(1834);
        resultado.Hechos.TipoDetectado.ShouldBe("RUT");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "llave-invalida")]
    [InlineData(HttpStatusCode.TooManyRequests, "limite-del-proveedor")]
    [InlineData(HttpStatusCode.InternalServerError, "http-500")]
    public async Task Un_error_http_es_una_falla_controlada(HttpStatusCode codigo, string motivo)
    {
        var analizador = Analizador(new HttpFalso((_, _) => new HttpResponseMessage(codigo) { Content = new StringContent("{\"error\":{}}") }));

        (await Should.ThrowAsync<AnalisisDeIaException>(() => analizador.AnalizarAsync(Solicitud(PdfDePaginas(1), paginas: 1), Llave, CancellationToken.None)))
            .Motivo.ShouldBe(motivo);
    }

    [Fact]
    public async Task El_tiempo_agotado_y_la_conexion_caida_son_fallas_controladas()
    {
        var lento = Analizador(new HttpFalso((_, _) => throw new TaskCanceledException("tiempo")));
        (await Should.ThrowAsync<AnalisisDeIaException>(() => lento.AnalizarAsync(Solicitud(PdfDePaginas(1), paginas: 1), Llave, CancellationToken.None)))
            .Motivo.ShouldBe("tiempo-agotado");

        var caido = Analizador(new HttpFalso((_, _) => throw new HttpRequestException("sin red")));
        (await Should.ThrowAsync<AnalisisDeIaException>(() => caido.AnalizarAsync(Solicitud(PdfDePaginas(1), paginas: 1), Llave, CancellationToken.None)))
            .Motivo.ShouldBe("error-de-conexion");
    }

    private static AnalizadorOpenAi Analizador(HttpFalso http) =>
        new(http, new OpcionesDeIa(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validasoft:IA:UrlBase"] = "http://openai.prueba/v1"
        }).Build()), NullLogger<AnalizadorOpenAi>.Instance);

    /// <summary>Respuesta de ejemplo de la API Responses (con un bloque de razonamiento antes del mensaje).</summary>
    private static string Respuesta()
    {
        var hechos = new JsonObject
        {
            ["tipoDetectado"] = "RUT",
            ["legible"] = true,
            ["confianza"] = 0.91,
            ["alertasDeManipulacion"] = new JsonArray(),
            ["observaciones"] = null,
            ["campos"] = new JsonObject
            {
                ["nit"] = "900123456", ["dv"] = "7", ["razonSocial"] = "Aceros del Norte S.A.S.", ["tipoContribuyente"] = "Persona jurídica",
                ["representanteLegalNombre"] = "Ana Gómez", ["representanteLegalIdentificacion"] = "52123456", ["direccion"] = "Calle 1 # 2-3",
                ["municipio"] = "Bogotá", ["codigoMunicipio"] = "11001", ["correo"] = "contabilidad@aceros.co", ["telefono"] = "6015550101",
                ["fechaExpedicion"] = "2026-10-01", ["actividadEconomicaPrincipal"] = "2410", ["actividadesSecundarias"] = new JsonArray("4663"),
                ["responsabilidades"] = new JsonArray("05", "48")
            }
        };
        return new JsonObject
        {
            ["id"] = "resp_123",
            ["object"] = "response",
            ["status"] = "completed",
            ["model"] = "gpt-6-luna-2026-10-01",
            ["output"] = new JsonArray(
                new JsonObject { ["type"] = "reasoning", ["id"] = "rs_1", ["summary"] = new JsonArray() },
                new JsonObject
                {
                    ["type"] = "message",
                    ["role"] = "assistant",
                    ["content"] = new JsonArray(new JsonObject { ["type"] = "output_text", ["text"] = hechos.ToJsonString(), ["annotations"] = new JsonArray() })
                }),
            ["usage"] = new JsonObject
            {
                ["input_tokens"] = 1834,
                ["input_tokens_details"] = new JsonObject { ["cached_tokens"] = 1024 },
                ["output_tokens"] = 412,
                ["output_tokens_details"] = new JsonObject { ["reasoning_tokens"] = 128 },
                ["total_tokens"] = 2246
            }
        }.ToJsonString();
    }

    /// <summary>Fábrica de clientes con un manejador que responde lo indicado y guarda las peticiones (sin red).</summary>
    private sealed class HttpFalso(Func<HttpRequestMessage, string, HttpResponseMessage> responder) : IHttpClientFactory
    {
        public List<(HttpRequestMessage Peticion, string Cuerpo)> Peticiones { get; } = [];

        public Func<HttpRequestMessage, string, HttpResponseMessage> Responder { get; } = responder;

        public HttpClient CreateClient(string name) => new(new Manejador(this));

        private sealed class Manejador(HttpFalso duenio) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var cuerpo = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
                duenio.Peticiones.Add((request, cuerpo));
                return duenio.Responder(request, cuerpo);
            }
        }
    }
}
