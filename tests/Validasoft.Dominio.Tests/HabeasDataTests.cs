using System.Security.Cryptography;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Habeas data (Ley 1581 de 2012, OD-1…OD-8): calendario hábil colombiano, plazos de 10 y 15 días hábiles, alertas de plazo, cifrado con
/// la llave del tercero (crypto-shredding), constancia sellada y retención de las versiones referenciadas.
/// </summary>
public class HabeasDataTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Tercero = Guid.NewGuid();

    // ---------- calendario hábil ----------

    [Fact]
    public void Los_festivos_de_Colombia_de_2026_siguen_la_ley_Emiliani_y_la_Semana_Santa()
    {
        var festivos = CalendarioHabil.FestivosDeColombia(2026);
        DateOnly[] esperados =
        [
            new(2026, 1, 1), new(2026, 1, 12), new(2026, 3, 23), new(2026, 4, 2), new(2026, 4, 3), new(2026, 5, 1), new(2026, 5, 18), new(2026, 6, 8),
            new(2026, 6, 15), new(2026, 6, 29), new(2026, 7, 20), new(2026, 8, 7), new(2026, 8, 17), new(2026, 10, 12), new(2026, 11, 2), new(2026, 11, 16),
            new(2026, 12, 8), new(2026, 12, 25)
        ];
        festivos.OrderBy(f => f).ShouldBe(esperados);
        CalendarioHabil.DomingoDePascua(2026).ShouldBe(new DateOnly(2026, 4, 5));
        CalendarioHabil.DomingoDePascua(2027).ShouldBe(new DateOnly(2027, 3, 28));
    }

    [Fact]
    public void Un_dia_es_habil_de_lunes_a_viernes_sin_festivos_ni_los_extras_del_tenant()
    {
        CalendarioHabil.EsHabil(new DateOnly(2026, 11, 11)).ShouldBeTrue(); // miércoles: el festivo de Cartagena pasó al lunes 16
        CalendarioHabil.EsHabil(new DateOnly(2026, 11, 16)).ShouldBeFalse();
        CalendarioHabil.EsHabil(new DateOnly(2026, 11, 14)).ShouldBeFalse(); // sábado
        CalendarioHabil.EsHabil(new DateOnly(2026, 11, 12), extras: new HashSet<DateOnly> { new(2026, 11, 12) }).ShouldBeFalse();
        CalendarioHabil.EsHabil(new DateOnly(2026, 11, 16), pais: "MX").ShouldBeTrue(); // otros países: solo lunes a viernes
    }

    [Fact]
    public void Contar_dias_habiles_se_salta_fines_de_semana_y_festivos()
    {
        // Desde el miércoles 11 de noviembre: jueves 12 (1), viernes 13 (2), lunes 16 festivo, martes 17 (3)…
        CalendarioHabil.SumarDiasHabiles(new DateOnly(2026, 11, 11), 3).ShouldBe(new DateOnly(2026, 11, 17));
        CalendarioHabil.SumarDiasHabiles(new DateOnly(2026, 11, 11), 10).ShouldBe(new DateOnly(2026, 11, 26));
        CalendarioHabil.SumarDiasHabiles(new DateOnly(2026, 11, 11), 15).ShouldBe(new DateOnly(2026, 12, 3));
        CalendarioHabil.SumarDiasHabiles(new DateOnly(2026, 12, 3), 3).ShouldBe(new DateOnly(2026, 12, 9)); // el 8 de diciembre es festivo
        CalendarioHabil.SumarDiasHabiles(new DateOnly(2026, 11, 11), 0).ShouldBe(new DateOnly(2026, 11, 11));

        CalendarioHabil.DiasHabilesEntre(new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 17)).ShouldBe(3);
        CalendarioHabil.DiasHabilesEntre(new DateOnly(2026, 11, 17), new DateOnly(2026, 11, 11)).ShouldBe(-3);
        CalendarioHabil.DiasHabilesEntre(new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 11)).ShouldBe(0);
    }

    // ---------- solicitud ----------

    private static SolicitudHabeas Presentar(TipoDeSolicitudHabeas tipo, AlcanceDeSupresion? alcance = null, Guid? empresa = null)
    {
        var evento = SolicitudHabeas.Recibir(Tercero, tipo, alcance, new DestinoDeSolicitud(empresa is null ? null : Guid.NewGuid(), empresa), "vsdp1.x.y.z", "10.0.0.1", Ahora);
        return SolicitudHabeas.Create(evento);
    }

    [Theory]
    [InlineData(TipoDeSolicitudHabeas.Consulta, 10, "2026-11-26")]
    [InlineData(TipoDeSolicitudHabeas.Correccion, 15, "2026-12-03")]
    [InlineData(TipoDeSolicitudHabeas.Supresion, 15, "2026-12-03")]
    public void La_consulta_vence_a_los_10_dias_habiles_y_los_reclamos_a_los_15(TipoDeSolicitudHabeas tipo, int dias, string vence)
    {
        var solicitud = Presentar(tipo);
        solicitud.DiasHabiles.ShouldBe(dias);
        solicitud.VenceEl.ShouldBe(DateOnly.Parse(vence));
        solicitud.RecibidaEl.ShouldBe(new DateOnly(2026, 11, 11));
        solicitud.Estado.ShouldBe(EstadoDeSolicitudHabeas.Recibida);
        solicitud.Radicado.ShouldStartWith("HD-2026-");
    }

    [Fact]
    public void El_alcance_solo_vale_en_la_supresion_y_por_omision_es_todo()
    {
        Presentar(TipoDeSolicitudHabeas.Supresion).Alcance.ShouldBe(AlcanceDeSupresion.Todo);
        Presentar(TipoDeSolicitudHabeas.Supresion, AlcanceDeSupresion.Documentos).Alcance.ShouldBe(AlcanceDeSupresion.Documentos);
        Presentar(TipoDeSolicitudHabeas.Consulta, AlcanceDeSupresion.Documentos).Alcance.ShouldBeNull();
        Presentar(TipoDeSolicitudHabeas.Supresion, empresa: Guid.NewGuid()).EsPlataforma.ShouldBeFalse();
        Presentar(TipoDeSolicitudHabeas.Supresion).EsPlataforma.ShouldBeTrue();
    }

    [Fact]
    public void El_aviso_de_plazo_sale_a_dos_dias_habiles_y_al_vencer_una_sola_vez_cada_uno()
    {
        var solicitud = Presentar(TipoDeSolicitudHabeas.Consulta); // vence el 2026-11-26
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 20)).ShouldBeNull(); // faltan 4: 23, 24, 25, 26
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 23)).ShouldBeNull(); // faltan 3
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 24)).ShouldBe(AvisoDePlazoHabeas.PorVencer); // faltan 2: 25 y 26
        solicitud.DiasHabilesRestantes(new DateOnly(2026, 11, 24)).ShouldBe(2);

        solicitud.Apply(new AvisoDePlazoDeSolicitudEnviado(AvisoDePlazoHabeas.PorVencer, Ahora));
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 25)).ShouldBeNull();
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 26)).ShouldBeNull(); // el último día aún está en plazo
        solicitud.AvisoPendiente(new DateOnly(2026, 11, 27)).ShouldBe(AvisoDePlazoHabeas.Vencida);
        solicitud.DiasHabilesRestantes(new DateOnly(2026, 11, 27)).ShouldBe(-1);

        solicitud.Apply(new AvisoDePlazoDeSolicitudEnviado(AvisoDePlazoHabeas.Vencida, Ahora));
        solicitud.AvisoPendiente(new DateOnly(2026, 12, 15)).ShouldBeNull();
    }

    [Fact]
    public void Una_solicitud_resuelta_ya_no_avisa_y_no_se_resuelve_dos_veces()
    {
        var solicitud = Presentar(TipoDeSolicitudHabeas.Correccion);
        var resuelta = solicitud.Resolver(ResultadoDeSolicitudHabeas.Atendida, "vsdp1.r", "{}", "hash", new ActorDeSolicitud("Usuario", "u1"), Ahora);
        resuelta.DentroDelPlazo.ShouldBeTrue();
        solicitud.Apply(resuelta);

        solicitud.Estado.ShouldBe(EstadoDeSolicitudHabeas.Resuelta);
        solicitud.ConstanciaHash.ShouldBe("hash");
        solicitud.AvisoPendiente(new DateOnly(2027, 1, 1)).ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => solicitud.Resolver(ResultadoDeSolicitudHabeas.Atendida, "x", "{}", "h", new ActorDeSolicitud("Usuario", "u1"), Ahora))
            .Codigo.ShouldBe("solicitud-ya-resuelta");
    }

    [Fact]
    public void Responder_pasado_el_plazo_queda_registrado_como_fuera_de_plazo()
    {
        var solicitud = Presentar(TipoDeSolicitudHabeas.Consulta);
        var tarde = Ahora.AddDays(30);
        solicitud.Resolver(ResultadoDeSolicitudHabeas.Atendida, "vsdp1.r", "{}", "h", new ActorDeSolicitud("Usuario", "u1"), tarde).DentroDelPlazo.ShouldBeFalse();
    }

    [Fact]
    public void La_constancia_no_lleva_datos_personales_y_su_huella_es_el_hash_del_json_canonico()
    {
        var constancia = new ConstanciaDeSolicitud("HD-2026-ABCD1234", "Supresion", "Todo", "Validasoft", new DateOnly(2026, 11, 11), new DateOnly(2026, 11, 20), true,
            "Parcial", [new ElementoSuprimido("documento-version", "d:1", "RUT (versión 1)")],
            [new ElementoRetenido("documento-version", "d:2", "RUT (versión 2)", "Obligación legal", new DateOnly(2036, 11, 11))], ["Contactos y nombre comercial"]);
        var (json, hash) = constancia.Sellar();

        hash.ShouldBe(Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))));
        ConstanciaDeSolicitud.Leer(json).ShouldBe(constancia, new ConstanciaIgual());
        json.ShouldNotContain("@");
        constancia.Sellar().Hash.ShouldBe(hash); // determinista
    }

    private sealed class ConstanciaIgual : IEqualityComparer<ConstanciaDeSolicitud>
    {
        public bool Equals(ConstanciaDeSolicitud? a, ConstanciaDeSolicitud? b) => a is not null && b is not null && a.Sellar().Hash == b.Sellar().Hash;

        public int GetHashCode(ConstanciaDeSolicitud c) => c.Radicado.GetHashCode();
    }

    // ---------- crypto-shredding ----------

    [Fact]
    public void El_texto_cifrado_con_la_llave_del_tercero_se_lee_con_ella_y_no_con_otra()
    {
        var llave = CifradoDePersonales.NuevaLlave();
        var kid = Guid.NewGuid();
        var protegido = CifradoDePersonales.Cifrar(llave, Tercero, kid, "Me llamo María y mi cédula es 52.123.456");

        protegido.ShouldStartWith("vsdp1.");
        protegido.ShouldNotContain("María");
        CifradoDePersonales.Leer(protegido).ShouldBe((Tercero, kid));
        CifradoDePersonales.Descifrar(llave, protegido).ShouldBe("Me llamo María y mi cédula es 52.123.456");
        Should.Throw<CryptographicException>(() => CifradoDePersonales.Descifrar(CifradoDePersonales.NuevaLlave(), protegido));
    }

    [Fact]
    public void Un_texto_protegido_alterado_o_copiado_a_otro_tercero_no_se_descifra()
    {
        var llave = CifradoDePersonales.NuevaLlave();
        var kid = Guid.NewGuid();
        var protegido = CifradoDePersonales.Cifrar(llave, Tercero, kid, "dato");
        var partes = protegido.Split('.');

        // Cambiar el tercero que dice ser el dueño: los datos asociados no coinciden.
        var ajeno = string.Join('.', partes[0], Guid.NewGuid().ToString("N"), partes[2], partes[3]);
        Should.Throw<CryptographicException>(() => CifradoDePersonales.Descifrar(llave, ajeno));

        // Cambiar un byte del cifrado.
        var alterado = string.Join('.', partes[0], partes[1], partes[2], (partes[3][0] == 'A' ? 'B' : 'A') + partes[3][1..]);
        Should.Throw<CryptographicException>(() => CifradoDePersonales.Descifrar(llave, alterado));
        CifradoDePersonales.Leer("no-es-un-texto-protegido").ShouldBeNull();
        CifradoDePersonales.Leer(null).ShouldBeNull();
    }

    [Fact]
    public void Dos_cifrados_del_mismo_texto_son_distintos()
    {
        var llave = CifradoDePersonales.NuevaLlave();
        var kid = Guid.NewGuid();
        CifradoDePersonales.Cifrar(llave, Tercero, kid, "igual").ShouldNotBe(CifradoDePersonales.Cifrar(llave, Tercero, kid, "igual"));
    }

    // ---------- retención de las versiones ----------

    private static Documento DocumentoConVersiones(int versiones)
    {
        var documento = Documento.Create(Documento.CrearGlobal(Tercero, CatalogoCanonico.Requerido("RUT"), Ahora));
        for (var i = 1; i <= versiones; i++)
        {
            var sha = $"sha{i}";
            var archivo = new ArchivoVerificado(documento.SiguienteNumero, sha, "boveda", $"terceros/{Tercero:N}/{sha}", "application/pdf", "application/pdf", 1000 * i, "rut.pdf", 1,
                null, Tercero.ToString());
            foreach (var evento in documento.RegistrarVersionArchivo(archivo, null, Antivirus.NoDisponible, false, Ahora))
                documento.Aplicar(evento);
        }

        return documento;
    }

    [Fact]
    public void Suprimir_una_version_borra_su_binario_y_sus_datos_pero_deja_su_rastro()
    {
        var documento = DocumentoConVersiones(2);
        var evento = documento.Suprimir(2, "Supresión por solicitud de habeas data", Ahora).ShouldNotBeNull();
        documento.Aplicar(evento);

        var version = documento.VersionRequerida(2);
        version.Suprimida.ShouldBeTrue();
        version.Estado.ShouldBe(EstadoVersion.Retirada);
        version.TieneArchivo.ShouldBeFalse();
        version.NombreOriginal.ShouldBe("(suprimido)");
        version.Datos.ShouldBeNull();
        version.Sha256.ShouldBe("sha2"); // el hash queda: es el rastro técnico
        documento.VersionActual.ShouldBeNull();
        documento.Suprimir(2, "otra vez", Ahora).ShouldBeNull(); // idempotente
    }

    [Fact]
    public void Una_version_retenida_por_ley_no_se_suprime_ni_se_comparte_de_nuevo()
    {
        var documento = DocumentoConVersiones(1);
        var vinculo = new VinculoCandidato(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tercero, true, EstadoVinculo.EnValidacion, true);
        documento.Compartir(vinculo, OrigenComparticion.Automatica, Ahora).ShouldNotBeNull();

        var marca = documento.MarcarRetencion(1, "Obligación legal", new DateOnly(2036, 11, 11), Guid.NewGuid(), Ahora).ShouldNotBeNull();
        documento.Aplicar(marca);

        var version = documento.VersionRequerida(1);
        version.RetenidaPorLey.ShouldBeTrue();
        version.BaseLegalDeRetencion.ShouldBe("Obligación legal");
        version.RetenidaHasta.ShouldBe(new DateOnly(2036, 11, 11));
        version.TieneArchivo.ShouldBeTrue(); // se conserva íntegra
        Should.Throw<ReglaDeNegocioException>(() => documento.Suprimir(1, "x", Ahora)).Codigo.ShouldBe("version-retenida-por-ley");
        documento.Compartir(new VinculoCandidato(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tercero, true, EstadoVinculo.EnValidacion, true),
            OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        documento.MarcarRetencion(1, "Obligación legal", new DateOnly(2036, 11, 11), Guid.NewGuid(), Ahora).ShouldBeNull(); // idempotente
        documento.MarcarRetencion(1, "Obligación legal", new DateOnly(2040, 1, 1), Guid.NewGuid(), Ahora).ShouldNotBeNull(); // una retención más larga reemplaza
    }
}
