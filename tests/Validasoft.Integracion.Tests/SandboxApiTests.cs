using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Alba;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Sdk;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// El sandbox del grupo (SB-1…SB-7, docs/08 §2.2): una API key <c>vsk_test_…</c> opera en un tenant hermano con la configuración
/// del grupo y ningún dato, sus correos no salen (van a un buzón consultable), no deja nada en producción —terceros, certificados
/// verificables, listados de la consola, correos de acceso— y se reinicia sin perder empresas ni llaves.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class SandboxApiTests(ApiFixture fixture)
{
    private static readonly string[] ScopesDelIntegrador =
    [
        "configuracion:leer", "vinculos:leer", "vinculos:escribir", "vinculos:bloquear", "flujos:leer", "expedientes:leer",
        "expedientes:administrar", "certificados:leer", "consultas:leer", "eventos:leer", "sandbox:administrar"
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private IAlbaHost Host => fixture.Host;

    private sealed record Grupo(string Admin, EmpresaDto Empresa, SistemaIntegradoDto Sistema, RSA ClaveSistema, ApiKeyDto Produccion, ApiKeyDto Prueba);

    // ---------- el sandbox hereda la configuración y ningún dato ----------

    [Fact]
    public async Task Una_llave_de_prueba_crea_un_sandbox_hermano_con_la_configuracion_del_grupo_y_sin_ninguno_de_sus_datos()
    {
        var g = await PrepararAsync();
        var (vinculoReal, nit) = await CertificadoEnProduccionAsync(g);

        // Emitir la llave de prueba creó el sandbox: lo ve el administrador de producción y la propia llave de prueba.
        g.Prueba.Valor.ShouldNotBeNull().ShouldStartWith("vsk_test_");
        g.Prueba.Sandbox.ShouldBeTrue();
        var desdeProduccion = await ClienteDe(g.Produccion.Valor!, g.Empresa.Id).Sandbox.ObtenerAsync();
        (desdeProduccion.Existe, desdeProduccion.Entorno).ShouldBe((true, "produccion"));
        var copia = desdeProduccion.Empresas.ShouldHaveSingleItem();
        copia.Id.ShouldNotBe(g.Empresa.Id);
        (copia.ProduccionId, copia.Nombre, copia.Activa).ShouldBe((g.Empresa.Id, "Norte S.A.S. (sandbox)", true));
        copia.PrefijoFolio.ShouldBe("T" + g.Empresa.PrefijoFolio);
        desdeProduccion.Sistemas.ShouldBe(["SRM"]);
        desdeProduccion.TenantId.ShouldNotBeNull().ShouldNotBe(g.Empresa.TenantId);

        // La llave de prueba se reconoce como tal en cada respuesta y solo conoce las empresas del sandbox.
        var sandbox = ClienteDe(g.Prueba.Valor!, copia.Id);
        var estado = await sandbox.Sandbox.ObtenerAsync();
        (estado.Entorno, estado.TenantId).ShouldBe(("sandbox", desdeProduccion.TenantId));
        var cruda = await EnviarAsync(HttpMethod.Get, "/api/v1/empresas", g.Prueba.Valor!, null, null);
        cruda.Headers.GetValues("Validasoft-Entorno").ShouldBe(["sandbox"]);
        (await cruda.Content.ReadFromJsonAsync<List<EmpresaDto>>(Json))!.Select(e => e.Id).ShouldBe([copia.Id]);
        (await EnviarAsync(HttpMethod.Get, "/api/v1/empresas", g.Produccion.Valor!, null, null)).Headers.Contains("Validasoft-Entorno").ShouldBeFalse();
        // El id de la empresa real no existe para una llave de prueba (ni al revés).
        (await EnviarAsync(HttpMethod.Get, "/api/v1/vinculos", g.Prueba.Valor!, g.Empresa.Id, null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await EnviarAsync(HttpMethod.Get, "/api/v1/vinculos", g.Produccion.Valor!, copia.Id, null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Hereda la configuración (el flujo de EMPLEADO ya publicado) y ningún dato: ni el vínculo ni el certificado de producción.
        var flujos = await ApiAsync<List<FlujoDto>>(HttpMethod.Get, "/api/v1/flujos", g.Prueba.Valor!, copia.Id, null);
        flujos.ShouldContain(f => f.Codigo.StartsWith("EMP_", StringComparison.Ordinal));
        (await ApiAsync<PaginaDto<VinculoDto>>(HttpMethod.Get, "/api/v1/vinculos", g.Prueba.Valor!, copia.Id, null)).Datos.ShouldBeEmpty();
        (await EnviarAsync(HttpMethod.Get, $"/api/v1/vinculos/{vinculoReal.Id}", g.Prueba.Valor!, copia.Id, null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // El mismo NIT en el sandbox es otro tercero: su vínculo se certifica allá y producción ni se entera.
        var (vinculoDePrueba, _) = await CertificadoEnSandboxAsync(g, copia, nit);
        vinculoDePrueba.TerceroId.ShouldNotBe(vinculoReal.TerceroId);
        vinculoDePrueba.EmpresaId.ShouldBe(copia.Id);
        var certificadoDePrueba = (await sandbox.Certificados.DelVinculoAsync(vinculoDePrueba.Id)).Actual.ShouldNotBeNull();
        certificadoDePrueba.Folio.ShouldStartWith(copia.PrefijoFolio + "-C-");
        var consultaReal = (await ClienteDe(g.Produccion.Valor!, g.Empresa.Id).Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", nit, "EMPLEADO"))).Datos!;
        (consultaReal.VinculoId, consultaReal.TerceroId, consultaReal.Estado).ShouldBe((vinculoReal.Id, vinculoReal.TerceroId, "Vigente"));
        (await ApiAsync<PaginaDto<VinculoDto>>(HttpMethod.Get, "/api/v1/vinculos", g.Produccion.Valor!, g.Empresa.Id, null)).Datos
            .ShouldHaveSingleItem().Id.ShouldBe(vinculoReal.Id);
        var consultaDePrueba = (await sandbox.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", nit, "EMPLEADO"))).Datos!;
        (consultaDePrueba.VinculoId, consultaDePrueba.TerceroId).ShouldBe((vinculoDePrueba.Id, vinculoDePrueba.TerceroId));
    }

    // ---------- los correos no salen ----------

    [Fact]
    public async Task Los_correos_del_sandbox_no_salen_van_al_buzon_y_los_de_produccion_no_van_al_buzon()
    {
        var g = await PrepararAsync();
        var copia = (await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync()).Empresas.Single();
        var sandbox = ClienteDe(g.Prueba.Valor!, copia.Id);
        var produccion = ClienteDe(g.Produccion.Valor!, g.Empresa.Id);

        var correoDePrueba = $"prueba.{Guid.NewGuid():N}@sandbox.co";
        var correoReal = $"real.{Guid.NewGuid():N}@proveedor.co";
        var nit = NitNuevo();
        await sandbox.Vinculos.CrearAsync(NuevoVinculo(nit, correoDePrueba, "CLIENTE"), "sbx-" + nit);
        await produccion.Vinculos.CrearAsync(NuevoVinculo(NitNuevo(), correoReal, "CLIENTE"), "real-" + Guid.NewGuid());

        // Producción sale por el transporte real; el sandbox, nunca.
        await fixture.Correos.EsperarAsync(correoReal);
        var invitacion = await sandbox.Sandbox.EsperarCorreoAsync(correoDePrueba);
        invitacion.Asunto.ShouldContain("Norte S.A.S. (sandbox)");
        var enlace = invitacion.Enlaces.Single(e => e.Contains("/e/", StringComparison.Ordinal));
        enlace.ShouldStartWith($"{ApiFixture.UrlPublica}/e/");
        invitacion.Html.ShouldContain("/e/");
        await Task.Delay(500);
        fixture.Correos.Para(correoDePrueba).ShouldBeEmpty();
        (await sandbox.Sandbox.CorreosAsync(correoReal)).Datos.ShouldBeEmpty();

        // El enlace del buzón abre la sesión del tercero de pruebas, con la empresa del sandbox.
        var token = enlace[(enlace.LastIndexOf('/') + 1)..];
        var sesion = await PublicoAsync(HttpMethod.Post, $"/public/v1/enlaces/{token}/canjear");
        sesion.StatusCode.ShouldBe(HttpStatusCode.OK);
        var abierta = (await sesion.Content.ReadFromJsonAsync<SesionTerceroDto>(Json))!;
        abierta.Vinculos.ShouldHaveSingleItem().Empresa.Nombre.ShouldBe("Norte S.A.S. (sandbox)");

        // Filtros, detalle y vaciado del buzón.
        (await sandbox.Sandbox.CorreosAsync(asunto: "invita")).Datos.ShouldContain(c => c.Id == invitacion.Id);
        (await sandbox.Sandbox.CorreoAsync(invitacion.Id)).Para.ShouldBe(correoDePrueba);
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => sandbox.Sandbox.CorreoAsync(Guid.NewGuid()))).Codigo.ShouldBe("correo-no-encontrado");
        (await sandbox.Sandbox.ObtenerAsync()).Correos.ShouldBeGreaterThan(0);
        await sandbox.Sandbox.VaciarCorreosAsync();
        (await sandbox.Sandbox.CorreosAsync()).Datos.ShouldBeEmpty();
    }

    [Fact]
    public async Task Un_proveedor_de_pruebas_recibe_su_codigo_otp_en_el_buzon_y_pedir_acceso_con_su_correo_no_envia_nada()
    {
        var g = await PrepararAsync();
        var copia = (await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync()).Empresas.Single();
        var sandbox = ClienteDe(g.Prueba.Valor!, copia.Id);
        var correo = $"proveedor.{Guid.NewGuid():N}@sandbox.co";
        await sandbox.Vinculos.CrearAsync(NuevoVinculo(NitNuevo(), correo, "PROVEEDOR"), "otp-" + Guid.NewGuid());

        var invitacion = await sandbox.Sandbox.EsperarCorreoAsync(correo, "invita");
        var enlace = invitacion.Enlaces.Single(e => e.Contains("/e/", StringComparison.Ordinal));
        var token = enlace[(enlace.LastIndexOf('/') + 1)..];
        (await PublicoAsync(HttpMethod.Post, $"/public/v1/enlaces/{token}/canjear")).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var otp = await sandbox.Sandbox.EsperarCorreoAsync(correo, "código");
        otp.Texto.ShouldContain("Tu código de acceso es");

        // «Volver a entrar con el correo» no emite enlaces para vínculos de un sandbox: responde igual y no sale ni se guarda nada.
        var antes = (await sandbox.Sandbox.CorreosAsync(correo)).Datos.Count;
        var acceso = await PublicoAsync(HttpMethod.Post, "/public/v1/acceso", new { correo });
        acceso.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await Task.Delay(1500);
        fixture.Correos.Para(correo).ShouldBeEmpty();
        (await sandbox.Sandbox.CorreosAsync(correo)).Datos.Count.ShouldBe(antes);
    }

    // ---------- nada se filtra a producción ----------

    [Fact]
    public async Task Un_certificado_del_sandbox_no_es_verificable_y_el_sandbox_no_aparece_en_la_consola_de_plataforma()
    {
        var g = await PrepararAsync();
        var (vinculoReal, nit) = await CertificadoEnProduccionAsync(g);
        var copia = (await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync()).Empresas.Single();
        var (vinculoDePrueba, _) = await CertificadoEnSandboxAsync(g, copia, nit);

        var folioReal = (await ClienteDe(g.Produccion.Valor!, g.Empresa.Id).Certificados.DelVinculoAsync(vinculoReal.Id)).Actual!.Folio;
        var folioDePrueba = (await ClienteDe(g.Prueba.Valor!, copia.Id).Certificados.DelVinculoAsync(vinculoDePrueba.Id)).Actual!.Folio;
        (await PublicoAsync(HttpMethod.Get, $"/public/v1/verificar/{folioReal}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PublicoAsync(HttpMethod.Get, $"/public/v1/verificar/{folioDePrueba}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Ni la lista de tenants ni la de grupos de la consola de Plataforma lo muestran como un cliente más.
        var tenants = await ApiAsync<List<TenantDto>>(HttpMethod.Get, "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null, null);
        tenants.ShouldContain(t => t.Id == g.Empresa.TenantId);
        tenants.ShouldNotContain(t => t.Slug.Contains("~sandbox", StringComparison.Ordinal));
        var grupos = await ApiAsync<PaginaDto<GrupoDto>>(HttpMethod.Get, "/api/v1/plataforma/grupos?q=Grupo%20Sandbox&limite=100", ApiFixture.ClavePlataforma, null, null);
        grupos.Datos.ShouldContain(x => x.Tenant.Id == g.Empresa.TenantId);
        grupos.Datos.ShouldNotContain(x => x.Tenant.Nombre.EndsWith("(sandbox)", StringComparison.Ordinal));
        grupos.Datos.SelectMany(x => x.Empresas).ShouldNotContain(e => e.Nombre.EndsWith("(sandbox)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Las_api_keys_solo_se_administran_en_produccion_y_el_sandbox_pide_su_propio_permiso()
    {
        var g = await PrepararAsync();
        var copia = (await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync()).Empresas.Single();

        // Con una credencial del sandbox no se emiten, listan ni rotan llaves (viven en producción), aunque tenga el permiso.
        var conPermiso = (await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", g.Admin, null,
            new CrearApiKeyDto("Administra llaves", ["api-keys:administrar", "sandbox:administrar"], null, null, Sandbox: true))).Valor!;
        foreach (var (metodo, ruta) in new[] { (HttpMethod.Get, "/api/v1/api-keys"), (HttpMethod.Post, "/api/v1/api-keys"), (HttpMethod.Post, $"/api/v1/api-keys/{g.Prueba.Id}/rotar") })
        {
            var respuesta = await EnviarAsync(metodo, ruta, conPermiso, null, metodo == HttpMethod.Post ? new { } : null);
            ((int)respuesta.StatusCode).ShouldBe(422);
            (await respuesta.Content.ReadAsStringAsync()).ShouldContain("no-disponible-en-sandbox");
        }
        (await EnviarAsync(HttpMethod.Post, "/api/v1/api-keys", g.Prueba.Valor!, null, new { })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Sin sandbox:administrar no se consulta el buzón ni se reinicia.
        var sinPermiso = (await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", g.Admin, null,
            new CrearApiKeyDto("Solo vínculos", ["vinculos:leer"], null, null, Sandbox: true))).Valor!;
        (await EnviarAsync(HttpMethod.Get, "/api/v1/sandbox/correos", sinPermiso, null, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await EnviarAsync(HttpMethod.Post, "/api/v1/sandbox/reiniciar", sinPermiso, null, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ApiAsync<PaginaDto<VinculoDto>>(HttpMethod.Get, "/api/v1/vinculos", sinPermiso, copia.Id, null)).Datos.ShouldBeEmpty();

        // Revocar una llave de prueba (en producción) la corta: el sandbox no vuelve a responder con ella.
        (await EnviarAsync(HttpMethod.Delete, $"/api/v1/api-keys/{g.Prueba.Id}", g.Admin, null, null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await EnviarAsync(HttpMethod.Get, "/api/v1/sandbox", g.Prueba.Valor!, null, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---------- identidad delegada ----------

    [Fact]
    public async Task Con_identidad_delegada_la_persona_nace_administradora_del_sandbox_y_su_llave_la_acota()
    {
        var g = await PrepararAsync();
        var copia = (await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync()).Empresas.Single();
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", g.ClaveSistema.ExportPkcs8PrivateKeyPem());
        var persona = new UsuarioDelegado("srm-nueva", "Nueva Persona", $"nueva.{Guid.NewGuid():N}@cliente.co");
        var cliente = new ClienteValidasoft(Host.Server.CreateClient(),
            new OpcionesValidasoft { ApiKey = g.Prueba.Valor!, EmpresaId = copia.Id, Firmante = firmante, Reintentos = 0 }).ComoUsuario(persona);

        // En producción esa persona no tendría roles; en el sandbox es administradora (acotada por los scopes de la llave).
        var nit = NitNuevo();
        var vinculo = await cliente.Vinculos.CrearAsync(NuevoVinculo(nit, $"t{Guid.NewGuid():N}@sandbox.co", "EMPLEADO"), "dlg-" + nit);
        (await cliente.Expedientes.AbrirAsync(vinculo.Id, claveIdempotencia: "dlg-exp-" + nit)).Estado.ShouldBe("Aprobado");
        (await cliente.Vinculos.BloquearAsync(vinculo.Id, new BloquearVinculoDto("Prueba del integrador", null))).Estado.ShouldBe("Bloqueado");
        var sesion = await cliente.Sesiones.CrearAsync();
        sesion.Token.ShouldNotBeNullOrEmpty();

        // El usuario existe solo en el sandbox: producción no lo conoce.
        var usuariosReales = await ApiAsync<List<UsuarioDto>>(HttpMethod.Get, "/api/v1/usuarios", g.Admin, null, null);
        usuariosReales.ShouldNotContain(u => u.Correo == persona.Correo);
    }

    // ---------- reinicio ----------

    [Fact]
    public async Task Reiniciar_el_sandbox_borra_lo_hecho_en_el_conserva_empresas_y_llaves_y_no_toca_produccion()
    {
        var g = await PrepararAsync();
        var (vinculoReal, nit) = await CertificadoEnProduccionAsync(g);
        var inicial = await ClienteDe(g.Prueba.Valor!, null).Sandbox.ObtenerAsync();
        var copia = inicial.Empresas.Single();
        var sandbox = ClienteDe(g.Prueba.Valor!, copia.Id);

        var (vinculoDePrueba, _) = await CertificadoEnSandboxAsync(g, copia, nit);
        var correo = $"reinicio.{Guid.NewGuid():N}@sandbox.co";
        await sandbox.Vinculos.CrearAsync(NuevoVinculo(NitNuevo(), correo, "CLIENTE"), "rei-" + Guid.NewGuid());
        await sandbox.Sandbox.EsperarCorreoAsync(correo);
        var previos = await EsperarEventosAsync(sandbox, vinculoDePrueba.Id);

        var reiniciado = await sandbox.Sandbox.ReiniciarAsync();
        reiniciado.ReiniciadoEn.ShouldNotBeNull();
        (reiniciado.TenantId, reiniciado.Correos).ShouldBe((inicial.TenantId, 0));
        reiniciado.Empresas.Single().Id.ShouldBe(copia.Id); // las empresas conservan su id: el X-Empresa del integrador sigue valiendo
        reiniciado.Sistemas.ShouldBe(["SRM"]);

        // La llave de prueba sigue valiendo, el sandbox quedó vacío —también el tercero de pruebas— y con la configuración sembrada.
        (await ApiAsync<PaginaDto<VinculoDto>>(HttpMethod.Get, "/api/v1/vinculos", g.Prueba.Valor!, copia.Id, null)).Datos.ShouldBeEmpty();
        (await EnviarAsync(HttpMethod.Get, $"/api/v1/vinculos/{vinculoDePrueba.Id}", g.Prueba.Valor!, copia.Id, null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await sandbox.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", nit, "EMPLEADO"))).Datos!.EstadoVinculo.ShouldBe("SinVinculo");
        (await sandbox.Eventos.ListarAsync()).Datos.ShouldNotContain(e => previos.Contains(e.Id));
        (await ApiAsync<List<FlujoDto>>(HttpMethod.Get, "/api/v1/flujos", g.Prueba.Valor!, copia.Id, null)).ShouldContain(f => f.Codigo.StartsWith("EMP_", StringComparison.Ordinal));
        var (otroVinculo, _) = await CertificadoEnSandboxAsync(g, copia, nit); // el mismo NIT se vuelve a crear de cero
        otroVinculo.TerceroId.ShouldNotBe(vinculoDePrueba.TerceroId);

        // Producción, intacta.
        var real = (await ClienteDe(g.Produccion.Valor!, g.Empresa.Id).Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", nit, "EMPLEADO"))).Datos!;
        (real.VinculoId, real.TerceroId, real.Estado).ShouldBe((vinculoReal.Id, vinculoReal.TerceroId, "Vigente"));

        // Reiniciar un grupo sin sandbox: 404 (la llave de producción no lo crea al reiniciar).
        var otro = await PrepararGrupoSinSandboxAsync();
        (await Should.ThrowAsync<ExcepcionValidasoft>(() => ClienteDe(otro, null).Sandbox.ReiniciarAsync())).Codigo.ShouldBe("sandbox-no-existe");
        var creado = await ClienteDe(otro, null).Sandbox.AsegurarAsync();
        (creado.Existe, creado.Empresas.Count).ShouldBe((true, 1));
        (await ClienteDe(otro, null).Sandbox.AsegurarAsync()).TenantId.ShouldBe(creado.TenantId); // idempotente
    }

    // ---------- preparación ----------

    /// <summary>
    /// Un grupo de producción con la empresa Norte, el sistema SRM, un flujo de EMPLEADO con un área automática (abrir el expediente
    /// lo aprueba y emite el certificado), una llave de producción del SRM y su llave de prueba.
    /// </summary>
    private async Task<Grupo> PrepararAsync()
    {
        var slug = "sbx" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>(HttpMethod.Post, "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto(slug, "Grupo Sandbox", "co", "America/Bogota"));
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var sufijo = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var empresa = await ApiAsync<EmpresaDto>(HttpMethod.Post, "/api/v1/empresas", admin, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "K" + sufijo, null, null));
        var clave = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>(HttpMethod.Post, "/api/v1/sistemas", admin, null,
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", clave.ExportSubjectPublicKeyInfoPem()));
        var automatica = await ApiAsync<AreaDto>(HttpMethod.Post, "/api/v1/areas", admin, empresa.Id, new CrearAreaDto("AUTO_" + sufijo, "Validación automática"));
        var flujo = await ApiAsync<FlujoDto>(HttpMethod.Post, "/api/v1/flujos", admin, empresa.Id,
            new CrearFlujoDto("EMP_" + sufijo, "Empleados", "EMPLEADO", [new EtapaDto(1, [new AreaDeEtapaDto(automatica.Id, Automatica: true)])]));
        await ApiAsync<FlujoDto>(HttpMethod.Post, $"/api/v1/flujos/{flujo.Id}/publicar", admin, empresa.Id, new { });

        var produccion = await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", admin, null,
            new CrearApiKeyDto("SRM producción", ScopesDelIntegrador, null, null, SistemaId: sistema.Id));
        // La llave de prueba se emite como cualquier otra: con Sandbox = true. Eso crea el sandbox.
        var prueba = await ApiAsync<ApiKeyDto>(HttpMethod.Post, "/api/v1/api-keys", admin, null,
            new CrearApiKeyDto("SRM sandbox", ScopesDelIntegrador, null, null, Sandbox: true, SistemaId: sistema.Id));
        return new Grupo(admin, empresa, sistema, clave, produccion, prueba);
    }

    private async Task<string> PrepararGrupoSinSandboxAsync()
    {
        var tenant = await ApiAsync<TenantCreadoDto>(HttpMethod.Post, "/api/v1/plataforma/tenants", ApiFixture.ClavePlataforma, null,
            new CrearTenantDto("sbx" + Guid.NewGuid().ToString("N")[..10], "Grupo sin sandbox", "co", "America/Bogota"));
        await ApiAsync<EmpresaDto>(HttpMethod.Post, "/api/v1/empresas", tenant.ApiKeyAdministrador.Valor!, null,
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Solitaria S.A.S.", "SOL", null, null));
        return tenant.ApiKeyAdministrador.Valor!;
    }

    private async Task<(VinculoDto Vinculo, string Nit)> CertificadoEnProduccionAsync(Grupo g) =>
        await CertificarAsync(ClienteDe(g.Produccion.Valor!, g.Empresa.Id), NitNuevo());

    private async Task<(VinculoDto Vinculo, string Nit)> CertificadoEnSandboxAsync(Grupo g, EmpresaDeSandboxDto copia, string nit) =>
        await CertificarAsync(ClienteDe(g.Prueba.Valor!, copia.Id), nit);

    private static async Task<(VinculoDto Vinculo, string Nit)> CertificarAsync(ClienteValidasoft cliente, string nit)
    {
        var vinculo = await cliente.Vinculos.CrearAsync(NuevoVinculo(nit, $"t{Guid.NewGuid():N}@prueba.co", "EMPLEADO"), "cert-" + Guid.NewGuid());
        (await cliente.Expedientes.AbrirAsync(vinculo.Id, claveIdempotencia: "exp-" + Guid.NewGuid())).Estado.ShouldBe("Aprobado");
        return (vinculo, nit);
    }

    private static CrearVinculoDto NuevoVinculo(string nit, string correo, string tipo) =>
        new(null, new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Servicios " + nit + " S.A.S.", null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            tipo, null, correo, new ReferenciaExternaDto("SRM", "ADP-" + nit + "-" + tipo));

    /// <summary>Espera a que la suscripción publique los eventos del vínculo (después del commit) y devuelve los ids de lo publicado.</summary>
    private static async Task<HashSet<Guid>> EsperarEventosAsync(ClienteValidasoft cliente, Guid vinculoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var eventos = (await cliente.Eventos.ListarAsync()).Datos;
            if (eventos.Any(e => e.Tipo == "certificado.emitido" && e.Datos.ValueKind == JsonValueKind.Object
                                 && e.Datos.TryGetProperty("vinculoId", out var id) && id.GetGuid() == vinculoId))
                return [.. eventos.Select(e => e.Id)];
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("Los eventos del sandbox no llegaron a tiempo.");
            await Task.Delay(250);
        }
    }

    private static string NitNuevo() => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private ClienteValidasoft ClienteDe(string llave, Guid? empresa) =>
        new(Host.Server.CreateClient(), new OpcionesValidasoft { ApiKey = llave, EmpresaId = empresa, Reintentos = 0 });

    private async Task<HttpResponseMessage> PublicoAsync(HttpMethod metodo, string url, object? cuerpo = null)
    {
        using var http = Host.Server.CreateClient();
        var peticion = new HttpRequestMessage(metodo, url);
        if (cuerpo is not null)
            peticion.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
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

    private async Task<T> ApiAsync<T>(HttpMethod metodo, string url, string llave, Guid? empresa, object? cuerpo)
    {
        var respuesta = await EnviarAsync(metodo, url, llave, empresa, cuerpo);
        var texto = await respuesta.Content.ReadAsStringAsync();
        respuesta.IsSuccessStatusCode.ShouldBeTrue($"{metodo} {url} → {(int)respuesta.StatusCode}: {texto}");
        return JsonSerializer.Deserialize<T>(texto, Json)!;
    }
}
