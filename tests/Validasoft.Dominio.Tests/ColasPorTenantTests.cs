using Validasoft.Aplicacion.Ia;

namespace Validasoft.Dominio.Tests;

/// <summary>El grupo de cada mensaje de la cola de IA decide su carril: del mismo tenant, en orden y de a uno; de tenants distintos, en paralelo (OC-5).</summary>
public class ColasPorTenantTests
{
    [Fact]
    public void Los_mensajes_del_mismo_tenant_comparten_grupo_aunque_sean_de_documentos_distintos()
    {
        var tenant = Guid.NewGuid();

        var uno = AnalisisConIa.GrupoDe(new AnalizarVersionConIa(Guid.NewGuid(), 1, tenant));
        var otro = AnalisisConIa.GrupoDe(new AnalizarVersionConIa(Guid.NewGuid(), 3, tenant));

        uno.ShouldBe(otro);
        uno.ShouldBe(tenant.ToString("N"));
    }

    [Fact]
    public void Los_tenants_distintos_no_comparten_grupo()
    {
        AnalisisConIa.GrupoDe(new AnalizarVersionConIa(Guid.NewGuid(), 1, Guid.NewGuid()))
            .ShouldNotBe(AnalisisConIa.GrupoDe(new AnalizarVersionConIa(Guid.NewGuid(), 1, Guid.NewGuid())));
    }

    [Fact]
    public void Sin_tenant_el_grupo_es_el_documento_y_sus_mensajes_siguen_en_orden()
    {
        var documento = Guid.NewGuid();

        AnalisisConIa.GrupoDe(new AnalizarVersionConIa(documento, 1)).ShouldBe(documento.ToString("N"));
        AnalisisConIa.GrupoDe(new AnalizarVersionConIa(documento, 2)).ShouldBe(AnalisisConIa.GrupoDe(new AnalizarVersionConIa(documento, 1)));
    }
}
