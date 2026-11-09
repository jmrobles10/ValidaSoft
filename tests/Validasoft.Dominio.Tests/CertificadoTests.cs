using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Tests;

/// <summary>JCS (RFC 8785) con los vectores del propio RFC: orden de claves, escape de cadenas, números y espacios.</summary>
public class JcsTests
{
    private const string Q = "\"";

    /// <summary>La secuencia de escape JSON <c>\uXXXX</c> (se arma en tiempo de ejecución para no depender de la codificación del fuente).</summary>
    private static string U(string hex) => "\\" + "u" + hex;

    private static string C(int punto) => char.ConvertFromUtf32(punto);

    [Fact]
    public void Canonicaliza_el_ejemplo_del_RFC_8785_seccion_3_2_2()
    {
        var cadena = Q + U("20ac") + "$" + U("000F") + U("000a") + "A'" + U("0042") + U("0022") + U("005c") + "\\\\" + "\\\"" + "\\/" + Q;
        var entrada = "{\n  \"numbers\": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001],\n  \"string\": " + cadena +
                      ",\n  \"literals\": [null, true, false]\n}";

        var esperado = "{\"literals\":[null,true,false],\"numbers\":[333333333.3333333,1e+30,4.5,0.002,1e-27],\"string\":" +
                       Q + C(0x20AC) + "$" + U("000f") + "\\n" + "A'B" + "\\\"" + "\\\\" + "\\\\" + "\\\"" + "/" + Q + "}";

        Jcs.Canonicalizar(entrada).ShouldBe(esperado);
    }

    [Fact]
    public void Ordena_las_claves_por_unidades_UTF16_como_el_RFC_8785_seccion_3_2_3()
    {
        var entrada = "{" + string.Join(",",
            Q + U("20ac") + Q + ":\"Euro Sign\"",
            Q + "\\r" + Q + ":\"Carriage Return\"",
            Q + U("fb33") + Q + ":\"Hebrew Letter Dalet With Dagesh\"",
            "\"1\":\"One\"",
            Q + U("d83d") + U("de00") + Q + ":\"Emoji: Grinning Face\"",
            Q + U("0080") + Q + ":\"Control\"",
            Q + U("00f6") + Q + ":\"Latin Small Letter O With Diaeresis\"") + "}";

        var canonico = Jcs.Canonicalizar(entrada);

        using var documento = JsonDocument.Parse(canonico);
        documento.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["\r", "1", C(0x80), C(0xF6), C(0x20AC), C(0x1F600), C(0xFB33)]);
        // El retorno de carro va escapado corto y U+0080 (no es control para JSON) va literal en UTF-8.
        canonico.ShouldStartWith("{\"\\r\":\"Carriage Return\",\"1\":\"One\",\"" + C(0x80) + "\":\"Control\"");
    }

    [Theory]
    [InlineData(0x0000000000000000L, "0")]
    [InlineData(unchecked((long)0x8000000000000000UL), "0")]
    [InlineData(0x0000000000000001L, "5e-324")]
    [InlineData(0x7fefffffffffffffL, "1.7976931348623157e+308")]
    [InlineData(0x4340000000000000L, "9007199254740992")]
    [InlineData(0x4430000000000000L, "295147905179352830000")]
    [InlineData(0x44b52d02c7e14af5L, "9.999999999999997e+22")]
    [InlineData(0x44b52d02c7e14af6L, "1e+23")]
    [InlineData(0x44b52d02c7e14af7L, "1.0000000000000001e+23")]
    [InlineData(0x444b1ae4d6e2ef50L, "1e+21")]
    [InlineData(0x3eb0c6f7a0b5ed8dL, "0.000001")]
    public void Serializa_los_numeros_de_los_vectores_del_RFC_8785_apendice_B(long bits, string esperado) =>
        Jcs.Numero(BitConverter.Int64BitsToDouble(bits)).ShouldBe(esperado);

    [Theory]
    [InlineData("1e20", "100000000000000000000")]
    [InlineData("1e21", "1e+21")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("0.0000012", "0.0000012")]
    [InlineData("-1.50", "-1.5")]
    [InlineData("123", "123")]
    [InlineData("-0.0", "0")]
    [InlineData("9007199254740993", "9007199254740992")]
    [InlineData("1.5E+2", "150")]
    public void Normaliza_numeros_como_ECMAScript(string json, string esperado) =>
        Jcs.Canonicalizar(json).ShouldBe(esperado);

    [Fact]
    public void Quita_espacios_y_ordena_tambien_los_objetos_anidados() =>
        Jcs.Canonicalizar("{ \"b\" : [ 1 , { \"z\" : true , \"a\" : null } ] , \"a\" : \"x y\" }")
            .ShouldBe("{\"a\":\"x y\",\"b\":[1,{\"a\":null,\"z\":true}]}");

    [Fact]
    public void Escribe_unicode_literal_y_escapa_solo_los_controles()
    {
        var entrada = Q + "Ñandú " + U("00e9") + U("0007") + U("001f") + U("007f") + Q;
        Jcs.Canonicalizar(entrada).ShouldBe(Q + "Ñandú " + C(0xE9) + U("0007") + U("001f") + C(0x7F) + Q);
        Encoding.UTF8.GetBytes(Jcs.Canonicalizar(Q + U("00f1") + Q)).ShouldBe([0x22, 0xC3, 0xB1, 0x22]);
    }

    [Fact]
    public void Rechaza_claves_repetidas_y_numeros_fuera_de_rango()
    {
        Should.Throw<FormatException>(() => Jcs.Canonicalizar("{\"a\":1,\"a\":2}"));
        Should.Throw<FormatException>(() => Jcs.Canonicalizar("[1e400]"));
        Should.Throw<FormatException>(() => Jcs.Numero(double.NaN));
        Should.Throw<FormatException>(() => Jcs.Numero(double.PositiveInfinity));
    }
}

/// <summary>Certificado: contenido canónico, hash, sello Ed25519, folios, enmascarado y transiciones de estado.</summary>
public class CertificadoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 8, 14, 3, 11, 450, TimeSpan.FromHours(-5));

    private static readonly OrigenDeEmision Origen = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "NORTE-E-2026-000007");

    private static ClavesDeCertificados Claves(byte[]? semilla = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validasoft:Certificados:ClavePrivada"] = Convert.ToBase64String(semilla ?? SelloEd25519.GenerarSemilla())
        }).Build());

    private static ContenidoDeCertificado Plantilla(params RequisitoCertificado[] requisitos) => new(
        ContenidoDeCertificado.EsquemaV1, string.Empty, 0,
        new EmpresaCertificadora(Guid.Parse("01890000-0000-7000-8000-000000000001"), "Norte S.A.S.",
            new IdentificacionDeEmpresa("CO", "NIT", "800197268", "4")),
        new TitularCertificado("Aceros del Norte S.A.S.", "NIT 90*****56", "CO", "Juridica"),
        new TipoVinculoCertificado("PROVEEDOR", "Proveedor"),
        new FlujoCertificado("HAB_PROV", "Habilitación de proveedores", 3),
        requisitos.Length > 0
            ? requisitos
            : [new RequisitoCertificado("RUT", "RUT", 2, new string('a', 64), new DateOnly(2027, 4, 30), false, null)],
        [],
        [new AreaCertificado("Oficial de cumplimiento", "OFICIAL_CUMPLIMIENTO", "Humano", Ahora.AddHours(-1))],
        DateTimeOffset.MinValue,
        null);

    private static (Certificado Certificado, CertificadoEmitido Emitido) Emitido(ClavesDeCertificados? claves = null,
        ContenidoDeCertificado? plantilla = null)
    {
        var emitido = Certificado.Emitir(Origen, "NORTE-C-2026-000001", plantilla ?? Plantilla(), claves ?? Claves(), "https://valida.norte.co/", Ahora);
        return (Certificado.Create(emitido), emitido);
    }

    [Fact]
    public void Una_revalidacion_deja_el_certificado_con_pendientes_sin_dejar_de_ser_valido_y_la_reemision_lo_vuelve_vigente()
    {
        var (certificado, _) = Emitido();
        var expediente = Guid.NewGuid();
        var pendientes = certificado.MarcarConPendientes("Revalidación de RUT", expediente, Ahora.AddDays(1)).ShouldNotBeNull();
        certificado = Aplicar(certificado, pendientes);
        certificado.Estado.ShouldBe(EstadoCertificado.ConPendientes);

        // Ya con pendientes (o en otro estado) no vuelve a marcarse; sigue siendo válido: se puede suspender.
        certificado.MarcarConPendientes("Revalidación de RUT", expediente, Ahora.AddDays(2)).ShouldBeNull();
        certificado.Suspender("Bloqueo", Ahora.AddDays(2)).EstadoPrevio.ShouldBe(EstadoCertificado.ConPendientes);

        var otro = Guid.NewGuid();
        certificado = Aplicar(certificado, [.. certificado.Reemitir(Origen with { ExpedienteId = otro }, Plantilla(), Claves(), "https://valida.norte.co/",
            "Expediente aprobado", Ahora.AddDays(3))]);
        certificado.Estado.ShouldBe(EstadoCertificado.Vigente);
        certificado.EmisionActual.ShouldBe(2);
    }

    private static Certificado Aplicar(Certificado certificado, params object[] eventos)
    {
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case CertificadoEmitido e: certificado.Apply(e); break;
                case CertificadoReemplazado e: certificado.Apply(e); break;
                case CertificadoSuspendido e: certificado.Apply(e); break;
                case CertificadoConPendientes e: certificado.Apply(e); break;
                case CertificadoRestablecido e: certificado.Apply(e); break;
                case CertificadoRevocado e: certificado.Apply(e); break;
                case CertificadoVencido e: certificado.Apply(e); break;
                case PdfDeCertificadoGenerado e: certificado.Apply(e); break;
                case CertificadoDescargado e: certificado.Apply(e); break;
                default: throw new InvalidOperationException(evento.GetType().Name);
            }
        }

        return certificado;
    }

    // ---------- contenido, hash y sello ----------

    [Fact]
    public void El_contenido_canonico_omite_nulos_ordena_requisitos_y_escribe_fechas_en_UTC()
    {
        var (_, emitido) = Emitido(plantilla: Plantilla(
            new RequisitoCertificado("RUT", "RUT", 1, new string('b', 64), null, false, null),
            new RequisitoCertificado("CAMARA_COMERCIO", "Cámara de comercio", 4, new string('c', 64), new DateOnly(2027, 1, 31), false, null),
            new RequisitoCertificado("POLIZA", "Póliza", null, null, null, true, "Contrato menor a 10 SMMLV")));

        var contenido = emitido.ContenidoCanonico;
        contenido.ShouldBe(Jcs.Canonicalizar(contenido)); // ya está en forma canónica
        contenido.ShouldNotContain("null");
        contenido.ShouldContain("\"emitidoEn\":\"2026-11-08T19:03:11Z\"");
        contenido.ShouldContain("\"esquema\":\"validasoft.certificado.v1\"");
        contenido.ShouldContain("\"folio\":\"NORTE-C-2026-000001-1\"");
        contenido.ShouldContain("\"vigenteHasta\":\"2027-01-31\"");

        using var documento = JsonDocument.Parse(contenido);
        documento.RootElement.GetProperty("requisitos").EnumerateArray().Select(r => r.GetProperty("codigo").GetString())
            .ShouldBe(["CAMARA_COMERCIO", "POLIZA", "RUT"]);
        var eximido = documento.RootElement.GetProperty("requisitos")[1];
        eximido.GetProperty("eximido").GetBoolean().ShouldBeTrue();
        eximido.GetProperty("justificacion").GetString().ShouldBe("Contrato menor a 10 SMMLV");
        eximido.TryGetProperty("sha256", out _).ShouldBeFalse();
        emitido.VigenteHasta.ShouldBe(new DateOnly(2027, 1, 31)); // la menor de las vigencias, sin contar el eximido
    }

    [Fact]
    public void El_hash_es_el_sha256_del_contenido_y_el_sello_verifica_con_la_clave_publica()
    {
        var semilla = SelloEd25519.GenerarSemilla();
        var claves = Claves(semilla);
        var (certificado, emitido) = Emitido(claves);

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(emitido.ContenidoCanonico)));
        emitido.HashContenido.ShouldBe(hash);
        certificado.Actual.Huella.ShouldBe($"{hash[..4]}·{hash[4..8]}·{hash[8..12]}");
        emitido.Kid.ShouldBe(claves.Kid);
        emitido.UrlVerificacion.ShouldBe($"https://valida.norte.co/verificar/NORTE-C-2026-000001-1?huella={hash[..12]}");

        var publica = claves.ClavePublica(emitido.Kid).ShouldNotBeNull();
        publica.ShouldBe(SelloEd25519.ClavePublica(semilla));
        SelloEd25519.Verificar(publica, Convert.FromHexString(hash), Convert.FromBase64String(emitido.Sello)).ShouldBeTrue();

        // Cambiar una sola letra del contenido cambia el hash y el sello deja de verificar.
        var alterado = emitido.ContenidoCanonico.Replace("Aceros", "Acerps", StringComparison.Ordinal);
        var hashAlterado = SHA256.HashData(Encoding.UTF8.GetBytes(alterado));
        SelloEd25519.Verificar(publica, hashAlterado, Convert.FromBase64String(emitido.Sello)).ShouldBeFalse();
        // Otra clave no verifica el sello.
        SelloEd25519.Verificar(SelloEd25519.ClavePublica(SelloEd25519.GenerarSemilla()), Convert.FromHexString(hash),
            Convert.FromBase64String(emitido.Sello)).ShouldBeFalse();
    }

    [Fact]
    public void El_sello_es_ed25519_estandar_vector_1_del_RFC_8032()
    {
        var semilla = Convert.FromHexString("9d61b19deffd5a60ba844af492ec2cc44449c5697b326919703bac031cae7f60");
        SelloEd25519.ClavePublica(semilla).ShouldBe(Convert.FromHexString("d75a980182b10ab7d54bfed3c964073a0ee172f3daa62325af021a68f707511a"));
        SelloEd25519.Firmar(semilla, []).ShouldBe(Convert.FromHexString(
            "e5564300c360ac729086e2cc806e828a84877f1eb8e5d974d873e065224901555fb8821590a33bacc61e39701cf9b46bd25bf5f0595bbe24655141438e7a100b"));
    }

    [Fact]
    public void Sin_clave_configurada_o_con_una_invalida_no_se_sella()
    {
        var sin = new ClavesDeCertificados(new ConfigurationBuilder().Build());
        Should.Throw<InvalidOperationException>(() => sin.Kid).Message.ShouldContain("ClavePrivada");
        var corta = new ClavesDeCertificados(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Validasoft:Certificados:ClavePrivada"] = Convert.ToBase64String(new byte[16]) }).Build());
        Should.Throw<InvalidOperationException>(() => corta.Kid).Message.ShouldContain("32 bytes");
    }

    [Fact]
    public void Publica_la_clave_vigente_y_las_anteriores_para_verificar_tras_una_rotacion()
    {
        var anterior = SelloEd25519.ClavePublica(SelloEd25519.GenerarSemilla());
        var claves = new ClavesDeCertificados(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Validasoft:Certificados:ClavePrivada"] = Convert.ToBase64String(SelloEd25519.GenerarSemilla()),
            ["Validasoft:Certificados:Kid"] = "vs-2027",
            ["Validasoft:Certificados:ClavesAnteriores:0:Kid"] = "vs-2026",
            ["Validasoft:Certificados:ClavesAnteriores:0:ClavePublica"] = Convert.ToBase64String(anterior)
        }).Build());

        claves.Kid.ShouldBe("vs-2027");
        claves.Publicadas().Select(c => (c.Kid, c.Vigente)).ShouldBe([("vs-2027", true), ("vs-2026", false)]);
        claves.ClavePublica("vs-2026").ShouldBe(anterior);
    }

    [Fact]
    public void No_emite_un_certificado_parcial() =>
        Should.Throw<ReglaDeNegocioException>(() => Emitido(plantilla: Plantilla(
                new RequisitoCertificado("RUT", "RUT", null, null, null, false, null))))
            .Codigo.ShouldBe("certificado-parcial");

    // ---------- folios, enmascarado e identidad ----------

    [Fact]
    public void Arma_y_lee_folios_con_sufijo_de_emision()
    {
        FoliosDeCertificado.Raiz("NORTE", 2026, 418).ShouldBe("NORTE-C-2026-000418");
        FoliosDeCertificado.DeEmision("NORTE-C-2026-000418", 2).ShouldBe("NORTE-C-2026-000418-2");
        FoliosDeCertificado.Leer("norte-c-2026-000418-2").ShouldBe(("NORTE-C-2026-000418", 2));
        FoliosDeCertificado.Leer("NORTE-C-2026-000418").ShouldBe(("NORTE-C-2026-000418", (int?)null));
        FoliosDeCertificado.Leer("NORTE-E-2026-000418").ShouldBeNull();
        FoliosDeCertificado.Leer("NORTE-C-2026-418").ShouldBeNull();
        FoliosDeCertificado.Leer("NORTE-C-2026-000418-0").ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => FoliosDeCertificado.Raiz(" ", 2026, 1)).Codigo.ShouldBe("prefijo-folio-invalido");
    }

    [Fact]
    public void Enmascara_la_identificacion_del_titular()
    {
        Enmascarado.Identificacion(Identificacion.Crear("CO", "NIT", "900123456")).ShouldBe("NIT 90*****56");
        Enmascarado.Identificacion(new Identificacion("CO", "CC", "1234567", null)).ShouldBe("CC *****67");
        Enmascarado.Identificacion(new Identificacion("CO", "PA", "A12", null)).ShouldBe("PA **2");
    }

    [Fact]
    public void La_huella_se_normaliza_y_el_id_del_certificado_es_determinista()
    {
        HuellaDeCertificado.Normalizar("5A7E·0c1d-9F22 ").ShouldBe("5a7e0c1d9f22");
        HuellaDeCertificado.Normalizar("5a7e").ShouldBeNull();
        var vinculo = Guid.NewGuid();
        Certificado.IdPara(vinculo).ShouldBe(Certificado.IdPara(vinculo));
        Certificado.IdPara(vinculo).ShouldNotBe(Certificado.IdPara(Guid.NewGuid()));
        Certificado.IdPara(vinculo).Version.ShouldBe(8);
    }

    // ---------- transiciones ----------

    [Fact]
    public void Nace_vigente_con_el_pdf_pendiente_y_registra_el_pdf_una_sola_vez()
    {
        var (certificado, emitido) = Emitido();

        certificado.Id.ShouldBe(Certificado.IdPara(Origen.VinculoId));
        certificado.Estado.ShouldBe(EstadoCertificado.Vigente);
        certificado.Bloqueante.ShouldBeFalse();
        certificado.FolioRaiz.ShouldBe("NORTE-C-2026-000001");
        certificado.Actual.Folio.ShouldBe("NORTE-C-2026-000001-1");
        certificado.PdfPendiente.ShouldBeTrue();
        certificado.VenceDesde.ShouldBe(new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero));
        emitido.Emision.ShouldBe(1);

        var pdf = certificado.RegistrarPdf(1, "tenant-x", "clave", "abc", 1234, Ahora).ShouldNotBeNull();
        Aplicar(certificado, pdf);
        certificado.PdfPendiente.ShouldBeFalse();
        certificado.RegistrarPdf(1, "tenant-x", "otra", "def", 1, Ahora).ShouldBeNull();

        Aplicar(certificado, certificado.RegistrarDescarga(1, "Usuario", "u1", "10.0.0.1", "xunit", false, Ahora));
        certificado.Actual.Descargas.ShouldBe(1);
    }

    [Fact]
    public void No_se_descarga_sin_pdf()
    {
        var (certificado, _) = Emitido();
        Should.Throw<ReglaDeNegocioException>(() => certificado.RegistrarDescarga(1, "Usuario", "u", "ip", "ua", false, Ahora))
            .Codigo.ShouldBe("pdf-pendiente");
    }

    [Fact]
    public void Bloquear_suspende_y_desbloquear_restablece_el_estado_previo()
    {
        var (certificado, _) = Emitido();

        Aplicar(certificado, certificado.Suspender("Vínculo bloqueado: sanción", Ahora));
        certificado.Estado.ShouldBe(EstadoCertificado.Suspendido);
        certificado.Bloqueante.ShouldBeTrue();
        certificado.MotivoSuspension.ShouldBe("Vínculo bloqueado: sanción");
        Should.Throw<ReglaDeNegocioException>(() => certificado.Suspender("otra vez", Ahora)).Codigo.ShouldBe("estado-invalido");
        Should.Throw<ReglaDeNegocioException>(() => certificado.Vencer(Ahora.AddYears(2))).Codigo.ShouldBe("estado-invalido");

        var restablecido = certificado.Restablecer("Vínculo desbloqueado", Ahora.AddDays(3)).ShouldBeOfType<CertificadoRestablecido>();
        restablecido.EstadoRestituido.ShouldBe(EstadoCertificado.Vigente);
        Aplicar(certificado, restablecido);
        certificado.Estado.ShouldBe(EstadoCertificado.Vigente);
        certificado.Bloqueante.ShouldBeFalse();
        certificado.MotivoSuspension.ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => certificado.Restablecer("de nuevo", Ahora)).Codigo.ShouldBe("estado-invalido");
    }

    [Fact]
    public void Si_vencio_durante_el_bloqueo_al_desbloquear_queda_vencido()
    {
        var (certificado, _) = Emitido();
        Aplicar(certificado, certificado.Suspender("sanción", Ahora));

        var resultado = certificado.Restablecer("fin de la sanción", new DateTimeOffset(2027, 5, 2, 0, 0, 0, TimeSpan.Zero));

        Aplicar(certificado, resultado.ShouldBeOfType<CertificadoVencido>());
        certificado.Estado.ShouldBe(EstadoCertificado.Vencido);
        certificado.Bloqueante.ShouldBeTrue();
    }

    [Fact]
    public void Vence_solo_despues_de_su_vigencia()
    {
        var (certificado, _) = Emitido();

        Should.Throw<ReglaDeNegocioException>(() => certificado.Vencer(new DateTimeOffset(2027, 4, 30, 23, 59, 59, TimeSpan.Zero)))
            .Codigo.ShouldBe("certificado-no-vencido");
        Aplicar(certificado, certificado.Vencer(new DateTimeOffset(2027, 5, 1, 0, 0, 0, TimeSpan.Zero)));
        certificado.Estado.ShouldBe(EstadoCertificado.Vencido);
        certificado.Actual.Estado.ShouldBe(EstadoCertificado.Vencido);

        var (sinVigencia, _) = Emitido(plantilla: Plantilla(new RequisitoCertificado("RUT", "RUT", 1, new string('d', 64), null, false, null)));
        sinVigencia.VenceDesde.ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => sinVigencia.Vencer(Ahora.AddYears(50))).Codigo.ShouldBe("certificado-no-vencido");
    }

    [Fact]
    public void Revocar_exige_justificacion_y_es_irreversible()
    {
        var (certificado, _) = Emitido();
        Should.Throw<ReglaDeNegocioException>(() => certificado.Revocar("  ", OrigenRevocacion.Administrador, "admin", Ahora))
            .Codigo.ShouldBe("justificacion-requerida");

        Aplicar(certificado, certificado.Suspender("sanción", Ahora));
        var revocado = certificado.Revocar("Fraude confirmado en la cámara de comercio", OrigenRevocacion.Administrador, "admin-1", Ahora);
        Aplicar(certificado, revocado);

        certificado.Estado.ShouldBe(EstadoCertificado.Revocado);
        certificado.Bloqueante.ShouldBeTrue();
        certificado.Actual.Revocacion!.Origen.ShouldBe(OrigenRevocacion.Administrador);
        certificado.EstadoPrevioASuspension.ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => certificado.Revocar("otra", OrigenRevocacion.Sistema, "s", Ahora)).Codigo.ShouldBe("certificado-revocado");
        Should.Throw<ReglaDeNegocioException>(() => certificado.Restablecer("desbloqueo", Ahora)).Codigo.ShouldBe("estado-invalido");
        Should.Throw<ReglaDeNegocioException>(() => certificado.Suspender("bloqueo", Ahora)).Codigo.ShouldBe("estado-invalido");
    }

    [Fact]
    public void Reemitir_reemplaza_la_emision_anterior_con_el_mismo_folio_raiz()
    {
        var claves = Claves();
        var (certificado, primero) = Emitido(claves);
        var origen = Origen with { ExpedienteId = Guid.NewGuid(), ExpedienteFolio = "NORTE-E-2027-000002" };

        var eventos = certificado.Reemitir(origen, Plantilla(), claves, "https://valida.norte.co", "Renovación aprobada", Ahora.AddMonths(6));
        Aplicar(certificado, [.. eventos]);

        eventos.Select(e => e.GetType()).ShouldBe([typeof(CertificadoReemplazado), typeof(CertificadoEmitido)]);
        var segundo = (CertificadoEmitido)eventos[1];
        segundo.Folio.ShouldBe("NORTE-C-2026-000001-2");
        segundo.HashContenido.ShouldNotBe(primero.HashContenido);
        certificado.EmisionActual.ShouldBe(2);
        certificado.Estado.ShouldBe(EstadoCertificado.Vigente);
        certificado.Buscar(1)!.Estado.ShouldBe(EstadoCertificado.Reemplazado);
        certificado.PdfPendiente.ShouldBeTrue();

        // Tras una revocación, la emisión nueva no «reemplaza» a la revocada: esa sigue revocada.
        Aplicar(certificado, certificado.Revocar("Error de emisión", OrigenRevocacion.Administrador, "admin", Ahora.AddMonths(7)));
        var tercera = certificado.Reemitir(origen with { ExpedienteId = Guid.NewGuid() }, Plantilla(), claves, "https://valida.norte.co",
            "Nueva validación", Ahora.AddMonths(8));
        tercera.ShouldHaveSingleItem().ShouldBeOfType<CertificadoEmitido>().Emision.ShouldBe(3);
        Aplicar(certificado, [.. tercera]);
        certificado.Buscar(2)!.Estado.ShouldBe(EstadoCertificado.Revocado);
        certificado.Estado.ShouldBe(EstadoCertificado.Vigente);
    }

    [Fact]
    public void La_secuencia_de_folios_consume_numeros_consecutivos()
    {
        var secuencia = SecuenciaDeFolios.Create(new SecuenciaDeFoliosIniciada(Guid.NewGuid(), "C", 2026, Ahora));
        var primero = secuencia.Consumir(Ahora);
        secuencia.Apply(primero);
        var segundo = secuencia.Consumir(Ahora);

        primero.Numero.ShouldBe(1);
        segundo.Numero.ShouldBe(2);
        SecuenciaDeFolios.IdPara(Guid.Empty, "C", 2026).ShouldNotBe(SecuenciaDeFolios.IdPara(Guid.Empty, "C", 2027));
        string.Create(CultureInfo.InvariantCulture, $"{segundo.Numero:D6}").ShouldBe("000002");
    }
}
