using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Dominio.Tests;

/// <summary>Plantillas de notificación de la empresa (PL-1): validación de los textos y composición del correo.</summary>
public class PlantillasNotificacionTests
{
    private static readonly MensajeDeCorreo Base = new("ana@aceros.co", "Asunto original", "Cuerpo del correo.", "<p>Cuerpo del correo.</p>");

    private static readonly IReadOnlyDictionary<string, string> Variables = EjemplosDeCorreo.VariablesDe("Norte S.A.S.", "NORTE-E-2026-000001");

    private static EventoDePlantilla Evento(string evento) => CatalogoDePlantillas.Buscar(evento).ShouldNotBeNull();

    [Fact]
    public void El_asunto_reemplaza_las_variables_como_texto()
    {
        var mensaje = ComposicionDeCorreo.Aplicar(Base, new TextosDePlantilla("{{empresa}} revisó tu {{ folio }}", null, null), Variables);

        mensaje.Asunto.ShouldBe("Norte S.A.S. revisó tu NORTE-E-2026-000001");
        mensaje.Texto.ShouldBe(Base.Texto);
        mensaje.Html.ShouldBe(Base.Html);
        mensaje.Para.ShouldBe(Base.Para);
    }

    [Fact]
    public void Cabecera_y_pie_van_antes_y_despues_del_cuerpo_en_texto_y_en_html()
    {
        var mensaje = ComposicionDeCorreo.Aplicar(Base, new TextosDePlantilla(null, "Hola de {{empresa}}.\nGracias.", "Compras\nTel. 601 555 0101"), Variables);

        mensaje.Asunto.ShouldBe("Asunto original");
        mensaje.Texto.ShouldBe("Hola de Norte S.A.S..\nGracias.\n\nCuerpo del correo.\n\nCompras\nTel. 601 555 0101");
        mensaje.Html.ShouldBe("<p>Hola de Norte S.A.S..<br>Gracias.</p><p>Cuerpo del correo.</p><p>Compras<br>Tel. 601 555 0101</p>");
    }

    [Fact]
    public void El_html_del_usuario_sale_escapado_nunca_crudo()
    {
        var mensaje = ComposicionDeCorreo.Aplicar(Base, new TextosDePlantilla(null, "<script>alert('x')</script>", "<b>{{empresa}}</b> & co"),
            EjemplosDeCorreo.VariablesDe("<i>Norte</i>"));

        mensaje.Html.ShouldNotContain("<script>");
        mensaje.Html.ShouldStartWith("<p>&lt;script&gt;alert(&#39;x&#39;)&lt;/script&gt;</p>");
        mensaje.Html.ShouldEndWith("<p>&lt;b&gt;&lt;i&gt;Norte&lt;/i&gt;&lt;/b&gt; &amp; co</p>");
        mensaje.Texto.ShouldStartWith("<script>"); // el texto plano no interpreta HTML
    }

    [Fact]
    public void Sin_plantilla_el_correo_no_cambia()
    {
        ComposicionDeCorreo.Aplicar(Base, new TextosDePlantilla(null, null, null), Variables).ShouldBe(Base);
    }

    [Fact]
    public void Los_textos_vacios_quedan_nulos_y_se_normalizan_los_saltos_de_linea()
    {
        var textos = PlantillaNotificacion.Normalizar(Evento("observacion"), "Correo", "  ", "Línea 1\r\nLínea 2  ", "");

        textos.ShouldBe(new TextosDePlantilla(null, "Línea 1\nLínea 2", null));
    }

    [Theory]
    [InlineData("whatsapp", null, null, null, "canal-invalido", null)]
    [InlineData("correo", "Una\nlínea de más", null, null, "asunto-invalido", "asunto")]
    [InlineData("correo", null, null, "Pie con {{documento}}", "variable-no-permitida", "pie")]
    [InlineData("correo", "{{ tercero }} y {{empresa}}", null, null, "variable-no-permitida", "asunto")]
    public void Valida_canal_asunto_y_variables_del_evento(string canal, string? asunto, string? cabecera, string? pie, string codigo, string? campo)
    {
        var error = Should.Throw<ReglaDeNegocioException>(() => PlantillaNotificacion.Normalizar(Evento("observacion"), canal, asunto, cabecera, pie));

        error.Codigo.ShouldBe(codigo);
        error.Tipo.ShouldBe(TipoFalla.Validacion);
        if (campo is not null)
            error.Datos.ShouldNotBeNull()["campo"].ShouldBe(campo);
    }

    [Fact]
    public void Valida_los_largos_y_dice_que_variables_se_permiten()
    {
        Should.Throw<ReglaDeNegocioException>(() => PlantillaNotificacion.Normalizar(Evento("vencido"), "correo", new string('a', 151), null, null))
            .Codigo.ShouldBe("asunto-demasiado-largo");
        var largo = Should.Throw<ReglaDeNegocioException>(() =>
            PlantillaNotificacion.Normalizar(Evento("vencido"), "correo", null, new string('a', 2001), null));
        largo.Codigo.ShouldBe("texto-demasiado-largo");
        largo.Datos.ShouldNotBeNull()["campo"].ShouldBe("cabecera");

        var variable = Should.Throw<ReglaDeNegocioException>(() =>
            PlantillaNotificacion.Normalizar(Evento("vencido"), "correo", "{{folio}}", null, null));
        variable.Message.ShouldContain("{{empresa}}, {{documento}}");
        PlantillaNotificacion.Normalizar(Evento("vencido"), "correo", new string('a', 150), new string('b', 2000), "{{documento}}").Asunto!.Length.ShouldBe(150);
    }

    [Fact]
    public void La_version_sube_en_cada_cambio_aunque_vuelva_al_predeterminado()
    {
        var empresa = Guid.NewGuid();
        var plantilla = PlantillaNotificacion.Nueva(empresa, Evento("invitacion"), "correo");
        plantilla.Id.ShouldBe($"{empresa:N}:invitacion:correo");

        plantilla.Actualizar(new TextosDePlantilla("{{empresa}} te espera", null, null), DateTimeOffset.UtcNow);
        plantilla.Personalizada.ShouldBeTrue();
        plantilla.Actualizar(new TextosDePlantilla(null, null, null), DateTimeOffset.UtcNow);

        plantilla.Version.ShouldBe(2);
        plantilla.Personalizada.ShouldBeFalse();
    }
}
