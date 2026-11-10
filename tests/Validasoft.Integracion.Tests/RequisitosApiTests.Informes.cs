using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- Informes y exportaciones (IF-1…IF-7) ----------

    private sealed record Descarga(HttpStatusCode Estado, string? Tipo, string? Nombre, byte[] Contenido);

    private async Task<Descarga> DescargarAsync(string url, Credencial credencial)
    {
        using var cliente = Host.Server.CreateClient();
        using var pedido = new HttpRequestMessage(HttpMethod.Get, url);
        pedido.Headers.Add("X-Api-Key", credencial.ApiKey);
        if (credencial.Empresa is not null)
            pedido.Headers.Add("X-Empresa", credencial.Empresa.Value.ToString());
        if (credencial.Delegado is not null)
            pedido.Headers.Add("X-Usuario-Delegado", credencial.Delegado);
        using var respuesta = await cliente.SendAsync(pedido);
        return new Descarga(respuesta.StatusCode, respuesta.Content.Headers.ContentType?.MediaType,
            respuesta.Content.Headers.ContentDisposition?.FileNameStar ?? respuesta.Content.Headers.ContentDisposition?.FileName,
            await respuesta.Content.ReadAsByteArrayAsync());
    }

    private static async Task<string> HojaAsync(byte[] libro, int numero)
    {
        using var zip = new ZipArchive(new MemoryStream(libro));
        using var lector = new StreamReader(zip.GetEntry($"xl/worksheets/sheet{numero}.xml")!.Open());
        return await lector.ReadToEndAsync();
    }

    private static int Filas(string hoja) => hoja.Split("<row ").Length - 1;

    [Fact]
    public async Task El_informe_de_estado_mide_los_tiempos_por_area_coincide_con_Inicio_y_se_exporta_a_Excel_y_PDF()
    {
        var a = await ProveedorAprobadoAsync();
        // Un segundo proveedor que aún no acepta la invitación (sin expediente) y un tercero con el expediente en revisión.
        await CrearVinculoAsync(a.E.Norte, "PROVEEDOR", CorreoNuevo());
        var correo = CorreoNuevo();
        var enCurso = await CrearVinculoAsync(a.E.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT otro"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara otro"));
        await AceptarAsync(sesion, enCurso.Id);
        var abierto = await ExpedienteAbiertoAsync(a.E, enCurso.Id);
        await EsperarAsync(a.E.Norte, abierto, x => x.Estado == "EnRevision");

        var informe = await ApiAsync<InformeEstadoExpedientesDto>("GET", "/api/v1/informes/estado-expedientes", Admin(a.E.Norte), null, 200);
        informe.Resumen.Total.ShouldBe(2);
        informe.Resumen.PorEstado.Select(c => (c.Clave, c.Cantidad)).ShouldBe([("EnRevision", 1), ("Aprobado", 1)], ignoreOrder: true);
        informe.Resumen.Aprobados.ShouldBe(1);
        informe.Datos.Select(f => f.Id).ShouldBe([abierto, a.Expediente]); // lo más reciente primero

        var aprobado = informe.Datos.Single(f => f.Id == a.Expediente);
        aprobado.Requisitos.ShouldBe(2);
        aprobado.RequisitosAprobados.ShouldBe(2);
        aprobado.DiasHastaAprobacion.ShouldBe(aprobado.DiasTotales);
        aprobado.CerradoEn.ShouldNotBeNull();
        aprobado.Areas.Select(x => (x.Codigo, x.Estado, x.Rondas)).ShouldBe([("COMPRAS", "Aprobada", 1), ("CUMPLIMIENTO", "Aprobada", 1)], ignoreOrder: true);
        var revision = informe.Datos.Single(f => f.Id == abierto);
        revision.DiasHastaAprobacion.ShouldBeNull();
        revision.Areas.ShouldAllBe(x => x.Estado == "Pendiente" && x.Rondas == 1);
        informe.Resumen.PorArea.Single(x => x.Codigo == "COMPRAS").Pendientes.ShouldBe(1);
        informe.Resumen.PorArea.Single(x => x.Codigo == "COMPRAS").Expedientes.ShouldBe(2);

        // Coherente con Inicio: mismo tiempo hasta la aprobación con la misma ventana (hoy y los 6 días anteriores, por cierre).
        var hoy = Dominio.Documentos.CalculadoraDeVigencia.Fecha(DateTimeOffset.UtcNow);
        var indicadores = await ApiAsync<IndicadoresDto>("GET", "/api/v1/indicadores?dias=7", Admin(a.E.Norte), null, 200);
        var porCierre = await ApiAsync<InformeEstadoExpedientesDto>("GET",
            $"/api/v1/informes/estado-expedientes?fecha=Cierre&estado=Aprobado&desde={hoy.AddDays(-6):yyyy-MM-dd}", Admin(a.E.Norte), null, 200);
        porCierre.Resumen.Aprobados.ShouldBe(indicadores.ExpedientesAprobados);
        porCierre.Resumen.TiempoPromedioDias.ShouldBe(indicadores.TiempoPromedioDias);
        porCierre.Resumen.TiempoMedianaDias.ShouldBe(indicadores.TiempoMedianaDias);

        // Filtros y paginación.
        (await ApiAsync<InformeEstadoExpedientesDto>("GET", $"/api/v1/informes/estado-expedientes?terceroId={a.Vinculo.TerceroId}", Admin(a.E.Norte), null, 200))
            .Datos.ShouldHaveSingleItem().Id.ShouldBe(a.Expediente);
        (await ApiAsync<InformeEstadoExpedientesDto>("GET", $"/api/v1/informes/estado-expedientes?areaId={a.E.Compras.Id}&tipo=Inicial&flujoId={a.E.Flujo.Id}",
            Admin(a.E.Norte), null, 200)).Resumen.Total.ShouldBe(2);
        (await ApiAsync<InformeEstadoExpedientesDto>("GET", $"/api/v1/informes/estado-expedientes?desde={hoy.AddDays(1):yyyy-MM-dd}", Admin(a.E.Norte), null, 200))
            .Resumen.Total.ShouldBe(0);
        var pagina = await ApiAsync<InformeEstadoExpedientesDto>("GET", "/api/v1/informes/estado-expedientes?limite=1", Admin(a.E.Norte), null, 200);
        pagina.Datos.Count.ShouldBe(1);
        pagina.Resumen.Total.ShouldBe(2);
        pagina.SiguienteCursor.ShouldNotBeNull();
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/informes/estado-expedientes?estado=Perdido", Admin(a.E.Norte), null, 422))).ShouldBe("estado-invalido");
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/informes/estado-expedientes?desde=2026-02-01&hasta=2026-01-01", Admin(a.E.Norte), null, 422)))
            .ShouldBe("rango-invalido");
        // Un revisor también lo ve (expedientes:leer).
        (await ApiAsync<InformeEstadoExpedientesDto>("GET", "/api/v1/informes/estado-expedientes", Delegado(a.E.Norte, a.E.Laura), null, 200)).Resumen.Total.ShouldBe(2);

        // Excel: tres hojas; una fila por expediente y una por expediente × área.
        var excel = await DescargarAsync("/api/v1/informes/estado-expedientes/excel", Admin(a.E.Norte));
        excel.Estado.ShouldBe(HttpStatusCode.OK);
        excel.Tipo.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        excel.Nombre!.ShouldStartWith("estado-expedientes-norte-sas-");
        var expedientes = await HojaAsync(excel.Contenido, 1);
        Filas(expedientes).ShouldBe(3);
        expedientes.ShouldContain("Días en revisión");
        expedientes.ShouldContain(aprobado.Folio);
        expedientes.ShouldContain(revision.Folio);
        expedientes.ShouldContain("En revisión");
        var areas = await HojaAsync(excel.Contenido, 2);
        Filas(areas).ShouldBe(5);
        areas.ShouldContain("Cumplimiento");
        var resumen = await HojaAsync(excel.Contenido, 3);
        resumen.ShouldContain("Días hasta la aprobación (promedio)");
        using (var libro = new ZipArchive(new MemoryStream(excel.Contenido)))
        {
            using var lector = new StreamReader(libro.GetEntry("xl/workbook.xml")!.Open());
            var indice = await lector.ReadToEndAsync();
            indice.ShouldContain("name=\"Expedientes\"");
            indice.ShouldContain("name=\"Tiempos por área\"");
            indice.ShouldContain("name=\"Resumen\"");
        }

        // PDF horizontal con el folio de cada expediente.
        var pdf = await DescargarAsync("/api/v1/informes/estado-expedientes/pdf?estado=Aprobado", Admin(a.E.Norte));
        pdf.Estado.ShouldBe(HttpStatusCode.OK);
        pdf.Tipo.ShouldBe("application/pdf");
        var texto = Encoding.Latin1.GetString(pdf.Contenido);
        texto.ShouldStartWith("%PDF-1.7");
        texto.ShouldContain("/MediaBox [0 0 841.89 595.28]");
        texto.ShouldContain($"({aprobado.Folio})");
        texto.ShouldNotContain($"({revision.Folio})");
        texto.ShouldContain("(Estado de expedientes)");

        // Cada exportación queda en la auditoría de accesos, con sus filtros.
        var accesos = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Acceso&entidad=Exportacion", Admin(a.E.Norte), null, 200);
        accesos.Datos.Count.ShouldBe(2);
        accesos.Datos.ShouldAllBe(r => r.Accion == "Exportado" && r.Entidad.Nombre == "Informe de estado de expedientes");
        accesos.Datos.First().Detalle!.ShouldContain("Pdf · 1 filas");
        accesos.Datos.First().Detalle!.ShouldContain("estado=Aprobado");
    }

    [Fact]
    public async Task El_informe_de_vinculos_trae_certificado_expediente_en_curso_y_vencimientos_y_se_exporta_a_Excel()
    {
        var a = await ProveedorAprobadoAsync();
        var invitado = await CrearVinculoAsync(a.E.Norte, "PROVEEDOR", CorreoNuevo());

        var informe = await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos", Admin(a.E.Norte), null, 200);
        informe.Resumen.Total.ShouldBe(2);
        informe.Resumen.ConCertificadoVigente.ShouldBe(1);
        var activo = informe.Datos.Single(f => f.VinculoId == a.Vinculo.Id);
        activo.Estado.ShouldBe("Activo");
        activo.Certificado.ShouldNotBeNull().Estado.ShouldBe("Vigente");
        activo.ExpedienteEnCurso.ShouldBeNull();
        activo.SeccionesPendientes.ShouldBeEmpty();
        var vencimientos = await ApiAsync<List<VencimientoDto>>("GET", "/api/v1/vencimientos?dias=0", Admin(a.E.Norte), null, 200);
        activo.Documentos.ShouldNotBeNull().ConVencimiento.ShouldBe(vencimientos.Count(v => v.VinculoId == a.Vinculo.Id));
        var pendiente = informe.Datos.Single(f => f.VinculoId == invitado.Id);
        pendiente.Estado.ShouldBe("Invitado");
        pendiente.Certificado.ShouldBeNull();
        pendiente.InvitacionAceptada.ShouldBeFalse();

        (await ApiAsync<InformeVinculosDto>("GET", "/api/v1/informes/vinculos?estado=Invitado", Admin(a.E.Norte), null, 200))
            .Datos.ShouldHaveSingleItem().VinculoId.ShouldBe(invitado.Id);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/informes/vinculos?estado=Raro", Admin(a.E.Norte), null, 422))).ShouldBe("estado-invalido");

        var excel = await DescargarAsync("/api/v1/informes/vinculos/excel", Admin(a.E.Norte));
        excel.Estado.ShouldBe(HttpStatusCode.OK);
        excel.Nombre!.ShouldStartWith("vinculos-norte-sas-");
        var hoja = await HojaAsync(excel.Contenido, 1);
        Filas(hoja).ShouldBe(3);
        hoja.ShouldContain("Secciones pendientes de validar");
        hoja.ShouldContain("Vigente");
        hoja.ShouldContain("Invitado");
        (await HojaAsync(excel.Contenido, 2)).ShouldContain("Con certificado vigente");
    }

    [Fact]
    public async Task La_auditoria_se_exporta_a_Excel_con_los_filtros_de_la_pantalla_y_solo_con_auditoria_exportar()
    {
        var e = await PrepararAsync();
        var excel = await DescargarAsync("/api/v1/auditoria/excel?tipo=Configuracion&entidad=Area", Admin(e.Norte));
        excel.Estado.ShouldBe(HttpStatusCode.OK);
        excel.Nombre!.ShouldStartWith("auditoria-norte-sas-");
        var hoja = await HojaAsync(excel.Contenido, 1);
        var pantalla = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Configuracion&entidad=Area&limite=200", Admin(e.Norte), null, 200);
        pantalla.Datos.Count.ShouldBeGreaterThan(0);
        Filas(hoja).ShouldBe(pantalla.Datos.Count + 1);
        hoja.ShouldContain("Cambios");
        hoja.ShouldContain("COMPRAS");
        hoja.ShouldNotContain("HAB_PROVEEDOR"); // el flujo no es un área: el filtro se respeta

        // Un revisor no exporta la auditoría.
        (await DescargarAsync("/api/v1/auditoria/excel", Delegado(e.Norte, e.Laura))).Estado.ShouldBe(HttpStatusCode.Forbidden);

        // La exportación queda registrada y sale en la siguiente.
        var segunda = await HojaAsync((await DescargarAsync("/api/v1/auditoria/excel?tipo=Acceso", Admin(e.Norte))).Contenido, 1);
        segunda.ShouldContain("Exportado");
        segunda.ShouldContain("Auditoría");
    }

    [Fact]
    public async Task El_expediente_se_exporta_en_ZIP_con_sus_documentos_aprobados_resumen_e_historial_y_cada_acceso_queda_auditado()
    {
        var a = await ProveedorAprobadoAsync();
        var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{a.Expediente}", Admin(a.E.Norte), null, 200);

        var zip = await DescargarAsync($"/api/v1/expedientes/{a.Expediente}/exportar", Admin(a.E.Norte));
        zip.Estado.ShouldBe(HttpStatusCode.OK);
        zip.Tipo.ShouldBe("application/zip");
        zip.Nombre.ShouldBe($"expediente-{expediente.Folio}.zip");
        using (var archivo = new ZipArchive(new MemoryStream(zip.Contenido)))
        {
            archivo.Entries.Select(x => x.FullName).Order().ShouldBe(
                ["documentos/CAMARA_COMERCIO-v1-camara_comercio.pdf", "documentos/RUT-v1-rut.pdf", "indice.json", "linea-de-tiempo.json", "resumen.pdf"]);
            foreach (var codigo in new[] { "RUT", "CAMARA_COMERCIO" })
            {
                using var copia = new MemoryStream();
                await using (var flujo = archivo.Entries.Single(x => x.FullName.StartsWith($"documentos/{codigo}-")).Open())
                    await flujo.CopyToAsync(copia);
                Convert.ToHexStringLower(SHA256.HashData(copia.ToArray())).ShouldBe(Requisito(expediente, codigo).VersionActual!.Sha256);
            }

            using var indice = JsonDocument.Parse(archivo.GetEntry("indice.json")!.Open());
            indice.RootElement.GetProperty("expediente").GetProperty("folio").GetString().ShouldBe(expediente.Folio);
            var archivos = indice.RootElement.GetProperty("archivos").EnumerateArray().ToList();
            archivos.Count.ShouldBe(4);
            archivos.Where(x => x.TryGetProperty("coincideConElExpediente", out _)).ShouldAllBe(x => x.GetProperty("coincideConElExpediente").GetBoolean());

            using var linea = JsonDocument.Parse(archivo.GetEntry("linea-de-tiempo.json")!.Open());
            linea.RootElement.EnumerateArray().First().GetProperty("tipo").GetString()!.ShouldContain("expediente_abierto");

            using var resumen = new MemoryStream();
            await using (var flujo = archivo.GetEntry("resumen.pdf")!.Open())
                await flujo.CopyToAsync(resumen);
            var texto = Encoding.Latin1.GetString(resumen.ToArray());
            texto.ShouldStartWith("%PDF-1.7");
            texto.ShouldContain($"(Expediente {expediente.Folio})");
            texto.ShouldContain("(Expediente aprobado)");
            texto.ShouldContain("(Laura Compras)");
        }

        // Auditoría: la exportación del expediente y el acceso a cada documento con su motivo.
        var exportacion = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET",
            $"/api/v1/auditoria?tipo=Acceso&entidad=Expediente&entidadId={a.Expediente}", Admin(a.E.Norte), null, 200);
        var registro = exportacion.Datos.ShouldHaveSingleItem();
        registro.Accion.ShouldBe("Exportado");
        registro.Entidad.Nombre.ShouldBe(expediente.Folio);
        registro.Detalle!.ShouldContain($"SHA-256 {Convert.ToHexStringLower(SHA256.HashData(zip.Contenido))}");
        var documentos = await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?tipo=Acceso&entidad=Documento", Admin(a.E.Norte), null, 200);
        documentos.Datos.Count(r => r.Detalle == $"Exportación del expediente {expediente.Folio}").ShouldBe(2);

        // Un revisor no exporta expedientes; un expediente de otra empresa no existe.
        (await DescargarAsync($"/api/v1/expedientes/{a.Expediente}/exportar", Delegado(a.E.Norte, a.E.Laura))).Estado.ShouldBe(HttpStatusCode.Forbidden);
        (await DescargarAsync($"/api/v1/expedientes/{Guid.NewGuid()}/exportar", Admin(a.E.Norte))).Estado.ShouldBe(HttpStatusCode.NotFound);
    }
}
