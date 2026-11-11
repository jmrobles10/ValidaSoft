using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Validasoft.Dominio.Ficha;

/// <summary>
/// Catálogos de plataforma de la ficha maestra (FM-10): globales, sin tenant (esquema <c>plataforma</c>). Los carga
/// Plataforma por importación; en desarrollo hay una semilla con lo más común. Los completos se importan desde la BD del
/// Portal en F4.
/// </summary>
public enum CatalogoDePlataforma
{
    /// <summary>Municipios DANE (DIVIPOLA): código de 5 dígitos = departamento (2) + municipio (3).</summary>
    Ciudades,

    /// <summary>Actividades económicas CIIU Rev. 4 A.C.: código de 4 dígitos.</summary>
    ActividadesEconomicas,

    /// <summary>
    /// Bancos: código de compensación local (en Colombia, el del Banco de la República, CENIT, 4 dígitos) y, si se conoce, su SWIFT/BIC
    /// (<see cref="EntradaDeCatalogo.Swift"/>). Un banco de otro país sin código local se declara en la cuenta solo con su SWIFT.
    /// </summary>
    Bancos,

    /// <summary>Responsabilidades de la casilla 53 del RUT (DIAN): código de 2 dígitos.</summary>
    ResponsabilidadesDian,

    /// <summary>Especialidades en tres niveles: grupo → categoría → especialidad.</summary>
    Especialidades
}

/// <summary>
/// Entrada de un catálogo de plataforma. Id determinista <c>{catalogo}|{codigo}</c>: la importación es un upsert por
/// código. <see cref="Padre"/>: en ciudades, el código del departamento (dos primeros dígitos); en especialidades, el
/// código del nivel anterior. Una entrada inactiva no se ofrece ni valida datos nuevos.
/// </summary>
public sealed class EntradaDeCatalogo
{
    public string Id { get; set; } = string.Empty;

    public CatalogoDePlataforma Catalogo { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Nombre en minúsculas y sin tildes, para buscar.</summary>
    public string NombreNormalizado { get; set; } = string.Empty;

    /// <summary>Solo ciudades: nombre del departamento.</summary>
    public string? Departamento { get; set; }

    /// <summary>Ciudades: país ISO 3166-1 alfa-2. Bancos: país del código de compensación local (CO por omisión).</summary>
    public string? Pais { get; set; }

    /// <summary>Solo bancos: código SWIFT/BIC (8 u 11 caracteres), si se conoce.</summary>
    public string? Swift { get; set; }

    public string? Padre { get; set; }

    /// <summary>Solo especialidades: 1 grupo, 2 categoría, 3 especialidad.</summary>
    public int? Nivel { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset ActualizadoEn { get; set; }

    public static string IdPara(CatalogoDePlataforma catalogo, string codigo) => $"{catalogo}|{codigo}";

    /// <summary>¿Cambia algo de lo que se importa? (la fecha no cuenta: la importación es idempotente).</summary>
    public bool MismoContenido(EntradaDeCatalogo otra) =>
        Nombre == otra.Nombre && Departamento == otra.Departamento && Pais == otra.Pais && Padre == otra.Padre && Nivel == otra.Nivel &&
        Activo == otra.Activo && Swift == otra.Swift;
}

/// <summary>Fila que llega a la importación (o a la semilla) antes de validarse.</summary>
public sealed record FilaDeCatalogo(
    string? Codigo, string? Nombre, string? Departamento = null, string? Pais = null, string? Padre = null, int? Nivel = null, bool? Activo = null,
    string? Swift = null);

/// <summary>Validación y normalización de las filas de cada catálogo, y la búsqueda sin tildes.</summary>
public static partial class ReglasDeCatalogo
{
    public const int MaximoNombre = 300;

    [GeneratedRegex("^[0-9]{5}$")]
    private static partial Regex RegexDane();

    [GeneratedRegex("^[0-9]{4}$")]
    private static partial Regex RegexCuatroDigitos();

    [GeneratedRegex("^[0-9]{1,2}$")]
    private static partial Regex RegexResponsabilidad();

    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{0,29}$")]
    private static partial Regex RegexEspecialidad();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex RegexPais();

    [GeneratedRegex("^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$")]
    private static partial Regex RegexSwift();

    /// <summary>
    /// SWIFT/BIC (ISO 9362) en mayúsculas y sin espacios: 4 letras de la entidad, 2 del país, 2 de la plaza y, opcional, 3 de la
    /// sucursal. Vacío ⇒ <c>null</c>; con formato inválido ⇒ <c>false</c> en <paramref name="valido"/>.
    /// </summary>
    public static string? NormalizarSwift(string? swift, out bool valido)
    {
        var limpio = (swift ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Trim().ToUpperInvariant();
        valido = limpio.Length == 0 || RegexSwift().IsMatch(limpio);
        return limpio.Length == 0 ? null : limpio;
    }

    /// <summary>Segmento de la URL (<c>/catalogos/{catalogo}</c>) → catálogo.</summary>
    private static readonly IReadOnlyDictionary<string, CatalogoDePlataforma> PorRuta = new Dictionary<string, CatalogoDePlataforma>(StringComparer.OrdinalIgnoreCase)
    {
        ["ciudades"] = CatalogoDePlataforma.Ciudades,
        ["actividades-economicas"] = CatalogoDePlataforma.ActividadesEconomicas,
        ["bancos"] = CatalogoDePlataforma.Bancos,
        ["responsabilidades-dian"] = CatalogoDePlataforma.ResponsabilidadesDian,
        ["especialidades"] = CatalogoDePlataforma.Especialidades
    };

    public static IReadOnlyCollection<string> Rutas => [.. PorRuta.Keys];

    public static string Ruta(CatalogoDePlataforma catalogo) => PorRuta.First(p => p.Value == catalogo).Key;

    /// <summary>El catálogo de la ruta; si no existe ⇒ 404 <c>catalogo-no-encontrado</c>.</summary>
    public static CatalogoDePlataforma DeRuta(string? ruta) =>
        ruta is not null && PorRuta.TryGetValue(ruta.Trim(), out var catalogo)
            ? catalogo
            : throw Comun.ReglaDeNegocioException.NoEncontrado("catalogo-no-encontrado",
                $"No existe el catálogo '{ruta}'. Disponibles: {string.Join(", ", PorRuta.Keys)}.");

    /// <summary>
    /// Normaliza un código según el catálogo (las responsabilidades DIAN van con dos dígitos: <c>5</c> ⇒ <c>05</c>; las
    /// especialidades en mayúsculas). Nulo si no tiene el formato.
    /// </summary>
    public static string? NormalizarCodigo(CatalogoDePlataforma catalogo, string? codigo)
    {
        var limpio = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        return catalogo switch
        {
            CatalogoDePlataforma.Ciudades => RegexDane().IsMatch(limpio) ? limpio : null,
            CatalogoDePlataforma.ActividadesEconomicas or CatalogoDePlataforma.Bancos => RegexCuatroDigitos().IsMatch(limpio) ? limpio : null,
            CatalogoDePlataforma.ResponsabilidadesDian => RegexResponsabilidad().IsMatch(limpio) ? limpio.PadLeft(2, '0') : null,
            CatalogoDePlataforma.Especialidades => RegexEspecialidad().IsMatch(limpio) ? limpio : null,
            _ => null
        };
    }

    public static string FormatoDeCodigo(CatalogoDePlataforma catalogo) => catalogo switch
    {
        CatalogoDePlataforma.Ciudades => "código DANE de 5 dígitos (departamento + municipio)",
        CatalogoDePlataforma.ActividadesEconomicas => "código CIIU de 4 dígitos",
        CatalogoDePlataforma.Bancos => "código de compensación de 4 dígitos",
        CatalogoDePlataforma.ResponsabilidadesDian => "código de 2 dígitos de la casilla 53 del RUT",
        _ => "código en mayúsculas, números, punto, guion o guion bajo (máx. 30)"
    };

    /// <summary>
    /// Valida una fila y devuelve la entrada normalizada, o el motivo del rechazo. <paramref name="padres"/> resuelve el
    /// nivel de un padre de especialidad (en la base o en el mismo lote); nulo si no existe.
    /// </summary>
    public static (EntradaDeCatalogo? Entrada, string? Rechazo) Validar(CatalogoDePlataforma catalogo, FilaDeCatalogo fila,
        Func<string, int?> padres, DateTimeOffset ahora)
    {
        var codigo = NormalizarCodigo(catalogo, fila.Codigo);
        if (codigo is null)
            return (null, $"El código '{fila.Codigo}' no es un {FormatoDeCodigo(catalogo)}.");
        var nombre = (fila.Nombre ?? string.Empty).Trim();
        if (nombre.Length == 0 || nombre.Length > MaximoNombre)
            return (null, $"El nombre es obligatorio (máx. {MaximoNombre} caracteres).");

        var entrada = new EntradaDeCatalogo
        {
            Id = EntradaDeCatalogo.IdPara(catalogo, codigo),
            Catalogo = catalogo,
            Codigo = codigo,
            Nombre = nombre,
            NombreNormalizado = NormalizarTexto(nombre),
            Activo = fila.Activo ?? true,
            ActualizadoEn = ahora
        };

        switch (catalogo)
        {
            case CatalogoDePlataforma.Bancos:
                var paisBanco = string.IsNullOrWhiteSpace(fila.Pais) ? "CO" : fila.Pais.Trim().ToUpperInvariant();
                if (!RegexPais().IsMatch(paisBanco))
                    return (null, $"El país '{fila.Pais}' no es un código ISO 3166-1 alfa-2.");
                var swift = NormalizarSwift(fila.Swift, out var swiftValido);
                if (!swiftValido)
                    return (null, $"El SWIFT '{fila.Swift}' no es un código SWIFT/BIC válido (8 u 11 caracteres).");
                entrada.Pais = paisBanco;
                entrada.Swift = swift;
                break;
            case CatalogoDePlataforma.Ciudades:
                var departamento = (fila.Departamento ?? string.Empty).Trim();
                if (departamento.Length == 0 || departamento.Length > MaximoNombre)
                    return (null, "La ciudad necesita el nombre de su departamento.");
                var pais = string.IsNullOrWhiteSpace(fila.Pais) ? "CO" : fila.Pais.Trim().ToUpperInvariant();
                if (!RegexPais().IsMatch(pais))
                    return (null, $"El país '{fila.Pais}' no es un código ISO 3166-1 alfa-2.");
                entrada.Departamento = departamento;
                entrada.Pais = pais;
                entrada.Padre = codigo[..2];
                break;
            case CatalogoDePlataforma.Especialidades:
                if (fila.Nivel is not (1 or 2 or 3))
                    return (null, "La especialidad necesita su nivel: 1 grupo, 2 categoría o 3 especialidad.");
                var padre = string.IsNullOrWhiteSpace(fila.Padre) ? null : fila.Padre.Trim().ToUpperInvariant();
                if (fila.Nivel == 1 && padre is not null)
                    return (null, "Un grupo (nivel 1) no tiene padre.");
                if (fila.Nivel > 1)
                {
                    if (padre is null)
                        return (null, $"El nivel {fila.Nivel} necesita el código de su padre (nivel {fila.Nivel - 1}).");
                    if (padres(padre) != fila.Nivel - 1)
                        return (null, $"El padre '{padre}' no existe como nivel {fila.Nivel - 1}.");
                }

                entrada.Padre = padre;
                entrada.Nivel = fila.Nivel;
                break;
        }

        return (entrada, null);
    }

    /// <summary>Minúsculas y sin tildes ni diéresis (para buscar «bogota» y encontrar «Bogotá»).</summary>
    public static string NormalizarTexto(string? texto)
    {
        var descompuesto = (texto ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
                resultado.Append(caracter);
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>Consulta de catálogos que necesitan las reglas de las secciones (la aplicación la llena con lo referido).</summary>
public interface ICatalogosDeFicha
{
    /// <summary>La entrada activa del catálogo con ese código (ya normalizado), o nula.</summary>
    EntradaDeCatalogo? Buscar(CatalogoDePlataforma catalogo, string codigo);
}

/// <summary>Catálogos precargados en memoria.</summary>
public sealed class CatalogosEnMemoria(IEnumerable<EntradaDeCatalogo> entradas) : ICatalogosDeFicha
{
    private readonly Dictionary<string, EntradaDeCatalogo> _porId = entradas.Where(e => e.Activo).ToDictionary(e => e.Id, StringComparer.Ordinal);

    public static readonly CatalogosEnMemoria Vacios = new([]);

    public EntradaDeCatalogo? Buscar(CatalogoDePlataforma catalogo, string codigo) =>
        _porId.GetValueOrDefault(EntradaDeCatalogo.IdPara(catalogo, codigo));
}

/// <summary>Referencia de un dato de sección a una entrada de catálogo.</summary>
public sealed record ReferenciaACatalogo(CatalogoDePlataforma Catalogo, string Codigo);
