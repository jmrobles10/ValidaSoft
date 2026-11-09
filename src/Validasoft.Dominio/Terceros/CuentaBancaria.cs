using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Terceros;

public enum TipoCuentaBancaria
{
    Ahorros,
    Corriente
}

public enum EstadoCuentaBancaria
{
    Activa,
    Retirada
}

/// <summary>Lo que el tercero declara de una cuenta (el número viene en claro y se protege antes de guardarlo).</summary>
public sealed record DatosDeCuentaBancaria(string? Pais, string? Banco, string? TipoCuenta, string? Numero, string? Titular,
    string? IdentificacionTitular, string? Moneda);

/// <summary>
/// Cuenta bancaria global del tercero (CB-1, docs/03 §4.4), en la bóveda: la registra el tercero una vez y la puede
/// designar para cobrarle a cada empresa. El número se guarda protegido (<see cref="NumeroProtegido"/>) y en claro solo los
/// últimos cuatro dígitos. La certificación bancaria es el documento global <c>CERTIFICACION_BANCARIA</c> de la bóveda.
/// </summary>
public sealed partial class CuentaBancaria
{
    public Guid Id { get; set; }

    public Guid TerceroId { get; set; }

    public string Pais { get; set; } = "CO";

    public string Banco { get; set; } = string.Empty;

    public TipoCuentaBancaria TipoCuenta { get; set; }

    public string NumeroProtegido { get; set; } = string.Empty;

    public string Ultimos4 { get; set; } = string.Empty;

    public string Titular { get; set; } = string.Empty;

    public string IdentificacionTitular { get; set; } = string.Empty;

    public string Moneda { get; set; } = "COP";

    public EstadoCuentaBancaria Estado { get; set; }

    public DateTimeOffset RegistradaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }

    public string NumeroEnmascarado => Enmascarar(Ultimos4);

    public static string Enmascarar(string ultimos4) => $"****{ultimos4}";

    /// <summary>
    /// Valida lo declarado y devuelve la cuenta con el número protegido por <paramref name="proteger"/>. Número de 6 a 20
    /// dígitos (se aceptan espacios y guiones); banco, titular e identificación del titular obligatorios; moneda ISO de tres
    /// letras (COP por defecto).
    /// </summary>
    public static CuentaBancaria Registrar(Guid terceroId, DatosDeCuentaBancaria datos, Func<string, string> proteger, DateTimeOffset ahora)
    {
        var banco = Requerido(datos.Banco, "banco-requerido", "Indica el banco.");
        var titular = Requerido(datos.Titular, "titular-requerido", "Indica el titular de la cuenta.");
        var identificacion = Requerido(datos.IdentificacionTitular, "identificacion-titular-requerida", "Indica la identificación del titular.");
        if (!Enum.TryParse<TipoCuentaBancaria>(datos.TipoCuenta, true, out var tipo) || !Enum.IsDefined(tipo))
            throw ReglaDeNegocioException.Validacion("tipo-cuenta-invalido", "El tipo de cuenta es Ahorros o Corriente.");

        var numero = NoDigitos().Replace(datos.Numero ?? string.Empty, string.Empty);
        if (numero.Length is < 6 or > 20 || !(datos.Numero ?? string.Empty).All(c => char.IsDigit(c) || c is ' ' or '-'))
            throw ReglaDeNegocioException.Validacion("numero-cuenta-invalido", "El número de cuenta tiene entre 6 y 20 dígitos.");

        var moneda = string.IsNullOrWhiteSpace(datos.Moneda) ? "COP" : datos.Moneda.Trim().ToUpperInvariant();
        if (moneda.Length != 3 || !moneda.All(char.IsAsciiLetterUpper))
            throw ReglaDeNegocioException.Validacion("moneda-invalida", "La moneda es un código ISO de tres letras (COP, USD…).");
        var pais = string.IsNullOrWhiteSpace(datos.Pais) ? "CO" : datos.Pais.Trim().ToUpperInvariant();

        return new CuentaBancaria
        {
            Id = Guid.CreateVersion7(),
            TerceroId = terceroId,
            Pais = pais,
            Banco = banco,
            TipoCuenta = tipo,
            NumeroProtegido = proteger(numero),
            Ultimos4 = numero[^4..],
            Titular = titular,
            IdentificacionTitular = identificacion,
            Moneda = moneda,
            Estado = EstadoCuentaBancaria.Activa,
            RegistradaEn = ahora
        };
    }

    /// <summary>La retira el tercero: no se puede designar de nuevo. Lo ya designado y aprobado conserva su copia.</summary>
    public void Retirar(DateTimeOffset ahora)
    {
        if (Estado == EstadoCuentaBancaria.Retirada)
            throw ReglaDeNegocioException.Conflicto("cuenta-retirada", "La cuenta ya está retirada.");
        Estado = EstadoCuentaBancaria.Retirada;
        RetiradaEn = ahora;
    }

    private static string Requerido(string? valor, string codigo, string mensaje) =>
        string.IsNullOrWhiteSpace(valor) ? throw ReglaDeNegocioException.Validacion(codigo, mensaje) : valor.Trim();

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex NoDigitos();
}
