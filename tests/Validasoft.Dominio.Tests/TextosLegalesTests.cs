using Validasoft.Aplicacion.Consentimientos;

namespace Validasoft.Dominio.Tests;

/// <summary>Aviso de tratamiento de datos que se siembra al arrancar la plataforma.</summary>
public class TextosLegalesTests
{
    [Fact]
    public void El_aviso_sembrado_es_una_autorizacion_de_tratamiento_de_datos_sin_marcas_de_provisional()
    {
        var aviso = TextosLegales.AvisoPlataformaInicial;

        aviso.ShouldNotContain("PROVISIONAL", Case.Insensitive);
        aviso.ShouldNotContain("pendiente de revisión", Case.Insensitive);
        aviso.ShouldNotContain("[");
        aviso.ShouldContain("Ley 1581 de 2012");
        aviso.ShouldContain("encargado del tratamiento");
        aviso.ShouldContain("inteligencia artificial");
        aviso.ShouldContain("Transferencia internacional");
        aviso.ShouldContain("supresión");
    }
}
