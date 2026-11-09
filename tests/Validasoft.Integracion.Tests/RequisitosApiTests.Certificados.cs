using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- lista de certificados (CE-11) ----------

    [Fact]
    public async Task La_lista_de_certificados_filtra_por_estado_y_texto_y_marca_los_bloqueantes()
    {
        var a = await ProveedorAprobadoAsync();
        var vinculo = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}", Admin(a.E.Norte), null, 200);
        async Task<List<FilaCertificadoDto>> Lista(string consulta = "") =>
            [.. (await ApiAsync<PaginaDto<FilaCertificadoDto>>("GET", $"/api/v1/certificados{consulta}", Admin(a.E.Norte), null, 200)).Datos];

        var fila = (await Lista()).ShouldHaveSingleItem();
        fila.VinculoId.ShouldBe(a.Vinculo.Id);
        fila.Estado.ShouldBe("Vigente");
        fila.Bloqueante.ShouldBeFalse();
        fila.Emision.ShouldBe(a.Emision);
        fila.Folio.ShouldEndWith($"-{a.Emision}");
        fila.TipoVinculo.Codigo.ShouldBe("PROVEEDOR");
        fila.VigenteHasta.ShouldNotBeNull();

        (await Lista($"?q={Uri.EscapeDataString(fila.Tercero.Nombre[..6])}")).ShouldHaveSingleItem();
        (await Lista($"?q={Uri.EscapeDataString(fila.Folio)}")).ShouldHaveSingleItem();
        (await Lista("?q=no-existe")).ShouldBeEmpty();
        (await Lista("?estado=Vencido")).ShouldBeEmpty();
        (await Lista("?estado=Bloqueante")).ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/certificados?estado=Caducado", Admin(a.E.Norte), null, 422))).ShouldBe("estado-invalido");

        // Al bloquear el vínculo, el certificado queda suspendido y bloqueante.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/bloquear", Admin(a.E.Norte), new BloquearVinculoDto("Revisión de fraude", null), 200);
        var suspendido = (await Lista("?estado=Bloqueante")).ShouldHaveSingleItem();
        suspendido.Estado.ShouldBe("Suspendido");
        suspendido.Bloqueante.ShouldBeTrue();
        (await Lista("?estado=suspendido")).ShouldHaveSingleItem();
        (await Lista("?estado=Vigente")).ShouldBeEmpty();
    }
}
