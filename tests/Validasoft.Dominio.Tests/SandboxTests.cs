using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>El sandbox como tenant hermano (SB-1…SB-5): nombres, claves de tercero, prefijos de folio, buzón y permiso propio.</summary>
public sealed class SandboxTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 10, 9, 0, 0, TimeSpan.FromHours(-5));

    private static Tenant Real() => Tenant.Crear("acme", "Grupo Acme", "co", "America/Bogota", Ahora);

    private static Empresa EmpresaReal(string prefijo = "NORTE") =>
        Empresa.Crear(Guid.NewGuid(), Identificacion.Crear("CO", "NIT", "800197268", "4"), "Constructora Norte S.A.S.", prefijo, null, "soporte@norte.co", Ahora);

    [Fact]
    public void El_tenant_sandbox_es_hermano_del_de_produccion_y_su_slug_no_choca_con_ninguno_real()
    {
        var real = Real();
        var sandbox = Tenant.CrearSandbox(real, Ahora);

        sandbox.EsSandbox.ShouldBeTrue();
        real.EsSandbox.ShouldBeFalse();
        sandbox.TenantDeProduccionId.ShouldBe(real.Id);
        sandbox.Id.ShouldNotBe(real.Id);
        (sandbox.Region, sandbox.ZonaHoraria, sandbox.Plan).ShouldBe((real.Region, real.ZonaHoraria, real.Plan));
        sandbox.Nombre.ShouldBe("Grupo Acme (sandbox)");
        sandbox.Slug.ShouldBe("acme~sandbox");
        // Ningún slug que un administrador pueda escribir contiene «~».
        Should.Throw<ReglaDeNegocioException>(() => Tenant.Crear(sandbox.Slug, "Otro", null, null, Ahora)).Codigo.ShouldBe("slug-invalido");
        Should.Throw<ReglaDeNegocioException>(() => Tenant.CrearSandbox(sandbox, Ahora)).Codigo.ShouldBe("sandbox-de-sandbox");
    }

    [Fact]
    public void La_copia_de_pruebas_de_una_empresa_conserva_su_identificacion_y_marca_pero_no_su_nombre_ni_su_prefijo()
    {
        var real = EmpresaReal();
        real.ActualizarMarca(null, "#E07A1F", null);
        var sandbox = Tenant.CrearSandbox(Real(), Ahora);

        var copia = Empresa.CrearCopiaDeSandbox(real, sandbox.Id, "TNORTE", Ahora);

        copia.Id.ShouldNotBe(real.Id);
        (copia.TenantId, copia.EmpresaDeProduccionId).ShouldBe((sandbox.Id, (Guid?)real.Id));
        (copia.ClaveIdentificacion, copia.ReplyTo, copia.ColorMarca).ShouldBe((real.ClaveIdentificacion, "soporte@norte.co", "#E07A1F"));
        copia.Nombre.ShouldBe("Constructora Norte S.A.S. (sandbox)");
        copia.PrefijoFolio.ShouldBe("TNORTE");

        real.ActualizarMarca("nuevo@norte.co", null, null);
        copia.SincronizarCopiaDeSandbox(real);
        (copia.ReplyTo, copia.PrefijoFolio, copia.Nombre).ShouldBe(("nuevo@norte.co", "TNORTE", "Constructora Norte S.A.S. (sandbox)"));
    }

    [Fact]
    public void El_prefijo_de_folio_de_prueba_lleva_una_T_y_evita_los_ya_usados()
    {
        AislamientoDeSandbox.PrefijoDeFolio("NORTE", new HashSet<string>()).ShouldBe("TNORTE");
        AislamientoDeSandbox.PrefijoDeFolio("norte", new HashSet<string>()).ShouldBe("TNORTE");
        AislamientoDeSandbox.PrefijoDeFolio("ABCDEFGHIJ", new HashSet<string>()).ShouldBe("TABCDEFGHI"); // máximo 10
        AislamientoDeSandbox.PrefijoDeFolio("NORTE", new HashSet<string> { "TNORTE" }).ShouldBe("TNORTE0");
        AislamientoDeSandbox.PrefijoDeFolio("ABCDEFGHIJ", new HashSet<string> { "TABCDEFGHI", "TABCDEFGH0" }).ShouldBe("TABCDEFGH1");
    }

    [Fact]
    public void Un_tercero_de_pruebas_lleva_el_sandbox_en_su_clave_y_no_choca_con_el_real()
    {
        var identificacion = Identificacion.Crear("CO", "NIT", "900123456", "8");
        var real = Tercero.Crear(identificacion, TipoPersona.Juridica, "Aceros S.A.S.", null, null, Ahora);
        var deSandbox = Tercero.Crear(identificacion, TipoPersona.Juridica, "Aceros S.A.S.", null, null, Ahora);
        var sandboxId = Guid.NewGuid();

        deSandbox.AislarEnSandbox(sandboxId);

        real.ClaveIdentificacion.ShouldBe(identificacion.Clave);
        real.SandboxTenantId.ShouldBeNull();
        deSandbox.SandboxTenantId.ShouldBe(sandboxId);
        deSandbox.ClaveIdentificacion.ShouldBe($"sbx:{sandboxId:N}|{identificacion.Clave}");
        deSandbox.ClaveIdentificacion.ShouldNotBe(real.ClaveIdentificacion);
        deSandbox.Identificacion.Clave.ShouldBe(identificacion.Clave); // la identificación visible no cambia
        AislamientoDeSandbox.ClaveDeTercero(identificacion.Clave, null).ShouldBe(identificacion.Clave);
        AislamientoDeSandbox.ClaveDeTercero(identificacion.Clave, sandboxId).ShouldBe(deSandbox.ClaveIdentificacion);
        // Dos sandbox distintos tampoco se mezclan entre sí.
        AislamientoDeSandbox.ClaveDeTercero(identificacion.Clave, Guid.NewGuid()).ShouldNotBe(deSandbox.ClaveIdentificacion);
    }

    [Fact]
    public void El_buzon_de_pruebas_extrae_los_enlaces_del_correo()
    {
        var correo = CorreoDePrueba.De(" Ana@Proveedor.co ", "Te invitan", "Abre este enlace (vence hoy):\nhttps://valida.test/e/AbC-123_xyz.\n\nO entra a http://valida.test, gracias.",
            "<p>hola</p>", Ahora);

        correo.Para.ShouldBe("Ana@Proveedor.co");
        correo.Enlaces.ShouldBe(["https://valida.test/e/AbC-123_xyz", "http://valida.test"]);
        correo.RecibidoEn.ShouldBe(Ahora);
        CorreoDePrueba.De("a@b.co", "x", "sin enlaces", "", Ahora).Enlaces.ShouldBeEmpty();
    }

    [Fact]
    public void Las_llaves_de_ia_del_sandbox_se_reconocen_por_su_prefijo_y_son_una_llave_valida_para_el_plan()
    {
        AislamientoDeSandbox.EsLlaveDeIaDeSandbox(AislamientoDeSandbox.LlaveDeIa).ShouldBeTrue();
        AislamientoDeSandbox.EsLlaveDeIaDeSandbox("sk-proj-abcdefghijklmnopqrstuvwxyz").ShouldBeFalse();
        AislamientoDeSandbox.EsLlaveDeIaDeSandbox(null).ShouldBeFalse();
        Dominio.Ia.PlanDeIaEmpresa.ValidarLlave(AislamientoDeSandbox.LlaveDeIa).ShouldBe(AislamientoDeSandbox.LlaveDeIa);
    }

    [Fact]
    public void El_permiso_del_sandbox_es_de_administracion_del_tenant_y_no_lo_obtiene_un_administrador_de_una_empresa()
    {
        Permisos.DelTenant.ShouldContain(Permisos.SandboxAdministrar);
        Permisos.SoloDeTenant.ShouldContain(Permisos.SandboxAdministrar);

        var empresa = Guid.NewGuid();
        var deEmpresa = Permisos.ParaUsuario([new RolAsignado(Rol.Administrador, empresa)], Permisos.DelTenant, [empresa.ToString()]);
        deEmpresa.EnEmpresa(empresa).ShouldNotContain(Permisos.SandboxAdministrar);
        var deGrupo = Permisos.ParaUsuario([new RolAsignado(Rol.Administrador, null)], Permisos.DelTenant, ["*"]);
        deGrupo.DeTenant.ShouldContain(Permisos.SandboxAdministrar);
        // El revisor y el oficial de cumplimiento no lo tienen.
        Permisos.DeRol(Rol.Revisor).ShouldNotContain(Permisos.SandboxAdministrar);
        Permisos.DeRol(Rol.OficialCumplimiento).ShouldNotContain(Permisos.SandboxAdministrar);
    }

    [Fact]
    public void El_mensaje_de_correo_se_atribuye_al_tenant_sin_cambiar_lo_demas()
    {
        var tenant = Guid.NewGuid();
        var original = new MensajeDeCorreo("a@b.co", "Asunto", "texto", "<p>html</p>");
        original.TenantId.ShouldBeNull();

        var atribuido = original.DeTenant(tenant);

        atribuido.TenantId.ShouldBe(tenant);
        (atribuido.Para, atribuido.Asunto, atribuido.Texto, atribuido.Html).ShouldBe((original.Para, original.Asunto, original.Texto, original.Html));
        atribuido.DeTenant(null).TenantId.ShouldBeNull();
    }
}
