using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Passkeys (WebAuthn) con Fido2NetLib 4.3.2 (ING-2). Se piden <b>credenciales detectables con verificación de usuario obligatoria</b> y sin
/// atestación (<c>none</c>): nos importa que la clave pertenezca a la persona, no la marca del autenticador. El sitio (<c>rp.id</c>) y los orígenes
/// admitidos salen de <c>Validasoft:Ingreso:Passkeys:{RpId, Origenes[]}</c>; sin ellos se derivan de <c>Validasoft:Correo:UrlPublica</c> y, si tampoco
/// está, de la petición. En desarrollo (<c>RpId = localhost</c>) basta con listar los orígenes de Vite y de la API.
/// </summary>
public sealed class ServicioDePasskeysFido2(IConfiguration configuracion, IHttpContextAccessor accesor) : IServicioDePasskeys
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public string OpcionesDeRegistro(byte[] manejador, string nombre, string nombreVisible, IReadOnlyCollection<string> credencialesExistentes)
    {
        var opciones = Fido().RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = manejador, Name = nombre, DisplayName = nombreVisible },
            ExcludeCredentials = [.. credencialesExistentes.Select(id => new PublicKeyCredentialDescriptor(Base64UrlEncoder.DecodeBytes(id)))],
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            },
            AttestationPreference = AttestationConveyancePreference.None
        });
        return opciones.ToJson();
    }

    public async Task<RegistroDePasskeyVerificado> VerificarRegistroAsync(string opcionesJson, string respuestaJson, CancellationToken ct)
    {
        try
        {
            var respuesta = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(respuestaJson, OpcionesJson)
                            ?? throw new JsonException("Respuesta vacía.");
            var registrada = await Fido().MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = respuesta,
                OriginalOptions = CredentialCreateOptions.FromJson(opcionesJson),
                IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true)
            }, ct);

            return new RegistroDePasskeyVerificado(Base64UrlEncoder.Encode(registrada.Id), Base64UrlEncoder.Encode(registrada.User.Id),
                Base64UrlEncoder.Encode(registrada.PublicKey), registrada.SignCount, registrada.AaGuid == Guid.Empty ? null : registrada.AaGuid.ToString(),
                registrada.IsBackedUp, [.. (registrada.Transports ?? []).Select(t => t.ToString().ToLowerInvariant())]);
        }
        catch (Exception e) when (e is Fido2VerificationException or JsonException or FormatException or ArgumentException)
        {
            throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "No pudimos verificar la passkey. Vuelve a intentarlo.");
        }
    }

    public string OpcionesDeAutenticacion(IReadOnlyCollection<string>? credencialesPermitidas) =>
        Fido().GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [.. (credencialesPermitidas ?? []).Select(id => new PublicKeyCredentialDescriptor(Base64UrlEncoder.DecodeBytes(id)))],
            UserVerification = UserVerificationRequirement.Required
        }).ToJson();

    public async Task<AsertoVerificado> VerificarAsertoAsync(string opcionesJson, string respuestaJson, Func<string, CancellationToken, Task<Passkey?>> buscar,
        CancellationToken ct)
    {
        AuthenticatorAssertionRawResponse respuesta;
        try
        {
            respuesta = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(respuestaJson, OpcionesJson) ?? throw new JsonException("Respuesta vacía.");
        }
        catch (Exception e) when (e is JsonException or FormatException)
        {
            throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "No pudimos verificar la passkey. Vuelve a intentarlo.");
        }

        var passkey = await buscar(Base64UrlEncoder.Encode(respuesta.RawId), ct)
                      ?? throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "Esa passkey no está registrada en Validasoft.");
        passkey.AsegurarActiva();

        try
        {
            var resultado = await Fido().MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = respuesta,
                OriginalOptions = AssertionOptions.FromJson(opcionesJson),
                StoredPublicKey = Base64UrlEncoder.DecodeBytes(passkey.ClavePublica),
                StoredSignatureCounter = passkey.ContadorDeFirmas,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) =>
                    Task.FromResult(args.UserHandle is not { Length: > 0 } || Base64UrlEncoder.Encode(args.UserHandle) == passkey.ManejadorDeUsuario)
            }, ct);
            return new AsertoVerificado(passkey, resultado.SignCount);
        }
        catch (Fido2VerificationException e) when (e.Message.Contains("SignCount", StringComparison.OrdinalIgnoreCase))
        {
            // El contador de firmas no creció: la credencial podría estar clonada.
            throw ReglaDeNegocioException.NoAutenticado("passkey-clonada",
                "El contador de la passkey no avanzó: podría estar clonada. Pide a un administrador que la revoque y registra una nueva.");
        }
        catch (Exception e) when (e is Fido2VerificationException or JsonException or FormatException or ArgumentException)
        {
            throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "No pudimos verificar la passkey. Vuelve a intentarlo.");
        }
    }

    private Fido2 Fido()
    {
        var peticion = accesor.HttpContext?.Request;
        var publica = Uri.TryCreate(configuracion["Validasoft:Correo:UrlPublica"], UriKind.Absolute, out var url) ? url : null;
        var rpId = configuracion["Validasoft:Ingreso:Passkeys:RpId"] is { Length: > 0 } configurado
            ? configurado
            : publica?.Host ?? peticion?.Host.Host ?? "localhost";

        var origenes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var origen in configuracion.GetSection("Validasoft:Ingreso:Passkeys:Origenes").Get<string[]>() ?? [])
            if (!string.IsNullOrWhiteSpace(origen))
                origenes.Add(origen.Trim().TrimEnd('/'));
        if (publica is not null)
            origenes.Add(publica.GetLeftPart(UriPartial.Authority));
        if (origenes.Count == 0 && peticion is not null)
            origenes.Add($"{peticion.Scheme}://{peticion.Host}");

        return new Fido2(new Fido2Configuration
        {
            RPID = rpId,
            RPName = configuracion["Validasoft:Ingreso:Passkeys:Nombre"] is { Length: > 0 } nombre ? nombre : "Validasoft",
            Origins = origenes,
            TimestampDriftTolerance = 300_000
        });
    }
}
