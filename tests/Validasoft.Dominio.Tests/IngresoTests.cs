using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Dominio.Tests;

/// <summary>Ingreso directo sin contraseñas (ING-1…ING-4): código de un solo uso, passkeys, sesiones con inactividad y política del grupo.</summary>
public sealed class IngresoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- código de ingreso ----------

    private static (DesafioDeIngreso Desafio, string Codigo) ConCuenta(string correo = "Ana@Aceros.co")
    {
        var (desafio, codigo) = DesafioDeIngreso.ParaCorreo(correo, conCuenta: true, Ahora, "10.0.0.1", "Chrome");
        return (desafio, codigo!);
    }

    [Fact]
    public void El_codigo_son_seis_digitos_se_guarda_solo_como_hash_y_vence_en_cinco_minutos()
    {
        var (desafio, codigo) = ConCuenta();

        codigo.ShouldMatch("^[0-9]{6}$");
        desafio.CodigoHash.ShouldNotBeNull().ShouldNotContain(codigo);
        desafio.Correo.ShouldBe("ana@aceros.co");
        desafio.Etapa.ShouldBe(EtapaDeIngreso.CodigoEnviado);
        desafio.Expira.ShouldBe(Ahora.AddMinutes(5));
        desafio.EsSenuelo.ShouldBeFalse();
    }

    [Fact]
    public void El_codigo_correcto_avanza_la_etapa_y_amplia_el_plazo_para_terminar()
    {
        var (desafio, codigo) = ConCuenta();

        desafio.VerificarCodigo(" " + codigo + " ", Ahora.AddMinutes(1)).ShouldBeTrue();

        desafio.Etapa.ShouldBe(EtapaDeIngreso.CodigoVerificado);
        desafio.CodigoHash.ShouldBeNull();
        desafio.Expira.ShouldBe(Ahora.AddMinutes(11));
    }

    [Fact]
    public void Un_codigo_correcto_solo_sirve_una_vez()
    {
        var (desafio, codigo) = ConCuenta();
        desafio.VerificarCodigo(codigo, Ahora).ShouldBeTrue();

        Codigo(() => desafio.VerificarCodigo(codigo, Ahora)).ShouldBe("desafio-usado");
    }

    [Fact]
    public void Un_codigo_incorrecto_cuenta_un_intento_y_al_quinto_se_bloquea_aunque_luego_llegue_el_correcto()
    {
        var (desafio, codigo) = ConCuenta();
        var malo = codigo == "000000" ? "111111" : "000000";

        for (var i = 1; i <= DesafioDeIngreso.IntentosMaximos; i++)
        {
            desafio.VerificarCodigo(malo, Ahora).ShouldBeFalse();
            desafio.Intentos.ShouldBe(i);
            desafio.IntentosRestantes.ShouldBe(DesafioDeIngreso.IntentosMaximos - i);
        }

        var e = Should.Throw<ReglaDeNegocioException>(() => desafio.VerificarCodigo(codigo, Ahora));
        e.Codigo.ShouldBe("codigo-bloqueado");
        e.Tipo.ShouldBe(TipoFalla.DemasiadosIntentos);
    }

    [Fact]
    public void Un_codigo_vencido_responde_410_aunque_sea_el_correcto()
    {
        var (desafio, codigo) = ConCuenta();

        var e = Should.Throw<ReglaDeNegocioException>(() => desafio.VerificarCodigo(codigo, Ahora.AddMinutes(5)));
        e.Codigo.ShouldBe("codigo-vencido");
        e.Tipo.ShouldBe(TipoFalla.Expirado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    public void Un_codigo_mal_formado_cuenta_como_incorrecto(string? codigo)
    {
        var (desafio, _) = ConCuenta();
        desafio.VerificarCodigo(codigo, Ahora).ShouldBeFalse();
        desafio.Intentos.ShouldBe(1);
    }

    [Fact]
    public void Un_correo_sin_cuenta_recibe_un_desafio_que_se_comporta_igual_pero_nunca_acepta_un_codigo()
    {
        var (desafio, codigo) = DesafioDeIngreso.ParaCorreo("nadie@otro.co", conCuenta: false, Ahora, null, null);

        codigo.ShouldBeNull();
        desafio.EsSenuelo.ShouldBeTrue();
        desafio.CodigoHash.ShouldNotBeNull();
        desafio.Expira.ShouldBe(Ahora.AddMinutes(5));

        // Ningún código de 6 dígitos sirve: se prueba una muestra y los intentos se cuentan como en uno real.
        foreach (var intento in new[] { "000000", "123456", "999999", "424242", "135790" })
            desafio.VerificarCodigo(intento, Ahora).ShouldBeFalse();
        Codigo(() => desafio.VerificarCodigo("000000", Ahora)).ShouldBe("codigo-bloqueado");
    }

    [Fact]
    public void Dos_desafios_con_el_mismo_codigo_no_comparten_hash()
    {
        var (a, _) = ConCuenta();
        var (b, _) = ConCuenta();
        a.CodigoHash.ShouldNotBe(b.CodigoHash);
    }

    [Fact]
    public void Un_correo_invalido_se_rechaza_al_crear_el_desafio()
    {
        Codigo(() => DesafioDeIngreso.ParaCorreo("sin-arroba", true, Ahora, null, null)).ShouldBe("correo-invalido");
    }

    [Fact]
    public void Elegir_cuenta_y_guardar_opciones_exigen_la_etapa_y_vigencia_y_consumir_cierra_la_ceremonia()
    {
        var (desafio, codigo) = ConCuenta();
        var cuenta = new CuentaDeIngreso(TipoDePropietario.Usuario, Guid.NewGuid(), Guid.NewGuid().ToString());

        Codigo(() => desafio.ElegirCuenta(cuenta, Ahora)).ShouldBe("desafio-usado"); // aún no verificó el código
        Codigo(() => desafio.GuardarOpciones("{}", Ahora)).ShouldBe("desafio-usado");
        Codigo(() => desafio.Consumir(Ahora)).ShouldBe("desafio-usado");

        desafio.VerificarCodigo(codigo, Ahora).ShouldBeTrue();
        desafio.ElegirCuenta(cuenta, Ahora);
        desafio.GuardarOpciones("{\"challenge\":\"x\"}", Ahora.AddMinutes(2));
        desafio.OpcionesDePasskey.ShouldBe("{\"challenge\":\"x\"}");
        desafio.Expira.ShouldBe(Ahora.AddMinutes(12));

        Codigo(() => desafio.Consumir(Ahora.AddMinutes(13))).ShouldBe("desafio-vencido");
        desafio.Consumir(Ahora.AddMinutes(3));
        desafio.Etapa.ShouldBe(EtapaDeIngreso.Consumido);
        desafio.OpcionesDePasskey.ShouldBeNull();
        Codigo(() => desafio.Consumir(Ahora.AddMinutes(3))).ShouldBe("desafio-usado");
    }

    [Fact]
    public void El_ingreso_solo_con_passkey_nace_sin_correo_y_el_registro_desde_mi_cuenta_con_la_cuenta_fijada()
    {
        var soloPasskey = DesafioDeIngreso.ParaPasskey(Ahora, "10.0.0.1", "Chrome");
        soloPasskey.Etapa.ShouldBe(EtapaDeIngreso.PasskeyPendiente);
        soloPasskey.Correo.ShouldBeNull();

        var cuenta = new CuentaDeIngreso(TipoDePropietario.Usuario, Guid.NewGuid(), "u1");
        var registro = DesafioDeIngreso.ParaRegistro(cuenta, "ana@aceros.co", Ahora, null, null);
        registro.Etapa.ShouldBe(EtapaDeIngreso.CodigoVerificado);
        registro.Cuenta.ShouldBe(cuenta);
        registro.Expira.ShouldBe(Ahora.AddMinutes(10));
    }

    // ---------- passkeys ----------

    private static Passkey NuevaPasskey(uint contador = 0) =>
        Passkey.Registrar(TipoDePropietario.Usuario, Guid.NewGuid(), "u1", "cred-1", "manejador", "clave-publica", contador, "  Mi teléfono ", ["internal", "hybrid", "internal"],
            "aaguid", true, Ahora);

    [Fact]
    public void Registrar_una_passkey_limpia_el_nombre_y_los_transportes()
    {
        var passkey = NuevaPasskey();

        passkey.Nombre.ShouldBe("Mi teléfono");
        passkey.Transportes.ShouldBe(["internal", "hybrid"]);
        passkey.Activa.ShouldBeTrue();
        passkey.CreadaEn.ShouldBe(Ahora);

        Passkey.Registrar(TipoDePropietario.Operador, null, "DIRECTO:a@b.co", "c", "m", "k", 0, null, null, null, false, Ahora).Nombre.ShouldBe("Mi passkey");
        Codigo(() => Passkey.Registrar(TipoDePropietario.Usuario, null, "", "c", "m", "k", 0, null, null, null, false, Ahora)).ShouldBe("passkey-invalida");
        Codigo(() => Passkey.Registrar(TipoDePropietario.Usuario, null, "u", "c", "m", "k", 0, new string('x', 61), null, null, false, Ahora)).ShouldBe("nombre-invalido");
    }

    [Fact]
    public void El_contador_de_firmas_debe_crecer_y_uno_que_no_crece_indica_una_credencial_clonada()
    {
        var passkey = NuevaPasskey(contador: 5);

        passkey.Usar(6, Ahora.AddDays(1), "10.0.0.9");
        passkey.ContadorDeFirmas.ShouldBe(6u);
        passkey.UltimoUso.ShouldBe(Ahora.AddDays(1));
        passkey.UltimaIp.ShouldBe("10.0.0.9");

        Codigo(() => passkey.Usar(6, Ahora, null)).ShouldBe("passkey-clonada");
        Codigo(() => passkey.Usar(2, Ahora, null)).ShouldBe("passkey-clonada");
    }

    [Fact]
    public void Un_autenticador_sin_contador_firma_siempre_con_cero()
    {
        var passkey = NuevaPasskey(contador: 0);
        passkey.Usar(0, Ahora, null);
        passkey.Usar(0, Ahora.AddMinutes(1), null);
        passkey.ContadorDeFirmas.ShouldBe(0u);
    }

    [Fact]
    public void Revocar_deja_la_passkey_sin_efecto_y_queda_quien_y_por_que()
    {
        var passkey = NuevaPasskey();

        passkey.Revocar("Usuario:admin", "Perdió el teléfono", Ahora.AddHours(1));

        passkey.Activa.ShouldBeFalse();
        passkey.RevocadaPor.ShouldBe("Usuario:admin");
        passkey.MotivoRevocacion.ShouldBe("Perdió el teléfono");
        Codigo(() => passkey.Usar(1, Ahora, null)).ShouldBe("passkey-revocada");
        Codigo(() => passkey.Renombrar("otro")).ShouldBe("passkey-revocada");
        Codigo(() => passkey.Revocar("x", null, Ahora)).ShouldBe("passkey-revocada");
        Should.Throw<ReglaDeNegocioException>(() => passkey.AsegurarActiva()).Tipo.ShouldBe(TipoFalla.NoAutenticado);
    }

    // ---------- sesión directa ----------

    private static (SesionDeUsuario Sesion, string Token) Sesion(int horas = 8, int inactividad = 30) =>
        SesionDeUsuario.Abrir(TipoDePropietario.Usuario, Guid.NewGuid(), "u1", "Ana@Aceros.co", MetodoDeIngreso.Passkey, TimeSpan.FromHours(horas), inactividad, Ahora,
            "10.0.0.1", "Chrome");

    [Fact]
    public void Abrir_guarda_solo_el_hash_y_fija_el_maximo_absoluto_del_grupo()
    {
        var (sesion, token) = Sesion(horas: 4);

        SesionDeRefresco.FamiliaDe(token).ShouldBe(sesion.Id);
        sesion.TokenHash.ShouldBe(SesionDeRefresco.HashDeToken(token));
        sesion.TokenHash.ShouldNotContain(token.Split('.')[1]);
        sesion.Correo.ShouldBe("ana@aceros.co");
        sesion.ExpiraEn.ShouldBe(Ahora.AddHours(4));
        sesion.UltimaActividad.ShouldBe(Ahora);
        sesion.VigenteEn(Ahora).ShouldBeTrue();
    }

    [Fact]
    public void Renovar_rota_el_token_y_actualiza_la_actividad_pero_no_el_maximo()
    {
        var (sesion, token) = Sesion();

        var primera = sesion.Renovar(token, Ahora.AddMinutes(20), "10.0.0.2");

        primera.Renovada.ShouldBeTrue();
        primera.Token.ShouldNotBe(token);
        sesion.TokenHash.ShouldBe(SesionDeRefresco.HashDeToken(primera.Token!));
        sesion.UltimaActividad.ShouldBe(Ahora.AddMinutes(20));
        sesion.UltimaIp.ShouldBe("10.0.0.2");
        sesion.Rotaciones.ShouldBe(1);
        sesion.ExpiraEn.ShouldBe(Ahora.AddHours(8));
        sesion.Renovar(primera.Token!, Ahora.AddMinutes(45), null).Renovada.ShouldBeTrue();
        sesion.UltimaIp.ShouldBe("10.0.0.2"); // sin IP nueva se conserva la última conocida
    }

    [Fact]
    public void Reusar_un_token_ya_rotado_revoca_toda_la_familia()
    {
        var (sesion, token) = Sesion();
        var nuevo = sesion.Renovar(token, Ahora.AddMinutes(1), null).Token!;

        var reuso = sesion.Renovar(token, Ahora.AddMinutes(2), null);

        reuso.Renovada.ShouldBeFalse();
        sesion.Revocada.ShouldBeTrue();
        sesion.MotivoRevocacion.ShouldBe(SesionDeUsuario.MotivoReuso);
        Codigo(() => sesion.Renovar(nuevo, Ahora.AddMinutes(3), null)).ShouldBe("sesion-revocada"); // también el vigente
    }

    [Fact]
    public void El_token_anterior_recien_rotado_es_una_peticion_duplicada_y_no_revoca_la_familia()
    {
        var (sesion, token) = Sesion();
        var nuevo = sesion.Renovar(token, Ahora.AddMinutes(1), null).Token!;

        // Dentro de la gracia (10 s): 409 sin revocar; la cookie nueva sigue sirviendo.
        var e = Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar(token, Ahora.AddMinutes(1).AddSeconds(5), null));
        e.Codigo.ShouldBe("renovacion-concurrente");
        e.Tipo.ShouldBe(TipoFalla.Conflicto);
        sesion.Revocada.ShouldBeFalse();
        sesion.Renovar(nuevo, Ahora.AddMinutes(1).AddSeconds(6), null).Renovada.ShouldBeTrue();
    }

    [Fact]
    public void El_token_anterior_pasada_la_gracia_o_un_token_ajeno_si_revocan()
    {
        var (sesion, token) = Sesion();
        sesion.Renovar(token, Ahora.AddMinutes(1), null);
        sesion.Renovar(token, Ahora.AddMinutes(1).AddSeconds(11), null).Renovada.ShouldBeFalse();
        sesion.Revocada.ShouldBeTrue();

        var (otra, tokenDeOtra) = Sesion();
        otra.Renovar(tokenDeOtra, Ahora.AddMinutes(1), null);
        // Un token que nunca fue el de esta familia no se confunde con el anterior, ni dentro de la gracia.
        otra.Renovar($"{otra.Id:N}.{new string('a', 43)}", Ahora.AddMinutes(1).AddSeconds(2), null).Renovada.ShouldBeFalse();
        otra.MotivoRevocacion.ShouldBe(SesionDeUsuario.MotivoReuso);
    }

    [Fact]
    public void Sin_renovar_durante_la_inactividad_la_sesion_se_cierra()
    {
        var (sesion, token) = Sesion(inactividad: 30);

        sesion.VigenteEn(Ahora.AddMinutes(30)).ShouldBeTrue();
        sesion.VigenteEn(Ahora.AddMinutes(31)).ShouldBeFalse();
        var e = Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar(token, Ahora.AddMinutes(31), null));
        e.Codigo.ShouldBe("sesion-inactiva");
        e.Tipo.ShouldBe(TipoFalla.NoAutenticado);
    }

    [Fact]
    public void Renovando_a_tiempo_la_sesion_dura_hasta_el_maximo_y_no_mas()
    {
        var (sesion, token) = Sesion(horas: 1, inactividad: 30);
        token = sesion.Renovar(token, Ahora.AddMinutes(25), null).Token!;
        token = sesion.Renovar(token, Ahora.AddMinutes(50), null).Token!;

        Codigo(() => sesion.Renovar(token, Ahora.AddMinutes(60), null)).ShouldBe("sesion-expirada");
        sesion.VigenteEn(Ahora.AddMinutes(60)).ShouldBeFalse();
    }

    [Fact]
    public void Revocar_una_sesion_la_deja_cerrada_con_su_motivo_y_repetir_no_cambia_el_primero()
    {
        var (sesion, token) = Sesion();

        sesion.Revocar(SesionDeUsuario.MotivoAdministrador, "Usuario:admin", Ahora.AddMinutes(5));
        sesion.Revocar(SesionDeUsuario.MotivoCierre, "otro", Ahora.AddMinutes(9));

        sesion.MotivoRevocacion.ShouldBe(SesionDeUsuario.MotivoAdministrador);
        sesion.RevocadaPor.ShouldBe("Usuario:admin");
        sesion.RevocadaEn.ShouldBe(Ahora.AddMinutes(5));
        sesion.VigenteEn(Ahora.AddMinutes(6)).ShouldBeFalse();
        Codigo(() => sesion.Renovar(token, Ahora.AddMinutes(6), null)).ShouldBe("sesion-revocada");
    }

    // ---------- política del grupo y quién exige passkey ----------

    [Fact]
    public void El_grupo_nace_solo_con_identidad_delegada_y_la_politica_se_valida()
    {
        var tenant = Tenant.Crear("grupo-x", "Grupo X", null, null, Ahora);
        tenant.ModosDeIngreso.ShouldBe(ModoDeIngreso.Delegado);
        tenant.SesionHoras.ShouldBe(8);
        tenant.InactividadMinutos.ShouldBe(30);
        tenant.PasskeyParaTodos.ShouldBeFalse();

        tenant.FijarIngreso(ModoDeIngreso.Delegado | ModoDeIngreso.Directo, 12, 45, true);
        tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo).ShouldBeTrue();
        tenant.SesionHoras.ShouldBe(12);
        tenant.InactividadMinutos.ShouldBe(45);
        tenant.PasskeyParaTodos.ShouldBeTrue();

        Codigo(() => tenant.FijarIngreso(ModoDeIngreso.Ninguno, 8, 30, false)).ShouldBe("modos-de-ingreso-invalidos");
        Codigo(() => tenant.FijarIngreso(ModoDeIngreso.Directo, 0, 30, false)).ShouldBe("sesion-invalida");
        Codigo(() => tenant.FijarIngreso(ModoDeIngreso.Directo, 25, 30, false)).ShouldBe("sesion-invalida");
        Codigo(() => tenant.FijarIngreso(ModoDeIngreso.Directo, 8, 4, false)).ShouldBe("inactividad-invalida");
        Codigo(() => tenant.FijarIngreso(ModoDeIngreso.Directo, 1, 90, false)).ShouldBe("inactividad-invalida"); // más que la sesión
    }

    [Theory]
    [InlineData(Rol.Administrador, true)]
    [InlineData(Rol.OficialCumplimiento, true)]
    [InlineData(Rol.Auditor, true)]
    [InlineData(Rol.Revisor, false)]
    public void Los_roles_que_exigen_passkey_son_administrador_cumplimiento_y_auditor(Rol rol, bool exige)
    {
        var usuario = Usuario.Crear("Ana", "ana@aceros.co", null, Ahora);
        usuario.AsignarRoles([new RolAsignado(rol, Guid.NewGuid())]);

        usuario.ExigePasskey(paraTodosEnElGrupo: false).ShouldBe(exige);
        usuario.ExigePasskey(paraTodosEnElGrupo: true).ShouldBeTrue();
    }

    [Fact]
    public void Un_usuario_sin_roles_no_exige_passkey_salvo_que_el_grupo_la_pida_a_todos()
    {
        var usuario = Usuario.Crear("Ana", "ana@aceros.co", null, Ahora, tenantId: Guid.NewGuid());
        usuario.TenantId.ShouldNotBeNull();
        usuario.ExigePasskey(false).ShouldBeFalse();
        usuario.ExigePasskey(true).ShouldBeTrue();
    }

    // ---------- operadores directos y límites ----------

    [Fact]
    public void Un_operador_directo_se_identifica_por_su_correo_y_siempre_es_de_ingreso_directo()
    {
        var operador = OperadorDePlataforma.RegistrarDirecto(" Paula ", "Paula@Validasoft.co", Ahora);

        operador.Id.ShouldBe("DIRECTO:paula@validasoft.co");
        operador.EsDirecto.ShouldBeTrue();
        operador.Nombre.ShouldBe("Paula");
        operador.Activo.ShouldBeTrue();
        Codigo(() => OperadorDePlataforma.RegistrarDirecto("", "a@b.co", Ahora)).ShouldBe("nombre-requerido");
        Codigo(() => OperadorDePlataforma.RegistrarDirecto("Paula", "malo", Ahora)).ShouldBe("correo-invalido");
        Codigo(() => OperadorDePlataforma.RegistrarDirecto("Paula", new string('a', 130) + "@b.co", Ahora)).ShouldBe("correo-invalido");

        operador.Desactivar();
        operador.Activo.ShouldBeFalse();
        Codigo(() => operador.Desactivar()).ShouldBe("operador-inactivo");
    }

    [Fact]
    public void El_limite_de_codigos_de_ingreso_es_aparte_del_del_enlace_y_permite_cinco_por_hora()
    {
        LimiteDeAccesoPorCorreo.IdDe("ana@aceros.co", "ingreso").ShouldNotBe(LimiteDeAccesoPorCorreo.IdDe("ana@aceros.co"));
        LimiteDeAccesoPorCorreo.IdDe("ANA@aceros.co ", "ingreso").ShouldBe(LimiteDeAccesoPorCorreo.IdDe("ana@aceros.co", "ingreso"));

        var limite = LimiteDeAccesoPorCorreo.Nuevo("ana@aceros.co", "ingreso");
        for (var i = 0; i < LimiteDeAccesoPorCorreo.MaximoDeIngreso; i++)
            limite.IntentarRegistrar(Ahora.AddMinutes(i), LimiteDeAccesoPorCorreo.MaximoDeIngreso).ShouldBeTrue();
        limite.IntentarRegistrar(Ahora.AddMinutes(10), LimiteDeAccesoPorCorreo.MaximoDeIngreso).ShouldBeFalse();
        limite.IntentarRegistrar(Ahora.AddMinutes(61), LimiteDeAccesoPorCorreo.MaximoDeIngreso).ShouldBeTrue(); // el primero salió de la ventana
    }

    // ---------- correo ----------

    private static RedactorDeCorreos Redactor() =>
        new(new ConfigurationBuilder().AddInMemoryCollection([new("Validasoft:Correo:UrlPublica", "https://valida.test")]).Build());

    [Fact]
    public void El_correo_del_codigo_lo_lleva_y_no_trae_enlaces_de_un_solo_uso()
    {
        var mensaje = Redactor().Redactar(new CorreoDeAccesoPendiente(PlantillaCorreo.CodigoDeIngreso, "ana@aceros.co", "x", Ahora.AddMinutes(5), null, null), "482915");

        mensaje.Asunto.ShouldBe("Tu código para ingresar a Validasoft");
        mensaje.Texto.ShouldContain("482915");
        mensaje.Html.ShouldContain("482915");
        mensaje.Texto.ShouldNotContain("/e/");
    }

    [Fact]
    public void La_invitacion_a_entrar_remite_a_la_app_con_el_grupo_y_los_roles_y_no_lleva_secreto()
    {
        var mensaje = Redactor().Redactar(new CorreoDeAccesoPendiente(PlantillaCorreo.InvitacionDeUsuario, "ana@aceros.co", "x", Ahora, "Grupo Norte", "Administrador, Auditor"), "-");

        mensaje.Asunto.ShouldBe("Grupo Norte te invitó a Validasoft");
        mensaje.Texto.ShouldContain("como Administrador, Auditor");
        mensaje.Texto.ShouldContain("https://valida.test/app/");
        mensaje.Texto.ShouldContain("contraseña", Case.Insensitive);
        mensaje.Html.ShouldContain("https://valida.test/app/");
    }
}
