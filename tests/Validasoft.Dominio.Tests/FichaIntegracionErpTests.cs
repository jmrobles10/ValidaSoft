using System.Text.Json;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Integración de la ficha con el SRM (F3, FM-32…): las calidades tributarias del Portal derivadas de los códigos DIAN (tabla de
/// casos), el representante legal que recibe AyF y el contenido aceptado normalizado para el ERP.
/// </summary>
public class FichaIntegracionErpTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);

    internal static readonly ICatalogosDeFicha Catalogos = CatalogosDeSemilla();

    internal static ICatalogosDeFicha CatalogosDeSemilla()
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

        return new CatalogosEnMemoria(entradas);
    }

    // ---------- las calidades del Portal (FM-33) ----------

    /// <summary>
    /// Casos: códigos de la casilla 53 ⇒ la calidad que se espera en <c>true</c> (todas las demás derivables de un código, en
    /// <c>false</c>). <c>null</c> = ninguna calidad en sí.
    /// </summary>
    public static TheoryData<string[], string[]> Casos() => new()
    {
        { ["48"], ["responsableIva"] },
        { ["11"], ["responsableIva"] },
        { ["49"], [] },
        { ["53"], [] },
        { ["15"], ["autorretenedor"] },
        { ["13"], ["granContribuyente"] },
        { ["05"], ["declaranteRenta"] },
        { ["5"], ["declaranteRenta"] },
        { ["04"], ["declaranteRenta", "regimenEspecial"] },
        { ["47"], ["regimenSimple"] },
        { ["52"], ["obligadoAFacturar"] },
        { ["16"], ["obligadoAFacturar"] },
        { ["09"], ["agenteRetenedorIva"] },
        { ["23"], ["agenteRetenedorIva"] },
        { ["07"], [] }, // retención a título de renta: NO es agente retenedor de IVA
        { ["10"], [] }, // usuario aduanero
        { ["45"], [] }, // autorretenedor de rendimientos financieros: no es el autorretenedor (15) del Portal
        { ["05", "07", "13", "15", "48", "52"], ["declaranteRenta", "granContribuyente", "autorretenedor", "responsableIva", "obligadoAFacturar"] },
        { [], [] }
    };

    [Theory]
    [MemberData(nameof(Casos))]
    public void Cada_calidad_derivable_es_si_con_alguno_de_sus_codigos_y_no_sin_ellos(string[] codigos, string[] esperadas)
    {
        var calidades = Valores(CalidadesTributarias.Derivar(codigos, null));

        foreach (var regla in CalidadesTributarias.Tabla.Where(r => r.Origen == OrigenDeCalidad.CodigoDian))
            calidades[regla.Calidad].ShouldBe(esperadas.Contains(regla.Calidad), regla.Calidad);
        foreach (var regla in CalidadesTributarias.NoDerivables)
            calidades[regla.Calidad].ShouldBeNull(regla.Calidad);
        calidades["autorretenedorIca"].ShouldBeNull();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_autorretenedor_de_ica_sale_del_dato_de_la_seccion_porque_ica_no_esta_en_la_casilla_53(bool autorretenedorIca)
    {
        CalidadesTributarias.Derivar(new DatosTributarios { Responsabilidades = ["48"], AutorretenedorIca = autorretenedorIca })
            .AutorretenedorIca.ShouldBe(autorretenedorIca);
    }

    [Fact]
    public void La_tabla_cubre_las_13_calidades_de_ayf_mas_la_rft_del_portal_y_cada_una_tiene_su_fuente_o_nota()
    {
        var tabla = CalidadesTributarias.Tabla;
        tabla.Count.ShouldBe(14);
        tabla.Select(r => r.Calidad).ShouldBeUnique();
        tabla.Where(r => r.CampoAyf is not null).Select(r => r.CampoAyf).ShouldBe(
        [
            "RegimenIva", "AutoRetenedor", "DeclaranteRenta", "GranContribuyente", "AutoRetenedorICA", "RegimenSimple", "GranContribuyenteBta",
            "RegimenEspecial", "DescuentoSenaFic", "ProfesionalIndependiente", "ExentoRetefuente", "AgenteRetenedorIVA", "NoObligadoFacturar"
        ]);
        tabla.Single(r => r.CampoAyf is null).Calidad.ShouldBe("autorretenedorRft");
        tabla.ShouldAllBe(r => r.Nota.Length > 0);
        tabla.Where(r => r.Origen == OrigenDeCalidad.CodigoDian).ShouldAllBe(r => r.Codigos.Count > 0);
        CalidadesTributarias.NoDerivables.Select(r => r.Calidad).ShouldBe(
            ["granContribuyenteBogota", "autorretenedorRft", "descuentoSenaFic", "profesionalIndependiente", "exentoRetencion"]);
        // Las propiedades del record son las de la tabla, en el mismo orden.
        Valores(CalidadesTributarias.Derivar([], null)).Keys.ShouldBe(tabla.Select(r => r.Calidad));
        // Todo código de la tabla existe en el catálogo de responsabilidades (menos el 37, histórico).
        tabla.SelectMany(r => r.Codigos).Where(c => c != "37")
            .ShouldAllBe(c => Catalogos.Buscar(CatalogoDePlataforma.ResponsabilidadesDian, c) != null);
    }

    private static Dictionary<string, bool?> Valores(CalidadesTributarias c) =>
        typeof(CalidadesTributarias).GetProperties()
            .Where(p => p.PropertyType == typeof(bool?))
            .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), p => (bool?)p.GetValue(c));

    // ---------- el representante legal que recibe AyF (FM-34) ----------

    private static PersonaRelacionada Persona(RolPersonaRelacionada rol, string numero, string? representacion = null) => new()
    {
        Rol = rol, TipoRepresentacion = representacion, TipoIdentificacion = "CC", Numero = numero, Nombres = $"Nombre {numero}", Apellidos = "Pérez",
        Correo = $"{numero}@aceros.co"
    };

    [Fact]
    public void En_juridica_es_el_principal_de_menor_orden_aunque_haya_otro_representante_antes()
    {
        var legal = new DatosLegales
        {
            Personas =
            [
                Persona(RolPersonaRelacionada.Socio, "1"),
                Persona(RolPersonaRelacionada.RepresentanteLegal, "2", "Primer suplente"),
                Persona(RolPersonaRelacionada.Suplente, "3", "Principal"),
                Persona(RolPersonaRelacionada.RepresentanteLegal, "4", "PRINCIPÁL"),
                Persona(RolPersonaRelacionada.RepresentanteLegal, "5", "Principal")
            ]
        };

        var elegido = RepresentanteLegalPrincipal.Elegir(TipoPersona.Juridica, null, legal).ShouldNotBeNull();
        (elegido.Origen, elegido.Indice, elegido.Persona.Numero, elegido.Nombre).ShouldBe((OrigenDelRepresentante.Legal, (int?)3, "4", "Nombre 4 Pérez"));
    }

    [Fact]
    public void En_juridica_sin_principal_es_el_primer_representante_y_sin_representantes_no_hay()
    {
        var sinPrincipal = new DatosLegales
        {
            Personas = [Persona(RolPersonaRelacionada.Socio, "1"), Persona(RolPersonaRelacionada.RepresentanteLegal, "2"),
                Persona(RolPersonaRelacionada.RepresentanteLegal, "3", "Suplente")]
        };
        RepresentanteLegalPrincipal.Elegir(TipoPersona.Consorcio, null, sinPrincipal)!.Persona.Numero.ShouldBe("2");
        RepresentanteLegalPrincipal.Elegir(TipoPersona.Juridica, null, new DatosLegales { Personas = [Persona(RolPersonaRelacionada.Socio, "1")] })
            .ShouldBeNull();
        RepresentanteLegalPrincipal.Elegir(TipoPersona.Juridica, null, null).ShouldBeNull();
    }

    [Fact]
    public void En_persona_natural_es_el_propio_tercero_de_la_informacion_general()
    {
        var general = new DatosGenerales
        {
            TipoPersona = TipoPersona.Natural, TipoIdentificacion = "CC", Numero = "79123456", Nombres = "Ana María", Apellidos = "Gómez Ruiz",
            Correo = "ana@correo.co", Telefono = "3001234567"
        };
        // Aunque llegara una legal, la persona natural no la diligencia (FM-17) y AyF recibe al propio tercero.
        var elegido = RepresentanteLegalPrincipal.Elegir(TipoPersona.Natural, general,
            new DatosLegales { Personas = [Persona(RolPersonaRelacionada.RepresentanteLegal, "9", "Principal")] }).ShouldNotBeNull();
        (elegido.Origen, elegido.Indice, elegido.Nombre, elegido.Persona.Numero, elegido.Persona.Correo, elegido.Persona.Rol).ShouldBe(
            (OrigenDelRepresentante.General, (int?)null, "Ana María Gómez Ruiz", "79123456", "ana@correo.co", RolPersonaRelacionada.RepresentanteLegal));
        RepresentanteLegalPrincipal.Elegir(TipoPersona.Natural, null, null).ShouldBeNull();
    }

    // ---------- el contenido aceptado, normalizado (FM-32) ----------

    private static JsonElement Canonico<T>(string seccion, T datos) where T : notnull =>
        CatalogoDeSecciones.Requerida(seccion).Esquema.Canonico(datos);

    [Fact]
    public void La_general_lleva_identificacion_nombre_direccion_ciudad_dane_con_departamento_y_ciiu_con_nombre()
    {
        var datos = Canonico(CodigosDeSeccion.General, new DatosGenerales
        {
            RazonSocial = "Aceros del Norte S.A.S.", TipoPersona = TipoPersona.Juridica, TipoIdentificacion = "NIT", Numero = "900373913", Dv = "8",
            Correo = "compras@aceros.co", Telefono = "+57 601 555 1234", Pais = "CO", Ciudad = "11001", Direccion = "Calle 45 # 12-08",
            DireccionEstructurada = new DireccionEstructurada { TipoVia = "Calle", NumeroVia = "45", NumeroCruce = "12", Placa = "08" },
            CiiuPrincipal = "4663", CiiuSecundarias = ["4290"]
        });

        var contenido = ContenidoParaErp.Normalizar(CodigosDeSeccion.General, datos, Catalogos);

        new object?[] { contenido.Tributaria, contenido.Legal, contenido.Financiera, contenido.Sst, contenido.Especialidades }.ShouldAllBe(x => x == null);
        var g = contenido.General.ShouldNotBeNull();
        (g.TipoPersona, g.TipoIdentificacion, g.NumeroIdentificacion, g.Dv, g.Nombre).ShouldBe(("Juridica", "NIT", "900373913", "8", "Aceros del Norte S.A.S."));
        g.Ciudad.ShouldBe(new CiudadErpDto("11001", "Bogotá, D.C.", "11", "Bogotá, D.C.", "CO"));
        g.CiiuPrincipal!.Nombre!.ShouldStartWith("Comercio al por mayor de materiales de construcción");
        g.CiiuSecundarias.ShouldBe([new CodigoNombreDto("4290", "Construcción de otras obras de ingeniería civil")]);
        (g.Direccion, g.DireccionEstructurada!.TipoVia, g.DireccionEstructurada.Placa).ShouldBe(("Calle 45 # 12-08", "Calle", "08"));
        g.RepresentanteLegal.ShouldBeNull(); // en jurídica sale de la legal
    }

    [Fact]
    public void La_general_de_una_persona_natural_trae_al_tercero_como_representante()
    {
        var datos = Canonico(CodigosDeSeccion.General, new DatosGenerales
        {
            Nombres = "Ana María", Apellidos = "Gómez", TipoPersona = TipoPersona.Natural, TipoIdentificacion = "CC", Numero = "79123456",
            Correo = "ana@correo.co", Ciudad = "05001"
        });

        var g = ContenidoParaErp.Normalizar(CodigosDeSeccion.General, datos, Catalogos).General!;
        (g.Nombre, g.Ciudad!.CodigoDepartamento, g.Ciudad.Departamento).ShouldBe(("Ana María Gómez", "05", "Antioquia"));
        var representante = g.RepresentanteLegal.ShouldNotBeNull();
        (representante.Origen, representante.Nombre, representante.Numero, representante.Correo).ShouldBe(
            (CodigosDeSeccion.General, "Ana María Gómez", "79123456", "ana@correo.co"));
    }

    [Fact]
    public void La_tributaria_lleva_responsabilidades_con_nombre_las_calidades_derivadas_resoluciones_e_ica()
    {
        var datos = Canonico(CodigosDeSeccion.Tributaria, new DatosTributarios
        {
            Responsabilidades = ["05", "07", "13", "48", "52"],
            ResolucionGranContribuyente = new Resolucion { Numero = "012635", Fecha = new DateOnly(2022, 12, 14) },
            ResponsableIca = true, AutorretenedorIca = false,
            Ica = [new FilaIca { Municipio = "11001", Ciiu = "4663", TarifaPorMil = 11.04m }]
        });

        var t = ContenidoParaErp.Normalizar(CodigosDeSeccion.Tributaria, datos, Catalogos).Tributaria.ShouldNotBeNull();

        t.Responsabilidades.Select(r => r.Codigo).ShouldBe(["05", "07", "13", "48", "52"]);
        t.Responsabilidades.Single(r => r.Codigo == "07").Nombre.ShouldBe("Retención en la fuente a título de renta");
        t.Calidades.ShouldBe(new CalidadesTributariasDto(ResponsableIva: true, Autorretenedor: false, DeclaranteRenta: true, GranContribuyente: true,
            AutorretenedorIca: false, RegimenSimple: false, GranContribuyenteBogota: null, RegimenEspecial: false, AutorretenedorRft: null,
            DescuentoSenaFic: null, ProfesionalIndependiente: null, ExentoRetencion: null, AgenteRetenedorIva: false, ObligadoAFacturar: true));
        t.CalidadesSinDerivar.Select(c => c.Calidad).ShouldBe(
            ["granContribuyenteBogota", "autorretenedorRft", "descuentoSenaFic", "profesionalIndependiente", "exentoRetencion"]);
        t.CalidadesSinDerivar.ShouldAllBe(c => c.Nota.Length > 0);
        t.ResolucionGranContribuyente.ShouldBe(new ResolucionDto("012635", new DateOnly(2022, 12, 14)));
        t.ResolucionAutorretenedor.ShouldBeNull();
        (t.ResponsableIca, t.AutorretenedorIca).ShouldBe(((bool?)true, (bool?)false));
        var ica = t.Ica.ShouldHaveSingleItem();
        (ica.Municipio!.Nombre, ica.Ciiu!.Codigo, ica.TarifaPorMil).ShouldBe(("Bogotá, D.C.", "4663", (decimal?)11.04m));

        // Sin autorretenedor_ica diligenciado, esa calidad también va sin derivar.
        var sinIca = Canonico(CodigosDeSeccion.Tributaria, new DatosTributarios { Responsabilidades = ["49"] });
        ContenidoParaErp.Normalizar(CodigosDeSeccion.Tributaria, sinIca, Catalogos).Tributaria!.CalidadesSinDerivar.Select(c => c.Calidad)
            .ShouldContain("autorretenedorIca");
    }

    [Fact]
    public void La_legal_lleva_el_representante_principal_y_todas_las_personas()
    {
        var datos = Canonico(CodigosDeSeccion.Legal, new DatosLegales
        {
            Personas =
            [
                Persona(RolPersonaRelacionada.RepresentanteLegal, "11", "Suplente"),
                Persona(RolPersonaRelacionada.RepresentanteLegal, "12", "Principal") with { Cargo = "Gerente", FechaExpedicion = new DateOnly(2001, 5, 3) },
                Persona(RolPersonaRelacionada.Socio, "13") with { Porcentaje = 60m, Pep = false }
            ],
            RevisorFiscalFirma = new FirmaRevisora { Nit = "800123456", RazonSocial = "Auditores S.A.S." }
        });

        var l = ContenidoParaErp.Normalizar(CodigosDeSeccion.Legal, datos, Catalogos).Legal.ShouldNotBeNull();
        var representante = l.RepresentanteLegal.ShouldNotBeNull();
        (representante.Origen, representante.Indice, representante.Numero, representante.Cargo, representante.FechaExpedicion).ShouldBe(
            (CodigosDeSeccion.Legal, (int?)1, "12", "Gerente", (DateOnly?)new DateOnly(2001, 5, 3)));
        l.Personas.Select(p => (p.Rol, p.Numero)).ShouldBe([("RepresentanteLegal", "11"), ("RepresentanteLegal", "12"), ("Socio", "13")]);
        l.Personas[2].Porcentaje.ShouldBe(60m);
        l.RevisorFiscalFirma.ShouldBe(new FirmaRevisoraDto("800123456", "Auditores S.A.S."));
    }

    [Fact]
    public void Financiera_sst_y_especialidades_van_tal_cual_con_los_nombres_de_las_especialidades()
    {
        var financiera = ContenidoParaErp.Normalizar(CodigosDeSeccion.Financiera, Canonico(CodigosDeSeccion.Financiera, new DatosFinancieros
        {
            Periodo = 2025, Activos = 1000m, Pasivos = 400m, Patrimonio = 600m, IngresosBrutos = 0m,
            MonedaExtranjera = new MonedaExtranjera { Realiza = true, Tipos = [TipoOperacionExtranjera.Importaciones], Paises = ["US"] }
        }), Catalogos).Financiera.ShouldNotBeNull();
        (financiera.Periodo, financiera.Activos, financiera.IngresosBrutos, financiera.IngresosNetos).ShouldBe(((int?)2025, (decimal?)1000m, (decimal?)0m,
            (decimal?)null)); // vacío no es cero
        financiera.MonedaExtranjera.ShouldBe(new MonedaExtranjeraDto(true, ["Importaciones"], ["US"]), new MonedaIgual());

        var sst = ContenidoParaErp.Normalizar(CodigosDeSeccion.Sst, Canonico(CodigosDeSeccion.Sst, new DatosSst { ComiteParitario = true, PoliticaHseq = false }),
            Catalogos).Sst.ShouldNotBeNull();
        (sst.ComiteParitario, sst.PoliticaHseq, sst.LicenciasYPermisos).ShouldBe(((bool?)true, (bool?)false, (bool?)null));

        var especialidades = ContenidoParaErp.Normalizar(CodigosDeSeccion.Especialidades,
            Canonico(CodigosDeSeccion.Especialidades, new DatosEspecialidades { Especialidades = ["OG.CON.PRE"] }), Catalogos).Especialidades!;
        especialidades.Especialidades.ShouldBe([new CodigoNombreDto("OG.CON.PRE", "Concreto premezclado")]);
    }

    private sealed class MonedaIgual : IEqualityComparer<MonedaExtranjeraDto>
    {
        public bool Equals(MonedaExtranjeraDto? x, MonedaExtranjeraDto? y) =>
            x is not null && y is not null && x.Realiza == y.Realiza && x.Tipos.SequenceEqual(y.Tipos) && x.Paises.SequenceEqual(y.Paises);

        public int GetHashCode(MonedaExtranjeraDto obj) => 0;
    }
}
