using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Habeas;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Habeas;
using Wolverine.Tracking;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Exportación firmada de la bóveda del tercero (OD-9, OD-10) con PostgreSQL y MinIO reales: se pide, se genera en segundo plano, se baja
/// por una URL prefirmada y lleva sus archivos, sus datos y un manifiesto firmado que se verifica con la clave publicada.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private async Task<ExportacionBovedaDto> EsperarExportacionAsync(SesionTerceroDto sesion, Guid id, string estado)
    {
        var limite = DateTime.UtcNow.AddSeconds(40);
        while (true)
        {
            var exportacion = await TerceroAsync<ExportacionBovedaDto>("GET", $"/public/v1/exportaciones/{id}", sesion.AccessToken, null, 200);
            if (exportacion.Estado == estado)
                return exportacion;
            exportacion.Estado.ShouldNotBe("Fallida", $"la exportación falló: {exportacion.CodigoDeError}");
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La exportación {id} no llegó a {estado} (está {exportacion.Estado}).");
            await Task.Delay(200);
        }
    }

    [Fact]
    public async Task El_tercero_exporta_su_boveda_en_un_zip_con_sus_archivos_sus_datos_y_un_manifiesto_firmado_que_se_verifica()
    {
        var (e, vinculo, sesion, correo) = await TerceroConAprobacionAsync();
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación para la exportación"));
        await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await SolicitarAsync(sesion, "Consulta", empresa: e.Norte.Datos.Id, detalle: "Para el expediente de la exportación.");

        // Se pide (202) y no se pide otra mientras haya una en curso.
        var pedida = await TerceroAsync<ExportacionBovedaDto>("POST", "/public/v1/boveda/exportar", sesion.AccessToken, null, 202);
        pedida.Estado.ShouldBeOneOf("Pendiente", "Generando", "Lista");
        if (pedida.Estado != "Lista")
            (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/exportar", sesion.AccessToken, null, 409))).ShouldBe("exportacion-en-curso");

        // Se genera en segundo plano; avisa por correo y no se descarga antes de estar lista.
        var lista = await EsperarExportacionAsync(sesion, pedida.Id, "Lista");
        lista.Archivos.ShouldBeGreaterThanOrEqualTo(8);
        lista.Sha256.ShouldNotBeNullOrEmpty();
        lista.HashManifiesto.ShouldNotBeNullOrEmpty();
        lista.ExpiraEn.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(6));
        (await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("exportación de la bóveda está lista"))).Texto.ShouldContain("privacidad");
        (await TerceroAsync<List<ExportacionBovedaDto>>("GET", "/public/v1/exportaciones", sesion.AccessToken, null, 200)).Select(x => x.Id).ShouldContain(pedida.Id);

        // Solo su dueño la ve y la baja.
        var (_, _, otraSesion, _) = await TerceroConAprobacionAsync();
        await TerceroAsync("GET", $"/public/v1/exportaciones/{pedida.Id}", otraSesion.AccessToken, null, 404);
        await TerceroAsync("POST", $"/public/v1/exportaciones/{pedida.Id}/descarga", otraSesion.AccessToken, null, 404);

        // Descarga por URL prefirmada: el ZIP trae el SHA-256 que dice la exportación.
        var descarga = await TerceroAsync<DescargaDto>("POST", $"/public/v1/exportaciones/{pedida.Id}/descarga", sesion.AccessToken, null, 200);
        descarga.Mime.ShouldBe("application/zip");
        using var respuesta = await Http.GetAsync(descarga.Url);
        respuesta.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Convert.ToHexStringLower(SHA256.HashData(bytes)).ShouldBe(lista.Sha256);
        (await TerceroAsync<ExportacionBovedaDto>("GET", $"/public/v1/exportaciones/{pedida.Id}", sesion.AccessToken, null, 200)).Descargas.ShouldBe(1);

        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entradas = zip.Entries.ToDictionary(x => x.FullName, x => Leer(x));
        entradas.Keys.ShouldContain("manifiesto.json");
        entradas.Keys.ShouldContain("manifiesto.firma.json");
        entradas.Keys.ShouldContain("LEEME.txt");
        entradas.Keys.Count(k => k.StartsWith("documentos/RUT/")).ShouldBe(1);
        entradas.Keys.Count(k => k.StartsWith("documentos/CAMARA_COMERCIO/")).ShouldBe(1);
        entradas.Keys.Count(k => k.StartsWith("documentos/CERTIFICACION_BANCARIA/")).ShouldBe(1);
        foreach (var dato in new[] { "datos/tercero.json", "datos/documentos.json", "datos/ficha/FICHA_GENERAL.json", "datos/cuentas-bancarias.json", "datos/vinculos.json", "datos/certificados.json", "datos/solicitudes-habeas-data.json" })
            entradas.Keys.ShouldContain(dato);

        // Los datos son los del tercero, con la cuenta enmascarada y sin el número completo.
        Encoding.UTF8.GetString(entradas["datos/tercero.json"]).ShouldContain("Tercero 9");
        var cuentas = Encoding.UTF8.GetString(entradas["datos/cuentas-bancarias.json"]);
        cuentas.ShouldContain("****5678");
        cuentas.ShouldNotContain("03112345678");
        Encoding.UTF8.GetString(entradas["datos/vinculos.json"]).ShouldContain("Norte S.A.S.");
        Encoding.UTF8.GetString(entradas["datos/solicitudes-habeas-data.json"]).ShouldContain("HD-");

        // El manifiesto lista cada archivo con su SHA-256 real y es el JSON canónico que se firmó.
        var manifiestoBytes = entradas["manifiesto.json"];
        var manifiesto = JsonDocument.Parse(manifiestoBytes).RootElement;
        manifiesto.GetProperty("esquema").GetString().ShouldBe("validasoft.exportacion-boveda.v1");
        manifiesto.GetProperty("terceroId").GetGuid().ShouldBe(vinculo.TerceroId);
        var archivos = manifiesto.GetProperty("archivos").EnumerateArray().ToList();
        manifiesto.GetProperty("totalArchivos").GetInt32().ShouldBe(archivos.Count);
        archivos.Count.ShouldBe(lista.Archivos);
        archivos.Select(a => a.GetProperty("ruta").GetString()!).OrderBy(r => r, StringComparer.Ordinal).ShouldBe(entradas.Keys.Where(k => k is not ("manifiesto.json" or "manifiesto.firma.json" or "LEEME.txt")).OrderBy(r => r, StringComparer.Ordinal));
        foreach (var archivo in archivos)
        {
            var ruta = archivo.GetProperty("ruta").GetString()!;
            Convert.ToHexStringLower(SHA256.HashData(entradas[ruta])).ShouldBe(archivo.GetProperty("sha256").GetString());
            entradas[ruta].LongLength.ShouldBe(archivo.GetProperty("bytes").GetInt64());
            if (archivo.GetProperty("tipo").GetString() == "documento")
                archivo.GetProperty("coincideConLaBoveda").GetBoolean().ShouldBeTrue();
        }

        manifiestoBytes.ShouldBe(Encoding.UTF8.GetBytes(Jcs.Canonicalizar(Encoding.UTF8.GetString(manifiestoBytes))));
        Convert.ToHexStringLower(SHA256.HashData(manifiestoBytes)).ShouldBe(lista.HashManifiesto);

        // La firma Ed25519 verifica con la clave que Validasoft publica en .well-known.
        var firma = JsonDocument.Parse(entradas["manifiesto.firma.json"]).RootElement;
        firma.GetProperty("algoritmo").GetString().ShouldBe("Ed25519");
        firma.GetProperty("hashManifiesto").GetString().ShouldBe(lista.HashManifiesto);
        firma.GetProperty("kid").GetString().ShouldBe(lista.Kid);
        var claves = (await (await Host.Scenario(s =>
        {
            s.Get.Url("/.well-known/validasoft-certificados.json");
            s.StatusCodeShouldBe(200);
        })).ReadAsJsonAsync<ClavesDeCertificadosDto>())!;
        var publica = Convert.FromBase64String(claves.Claves.Single(c => c.Kid == lista.Kid).ClavePublica);
        SelloEd25519.Verificar(publica, manifiestoBytes, Convert.FromBase64String(firma.GetProperty("firma").GetString()!)).ShouldBeTrue();
        SelloEd25519.Verificar(publica, Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(manifiestoBytes) + " "), Convert.FromBase64String(firma.GetProperty("firma").GetString()!)).ShouldBeFalse();
        Encoding.UTF8.GetString(entradas["LEEME.txt"]).ShouldContain(lista.HashManifiesto!);
    }

    [Fact]
    public async Task La_exportacion_vence_a_los_7_dias_se_elimina_del_almacenamiento_y_no_se_descarga_mas()
    {
        var (_, vinculo, sesion, _) = await TerceroConAprobacionAsync();
        var pedida = await TerceroAsync<ExportacionBovedaDto>("POST", "/public/v1/boveda/exportar", sesion.AccessToken, null, 202);
        var lista = await EsperarExportacionAsync(sesion, pedida.Id, "Lista");

        var almacen = Host.Services.GetRequiredService<IAlmacenDeArchivos>();
        var clave = ClavesDeAlmacen.ExportacionDeBoveda(vinculo.TerceroId, pedida.Id);
        (await almacen.ExisteAsync(ClavesDeAlmacen.BucketBoveda, clave, default)).ShouldBeTrue();

        fixture.Reloj.Desfase = TimeSpan.FromDays(8);
        try
        {
            // Pasado su plazo ya no se descarga (410) aunque el ZIP siga en el almacenamiento; la limpieza lo elimina.
            (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/exportaciones/{pedida.Id}/descarga", sesion.AccessToken, null, 410))).ShouldBe("exportacion-vencida");
            await Host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).SendMessageAndWaitAsync(new LimpiarExportacionesDeBoveda());
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        (await TerceroAsync<ExportacionBovedaDto>("GET", $"/public/v1/exportaciones/{pedida.Id}", sesion.AccessToken, null, 200)).Estado.ShouldBe("Vencida");
        (await almacen.ExisteAsync(ClavesDeAlmacen.BucketBoveda, clave, default)).ShouldBeFalse();
        lista.Estado.ShouldBe("Lista");
    }

    private static byte[] Leer(ZipArchiveEntry entrada)
    {
        using var flujo = entrada.Open();
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }
}
