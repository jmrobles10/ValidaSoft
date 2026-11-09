using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

public class CuentasTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 9, 0, 0, TimeSpan.FromHours(-5));

    private static DatosDeCuentaBancaria Datos(string numero = "031-123456-78", string tipo = "Ahorros") =>
        new("CO", "Bancolombia", tipo, numero, "Ferretería El Tornillo S.A.S.", "NIT 900123456", null);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void Una_cuenta_guarda_el_numero_protegido_y_solo_los_ultimos_cuatro_en_claro()
    {
        var cuenta = CuentaBancaria.Registrar(Guid.NewGuid(), Datos(), n => $"protegido({n})", Ahora);
        cuenta.NumeroProtegido.ShouldBe("protegido(03112345678)");
        cuenta.Ultimos4.ShouldBe("5678");
        cuenta.NumeroEnmascarado.ShouldBe("****5678");
        cuenta.TipoCuenta.ShouldBe(TipoCuentaBancaria.Ahorros);
        cuenta.Moneda.ShouldBe("COP");
        cuenta.Estado.ShouldBe(EstadoCuentaBancaria.Activa);

        cuenta.Retirar(Ahora);
        cuenta.Estado.ShouldBe(EstadoCuentaBancaria.Retirada);
        Codigo(() => cuenta.Retirar(Ahora)).ShouldBe("cuenta-retirada");
    }

    [Theory]
    [InlineData("12345", "Ahorros", "numero-cuenta-invalido")]
    [InlineData("12A4567", "Ahorros", "numero-cuenta-invalido")]
    [InlineData("1234567", "Nomina", "tipo-cuenta-invalido")]
    public void Una_cuenta_mal_declarada_no_se_registra(string numero, string tipo, string codigo) =>
        Codigo(() => CuentaBancaria.Registrar(Guid.NewGuid(), Datos(numero, tipo), n => n, Ahora)).ShouldBe(codigo);

    private static Vinculo Aceptado()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "c@p.co", Ahora));
        vinculo.InvitacionAceptada = true;
        return vinculo;
    }

    private static CuentaParaDesignar Cuenta(Guid? id = null, string ultimos4 = "5678") =>
        new(id ?? Guid.NewGuid(), "Bancolombia", "Ahorros", "x", ultimos4, "Ferretería", "NIT 900123456", "COP");

    private static void Aplicar(Vinculo v, IEnumerable<object> eventos)
    {
        foreach (var evento in eventos)
        {
            switch (evento)
            {
                case CuentaDesignadaPropuesta e: v.Apply(e); break;
                case AprobacionDeCuentaRegistrada e: v.Apply(e); break;
                case CuentaDesignadaAprobada e: v.Apply(e); break;
                case CuentaDesignadaRechazada e: v.Apply(e); break;
            }
        }
    }

    [Fact]
    public void La_cuenta_que_elige_el_tercero_queda_designada_cuando_la_aprueban_dos_personas_distintas()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "c@p.co", Ahora));
        Codigo(() => vinculo.ProponerCuenta(Cuenta(), Ahora)).ShouldBe("invitacion-no-aceptada");
        vinculo.InvitacionAceptada = true;

        var primera = Cuenta();
        Aplicar(vinculo, [vinculo.ProponerCuenta(primera, Ahora)!]);
        vinculo.ProponerCuenta(primera, Ahora).ShouldBeNull(); // ya propuesta
        vinculo.CuentaPropuesta!.Estado.ShouldBe(EstadoCuentaDesignada.Propuesta);

        var una = vinculo.AprobarCuenta("ana", Ahora);
        una.ShouldHaveSingleItem().ShouldBeOfType<AprobacionDeCuentaRegistrada>();
        Aplicar(vinculo, una);
        Codigo(() => vinculo.AprobarCuenta("ana", Ahora)).ShouldBe("aprobador-repetido");
        vinculo.CuentaDesignada.ShouldBeNull();

        Aplicar(vinculo, vinculo.AprobarCuenta("beto", Ahora));
        vinculo.CuentaPropuesta.ShouldBeNull();
        vinculo.CuentaDesignada!.Cuenta.CuentaId.ShouldBe(primera.CuentaId);
        vinculo.CuentaDesignada.Aprobaciones.Select(a => a.UsuarioId).ShouldBe(["ana", "beto"]);
        Codigo(() => vinculo.ProponerCuenta(primera, Ahora)).ShouldBe("cuenta-ya-designada");
        Codigo(() => vinculo.AprobarCuenta("carla", Ahora)).ShouldBe("sin-cuenta-propuesta");

        // Cambiarla vuelve a pedir dos aprobaciones; mientras tanto la anterior sigue designada.
        var segunda = Cuenta(ultimos4: "9999");
        var cambio = vinculo.ProponerCuenta(segunda, Ahora)!;
        cambio.CuentaAnteriorId.ShouldBe(primera.CuentaId);
        Aplicar(vinculo, [cambio]);
        vinculo.CuentaDesignada!.Cuenta.CuentaId.ShouldBe(primera.CuentaId);
    }

    [Fact]
    public void Rechazar_la_cuenta_propuesta_exige_un_motivo_y_lo_conserva_para_el_tercero()
    {
        var vinculo = Aceptado();
        Aplicar(vinculo, [vinculo.ProponerCuenta(Cuenta(), Ahora)!]);
        Codigo(() => vinculo.RechazarCuenta("ana", " ", Ahora)).ShouldBe("motivo-requerido");

        Aplicar(vinculo, [vinculo.RechazarCuenta("ana", "El titular no coincide con la certificación.", Ahora)]);
        vinculo.CuentaPropuesta.ShouldBeNull();
        vinculo.CuentaRechazada!.MotivoRechazo.ShouldBe("El titular no coincide con la certificación.");
        vinculo.CuentaRechazada.Estado.ShouldBe(EstadoCuentaDesignada.Rechazada);

        Aplicar(vinculo, [vinculo.ProponerCuenta(Cuenta(), Ahora)!]);
        vinculo.CuentaRechazada.ShouldBeNull();
    }
}
