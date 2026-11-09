using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Amazon.S3.Model;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Infraestructura.Almacenamiento;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Bóveda de documentos (H2, primera parte) con MinIO real: carga en dos pasos con URL prefirmada, verificación del
/// archivo en el servidor (hash, tamaño, magic bytes, PDF activo), compartición solo tras aceptar la invitación (B-1) y
/// descarga auditada por la empresa. E1 de docs/02 §11 con dos empresas de tenants distintos.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class BovedaApiTests(ApiFixture fixture)
{
    private static readonly HttpClient Http = new();

    private IAlbaHost Host => fixture.Host;

    private sealed record Empresa(string ApiKey, EmpresaDto Datos);

    /// <summary>Un tercero con vínculo CLIENTE en dos empresas de tenants distintos, cuyos flujos piden RUT, y su sesión.</summary>
    private sealed record Escenario(Empresa A, Empresa B, VinculoDto VinculoA, VinculoDto VinculoB, SesionTerceroDto Sesion, string Correo);

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    // ---------- E1 ----------

    [Fact]
    public async Task E1_un_rut_subido_una_vez_llega_a_cada_empresa_solo_al_aceptar_y_la_version_nueva_a_ambas()
    {
        var e = await EscenarioAsync();
        var rut = Pdf("RUT v1");

        // El tercero sube su RUT una sola vez, antes de aceptar ninguna invitación.
        var v1 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", rut.Length, "application/pdf", TipoCanonico: "RUT"), rut);
        v1.Duplicado.ShouldBeFalse();
        v1.Version.Estado.ShouldBe("Actual");
        v1.Version.N.ShouldBe(1);
        v1.Version.Sha256.ShouldBe(Sha256(rut));
        v1.Version.Paginas.ShouldBe(1);
        v1.Version.Hechos!.MimeReal.ShouldBe("application/pdf");
        v1.Version.Hechos.ConIa.ShouldBeFalse();
        v1.Compartidos.ShouldBeEmpty(); // nadie ha aceptado: nada se comparte (B-1)
        var documentoId = v1.Version.DocumentoId;

        // Antes de aceptar ninguna empresa lo ve (404, T-4).
        foreach (var (empresa, vinculo) in new[] { (e.A, e.VinculoA), (e.B, e.VinculoB) })
        {
            (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/documentos", empresa, null, 200)).ShouldBeEmpty();
            await EmpresaAsync("GET", $"/api/v1/vinculos/{vinculo.Id}/documentos/{documentoId}", empresa, null, 404);
            await EmpresaAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/documentos/{documentoId}/versiones/1/descarga", empresa, null, 404);
        }

        // El tercero ve su documento y que aún no está compartido con nadie.
        var boveda = await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", e.Sesion.AccessToken, null, 200);
        var enBoveda = boveda.Documentos.ShouldHaveSingleItem();
        enBoveda.Tipo.Codigo.ShouldBe("RUT");
        enBoveda.VersionActual!.N.ShouldBe(1);
        enBoveda.PorEmpresa.Count.ShouldBe(2);
        enBoveda.PorEmpresa.ShouldAllBe(p => !p.Compartido);

        // Acepta en A: se comparte la versión actual del RUT con A.
        var aceptadaA = await AceptarAsync(e.Sesion, e.VinculoA.Id);
        var compartidoA = aceptadaA.Compartidos.ShouldHaveSingleItem();
        compartidoA.DocumentoId.ShouldBe(documentoId);
        compartidoA.Tipo.ShouldBe("RUT");
        compartidoA.Version.ShouldBe(1);
        compartidoA.EmpresaId.ShouldBe(e.A.Datos.Id);

        var vistaA = (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoA.Id}/documentos", e.A, null, 200))
            .ShouldHaveSingleItem();
        vistaA.VersionActual.ShouldBe(1);
        var versionA = vistaA.VersionesCompartidas.ShouldHaveSingleItem();

        // A descarga: URL prefirmada que entrega el mismo binario y queda DocumentoAccedido con su empresa.
        var descarga = await EmpresaAsync<DescargaDto>("POST", $"/api/v1/vinculos/{e.VinculoA.Id}/documentos/{documentoId}/versiones/1/descarga",
            e.A, null, 200);
        descarga.Nombre.ShouldBe("rut.pdf");
        descarga.Mime.ShouldBe("application/pdf");
        (await Http.GetByteArrayAsync(descarga.Url)).ShouldBe(rut);

        var accesos = (await EventosAsync(documentoId)).OfType<DocumentoAccedido>().ToList();
        var acceso = accesos.ShouldHaveSingleItem();
        acceso.EmpresaId.ShouldBe(e.A.Datos.Id);
        acceso.VinculoId.ShouldBe(e.VinculoA.Id);
        acceso.Numero.ShouldBe(1);
        acceso.ActorTipo.ShouldBe("Integracion");

        // B (otro tenant) todavía no lo ve.
        (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos", e.B, null, 200)).ShouldBeEmpty();
        await EmpresaAsync("POST", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos/{documentoId}/versiones/1/descarga", e.B, null, 404);

        // Acepta en B: B ve la MISMA versión (mismo hash) y hay un solo objeto en MinIO.
        var aceptadaB = await AceptarAsync(e.Sesion, e.VinculoB.Id);
        aceptadaB.Compartidos.ShouldHaveSingleItem().EmpresaId.ShouldBe(e.B.Datos.Id);
        var versionB = (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos", e.B, null, 200))
            .ShouldHaveSingleItem().VersionesCompartidas.ShouldHaveSingleItem();
        versionB.DocumentoId.ShouldBe(versionA.DocumentoId);
        versionB.N.ShouldBe(versionA.N);
        versionB.Sha256.ShouldBe(versionA.Sha256);
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, $"terceros/{e.Sesion.TerceroId:N}/")).ShouldBe([$"terceros/{e.Sesion.TerceroId:N}/{Sha256(rut)}"]);
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, "cargas/")).ShouldBeEmpty(); // la zona temporal se limpia

        // Versión nueva: pasa a actual y se comparte con A y B en la misma confirmación.
        var rut2 = Pdf("RUT v2 con dirección nueva");
        var v2 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut-2026.pdf", rut2.Length, "application/pdf", TipoCanonico: "RUT"), rut2);
        v2.Version.N.ShouldBe(2);
        v2.Version.Estado.ShouldBe("Actual");
        v2.Version.Hechos!.Comparacion.ShouldBe("Distinta");
        v2.Compartidos.Select(c => c.VinculoId).ShouldBe([e.VinculoA.Id, e.VinculoB.Id], ignoreOrder: true);

        var documento = await TerceroAsync<DocumentoDto>("GET", $"/public/v1/boveda/documentos/{documentoId}", e.Sesion.AccessToken, null, 200);
        documento.VersionActual.ShouldBe(2);
        documento.Versiones.Select(v => v.Estado).ShouldBe(["Superada", "Actual"]);
        documento.PorEmpresa.ShouldAllBe(p => p.Compartido && p.VersionActualCompartida && p.Version == 2);

        var vistaA2 = (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoA.Id}/documentos", e.A, null, 200))
            .ShouldHaveSingleItem();
        vistaA2.VersionActual.ShouldBe(2);
        vistaA2.VersionesCompartidas.Select(v => v.N).ShouldBe([1, 2]);
        var vistaB2 = (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos", e.B, null, 200))
            .ShouldHaveSingleItem();
        vistaB2.VersionesCompartidas.Select(v => v.N).ShouldBe([1, 2]);

        // El historial del documento lleva al tercero como actor de la carga y de las comparticiones.
        var eventos = await EventosAsync(documentoId);
        eventos.OfType<VersionCompartida>().Count().ShouldBe(4);
        eventos.OfType<VersionActualCambiada>().Select(c => c.Numero).ShouldBe([1, 2]);
    }

    // ---------- dedupe y validaciones del archivo ----------

    [Fact]
    public async Task Subir_el_mismo_archivo_responde_duplicado_sin_crear_version()
    {
        var e = await EscenarioAsync();
        var rut = Pdf("RUT único");
        var v1 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", rut.Length, "application/pdf", TipoCanonico: "RUT"), rut);

        // Con el SHA-256 declarado, el duplicado se detecta en el paso 1 (200, sin URL).
        var temprano = await TerceroAsync<CargaPrefirmadaDto>("POST", "/public/v1/boveda/documentos", e.Sesion.AccessToken,
            new IniciarCargaDto("copia.pdf", rut.Length, "application/pdf", TipoCanonico: "RUT", Sha256: Sha256(rut)), 200);
        temprano.Duplicado.ShouldBeTrue();
        temprano.Version.ShouldBe(1);
        temprano.Carga.ShouldBeNull();

        // Sin declararlo, se detecta al confirmar.
        var tardio = await SubirAsync(e.Sesion, new IniciarCargaDto("otra-copia.pdf", rut.Length, "application/pdf", TipoCanonico: "RUT"), rut);
        tardio.Duplicado.ShouldBeTrue();
        tardio.Version.N.ShouldBe(1);

        var documento = await TerceroAsync<DocumentoDto>("GET", $"/public/v1/boveda/documentos/{v1.Version.DocumentoId}", e.Sesion.AccessToken, null, 200);
        documento.Versiones.ShouldHaveSingleItem();
        (await EventosAsync(v1.Version.DocumentoId)).OfType<VersionDuplicadaDetectada>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Un_archivo_con_extension_pdf_que_no_es_pdf_responde_415_y_uno_demasiado_grande_413()
    {
        var e = await EscenarioAsync();

        var falso = Encoding.UTF8.GetBytes("Esto no es un PDF, aunque se llame factura.pdf");
        var noPdf = await SubirAsync(e.Sesion, new IniciarCargaDto("factura.pdf", falso.Length, "application/pdf", TipoCanonico: "RUT"), falso,
            esperado: 415);
        (await CodigoAsync(noPdf)).ShouldBe("tipo-archivo-no-soportado");

        // Declara un tamaño válido pero sube más del máximo: lo detecta el servidor al confirmar.
        var grande = Pdf("grande", relleno: (int)ApiFixture.TamanoMaximo + 1024);
        var demasiado = await SubirAsync(e.Sesion, new IniciarCargaDto("grande.pdf", 1000, "application/pdf", TipoCanonico: "RUT"), grande,
            esperado: 413);
        (await CodigoAsync(demasiado)).ShouldBe("archivo-demasiado-grande");

        // Declarado demasiado grande o de un tipo no permitido ⇒ rechazado en el paso 1.
        await TerceroAsync("POST", "/public/v1/boveda/documentos", e.Sesion.AccessToken,
            new IniciarCargaDto("grande.pdf", ApiFixture.TamanoMaximo + 1, "application/pdf", TipoCanonico: "RUT"), 413);
        await TerceroAsync("POST", "/public/v1/boveda/documentos", e.Sesion.AccessToken,
            new IniciarCargaDto("hoja.xlsx", 1000, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", TipoCanonico: "RUT"), 415);

        // Un formulario aún no se carga.
        var formulario = await TerceroAsync("POST", "/public/v1/boveda/documentos", e.Sesion.AccessToken,
            new IniciarCargaDto("cuenta.pdf", 1000, "application/pdf", TipoCanonico: "CUENTA_BANCARIA"), 422);
        (await CodigoAsync(formulario)).ShouldBe("clase-no-soportada");

        // Nada de lo anterior creó versiones y la zona temporal quedó limpia.
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", e.Sesion.AccessToken, null, 200)).Documentos.ShouldAllBe(d => d.Versiones == 0);
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, $"terceros/{e.Sesion.TerceroId:N}/")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Un_pdf_con_javascript_queda_devuelto_con_motivo_y_no_se_comparte()
    {
        var e = await EscenarioAsync();
        await AceptarAsync(e.Sesion, e.VinculoA.Id);

        var malicioso = Pdf("con JS", javascript: true);
        var devuelta = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", malicioso.Length, "application/pdf", TipoCanonico: "RUT"), malicioso);

        devuelta.Version.Estado.ShouldBe("Devuelta");
        devuelta.Version.MotivoDevolucion.ShouldBe("pdf-con-contenido-activo");
        devuelta.Version.DetalleDevolucion!.ShouldContain("JavaScript");
        devuelta.Compartidos.ShouldBeEmpty();
        (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoA.Id}/documentos", e.A, null, 200)).ShouldBeEmpty();

        // Va a cuarentena, nunca a la clave content-addressed del tercero.
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, $"terceros/{e.Sesion.TerceroId:N}/")).ShouldBeEmpty();
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, $"cuarentena/{devuelta.Version.DocumentoId:N}/")).ShouldHaveSingleItem();

        // Una versión limpia posterior sí pasa a actual y se comparte con A, que ya aceptó.
        var limpio = Pdf("RUT limpio");
        var v2 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", limpio.Length, "application/pdf", TipoCanonico: "RUT"), limpio);
        v2.Version.N.ShouldBe(2);
        v2.Version.Estado.ShouldBe("Actual");
        v2.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(e.VinculoA.Id);
    }

    // ---------- ámbito PorVinculo, retiro y aislamiento ----------

    [Fact]
    public async Task Un_documento_por_vinculo_de_A_es_invisible_para_B()
    {
        var e = await EscenarioAsync();
        await AceptarAsync(e.Sesion, e.VinculoA.Id);
        await AceptarAsync(e.Sesion, e.VinculoB.Id);

        var poliza = Pdf("Póliza a favor de A");
        var cargada = await SubirAsync(e.Sesion,
            new IniciarCargaDto("poliza.pdf", poliza.Length, "application/pdf", TipoCanonico: "POLIZA", VinculoId: e.VinculoA.Id), poliza);
        cargada.Version.Estado.ShouldBe("Actual");
        cargada.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(e.VinculoA.Id);
        var documentoId = cargada.Version.DocumentoId;

        var deA = (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoA.Id}/documentos", e.A, null, 200))
            .ShouldHaveSingleItem();
        deA.Ambito.ShouldBe("PorVinculo");
        deA.Tipo.Codigo.ShouldBe("POLIZA");

        (await EmpresaAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos", e.B, null, 200)).ShouldBeEmpty();
        await EmpresaAsync("GET", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos/{documentoId}", e.B, null, 404);
        await EmpresaAsync("POST", $"/api/v1/vinculos/{e.VinculoB.Id}/documentos/{documentoId}/versiones/1/descarga", e.B, null, 404);

        // Vive en el bucket del tenant de A, bajo su empresa y vínculo (docs/02 §10.2).
        var bucket = ClavesDeAlmacen.BucketDeTenant(e.A.Datos.TenantId);
        (await ObjetosAsync(bucket, $"empresas/{e.A.Datos.Id:N}/vinculos/{e.VinculoA.Id:N}/")).ShouldBe(
            [ClavesDeAlmacen.PorVinculo(e.A.Datos.Id, e.VinculoA.Id, Sha256(poliza))]);

        // En la bóveda del tercero solo aparece con la empresa de A.
        var documento = await TerceroAsync<DocumentoDto>("GET", $"/public/v1/boveda/documentos/{documentoId}", e.Sesion.AccessToken, null, 200);
        documento.PorEmpresa.ShouldHaveSingleItem().VinculoId.ShouldBe(e.VinculoA.Id);
    }

    [Fact]
    public async Task Retirar_la_actual_restituye_la_anterior_y_solo_borra_el_binario_que_nadie_vio()
    {
        var e = await EscenarioAsync();
        await AceptarAsync(e.Sesion, e.VinculoA.Id);
        var rut1 = Pdf("v1");
        var v1 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", rut1.Length, "application/pdf", TipoCanonico: "RUT"), rut1);
        var documentoId = v1.Version.DocumentoId;

        // B no aceptó: la v2 solo se comparte con A; A no la descarga. Retirarla no borra el binario porque A la recibió.
        var rut2 = Pdf("v2");
        var v2 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", rut2.Length, "application/pdf", TipoCanonico: "RUT"), rut2);
        v2.Compartidos.ShouldHaveSingleItem();
        var retiro = await TerceroAsync<RetiroDto>("POST", $"/public/v1/boveda/documentos/{documentoId}/versiones/2/retirar", e.Sesion.AccessToken,
            new RetirarVersionDto("Subí el RUT de otra empresa"), 200);
        retiro.BinarioEliminado.ShouldBeFalse();
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, ClavesDeAlmacen.Global(e.Sesion.TerceroId, Sha256(rut2)))).ShouldHaveSingleItem();
        retiro.Documento.VersionActual.ShouldBe(1);
        retiro.Documento.Versiones.Select(v => v.Estado).ShouldBe(["Actual", "Retirada"]);

        // Retirar otra vez ⇒ 409.
        var otraVez = await TerceroAsync("POST", $"/public/v1/boveda/documentos/{documentoId}/versiones/2/retirar", e.Sesion.AccessToken,
            new RetirarVersionDto("de nuevo"), 409);
        (await CodigoAsync(otraVez)).ShouldBe("estado-invalido");

        // Un CEDULA que ninguna empresa exige: nadie lo vio, así que retirarlo borra el objeto.
        var cedula = Pdf("cédula");
        var subida = await SubirAsync(e.Sesion, new IniciarCargaDto("cedula.pdf", cedula.Length, "application/pdf", TipoCanonico: "CEDULA"), cedula);
        subida.Compartidos.ShouldBeEmpty();
        var retiroCedula = await TerceroAsync<RetiroDto>("POST",
            $"/public/v1/boveda/documentos/{subida.Version.DocumentoId}/versiones/1/retirar", e.Sesion.AccessToken, new RetirarVersionDto("error"), 200);
        retiroCedula.BinarioEliminado.ShouldBeTrue();
        retiroCedula.Documento.VersionActual.ShouldBeNull();
        (await ObjetosAsync(ClavesDeAlmacen.BucketBoveda, ClavesDeAlmacen.Global(e.Sesion.TerceroId, Sha256(cedula)))).ShouldBeEmpty();
    }

    [Fact]
    public async Task Un_tercero_ajeno_no_ve_la_boveda_de_otro()
    {
        var e = await EscenarioAsync();
        var rut = Pdf("RUT privado");
        var v1 = await SubirAsync(e.Sesion, new IniciarCargaDto("rut.pdf", rut.Length, "application/pdf", TipoCanonico: "RUT"), rut);
        var documentoId = v1.Version.DocumentoId;

        var ajeno = await EscenarioAsync();
        (await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", ajeno.Sesion.AccessToken, null, 200)).Documentos.ShouldBeEmpty();
        await TerceroAsync("GET", $"/public/v1/boveda/documentos/{documentoId}", ajeno.Sesion.AccessToken, null, 404);
        await TerceroAsync("POST", $"/public/v1/boveda/documentos/{documentoId}/versiones/1/descarga", ajeno.Sesion.AccessToken, null, 404);
        await TerceroAsync("POST", $"/public/v1/boveda/documentos/{documentoId}/versiones/1/retirar", ajeno.Sesion.AccessToken,
            new RetirarVersionDto("no es mío"), 404);

        // Su propio dueño sí lo descarga (y queda el acceso con el tercero como actor).
        var propia = await TerceroAsync<DescargaDto>("POST", $"/public/v1/boveda/documentos/{documentoId}/versiones/1/descarga",
            e.Sesion.AccessToken, null, 200);
        (await Http.GetByteArrayAsync(propia.Url)).ShouldBe(rut);
        var acceso = (await EventosAsync(documentoId)).OfType<DocumentoAccedido>().ShouldHaveSingleItem();
        acceso.ActorTipo.ShouldBe("Tercero");
        acceso.EmpresaId.ShouldBeNull();
    }

    // ---------- catálogo y tipos del tenant ----------

    [Fact]
    public async Task El_catalogo_y_los_tipos_del_tenant_respetan_el_ambito_y_quedan_auditados()
    {
        var a = await EmpresaNuevaAsync("Norte S.A.S.");

        var catalogo = await EmpresaAsync<List<TipoDocumentoCanonicoDto>>("GET", "/api/v1/catalogos/tipos-documento", a, null, 200);
        catalogo.Single(t => t.Codigo == "RUT").Ambito.ShouldBe("Global");
        catalogo.Single(t => t.Codigo == "POLIZA").Ambito.ShouldBe("PorVinculo");
        catalogo.Single(t => t.Codigo == "CUENTA_BANCARIA").CargaSoportada.ShouldBeFalse();

        // Semilla del tenant: seis canónicos de archivo (ya sin exigencias: lo que se pide lo dicen los flujos, RQ-3).
        var tipos = await EmpresaAsync<List<TipoDocumentoTenantDto>>("GET", "/api/v1/tipos-documento", a, null, 200);
        tipos.Select(t => t.Codigo).ShouldBe(["CAMARA_COMERCIO", "CEDULA", "CERTIFICACION_BANCARIA", "ESTADOS_FINANCIEROS", "POLIZA", "RUT"]);
        tipos.ShouldAllBe(t => t.Activo);
        tipos.Single(t => t.Codigo == "POLIZA").DiasAviso.ShouldBe([60, 30, 7]);

        // Un tipo propio sin canónico no puede ser Global; un canónico PorVinculo tampoco se amplía.
        var propio = await EmpresaAsync("POST", "/api/v1/tipos-documento", a, new CrearTipoDocumentoDto("FORMATO_NORTE", "Formato Norte", null, "Global"), 422);
        (await CodigoAsync(propio)).ShouldBe("ambito-no-ampliable");
        var poliza = tipos.Single(t => t.Codigo == "POLIZA");
        var ampliar = await EmpresaAsync("PUT", $"/api/v1/tipos-documento/{poliza.Id}", a, new ActualizarTipoDocumentoDto(Ambito: "Global"), 422);
        (await CodigoAsync(ampliar)).ShouldBe("ambito-no-ampliable");

        var creado = await EmpresaAsync<TipoDocumentoTenantDto>("POST", "/api/v1/tipos-documento", a,
            new CrearTipoDocumentoDto("FORMATO_NORTE", "Formato de vinculación Norte", null,
                ReglaFrescura: new ReglaFrescuraDto("MaxDiasDesdeExpedicion", 60)), 201);
        creado.Ambito.ShouldBe("PorVinculo");
        creado.ReglaFrescura.Dias.ShouldBe(60);

        // Restringir un canónico global a PorVinculo sí se permite.
        var rut = tipos.Single(t => t.Codigo == "RUT");
        var restringido = await EmpresaAsync<TipoDocumentoTenantDto>("PUT", $"/api/v1/tipos-documento/{rut.Id}", a,
            new ActualizarTipoDocumentoDto(Ambito: "PorVinculo"), 200);
        restringido.Ambito.ShouldBe("PorVinculo");

        var duplicado = await EmpresaAsync("POST", "/api/v1/tipos-documento", a, new CrearTipoDocumentoDto("FORMATO_NORTE", "Otro", null), 409);
        (await CodigoAsync(duplicado)).ShouldBe("tipo-documento-duplicado");

        // Cada cambio deja su registro de auditoría en la misma transacción.
        await using var consulta = fixture.Host.Services.GetRequiredService<IDocumentStore>().QuerySession(a.Datos.TenantId.ToString("N"));
        var registros = await consulta.Query<Dominio.Auditoria.RegistroDeCambioDeConfiguracion>()
            .Where(r => r.Entidad == "TipoDocumentoTenant")
            .ToListAsync();
        registros.Count.ShouldBe(8); // 6 de la semilla + creado + modificado
    }

    // ---------- utilidades ----------

    private async Task<Escenario> EscenarioAsync()
    {
        var a = await EmpresaNuevaAsync("Norte S.A.S.");
        var b = await EmpresaNuevaAsync("Sur Global S.A.S.");
        foreach (var empresa in new[] { a, b })
        {
            await PedirRutEnElFlujoAsync(empresa);
            await EmpresaAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{empresa.Datos.Id}/textos-legales/autorizacion", empresa,
                new PublicarTextoLegalDto($"Autorizo a {empresa.Datos.Nombre}"), 200, conEmpresa: false);
            await EmpresaAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{empresa.Datos.Id}/textos-legales/terminos", empresa,
                new PublicarTextoLegalDto($"Términos de {empresa.Datos.Nombre}"), 200, conEmpresa: false);
        }

        var correo = $"t{Guid.NewGuid():N}@prueba.co";
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var vinculoA = await CrearVinculoAsync(a, nit, correo);
        var vinculoB = await CrearVinculoAsync(b, nit, correo);
        vinculoB.TerceroId.ShouldBe(vinculoA.TerceroId);

        var invitacion = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains(a.Datos.Nombre));
        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{RegexToken().Match(invitacion.Texto).Groups[1].Value}/canjear", null, 200);
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
        sesion.Vinculos.Select(v => v.Id).ShouldBe([vinculoA.Id, vinculoB.Id], ignoreOrder: true);
        return new Escenario(a, b, vinculoA, vinculoB, sesion, correo);
    }

    private async Task<Empresa> EmpresaNuevaAsync(string nombre)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await EmpresaAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Empresa(ApiFixture.ClavePlataforma, null!),
            new CrearTenantDto(slug, "Grupo " + nombre, "co", "America/Bogota"), 201, conEmpresa: false);
        var clave = tenant.ApiKeyAdministrador.Valor!;
        var datos = await EmpresaAsync<EmpresaDto>("POST", "/api/v1/empresas", new Empresa(clave, null!),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), nombre, "EMP", null, null), 201, conEmpresa: false);
        return new Empresa(clave, datos);
    }

    /// <summary>
    /// La empresa pide el RUT (tipo de la semilla) a sus vínculos CLIENTE con el requisito de su flujo publicado (RQ-3;
    /// antes se configuraba como exigencia del tipo, BO-3). El flujo por defecto abre el expediente al aceptar.
    /// </summary>
    private async Task PedirRutEnElFlujoAsync(Empresa empresa)
    {
        await EmpresaAsync<AreaDto>("POST", "/api/v1/areas", empresa, new CrearAreaDto("REVISION", "Revisión"), 201);
        var flujo = await EmpresaAsync<FlujoDto>("POST", "/api/v1/flujos", empresa,
            new CrearFlujoDto("CLIENTES", "Habilitación de clientes", "CLIENTE", Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]), 201);
        var publicado = await EmpresaAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", empresa, null, 200);
        publicado.Publicada!.Requisitos.ShouldNotBeNull().ShouldHaveSingleItem().TipoDocumento.ShouldBe("RUT");
    }

    private Task<VinculoDto> CrearVinculoAsync(Empresa empresa, string nit, string correo) =>
        EmpresaAsync<VinculoDto>("POST", "/api/v1/vinculos", empresa, new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: "CLIENTE",
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);

    private async Task<InvitacionAceptadaDto> AceptarAsync(SesionTerceroDto sesion, Guid vinculoId)
    {
        var invitacion = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{vinculoId}", sesion.AccessToken, null, 200);
        invitacion.PuedeAceptar.ShouldBeTrue();
        return await TerceroAsync<InvitacionAceptadaDto>("POST", $"/public/v1/invitaciones/{vinculoId}/aceptar", sesion.AccessToken,
            new AceptarInvitacionDto([.. invitacion.TextosPorAceptar.Select(t => new ConsentimientoDto(t.Tipo, t.TextoVersion, true))]), 200);
    }

    /// <summary>Paso 1, PUT a la URL prefirmada (directo a MinIO, el API no ve los bytes) y paso 2.</summary>
    private async Task<ConfirmacionCargaDto> SubirAsync(SesionTerceroDto sesion, IniciarCargaDto inicio, byte[] contenido)
    {
        var resultado = await SubirAsync(sesion, inicio, contenido, 200);
        return (await resultado.ReadAsJsonAsync<ConfirmacionCargaDto>())!;
    }

    private async Task<IScenarioResult> SubirAsync(SesionTerceroDto sesion, IniciarCargaDto inicio, byte[] contenido, int esperado)
    {
        var carga = await TerceroAsync<CargaPrefirmadaDto>("POST", "/public/v1/boveda/documentos", sesion.AccessToken, inicio, 201);
        carga.Duplicado.ShouldBeFalse();
        carga.Carga!.Metodo.ShouldBe("PUT");
        carga.Carga.Expira.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

        using var put = new HttpRequestMessage(HttpMethod.Put, carga.Carga.Url) { Content = new ByteArrayContent(contenido) };
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(carga.Carga.Cabeceras["Content-Type"]);
        using var respuesta = await Http.SendAsync(put);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync());

        return await TerceroAsync("POST", $"/public/v1/boveda/documentos/{carga.DocumentoId}/versiones/{carga.Version}/confirmar",
            sesion.AccessToken, null, esperado);
    }

    private async Task<IReadOnlyList<object>> EventosAsync(Guid documentoId)
    {
        await using var consulta = fixture.Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda);
        return [.. (await consulta.Events.FetchStreamAsync(documentoId)).Select(e => e.Data)];
    }

    private async Task<IReadOnlyList<string>> ObjetosAsync(string bucket, string prefijo)
    {
        var s3 = fixture.Host.Services.GetRequiredService<FabricaClienteS3>().Interno;
        var lista = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = bucket, Prefix = prefijo });
        return [.. (lista.S3Objects ?? []).Select(o => o.Key)];
    }

    /// <summary>PDF mínimo válido de una página; el texto lo hace único (otro hash). Opcionalmente con JavaScript al abrir.</summary>
    private static byte[] Pdf(string texto, bool javascript = false, int relleno = 0)
    {
        var accion = javascript ? " /OpenAction << /S /JavaScript /JS (app.alert('hola')) >>" : string.Empty;
        var pdf = new StringBuilder()
            .Append("%PDF-1.4\n")
            .Append(System.Globalization.CultureInfo.InvariantCulture, $"% {texto} {Guid.NewGuid():N}\n")
            .Append(System.Globalization.CultureInfo.InvariantCulture, $"1 0 obj\n<< /Type /Catalog /Pages 2 0 R{accion} >>\nendobj\n")
            .Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n")
            .Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n")
            .Append(relleno > 0 ? "% " + new string('x', relleno) + "\n" : string.Empty)
            .Append("trailer\n<< /Root 1 0 R >>\n%%EOF\n");
        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static string Sha256(byte[] contenido) => Convert.ToHexStringLower(SHA256.HashData(contenido));

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private static void IpAleatoria(Scenario s) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
            new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    private Task<IScenarioResult> PublicoAsync(string metodo, string url, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private Task<IScenarioResult> TerceroAsync(string metodo, string url, string token, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("Authorization", "Bearer " + token);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> TerceroAsync<T>(string metodo, string url, string token, object? cuerpo, int esperado) =>
        (await (await TerceroAsync(metodo, url, token, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> EmpresaAsync(string metodo, string url, Empresa empresa, object? cuerpo, int esperado, bool conEmpresa = true) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", empresa.ApiKey);
            if (conEmpresa && empresa.Datos is not null)
                s.WithRequestHeader("X-Empresa", empresa.Datos.Id.ToString());
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> EmpresaAsync<T>(string metodo, string url, Empresa empresa, object? cuerpo, int esperado, bool conEmpresa = true) =>
        (await (await EmpresaAsync(metodo, url, empresa, cuerpo, esperado, conEmpresa)).ReadAsJsonAsync<T>())!;

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
