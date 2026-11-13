using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Cuenta designada y cierre del expediente (CB-4): si el tipo de vínculo exige una cuenta designada, el expediente no se aprueba
/// mientras la empresa no la apruebe; espera en revisión como con una verificación pendiente y se cierra solo al aprobarla.
/// </summary>
public class CuentaDesignadaDelExpedienteTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 13, 9, 0, 0, TimeSpan.FromHours(-5));
    private const string ClaveTercero = "CO|NIT|900123456";

    private readonly Guid _a = Guid.NewGuid();
    private readonly Revisor _ana = new(Guid.NewGuid(), null);

    private FlujoParaExpediente Flujo(params VerificacionRequeridaDelFlujo[] verificaciones) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "HAB", "Habilitación", 1, OrigenFlujo.PorDefecto,
            [new EtapaDelFlujo(1, [new AreaDelFlujo(_a, "COMPRAS", "Compras", 1, true, false, null)])], PoliticaDevolucion.Inmediata, false, verificaciones);

    private static DatosDeApertura Datos(bool exigeCuenta, bool cuentaAprobada = false, TipoExpediente tipo = TipoExpediente.Inicial) =>
        new(Guid.NewGuid(), Guid.NewGuid(), tipo, "NORTE-E-2026-000001", Guid.NewGuid(), "Aceros S.A.S.", ClaveTercero, Guid.NewGuid(), "PROVEEDOR", "Proveedor",
            new ActorDeExpediente(ActorTipo.Tercero, "tercero"), [], exigeCuenta, cuentaAprobada);

    private Expediente Abrir(DatosDeApertura datos, params VerificacionRequeridaDelFlujo[] verificaciones) =>
        Expediente.Reproducir(null, Expediente.Abrir(datos, Flujo(verificaciones), Ahora));

    private static Expediente Aplicar(Expediente e, IReadOnlyList<object> eventos) => Expediente.Reproducir(e, eventos);

    private Expediente AreaAprueba(Expediente e) => Aplicar(e, e.EmitirVeredicto(_a, _ana, Veredicto.Aprobado, [], null, Ahora));

    [Fact]
    public void Con_la_exigencia_y_la_cuenta_sin_aprobar_las_areas_aprueban_pero_el_expediente_espera_en_revision()
    {
        var e = Abrir(Datos(exigeCuenta: true));
        e.ExigeCuentaDesignada.ShouldBeTrue();
        e.CuentaDesignadaAprobada.ShouldBeFalse();

        e = AreaAprueba(e);

        e.Estado.ShouldBe(EstadoExpediente.EnRevision); // no pasa a Aprobado
        e.Etapas.ShouldAllBe(etapa => etapa.Estado == EstadoEtapa.Aprobada);
        e.EsperandoCuentaDesignada.ShouldBeTrue();
        e.EsperandoSoloLaCuentaDesignada.ShouldBeTrue();
        e.EsperandoVerificaciones.ShouldBeTrue(); // el mismo estado de espera que una verificación pendiente
        e.CerradoEn.ShouldBeNull();
    }

    [Fact]
    public void Al_aprobar_la_empresa_la_cuenta_el_expediente_cierra_aprobado_solo()
    {
        var e = AreaAprueba(Abrir(Datos(exigeCuenta: true)));
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);

        var eventos = e.ConfirmarCuentaDesignada(Ahora.AddHours(2));

        eventos.ShouldContain(x => x is CuentaDesignadaConfirmada);
        eventos.ShouldContain(x => x is ExpedienteAprobado);
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        e.CuentaDesignadaAprobada.ShouldBeTrue();
        e.EsperandoCuentaDesignada.ShouldBeFalse();
        // Ya cerrado, aprobar otra cuenta no hace nada.
        e.ConfirmarCuentaDesignada(Ahora.AddHours(3)).ShouldBeEmpty();
    }

    [Fact]
    public void Si_la_cuenta_se_aprueba_antes_de_que_las_areas_terminen_el_expediente_cierra_cuando_la_ultima_aprueba()
    {
        var e = Abrir(Datos(exigeCuenta: true));

        var eventos = e.ConfirmarCuentaDesignada(Ahora.AddHours(1));
        eventos.Select(x => x.GetType().Name).ShouldBe(["CuentaDesignadaConfirmada"]); // aún falta el área: nada se cierra
        e = Aplicar(e, eventos);
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EsperandoCuentaDesignada.ShouldBeFalse();

        AreaAprueba(e).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Si_la_cuenta_ya_estaba_aprobada_al_abrir_no_frena_el_cierre()
    {
        var e = Abrir(Datos(exigeCuenta: true, cuentaAprobada: true));

        e.ExigeCuentaDesignada.ShouldBeTrue();
        e.CuentaDesignadaAprobada.ShouldBeTrue();
        AreaAprueba(e).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Sin_la_exigencia_el_expediente_se_aprueba_como_siempre_y_confirmar_la_cuenta_no_hace_nada()
    {
        var e = Abrir(Datos(exigeCuenta: false));

        e.ExigeCuentaDesignada.ShouldBeFalse();
        e.CuentaDesignadaSatisfecha.ShouldBeTrue();
        e.ConfirmarCuentaDesignada(Ahora).ShouldBeEmpty();
        AreaAprueba(e).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Un_rechazo_de_la_cuenta_no_toca_al_expediente_que_sigue_esperando_a_que_el_tercero_designe_otra()
    {
        var e = AreaAprueba(Abrir(Datos(exigeCuenta: true)));
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "c@p.co", Ahora));
        vinculo.InvitacionAceptada = true;
        vinculo.Apply(vinculo.ProponerCuenta(Cuenta(), Ahora)!);

        // El rechazo es un evento del vínculo: el expediente no se entera ni se rechaza.
        var rechazo = vinculo.RechazarCuenta("usuario-1", "El titular no coincide con la certificación.", Ahora.AddHours(1));
        vinculo.Apply(rechazo);

        vinculo.CuentaRechazada.ShouldNotBeNull();
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EsperandoCuentaDesignada.ShouldBeTrue();
        e.ConfirmarCuentaDesignada(Ahora.AddHours(2)).ShouldNotBeEmpty(); // pero cuando una cuenta se apruebe, cierra
    }

    [Fact]
    public void Con_una_verificacion_pendiente_y_la_cuenta_pendiente_espera_por_las_dos_y_cierra_con_la_ultima()
    {
        var listas = new VerificacionRequeridaDelFlujo(TipoVerificacion.ListasRestrictivas);
        var e = AreaAprueba(Abrir(Datos(exigeCuenta: true), listas));
        e.EsperandoCuentaDesignada.ShouldBeTrue();
        e.EsperandoSoloLaCuentaDesignada.ShouldBeFalse(); // también falta la verificación: no es «solo la cuenta»

        // La cuenta llega primero: sigue esperando la verificación (no se cierra).
        var tras = Aplicar(e, e.ConfirmarCuentaDesignada(Ahora.AddHours(1)));
        tras.Estado.ShouldBe(EstadoExpediente.EnRevision);
        tras.EsperandoCuentaDesignada.ShouldBeFalse();
        tras.EsperandoVerificaciones.ShouldBeTrue();
    }

    [Fact]
    public void Una_actualizacion_de_un_documento_no_repite_la_exigencia_de_la_cuenta()
    {
        var e = Abrir(Datos(exigeCuenta: true, tipo: TipoExpediente.Actualizacion));

        e.ExigeCuentaDesignada.ShouldBeFalse();
        AreaAprueba(e).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Una_renovacion_si_la_exige()
    {
        var e = AreaAprueba(Abrir(Datos(exigeCuenta: true, tipo: TipoExpediente.Renovacion)));

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EsperandoCuentaDesignada.ShouldBeTrue();
    }

    [Fact]
    public void El_expediente_que_espera_la_cuenta_guarda_su_estado_al_serializarse_como_lo_hace_la_proyeccion()
    {
        var e = AreaAprueba(Abrir(Datos(exigeCuenta: true)));

        var copia = System.Text.Json.JsonSerializer.Deserialize<Expediente>(System.Text.Json.JsonSerializer.Serialize(e))!;

        (copia.ExigeCuentaDesignada, copia.CuentaDesignadaAprobada, copia.EsperandoCuentaDesignada).ShouldBe((true, false, true));
    }

    private static CuentaParaDesignar Cuenta() =>
        new(Guid.NewGuid(), "Bancolombia", "Ahorros", "protegido", "1234", "Aceros S.A.S.", "CO|NIT|900123456", "COP");
}
