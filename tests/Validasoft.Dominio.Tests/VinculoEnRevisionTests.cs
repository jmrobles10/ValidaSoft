using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// «En revisión» por una actualización (VR-1): el vínculo con una actualización abierta se muestra «En revisión» sin cambiar de estado
/// (el certificado, la vigencia y la consulta siguen como siempre, A-2); al cerrarse la actualización deja de estarlo.
/// </summary>
public class VinculoEnRevisionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    /// <summary>Un vínculo ya aprobado (Activo), sin expediente abierto.</summary>
    private static Vinculo Aprobado()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        Abrir(vinculo, TipoExpediente.Inicial);
        Cerrar(vinculo, ResultadoExpediente.Aprobado);
        return vinculo;
    }

    private static Guid Abrir(Vinculo vinculo, TipoExpediente tipo)
    {
        var id = Guid.NewGuid();
        vinculo.Apply(vinculo.VincularExpediente(id, tipo, "N-E-1", Ahora));
        return id;
    }

    private static void Cerrar(Vinculo vinculo, ResultadoExpediente resultado)
    {
        foreach (var evento in vinculo.CerrarExpediente(vinculo.ExpedienteAbiertoId!.Value, resultado, Ahora.AddDays(1)))
        {
            if (evento is ExpedienteCerrado cerrado) vinculo.Apply(cerrado);
            if (evento is VinculoActivado activado) vinculo.Apply(activado);
        }
    }

    [Fact]
    public void Un_vinculo_aprobado_sin_expediente_abierto_no_esta_en_revision()
    {
        Aprobado().EnRevisionPorActualizacion.ShouldBeFalse();
    }

    [Fact]
    public void Una_actualizacion_abierta_lo_deja_en_revision_sin_cambiar_su_estado_y_al_aprobarse_vuelve_a_Activo()
    {
        var vinculo = Aprobado();
        Abrir(vinculo, TipoExpediente.Actualizacion);

        vinculo.EnRevisionPorActualizacion.ShouldBeTrue();
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo); // el estado de dominio no cambia: el certificado y los eventos siguen igual (A-2)

        Cerrar(vinculo, ResultadoExpediente.Aprobado);
        vinculo.EnRevisionPorActualizacion.ShouldBeFalse();
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
    }

    [Theory]
    [InlineData(ResultadoExpediente.Rechazado)]
    [InlineData(ResultadoExpediente.Anulado)]
    public void Una_actualizacion_rechazada_o_anulada_deja_de_tenerlo_en_revision_y_conserva_su_estado(ResultadoExpediente resultado)
    {
        var vinculo = Aprobado();
        Abrir(vinculo, TipoExpediente.Actualizacion);

        Cerrar(vinculo, resultado);

        vinculo.EnRevisionPorActualizacion.ShouldBeFalse();
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo); // RV-2: la empresa decide qué hacer
    }

    [Fact]
    public void Por_vencer_y_vencido_con_una_actualizacion_abierta_tambien_estan_en_revision_y_conservan_su_estado()
    {
        var porVencer = Aprobado();
        porVencer.Apply(new VinculoPorVencer(new DateOnly(2026, 12, 1), 25, [30], Ahora.AddDays(2)));
        porVencer.Estado.ShouldBe(EstadoVinculo.PorVencer);
        porVencer.EnRevisionPorActualizacion.ShouldBeFalse();
        Abrir(porVencer, TipoExpediente.Actualizacion);
        porVencer.EnRevisionPorActualizacion.ShouldBeTrue();
        porVencer.Estado.ShouldBe(EstadoVinculo.PorVencer);

        var vencido = Aprobado();
        vencido.Apply(vencido.Vencer("RUT", Ahora.AddDays(400))!);
        vencido.Estado.ShouldBe(EstadoVinculo.Vencido);
        Abrir(vencido, TipoExpediente.Actualizacion);
        vencido.EnRevisionPorActualizacion.ShouldBeTrue();
        vencido.Estado.ShouldBe(EstadoVinculo.Vencido);
    }

    [Fact]
    public void Una_renovacion_o_un_expediente_inicial_abierto_no_es_una_actualizacion()
    {
        var renovacion = Aprobado();
        Abrir(renovacion, TipoExpediente.Renovacion);
        renovacion.EnRevisionPorActualizacion.ShouldBeFalse();

        var inicial = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "a@b.co", Ahora));
        Abrir(inicial, TipoExpediente.Inicial);
        inicial.Estado.ShouldBe(EstadoVinculo.EnValidacion);
        inicial.EnRevisionPorActualizacion.ShouldBeFalse();
    }

    [Fact]
    public void Bloqueado_manda_sobre_en_revision_y_al_desbloquear_con_la_actualizacion_abierta_vuelve_a_estarlo()
    {
        var vinculo = Aprobado();
        Abrir(vinculo, TipoExpediente.Actualizacion);

        vinculo.Apply(vinculo.Bloquear("Sanción", null, Ahora.AddDays(2)));
        vinculo.Estado.ShouldBe(EstadoVinculo.Bloqueado);
        vinculo.EnRevisionPorActualizacion.ShouldBeFalse(); // el bloqueo se ve primero

        vinculo.Apply(vinculo.Desbloquear("Resuelto", Ahora.AddDays(3)));
        vinculo.Estado.ShouldBe(EstadoVinculo.Activo);
        vinculo.EnRevisionPorActualizacion.ShouldBeTrue();
    }

    [Fact]
    public void Un_vinculo_desvinculado_o_anulado_no_tiene_nada_en_revision()
    {
        var desvinculado = Aprobado();
        Abrir(desvinculado, TipoExpediente.Actualizacion);
        desvinculado.Apply(desvinculado.Desvincular("Fin", Ahora.AddDays(2)));
        desvinculado.EnRevisionPorActualizacion.ShouldBeFalse();

        var anulado = Aprobado();
        Abrir(anulado, TipoExpediente.Actualizacion);
        anulado.Apply(anulado.Anular("Error", Ahora.AddDays(2)));
        anulado.EnRevisionPorActualizacion.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ResultadoExpediente.Rechazado)]
    [InlineData(ResultadoExpediente.Anulado)]
    public void Una_actualizacion_rechazada_o_anulada_deja_el_aviso_de_que_no_se_aprobo_hasta_que_se_abre_otro_expediente(ResultadoExpediente resultado)
    {
        var vinculo = Aprobado();
        vinculo.ActualizacionNoAprobada.ShouldBeFalse(); // el expediente inicial aprobado no es una actualización
        var actualizacion = Abrir(vinculo, TipoExpediente.Actualizacion);
        vinculo.ActualizacionNoAprobada.ShouldBeFalse(); // abierta: está en revisión, no «no aprobada»

        Cerrar(vinculo, resultado);

        vinculo.ActualizacionNoAprobada.ShouldBeTrue();
        (vinculo.Estado, vinculo.EnRevisionPorActualizacion, vinculo.UltimoExpedienteId, vinculo.UltimoExpedienteTipo, vinculo.UltimoResultado)
            .ShouldBe((EstadoVinculo.Activo, false, (Guid?)actualizacion, (TipoExpediente?)TipoExpediente.Actualizacion, (ResultadoExpediente?)resultado));
        vinculo.UltimoExpedienteCerradoEn.ShouldBe(Ahora.AddDays(1));

        // Dura hasta que se abre otro expediente (la siguiente actualización): ahí vuelve a estar en revisión y, aprobada, deja de haber aviso.
        Abrir(vinculo, TipoExpediente.Actualizacion);
        vinculo.ActualizacionNoAprobada.ShouldBeFalse();
        vinculo.EnRevisionPorActualizacion.ShouldBeTrue();
        Cerrar(vinculo, ResultadoExpediente.Aprobado);
        vinculo.ActualizacionNoAprobada.ShouldBeFalse();
    }

    [Fact]
    public void El_aviso_de_actualizacion_no_aprobada_no_se_muestra_con_el_vinculo_bloqueado_ni_si_fue_una_renovacion_ni_tras_terminarlo_y_reintegrarlo()
    {
        var bloqueado = Aprobado();
        Abrir(bloqueado, TipoExpediente.Actualizacion);
        Cerrar(bloqueado, ResultadoExpediente.Rechazado);
        bloqueado.ActualizacionNoAprobada.ShouldBeTrue();
        bloqueado.Apply(bloqueado.Bloquear("Sanción", null, Ahora.AddDays(2)));
        bloqueado.ActualizacionNoAprobada.ShouldBeFalse(); // el bloqueo se ve primero
        bloqueado.Apply(bloqueado.Desbloquear("Resuelto", Ahora.AddDays(3)));
        bloqueado.ActualizacionNoAprobada.ShouldBeTrue();

        var renovacion = Aprobado();
        Abrir(renovacion, TipoExpediente.Renovacion);
        Cerrar(renovacion, ResultadoExpediente.Rechazado);
        renovacion.ActualizacionNoAprobada.ShouldBeFalse();

        // Terminar el vínculo anula la actualización abierta, pero no es una decisión sobre ella: al reintegrarlo sin revalidar no hay aviso.
        var terminado = Aprobado();
        Abrir(terminado, TipoExpediente.Actualizacion);
        terminado.Apply(terminado.Desvincular("Fin", Ahora.AddDays(2)));
        terminado.ActualizacionNoAprobada.ShouldBeFalse();
        terminado.Apply(terminado.Reintegrar("Se renovó el contrato.", Ahora.AddDays(3), sinRevalidar: true));
        (terminado.Estado, terminado.ActualizacionNoAprobada).ShouldBe((EstadoVinculo.Activo, false));
    }

    [Fact]
    public void La_expresion_que_filtra_en_la_base_y_la_propiedad_son_la_misma_regla()
    {
        var expresion = Vinculo.EsEnRevisionPorActualizacion.Compile();
        foreach (var estado in Enum.GetValues<EstadoVinculo>())
        foreach (var tipo in new TipoExpediente?[] { null, TipoExpediente.Inicial, TipoExpediente.Renovacion, TipoExpediente.Actualizacion })
        foreach (var abierto in new Guid?[] { null, Guid.NewGuid() })
        {
            var vinculo = new Vinculo { Estado = estado, ExpedienteAbiertoTipo = tipo, ExpedienteAbiertoId = abierto };
            vinculo.EnRevisionPorActualizacion.ShouldBe(expresion(vinculo), $"{estado} / {tipo} / {abierto}");
            vinculo.EnRevisionPorActualizacion.ShouldBe(
                abierto is not null && tipo == TipoExpediente.Actualizacion &&
                estado is EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido, $"{estado} / {tipo} / {abierto}");
        }
    }
}
