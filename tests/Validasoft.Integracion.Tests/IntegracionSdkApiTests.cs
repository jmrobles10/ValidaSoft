using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Alba;
using Validasoft.Contratos;
using Validasoft.Sdk;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// H9 (CN-1…CN-5): lo que necesitan el SRM y Marco para hablar con Validasoft, probado con el SDK .NET contra la API en
/// memoria — consulta de certificación (200, 304 con ETag, bloqueado, sin vínculo), eventos por pull, webhook verificado con
/// el SDK, sesión embebida con identidad delegada y rotación de API keys con solapamiento (la anterior vale hasta la fecha y
/// luego 401).
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class IntegracionSdkApiTests(ApiFixture fixture)
{
    private static readonly string[] ScopesDelSrm =
    [
        "configuracion:leer", "vinculos:leer", "vinculos:escribir", "vinculos:bloquear", "flujos:leer", "expedientes:leer",
        "expedientes:administrar", "certificados:leer"
    ];

    private IAlbaHost Host => fixture.Host;

    private sealed record Escenario(string Admin, EmpresaDto Empresa, EmpresaDto Otra, RSA ClaveSrm, string LlaveSrm, UsuarioDto Laura);

    // ---------- consulta de certificación ----------

    [Fact]
    public async Task La_consulta_de_certificacion_responde_estado_vigencia_y_bloqueo_con_etag_y_304()
    {
        var e = await PrepararAsync();
        var (vinculo, nit) = await VinculoCertificadoAsync(e);
        var marcoLlave = await CrearLlaveAsync(e, "Marco consultas", ["consultas:leer"], [e.Empresa.Id.ToString()]);
        var marco = Cliente(marcoLlave, e.Empresa.Id);
        // El número llega como lo guarda el ERP (con puntos); el tipo de vínculo, en minúsculas.
        var consulta = new ConsultaDeCertificacion("NIT", $"{nit[..3]}.{nit[3..6]}.{nit[6..]}", "empleado");

        var vigente = await marco.Consultas.CertificacionAsync(consulta);
        var datos = vigente.Datos.ShouldNotBeNull();
        (datos.Certificado, datos.Estado, datos.EstadoVinculo, datos.Bloqueado, datos.MotivoBloqueo, datos.FuenteRespaldo)
            .ShouldBe((true, "Vigente", "Activo", false, (string?)null, false));
        (datos.VinculoId, datos.TerceroId).ShouldBe((vinculo.Id, vinculo.TerceroId));
        datos.Folio.ShouldStartWith($"{e.Empresa.PrefijoFolio}-C-");
        datos.UrlVerificacion.ShouldNotBeNull().ShouldContain("/verificar/");
        datos.RevalidacionPendiente.ShouldBeEmpty();
        datos.DocumentosVencidos.ShouldBeEmpty();
        (datos.CuentaDesignada, datos.Legacy).ShouldBe(((CuentaDesignadaResumenDto?)null, (LegadoDto?)null));
        vigente.ETag.ShouldNotBeNull().ShouldStartWith("\"c-");

        // Cabeceras: ETag fuerte y caché privada de 5 minutos; con If-None-Match igual, 304 sin cuerpo.
        var cruda = await CrudaAsync(marcoLlave, e.Empresa.Id, $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}");
        cruda.StatusCode.ShouldBe(HttpStatusCode.OK);
        (cruda.Headers.CacheControl!.Private, cruda.Headers.CacheControl.MaxAge).ShouldBe((true, (TimeSpan?)TimeSpan.FromSeconds(300)));
        cruda.Headers.ETag!.ToString().ShouldBe(vigente.ETag);
        var sinCambios = await CrudaAsync(marcoLlave, e.Empresa.Id, $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}", vigente.ETag);
        sinCambios.StatusCode.ShouldBe(HttpStatusCode.NotModified);
        (await sinCambios.Content.ReadAsStringAsync()).ShouldBeEmpty();
        (await marco.Consultas.CertificacionAsync(consulta, vigente.ETag)).NoModificado.ShouldBeTrue();

        // El SRM bloquea el vínculo: la misma consulta con el ETag viejo trae el cambio (otro ETag) y bloqueado.
        var srm = Cliente(e.LlaveSrm, e.Empresa.Id);
        await srm.Vinculos.BloquearAsync(vinculo.Id, new BloquearVinculoDto("Coincidencia en lista restrictiva", null));
        var bloqueado = await marco.Consultas.CertificacionAsync(consulta, vigente.ETag);
        bloqueado.NoModificado.ShouldBeFalse();
        bloqueado.ETag.ShouldNotBe(vigente.ETag);
        (bloqueado.Datos!.Bloqueado, bloqueado.Datos.MotivoBloqueo, bloqueado.Datos.Estado, bloqueado.Datos.EstadoVinculo)
            .ShouldBe((true, "Bloqueo", "Suspendido", "Bloqueado"));

        await srm.Vinculos.DesbloquearAsync(vinculo.Id, new JustificacionDto("Falso positivo verificado por cumplimiento."));
        var restablecido = (await marco.Consultas.CertificacionDelVinculoAsync(vinculo.Id)).Datos!;
        (restablecido.Bloqueado, restablecido.Estado, restablecido.EstadoVinculo).ShouldBe((false, "Vigente", "Activo"));

        // Sin vínculo (tercero desconocido u otra empresa del grupo): 200 SinVinculo, sin revelar si el tercero existe.
        var desconocido = (await marco.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", "811222333", "EMPLEADO"))).Datos!;
        (desconocido.Certificado, desconocido.EstadoVinculo, desconocido.VinculoId, desconocido.TerceroId, desconocido.Bloqueado)
            .ShouldBe((false, "SinVinculo", (Guid?)null, (Guid?)null, false));
        var deOtra = (await Cliente(e.Admin, e.Otra.Id).Consultas.CertificacionAsync(consulta)).Datos!;
        (deOtra.EstadoVinculo, deOtra.TerceroId).ShouldBe(("SinVinculo", (Guid?)null));
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => Cliente(e.Admin, e.Otra.Id).Consultas.CertificacionDelVinculoAsync(vinculo.Id)))
            .Codigo.ShouldBe("vinculo-no-encontrado");
        // Sin tipo de vínculo sirve porque el tercero tiene uno solo con la empresa.
        (await marco.Consultas.CertificacionAsync(consulta with { TipoVinculo = null })).Datos!.VinculoId.ShouldBe(vinculo.Id);

        // Permiso propio: la llave del SRM (sin consultas:leer) no consulta; los datos incompletos o inválidos responden 422.
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => srm.Consultas.CertificacionAsync(consulta))).Estado.ShouldBe(HttpStatusCode.Forbidden);
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => marco.Consultas.CertificacionAsync(consulta with { Numero = "NIT-ABC" })))
            .Codigo.ShouldBe("numero-identificacion-invalido");
        (await CrudaAsync(marcoLlave, e.Empresa.Id, "/api/v1/consultas/certificacion?pais=CO")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CrudaAsync(marcoLlave, null, $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- eventos por pull y webhooks verificados con el SDK ----------

    [Fact]
    public async Task Los_eventos_por_pull_traen_los_mismos_eventos_que_el_webhook_firmado()
    {
        var e = await PrepararAsync();
        await using var receptor = await NotificacionesApiTests.ReceptorDeWebhooks.IniciarAsync();
        var srm = (await ApiAsync<List<SistemaIntegradoDto>>(HttpMethod.Get, "/api/v1/sistemas", e.Admin, null, null)).Single(s => s.Codigo == "SRM");
        var webhook = await ApiAsync<WebhookDto>(HttpMethod.Post, "/api/v1/webhooks", e.Admin, null,
            new CrearWebhookDto(receptor.Url, ["*"], [e.Empresa.Id.ToString()], srm.Id, "SRM: todo"));
        var (vinculo, _) = await VinculoCertificadoAsync(e);
        var lector = Cliente(await CrearLlaveAsync(e, "SRM pull", ["eventos:leer"], [e.Empresa.Id.ToString()]), null);

        // Pull filtrado por tipo: llega el certificado del vínculo (la suscripción publica después del commit).
        var certificados = await EsperarAsync(async () =>
        {
            var lista = new List<EventoPublicoDto>();
            await foreach (var evento in lector.Eventos.RecorrerAsync(tipos: ["certificado.emitido"], limite: 2))
                lista.Add(evento);
            return lista.Any(x => VinculoDe(x) == vinculo.Id) ? lista : null;
        });
        certificados.ShouldAllBe(x => x.Tipo == "certificado.emitido" && x.EmpresaId == e.Empresa.Id);

        // Sin filtro: el recorrido completo del vínculo en orden de cursor; y desde el último cursor no hay nada nuevo.
        var todos = new List<EventoPublicoDto>();
        await foreach (var evento in lector.Eventos.RecorrerAsync())
            todos.Add(evento);
        var delVinculo = todos.Where(x => VinculoDe(x) == vinculo.Id).Select(x => x.Tipo).ToList();
        delVinculo.ShouldContain("vinculo.creado");
        delVinculo.ShouldContain("expediente.aprobado");
        delVinculo.ShouldContain("certificado.emitido");
        todos.Select(x => long.Parse(x.Cursor, System.Globalization.CultureInfo.InvariantCulture)).ShouldBeInOrder();
        var ultimo = todos[^1].Cursor;
        var despues = await lector.Eventos.ListarAsync(desde: ultimo);
        (despues.Datos.Count, despues.Cursor, despues.HayMas).ShouldBe((0, ultimo, false));

        // Con empresaId: la propia sirve; otra fuera del alcance de la llave, 403.
        (await lector.Eventos.ListarAsync(tipos: ["certificado.emitido"], empresaId: e.Empresa.Id)).Datos.ShouldNotBeEmpty();
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => lector.Eventos.ListarAsync(empresaId: e.Otra.Id))).Codigo.ShouldBe("alcance-insuficiente");

        // El webhook del mismo evento se verifica con el SDK (firma del servidor) y trae el mismo sobre que el pull.
        var emitido = certificados.First(x => VinculoDe(x) == vinculo.Id);
        var recibido = await EsperarAsync(() => Task.FromResult(receptor.Recibidos.FirstOrDefault(r => r.Id == emitido.Id)));
        var verificado = new VerificadorDeWebhooks(webhook.Secreto!).Validar(n => n switch
        {
            VerificadorDeWebhooks.CabeceraFirma => recibido.Firma,
            _ => null
        }, recibido.Cuerpo);
        (verificado.Id, verificado.Tipo, verificado.Cursor).ShouldBe((emitido.Id, "certificado.emitido", emitido.Cursor));
        verificado.Datos.GetRawText().ShouldBe(emitido.Datos.GetRawText());
        Should.Throw<WebhookInvalidoException>(() => new VerificadorDeWebhooks("whsec_otro").Validar(recibido.Firma, recibido.Cuerpo));
    }

    // ---------- identidad delegada y embebido ----------

    [Fact]
    public async Task Con_identidad_delegada_el_sdk_actua_como_la_persona_y_crea_la_sesion_del_embebido()
    {
        var e = await PrepararAsync();
        var (vinculo, _) = await VinculoCertificadoAsync(e);
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", e.ClaveSrm.ExportPkcs8PrivateKeyPem());
        var laura = Cliente(e.LlaveSrm, e.Empresa.Id, firmante).ComoUsuario(new UsuarioDelegado("srm-laura", e.Laura.Nombre, e.Laura.Correo));

        // Cada petición lleva una aserción nueva (jti de un solo uso): dos lecturas seguidas no chocan.
        (await laura.Vinculos.ObtenerAsync(vinculo.Id)).Id.ShouldBe(vinculo.Id);
        (await laura.Certificados.DelVinculoAsync(vinculo.Id)).Actual.ShouldNotBeNull().Estado.ShouldBe("Vigente");

        var sesion = await laura.Sesiones.CrearAsync();
        sesion.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(25));
        using var http = Host.Server.CreateClient();
        using var peticion = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/vinculos/{vinculo.Id}");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.Token);
        peticion.Headers.Add("X-Empresa", e.Empresa.Id.ToString());
        (await http.SendAsync(peticion)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Laura (oficial de cumplimiento) no tiene expedientes:administrar: la intersección con la llave se lo niega.
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => laura.Expedientes.AbrirAsync(vinculo.Id))).Estado.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---------- rotación de API keys ----------

    [Fact]
    public async Task Rotar_una_api_key_emite_otra_y_deja_la_anterior_valida_hasta_la_fecha_y_luego_401()
    {
        var e = await PrepararAsync();
        var (vinculo, _) = await VinculoCertificadoAsync(e);
        var vieja = await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", e.Admin, null,
            new CrearApiKeyDto("Marco producción", ["consultas:leer"], [e.Empresa.Id.ToString()], null));
        var consulta = $"/api/v1/consultas/certificacion?vinculoId={vinculo.Id}";
        (await CrudaAsync(vieja.Valor!, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var hasta = DateTimeOffset.UtcNow.AddHours(1);
        var nueva = await ApiAsync<ApiKeyDto>(HttpMethod.Post, $"/api/v1/api-keys/{vieja.Id}/rotar", e.Admin, null, new RotarApiKeyDto(hasta), 201);
        nueva.Valor.ShouldNotBeNull().ShouldStartWith("vsk_dev_");
        nueva.Valor.ShouldNotBe(vieja.Valor);
        (nueva.Nombre, nueva.RotadaDe, nueva.ExpiraEn).ShouldBe(("Marco producción", (Guid?)vieja.Id, (DateTimeOffset?)null));
        nueva.Scopes.ShouldBe(vieja.Scopes);
        nueva.Empresas.ShouldBe(vieja.Empresas);

        // Las dos valen durante el solapamiento; el listado muestra cuál reemplaza a cuál y hasta cuándo vale la vieja.
        (await CrudaAsync(vieja.Valor!, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CrudaAsync(nueva.Valor, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var listadas = await ApiAsync<List<ApiKeyDto>>(HttpMethod.Get, "/api/v1/api-keys", e.Admin, null, null);
        var anterior = listadas.Single(k => k.Id == vieja.Id);
        (anterior.ReemplazadaPor, anterior.Valor).ShouldBe(((Guid?)nueva.Id, (string?)null));
        anterior.ExpiraEn.ShouldNotBeNull().ShouldBe(hasta, TimeSpan.FromSeconds(1));
        listadas.Single(k => k.Id == nueva.Id).Valor.ShouldBeNull();

        // Rotar otra vez la vieja: 409; un solapamiento de más de 30 días: 422.
        (await CodigoAsync(HttpMethod.Post, $"/api/v1/api-keys/{vieja.Id}/rotar", e.Admin, new RotarApiKeyDto(null), 409)).ShouldBe("api-key-ya-rotada");
        (await CodigoAsync(HttpMethod.Post, $"/api/v1/api-keys/{nueva.Id}/rotar", e.Admin, new RotarApiKeyDto(DateTimeOffset.UtcNow.AddDays(31)), 422))
            .ShouldBe("solapamiento-invalido");

        try
        {
            // Pasada la fecha (reloj de Validasoft): la vieja responde 401 aunque estuviera en caché; la nueva sigue.
            fixture.Reloj.Desfase = TimeSpan.FromHours(2);
            (await CrudaAsync(vieja.Valor!, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
            (await CrudaAsync(nueva.Valor, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        // Rotación inmediata (fecha = ahora): la anterior deja de valer ya, aunque se acabara de usar (se olvida su caché).
        (await CrudaAsync(nueva.Valor, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var tercera = await ApiAsync<ApiKeyDto>(HttpMethod.Post, $"/api/v1/api-keys/{nueva.Id}/rotar", e.Admin, null,
            new RotarApiKeyDto(DateTimeOffset.UtcNow), 201);
        (await CrudaAsync(tercera.Valor!, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CrudaAsync(nueva.Valor, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Revocar también corta de inmediato.
        (await EnviarAsync(HttpMethod.Delete, $"/api/v1/api-keys/{tercera.Id}", e.Admin, null, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CrudaAsync(tercera.Valor!, e.Empresa.Id, consulta)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------- preparación ----------

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "s" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>(HttpMethod.Post, "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Grupo SDK", "co", "America/Bogota"));
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var empresa = await ApiAsync<EmpresaDto>(HttpMethod.Post, "/api/v1/empresas", admin, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "K" + Sufijo(), null, null));
        var otra = await ApiAsync<EmpresaDto>(HttpMethod.Post, "/api/v1/empresas", admin, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null));

        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>(HttpMethod.Post, "/api/v1/sistemas", admin, null,
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", rsa.ExportSubjectPublicKeyInfoPem()));
        var llave = await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", admin, null,
            new CrearApiKeyDto("Integración SRM", ScopesDelSrm, null, null, SistemaId: sistema.Id));
        var laura = await ApiAsync<UsuarioDto>(HttpMethod.Post, "/api/v1/usuarios", admin, null,
            new CrearUsuarioDto("Laura Revisora SDK", $"laura.{Guid.NewGuid():N}@constructora.co", null,
                [new RolAsignadoDto("OficialCumplimiento", empresa.Id)], null));

        // Flujo de EMPLEADO con un área automática: abrir el expediente lo aprueba y emite el certificado.
        var automatica = await ApiAsync<AreaDto>(HttpMethod.Post, "/api/v1/areas", admin, empresa.Id, new CrearAreaDto("AUTO_" + Sufijo(), "Validación automática"));
        var flujo = await ApiAsync<FlujoDto>(HttpMethod.Post, "/api/v1/flujos", admin, empresa.Id,
            new CrearFlujoDto("EMP_" + Sufijo(), "Empleados", "EMPLEADO", [new EtapaDto(1, [new AreaDeEtapaDto(automatica.Id, Automatica: true)])]));
        await ApiAsync<FlujoDto>(HttpMethod.Post, $"/api/v1/flujos/{flujo.Id}/publicar", admin, empresa.Id, new { });
        return new Escenario(admin, empresa, otra, rsa, llave.Valor!, laura);
    }

    /// <summary>El SRM (con el SDK) crea el vínculo y abre el expediente, que el área automática aprueba: queda certificado.</summary>
    private async Task<(VinculoDto Vinculo, string Nit)> VinculoCertificadoAsync(Escenario e)
    {
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var correo = $"t{Guid.NewGuid():N}@prueba.co";
        var srm = Cliente(e.LlaveSrm, e.Empresa.Id);
        var vinculo = await srm.Vinculos.CrearAsync(new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Servicios " + nit + " S.A.S.", null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            "EMPLEADO", null, correo, new ReferenciaExternaDto("SRM", "ADP-" + nit)), claveIdempotencia: "srm-" + nit);
        (await srm.Expedientes.AbrirAsync(vinculo.Id, claveIdempotencia: "srm-exp-" + nit)).Estado.ShouldBe("Aprobado");
        (await srm.Vinculos.PorReferenciaAsync("SRM", "ADP-" + nit)).Id.ShouldBe(vinculo.Id);
        return (vinculo, nit);
    }

    private Task<string> CrearLlaveAsync(Escenario e, string nombre, IReadOnlyList<string> scopes, IReadOnlyList<string>? empresas) =>
        ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", e.Admin, null, new CrearApiKeyDto(nombre, scopes, empresas, null))
            .ContinueWith(t => t.Result.Valor!, TaskScheduler.Default);

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    private static Guid? VinculoDe(EventoPublicoDto evento) =>
        evento.Datos.ValueKind == JsonValueKind.Object && evento.Datos.TryGetProperty("vinculoId", out var id) && id.ValueKind == JsonValueKind.String
            ? id.GetGuid()
            : null;

    /// <summary>Cliente del SDK sobre el servidor de pruebas (sin <c>UrlBase</c>: usa la del <c>HttpClient</c>).</summary>
    private ClienteValidasoft Cliente(string llave, Guid? empresa, FirmanteDeAsercionesDelegadas? firmante = null) =>
        new(Host.Server.CreateClient(), new OpcionesValidasoft { ApiKey = llave, EmpresaId = empresa, Firmante = firmante, Reintentos = 0 });

    private static async Task<T> EsperarAsync<T>(Func<Task<T?>> obtener) where T : class
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            if (await obtener() is { } valor)
                return valor;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("No llegó a tiempo.");
            await Task.Delay(200);
        }
    }

    private async Task<HttpResponseMessage> CrudaAsync(string llave, Guid? empresa, string url, string? siNoCoincide = null)
    {
        using var http = Host.Server.CreateClient();
        var peticion = new HttpRequestMessage(HttpMethod.Get, url);
        peticion.Headers.Add("X-Api-Key", llave);
        if (empresa is not null)
            peticion.Headers.Add("X-Empresa", empresa.Value.ToString());
        if (siNoCoincide is not null)
            peticion.Headers.TryAddWithoutValidation("If-None-Match", siNoCoincide);
        return await http.SendAsync(peticion);
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, string llave, Guid? empresa, object? cuerpo)
    {
        using var http = Host.Server.CreateClient();
        var peticion = new HttpRequestMessage(metodo, url);
        peticion.Headers.Add("X-Api-Key", llave);
        if (empresa is not null)
            peticion.Headers.Add("X-Empresa", empresa.Value.ToString());
        if (metodo == HttpMethod.Post)
            peticion.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
        return await http.SendAsync(peticion);
    }

    private async Task<T> ApiAsync<T>(HttpMethod metodo, string url, string llave, Guid? empresa, object? cuerpo, int? esperado = null)
    {
        var respuesta = await EnviarAsync(metodo, url, llave, empresa, cuerpo);
        var texto = await respuesta.Content.ReadAsStringAsync();
        if (esperado is not null)
            ((int)respuesta.StatusCode).ShouldBe(esperado.Value, texto);
        else
            respuesta.IsSuccessStatusCode.ShouldBeTrue($"{metodo} {url} → {(int)respuesta.StatusCode}: {texto}");
        return JsonSerializer.Deserialize<T>(texto, Json)!;
    }

    private async Task<string?> CodigoAsync(HttpMethod metodo, string url, string llave, object cuerpo, int esperado)
    {
        var respuesta = await EnviarAsync(metodo, url, llave, null, cuerpo);
        var texto = await respuesta.Content.ReadAsStringAsync();
        ((int)respuesta.StatusCode).ShouldBe(esperado, texto);
        return JsonDocument.Parse(texto).RootElement.GetProperty("codigo").GetString();
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
}
