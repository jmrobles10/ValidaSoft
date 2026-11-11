using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Autenticador WebAuthn por software para las pruebas (credencial detectable con verificación de usuario, ES256, sin atestación): hace lo que el
/// navegador y la llave hacen al registrar y al firmar, así la API se prueba con respuestas criptográficamente reales.
/// </summary>
public sealed class AutenticadorVirtual
{
    private readonly ECDsa _clave = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public byte[] CredencialId { get; } = RandomNumberGenerator.GetBytes(32);

    public byte[] ManejadorDeUsuario { get; private set; } = [];

    public uint Contador { get; set; }

    /// <summary>Origen del que dice venir la página (<c>clientDataJSON.origin</c>).</summary>
    public string Origen { get; }

    /// <summary>Con <c>false</c> el autenticador no verifica al usuario (sin huella ni PIN): la API debe rechazarlo.</summary>
    public bool VerificaUsuario { get; set; } = true;

    public AutenticadorVirtual(string origen) => Origen = origen;

    public string CredencialIdBase64Url => Base64UrlEncoder.Encode(CredencialId);

    /// <summary>Respuesta a <c>navigator.credentials.create</c> con las opciones del servidor.</summary>
    public JsonElement Registrar(JsonElement opciones)
    {
        var rpId = opciones.GetProperty("rp").GetProperty("id").GetString()!;
        var desafio = opciones.GetProperty("challenge").GetString()!;
        ManejadorDeUsuario = Base64UrlEncoder.DecodeBytes(opciones.GetProperty("user").GetProperty("id").GetString()!);

        var cliente = DatosDelCliente("webauthn.create", desafio);
        var datos = DatosDelAutenticador(rpId, indicadores: 0x01 | (VerificaUsuario ? 0x04 : 0) | 0x40, Contador, conCredencial: true);

        var objeto = new CborWriter(CborConformanceMode.Ctap2Canonical);
        objeto.WriteStartMap(3);
        objeto.WriteTextString("fmt");
        objeto.WriteTextString("none");
        objeto.WriteTextString("attStmt");
        objeto.WriteStartMap(0);
        objeto.WriteEndMap();
        objeto.WriteTextString("authData");
        objeto.WriteByteString(datos);
        objeto.WriteEndMap();

        return Json(new
        {
            id = CredencialIdBase64Url,
            rawId = CredencialIdBase64Url,
            type = "public-key",
            response = new
            {
                attestationObject = Base64UrlEncoder.Encode(objeto.Encode()),
                clientDataJSON = Base64UrlEncoder.Encode(cliente),
                transports = new[] { "internal" }
            },
            clientExtensionResults = new { }
        });
    }

    /// <summary>Respuesta a <c>navigator.credentials.get</c>: firma la aserción y avanza el contador.</summary>
    public JsonElement Firmar(JsonElement opciones, bool conManejador = true)
    {
        var rpId = opciones.GetProperty("rpId").GetString()!;
        var desafio = opciones.GetProperty("challenge").GetString()!;
        Contador++;

        var cliente = DatosDelCliente("webauthn.get", desafio);
        var datos = DatosDelAutenticador(rpId, indicadores: 0x01 | (VerificaUsuario ? 0x04 : 0), Contador, conCredencial: false);
        var firma = _clave.SignData([.. datos, .. SHA256.HashData(cliente)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return Json(new
        {
            id = CredencialIdBase64Url,
            rawId = CredencialIdBase64Url,
            type = "public-key",
            response = new
            {
                authenticatorData = Base64UrlEncoder.Encode(datos),
                clientDataJSON = Base64UrlEncoder.Encode(cliente),
                signature = Base64UrlEncoder.Encode(firma),
                userHandle = conManejador && ManejadorDeUsuario.Length > 0 ? Base64UrlEncoder.Encode(ManejadorDeUsuario) : null
            },
            clientExtensionResults = new { }
        });
    }

    private byte[] DatosDelCliente(string tipo, string desafio) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type = tipo, challenge = desafio, origin = Origen, crossOrigin = false }));

    private byte[] DatosDelAutenticador(string rpId, int indicadores, uint contador, bool conCredencial)
    {
        var datos = new List<byte>();
        datos.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        datos.Add((byte)indicadores);
        datos.AddRange([(byte)(contador >> 24), (byte)(contador >> 16), (byte)(contador >> 8), (byte)contador]);
        if (conCredencial)
        {
            datos.AddRange(new byte[16]); // AAGUID: autenticador anónimo
            datos.AddRange([(byte)(CredencialId.Length >> 8), (byte)CredencialId.Length]);
            datos.AddRange(CredencialId);
            datos.AddRange(ClavePublicaCose());
        }

        return [.. datos];
    }

    /// <summary>Clave pública EC2 P-256 (ES256) en formato COSE.</summary>
    private byte[] ClavePublicaCose()
    {
        var parametros = _clave.ExportParameters(false);
        var cose = new CborWriter(CborConformanceMode.Ctap2Canonical);
        cose.WriteStartMap(5);
        cose.WriteInt32(1);
        cose.WriteInt32(2); // kty: EC2
        cose.WriteInt32(3);
        cose.WriteInt32(-7); // alg: ES256
        cose.WriteInt32(-1);
        cose.WriteInt32(1); // crv: P-256
        cose.WriteInt32(-2);
        cose.WriteByteString(parametros.Q.X!);
        cose.WriteInt32(-3);
        cose.WriteByteString(parametros.Q.Y!);
        cose.WriteEndMap();
        return cose.Encode();
    }

    private static JsonElement Json(object valor) => JsonSerializer.SerializeToElement(valor, new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never });
}
