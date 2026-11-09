using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- ficha del tercero (Tercero 360) ----------

    [Fact]
    public async Task La_ficha_del_tercero_muestra_su_vinculo_con_la_empresa_y_de_las_otras_solo_que_existe()
    {
        var a = await ProveedorAprobadoAsync();
        var terceroId = a.Vinculo.TerceroId;
        var ruta = $"/api/v1/terceros/{terceroId}/ficha";

        var ficha = await ApiAsync<FichaTerceroDto>("GET", ruta, Admin(a.E.Norte), null, 200);
        ficha.Tercero.Id.ShouldBe(terceroId);
        ficha.OtrasEmpresasDelGrupo.ShouldBeEmpty();
        var vinculo = ficha.Vinculos.ShouldHaveSingleItem();
        vinculo.Vinculo.Id.ShouldBe(a.Vinculo.Id);
        vinculo.Vinculo.Estado.ShouldBe("Activo");
        vinculo.Certificado.ShouldNotBeNull().Actual.ShouldNotBeNull().Estado.ShouldBe("Vigente");
        vinculo.CuentaDesignada.Actual.ShouldBeNull();
        vinculo.Documentos.ShouldNotBeNull().Select(d => d.Tipo.Codigo).ShouldBe(["RUT", "CAMARA_COMERCIO"], ignoreOrder: true);

        // Otra empresa del grupo sin vínculo con el tercero no ve su ficha (T-4).
        var sur = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(a.E.Norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null), 201);
        var adminSur = new Credencial(a.E.Norte.Admin, sur.Id);
        (await CodigoAsync(await ApiAsync("GET", ruta, adminSur, null, 404))).ShouldBe("tercero-no-encontrado");

        // Cuando Sur lo vincula, cada empresa ve el suyo y del otro solo el estado.
        var deSur = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", adminSur, new CrearVinculoDto(
            TerceroId: terceroId, Tercero: null, TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: CorreoNuevo(), ReferenciaExterna: null,
            EnviarInvitacion: false), 201);
        var otra = (await ApiAsync<FichaTerceroDto>("GET", ruta, Admin(a.E.Norte), null, 200)).OtrasEmpresasDelGrupo.ShouldHaveSingleItem();
        otra.EmpresaId.ShouldBe(sur.Id);
        otra.Estado.ShouldBe(deSur.Estado);
        var vistaDeSur = await ApiAsync<FichaTerceroDto>("GET", ruta, adminSur, null, 200);
        vistaDeSur.Vinculos.ShouldHaveSingleItem().Vinculo.Id.ShouldBe(deSur.Id);
        vistaDeSur.OtrasEmpresasDelGrupo.ShouldHaveSingleItem().Estado.ShouldBe("Activo");

        // Sin certificados:leer ni documentos:leer, esas secciones vienen nulas (no vacías).
        var lector = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(a.E.Norte.Admin),
            new CrearApiKeyDto("Solo terceros y vínculos", ["terceros:leer", "vinculos:leer"], null, null), 201);
        var restringida = (await ApiAsync<FichaTerceroDto>("GET", ruta, new Credencial(lector.Valor!, a.E.Norte.Datos.Id), null, 200))
            .Vinculos.ShouldHaveSingleItem();
        restringida.Certificado.ShouldBeNull();
        restringida.Documentos.ShouldBeNull();

        (await CodigoAsync(await ApiAsync("GET", $"/api/v1/terceros/{Guid.NewGuid()}/ficha", Admin(a.E.Norte), null, 404)))
            .ShouldBe("tercero-no-encontrado");
    }
}
