using System.Text.Json;
using System.Text.Json.Nodes;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- integración de la ficha con el SRM (F3: FM-32…) ----------

    /// <summary>Aprueba con el administrador cada área que aún no aprobó, hasta que el expediente quede aprobado.</summary>
    private async Task<ExpedienteDto> AprobarComoAdministradorAsync(Empresa empresa, Guid expedienteId)
    {
        var url = $"/api/v1/expedientes/{expedienteId}";
        var expediente = await ApiAsync<ExpedienteDto>("GET", url, Admin(empresa), null, 200);
        foreach (var area in expediente.Etapas.SelectMany(t => t.Areas).Select(a => a.AreaId).ToList())
        {
            expediente = await ApiAsync<ExpedienteDto>("GET", url, Admin(empresa), null, 200);
            if (expediente.Estado == "Aprobado")
                break;
            if (expediente.Etapas.SelectMany(t => t.Areas).Single(a => a.AreaId == area).Estado == "Aprobada")
                continue;
            expediente = await ApiAsync<ExpedienteDto>("POST", $"{url}/areas/{area}/aprobar-como-administrador", Admin(empresa),
                new JustificacionDto("Revisada con el área."), 200);
        }

        return expediente;
    }

    /// <summary>Los <c>seccion.aceptada</c> del vínculo en el modo pull (la suscripción corre después del commit).</summary>
    private async Task<List<EventoPublicoDto>> SeccionesAceptadasAsync(Empresa empresa, Guid vinculoId, int esperados)
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var pagina = await ApiAsync<PaginaEventosDto>("GET", "/api/v1/eventos?tipos=seccion.aceptada&limite=100", Admin(empresa), null, 200);
            var deEste = pagina.Datos.Where(x => x.Datos.GetProperty("vinculoId").GetGuid() == vinculoId).ToList();
            if (deEste.Count >= esperados)
                return deEste;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"Llegaron {deEste.Count} de {esperados} seccion.aceptada.");
            await Task.Delay(200);
        }
    }

    [Fact]
    public async Task Al_aprobarse_el_expediente_sale_seccion_aceptada_por_webhook_y_pull_y_la_ficha_del_vinculo_trae_lo_aceptado()
    {
        var e = await PrepararAsync();
        await using var receptor = await NotificacionesApiTests.ReceptorDeWebhooks.IniciarAsync();
        var srm = (await ApiAsync<List<SistemaIntegradoDto>>("GET", "/api/v1/sistemas", new Credencial(e.Norte.Admin), null, 200)).Single(s => s.Codigo == "SRM");
        await ApiAsync<WebhookDto>("POST", "/api/v1/webhooks", Admin(e.Norte),
            new CrearWebhookDto(receptor.Url, ["seccion.aceptada"], [e.Norte.Datos.Id.ToString()], srm.Id, "SRM: ficha aceptada"), 201);

        // El catálogo de eventos lo incluye, con su descripción.
        var tipos = await ApiAsync<List<TipoEventoPublicoDto>>("GET", "/api/v1/eventos/tipos", Admin(e.Norte), null, 200);
        tipos.Single(t => t.Tipo == "seccion.aceptada").Descripcion.ShouldContain("ERP");

        await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(Seccion: "FICHA_TRIBUTARIA", AreasQueRevisan: [e.Cumplimiento.Id])
        ]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        var rutaFicha = $"/api/v1/vinculos/{vinculo.Id}/ficha";

        // Antes de aprobar no hay nada aceptado.
        var vacia = await ApiAsync<FichaDelVinculoDto>("GET", rutaFicha, Admin(e.Norte), null, 200);
        (vacia.VinculoId, vacia.TerceroId, vacia.TipoPersona).ShouldBe((vinculo.Id, vinculo.TerceroId, "Juridica"));
        vacia.Secciones.ShouldBeEmpty();
        vacia.RepresentanteLegal.ShouldBeNull();
        vacia.CuentaDesignada.ShouldBeNull();

        // El tercero diligencia la general y la tributaria (05, 07, 48, 52: el 07 NO lo hace agente retenedor de IVA).
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA", TributariaCompleta);
        await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        (await AprobarComoAdministradorAsync(e.Norte, expedienteId)).Estado.ShouldBe("Aprobado");

        // Una seccion.aceptada por sección, por el pull de eventos…
        var pull = await SeccionesAceptadasAsync(e.Norte, vinculo.Id, 2);
        pull.ShouldAllBe(x => x.EmpresaId == e.Norte.Datos.Id && x.Tipo == "seccion.aceptada");
        var general = pull.Single(x => x.Datos.GetProperty("seccion").GetString() == "FICHA_GENERAL").Datos;
        (general.GetProperty("terceroId").GetGuid(), general.GetProperty("empresaId").GetGuid(), general.GetProperty("version").GetInt32(),
            general.GetProperty("expedienteId").GetGuid()).ShouldBe((vinculo.TerceroId, e.Norte.Datos.Id, 1, expedienteId));
        general.GetProperty("periodo").ValueKind.ShouldBe(JsonValueKind.Null);
        var contenidoGeneral = general.GetProperty("contenido").GetProperty("general");
        contenidoGeneral.GetProperty("nombre").GetString().ShouldBe("Aceros del Norte S.A.S.");
        contenidoGeneral.GetProperty("ciudad").GetProperty("nombre").GetString().ShouldBe("Bogotá, D.C.");
        contenidoGeneral.GetProperty("ciudad").GetProperty("codigoDepartamento").GetString().ShouldBe("11");
        contenidoGeneral.GetProperty("ciiuPrincipal").GetProperty("codigo").GetString().ShouldBe("4663");
        general.GetProperty("contenido").GetProperty("tributaria").ValueKind.ShouldBe(JsonValueKind.Null);

        var tributaria = pull.Single(x => x.Datos.GetProperty("seccion").GetString() == "FICHA_TRIBUTARIA").Datos.GetProperty("contenido")
            .GetProperty("tributaria");
        tributaria.GetProperty("responsabilidades").EnumerateArray().Select(r => r.GetProperty("codigo").GetString()).ShouldBe(["05", "07", "48", "52"]);
        var calidades = tributaria.GetProperty("calidades");
        (calidades.GetProperty("responsableIva").GetBoolean(), calidades.GetProperty("declaranteRenta").GetBoolean(),
            calidades.GetProperty("obligadoAFacturar").GetBoolean(), calidades.GetProperty("agenteRetenedorIva").GetBoolean(),
            calidades.GetProperty("granContribuyente").GetBoolean()).ShouldBe((true, true, true, false, false));
        calidades.GetProperty("granContribuyenteBogota").ValueKind.ShouldBe(JsonValueKind.Null);
        tributaria.GetProperty("calidadesSinDerivar").EnumerateArray().Select(c => c.GetProperty("calidad").GetString()).ShouldContain("autorretenedorIca");

        // …y por webhook al receptor del SRM, con el mismo cuerpo.
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (receptor.Recibidos.Count(r => r.VinculoId == vinculo.Id) < 2 && DateTime.UtcNow < limite)
            await Task.Delay(200);
        var recibidos = receptor.Recibidos.Where(r => r.VinculoId == vinculo.Id).ToList();
        recibidos.Count.ShouldBe(2);
        recibidos.ShouldAllBe(r => r.Tipo == "seccion.aceptada" && r.TipoCabecera == "seccion.aceptada");
        foreach (var evento in pull)
            JsonNode.DeepEquals(JsonNode.Parse(recibidos.Single(r => r.Id == evento.Id).Cuerpo)!["datos"], JsonNode.Parse(evento.Datos.GetRawText()))
                .ShouldBeTrue();

        // GET /vinculos/{id}/ficha: lo aceptado, con el mismo contenido que el evento.
        var ficha = await ApiAsync<FichaDelVinculoDto>("GET", rutaFicha, Admin(e.Norte), null, 200);
        ficha.Secciones.Select(s => (s.Seccion, s.Version, s.VersionVigente, s.VersionNuevaPendiente, s.ExpedienteId)).ShouldBe(
        [
            ("FICHA_GENERAL", 1, (int?)1, false, expedienteId),
            ("FICHA_TRIBUTARIA", 1, (int?)1, false, expedienteId)
        ]);
        ficha.Secciones[0].Contenido.General!.Direccion.ShouldBe("Calle 45 # 12-08");
        ficha.Secciones[1].Contenido.Tributaria!.Calidades.ShouldBe(new CalidadesTributariasDto(true, false, true, false, null, false, null, false, null,
            null, null, null, false, true));
        var delEvento = pull.Single(x => x.Datos.GetProperty("seccion").GetString() == "FICHA_TRIBUTARIA").Datos.GetProperty("contenido");
        JsonNode.DeepEquals(JsonNode.Parse(delEvento.GetRawText()), JsonSerializer.SerializeToNode(ficha.Secciones[1].Contenido, JsonWeb)).ShouldBeTrue();
        ficha.RepresentanteLegal.ShouldBeNull(); // jurídica sin la legal aceptada

        // El tercero cambia la general (con el RUT) ⇒ la vigente es otra: lo aceptado no cambia y se marca la pendiente.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"),
            new SoporteSeccionDto(rut.DocumentoId, rut.N));
        var conPendiente = (await ApiAsync<FichaDelVinculoDto>("GET", rutaFicha, Admin(e.Norte), null, 200)).Secciones[0];
        (conPendiente.Version, conPendiente.VersionVigente, conPendiente.VersionNuevaPendiente, conPendiente.Contenido.General!.Direccion).ShouldBe(
            (1, (int?)2, true, "Calle 45 # 12-08"));

        // Scopes: hacen falta vinculos:leer y terceros:leer; otra empresa del tenant no lo ve (404).
        var soloVinculos = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Norte.Admin),
            new CrearApiKeyDto("Solo vínculos", ["vinculos:leer"], null, null), 201);
        await ApiAsync("GET", rutaFicha, new Credencial(soloVinculos.Valor!, e.Norte.Datos.Id), null, 403);
        var lector = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(e.Norte.Admin),
            new CrearApiKeyDto("Ficha", ["vinculos:leer", "terceros:leer"], null, null), 201);
        (await ApiAsync<FichaDelVinculoDto>("GET", rutaFicha, new Credencial(lector.Valor!, e.Norte.Datos.Id), null, 200)).Secciones.Count.ShouldBe(2);

        var otra = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(e.Norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", Nit()), "Otra del grupo S.A.S.", "OTRA", null, null), 201);
        (await CodigoAsync(await ApiAsync("GET", rutaFicha, new Credencial(e.Norte.Admin, otra.Id), null, 404))).ShouldBe("vinculo-no-encontrado");
    }

    private static readonly JsonSerializerOptions JsonWeb = CrearJsonWeb();

    private static JsonSerializerOptions CrearJsonWeb()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opciones.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return opciones;
    }
}
