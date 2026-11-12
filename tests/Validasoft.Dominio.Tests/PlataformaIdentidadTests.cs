using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Plataforma e identidad (PI-1…PI-18): configuración de SSO (dominios, JIT, reglas de grupos a roles), el inicio de sesión con el proveedor del cliente, la
/// reautenticación, los códigos de recuperación, la tarifa y el consumo de IA, y el estado de la clave de arranque.
/// </summary>
public sealed class PlataformaIdentidadTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid Grupo = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Empresa = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    private static string Proteger(string secreto) => "protegido:" + secreto;

    private static CambiosDeSso Cambios(bool activo = false, string proveedor = "oidc", string nombre = "Acme", string? autoridad = "https://login.acme.test", string? clientId = "cliente-1",
        string? secreto = "secreto-del-cliente", bool jit = false, IReadOnlyList<RolAsignado>? roles = null, IReadOnlyList<ReglaDeGrupoSso>? reglas = null, bool sincronizar = false,
        ModoDeSso modo = ModoDeSso.Complementario, string? organizacion = null, string? conexion = null) =>
        new(activo, proveedor, nombre, autoridad, clientId, secreto, null, null, organizacion, conexion, jit, roles ?? [], reglas ?? [], sincronizar, modo);

    private static ConfiguracionDeSso ConDominioVerificado(string dominio = "acme.co")
    {
        var configuracion = ConfiguracionDeSso.Nueva(Grupo, Ahora);
        var d = configuracion.AgregarDominio(dominio, Ahora);
        configuracion.MarcarVerificado(d, Ahora);
        return configuracion;
    }

    // ---------- SSO: configuración ----------

    [Fact]
    public void La_configuracion_valida_guarda_el_secreto_cifrado_y_solo_sus_ultimos_cuatro()
    {
        var configuracion = ConDominioVerificado();

        configuracion.Actualizar(Cambios(activo: true), Proteger, permitirHttpLocal: false, Ahora);

        configuracion.Activo.ShouldBeTrue();
        configuracion.SecretoProtegido.ShouldBe("protegido:secreto-del-cliente");
        configuracion.UltimosCuatroSecreto.ShouldBe("ente");
        configuracion.LlaveConfigurada.ShouldBeTrue();
        configuracion.ReclamoDeGrupos.ShouldBe("groups");
        configuracion.Alcances.ShouldBe(["openid", "email", "profile"]);
    }

    [Fact]
    public void Un_secreto_nulo_conserva_el_actual_y_uno_vacio_lo_quita()
    {
        var configuracion = ConDominioVerificado();
        configuracion.Actualizar(Cambios(), Proteger, false, Ahora);

        configuracion.Actualizar(Cambios(secreto: null), Proteger, false, Ahora);
        configuracion.SecretoProtegido.ShouldBe("protegido:secreto-del-cliente");

        configuracion.Actualizar(Cambios(secreto: ""), Proteger, false, Ahora);
        configuracion.SecretoProtegido.ShouldBeNull();
        configuracion.LlaveConfigurada.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ftp://login.acme.test")]
    [InlineData("http://login.acme.test")]
    [InlineData("login.acme.test")]
    [InlineData("https://usuario:clave@login.acme.test")]
    [InlineData("https://login.acme.test/?a=1")]
    [InlineData("")]
    public void El_emisor_debe_ser_https_sin_credenciales_ni_consulta(string autoridad)
    {
        var configuracion = ConDominioVerificado();

        Codigo(() => configuracion.Actualizar(Cambios(autoridad: autoridad), Proteger, false, Ahora)).ShouldBe("autoridad-invalida");
    }

    [Fact]
    public void Un_proveedor_en_localhost_solo_se_admite_si_la_instalacion_lo_permite()
    {
        var configuracion = ConDominioVerificado();

        Codigo(() => configuracion.Actualizar(Cambios(autoridad: "http://127.0.0.1:5999"), Proteger, false, Ahora)).ShouldBe("autoridad-invalida");
        configuracion.Actualizar(Cambios(autoridad: "http://127.0.0.1:5999/"), Proteger, permitirHttpLocal: true, Ahora);
        configuracion.Autoridad.ShouldBe("http://127.0.0.1:5999");
    }

    [Fact]
    public void Las_reglas_de_la_configuracion_se_validan()
    {
        var configuracion = ConDominioVerificado();

        Codigo(() => configuracion.Actualizar(Cambios(proveedor: "saml"), Proteger, false, Ahora)).ShouldBe("proveedor-sso-invalido");
        Codigo(() => configuracion.Actualizar(Cambios(nombre: " "), Proteger, false, Ahora)).ShouldBe("nombre-sso-requerido");
        Codigo(() => configuracion.Actualizar(Cambios(clientId: ""), Proteger, false, Ahora)).ShouldBe("client-id-requerido");
        Codigo(() => configuracion.Actualizar(Cambios(secreto: "corto"), Proteger, false, Ahora)).ShouldBe("secreto-sso-invalido");
        Codigo(() => configuracion.Actualizar(Cambios(proveedor: "workos"), Proteger, false, Ahora)).ShouldBe("conexion-workos-requerida");
        configuracion.Actualizar(Cambios(proveedor: "workos", organizacion: "org_01"), Proteger, false, Ahora);
        configuracion.OrganizacionWorkOs.ShouldBe("org_01");
        configuracion.Autoridad.ShouldBeNull(); // con WorkOS no hay emisor ni client id propios
    }

    [Fact]
    public void El_aprovisionamiento_JIT_necesita_un_rol_por_omision_o_reglas()
    {
        var configuracion = ConDominioVerificado();

        Codigo(() => configuracion.Actualizar(Cambios(jit: true), Proteger, false, Ahora)).ShouldBe("rol-por-omision-requerido");
        configuracion.Actualizar(Cambios(jit: true, roles: [new RolAsignado(Rol.Revisor, null)]), Proteger, false, Ahora);
        configuracion.AprovisionamientoJit.ShouldBeTrue();
        configuracion.Actualizar(Cambios(jit: true, reglas: [new ReglaDeGrupoSso { Grupo = "Auditores", Rol = Rol.Auditor }]), Proteger, false, Ahora);
        configuracion.Reglas.ShouldHaveSingleItem();
    }

    [Fact]
    public void No_se_activa_el_SSO_sin_un_dominio_verificado()
    {
        var sinDominio = ConfiguracionDeSso.Nueva(Grupo, Ahora);
        var sinVerificar = ConfiguracionDeSso.Nueva(Grupo, Ahora);
        sinVerificar.AgregarDominio("acme.co", Ahora);

        Codigo(() => sinDominio.Actualizar(Cambios(activo: true), Proteger, false, Ahora)).ShouldBe("sso-sin-dominios");
        Codigo(() => sinVerificar.Actualizar(Cambios(activo: true), Proteger, false, Ahora)).ShouldBe("sso-sin-dominios");
    }

    // ---------- SSO: dominios ----------

    [Theory]
    [InlineData("Acme.CO", "acme.co")]
    [InlineData("@acme.co", "acme.co")]
    [InlineData(" acme.co. ", "acme.co")]
    [InlineData("ti.acme.com.co", "ti.acme.com.co")]
    public void Los_dominios_se_normalizan(string escrito, string esperado)
    {
        var configuracion = ConfiguracionDeSso.Nueva(Grupo, Ahora);

        var dominio = configuracion.AgregarDominio(escrito, Ahora);

        dominio.Dominio.ShouldBe(esperado);
        dominio.Verificado.ShouldBeFalse();
        dominio.Token.ShouldStartWith("validasoft-verificacion=");
    }

    [Theory]
    [InlineData("acme")]
    [InlineData("https://acme.co")]
    [InlineData("acme.co/ruta")]
    [InlineData("a b.co")]
    public void Un_dominio_mal_escrito_se_rechaza(string escrito) =>
        Codigo(() => ConfiguracionDeSso.Nueva(Grupo, Ahora).AgregarDominio(escrito, Ahora)).ShouldBe("dominio-invalido");

    [Theory]
    [InlineData("gmail.com")]
    [InlineData("Hotmail.com")]
    [InlineData("outlook.com")]
    public void Un_proveedor_de_correo_abierto_no_se_puede_reclamar(string escrito) =>
        Codigo(() => ConfiguracionDeSso.Nueva(Grupo, Ahora).AgregarDominio(escrito, Ahora)).ShouldBe("dominio-publico");

    [Fact]
    public void Agregar_dos_veces_el_mismo_dominio_devuelve_el_mismo_desafio()
    {
        var configuracion = ConfiguracionDeSso.Nueva(Grupo, Ahora);
        var primero = configuracion.AgregarDominio("acme.co", Ahora);

        var segundo = configuracion.AgregarDominio("ACME.co", Ahora.AddHours(1));

        segundo.Token.ShouldBe(primero.Token);
        configuracion.Dominios.ShouldHaveSingleItem();
    }

    [Fact]
    public void Solo_un_dominio_verificado_hace_que_el_SSO_aplique_al_correo()
    {
        var configuracion = ConfiguracionDeSso.Nueva(Grupo, Ahora);
        var d = configuracion.AgregarDominio("acme.co", Ahora);
        configuracion.Actualizar(Cambios(), Proteger, false, Ahora);

        configuracion.CubreCorreo("ana@acme.co").ShouldBeFalse();
        configuracion.MarcarVerificado(d, Ahora);
        configuracion.Actualizar(Cambios(activo: true), Proteger, false, Ahora);

        configuracion.CubreCorreo("Ana@ACME.co").ShouldBeTrue();
        configuracion.CubreCorreo("ana@otra.co").ShouldBeFalse();
        configuracion.CubreCorreo("ana@sub.acme.co").ShouldBeFalse(); // un subdominio es otro dominio
        configuracion.AplicaA("ana@acme.co").ShouldBeTrue();
    }

    [Fact]
    public void Con_el_SSO_activo_no_se_quita_el_unico_dominio_verificado()
    {
        var configuracion = ConDominioVerificado();
        configuracion.Actualizar(Cambios(activo: true), Proteger, false, Ahora);

        Codigo(() => configuracion.QuitarDominio("acme.co", Ahora)).ShouldBe("sso-sin-dominios");
        configuracion.Actualizar(Cambios(activo: false), Proteger, false, Ahora);
        configuracion.QuitarDominio("acme.co", Ahora);
        configuracion.Dominios.ShouldBeEmpty();
    }

    // ---------- SSO: roles por los grupos del proveedor ----------

    [Fact]
    public void Los_roles_salen_de_las_reglas_que_coinciden_y_si_ninguna_los_de_por_omision()
    {
        var configuracion = ConDominioVerificado();
        configuracion.Actualizar(Cambios(jit: true, roles: [new RolAsignado(Rol.Revisor, Empresa)], reglas:
        [
            new ReglaDeGrupoSso { Grupo = "TI-Admins", Rol = Rol.Administrador },
            new ReglaDeGrupoSso { Grupo = "Cumplimiento", Rol = Rol.OficialCumplimiento, EmpresaId = Empresa },
            new ReglaDeGrupoSso { Grupo = "Auditoria", Rol = Rol.Auditor }
        ]), Proteger, false, Ahora);

        configuracion.RolesPara(["ti-admins", "Cumplimiento"]).ShouldBe([new RolAsignado(Rol.Administrador, null), new RolAsignado(Rol.OficialCumplimiento, Empresa)], ignoreOrder: true);
        configuracion.RolesPara(["Otro-grupo"]).ShouldBe([new RolAsignado(Rol.Revisor, Empresa)]);
        configuracion.RolesPara([]).ShouldBe([new RolAsignado(Rol.Revisor, Empresa)]);
    }

    [Fact]
    public void Un_rol_de_todo_el_grupo_hace_redundante_el_mismo_rol_por_empresa()
    {
        var configuracion = ConDominioVerificado();
        configuracion.Actualizar(Cambios(jit: true, reglas:
        [
            new ReglaDeGrupoSso { Grupo = "A", Rol = Rol.Revisor },
            new ReglaDeGrupoSso { Grupo = "B", Rol = Rol.Revisor, EmpresaId = Empresa }
        ]), Proteger, false, Ahora);

        configuracion.RolesPara(["A", "B"]).ShouldBe([new RolAsignado(Rol.Revisor, null)]);
    }

    [Fact]
    public void El_modo_exclusivo_reemplaza_el_codigo_solo_para_los_correos_de_sus_dominios()
    {
        var configuracion = ConDominioVerificado();
        configuracion.Actualizar(Cambios(activo: true, modo: ModoDeSso.Exclusivo), Proteger, false, Ahora);

        configuracion.ReemplazaElCodigoPara("ana@acme.co").ShouldBeTrue();
        configuracion.ReemplazaElCodigoPara("ana@otra.co").ShouldBeFalse();
        configuracion.Actualizar(Cambios(activo: true, modo: ModoDeSso.Complementario), Proteger, false, Ahora);
        configuracion.ReemplazaElCodigoPara("ana@acme.co").ShouldBeFalse();
    }

    // ---------- SSO: el desafío ----------

    private static (DesafioDeIngreso Desafio, string Estado, string Navegador) Inicio() =>
        DesafioDeIngreso.ParaSso(Grupo, "oidc", "Ana@Acme.co", "nonce-1", "verificador-protegido", "https://api.validasoft.test/api/v1/ingreso/sso/callback", Ahora, "10.0.0.1", "Chrome");

    [Fact]
    public void El_inicio_guarda_solo_hashes_del_state_y_del_navegador()
    {
        var (desafio, estado, navegador) = Inicio();

        desafio.Etapa.ShouldBe(EtapaDeIngreso.SsoPendiente);
        desafio.Correo.ShouldBe("ana@acme.co");
        desafio.Sso!.EstadoHash.ShouldNotContain(estado);
        desafio.Sso.NavegadorHash.ShouldNotContain(navegador);
        estado.ShouldStartWith(desafio.Id.ToString("N") + ".");
        desafio.Expira.ShouldBe(Ahora.AddMinutes(10));
    }

    [Fact]
    public void El_regreso_exige_el_state_y_la_cookie_del_navegador_que_empezo()
    {
        var (desafio, estado, navegador) = Inicio();

        desafio.VerificarRetornoSso(estado, navegador, Ahora.AddMinutes(1)); // no lanza
        Codigo(() => desafio.VerificarRetornoSso(estado, "otro-navegador", Ahora.AddMinutes(1))).ShouldBe("sso-estado-invalido");
        Codigo(() => desafio.VerificarRetornoSso(estado, null, Ahora.AddMinutes(1))).ShouldBe("sso-estado-invalido");
        Codigo(() => desafio.VerificarRetornoSso(desafio.Id.ToString("N") + ".adivinado", navegador, Ahora.AddMinutes(1))).ShouldBe("sso-estado-invalido");
        Codigo(() => desafio.VerificarRetornoSso(null, navegador, Ahora.AddMinutes(1))).ShouldBe("sso-estado-invalido");
    }

    [Fact]
    public void El_regreso_vencido_o_ya_usado_no_sirve()
    {
        var (desafio, estado, navegador) = Inicio();
        Codigo(() => desafio.VerificarRetornoSso(estado, navegador, Ahora.AddMinutes(11))).ShouldBe("desafio-vencido");

        desafio.RegistrarIdentidadSso("ana@acme.co", "Ana", "sub-1", ["A"], true, true, Ahora.AddMinutes(1));
        Codigo(() => desafio.VerificarRetornoSso(estado, navegador, Ahora.AddMinutes(2))).ShouldBe("desafio-usado");
    }

    [Fact]
    public void La_identidad_declarada_queda_en_el_desafio_y_confirmarla_lo_deja_listo_para_la_cuenta()
    {
        var (desafio, _, _) = Inicio();

        desafio.RegistrarIdentidadSso("Ana@Acme.co", "  Ana Pérez ", "sub-1", ["TI", "ti", " ", "Cumplimiento"], gruposDeclarados: true, multifactor: true, Ahora.AddMinutes(1));

        desafio.Etapa.ShouldBe(EtapaDeIngreso.SsoVerificado);
        desafio.Sso!.Correo.ShouldBe("ana@acme.co");
        desafio.Sso.Nombre.ShouldBe("Ana Pérez");
        desafio.Sso.Grupos.ShouldBe(["TI", "Cumplimiento"]); // sin repetidos ni vacíos
        desafio.Sso.GruposDeclarados.ShouldBeTrue();
        desafio.Sso.Multifactor.ShouldBeTrue();

        desafio.ConfirmarSso(Ahora.AddMinutes(2));
        desafio.Etapa.ShouldBe(EtapaDeIngreso.CodigoVerificado);
        Codigo(() => desafio.ConfirmarSso(Ahora.AddMinutes(3))).ShouldBe("desafio-usado");
    }

    [Fact]
    public void Un_desafio_de_SSO_anulado_ya_no_sirve()
    {
        var (desafio, estado, navegador) = Inicio();

        desafio.Anular();

        Codigo(() => desafio.VerificarRetornoSso(estado, navegador, Ahora.AddMinutes(1))).ShouldBe("desafio-usado");
    }

    // ---------- reautenticación (step-up) ----------

    [Fact]
    public void La_reautenticacion_vale_cinco_minutos_y_solo_desde_que_se_hizo()
    {
        var (sesion, _) = SesionDeUsuario.Abrir(TipoDePropietario.Usuario, Grupo, Guid.NewGuid().ToString(), "ana@acme.co", MetodoDeIngreso.Passkey, TimeSpan.FromHours(8), 30, Ahora, null, null);

        sesion.ReautenticadaVigente(Ahora).ShouldBeFalse();
        sesion.Reautenticar(Ahora);
        sesion.ReautenticadaVigente(Ahora).ShouldBeTrue();
        sesion.ReautenticadaVigente(Ahora.AddMinutes(5)).ShouldBeTrue();
        sesion.ReautenticadaVigente(Ahora.AddMinutes(5).AddSeconds(1)).ShouldBeFalse();
        sesion.ReautenticadaVigente(Ahora.AddSeconds(-1)).ShouldBeFalse(); // un reloj que retrocede no la alarga
    }

    [Fact]
    public void El_desafio_de_reautenticacion_es_de_una_sesion_y_nace_en_la_etapa_de_la_passkey()
    {
        var cuenta = new CuentaDeIngreso(TipoDePropietario.Usuario, Grupo, Guid.NewGuid().ToString());
        var sesionId = Guid.NewGuid();

        var desafio = DesafioDeIngreso.ParaReautenticacion(cuenta, "ana@acme.co", sesionId, Ahora, "10.0.0.1", "Chrome");

        desafio.EsReautenticacion.ShouldBeTrue();
        desafio.SesionId.ShouldBe(sesionId);
        desafio.Cuenta.ShouldBe(cuenta);
        desafio.Etapa.ShouldBe(EtapaDeIngreso.PasskeyPendiente);
        desafio.Expira.ShouldBe(Ahora.AddMinutes(5));
    }

    // ---------- códigos de recuperación ----------

    private static CuentaDeIngreso Cuenta() => new(TipoDePropietario.Usuario, Grupo, "usuario-1");

    [Fact]
    public void Se_generan_diez_codigos_distintos_con_formato_para_copiar_y_solo_se_guarda_su_hash()
    {
        var (documento, claros) = CodigosDeRecuperacion.Generar(Cuenta(), Ahora);

        claros.Count.ShouldBe(CodigosDeRecuperacion.Cantidad);
        claros.Distinct().Count().ShouldBe(CodigosDeRecuperacion.Cantidad);
        claros.ShouldAllBe(c => c.Length == 11 && c[5] == '-');
        claros.ShouldAllBe(c => !c.Contains('0') && !c.Contains('1') && !c.Contains('O') && !c.Contains('I') && !c.Contains('L'));
        documento.Disponibles.ShouldBe(10);
        foreach (var claro in claros)
            documento.Codigos.ShouldAllBe(c => !c.Hash.Contains(CodigosDeRecuperacion.Normalizar(claro)));
    }

    [Fact]
    public void Un_codigo_de_recuperacion_sirve_una_sola_vez_y_sin_importar_mayusculas_ni_separadores()
    {
        var (documento, claros) = CodigosDeRecuperacion.Generar(Cuenta(), Ahora);

        documento.Consumir(claros[0].ToLowerInvariant().Replace("-", " "), Ahora).ShouldBeTrue();
        documento.Consumir(claros[0], Ahora).ShouldBeFalse(); // ya se usó
        documento.Disponibles.ShouldBe(9);
        documento.Consumir(claros[1], Ahora).ShouldBeTrue();
        documento.Disponibles.ShouldBe(8);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABCDE-FGHJK")]
    [InlineData("1234")]
    [InlineData(null)]
    public void Un_codigo_que_no_es_de_la_cuenta_no_se_acepta(string? codigo)
    {
        var (documento, _) = CodigosDeRecuperacion.Generar(Cuenta(), Ahora);

        documento.Consumir(codigo, Ahora).ShouldBeFalse();
        documento.Disponibles.ShouldBe(10);
    }

    [Fact]
    public void El_hash_depende_de_la_cuenta_un_codigo_de_otra_no_vale()
    {
        var (documento, claros) = CodigosDeRecuperacion.Generar(Cuenta(), Ahora);
        var (otro, _) = CodigosDeRecuperacion.Generar(new CuentaDeIngreso(TipoDePropietario.Usuario, Grupo, "usuario-2"), Ahora);

        otro.Consumir(claros[0], Ahora).ShouldBeFalse();
        documento.Id.ShouldNotBe(otro.Id);
    }

    // ---------- tarifa y consumo de IA ----------

    [Fact]
    public void La_tarifa_se_redondea_a_centavos_y_valida_el_precio_y_la_moneda()
    {
        var tarifa = TarifaDeIa.PorDefecto();

        tarifa.Fijar(1234.567m, " cop ", "plataforma:ana", Ahora);

        tarifa.PrecioPorValidacionAdicional.ShouldBe(1234.57m);
        tarifa.Moneda.ShouldBe("COP");
        tarifa.ActualizadaPor.ShouldBe("plataforma:ana");
        Codigo(() => tarifa.Fijar(-1m, "COP", "x", Ahora)).ShouldBe("precio-adicional-invalido");
        Codigo(() => tarifa.Fijar(TarifaDeIa.PrecioMaximo + 1, "COP", "x", Ahora)).ShouldBe("precio-adicional-invalido");
        Codigo(() => tarifa.Fijar(10m, "PESOS", "x", Ahora)).ShouldBe("moneda-invalida");
        tarifa.Fijar(10m, null, "x", Ahora);
        tarifa.Moneda.ShouldBe("COP");
    }

    [Fact]
    public void El_precio_de_una_empresa_gana_sobre_la_tarifa_general_y_sin_el_se_usa_la_general()
    {
        var tarifa = TarifaDeIa.PorDefecto();
        tarifa.Fijar(100m, "COP", "x", Ahora);
        var plan = PlanDeIaEmpresa.Nuevo(Empresa, Ahora);

        tarifa.PrecioPara(plan).ShouldBe(100m);
        tarifa.PrecioPara(null).ShouldBe(100m);
        plan.Actualizar(new CambiosDePlanIa(10, true, null, null, null, null, null, true, PrecioAdicional: 80m), Proteger, Ahora);
        tarifa.PrecioPara(plan).ShouldBe(80m);
        plan.Actualizar(new CambiosDePlanIa(10, true, null, null, null, null, null, true, PrecioAdicional: 0m), Proteger, Ahora);
        tarifa.PrecioPara(plan).ShouldBe(0m); // gratis es un precio propio, no «sin precio»
        Codigo(() => plan.Actualizar(new CambiosDePlanIa(10, true, null, null, null, null, null, true, PrecioAdicional: -5m), Proteger, Ahora)).ShouldBe("precio-adicional-invalido");
        TarifaDeIa.ValorDe(3, 80m).ShouldBe(240m);
    }

    [Fact]
    public void El_uso_mensual_suma_las_llamadas_por_modelo_ademas_de_los_totales()
    {
        var uso = UsoMensualIa.Nuevo(Empresa, "2026-11");
        var dia = new DateOnly(2026, 11, 11);

        uso.RegistrarLlamadas(
        [
            new LlamadaDeIa("gpt-6-luna", 0.9m, 1000, 200, 300, 0.01m),
            new LlamadaDeIa("gpt-6.1-terra", 0.7m, 2000, 0, 500, 0.05m),
            new LlamadaDeIa("gpt-6-luna", 0.8m, 500, 0, 100, 0.004m)
        ], dia, analisis: true);
        uso.RegistrarLlamadas([new LlamadaDeIa("gpt-6-luna", 0.9m, 10, 0, 5, 0.0001m)], dia, analisis: false);

        uso.TokensEntrada.ShouldBe(3510);
        uso.TokensSalida.ShouldBe(905);
        uso.CostoUsd.ShouldBe(0.0641m);
        uso.AnalisisPagados.ShouldBe(1);
        uso.PorModelo["gpt-6-luna"].Llamadas.ShouldBe(3);
        uso.PorModelo["gpt-6-luna"].TokensEntrada.ShouldBe(1510);
        uso.PorModelo["gpt-6-luna"].TokensEntradaEnCache.ShouldBe(200);
        uso.PorModelo["gpt-6.1-terra"].CostoUsd.ShouldBe(0.05m);
        uso.PorModelo.Values.Sum(m => m.TokensSalida).ShouldBe(uso.TokensSalida);
    }

    // ---------- sistemas, operadores y clave de arranque ----------

    [Fact]
    public void Un_sistema_desactivado_se_puede_reactivar_y_uno_activo_no()
    {
        var sistema = SistemaIntegrado.Crear("SRM", "SRM", ClaveDePrueba(), Ahora);

        Codigo(sistema.Reactivar).ShouldBe("sistema-activo");
        sistema.Desactivar();
        sistema.Activo.ShouldBeFalse();
        sistema.Reactivar();
        sistema.Activo.ShouldBeTrue();
    }

    [Fact]
    public void Un_operador_y_un_sistema_de_plataforma_tambien_se_reactivan()
    {
        var operador = OperadorDePlataforma.RegistrarDirecto("Paula", "paula@validasoft.co", Ahora);
        Codigo(operador.Reactivar).ShouldBe("operador-activo");
        operador.Desactivar();
        operador.Reactivar();
        operador.Activo.ShouldBeTrue();

        var sistema = SistemaDePlataforma.Fijar(null, "CONSOLA", "Consola", ClaveDePrueba(), null, permitirHttpLocal: false, Ahora);
        Codigo(() => sistema.Reactivar(Ahora)).ShouldBe("sistema-activo");
        sistema.Desactivar(Ahora);
        sistema.Reactivar(Ahora.AddMinutes(1));
        sistema.Activo.ShouldBeTrue();
        sistema.ActualizadoEn.ShouldBe(Ahora.AddMinutes(1));
    }

    [Fact]
    public void La_clave_de_arranque_se_desactiva_y_reactiva_una_vez_por_vez()
    {
        var configuracion = new ConfiguracionDePlataforma();

        Codigo(() => configuracion.ReactivarClaveDeArranque("plataforma:ana", Ahora)).ShouldBe("clave-de-arranque-activa");
        configuracion.DesactivarClaveDeArranque("plataforma:ana", Ahora);
        configuracion.ClaveDeArranqueDesactivada.ShouldBeTrue();
        configuracion.DesactivadaPor.ShouldBe("plataforma:ana");
        Codigo(() => configuracion.DesactivarClaveDeArranque("plataforma:ana", Ahora)).ShouldBe("clave-de-arranque-desactivada");
        configuracion.ReactivarClaveDeArranque("plataforma:luis", Ahora.AddHours(1));
        configuracion.ClaveDeArranqueDesactivada.ShouldBeFalse();
        configuracion.ReactivadaPor.ShouldBe("plataforma:luis");
    }

    private static string ClaveDePrueba()
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        return rsa.ExportSubjectPublicKeyInfoPem();
    }
}
