using System.Security.Cryptography;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- integración abierta: referencias externas, contactos por empresa, datos de pago y embebido genérico (IO) ----------

    /// <summary>Un sistema integrador cualquiera (ni SRM ni Portal) con su API key de los permisos dados, para la empresa.</summary>
    private async Task<Credencial> SistemaNuevoAsync(Empresa empresa, string codigo, params string[] scopes)
    {
        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(empresa.Admin),
            new CrearSistemaIntegradoDto(codigo, "Sistema " + codigo, rsa.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(empresa.Admin),
            new CrearApiKeyDto("Integración " + codigo, scopes, null, null, SistemaId: sistema.Id), 201);
        return new Credencial(llave.Valor!, empresa.Datos.Id);
    }

    private static readonly string[] ScopesDeReferencias = ["terceros:leer", "terceros:escribir", "vinculos:leer", "vinculos:escribir"];

    private Task<TerceroDto> TerceroConReferenciaAsync(Credencial sistema, string nit, string? claveExterna, int estado = 201) =>
        ApiAsync<TerceroDto>("POST", "/api/v1/terceros", sistema, new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null, null,
            claveExterna is null ? null : new ReferenciaExternaDto(string.Empty, claveExterna)), estado);

    [Fact]
    public async Task Referencias_externas_del_tercero_y_del_vinculo_son_idempotentes_por_sistema_y_privadas_de_cada_tenant()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var erpNorte = await SistemaNuevoAsync(norte, "ERPX", ScopesDeReferencias);
        var erpSur = await SistemaNuevoAsync(sur, "ERPX", ScopesDeReferencias); // el mismo código en otro tenant: no se mezclan
        var crmNorte = await SistemaNuevoAsync(norte, "CRMX", ScopesDeReferencias);

        // 1. Crear el tercero con su clave en el sistema: 201; repetir la misma creación es idempotente (200, el mismo tercero).
        var nit = Nit();
        var tercero = await TerceroConReferenciaAsync(erpNorte, nit, "C-77");
        (await TerceroConReferenciaAsync(erpNorte, nit, "C-77", 200)).Id.ShouldBe(tercero.Id);
        (await ApiAsync<List<ReferenciaExternaDto>>("GET", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, null, 200))
            .ShouldBe([new ReferenciaExternaDto("ERPX", "C-77")]);

        // La clave ya es de otro tercero: 409. Una llave sin sistema integrador o de otro sistema no puede registrar la referencia.
        var ocupada = await ApiAsync("POST", "/api/v1/terceros", erpNorte, new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Otro", null, null,
            new ReferenciaExternaDto("ERPX", "C-77")), 409);
        (await CodigoAsync(ocupada)).ShouldBe("referencia-duplicada");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", new Credencial(norte.Admin), new ReferenciaExternaDto("ERPX", "C-1"), 422)))
            .ShouldBe("referencia-sin-sistema");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("CRMX", "C-1"), 422)))
            .ShouldBe("referencia-sistema-invalido");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "  "), 422)))
            .ShouldBe("referencia-externa-invalida");

        // 2. Un tercero puede tener varias claves en un sistema (p. ej. una por instancia); agregarla es 201 y repetirla, 200.
        var dos = await ApiAsync<List<ReferenciaExternaDto>>("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("erpx", "C-78"), 201);
        dos.ShouldBe([new ReferenciaExternaDto("ERPX", "C-77"), new ReferenciaExternaDto("ERPX", "C-78")]);
        (await ApiAsync<List<ReferenciaExternaDto>>("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "C-78"), 200)).Count.ShouldBe(2);
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=erpx&clave=C-78", erpNorte, null, 200)).Id.ShouldBe(tercero.Id);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=NO-EXISTE", erpNorte, null, 404))).ShouldBe("tercero-no-encontrado");
        // Otro sistema del mismo tenant tiene sus propias claves.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=CRMX&clave=C-78", crmNorte, null, 404))).ShouldBe("tercero-no-encontrado");
        var otroTercero = await TerceroConReferenciaAsync(erpNorte, Nit(), null);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{otroTercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "C-77"), 409))).ShouldBe("referencia-duplicada");

        // 3. Las referencias son de cada tenant: el mismo código de sistema en otro tenant no ve las de Norte ni choca con ellas.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpSur, null, 404))).ShouldBe("tercero-no-encontrado");
        await ApiAsync("GET", $"/api/v1/terceros/{tercero.Id}/referencias", erpSur, null, 404); // ni siquiera ve al tercero de Norte
        var deSur = await TerceroConReferenciaAsync(erpSur, Nit(), "C-77");
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpSur, null, 200)).Id.ShouldBe(deSur.Id);
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpNorte, null, 200)).Id.ShouldBe(tercero.Id);

        // 4. Vínculos: idempotente por referencia; otra clave del mismo sistema es conflicto; otro sistema comparte la validación.
        var correo = CorreoNuevo();
        var vinculo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", erpNorte, new CrearVinculoDto(TerceroId: tercero.Id, Tercero: null,
            TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: correo, ReferenciaExterna: new ReferenciaExternaDto("ERPX", "S-1")), 201);
        vinculo.Referencias.ShouldBe([new ReferenciaExternaDto("ERPX", "S-1")]);
        (await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", erpNorte, new CrearVinculoDto(TerceroId: tercero.Id, Tercero: null, TipoVinculo: "PROVEEDOR",
            Atributos: null, CorreoInvitacion: correo, ReferenciaExterna: new ReferenciaExternaDto("ERPX", "S-1")), 200)).Id.ShouldBe(vinculo.Id);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "S-2"), 409))).ShouldBe("referencia-en-conflicto");
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=erpx&clave=S-1", erpNorte, null, 200)).Id.ShouldBe(vinculo.Id);
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=ERPX&id=S-1", erpNorte, null, 200)).Id.ShouldBe(vinculo.Id); // el nombre anterior sigue valiendo

        var compartido = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", crmNorte, new ReferenciaExternaDto("CRMX", "K-9"), 200);
        compartido.Referencias.ShouldBe([new ReferenciaExternaDto("ERPX", "S-1"), new ReferenciaExternaDto("CRMX", "K-9")]);
        (await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", crmNorte, new ReferenciaExternaDto("CRMX", "K-9"), 200)).Referencias.Count.ShouldBe(2);
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=CRMX&clave=K-9", crmNorte, null, 200)).Id.ShouldBe(vinculo.Id);
    }
}
