using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Alba;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Validasoft.Aplicacion.Comun;
using Testcontainers.Minio;
using Testcontainers.PostgreSql;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Levanta PostgreSQL 17 y MinIO en Docker (Testcontainers) y la API completa en memoria (Alba). Los correos no salen: los
/// captura <see cref="CorreosCapturados"/>. El límite de peticiones públicas es bajo (20 por minuto y por IP) y el tamaño
/// máximo de archivo también (<see cref="TamanoMaximo"/>, 3 MB) para probar el 413 sin subir 20 MB y dejar espacio a un tipo
/// de 1 MB (TD-1). El reloj de la aplicación es <see cref="Reloj"/>: el real, salvo que una prueba lo adelante.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    public const string ClavePlataforma = "dev-plataforma-solo-local-cambiar-0001";
    public const string UrlPublica = "https://publico.validasoft.test";
    public const int LimitePublico = 20;

    /// <summary>Peticiones por minuto y por IP de <c>/api/v1/ingreso</c> en las pruebas (ING-1).</summary>
    public const int LimiteIngreso = 25;

    /// <summary>Sitio (<c>rp.id</c>) y origen de la app web que declaran las passkeys de las pruebas (ING-2).</summary>
    public const string SitioDePasskeys = "validasoft.test";

    public const string OrigenDeLaApp = "https://app.validasoft.test";
    public const long TamanoMaximo = 3 * 1024 * 1024;

    /// <summary>Fork comunitario de MinIO (misma línea de comandos; minio/minio ya no se publica en Docker Hub).</summary>
    public const string ImagenMinio = "pgsty/minio:RELEASE.2026-08-04T00-00-00Z";

    /// <summary>Origen de un frontend en otro dominio con CORS habilitado (SE-5).</summary>
    public const string OrigenCors = "https://app.cliente.test";

    /// <summary>Proxy confiable (IP suelta) y red confiable para <c>X-Forwarded-For</c> (SE-6).</summary>
    public const string ProxyConfiable = "10.20.30.40";

    public const string RedConfiable = "10.99.0.0/16";

    /// <summary>Fallos seguidos que deshabilitan una suscripción de webhook en las pruebas (NW-7).</summary>
    public const int FallosParaDeshabilitarWebhook = 3;

    /// <summary>Carpeta temporal con un <c>dist/</c> mínimo de cada app para servirlas desde la API (SE-3).</summary>
    public string CarpetaApps { get; } = Path.Combine(Path.GetTempPath(), "validasoft-apps-" + Guid.NewGuid().ToString("N"));

    /// <summary>JSON del Portal que lee el sincronizador de coexistencia (origen configurado <c>Archivo</c>, F4); lo escriben las pruebas.</summary>
    public string ArchivoPortal { get; } = Path.Combine(Path.GetTempPath(), "validasoft-portal-" + Guid.NewGuid().ToString("N") + ".json");

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();
    private readonly MinioContainer _minio = new MinioBuilder(ImagenMinio).Build();

    public IAlbaHost Host { get; private set; } = null!;

    public CorreosCapturados Correos { get; } = new();

    public RelojDePruebas Reloj { get; } = new();

    /// <summary>Analizador de IA falso (ADR-0012): las pruebas nunca llaman a OpenAI.</summary>
    public AnalizadorFalso Analizador { get; } = new();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _minio.StartAsync());
        CrearApps();
        Host = await AlbaHost.For<Program>(b =>
        {
            b.UseEnvironment("Development");
            b.UseSetting("ConnectionStrings:Validasoft", _postgres.GetConnectionString());
            b.UseSetting("Validasoft:Almacen:Endpoint", _minio.GetConnectionString());
            b.UseSetting("Validasoft:Almacen:EndpointPublico", "");
            b.UseSetting("Validasoft:Almacen:AccessKey", _minio.GetAccessKey());
            b.UseSetting("Validasoft:Almacen:SecretKey", _minio.GetSecretKey());
            b.UseSetting("Validasoft:Almacen:CrearBucketsAlIniciar", "true");
            b.UseSetting("Validasoft:Boveda:TamanoMaximoBytes", TamanoMaximo.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("Validasoft:Correo:UrlPublica", UrlPublica);
            b.UseSetting("Validasoft:LimitePublico:Peticiones", LimitePublico.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("Validasoft:LimitePublico:VentanaSegundos", "60");
            b.UseSetting("Validasoft:SesionTercero:CookieSegura", "true"); // como producción: se verifica el atributo Secure
            b.UseSetting("Validasoft:Ingreso:Passkeys:RpId", SitioDePasskeys);
            b.UseSetting("Validasoft:Ingreso:Passkeys:Origenes:0", OrigenDeLaApp);
            b.UseSetting("Validasoft:LimiteIngreso:Peticiones", LimiteIngreso.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("Validasoft:LimiteIngreso:VentanaSegundos", "60");
            b.UseSetting("Validasoft:Apps:Web", Path.Combine(CarpetaApps, "web"));
            b.UseSetting("Validasoft:Apps:Publica", Path.Combine(CarpetaApps, "public"));
            b.UseSetting("Validasoft:Cors:OrigenesPermitidos:0", OrigenCors);
            b.UseSetting("Validasoft:Proxies:Confiables:0", ProxyConfiable);
            b.UseSetting("Validasoft:Proxies:Confiables:1", RedConfiable);
            b.UseSetting("Validasoft:Importacion:Portal:Archivo", ArchivoPortal); // sincronizador del Portal sin red (F4)
            b.UseSetting("Validasoft:Certificados:SegundosProceso", "1"); // PDF de los certificados casi al instante
            b.UseSetting("Validasoft:Verificadores:SegundosProceso", "1"); // consultas automáticas de listas casi al instante
            b.UseSetting("Validasoft:RevisionAutomatica:SegundosProceso", "1"); // revisión automática con IA casi al instante
            // Webhooks (NW): receptor local por http, reintentos de segundos y deshabilitación tras 3 fallos seguidos.
            b.UseSetting("Validasoft:Webhooks:PermitirHttpLocal", "true");
            b.UseSetting("Validasoft:Webhooks:Reintentos:0", "00:00:01");
            b.UseSetting("Validasoft:Webhooks:Reintentos:1", "00:00:01");
            b.UseSetting("Validasoft:Webhooks:FallosParaDeshabilitar", FallosParaDeshabilitarWebhook.ToString(System.Globalization.CultureInfo.InvariantCulture));
            b.UseSetting("Validasoft:Webhooks:TimeoutSegundos", "3");
            // Plazos de cargue (LT-4): sin barrido periódico; las pruebas lo disparan con el reloj o la fecha que necesitan.
            b.UseSetting("Validasoft:PlazosDeCargue:Intervalo", "00:00:00");
            // Invitación masiva (LT-8): tandas pequeñas para ver el avance, la pausa y la reanudación.
            b.UseSetting("Validasoft:Lotes:TamanoTanda", "5");
            // Plan del grupo (OD-14…OD-17): el conteo de llamadas se vuelca cada segundo, el almacenamiento se mide en cada lectura y los barridos
            // periódicos del plan y del habeas data se apagan (las pruebas los disparan con los endpoints de Plataforma).
            b.UseSetting("Validasoft:Plan:SegundosDeVolcado", "1");
            b.UseSetting("Validasoft:Plan:MinutosMedicionAlmacenamiento", "0");
            b.UseSetting("Validasoft:Plan:Intervalo", "00:00:00");
            b.UseSetting("Validasoft:Habeas:Intervalo", "00:00:00");
            b.ConfigureTestServices(s =>
            {
                // El transporte SMTP se reemplaza, no el enviador: el aislamiento del sandbox (SB-4) sigue activo en las pruebas y lo que
                // llega a Correos es solo lo que de verdad saldría a un destinatario.
                s.RemoveAll<ITransporteDeCorreo>();
                s.AddSingleton<ITransporteDeCorreo>(Correos);
                s.RemoveAll<TimeProvider>();
                s.AddSingleton<TimeProvider>(Reloj);
                s.RemoveAll<Validasoft.Aplicacion.Ia.IAnalizadorDeDocumentos>();
                s.AddSingleton<Validasoft.Aplicacion.Ia.IAnalizadorDeDocumentos>(Analizador);
            });
        });
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await _postgres.DisposeAsync();
        await _minio.DisposeAsync();
        Directory.Delete(CarpetaApps, recursive: true);
    }

    private void CrearApps()
    {
        Directory.CreateDirectory(Path.Combine(CarpetaApps, "web", "assets"));
        Directory.CreateDirectory(Path.Combine(CarpetaApps, "public", "assets"));
        File.WriteAllText(Path.Combine(CarpetaApps, "web", "index.html"), "<!doctype html><title>web</title><script src=\"./assets/web.js\"></script>");
        File.WriteAllText(Path.Combine(CarpetaApps, "web", "assets", "web-abc123.js"), "console.log('web');");
        File.WriteAllText(Path.Combine(CarpetaApps, "public", "index.html"), "<!doctype html><title>public</title>");
        File.WriteAllText(Path.Combine(CarpetaApps, "public", "assets", "public-abc123.js"), "console.log('public');");
    }
}

/// <summary>Reloj real desplazado <see cref="Desfase"/> (las pruebas de vigencias lo adelantan y lo devuelven a cero).</summary>
public sealed class RelojDePruebas : TimeProvider
{
    public TimeSpan Desfase { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Desfase;
}

/// <summary>Enviador falso: guarda los correos para leer el enlace o el código; el envío real ocurre tras el commit (outbox).</summary>
public sealed class CorreosCapturados : ITransporteDeCorreo, IEnviadorDeCorreo
{
    private readonly ConcurrentQueue<MensajeDeCorreo> _mensajes = new();

    public Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        _mensajes.Enqueue(mensaje);
        return Task.CompletedTask;
    }

    public IReadOnlyList<MensajeDeCorreo> Para(string correo) =>
        [.. _mensajes.Where(m => string.Equals(m.Para, correo, StringComparison.OrdinalIgnoreCase))];

    /// <summary>Espera hasta que haya <paramref name="cuantos"/> correos a <paramref name="para"/> que cumplan la condición; devuelve el último.</summary>
    public async Task<MensajeDeCorreo> EsperarAsync(string para, Func<MensajeDeCorreo, bool>? condicion = null, int cuantos = 1)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var encontrados = Para(para).Where(condicion ?? (_ => true)).ToList();
            if (encontrados.Count >= cuantos)
                return encontrados[cuantos - 1];
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No llegaron {cuantos} correos a {para} (hay {encontrados.Count}).");
            await Task.Delay(50);
        }
    }
}

[CollectionDefinition("api")]
public sealed class ColeccionApi : ICollectionFixture<ApiFixture>;

[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class VinculosApiTests(ApiFixture fixture)
{
    private IAlbaHost Host => fixture.Host;

    private sealed record Escenario(string ApiKey, EmpresaDto EmpresaA, EmpresaDto EmpresaB);

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await PostAsync<TenantCreadoDto>("/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Constructora Demo", "co", "America/Bogota"));
        var clave = tenant.ApiKeyAdministrador.Valor!;

        var a = await PostAsync<EmpresaDto>("/api/v1/empresas", clave, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null));
        var b = await PostAsync<EmpresaDto>("/api/v1/empresas", clave, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null));
        return new Escenario(clave, a, b);
    }

    /// <summary>La llave de administración no pertenece a un sistema integrado: no envía referencia externa (ADR-0011).</summary>
    private static CrearVinculoDto Proveedor(string nit) => new(
        TerceroId: null,
        Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Aceros del Norte S.A.S.", null,
            [new ContactoDto("Ana Gómez", "ana@aceros.co", null, "Comercial")]),
        TipoVinculo: "PROVEEDOR",
        Atributos: null,
        CorreoInvitacion: "ana@aceros.co",
        ReferenciaExterna: null);

    [Fact]
    public async Task Crea_un_vinculo_con_identificador_invitacion_e_historial()
    {
        var e = await PrepararAsync();

        var vinculo = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900123456"));

        vinculo.Id.ShouldNotBe(Guid.Empty);
        vinculo.Estado.ShouldBe("Invitado");
        vinculo.TipoVinculo.Codigo.ShouldBe("PROVEEDOR");
        vinculo.Invitacion!.Envios.ShouldBe(1);

        var leido = await GetAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}", e.ApiKey, e.EmpresaA.Id);
        leido.Id.ShouldBe(vinculo.Id);

        var historial = await GetAsync<List<EventoHistorialDto>>($"/api/v1/vinculos/{vinculo.Id}/historial", e.ApiKey, e.EmpresaA.Id);
        historial.Select(h => h.Tipo).ShouldBe(["vinculo_creado", "invitacion_enviada"]);
        historial[0].ActorTipo.ShouldBe("Integracion");
    }

    [Fact]
    public async Task No_permite_dos_vinculos_del_mismo_tipo_con_el_mismo_tercero_en_la_empresa()
    {
        var e = await PrepararAsync();
        await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900555111"));

        var respuesta = await Host.Scenario(s =>
        {
            s.Post.Json(Proveedor("900555111")).ToUrl("/api/v1/vinculos");
            Cabeceras(s, e.ApiKey, e.EmpresaA.Id, Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(409);
        });
        (await respuesta.ReadAsTextAsync()).ShouldContain("vinculo-duplicado");
    }

    [Fact]
    public async Task Una_llave_sin_sistema_integrado_no_registra_referencias_externas()
    {
        var e = await PrepararAsync();

        var respuesta = await Host.Scenario(s =>
        {
            s.Post.Json(Proveedor("900555222") with { ReferenciaExterna = new ReferenciaExternaDto("SRM", "ADP-00123") }).ToUrl("/api/v1/vinculos");
            Cabeceras(s, e.ApiKey, e.EmpresaA.Id, Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(422);
        });
        (await respuesta.ReadAsTextAsync()).ShouldContain("referencia-sin-sistema");
    }

    [Fact]
    public async Task Otra_empresa_del_mismo_tenant_no_ve_el_vinculo_y_puede_crear_el_suyo()
    {
        var e = await PrepararAsync();
        var deA = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900777222"));

        await Host.Scenario(s =>
        {
            s.Get.Url($"/api/v1/vinculos/{deA.Id}");
            Cabeceras(s, e.ApiKey, e.EmpresaB.Id);
            s.StatusCodeShouldBe(404);
        });

        var deB = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaB.Id, Proveedor("900777222"));
        deB.TerceroId.ShouldBe(deA.TerceroId); // mismo tercero en la bóveda
        deB.Id.ShouldNotBe(deA.Id);            // vínculos distintos por empresa
    }

    [Fact]
    public async Task Idempotency_key_repite_la_respuesta_y_rechaza_otro_cuerpo()
    {
        var e = await PrepararAsync();
        var clave = Guid.NewGuid().ToString();

        var primero = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900888333"), clave);
        var repetido = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900888333"), clave);
        repetido.Id.ShouldBe(primero.Id);

        await Host.Scenario(s =>
        {
            s.Post.Json(Proveedor("900888444")).ToUrl("/api/v1/vinculos");
            Cabeceras(s, e.ApiKey, e.EmpresaA.Id, clave);
            s.StatusCodeShouldBe(422);
        });
    }

    [Fact]
    public async Task Bloquear_y_desbloquear_restituye_el_estado()
    {
        var e = await PrepararAsync();
        var vinculo = await PostAsync<VinculoDto>("/api/v1/vinculos", e.ApiKey, e.EmpresaA.Id, Proveedor("900999444"));

        var bloqueado = await PostAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}/bloquear", e.ApiKey, e.EmpresaA.Id,
            new BloquearVinculoDto("Sanción contractual", null), esperado: 200);
        bloqueado.Estado.ShouldBe("Bloqueado");

        // El historial usa las convenciones JSON de la API: camelCase y enums como texto.
        var historial = await GetAsync<List<EventoHistorialDto>>($"/api/v1/vinculos/{vinculo.Id}/historial", e.ApiKey, e.EmpresaA.Id);
        var datos = historial.Single(h => h.Tipo == "vinculo_bloqueado").Datos;
        datos.GetProperty("estadoPrevio").ValueKind.ShouldBe(JsonValueKind.String);
        datos.GetProperty("estadoPrevio").GetString().ShouldBe("Invitado");
        datos.GetProperty("motivo").GetString().ShouldBe("Sanción contractual");

        var desbloqueado = await PostAsync<VinculoDto>($"/api/v1/vinculos/{vinculo.Id}/desbloquear", e.ApiKey, e.EmpresaA.Id,
            new JustificacionDto("Sanción levantada"), esperado: 200);
        desbloqueado.Estado.ShouldBe("Invitado");
    }

    [Fact]
    public async Task Sin_credencial_responde_401() =>
        await Host.Scenario(s =>
        {
            s.Get.Url("/api/v1/vinculos");
            s.StatusCodeShouldBe(401);
        });

    // ---------- utilidades ----------

    private async Task<T> PostAsync<T>(string url, string apiKey, Guid? empresa, object cuerpo, string? idempotencia = null, int esperado = 201)
    {
        var resultado = await Host.Scenario(s =>
        {
            s.Post.Json(cuerpo).ToUrl(url);
            Cabeceras(s, apiKey, empresa, idempotencia ?? Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });
        return (await resultado.ReadAsJsonAsync<T>())!;
    }

    private async Task<T> GetAsync<T>(string url, string apiKey, Guid? empresa)
    {
        var resultado = await Host.Scenario(s =>
        {
            s.Get.Url(url);
            Cabeceras(s, apiKey, empresa);
            s.StatusCodeShouldBeOk();
        });
        return (await resultado.ReadAsJsonAsync<T>())!;
    }

    private static void Cabeceras(Scenario s, string apiKey, Guid? empresa, string? idempotencia = null)
    {
        s.WithRequestHeader("X-Api-Key", apiKey);
        if (empresa is not null)
            s.WithRequestHeader("X-Empresa", empresa.Value.ToString());
        if (idempotencia is not null)
            s.WithRequestHeader("Idempotency-Key", idempotencia);
    }
}
