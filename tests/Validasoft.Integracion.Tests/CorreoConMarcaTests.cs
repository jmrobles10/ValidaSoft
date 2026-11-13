using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Infraestructura.Correo;

namespace Validasoft.Integracion.Tests;

/// <summary>El correo tal como sale por SMTP con la marca (MA-1): el HTML dentro del marco y sus imágenes dentro del mensaje, y el manifest de las apps.</summary>
public class CorreoConMarcaTests
{
    private static MimeMessage Armar(string html, string texto = "Hola.", RemitenteDeCorreo? remitente = null) =>
        EnviadorSmtp.ArmarMensaje(new MensajeDeCorreo("ana@aceros.co", "Asunto", texto, html, Remitente: remitente), "Validasoft <avisos@validasoft.test>");

    private static string Html(MimeMessage correo) => correo.HtmlBody.ShouldNotBeNull();

    private static MimePart[] Imagenes(MimeMessage correo) => [.. correo.BodyParts.OfType<MimePart>().Where(p => p.ContentType.MediaType == "image")];

    [Fact]
    public void El_correo_sale_con_el_logotipo_dentro_del_mensaje_y_sin_pedirlo_a_ningun_servidor()
    {
        var correo = Armar("<p>Hola, Ana.</p>");

        Html(correo).ShouldContain("<p>Hola, Ana.</p>");
        Html(correo).ShouldContain("src=\"cid:validasoft-logotipo\"");
        Html(correo).ShouldNotContain("http://");
        Html(correo).ShouldNotContain("https://");
        var logotipo = Imagenes(correo).ShouldHaveSingleItem();
        logotipo.ContentId.ShouldBe("validasoft-logotipo");
        logotipo.ContentType.MimeType.ShouldBe("image/png");
        logotipo.ContentDisposition.ShouldNotBeNull().Disposition.ShouldBe(ContentDisposition.Inline);
        logotipo.Content.ShouldNotBeNull();
        correo.TextBody.ShouldBe("Hola.");
    }

    [Fact]
    public void El_correo_de_aprobacion_lleva_ademas_el_gif_de_exito()
    {
        var redactor = new RedactorDeCorreos(new ConfigurationBuilder()
            .AddInMemoryCollection([new("Validasoft:Correo:UrlPublica", "https://valida.test")]).Build());
        var aprobacion = redactor.RedactarResultado(new CorreoDeResultadoPendiente("ana@aceros.co", "Constructora Norte S.A.S.", "NORTE-E-2026-000001", true, []));

        var correo = EnviadorSmtp.ArmarMensaje(aprobacion, null);

        Imagenes(correo).Select(i => (i.ContentId, i.ContentType.MimeType)).ShouldBe(
            [("validasoft-logotipo", "image/png"), ("validasoft-exito", "image/gif")], ignoreOrder: true);
        Html(correo).ShouldContain("src=\"cid:validasoft-exito\"");
        correo.TextBody.ShouldNotBeNull().ShouldNotContain("cid:");
        // El enlace de siempre sigue en el cuerpo (el marco no toca el contenido).
        Html(correo).ShouldContain("href=\"https://valida.test\"");
        correo.From.Mailboxes.Single().Address.ShouldBe("no-responder@validasoft.local");
    }

    [Fact]
    public void Un_correo_sin_imagen_de_exito_no_adjunta_el_gif_y_el_remitente_propio_de_la_empresa_gana()
    {
        var correo = Armar("<p>No se aprobó.</p>", remitente: new RemitenteDeCorreo("Constructora Norte", "validacion@norte.co"));

        Imagenes(correo).ShouldHaveSingleItem().ContentId.ShouldBe("validasoft-logotipo");
        correo.From.Mailboxes.Single().Address.ShouldBe("validacion@norte.co");
    }

    [Fact]
    public void El_correo_de_soporte_de_la_empresa_va_como_Reply_To_y_sin_el_las_respuestas_van_al_remitente()
    {
        var conSoporte = EnviadorSmtp.ArmarMensaje(
            new MensajeDeCorreo("ana@aceros.co", "Asunto", "Hola.", "<p>Hola.</p>", ResponderA: "proveedores@norte.example"), "Validasoft <avisos@validasoft.test>");
        conSoporte.ReplyTo.Mailboxes.Single().Address.ShouldBe("proveedores@norte.example");
        conSoporte.From.Mailboxes.Single().Address.ShouldBe("avisos@validasoft.test"); // el remitente no cambia

        Armar("<p>Hola.</p>").ReplyTo.ShouldBeEmpty();
        // Una dirección ilegible no impide que el correo salga.
        EnviadorSmtp.ArmarMensaje(new MensajeDeCorreo("ana@aceros.co", "Asunto", "Hola.", "<p>Hola.</p>", ResponderA: "esto no es un correo"), null)
            .ReplyTo.ShouldBeEmpty();
    }

    [Fact]
    public void Sin_remitente_propio_sale_con_el_configurado_de_la_plataforma()
    {
        Armar("<p>Hola.</p>").From.Mailboxes.Single().Address.ShouldBe("avisos@validasoft.test");
    }

    [Fact]
    public void Las_apps_publican_un_manifest_con_su_tipo_de_contenido_conocido_por_el_servidor_de_archivos()
    {
        // El manifest y los íconos de las apps (`public/` de cada una) se sirven como archivos estáticos: el servidor debe conocer su tipo.
        var tipos = new FileExtensionContentTypeProvider();
        tipos.TryGetContentType("manifest.webmanifest", out var manifest).ShouldBeTrue();
        manifest.ShouldBe("application/manifest+json");
        tipos.TryGetContentType("favicon.svg", out var svg).ShouldBeTrue();
        svg.ShouldBe("image/svg+xml");
        tipos.TryGetContentType("icon-192.png", out var png).ShouldBeTrue();
        png.ShouldBe("image/png");
    }
}
