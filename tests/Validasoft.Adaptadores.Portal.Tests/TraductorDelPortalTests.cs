using Validasoft.Aplicacion.Ficha;
using Validasoft.Aplicacion.Importacion;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Adaptadores.Portal.Tests;

/// <summary>
/// Adaptador de origen del Portal (F4, FM-39…): el traductor puro con una tabla de casos por sección (DV recalculado,
/// calidades ⇒ códigos DIAN con <c>NoObligFacturar</c>, fechas y porcentajes de texto, ciudad sin DANE, personas duplicadas,
/// financiera por año), las conversiones de catálogos y el adaptador visto como origen genérico (clave, unidades aceptadas).
/// </summary>
public class TraductorDelPortalTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 9);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);

    internal static readonly CatalogosDelPortal Catalogos = new(
        Ciudades:
        [
            new Ciudades(1, "Bogotá", "11", "001"), new Ciudades(2, "Medellín", "5", "1"), new Ciudades(3, "Sin código", null, null),
            new Ciudades(4, "Departamento inexistente", "98", "001")
        ],
        ActividadesEconomicas: [new ActividadEconomica(10, "4663", "Ferretería"), new ActividadEconomica(11, "4290", "Obras civiles"), new ActividadEconomica(12, "X1", "Mala")],
        Bancos: [new Bancos("1", "1007", "Bancolombia"), new Bancos("2", "51", "Davivienda"), new Bancos("Z", "ABC", "Sin código", false)],
        Grupos: [new GruposTercero(1, "Obra gris")],
        Categorias: [new CategoriasTercero(5, 1, "Concretos")],
        Especialidades: [new EspecialidadesTercero(20, 5, 1, "Concreto premezclado"), new EspecialidadesTercero(21, 5, 1, "Bombeo")],
        Paises: [new Paises(1, "CO", "Colombia"), new Paises(2, "US", "Estados Unidos"), new Paises(3, "NARNIA", "Narnia")]);

    /// <summary>Catálogos de plataforma: la semilla más los del Portal convertidos (como en una corrida que los importa).</summary>
    internal static ICatalogosDeFicha Plataforma()
    {
        var entradas = new List<EntradaDeCatalogo>();
        foreach (var (catalogo, filas) in SemillaDeCatalogos.Todas)
        {
            var niveles = new Dictionary<string, int>();
            foreach (var fila in filas.OrderBy(f => f.Nivel ?? 0))
            {
                var (entrada, _) = ReglasDeCatalogo.Validar(catalogo, fila, p => niveles.TryGetValue(p, out var n) ? n : null, Ahora);
                if (entrada!.Nivel is { } nivel)
                    niveles[entrada.Codigo] = nivel;
                entradas.Add(entrada);
            }
        }

        return ImportadorDeTerceros.Combinar(entradas, CatalogosImportables(), Ahora);
    }

    /// <summary>Los catálogos del Portal como los entrega el adaptador al importador genérico.</summary>
    internal static IReadOnlyList<CatalogoImportable> CatalogosImportables() =>
        [.. CodigosDelPortal.Filas(Catalogos).Select(c => new CatalogoImportable(c.Catalogo, c.Filas, c.Rechazos))];

    private static TerceroDelPortal Juridico(int terid = 101) => new(
        new TercerosPortal(terid, 3),
        new TerInformacionGeneral(1, "Aceros del Norte S.A.S.", null, "J", "NIT", "900123456", 0, "COMPRAS@ACEROS.CO", 1, "Calle 45 # 12-08", 10,
            "6015551234", "aceros.co", true, null, "Colombia", 2005),
        new TerDireccionEstructurada("CL", "45A BIS", "SUR", "12", null, "08", "OF", "301"),
        new TerInfoTributaria(ResponsableIVA: true, Declarante: true, GranContribuyente: true, AutoRetenedorICA: true, NoObligFacturar: true,
            ResolucionGC: 9061, FechaGC: new DateTime(2020, 12, 10)),
        [new TerRetencionICA(1, 1, 10)],
        [
            new TerInfoFinanciera(1, 2024, 1_000_000, 600_000, 400_000, 900_000, 800_000, 100_000, 10_000, 5_000),
            new TerInfoFinanciera(2, 2025, 1_200_000, 700_000, 500_000, 950_000, 850_000, 110_000, 12_000, 6_000)
        ],
        new TerTransaccionMonedaExtranjera(true, [new TerTransaccionMonedaExtranjeraTipo(1, 1, null, [2])]),
        [
            new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", "Gerente", null, false, false, 1, "2001-05-10", "Bogotá", "15/03/1980"),
            new TerCamaraComercio(2, 2, "CC", "79123456", "Juan", "Pérez", null, "60", false, false),
            new TerCamaraComercio(3, 2, "NIT", "860034313-7", "Inversiones S.A.", null, null, "40,0", false, false)
        ],
        new TerSISO(true, true, true, true, true, true, true, true),
        [20],
        [new TercerosConstructora(7, 3)]);

    private static ResultadoDeTraduccion Traducir(TerceroDelPortal t) => TraductorDelPortal.Traducir(t, Catalogos, Plataforma(), Hoy);

    private static T Datos<T>(ResultadoDeTraduccion r, string seccion, int? periodo = null) =>
        (T)r.Secciones.Single(s => s.Definicion.Codigo == seccion && s.Periodo == periodo).Datos;

    private static ResultadoDeRegla Regla(ResultadoDeTraduccion r, string seccion, int? periodo = null)
    {
        var s = r.Secciones.Single(x => x.Definicion.Codigo == seccion && x.Periodo == periodo);
        return ReglasDeFicha.Evaluar(s.Definicion, s.Datos, new ContextoDeSeccion(r.Tercero!.TipoPersona, r.Tercero.Identificacion, periodo, Hoy),
            Plataforma(), false, 0);
    }

    // ---------- el tercero completo ----------

    [Fact]
    public void Un_juridico_completo_da_las_seis_secciones_completas_y_la_financiera_de_cada_anio()
    {
        var r = Traducir(Juridico());

        r.ClaveLegado.ShouldBe("portal:101");
        r.Rechazos.ShouldBeEmpty();
        (r.Tercero!.Identificacion.Clave, r.Tercero.TipoPersona, r.Tercero.Nombre).ShouldBe(("CO|NIT|900123456", TipoPersona.Juridica, "Aceros del Norte S.A.S."));
        r.Secciones.Select(s => (s.Definicion.Codigo, s.Periodo)).ShouldBe(
        [
            (CodigosDeSeccion.General, (int?)null), (CodigosDeSeccion.Tributaria, null), (CodigosDeSeccion.Legal, null), (CodigosDeSeccion.Financiera, 2024),
            (CodigosDeSeccion.Financiera, 2025), (CodigosDeSeccion.Sst, null), (CodigosDeSeccion.Especialidades, null)
        ]);
        foreach (var s in r.Secciones)
            Regla(r, s.Definicion.Codigo, s.Periodo).Completitud.Completa.ShouldBeTrue(s.Definicion.Codigo);

        var general = Datos<DatosGenerales>(r, CodigosDeSeccion.General);
        (general.Correo, general.Ciudad, general.CiiuPrincipal, general.Pais, general.PaginaWeb, general.Dv).ShouldBe(
            ("compras@aceros.co", "11001", "4663", "CO", "https://aceros.co", "8"));
        general.DireccionEstructurada.ShouldBe(new DireccionEstructurada
        {
            TipoVia = "Calle", NumeroVia = "45", LetraVia = "A", Bis = true, Cuadrante = "Sur", NumeroCruce = "12", Placa = "08", Complemento = "OF 301"
        });
        Datos<DatosEspecialidades>(r, CodigosDeSeccion.Especialidades).Especialidades.ShouldBe(["PE20"]);
        Datos<DatosSst>(r, CodigosDeSeccion.Sst).PoliticaHseq.ShouldBe(true);
    }

    [Fact]
    public void El_dv_se_recalcula_aunque_el_portal_traiga_otro_y_se_anota()
    {
        var r = Traducir(Juridico());
        r.Tercero!.Identificacion.DigitoVerificacion.ShouldBe("8");
        var aviso = r.Advertencias.Where(a => a.Codigo == "dv-recalculado").ShouldHaveSingleItem();
        (aviso.Seccion, aviso.Dato).ShouldBe((CodigosDeSeccion.General, "0"));

        // El DV pegado al número con guion también se recalcula; si coincide, no se anota nada.
        var bien = Juridico() with { InformacionGeneral = Juridico().InformacionGeneral! with { TigNumeroIdentificacion = "900.123.456-8", TerDigitoVerificacion = null } };
        Traducir(bien).Advertencias.ShouldNotContain(a => a.Codigo == "dv-recalculado");
        var mal = Juridico() with { InformacionGeneral = Juridico().InformacionGeneral! with { TigNumeroIdentificacion = "900123456-3", TerDigitoVerificacion = null } };
        Traducir(mal).Advertencias.ShouldContain(a => a.Codigo == "dv-recalculado" && a.Dato == "3");
    }

    // ---------- tributaria: las calidades ⇒ códigos DIAN ----------

    /// <summary>Casos: calidades del Portal ⇒ códigos de la casilla 53 esperados.</summary>
    public static TheoryData<TerInfoTributaria, string[]> CasosDeCalidades() => new()
    {
        { new TerInfoTributaria(), ["49"] },
        { new TerInfoTributaria(ResponsableIVA: true), ["48"] },
        { new TerInfoTributaria(Autorretenedor: true), ["15", "49"] },
        { new TerInfoTributaria(Declarante: true), ["05", "49"] },
        { new TerInfoTributaria(Declarante: true, RegimenEspecial: true), ["04", "49"] },
        { new TerInfoTributaria(RegimenEspecial: true), ["04", "49"] },
        { new TerInfoTributaria(GranContribuyente: true), ["13", "49"] },
        { new TerInfoTributaria(RegimenSimple: true), ["47", "49"] },
        { new TerInfoTributaria(AgenteRetenedorIVA: true), ["09", "49"] },
        // NoObligFacturar guarda «obligado a facturar» (semántica invertida del legado) ⇒ 52.
        { new TerInfoTributaria(NoObligFacturar: true), ["49", "52"] },
        { new TerInfoTributaria(GranContribuyenteBta: true, AutoretenedorRFT: true, DescuentoSENAFIC: true, ProfesionalIndependiente: true,
            ExentoRetencionFuente: true, AutoRetenedorICA: true), ["49"] }
    };

    [Theory]
    [MemberData(nameof(CasosDeCalidades))]
    public void Las_calidades_del_portal_dan_los_codigos_dian(TerInfoTributaria calidades, string[] codigos) =>
        CalidadesDelPortal.Traducir(calidades).Responsabilidades.ShouldBe(codigos);

    [Theory]
    [MemberData(nameof(CasosDeCalidades))]
    public void Derivar_de_vuelta_los_codigos_da_las_calidades_del_portal(TerInfoTributaria t, string[] _)
    {
        var traduccion = CalidadesDelPortal.Traducir(t);
        var d = CalidadesTributarias.Derivar(traduccion.Responsabilidades, traduccion.AutorretenedorIca);
        (d.ResponsableIva, d.Autorretenedor, d.GranContribuyente, d.RegimenSimple, d.RegimenEspecial, d.AgenteRetenedorIva, d.ObligadoAFacturar,
                d.AutorretenedorIca)
            .ShouldBe((t.ResponsableIVA, t.Autorretenedor, t.GranContribuyente, t.RegimenSimple, t.RegimenEspecial, t.AgenteRetenedorIVA, t.NoObligFacturar,
                t.AutoRetenedorICA));
        d.DeclaranteRenta.ShouldBe(t.Declarante || t.RegimenEspecial);
    }

    [Fact]
    public void Lo_que_no_tiene_codigo_dian_se_anota_y_las_incoherencias_tambien()
    {
        var notas = CalidadesDelPortal.Traducir(new TerInfoTributaria(GranContribuyenteBta: true, ExentoRetencionFuente: true, RegimenEspecial: true)).Notas;
        notas.Select(n => n.Campo).ShouldBe(["GranContribuyenteBta", "ExentoRetencionFuente", "Declarante"]);
        CalidadesDelPortal.Tabla.Count.ShouldBe(14);

        var r = Traducir(Juridico() with { InfoTributaria = new TerInfoTributaria(ResponsableIVA: true, GranContribuyenteBta: true) });
        r.Advertencias.ShouldContain(a => a.Seccion == CodigosDeSeccion.Tributaria && a.Codigo == "calidad-sin-codigo" && a.Dato == "GranContribuyenteBta");
    }

    [Fact]
    public void La_tributaria_lleva_resoluciones_fechas_e_ica_y_sin_municipios_deja_ica_por_diligenciar()
    {
        var t = Datos<DatosTributarios>(Traducir(Juridico()), CodigosDeSeccion.Tributaria);
        t.Responsabilidades.ShouldBe(["05", "13", "48", "52"]);
        t.ResolucionGranContribuyente.ShouldBe(new Resolucion { Numero = "9061", Fecha = new DateOnly(2020, 12, 10) });
        (t.ResponsableIca, t.AutorretenedorIca).ShouldBe((true, true));
        t.Ica.ShouldBe([new FilaIca { Municipio = "11001", Ciiu = "4663" }]);

        var sinIca = Traducir(Juridico() with { RetencionesIca = [] });
        Datos<DatosTributarios>(sinIca, CodigosDeSeccion.Tributaria).ResponsableIca.ShouldBeNull();
        sinIca.Advertencias.ShouldContain(a => a.Codigo == "ica-sin-municipios");
        Regla(sinIca, CodigosDeSeccion.Tributaria).Completitud.Faltantes.ShouldContain(f => f.Campo == "responsable_ica");
    }

    // ---------- fechas, lugares y porcentajes de texto ----------

    [Theory]
    [InlineData("2020-05-17", "2020-05-17")]
    [InlineData("17/05/2020", "2020-05-17")]
    [InlineData("7/5/2020", "2020-05-07")]
    [InlineData("17-05-2020", "2020-05-17")]
    [InlineData("2020-05-17T00:00:00", "2020-05-17")]
    [InlineData("2020/05/17", "2020-05-17")]
    [InlineData("31/02/2020", null)]
    [InlineData("mayo de 2020", null)]
    [InlineData("", null)]
    public void Las_fechas_de_texto_se_tipan_o_quedan_nulas(string texto, string? esperada) =>
        CodigosDelPortal.Fecha(texto).ShouldBe(esperada is null ? null : DateOnly.Parse(esperada, System.Globalization.CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("50", "50", false)]
    [InlineData("50 %", "50", false)]
    [InlineData("33,33", "33.33", false)]
    [InlineData("33.33", "33.33", false)]
    [InlineData("33,333333", "33.3333", true)]
    [InlineData("0", "0", false)]
    [InlineData("100", "100", false)]
    [InlineData("101", null, false)]
    [InlineData("-5", null, false)]
    [InlineData("1.234,5", null, false)]
    [InlineData("la mitad", null, false)]
    public void Los_porcentajes_de_texto_se_tipan_o_quedan_nulos(string texto, string? esperado, bool redondea)
    {
        CodigosDelPortal.Porcentaje(texto, out var redondeado)
            .ShouldBe(esperado is null ? null : decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture));
        redondeado.ShouldBe(redondea);
    }

    [Fact]
    public void En_la_legal_lo_que_no_se_puede_convertir_queda_fuera_como_rechazo_sin_inventar()
    {
        var malo = Juridico() with
        {
            CamaraComercio =
            [
                new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", null, null, false, false, 1, "ayer", null, "2030-01-01", null, null, "Narnia",
                    "Calle 1", "colombiana"),
                new TerCamaraComercio(2, 2, "NIT", "860034313", "Inversiones S.A.", null, null, "cuarenta", false, false)
            ]
        };
        var r = Traducir(malo);

        r.Rechazos.Select(x => (x.Campo, x.Codigo)).ShouldBe(
        [
            ("personas[0].fecha_expedicion", "fecha-invalida"), ("personas[0].residencia", "pais-no-reconocido"), ("personas[1].porcentaje", "porcentaje-invalido"),
            ("personas[0].fecha_nacimiento", "valor-invalido")
        ], ignoreOrder: true);
        var personas = Datos<DatosLegales>(r, CodigosDeSeccion.Legal).Personas!;
        (personas[0].FechaExpedicion, personas[0].FechaNacimiento, personas[0].Residencia, personas[0].Nacionalidad).ShouldBe(
            ((DateOnly?)null, (DateOnly?)null, (string?)null, "CO"));
        personas[1].Porcentaje.ShouldBeNull();
        Regla(r, CodigosDeSeccion.Legal).Completitud.Faltantes.ShouldContain(f => f.Campo == "personas[1].porcentaje");
        r.Advertencias.ShouldContain(a => a.Codigo == "lugar-sin-campo" && a.Dato == "Calle 1");
        r.Rechazos.ShouldAllBe(x => x.Seccion == CodigosDeSeccion.Legal && x.AccionSugerida != null);
    }

    // ---------- ciudades, CIIU y catálogos ----------

    [Fact]
    public void Una_ciudad_sin_dane_se_rechaza_y_la_general_queda_sin_ciudad()
    {
        var sinCodigo = Traducir(Juridico() with { InformacionGeneral = Juridico().InformacionGeneral! with { TigCiudad = 3 } });
        sinCodigo.Rechazos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            x => x.Codigo.ShouldBe("ciudad-sin-dane"), x => x.Campo.ShouldBe("ciudad"), x => x.Seccion.ShouldBe(CodigosDeSeccion.General));
        Datos<DatosGenerales>(sinCodigo, CodigosDeSeccion.General).Ciudad.ShouldBeNull();
        Regla(sinCodigo, CodigosDeSeccion.General).Completitud.Faltantes.ShouldContain(f => f.Campo == "ciudad");

        // Con DANE bien formado pero fuera del catálogo de plataforma: la regla única lo quita (codigo-invalido).
        var fuera = Traducir(Juridico() with { InformacionGeneral = Juridico().InformacionGeneral! with { TigCiudad = 4 } });
        fuera.Rechazos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(x => x.Codigo.ShouldBe("codigo-invalido"), x => x.Dato.ShouldBe("98001"));
        Datos<DatosGenerales>(fuera, CodigosDeSeccion.General).Ciudad.ShouldBeNull();

        // Un CIIU ilegible y una fila de ICA sin DANE.
        var r = Traducir(Juridico() with
        {
            InformacionGeneral = Juridico().InformacionGeneral! with { TigActEconomicaPri = 12 }, RetencionesIca = [new TerRetencionICA(9, 3, 10)]
        });
        r.Rechazos.Select(x => (x.Campo, x.Codigo)).ShouldBe([("ciiu_principal", "ciiu-invalido"), ("ica[0]", "ciudad-sin-dane")]);
    }

    [Fact]
    public void Los_catalogos_del_portal_se_convierten_a_codigos_de_plataforma()
    {
        var filas = CodigosDelPortal.Filas(Catalogos).ToDictionary(f => f.Catalogo);
        filas[CatalogoDePlataforma.Ciudades].Filas.Select(f => (f.Codigo, f.Departamento)).ShouldBe([("11001", "Bogotá, D.C."), ("05001", "Antioquia")]);
        filas[CatalogoDePlataforma.Ciudades].Rechazos.Count.ShouldBe(2); // sin código y departamento 98 (no existe en la DIVIPOLA)
        filas[CatalogoDePlataforma.ActividadesEconomicas].Filas.Select(f => f.Codigo).ShouldBe(["4663", "4290"]);
        filas[CatalogoDePlataforma.Bancos].Filas.Select(f => (f.Codigo, f.Activo)).ShouldBe([("1007", (bool?)true), ("0051", true)]);
        filas[CatalogoDePlataforma.Bancos].Rechazos.ShouldHaveSingleItem();
        filas[CatalogoDePlataforma.Especialidades].Filas.Select(f => (f.Codigo, f.Padre, f.Nivel)).ShouldBe(
            [("PG1", (string?)null, (int?)1), ("PC5", "PG1", 2), ("PE20", "PC5", 3), ("PE21", "PC5", 3)]);
    }

    [Theory]
    [InlineData("Colombia", "CO")]
    [InlineData("COLOMBIA", "CO")]
    [InlineData("colombiana", "CO")]
    [InlineData("US", "US")]
    [InlineData("Estados Unidos", "US")]
    [InlineData("Narnia", null)]
    [InlineData("", null)]
    public void Los_paises_de_texto_van_a_iso(string texto, string? iso) => CodigosDelPortal.Pais(texto, Catalogos.Paises).ShouldBe(iso);

    // ---------- personas y financiera ----------

    [Fact]
    public void Las_personas_se_deduplican_por_rol_e_identificacion()
    {
        var r = Traducir(Juridico() with
        {
            CamaraComercio =
            [
                new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", null, null, false, false, 1),
                new TerCamaraComercio(2, 1, "CC", "79.123.456", "Juan", "Pérez", null, null, false, false, 1),
                new TerCamaraComercio(3, 2, "CC", "79123456", "Juan", "Pérez", null, "100", false, false),
                new TerCamaraComercio(4, 1, "CC", "52111222", "Ana", "Ruiz", null, null, false, false, 2)
            ]
        });

        var personas = Datos<DatosLegales>(r, CodigosDeSeccion.Legal).Personas!;
        personas.Select(p => (p.Rol, p.Numero, p.TipoRepresentacion)).ShouldBe(
        [
            (RolPersonaRelacionada.RepresentanteLegal, "79123456", "Principal"), (RolPersonaRelacionada.Socio, "79123456", null),
            (RolPersonaRelacionada.Suplente, "52111222", "Suplente")
        ]);
        r.Advertencias.Where(a => a.Codigo == "persona-duplicada").ShouldHaveSingleItem().Dato.ShouldBe("TerCamId 2");
        RepresentanteLegalPrincipal.Elegir(TipoPersona.Juridica, null, Datos<DatosLegales>(r, CodigosDeSeccion.Legal))!.Persona.Numero.ShouldBe("79123456");
    }

    [Fact]
    public void La_firma_del_revisor_fiscal_y_el_socio_con_nit_y_dv_recalculado()
    {
        var r = Traducir(Juridico() with
        {
            CamaraComercio =
            [
                new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", null, null, false, false, 1),
                new TerCamaraComercio(2, 2, "NIT", "860034313-1", "Inversiones S.A.", null, null, "100", false, false),
                new TerCamaraComercio(3, 3, "CC", "80111222", "Luz", "Gómez", null, null, false, false, 4, TerRazonSocialNombre: "Auditores Ltda.",
                    TerRazonSocialDoc: "830077655-6")
            ]
        });

        var legal = Datos<DatosLegales>(r, CodigosDeSeccion.Legal);
        legal.RevisorFiscalFirma.ShouldBe(new FirmaRevisora { Nit = "830077655", RazonSocial = "Auditores Ltda." });
        var socio = legal.Personas!.Single(p => p.Rol == RolPersonaRelacionada.Socio);
        (socio.Numero, socio.Dv, socio.Nombres, socio.Apellidos).ShouldBe(("860034313", "7", "Inversiones S.A.", (string?)null));
        r.Advertencias.ShouldContain(a => a.Campo == "personas[1].dv" && a.Codigo == "dv-recalculado");
        Regla(r, CodigosDeSeccion.Legal).Completitud.Completa.ShouldBeTrue();
    }

    [Fact]
    public void La_financiera_da_una_version_por_anio_sin_filas_en_cero_ni_anios_repetidos()
    {
        var r = Traducir(Juridico() with
        {
            InfoFinanciera =
            [
                new TerInfoFinanciera(1, 2023, 1_000_000, 600_000, 400_000, 900_000, 800_000, 1, 1, 1),
                new TerInfoFinanciera(2, 2024),
                new TerInfoFinanciera(3, 2025, 500, 300, 200, 100, 90, 1, 1, 1),
                new TerInfoFinanciera(4, 2025, 600, 300, 300, 100, 90, 1, 1, 1),
                new TerInfoFinanciera(5, 1980, 1, 1, 0, 1, 1, 1, 1, 1)
            ]
        });

        r.Secciones.Where(s => s.Definicion.Codigo == CodigosDeSeccion.Financiera).Select(s => s.Periodo).ShouldBe([2023, 2025]);
        Datos<DatosFinancieros>(r, CodigosDeSeccion.Financiera, 2025).Activos.ShouldBe(600);
        Datos<DatosFinancieros>(r, CodigosDeSeccion.Financiera, 2023).FechaCorte.ShouldBe(new DateOnly(2023, 12, 31));
        var moneda = Datos<DatosFinancieros>(r, CodigosDeSeccion.Financiera, 2023).MonedaExtranjera!;
        moneda.Realiza.ShouldBe(true);
        moneda.Tipos.ShouldBe([TipoOperacionExtranjera.Importaciones]);
        moneda.Paises.ShouldBe(["US"]);
        r.Advertencias.Select(a => a.Codigo).ShouldContain("fila-en-ceros");
        r.Advertencias.Select(a => a.Codigo).ShouldContain("periodo-duplicado");
        r.Rechazos.ShouldHaveSingleItem().ShouldSatisfyAllConditions(x => x.Codigo.ShouldBe("periodo-invalido"), x => x.Periodo.ShouldBe(1980));
    }

    // ---------- persona natural y terceros rechazados ----------

    [Fact]
    public void Un_natural_no_lleva_legal_ni_financiera_y_separa_nombres_y_apellidos()
    {
        var natural = new TerceroDelPortal(new TercerosPortal(202, 3),
            new TerInformacionGeneral(2, "María", "López Díaz", "N", "CC", "52.987.654", null, "maria@correo.co", 2, "Cra 7 # 1-2", 11, "3001234567",
                null, false, null, "Colombia"),
            InfoTributaria: new TerInfoTributaria(),
            InfoFinanciera: [new TerInfoFinanciera(1, 2025, 10, 5, 5, 1, 1, 1, 1, 1)],
            CamaraComercio: [new TerCamaraComercio(1, 1, "CC", "52987654", "María", "López")],
            Siso: new TerSISO());

        var r = Traducir(natural);
        (r.Tercero!.Identificacion.Clave, r.Tercero.TipoPersona, r.Tercero.Nombre).ShouldBe(("CO|CC|52987654", TipoPersona.Natural, "María López Díaz"));
        r.Secciones.Select(s => s.Definicion.Codigo).ShouldBe([CodigosDeSeccion.General, CodigosDeSeccion.Tributaria, CodigosDeSeccion.Sst]);
        r.Advertencias.Count(a => a.Codigo == "no-aplica-a-persona-natural").ShouldBe(2);
        var general = Datos<DatosGenerales>(r, CodigosDeSeccion.General);
        (general.Nombres, general.Apellidos, general.RazonSocial, general.Ciudad, general.Dv).ShouldBe(("María", "López Díaz", (string?)null, "05001", (string?)null));
        Regla(r, CodigosDeSeccion.General).Completitud.Completa.ShouldBeTrue();
        Datos<DatosSst>(r, CodigosDeSeccion.Sst).ProgramaSaludOcupacional.ShouldBe(false); // un «no» del Portal es una respuesta
    }

    public static TheoryData<TerceroDelPortal, string> TercerosRechazados() => new()
    {
        { new TerceroDelPortal(new TercerosPortal(1)), "sin-informacion-general" },
        { new TerceroDelPortal(new TercerosPortal(2), new TerInformacionGeneral(TigNombre: "X", TigTipoPersona: "Z", TigTipoDocumento: "NIT", TigNumeroIdentificacion: "900123456")), "tipo-persona-invalido" },
        { new TerceroDelPortal(new TercerosPortal(3), new TerInformacionGeneral(TigNombre: "X", TigTipoPersona: "J", TigTipoDocumento: "RC", TigNumeroIdentificacion: "1")), "tipo-identificacion-invalido" },
        { new TerceroDelPortal(new TercerosPortal(4), new TerInformacionGeneral(TigNombre: "X", TigTipoPersona: "J", TigTipoDocumento: "NIT", TigNumeroIdentificacion: "12")), "numero-identificacion-invalido" },
        { new TerceroDelPortal(new TercerosPortal(5), new TerInformacionGeneral(TigNombre: "X", TigTipoPersona: "J", TigTipoDocumento: "CC", TigNumeroIdentificacion: "79123456")), "tipo-identificacion-no-aplica" },
        { new TerceroDelPortal(new TercerosPortal(6), new TerInformacionGeneral(TigNombre: " ", TigTipoPersona: "J", TigTipoDocumento: "NIT", TigNumeroIdentificacion: "900123456")), "nombre-requerido" },
        { new TerceroDelPortal(new TercerosPortal(7), new TerInformacionGeneral(TigNombre: "X", TigTipoPersona: "J", TigTipoDocumento: "EXT", TigNumeroIdentificacion: "ABC123", TigPais: "Colombia")), "pais-de-identificacion-invalido" }
    };

    [Theory]
    [MemberData(nameof(TercerosRechazados))]
    public void Sin_identidad_valida_el_tercero_entero_se_rechaza(TerceroDelPortal tercero, string codigo)
    {
        var r = Traducir(tercero);
        r.Tercero.ShouldBeNull();
        r.Secciones.ShouldBeEmpty();
        r.Rechazos.ShouldHaveSingleItem().Codigo.ShouldBe(codigo);
    }

    [Fact]
    public void Si_validasoft_ya_lo_tiene_con_otro_tipo_de_persona_manda_el_registro()
    {
        TraductorDelPortal.Traducir(Juridico(), Catalogos, Plataforma(), Hoy, TipoPersona.Consorcio).Tercero!.TipoPersona.ShouldBe(TipoPersona.Consorcio);
        var natural = TraductorDelPortal.Traducir(Juridico(), Catalogos, Plataforma(), Hoy, TipoPersona.Natural);
        natural.Tercero.ShouldBeNull();
        natural.Rechazos.ShouldHaveSingleItem().Codigo.ShouldBe("tipo-persona-distinto");
    }

    [Fact]
    public void Traducir_dos_veces_da_la_misma_huella()
    {
        var a = Traducir(Juridico());
        var b = Traducir(Juridico());
        a.Secciones.Select(s => JsonDeFicha.Huella(s.Definicion.Esquema.Canonico(s.Datos)))
            .ShouldBe(b.Secciones.Select(s => JsonDeFicha.Huella(s.Definicion.Esquema.Canonico(s.Datos))));
    }

    [Fact]
    public void Las_versiones_de_un_sistema_de_origen_no_exigen_el_soporte_del_cambio_y_las_del_tercero_si()
    {
        var r = Traducir(Juridico());
        var general = r.Secciones.Single(s => s.Definicion.Codigo == CodigosDeSeccion.General);
        var contexto = new ContextoDeSeccion(r.Tercero!.TipoPersona, r.Tercero.Identificacion, null, Hoy);
        var cambiada = ((DatosGenerales)general.Datos) with { Telefono = "6017778899" };
        var terceroId = Guid.NewGuid();

        foreach (var (origen, completa) in new[] { (OrigenDeSeccion.Importada, true), (OrigenDeSeccion.SistemaExterno, true), (OrigenDeSeccion.Tercero, false) })
        {
            var seccion = SeccionDeFicha.Create(SeccionDeFicha.Iniciar(terceroId, general.Definicion, null, Ahora));
            seccion = seccion.Proyectar(seccion.Registrar(general.Definicion, general.Datos, [], contexto, Plataforma(), OrigenDeSeccion.Importada, "importador:portal", Ahora));
            var v2 = (VersionDeSeccionRegistrada)seccion.Registrar(general.Definicion, cambiada, [], contexto, Plataforma(), origen, "x", Ahora).Single();
            (v2.Numero, v2.Completitud.Completa).ShouldBe((2, completa), origen.ToString());
            seccion.Proyectar([v2]).Recalcular(general.Definicion, contexto, Plataforma(), Ahora).ShouldBeNull(); // el recálculo dice lo mismo
        }
    }

    // ---------- el adaptador como origen genérico ----------

    [Fact]
    public async Task El_adaptador_lee_un_archivo_y_entrega_la_clave_las_unidades_aceptadas_y_el_traductor()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new ArchivoDelPortal(Catalogos, [Juridico(101), Juridico(102) with { Constructoras = [new TercerosConstructora(7, 2), new TercerosConstructora(9, 4)] }]),
            OrigenPortalArchivo.Opciones);
        var adaptador = new AdaptadorPortal(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        adaptador.ValidarArchivo(json);
        ((IAdaptadorDeOrigen)adaptador).Codigo.ShouldBe("portal");
        adaptador.ConectadoConfigurado.ShouldBeFalse();

        var origen = adaptador.DesdeArchivo(json);
        var lote = await origen.TercerosAsync(null, 10, CancellationToken.None);
        lote.Select(t => t.Clave).ShouldBe(["101", "102"]);
        lote[0].UnidadesAceptadas.ShouldBe(["7"]); // Completada (3); la 102 solo cuenta la de estado 4 (la 7, en estado 2, no)
        lote[1].UnidadesAceptadas.ShouldBe(["9"]);
        (await origen.TercerosAsync("101", 10, CancellationToken.None)).Select(t => t.Clave).ShouldBe(["102"]);
        (await origen.TerceroAsync("101", CancellationToken.None))!.Clave.ShouldBe("101");
        (await origen.TerceroAsync("999", CancellationToken.None)).ShouldBeNull();
        (await origen.TerceroAsync("no-numerico", CancellationToken.None)).ShouldBeNull();

        var traduccion = lote[0].Traducir(Plataforma(), Hoy, null);
        (traduccion.Sistema, traduccion.Clave, traduccion.ClaveLegado).ShouldBe(("portal", "101", "portal:101"));
        traduccion.Tercero!.Identificacion.Numero.ShouldBe("900123456");
    }

    [Fact]
    public void Sin_cadena_de_conexion_el_origen_conectado_responde_409_y_con_archivo_configurado_se_usa_el_archivo()
    {
        var adaptador = new AdaptadorPortal(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        Should.Throw<Validasoft.Dominio.Comun.ReglaDeNegocioException>(() => adaptador.Conectado()).Codigo.ShouldBe("origen-no-configurado");
        Should.Throw<Validasoft.Dominio.Comun.ReglaDeNegocioException>(() => adaptador.Configurado()).Codigo.ShouldBe("origen-no-configurado");
        Should.Throw<Validasoft.Dominio.Comun.ReglaDeNegocioException>(() => adaptador.ValidarArchivo("{ no es json")).Codigo.ShouldBe("archivo-invalido");
    }
}
