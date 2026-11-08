using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Validasoft.Dominio.Certificados;

/// <summary>
/// JSON Canonicalization Scheme (RFC 8785): claves de cada objeto ordenadas por unidades UTF-16, sin espacios, cadenas
/// con el escape mínimo de ECMAScript (solo <c>"</c>, <c>\</c> y controles; lo demás va literal en UTF-8) y números con la
/// serialización de <c>Number.prototype.toString</c> (el doble más corto que vuelve al mismo valor). Es lo que se hashea
/// en el certificado (docs/03 §7.1): cualquier cliente que canonicalice el <c>contenidoCanonico</c> con RFC 8785 obtiene
/// los mismos bytes. Implementación propia (unas 150 líneas) para no depender de una librería: se prueba con los vectores
/// del RFC.
/// </summary>
public static class Jcs
{
    /// <summary>Canonicaliza un JSON en texto; falla si no es JSON válido, si tiene números no finitos o claves repetidas.</summary>
    public static string Canonicalizar(string json)
    {
        using var documento = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false });
        return Canonicalizar(documento.RootElement);
    }

    public static string Canonicalizar(JsonElement elemento)
    {
        var salida = new StringBuilder();
        Escribir(elemento, salida);
        return salida.ToString();
    }

    /// <summary>Bytes UTF-8 del JSON canónico (lo que entra al SHA-256).</summary>
    public static byte[] Bytes(string json) => Encoding.UTF8.GetBytes(Canonicalizar(json));

    private static void Escribir(JsonElement elemento, StringBuilder salida)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                var propiedades = elemento.EnumerateObject().ToList();
                var repetida = propiedades.GroupBy(p => p.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
                if (repetida is not null)
                    throw new FormatException($"La clave '{repetida.Key}' está repetida: RFC 8785 exige claves únicas.");

                salida.Append('{');
                var primera = true;
                // Orden por unidades de código UTF-16 (string.CompareOrdinal compara exactamente eso).
                foreach (var propiedad in propiedades.OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!primera)
                        salida.Append(',');
                    primera = false;
                    Cadena(propiedad.Name, salida);
                    salida.Append(':');
                    Escribir(propiedad.Value, salida);
                }

                salida.Append('}');
                break;
            case JsonValueKind.Array:
                salida.Append('[');
                var inicial = true;
                foreach (var item in elemento.EnumerateArray())
                {
                    if (!inicial)
                        salida.Append(',');
                    inicial = false;
                    Escribir(item, salida);
                }

                salida.Append(']');
                break;
            case JsonValueKind.String:
                Cadena(elemento.GetString()!, salida);
                break;
            case JsonValueKind.Number:
                if (!double.TryParse(elemento.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture, out var numero) || !double.IsFinite(numero))
                    throw new FormatException($"El número {elemento.GetRawText()} no es un doble IEEE 754 finito.");
                salida.Append(Numero(numero));
                break;
            case JsonValueKind.True:
                salida.Append("true");
                break;
            case JsonValueKind.False:
                salida.Append("false");
                break;
            case JsonValueKind.Null:
                salida.Append("null");
                break;
            default:
                throw new FormatException($"Valor JSON no admitido: {elemento.ValueKind}.");
        }
    }

    /// <summary>Cadena con el escape de JSON.stringify: <c>\b \t \n \f \r</c> cortos, otros controles <c>\u00xx</c> en minúsculas.</summary>
    private static void Cadena(string texto, StringBuilder salida)
    {
        salida.Append('"');
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (char.IsHighSurrogate(c) && (i + 1 >= texto.Length || !char.IsLowSurrogate(texto[i + 1])) || char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(texto[i - 1])))
                throw new FormatException("La cadena tiene un sustituto UTF-16 suelto: no es Unicode válido.");

            switch (c)
            {
                case '"': salida.Append("\\\""); break;
                case '\\': salida.Append("\\\\"); break;
                case '\b': salida.Append("\\b"); break;
                case '\t': salida.Append("\\t"); break;
                case '\n': salida.Append("\\n"); break;
                case '\f': salida.Append("\\f"); break;
                case '\r': salida.Append("\\r"); break;
                default:
                    if (c < 0x20)
                        salida.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else
                        salida.Append(c);
                    break;
            }
        }

        salida.Append('"');
    }

    /// <summary>
    /// <c>Number.prototype.toString</c> de ECMAScript (ES2015 §7.1.12.1) para un doble finito: los dígitos más cortos que
    /// vuelven al mismo valor (los da <c>"R"</c> en .NET) colocados en notación decimal entre 1e-7 y 1e21 y exponencial
    /// fuera de ese rango; <c>-0</c> es <c>0</c>.
    /// </summary>
    public static string Numero(double valor)
    {
        if (!double.IsFinite(valor))
            throw new FormatException("RFC 8785 no admite NaN ni infinitos.");
        if (valor == 0)
            return "0";

        var texto = Math.Abs(valor).ToString("R", CultureInfo.InvariantCulture);
        var exponente = 0;
        var corte = texto.IndexOfAny(['E', 'e']);
        if (corte >= 0)
        {
            exponente = int.Parse(texto[(corte + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            texto = texto[..corte];
        }

        var punto = texto.IndexOf('.');
        var entera = punto < 0 ? texto : texto[..punto];
        var fraccion = punto < 0 ? string.Empty : texto[(punto + 1)..];
        var digitos = entera + fraccion;
        var n = entera.Length + exponente; // posición del punto decimal respecto de los dígitos
        var ceros = digitos.Length - digitos.TrimStart('0').Length;
        digitos = digitos.TrimStart('0');
        n -= ceros;
        digitos = digitos.TrimEnd('0');
        var k = digitos.Length;

        string resultado;
        if (k <= n && n <= 21)
            resultado = digitos + new string('0', n - k);
        else if (0 < n && n <= 21)
            resultado = digitos[..n] + "." + digitos[n..];
        else if (-6 < n && n <= 0)
            resultado = "0." + new string('0', -n) + digitos;
        else
        {
            var e = n - 1;
            var signo = e < 0 ? "-" : "+";
            resultado = (k == 1 ? digitos : digitos[..1] + "." + digitos[1..]) + "e" + signo + Math.Abs(e).ToString(CultureInfo.InvariantCulture);
        }

        return valor < 0 ? "-" + resultado : resultado;
    }
}
