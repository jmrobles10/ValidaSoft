using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

public class VinculoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private static Vinculo Nuevo(out VinculoCreado creado)
    {
        creado = Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api,
            new ReferenciaExterna("SRM", "ADP-00123"), "Compras@Proveedor.co", Ahora);
        return Vinculo.Create(creado);
    }

    private static Vinculo Nuevo() => Nuevo(out _);

    [Fact]
    public void Nace_invitado_con_identificador_propio_y_correo_normalizado()
    {
        var vinculo = Nuevo(out var creado);

        creado.VinculoId.ShouldNotBe(Guid.Empty);
        vinculo.Id.ShouldBe(creado.VinculoId);
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        vinculo.CorreoTercero.ShouldBe("compras@proveedor.co");
        vinculo.InvitacionAceptada.ShouldBeFalse();
    }

    [Fact]
    public void No_se_crea_sin_correo_del_tercero() =>
        Should.Throw<ReglaDeNegocioException>(() =>
                Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "sin-arroba", Ahora))
            .Codigo.ShouldBe("correo-invitacion-requerido");

    [Fact]
    public void Envia_y_reenvia_la_invitacion_contando_envios()
    {
        var vinculo = Nuevo();

        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora));
        vinculo.Apply(vinculo.EnviarInvitacion("otro@proveedor.co", TimeSpan.FromHours(72), null, Ahora.AddHours(1)));

        vinculo.Invitacion!.Envios.ShouldBe(2);
        vinculo.Invitacion.Correo.ShouldBe("otro@proveedor.co");
        vinculo.Invitacion.Expira.ShouldBe(Ahora.AddHours(73));
    }

    [Fact]
    public void Aceptar_marca_la_aceptacion_sin_cambiar_el_estado()
    {
        var vinculo = Nuevo();
        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora));

        vinculo.Apply(vinculo.AceptarInvitacion("compras@proveedor.co", Ahora.AddHours(2)));

        vinculo.InvitacionAceptada.ShouldBeTrue();
        vinculo.InvitacionAceptadaEn.ShouldBe(Ahora.AddHours(2));
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        Should.Throw<ReglaDeNegocioException>(() => vinculo.EnviarInvitacion(null, TimeSpan.FromHours(1), null, Ahora))
            .Codigo.ShouldBe("invitacion-ya-aceptada");
    }

    [Fact]
    public void No_acepta_sin_invitacion_ni_con_el_enlace_expirado()
    {
        var vinculo = Nuevo();
        Should.Throw<ReglaDeNegocioException>(() => vinculo.AceptarInvitacion("a@b.co", Ahora)).Codigo.ShouldBe("sin-invitacion");

        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(1), null, Ahora));
        Should.Throw<ReglaDeNegocioException>(() => vinculo.AceptarInvitacion("a@b.co", Ahora.AddHours(2)))
            .Codigo.ShouldBe("invitacion-expirada");
    }

    [Fact]
    public void Desbloquear_restituye_el_estado_previo()
    {
        var vinculo = Nuevo();

        vinculo.Apply(vinculo.Bloquear("Sanción contractual", null, Ahora));
        vinculo.Estado.ShouldBe(EstadoVinculo.Bloqueado);
        vinculo.Bloqueo!.Motivo.ShouldBe("Sanción contractual");
        Should.Throw<ReglaDeNegocioException>(() => vinculo.EnviarInvitacion(null, TimeSpan.FromHours(1), null, Ahora));

        vinculo.Apply(vinculo.Desbloquear("Se levantó la sanción", Ahora.AddDays(1)));
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        vinculo.Bloqueo.ShouldBeNull();
    }

    [Fact]
    public void Reintegrar_sin_aprobacion_previa_vuelve_a_invitado_y_exige_nueva_aceptacion()
    {
        var vinculo = Nuevo();
        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora));
        vinculo.Apply(vinculo.AceptarInvitacion("a@b.co", Ahora));
        vinculo.Apply(vinculo.Desvincular("Fin de contrato", Ahora));

        vinculo.Apply(vinculo.Reintegrar("Nuevo contrato", Ahora.AddDays(30)));

        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        vinculo.InvitacionAceptada.ShouldBeFalse();
    }

    [Fact]
    public void Reintegrar_con_aprobacion_previa_vuelve_a_validacion()
    {
        var vinculo = Nuevo();
        vinculo.TuvoAprobacion = true;
        vinculo.Apply(vinculo.Desvincular("Fin de contrato", Ahora));

        vinculo.Apply(vinculo.Reintegrar("Nuevo contrato", Ahora));

        vinculo.Estado.ShouldBe(EstadoVinculo.EnValidacion);
    }

    [Fact]
    public void Un_vinculo_anulado_no_admite_mas_cambios()
    {
        var vinculo = Nuevo();
        vinculo.Apply(vinculo.Bloquear("Fraude", null, Ahora));
        vinculo.Apply(vinculo.Anular("Fraude confirmado", Ahora));

        vinculo.Estado.ShouldBe(EstadoVinculo.Anulado);
        Should.Throw<ReglaDeNegocioException>(() => vinculo.Desvincular("x", Ahora)).Codigo.ShouldBe("estado-terminal");
        Should.Throw<ReglaDeNegocioException>(() => vinculo.Bloquear("x", null, Ahora)).Codigo.ShouldBe("estado-terminal");
        Should.Throw<ReglaDeNegocioException>(() => vinculo.ActualizarAtributos([], Ahora)).Codigo.ShouldBe("estado-terminal");
    }

    [Fact]
    public void Solo_se_reintegra_un_vinculo_desvinculado() =>
        Should.Throw<ReglaDeNegocioException>(() => Nuevo().Reintegrar("x", Ahora)).Codigo.ShouldBe("estado-invalido");

    [Fact]
    public void La_referencia_del_evento_de_creacion_es_la_primera_de_la_lista()
    {
        var vinculo = Nuevo();

        vinculo.Referencias.ShouldBe([new ReferenciaExterna("SRM", "ADP-00123")]);
        vinculo.ClavesReferencia.ShouldBe(["SRM|ADP-00123"]);
    }

    [Fact]
    public void Otro_sistema_agrega_su_referencia_y_comparte_el_vinculo()
    {
        var vinculo = Nuevo();

        var agregada = vinculo.AgregarReferencia(new ReferenciaExterna("MARCO", "PRV-77"), Ahora.AddDays(1));
        agregada.ShouldNotBeNull();
        vinculo.Apply(agregada);

        vinculo.Referencias.Select(r => r.Sistema).ShouldBe(["SRM", "MARCO"]);
        vinculo.ClavesReferencia.ShouldContain("MARCO|PRV-77");
        vinculo.ActualizadoEn.ShouldBe(Ahora.AddDays(1));
    }

    [Fact]
    public void La_misma_referencia_es_idempotente_y_otro_id_del_mismo_sistema_es_conflicto()
    {
        var vinculo = Nuevo();

        vinculo.AgregarReferencia(new ReferenciaExterna("SRM", "ADP-00123"), Ahora).ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => vinculo.AgregarReferencia(new ReferenciaExterna("SRM", "ADP-99999"), Ahora))
            .Codigo.ShouldBe("referencia-en-conflicto");
        Should.Throw<ReglaDeNegocioException>(() => vinculo.AgregarReferencia(new ReferenciaExterna("MARCO", " "), Ahora))
            .Codigo.ShouldBe("referencia-externa-invalida");
    }

    [Fact]
    public void Los_atributos_se_validan_contra_el_tipo_de_vinculo()
    {
        var proveedor = TipoVinculo.Semilla().Single(t => t.Codigo == "PROVEEDOR");
        var validos = new Dictionary<string, JsonElement>
        {
            ["montoEstimado"] = JsonSerializer.SerializeToElement(150_000_000),
            ["nivelRiesgo"] = JsonSerializer.SerializeToElement("Medio"),
            ["categorias"] = JsonSerializer.SerializeToElement(new[] { "Materiales", "Equipos" })
        };

        ValidadorDeAtributos.Validar(proveedor, validos).Count.ShouldBe(3);

        Should.Throw<ReglaDeNegocioException>(() => ValidadorDeAtributos.Validar(proveedor,
                new Dictionary<string, JsonElement> { ["nivelRiesgo"] = JsonSerializer.SerializeToElement("Extremo") }))
            .Codigo.ShouldBe("atributo-invalido");

        Should.Throw<ReglaDeNegocioException>(() => ValidadorDeAtributos.Validar(proveedor,
                new Dictionary<string, JsonElement> { ["color"] = JsonSerializer.SerializeToElement("azul") }))
            .Codigo.ShouldBe("atributo-no-permitido");
    }
}
