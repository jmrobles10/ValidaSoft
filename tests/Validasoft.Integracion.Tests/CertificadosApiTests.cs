using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Certificado verificable (H5): un expediente aprobado emite un certificado una sola vez (misma transacción que
/// <c>VinculoActivado</c>) con su PDF en MinIO; la verificación pública devuelve un contenido canónico cuyo SHA-256 se
/// recalcula y cuyo sello verifica con la clave de <c>.well-known</c>; bloquear suspende, desbloquear restablece, revocar
/// es definitivo y anular el vínculo revoca por el sistema; la vista pública no expone revisores ni contactos.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class CertificadosApiTests(ApiFixture fixture)
{
    private static readonly HttpClient Http = new();

    private static readonly string[] ScopesDeSistema =
    [
        "configuracion:leer", "vinculos:leer", "flujos:leer", "expedientes:leer", "expedientes:revisar", "expedientes:administrar"
    ];

    private IAlbaHost Host => fixture.Host;

    private IDocumentStore Store => Host.Services.GetRequiredService<IDocumentStore>();

    private sealed record Persona(UsuarioDto Usuario, string Sub);

    private sealed record Escenario(string Admin, EmpresaDto Empresa, EmpresaDto Otra, string LlaveSrm, RSA ClaveSrm, AreaDto Cumplimiento,
        Persona Laura);

    // ---------- emisión, PDF y verificación pública ----------

    [Fact]
    public async Task Un_expediente_aprobado_emite_un_certificado_verificable_una_sola_vez_con_su_pdf()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Cumplimiento.Id)])]);
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", "contacto.privado@aceros.co");

        // Antes de aprobar no hay certificado.
        var antes = await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e), null, 200);
        antes.Actual.ShouldBeNull();
        antes.Emisiones.ShouldBeEmpty();

        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        aprobado.Estado.ShouldBe("Aprobado");

        var delVinculo = await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e), null, 200);
        var certificado = delVinculo.Actual.ShouldNotBeNull();
        var anio = DateTimeOffset.UtcNow.Year;
        certificado.FolioRaiz.ShouldBe($"{e.Empresa.PrefijoFolio}-C-{anio}-000001");
        certificado.Folio.ShouldBe($"{e.Empresa.PrefijoFolio}-C-{anio}-000001-1");
        certificado.Estado.ShouldBe("Vigente");
        certificado.Bloqueante.ShouldBeFalse();
        certificado.EsLaActual.ShouldBeTrue();
        certificado.ExpedienteFolio.ShouldBe(expediente.Folio);
        certificado.Tercero.IdentificacionEnmascarada.ShouldStartWith("NIT 9");
        certificado.Tercero.IdentificacionEnmascarada.ShouldContain("*");
        certificado.Flujo.Version.ShouldBe(1);
        certificado.Requisitos.ShouldBeEmpty();
        var area = certificado.Areas.ShouldHaveSingleItem();
        area.Codigo.ShouldBe("OFICIAL_CUMPLIMIENTO");
        area.Origen.ShouldBe("Humano");
        certificado.Huella.ShouldBe($"{certificado.HashContenido[..4]}·{certificado.HashContenido[4..8]}·{certificado.HashContenido[8..12]}");
        certificado.UrlVerificacion.ShouldBe($"{ApiFixture.UrlPublica}/verificar/{certificado.Folio}?huella={certificado.HashContenido[..12]}");
        delVinculo.Emisiones.ShouldHaveSingleItem().Folio.ShouldBe(certificado.Folio);

        // Emitido una sola vez, en la misma transacción que VinculoActivado; volver a emitir para el mismo expediente no hace nada.
        var tenant = e.Empresa.TenantId.ToString("N");
        await using (var sesion = Store.LightweightSession(tenant))
        {
            var emisor = Host.Services.GetRequiredService<EmisorDeCertificados>();
            await emisor.EmitirAsync(sesion, (await sesion.LoadAsync<Expediente>(expediente.Id))!, (await sesion.LoadAsync<Vinculo>(vinculo.Id))!,
                DateTimeOffset.UtcNow, CancellationToken.None);
            await sesion.SaveChangesAsync();
        }

        await using (var consulta = Store.QuerySession(tenant))
        {
            var eventos = await consulta.Events.FetchStreamAsync(certificado.Id);
            eventos.Count(x => x.Data is CertificadoEmitido).ShouldBe(1);
            var activado = (await consulta.Events.FetchStreamAsync(vinculo.Id)).Single(x => x.Data is VinculoActivado);
            eventos[0].Timestamp.ShouldBe(activado.Timestamp, TimeSpan.FromSeconds(1));
        }

        // El PDF llega después del commit (procesador de certificados) y queda en MinIO con su hash.
        var conPdf = await EsperarPdfAsync(e, certificado.Folio);
        Certificado guardado;
        await using (var consulta = Store.QuerySession(tenant))
            guardado = (await consulta.LoadAsync<Certificado>(certificado.Id))!;
        var pdf = guardado.Actual.Pdf.ShouldNotBeNull();
        pdf.Bucket.ShouldBe($"tenant-{e.Empresa.TenantId:N}");
        var almacen = Host.Services.GetRequiredService<IAlmacenDeArchivos>();
        byte[] bytes;
        await using (var flujo = await almacen.AbrirLecturaAsync(pdf.Bucket, pdf.Clave, CancellationToken.None))
        using (var memoria = new MemoryStream())
        {
            await flujo.CopyToAsync(memoria);
            bytes = memoria.ToArray();
        }

        Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(conPdf.Pdf!.Sha256);
        conPdf.Pdf.Tamano.ShouldBe(bytes.LongLength);
        Encoding.Latin1.GetString(bytes).ShouldContain(certificado.Folio);
        Encoding.Latin1.GetString(bytes).ShouldContain(certificado.HashContenido);

        // Regenerarlo da los mismos bytes y no registra otro PDF.
        var archivo = Host.Services.GetRequiredService<ArchivoDeCertificados>();
        (await archivo.AsegurarPdfAsync(e.Empresa.TenantId, certificado.Id, 1, CancellationToken.None))!.Sha256.ShouldBe(pdf.Sha256);
        var generador = Host.Services.GetRequiredService<IGeneradorDePdfDeCertificados>();
        var emision = guardado.Actual;
        Convert.ToHexStringLower(SHA256.HashData(generador.Generar(new DatosDelPdf(ContenidoDeCertificado.Leer(emision.ContenidoCanonico), emision.Folio,
            emision.HashContenido, emision.Sello, emision.Kid, emision.UrlVerificacion)))).ShouldBe(pdf.Sha256);

        // Descarga auditada con URL prefirmada.
        var descarga = await ApiAsync<DescargaDto>("POST", $"/api/v1/certificados/{certificado.Folio}/descarga", Admin(e), null, 200);
        descarga.Nombre.ShouldBe($"{certificado.Folio}.pdf");
        descarga.Mime.ShouldBe("application/pdf");
        (await Http.GetByteArrayAsync(descarga.Url)).ShouldBe(bytes);
        (await ApiAsync<CertificadoDto>("GET", $"/api/v1/certificados/{certificado.FolioRaiz}", Admin(e), null, 200)).Descargas.ShouldBe(1);
        await using (var consulta = Store.QuerySession(tenant))
        {
            var eventos = await consulta.Events.FetchStreamAsync(certificado.Id);
            eventos.Count(x => x.Data is PdfDeCertificadoGenerado).ShouldBe(1);
            var acceso = eventos.Select(x => x.Data).OfType<CertificadoDescargado>().ShouldHaveSingleItem();
            acceso.ActorTipo.ShouldBe("Integracion");
            acceso.Publico.ShouldBeFalse();
        }

        // Otra empresa del tenant no lo ve; sin certificados:leer, 403.
        await ApiAsync("GET", $"/api/v1/certificados/{certificado.Folio}", new Credencial(e.Admin, e.Otra.Id), null, 404);
        await ApiAsync("GET", $"/api/v1/certificados/{certificado.Folio}", new Credencial(e.LlaveSrm, e.Empresa.Id), null, 403);

        // ---------- verificación pública ----------
        var respuesta = await PublicoAsync("GET", $"/public/v1/verificar/{certificado.Folio}", null, 200);
        var texto = await respuesta.ReadAsTextAsync();
        var publico = JsonSerializer.Deserialize<CertificadoPublicoDto>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        publico.Folio.ShouldBe(certificado.Folio);
        publico.Estado.ShouldBe("Vigente");
        publico.Bloqueante.ShouldBeFalse();
        publico.PdfDescargable.ShouldBeTrue();
        publico.Titular.Nombre.ShouldBe(certificado.Tercero.Nombre);
        publico.Titular.IdentificacionEnmascarada.ShouldBe(certificado.Tercero.IdentificacionEnmascarada);
        publico.Emisor.Empresa.ShouldBe("Norte S.A.S.");
        publico.Huella.ShouldBe(certificado.Huella);

        // El cliente recalcula: SHA-256(JCS(contenidoCanonico)) = hashContenido, y el sello verifica con la clave publicada.
        var recalculado = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Jcs.Canonicalizar(publico.ContenidoCanonico))));
        recalculado.ShouldBe(publico.HashContenido);
        recalculado.ShouldBe(certificado.HashContenido);
        var claves = (await (await Host.Scenario(s =>
        {
            s.Get.Url("/.well-known/validasoft-certificados.json");
            s.StatusCodeShouldBe(200);
        })).ReadAsJsonAsync<ClavesDeCertificadosDto>())!;
        claves.Algoritmo.ShouldBe("Ed25519");
        var clave = claves.Claves.Single(c => c.Kid == publico.Firma.Kid);
        clave.Vigente.ShouldBeTrue();
        clave.RetiradaEn.ShouldBeNull(); // la vigente no tiene fecha límite (CL-2)
        clave.VerificableHasta.ShouldBeNull();
        publico.Firma.ClavePublicaUrl.ShouldBe($"{ApiFixture.UrlPublica}/.well-known/validasoft-certificados.json");
        var publica = Convert.FromBase64String(clave.ClavePublica);
        Convert.FromBase64String(clave.Jwk.X.Replace('-', '+').Replace('_', '/') + "=").ShouldBe(publica);
        SelloEd25519.Verificar(publica, Convert.FromHexString(recalculado), Convert.FromBase64String(publico.Firma.Valor)).ShouldBeTrue();

        // Sin revisores ni contactos: ni el nombre, el correo o el id de quien aprobó, ni el contacto del tercero.
        foreach (var prohibido in new[]
                 {
                     e.Laura.Usuario.Nombre, e.Laura.Usuario.Correo, e.Laura.Usuario.Id.ToString(), e.Laura.Sub, "contacto.privado@aceros.co",
                     "Ana Gómez", "revisorId", "contactos"
                 })
            texto.ShouldNotContain(prohibido, Case.Insensitive);

        // Folio raíz y huella (con o sin separadores) llevan a la misma emisión; otra huella o un folio inexistente, 404.
        (await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{certificado.FolioRaiz}?huella={Uri.EscapeDataString(certificado.Huella)}", 200))
            .HashContenido.ShouldBe(certificado.HashContenido);
        await PublicoAsync("GET", $"/public/v1/verificar/{certificado.Folio}?huella=000000000000", null, 404);
        (await CodigoAsync(await PublicoAsync("GET", $"/public/v1/verificar/{e.Empresa.PrefijoFolio}-C-{anio}-999999", null, 404)))
            .ShouldBe("certificado-no-encontrado");
        (await CodigoAsync(await PublicoAsync("GET", "/public/v1/verificar/no-es-un-folio", null, 404))).ShouldBe("certificado-no-encontrado");
        (await CodigoAsync(await PublicoAsync("GET", $"/public/v1/verificar/{certificado.Folio}?huella=xyz", null, 422))).ShouldBe("huella-invalida");

        // La copia pública del PDF de un certificado vigente se entrega y queda auditada como pública.
        var publicaPdf = await PublicoAsync<DescargaDto>("POST", $"/public/v1/verificar/{certificado.Folio}/descarga", 200);
        (await Http.GetByteArrayAsync(publicaPdf.Url)).ShouldBe(bytes);
    }

    [Fact]
    public async Task Bloquear_suspende_desbloquear_restablece_y_revocar_es_definitivo()
    {
        var e = await PrepararAsync();
        var vinculo = await VinculoAprobadoAutomaticamenteAsync(e);
        var actual = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e), null, 200)).Actual!;
        actual.Areas.ShouldHaveSingleItem().Origen.ShouldBe("Sistema");
        var folio = actual.Folio;

        // Bloquear ⇒ Suspendido y bloqueante (no se revoca); la copia pública del PDF ya no se entrega.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e), new BloquearVinculoDto("Sanción contractual", null), 200);
        var suspendido = await ApiAsync<CertificadoDto>("GET", $"/api/v1/certificados/{folio}", Admin(e), null, 200);
        suspendido.Estado.ShouldBe("Suspendido");
        suspendido.Bloqueante.ShouldBeTrue();
        suspendido.Suspension!.Motivo.ShouldContain("Sanción contractual");
        var publico = await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{folio}", 200);
        publico.Estado.ShouldBe("Suspendido");
        publico.Bloqueante.ShouldBeTrue();
        publico.PdfDescargable.ShouldBeFalse();
        publico.ContenidoCanonico.GetRawText().ShouldNotContain("Sanción"); // el bloqueo es privado: no cambia el contenido sellado
        (await CodigoAsync(await PublicoAsync("POST", $"/public/v1/verificar/{folio}/descarga", null, 409))).ShouldBe("certificado-no-vigente");

        // Desbloquear ⇒ restablecido a Vigente con el mismo hash.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desbloquear", Admin(e), new JustificacionDto("Sanción cumplida"), 200);
        var restablecido = await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{folio}", 200);
        restablecido.Estado.ShouldBe("Vigente");
        restablecido.Bloqueante.ShouldBeFalse();
        restablecido.HashContenido.ShouldBe(actual.HashContenido);

        // Revocar: solo con expedientes:administrar y justificación; luego es definitivo.
        await ApiAsync("POST", $"/api/v1/certificados/{folio}/revocar", Delegado(e, e.Laura), new RevocarCertificadoDto("Fraude"), 403);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/certificados/{folio}/revocar", Admin(e), new RevocarCertificadoDto("  "), 422)))
            .ShouldBe("justificacion-requerida");
        var revocado = await ApiAsync<CertificadoDto>("POST", $"/api/v1/certificados/{folio}/revocar", Admin(e),
            new RevocarCertificadoDto("Fraude confirmado en la cámara de comercio"), 200);
        revocado.Estado.ShouldBe("Revocado");
        revocado.Bloqueante.ShouldBeTrue();
        revocado.Revocacion!.Origen.ShouldBe("Administrador");
        revocado.Revocacion.Motivo.ShouldBe("Fraude confirmado en la cámara de comercio");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/certificados/{folio}/revocar", Admin(e), new RevocarCertificadoDto("Otra vez"), 409)))
            .ShouldBe("certificado-revocado");

        // Bloquear y desbloquear después no lo resucitan.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(e), new BloquearVinculoDto("Otra sanción", null), 200);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desbloquear", Admin(e), new JustificacionDto("Fin"), 200);
        var final = await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{folio}", 200);
        final.Estado.ShouldBe("Revocado");
        final.Bloqueante.ShouldBeTrue();
    }

    [Fact]
    public async Task Un_requisito_eximido_queda_en_el_certificado_y_anular_el_vinculo_lo_revoca_por_el_sistema()
    {
        var e = await PrepararAsync();
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e), new CrearFlujoDto("HAB_PROV_" + Sufijo(), "Habilitación de proveedores",
            "PROVEEDOR", [new EtapaDto(1, [new AreaDeEtapaDto(e.Cumplimiento.Id)])], Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200);
        var vinculo = await CrearVinculoAsync(e, "PROVEEDOR", $"t{Guid.NewGuid():N}@prueba.co");
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        var rut = expediente.Requisitos!.Single(r => r.TipoDocumento.Codigo == "RUT");

        // Eximir el único requisito aprueba el área (origen Administrador) y el expediente ⇒ certificado.
        var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/requisitos/{rut.Id}/eximir", Admin(e),
            new JustificacionDto("Entidad pública exenta de RUT."), 200);
        aprobado.Estado.ShouldBe("Aprobado");

        var certificado = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e), null, 200)).Actual!;
        var requisito = certificado.Requisitos.ShouldHaveSingleItem();
        requisito.Codigo.ShouldBe("RUT");
        requisito.Eximido.ShouldBeTrue();
        requisito.Justificacion.ShouldBe("Entidad pública exenta de RUT.");
        requisito.Sha256.ShouldBeNull();
        certificado.Areas.ShouldHaveSingleItem().Origen.ShouldBe("Administrador");
        certificado.VigenteHasta.ShouldBeNull();
        certificado.ContenidoCanonico.GetProperty("requisitos")[0].GetProperty("justificacion").GetString().ShouldBe("Entidad pública exenta de RUT.");

        // Anular el vínculo revoca el certificado por el sistema, con el motivo.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/anular", Admin(e), new MotivoDto("Proveedor liquidado"), 200);
        var revocado = await ApiAsync<CertificadoDto>("GET", $"/api/v1/certificados/{certificado.Folio}", Admin(e), null, 200);
        revocado.Estado.ShouldBe("Revocado");
        revocado.Revocacion!.Origen.ShouldBe("Sistema");
        revocado.Revocacion.Motivo.ShouldBe("Vínculo anulado: Proveedor liquidado");
    }

    [Fact]
    public async Task Un_folio_repetido_en_dos_tenants_se_desambigua_con_la_huella()
    {
        var prefijo = "Z" + Sufijo();
        var uno = await PrepararAsync(prefijo);
        var dos = await PrepararAsync(prefijo);
        var deUno = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{(await VinculoAprobadoAutomaticamenteAsync(uno)).Id}/certificado",
            Admin(uno), null, 200)).Actual!;
        var deDos = (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{(await VinculoAprobadoAutomaticamenteAsync(dos)).Id}/certificado",
            Admin(dos), null, 200)).Actual!;
        deUno.Folio.ShouldBe(deDos.Folio);

        (await CodigoAsync(await PublicoAsync("GET", $"/public/v1/verificar/{deUno.Folio}", null, 409))).ShouldBe("folio-ambiguo");
        (await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{deUno.Folio}?huella={deUno.HashContenido[..12]}", 200))
            .HashContenido.ShouldBe(deUno.HashContenido);
        (await PublicoAsync<CertificadoPublicoDto>("GET", $"/public/v1/verificar/{deDos.Folio}?huella={Uri.EscapeDataString(deDos.Huella)}", 200))
            .HashContenido.ShouldBe(deDos.HashContenido);
        // Cada tenant ve el suyo por folio.
        (await ApiAsync<CertificadoDto>("GET", $"/api/v1/certificados/{deDos.Folio}", Admin(dos), null, 200)).Id.ShouldBe(deDos.Id);
    }

    // ---------- preparación ----------

    private async Task<Escenario> PrepararAsync(string? prefijo = null)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Grupo Norte", "co", "America/Bogota"), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        // Prefijo propio: el folio del certificado no choca con el de otras pruebas en la verificación pública (CE-8).
        var empresa = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", prefijo ?? "C" + Sufijo(), null, null), 201);
        var otra = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null), 201);

        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(admin),
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", rsa.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(admin),
            new CrearApiKeyDto("Integración SRM", ScopesDeSistema, null, null, SistemaId: sistema.Id), 201);

        var correo = $"laura.{Guid.NewGuid():N}@constructora.co";
        var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(admin),
            new CrearUsuarioDto("Laura Revisora Certificados", correo, null, [new RolAsignadoDto("OficialCumplimiento", empresa.Id)], null), 201);
        var laura = new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);

        var credencial = new Credencial(admin, empresa.Id);
        var cumplimiento = await ApiAsync<AreaDto>("POST", "/api/v1/areas", credencial, new CrearAreaDto("OFICIAL_CUMPLIMIENTO", "Oficial de cumplimiento"), 201);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{cumplimiento.Id}/miembros", credencial, new MiembrosAreaDto([laura.Usuario.Id]), 200);
        await ExigirCuentaDelProveedorAsync(admin, false);
        return new Escenario(admin, empresa, otra, llave.Valor!, rsa, cumplimiento, laura);
    }

    /// <summary>
    /// El tipo PROVEEDOR nace exigiendo cuenta designada: con ella, el expediente espera en revisión hasta que la empresa apruebe la cuenta (CB-4).
    /// Las pruebas que aprueban expedientes sin hablar de cuentas la quitan; las de la cuenta la vuelven a poner.
    /// </summary>
    private async Task ExigirCuentaDelProveedorAsync(string admin, bool exige)
    {
        var tipos = await ApiAsync<List<TipoVinculoDto>>("GET", "/api/v1/tipos-vinculo", new Credencial(admin), null, 200);
        var proveedor = tipos.Single(t => t.Codigo == "PROVEEDOR");
        await ApiAsync<TipoVinculoDto>("PUT", $"/api/v1/tipos-vinculo/{proveedor.Id}", new Credencial(admin),
            new CrearTipoVinculoDto(proveedor.Codigo, proveedor.Nombre, proveedor.AtributosPermitidos, exige, proveedor.ExigeOtpEnEnlace), 200);
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();

    private async Task CrearFlujoAsync(Escenario e, string tipo, IReadOnlyList<EtapaDto>? etapas)
    {
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e), new CrearFlujoDto("HAB_" + tipo + "_" + Sufijo(), "Habilitación " + tipo, tipo, etapas), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200);
    }

    /// <summary>Flujo con un área automática: abrir el expediente lo aprueba y emite el certificado en la misma petición.</summary>
    private async Task<VinculoDto> VinculoAprobadoAutomaticamenteAsync(Escenario e)
    {
        var automatica = await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(e), new CrearAreaDto("AUTO_" + Sufijo(), "Validación automática"), 201);
        await CrearFlujoAsync(e, "EMPLEADO", [new EtapaDto(1, [new AreaDeEtapaDto(automatica.Id, Automatica: true)])]);
        var vinculo = await CrearVinculoAsync(e, "EMPLEADO", $"t{Guid.NewGuid():N}@prueba.co");
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201))
            .Estado.ShouldBe("Aprobado");
        return vinculo;
    }

    private Task<VinculoDto> CrearVinculoAsync(Escenario e, string tipo, string correo)
    {
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(e), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Aceros " + nit + " S.A.S.", null,
                [new ContactoDto("Ana Gómez", correo, "3001234567", "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);
    }

    /// <summary>Espera a que el procesador de certificados genere el PDF de la emisión (cada segundo en las pruebas).</summary>
    private async Task<CertificadoDto> EsperarPdfAsync(Escenario e, string folio)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var certificado = await ApiAsync<CertificadoDto>("GET", $"/api/v1/certificados/{folio}", Admin(e), null, 200);
            if (certificado.Pdf is not null)
                return certificado;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El PDF de {folio} no se generó.");
            await Task.Delay(200);
        }
    }

    // ---------- utilidades HTTP ----------

    private sealed record Credencial(string ApiKey, Guid? Empresa = null, string? Delegado = null);

    private static Credencial Admin(Escenario e) => new(e.Admin, e.Empresa.Id);

    /// <summary>La persona actúa desde el SRM con una aserción de un solo uso firmada por el sistema (ADR-0011, SE-4).</summary>
    private static Credencial Delegado(Escenario e, Persona p)
    {
        var ahora = DateTime.UtcNow;
        var asercion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "SRM",
            Audience = "validasoft",
            Subject = new ClaimsIdentity([new Claim("sub", p.Sub), new Claim("name", p.Usuario.Nombre), new Claim("email", p.Usuario.Correo),
                new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora.AddSeconds(-5),
            NotBefore = ahora.AddSeconds(-5),
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(e.ClaveSrm), SecurityAlgorithms.RsaSha256)
        });
        return new Credencial(e.LlaveSrm, e.Empresa.Id, asercion);
    }

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<T> ApiAsync<T>(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        (await (await ApiAsync(metodo, url, credencial, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> ApiAsync(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", credencial.ApiKey);
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());
            if (credencial.Delegado is not null)
                s.WithRequestHeader("X-Usuario-Delegado", credencial.Delegado);
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });

    /// <summary>Sin credencial y desde una IP aleatoria (el límite de <c>/public/v1</c> es por IP).</summary>
    private Task<IScenarioResult> PublicoAsync(string metodo, string url, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
                new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> PublicoAsync<T>(string metodo, string url, int esperado) =>
        (await (await PublicoAsync(metodo, url, null, esperado)).ReadAsJsonAsync<T>())!;

    private static void Preparar(Scenario s, string metodo, string url, object? cuerpo)
    {
        switch (metodo)
        {
            case "GET":
                s.Get.Url(url);
                break;
            case "PUT":
                s.Put.Json(cuerpo ?? new { }).ToUrl(url);
                break;
            case "POST" when cuerpo is null:
                s.Post.Url(url);
                break;
            default:
                s.Post.Json(cuerpo!).ToUrl(url);
                break;
        }
    }
}
