using System.IO.Compression;
using System.Text;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

public class DocumentoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid Tercero = Guid.NewGuid();

    private static Documento Rut() => Documento.Create(Documento.CrearGlobal(Tercero, CatalogoCanonico.Requerido("RUT"), Ahora));

    private static ArchivoVerificado Archivo(Documento d, string sha, Guid? vinculo = null) =>
        new(d.SiguienteNumero, sha, "boveda", $"terceros/{Tercero:N}/{sha}", "application/pdf", "application/pdf", 1000, "rut.pdf", 1, vinculo,
            Tercero.ToString());

    /// <summary>Registra la versión aplicando sus eventos (como hará Marten con la proyección inline).</summary>
    private static IReadOnlyList<object> Cargar(Documento d, string sha, MotivoDeDevolucion? devolucion = null,
        Antivirus antivirus = Antivirus.NoDisponible, bool exigirAntivirus = false)
    {
        var eventos = d.RegistrarVersionArchivo(Archivo(d, sha), devolucion, antivirus, exigirAntivirus, Ahora);
        foreach (var e in eventos)
            d.Aplicar(e);
        return eventos;
    }

    private static VinculoCandidato Vinculo(Guid? id = null, bool aceptada = true, bool exige = true,
        EstadoVinculo estado = EstadoVinculo.EnValidacion) =>
        new(id ?? Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tercero, aceptada, estado, exige);

    // ---------- versiones ----------

    [Fact]
    public void Una_version_valida_pasa_por_recibida_y_analisis_hasta_actual()
    {
        var rut = Rut();
        var eventos = Cargar(rut, "a1");

        eventos.Select(e => e.GetType()).ShouldBe([typeof(VersionCargada), typeof(AnalisisDeVersionIniciado), typeof(HechosDeVersionRegistrados),
            typeof(VersionActualCambiada)]);
        rut.VersionActual.ShouldBe(1);
        var v1 = rut.VersionRequerida(1);
        v1.Estado.ShouldBe(EstadoVersion.Actual);
        v1.Hechos!.Comparacion.ShouldBe(Comparacion.SinAnterior);
        v1.Hechos.ConIa.ShouldBeFalse();
        v1.Hechos.PendienteRevisionHumana.ShouldBeTrue();
    }

    [Fact]
    public void Solo_hay_una_version_actual_y_la_anterior_queda_superada()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        Cargar(rut, "b2");
        var cambio = Cargar(rut, "c3").OfType<VersionActualCambiada>().Single();

        cambio.NumeroAnterior.ShouldBe(2);
        cambio.Comparacion.ShouldBe(Comparacion.Distinta);
        cambio.CamposCambiados.ShouldBeEmpty();
        rut.Versiones.Count(v => v.Estado == EstadoVersion.Actual).ShouldBe(1);
        rut.Versiones.Select(v => v.Estado).ShouldBe([EstadoVersion.Superada, EstadoVersion.Superada, EstadoVersion.Actual]);
        rut.VersionActual.ShouldBe(3);
    }

    [Fact]
    public void El_mismo_hash_no_crea_otra_version()
    {
        var rut = Rut();
        Cargar(rut, "a1");

        rut.DetectarDuplicado("A1", Ahora).ShouldNotBeNull().NumeroExistente.ShouldBe(1);
        rut.DetectarDuplicado("b2", Ahora).ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => rut.RegistrarVersionArchivo(Archivo(rut, "a1"), null, Antivirus.NoDisponible, false, Ahora))
            .Codigo.ShouldBe("version-duplicada");
    }

    [Fact]
    public void Una_version_devuelta_no_reemplaza_a_la_actual_ni_se_comparte()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        Cargar(rut, "b2", new MotivoDeDevolucion("pdf-con-contenido-activo", "Trae JavaScript"));

        rut.VersionActual.ShouldBe(1);
        var devuelta = rut.VersionRequerida(2);
        devuelta.Estado.ShouldBe(EstadoVersion.Devuelta);
        devuelta.MotivoDevolucion.ShouldBe("pdf-con-contenido-activo");

        var compartida = rut.Compartir(Vinculo(), OrigenComparticion.Automatica, Ahora).ShouldNotBeNull();
        compartida.Numero.ShouldBe(1); // la actual, nunca la devuelta
    }

    [Fact]
    public void Sin_antivirus_exigido_la_version_espera_en_analisis_y_un_virus_la_devuelve()
    {
        var rut = Rut();
        Cargar(rut, "a1", exigirAntivirus: true);
        rut.VersionRequerida(1).Estado.ShouldBe(EstadoVersion.EnAnalisis);
        rut.VersionActual.ShouldBeNull();

        Cargar(rut, "b2", antivirus: Antivirus.Infectado);
        rut.VersionRequerida(2).Estado.ShouldBe(EstadoVersion.Devuelta);
        rut.VersionRequerida(2).MotivoDevolucion.ShouldBe("archivo-infectado");
    }

    [Fact]
    public void Una_carga_con_numero_viejo_es_conflicto()
    {
        var rut = Rut();
        var vieja = Archivo(rut, "a1");
        Cargar(rut, "b2");

        Should.Throw<ReglaDeNegocioException>(() => rut.RegistrarVersionArchivo(vieja, null, Antivirus.NoDisponible, false, Ahora))
            .Codigo.ShouldBe("carga-desactualizada");
    }

    [Fact]
    public void Un_formulario_no_se_carga_todavia()
    {
        Should.Throw<ReglaDeNegocioException>(() => Documento.CrearGlobal(Tercero, CatalogoCanonico.Requerido("CUENTA_BANCARIA"), Ahora))
            .Codigo.ShouldBe("clase-no-soportada");
        Should.Throw<ReglaDeNegocioException>(() => Documento.CrearGlobal(Tercero, CatalogoCanonico.Requerido("POLIZA"), Ahora))
            .Codigo.ShouldBe("vinculo-requerido");
    }

    // ---------- retiro ----------

    [Fact]
    public void Retirar_la_actual_restituye_la_superada_mas_reciente()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        Cargar(rut, "b2");

        var eventos = rut.RetirarVersion(2, "Subí el RUT de otra empresa", Ahora);
        eventos.Select(e => e.GetType()).ShouldBe([typeof(VersionRetirada), typeof(VersionActualRestituida)]);
        foreach (var e in eventos)
            rut.Aplicar(e);

        rut.VersionActual.ShouldBe(1);
        rut.VersionRequerida(1).Estado.ShouldBe(EstadoVersion.Actual);
        rut.VersionRequerida(2).Estado.ShouldBe(EstadoVersion.Retirada);
    }

    [Fact]
    public void Retirar_la_unica_version_deja_el_documento_sin_actual_y_no_se_retira_dos_veces()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        Cargar(rut, "b2", new MotivoDeDevolucion("demasiadas-paginas", "60 páginas"));

        foreach (var e in rut.RetirarVersion(1, "error", Ahora))
            rut.Aplicar(e);
        rut.VersionActual.ShouldBeNull();

        Should.Throw<ReglaDeNegocioException>(() => rut.RetirarVersion(1, "otra vez", Ahora)).Codigo.ShouldBe("estado-invalido");
        Should.Throw<ReglaDeNegocioException>(() => rut.RetirarVersion(2, "devuelta", Ahora)).Codigo.ShouldBe("estado-invalido");
        Should.Throw<ReglaDeNegocioException>(() => rut.RetirarVersion(9, "no existe", Ahora)).Codigo.ShouldBe("version-no-encontrada");
    }

    // ---------- compartición ----------

    [Fact]
    public void Nada_se_comparte_antes_de_aceptar_la_invitacion_ni_con_un_vinculo_congelado()
    {
        var rut = Rut();
        Cargar(rut, "a1");

        rut.Compartir(Vinculo(aceptada: false), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        rut.Compartir(Vinculo(estado: EstadoVinculo.Bloqueado), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        rut.Compartir(Vinculo(estado: EstadoVinculo.Desvinculado), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        rut.Compartir(Vinculo(exige: false), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        rut.Compartir(Vinculo(), OrigenComparticion.AlAceptarInvitacion, Ahora).ShouldNotBeNull();
    }

    [Fact]
    public void Una_version_nueva_se_comparte_con_todos_los_vinculos_aceptados_que_exigen_el_tipo_una_sola_vez()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        var a = Vinculo();
        var b = Vinculo();
        var sinAceptar = Vinculo(aceptada: false);
        foreach (var e in rut.CompartirActual([a], OrigenComparticion.AlAceptarInvitacion, Ahora))
            rut.Aplicar(e);

        // Repetir no duplica (idempotente por vínculo y versión).
        rut.CompartirActual([a], OrigenComparticion.Automatica, Ahora).ShouldBeEmpty();

        Cargar(rut, "b2");
        var nuevas = rut.CompartirActual([a, b, sinAceptar], OrigenComparticion.Automatica, Ahora);
        nuevas.Select(c => c.VinculoId).ShouldBe([a.VinculoId, b.VinculoId]);
        nuevas.ShouldAllBe(c => c.Numero == 2);
    }

    [Fact]
    public void Un_documento_por_vinculo_no_sale_de_su_vinculo()
    {
        var tipo = TipoDocumentoTenant.Crear(Guid.NewGuid(), "POLIZA_A", null, "POLIZA", null, null, null, null, null, null, Ahora);
        var propio = Guid.NewGuid();
        var poliza = Documento.Create(Documento.CrearPorVinculo(Tercero, tipo, tipo.TenantId, Guid.NewGuid(), propio, Ahora));
        poliza.ClaveNatural.ShouldBe(Documento.ClavePorVinculo(propio, tipo.Id));
        Cargar(poliza, "p1");

        // Otro vínculo (aunque su flujo pida el canónico) no recibe nada; el propio sí, tras aceptar.
        poliza.Compartir(Vinculo(exige: true), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        poliza.Compartir(Vinculo(propio, aceptada: false), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        poliza.Compartir(Vinculo(propio, exige: false), OrigenComparticion.Automatica, Ahora).ShouldNotBeNull().VinculoId.ShouldBe(propio);
    }

    [Fact]
    public void Una_empresa_solo_registra_acceso_a_versiones_compartidas_con_su_vinculo()
    {
        var rut = Rut();
        Cargar(rut, "a1");
        var a = Vinculo();
        rut.Aplicar(rut.Compartir(a, OrigenComparticion.AlAceptarInvitacion, Ahora)!);

        var empresaA = new DestinoDeComparticion(a.TenantId, a.EmpresaId, a.VinculoId);
        var acceso = rut.RegistrarAcceso(1, empresaA, "Usuario", "u-1", "10.0.0.1", "pruebas", "Descarga", Ahora);
        rut.Aplicar(acceso);
        rut.VersionRequerida(1).AccedidaPorEmpresa.ShouldBeTrue();

        var otra = new DestinoDeComparticion(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Should.Throw<ReglaDeNegocioException>(() => rut.RegistrarAcceso(1, otra, "Usuario", "u-2", "ip", "ua", "Descarga", Ahora))
            .Codigo.ShouldBe("version-no-encontrada");

        // El propio tercero descarga cualquiera de sus versiones.
        rut.RegistrarAcceso(1, null, "Tercero", Tercero.ToString(), "ip", "ua", "Descarga", Ahora).EmpresaId.ShouldBeNull();
    }

    [Fact]
    public void Proyectar_no_modifica_el_agregado_original()
    {
        var rut = Rut();
        var eventos = rut.RegistrarVersionArchivo(Archivo(rut, "a1"), null, Antivirus.NoDisponible, false, Ahora);

        var proyectado = rut.Proyectar(eventos);

        proyectado.VersionActual.ShouldBe(1);
        rut.VersionActual.ShouldBeNull();
        rut.Versiones.ShouldBeEmpty();
    }
}

public class TipoDocumentoTenantTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Hereda_nombre_frescura_y_ambito_del_canonico()
    {
        var tipo = TipoDocumentoTenant.Crear(Guid.NewGuid(), "rut", null, "RUT", null, null, null, null, null, null, Ahora);

        tipo.Codigo.ShouldBe("RUT");
        tipo.Nombre.ShouldBe("Registro Único Tributario");
        tipo.Ambito.ShouldBe(AmbitoDocumento.Global);
        tipo.ReglaFrescura.ShouldBe(ReglaFrescura.DiasDesdeExpedicion(365));
        tipo.DiasGracia.ShouldBe(15);
    }

    [Fact]
    public void El_ambito_se_puede_restringir_pero_nunca_ampliar()
    {
        var restringido = TipoDocumentoTenant.Crear(Guid.NewGuid(), "RUT_PROPIO", null, "RUT", AmbitoDocumento.PorVinculo, null, null, null, null,
            null, Ahora);
        restringido.Ambito.ShouldBe(AmbitoDocumento.PorVinculo);

        Should.Throw<ReglaDeNegocioException>(() =>
                TipoDocumentoTenant.Crear(Guid.NewGuid(), "POLIZA", null, "POLIZA", AmbitoDocumento.Global, null, null, null, null, null, Ahora))
            .Codigo.ShouldBe("ambito-no-ampliable");

        // Un tipo propio sin canónico es siempre PorVinculo.
        var propio = TipoDocumentoTenant.Crear(Guid.NewGuid(), "FORMATO_A", "Formato A", null, null, null, null, null, null, null, Ahora);
        propio.Ambito.ShouldBe(AmbitoDocumento.PorVinculo);
        Should.Throw<ReglaDeNegocioException>(() => propio.Actualizar(null, AmbitoDocumento.Global, null, null, null, null, null, null))
            .Codigo.ShouldBe("ambito-no-ampliable");
    }

    // Antes (BO-3) aquí se probaban las exigencias por empresa y tipo de vínculo; desde RQ-3 lo que se pide lo deciden
    // los requisitos del flujo (FlujoTests y RequisitosExpedienteTests). Se conserva la regla que sigue en el tipo:
    // los valores nulos no cambian nada y desactivar es explícito.
    [Fact]
    public void Actualizar_conserva_lo_que_llega_nulo_y_puede_desactivar()
    {
        var tipo = TipoDocumentoTenant.Crear(Guid.NewGuid(), "RUT", null, "RUT", null, null, null, null, null, "Ayuda", Ahora);

        tipo.Actualizar(null, null, null, null, null, null, null, activo: false);

        tipo.Activo.ShouldBeFalse();
        tipo.Nombre.ShouldBe("Registro Único Tributario");
        tipo.TextoAyuda.ShouldBe("Ayuda");
    }

    [Fact]
    public void La_semilla_trae_los_seis_canonicos_de_archivo()
    {
        var semilla = TipoDocumentoTenant.Semilla(Guid.NewGuid(), Ahora);

        semilla.Select(t => t.Codigo).ShouldBe(["RUT", "CAMARA_COMERCIO", "CEDULA", "ESTADOS_FINANCIEROS", "CERTIFICACION_BANCARIA", "POLIZA"]);
        semilla.ShouldAllBe(t => t.Activo && t.Clase == ClaseDocumento.Archivo);
        semilla.Single(t => t.Codigo == "POLIZA").Ambito.ShouldBe(AmbitoDocumento.PorVinculo);
    }

    [Fact]
    public void El_catalogo_canonico_tiene_codigos_unicos_y_no_trae_los_retirados()
    {
        CatalogoCanonico.Todos.Select(t => t.Codigo).ShouldBeUnique();
        foreach (var retirado in new[] { "CERT_BANCARIA", "INFO_BANCARIA", "TRATAMIENTO_DATOS", "INFO_GENERAL", "CEDULA_REPRESENTANTE", "POLIZA_RC" })
            CatalogoCanonico.Buscar(retirado).ShouldBeNull();
        CatalogoCanonico.Buscar("rut")!.Codigo.ShouldBe("RUT");
    }
}

public class InspectorDeArchivosTests
{
    private static byte[] Pdf(string catalogo, string extra = "") =>
        Encoding.Latin1.GetBytes(
            $"%PDF-1.7\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R {catalogo} >>\nendobj\n" +
            "2 0 obj\n<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 >>\nendobj\n" +
            "3 0 obj\n<< /Type /Page /Parent 2 0 R >>\nendobj\n4 0 obj\n<< /Type /Page /Parent 2 0 R >>\nendobj\n" +
            extra + "trailer\n<< /Root 1 0 R >>\n%%EOF\n");

    [Fact]
    public void Detecta_el_tipo_real_por_magic_bytes()
    {
        InspectorDeArchivos.DetectarMime(Pdf("")).ShouldBe("application/pdf");
        InspectorDeArchivos.DetectarMime([0xFF, 0xD8, 0xFF, 0xE0, 0, 0]).ShouldBe("image/jpeg");
        InspectorDeArchivos.DetectarMime([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0]).ShouldBe("image/png");
        InspectorDeArchivos.DetectarMime("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()).ShouldBe("image/webp");
        InspectorDeArchivos.DetectarMime("<html>no soy un PDF</html>"u8.ToArray()).ShouldBeNull();
        InspectorDeArchivos.DetectarMime("PK\x03\x04 xlsx"u8.ToArray()).ShouldBeNull();
    }

    [Fact]
    public void Un_pdf_simple_no_tiene_contenido_activo_y_cuenta_sus_paginas()
    {
        var inspeccion = InspectorDeArchivos.Inspeccionar(Pdf("/OpenAction [3 0 R /Fit]"));

        inspeccion.MimeReal.ShouldBe("application/pdf");
        inspeccion.Paginas.ShouldBe(2);
        inspeccion.ContenidoActivo.ShouldBeEmpty(); // OpenAction con destino (arreglo) es solo la vista inicial
        InspectorDeArchivos.MotivoDeDevolucion(inspeccion, 50).ShouldBeNull();
        InspectorDeArchivos.MotivoDeDevolucion(inspeccion, 1)!.Codigo.ShouldBe("demasiadas-paginas");
    }

    [Theory]
    [InlineData("/OpenAction << /S /JavaScript /JS (app.alert(1)) >>", "JavaScript")]
    [InlineData("/Names << /JavaScript 9 0 R >>", "JavaScript")]
    [InlineData("/OpenAction << /S /Launch /F (calc.exe) >>", "Launch")]
    [InlineData("/Names << /EmbeddedFiles 9 0 R >>", "EmbeddedFiles")]
    [InlineData("/AA << /O 9 0 R >>", "AA")]
    [InlineData("/OpenAction << /S /J#61vaScript /JS (x) >>", "JavaScript")]
    public void Un_pdf_con_contenido_activo_se_devuelve(string catalogo, string esperado)
    {
        var inspeccion = InspectorDeArchivos.Inspeccionar(Pdf(catalogo));

        inspeccion.ContenidoActivo.ShouldContain(esperado);
        InspectorDeArchivos.MotivoDeDevolucion(inspeccion, 50)!.Codigo.ShouldBe("pdf-con-contenido-activo");
    }

    [Fact]
    public void Detecta_javascript_escondido_en_un_flujo_de_objetos_comprimido()
    {
        var objetos = Encoding.Latin1.GetBytes("9 0 << /S /JavaScript /JS (app.launchURL('x')) >>");
        using var comprimido = new MemoryStream();
        using (var zlib = new ZLibStream(comprimido, CompressionLevel.Optimal, leaveOpen: true))
            zlib.Write(objetos);

        var flujo = Encoding.Latin1.GetBytes($"5 0 obj\n<< /Type /ObjStm /N 1 /First 4 /Filter /FlateDecode /Length {comprimido.Length} >>\nstream\n")
            .Concat(comprimido.ToArray())
            .Concat(Encoding.Latin1.GetBytes("\nendstream\nendobj\n"));
        var pdf = Pdf("", Encoding.Latin1.GetString([.. flujo]));

        InspectorDeArchivos.Inspeccionar(pdf).ContenidoActivo.ShouldContain("JavaScript");
    }

    [Fact]
    public void Los_datos_binarios_de_un_flujo_no_dan_falsos_positivos()
    {
        // Un flujo de imagen que casualmente contiene "/JS " no se revisa: solo diccionarios y flujos de objetos.
        var pdf = Pdf("", "6 0 obj\n<< /Type /XObject /Subtype /Image /Length 12 >>\nstream\n/JS /AA data\nendstream\nendobj\n");

        InspectorDeArchivos.Inspeccionar(pdf).ContenidoActivo.ShouldBeEmpty();
    }
}
