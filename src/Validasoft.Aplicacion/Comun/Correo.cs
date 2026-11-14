using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Correo listo para enviar (texto plano y HTML). <see cref="TenantId"/>: el tenant de quien lo origina; si es un sandbox el correo
/// no sale a su destinatario sino al buzón de pruebas (SB-4). Nulo = no es de un tenant (acceso del tercero sin empresa).
/// <see cref="ResponderA"/>: la dirección a la que llegan las respuestas (cabecera <c>Reply-To</c>): el correo de soporte que la
/// empresa configuró; el enviador la completa para los correos de una empresa (nulo = las respuestas van al remitente).
/// </summary>
public sealed record MensajeDeCorreo(string Para, string Asunto, string Texto, string Html, Guid? TenantId = null, Guid? EmpresaId = null,
    RemitenteDeCorreo? Remitente = null, string? ResponderA = null)
{
    /// <summary>
    /// El mismo correo atribuido al tenant que lo origina (lo usan todos los manejadores de la cola <c>correo</c>) y, si es de una empresa,
    /// a ella: si la empresa tiene un dominio de correo verificado, sale desde su dirección (OD-20).
    /// </summary>
    public MensajeDeCorreo DeTenant(Guid? tenantId, Guid? empresaId = null) => this with { TenantId = tenantId, EmpresaId = empresaId };
}

/// <summary>Envío de correo; la implementación (SMTP con MailKit) vive en infraestructura.</summary>
/// <remarks>
/// Los manejadores envían por aquí. La implementación (<c>EnviadorDeCorreo</c>) retiene en el buzón del sandbox los correos de un
/// tenant sandbox y entrega los demás al <see cref="ITransporteDeCorreo"/>.
/// </remarks>
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
    AccesoPorCorreo,

    /// <summary>Código de 6 dígitos del ingreso directo de una persona de un grupo o de un operador de Plataforma (ING-1).</summary>
    CodigoDeIngreso,

    /// <summary>Invitación a entrar directo a Validasoft con el correo (ING-5): sin enlace ni secreto, remite a la app.</summary>
    InvitacionDeUsuario,

    /// <summary>
    /// Aviso a un contacto del tercero de que una empresa invitó a otro correo (acceso aislado): con el enlace entra a su cuenta y lo aprueba o lo
    /// rechaza. <c>Empresa</c> es quien invitó y <c>CorreoInvitado</c>, a quién.
    /// </summary>
    SolicitudDeAcceso,

    /// <summary>
    /// Aviso a un contacto <b>verificado</b> del tercero de que otro contacto, sin verificar, pasó a acceso aislado porque este se verificó (el verificado prevalece): con el
    /// enlace entra a su cuenta y aprueba o rechaza a ese correo (<c>CorreoInvitado</c>). Sus versiones globales quedaron marcadas para revisión.
    /// </summary>
    ContactoEnRevision
}

/// <summary>
/// Mensaje del outbox (cola local durable <c>correo</c>): se guarda en la transacción que crea el enlace o el código y
/// se despacha después de confirmarla. <see cref="SecretoProtegido"/> es el token o el código <b>cifrado</b>: el
/// secreto nunca queda en claro en las tablas de Wolverine ni en los logs (IN-3). <see cref="TenantId"/> y
/// <see cref="EmpresaId"/> (al final y opcionales: los mensajes ya encolados siguen deserializando) dicen de qué empresa es
/// la invitación para aplicar su plantilla (PL-1).
/// </summary>
public sealed record CorreoDeAccesoPendiente(
    PlantillaCorreo Plantilla, string Para, string SecretoProtegido, DateTimeOffset Expira, string? Empresa, string? TipoVinculo,
    Guid? TenantId = null, Guid? EmpresaId = null, string? CorreoInvitado = null);

/// <summary>Arma el correo a partir de la plantilla; la URL pública sale de <c>Validasoft:Correo:UrlPublica</c>.</summary>
public sealed class RedactorDeCorreos(IConfiguration configuracion)
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public string UrlPublica => (configuracion["Validasoft:Correo:UrlPublica"] ?? "http://localhost:5173").TrimEnd('/');

    /// <summary>
    /// URL de <c>apps/web</c> (rutas con <c>#</c>) para los enlaces de los correos a revisores y administradores
    /// (<c>Validasoft:Correo:UrlApp</c>; por defecto <c>{UrlPublica}/app/</c>, SE-3).
    /// </summary>
    public string UrlApp => configuracion["Validasoft:Correo:UrlApp"] is { Length: > 0 } app ? app.TrimEnd('/') + "/" : UrlPublica + "/app/";

    /// <summary>Correo consolidado a una persona del tenant (NW-9): líneas de texto y, si hay, el enlace a la pantalla.</summary>
    public MensajeDeCorreo RedactarNotificacion(Notificaciones.CorreoDeNotificacionPendiente correo)
    {
        var enlace = correo.Ruta is null ? null : $"{UrlApp}#{correo.Ruta}";
        var saludo = $"Hola, {correo.Nombre}.";
        var texto = saludo + "\n\n" + string.Join("\n", correo.Lineas) +
                    (enlace is null ? string.Empty : $"\n\nÁbrelo en Validasoft:\n{enlace}") +
                    "\n\nPuedes apagar estos correos en tus preferencias de notificación.";
        var html = $"<p>{WebUtility.HtmlEncode(saludo)}</p>" + string.Concat(correo.Lineas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>")) +
                   (enlace is null ? string.Empty : $"<p><a href=\"{WebUtility.HtmlEncode(enlace)}\">Abrir en Validasoft</a></p>") +
                   "<p>Puedes apagar estos correos en tus preferencias de notificación.</p>";
        return new MensajeDeCorreo(correo.Para, correo.Asunto, texto, html);
    }

    /// <summary>
    /// Constancia al tercero de lo que aceptó (plantilla <c>consentimiento-empresa</c>, NW-10): cada texto con su versión y
    /// huella SHA-256 y la fecha. No incluye la IP ni otros datos de la evidencia.
    /// </summary>
    public MensajeDeCorreo RedactarConstancia(Notificaciones.CorreoDeConstanciaPendiente correo)
    {
        var fecha = correo.AceptadaEn.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy, HH:mm 'UTC'", Cultura);
        var lineas = correo.Textos.Select(t => $"{t.Titulo}, versión {t.Version} (huella {Corta(t.Hash)})").ToList();
        var asunto = $"Constancia de tu aceptación con {correo.Empresa}";
        var texto = $"El {fecha} aceptaste la invitación de {correo.Empresa} en Validasoft y estos textos:\n\n" +
                    string.Join("\n", lineas.Select(l => "- " + l)) +
                    "\n\nGuarda este correo como constancia. Puedes consultar los textos completos al entrar a Validasoft " +
                    $"({UrlPublica}) con tu correo.";
        var html = $"<p>El {WebUtility.HtmlEncode(fecha)} aceptaste la invitación de <b>{WebUtility.HtmlEncode(correo.Empresa)}</b> en Validasoft y estos textos:</p><ul>" +
                   string.Concat(lineas.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>" +
                   "<p>Guarda este correo como constancia. Puedes consultar los textos completos al entrar a " +
                   $"<a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> con tu correo.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    private static string Corta(string hash) => hash.Length > 16 ? hash[..16] : hash;

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
            PlantillaCorreo.SolicitudDeAcceso => ConEnlace(correo.Para,
                $"{correo.Empresa} invitó a {correo.CorreoInvitado} a tu información en Validasoft",
                $"{correo.Empresa} invitó al correo {correo.CorreoInvitado} a validar la información que Validasoft guarda de tu empresa" +
                (string.IsNullOrWhiteSpace(correo.TipoVinculo) ? string.Empty : $" como {Minuscula(correo.TipoVinculo)}") +
                ", y ese correo no figura entre los contactos verificados de tu empresa. Mientras nadie lo apruebe, esa persona entra con acceso aislado: " +
                "solo ve y sube lo de su relación con esa empresa y no ve tu bóveda de documentos ni tu información. " +
                "Entra para aprobarlo o rechazarlo; si no lo reconoces, recházalo.",
                $"{UrlPublica}/e/{secreto}", vence),
            PlantillaCorreo.ContactoEnRevision => ConEnlace(correo.Para,
                $"{correo.CorreoInvitado} pasó a acceso aislado en tu información de Validasoft",
                $"El correo {correo.CorreoInvitado} figuraba como contacto de tu empresa, pero no estaba verificado, y otro contacto se verificó: el verificado prevalece. " +
                "Mientras tanto esa persona entra con acceso aislado: no ve tu bóveda de documentos ni tu información, y los documentos que subió a tu bóveda quedaron marcados " +
                "para revisión (las empresas con las que están compartidos también se enteran). Entra para aprobarla de nuevo o rechazarla; si no la reconoces, recházala " +
                "y retira esas versiones.",
                $"{UrlPublica}/e/{secreto}", vence),
            PlantillaCorreo.CodigoDeIngreso => new MensajeDeCorreo(correo.Para,
                "Tu código para ingresar a Validasoft",
                $"Tu código para ingresar es {secreto}. Vence el {vence} y solo sirve una vez.\n\n" +
                "Si no pediste ingresar, ignora este correo: sin este código nadie entra a tu cuenta.",
                $"<p>Tu código para ingresar es</p><p style=\"font-size:24px;letter-spacing:4px\"><b>{secreto}</b></p>" +
                $"<p>Vence el {WebUtility.HtmlEncode(vence)} y solo sirve una vez.</p>" +
                "<p>Si no pediste ingresar, ignora este correo: sin este código nadie entra a tu cuenta.</p>"),
            PlantillaCorreo.InvitacionDeUsuario => Invitar(correo),
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

    /// <summary>
    /// Invitación a entrar directo (ING-5): <c>Empresa</c> trae el nombre del grupo y <c>TipoVinculo</c>, sus roles. No hay contraseña que crear ni enlace de un
    /// solo uso: la persona entra a la app con este mismo correo y pide su código allí.
    /// </summary>
    private MensajeDeCorreo Invitar(CorreoDeAccesoPendiente correo)
    {
        var rol = string.IsNullOrWhiteSpace(correo.TipoVinculo) ? string.Empty : $" como {correo.TipoVinculo}";
        var url = UrlApp;
        return new MensajeDeCorreo(correo.Para,
            $"{correo.Empresa} te invitó a Validasoft",
            $"{correo.Empresa} te dio acceso a Validasoft{rol}.\n\nEntra a {url} con este mismo correo: te enviaremos un código de 6 dígitos cada vez que ingreses. " +
            "No hay contraseña que crear; si tu rol lo exige, te guiaremos para registrar una passkey (tu huella, tu rostro o una llave de seguridad).\n\n" +
            "Si no esperabas esta invitación, ignora este correo.",
            $"<p>{WebUtility.HtmlEncode(correo.Empresa ?? "Una empresa")} te dio acceso a Validasoft{WebUtility.HtmlEncode(rol)}.</p>" +
            $"<p><a href=\"{WebUtility.HtmlEncode(url)}\">Entrar a Validasoft</a></p>" +
            "<p>Usa este mismo correo: te enviaremos un código de 6 dígitos cada vez que ingreses. No hay contraseña que crear; si tu rol lo exige, " +
            "te guiaremos para registrar una passkey (tu huella, tu rostro o una llave de seguridad).</p>" +
            "<p>Si no esperabas esta invitación, ignora este correo.</p>");
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
    /// Recordatorio de las observaciones que el tercero sigue teniendo pendientes (NT-2): las mismas observaciones vigentes del
    /// correo de observación, con los días que lleva devuelto el expediente y un enlace de acceso nuevo de un solo uso. Nunca
    /// nombra a los revisores ni a las áreas.
    /// </summary>
    public MensajeDeCorreo RedactarRecordatorioDeObservaciones(Expedientes.CorreoDeRecordatorioDeObservacionesPendiente correo, string? token = null)
    {
        var asunto = $"{correo.Empresa} te recuerda las observaciones de tu validación {correo.Folio}";
        var lineas = correo.Observaciones.Select(o =>
            (o.Requisito is null ? string.Empty : $"{o.Requisito}: ") + UnirMotivos(o.Motivos) +
            (string.IsNullOrWhiteSpace(o.Observacion) ? string.Empty : $" — {o.Observacion.Trim()}")).ToList();
        var pendiente = correo.DiasPendiente switch
        {
            <= 0 => "tiene observaciones pendientes",
            1 => "lleva 1 día con observaciones pendientes",
            var dias => $"lleva {dias} días con observaciones pendientes"
        };
        var texto = $"{correo.Empresa} sigue esperando tu respuesta: tu validación {correo.Folio} {pendiente}. Esto es lo que falta corregir:\n\n" +
                    string.Join("\n", lineas.Select(l => "- " + l)) + "\n\n" + ComoEntrar(token, correo.EnlaceExpira);
        var html = $"<p>{WebUtility.HtmlEncode(correo.Empresa)} sigue esperando tu respuesta: tu validación <b>{WebUtility.HtmlEncode(correo.Folio)}</b> " +
                   $"{WebUtility.HtmlEncode(pendiente)}. Esto es lo que falta corregir:</p><ul>" +
                   string.Concat(lineas.Select(l => $"<li>{WebUtility.HtmlEncode(l)}</li>")) + "</ul>" +
                   ComoEntrarHtml(token, correo.EnlaceExpira);
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    /// <summary>
    /// Resultado de la validación para el tercero (docs/05 §8, plantillas <c>rechazo</c> y <c>certificado</c>): aprobado (el
    /// vínculo quedó activo y su certificado se puede compartir) o rechazado con los textos al tercero de los motivos. Nunca
    /// lleva la justificación interna del rechazo ni nombres de revisores o áreas.
    /// </summary>
    public MensajeDeCorreo RedactarResultado(CorreoDeResultadoPendiente correo)
    {
        if (correo.Aprobado)
        {
            var asuntoAprobado = $"{correo.Empresa} aprobó tu validación {correo.Folio}";
            var textoAprobado = $"{correo.Empresa} aprobó tu validación {correo.Folio}: tu vínculo está activo y ya tienes tu certificado.\n\n" +
                                $"Entra a Validasoft ({UrlPublica}) con tu correo para verlo y compartirlo.";
            // La aprobación es el momento bueno del proceso: abre con el GIF de éxito de marca (MA-1).
            var htmlAprobado = MarcoDeCorreo.ImagenDeExito("Tu validación fue aprobada") +
                               $"<p>{WebUtility.HtmlEncode(correo.Empresa)} aprobó tu validación <b>{WebUtility.HtmlEncode(correo.Folio)}</b>: " +
                               "tu vínculo está activo y ya tienes tu certificado.</p>" +
                               $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> con tu correo para verlo y compartirlo.</p>";
            return new MensajeDeCorreo(correo.Para, asuntoAprobado, textoAprobado, htmlAprobado);
        }

        var motivos = UnirMotivos(correo.Motivos);
        // Una actualización rechazada deja el vínculo como estaba (RV-2): lo ya aprobado sigue vigente (VR-13).
        var asunto = correo.EsActualizacion
            ? $"{correo.Empresa} no aprobó tu actualización {correo.Folio}"
            : $"{correo.Empresa} cerró tu validación {correo.Folio} sin aprobarla";
        var queSePaso = correo.EsActualizacion
            ? "revisó tu actualización {0} y no la aprobó. Lo que ya tenías aprobado sigue vigente."
            : "revisó tu validación {0} y la cerró sin aprobarla.";
        var texto = $"{correo.Empresa} {string.Format(queSePaso, correo.Folio)}" +
                    (motivos.Length == 0 ? string.Empty : $"\n\nMotivos: {motivos}") +
                    "\n\nSi tienes dudas, escríbele directamente a la empresa.";
        var html = $"<p>{WebUtility.HtmlEncode(correo.Empresa)} {string.Format(queSePaso, $"<b>{WebUtility.HtmlEncode(correo.Folio)}</b>")}</p>" +
                   (motivos.Length == 0 ? string.Empty : $"<p>Motivos: {WebUtility.HtmlEncode(motivos)}</p>") +
                   "<p>Si tienes dudas, escríbele directamente a la empresa.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    /// <summary>
    /// El certificado dejó de valer (plantilla <c>certificado-revocado</c>, NW-16): revocado o suspendido, sin el motivo.
    /// </summary>
    public MensajeDeCorreo RedactarCertificado(Notificaciones.CorreoDeCertificadoPendiente correo)
    {
        var (asunto, que) = correo.Revocado
            ? ($"{correo.Empresa} revocó tu certificado {correo.Folio}", "fue revocado: ya no es válido y no puede volver a usarse")
            : ($"{correo.Empresa} suspendió tu certificado {correo.Folio}", "está suspendido mientras la empresa revisa tu vínculo");
        var texto = $"Tu certificado {correo.Folio} con {correo.Empresa} {que}. Quien lo verifique verá este estado.\n\n" +
                    "Si tienes dudas, escríbele directamente a la empresa.";
        var html = $"<p>Tu certificado <b>{WebUtility.HtmlEncode(correo.Folio)}</b> con {WebUtility.HtmlEncode(correo.Empresa)} " +
                   $"{WebUtility.HtmlEncode(que)}. Quien lo verifique verá este estado.</p><p>Si tienes dudas, escríbele directamente a la empresa.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    /// <summary>
    /// La empresa devolvió a revisión una validación ya aprobada (plantilla <c>desaprobacion</c>, DS-2): el vínculo queda en validación
    /// mientras la revisa y no hay nada que hacer por ahora. Sin la justificación (interna) ni nombres de revisores o áreas.
    /// </summary>
    public MensajeDeCorreo RedactarDesaprobacion(Notificaciones.CorreoDeDesaprobacionPendiente correo)
    {
        var asunto = $"{correo.Empresa} volvió a revisar tu validación {correo.Folio}";
        var parrafo = $"{correo.Empresa} devolvió a revisión tu validación {correo.Folio}, que ya estaba aprobada. Tu vínculo queda en validación mientras la revisa; " +
                      "no tienes que hacer nada por ahora y te avisamos cuando termine.";
        var texto = parrafo + $"\n\nPuedes ver el estado en Validasoft ({UrlPublica}) entrando con tu correo.";
        var html = $"<p>{WebUtility.HtmlEncode(parrafo)}</p>" +
                   $"<p>Puedes ver el estado en <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> entrando con tu correo.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, texto, html);
    }

    /// <summary>
    /// La empresa reintegró el vínculo del tercero (plantilla <c>reintegro</c>, DS-5): según el modo, no hay nada que hacer, hay una
    /// validación abierta con documentos por subir o falta completar la validación. Sin la justificación (interna).
    /// </summary>
    public MensajeDeCorreo RedactarReintegro(Notificaciones.CorreoDeReintegroPendiente correo)
    {
        var asunto = $"{correo.Empresa} reintegró tu vínculo";
        var folio = string.IsNullOrWhiteSpace(correo.Folio) ? string.Empty : $" ({correo.Folio})";
        var (parrafo, siguiente) = correo.Modo switch
        {
            Notificaciones.ModoDeReintegro.SinRevalidar => (
                $"{correo.Empresa} reintegró tu vínculo sin pedirte documentos nuevos: quedó como estaba antes y tu certificado se emitió de nuevo.",
                $"Puedes verlo y compartirlo en Validasoft ({UrlPublica}) entrando con tu correo."),
            Notificaciones.ModoDeReintegro.ConRenovacion => (
                $"{correo.Empresa} reintegró tu vínculo y abrió una nueva validación{folio}.",
                $"Entra a Validasoft ({UrlPublica}) con tu correo, sube o confirma tus documentos y completa la validación."),
            _ => (
                $"{correo.Empresa} reintegró tu vínculo: falta que completes la validación de tu información para dejarlo activo.",
                $"Entra a Validasoft ({UrlPublica}) con tu correo para completarla.")
        };
        return new MensajeDeCorreo(correo.Para, asunto, parrafo + "\n\n" + siguiente,
            $"<p>{WebUtility.HtmlEncode(parrafo)}</p><p>{WebUtility.HtmlEncode(siguiente)}</p>");
    }

    /// <summary>
    /// Un documento venció para una empresa (plantilla <c>vencido</c>, docs/05 §8): sin enlace de acceso, remite a pedir uno
    /// con el correo para subir la versión nueva.
    /// </summary>
    public MensajeDeCorreo RedactarVencido(Notificaciones.CorreoDeVencidoPendiente correo)
    {
        var fecha = FechaLarga(correo.VigenteHasta);
        var asunto = $"Tu {correo.Documento} venció para {correo.Empresa}";
        var texto = $"Tu {correo.Documento} venció para {correo.Empresa} (vigente hasta el {fecha}) y ya pasó el plazo de gracia.\n\n" +
                    $"Entra a Validasoft ({UrlPublica}) con tu correo y sube la versión nueva para seguir al día.";
        var html = $"<p>Tu {WebUtility.HtmlEncode(correo.Documento)} venció para {WebUtility.HtmlEncode(correo.Empresa)} " +
                   $"(vigente hasta el {WebUtility.HtmlEncode(fecha)}) y ya pasó el plazo de gracia.</p>" +
                   $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> con tu correo y sube la versión nueva para seguir al día.</p>";
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
    /// Plazo de cargue del expediente inicial (plantilla <c>caducidad</c>, LT-2, LT-3): recordatorio con los días que quedan,
    /// aviso de caducidad (la empresa puede reabrirlo) o reapertura con el plazo nuevo y un enlace de acceso.
    /// </summary>
    public MensajeDeCorreo RedactarPlazoDeCargue(Expedientes.CorreoDePlazoDeCarguePendiente correo, string? token = null)
    {
        var hasta = FechaLarga(Dominio.Documentos.CalculadoraDeVigencia.Fecha(correo.PlazoHasta));
        var (asunto, parrafo) = correo.Aviso switch
        {
            Expedientes.AvisoDePlazo.Recordatorio => (
                correo.DiasRestantes == 1
                    ? $"Mañana vence tu plazo para completar la validación {correo.Folio} con {correo.Empresa}"
                    : $"Te quedan {correo.DiasRestantes} días para completar la validación {correo.Folio} con {correo.Empresa}",
                $"{correo.Empresa} está esperando los documentos de tu validación {correo.Folio}. Tienes hasta el {hasta} para completarla; " +
                "si vence el plazo, la validación caduca y la empresa tendrá que reabrirla."),
            Expedientes.AvisoDePlazo.Caducado => (
                $"Tu validación {correo.Folio} con {correo.Empresa} caducó",
                $"El plazo para completar tu validación {correo.Folio} con {correo.Empresa} venció el {hasta} y la validación caducó. " +
                "Si todavía quieres continuar, escríbele a la empresa para que la reabra."),
            _ => (
                $"{correo.Empresa} reabrió tu validación {correo.Folio}",
                $"{correo.Empresa} reabrió tu validación {correo.Folio}. Tienes hasta el {hasta} para completarla.")
        };

        var entrar = token is null
            ? $"Entra a Validasoft ({UrlPublica}) y pide un enlace con tu correo."
            : $"Abre este enlace (es personal, de un solo uso y vence el {Fecha(correo.EnlaceExpira)}):\n{UrlPublica}/e/{token}";
        var entrarHtml = token is null
            ? $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(UrlPublica)}\">Validasoft</a> y pide un enlace con tu correo.</p>"
            : $"<p><a href=\"{WebUtility.HtmlEncode($"{UrlPublica}/e/{token}")}\">Completar en Validasoft</a></p>" +
              $"<p>El enlace es personal, de un solo uso y vence el {WebUtility.HtmlEncode(Fecha(correo.EnlaceExpira))}.</p>";
        var caducado = correo.Aviso == Expedientes.AvisoDePlazo.Caducado;
        return new MensajeDeCorreo(correo.Para, asunto,
            parrafo + (caducado ? string.Empty : "\n\n" + entrar),
            $"<p>{WebUtility.HtmlEncode(parrafo)}</p>" + (caducado ? string.Empty : entrarHtml));
    }

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
    DateTimeOffset? EnlaceExpira = null, Guid? TenantId = null, Guid? EmpresaId = null, Guid? TerceroId = null);

public static class CorreoDeObservacionPendienteHandler
{
    public static async Task Handle(CorreoDeObservacionPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        // El tercero puede apagar el correo de este tipo (NT-1): la campana de su portal avisa igual.
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.Observado, ct))
            return;
        var mensaje = redactor.RedactarObservacion(correo, correo.EnlaceProtegido is null ? null : protector.Desproteger(correo.EnlaceProtegido));
        await enviador.EnviarAsync((await plantillas.AplicarAsync(mensaje, CatalogoDePlantillas.Observacion, correo.TenantId, correo.EmpresaId,
            EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct)).DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }
}

/// <summary>
/// Correo al tercero cuando su expediente se cierra aprobado o rechazado (outbox, cola <c>correo</c>): sale solo si la
/// transacción de la decisión se confirma. <see cref="Motivos"/>: textos al tercero de los motivos del rechazo.
/// </summary>
public sealed record CorreoDeResultadoPendiente(string Para, string Empresa, string Folio, bool Aprobado, IReadOnlyList<string> Motivos,
    Guid? TenantId = null, Guid? EmpresaId = null, Guid? TerceroId = null, bool EsActualizacion = false);

public static class CorreoDeResultadoPendienteHandler
{
    public static async Task Handle(CorreoDeResultadoPendiente correo, IEnviadorDeCorreo enviador, RedactorDeCorreos redactor,
        PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.Resultado, ct))
            return;
        await enviador.EnviarAsync((await plantillas.AplicarAsync(redactor.RedactarResultado(correo), CatalogoDePlantillas.Resultado, correo.TenantId,
            correo.EmpresaId, EjemplosDeCorreo.VariablesDe(correo.Empresa, correo.Folio), ct)).DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }
}

/// <summary>Despacha los correos del outbox. Wolverine reintenta si el servidor SMTP falla.</summary>
public static class CorreoDeAccesoPendienteHandler
{
    /// <summary>Solo la invitación es de una empresa y lleva su plantilla (PL-1); OTP y enlaces de acceso no se personalizan.</summary>
    public static async Task Handle(CorreoDeAccesoPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector,
        RedactorDeCorreos redactor, PlantillasDeCorreo plantillas, CancellationToken ct)
    {
        var mensaje = redactor.Redactar(correo, protector.Desproteger(correo.SecretoProtegido));
        if (correo.Plantilla == PlantillaCorreo.Invitacion)
            mensaje = await plantillas.AplicarAsync(mensaje, CatalogoDePlantillas.Invitacion, correo.TenantId, correo.EmpresaId,
                EjemplosDeCorreo.VariablesDe(correo.Empresa, tipoVinculo: correo.TipoVinculo), ct);
        await enviador.EnviarAsync(mensaje.DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }
}
