using Validasoft.Aplicacion.Delegaciones;
using Validasoft.Dominio.Campanias;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Delegaciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>Delegación de revisión (OP-7, OP-8) y ciclo de vida de la campaña de renovación (OP-1…OP-3).</summary>
public sealed class OperacionDeLaRevisionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Hoy = new(2026, 11, 11);
    private static readonly Guid Empresa = Guid.NewGuid();
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();

    private static Area Area(string codigo, params Guid[] miembros)
    {
        var area = Configuracion.Area.Crear(Empresa, codigo, codigo);
        area.DefinirMiembros(miembros);
        return area;
    }

    private static DelegacionDeRevision Delegar(IEnumerable<Area> areas, bool todas, DateOnly desde, DateOnly hasta) =>
        DelegacionDeRevision.Crear(Empresa, Ana, Beto, areas, todas, desde, hasta, null, $"Usuario:{Ana}", Hoy, Ahora);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void La_delegacion_cubre_las_areas_delegadas_solo_entre_sus_fechas_y_mientras_quien_delega_siga_siendo_miembro()
    {
        var compras = Area("COMPRAS", Ana);
        var juridica = Area("JURIDICA", Ana);
        var d = Delegar([compras], false, Hoy.AddDays(2), Hoy.AddDays(5));

        d.EstadoEn(Hoy).ShouldBe(EstadoDeDelegacion.Programada);
        d.EstadoEn(Hoy.AddDays(2)).ShouldBe(EstadoDeDelegacion.Vigente);
        d.EstadoEn(Hoy.AddDays(5)).ShouldBe(EstadoDeDelegacion.Vigente);
        d.EstadoEn(Hoy.AddDays(6)).ShouldBe(EstadoDeDelegacion.Vencida);

        d.CubreA(compras, Beto, Hoy).ShouldBeFalse();
        d.CubreA(compras, Beto, Hoy.AddDays(3)).ShouldBeTrue();
        d.CubreA(compras, Guid.NewGuid(), Hoy.AddDays(3)).ShouldBeFalse(); // solo la persona delegada
        d.CubreA(juridica, Beto, Hoy.AddDays(3)).ShouldBeFalse(); // no es un área delegada
        compras.DefinirMiembros([Guid.NewGuid()]); // Ana ya no es miembro: no se delega lo que no se tiene
        d.CubreA(compras, Beto, Hoy.AddDays(3)).ShouldBeFalse();
    }

    [Fact]
    public void Con_todas_las_areas_la_delegacion_alcanza_las_que_quien_delega_tenga_en_cada_momento_y_no_se_encadena()
    {
        var d = Delegar([], true, Hoy, Hoy.AddDays(3));
        var nueva = Area("NUEVA", Ana);
        d.CubreA(nueva, Beto, Hoy).ShouldBeTrue();

        // Beto no delega a Carla lo que recibió de Ana: ante el área, solo cuenta quien es miembro.
        var carla = DelegacionDeRevision.Crear(Empresa, Beto, Guid.NewGuid(), [nueva], false, Hoy, Hoy.AddDays(1), null, "Usuario:beto", Hoy, Ahora);
        carla.CubreA(nueva, carla.AUsuarioId, Hoy).ShouldBeFalse();
    }

    [Fact]
    public void Las_notificaciones_del_area_llegan_tambien_a_quien_revisa_en_delegacion()
    {
        var compras = Area("COMPRAS", Ana);
        var d = Delegar([compras], false, Hoy, Hoy.AddDays(1));
        DelegacionesDeRevision.QuienesRevisan(compras, [d], Hoy).ShouldBe([Ana, Beto], ignoreOrder: true);
        DelegacionesDeRevision.QuienesRevisan(compras, [d], Hoy.AddDays(2)).ShouldBe([Ana]);
    }

    [Fact]
    public void Una_delegacion_se_valida_y_se_revoca_una_sola_vez()
    {
        var compras = Area("COMPRAS", Ana);
        Codigo(() => DelegacionDeRevision.Crear(Empresa, Ana, Ana, [compras], false, Hoy, Hoy, null, "x", Hoy, Ahora)).ShouldBe("delegacion-a-si-mismo");
        Codigo(() => Delegar([compras], false, Hoy.AddDays(2), Hoy)).ShouldBe("rango-invalido");
        Codigo(() => Delegar([compras], false, Hoy.AddDays(-5), Hoy.AddDays(-1))).ShouldBe("delegacion-vencida");
        Codigo(() => Delegar([compras], false, Hoy, Hoy.AddDays(400))).ShouldBe("rango-demasiado-largo");
        Codigo(() => DelegacionDeRevision.Crear(Empresa, Ana, Beto, [compras], false, Hoy, Hoy, new string('x', 301), "x", Hoy, Ahora)).ShouldBe("motivo-demasiado-largo");

        var d = Delegar([compras], false, Hoy, Hoy.AddDays(5));
        d.Revocar("Usuario:ana", " Vuelvo antes ", Hoy, Ahora);
        d.EstadoEn(Hoy).ShouldBe(EstadoDeDelegacion.Revocada);
        d.MotivoDeRevocacion.ShouldBe("Vuelvo antes");
        d.CubreA(compras, Beto, Hoy).ShouldBeFalse();
        Codigo(() => d.Revocar("Usuario:ana", null, Hoy, Ahora)).ShouldBe("delegacion-no-revocable");
    }

    [Fact]
    public void Dos_delegaciones_de_las_mismas_areas_a_la_misma_persona_no_se_pisan_en_las_fechas()
    {
        var compras = Area("COMPRAS", Ana);
        var juridica = Area("JURIDICA", Ana);
        var una = Delegar([compras], false, Hoy, Hoy.AddDays(5));
        una.SePisaCon(Delegar([compras], false, Hoy.AddDays(5), Hoy.AddDays(8)), Hoy).ShouldBeTrue();
        una.SePisaCon(Delegar([compras], false, Hoy.AddDays(6), Hoy.AddDays(8)), Hoy).ShouldBeFalse();
        una.SePisaCon(Delegar([juridica], false, Hoy, Hoy.AddDays(5)), Hoy).ShouldBeFalse();
        una.SePisaCon(Delegar([], true, Hoy, Hoy.AddDays(5)), Hoy).ShouldBeTrue();
    }

    // ---------- Campañas ----------

    private static CampaniaDeRenovacion NuevaCampania(int pendientes = 3, int omitidas = 1)
    {
        var creada = CampaniaDeRenovacion.Crear(Empresa, null, new FiltroDeCampania(30, null, []),
            new TotalesDeCampania(pendientes + omitidas, pendientes, 0, 0, omitidas), new ActorDeCampania(ActorTipo.Usuario, Ana.ToString()), Ahora);
        return CampaniaDeRenovacion.Create(creada);
    }

    private static void Aplicar(CampaniaDeRenovacion campania, IEnumerable<object> eventos)
    {
        foreach (var evento in eventos)
            switch (evento)
            {
                case AvanceDeCampaniaRegistrado e: campania.Apply(e); break;
                case CampaniaPausada e: campania.Apply(e); break;
                case CampaniaReanudada e: campania.Apply(e); break;
                case CampaniaCancelada e: campania.Apply(e); break;
                case FilasDeCampaniaReintentadas e: campania.Apply(e); break;
                case CampaniaTerminada e: campania.Apply(e); break;
            }
    }

    [Fact]
    public void La_campania_nace_procesando_con_el_filtro_validado_y_sin_vinculos_por_procesar_no_se_crea()
    {
        var campania = NuevaCampania();
        campania.Estado.ShouldBe(EstadoCampania.Procesando);
        campania.Nombre.ShouldBe("Renovación del 2026-11-11");
        campania.Filtro.Estados.ShouldBe(FiltroDeCampania.EstadosPorOmision);
        campania.Ejecucion.ShouldNotBeNull();

        Codigo(() => NuevaCampania(pendientes: 0)).ShouldBe("campania-sin-vinculos");
    }

    [Theory]
    [InlineData(-1, null, "dias-invalidos")]
    [InlineData(366, null, "dias-invalidos")]
    [InlineData(30, EstadoVinculo.Bloqueado, "estado-invalido")]
    [InlineData(30, EstadoVinculo.Invitado, "estado-invalido")]
    public void El_filtro_admite_de_0_a_365_dias_y_solo_vinculos_activos_por_vencer_o_vencidos(int dias, EstadoVinculo? estado, string codigo)
    {
        var filtro = new FiltroDeCampania(dias, null, estado is { } e ? [e] : []);
        Codigo(() => filtro.Validar()).ShouldBe(codigo);
        new FiltroDeCampania(0, null, [EstadoVinculo.Vencido]).Validar().Estados.ShouldBe([EstadoVinculo.Vencido]);
        new FiltroDeCampania(365, null, []).Validar().Estados.Count.ShouldBe(3);
    }

    [Fact]
    public void La_campania_se_pausa_se_reanuda_con_otra_ejecucion_y_las_tandas_viejas_dejan_de_valer()
    {
        var campania = NuevaCampania();
        var primera = campania.Ejecucion;
        Aplicar(campania, [campania.Pausar(Ahora)]);
        campania.Estado.ShouldBe(EstadoCampania.Pausada);
        campania.Ejecucion.ShouldBeNull();
        Codigo(() => campania.Pausar(Ahora)).ShouldBe("estado-invalido");

        Aplicar(campania, [campania.Reanudar(Ahora)]);
        campania.Estado.ShouldBe(EstadoCampania.Procesando);
        campania.Ejecucion.ShouldNotBe(primera);
        Codigo(() => campania.Reanudar(Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Termina_cuando_no_quedan_pendientes_con_o_sin_errores_y_las_fallidas_se_reintentan_solo_entonces()
    {
        var campania = NuevaCampania();
        Aplicar(campania, campania.RegistrarAvance(new TotalesDeCampania(4, 1, 2, 0, 1), Ahora));
        campania.Estado.ShouldBe(EstadoCampania.Procesando);
        Codigo(() => campania.ReintentarFallidas(1, Ahora)).ShouldBe("estado-invalido");

        Aplicar(campania, campania.RegistrarAvance(new TotalesDeCampania(4, 0, 2, 1, 1), Ahora));
        campania.Estado.ShouldBe(EstadoCampania.CompletadaConErrores);
        campania.TerminadaEn.ShouldNotBeNull();
        campania.RegistrarAvance(new TotalesDeCampania(4, 0, 2, 1, 1), Ahora).ShouldBeEmpty();
        Codigo(() => campania.Pausar(Ahora)).ShouldBe("campania-en-estado-terminal");
        Codigo(() => campania.ReintentarFallidas(0, Ahora)).ShouldBe("sin-filas-fallidas");

        Aplicar(campania, [campania.ReintentarFallidas(1, Ahora)]);
        campania.Estado.ShouldBe(EstadoCampania.Procesando);
        campania.Totales.ShouldBe(new TotalesDeCampania(4, 1, 2, 0, 1));
        campania.Reintentos.ShouldBe(1);
        campania.TerminadaEn.ShouldBeNull();
    }

    [Fact]
    public void Cancelar_es_terminal_y_una_campania_cancelada_no_reintenta()
    {
        var campania = NuevaCampania();
        Aplicar(campania, [campania.Cancelar(new TotalesDeCampania(4, 3, 0, 0, 1), Ahora)]);
        campania.Estado.ShouldBe(EstadoCampania.Cancelada);
        Codigo(() => campania.Cancelar(campania.Totales, Ahora)).ShouldBe("campania-en-estado-terminal");
        Codigo(() => campania.ReintentarFallidas(1, Ahora)).ShouldBe("campania-en-estado-terminal");
    }

    [Fact]
    public void Una_fila_exitosa_fallida_u_omitida_guarda_su_resultado()
    {
        var fila = new FilaDeCampania { Id = FilaDeCampania.Clave(Guid.NewGuid(), 1), N = 1 };
        fila.MarcarFallida("flujo-no-publicado", "Sin flujo.", Ahora);
        (fila.Estado, fila.ErrorCodigo, fila.Intentos).ShouldBe((EstadoFilaCampania.Fallida, "flujo-no-publicado", 1));
        fila.MarcarExitosa(AccionFilaCampania.RenovacionAbierta, Guid.NewGuid(), "NORTE-E-2026-000007", null, Ahora);
        (fila.Estado, fila.ErrorCodigo, fila.Folio, fila.Intentos).ShouldBe((EstadoFilaCampania.Exitosa, null, "NORTE-E-2026-000007", 2));
        fila.MarcarOmitida("Nada por renovar.", Ahora);
        fila.Estado.ShouldBe(EstadoFilaCampania.Omitida);
    }
}
