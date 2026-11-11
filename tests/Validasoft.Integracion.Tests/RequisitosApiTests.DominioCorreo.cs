using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Dominio de correo propio por empresa (OD-18…OD-22): registros DNS que debe crear la empresa, verificación con el proveedor simulado
/// (sin DNS ni proveedores reales) y remitente propio de los correos a terceros, con respaldo al remitente de la plataforma mientras
/// el dominio no esté verificado.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private string UrlDominio(Escenario e) => $"/api/v1/empresas/{e.Norte.Datos.Id}/dominio-correo";

    /// <summary>Invita a un tercero nuevo y devuelve el remitente con el que salió el correo (nulo = el de la plataforma).</summary>
    private async Task<Validasoft.Dominio.Configuracion.RemitenteDeCorreo?> RemitenteDeLaInvitacionAsync(Escenario e)
    {
        var correo = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        return (await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("te invita"))).Remitente;
    }

    [Fact]
    public async Task El_dominio_propio_se_registra_muestra_sus_registros_dns_y_solo_al_verificarse_los_correos_salen_desde_el_dominio_de_la_empresa()
    {
        var e = await PrepararAsync();

        // Sin dominio: nada configurado y el correo sale con el remitente de la plataforma.
        var sin = await ApiAsync<DominioDeCorreoDto>("GET", UrlDominio(e), Admin(e.Norte), null, 200);
        (sin.Configurado, sin.Estado, sin.Simulado, sin.RemitenteEnUso).ShouldBe((false, null, true, sin.RemitenteDeLaPlataforma));
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBeNull();

        // Se registra: queda Pendiente con SPF, DKIM y DMARC por crear; mientras tanto el respaldo es la plataforma.
        var pendiente = await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("Proveedores.Norte.CO"), 200);
        (pendiente.Configurado, pendiente.Dominio, pendiente.Estado, pendiente.ParteLocal).ShouldBe((true, "proveedores.norte.co", "Pendiente", "no-responder"));
        pendiente.Registros.Select(r => r.Finalidad).ShouldBe(["Spf", "Dkim", "Dmarc"]);
        pendiente.Registros.ShouldAllBe(r => r.Tipo == "TXT");
        pendiente.Registros.Single(r => r.Finalidad == "Spf").Valor.ShouldStartWith("v=spf1 include:");
        pendiente.Registros.Single(r => r.Finalidad == "Dkim").Nombre.ShouldBe("vs1._domainkey.proveedores.norte.co");
        pendiente.Registros.Single(r => r.Finalidad == "Dkim").Valor.ShouldStartWith("v=DKIM1; k=rsa; p=");
        pendiente.Registros.Single(r => r.Finalidad == "Dmarc").Nombre.ShouldBe("_dmarc.proveedores.norte.co");
        pendiente.RemitenteEnUso.ShouldBe(pendiente.RemitenteDeLaPlataforma);
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBeNull();

        // Se verifica (simulado: sin DNS ni proveedor reales): Verificado, y los correos de la empresa salen desde su dominio con su nombre.
        var verificado = await ApiAsync<DominioDeCorreoDto>("POST", $"{UrlDominio(e)}/verificar", Admin(e.Norte), null, 200);
        (verificado.Estado, verificado.VerificadoEn.HasValue).ShouldBe(("Verificado", true));
        verificado.Verificacion.ShouldAllBe(c => c.Correcto && c.Encontrado != null);
        verificado.RemitenteEnUso.ShouldBe("Norte S.A.S. <no-responder@proveedores.norte.co>");
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBe(new Validasoft.Dominio.Configuracion.RemitenteDeCorreo("Norte S.A.S.", "no-responder@proveedores.norte.co"));

        // Cambiar el nombre y la parte local conserva la verificación.
        var renombrado = await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("proveedores.norte.co", "compras", "Compras Norte"), 200);
        (renombrado.Estado, renombrado.RemitenteEnUso).ShouldBe(("Verificado", "Compras Norte <compras@proveedores.norte.co>"));
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBe(new Validasoft.Dominio.Configuracion.RemitenteDeCorreo("Compras Norte", "compras@proveedores.norte.co"));

        // Quitarlo devuelve los correos al remitente de la plataforma.
        var quitado = await ApiAsync<DominioDeCorreoDto>("DELETE", UrlDominio(e), Admin(e.Norte), null, 200);
        (quitado.Configurado, quitado.RemitenteEnUso).ShouldBe((false, quitado.RemitenteDeLaPlataforma));
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBeNull();
        (await CodigoAsync(await ApiAsync("DELETE", UrlDominio(e), Admin(e.Norte), null, 404))).ShouldBe("dominio-sin-registrar");
        (await CodigoAsync(await ApiAsync("POST", $"{UrlDominio(e)}/verificar", Admin(e.Norte), null, 409))).ShouldBe("dominio-sin-registrar");

        // Todo quedó en la auditoría de la empresa.
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(e.Norte.Datos.TenantId.ToString("N"));
        var registros = await consulta.Query<RegistroDeCambioDeConfiguracion>().Where(r => r.Entidad == "DominioDeCorreo" && r.EntidadId == e.Norte.Datos.Id.ToString()).ToListAsync();
        registros.Count.ShouldBe(4); // registrar, verificar, renombrar y quitar
        registros.Count(r => r.Accion == AccionConfiguracion.Creado).ShouldBe(1);
    }

    [Fact]
    public async Task Un_dominio_sin_registros_dns_queda_fallido_con_lo_que_se_encontro_y_el_correo_sigue_saliendo_por_la_plataforma()
    {
        var e = await PrepararAsync();

        await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("sin-dns.norte.co"), 200);
        var sinDns = await ApiAsync<DominioDeCorreoDto>("POST", $"{UrlDominio(e)}/verificar", Admin(e.Norte), null, 200);
        sinDns.Estado.ShouldBe("Fallido");
        sinDns.Verificacion.Select(v => (v.Finalidad, v.Correcto, v.Encontrado)).ShouldBe([("Spf", false, null), ("Dkim", false, null), ("Dmarc", false, null)]);
        sinDns.RemitenteEnUso.ShouldBe(sinDns.RemitenteDeLaPlataforma);
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBeNull();

        // Con SPF y DMARC pero sin DKIM tampoco: la firma es obligatoria.
        await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("dkim-pendiente.norte.co"), 200);
        var sinDkim = await ApiAsync<DominioDeCorreoDto>("POST", $"{UrlDominio(e)}/verificar", Admin(e.Norte), null, 200);
        sinDkim.Estado.ShouldBe("Fallido");
        sinDkim.Verificacion.Select(v => (v.Finalidad, v.Correcto)).ShouldBe([("Spf", true), ("Dkim", false), ("Dmarc", true)]);
        (await RemitenteDeLaInvitacionAsync(e)).ShouldBeNull();

        // Un dominio bien configurado lo deja verificado aunque antes haya fallado.
        await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("norte.co"), 200);
        (await ApiAsync<DominioDeCorreoDto>("POST", $"{UrlDominio(e)}/verificar", Admin(e.Norte), null, 200)).Estado.ShouldBe("Verificado");
    }

    [Fact]
    public async Task El_dominio_valida_lo_escrito_solo_lo_administra_quien_puede_y_cada_empresa_ve_solo_el_suyo()
    {
        var e = await PrepararAsync();
        var otra = await PrepararAsync();

        foreach (var invalido in new[] { "localhost", "http://norte.co", "192.168.0.10", "sin espacios.co" })
            (await CodigoAsync(await ApiAsync("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto(invalido), 422))).ShouldBe("dominio-invalido");
        (await CodigoAsync(await ApiAsync("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("norte.co", "a b"), 422))).ShouldBe("parte-local-invalida");
        (await CodigoAsync(await ApiAsync("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("norte.co", null, "Norte <x@y.co>"), 422))).ShouldBe("nombre-remitente-invalido");

        // El revisor delegado no administra empresas; la empresa de otro grupo no existe para esta llave.
        await ApiAsync("PUT", UrlDominio(e), Delegado(e.Norte, e.Laura), new ActualizarDominioDeCorreoDto("norte.co"), 403);
        await ApiAsync("GET", UrlDominio(otra), Admin(e.Norte), null, 404);
        await ApiAsync("PUT", UrlDominio(otra), Admin(e.Norte), new ActualizarDominioDeCorreoDto("norte.co"), 404);

        await ApiAsync<DominioDeCorreoDto>("PUT", UrlDominio(e), Admin(e.Norte), new ActualizarDominioDeCorreoDto("norte.co"), 200);
        (await ApiAsync<DominioDeCorreoDto>("GET", UrlDominio(otra), Admin(otra.Norte), null, 200)).Configurado.ShouldBeFalse();
    }
}
