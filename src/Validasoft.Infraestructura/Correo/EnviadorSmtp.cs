using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Correo;

/// <summary>
/// Envío SMTP con MailKit configurado por <c>Validasoft:Correo:{Host, Puerto, Remitente, Seguridad, Usuario, Clave}</c>.
/// En desarrollo apunta a Mailpit (SMTP 1025, interfaz en http://localhost:8025). Nunca registra el cuerpo del correo:
/// lleva el token del enlace o el código OTP.
/// </summary>
public sealed class EnviadorSmtp(IConfiguration configuracion, ILogger<EnviadorSmtp> logger) : IEnviadorDeCorreo
{
    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        var seccion = configuracion.GetSection("Validasoft:Correo");
        var host = seccion["Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Falta Validasoft:Correo:Host; el correo queda en la cola para reintento.");

        var puerto = seccion.GetValue("Puerto", 25);
        var seguridad = Enum.TryParse<SecureSocketOptions>(seccion["Seguridad"], true, out var opcion) ? opcion : SecureSocketOptions.Auto;

        var correo = new MimeMessage();
        correo.From.Add(MailboxAddress.Parse(seccion["Remitente"] is { Length: > 0 } remitente ? remitente : "Validasoft <no-responder@validasoft.local>"));
        correo.To.Add(MailboxAddress.Parse(mensaje.Para));
        correo.Subject = mensaje.Asunto;
        correo.Body = new BodyBuilder { TextBody = mensaje.Texto, HtmlBody = mensaje.Html }.ToMessageBody();

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(host, puerto, seguridad, ct);
        if (seccion["Usuario"] is { Length: > 0 } usuario)
            await cliente.AuthenticateAsync(usuario, seccion["Clave"] ?? string.Empty, ct);
        await cliente.SendAsync(correo, ct);
        await cliente.DisconnectAsync(true, ct);

        logger.LogInformation("Correo enviado: {Asunto}", mensaje.Asunto);
    }
}
