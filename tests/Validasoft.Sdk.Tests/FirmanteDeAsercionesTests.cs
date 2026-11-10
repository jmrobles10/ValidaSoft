using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Sdk;

namespace Validasoft.Sdk.Tests;

/// <summary>Las aserciones del SDK pasan la misma validación que hace el servidor (VerificadorDeIdentidadDelegada).</summary>
public sealed class FirmanteDeAsercionesTests
{
    private static readonly UsuarioDelegado Laura = new("srm-laura", "Laura Revisora", "laura@constructora.co");

    private static async Task<JsonWebToken> ValidarComoElServidorAsync(string asercion, SecurityKey clave, string sistema)
    {
        var resultado = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(asercion, new TokenValidationParameters
        {
            ValidIssuer = sistema,
            ValidAudience = "validasoft",
            IssuerSigningKey = clave,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256],
            ValidateLifetime = true
        });
        resultado.IsValid.ShouldBeTrue(resultado.Exception?.Message);
        return (JsonWebToken)resultado.SecurityToken;
    }

    [Fact]
    public async Task Con_clave_rsa_firma_rs256_con_los_reclamos_que_exige_validasoft_y_un_jti_por_asercion()
    {
        using var rsa = RSA.Create(2048);
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", rsa.ExportPkcs8PrivateKeyPem());
        firmante.Algoritmo.ShouldBe("RS256");

        var primera = firmante.Firmar(Laura);
        var jwt = await ValidarComoElServidorAsync(primera, new RsaSecurityKey(rsa.ExportParameters(false)), "SRM");
        (jwt.Subject, jwt.GetPayloadValue<string>("name"), jwt.GetPayloadValue<string>("email")).ShouldBe(("srm-laura", "Laura Revisora", "laura@constructora.co"));
        (jwt.ValidTo - DateTime.UtcNow).ShouldBeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        jwt.Id.ShouldNotBeNullOrWhiteSpace();

        var segunda = await ValidarComoElServidorAsync(firmante.Firmar(Laura), new RsaSecurityKey(rsa.ExportParameters(false)), "SRM");
        segunda.Id.ShouldNotBe(jwt.Id);
    }

    [Fact]
    public async Task Con_clave_ec_p256_firma_es256()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var firmante = new FirmanteDeAsercionesDelegadas("MARCO", ec.ExportECPrivateKeyPem());
        firmante.Algoritmo.ShouldBe("ES256");
        await ValidarComoElServidorAsync(firmante.Firmar(Laura), new ECDsaSecurityKey(ECDsa.Create(ec.ExportParameters(false))), "MARCO");
    }

    [Fact]
    public async Task Otro_sistema_u_otra_clave_no_validan()
    {
        using var rsa = RSA.Create(2048);
        using var otra = RSA.Create(2048);
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", rsa.ExportPkcs8PrivateKeyPem());
        var asercion = firmante.Firmar(Laura);
        var handler = new JsonWebTokenHandler();
        (await handler.ValidateTokenAsync(asercion, new TokenValidationParameters
        {
            ValidIssuer = "SRM", ValidAudience = "validasoft", IssuerSigningKey = new RsaSecurityKey(otra.ExportParameters(false))
        })).IsValid.ShouldBeFalse();
        (await handler.ValidateTokenAsync(asercion, new TokenValidationParameters
        {
            ValidIssuer = "PORTAL", ValidAudience = "validasoft", IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false))
        })).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void No_firma_mas_de_cinco_minutos_ni_acepta_claves_debiles()
    {
        using var rsa = RSA.Create(2048);
        using var firmante = new FirmanteDeAsercionesDelegadas("SRM", rsa.ExportPkcs8PrivateKeyPem());
        Should.Throw<ArgumentOutOfRangeException>(() => firmante.Firmar(Laura, TimeSpan.FromMinutes(6)));

        using var debil = RSA.Create(1024);
        Should.Throw<ArgumentException>(() => new FirmanteDeAsercionesDelegadas("SRM", debil.ExportPkcs8PrivateKeyPem()));
        Should.Throw<ArgumentException>(() => new FirmanteDeAsercionesDelegadas(" ", rsa.ExportPkcs8PrivateKeyPem()));
    }
}
