using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Sdk;

/// <summary>Por qué se rechazó una entrega de webhook.</summary>
public enum MotivoRechazoWebhook
{
    /// <summary>Falta <c>Validasoft-Signature</c> o no tiene <c>t=</c> y al menos un <c>v1=</c>.</summary>
    CabeceraInvalida,

    /// <summary>El <c>t=</c> está a más de la tolerancia (repetición o reloj desfasado).</summary>
    FueraDeTolerancia,

    /// <summary>Ninguna firma <c>v1=</c> coincide con los secretos configurados.</summary>
    FirmaNoCoincide,

    /// <summary>El cuerpo no es un evento público válido.</summary>
    CuerpoInvalido
}

public sealed class WebhookInvalidoException(MotivoRechazoWebhook motivo, string mensaje) : Exception(mensaje)
{
    public MotivoRechazoWebhook Motivo { get; } = motivo;
}

/// <summary>
/// Verifica las entregas de webhooks de Validasoft (docs/05 §6, NW-5) igual que <c>FirmaDeWebhook.Verificar</c> del servidor:
/// <c>Validasoft-Signature: t={unix},v1={hex(HMAC-SHA256(secreto, "{t}.{cuerpo}"))}[,v1=…]</c> (durante una rotación del
/// secreto llega un <c>v1</c> por cada secreto vigente), comparación en tiempo constante y <c>t</c> dentro de la tolerancia
/// (5 minutos). El cuerpo debe ser el texto <b>exacto</b> recibido (sin volver a serializarlo). Deduplica por
/// <see cref="EventoPublicoDto.Id"/> (= cabecera <c>Validasoft-Event-Id</c>, T-6), nunca por <c>Validasoft-Delivery</c>.
/// </summary>
public sealed class VerificadorDeWebhooks
{
    public const string CabeceraFirma = "Validasoft-Signature";
    public const string CabeceraEventoId = "Validasoft-Event-Id";
    public const string CabeceraTipo = "Validasoft-Event-Type";
    public const string CabeceraEntrega = "Validasoft-Delivery";
    public const string CabeceraTimestamp = "Validasoft-Timestamp";

    public static readonly TimeSpan ToleranciaPorDefecto = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IReadOnlyList<byte[]> _secretos;
    private readonly TimeSpan _tolerancia;
    private readonly Func<DateTimeOffset> _reloj;

    /// <param name="secreto">Secreto de la suscripción (<c>whsec_…</c>, tal como lo entregó Validasoft).</param>
    public VerificadorDeWebhooks(string secreto, TimeSpan? tolerancia = null, Func<DateTimeOffset>? reloj = null)
        : this([secreto], tolerancia, reloj)
    {
    }

    /// <param name="secretos">El actual y, mientras dura una rotación, el anterior: basta que coincida uno.</param>
    public VerificadorDeWebhooks(IEnumerable<string> secretos, TimeSpan? tolerancia = null, Func<DateTimeOffset>? reloj = null)
    {
        _secretos = (secretos ?? throw new ArgumentNullException(nameof(secretos)))
            .Where(s => !string.IsNullOrEmpty(s)).Select(s => Encoding.UTF8.GetBytes(s)).ToList();
        if (_secretos.Count == 0)
            throw new ArgumentException("Indica al menos un secreto.", nameof(secretos));
        _tolerancia = tolerancia ?? ToleranciaPorDefecto;
        _reloj = reloj ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>¿La firma es válida y reciente para este cuerpo?</summary>
    public bool FirmaValida(string? cabeceraFirma, string cuerpo) => Comprobar(cabeceraFirma, Encoding.UTF8.GetBytes(cuerpo)) is null;

    /// <summary>Verifica y devuelve el evento; lanza <see cref="WebhookInvalidoException"/> si no es auténtico (responde 401).</summary>
    public EventoPublicoDto Validar(string? cabeceraFirma, string cuerpo) => Validar(cabeceraFirma, Encoding.UTF8.GetBytes(cuerpo));

    /// <inheritdoc cref="Validar(string?, string)"/>
    public EventoPublicoDto Validar(string? cabeceraFirma, byte[] cuerpo)
    {
        if (Comprobar(cabeceraFirma, cuerpo) is { } motivo)
            throw new WebhookInvalidoException(motivo, motivo switch
            {
                MotivoRechazoWebhook.CabeceraInvalida => "Falta la cabecera Validasoft-Signature o no tiene t= y v1=.",
                MotivoRechazoWebhook.FueraDeTolerancia => "La firma es demasiado vieja o del futuro (revisa el reloj del servidor).",
                _ => "La firma no corresponde a ningún secreto configurado."
            });

        try
        {
            return JsonSerializer.Deserialize<EventoPublicoDto>(cuerpo, Json)
                   ?? throw new WebhookInvalidoException(MotivoRechazoWebhook.CuerpoInvalido, "El cuerpo está vacío.");
        }
        catch (JsonException e)
        {
            throw new WebhookInvalidoException(MotivoRechazoWebhook.CuerpoInvalido, "El cuerpo no es un evento público: " + e.Message);
        }
    }

    /// <summary>Lee las cabeceras con <paramref name="cabecera"/> (p. ej. <c>n => request.Headers[n]</c>) y valida.</summary>
    public EventoPublicoDto Validar(Func<string, string?> cabecera, string cuerpo) => Validar(cabecera(CabeceraFirma), cuerpo);

    /// <summary>Firma como lo hace Validasoft (para probar el receptor sin el servidor).</summary>
    public static string Firmar(string secreto, long timestamp, string cuerpo) =>
        Hex(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + "." + cuerpo)));

    /// <summary>Cabecera <c>Validasoft-Signature</c> completa con un <c>v1</c> por secreto.</summary>
    public static string Cabecera(IEnumerable<string> secretos, long timestamp, string cuerpo) =>
        "t=" + timestamp.ToString(CultureInfo.InvariantCulture) + "," + string.Join(",", secretos.Select(s => "v1=" + Firmar(s, timestamp, cuerpo)));

    private MotivoRechazoWebhook? Comprobar(string? cabecera, byte[] cuerpo)
    {
        long? timestamp = null;
        var firmas = new List<string>();
        foreach (var parte in (cabecera ?? string.Empty).Split(','))
        {
            var texto = parte.Trim();
            var igual = texto.IndexOf('=');
            if (igual <= 0)
                continue;
            var clave = texto.Substring(0, igual);
            var valor = texto.Substring(igual + 1);
            if (clave == "t" && long.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out var t))
                timestamp = t;
            else if (clave == "v1" && valor.Length == 64)
                firmas.Add(valor.ToLowerInvariant());
        }

        if (timestamp is not { } ts || firmas.Count == 0)
            return MotivoRechazoWebhook.CabeceraInvalida;
        if (Math.Abs(_reloj().ToUnixTimeSeconds() - ts) > _tolerancia.TotalSeconds)
            return MotivoRechazoWebhook.FueraDeTolerancia;

        var prefijo = Encoding.UTF8.GetBytes(ts.ToString(CultureInfo.InvariantCulture) + ".");
        var mensaje = new byte[prefijo.Length + cuerpo.Length];
        Buffer.BlockCopy(prefijo, 0, mensaje, 0, prefijo.Length);
        Buffer.BlockCopy(cuerpo, 0, mensaje, prefijo.Length, cuerpo.Length);

        var coincide = false;
        foreach (var secreto in _secretos)
        {
            var esperada = Encoding.ASCII.GetBytes(Hex(HMACSHA256.HashData(secreto, mensaje)));
            foreach (var firma in firmas)
                coincide |= CryptographicOperations.FixedTimeEquals(esperada, Encoding.ASCII.GetBytes(firma));
        }

        return coincide ? null : MotivoRechazoWebhook.FirmaNoCoincide;
    }

    private static string Hex(byte[] datos) => Convert.ToHexString(datos).ToLowerInvariant();
}
