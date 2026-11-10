using System.Security.Cryptography;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Dominio.Tests;

public class SistemaIntegradoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    private static string ClaveRsa(int bits)
    {
        using var rsa = RSA.Create(bits);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }

    private static string ClaveEc(ECCurve curva)
    {
        using var ec = ECDsa.Create(curva);
        return ec.ExportSubjectPublicKeyInfoPem();
    }

    [Fact]
    public void Acepta_RSA_2048_y_EC_P256_con_codigo_en_mayusculas()
    {
        var srm = SistemaIntegrado.Crear(" srm ", "SRM de ADPRO", ClaveRsa(2048), Ahora);
        var marco = SistemaIntegrado.Crear("MARCO", "Marco ERP", ClaveEc(ECCurve.NamedCurves.nistP256), Ahora);

        srm.Codigo.ShouldBe("SRM");
        srm.Activo.ShouldBeTrue();
        marco.ClavePublicaPem.ShouldStartWith("-----BEGIN PUBLIC KEY-----");
    }

    [Fact]
    public void Rechaza_RSA_corta_otras_curvas_claves_privadas_y_texto_que_no_es_PEM()
    {
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("SRM", "SRM", ClaveRsa(1024), Ahora))
            .Codigo.ShouldBe("clave-publica-invalida");
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("SRM", "SRM", ClaveEc(ECCurve.NamedCurves.nistP384), Ahora))
            .Codigo.ShouldBe("clave-publica-invalida");

        using var rsa = RSA.Create(2048);
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("SRM", "SRM", rsa.ExportPkcs8PrivateKeyPem(), Ahora))
            .Codigo.ShouldBe("clave-publica-invalida");
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("SRM", "SRM", "no-es-una-clave", Ahora))
            .Codigo.ShouldBe("clave-publica-invalida");
    }

    [Fact]
    public void Valida_el_codigo_y_el_nombre()
    {
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("1SRM", "SRM", ClaveRsa(2048), Ahora)).Codigo.ShouldBe("codigo-invalido");
        Should.Throw<ReglaDeNegocioException>(() => SistemaIntegrado.Crear("SRM", " ", ClaveRsa(2048), Ahora)).Codigo.ShouldBe("nombre-requerido");
    }

    [Fact]
    public void Rota_la_clave_y_un_sistema_desactivado_no_la_cambia()
    {
        var sistema = SistemaIntegrado.Crear("SRM", "SRM", ClaveRsa(2048), Ahora);
        var nueva = ClaveEc(ECCurve.NamedCurves.nistP256);

        sistema.RotarClavePublica(nueva);
        sistema.ClavePublicaPem.ShouldBe(nueva);

        sistema.Desactivar();
        Should.Throw<ReglaDeNegocioException>(() => sistema.RotarClavePublica(ClaveRsa(2048))).Codigo.ShouldBe("sistema-inactivo");
        Should.Throw<ReglaDeNegocioException>(sistema.Desactivar).Codigo.ShouldBe("sistema-inactivo");
    }

    [Theory]
    [InlineData("https://SRM.Cliente.co/", false, "https://srm.cliente.co")]
    [InlineData("https://srm.cliente.co:443", false, "https://srm.cliente.co")]
    [InlineData("https://srm.cliente.co:8443", false, "https://srm.cliente.co:8443")]
    [InlineData("https://bücher.co", false, "https://xn--bcher-kva.co")]
    [InlineData("https://10.0.0.5", false, "https://10.0.0.5")]
    [InlineData("http://localhost:5173", true, "http://localhost:5173")]
    [InlineData("http://127.0.0.1:4000/", true, "http://127.0.0.1:4000")]
    public void Normaliza_los_origenes_como_los_reporta_el_navegador(string origen, bool permitirHttpLocal, string esperado) =>
        OrigenWeb.Normalizar(origen, permitirHttpLocal).ShouldBe(esperado);

    [Theory]
    [InlineData("http://srm.cliente.co", true)]
    [InlineData("http://localhost:5173", false)]
    [InlineData("https://srm.cliente.co/compras", false)]
    [InlineData("https://srm.cliente.co?x=1", false)]
    [InlineData("https://srm.cliente.co#a", false)]
    [InlineData("https://ana@srm.cliente.co", false)]
    [InlineData("https://*.cliente.co", false)]
    [InlineData("http://localhost:*", true)]
    [InlineData("srm.cliente.co", false)]
    [InlineData("ftp://srm.cliente.co", false)]
    [InlineData("", false)]
    public void Rechaza_origenes_sin_https_con_ruta_o_comodines(string origen, bool permitirHttpLocal) =>
        Should.Throw<ReglaDeNegocioException>(() => OrigenWeb.Normalizar(origen, permitirHttpLocal)).Codigo.ShouldBe("origen-invalido");

    [Fact]
    public void Fija_origenes_sin_repetidos_hasta_veinte_y_no_en_un_sistema_inactivo()
    {
        var sistema = SistemaIntegrado.Crear("SRM", "SRM", ClaveRsa(2048), Ahora);
        sistema.FijarOrigenes(["https://a.co", "https://A.co/", "https://b.co"], permitirHttpLocal: false);
        sistema.OrigenesPermitidos.ShouldBe(["https://a.co", "https://b.co"]);

        Should.Throw<ReglaDeNegocioException>(() => sistema.FijarOrigenes(Enumerable.Range(0, 21).Select(i => $"https://s{i}.co"), false))
            .Codigo.ShouldBe("origenes-excesivos");
        sistema.OrigenesPermitidos.ShouldBe(["https://a.co", "https://b.co"]);

        sistema.Desactivar();
        Should.Throw<ReglaDeNegocioException>(() => sistema.FijarOrigenes([], false)).Codigo.ShouldBe("sistema-inactivo");
    }
}

public class UsuarioTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.FromHours(-5));

    [Fact]
    public void El_usuario_de_un_sistema_nace_sin_roles_con_su_identidad_y_correo_normalizado()
    {
        var sistema = Guid.NewGuid();
        var usuario = Usuario.CrearDesdeSistema(sistema, "u-42", "Laura Pérez", " Laura@Constructora.CO ", Ahora);

        usuario.Roles.ShouldBeEmpty();
        usuario.Correo.ShouldBe("laura@constructora.co");
        usuario.Identidades.ShouldBe([new IdentidadExterna(sistema, "u-42")]);
        usuario.UltimoAcceso.ShouldBe(Ahora);
    }

    [Fact]
    public void Rechaza_correos_invalidos() =>
        Should.Throw<ReglaDeNegocioException>(() => Usuario.Crear("Laura", "laura@@x.co", null, Ahora)).Codigo.ShouldBe("correo-invalido");

    [Fact]
    public void Una_sola_identidad_por_sistema_y_la_misma_es_idempotente()
    {
        var srm = Guid.NewGuid();
        var marco = Guid.NewGuid();
        var usuario = Usuario.CrearDesdeSistema(srm, "u-42", "Laura", "laura@x.co", Ahora);

        usuario.EnlazarIdentidad(srm, "u-42");
        usuario.EnlazarIdentidad(marco, "LP01");
        usuario.Identidades.Count.ShouldBe(2);

        Should.Throw<ReglaDeNegocioException>(() => usuario.EnlazarIdentidad(srm, "u-99")).Codigo.ShouldBe("identidad-en-conflicto");
    }

    [Fact]
    public void Asignar_roles_quita_duplicados_y_redundancias_y_responde_si_tiene_rol_en_la_empresa()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var usuario = Usuario.Crear("Laura", "laura@x.co", null, Ahora);

        usuario.AsignarRoles([new(Rol.Revisor, a), new(Rol.Revisor, a), new(Rol.Auditor, null), new(Rol.Auditor, b)]);

        usuario.Roles.ShouldBe([new RolAsignado(Rol.Revisor, a), new RolAsignado(Rol.Auditor, null)]);
        usuario.TieneRolEn(b).ShouldBeTrue(); // por el rol de todo el tenant

        usuario.AsignarRoles([new(Rol.Revisor, a)]);
        usuario.TieneRolEn(a).ShouldBeTrue();
        usuario.TieneRolEn(b).ShouldBeFalse();
    }

    [Fact]
    public void Un_usuario_desactivado_no_recibe_roles()
    {
        var usuario = Usuario.Crear("Laura", "laura@x.co", null, Ahora);
        usuario.Desactivar();

        Should.Throw<ReglaDeNegocioException>(() => usuario.AsignarRoles([new(Rol.Revisor, null)])).Codigo.ShouldBe("usuario-inactivo");
        Should.Throw<ReglaDeNegocioException>(usuario.Desactivar).Codigo.ShouldBe("usuario-inactivo");
    }
}

public class AreaTests
{
    [Fact]
    public void Normaliza_el_codigo_y_define_miembros_sin_repetir()
    {
        var area = Area.Crear(Guid.NewGuid(), "juridica", "Jurídica");
        var laura = Guid.NewGuid();

        area.DefinirMiembros([laura, laura]);

        area.Codigo.ShouldBe("JURIDICA");
        area.Miembros.ShouldBe([laura]);
    }

    [Fact]
    public void Valida_codigo_nombre_y_miembros()
    {
        Should.Throw<ReglaDeNegocioException>(() => Area.Crear(Guid.NewGuid(), "área legal", "Legal")).Codigo.ShouldBe("codigo-invalido");
        Should.Throw<ReglaDeNegocioException>(() => Area.Crear(Guid.NewGuid(), "LEGAL", "")).Codigo.ShouldBe("nombre-requerido");
        Should.Throw<ReglaDeNegocioException>(() => Area.Crear(Guid.NewGuid(), "LEGAL", "Legal").DefinirMiembros([Guid.Empty]))
            .Codigo.ShouldBe("miembro-invalido");
    }

    [Fact]
    public void Un_area_inactiva_no_cambia_sus_miembros()
    {
        var area = Area.Crear(Guid.NewGuid(), "LEGAL", "Legal");
        area.Actualizar("Jurídica", activa: false);

        area.Nombre.ShouldBe("Jurídica");
        Should.Throw<ReglaDeNegocioException>(() => area.DefinirMiembros([Guid.NewGuid()])).Codigo.ShouldBe("area-inactiva");
    }
}

public class PermisosTests
{
    private static readonly string[] Lecturas =
    [
        Permisos.ConfiguracionLeer, Permisos.TercerosLeer, Permisos.VinculosLeer, Permisos.FlujosLeer, Permisos.ExpedientesLeer,
        Permisos.DocumentosLeer, Permisos.DocumentosDescargar, Permisos.CertificadosLeer, Permisos.ConsultasLeer
    ];

    [Fact]
    public void Mapea_cada_rol_a_sus_scopes()
    {
        Permisos.DeRol(Rol.Administrador).ShouldBe(Permisos.DelTenant);
        Permisos.DeRol(Rol.Revisor).ShouldBe([.. Lecturas, Permisos.VinculosEscribir, Permisos.ExpedientesRevisar], ignoreOrder: true);
        Permisos.DeRol(Rol.OficialCumplimiento).ShouldBe(
            [.. Lecturas, Permisos.VinculosEscribir, Permisos.VinculosBloquear, Permisos.CuentasAprobar, Permisos.ExpedientesRevisar], ignoreOrder: true);
        Permisos.DeRol(Rol.Auditor).ShouldBe([.. Lecturas, Permisos.AuditoriaLeer], ignoreOrder: true);
    }

    [Fact]
    public void El_administrador_tiene_todos_los_permisos_de_flujos_y_expedientes_y_el_auditor_solo_lectura()
    {
        string[] nuevos =
        [
            Permisos.FlujosLeer, Permisos.FlujosEscribir, Permisos.ExpedientesLeer, Permisos.ExpedientesRevisar, Permisos.ExpedientesAdministrar
        ];
        nuevos.ShouldAllBe(n => Permisos.DelTenant.Contains(n));
        nuevos.ShouldAllBe(n => Permisos.DeRol(Rol.Administrador).Contains(n));
        Permisos.DeRol(Rol.Auditor).ShouldNotContain(Permisos.ExpedientesRevisar);
        Permisos.DeRol(Rol.Auditor).ShouldNotContain(Permisos.FlujosEscribir);
        Permisos.DeRol(Rol.Revisor).ShouldNotContain(Permisos.ExpedientesAdministrar);
        Permisos.DeRol(Rol.OficialCumplimiento).ShouldNotContain(Permisos.FlujosEscribir);
    }

    [Fact]
    public void El_permiso_efectivo_es_la_interseccion_de_la_llave_y_los_roles()
    {
        var efectivos = Permisos.ParaUsuario([new(Rol.OficialCumplimiento, null)], [Permisos.VinculosLeer, Permisos.VinculosEscribir, Permisos.AuditoriaLeer], ["*"]);

        efectivos.DeTenant.ShouldBe([Permisos.VinculosLeer, Permisos.VinculosEscribir], ignoreOrder: true);
        efectivos.Empresas.ShouldBe(["*"]);
    }

    [Fact]
    public void Un_usuario_sin_roles_no_tiene_scopes_ni_empresas()
    {
        var efectivos = Permisos.ParaUsuario([], Permisos.DelTenant, ["*"]);

        efectivos.DeTenant.ShouldBeEmpty();
        efectivos.PorEmpresa.ShouldBeEmpty();
        efectivos.Empresas.ShouldBeEmpty();
    }

    [Fact]
    public void Los_roles_por_empresa_solo_valen_en_esa_empresa_y_nunca_dan_administracion_del_tenant()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var efectivos = Permisos.ParaUsuario([new(Rol.Administrador, a), new(Rol.Auditor, null)], Permisos.DelTenant, ["*"]);

        efectivos.Empresas.ShouldBe(["*"]);
        efectivos.EnEmpresa(null).ShouldNotContain(Permisos.VinculosEscribir);
        efectivos.EnEmpresa(a).ShouldContain(Permisos.VinculosEscribir);
        efectivos.EnEmpresa(a).ShouldNotContain(Permisos.UsuariosAdministrar);
        efectivos.EnEmpresa(b).ShouldNotContain(Permisos.VinculosEscribir);
        efectivos.EnEmpresa(b).ShouldContain(Permisos.AuditoriaLeer);
    }

    [Fact]
    public void Las_empresas_efectivas_se_recortan_por_las_de_la_llave()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        Permisos.ParaUsuario([new(Rol.Revisor, a), new(Rol.Revisor, b)], Permisos.DelTenant, [a.ToString()]).Empresas.ShouldBe([a.ToString()]);
        Permisos.ParaUsuario([new(Rol.Revisor, null)], Permisos.DelTenant, [b.ToString()]).Empresas.ShouldBe([b.ToString()]);

        var fuera = Permisos.ParaUsuario([new(Rol.Revisor, b)], Permisos.DelTenant, [a.ToString()]);
        fuera.Empresas.ShouldBeEmpty();
        fuera.PorEmpresa.ShouldBeEmpty();
    }
}
