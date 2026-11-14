using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Tests;

// Exención del vínculo en el expediente (EX-3): lo que la empresa tiene eximido para el vínculo nace Eximido, citando la exención.
public partial class RequisitosExpedienteTests
{
    private readonly Guid _exencion = Guid.NewGuid();

    private ExencionAplicada Exencion() => new(_exencion, "El RUT lo gestiona Contabilidad.", "usuario-admin", Ahora);

    [Fact]
    public void Un_requisito_que_el_vinculo_tiene_eximido_nace_eximido_citando_la_exencion_y_no_frena_la_revision()
    {
        // El RUT está eximido por el vínculo; la Cámara ya está cargada: todo lo que se pide está resuelto y el expediente va directo a revisión.
        var e = Abrir(Flujo(), out var eventos, Rut() with { Exencion = Exencion() }, Camara(V(_docCamara, 1)));

        var rut = e.BuscarRequisito(_rut)!;
        rut.Eximido.ShouldBeTrue();
        rut.Estado.ShouldBe(EstadoRequisito.Eximido);
        rut.ExencionDeVinculoId.ShouldBe(_exencion);
        (rut.EximidoPor, rut.JustificacionExencion, rut.EximidoEn).ShouldBe(("usuario-admin", "El RUT lo gestiona Contabilidad.", Ahora));
        rut.VersionActual.ShouldBeNull(); // nadie lo cargó ni lo revisa
        e.ListoParaRevision.ShouldBeTrue();
        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        eventos.OfType<RequisitoEximido>().ShouldBeEmpty(); // nace eximido: no es un evento aparte

        // Compras solo revisaba el RUT: sin nada que decidir queda aprobada; Cumplimiento decide solo la Cámara.
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.EnRevision);
        Aprobar(e, _camara, _cumplimiento, _ana).Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Lo_eximido_por_el_vinculo_no_cuenta_como_falta_aunque_sea_obligatorio_y_no_tenga_version()
    {
        var e = Abrir(Flujo(), Rut() with { Exencion = Exencion() }, Camara());

        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Eximido);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.Pendiente);
        e.Estado.ShouldBe(EstadoExpediente.Abierto); // espera la Cámara, no el RUT
        e.ListoParaRevision.ShouldBeFalse();

        // Llega la Cámara y entra a revisión sin que nadie le pida el RUT.
        Adjuntar(e, _camara, V(_docCamara, 1)).Estado.ShouldBe(EstadoExpediente.EnRevision);
    }

    [Fact]
    public void Un_expediente_cuyos_requisitos_estan_todos_eximidos_por_el_vinculo_se_aprueba_al_abrir()
    {
        var e = Abrir(Flujo(), Rut() with { Exencion = Exencion() }, Camara() with { Exencion = Exencion() });

        e.Requisitos.ShouldAllBe(r => r.Estado == EstadoRequisito.Eximido && r.ExencionDeVinculoId == _exencion);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Una_version_nueva_no_adjunta_a_un_requisito_eximido_por_el_vinculo()
    {
        var e = Abrir(Flujo(), Rut() with { Exencion = Exencion() }, Camara(V(_docCamara, 1)));

        // La bóveda comparte una versión del RUT: el requisito eximido no se toca (sin versión, sin veredictos, sin revisión).
        var despues = Adjuntar(e, _rut, V(_docRut, 1));

        despues.BuscarRequisito(_rut)!.VersionActual.ShouldBeNull();
        despues.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.Eximido);
    }

    [Fact]
    public void Eximir_con_el_expediente_abierto_por_una_exencion_del_vinculo_deja_la_cita_en_el_evento_y_en_el_requisito()
    {
        var e = Abrir(Flujo(), Rut(V(_docRut, 1)), Camara());

        var eventos = e.EximirRequisito(_camara, "usuario-admin", "Exención del vínculo: la Cámara la gestiona Legal.", Ahora, _exencion);
        var eximido = eventos.OfType<RequisitoEximido>().ShouldHaveSingleItem();
        (eximido.RequisitoId, eximido.Por, eximido.ExencionDeVinculoId).ShouldBe((_camara, "usuario-admin", _exencion));

        var despues = Aplicar(e, eventos);
        var camara = despues.BuscarRequisito(_camara)!;
        (camara.Eximido, camara.ExencionDeVinculoId, camara.JustificacionExencion).ShouldBe((true, _exencion, "Exención del vínculo: la Cámara la gestiona Legal."));
        despues.Estado.ShouldBe(EstadoExpediente.EnRevision); // era lo único que faltaba cargar

        // Eximir a mano, como siempre, no lleva la cita de ninguna exención del vínculo.
        var aMano = Aplicar(e, e.EximirRequisito(_camara, "usuario-admin", "Lo exime el administrador.", Ahora));
        aMano.BuscarRequisito(_camara)!.ExencionDeVinculoId.ShouldBeNull();
    }
}
