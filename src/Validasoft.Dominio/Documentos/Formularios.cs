using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Documentos;

/// <summary>Qué entrega el tercero para un tipo (TD-4): solo el archivo, solo los campos o ambos en la misma versión.</summary>
public enum ModoCaptura
{
    Archivo,
    Formulario,
    ArchivoYFormulario
}

public enum TipoCampo
{
    Texto,
    Numero,
    Fecha,
    Lista,
    SiNo
}

/// <summary>
/// Campo que llena el tercero. <see cref="Clave"/> en snake_case y única en el tipo; el orden es el de la lista.
/// <see cref="Opciones"/> solo en <c>Lista</c>. Las claves <c>fecha_expedicion</c> y <c>fecha_vencimiento</c> están
/// reservadas (tipo <c>Fecha</c>): alimentan la vigencia de la versión (TD-6).
/// </summary>
public sealed record CampoFormulario(
    string Clave, string Etiqueta, TipoCampo Tipo, bool Obligatorio, IReadOnlyList<string>? Opciones = null, string? Ayuda = null);

/// <summary>Valor declarado ya validado y normalizado (número invariante, fecha ISO, <c>true</c>/<c>false</c>), con la etiqueta y el tipo del momento en que se capturó.</summary>
public sealed record DatoDeclarado(string Clave, string Etiqueta, TipoCampo Tipo, string Valor);

/// <summary>Datos declarados de una versión y su huella (SHA-256 del JSON canónico), con la que se deduplica.</summary>
public sealed record DatosVerificados(IReadOnlyList<DatoDeclarado> Datos, string Hash)
{
    public string? Valor(string clave) => Datos.FirstOrDefault(d => d.Clave == clave)?.Valor;
}

/// <summary>Reglas del esquema de campos de un tipo y validación de los datos que envía el tercero (TD-4).</summary>
public static partial class EsquemaDeFormulario
{
    public const string FechaExpedicion = "fecha_expedicion";
    public const string FechaVencimiento = "fecha_vencimiento";
    public const int MaximoCampos = 50;
    public const int MaximoTexto = 2000;

    /// <summary>Claves reservadas: siempre de tipo <c>Fecha</c>; alimentan la vigencia.</summary>
    public static readonly IReadOnlyList<string> Reservadas = [FechaExpedicion, FechaVencimiento];

    [GeneratedRegex("^[a-z][a-z0-9_]{0,49}$")]
    private static partial Regex RegexClave();

    /// <summary>
    /// Normaliza y valida el esquema: claves snake_case únicas (422 <c>campo-invalido</c>, <c>campo-duplicado</c>),
    /// etiqueta obligatoria, opciones solo en <c>Lista</c> (al menos una, sin repetir) y claves reservadas de tipo <c>Fecha</c>
    /// (<c>campo-reservado</c>). Máximo <see cref="MaximoCampos"/>.
    /// </summary>
    public static List<CampoFormulario> Validar(IEnumerable<CampoFormulario>? campos)
    {
        var lista = (campos ?? []).ToList();
        if (lista.Count > MaximoCampos)
            throw ReglaDeNegocioException.Validacion("campo-invalido", $"Un tipo admite hasta {MaximoCampos} campos.");

        var claves = new HashSet<string>(StringComparer.Ordinal);
        var resultado = new List<CampoFormulario>();
        foreach (var campo in lista)
        {
            var clave = (campo.Clave ?? string.Empty).Trim();
            if (!RegexClave().IsMatch(clave))
                throw ReglaDeNegocioException.Validacion("campo-invalido",
                    $"La clave '{campo.Clave}' debe ir en snake_case: minúsculas, números y guion bajo, empezando por letra (máx. 50).");
            if (!claves.Add(clave))
                throw ReglaDeNegocioException.Validacion("campo-duplicado", $"La clave '{clave}' está repetida.");
            if (string.IsNullOrWhiteSpace(campo.Etiqueta))
                throw ReglaDeNegocioException.Validacion("campo-invalido", $"El campo '{clave}' necesita una etiqueta.");
            if (!Enum.IsDefined(campo.Tipo))
                throw ReglaDeNegocioException.Validacion("campo-invalido", $"El campo '{clave}' tiene un tipo desconocido.");
            if (Reservadas.Contains(clave) && campo.Tipo != TipoCampo.Fecha)
                throw ReglaDeNegocioException.Validacion("campo-reservado", $"La clave '{clave}' está reservada para una fecha (tipo Fecha).");

            List<string>? opciones = null;
            if (campo.Tipo == TipoCampo.Lista)
            {
                opciones = [.. (campo.Opciones ?? []).Select(o => (o ?? string.Empty).Trim())];
                if (opciones.Count == 0 || opciones.Any(o => o.Length == 0))
                    throw ReglaDeNegocioException.Validacion("campo-invalido", $"El campo '{clave}' es una lista: indica sus opciones (no vacías).");
                if (opciones.Distinct(StringComparer.Ordinal).Count() != opciones.Count)
                    throw ReglaDeNegocioException.Validacion("campo-invalido", $"El campo '{clave}' tiene opciones repetidas.");
            }
            else if (campo.Opciones is { Count: > 0 })
            {
                throw ReglaDeNegocioException.Validacion("campo-invalido", $"Solo un campo de tipo Lista lleva opciones ('{clave}' es {campo.Tipo}).");
            }

            resultado.Add(new CampoFormulario(clave, campo.Etiqueta.Trim(), campo.Tipo, campo.Obligatorio, opciones,
                string.IsNullOrWhiteSpace(campo.Ayuda) ? null : campo.Ayuda.Trim()));
        }

        return resultado;
    }

    /// <summary>
    /// Valida los valores contra el esquema y los normaliza: clave desconocida ⇒ 422 <c>campo-desconocido</c>; obligatorio
    /// vacío ⇒ <c>campo-requerido</c>; número, fecha ISO (<c>aaaa-mm-dd</c>), opción o sí/no inválidos ⇒ <c>valor-invalido</c>;
    /// <c>fecha_expedicion</c> posterior a <paramref name="hoy"/> o <c>fecha_vencimiento</c> anterior a ella ⇒ <c>valor-invalido</c>.
    /// El error lleva el campo en <c>campo</c>. Los opcionales vacíos no se guardan.
    /// </summary>
    public static DatosVerificados ValidarDatos(IReadOnlyList<CampoFormulario> campos, IReadOnlyDictionary<string, string?>? valores, DateOnly hoy)
    {
        valores ??= new Dictionary<string, string?>();
        foreach (var clave in valores.Keys)
        {
            if (campos.All(c => c.Clave != clave))
                throw Error("campo-desconocido", clave, $"El formulario no tiene el campo '{clave}'.");
        }

        var datos = new List<DatoDeclarado>();
        foreach (var campo in campos)
        {
            var texto = valores.TryGetValue(campo.Clave, out var crudo) ? crudo?.Trim() : null;
            if (string.IsNullOrEmpty(texto))
            {
                if (campo.Obligatorio)
                    throw Error("campo-requerido", campo.Clave, $"Falta «{campo.Etiqueta}».");
                continue;
            }

            datos.Add(new DatoDeclarado(campo.Clave, campo.Etiqueta, campo.Tipo, Normalizar(campo, texto)));
        }

        var expedicion = Fecha(datos, FechaExpedicion);
        if (expedicion > hoy)
            throw Error("valor-invalido", FechaExpedicion, "La fecha de expedición no puede ser posterior a hoy.");
        if (Fecha(datos, FechaVencimiento) is { } vencimiento && expedicion is { } desde && vencimiento < desde)
            throw Error("valor-invalido", FechaVencimiento, "La fecha de vencimiento no puede ser anterior a la de expedición.");

        return new DatosVerificados(datos, Hash(datos));
    }

    /// <summary>Fecha declarada (clave reservada) si existe.</summary>
    public static DateOnly? Fecha(IReadOnlyList<DatoDeclarado>? datos, string clave) =>
        datos?.FirstOrDefault(d => d.Clave == clave && d.Tipo == TipoCampo.Fecha) is { } dato &&
        DateOnly.TryParseExact(dato.Valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;

    /// <summary>SHA-256 (hex en minúsculas) del JSON canónico: pares <c>[clave, valor]</c> ordenados por clave.</summary>
    public static string Hash(IReadOnlyList<DatoDeclarado> datos)
    {
        var canonico = JsonSerializer.Serialize(datos.OrderBy(d => d.Clave, StringComparer.Ordinal).Select(d => new[] { d.Clave, d.Valor }));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    private static string Normalizar(CampoFormulario campo, string texto)
    {
        switch (campo.Tipo)
        {
            case TipoCampo.Texto:
                return texto.Length <= MaximoTexto
                    ? texto
                    : throw Error("valor-invalido", campo.Clave, $"«{campo.Etiqueta}» admite hasta {MaximoTexto} caracteres.");
            case TipoCampo.Numero:
                return decimal.TryParse(texto, NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var numero)
                    ? numero.ToString("0.############################", CultureInfo.InvariantCulture)
                    : throw Error("valor-invalido", campo.Clave, $"«{campo.Etiqueta}» debe ser un número (con punto decimal).");
            case TipoCampo.Fecha:
                return DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
                    ? fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : throw Error("valor-invalido", campo.Clave, $"«{campo.Etiqueta}» debe ser una fecha aaaa-mm-dd.");
            case TipoCampo.Lista:
                return (campo.Opciones ?? []).Contains(texto, StringComparer.Ordinal)
                    ? texto
                    : throw Error("valor-invalido", campo.Clave,
                        $"«{campo.Etiqueta}» debe ser una de: {string.Join(", ", campo.Opciones ?? [])}.");
            case TipoCampo.SiNo:
                return texto.ToLowerInvariant() switch
                {
                    "true" or "si" or "sí" => "true",
                    "false" or "no" => "false",
                    _ => throw Error("valor-invalido", campo.Clave, $"«{campo.Etiqueta}» es sí o no (true/false).")
                };
            default:
                throw Error("valor-invalido", campo.Clave, $"«{campo.Etiqueta}» tiene un tipo desconocido.");
        }
    }

    private static ReglaDeNegocioException Error(string codigo, string clave, string mensaje) =>
        new(TipoFalla.Validacion, codigo, mensaje) { Datos = new Dictionary<string, object?> { ["campo"] = clave } };
}
