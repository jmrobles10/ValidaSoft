using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Tests;

/// <summary>Concordancia de las cantidades en avisos y correos: nunca «1 días hábiles».</summary>
public class PluralTests
{
    [Theory]
    [InlineData(0, "0 días hábiles")]
    [InlineData(1, "1 día hábil")]
    [InlineData(2, "2 días hábiles")]
    [InlineData(15, "15 días hábiles")]
    public void Los_dias_habiles_van_en_singular_solo_con_uno(int dias, string esperado) => Plural.DiasHabiles(dias).ShouldBe(esperado);

    [Theory]
    [InlineData(0, "0 días")]
    [InlineData(1, "1 día")]
    [InlineData(30, "30 días")]
    public void Los_dias_van_en_singular_solo_con_uno(int dias, string esperado) => Plural.Dias(dias).ShouldBe(esperado);

    [Theory]
    [InlineData(1, "1 intento")]
    [InlineData(4, "4 intentos")]
    public void Los_intentos_concuerdan_con_la_cantidad(int intentos, string esperado) => Plural.Intentos(intentos).ShouldBe(esperado);

    [Theory]
    [InlineData(1, "1 fila")]
    [InlineData(758, "758 filas")]
    public void Las_filas_concuerdan_con_la_cantidad(int filas, string esperado) => Plural.Filas(filas).ShouldBe(esperado);

    [Fact]
    public void Contar_acepta_cualquier_sustantivo_y_su_plural()
    {
        Plural.Contar(1, "fila invitada", "filas invitadas").ShouldBe("1 fila invitada");
        Plural.Contar(3, "fila invitada", "filas invitadas").ShouldBe("3 filas invitadas");
    }
}
