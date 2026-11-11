using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Tests;

/// <summary>Desaprobar con requisitos (DS-1, DS-2): los veredictos por documento de las áreas devueltas dejan de contar.</summary>
public partial class RequisitosExpedienteTests
{
    private const string Admin = "admin-1";

    /// <summary>Compras (etapa 1) y Cumplimiento (etapa 2); Compras revisa el RUT, Cumplimiento el RUT y la Cámara; todo aprobado.</summary>
    private Expediente AprobadoEnSecuencia()
    {
        var e = Abrir(Flujo(enSecuencia: true), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        e = Aprobar(e, _rut, _compras, _ana);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e = Aprobar(e, _rut, _cumplimiento, _beto);
        e = Aprobar(e, _camara, _cumplimiento, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
        return e;
    }

    [Fact]
    public void Reabrir_la_primera_etapa_invalida_los_veredictos_por_documento_de_todas_las_etapas_devueltas()
    {
        var e = Abrir(Flujo(enSecuencia: true), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        e = Aprobar(e, _rut, _compras, _ana); // etapa 1 aprobada; Cumplimiento activa
        e = Aprobar(e, _rut, _cumplimiento, _beto);

        var eventos = e.ReabrirEtapa(1, Admin, "Compras aprobó sin ver el RUT completo.", Ahora.AddDays(1));
        eventos.Select(x => x.GetType().Name).ShouldBe(["EtapaReabierta", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.EtapaActiva!.Orden.ShouldBe(1);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);
        var rut = e.BuscarRequisito(_rut)!;
        rut.Estado.ShouldBe(EstadoRequisito.EnRevision);
        rut.Veredictos.ShouldAllBe(v => !v.Vigente && v.Revertido);
        rut.Veredictos.Count.ShouldBe(2); // quedan en el historial: el de Compras y el de Cumplimiento

        // Compras vuelve a aprobar el RUT y Cumplimiento decide de nuevo lo suyo: el veredicto anterior no cuenta.
        e = Aprobar(e, _rut, _compras, _ana);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.BuscarRequisito(_rut)!.Estado.ShouldBe(EstadoRequisito.EnRevision);
        e = Aprobar(e, _rut, _cumplimiento, _beto);
        e = Aprobar(e, _camara, _cumplimiento, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Desaprobar_un_expediente_aprobado_con_requisitos_revierte_solo_lo_de_las_etapas_devueltas()
    {
        var e = AprobadoEnSecuencia();

        e = Aplicar(e, e.Desaprobar(null, Admin, "El certificado de la Cámara tiene un error.", Ahora.AddDays(30)));

        e.Estado.ShouldBe(EstadoExpediente.EnRevision);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada); // la etapa 1 sigue aprobada
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Pendiente);
        var rut = e.BuscarRequisito(_rut)!;
        rut.Veredictos.Where(v => v.AreaId == _compras).ShouldAllBe(v => v.Vigente && !v.Revertido);
        rut.Veredictos.Where(v => v.AreaId == _cumplimiento).ShouldAllBe(v => !v.Vigente && v.Revertido);
        rut.Estado.ShouldBe(EstadoRequisito.EnRevision);
        e.BuscarRequisito(_camara)!.Estado.ShouldBe(EstadoRequisito.EnRevision);

        e = Aprobar(e, _rut, _cumplimiento, _beto);
        e = Aprobar(e, _camara, _cumplimiento, _beto);
        e.Estado.ShouldBe(EstadoExpediente.Aprobado);
    }

    [Fact]
    public void Volver_a_la_fase_inicial_con_requisitos_deja_todos_los_documentos_en_revision_desde_la_primera_etapa()
    {
        var e = AprobadoEnSecuencia();

        var eventos = e.VolverAFaseInicial(Admin, "Cambió la política.", Ahora.AddDays(30));
        eventos.Select(x => x.GetType().Name).ShouldBe(["ExpedienteDesaprobado", "EtapaActivada"]);
        e = Aplicar(e, eventos);

        e.EtapaActiva!.Orden.ShouldBe(1);
        e.Requisitos.ShouldAllBe(r => r.Estado == EstadoRequisito.EnRevision && r.Veredictos.All(v => !v.Vigente && v.Revertido));
        e.BuscarArea(_cumplimiento)!.Estado.ShouldBe(EstadoAreaExpediente.Esperando);
    }

    [Fact]
    public void Una_area_automatica_con_requisitos_vuelve_a_aprobar_sola_al_reabrir_su_etapa()
    {
        var e = Abrir(Flujo(comprasAutomatica: true, enSecuencia: true), Rut(V(_docRut, 1)), Camara(V(_docCamara, 1)));
        e.EtapaActiva!.Orden.ShouldBe(2); // Compras (automática) aprobó el RUT sola

        e = Aplicar(e, e.ReabrirEtapa(1, Admin, "Revisar la etapa automática.", Ahora.AddDays(1)));

        e.BuscarArea(_compras)!.Estado.ShouldBe(EstadoAreaExpediente.Aprobada);
        e.EtapaActiva!.Orden.ShouldBe(2);
        e.BuscarRequisito(_rut)!.Veredictos.Count(v => v.Vigente && v.AreaId == _compras).ShouldBe(1);
    }
}
