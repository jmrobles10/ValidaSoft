using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Validasoft.Contratos;
using Validasoft.Sdk;

namespace Validasoft.Sdk.Tests;

/// <summary>Cliente HTTP del SDK contra un servidor falso: cabeceras, idempotencia, reintentos, 304, errores y paginación.</summary>
public sealed class ClienteValidasoftTests
{
    private static readonly Guid Empresa = Guid.Parse("0195f1c2-7d3e-7a00-8000-0000000000e1");
    private const string Llave = "vsk_test_acme_" + "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>Responde en orden las respuestas encoladas y guarda las peticiones (con su cuerpo leído).</summary>
    private sealed class ServidorFalso : HttpMessageHandler
    {
        public ConcurrentQueue<Func<HttpRequestMessage, HttpResponseMessage>> Respuestas { get; } = new();

        public List<(HttpRequestMessage Peticion, string? Cuerpo)> Recibidas { get; } = [];

        public void Responder(HttpStatusCode codigo, object? cuerpo = null, Action<HttpResponseMessage>? ajustar = null) =>
            Respuestas.Enqueue(_ =>
            {
                var respuesta = new HttpResponseMessage(codigo);
                if (cuerpo is not null)
                    respuesta.Content = new StringContent(cuerpo as string ?? JsonSerializer.Serialize(cuerpo, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                        Encoding.UTF8, "application/json");
                ajustar?.Invoke(respuesta);
                return respuesta;
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Recibidas.Add((request, request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return Respuestas.TryDequeue(out var siguiente) ? siguiente(request) : new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }

    private static (ClienteValidasoft Cliente, ServidorFalso Servidor) Crear(FirmanteDeAsercionesDelegadas? firmante = null, string urlBase = "https://valida.acme.co/")
    {
        var servidor = new ServidorFalso();
        var cliente = new ClienteValidasoft(new HttpClient(servidor), new OpcionesValidasoft
        {
            UrlBase = new Uri(urlBase),
            ApiKey = Llave,
            EmpresaId = Empresa,
            Firmante = firmante,
            EsperaEntreReintentos = TimeSpan.FromMilliseconds(1)
        });
        return (cliente, servidor);
    }

    private static string? Cabecera(HttpRequestMessage peticion, string nombre) =>
        peticion.Headers.TryGetValues(nombre, out var valores) ? valores.Single() : null;

    private static ConsultaCertificacionDto Consulta(bool bloqueado = false) =>
        new(true, "N1-C-2026-000001-1", "Vigente", "Activo", Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2027, 4, 30), [], [], bloqueado,
            bloqueado ? "Bloqueo" : null, new CuentaDesignadaResumenDto("****6578", "Bancolombia"), "https://valida.acme.co/verificar/N1", null);

    [Fact]
    public async Task Envia_la_api_key_la_empresa_y_una_clave_de_idempotencia_en_cada_post()
    {
        var (cliente, servidor) = Crear();
        servidor.Responder(HttpStatusCode.Created, new { id = Guid.NewGuid(), correoDestino = "a@b.co", expira = DateTimeOffset.UtcNow, envios = 1 });
        servidor.Responder(HttpStatusCode.OK, Consulta());

        await cliente.Invitaciones.EnviarAsync(Guid.NewGuid(), new InvitacionDto("proveedor@aceros.co"));
        await cliente.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", "900.123.456-7", "proveedor"));

        var (post, cuerpo) = servidor.Recibidas[0];
        post.Method.ShouldBe(HttpMethod.Post);
        Cabecera(post, "X-Api-Key").ShouldBe(Llave);
        Cabecera(post, "X-Empresa").ShouldBe(Empresa.ToString());
        Guid.TryParse(Cabecera(post, "Idempotency-Key"), out _).ShouldBeTrue();
        cuerpo.ShouldNotBeNull().ShouldContain("\"correoDestino\":\"proveedor@aceros.co\"");

        var get = servidor.Recibidas[1].Peticion;
        get.RequestUri!.ToString().ShouldBe(
            "https://valida.acme.co/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900.123.456-7&tipoVinculo=proveedor");
        Cabecera(get, "Idempotency-Key").ShouldBeNull();
        Cabecera(get, "X-Usuario-Delegado").ShouldBeNull();
    }

    [Fact]
    public async Task La_consulta_condicional_devuelve_no_modificado_con_304_y_reutiliza_el_etag()
    {
        var (cliente, servidor) = Crear();
        servidor.Responder(HttpStatusCode.OK, Consulta(), r => r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"c-1\""));
        servidor.Responder(HttpStatusCode.NotModified, null, r => r.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"c-1\""));

        var primera = await cliente.Consultas.CertificacionDelVinculoAsync(Guid.NewGuid());
        (primera.NoModificado, primera.ETag, primera.Datos!.Folio, primera.Datos.CuentaDesignada!.Banco).ShouldBe((false, "\"c-1\"", "N1-C-2026-000001-1", "Bancolombia"));
        primera.Datos.VigenteHasta.ShouldBe(new DateOnly(2027, 4, 30));

        var segunda = await cliente.Consultas.CertificacionDelVinculoAsync(Guid.NewGuid(), primera.ETag);
        (segunda.NoModificado, segunda.ETag, segunda.Datos).ShouldBe((true, "\"c-1\"", (ConsultaCertificacionDto?)null));
        Cabecera(servidor.Recibidas[1].Peticion, "If-None-Match").ShouldBe("\"c-1\"");
    }

    [Fact]
    public async Task Un_error_de_la_api_llega_como_excepcion_con_su_codigo_y_correlacion()
    {
        var (cliente, servidor) = Crear();
        servidor.Responder(HttpStatusCode.NotFound, """{"title":"No encontrado","detail":"No existe ese vínculo en la empresa.","codigo":"vinculo-no-encontrado"}""",
            r => r.Headers.Add("X-Correlation-Id", "corr-1"));

        var error = await Should.ThrowAsync<ExcepcionValidasoft>(() => cliente.Vinculos.ObtenerAsync(Guid.NewGuid()));
        (error.Estado, error.Codigo, error.Detalle, error.Correlacion).ShouldBe(
            (HttpStatusCode.NotFound, "vinculo-no-encontrado", "No existe ese vínculo en la empresa.", "corr-1"));
    }

    [Fact]
    public async Task Reintenta_lo_idempotente_con_la_misma_clave_y_no_un_post_sin_clave()
    {
        var (cliente, servidor) = Crear();
        // GET: 503 y luego 200.
        servidor.Responder(HttpStatusCode.ServiceUnavailable);
        servidor.Responder(HttpStatusCode.OK, Consulta());
        (await cliente.Consultas.CertificacionDelVinculoAsync(Guid.NewGuid())).Datos.ShouldNotBeNull();
        servidor.Recibidas.Count.ShouldBe(2);

        // POST con clave explícita (crear vínculo): se reintenta con la MISMA Idempotency-Key.
        servidor.Recibidas.Clear();
        servidor.Responder(HttpStatusCode.BadGateway);
        servidor.Responder(HttpStatusCode.Created, """{"id":"0195f1c2-7d3e-7a00-8000-000000000001"}""");
        await cliente.Vinculos.CrearAsync(new CrearVinculoDto(null, null, "PROVEEDOR", null, "a@b.co", null), "srm-sol-7731");
        servidor.Recibidas.Select(r => Cabecera(r.Peticion, "Idempotency-Key")).ShouldBe(["srm-sol-7731", "srm-sol-7731"]);

        // POST sin clave estable (bloquear): un 503 no se reintenta.
        servidor.Recibidas.Clear();
        servidor.Responder(HttpStatusCode.ServiceUnavailable);
        await Should.ThrowAsync<ExcepcionValidasoft>(() => cliente.Vinculos.BloquearAsync(Guid.NewGuid(), new BloquearVinculoDto("Lista restrictiva", null)));
        servidor.Recibidas.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Como_usuario_firma_una_asercion_nueva_en_cada_intento_y_crea_la_sesion_embebida()
    {
        using var rsa = RSA.Create(2048);
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", rsa.ExportPkcs8PrivateKeyPem());
        var (cliente, servidor) = Crear(firmante);
        var laura = cliente.ComoUsuario(new UsuarioDelegado("srm-laura", "Laura", "laura@constructora.co"));

        servidor.Responder(HttpStatusCode.ServiceUnavailable);
        servidor.Responder(HttpStatusCode.OK, """{"id":"0195f1c2-7d3e-7a00-8000-000000000001"}""");
        await laura.Expedientes.ObtenerAsync(Guid.NewGuid());
        var jtis = servidor.Recibidas.Select(r => new JsonWebToken(Cabecera(r.Peticion, "X-Usuario-Delegado")).Id).ToList();
        jtis.Count.ShouldBe(2);
        jtis.Distinct().Count().ShouldBe(2);

        servidor.Responder(HttpStatusCode.OK, new SesionUsuarioDto("token-corto", DateTimeOffset.UtcNow.AddMinutes(30)));
        (await laura.Sesiones.CrearAsync()).Token.ShouldBe("token-corto");
        var sesion = servidor.Recibidas[^1].Peticion;
        sesion.RequestUri!.AbsolutePath.ShouldBe("/api/v1/sesiones-usuario");
        Cabecera(sesion, "X-Empresa").ShouldBeNull();

        // Sin persona no hay sesión embebida; sin firmante no se puede actuar como usuario.
        await Should.ThrowAsync<InvalidOperationException>(() => cliente.Sesiones.CrearAsync());
        var (sinFirmante, _) = Crear();
        await Should.ThrowAsync<InvalidOperationException>(() => sinFirmante.ComoUsuario(new UsuarioDelegado("x", "X", "x@y.co")).Vinculos.ObtenerAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Integracion_abierta_referencias_contactos_ficha_y_sesion_del_tercero()
    {
        var (cliente, servidor) = Crear();
        var tercero = Guid.NewGuid();
        var vinculo = Guid.NewGuid();
        servidor.Responder(HttpStatusCode.Created, """{"id":"0195f1c2-7d3e-7a00-8000-000000000002","nombre":"Aceros"}""");
        servidor.Responder(HttpStatusCode.Created, """[{"sistema":"ERPX","id":"C-1"}]""");
        servidor.Responder(HttpStatusCode.OK, """{"id":"0195f1c2-7d3e-7a00-8000-000000000002"}""");
        servidor.Responder(HttpStatusCode.OK, """{"id":"0195f1c2-7d3e-7a00-8000-000000000001","contactos":[]}""");
        servidor.Responder(HttpStatusCode.OK, """{"id":"0195f1c2-7d3e-7a00-8000-000000000001","contactos":[]}""");
        servidor.Responder(HttpStatusCode.OK, """{"vinculoId":"0195f1c2-7d3e-7a00-8000-000000000001"}""");
        servidor.Responder(HttpStatusCode.Created, """{"token":"a.b.c","expira":"2026-11-11T12:10:00Z","url":"https://valida.acme.co/embed/boveda","origen":"https://anfitrion.test","componente":"boveda","vinculoId":"0195f1c2-7d3e-7a00-8000-000000000001","terceroId":"0195f1c2-7d3e-7a00-8000-000000000002"}""");

        await cliente.Terceros.CrearAsync(new CrearTerceroDto(new IdentificacionDto("CO", "NIT", "900123456"), "Juridica", "Aceros", null, null, new ReferenciaExternaDto("", "C-1")), "erpx-c-1");
        (await cliente.Terceros.AgregarReferenciaAsync(tercero, new ReferenciaExternaDto("", "C-1"))).ShouldBe([new ReferenciaExternaDto("ERPX", "C-1")]);
        await cliente.Terceros.PorReferenciaAsync("ERPX", "C 1");
        await cliente.Vinculos.AgregarReferenciaAsync(vinculo, new ReferenciaExternaDto("", "S-1"));
        await cliente.Vinculos.ActualizarContactosAsync(vinculo, [new ContactoDeEmpresaDto("Ana", null, "ana@aceros.co", null, "Pagos")]);
        await cliente.Vinculos.FichaAsync(vinculo);
        var sesion = await cliente.Sesiones.CrearDelTerceroAsync(new CrearSesionEmbebidaDto(vinculo, "boveda", "https://anfitrion.test", 5));
        sesion.Token.ShouldBe("a.b.c");

        var r = servidor.Recibidas;
        (r[0].Peticion.Method, r[0].Peticion.RequestUri!.AbsolutePath, Cabecera(r[0].Peticion, "Idempotency-Key")).ShouldBe((HttpMethod.Post, "/api/v1/terceros", "erpx-c-1"));
        r[0].Cuerpo!.ShouldContain("\"referenciaExterna\":{\"sistema\":\"\",\"id\":\"C-1\"}");
        r[1].Peticion.RequestUri!.AbsolutePath.ShouldBe($"/api/v1/terceros/{tercero}/referencias");
        r[2].Peticion.RequestUri!.PathAndQuery.ShouldBe("/api/v1/terceros/por-referencia?sistema=ERPX&clave=C%201");
        Cabecera(r[2].Peticion, "X-Empresa").ShouldBeNull(); // el tercero es del tenant, no de una empresa
        (r[3].Peticion.Method, r[3].Peticion.RequestUri!.AbsolutePath).ShouldBe((HttpMethod.Post, $"/api/v1/vinculos/{vinculo}/referencias"));
        (r[4].Peticion.Method, r[4].Peticion.RequestUri!.AbsolutePath).ShouldBe((HttpMethod.Put, $"/api/v1/vinculos/{vinculo}/contactos"));
        r[4].Cuerpo!.ShouldContain("\"contactos\":[{\"nombre\":\"Ana\"");
        Cabecera(r[4].Peticion, "X-Empresa").ShouldBe(Empresa.ToString());
        r[5].Peticion.RequestUri!.AbsolutePath.ShouldBe($"/api/v1/vinculos/{vinculo}/ficha");
        (r[6].Peticion.Method, r[6].Peticion.RequestUri!.AbsolutePath).ShouldBe((HttpMethod.Post, "/api/v1/sesiones-tercero"));
        r[6].Cuerpo!.ShouldContain("\"origen\":\"https://anfitrion.test\"");
        Cabecera(r[6].Peticion, "X-Usuario-Delegado").ShouldBeNull();
    }

    [Fact]
    public async Task Para_empresa_cambia_x_empresa_y_la_url_base_puede_tener_ruta()
    {
        var (cliente, servidor) = Crear(urlBase: "https://erp.acme.co/validasoft");
        var otra = Guid.NewGuid();
        servidor.Responder(HttpStatusCode.OK, """{"id":"0195f1c2-7d3e-7a00-8000-000000000001"}""");
        await cliente.ParaEmpresa(otra).Vinculos.PorReferenciaAsync("SRM", "ADP 00123");

        var peticion = servidor.Recibidas.Single().Peticion;
        peticion.RequestUri!.AbsoluteUri.ShouldBe("https://erp.acme.co/validasoft/api/v1/vinculos/por-referencia?sistema=SRM&clave=ADP%2000123");
        Cabecera(peticion, "X-Empresa").ShouldBe(otra.ToString());
    }

    [Fact]
    public async Task Recorre_los_eventos_por_paginas_desde_el_cursor_guardado()
    {
        var (cliente, servidor) = Crear();
        static object Evento(string cursor) => new
        {
            id = Guid.NewGuid(), tipo = "certificado.emitido", version = 1, ocurridoEn = DateTimeOffset.UtcNow, tenantId = Guid.NewGuid(),
            empresaId = Empresa, cursor, datos = new { vinculoId = Guid.NewGuid() }
        };
        servidor.Responder(HttpStatusCode.OK, new { datos = new[] { Evento("11"), Evento("12") }, cursor = "12", hayMas = true });
        servidor.Responder(HttpStatusCode.OK, new { datos = new[] { Evento("15") }, cursor = "15", hayMas = false });

        var cursores = new List<string>();
        await foreach (var evento in cliente.Eventos.RecorrerAsync("10", ["certificado.emitido", "vinculo.bloqueado"], limite: 2))
            cursores.Add(evento.Cursor);

        cursores.ShouldBe(["11", "12", "15"]);
        servidor.Recibidas.Select(r => r.Peticion.RequestUri!.Query).ShouldBe(
        [
            "?desde=10&tipos=certificado.emitido%2Cvinculo.bloqueado&limite=2",
            "?desde=12&tipos=certificado.emitido%2Cvinculo.bloqueado&limite=2"
        ]);

        // Con empresaId explícito no se manda X-Empresa (la API exige que coincidan).
        servidor.Responder(HttpStatusCode.OK, new { datos = Array.Empty<object>(), cursor = "0", hayMas = false });
        await cliente.Eventos.ListarAsync(empresaId: Empresa);
        Cabecera(servidor.Recibidas[^1].Peticion, "X-Empresa").ShouldBeNull();
        servidor.Recibidas[^1].Peticion.RequestUri!.Query.ShouldBe($"?empresaId={Empresa}");
    }

    [Fact]
    public void Sin_api_key_no_se_construye()
    {
        Should.Throw<ArgumentException>(() => new ClienteValidasoft(new HttpClient(), new OpcionesValidasoft { UrlBase = new Uri("https://x.co") }));
    }
}
