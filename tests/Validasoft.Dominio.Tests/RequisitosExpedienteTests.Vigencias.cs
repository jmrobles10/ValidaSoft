using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Vigencias en el expediente: el plazo de revalidación configurable (RV-4, el menor de los requisitos afectados) y la renovación
/// periódica del vínculo (VV-3), donde el tercero ratifica las secciones de la ficha que ya había aceptado o las actualiza.
/// </summary>
public partial class RequisitosExpedienteTests
{
    private static DatosDeApertura DatosDeRenovacion() => DatosDeActualizacion() with { Tipo = TipoExpediente.Renovacion };

    // ---------- plazo de revalidación (RV-4) ----------

    [Fact]
    public void El_plazo_de_revalidacion_es_15_dias_por_omision_y_el_menor_de_los_requisitos_si_alguno_trae_el_suyo()
    {
        var porOmision = ActualizacionPorElRut(Flujo(), out _);
        porOmision.DiasDePlazoDeRevalidacion.ShouldBe(Expediente.DiasDePlazoDeRevalidacionPorOmision);
        porOmision.PlazoDeRevalidacionHasta.ShouldBe(Ahora.AddDays(15));

        // El RUT de este tenant da 30 días y la sección general 7: rige el menor.
        var configurada = Expediente.Reproducir(null,
            Expediente.Abrir(DatosDeActualizacion(), Flujo(), Ahora, [Rut(V(_docRut, 2)) with { DiasRevalidacion = 30 }, General(V(_docGeneral, 2)) with { DiasRevalidacion = 7 }]));
        configurada.DiasDePlazoDeRevalidacion.ShouldBe(7);
        configurada.PlazoDeRevalidacionHasta.ShouldBe(Ahora.AddDays(7));

        // Solo documentos de 30 días: vale 30, no los 15 de por omisión.
        var largo = Expediente.Reproducir(null,
            Expediente.Abrir(DatosDeActualizacion(), Flujo(), Ahora, [Rut(V(_docRut, 2)) with { DiasRevalidacion = 30 }]));
        largo.DiasDePlazoDeRevalidacion.ShouldBe(30);
    }

    [Fact]
    public void Ampliar_la_actualizacion_con_un_requisito_de_plazo_menor_acorta_el_plazo_de_toda_la_actualizacion()
    {
        var e = Expediente.Reproducir(null,
            Expediente.Abrir(DatosDeActualizacion(), Flujo(), Ahora, [Rut(V(_docRut, 2)) with { DiasRevalidacion = 30 }]));
        e.PlazoDeRevalidacionHasta.ShouldBe(Ahora.AddDays(30));

        e = Aplicar(e, e.AgregarRequisitos([General(V(_docGeneral, 2)) with { DiasRevalidacion = 3 }], Ahora.AddDays(1)));

        e.DiasDePlazoDeRevalidacion.ShouldBe(3);
        e.PlazoDeRevalidacionHasta.ShouldBe(Ahora.AddDays(3));
    }

    // ---------- renovación periódica: ratificar secciones (VV-3) ----------

    private RequisitoCalculado GeneralPorRatificar() => General(null) with { ExigeRatificacion = true };

    private IReadOnlyDictionary<Guid, EvaluacionDeSeccion> Evaluada(int version) =>
        new Dictionary<Guid, EvaluacionDeSeccion>
        {
            [_general] = new(V(_docGeneral, version), new CompletitudParaEmpresa(version, true, []))
        };

    [Fact]
    public void La_renovacion_periodica_deja_la_seccion_ya_aceptada_pendiente_y_la_sincronizacion_no_la_cubre_con_la_misma_version()
    {
        var renovacion = Expediente.Reproducir(null, Expediente.Abrir(DatosDeRenovacion(), Flujo(), Ahora, [GeneralPorRatificar()]));

        renovacion.Estado.ShouldBe(EstadoExpediente.Abierto);
        var general = renovacion.BuscarRequisito(_general)!;
        general.ShouldSatisfyAllConditions(
            r => r.EsSeccion.ShouldBeTrue(),
            r => r.ExigeRatificacion.ShouldBeTrue(),
            r => r.VersionActual.ShouldBeNull(),
            r => r.VersionAceptadaPrevia!.Numero.ShouldBe(1),
            r => r.Estado.ShouldBe(EstadoRequisito.Pendiente));

        // La ficha sigue en la versión 1 que la empresa ya aceptó: no es una versión nueva, no la adjunta.
        renovacion.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora.AddHours(1), Evaluada(1))
            .OfType<VersionAdjuntadaARequisito>().ShouldBeEmpty();
    }

    [Fact]
    public void Si_el_tercero_actualiza_la_seccion_la_version_nueva_cubre_el_requisito_y_las_areas_la_revisan()
    {
        var renovacion = Expediente.Reproducir(null, Expediente.Abrir(DatosDeRenovacion(), Flujo(), Ahora, [GeneralPorRatificar()]));

        var eventos = renovacion.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito>(), Ahora.AddHours(1), Evaluada(2));
        eventos.OfType<VersionAdjuntadaARequisito>().ShouldHaveSingleItem().Version.Numero.ShouldBe(2);
        var actualizada = Aplicar(renovacion, eventos);

        actualizada.Estado.ShouldBe(EstadoExpediente.EnRevision);
        var general = actualizada.BuscarRequisito(_general)!;
        general.Ratificado.ShouldBeFalse();
        general.Estado.ShouldBe(EstadoRequisito.EnRevision);
        actualizada.Etapas.Single().Areas.ShouldAllBe(a => a.Estado == EstadoAreaExpediente.Pendiente);
    }

    [Fact]
    public void Ratificar_la_seccion_la_confirma_sin_cambiar_datos_las_areas_la_aprueban_solas_y_la_empresa_la_acepta_de_nuevo()
    {
        var renovacion = Expediente.Reproducir(null, Expediente.Abrir(DatosDeRenovacion(), Flujo(), Ahora, [GeneralPorRatificar()]));
        var cuando = Ahora.AddDays(2);

        var eventos = renovacion.RatificarRequisito(_general, V(_docGeneral, 1), "tercero:compras@aceros.co", cuando);

        eventos.OfType<RequisitoRatificado>().ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            r => r.RequisitoId.ShouldBe(_general),
            r => r.Version.Numero.ShouldBe(1),
            r => r.Por.ShouldBe("tercero:compras@aceros.co"));
        var ratificada = Aplicar(renovacion, eventos);
        ratificada.Estado.ShouldBe(EstadoExpediente.Aprobado);
        var general = ratificada.BuscarRequisito(_general)!;
        general.Ratificado.ShouldBeTrue();
        general.VersionActual!.Numero.ShouldBe(1);
        general.Veredictos.Where(v => v.Vigente).ShouldAllBe(v =>
            v.Origen == OrigenVeredicto.Sistema && v.Justificacion!.Contains("ratificó") && v.Justificacion.Contains("la información"));
        // Aprobada la renovación, la empresa acepta de nuevo la sección: la base de su vigencia propia.
        var aceptada = eventos.OfType<SeccionAceptadaPorEmpresa>().ShouldHaveSingleItem();
        (aceptada.Seccion, aceptada.Version, aceptada.Fecha).ShouldBe(("FICHA_GENERAL", 1, cuando));
        Codigo(() => ratificada.RatificarRequisito(_general, V(_docGeneral, 1), "tercero", cuando)).ShouldBe("estado-invalido");
    }
}
