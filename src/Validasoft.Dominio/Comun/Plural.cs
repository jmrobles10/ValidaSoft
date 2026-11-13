namespace Validasoft.Dominio.Comun;

/// <summary>
/// Concordancia en singular o plural de los textos que llevan una cantidad («1 día hábil», «2 días hábiles»): los avisos y
/// correos no deben decir «1 días».
/// </summary>
public static class Plural
{
    /// <summary>«1 día», «2 días»: el sustantivo va en singular solo cuando la cantidad es exactamente uno.</summary>
    public static string Contar(long cantidad, string singular, string plural) => $"{cantidad} {(cantidad == 1 ? singular : plural)}";

    public static string Dias(long dias) => Contar(dias, "día", "días");

    public static string DiasHabiles(long dias) => Contar(dias, "día hábil", "días hábiles");

    public static string Intentos(long intentos) => Contar(intentos, "intento", "intentos");

    public static string Filas(long filas) => Contar(filas, "fila", "filas");
}
