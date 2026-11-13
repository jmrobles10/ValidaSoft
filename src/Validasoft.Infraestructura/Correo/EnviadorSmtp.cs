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
public sealed class EnviadorSmtp(IConfiguration configuracion, ILogger<EnviadorSmtp> logger) : ITransporteDeCorreo
{
    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        var seccion = configuracion.GetSection("Validasoft:Correo");
        var host = seccion["Host"];
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("Falta Validasoft:Correo:Host; el correo queda en la cola para reintento.");

        var puerto = seccion.GetValue("Puerto", 25);
        var seguridad = Enum.TryParse<SecureSocketOptions>(seccion["Seguridad"], true, out var opcion) ? opcion : SecureSocketOptions.Auto;

        var correo = ArmarMensaje(mensaje, seccion["Remitente"]);

        using var cliente = new SmtpClient();
        await cliente.ConnectAsync(host, puerto, seguridad, ct);
        if (seccion["Usuario"] is { Length: > 0 } usuario)
            await cliente.AuthenticateAsync(usuario, seccion["Clave"] ?? string.Empty, ct);
        await cliente.SendAsync(correo, ct);
        await cliente.DisconnectAsync(true, ct);

        logger.LogInformation("Correo enviado: {Asunto}", mensaje.Asunto);
    }

    /// <summary>
    /// El mensaje MIME listo para enviar: remitente (el propio de la empresa si su dominio está verificado, si no el de la plataforma), el
    /// <c>Reply-To</c> con el correo de soporte de la empresa si lo configuró, el texto plano y el HTML dentro del marco de marca con las imágenes que pide por <c>cid:</c> adjuntas en línea, sin pedirlas a ningún
    /// servidor (MA-1).
    /// </summary>
    internal static MimeMessage ArmarMensaje(MensajeDeCorreo mensaje, string? remitentePorOmision)
    {
        var correo = new MimeMessage();
        // Con el dominio de correo de la empresa verificado (OD-20) sale desde su dirección; si no, con el de la plataforma.
        correo.From.Add(mensaje.Remitente is { } propio
            ? new MailboxAddress(propio.Nombre, propio.Direccion)
            : MailboxAddress.Parse(remitentePorOmision is { Length: > 0 } remitente ? remitente : "Validasoft <no-responder@validasoft.local>"));
        correo.To.Add(MailboxAddress.Parse(mensaje.Para));
        // El correo de soporte de la empresa recibe las respuestas del tercero (Reply-To); una dirección ilegible no impide el envío.
        if (mensaje.ResponderA is { Length: > 0 } responderA && MailboxAddress.TryParse(responderA, out var respuestas))
            correo.ReplyTo.Add(respuestas);
        correo.Subject = mensaje.Asunto;
        var html = MarcoDeCorreo.Envolver(mensaje.Html);
        var cuerpo = new BodyBuilder { TextBody = mensaje.Texto, HtmlBody = html };
        foreach (var recurso in MarcoDeCorreo.RecursosUsados(html))
            cuerpo.LinkedResources.Add(recurso.Nombre, recurso.Contenido, ContentType.Parse(recurso.TipoMime)).ContentId = recurso.ContentId;
        correo.Body = cuerpo.ToMessageBody();
        return correo;
    }
}
