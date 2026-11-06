namespace Validasoft.Dominio.Identificaciones;

/// <summary>Cálculo de dígitos de verificación por país.</summary>
public static class DigitoVerificacion
{
    private static readonly int[] PesosNit = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];

    private static readonly int[] PesosCuit = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static string? Calcular(AlgoritmoDv algoritmo, string numero) => algoritmo switch
    {
        AlgoritmoDv.NitColombia => NitColombia(numero),
        AlgoritmoDv.RutChile => RutChile(numero),
        AlgoritmoDv.CuitArgentina => CuitArgentina(numero),
        _ => null
    };

    /// <summary>DIAN: pesos primos de derecha a izquierda, residuo módulo 11.</summary>
    public static string NitColombia(string numero)
    {
        var suma = 0;
        for (var i = 0; i < numero.Length; i++)
            suma += (numero[numero.Length - 1 - i] - '0') * PesosNit[i];

        var residuo = suma % 11;
        return (residuo > 1 ? 11 - residuo : residuo).ToString();
    }

    /// <summary>Módulo 11 con pesos 2..7 cíclicos de derecha a izquierda; 10 se escribe K.</summary>
    public static string RutChile(string numero)
    {
        var suma = 0;
        var peso = 2;
        for (var i = numero.Length - 1; i >= 0; i--)
        {
            suma += (numero[i] - '0') * peso;
            peso = peso == 7 ? 2 : peso + 1;
        }

        return (11 - (suma % 11)) switch
        {
            11 => "0",
            10 => "K",
            var r => r.ToString()
        };
    }

    /// <summary>AFIP: pesos 5432765432 sobre los diez primeros dígitos.</summary>
    public static string CuitArgentina(string numero)
    {
        var suma = 0;
        for (var i = 0; i < PesosCuit.Length; i++)
            suma += (numero[i] - '0') * PesosCuit[i];

        return (11 - (suma % 11)) switch
        {
            11 => "0",
            10 => "9",
            var r => r.ToString()
        };
    }
}
