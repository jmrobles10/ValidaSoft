using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Wolverine;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Requisitos documentales en el expediente (RQ-1…RQ-9) con PostgreSQL y MinIO reales: cada área aprueba u observa
/// documentos concretos, la aprobación del área se deriva, la bóveda comparte lo que pide el flujo y una versión nueva
/// se adjunta sola (outbox) invalidando solo ese requisito. Además: exención, flujos distintos por empresa, idempotencia
/// de la sincronización y las lecturas de identidad (<c>/usuarios/resumen</c>, <c>/yo</c>).
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class RequisitosApiTests(ApiFixture fixture)
{
    private static readonly HttpClient Http = new();

    private static readonly string[] ScopesDeSistema =
    [
        "configuracion:leer", "vinculos:leer", "flujos:leer", "expedientes:leer", "expedientes:revisar", "documentos:leer"
    ];

    private IAlbaHost Host => fixture.Host;

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    [GeneratedRegex(@"\b(\d{6})\b")]
    private static partial Regex RegexCodigo();

    private sealed record Credencial(string ApiKey, Guid? Empresa = null, string? Delegado = null);

    private sealed record Persona(UsuarioDto Usuario, string Sub);

    private sealed record Empresa(string Admin, EmpresaDto Datos, string LlaveSrm, RSA ClaveSrm);

    private sealed record Escenario(Empresa Norte, AreaDto Compras, AreaDto Cumplimiento, Persona Laura, Persona Carlos, FlujoDto Flujo);

    // ---------- flujo principal ----------

    [Fact]
    public async Task Proveedor_con_rut_y_camara_compartidos_observacion_version_nueva_reenvio_y_aprobacion_por_requisito()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);

        // El tercero sube su RUT antes de aceptar: nadie lo ve todavía (B-1).
        var rut = Pdf("RUT");
        (await SubirAsync(sesion, "RUT", rut)).Compartidos.ShouldBeEmpty();

        // Acepta ⇒ el RUT se comparte (lo pide el flujo) y nace Cargado; la Cámara falta ⇒ expediente Abierto (fase de carga).
        var aceptada = await AceptarAsync(sesion, vinculo.Id);
        aceptada.Compartidos.ShouldHaveSingleItem().Tipo.ShouldBe("RUT");
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId!.Value;
        var abierto = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        abierto.Estado.ShouldBe("Abierto");
        var requisitoRut = Requisito(abierto, "RUT");
        var requisitoCamara = Requisito(abierto, "CAMARA_COMERCIO");
        requisitoRut.Estado.ShouldBe("Cargado");
        requisitoRut.VersionActual!.N.ShouldBe(1);
        requisitoRut.VersionActual.Sha256.ShouldBe(Sha256(rut));
        requisitoRut.Areas.Select(a => a.Codigo).ShouldBe(["COMPRAS", "CUMPLIMIENTO"]);
        requisitoCamara.Estado.ShouldBe("Pendiente");
        requisitoCamara.VersionActual.ShouldBeNull();
        requisitoCamara.Areas.ShouldHaveSingleItem().Codigo.ShouldBe("CUMPLIMIENTO");
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e.Norte, e.Carlos), null, 200)).Datos.ShouldBeEmpty();

        // El tercero ve qué le piden y qué le falta, sin áreas ni revisores.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        publico.RequisitosFaltantes.ShouldBe(1);
        publico.Requisitos!.Select(r => (r.TipoDocumento.Codigo, r.Estado)).ShouldBe([("RUT", "Cargado"), ("CAMARA_COMERCIO", "Pendiente")]);

        // Sube la Cámara ⇒ se comparte, se adjunta sola (outbox) y el expediente pasa a revisión.
        var camara = Pdf("Cámara");
        (await SubirAsync(sesion, "CAMARA_COMERCIO", camara)).Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(vinculo.Id);
        var enRevision = await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        Requisito(enRevision, "CAMARA_COMERCIO").VersionActual!.N.ShouldBe(1);
        enRevision.Requisitos!.ShouldAllBe(r => r.Estado == "EnRevision");

        // Cada área ve el expediente en su bandeja con lo que le falta decidir.
        var filaCarlos = (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldHaveSingleItem();
        filaCarlos.RequisitosPendientes.ShouldBe(2);
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e.Norte, e.Laura), null, 200)).Datos
            .ShouldHaveSingleItem().RequisitosPendientes.ShouldBe(1);

        // Con requisitos, «Aprobar área» pide la versión de cada documento que decide (DA-1); Compras no revisa la Cámara.
        var porArea = await ApiAsync("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Compras.Id}/veredicto", Delegado(e.Norte, e.Laura),
            new VeredictoDto("Aprobado"), 422);
        (await CodigoAsync(porArea)).ShouldBe("versiones-requeridas");
        var fueraDeAlcance = await VeredictoAsync(e, expedienteId, requisitoCamara.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1), 409);
        (await CodigoAsync(fueraDeAlcance)).ShouldBe("requisito-no-en-alcance");

        // Cumplimiento aprueba el RUT y observa la Cámara ⇒ devuelto; Compras ya no puede decidir.
        await VeredictoAsync<ExpedienteDto>(e, expedienteId, requisitoRut.Id, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", SobreVersion: 1));
        var observado = await VeredictoAsync<ExpedienteDto>(e, expedienteId, requisitoCamara.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Observado", ["ilegible"], "La cámara está cortada.", SobreVersion: 1));
        observado.Estado.ShouldBe("Observado");
        Requisito(observado, "CAMARA_COMERCIO").Estado.ShouldBe("Observado");
        Area(observado, "CUMPLIMIENTO").Estado.ShouldBe("Observada");
        var frenado = await VeredictoAsync(e, expedienteId, requisitoRut.Id, e.Compras, e.Laura, new VeredictoDto("Aprobado", SobreVersion: 1), 409);
        (await CodigoAsync(frenado)).ShouldBe("expediente-observado");

        // Correo y vista pública: el documento y el motivo, nunca el área ni el revisor.
        var correoObservacion = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("observaciones"));
        correoObservacion.Texto.ShouldContain(requisitoCamara.TipoDocumento.Nombre);
        correoObservacion.Texto.ShouldContain("La cámara está cortada.");
        correoObservacion.Texto.ShouldNotContain("CUMPLIMIENTO");
        var vista = await TerceroAsync("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        var textoVista = await vista.ReadAsTextAsync();
        textoVista.ShouldNotContain(e.Carlos.Usuario.Id.ToString());
        textoVista.ShouldNotContain("CUMPLIMIENTO");
        var devuelto = JsonSerializer.Deserialize<ExpedientePublicoDto>(textoVista, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var camaraPublica = devuelto.Requisitos!.Single(r => r.TipoDocumento.Codigo == "CAMARA_COMERCIO");
        camaraPublica.Estado.ShouldBe("Observado");
        camaraPublica.Observaciones.ShouldHaveSingleItem().Motivos.ShouldHaveSingleItem().Codigo.ShouldBe("ilegible");
        devuelto.Observaciones.ShouldHaveSingleItem().Requisito.ShouldBe("CAMARA_COMERCIO");

        // Sin versión nueva ni respuesta no se reenvía.
        var sinCorregir = await TerceroAsync("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken, new ReenviarExpedienteDto(), 409);
        (await CodigoAsync(sinCorregir)).ShouldBe("requisitos-sin-corregir");

        // Cámara v2 ⇒ solo ese requisito se invalida; el RUT conserva la aprobación de Cumplimiento.
        var camara2 = Pdf("Cámara completa");
        await SubirAsync(sesion, "CAMARA_COMERCIO", camara2);
        var corregido = await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "CAMARA_COMERCIO").VersionActual!.N == 2);
        corregido.Estado.ShouldBe("Observado");
        Requisito(corregido, "CAMARA_COMERCIO").Estado.ShouldBe("Cargado");
        Requisito(corregido, "CAMARA_COMERCIO").Areas.Single().Veredictos.ShouldHaveSingleItem().Invalidado.ShouldBeTrue();
        var rutCumplimiento = Requisito(corregido, "RUT").Areas.Single(a => a.Codigo == "CUMPLIMIENTO");
        rutCumplimiento.Estado.ShouldBe("Aprobado");
        rutCumplimiento.Veredictos.ShouldHaveSingleItem().Vigente.ShouldBeTrue();

        var reenviado = await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken,
            new ReenviarExpedienteDto(), 200);
        reenviado.Estado.ShouldBe("EnRevision");

        // Un veredicto sobre la versión superada ⇒ 409.
        var superada = await VeredictoAsync(e, expedienteId, requisitoCamara.Id, e.Cumplimiento, e.Carlos, new VeredictoDto("Aprobado", SobreVersion: 1), 409);
        (await CodigoAsync(superada)).ShouldBe("version-superada");

        // Compras aprueba el RUT y Cumplimiento la Cámara v2 ⇒ áreas aprobadas, expediente aprobado y vínculo activo.
        var comprasAprobo = await VeredictoAsync<ExpedienteDto>(e, expedienteId, requisitoRut.Id, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", SobreVersion: 1));
        Area(comprasAprobo, "COMPRAS").Estado.ShouldBe("Aprobada");
        Area(comprasAprobo, "COMPRAS").Veredictos.ShouldContain(v => v.Derivado && v.Vigente && v.Origen == "Humano");
        var aprobado = await VeredictoAsync<ExpedienteDto>(e, expedienteId, requisitoCamara.Id, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", SobreVersion: 2));
        aprobado.Estado.ShouldBe("Aprobado");
        aprobado.Requisitos!.ShouldAllBe(r => r.Estado == "Aprobado");
        aprobado.Etapas.Single().Areas.ShouldAllBe(a => a.Estado == "Aprobada");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Activo");

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expedienteId}/historial", Admin(e.Norte), null, 200);
        historial.Select(h => h.Tipo).ShouldBe(
        [
            "expediente_abierto", "flujo_seleccionado", "requisitos_calculados", "version_adjuntada_a_requisito", "expediente_enviado_a_revision",
            "etapa_activada", "veredicto_de_requisito_emitido", "veredicto_de_requisito_emitido", "expediente_observado",
            "version_adjuntada_a_requisito", "veredictos_invalidados_por_nueva_version", "expediente_reenviado", "veredicto_de_requisito_emitido",
            "veredicto_de_area_emitido", "veredicto_de_requisito_emitido", "veredicto_de_area_emitido", "etapa_aprobada", "expediente_aprobado"
        ]);
        historial[3].ActorTipo.ShouldBe("Tercero"); // la Cámara la adjuntó la carga del tercero (por el outbox)
    }

    // ---------- decisión por área (DA-1…DA-4) ----------

    [Fact]
    public async Task Decision_por_area_observacion_multiple_un_solo_correo_y_aprobar_area_hasta_activar_el_vinculo()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId!.Value;
        var url = $"/api/v1/expedientes/{expedienteId}";

        // Cada persona ve lo que «Aprobar área» le aprobaría, con la versión vigente.
        var visto = await ApiAsync<ExpedienteDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200);
        visto.Estado.ShouldBe("EnRevision");
        var rut = Requisito(visto, "RUT");
        var camara = Requisito(visto, "CAMARA_COMERCIO");
        Area(visto, "CUMPLIMIENTO").DocumentosPorDecidir!.ShouldBe([new DocumentoPorDecidirDto(rut.Id, 1), new DocumentoPorDecidirDto(camara.Id, 1)]);
        Area(visto, "COMPRAS").DocumentosPorDecidir!.ShouldBe([new DocumentoPorDecidirDto(rut.Id, 1)]);

        // Cumplimiento observa RUT y Cámara en una sola llamada ⇒ una sola devolución y un solo correo con ambas observaciones.
        var sinDocumentos = await AreaAsync(e, expedienteId, e.Cumplimiento, e.Carlos, new VeredictoDto("Observado", ["ilegible"]), 422);
        (await CodigoAsync(sinDocumentos)).ShouldBe("requisitos-requeridos");
        var observado = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Cumplimiento, e.Carlos, new VeredictoDto("Observado", Requisitos:
        [
            new ObservacionDeRequisitoDto(rut.Id, 1, ["ilegible"], "El RUT no está actualizado."),
            new ObservacionDeRequisitoDto(camara.Id, 1, ["ilegible"], "La cámara está cortada.")
        ]));
        observado.Estado.ShouldBe("Observado");
        observado.Requisitos!.ShouldAllBe(r => r.Estado == "Observado");
        Area(observado, "CUMPLIMIENTO").Estado.ShouldBe("Observada");
        Area(observado, "COMPRAS").DocumentosPorDecidir!.ShouldBeEmpty(); // devuelto: nadie decide

        var aviso = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("observaciones"));
        aviso.Texto.ShouldContain(rut.TipoDocumento.Nombre);
        aviso.Texto.ShouldContain(camara.TipoDocumento.Nombre);
        aviso.Texto.ShouldContain("El RUT no está actualizado.");
        aviso.Texto.ShouldContain("La cámara está cortada.");
        aviso.Texto.ShouldNotContain("CUMPLIMIENTO");

        // El tercero sube versiones nuevas de ambos y reenvía.
        await SubirAsync(sesion, "RUT", Pdf("RUT actualizado"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara completa"));
        await EsperarAsync(e.Norte, expedienteId, x => x.Requisitos!.All(r => r.VersionActual!.N == 2));
        (await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken,
            new ReenviarExpedienteDto(), 200)).Estado.ShouldBe("EnRevision");
        fixture.Correos.Para(correo).Count(m => m.Asunto.Contains("observaciones")).ShouldBe(1);

        // Compras «Aprobar área» con lo que leyó.
        var leidoPorLaura = await ApiAsync<ExpedienteDto>("GET", url, Delegado(e.Norte, e.Laura), null, 200);
        var deCompras = Area(leidoPorLaura, "COMPRAS").DocumentosPorDecidir!;
        deCompras.ShouldBe([new DocumentoPorDecidirDto(rut.Id, 2)]);
        var comprasAprobo = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: deCompras.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)));
        Area(comprasAprobo, "COMPRAS").Estado.ShouldBe("Aprobada");
        Area(comprasAprobo, "COMPRAS").Veredictos.ShouldContain(v => v.Derivado && v.Vigente && v.Origen == "Humano");
        var ya = await AreaAsync(e, expedienteId, e.Compras, e.Laura,
            new VeredictoDto("Aprobado", Versiones: deCompras.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)), 409);
        (await CodigoAsync(ya)).ShouldBe("area-ya-se-pronuncio");

        // Cumplimiento lee y, antes de decidir, llega la Cámara v3 ⇒ 409 version-superada y no se aprueba nada.
        var leidoPorCarlos = Area(await ApiAsync<ExpedienteDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200), "CUMPLIMIENTO")
            .DocumentosPorDecidir!;
        leidoPorCarlos.ShouldBe([new DocumentoPorDecidirDto(rut.Id, 2), new DocumentoPorDecidirDto(camara.Id, 2)]);
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara v3"));
        await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "CAMARA_COMERCIO").VersionActual!.N == 3);

        var faltan = await AreaAsync(e, expedienteId, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: new Dictionary<Guid, int> { [rut.Id] = 2 }), 422);
        var problemaFaltan = await faltan.ReadAsJsonAsync<JsonElement>();
        problemaFaltan.GetProperty("codigo").GetString().ShouldBe("versiones-requeridas");
        problemaFaltan.GetProperty("requisitos").EnumerateArray().Select(r => r.GetProperty("requisitoId").GetGuid()).ShouldBe([camara.Id]);

        var superada = await AreaAsync(e, expedienteId, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: leidoPorCarlos.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)), 409);
        var problema = await superada.ReadAsJsonAsync<JsonElement>();
        problema.GetProperty("codigo").GetString().ShouldBe("version-superada");
        var afectado = problema.GetProperty("requisitos").EnumerateArray().ShouldHaveSingleItem();
        afectado.GetProperty("requisitoId").GetGuid().ShouldBe(camara.Id);
        afectado.GetProperty("versionVigente").GetInt32().ShouldBe(3);
        var sinCambios = await ApiAsync<ExpedienteDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200);
        Requisito(sinCambios, "RUT").Areas.Single(a => a.Codigo == "CUMPLIMIENTO").Veredictos.ShouldAllBe(v => !v.Vigente);

        // Vuelve a leer y aprueba: lo aprobado coincide con documentosPorDecidir ⇒ expediente aprobado y vínculo activo.
        var releido = Area(sinCambios, "CUMPLIMIENTO").DocumentosPorDecidir!;
        releido.ShouldBe([new DocumentoPorDecidirDto(rut.Id, 2), new DocumentoPorDecidirDto(camara.Id, 3)]);
        var aprobado = await AreaAsync<ExpedienteDto>(e, expedienteId, e.Cumplimiento, e.Carlos,
            new VeredictoDto("Aprobado", Versiones: releido.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)));
        aprobado.Estado.ShouldBe("Aprobado");
        aprobado.Requisitos!.ShouldAllBe(r => r.Estado == "Aprobado");
        var deCarlos = aprobado.Requisitos!
            .SelectMany(r => r.Areas.Where(a => a.Codigo == "CUMPLIMIENTO").SelectMany(a => a.Veredictos.Where(v => v.Vigente))
                .Select(v => (r.Id, v.SobreVersion, v.Usuario)))
            .ToList();
        var carlos = e.Carlos.Usuario.Id.ToString();
        deCarlos.ShouldBe([(rut.Id, (int?)2, (string?)carlos), (camara.Id, (int?)3, (string?)carlos)]);
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Activo");

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"{url}/historial", Admin(e.Norte), null, 200);
        historial.Count(h => h.Tipo == "expediente_observado").ShouldBe(1);
        // Dos observaciones, el RUT de Compras y el RUT y la Cámara de Cumplimiento.
        historial.Count(h => h.Tipo == "veredicto_de_requisito_emitido").ShouldBe(5);
    }

    // ---------- exención ----------

    [Fact]
    public async Task Eximir_un_requisito_pide_justificacion_y_permiso_de_administrador()
    {
        var e = await PrepararAsync();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e.Norte), new AbrirExpedienteDto(), 201);
        expediente.Estado.ShouldBe("Abierto");
        expediente.Requisitos!.ShouldAllBe(r => r.Estado == "Pendiente"); // sin aceptar, nada está compartido (B-1)
        var camara = Requisito(expediente, "CAMARA_COMERCIO");
        var url = $"/api/v1/expedientes/{expediente.Id}/requisitos/{camara.Id}/eximir";

        await ApiAsync("POST", url, Delegado(e.Norte, e.Carlos), new JustificacionDto("No aplica"), 403);
        var sinJustificacion = await ApiAsync("POST", url, Admin(e.Norte), new JustificacionDto(" "), 422);
        (await CodigoAsync(sinJustificacion)).ShouldBe("justificacion-requerida");

        var eximido = await ApiAsync<ExpedienteDto>("POST", url, Admin(e.Norte), new JustificacionDto("Entidad sin registro mercantil."), 200);
        var requisito = Requisito(eximido, "CAMARA_COMERCIO");
        requisito.Estado.ShouldBe("Eximido");
        requisito.Exencion!.Justificacion.ShouldBe("Entidad sin registro mercantil.");
        requisito.Areas.ShouldAllBe(a => a.Estado == "Eximido");
        eximido.Estado.ShouldBe("Abierto"); // aún falta el RUT

        var otraVez = await ApiAsync("POST", url, Admin(e.Norte), new JustificacionDto("Otra vez"), 409);
        (await CodigoAsync(otraVez)).ShouldBe("requisito-eximido");
    }

    // ---------- compartición por requisitos (reemplaza BO-3) ----------

    [Fact]
    public async Task Dos_empresas_con_flujos_distintos_reciben_solo_lo_que_pide_su_flujo()
    {
        var a = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var b = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        await FlujoConAsync(a, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        await FlujoConAsync(b, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO")]);

        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculoA = await CrearVinculoAsync(a, "CLIENTE", correo, nit);
        var vinculoB = await CrearVinculoAsync(b, "CLIENTE", correo, nit);
        var sesion = await SesionAsync(correo, a.Datos.Nombre);

        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));

        (await AceptarAsync(sesion, vinculoA.Id)).Compartidos.ShouldHaveSingleItem().Tipo.ShouldBe("RUT");
        (await AceptarAsync(sesion, vinculoB.Id)).Compartidos.ShouldHaveSingleItem().Tipo.ShouldBe("CAMARA_COMERCIO");

        (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{vinculoA.Id}/documentos", Admin(a), null, 200))
            .ShouldHaveSingleItem().Tipo.Codigo.ShouldBe("RUT");
        (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{vinculoB.Id}/documentos", Admin(b), null, 200))
            .ShouldHaveSingleItem().Tipo.Codigo.ShouldBe("CAMARA_COMERCIO");

        // Cada expediente nace con su requisito Cargado y pasa a revisión de inmediato.
        foreach (var (empresa, vinculo) in new[] { (a, vinculoA), (b, vinculoB) })
        {
            var id = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(empresa), null, 200)).ExpedienteAbiertoId!.Value;
            var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{id}", Admin(empresa), null, 200);
            expediente.Estado.ShouldBe("EnRevision");
            expediente.Requisitos!.ShouldHaveSingleItem().VersionActual.ShouldNotBeNull();
        }

        // Una versión nueva del RUT solo va a quien lo pide.
        (await SubirAsync(sesion, "RUT", Pdf("RUT v2"))).Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(vinculoA.Id);
    }

    // ---------- requisitos condicionados (RQ-12) ----------

    [Fact]
    public async Task Un_mismo_flujo_pide_distinto_a_una_persona_juridica_y_a_una_natural_y_solo_comparte_lo_que_le_pide()
    {
        var a = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var juridica = new CondicionDto("Predicado", Atributo: "tercero.tipoPersona", Operador: "Igual", Valores: ["juridica"]);
        var natural = new CondicionDto("Predicado", Atributo: "tercero.tipoPersona", Operador: "Igual", Valores: ["Natural"]);

        var flujo = await FlujoConAsync(a, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT"),
            new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO", Condicion: juridica),
            new RequisitoDelFlujoDto(TipoDocumento: "CEDULA", Condicion: natural)
        ]);
        var leidos = flujo.Publicada!.Requisitos!;
        leidos[0].Condicion.ShouldBeNull();
        leidos[1].Condicion!.Valores.ShouldBe(["Juridica"]); // normalizada al guardar
        leidos[2].Condicion!.Atributo.ShouldBe("tercero.tipoPersona");

        // Validación al guardar: condición mal formada y flujo sin nada que se pida siempre.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(a), new CrearFlujoDto("MAL", "Mal", "CLIENTE",
            Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT"),
                new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO", Condicion: juridica with { Operador = "Mayor" })]), 422)))
            .ShouldBe("condicion-invalida");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/flujos", Admin(a), new CrearFlujoDto("MAL", "Mal", "CLIENTE",
            Requisitos: [new RequisitoDelFlujoDto(TipoDocumento: "RUT", Condicion: juridica)]), 422)))
            .ShouldBe("requisito-siempre-requerido");

        // La persona jurídica y la natural ya tienen RUT y Cámara en su bóveda antes de aceptar.
        var correoJuridica = CorreoNuevo();
        var correoNatural = CorreoNuevo();
        var vinculoJuridica = await CrearVinculoAsync(a, "CLIENTE", correoJuridica);
        var vinculoNatural = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(a), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "CC", "10" + Random.Shared.Next(10_000_000, 99_999_999)), "Natural", "Ana Pérez", null,
                [new ContactoDto("Ana Pérez", correoNatural, null, "Titular")]),
            TipoVinculo: "CLIENTE", Atributos: null, CorreoInvitacion: correoNatural, ReferenciaExterna: null), 201);

        var sesionJuridica = await SesionAsync(correoJuridica);
        var sesionNatural = await SesionAsync(correoNatural);
        foreach (var sesion in new[] { sesionJuridica, sesionNatural })
        {
            await SubirAsync(sesion, "RUT", Pdf("RUT " + sesion.TerceroId));
            await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara " + sesion.TerceroId));
        }

        (await AceptarAsync(sesionJuridica, vinculoJuridica.Id)).Compartidos.Select(c => c.Tipo).Order().ShouldBe(["CAMARA_COMERCIO", "RUT"]);
        (await AceptarAsync(sesionNatural, vinculoNatural.Id)).Compartidos.ShouldHaveSingleItem().Tipo.ShouldBe("RUT"); // la Cámara no se le pide

        async Task<ExpedienteDto> ExpedienteAsync(VinculoDto vinculo)
        {
            var id = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(a), null, 200)).ExpedienteAbiertoId!.Value;
            return await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{id}", Admin(a), null, 200);
        }

        var deJuridica = await ExpedienteAsync(vinculoJuridica);
        deJuridica.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT", "CAMARA_COMERCIO"]);
        deJuridica.Estado.ShouldBe("EnRevision"); // todo lo que se le pide ya estaba en su bóveda

        var deNatural = await ExpedienteAsync(vinculoNatural);
        deNatural.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT", "CEDULA"]);
        deNatural.Estado.ShouldBe("Abierto"); // le falta la cédula
        Requisito(deNatural, "CEDULA").Estado.ShouldBe("Pendiente");

        // Lo que el tercero ve en su expediente es lo mismo que se le pidió.
        var publico = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{deNatural.Id}", sesionNatural.AccessToken, null, 200);
        publico.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT", "CEDULA"]);

        // Una versión nueva de la Cámara de la persona natural no se comparte: su vínculo no la pide.
        (await SubirAsync(sesionNatural, "CAMARA_COMERCIO", Pdf("Cámara v2"))).Compartidos.ShouldBeEmpty();
        (await SubirAsync(sesionJuridica, "CAMARA_COMERCIO", Pdf("Cámara v2"))).Compartidos.ShouldHaveSingleItem().VinculoId.ShouldBe(vinculoJuridica.Id);
    }

    // ---------- sincronización: no se pierde ni se duplica ----------

    [Fact]
    public async Task La_sincronizacion_por_el_outbox_llega_sola_y_repetirla_no_duplica_eventos()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId!.Value;

        // La carga solo deja el mensaje en el outbox; el expediente se pone al día sin que nadie más intervenga.
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await EsperarAsync(e.Norte, expedienteId, x => Requisito(x, "RUT").VersionActual is not null);

        // Entregas repetidas y simultáneas del mismo mensaje (reintentos de Wolverine) no adjuntan dos veces.
        var mensaje = new SincronizarRequisitosDelVinculo(e.Norte.Datos.TenantId, vinculo.Id, "Tercero", sesion.TerceroId.ToString());
        await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => InvocarAsync(mensaje)));
        await InvocarAsync(mensaje);

        var eventos = await EventosAsync(e.Norte.Datos.TenantId, expedienteId);
        eventos.OfType<VersionAdjuntadaARequisito>().ShouldHaveSingleItem().Version.Numero.ShouldBe(1);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Abierto");
    }

    // ---------- identidad para las pantallas ----------

    [Fact]
    public async Task Resumen_de_usuarios_y_quien_soy()
    {
        var e = await PrepararAsync();

        var resumen = await ApiAsync("GET", $"/api/v1/usuarios/resumen?ids={e.Laura.Usuario.Id},{e.Carlos.Usuario.Id},{Guid.NewGuid()}",
            Delegado(e.Norte, e.Laura), null, 200);
        var texto = await resumen.ReadAsTextAsync();
        texto.ShouldNotContain("@"); // sin correos
        texto.ShouldNotContain("Revisor"); // ni roles
        var usuarios = JsonSerializer.Deserialize<List<UsuarioResumenDto>>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        usuarios.Select(u => u.Nombre).ShouldBe(["Carlos Cumplimiento", "Laura Compras"]);
        (await ApiAsync<List<UsuarioResumenDto>>("GET", "/api/v1/usuarios/resumen", Admin(e.Norte), null, 200)).ShouldBeEmpty();
        var invalido = await ApiAsync("GET", "/api/v1/usuarios/resumen?ids=no-es-un-id", Admin(e.Norte), null, 422);
        (await CodigoAsync(invalido)).ShouldBe("ids-invalidos");

        // Una llave sin expedientes:leer ni vinculos:leer no ve nombres.
        var soloConfig = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Norte.Admin),
            new CrearApiKeyDto("Solo configuración", ["configuracion:leer"], null, null), 201);
        var sinPermiso = await ApiAsync("GET", $"/api/v1/usuarios/resumen?ids={e.Laura.Usuario.Id}", new Credencial(soloConfig.Valor!), null, 403);
        (await CodigoAsync(sinPermiso)).ShouldBe("permiso-insuficiente");

        var laura = await ApiAsync<YoDto>("GET", "/api/v1/yo", Delegado(e.Norte, e.Laura), null, 200);
        laura.ActorTipo.ShouldBe("Usuario");
        laura.Usuario!.Id.ShouldBe(e.Laura.Usuario.Id);
        laura.Usuario.Nombre.ShouldBe("Laura Compras");
        laura.Roles.ShouldHaveSingleItem().Rol.ShouldBe("Revisor");
        laura.Sistema.ShouldBe("SRM");
        laura.Empresas.ShouldHaveSingleItem().Id.ShouldBe(e.Norte.Datos.Id);
        laura.EmpresaActiva.ShouldBe(e.Norte.Datos.Id);
        laura.Permisos.ShouldContain("expedientes:revisar");
        laura.Permisos.ShouldNotContain("expedientes:administrar");

        var admin = await ApiAsync<YoDto>("GET", "/api/v1/yo", new Credencial(e.Norte.Admin), null, 200);
        admin.Usuario.ShouldBeNull();
        admin.Roles.ShouldBeEmpty();
        admin.TenantId.ShouldBe(e.Norte.Datos.TenantId);
        admin.Empresas.ShouldHaveSingleItem().Nombre.ShouldBe("Norte S.A.S.");
        admin.Permisos.ShouldContain("expedientes:administrar");
    }

    // ---------- preparación ----------

    /// <summary>
    /// Empresa con Compras (Laura) y Cumplimiento (Carlos) en paralelo y el flujo PROVEEDOR que pide RUT (ambas áreas) y
    /// Cámara de comercio (solo Cumplimiento), publicado y por defecto; textos legales publicados.
    /// </summary>
    private async Task<Escenario> PrepararAsync()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");

        async Task<Persona> PersonaAsync(string nombre, string rol)
        {
            var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
            var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(norte.Admin),
                new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto(rol, norte.Datos.Id)], null), 201);
            return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
        }

        var laura = await PersonaAsync("Laura Compras", "Revisor");
        var carlos = await PersonaAsync("Carlos Cumplimiento", "OficialCumplimiento");
        var compras = await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(norte), new CrearAreaDto("COMPRAS", "Compras"), 201);
        var cumplimiento = await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(norte), new CrearAreaDto("CUMPLIMIENTO", "Cumplimiento"), 201);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{compras.Id}/miembros", Admin(norte), new MiembrosAreaDto([laura.Usuario.Id]), 200);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{cumplimiento.Id}/miembros", Admin(norte), new MiembrosAreaDto([carlos.Usuario.Id]), 200);

        var flujo = await FlujoConAsync(norte, "PROVEEDOR",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT", AreasQueRevisan: [compras.Id, cumplimiento.Id]),
            new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO", AreasQueRevisan: [cumplimiento.Id])
        ]);
        flujo.Publicada!.Requisitos!.Select(r => r.TipoDocumento).ShouldBe(["RUT", "CAMARA_COMERCIO"]);
        return new Escenario(norte, compras, cumplimiento, laura, carlos, flujo);
    }

    private async Task<Empresa> EmpresaNuevaAsync(string nombre, string prefijo)
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Grupo " + nombre, "co", "America/Bogota"), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var datos = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), nombre, prefijo, null, null), 201);

        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(admin),
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", rsa.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(admin),
            new CrearApiKeyDto("Integración SRM", ScopesDeSistema, null, null, SistemaId: sistema.Id), 201);

        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/autorizacion", new Credencial(admin),
            new PublicarTextoLegalDto($"Autorizo a {nombre}"), 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/terminos", new Credencial(admin),
            new PublicarTextoLegalDto($"Términos de {nombre}"), 200);
        return new Empresa(admin, datos, llave.Valor!, rsa);
    }

    /// <summary>Crea y publica (queda por defecto) un flujo con todas las áreas activas en paralelo y los requisitos dados.</summary>
    private async Task<FlujoDto> FlujoConAsync(Empresa empresa, string tipoVinculo, IReadOnlyList<RequisitoDelFlujoDto> requisitos)
    {
        var areas = await ApiAsync<List<AreaDto>>("GET", "/api/v1/areas", Admin(empresa), null, 200);
        if (areas.Count == 0)
            await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(empresa), new CrearAreaDto("REVISION", "Revisión"), 201);

        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(empresa),
            new CrearFlujoDto("HAB_" + tipoVinculo, "Habilitación " + tipoVinculo, tipoVinculo, Requisitos: requisitos), 201);
        flujo.Borrador!.Requisitos!.Count.ShouldBe(requisitos.Count);
        return await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(empresa), new { }, 200);
    }

    private Task<VinculoDto> CrearVinculoAsync(Empresa empresa, string tipo, string correo, string? nit = null)
    {
        nit ??= Nit();
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(empresa), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);
    }

    /// <summary>
    /// Canjea el enlace del correo de invitación (de la empresa indicada, si hay varias) y devuelve la sesión del tercero;
    /// si el tipo de vínculo exige OTP (PROVEEDOR), lo verifica con el código que llega al mismo correo.
    /// </summary>
    private async Task<SesionTerceroDto> SesionAsync(string correo, string? empresa = null)
    {
        var invitacion = await fixture.Correos.EsperarAsync(correo, m => empresa is null || m.Asunto.Contains(empresa));
        var token = RegexToken().Match(invitacion.Texto).Groups[1].Value;
        var canje = await PublicoAsync($"/public/v1/enlaces/{token}/canjear", null);
        if (canje.Context.Response.StatusCode == StatusCodes.Status200OK)
            return (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;

        canje.Context.Response.StatusCode.ShouldBe(StatusCodes.Status202Accepted);
        var codigo = RegexCodigo().Match((await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("código"))).Texto).Groups[1].Value;
        var verificado = await PublicoAsync($"/public/v1/enlaces/{token}/otp", new VerificarOtpDto(codigo));
        verificado.Context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
        return (await verificado.ReadAsJsonAsync<SesionTerceroDto>())!;
    }

    private Task<IScenarioResult> PublicoAsync(string url, object? cuerpo) =>
        Host.Scenario(s =>
        {
            Preparar(s, "POST", url, cuerpo);
            IpAleatoria(s);
            s.IgnoreStatusCode();
        });

    private async Task<InvitacionAceptadaDto> AceptarAsync(SesionTerceroDto sesion, Guid vinculoId)
    {
        var invitacion = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{vinculoId}", sesion.AccessToken, null, 200);
        return await TerceroAsync<InvitacionAceptadaDto>("POST", $"/public/v1/invitaciones/{vinculoId}/aceptar", sesion.AccessToken,
            new AceptarInvitacionDto([.. invitacion.TextosPorAceptar.Select(t => new ConsentimientoDto(t.Tipo, t.TextoVersion, true))]), 200);
    }

    /// <summary>Paso 1, PUT directo a MinIO con la URL prefirmada y paso 2 (documento global del canónico).</summary>
    private async Task<ConfirmacionCargaDto> SubirAsync(SesionTerceroDto sesion, string canonico, byte[] contenido)
    {
        var carga = await TerceroAsync<CargaPrefirmadaDto>("POST", "/public/v1/boveda/documentos", sesion.AccessToken,
            new IniciarCargaDto(canonico.ToLowerInvariant() + ".pdf", contenido.Length, "application/pdf", TipoCanonico: canonico), 201);
        using var put = new HttpRequestMessage(HttpMethod.Put, carga.Carga!.Url) { Content = new ByteArrayContent(contenido) };
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(carga.Carga.Cabeceras["Content-Type"]);
        using var respuesta = await Http.SendAsync(put);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await TerceroAsync<ConfirmacionCargaDto>("POST",
            $"/public/v1/boveda/documentos/{carga.DocumentoId}/versiones/{carga.Version}/confirmar", sesion.AccessToken, null, 200);
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
                throw new TimeoutException($"El expediente {expedienteId} no llegó al estado esperado (está {expediente.Estado}).");
            await Task.Delay(100);
        }
    }

    private async Task InvocarAsync(SincronizarRequisitosDelVinculo mensaje)
    {
        using var alcance = Host.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(mensaje);
    }

    private async Task<IReadOnlyList<object>> EventosAsync(Guid tenantId, Guid streamId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(tenantId.ToString("N"));
        return [.. (await consulta.Events.FetchStreamAsync(streamId)).Select(x => x.Data)];
    }

    private Task<T> VeredictoAsync<T>(Escenario e, Guid expedienteId, Guid requisitoId, AreaDto area, Persona persona, VeredictoDto dto) =>
        ApiAsync<T>("POST", $"/api/v1/expedientes/{expedienteId}/requisitos/{requisitoId}/areas/{area.Id}/veredicto", Delegado(e.Norte, persona), dto, 200);

    private Task<IScenarioResult> VeredictoAsync(Escenario e, Guid expedienteId, Guid requisitoId, AreaDto area, Persona persona, VeredictoDto dto,
        int esperado) =>
        ApiAsync("POST", $"/api/v1/expedientes/{expedienteId}/requisitos/{requisitoId}/areas/{area.Id}/veredicto", Delegado(e.Norte, persona), dto,
            esperado);

    private Task<T> AreaAsync<T>(Escenario e, Guid expedienteId, AreaDto area, Persona persona, VeredictoDto dto) =>
        ApiAsync<T>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{area.Id}/veredicto", Delegado(e.Norte, persona), dto, 200);

    private Task<IScenarioResult> AreaAsync(Escenario e, Guid expedienteId, AreaDto area, Persona persona, VeredictoDto dto, int esperado) =>
        ApiAsync("POST", $"/api/v1/expedientes/{expedienteId}/areas/{area.Id}/veredicto", Delegado(e.Norte, persona), dto, esperado);

    private static RequisitoExpedienteDto Requisito(ExpedienteDto expediente, string codigo) =>
        expediente.Requisitos!.Single(r => r.TipoDocumento.Codigo == codigo);

    private static AreaExpedienteDto Area(ExpedienteDto expediente, string codigo) =>
        expediente.Etapas.SelectMany(x => x.Areas).Single(a => a.Codigo == codigo);

    private static Credencial Admin(Empresa empresa) => new(empresa.Admin, empresa.Datos.Id);

    /// <summary>La persona actúa desde el SRM con una aserción firmada por la clave privada del sistema (ADR-0011).</summary>
    private static Credencial Delegado(Empresa empresa, Persona p)
    {
        var ahora = DateTime.UtcNow;
        var asercion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "SRM",
            Audience = "validasoft",
            // jti de un solo uso (SE-4): cada petición lleva su propia aserción.
            Subject = new ClaimsIdentity([new Claim("sub", p.Sub), new Claim("name", p.Usuario.Nombre), new Claim("email", p.Usuario.Correo),
                new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora.AddSeconds(-5),
            NotBefore = ahora.AddSeconds(-5),
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(empresa.ClaveSrm), SecurityAlgorithms.RsaSha256)
        });
        return new Credencial(empresa.LlaveSrm, empresa.Datos.Id, asercion);
    }

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static string Nit() => "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>PDF mínimo válido de una página; el texto lo hace único (otro hash).</summary>
    private static byte[] Pdf(string texto) => Encoding.Latin1.GetBytes(new StringBuilder()
        .Append("%PDF-1.4\n")
        .Append(System.Globalization.CultureInfo.InvariantCulture, $"% {texto} {Guid.NewGuid():N}\n")
        .Append("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n")
        .Append("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n")
        .Append("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\n")
        .Append("trailer\n<< /Root 1 0 R >>\n%%EOF\n")
        .ToString());

    private static string Sha256(byte[] contenido) => Convert.ToHexStringLower(SHA256.HashData(contenido));

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
