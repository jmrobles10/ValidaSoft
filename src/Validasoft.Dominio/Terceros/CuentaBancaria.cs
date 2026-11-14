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

/// <summary>
/// Persona del banco o del tercero a quien escribir por los pagos de esta cuenta (IO-4): la lleva la cuenta y viaja con ella a cualquier ERP.
/// </summary>
public sealed record ContactoDePagos(string Nombre, string Correo, string? Telefono);

/// <summary>
/// Lo que el tercero declara de una cuenta (el número viene en claro y se protege antes de guardarlo). El banco se toma del catálogo
/// de plataforma: <see cref="BancoCodigo"/> es su código de compensación local (CENIT en Colombia) y reemplaza al nombre escrito;
/// <see cref="BancoSwift"/> es el SWIFT/BIC (de un banco fuera del catálogo, o el que el catálogo ya trae).
/// </summary>
public sealed record DatosDeCuentaBancaria(string? Pais, string? Banco, string? TipoCuenta, string? Numero, string? Titular,
    string? IdentificacionTitular, string? Moneda, string? BancoCodigo = null, string? BancoSwift = null, ContactoDePagos? ContactoPagos = null);

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

    /// <summary>Código de compensación local del banco en el catálogo de plataforma (CENIT en Colombia); nulo si no se tomó del catálogo.</summary>
    public string? BancoCodigo { get; set; }

    /// <summary>SWIFT/BIC del banco, si se conoce.</summary>
    public string? BancoSwift { get; set; }

    /// <summary>A quién escribir por los pagos de esta cuenta, si el tercero lo indicó.</summary>
    public ContactoDePagos? ContactoPagos { get; set; }

    public TipoCuentaBancaria TipoCuenta { get; set; }

    public string NumeroProtegido { get; set; } = string.Empty;

    public string Ultimos4 { get; set; } = string.Empty;

    public string Titular { get; set; } = string.Empty;

    public string IdentificacionTitular { get; set; } = string.Empty;

    public string Moneda { get; set; } = "COP";

    public EstadoCuentaBancaria Estado { get; set; }

    public DateTimeOffset RegistradaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }

    /// <summary>
    /// El vínculo (de un expediente embebido) desde el que se registró: un token embebido de expediente solo ve y designa las cuentas que registró para su vínculo o que ya
    /// designó con él; nunca las demás cuentas del tercero. Nulo si la registró el tercero desde su cuenta o una sesión que no es de un expediente.
    /// </summary>
    public Guid? VinculoDeOrigenId { get; set; }

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
        var swift = Ficha.ReglasDeCatalogo.NormalizarSwift(datos.BancoSwift, out var swiftValido);
        if (!swiftValido)
            throw ReglaDeNegocioException.Validacion("swift-invalido", "El SWIFT/BIC del banco tiene 8 u 11 caracteres (4 letras, 2 de país, 2 de plaza y 3 opcionales de sucursal).");

        return new CuentaBancaria
        {
            Id = Guid.CreateVersion7(),
            TerceroId = terceroId,
            Pais = pais,
            Banco = banco,
            BancoCodigo = string.IsNullOrWhiteSpace(datos.BancoCodigo) ? null : datos.BancoCodigo.Trim(),
            BancoSwift = swift,
            ContactoPagos = NormalizarContactoPagos(datos.ContactoPagos),
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

    /// <summary>Contacto de pagos normalizado (nombre y correo obligatorios si se indica alguno); 422 si el correo no es válido.</summary>
    private static ContactoDePagos? NormalizarContactoPagos(ContactoDePagos? contacto)
    {
        if (contacto is null || (string.IsNullOrWhiteSpace(contacto.Nombre) && string.IsNullOrWhiteSpace(contacto.Correo) && string.IsNullOrWhiteSpace(contacto.Telefono)))
            return null;
        var nombre = Requerido(contacto.Nombre, "contacto-pagos-nombre-requerido", "Indica el nombre del contacto de pagos.");
        var correo = Requerido(contacto.Correo, "correo-invalido", "Indica el correo del contacto de pagos.").ToLowerInvariant();
        var arroba = correo.IndexOf('@', StringComparison.Ordinal);
        if (arroba < 1 || arroba == correo.Length - 1 || correo.Contains(' ', StringComparison.Ordinal) || !correo[(arroba + 1)..].Contains('.', StringComparison.Ordinal))
            throw ReglaDeNegocioException.Validacion("correo-invalido", $"El correo '{contacto.Correo}' del contacto de pagos no es válido.");
        if (nombre.Length > 200 || (contacto.Telefono?.Trim().Length ?? 0) > 40)
            throw ReglaDeNegocioException.Validacion("contacto-pagos-demasiado-largo", "El nombre del contacto de pagos admite hasta 200 caracteres y el teléfono 40.");
        return new ContactoDePagos(nombre, correo, string.IsNullOrWhiteSpace(contacto.Telefono) ? null : contacto.Telefono.Trim());
    }

    private static string Requerido(string? valor, string codigo, string mensaje) =>
        string.IsNullOrWhiteSpace(valor) ? throw ReglaDeNegocioException.Validacion(codigo, mensaje) : valor.Trim();

    [GeneratedRegex(@"[\s-]")]
    private static partial Regex NoDigitos();
}
