using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

public class EnlaceDeAccesoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static (EnlaceDeAcceso Enlace, string Token) Nuevo(bool otp = false) =>
        EnlaceDeAcceso.Emitir(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Ana@Aceros.co", Ahora.AddHours(72), otp, Ahora);

    [Fact]
    public void Guarda_solo_el_hash_del_token_de_256_bits()
    {
        var (enlace, token) = Nuevo();

        token.Length.ShouldBe(43); // 32 bytes en base64url sin relleno
        token.ShouldNotContain("+");
        token.ShouldNotContain("/");
        enlace.Id.ShouldBe(EnlaceDeAcceso.HashDeToken(token));
        enlace.Id.ShouldNotContain(token);
        enlace.Id.Length.ShouldBe(64);
        enlace.CorreoDestino.ShouldBe("ana@aceros.co");
        Nuevo().Token.ShouldNotBe(token);
    }

    [Fact]
    public void Sin_otp_se_canjea_una_sola_vez()
    {
        var (enlace, _) = Nuevo();

        enlace.Canjear(Ahora.AddMinutes(1)).SesionAbierta.ShouldBeTrue();
        enlace.Usado.ShouldBeTrue();

        var ex = Should.Throw<ReglaDeNegocioException>(() => enlace.Canjear(Ahora.AddMinutes(2)));
        ex.Codigo.ShouldBe("enlace-usado");
        ex.Tipo.ShouldBe(TipoFalla.Expirado);
    }

    [Fact]
    public void Vencido_no_se_canjea()
    {
        var (enlace, _) = Nuevo();

        var ex = Should.Throw<ReglaDeNegocioException>(() => enlace.Canjear(Ahora.AddHours(72)));
        ex.Codigo.ShouldBe("enlace-vencido");
        ex.Tipo.ShouldBe(TipoFalla.Expirado);
        enlace.Usado.ShouldBeFalse();
    }

    [Fact]
    public void Con_otp_el_canje_genera_un_codigo_de_seis_digitos_que_vence_en_diez_minutos()
    {
        var (enlace, _) = Nuevo(otp: true);

        var resultado = enlace.Canjear(Ahora);

        resultado.SesionAbierta.ShouldBeFalse();
        resultado.CodigoOtp!.ShouldMatch("^[0-9]{6}$");
        resultado.OtpExpira.ShouldBe(Ahora.AddMinutes(10));
        enlace.Usado.ShouldBeFalse();
        enlace.OtpHash.ShouldNotBeNull();
        enlace.OtpHash.ShouldNotContain(resultado.CodigoOtp!);

        enlace.VerificarOtp(resultado.CodigoOtp, Ahora.AddMinutes(1)).ShouldBeTrue();
        enlace.Usado.ShouldBeTrue();
        Should.Throw<ReglaDeNegocioException>(() => enlace.VerificarOtp(resultado.CodigoOtp, Ahora.AddMinutes(2))).Codigo.ShouldBe("enlace-usado");
    }

    [Fact]
    public void El_codigo_vencido_no_sirve()
    {
        var (enlace, _) = Nuevo(otp: true);
        var codigo = enlace.Canjear(Ahora).CodigoOtp;

        Should.Throw<ReglaDeNegocioException>(() => enlace.VerificarOtp(codigo, Ahora.AddMinutes(10))).Codigo.ShouldBe("otp-vencido");
    }

    [Fact]
    public void Cinco_intentos_fallidos_bloquean_el_enlace_aunque_se_pida_otro_codigo()
    {
        var (enlace, _) = Nuevo(otp: true);
        var codigo = enlace.Canjear(Ahora).CodigoOtp!;
        var malo = codigo == "000000" ? "111111" : "000000";

        for (var i = 1; i <= EnlaceDeAcceso.IntentosOtp; i++)
        {
            enlace.VerificarOtp(malo, Ahora.AddMinutes(1)).ShouldBeFalse();
            enlace.IntentosRestantes.ShouldBe(EnlaceDeAcceso.IntentosOtp - i);
        }

        var bloqueo = Should.Throw<ReglaDeNegocioException>(() => enlace.VerificarOtp(codigo, Ahora.AddMinutes(1)));
        bloqueo.Codigo.ShouldBe("otp-bloqueado");
        bloqueo.Tipo.ShouldBe(TipoFalla.DemasiadosIntentos);
        Should.Throw<ReglaDeNegocioException>(() => enlace.Canjear(Ahora.AddMinutes(2))).Codigo.ShouldBe("otp-bloqueado");
    }

    [Fact]
    public void Verificar_sin_haber_canjeado_es_conflicto()
    {
        var (enlace, _) = Nuevo(otp: true);
        Should.Throw<ReglaDeNegocioException>(() => enlace.VerificarOtp("123456", Ahora)).Codigo.ShouldBe("otp-no-solicitado");
    }
}

public class TextoLegalTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Cada_publicacion_es_una_version_nueva_con_su_hash()
    {
        var empresa = Guid.NewGuid();
        var tenant = Guid.NewGuid();

        var v1 = TextoLegal.Publicar(TipoTextoLegal.TerminosEmpresa, tenant, empresa, "  Términos v1  ", null, "u-1", Ahora);
        var v2 = TextoLegal.Publicar(TipoTextoLegal.TerminosEmpresa, tenant, empresa, "Términos v2", v1, "u-1", Ahora.AddDays(1));

        v1.Version.ShouldBe(1);
        v2.Version.ShouldBe(2);
        v1.Contenido.ShouldBe("Términos v1");
        v1.Hash.ShouldBe(TextoLegal.HashDe("Términos v1"));
        v1.Hash.Length.ShouldBe(64);
        v2.Hash.ShouldNotBe(v1.Hash);
        v1.Serie.ShouldBe(v2.Serie);
        v1.Ambito.ShouldBe(AmbitoTextoLegal.Empresa);
    }

    [Fact]
    public void El_aviso_es_de_plataforma_y_los_textos_de_empresa_necesitan_empresa()
    {
        TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, "Aviso", null, "p", Ahora).Ambito.ShouldBe(AmbitoTextoLegal.Plataforma);

        Should.Throw<ReglaDeNegocioException>(() =>
            TextoLegal.Publicar(TipoTextoLegal.AutorizacionEmpresa, null, null, "x", null, "p", Ahora)).Codigo.ShouldBe("empresa-requerida");
        Should.Throw<ReglaDeNegocioException>(() =>
            TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, Guid.NewGuid(), Guid.NewGuid(), "x", null, "p", Ahora)).Codigo.ShouldBe("ambito-invalido");
        Should.Throw<ReglaDeNegocioException>(() =>
            TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, "   ", null, "p", Ahora)).Codigo.ShouldBe("contenido-requerido");
    }
}

public class AceptacionDeInvitacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static readonly EvidenciaDeConsentimiento Evidencia =
        new(Guid.NewGuid(), "ana@aceros.co", "10.0.0.1", "pruebas", true, CanalAceptacion.Web);

    private static (Vinculo Vinculo, TextoLegal Aviso, TextoLegal Autorizacion, TextoLegal Terminos) Preparar()
    {
        var creado = Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "ana@aceros.co", Ahora);
        var vinculo = Vinculo.Create(creado);
        vinculo.Apply(vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora));

        var tenant = Guid.NewGuid();
        return (vinculo,
            TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, "Aviso", null, "p", Ahora),
            TextoLegal.Publicar(TipoTextoLegal.AutorizacionEmpresa, tenant, vinculo.EmpresaId, "Autorización", null, "u", Ahora),
            TextoLegal.Publicar(TipoTextoLegal.TerminosEmpresa, tenant, vinculo.EmpresaId, "Términos", null, "u", Ahora));
    }

    private static List<ConsentimientoOtorgado> Todos(params TextoLegal[] textos) =>
        [.. textos.Select(t => new ConsentimientoOtorgado(t.Tipo, t.Version, true))];

    [Fact]
    public void Registra_cada_consentimiento_con_version_hash_y_evidencia()
    {
        var (vinculo, aviso, autorizacion, terminos) = Preparar();

        var evento = vinculo.RegistrarConsentimientos([aviso, autorizacion, terminos], Todos(aviso, autorizacion, terminos), Evidencia, Ahora);
        vinculo.Apply(evento);
        vinculo.Apply(vinculo.AceptarInvitacion("ana@aceros.co", Ahora));

        evento.Consentimientos.Select(c => c.Tipo).ShouldBe([TipoTextoLegal.AvisoPlataforma, TipoTextoLegal.AutorizacionEmpresa, TipoTextoLegal.TerminosEmpresa]);
        evento.Consentimientos[2].Hash.ShouldBe(terminos.Hash);
        evento.Consentimientos[2].TextoLegalId.ShouldBe(terminos.Id);
        evento.Evidencia.ShouldBe(Evidencia);
        vinculo.Consentimientos.Count.ShouldBe(3);
        vinculo.InvitacionAceptada.ShouldBeTrue();
    }

    [Fact]
    public void Exige_los_textos_de_la_empresa_publicados()
    {
        var (vinculo, aviso, autorizacion, _) = Preparar();

        Should.Throw<ReglaDeNegocioException>(() =>
                vinculo.RegistrarConsentimientos([aviso, autorizacion], Todos(aviso, autorizacion), Evidencia, Ahora))
            .Codigo.ShouldBe("textos-legales-faltantes");
    }

    [Fact]
    public void Exige_cada_consentimiento_requerido_y_aceptado()
    {
        var (vinculo, aviso, autorizacion, terminos) = Preparar();

        Should.Throw<ReglaDeNegocioException>(() =>
                vinculo.RegistrarConsentimientos([aviso, autorizacion, terminos], Todos(autorizacion, terminos), Evidencia, Ahora))
            .Codigo.ShouldBe("consentimiento-faltante");

        var rechazado = Should.Throw<ReglaDeNegocioException>(() => vinculo.RegistrarConsentimientos([autorizacion, terminos],
            [new(TipoTextoLegal.AutorizacionEmpresa, 1, true), new(TipoTextoLegal.TerminosEmpresa, 1, false)], Evidencia, Ahora));
        rechazado.Codigo.ShouldBe("consentimiento-rechazado");
        rechazado.Tipo.ShouldBe(TipoFalla.Validacion);
    }

    [Fact]
    public void Una_version_distinta_a_la_vigente_es_texto_desactualizado()
    {
        var (vinculo, _, autorizacion, terminos) = Preparar();

        var ex = Should.Throw<ReglaDeNegocioException>(() => vinculo.RegistrarConsentimientos([autorizacion, terminos],
            [new(TipoTextoLegal.AutorizacionEmpresa, 1, true), new(TipoTextoLegal.TerminosEmpresa, 2, true)], Evidencia, Ahora));
        ex.Codigo.ShouldBe("texto-desactualizado");
        ex.Tipo.ShouldBe(TipoFalla.Conflicto);
    }

    [Fact]
    public void No_se_acepta_dos_veces()
    {
        var (vinculo, _, autorizacion, terminos) = Preparar();
        vinculo.Apply(vinculo.RegistrarConsentimientos([autorizacion, terminos], Todos(autorizacion, terminos), Evidencia, Ahora));
        vinculo.Apply(vinculo.AceptarInvitacion("ana@aceros.co", Ahora));

        Should.Throw<ReglaDeNegocioException>(() =>
                vinculo.RegistrarConsentimientos([autorizacion, terminos], Todos(autorizacion, terminos), Evidencia, Ahora))
            .Codigo.ShouldBe("invitacion-ya-aceptada");
    }

    [Fact]
    public void Un_vinculo_bloqueado_o_terminal_no_acepta()
    {
        var (bloqueado, _, autorizacion, terminos) = Preparar();
        bloqueado.Apply(bloqueado.Bloquear("Sanción", null, Ahora));
        Should.Throw<ReglaDeNegocioException>(() =>
                bloqueado.RegistrarConsentimientos([autorizacion, terminos], Todos(autorizacion, terminos), Evidencia, Ahora))
            .Codigo.ShouldBe("estado-invalido");

        var (anulado, _, autorizacionB, terminosB) = Preparar();
        anulado.Apply(anulado.Anular("Duplicado", Ahora));
        Should.Throw<ReglaDeNegocioException>(() =>
                anulado.RegistrarConsentimientos([autorizacionB, terminosB], Todos(autorizacionB, terminosB), Evidencia, Ahora))
            .Codigo.ShouldBe("estado-invalido");
    }

    [Fact]
    public void El_tercero_debe_aceptar_el_aviso_solo_si_no_tiene_la_version_vigente()
    {
        var tercero = new Terceros.Tercero();
        var v1 = TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, "Aviso 1", null, "p", Ahora);
        var v2 = TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, "Aviso 2", v1, "p", Ahora);

        tercero.RequiereAvisoPlataforma(v1).ShouldBeTrue();
        tercero.AceptarAvisoPlataforma(v1, Ahora);
        tercero.RequiereAvisoPlataforma(v1).ShouldBeFalse();
        tercero.RequiereAvisoPlataforma(v2).ShouldBeTrue();
        tercero.AvisoPlataformaAceptadoEn.ShouldBe(Ahora);
    }
}
