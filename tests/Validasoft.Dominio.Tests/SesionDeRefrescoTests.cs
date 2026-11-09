using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Dominio.Tests;

public class SesionDeRefrescoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    private static (SesionDeRefresco Sesion, string Token) Nueva(bool otp = false) =>
        SesionDeRefresco.Abrir(Guid.NewGuid(), "Ana@Aceros.co", otp, Ahora);

    [Fact]
    public void Guarda_solo_el_hash_y_el_token_lleva_la_familia()
    {
        var (sesion, token) = Nueva();

        SesionDeRefresco.FamiliaDe(token).ShouldBe(sesion.Id);
        sesion.TokenHash.ShouldBe(SesionDeRefresco.HashDeToken(token));
        sesion.TokenHash.ShouldNotContain(token.Split('.')[1]);
        token.Split('.')[1].Length.ShouldBe(43); // 256 bits en base64url
        sesion.Correo.ShouldBe("ana@aceros.co");
        sesion.ExpiraEn.ShouldBe(Ahora.AddHours(8));
        sesion.Revocada.ShouldBeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-punto")]
    [InlineData("no-es-guid.abc")]
    public void Un_token_mal_formado_no_tiene_familia(string? token) => SesionDeRefresco.FamiliaDe(token).ShouldBeNull();

    [Fact]
    public void Renovar_rota_el_token_y_el_anterior_deja_de_servir()
    {
        var (sesion, token) = Nueva();

        var primera = sesion.Renovar(token, Ahora.AddMinutes(25));
        primera.Renovada.ShouldBeTrue();
        primera.Token.ShouldNotBe(token);
        SesionDeRefresco.FamiliaDe(primera.Token).ShouldBe(sesion.Id);
        sesion.TokenHash.ShouldBe(SesionDeRefresco.HashDeToken(primera.Token!));
        sesion.Rotaciones.ShouldBe(1);
        sesion.RotadaEn.ShouldBe(Ahora.AddMinutes(25));

        var segunda = sesion.Renovar(primera.Token!, Ahora.AddMinutes(50));
        segunda.Renovada.ShouldBeTrue();
        sesion.Rotaciones.ShouldBe(2);
    }

    [Fact]
    public void Reusar_un_token_rotado_revoca_toda_la_familia()
    {
        var (sesion, token) = Nueva();
        var nuevo = sesion.Renovar(token, Ahora.AddMinutes(1)).Token!;

        var reuso = sesion.Renovar(token, Ahora.AddMinutes(2));

        reuso.Renovada.ShouldBeFalse();
        reuso.Token.ShouldBeNull();
        sesion.Revocada.ShouldBeTrue();
        sesion.MotivoRevocacion.ShouldBe(SesionDeRefresco.MotivoReuso);
        sesion.RevocadaEn.ShouldBe(Ahora.AddMinutes(2));

        // El token vigente tampoco sirve: la familia completa quedó revocada.
        var ex = Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar(nuevo, Ahora.AddMinutes(3)));
        ex.Codigo.ShouldBe("sesion-revocada");
        ex.Tipo.ShouldBe(TipoFalla.NoAutenticado);
    }

    [Fact]
    public void Vence_a_las_ocho_horas_del_canje_aunque_se_renueve()
    {
        var (sesion, token) = Nueva();
        for (var minuto = 25; minuto < 8 * 60; minuto += 25)
            token = sesion.Renovar(token, Ahora.AddMinutes(minuto)).Token!;

        sesion.ExpiraEn.ShouldBe(Ahora.AddHours(8));
        var ex = Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar(token, Ahora.AddHours(8)));
        ex.Codigo.ShouldBe("sesion-expirada");
        ex.Tipo.ShouldBe(TipoFalla.NoAutenticado);
        sesion.Revocada.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Renovar_conserva_otp_tercero_y_correo(bool otp)
    {
        var (sesion, token) = Nueva(otp);
        var tercero = sesion.TerceroId;

        sesion.Renovar(token, Ahora.AddMinutes(30)).Renovada.ShouldBeTrue();

        sesion.OtpVerificado.ShouldBe(otp);
        sesion.TerceroId.ShouldBe(tercero);
        sesion.Correo.ShouldBe("ana@aceros.co");
    }

    [Fact]
    public void Cerrar_revoca_y_conserva_el_primer_motivo()
    {
        var (sesion, token) = Nueva();

        sesion.Revocar(SesionDeRefresco.MotivoCierre, Ahora.AddMinutes(5));
        sesion.Revocar(SesionDeRefresco.MotivoReuso, Ahora.AddMinutes(6));

        sesion.MotivoRevocacion.ShouldBe(SesionDeRefresco.MotivoCierre);
        sesion.RevocadaEn.ShouldBe(Ahora.AddMinutes(5));
        Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar(token, Ahora.AddMinutes(7))).Codigo.ShouldBe("sesion-revocada");
    }
}

public class LimiteDeAccesoPorCorreoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Permite_tres_envios_por_hora_por_correo()
    {
        var limite = LimiteDeAccesoPorCorreo.Nuevo(" Ana@Aceros.co ");
        limite.Id.ShouldBe(LimiteDeAccesoPorCorreo.IdDe("ana@aceros.co"));
        limite.Id.ShouldNotContain("aceros");

        limite.IntentarRegistrar(Ahora).ShouldBeTrue();
        limite.IntentarRegistrar(Ahora.AddMinutes(10)).ShouldBeTrue();
        limite.IntentarRegistrar(Ahora.AddMinutes(20)).ShouldBeTrue();
        limite.IntentarRegistrar(Ahora.AddMinutes(30)).ShouldBeFalse();

        // Pasada una hora del primero, cabe otro.
        limite.IntentarRegistrar(Ahora.AddMinutes(60)).ShouldBeTrue();
        limite.Envios.Count.ShouldBe(3);
    }
}
