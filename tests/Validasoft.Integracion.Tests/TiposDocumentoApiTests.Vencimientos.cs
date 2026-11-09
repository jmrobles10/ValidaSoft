using System.IO.Compression;
using System.Net;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Integracion.Tests;

public sealed partial class TiposDocumentoApiTests
{
    // ---------- vencimientos de la empresa (VE-1..VE-3) ----------

    [Fact]
    public async Task La_empresa_ve_sus_vencimientos_los_exporta_a_Excel_y_se_los_recuerda_al_tercero_una_vez_al_dia()
    {
        var e = await EscenarioFormularioAsync();
        var hoy = Hoy();
        var enviado = await TerceroAsync<ConfirmacionCargaDto>("POST", "/public/v1/boveda/formularios", e.Sesion.AccessToken,
            Formulario(e, ("fecha_expedicion", hoy.AddDays(-20).ToString("yyyy-MM-dd")), ("origen", "Nacional")), 200);
        var documentoId = enviado.Version.DocumentoId;
        (await EsperarEventoAsync<VigenciaDeVersionCalculada>(documentoId)).VigenteHasta.ShouldBe(hoy.AddDays(10));

        // Vence en 10 días (avisa a 5): entra en el horizonte de 30 y no en el de 5.
        var fila = (await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos?dias=30", Admin(e.Empresa), null, 200)).ShouldHaveSingleItem();
        fila.VinculoId.ShouldBe(e.Vinculo.Id);
        fila.DocumentoId.ShouldBe(documentoId);
        fila.Documento.ShouldBe("Declaración de origen");
        fila.VigenteHasta.ShouldBe(hoy.AddDays(10));
        fila.DiasRestantes.ShouldBe(10);
        fila.Estado.ShouldBe("Vigente");
        fila.Base.ShouldBe("FechaExpedicion");
        fila.FinDeGracia.ShouldBe(hoy.AddDays(12));
        fila.PermiteRatificar.ShouldBeFalse();
        fila.UltimoRecordatorio.ShouldBeNull();
        (await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos?dias=5", Admin(e.Empresa), null, 200)).ShouldBeEmpty();
        (await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos?estado=PorVencer", Admin(e.Empresa), null, 200)).ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/vencimientos?estado=Caducado", Admin(e.Empresa), null, 422))).ShouldBe("estado-invalido");

        // El mismo informe en Excel.
        using var cliente = Host.Server.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/v1/vencimientos/excel?dias=30");
        pedido.Headers.Add("X-Api-Key", e.Empresa.Admin);
        pedido.Headers.Add("X-Empresa", e.Empresa.Datos.Id.ToString());
        using var excel = await cliente.SendAsync(pedido);
        excel.StatusCode.ShouldBe(HttpStatusCode.OK);
        excel.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        excel.Content.Headers.ContentDisposition!.FileNameStar.ShouldStartWith("vencimientos-");
        using (var zip = new ZipArchive(new MemoryStream(await excel.Content.ReadAsByteArrayAsync())))
        {
            using var lector = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
            var hoja = await lector.ReadToEndAsync();
            hoja.ShouldContain("Declaración de origen");
            hoja.ShouldContain("Vigente hasta");
            hoja.ShouldContain($"<v>{hoy.AddDays(10).DayNumber - new DateOnly(1899, 12, 30).DayNumber}</v>"); // la fecha como fecha de Excel
        }

        // Recordarlo: un correo al tercero con la fecha para esta empresa; repetido el mismo día, se omite.
        var lote = new RecordarVencimientosDto([new VencimientoARecordarDto(e.Vinculo.Id, documentoId)]);
        var enviadoHoy = await ApiAsync<RecordatorioEnviadoDto>("POST", "/api/v1/vencimientos/recordar", Admin(e.Empresa), lote, 200);
        enviadoHoy.ShouldBe(new RecordatorioEnviadoDto(1, 1, 0));
        var correo = await fixture.Correos.EsperarAsync(e.Correo, m => m.Asunto.Contains("vencer"));
        correo.Texto.ShouldContain($"Declaración de origen: vence para {e.Empresa.Nombre} el");
        (await ApiAsync<RecordatorioEnviadoDto>("POST", "/api/v1/vencimientos/recordar", Admin(e.Empresa), lote, 200))
            .ShouldBe(new RecordatorioEnviadoDto(0, 0, 1));
        (await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos", Admin(e.Empresa), null, 200)).Single().UltimoRecordatorio.ShouldNotBeNull();
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/vencimientos/recordar", Admin(e.Empresa), new RecordarVencimientosDto([]), 422)))
            .ShouldBe("lote-vacio");
    }
}
