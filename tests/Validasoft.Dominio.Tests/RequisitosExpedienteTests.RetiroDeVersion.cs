using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Tests;

public partial class RequisitosExpedienteTests
{
    // ---------- el tercero retiró la versión que el requisito tenía (RT-3) ----------

    [Fact]
    public void Un_documento_que_vuelve_atras_por_un_retiro_deja_el_motivo_en_el_requisito_y_una_version_nueva_lo_limpia()
    {
        var e = Abrir(Flujo(), Rut(V(_docRut, 8)), Camara(V(_docCamara, 1)));
        e.Requisitos.ShouldAllBe(r => r.Retiro == null);

        var retiro = new RetiroDeVersion(8, "Subí el RUT de otra empresa", Ahora);
        var retiradas = new Dictionary<(Guid, int), RetiroDeVersion> { [(_docRut, 8)] = retiro };

        var eventos = e.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 7) }, Ahora, retiradas: retiradas);
        var adjuntada = eventos.OfType<VersionAdjuntadaARequisito>().Single();
        adjuntada.NumeroAnterior.ShouldBe(8);
        adjuntada.Retiro.ShouldBe(retiro);

        var despues = Aplicar(e, eventos);
        var rut = despues.BuscarRequisito(_rut)!;
        rut.VersionActual!.Numero.ShouldBe(7);
        rut.Retiro.ShouldBe(retiro);
        despues.BuscarRequisito(_camara)!.Retiro.ShouldBeNull();

        // Una versión nueva que no reemplaza a una retirada (la 7 sigue vigente y llega la 9) no arrastra el motivo.
        var nueva = Aplicar(despues, despues.SincronizarVersiones(new Dictionary<Guid, VersionDeRequisito> { [_rut] = V(_docRut, 9) }, Ahora, retiradas: retiradas));
        nueva.BuscarRequisito(_rut)!.VersionActual!.Numero.ShouldBe(9);
        nueva.BuscarRequisito(_rut)!.Retiro.ShouldBeNull();
    }

    [Fact]
    public void Una_actualizacion_que_se_abre_por_un_retiro_nace_con_el_motivo_en_su_requisito()
    {
        var retiro = new RetiroDeVersion(8, "Era el RUT de otra empresa", Ahora);
        var e = Abrir(Flujo(), Rut(V(_docRut, 7)) with { Retiro = retiro });

        e.BuscarRequisito(_rut)!.Retiro.ShouldBe(retiro);
        e.BuscarRequisito(_rut)!.VersionActual!.Numero.ShouldBe(7);
    }
}
