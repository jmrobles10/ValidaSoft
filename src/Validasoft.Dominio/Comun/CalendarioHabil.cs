namespace Validasoft.Dominio.Comun;

/// <summary>
/// Días hábiles (OD-1): de lunes a viernes, sin festivos. Para Colombia (<c>CO</c>) aplica la ley 51 de 1983 (Ley Emiliani): los
/// festivos fijos, los que pasan al lunes siguiente y los de Semana Santa y sus derivados (Ascensión, Corpus Christi y Sagrado
/// Corazón, también en lunes). Otros países cuentan solo lunes a viernes, más los festivos que se indiquen. Los plazos del habeas
/// data (Ley 1581 de 2012: 10 días hábiles para consultas y 15 para reclamos) se cuentan aquí.
/// </summary>
public static class CalendarioHabil
{
    public const string PaisPorDefecto = "CO";

    /// <summary>¿Es lunes a viernes y no es festivo (del país o de <paramref name="extras"/>)?</summary>
    public static bool EsHabil(DateOnly fecha, string? pais = PaisPorDefecto, IReadOnlySet<DateOnly>? extras = null) =>
        fecha.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !EsFestivo(fecha, pais, extras);

    public static bool EsFestivo(DateOnly fecha, string? pais = PaisPorDefecto, IReadOnlySet<DateOnly>? extras = null) =>
        (extras?.Contains(fecha) ?? false) ||
        (string.Equals(pais, PaisPorDefecto, StringComparison.OrdinalIgnoreCase) && FestivosDeColombia(fecha.Year).Contains(fecha));

    /// <summary>
    /// El día hábil que resulta de contar <paramref name="dias"/> días hábiles después de <paramref name="desde"/> (el día de
    /// <paramref name="desde"/> no cuenta, aunque sea hábil). Con 0 días devuelve <paramref name="desde"/>.
    /// </summary>
    public static DateOnly SumarDiasHabiles(DateOnly desde, int dias, string? pais = PaisPorDefecto, IReadOnlySet<DateOnly>? extras = null)
    {
        if (dias < 0)
            throw new ArgumentOutOfRangeException(nameof(dias), "Los días hábiles a sumar no pueden ser negativos.");
        var fecha = desde;
        for (var contados = 0; contados < dias;)
        {
            fecha = fecha.AddDays(1);
            if (EsHabil(fecha, pais, extras))
                contados++;
        }

        return fecha;
    }

    /// <summary>
    /// Cuántos días hábiles hay después de <paramref name="desde"/> y hasta <paramref name="hasta"/> inclusive (el día de
    /// <paramref name="desde"/> no cuenta). Negativo si <paramref name="hasta"/> es anterior: los días hábiles que ya pasaron.
    /// </summary>
    public static int DiasHabilesEntre(DateOnly desde, DateOnly hasta, string? pais = PaisPorDefecto, IReadOnlySet<DateOnly>? extras = null)
    {
        if (hasta < desde)
            return -DiasHabilesEntre(hasta, desde, pais, extras);
        var dias = 0;
        for (var fecha = desde.AddDays(1); fecha <= hasta; fecha = fecha.AddDays(1))
        {
            if (EsHabil(fecha, pais, extras))
                dias++;
        }

        return dias;
    }

    /// <summary>Festivos de Colombia de un año (ley 51 de 1983 y ley 1 de 1992 sobre Semana Santa).</summary>
    public static IReadOnlySet<DateOnly> FestivosDeColombia(int anio)
    {
        var festivos = new HashSet<DateOnly>
        {
            new(anio, 1, 1), new(anio, 5, 1), new(anio, 7, 20), new(anio, 8, 7), new(anio, 12, 8), new(anio, 12, 25)
        };

        // Pasan al lunes siguiente (si ya es lunes, se quedan).
        foreach (var (mes, dia) in new[] { (1, 6), (3, 19), (6, 29), (8, 15), (10, 12), (11, 1), (11, 11) })
            festivos.Add(ALunes(new DateOnly(anio, mes, dia)));

        // Semana Santa: jueves y viernes santos no se mueven; Ascensión, Corpus Christi y Sagrado Corazón pasan al lunes.
        var pascua = DomingoDePascua(anio);
        festivos.Add(pascua.AddDays(-3));
        festivos.Add(pascua.AddDays(-2));
        festivos.Add(ALunes(pascua.AddDays(39)));
        festivos.Add(ALunes(pascua.AddDays(60)));
        festivos.Add(ALunes(pascua.AddDays(68)));
        return festivos;
    }

    /// <summary>Domingo de Pascua (algoritmo de Meeus, Jones y Butcher del calendario gregoriano).</summary>
    public static DateOnly DomingoDePascua(int anio)
    {
        var a = anio % 19;
        var b = anio / 100;
        var c = anio % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mes = (h + l - 7 * m + 114) / 31;
        var dia = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(anio, mes, dia);
    }

    /// <summary>El mismo día si es lunes; si no, el lunes siguiente.</summary>
    private static DateOnly ALunes(DateOnly fecha) => fecha.AddDays((8 - (int)fecha.DayOfWeek) % 7);
}
