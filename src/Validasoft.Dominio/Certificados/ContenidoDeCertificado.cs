using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Certificados;

public sealed record IdentificacionDeEmpresa(string Pais, string Tipo, string Numero, string? DigitoVerificacion);

public sealed record EmpresaCertificadora(Guid Id, string Nombre, IdentificacionDeEmpresa Identificacion);

/// <summary>Titular del certificado: nombre e identificación <b>enmascarada</b> (P-8-1); nunca contactos ni direcciones.</summary>
public sealed record TitularCertificado(string Nombre, string IdentificacionEnmascarada, string Pais, string TipoPersona);

public sealed record TipoVinculoCertificado(string Codigo, string Nombre);

public sealed record FlujoCertificado(string Codigo, string Nombre, int Version);

/// <summary>
/// Documento que sostuvo la aprobación: versión y huella SHA-256 (de los bytes del archivo o, si la versión es solo un
/// formulario, de sus datos, TD-4), vigencia para el vínculo y, si se eximió, la justificación (P-5-4).
/// </summary>
public sealed record RequisitoCertificado(
    string Codigo, string Nombre, int? VersionN, string? Sha256, DateOnly? VigenteHasta, bool Eximido, string? Justificacion);

/// <summary>Reservado para las verificaciones externas (listas, identidad): aún no existen, la lista va vacía.</summary>
public sealed record VerificacionCertificado(string Tipo, string Proveedor, string Resultado, DateTimeOffset Fecha);

/// <summary>Área que aprobó y de qué forma (<c>Humano | Sistema | Administrador</c>), sin el nombre de quién (#61).</summary>
public sealed record AreaCertificado(string Area, string Codigo, string Origen, DateTimeOffset Fecha);

/// <summary>
/// Contenido canónico del certificado (docs/03 §7.1, esquema <c>validasoft.certificado.v1</c>): lo que se hashea y se sella.
/// No lleva contactos, direcciones, revisores, URLs de descarga ni datos de otras empresas. Las fechas se escriben en UTC
/// con <c>Z</c> (al segundo) y las vigencias como <c>YYYY-MM-DD</c>; los nulos se omiten.
/// </summary>
public sealed record ContenidoDeCertificado(
    string Esquema,
    string Folio,
    int Emision,
    EmpresaCertificadora Empresa,
    TitularCertificado Tercero,
    TipoVinculoCertificado TipoVinculo,
    FlujoCertificado Flujo,
    IReadOnlyList<RequisitoCertificado> Requisitos,
    IReadOnlyList<VerificacionCertificado> Verificaciones,
    IReadOnlyList<AreaCertificado> Areas,
    DateTimeOffset EmitidoEn,
    DateOnly? VigenteHasta)
{
    public const string EsquemaV1 = "validasoft.certificado.v1";

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new FechaUtcAlSegundo() }
    };

    /// <summary>JSON canónico (RFC 8785) con los requisitos ordenados por código.</summary>
    public string Canonico()
    {
        var ordenado = this with { Requisitos = [.. Requisitos.OrderBy(r => r.Codigo, StringComparer.Ordinal)] };
        return Jcs.Canonicalizar(JsonSerializer.Serialize(ordenado, Opciones));
    }

    /// <summary>Lee un contenido canónico guardado (para el PDF y las vistas).</summary>
    public static ContenidoDeCertificado Leer(string canonico) =>
        JsonSerializer.Deserialize<ContenidoDeCertificado>(canonico, Opciones)
        ?? throw new FormatException("El contenido canónico del certificado está vacío.");

    /// <summary>Fechas con hora siempre en UTC con <c>Z</c> y sin fracciones: un mismo instante produce siempre los mismos bytes.</summary>
    private sealed class FechaUtcAlSegundo : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }
}

/// <summary>Hash del contenido y su presentación (docs/03 §7.1).</summary>
public static class HuellaDeCertificado
{
    /// <summary><c>HashContenido</c>: SHA-256 en hexadecimal minúsculo de los bytes UTF-8 del JSON canónico.</summary>
    public static string Hash(string canonico) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));

    /// <summary>Huella para mostrar: los primeros 12 hexadecimales en grupos de 4, <c>xxxx·xxxx·xxxx</c>.</summary>
    public static string Formatear(string hash) =>
        hash.Length < 12 ? throw new ArgumentException("El hash es demasiado corto.", nameof(hash)) : $"{hash[..4]}·{hash[4..8]}·{hash[8..12]}";

    /// <summary>Normaliza una huella escrita a mano (con o sin puntos, guiones o espacios) a sus 12 hexadecimales; nulo si no lo es.</summary>
    public static string? Normalizar(string? huella)
    {
        if (string.IsNullOrWhiteSpace(huella))
            return null;
        var limpia = new string([.. huella.Where(char.IsAsciiHexDigit)]).ToLowerInvariant();
        return limpia.Length >= 12 ? limpia[..12] : null;
    }
}

/// <summary>Enmascarado de la identificación del titular para la vista pública y el certificado (P-8-1).</summary>
public static class Enmascarado
{
    /// <summary>
    /// <c>NIT 90*****56</c>: tipo y número sin dígito de verificación, con visibles los dos primeros y los dos últimos
    /// dígitos (solo los dos últimos con 5 a 7; solo el último con 4 o menos).
    /// </summary>
    public static string Identificacion(Identificacion identificacion)
    {
        var numero = identificacion.Numero;
        var (inicio, fin) = numero.Length switch
        {
            >= 8 => (2, 2),
            >= 5 => (0, 2),
            _ => (0, 1)
        };
        var oculto = new string('*', Math.Max(0, numero.Length - inicio - fin));
        return $"{identificacion.Tipo} {numero[..inicio]}{oculto}{numero[^fin..]}";
    }
}

/// <summary>Folios del certificado (T-9): raíz <c>{Prefijo}-C-{Año}-{Secuencia}</c> por vínculo y sufijo <c>-{n}</c> por emisión.</summary>
public static partial class FoliosDeCertificado
{
    public static string Raiz(string prefijoEmpresa, int anio, long secuencia)
    {
        if (string.IsNullOrWhiteSpace(prefijoEmpresa))
            throw ReglaDeNegocioException.Validacion("prefijo-folio-invalido", "La empresa no tiene prefijo de folio.");
        if (secuencia < 1)
            throw new ArgumentOutOfRangeException(nameof(secuencia), "La secuencia empieza en 1.");
        return $"{prefijoEmpresa}-C-{anio}-{secuencia:D6}";
    }

    public static string DeEmision(string raiz, int emision) => $"{raiz}-{emision.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>Separa un folio escrito (con o sin emisión) en su raíz y su número de emisión; nulo si no tiene la forma de un folio.</summary>
    public static (string Raiz, int? Emision)? Leer(string? folio)
    {
        var coincidencia = Patron().Match((folio ?? string.Empty).Trim().ToUpperInvariant());
        if (!coincidencia.Success)
            return null;
        int? emision = coincidencia.Groups["n"].Success && int.TryParse(coincidencia.Groups["n"].Value, CultureInfo.InvariantCulture, out var n) ? n : null;
        return (coincidencia.Groups["raiz"].Value, emision);
    }

    [GeneratedRegex(@"^(?<raiz>[A-Z0-9]{2,10}-C-\d{4}-\d{6})(?:-(?<n>[1-9]\d{0,4}))?$")]
    private static partial Regex Patron();
}
