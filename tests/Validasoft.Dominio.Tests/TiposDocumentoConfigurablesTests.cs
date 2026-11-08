using System.Text;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>Tipos de documento configurables (TD-1…TD-6): reglas de archivo, campos, datos declarados, plantilla y vigencias.</summary>
public class TiposDocumentoConfigurablesTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 15, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 6);
    private static readonly Guid Tercero = Guid.NewGuid();
    private const long Mega = 1024 * 1024;

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private static TipoDocumentoTenant Propio(string codigo = "FORMATO_A", ReglaFrescura? frescura = null) =>
        TipoDocumentoTenant.Crear(Guid.NewGuid(), codigo, "Formato de vinculación", null, null, frescura, [5], 2, null, null, Ahora);

    private static readonly CampoFormulario Expedicion = new("fecha_expedicion", "Fecha de expedición", TipoCampo.Fecha, true);

    // ---------- formato, peso y páginas (TD-1) ----------

    [Fact]
    public void Los_canonicos_sembrados_restringen_el_formato_del_rut_y_la_camara_a_pdf()
    {
        CatalogoCanonico.Requerido("RUT").Formatos.ShouldBe([InspectorDeArchivos.Pdf]);
        CatalogoCanonico.Requerido("CAMARA_COMERCIO").Formatos.ShouldBe([InspectorDeArchivos.Pdf]);
        CatalogoCanonico.Requerido("CEDULA").Formatos.ShouldBeEmpty(); // todos
    }

    [Fact]
    public void Un_tipo_puede_ser_mas_estricto_que_el_canonico_nunca_mas_laxo()
    {
        var camara = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CAMARA", null, "CAMARA_COMERCIO", null, null, null, null, null, null, Ahora);
        Codigo(() => camara.ConfigurarCaptura(["pdf", "jpg"], null, null, null, null)).ShouldBe("formato-mas-laxo");

        camara.ConfigurarCaptura(["PDF"], 5, 10, null, null);
        camara.FormatosPermitidos.ShouldBe([InspectorDeArchivos.Pdf]);
        camara.TamanoMaximoMb.ShouldBe(5);
        camara.PaginasMaximas.ShouldBe(10);

        var cedula = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CEDULA", null, "CEDULA", null, null, null, null, null, null, Ahora);
        cedula.ConfigurarCaptura(["image/jpeg", "jpg", "png"], null, null, null, null);
        cedula.FormatosPermitidos.ShouldBe([InspectorDeArchivos.Jpeg, InspectorDeArchivos.Png]);
        Codigo(() => cedula.ConfigurarCaptura(["docx"], null, null, null, null)).ShouldBe("formato-invalido");
        Codigo(() => cedula.ConfigurarCaptura(null, -1, null, null, null)).ShouldBe("limite-invalido");

        // 0 vuelve al límite global; nulo conserva.
        camara.ConfigurarCaptura(null, 0, null, null, null);
        camara.TamanoMaximoMb.ShouldBeNull();
        camara.PaginasMaximas.ShouldBe(10);
    }

    [Fact]
    public void Las_reglas_efectivas_son_lo_mas_estricto_y_sus_mensajes_nombran_el_limite_del_tipo()
    {
        var camara = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CAMARA", "Cámara de comercio", "CAMARA_COMERCIO", null, null, null, null, null,
            null, Ahora);
        camara.ConfigurarCaptura(null, 5, null, null, null);

        var reglas = camara.ReglasEfectivas(20 * Mega, 50);
        reglas.Formatos.ShouldBe([InspectorDeArchivos.Pdf]); // el canónico ya restringe
        reglas.TamanoMaximoBytes.ShouldBe(5 * Mega);
        reglas.PaginasMaximas.ShouldBe(50);
        reglas.Descripcion.ShouldBe("Cámara de comercio admite solo PDF de hasta 5 MB");

        var jpg = Should.Throw<ReglaDeNegocioException>(() => reglas.ValidarDeclarado("image/jpeg", 1000));
        jpg.Tipo.ShouldBe(TipoFalla.TipoNoSoportado);
        jpg.Message.ShouldBe("Cámara de comercio admite solo PDF de hasta 5 MB.");
        Should.Throw<ReglaDeNegocioException>(() => reglas.ValidarDeclarado(InspectorDeArchivos.Pdf, 6 * Mega)).Tipo.ShouldBe(TipoFalla.DemasiadoGrande);
        Should.Throw<ReglaDeNegocioException>(() => reglas.ValidarTipoReal(InspectorDeArchivos.Png)).Tipo.ShouldBe(TipoFalla.TipoNoSoportado);
        reglas.ValidarDeclarado(InspectorDeArchivos.Pdf, 5 * Mega);

        // Nunca por encima del global aunque el tipo diga más.
        camara.ReglasEfectivas(2 * Mega, 50).TamanoMaximoBytes.ShouldBe(2 * Mega);

        // Páginas: el tipo devuelve la versión con su propio límite.
        camara.ConfigurarCaptura(null, null, 3, null, null);
        var devolucion = camara.ReglasEfectivas(20 * Mega, 50).PorPaginas(4).ShouldNotBeNull();
        devolucion.Codigo.ShouldBe("demasiadas-paginas");
        devolucion.Detalle.ShouldContain("hasta 3");
        camara.ReglasEfectivas(20 * Mega, 50).PorPaginas(3).ShouldBeNull();

        // Sin reglas propias, los mensajes globales de siempre.
        var global = ReglasDeArchivo.Efectivas(null, null, 20 * Mega, 50);
        global.Propias.ShouldBeFalse();
        Should.Throw<ReglaDeNegocioException>(() => global.ValidarTamano(21 * Mega)).Message.ShouldBe("El archivo supera el máximo de 20 MB.");
    }

    // ---------- campos (TD-4) ----------

    [Fact]
    public void El_esquema_de_campos_valida_claves_tipos_opciones_y_reservadas()
    {
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("Numero Poliza", "Número", TipoCampo.Texto, true)])).ShouldBe("campo-invalido");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("numero", "A", TipoCampo.Texto, true), new CampoFormulario("numero", "B", TipoCampo.Numero, false)]))
            .ShouldBe("campo-duplicado");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("banco", "Banco", TipoCampo.Lista, true)])).ShouldBe("campo-invalido");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("banco", "Banco", TipoCampo.Lista, true, ["A", "A"])])).ShouldBe("campo-invalido");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("monto", "Monto", TipoCampo.Numero, true, ["1"])])).ShouldBe("campo-invalido");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("fecha_expedicion", "Expedición", TipoCampo.Texto, true)])).ShouldBe("campo-reservado");
        Codigo(() => EsquemaDeFormulario.Validar([new CampoFormulario("nota", " ", TipoCampo.Texto, false)])).ShouldBe("campo-invalido");

        var campos = EsquemaDeFormulario.Validar([
            new CampoFormulario(" numero ", " Número ", TipoCampo.Texto, true, null, " Como aparece "),
            new CampoFormulario("banco", "Banco", TipoCampo.Lista, false, [" Bogotá ", "Occidente"])
        ]);
        campos.Select(c => c.Clave).ShouldBe(["numero", "banco"]); // el orden es el de la lista
        campos[0].Etiqueta.ShouldBe("Número");
        campos[0].Ayuda.ShouldBe("Como aparece");
        campos[1].Opciones.ShouldBe(["Bogotá", "Occidente"]);
    }

    [Fact]
    public void Un_tipo_con_campos_es_siempre_por_vinculo_y_su_modo_debe_ser_coherente()
    {
        var propio = Propio();
        propio.CargaSoportada.ShouldBeTrue(); // archivo
        Codigo(() => propio.ConfigurarCaptura(null, null, null, ModoCaptura.Formulario, null)).ShouldBe("campos-requeridos");
        Codigo(() => propio.ConfigurarCaptura(null, null, null, null, [Expedicion])).ShouldBe("modo-invalido");

        propio.ConfigurarCaptura(null, null, null, ModoCaptura.ArchivoYFormulario, [Expedicion]);
        propio.Modo.ShouldBe(ModoCaptura.ArchivoYFormulario);
        propio.PideArchivo.ShouldBeTrue();
        propio.PideDatos.ShouldBeTrue();
        propio.CargaSoportada.ShouldBeTrue();

        // Un RUT global no lleva campos de una empresa: los datos se compartirían con todas.
        var rut = TipoDocumentoTenant.Crear(Guid.NewGuid(), "RUT", null, "RUT", null, null, null, null, null, null, Ahora);
        Codigo(() => rut.ConfigurarCaptura(null, null, null, ModoCaptura.ArchivoYFormulario, [Expedicion])).ShouldBe("formulario-solo-por-vinculo");
        var rutPropio = TipoDocumentoTenant.Crear(Guid.NewGuid(), "RUT_A", null, "RUT", AmbitoDocumento.PorVinculo, null, null, null, null, null, Ahora);
        rutPropio.ConfigurarCaptura(null, null, null, ModoCaptura.ArchivoYFormulario, [Expedicion]);
        Codigo(() => rutPropio.Actualizar(null, AmbitoDocumento.Global, null, null, null, null, null, null)).ShouldBe("formulario-solo-por-vinculo");

        // Un canónico formulario global sin campos no se entrega; restringido a PorVinculo con campos, sí.
        var cuenta = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CUENTA", null, "CUENTA_BANCARIA", null, null, null, null, null, null, Ahora);
        cuenta.Modo.ShouldBe(ModoCaptura.Formulario);
        cuenta.CargaSoportada.ShouldBeFalse();
        Codigo(() => cuenta.ConfigurarCaptura(null, null, null, ModoCaptura.Archivo, null)).ShouldBe("modo-invalido");
        var cuentaPropia = TipoDocumentoTenant.Crear(Guid.NewGuid(), "CUENTA_A", null, "CUENTA_BANCARIA", AmbitoDocumento.PorVinculo, null, null,
            null, null, null, Ahora);
        cuentaPropia.ConfigurarCaptura(null, null, null, null, [new CampoFormulario("numero", "Número", TipoCampo.Texto, true)]);
        cuentaPropia.CargaSoportada.ShouldBeTrue();
        cuentaPropia.PideArchivo.ShouldBeFalse();
    }

    [Fact]
    public void Los_datos_declarados_se_validan_y_normalizan_contra_el_esquema()
    {
        IReadOnlyList<CampoFormulario> campos = EsquemaDeFormulario.Validar([
            Expedicion,
            new CampoFormulario("monto", "Monto", TipoCampo.Numero, true),
            new CampoFormulario("banco", "Banco", TipoCampo.Lista, false, ["Bogotá", "Occidente"]),
            new CampoFormulario("acepta", "Acepta", TipoCampo.SiNo, true),
            new CampoFormulario("nota", "Nota", TipoCampo.Texto, false)
        ]);
        Dictionary<string, string?> Validos() => new()
        {
            ["fecha_expedicion"] = "2026-10-01", ["monto"] = "1200.50", ["banco"] = "Bogotá", ["acepta"] = "Sí", ["nota"] = "  "
        };

        var datos = EsquemaDeFormulario.ValidarDatos(campos, Validos(), Hoy);
        datos.Datos.Select(d => (d.Clave, d.Valor)).ShouldBe([("fecha_expedicion", "2026-10-01"), ("monto", "1200.5"), ("banco", "Bogotá"), ("acepta", "true")]);
        datos.Hash.Length.ShouldBe(64);

        var faltante = Should.Throw<ReglaDeNegocioException>(() => EsquemaDeFormulario.ValidarDatos(campos,
            new Dictionary<string, string?>(Validos()) { ["monto"] = null }, Hoy));
        faltante.Codigo.ShouldBe("campo-requerido");
        faltante.Datos!["campo"].ShouldBe("monto");
        faltante.Message.ShouldContain("Monto");

        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["monto"] = "1.200,5" }, Hoy)).ShouldBe("valor-invalido");
        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["fecha_expedicion"] = "01/10/2026" }, Hoy))
            .ShouldBe("valor-invalido");
        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["fecha_expedicion"] = "2026-11-07" }, Hoy))
            .ShouldBe("valor-invalido"); // expedida mañana
        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["banco"] = "Otro" }, Hoy)).ShouldBe("valor-invalido");
        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["acepta"] = "tal vez" }, Hoy)).ShouldBe("valor-invalido");
        Codigo(() => EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["extra"] = "x" }, Hoy)).ShouldBe("campo-desconocido");

        // La huella no depende del orden en que llegan los valores; un valor distinto cambia la huella.
        var invertidos = new Dictionary<string, string?>(Validos().Reverse());
        EsquemaDeFormulario.ValidarDatos(campos, invertidos, Hoy).Hash.ShouldBe(datos.Hash);
        EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["monto"] = "1200.5" }, Hoy).Hash.ShouldBe(datos.Hash);
        EsquemaDeFormulario.ValidarDatos(campos, new Dictionary<string, string?>(Validos()) { ["monto"] = "1300" }, Hoy).Hash.ShouldNotBe(datos.Hash);
    }

    [Fact]
    public void Una_version_de_formulario_no_tiene_archivo_y_se_deduplica_por_sus_datos()
    {
        var tipo = Propio();
        tipo.ConfigurarCaptura(null, null, null, ModoCaptura.Formulario, [Expedicion]);
        var vinculo = Guid.NewGuid();
        var documento = Documento.Create(Documento.CrearPorVinculo(Tercero, tipo, tipo.TenantId, Guid.NewGuid(), vinculo, Ahora));
        var datos = EsquemaDeFormulario.ValidarDatos(tipo.Campos, new Dictionary<string, string?> { ["fecha_expedicion"] = "2026-10-01" }, Hoy);

        foreach (var evento in documento.RegistrarVersionFormulario(datos, vinculo, Tercero.ToString(), Ahora))
            documento.Aplicar(evento);

        var v1 = documento.VersionRequerida(1);
        v1.Estado.ShouldBe(EstadoVersion.Actual);
        v1.Clase.ShouldBe(ClaseDocumento.Formulario);
        v1.TieneArchivo.ShouldBeFalse();
        v1.Datos!.ShouldHaveSingleItem().Valor.ShouldBe("2026-10-01");
        documento.DetectarDuplicado(datos.Hash, Ahora, datos.Hash).ShouldNotBeNull().NumeroExistente.ShouldBe(1);
        Codigo(() => documento.RegistrarVersionFormulario(datos, vinculo, Tercero.ToString(), Ahora)).ShouldBe("version-duplicada");
        Codigo(() => documento.RegistrarAcceso(1, null, "Tercero", Tercero.ToString(), "ip", "ua", "Descarga", Ahora)).ShouldBe("version-sin-archivo");
    }

    [Fact]
    public void El_mismo_archivo_con_otros_datos_es_una_version_nueva()
    {
        var tipo = Propio();
        tipo.ConfigurarCaptura(null, null, null, ModoCaptura.ArchivoYFormulario, [Expedicion]);
        var vinculo = Guid.NewGuid();
        var documento = Documento.Create(Documento.CrearPorVinculo(Tercero, tipo, tipo.TenantId, Guid.NewGuid(), vinculo, Ahora));
        DatosVerificados Datos(string fecha) => EsquemaDeFormulario.ValidarDatos(tipo.Campos, new Dictionary<string, string?> { ["fecha_expedicion"] = fecha }, Hoy);
        ArchivoVerificado Archivo() => new(documento.SiguienteNumero, "a1", "b", "k/a1", "application/pdf", "application/pdf", 10, "f.pdf", 1, vinculo, "t");

        foreach (var evento in documento.RegistrarVersionArchivo(Archivo(), null, Antivirus.NoDisponible, false, Ahora, Datos("2026-10-01")))
            documento.Aplicar(evento);
        documento.DetectarDuplicado("a1", Ahora, Datos("2026-10-01").Hash).ShouldNotBeNull();
        documento.DetectarDuplicado("a1", Ahora, Datos("2026-10-02").Hash).ShouldBeNull();

        foreach (var evento in documento.RegistrarVersionArchivo(Archivo(), null, Antivirus.NoDisponible, false, Ahora, Datos("2026-10-02")))
            documento.Aplicar(evento);
        documento.VersionActual.ShouldBe(2);
        documento.VersionRequerida(2).HashDatos.ShouldBe(Datos("2026-10-02").Hash);
    }

    // ---------- vigencia (TD-6) ----------

    [Fact]
    public void La_vigencia_cuenta_desde_la_expedicion_declarada_o_desde_la_carga()
    {
        var regla = ReglaFrescura.DiasDesdeExpedicion(30);
        var expedida = new DatoDeclarado(EsquemaDeFormulario.FechaExpedicion, "Expedición", TipoCampo.Fecha, "2026-10-01");

        CalculadoraDeVigencia.Calcular(regla, Ahora, [expedida]).ShouldBe(new CalculoDeVigencia(new DateOnly(2026, 10, 31), BaseVigencia.FechaExpedicion));
        CalculadoraDeVigencia.Calcular(regla, Ahora, null).ShouldBe(new CalculoDeVigencia(new DateOnly(2026, 12, 6), BaseVigencia.FechaCarga));
        CalculadoraDeVigencia.Calcular(ReglaFrescura.DiasDesdeAprobacion(10), Ahora, [expedida]).VigenteHasta.ShouldBe(new DateOnly(2026, 11, 16));
        CalculadoraDeVigencia.Calcular(ReglaFrescura.Ninguna, Ahora, [expedida]).ShouldBe(new CalculoDeVigencia(null, BaseVigencia.SinVencimiento));
        CalculadoraDeVigencia.Calcular(ReglaFrescura.Periodo("ultimo-anio"), Ahora, null).Base.ShouldBe(BaseVigencia.PeriodoNoCalculado);
    }

    [Fact]
    public void El_vencimiento_propio_acota_la_vigencia_si_llega_antes()
    {
        var expedida = new DatoDeclarado(EsquemaDeFormulario.FechaExpedicion, "Expedición", TipoCampo.Fecha, "2026-10-01");
        var vence = new DatoDeclarado(EsquemaDeFormulario.FechaVencimiento, "Vencimiento", TipoCampo.Fecha, "2026-10-20");

        CalculadoraDeVigencia.Calcular(ReglaFrescura.DiasDesdeExpedicion(30), Ahora, [expedida, vence])
            .ShouldBe(new CalculoDeVigencia(new DateOnly(2026, 10, 20), BaseVigencia.VencimientoPropio));
        CalculadoraDeVigencia.Calcular(ReglaFrescura.DiasDesdeExpedicion(10), Ahora, [expedida, vence]).Base.ShouldBe(BaseVigencia.FechaExpedicion);
        // Sin regla, una póliza vence en su propia fecha.
        CalculadoraDeVigencia.Calcular(ReglaFrescura.Ninguna, Ahora, [vence]).VigenteHasta.ShouldBe(new DateOnly(2026, 10, 20));
    }

    [Fact]
    public void El_estado_pasa_por_por_vencer_gracia_y_vencida()
    {
        var hasta = new DateOnly(2026, 11, 30);
        int[] avisos = [15, 5];
        CalculadoraDeVigencia.Estado(hasta, avisos, 3, new DateOnly(2026, 11, 14)).ShouldBe(EstadoVigencia.Vigente);
        CalculadoraDeVigencia.Estado(hasta, avisos, 3, new DateOnly(2026, 11, 15)).ShouldBe(EstadoVigencia.PorVencer);
        CalculadoraDeVigencia.Estado(hasta, avisos, 3, hasta).ShouldBe(EstadoVigencia.PorVencer);
        CalculadoraDeVigencia.Estado(hasta, avisos, 3, new DateOnly(2026, 12, 3)).ShouldBe(EstadoVigencia.EnGracia);
        CalculadoraDeVigencia.Estado(hasta, avisos, 3, new DateOnly(2026, 12, 4)).ShouldBe(EstadoVigencia.Vencida);
        CalculadoraDeVigencia.Estado(null, avisos, 3, hasta).ShouldBe(EstadoVigencia.SinVencimiento);
        CalculadoraDeVigencia.FechasDeAviso(hasta, [5, 15, 15]).ShouldBe([new DateOnly(2026, 11, 15), new DateOnly(2026, 11, 25)]);
    }

    [Fact]
    public void Cada_vinculo_tiene_su_vigencia_y_la_version_vence_tras_la_gracia_de_todos()
    {
        var rut = Documento.Create(Documento.CrearGlobal(Tercero, CatalogoCanonico.Requerido("RUT"), Ahora));
        foreach (var evento in rut.RegistrarVersionArchivo(new ArchivoVerificado(1, "a1", "boveda", "t/a1", "application/pdf", "application/pdf", 10,
                     "rut.pdf", 1, null, "t"), null, Antivirus.NoDisponible, false, Ahora))
            rut.Aplicar(evento);
        var a = new VinculoCandidato(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tercero, true, EstadoVinculo.EnValidacion, true);
        var b = new VinculoCandidato(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Tercero, true, EstadoVinculo.EnValidacion, true);
        rut.Aplicar(rut.Compartir(a, OrigenComparticion.AlAceptarInvitacion, Ahora)!);
        rut.Aplicar(rut.Compartir(b, OrigenComparticion.AlAceptarInvitacion, Ahora)!);

        var estricto = TipoDocumentoTenant.Crear(a.TenantId, "RUT", null, "RUT", null, ReglaFrescura.DiasDesdeExpedicion(10), [5], 2, null, null, Ahora);
        var laxo = TipoDocumentoTenant.Crear(b.TenantId, "RUT", null, "RUT", null, ReglaFrescura.DiasDesdeExpedicion(30), [5], 0, null, null, Ahora);
        DestinoDeVigencia[] destinos =
        [
            new(a.TenantId, a.EmpresaId, a.VinculoId, estricto), new(b.TenantId, b.EmpresaId, b.VinculoId, laxo),
            new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), laxo) // no compartido: se ignora
        ];

        var vigencias = rut.CalcularVigencias(destinos, Ahora);
        vigencias.Select(v => v.VigenteHasta).ShouldBe([new DateOnly(2026, 11, 16), new DateOnly(2026, 12, 6)]);
        foreach (var evento in vigencias)
            rut.Aplicar(evento);
        rut.CalcularVigencias(destinos, Ahora).ShouldBeEmpty(); // idempotente
        rut.VersionRequerida(1).ProximaVigencia!.VinculoId.ShouldBe(a.VinculoId);

        // A: vigente hasta el 16 + 2 días de gracia ⇒ vence desde el 19; antes, nada.
        rut.Vencer(1, a.VinculoId, new DateTimeOffset(2026, 11, 18, 23, 0, 0, TimeSpan.Zero)).ShouldBeNull();
        var vencidaA = rut.Vencer(1, a.VinculoId, new DateTimeOffset(2026, 11, 19, 5, 0, 0, TimeSpan.Zero)).ShouldNotBeNull();
        rut.Aplicar(vencidaA);
        rut.VersionRequerida(1).Estado.ShouldBe(EstadoVersion.Actual); // B la sigue viendo vigente
        rut.Vencer(1, a.VinculoId, Ahora.AddDays(60)).ShouldBeNull(); // ya venció para A

        rut.Aplicar(rut.Vencer(1, b.VinculoId, new DateTimeOffset(2026, 12, 7, 5, 0, 0, TimeSpan.Zero)).ShouldNotBeNull());
        rut.VersionRequerida(1).Estado.ShouldBe(EstadoVersion.Vencida);
        rut.VersionActual.ShouldBe(1); // sigue siendo la actual: renovarla es H6

        // Una versión nueva actual deja sin efecto el vencimiento de la anterior.
        foreach (var evento in rut.RegistrarVersionArchivo(new ArchivoVerificado(2, "b2", "boveda", "t/b2", "application/pdf", "application/pdf", 10,
                     "rut.pdf", 1, null, "t"), null, Antivirus.NoDisponible, false, Ahora))
            rut.Aplicar(evento);
        rut.VersionRequerida(1).Estado.ShouldBe(EstadoVersion.Superada);
        rut.Vencer(1, a.VinculoId, Ahora.AddDays(90)).ShouldBeNull();
    }

    // ---------- plantilla (TD-5) ----------

    [Fact]
    public void La_plantilla_se_versiona_deduplica_y_se_retira_conservando_el_historial()
    {
        var tipo = Propio();
        Should.Throw<ReglaDeNegocioException>(() => tipo.IniciarPlantilla("b", "f.jpg", 10, "image/jpeg", null, Mega, TimeSpan.FromMinutes(15), Ahora))
            .Tipo.ShouldBe(TipoFalla.TipoNoSoportado);
        Should.Throw<ReglaDeNegocioException>(() => tipo.IniciarPlantilla("b", "f.pdf", 2 * Mega, "application/pdf", null, Mega, TimeSpan.FromMinutes(15), Ahora))
            .Tipo.ShouldBe(TipoFalla.DemasiadoGrande);

        var carga = tipo.IniciarPlantilla("b", "formato.docx", 10, FormatosDePlantilla.Docx, null, Mega, TimeSpan.FromMinutes(15), Ahora);
        carga.Version.ShouldBe(1);
        var (v1, duplicado) = tipo.ConfirmarPlantilla(1, "s1", FormatosDePlantilla.Docx, 10, "plantillas/x/s1", "admin", Ahora);
        duplicado.ShouldBeFalse();
        tipo.Plantilla.ShouldBe(v1);
        Codigo(() => tipo.ConfirmarPlantilla(1, "s1", FormatosDePlantilla.Docx, 10, "k", "admin", Ahora)).ShouldBe("carga-no-encontrada");

        tipo.IniciarPlantilla("b", "formato.docx", 10, FormatosDePlantilla.Docx, null, Mega, TimeSpan.FromMinutes(15), Ahora);
        tipo.ConfirmarPlantilla(2, "s1", FormatosDePlantilla.Docx, 10, "k", "admin", Ahora).Duplicado.ShouldBeTrue(); // mismo archivo
        tipo.IniciarPlantilla("b", "formato-2.pdf", 10, InspectorDeArchivos.Pdf, null, Mega, TimeSpan.FromMinutes(15), Ahora).Version.ShouldBe(2);
        tipo.ConfirmarPlantilla(2, "s2", InspectorDeArchivos.Pdf, 10, "k2", "admin", Ahora).Plantilla.Version.ShouldBe(2);
        tipo.PlantillaVigente.ShouldBe(2);

        tipo.RetirarPlantilla(Ahora);
        tipo.Plantilla.ShouldBeNull();
        tipo.Plantillas.Count.ShouldBe(2);
        tipo.PlantillaRequerida(1).Sha256.ShouldBe("s1");
        Codigo(() => tipo.RetirarPlantilla(Ahora)).ShouldBe("plantilla-no-encontrada");
    }

    [Fact]
    public void El_tipo_real_de_la_plantilla_sale_de_sus_bytes()
    {
        FormatosDePlantilla.Detectar(Encoding.Latin1.GetBytes("%PDF-1.4\n%%EOF")).ShouldBe(InspectorDeArchivos.Pdf);
        FormatosDePlantilla.Detectar(Zip("word/document.xml")).ShouldBe(FormatosDePlantilla.Docx);
        FormatosDePlantilla.Detectar(Zip("xl/workbook.xml")).ShouldBe(FormatosDePlantilla.Xlsx);
        FormatosDePlantilla.Detectar(Zip("otro.txt")).ShouldBeNull();
        FormatosDePlantilla.Detectar([0x50, 0x4B, 0x03, 0x04, 1, 2, 3]).ShouldBeNull();
    }

    internal static byte[] Zip(string entrada)
    {
        using var memoria = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(memoria, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var escritor = new StreamWriter(zip.CreateEntry(entrada).Open());
            escritor.Write("<x/>");
        }

        return memoria.ToArray();
    }
}
