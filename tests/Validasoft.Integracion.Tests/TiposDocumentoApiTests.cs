using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Tipos de documento configurables (TD-1…TD-6) con PostgreSQL y MinIO reales: formato, peso y páginas por tipo, plantilla
/// descargable versionada, campos que llena el proveedor (formularios) y vigencia con aviso programado y vencimiento.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class TiposDocumentoApiTests(ApiFixture fixture)
{
    private static readonly HttpClient Http = new();
    private const long Mega = 1024 * 1024;

    private IAlbaHost Host => fixture.Host;

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex RegexCodigo();

    private sealed record Credencial(string ApiKey, Guid? Empresa = null);

    private sealed record Empresa(string Admin, EmpresaDto Datos, string Nombre);

    // ---------- formato, peso y páginas (TD-1) ----------

    [Fact]
    public async Task Un_tipo_solo_pdf_de_1_mb_rechaza_un_jpg_y_un_pdf_de_2_mb_con_su_limite_en_el_mensaje()
    {
        var norte = await EmpresaNuevaAsync("Constructora Norte", "CN");

        // Configuración: nunca más laxo que el canónico ni que el global (3 MB en las pruebas).
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/tipos-documento", Admin(norte),
            new CrearTipoDocumentoDto("CERT_GRANDE", "Certificado grande", null, TamanoMaximoMb: 10), 422))).ShouldBe("limite-sobre-el-global");
        var rut = (await ApiAsync<List<TipoDocumentoTenantDto>>("GET", "/api/v1/tipos-documento", Admin(norte), null, 200)).Single(t => t.Codigo == "RUT");
        rut.ReglasEfectivas!.Formatos.ShouldBe([InspectorDeArchivos.Pdf]); // el canónico sembrado: solo PDF
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/tipos-documento/{rut.Id}", Admin(norte),
            new ActualizarTipoDocumentoDto(FormatosPermitidos: ["pdf", "jpg"]), 422))).ShouldBe("formato-mas-laxo");

        var tipo = await ApiAsync<TipoDocumentoTenantDto>("POST", "/api/v1/tipos-documento", Admin(norte),
            new CrearTipoDocumentoDto("CERT_NORTE", "Certificado Norte", null, FormatosPermitidos: ["pdf"], TamanoMaximoMb: 1), 201);
        tipo.FormatosPermitidos.ShouldBe([InspectorDeArchivos.Pdf]);
        tipo.TamanoMaximoMb.ShouldBe(1);
        tipo.ReglasEfectivas!.Descripcion.ShouldBe("Certificado Norte admite solo PDF de hasta 1 MB");

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, correo);
        var sesion = await SesionAsync(correo);
        IniciarCargaDto Carga(string nombre, long tamano, string mime) =>
            new(nombre, tamano, mime, TipoDocumentoTenantId: tipo.Id, VinculoId: vinculo.Id);

        // Paso 1: tipo y tamaño declarados.
        var jpg = await TerceroAsync("POST", "/public/v1/boveda/documentos", sesion.AccessToken, Carga("foto.jpg", 1000, "image/jpeg"), 415);
        (await CodigoAsync(jpg)).ShouldBe("tipo-archivo-no-soportado");
        (await DetalleAsync(jpg)).ShouldBe("Certificado Norte admite solo PDF de hasta 1 MB.");
        var dosMegas = await TerceroAsync("POST", "/public/v1/boveda/documentos", sesion.AccessToken, Carga("grande.pdf", 2 * Mega, "application/pdf"), 413);
        (await CodigoAsync(dosMegas)).ShouldBe("archivo-demasiado-grande");
        (await DetalleAsync(dosMegas)).ShouldBe("Certificado Norte admite solo PDF de hasta 1 MB.");

        // Paso 2: declara poco y sube 2 MB ⇒ 413; declara PDF y sube un PNG ⇒ 415 (magic bytes).
        var grande = Pdf("grande", relleno: (int)(2 * Mega));
        await SubirAsync(sesion, Carga("grande.pdf", 1000, "application/pdf"), grande, 413);
        await SubirAsync(sesion, Carga("foto.pdf", 1000, "application/pdf"), Png(), 415);

        // Un PDF dentro del límite sí entra.
        var valido = await SubirAsync(sesion, Carga("cert.pdf", 1000, "application/pdf"), Pdf("certificado"), 200);
        (await valido.ReadAsJsonAsync<ConfirmacionCargaDto>())!.Version.Estado.ShouldBe("Actual");

        // El RUT (canónico global, solo PDF) en el contexto de este vínculo usa las reglas de su tipo.
        var rutJpg = await TerceroAsync("POST", "/public/v1/boveda/documentos", sesion.AccessToken,
            new IniciarCargaDto("rut.jpg", 1000, "image/jpeg", TipoCanonico: "RUT", VinculoId: vinculo.Id), 415);
        (await DetalleAsync(rutJpg)).ShouldStartWith("Registro Único Tributario admite solo PDF");
    }

    // ---------- plantilla (TD-5) ----------

    [Fact]
    public async Task La_plantilla_que_sube_la_empresa_la_descarga_el_tercero_de_su_vinculo_y_no_otro()
    {
        var norte = await EmpresaNuevaAsync("Constructora Plantillas", "CP");
        var tipo = await ApiAsync<TipoDocumentoTenantDto>("POST", "/api/v1/tipos-documento", Admin(norte),
            new CrearTipoDocumentoDto("FORMATO_VINC", "Formato de vinculación", "FORMATO_VINCULACION"), 201);
        await ApiAsync("GET", $"/api/v1/tipos-documento/{tipo.Id}/plantilla/descarga", Admin(norte), null, 404);

        // v1 en PDF y v2 en DOCX, en dos pasos; una imagen no es plantilla (415).
        var pdfV1 = Pdf("formato v1");
        var v1 = await SubirPlantillaAsync(norte, tipo.Id, "formato.pdf", "application/pdf", pdfV1);
        v1.Plantilla.Version.ShouldBe(1);
        var docx = Zip("word/document.xml");
        var v2 = await SubirPlantillaAsync(norte, tipo.Id, "formato.docx", FormatosDePlantilla.Docx, docx);
        v2.Duplicado.ShouldBeFalse();
        v2.Plantilla.Version.ShouldBe(2);
        v2.Plantilla.Mime.ShouldBe(FormatosDePlantilla.Docx);
        var imagen = await ApiAsync("PUT", $"/api/v1/tipos-documento/{tipo.Id}/plantilla", Admin(norte),
            new IniciarPlantillaDto("foto.png", 100, "image/png"), 415);
        (await CodigoAsync(imagen)).ShouldBe("tipo-archivo-no-soportado");

        var plantilla = await ApiAsync<PlantillaTipoDocumentoDto>("GET", $"/api/v1/tipos-documento/{tipo.Id}/plantilla", Admin(norte), null, 200);
        plantilla.Vigente!.Version.ShouldBe(2);
        plantilla.Historial.Select(p => (p.Version, p.Vigente)).ShouldBe([(2, true), (1, false)]);
        var historica = await ApiAsync<DescargaDto>("GET", $"/api/v1/tipos-documento/{tipo.Id}/plantilla/descarga?version=1", Admin(norte), null, 200);
        (await Http.GetByteArrayAsync(historica.Url)).ShouldBe(pdfV1);
        (await ApiAsync<TipoDocumentoTenantDto>("GET", $"/api/v1/tipos-documento/{tipo.Id}", Admin(norte), null, 200)).Plantilla!.Version.ShouldBe(2);

        // Dos terceros con el flujo que pide el formato: cada uno descarga la plantilla de su expediente, no la del otro.
        await FlujoConAsync(norte, [new RequisitoDelFlujoDto(TipoDocumentoTenantId: tipo.Id)]);
        var (sesionA, expedienteA) = await TerceroConExpedienteAsync(norte);
        var (sesionB, expedienteB) = await TerceroConExpedienteAsync(norte);
        var requisitoA = expedienteA.Requisitos!.ShouldHaveSingleItem();
        requisitoA.TipoDocumento.TienePlantilla.ShouldBeTrue();
        requisitoA.TipoDocumento.Modo.ShouldBe("Archivo");

        var descarga = await TerceroAsync<DescargaDto>("GET", $"/public/v1/expedientes/{expedienteA.Id}/requisitos/{requisitoA.Id}/plantilla",
            sesionA.AccessToken, null, 200);
        descarga.Nombre.ShouldBe("formato.docx");
        (await Http.GetByteArrayAsync(descarga.Url)).ShouldBe(docx);
        (await CodigoAsync(await TerceroAsync("GET", $"/public/v1/expedientes/{expedienteA.Id}/requisitos/{requisitoA.Id}/plantilla",
            sesionB.AccessToken, null, 404))).ShouldBe("expediente-no-encontrado");
        await TerceroAsync("GET", $"/public/v1/expedientes/{expedienteB.Id}/requisitos/{requisitoA.Id}/plantilla", sesionB.AccessToken, null, 200);

        // Retirada: el historial queda, el tercero ya no la descarga.
        (await ApiAsync<PlantillaTipoDocumentoDto>("DELETE", $"/api/v1/tipos-documento/{tipo.Id}/plantilla", Admin(norte), null, 200)).Vigente.ShouldBeNull();
        (await CodigoAsync(await TerceroAsync("GET", $"/public/v1/expedientes/{expedienteA.Id}/requisitos/{requisitoA.Id}/plantilla",
            sesionA.AccessToken, null, 404))).ShouldBe("plantilla-no-encontrada");
    }

    // ---------- formularios (TD-4) ----------

    [Fact]
    public async Task Un_formulario_con_campos_obligatorios_crea_una_version_con_datos_visible_para_el_revisor()
    {
        var e = await EscenarioFormularioAsync();
        var hoy = Hoy();

        // El tercero ve qué llenar en su expediente.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{e.Expediente.Id}", e.Sesion.AccessToken, null, 200);
        var requisito = publico.Requisitos!.ShouldHaveSingleItem();
        requisito.Estado.ShouldBe("Pendiente");
        requisito.TipoDocumento.Modo.ShouldBe("Formulario");
        requisito.TipoDocumento.Campos!.Select(c => c.Clave).ShouldBe(["fecha_expedicion", "origen", "monto"]);
        requisito.TipoDocumento.Archivo.ShouldBeNull();

        // Falta un obligatorio ⇒ 422 con el campo; un archivo no se acepta para un tipo solo de formulario.
        var faltante = await TerceroAsync("POST", "/public/v1/boveda/formularios", e.Sesion.AccessToken,
            Formulario(e, ("fecha_expedicion", hoy.AddDays(-20).ToString("yyyy-MM-dd"))), 422);
        (await CodigoAsync(faltante)).ShouldBe("campo-requerido");
        (await faltante.ReadAsJsonAsync<JsonElement>()).GetProperty("campo").GetString().ShouldBe("origen");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/documentos", e.Sesion.AccessToken,
            new IniciarCargaDto("x.pdf", 100, "application/pdf", TipoDocumentoTenantId: e.Tipo.Id, VinculoId: e.Vinculo.Id), 422)))
            .ShouldBe("archivo-no-permitido");

        var datos = Formulario(e, ("fecha_expedicion", hoy.AddDays(-20).ToString("yyyy-MM-dd")), ("origen", "Nacional"), ("monto", 1500.5m));
        var enviado = await TerceroAsync<ConfirmacionCargaDto>("POST", "/public/v1/boveda/formularios", e.Sesion.AccessToken, datos, 200);
        enviado.Duplicado.ShouldBeFalse();
        enviado.Version.Estado.ShouldBe("Actual");
        enviado.Version.Clase.ShouldBe("Formulario");
        enviado.Version.DatosDeclarados!.Select(d => (d.Clave, d.Valor)).ShouldBe(
            [("fecha_expedicion", hoy.AddDays(-20).ToString("yyyy-MM-dd")), ("origen", "Nacional"), ("monto", "1500.5")]);
        enviado.Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(e.Vinculo.Id);
        (await TerceroAsync<ConfirmacionCargaDto>("POST", "/public/v1/boveda/formularios", e.Sesion.AccessToken, datos, 200)).Duplicado.ShouldBeTrue();

        // El revisor ve la versión con sus datos en el expediente y en los documentos del vínculo.
        var expediente = await EsperarAsync(e.Empresa, e.Expediente.Id, x => x.Requisitos!.Single().VersionActual is not null);
        var version = expediente.Requisitos!.Single().VersionActual!;
        version.DatosDeclarados!.Single(d => d.Clave == "origen").Valor.ShouldBe("Nacional");
        version.Clase.ShouldBe("Formulario");
        version.Vigencia!.VigenteHasta.ShouldBe(hoy.AddDays(10));
        version.Vigencia.Base.ShouldBe("FechaExpedicion");
        var vista = (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.Vinculo.Id}/documentos", Admin(e.Empresa), null, 200))
            .ShouldHaveSingleItem();
        vista.VersionesCompartidas.ShouldHaveSingleItem().DatosDeclarados!.Count.ShouldBe(3);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{e.Vinculo.Id}/documentos/{vista.DocumentoId}/versiones/1/descarga",
            Admin(e.Empresa), null, 409))).ShouldBe("version-sin-archivo");

        // El tercero lo ve en su bóveda.
        var boveda = await TerceroAsync<BovedaDto>("GET", "/public/v1/boveda", e.Sesion.AccessToken, null, 200);
        boveda.Documentos.Single(d => d.Id == vista.DocumentoId).VersionActual!.DatosDeclarados!.Count.ShouldBe(3);
    }

    // ---------- vigencia, aviso y vencimiento (TD-6) ----------

    [Fact]
    public async Task La_vigencia_se_calcula_el_aviso_se_programa_y_llega_al_adelantar_el_reloj_y_la_version_vence_tras_la_gracia()
    {
        var e = await EscenarioFormularioAsync();
        var hoy = Hoy();
        var enviado = await TerceroAsync<ConfirmacionCargaDto>("POST", "/public/v1/boveda/formularios", e.Sesion.AccessToken,
            Formulario(e, ("fecha_expedicion", hoy.AddDays(-20).ToString("yyyy-MM-dd")), ("origen", "Extranjero")), 200);
        var documentoId = enviado.Version.DocumentoId;
        var hasta = hoy.AddDays(10); // expedición + 30 días
        var fechaAviso = hasta.AddDays(-5);

        // La bóveda registra la vigencia del vínculo (por mensaje) y programa el aviso (uno por tercero y fecha) y el vencimiento.
        var calculada = await EsperarEventoAsync<VigenciaDeVersionCalculada>(documentoId);
        calculada.VigenteHasta.ShouldBe(hasta);
        calculada.VinculoId.ShouldBe(e.Vinculo.Id);
        calculada.DiasGracia.ShouldBe(2);
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
        {
            var programado = (await consulta.LoadAsync<AvisoDeVencimiento>(AvisoDeVencimiento.Clave(e.Sesion.TerceroId, fechaAviso))).ShouldNotBeNull();
            programado.Estado.ShouldBe(EstadoAviso.Programado);
        }

        var programados = (await Host.Services.GetRequiredService<IWolverineRuntime>().Storage.Admin.AllIncomingAsync())
            .Where(x => x.Status == EnvelopeStatus.Scheduled).ToList();
        programados.ShouldContain(x => x.MessageType!.Contains(nameof(EnviarAvisosDeVencimiento)) &&
                                       x.ScheduledTime == CalendarioDeVigencias.MomentoDeAviso(fechaAviso));
        programados.ShouldContain(x => x.MessageType!.Contains(nameof(VencerVersionDeDocumento)) &&
                                       x.ScheduledTime == CalendarioDeVigencias.MomentoDeVencimiento(hasta.AddDays(2)));

        // Vista del tercero: vigente hasta, por vencer después.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{e.Expediente.Id}", e.Sesion.AccessToken, null, 200);
        publico.Requisitos!.Single().VersionActual!.Vigencia!.Estado.ShouldBe("Vigente");

        var aviso = new EnviarAvisosDeVencimiento(e.Sesion.TerceroId, fechaAviso);
        var vencer = new VencerVersionDeDocumento(documentoId, 1, e.Vinculo.Id, hasta, 2);
        try
        {
            // Antes de la fecha el mensaje (por su cola durable, como lo dispara el programado) no hace nada.
            await ProcesarAsync(aviso);
            fixture.Correos.Para(e.Correo).ShouldNotContain(m => m.Asunto.Contains("vencer"));

            // El día del aviso: un correo con el documento y su fecha para la empresa; repetido, no sale otro.
            fixture.Reloj.Desfase = TimeSpan.FromDays(5);
            await ProcesarAsync(aviso);
            await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
            {
                var marcado = (await consulta.LoadAsync<AvisoDeVencimiento>(AvisoDeVencimiento.Clave(e.Sesion.TerceroId, fechaAviso))).ShouldNotBeNull();
                marcado.Estado.ShouldBe(EstadoAviso.Enviado);
                marcado.Documentos.ShouldBe(1);
            }

            var correo = await fixture.Correos.EsperarAsync(e.Correo, m => m.Asunto.Contains("vencer"));
            correo.Asunto.ShouldBe("Tu Declaración de origen está por vencer");
            correo.Texto.ShouldContain($"Declaración de origen: vence para {e.Empresa.Nombre} el");
            await ProcesarAsync(aviso);
            fixture.Correos.Para(e.Correo).Count(m => m.Asunto.Contains("vencer")).ShouldBe(1);
            var porVencer = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{e.Expediente.Id}", Admin(e.Empresa), null, 200);
            porVencer.Requisitos!.Single().VersionActual!.Vigencia!.Estado.ShouldBe("PorVencer");

            // En la gracia aún no vence; pasada la gracia, la versión queda Vencida (evento en el stream del documento).
            fixture.Reloj.Desfase = TimeSpan.FromDays(12);
            await ProcesarAsync(vencer);
            (await EventosAsync(documentoId)).OfType<VersionVencida>().ShouldBeEmpty();
            fixture.Reloj.Desfase = TimeSpan.FromDays(13);
            await ProcesarAsync(vencer);
            var vencida = (await EventosAsync(documentoId)).OfType<VersionVencida>().ShouldHaveSingleItem();
            vencida.VinculoId.ShouldBe(e.Vinculo.Id);
            vencida.VigenteHasta.ShouldBe(hasta);

            var vista = (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{e.Vinculo.Id}/documentos", Admin(e.Empresa), null, 200))
                .ShouldHaveSingleItem().VersionesCompartidas.ShouldHaveSingleItem();
            vista.Estado.ShouldBe("Vencida");
            vista.Vigencia!.Estado.ShouldBe("Vencida");
            vista.Vigencia.VencidaEn.ShouldNotBeNull();
            // El vínculo no cambia de estado todavía (H6).
            (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{e.Vinculo.Id}", Admin(e.Empresa), null, 200)).Estado.ShouldNotBe("Vencido");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    // ---------- escenario ----------

    private sealed record EscenarioFormulario(Empresa Empresa, TipoDocumentoTenantDto Tipo, VinculoDto Vinculo, SesionTerceroDto Sesion,
        ExpedienteDto Expediente, string Correo);

    /// <summary>
    /// Una empresa con el tipo propio «Declaración de origen» (solo formulario: fecha de expedición, origen y monto; vence 30
    /// días después de expedida, aviso a 5 días, 2 de gracia) pedido por su flujo, y un tercero que ya aceptó (expediente abierto).
    /// </summary>
    private async Task<EscenarioFormulario> EscenarioFormularioAsync()
    {
        var empresa = await EmpresaNuevaAsync("Comercial " + Guid.NewGuid().ToString("N")[..6], "F" + Random.Shared.Next(10, 99));
        var tipo = await ApiAsync<TipoDocumentoTenantDto>("POST", "/api/v1/tipos-documento", Admin(empresa),
            new CrearTipoDocumentoDto("DECLARACION_ORIGEN", "Declaración de origen", null,
                ReglaFrescura: new ReglaFrescuraDto("MaxDiasDesdeExpedicion", 30), DiasAviso: [5], DiasGracia: 2, Modo: "Formulario",
                Campos:
                [
                    new CampoFormularioDto("fecha_expedicion", "Fecha de expedición", "Fecha", true),
                    new CampoFormularioDto("origen", "Origen de los fondos", "Lista", true, ["Nacional", "Extranjero"]),
                    new CampoFormularioDto("monto", "Monto", "Numero", false, Ayuda: "En pesos")
                ]), 201);
        tipo.Modo.ShouldBe("Formulario");
        tipo.CargaSoportada.ShouldBeTrue();
        tipo.Campos!.Count.ShouldBe(3);

        await FlujoConAsync(empresa, [new RequisitoDelFlujoDto(TipoDocumentoTenantId: tipo.Id)]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(empresa, correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(empresa), null, 200)).ExpedienteAbiertoId!.Value;
        var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(empresa), null, 200);
        return new EscenarioFormulario(empresa, tipo, vinculo, sesion, expediente, correo);
    }

    private async Task<(SesionTerceroDto Sesion, ExpedienteDto Expediente)> TerceroConExpedienteAsync(Empresa empresa)
    {
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(empresa, correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(empresa), null, 200)).ExpedienteAbiertoId!.Value;
        return (sesion, await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(empresa), null, 200));
    }

    private static EnviarFormularioDto Formulario(EscenarioFormulario e, params (string Clave, object Valor)[] valores) =>
        new(e.Vinculo.Id, valores.ToDictionary(v => v.Clave, v => JsonSerializer.SerializeToElement(v.Valor)), e.Tipo.Id);

    // ---------- ayudas ----------

    private async Task<Empresa> EmpresaNuevaAsync(string nombre, string prefijo)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Grupo " + nombre, "co", "America/Bogota"), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var datos = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), nombre, prefijo, null, null), 201);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/autorizacion", new Credencial(admin),
            new PublicarTextoLegalDto($"Autorizo a {nombre}"), 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/terminos", new Credencial(admin),
            new PublicarTextoLegalDto($"Términos de {nombre}"), 200);
        return new Empresa(admin, datos, nombre);
    }

    /// <summary>Crea y publica (queda por defecto para CLIENTE) un flujo con una sola área y los requisitos dados.</summary>
    private async Task FlujoConAsync(Empresa empresa, IReadOnlyList<RequisitoDelFlujoDto> requisitos)
    {
        await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(empresa), new CrearAreaDto("REVISION", "Revisión"), 201);
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(empresa),
            new CrearFlujoDto("HAB_CLIENTE", "Habilitación de clientes", "CLIENTE", Requisitos: requisitos), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(empresa), new { }, 200);
    }

    private Task<VinculoDto> CrearVinculoAsync(Empresa empresa, string correo)
    {
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(empresa), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: "CLIENTE",
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);
    }

    /// <summary>Canjea el enlace del correo de invitación (con OTP si el tipo de vínculo lo exige) y devuelve la sesión del tercero.</summary>
    private async Task<SesionTerceroDto> SesionAsync(string correo)
    {
        var invitacion = await fixture.Correos.EsperarAsync(correo);
        var token = RegexToken().Match(invitacion.Texto).Groups[1].Value;
        var canje = await PublicoAsync($"/public/v1/enlaces/{token}/canjear", null);
        if (canje.Context.Response.StatusCode == StatusCodes.Status200OK)
            return (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;

        var codigo = RegexCodigo().Match((await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("código"))).Texto).Groups[1].Value;
        var verificado = await PublicoAsync($"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo));
        return (await verificado.ReadAsJsonAsync<SesionTerceroDto>())!;
    }

    private Task<IScenarioResult> PublicoAsync(string url, object? cuerpo) =>
        Host.Scenario(s =>
        {
            Preparar(s, "POST", url, cuerpo);
            IpAleatoria(s);
            s.IgnoreStatusCode();
        });

    private async Task AceptarAsync(SesionTerceroDto sesion, Guid vinculoId)
    {
        var invitacion = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{vinculoId}", sesion.AccessToken, null, 200);
        await TerceroAsync("POST", $"/public/v1/invitaciones/{vinculoId}/aceptar", sesion.AccessToken,
            new AceptarInvitacionDto([.. invitacion.TextosPorAceptar.Select(t => new ConsentimientoDto(t.Tipo, t.TextoVersion, true))]), 200);
    }

    /// <summary>Paso 1, PUT directo a MinIO y paso 2; devuelve la respuesta de la confirmación (o la del paso 1 si falla ahí).</summary>
    private async Task<IScenarioResult> SubirAsync(SesionTerceroDto sesion, IniciarCargaDto dto, byte[] contenido, int esperado)
    {
        var carga = await TerceroAsync<CargaPrefirmadaDto>("POST", "/public/v1/boveda/documentos", sesion.AccessToken, dto, 201);
        await PutAsync(carga.Carga!, contenido);
        return await TerceroAsync("POST", $"/public/v1/boveda/documentos/{carga.DocumentoId}/versiones/{carga.Version}/confirmar",
            sesion.AccessToken, null, esperado);
    }

    private async Task<ConfirmacionPlantillaDto> SubirPlantillaAsync(Empresa empresa, Guid tipoId, string nombre, string mime, byte[] contenido)
    {
        var carga = await ApiAsync<CargaDePlantillaDto>("PUT", $"/api/v1/tipos-documento/{tipoId}/plantilla", Admin(empresa),
            new IniciarPlantillaDto(nombre, contenido.Length, mime), 200);
        await PutAsync(carga.Carga, contenido);
        return await ApiAsync<ConfirmacionPlantillaDto>("POST", $"/api/v1/tipos-documento/{tipoId}/plantilla/{carga.Version}/confirmar",
            Admin(empresa), null, 200);
    }

    private static async Task PutAsync(InstruccionCargaDto carga, byte[] contenido)
    {
        using var put = new HttpRequestMessage(HttpMethod.Put, carga.Url) { Content = new ByteArrayContent(contenido) };
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(carga.Cabeceras["Content-Type"]);
        using var respuesta = await Http.SendAsync(put);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>La adjunción de versiones llega por el outbox: espera (hasta 20 s) a que el expediente cumpla la condición.</summary>
    private async Task<ExpedienteDto> EsperarAsync(Empresa empresa, Guid expedienteId, Func<ExpedienteDto, bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(empresa), null, 200);
            if (condicion(expediente))
                return expediente;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El expediente {expedienteId} no llegó al estado esperado.");
            await Task.Delay(100);
        }
    }

    private async Task<T> EsperarEventoAsync<T>(Guid documentoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            if ((await EventosAsync(documentoId)).OfType<T>().FirstOrDefault() is { } evento)
                return evento;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El documento {documentoId} no registró {typeof(T).Name}.");
            await Task.Delay(100);
        }
    }

    private async Task<IReadOnlyList<object>> EventosAsync(Guid documentoId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda);
        return [.. (await consulta.Events.FetchStreamAsync(documentoId)).Select((IEvent x) => x.Data)];
    }

    /// <summary>Envía el mensaje por su cola durable (como lo dispara el programado) y espera a que se procese con lo que encadene.</summary>
    private Task ProcesarAsync<T>(T mensaje) where T : class =>
        Host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).SendMessageAndWaitAsync(mensaje);

    private DateOnly Hoy() => CalculadoraDeVigencia.Fecha(fixture.Reloj.GetUtcNow());

    private static Credencial Admin(Empresa empresa) => new(empresa.Admin, empresa.Datos.Id);

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private static async Task<string?> DetalleAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("detail").GetString();

    /// <summary>PDF mínimo válido de una página; el texto lo hace único y el relleno le da tamaño.</summary>
    private static byte[] Pdf(string texto, int relleno = 0) => Encoding.Latin1.GetBytes(new StringBuilder()
        .Append("%PDF-1.4\n")
        .Append(System.Globalization.CultureInfo.InvariantCulture, $"% {texto} {Guid.NewGuid():N}\n")
        .Append('%', relleno)
        .Append("\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n")
        .Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n")
        .Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n")
        .Append("trailer\n<< /Root 1 0 R >>\n%%EOF\n")
        .ToString());

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0, 1, 2, 3, 4];

    private static byte[] Zip(string entrada)
    {
        using var memoria = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memoria, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var escritor = new StreamWriter(zip.CreateEntry(entrada).Open());
            escritor.Write("<w:document/>");
        }

        return memoria.ToArray();
    }

    private async Task<T> ApiAsync<T>(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        (await (await ApiAsync(metodo, url, credencial, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> ApiAsync(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", credencial.ApiKey);
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
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

    private static void IpAleatoria(Scenario s) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
            new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    private static void Preparar(Scenario s, string metodo, string url, object? cuerpo)
    {
        switch (metodo)
        {
            case "GET":
                s.Get.Url(url);
                break;
            case "DELETE":
                s.Delete.Url(url);
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
