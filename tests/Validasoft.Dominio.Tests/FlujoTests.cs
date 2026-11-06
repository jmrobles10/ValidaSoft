using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

public class FlujoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private readonly Guid _empresa = Guid.NewGuid();
    private readonly Area _aprobador;
    private readonly Area _cumplimiento;
    private readonly List<Area> _areas;

    public FlujoTests()
    {
        _aprobador = Area.Crear(_empresa, "APROBADOR_INICIAL", "Aprobador inicial");
        _cumplimiento = Area.Crear(_empresa, "OFICIAL_CUMPLIMIENTO", "Oficial de cumplimiento");
        _areas = [_aprobador, _cumplimiento];
    }

    private static DefinicionDeFlujo Definicion(params EtapaDeFlujo[] etapas) => new(etapas, PoliticaDevolucion.Inmediata, false);

    private static EtapaDeFlujo Etapa(int orden, params AreaDeEtapa[] areas) => new(orden, areas);

    private static AreaDeEtapa En(Area area, int aprobadores = 1, bool rechaza = false, bool automatica = false, int? sla = null) =>
        new(area.Id, aprobadores, rechaza, automatica, sla);

    private Flujo Nuevo(DefinicionDeFlujo? definicion = null) =>
        Flujo.Crear(_empresa, "hab_proveedores", "Habilitación de proveedores", Guid.NewGuid(),
            definicion ?? DefinicionDeFlujo.TodasEnParalelo(_areas), _areas, Ahora);

    private string Codigo(DefinicionDeFlujo definicion) =>
        Should.Throw<ReglaDeNegocioException>(() => definicion.Validar(_empresa, _areas)).Codigo;

    [Fact]
    public void Por_defecto_una_sola_etapa_con_todas_las_areas_activas_en_paralelo()
    {
        var inactiva = Area.Crear(_empresa, "SST", "Seguridad y salud");
        inactiva.Actualizar("Seguridad y salud", false);

        var definicion = DefinicionDeFlujo.TodasEnParalelo([.. _areas, inactiva]);

        var etapa = definicion.Etapas.ShouldHaveSingleItem();
        etapa.Areas.Select(a => a.AreaId).ShouldBe([_aprobador.Id, _cumplimiento.Id]);
        etapa.Areas.ShouldAllBe(a => a.AprobadoresRequeridos == 1 && !a.Automatica);
        definicion.PoliticaDevolucion.ShouldBe(PoliticaDevolucion.Inmediata);
    }

    [Fact]
    public void Nace_en_borrador_y_publicar_congela_la_version()
    {
        var flujo = Nuevo();
        flujo.Codigo.ShouldBe("HAB_PROVEEDORES");
        flujo.Borrador!.Version.ShouldBe(1);
        flujo.VersionPublicada.ShouldBeNull();

        var v1 = flujo.Publicar(_areas, Ahora);
        v1.Estado.ShouldBe(EstadoFlujoVersion.Publicada);
        flujo.Borrador.ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => flujo.Publicar(_areas, Ahora)).Codigo.ShouldBe("sin-borrador");

        // Editar crea un borrador nuevo (v2); la publicada no cambia.
        var v2 = flujo.EditarBorrador(null, Definicion(Etapa(1, En(_aprobador))), _areas, Ahora.AddDays(1));
        v2.Version.ShouldBe(2);
        v2.Estado.ShouldBe(EstadoFlujoVersion.Borrador);
        flujo.VersionPublicada!.Version.ShouldBe(1);
        flujo.VersionPublicada.Etapas.ShouldHaveSingleItem().Areas.Count.ShouldBe(2);

        // Editar otra vez reemplaza el mismo borrador.
        flujo.EditarBorrador("Habilitación v2", Definicion(Etapa(1, En(_cumplimiento))), _areas, Ahora.AddDays(2)).Version.ShouldBe(2);
        flujo.Versiones.Count.ShouldBe(2);
        flujo.Nombre.ShouldBe("Habilitación v2");

        flujo.Publicar(_areas, Ahora.AddDays(3));
        flujo.VersionPublicada!.Version.ShouldBe(2);
        flujo.Versiones.Single(v => v.Version == 1).Estado.ShouldBe(EstadoFlujoVersion.Retirada);
    }

    [Fact]
    public void Las_etapas_se_ordenan_y_numeran_de_1_a_n()
    {
        var validada = Definicion(Etapa(20, En(_cumplimiento)), Etapa(5, En(_aprobador))).Validar(_empresa, _areas);

        validada.Etapas.Select(e => e.Orden).ShouldBe([1, 2]);
        validada.Etapas[0].Areas.ShouldHaveSingleItem().AreaId.ShouldBe(_aprobador.Id);
    }

    [Fact]
    public void Valida_que_cada_area_exista_este_activa_y_sea_de_la_empresa()
    {
        var deOtra = Area.Crear(Guid.NewGuid(), "LEGAL", "Legal");
        var inactiva = Area.Crear(_empresa, "SST", "SST");
        inactiva.Actualizar("SST", false);

        Codigo(Definicion(Etapa(1, new AreaDeEtapa(Guid.NewGuid(), 1, false, false, null)))).ShouldBe("area-invalida");
        Should.Throw<ReglaDeNegocioException>(() => Definicion(Etapa(1, En(deOtra))).Validar(_empresa, [.. _areas, deOtra]))
            .Codigo.ShouldBe("area-invalida");
        Should.Throw<ReglaDeNegocioException>(() => Definicion(Etapa(1, En(inactiva))).Validar(_empresa, [.. _areas, inactiva]))
            .Codigo.ShouldBe("area-invalida");
    }

    [Fact]
    public void Rechaza_definiciones_invalidas()
    {
        Codigo(Definicion()).ShouldBe("etapas-requeridas");
        Codigo(Definicion(Etapa(1))).ShouldBe("etapa-sin-areas");
        Codigo(Definicion(Etapa(1, En(_aprobador)), Etapa(1, En(_cumplimiento)))).ShouldBe("orden-duplicado");
        Codigo(Definicion(Etapa(1, En(_aprobador)), Etapa(2, En(_aprobador)))).ShouldBe("area-repetida");
        Codigo(Definicion(Etapa(1, En(_aprobador, aprobadores: 0)))).ShouldBe("aprobadores-invalidos");
        Codigo(Definicion(Etapa(1, En(_aprobador, aprobadores: 11)))).ShouldBe("aprobadores-invalidos");
        Codigo(Definicion(Etapa(1, En(_aprobador, sla: 0)))).ShouldBe("sla-invalido");
        Codigo(Definicion(Etapa(1, En(_aprobador, rechaza: true, automatica: true)))).ShouldBe("area-automatica-no-rechaza");
        Should.Throw<ReglaDeNegocioException>(() => Flujo.Crear(_empresa, "1MAL", "x", Guid.NewGuid(), DefinicionDeFlujo.TodasEnParalelo(_areas),
            _areas, Ahora)).Codigo.ShouldBe("codigo-invalido");
    }

    [Fact]
    public void Un_area_automatica_cuenta_como_un_solo_aprobador()
    {
        var validada = Definicion(Etapa(1, En(_aprobador, aprobadores: 3, automatica: true))).Validar(_empresa, _areas);
        validada.Etapas[0].Areas[0].AprobadoresRequeridos.ShouldBe(1);
    }

    [Fact]
    public void Publicar_revalida_las_areas_por_si_se_desactivaron()
    {
        var flujo = Nuevo();
        _cumplimiento.Actualizar(_cumplimiento.Nombre, false);

        Should.Throw<ReglaDeNegocioException>(() => flujo.Publicar(_areas, Ahora)).Codigo.ShouldBe("area-invalida");
    }
}

public class MotivoTests
{
    [Fact]
    public void La_plataforma_trae_los_motivos_base_de_docs_03()
    {
        MotivosBase.Todos.Select(m => m.Codigo).ShouldBe(
        [
            "ilegible", "incompleto", "vencido", "no-corresponde-al-tercero", "datos-no-coinciden", "formato-no-vigente", "firma-faltante",
            "frescura-incumplida"
        ]);
        MotivosBase.Todos.ShouldAllBe(m => m.Base && m.AplicaA == AplicaMotivo.Ambos && m.TextoAlTercero.Length > 0);
        MotivosBase.Todos.Select(m => m.Id).Distinct().Count().ShouldBe(8);
    }

    [Fact]
    public void Un_motivo_propio_no_puede_usar_un_codigo_base_ni_un_codigo_invalido()
    {
        Should.Throw<ReglaDeNegocioException>(() => Motivo.Crear("Incompleto", "x", CategoriaMotivo.Otro, AplicaMotivo.Ambos, "x"))
            .Codigo.ShouldBe("motivo-duplicado");
        Should.Throw<ReglaDeNegocioException>(() => Motivo.Crear("con espacio", "x", CategoriaMotivo.Otro, AplicaMotivo.Ambos, "x"))
            .Codigo.ShouldBe("codigo-invalido");
        Should.Throw<ReglaDeNegocioException>(() => Motivo.Crear("poliza", "x", CategoriaMotivo.Otro, AplicaMotivo.Ambos, " "))
            .Codigo.ShouldBe("texto-requerido");
    }

    [Fact]
    public void Un_motivo_de_rechazo_no_sirve_para_observar()
    {
        var motivo = Motivo.Crear("Listas-Restrictivas", "Listas restrictivas", CategoriaMotivo.Otro, AplicaMotivo.Rechazo,
            "No es posible continuar con la validación.");

        motivo.Codigo.ShouldBe("listas-restrictivas");
        motivo.SirvePara(AplicaMotivo.Rechazo).ShouldBeTrue();
        motivo.SirvePara(AplicaMotivo.Observacion).ShouldBeFalse();
        Should.Throw<ReglaDeNegocioException>(() => MotivosBase.Todos[0].Actualizar("x", CategoriaMotivo.Otro, AplicaMotivo.Ambos, "x", null))
            .Codigo.ShouldBe("motivo-base");
    }
}
