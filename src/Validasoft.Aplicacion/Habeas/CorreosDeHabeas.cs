using System.Globalization;
using System.Net;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Habeas;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>En qué momento de la solicitud se le escribe al titular.</summary>
public enum MomentoDeSolicitudHabeas
{
    Recibida,
    Resuelta
}

/// <summary>
/// Correo al titular por una solicitud de habeas data (cola <c>correo</c>, por el outbox): el acuse con el radicado y la fecha límite, y
/// la respuesta con la constancia. <see cref="RespuestaProtegida"/> viaja cifrada con la clave maestra (la respuesta puede traer datos
/// del titular y el outbox es una tabla): nunca en claro.
/// </summary>
public sealed record CorreoDeSolicitudHabeasPendiente(
    string Para, string Radicado, MomentoDeSolicitudHabeas Momento, TipoDeSolicitudHabeas Tipo, string Destino, DateOnly VenceEl,
    ResultadoDeSolicitudHabeas? Resultado = null, string? RespuestaProtegida = null, string? ConstanciaHash = null, Guid? TenantId = null, Guid? EmpresaId = null);

/// <summary>Aviso al titular de que su exportación de la bóveda está lista para descargar (cola <c>correo</c>).</summary>
public sealed record CorreoDeExportacionListaPendiente(string Para, DateTimeOffset ExpiraEn, int Archivos);

public static class RedaccionDeCorreosDeHabeas
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static string Derecho(TipoDeSolicitudHabeas tipo) => tipo switch
    {
        TipoDeSolicitudHabeas.Consulta => "consulta",
        TipoDeSolicitudHabeas.Correccion => "corrección",
        _ => "supresión"
    };

    public static MensajeDeCorreo Redactar(this RedactorDeCorreos redactor, CorreoDeSolicitudHabeasPendiente correo, string? respuesta)
    {
        var fecha = correo.VenceEl.ToString("d 'de' MMMM 'de' yyyy", Cultura);
        var derecho = Derecho(correo.Tipo);
        string asunto, texto, html;
        if (correo.Momento == MomentoDeSolicitudHabeas.Recibida)
        {
            asunto = $"Recibimos tu solicitud de {derecho} ({correo.Radicado})";
            texto = $"Recibimos tu solicitud de {derecho} dirigida a {correo.Destino}.\n\nRadicado: {correo.Radicado}\nLa responderemos a más tardar el {fecha}, " +
                    $"dentro del plazo de la Ley 1581 de 2012.\n\nPuedes seguirla en Validasoft ({redactor.UrlPublica}/privacidad) entrando con tu correo.";
            html = $"<p>Recibimos tu solicitud de {WebUtility.HtmlEncode(derecho)} dirigida a <b>{WebUtility.HtmlEncode(correo.Destino)}</b>.</p>" +
                   $"<p>Radicado: <b>{WebUtility.HtmlEncode(correo.Radicado)}</b><br>La responderemos a más tardar el {WebUtility.HtmlEncode(fecha)}, dentro del plazo de la Ley 1581 de 2012.</p>" +
                   $"<p>Puedes seguirla en <a href=\"{WebUtility.HtmlEncode(redactor.UrlPublica + "/privacidad")}\">Validasoft</a> entrando con tu correo.</p>";
        }
        else
        {
            var resultado = correo.Resultado switch
            {
                ResultadoDeSolicitudHabeas.Rechazada => "no pudimos atenderla",
                ResultadoDeSolicitudHabeas.Parcial => "la atendimos conservando lo que la ley nos obliga a guardar",
                _ => "la atendimos"
            };
            asunto = $"Respondimos tu solicitud de {derecho} ({correo.Radicado})";
            texto = $"{correo.Destino} respondió tu solicitud de {derecho} ({correo.Radicado}): {resultado}.\n\n{respuesta}" +
                    (correo.ConstanciaHash is null ? string.Empty : $"\n\nConstancia (huella SHA-256): {correo.ConstanciaHash}") +
                    "\n\nGuarda este correo como prueba de la respuesta.";
            html = $"<p><b>{WebUtility.HtmlEncode(correo.Destino)}</b> respondió tu solicitud de {WebUtility.HtmlEncode(derecho)} ({WebUtility.HtmlEncode(correo.Radicado)}): {WebUtility.HtmlEncode(resultado)}.</p>" +
                   $"<p>{WebUtility.HtmlEncode(respuesta ?? string.Empty).Replace("\n", "<br>")}</p>" +
                   (correo.ConstanciaHash is null ? string.Empty : $"<p>Constancia (huella SHA-256): <code>{WebUtility.HtmlEncode(correo.ConstanciaHash)}</code></p>") +
                   "<p>Guarda este correo como prueba de la respuesta.</p>";
        }

        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    public static MensajeDeCorreo Redactar(this RedactorDeCorreos redactor, CorreoDeExportacionListaPendiente correo)
    {
        var hasta = correo.ExpiraEn.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy, HH:mm 'UTC'", Cultura);
        var enlace = redactor.UrlPublica + "/privacidad";
        return new MensajeDeCorreo(correo.Para, "Tu exportación de la bóveda está lista",
            $"Tu exportación de la bóveda ({correo.Archivos} archivos) está lista. Descárgala en Validasoft ({enlace}) entrando con tu correo; estará disponible hasta el {hasta}.",
            $"<p>Tu exportación de la bóveda ({correo.Archivos} archivos) está lista.</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(enlace)}\">Descárgala en Validasoft</a> entrando con tu correo; estará disponible hasta el {WebUtility.HtmlEncode(hasta)}.</p>");
    }
}

public static class CorreoDeSolicitudHabeasPendienteHandler
{
    public static async Task Handle(CorreoDeSolicitudHabeasPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector, RedactorDeCorreos redactor,
        CancellationToken ct)
    {
        var respuesta = correo.RespuestaProtegida is null ? null : protector.Desproteger(correo.RespuestaProtegida);
        await enviador.EnviarAsync(redactor.Redactar(correo, respuesta).DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }
}

public static class CorreoDeExportacionListaPendienteHandler
{
    public static Task Handle(CorreoDeExportacionListaPendiente correo, IEnviadorDeCorreo enviador, RedactorDeCorreos redactor, CancellationToken ct) =>
        enviador.EnviarAsync(redactor.Redactar(correo), ct);
}
