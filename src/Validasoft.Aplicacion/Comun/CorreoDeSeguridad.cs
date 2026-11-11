using System.Globalization;
using System.Net;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Dominio.Identidad;
using Wolverine;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Qué cambió en la seguridad de la cuenta (PI-15, PI-18): lo que se le avisa a su dueña o dueño por correo.</summary>
public static class TiposDeAvisoDeSeguridad
{
    public const string PasskeyRegistrada = "passkey-registrada";
    public const string PasskeyRevocada = "passkey-revocada";
    public const string PasskeyEliminada = "passkey-eliminada";
    public const string CodigosGenerados = "codigos-generados";
    public const string RecuperacionUsada = "recuperacion-usada";
}

/// <summary>
/// Aviso de seguridad por correo a la persona dueña de la cuenta (outbox, cola <c>correo</c>): se registró o se revocó una passkey, se generaron códigos de
/// recuperación o se usó uno. <see cref="Detalle"/> es el nombre de la passkey (o cuántos códigos quedan); <see cref="PorAdministrador"/> dice si lo hizo otra
/// persona (un administrador del grupo o de Plataforma) y no ella; <see cref="Ip"/> y <see cref="Dispositivo"/> son los de quien hizo el cambio. Nunca lleva
/// secretos ni enlaces de un solo uso.
/// </summary>
public sealed record CorreoDeSeguridadPendiente(
    string Para, string Nombre, string Tipo, string? Detalle, bool PorAdministrador, string? Ip, string? Dispositivo, DateTimeOffset Fecha, Guid? TenantId = null);

public static class CorreoDeSeguridadPendienteHandler
{
    public static async Task Handle(CorreoDeSeguridadPendiente correo, IEnviadorDeCorreo enviador, RedactorDeCorreos redactor, CancellationToken ct) =>
        await enviador.EnviarAsync(Redactar(correo, redactor.UrlApp).DeTenant(correo.TenantId), ct);

    /// <summary>El correo: qué pasó, cuándo y desde dónde, y qué hacer si no fue la persona.</summary>
    public static MensajeDeCorreo Redactar(CorreoDeSeguridadPendiente correo, string urlApp)
    {
        var cultura = CultureInfo.GetCultureInfo("es-CO");
        var cuando = correo.Fecha.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy, HH:mm 'UTC'", cultura);
        var detalle = string.IsNullOrWhiteSpace(correo.Detalle) ? "tu passkey" : $"la passkey «{correo.Detalle}»";
        var desde = string.IsNullOrWhiteSpace(correo.Ip) ? string.Empty : $" desde la dirección {correo.Ip}";
        var quien = correo.PorAdministrador ? "Lo hizo un administrador" : "Lo hiciste tú";

        var (asunto, que) = correo.Tipo switch
        {
            TiposDeAvisoDeSeguridad.PasskeyRegistrada => ("Registraste una passkey en Validasoft", $"Se registró {detalle} en tu cuenta de Validasoft el {cuando}{desde}."),
            TiposDeAvisoDeSeguridad.PasskeyRevocada => ("Se revocó una passkey de tu cuenta de Validasoft", $"Se revocó {detalle} de tu cuenta el {cuando}. {quien}: esa passkey ya no sirve para entrar."),
            TiposDeAvisoDeSeguridad.PasskeyEliminada => ("Eliminaste una passkey de tu cuenta de Validasoft", $"Se eliminó {detalle} de tu cuenta el {cuando}{desde}."),
            TiposDeAvisoDeSeguridad.CodigosGenerados => ("Generaste códigos de recuperación en Validasoft", $"Se generó un juego nuevo de códigos de recuperación de tu cuenta el {cuando}{desde}. Los anteriores dejaron de valer."),
            TiposDeAvisoDeSeguridad.RecuperacionUsada => ("Usaste un código de recuperación en Validasoft", $"Se usó un código de recuperación para entrar a tu cuenta el {cuando}{desde}.{(string.IsNullOrWhiteSpace(correo.Detalle) ? string.Empty : " " + correo.Detalle)}"),
            _ => throw new ArgumentOutOfRangeException(nameof(correo), correo.Tipo, "Tipo de aviso desconocido.")
        };

        var texto = $"Hola, {correo.Nombre}.\n\n{que}\n\nSi fuiste tú, no tienes que hacer nada.\n\n" +
                    $"Si no fuiste tú, entra a {urlApp}, revisa «Mi cuenta» (passkeys y sesiones), revoca lo que no reconozcas y avisa a un administrador de tu grupo.";
        var html = $"<p>Hola, {WebUtility.HtmlEncode(correo.Nombre)}.</p><p>{WebUtility.HtmlEncode(que)}</p><p>Si fuiste tú, no tienes que hacer nada.</p>" +
                   $"<p>Si no fuiste tú, entra a <a href=\"{WebUtility.HtmlEncode(urlApp)}\">Validasoft</a>, revisa «Mi cuenta» (passkeys y sesiones), revoca lo que no reconozcas y avisa a un administrador de tu grupo.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }
}

/// <summary>Publica los avisos de seguridad de la cuenta (PI-15, PI-18) por el outbox de <see cref="IMessageBus"/>.</summary>
public static class AvisosDeSeguridad
{
    public static async Task PasskeyRegistradaAsync(IMessageBus mensajes, CuentaResuelta cuenta, Passkey passkey, ContextoDeCliente cliente, DateTimeOffset ahora) =>
        await mensajes.PublishAsync(new CorreoDeSeguridadPendiente(cuenta.Correo, cuenta.Nombre, TiposDeAvisoDeSeguridad.PasskeyRegistrada, passkey.Nombre, false, cliente.Ip,
            cliente.Dispositivo, ahora, cuenta.Cuenta.TenantId));

    public static async Task PasskeyRevocadaAsync(IMessageBus mensajes, string correo, string nombre, Guid? tenantId, string nombreDePasskey, bool porAdministrador,
        ContextoDeCliente cliente, DateTimeOffset ahora, bool eliminada = false) =>
        await mensajes.PublishAsync(new CorreoDeSeguridadPendiente(correo, nombre, eliminada ? TiposDeAvisoDeSeguridad.PasskeyEliminada : TiposDeAvisoDeSeguridad.PasskeyRevocada,
            nombreDePasskey, porAdministrador, cliente.Ip, cliente.Dispositivo, ahora, tenantId));

    public static async Task CodigosGeneradosAsync(IMessageBus mensajes, CuentaResuelta cuenta, ContextoDeCliente cliente, DateTimeOffset ahora) =>
        await mensajes.PublishAsync(new CorreoDeSeguridadPendiente(cuenta.Correo, cuenta.Nombre, TiposDeAvisoDeSeguridad.CodigosGenerados, null, false, cliente.Ip,
            cliente.Dispositivo, ahora, cuenta.Cuenta.TenantId));

    public static async Task RecuperacionUsadaAsync(IMessageBus mensajes, CuentaResuelta cuenta, int quedan, ContextoDeCliente cliente, DateTimeOffset ahora) =>
        await mensajes.PublishAsync(new CorreoDeSeguridadPendiente(cuenta.Correo, cuenta.Nombre, TiposDeAvisoDeSeguridad.RecuperacionUsada,
            $"Te quedan {quedan} códigos de recuperación.", false, cliente.Ip, cliente.Dispositivo, ahora, cuenta.Cuenta.TenantId));
}
