using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Correo listo para enviar (texto plano y HTML).</summary>
public sealed record MensajeDeCorreo(string Para, string Asunto, string Texto, string Html);

/// <summary>Envío de correo; la implementación (SMTP con MailKit) vive en infraestructura.</summary>
public interface IEnviadorDeCorreo
{
    Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct);
}

public enum PlantillaCorreo
{
    /// <summary>Invitación de una empresa con el enlace de acceso (<c>invitacion</c>, docs/05 §8).</summary>
    Invitacion,

    /// <summary>Código OTP del enlace de acceso.</summary>
    CodigoOtp,

    /// <summary>Enlace de acceso tras el autorregistro (<c>autorregistro</c>, docs/05 §8).</summary>
    AccesoAutorregistro,

    /// <summary>Enlace para volver a entrar pedido con el correo (<c>POST /public/v1/acceso</c>, RE-1).</summary>
    AccesoPorCorreo
}

/// <summary>
/// Mensaje del outbox (cola local durable <c>correo</c>): se guarda en la transacción que crea el enlace o el código y
/// se despacha después de confirmarla. <see cref="SecretoProtegido"/> es el token o el código <b>cifrado</b>: el
/// secreto nunca queda en claro en las tablas de Wolverine ni en los logs (IN-3).
/// </summary>
public sealed record CorreoDeAccesoPendiente(
    PlantillaCorreo Plantilla, string Para, string SecretoProtegido, DateTimeOffset Expira, string? Empresa, string? TipoVinculo);

/// <summary>Arma el correo a partir de la plantilla; la URL pública sale de <c>Validasoft:Correo:UrlPublica</c>.</summary>
public sealed class RedactorDeCorreos(IConfiguration configuracion)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public string UrlPublica => (configuracion["Validasoft:Correo:UrlPublica"] ?? "http://localhost:5173").TrimEnd('/');

    public MensajeDeCorreo Redactar(CorreoDeAccesoPendiente correo, string secreto)
    {
        var vence = correo.Expira.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy, HH:mm 'UTC'", Cultura);
        return correo.Plantilla switch
        {
            PlantillaCorreo.Invitacion => ConEnlace(correo.Para,
                $"{correo.Empresa} te invita a validar tu vínculo como {Minuscula(correo.TipoVinculo)}",
                $"{correo.Empresa} te invitó a Validasoft para validar tu vínculo como {Minuscula(correo.TipoVinculo)}.",
                $"{UrlPublica}/e/{secreto}", vence),
            PlantillaCorreo.AccesoAutorregistro => ConEnlace(correo.Para,
                "Tu enlace de acceso a Validasoft",
                "Recibimos tu registro en Validasoft. Con este enlace entras a tu bóveda y ves las empresas que te han invitado.",
                $"{UrlPublica}/e/{secreto}", vence),
            PlantillaCorreo.AccesoPorCorreo => ConEnlace(correo.Para,
                "Tu enlace para entrar a Validasoft",
                "Pediste entrar a Validasoft. Con este enlace ves las empresas con las que tienes un vínculo y respondes lo que te pidan.",
                $"{UrlPublica}/e/{secreto}", vence),
            PlantillaCorreo.CodigoOtp => new MensajeDeCorreo(correo.Para,
                "Tu código de acceso a Validasoft",
                $"Tu código de acceso es {secreto}. Vence el {vence} y solo sirve para el enlace que acabas de abrir.\n\n" +
                "Si no fuiste tú, ignora este correo.",
                $"<p>Tu código de acceso es</p><p style=\"font-size:24px;letter-spacing:4px\"><b>{secreto}</b></p>" +
                $"<p>Vence el {WebUtility.HtmlEncode(vence)} y solo sirve para el enlace que acabas de abrir.</p>" +
                "<p>Si no fuiste tú, ignora este correo.</p>"),
            _ => throw new ArgumentOutOfRangeException(nameof(correo), correo.Plantilla, "Plantilla desconocida.")
        };
    }

    private static MensajeDeCorreo ConEnlace(string para, string asunto, string introduccion, string url, string vence) =>
        new(para, asunto,
            $"{introduccion}\n\nAbre este enlace (es personal, de un solo uso y vence el {vence}):\n{url}\n\n" +
            "Si no esperabas este correo, ignóralo.",
            $"<p>{WebUtility.HtmlEncode(introduccion)}</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">Abrir Validasoft</a></p>" +
            $"<p>El enlace es personal, de un solo uso y vence el {WebUtility.HtmlEncode(vence)}.</p>" +
            "<p>Si no esperabas este correo, ignóralo.</p>");

    /// <summary>
    /// Observaciones del expediente para el tercero: motivos (texto al tercero) y la observación escrita. Nunca nombra a
    /// los revisores ni a las áreas internas. Con <paramref name="token"/> lleva un enlace de acceso de un solo uso (RE-4);
    /// sin él (mensajes encolados antes de RE-4) remite a pedir uno con el correo.
    /// </summary>
    public MensajeDeCorreo RedactarObservacion(CorreoDeObservacionPendiente correo, string? token = null)
    {
        var asunto = $"{correo.Empresa} tiene observaciones sobre tu validación {correo.Folio}";
        var lineas = correo.Observaciones.Select(o =>
            (o.Requisito is null ? string.Empty : $"{o.Requisito}: ") + UnirMotivos(o.Motivos) +
            (string.IsNullOrWhiteSpace(o.Observacion) ? string.Empty : $" — {o.Observacion.Trim()}")).ToList();
        var texto = $"{correo.Empresa} revisó tu validación {correo.Folio} y necesita que corrijas lo siguiente:\n\n" +
                    string.Join("\n", lineas.Select(l => "- " + l)) + "\n\n" + ComoEntrar(token, correo.EnlaceExpira);
        var html = $"<p>{WebUtility.HtmlEncode(correo.Empresa)} revisó tu validación <b>{WebUtility.HtmlEncode(correo.Folio)}</b> " +
                   "y necesita que corrijas lo siguiente:</p><ul>" +
                   string.Concat(lineas.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>" +
                   ComoEntrarHtml(token, correo.EnlaceExpira);
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    /// <summary>
    /// Aviso consolidado de vencimiento (TD-6, docs/02 §8.3): un correo por tercero y fecha con cada documento y su fecha por
    /// empresa («vence para A el 31 de octubre de 2026 y para B el 30 de diciembre de 2026»). Sin enlace de acceso: remite a
    /// pedir uno con el correo.
    /// </summary>
    public MensajeDeCorreo RedactarAvisoDeVencimiento(Documentos.CorreoDeVencimientoPendiente correo)
    {
        var lineas = correo.Documentos.Select(d =>
            $"{d.Documento}: vence " + string.Join(" y ", d.Vencimientos.Select(v => $"para {v.Empresa} el {FechaLarga(v.VigenteHasta)}")) + ".").ToList();
        var asunto = correo.Documentos.Count == 1
            ? $"Tu {correo.Documentos[0].Documento} está por vencer"
            : $"Tienes {correo.Documentos.Count} documentos por vencer";
        var texto = "Estos documentos que entregaste están por vencer. Actualízalos antes de la fecha para seguir al día:\n\n" +
                    string.Join("\n", lineas.Select(l => "- " + l)) +
                    $"\n\nEntra a Validasoft ({UrlPublica}) y pide un enlace con tu correo para subir la versión nueva.";
        var html = "<p>Estos documentos que entregaste están por vencer. Actualízalos antes de la fecha para seguir al día:</p><ul>" +
                   string.Concat(lineas.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>" +
                   $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> y pide un enlace con tu correo para subir la versión nueva.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    private static string FechaLarga(DateOnly fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy", Cultura);

    /// <summary>
    /// Los textos al tercero de los motivos son oraciones completas («Falta la firma requerida.»): se unen como un
    /// párrafo, con su punto final, en lugar de separarlas con «; » (quedaba «…soportes.; Falta…»).
    /// </summary>
    public static string UnirMotivos(IEnumerable<string> motivos) =>
        string.Join(" ", motivos
            .Select(m => m.Trim())
            .Where(m => m.Length > 0)
            .Select(m => m[^1] is '.' or '!' or '?' ? m : m + "."));

    private string ComoEntrar(string? token, DateTimeOffset? expira) =>
        token is null
            ? $"Entra a Validasoft ({UrlPublica}) y pide un enlace con tu correo para responder y reenviar."
            : $"Para responder y reenviar, abre este enlace (es personal, de un solo uso y vence el {Fecha(expira)}):\n{UrlPublica}/e/{token}\n\n" +
              $"Si ya venció, entra a {UrlPublica} y pide uno nuevo con tu correo.";

    private string ComoEntrarHtml(string? token, DateTimeOffset? expira) =>
        token is null
            ? $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> y pide un enlace con tu correo para responder.</p>"
            : $"<p><a href=\"{WebUtility.HtmlEncode($"{UrlPublica}/e/{token}")}\">Responder en Validasoft</a></p>" +
              $"<p>El enlace es personal, de un solo uso y vence el {WebUtility.HtmlEncode(Fecha(expira))}. Si ya venció, entra a " +
              $"<a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> y pide uno nuevo con tu correo.</p>";

    private static string Fecha(DateTimeOffset? fecha) =>
        fecha?.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy, HH:mm 'UTC'", Cultura) ?? "pronto";

    private static string Minuscula(string? texto) => (texto ?? string.Empty).ToLower(Cultura);
}

/// <summary>Una observación vigente tal como la lee el tercero; <see cref="Requisito"/>: el documento observado (nombre del tipo), si lo hay.</summary>
public sealed record ObservacionParaCorreo(IReadOnlyList<string> Motivos, string? Observacion, string? Requisito = null);

/// <summary>
/// Correo al tercero cuando su expediente queda <c>Observado</c>; va por el outbox (cola <c>correo</c>) y sale solo si la
/// transacción del veredicto se confirma. <see cref="EnlaceProtegido"/> es el token del enlace de acceso creado en esa
/// misma transacción, <b>cifrado</b> como en <see cref="CorreoDeAccesoPendiente"/> (IN-3, RE-4).
/// </summary>
public sealed record CorreoDeObservacionPendiente(
    string Para, string Empresa, string Folio, IReadOnlyList<ObservacionParaCorreo> Observaciones, string? EnlaceProtegido = null,
    DateTimeOffset? EnlaceExpira = null);

public static class CorreoDeObservacionPendienteHandler
{
    public static Task Handle(CorreoDeObservacionPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, CancellationToken ct) =>
        enviador.EnviarAsync(redactor.RedactarObservacion(correo,
            correo.EnlaceProtegido is null ? null : protector.Desproteger(correo.EnlaceProtegido)), ct);
}

/// <summary>Despacha los correos del outbox. Wolverine reintenta si el servidor SMTP falla.</summary>
public static class CorreoDeAccesoPendienteHandler
{
    public static Task Handle(CorreoDeAccesoPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, CancellationToken ct) =>
        enviador.EnviarAsync(redactor.Redactar(correo, protector.Desproteger(correo.SecretoProtegido)), ct);
}
