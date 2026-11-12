using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;

namespace Validasoft.Adaptadores.Portal.Tests;

/// <summary>
/// La imagen de la firma de la declaración PEP al importar del Portal (DP-19): se lee por un origen de archivos (simulado aquí), solo se importa si su
/// SHA-256 es el que el Portal guardó y, si falta, no coincide o no sirve, la declaración se importa igual sin imagen y con una advertencia (no se
/// rechaza). Además, los tres orígenes de archivos (carpeta, URL y simulado) y lo que cambió la HU: PEP relacionadas y firma por apoderado.
/// </summary>
public class FirmaDeDeclaracionPepDelPortalTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 12);
    private const string RutaDelPortal = @"C:\inetpub\Portal\DeclaracionesPEP\202\1\firma_20261005103000_ab12.png";

    /// <summary>Un PNG RGB de 8 bits de 60 × 20 (lo que genera el navegador, en pequeño); la semilla cambia el dibujo y, con él, la huella.</summary>
    private static byte[] Png(int semilla = 0)
    {
        var crudo = new List<byte>();
        for (var y = 0; y < 20; y++)
        {
            crudo.Add(0);
            for (var x = 0; x < 60; x++)
                crudo.AddRange(Math.Abs(y - (10 + semilla % 5)) < 2 && x is > 4 and < 55 ? [20, 20, 90] : [255, 255, 255]);
        }

        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write([.. crudo]);
        var png = new List<byte> { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        void Trozo(string tipo, byte[] datos)
        {
            png.AddRange([(byte)(datos.Length >> 24), (byte)(datos.Length >> 16), (byte)(datos.Length >> 8), (byte)datos.Length]);
            png.AddRange(Encoding.ASCII.GetBytes(tipo));
            png.AddRange(datos);
            png.AddRange(new byte[4]);
        }

        Trozo("IHDR", [0, 0, 0, 60, 0, 0, 0, 20, 8, 2, 0, 0, 0]);
        Trozo("IDAT", comprimido.ToArray());
        Trozo("IEND", []);
        return [.. png];
    }

    private static string Huella(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static TerDeclaracionPep Declaracion(byte[]? imagen, string? hash, string? ruta = RutaDelPortal) => new(
        TerCamId: 1, RelacionEmpresa: 1, Nacionalidad: "Colombia", Direccion: "Cra 7 # 1-2", CargoDesempenado: "Alcalde municipal", Institucion: "Alcaldía de Sopó",
        PaisInstitucion: "Colombia", FechaVinculacion: new DateTime(2018, 1, 1), FechaDesvinculacion: new DateTime(2022, 1, 1), AceptaJuramento: true, FirmaEnCalidad: 1,
        FirmanteNombre: "María López Díaz", FirmanteTipoDocumento: "CC", FirmanteNumeroDocumento: "52987654", FechaFirma: new DateTime(2026, 10, 5), FirmaHash: hash,
        FirmaRuta: ruta, FirmaImagen: imagen);

    private static TerceroDelPortal Natural(params TerDeclaracionPep[] declaraciones) => new(new TercerosPortal(202, 3),
        new TerInformacionGeneral(2, "María", "López Díaz", "N", "CC", "52.987.654", null, "maria@correo.co", 2, "Cra 7 # 1-2", 11, "3001234567", null, false, null, "Colombia"),
        InfoTributaria: new TerInfoTributaria(),
        CamaraComercio: [new TerCamaraComercio(1, 1, "CC", "52987654", "María", "López", TerCamEsPep: true, TerCamVinculoPep: false, TerFechaExpedicion: "20/05/2001", TerLugarExpedicion: "Medellín")],
        Siso: new TerSISO(),
        DeclaracionesPep: declaraciones);

    private static ResultadoDeTraduccion Traducir(TerceroDelPortal t) =>
        TraductorDelPortal.Traducir(t, TraductorDelPortalTests.Catalogos, TraductorDelPortalTests.Plataforma(), Hoy);

    private static DeclaracionPep Declarada(ResultadoDeTraduccion r) =>
        ((DatosGenerales)r.Secciones.Single(s => s.Definicion.Codigo == CodigosDeSeccion.General).Datos).DeclaracionPep.ShouldNotBeNull();

    private static ResultadoDeRegla Regla(ResultadoDeTraduccion r)
    {
        var s = r.Secciones.Single(x => x.Definicion.Codigo == CodigosDeSeccion.General);
        return ReglasDeFicha.Evaluar(s.Definicion, s.Datos, new ContextoDeSeccion(r.Tercero!.TipoPersona, r.Tercero.Identificacion, null, Hoy), TraductorDelPortalTests.Plataforma(), false, 0);
    }

    // ---------- La firma ----------

    [Fact]
    public void Con_la_imagen_leida_y_la_huella_que_coincide_la_declaracion_se_importa_firmada()
    {
        var imagen = Png();
        var r = Traducir(Natural(Declaracion(imagen, Huella(imagen).ToUpperInvariant())));
        var d = Declarada(r);

        d.FirmaPng.ShouldBe(Convert.ToBase64String(imagen));
        d.FirmaHuella.ShouldBe(Huella(imagen)); // la de la imagen, igual a la del Portal
        d.FechaFirma.ShouldBe(new DateOnly(2026, 10, 5));
        r.Advertencias.ShouldNotContain(a => a.Codigo.StartsWith("firma-", StringComparison.Ordinal));
        // La declaración completa, con la imagen, pasa la regla de la sección (la validación del PNG es la de siempre).
        var regla = Regla(r);
        regla.Errores.ShouldBeEmpty();
        regla.Completitud.Faltantes.ShouldBeEmpty();
    }

    [Fact]
    public void Si_la_huella_no_coincide_con_la_imagen_se_importa_sin_imagen_con_el_aviso_y_no_se_rechaza()
    {
        var r = Traducir(Natural(Declaracion(Png(1), Huella(Png(2)))));
        var d = Declarada(r);

        d.FirmaPng.ShouldBeNull();
        d.FirmaHuella.ShouldBe(Huella(Png(2))); // queda la del Portal: allá estaba firmada con esa imagen
        r.Rechazos.ShouldBeEmpty();
        var aviso = r.Advertencias.Single(a => a.Codigo == "firma-huella-no-coincide");
        (aviso.Seccion, aviso.Campo, aviso.Dato).ShouldBe((CodigosDeSeccion.General, "declaracion_pep.firma_png", RutaDelPortal));
        Regla(r).Completitud.Faltantes.ShouldBeEmpty(); // importada sin imagen no deja la sección incompleta (vale la huella del origen)
    }

    [Fact]
    public void Si_no_hay_imagen_se_importa_con_la_huella_del_portal_y_el_aviso()
    {
        var r = Traducir(Natural(Declaracion(null, "ABCD".PadRight(64, 'e'))));
        var d = Declarada(r);

        (d.FirmaPng, d.FirmaHuella).ShouldBe((null, "abcd".PadRight(64, 'e')));
        r.Advertencias.Single(a => a.Codigo == "firma-sin-imagen").Dato.ShouldBe(RutaDelPortal);
        r.Rechazos.ShouldBeEmpty();
    }

    [Fact]
    public void Una_imagen_que_coincide_pero_Validasoft_no_puede_dibujar_se_importa_sin_ella()
    {
        var noEsPng = Encoding.UTF8.GetBytes("esto no es una imagen, pero su huella sí coincide");
        var r = Traducir(Natural(Declaracion(noEsPng, Huella(noEsPng))));

        Declarada(r).FirmaPng.ShouldBeNull();
        r.Advertencias.ShouldContain(a => a.Codigo == "firma-imagen-invalida");
        r.Rechazos.ShouldBeEmpty();
    }

    [Fact]
    public void Sin_huella_en_el_portal_la_imagen_no_se_puede_verificar_y_no_se_importa()
    {
        var imagen = Png();
        var r = Traducir(Natural(Declaracion(imagen, null)));

        (Declarada(r).FirmaPng, Declarada(r).FirmaHuella).ShouldBe((null, null));
        r.Advertencias.ShouldContain(a => a.Codigo == "firma-huella-no-coincide");
    }

    // ---------- Lo que cambió la HU en el Portal ----------

    [Fact]
    public void La_firma_por_apoderado_se_importa_como_representante_legal_con_su_firmante_y_se_avisa()
    {
        var imagen = Png();
        var socio = Declaracion(imagen, Huella(imagen)) with { TerCamId = 2, RelacionEmpresa = 2, FirmaEnCalidad = 3, FirmanteNombre = "Pedro Apoderado Gil", FirmanteNumeroDocumento = "80123456" };
        var tercero = new TerceroDelPortal(new TercerosPortal(101, 3),
            new TerInformacionGeneral(1, "Aceros del Norte S.A.S.", null, "J", "NIT", "900123456", 0, "COMPRAS@ACEROS.CO", 1, "Calle 45 # 12-08", 10, "6015551234", "aceros.co", true, null, "Colombia", 2005),
            InfoTributaria: new TerInfoTributaria(),
            CamaraComercio:
            [
                new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", "Gerente", null, false, false, 1, TerNacionalidad: "Colombia"),
                new TerCamaraComercio(2, 2, "CC", "80111222", "Luis", "Ruiz", null, "100", true, false)
            ],
            Siso: new TerSISO(),
            DeclaracionesPep: [socio]);
        var r = Traducir(tercero);

        var persona = ((DatosLegales)r.Secciones.Single(s => s.Definicion.Codigo == CodigosDeSeccion.Legal).Datos).Personas![1].DeclaracionPep.ShouldNotBeNull();
        (persona.FirmaEnCalidad, persona.FirmanteNombre, persona.FirmanteNumero).ShouldBe((FirmaEnCalidad.RepresentanteLegal, "Pedro Apoderado Gil", "80123456"));
        r.Advertencias.ShouldContain(a => a.Codigo == "firma-por-apoderado" && a.Campo == "personas[1].declaracion_pep.firma_en_calidad");
    }

    [Fact]
    public void Las_pep_relacionadas_de_la_tabla_nueva_se_importan_la_primera_y_se_avisa_de_las_demas()
    {
        var imagen = Png();
        var declaracion = Declaracion(imagen, Huella(imagen)) with
        {
            TieneVinculoPep = true,
            Vinculos =
            [
                new TerDeclaracionPepVinculo(1, "Hermano", "Carlos Ruiz", "Senador", "Senado de la República", "Colombia", new DateTime(2022, 8, 1), null, true),
                new TerDeclaracionPepVinculo(2, "Socio", "Ana Mora", "Gobernadora", "Gobernación", "Colombia", new DateTime(2019, 1, 1), new DateTime(2021, 1, 1), false)
            ]
        };
        var r = Traducir(Natural(declaracion));

        Declarada(r).VinculoConPep.ShouldBe(new VinculoConPep
        {
            Tiene = true, Nombre = "Carlos Ruiz", Parentesco = "Hermano", CargoInstitucion = "Senador, Senado de la República", FechaVinculacion = new DateOnly(2022, 8, 1), Vigente = true
        });
        r.Advertencias.Single(a => a.Codigo == "varias-pep-relacionadas").Mensaje.ShouldContain("2 PEP relacionadas");
        Regla(r).Completitud.Faltantes.ShouldBeEmpty();

        // Sin filas en la tabla nueva vale el vínculo único de la fase 1.
        var antigua = Traducir(Natural(declaracion with { Vinculos = null, VinculoPepNombre = "Marta Gil", VinculoPepParentesco = "Prima", VinculoPepCargoInstitucion = "Edil, Concejo", VinculoPepVigente = true }));
        Declarada(antigua).VinculoConPep!.Nombre.ShouldBe("Marta Gil");
        antigua.Advertencias.ShouldNotContain(a => a.Codigo == "varias-pep-relacionadas");
    }

    // ---------- El adaptador lee la imagen por el origen de archivos ----------

    private static string JsonDelPortal(params TerDeclaracionPep[] declaraciones) =>
        JsonSerializer.Serialize(new ArchivoDelPortal(TraductorDelPortalTests.Catalogos, [Natural(declaraciones)]), OrigenPortalArchivo.Opciones);

    private static IConfiguration Configuracion(Dictionary<string, string?>? valores = null) => new ConfigurationBuilder().AddInMemoryCollection(valores ?? []).Build();

    [Fact]
    public async Task El_adaptador_lee_la_firma_del_origen_de_archivos_y_solo_la_de_quien_responde_si()
    {
        var imagen = Png(3);
        var archivos = new ArchivosDelPortalSimulado().Con(RutaDelPortal, imagen);
        var otra = Declaracion(null, Huella(Png(4)), @"C:\inetpub\Portal\DeclaracionesPEP\202\9\otra.png") with { TerCamId = 9 }; // un contacto que no es PEP
        var origen = new AdaptadorPortal(Configuracion(), archivos).DesdeArchivo(JsonDelPortal(Declaracion(null, Huella(imagen)), otra));

        var tercero = (await origen.TerceroAsync("202", CancellationToken.None)).ShouldNotBeNull();
        var r = tercero.Traducir(TraductorDelPortalTests.Plataforma(), Hoy, null);

        Declarada(r).FirmaPng.ShouldBe(Convert.ToBase64String(imagen));
        r.Advertencias.ShouldNotContain(a => a.Codigo.StartsWith("firma-", StringComparison.Ordinal));
        archivos.Lecturas.ShouldBe(1);
    }

    [Fact]
    public async Task Sin_origen_de_archivos_o_con_un_archivo_distinto_la_declaracion_se_importa_sin_imagen_y_con_aviso()
    {
        var imagen = Png();
        var json = JsonDelPortal(Declaracion(null, Huella(imagen)));

        foreach (var archivos in new IOrigenDeArchivosDelPortal?[] { null, new ArchivosDelPortalNulo(), new ArchivosDelPortalSimulado(), new ArchivosDelPortalSimulado().Con(RutaDelPortal, Png(2)) })
        {
            var tercero = (await new AdaptadorPortal(Configuracion(), archivos).DesdeArchivo(json).TerceroAsync("202", CancellationToken.None)).ShouldNotBeNull();
            var r = tercero.Traducir(TraductorDelPortalTests.Plataforma(), Hoy, null);
            Declarada(r).FirmaPng.ShouldBeNull();
            r.Advertencias.ShouldContain(a => a.Codigo == "firma-sin-imagen" || a.Codigo == "firma-huella-no-coincide");
            r.Rechazos.ShouldBeEmpty();
        }
    }

    // ---------- Los orígenes de archivos ----------

    [Fact]
    public async Task El_origen_en_disco_lee_lo_que_sigue_a_DeclaracionesPEP_y_no_sale_de_su_carpeta()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "validasoft-firmas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(carpeta, "202", "1"));
        try
        {
            var imagen = Png();
            await File.WriteAllBytesAsync(Path.Combine(carpeta, "202", "1", "firma.png"), imagen);
            await File.WriteAllBytesAsync(Path.Combine(carpeta, "202", "1", "enorme.png"), new byte[ArchivosDelPortalEnDisco.MaximoBytes + 1]);
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(carpeta)!, "secreto-" + Path.GetFileName(carpeta) + ".txt"), "no se lee");
            var disco = new ArchivosDelPortalEnDisco(carpeta);

            // Con la ruta absoluta del servidor del Portal (de Windows o de Linux), en lo que sigue a DeclaracionesPEP.
            (await disco.LeerAsync(@"C:\inetpub\Portal\DeclaracionesPEP\202\1\firma.png", CancellationToken.None)).ShouldBe(imagen);
            (await disco.LeerAsync("/var/www/portal/declaracionespep/202/1/firma.png", CancellationToken.None)).ShouldBe(imagen);
            (await disco.LeerAsync("DeclaracionesPEP/202/1/firma.png", CancellationToken.None)).ShouldBe(imagen);

            foreach (var ruta in new[]
                     {
                         @"C:\inetpub\Portal\DeclaracionesPEP\202\1\no-existe.png", @"C:\inetpub\Portal\DeclaracionesPEP\202\1\enorme.png", @"C:\inetpub\Portal\otra\202\1\firma.png",
                         @"C:\inetpub\Portal\DeclaracionesPEP\..\..\secreto.txt", "DeclaracionesPEP/../secreto-" + Path.GetFileName(carpeta) + ".txt", "DeclaracionesPEP", "", "   "
                     })
                (await disco.LeerAsync(ruta, CancellationToken.None)).ShouldBeNull();
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
            foreach (var suelto in Directory.GetFiles(Path.GetDirectoryName(carpeta)!, "secreto-" + Path.GetFileName(carpeta) + ".txt"))
                File.Delete(suelto);
        }
    }

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Peticiones { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    [Fact]
    public async Task El_origen_por_http_pide_la_ruta_relativa_con_el_token_y_descarta_lo_que_falla_o_pesa_de_mas()
    {
        var imagen = Png();
        var manejador = new ManejadorFalso(p => p.RequestUri!.AbsolutePath switch
        {
            "/firmas/202/1/firma.png" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(imagen) },
            "/firmas/202/1/enorme.png" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[ArchivosDelPortalEnDisco.MaximoBytes + 1]) },
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
        var http = new ArchivosDelPortalPorHttp(new HttpClient(manejador), new Uri("https://portal.interno.co/firmas/"), "token-de-prueba");

        (await http.LeerAsync(@"C:\inetpub\Portal\DeclaracionesPEP\202\1\firma.png", CancellationToken.None)).ShouldBe(imagen);
        manejador.Peticiones.Single().Headers.Authorization!.ToString().ShouldBe("Bearer token-de-prueba");
        (await http.LeerAsync(@"DeclaracionesPEP\202\1\enorme.png", CancellationToken.None)).ShouldBeNull();
        (await http.LeerAsync(@"DeclaracionesPEP\202\1\no-esta.png", CancellationToken.None)).ShouldBeNull();
        (await http.LeerAsync(@"DeclaracionesPEP\..\..\etc\passwd", CancellationToken.None)).ShouldBeNull();
        manejador.Peticiones.Count.ShouldBe(3); // la ruta que se sale de la carpeta ni siquiera se pide
    }

    [Fact]
    public void La_configuracion_elige_carpeta_url_o_ninguno()
    {
        ArchivosDelPortal.Configurado(Configuracion()).ShouldBeOfType<ArchivosDelPortalNulo>();
        ArchivosDelPortal.Configurado(Configuracion(new() { [$"{ArchivosDelPortal.Seccion}:RutaBase"] = Path.GetTempPath() })).ShouldBeOfType<ArchivosDelPortalEnDisco>();
        ArchivosDelPortal.Configurado(Configuracion(new() { [$"{ArchivosDelPortal.Seccion}:UrlBase"] = "https://portal.interno.co/firmas" })).ShouldBeOfType<ArchivosDelPortalPorHttp>();
        // Solo http o https, y una URL que no lo es no activa nada.
        ArchivosDelPortal.Configurado(Configuracion(new() { [$"{ArchivosDelPortal.Seccion}:UrlBase"] = "file:///c:/firmas" })).ShouldBeOfType<ArchivosDelPortalNulo>();
        ArchivosDelPortal.Configurado(Configuracion(new() { [$"{ArchivosDelPortal.Seccion}:UrlBase"] = "no es una url" })).ShouldBeOfType<ArchivosDelPortalNulo>();
    }
}
