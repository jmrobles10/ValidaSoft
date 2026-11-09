using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identificaciones;

/// <summary>
/// Identificación de un tercero o de una empresa. La unicidad es <c>(Pais, Tipo, Numero)</c> con el número
/// normalizado y <b>sin</b> dígito de verificación (P-1-1); el DV se guarda aparte y se valida.
/// </summary>
public sealed record Identificacion(string Pais, string Tipo, string Numero, string? DigitoVerificacion)
{
    /// <summary>Clave estable para índices únicos, p. ej. <c>CO|NIT|900123456</c>.</summary>
    public string Clave => $"{Pais}|{Tipo}|{Numero}";

    /// <summary>
    /// Normaliza y valida. Acepta el número con puntos, espacios o guiones. Si el tipo tiene DV y el número
    /// trae guion (<c>900.123.456-7</c>), el último segmento se toma como DV. Si el DV llega aparte, debe
    /// coincidir con el calculado; si no llega, se calcula.
    /// </summary>
    public static Identificacion Crear(string pais, string tipo, string numero, string? digitoVerificacion = null)
    {
        var codigoPais = CatalogoIdentificacion.NormalizarPais(pais);
        var tipoId = CatalogoIdentificacion.Buscar(codigoPais, tipo)
            ?? throw ReglaDeNegocioException.Validacion("tipo-identificacion-invalido",
                $"El tipo de identificación '{tipo}' no existe para el país {codigoPais}.");

        var crudo = (numero ?? string.Empty).Trim().ToUpperInvariant();
        string? dvDelNumero = null;

        if (tipoId.TieneDv && crudo.Contains('-'))
        {
            var corte = crudo.LastIndexOf('-');
            dvDelNumero = crudo[(corte + 1)..].Trim();
            crudo = crudo[..corte];
        }

        var normalizado = Regex.Replace(crudo, @"[\s.\-]", string.Empty);
        if (!Regex.IsMatch(normalizado, tipoId.Patron))
            throw ReglaDeNegocioException.Validacion("numero-identificacion-invalido",
                $"El número '{numero}' no tiene el formato de {tipoId.Nombre} ({codigoPais}).");

        if (!tipoId.TieneDv)
            return new Identificacion(codigoPais, tipoId.Codigo, normalizado, null);

        var calculado = Identificaciones.DigitoVerificacion.Calcular(tipoId.AlgoritmoDv, normalizado)!;
        var informados = new[] { dvDelNumero, digitoVerificacion?.Trim().ToUpperInvariant() }
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct()
            .ToArray();

        if (informados.Length > 1 || (informados.Length == 1 && informados[0] != calculado))
            throw ReglaDeNegocioException.Validacion("digito-verificacion-invalido",
                $"El dígito de verificación no corresponde a {tipoId.Codigo} {normalizado} (esperado {calculado}).");

        return new Identificacion(codigoPais, tipoId.Codigo, normalizado, calculado);
    }
}
