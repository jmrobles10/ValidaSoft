using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- auditoría (AU-1) ----------

    [Fact]
    public async Task La_auditoria_muestra_los_cambios_de_configuracion_con_su_diff_y_las_descargas_de_la_empresa()
    {
        var a = await ProveedorAprobadoAsync();
        var norte = a.E.Norte;
        async Task<List<RegistroAuditoriaDto>> Auditoria(string consulta = "") =>
            [.. (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", $"/api/v1/auditoria{consulta}", Admin(norte), null, 200)).Datos];

        // Configuración: el alta del área y el cambio de miembros, con el campo que cambió.
        var compras = a.E.Compras.Id.ToString();
        var delArea = await Auditoria($"?entidad=Area&entidadId={compras}");
        delArea.Select(r => r.Accion).ShouldBe(["Modificado", "Creado"]); // lo más reciente primero
        var alta = delArea[1];
        alta.Tipo.ShouldBe("Configuracion");
        alta.Entidad.Nombre.ShouldBe("Compras");
        alta.EmpresaId.ShouldBe(norte.Datos.Id);
        alta.Actor.Tipo.ShouldBe("Integracion");
        alta.Diff.ShouldContain(d => d.Campo == "codigo" && d.TipoCambio == "Agregado");
        var miembros = delArea[0].Diff.ShouldHaveSingleItem();
        miembros.Campo.ShouldBe("miembros");
        miembros.TipoCambio.ShouldBe("Modificado");

        // Lo de todo el grupo (los usuarios) también se ve desde la empresa, sin empresa propia.
        var usuario = (await Auditoria("?entidad=Usuario")).First(r => r.Entidad.Id == a.E.Laura.Usuario.Id.ToString());
        usuario.EmpresaId.ShouldBeNull();
        usuario.Entidad.Nombre.ShouldBe("Laura Compras");
        (await Auditoria($"?entidad=Area&desde={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("O"))}")).ShouldBeEmpty();

        // Accesos: la descarga de una versión queda con el documento, la versión y el vínculo.
        (await Auditoria("?tipo=Acceso")).ShouldBeEmpty();
        var documento = (await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}/documentos", Admin(norte), null, 200))
            .First(d => d.Tipo.Codigo == "RUT");
        await ApiAsync<DescargaDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/documentos/{documento.DocumentoId}/versiones/1/descarga",
            Admin(norte), null, 200);
        var acceso = (await Auditoria("?tipo=acceso")).ShouldHaveSingleItem();
        acceso.Tipo.ShouldBe("Acceso");
        acceso.Accion.ShouldBe("Descargado");
        acceso.Entidad.Tipo.ShouldBe("Documento");
        acceso.Entidad.Id.ShouldBe(documento.DocumentoId.ToString());
        acceso.Entidad.Nombre.ShouldNotBeNull().ShouldEndWith("versión 1");
        acceso.VinculoId.ShouldBe(a.Vinculo.Id);
        (await Auditoria($"?tipo=Acceso&entidadId={a.Vinculo.Id}")).ShouldHaveSingleItem();
        (await Auditoria("?tipo=Acceso&entidad=Certificado")).ShouldBeEmpty();

        // Tipos que no se consultan aquí y permisos.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/auditoria?tipo=Dominio", Admin(norte), null, 422))).ShouldBe("tipo-no-disponible");
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/auditoria?tipo=Otro", Admin(norte), null, 422))).ShouldBe("tipo-invalido");
        await ApiAsync("GET", "/api/v1/auditoria", Delegado(norte, a.E.Laura), null, 403); // Revisor: sin auditoria:leer
    }
}
