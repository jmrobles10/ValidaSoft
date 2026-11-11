using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>Reinicio tras un rechazo (DS-4): lo que el flujo configura y desde cuándo puede volver a postularse el tercero.</summary>
public class ReintentoTrasRechazoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _empresa = Guid.NewGuid();
    private readonly List<Area> _areas;

    public ReintentoTrasRechazoTests() => _areas = [Area.Crear(_empresa, "COMPRAS", "Compras")];

    private DefinicionDeFlujo Definicion(ReintentoTrasRechazo? reintento) =>
        DefinicionDeFlujo.TodasEnParalelo(_areas) with { Reintento = reintento };

    [Fact]
    public void Por_defecto_no_se_permite_volver_a_postularse_por_cuenta_propia()
    {
        var flujo = Flujo.Crear(_empresa, "PROV", "Proveedores", Guid.NewGuid(), Definicion(null), _areas, Ahora);

        var version = flujo.Ultima;
        version.Reintento.ShouldNotBeNull().Permitido.ShouldBeFalse();
        version.ReintentoEfectivo().PuedeDesde(Ahora).ShouldBeNull();
    }

    [Fact]
    public void Con_permiso_el_tercero_puede_volver_a_postularse_pasados_los_dias_de_espera_desde_el_rechazo()
    {
        var politica = new ReintentoTrasRechazo(true, 15);

        politica.PuedeDesde(Ahora).ShouldBe(Ahora.AddDays(15));
        new ReintentoTrasRechazo(true, 0).PuedeDesde(Ahora).ShouldBe(Ahora); // de inmediato
    }

    [Fact]
    public void La_politica_se_guarda_al_crear_editar_y_publicar_el_flujo()
    {
        var flujo = Flujo.Crear(_empresa, "PROV", "Proveedores", Guid.NewGuid(), Definicion(new ReintentoTrasRechazo(true, 30)), _areas, Ahora);
        flujo.Ultima.Reintento.ShouldBe(new ReintentoTrasRechazo(true, 30));

        flujo.Publicar(_areas, Ahora);
        flujo.VersionPublicada!.ReintentoEfectivo().ShouldBe(new ReintentoTrasRechazo(true, 30));

        // Un borrador nuevo parte de la política vigente y puede cambiarla; la publicada no cambia.
        var borrador = flujo.EditarBorrador(null, flujo.VersionPublicada.Definicion() with { Reintento = new ReintentoTrasRechazo(false, 99) }, _areas, Ahora);
        borrador.ReintentoEfectivo().ShouldBe(ReintentoTrasRechazo.PorDefecto); // sin permiso no hay espera
        flujo.VersionPublicada.ReintentoEfectivo().ShouldBe(new ReintentoTrasRechazo(true, 30));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void La_espera_va_de_0_a_365_dias(int dias) =>
        Should.Throw<ReglaDeNegocioException>(() => new ReintentoTrasRechazo(true, dias).Validar()).Codigo.ShouldBe("reintento-invalido");

    [Fact]
    public void Una_version_guardada_antes_sin_politica_vale_la_de_por_defecto()
    {
        var version = new FlujoVersion { Reintento = null };

        version.ReintentoEfectivo().ShouldBe(ReintentoTrasRechazo.PorDefecto);
    }
}
