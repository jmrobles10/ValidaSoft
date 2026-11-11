using System.Globalization;
using System.Text.RegularExpressions;

using Validasoft.Dominio.Ficha;

namespace Validasoft.Adaptadores.Portal;

/// <summary>
/// Conversiones puras de los datos del Portal a los tipos y códigos de plataforma (FM-10, FM-40): DANE, CIIU, bancos,
/// especialidades, países ISO, tipos de identificación, fechas y porcentajes guardados como texto. Lo que no se puede
/// convertir devuelve nulo: el traductor lo anota, nunca lo inventa.
/// </summary>
public static partial class CodigosDelPortal
{
    /// <summary>Departamentos de la DIVIPOLA (DANE): el Portal guarda solo el código en <c>CiuCodDepartamento</c>.</summary>
    public static readonly IReadOnlyDictionary<string, string> DepartamentosDane = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["05"] = "Antioquia", ["08"] = "Atlántico", ["11"] = "Bogotá, D.C.", ["13"] = "Bolívar", ["15"] = "Boyacá", ["17"] = "Caldas",
        ["18"] = "Caquetá", ["19"] = "Cauca", ["20"] = "Cesar", ["23"] = "Córdoba", ["25"] = "Cundinamarca", ["27"] = "Chocó", ["41"] = "Huila",
        ["44"] = "La Guajira", ["47"] = "Magdalena", ["50"] = "Meta", ["52"] = "Nariño", ["54"] = "Norte de Santander", ["63"] = "Quindío",
        ["66"] = "Risaralda", ["68"] = "Santander", ["70"] = "Sucre", ["73"] = "Tolima", ["76"] = "Valle del Cauca", ["81"] = "Arauca",
        ["85"] = "Casanare", ["86"] = "Putumayo", ["88"] = "Archipiélago de San Andrés, Providencia y Santa Catalina", ["91"] = "Amazonas",
        ["94"] = "Guainía", ["95"] = "Guaviare", ["97"] = "Vaupés", ["99"] = "Vichada"
    };

    /// <summary>
    /// Tipo de documento del Portal ⇒ tipo de identificación de Validasoft (CO). <c>PT</c> es el PPT; <c>EXT</c> (documento
    /// extranjero) no dice cuál es: va como <c>ID_FISCAL</c> (con advertencia).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> TiposDeDocumento = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["NIT"] = "NIT", ["CC"] = "CC", ["CE"] = "CE", ["TI"] = "TI", ["PA"] = "PA", ["PT"] = "PPT", ["PPT"] = "PPT", ["EXT"] = "ID_FISCAL",
        ["PASAPORTE"] = "PASAPORTE"
    };

    /// <summary>Vía del Portal (<c>dbo.Avenidas</c>) ⇒ tipo de vía de AyF; las demás (manzana, vereda, finca…) no tienen equivalente.</summary>
    public static readonly IReadOnlyDictionary<string, string> TiposDeVia = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["CL"] = "Calle", ["CR"] = "Carrera", ["KR"] = "Carrera", ["AV"] = "Avenida", ["AC"] = "AvenidaCalle", ["AK"] = "AvenidaCarrera",
        ["DG"] = "Diagonal", ["TV"] = "Transversal", ["CIR"] = "Circular", ["AUT"] = "Autopista", ["KIL"] = "Kilometro"
    };

    /// <summary>Intersección del Portal (<c>dbo.Intersecciones</c>) ⇒ cuadrante de AyF.</summary>
    public static readonly IReadOnlyDictionary<string, string> Cuadrantes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["SUR"] = "Sur", ["ESTE"] = "Este", ["NORTE"] = "Norte", ["OESTE"] = "Oeste", ["O"] = "Este", ["OCC"] = "Oeste"
    };

    /// <summary>
    /// <c>TipoTransaccionMonedaExtranjeraEnum</c> del Portal ⇒ operación de la ficha. Leasing (3) y redescuento (8) no tienen
    /// equivalente: van como <c>Otras</c> (con advertencia).
    /// </summary>
    public static readonly IReadOnlyDictionary<int, TipoOperacionExtranjera> OperacionesExtranjeras = new Dictionary<int, TipoOperacionExtranjera>
    {
        [1] = TipoOperacionExtranjera.Importaciones, [2] = TipoOperacionExtranjera.Inversiones, [3] = TipoOperacionExtranjera.Otras,
        [4] = TipoOperacionExtranjera.PagoDeServicios, [5] = TipoOperacionExtranjera.Prestamos, [6] = TipoOperacionExtranjera.Exportaciones,
        [7] = TipoOperacionExtranjera.Giros, [8] = TipoOperacionExtranjera.Otras, [9] = TipoOperacionExtranjera.Otras
    };

    /// <summary>Nombres y gentilicios frecuentes ⇒ ISO 3166-1 alfa-2 (respaldo cuando <c>dbo.Paises</c> no lo resuelve).</summary>
    private static readonly IReadOnlyDictionary<string, string> PaisesConocidos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["colombia"] = "CO", ["colombiana"] = "CO", ["colombiano"] = "CO", ["venezuela"] = "VE", ["venezolana"] = "VE", ["venezolano"] = "VE",
        ["ecuador"] = "EC", ["ecuatoriana"] = "EC", ["ecuatoriano"] = "EC", ["peru"] = "PE", ["peruana"] = "PE", ["peruano"] = "PE",
        ["panama"] = "PA", ["panamena"] = "PA", ["panameno"] = "PA", ["mexico"] = "MX", ["mexicana"] = "MX", ["mexicano"] = "MX",
        ["estados unidos"] = "US", ["estadounidense"] = "US", ["espana"] = "ES", ["espanola"] = "ES", ["espanol"] = "ES", ["chile"] = "CL",
        ["chilena"] = "CL", ["chileno"] = "CL", ["argentina"] = "AR", ["argentino"] = "AR", ["brasil"] = "BR", ["brasilena"] = "BR",
        ["brasileno"] = "BR", ["china"] = "CN", ["china popular"] = "CN", ["chino"] = "CN"
    };

    private static readonly string[] FormatosDeFecha =
    [
        "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd", "yyyy/M/d", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "yyyyMMdd"
    ];

    [GeneratedRegex("^[0-9]+$")]
    private static partial Regex RegexDigitos();

    [GeneratedRegex(@"^\s*(\d{4}-\d{2}-\d{2})[T ]")]
    private static partial Regex RegexFechaHora();

    [GeneratedRegex(@"^(?<numero>[0-9]{1,4})\s*(?<letra>(?!BIS)[A-Z]{1,2})?\s*(?<bis>BIS)?\s*(?<letra2>[A-Z])?$")]
    private static partial Regex RegexNumeroDeVia();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex RegexIso2();

    // ---------- catálogos ----------

    /// <summary>Código DANE de 5 dígitos de una ciudad del Portal (o nulo).</summary>
    public static string? Dane(Ciudades? ciudad)
    {
        var departamento = SoloDigitos(ciudad?.CiuCodDepartamento);
        var municipio = SoloDigitos(ciudad?.CiuCodMunicipio);
        if (departamento is null || municipio is null || departamento.Length > 2)
            return null;
        departamento = departamento.PadLeft(2, '0');
        if (municipio.Length == 5 && municipio.StartsWith(departamento, StringComparison.Ordinal))
            return municipio;
        return municipio.Length <= 3 ? departamento + municipio.PadLeft(3, '0') : null;
    }

    /// <summary>CIIU de 4 dígitos (<c>111</c> ⇒ <c>0111</c>) o nulo.</summary>
    public static string? Ciiu(ActividadEconomica? actividad) =>
        SoloDigitos(actividad?.ActEcCodigo) is { Length: >= 3 and <= 4 } codigo ? codigo.PadLeft(4, '0') : null;

    /// <summary>Código de compensación del banco (4 dígitos) desde <c>Codigo</c> o, si no es numérico, desde <c>Id</c>.</summary>
    public static string? Banco(Bancos banco) =>
        new[] { banco.Codigo, banco.Id }.Select(SoloDigitos).FirstOrDefault(c => c is { Length: >= 1 and <= 4 })?.PadLeft(4, '0');

    public static string Grupo(int gruId) => "PG" + gruId.ToString(CultureInfo.InvariantCulture);

    public static string Categoria(int catId) => "PC" + catId.ToString(CultureInfo.InvariantCulture);

    public static string Especialidad(int espId) => "PE" + espId.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Las filas de los catálogos de plataforma (F1, FM-22) que salen de los del Portal, con las que no se pudieron convertir
    /// (fila del Portal y motivo). Especialidades: grupo <c>PG{GruId}</c> → categoría <c>PC{CatId}</c> → especialidad
    /// <c>PE{EspId}</c> (ids estables del Portal; el padre puede cambiar sin cambiar el código).
    /// </summary>
    public static IReadOnlyList<(CatalogoDePlataforma Catalogo, IReadOnlyList<FilaDeCatalogo> Filas, IReadOnlyList<string> Rechazos)> Filas(
        CatalogosDelPortal catalogos)
    {
        var ciudades = new List<FilaDeCatalogo>();
        var rechazosCiudades = new List<string>();
        foreach (var c in catalogos.Ciudades ?? [])
        {
            var dane = Dane(c);
            if (dane is null || !DepartamentosDane.TryGetValue(dane[..2], out var departamento))
                rechazosCiudades.Add($"Ciudades.CiudID {c.CiudID} ({c.CiuNombre}): sin código DANE válido ({c.CiuCodDepartamento}/{c.CiuCodMunicipio}).");
            else
                ciudades.Add(new FilaDeCatalogo(dane, c.CiuNombre, departamento, "CO"));
        }

        var actividades = new List<FilaDeCatalogo>();
        var rechazosActividades = new List<string>();
        foreach (var a in catalogos.ActividadesEconomicas ?? [])
        {
            if (Ciiu(a) is { } ciiu)
                actividades.Add(new FilaDeCatalogo(ciiu, a.ActECtexto));
            else
                rechazosActividades.Add($"ActividadEconomica.ActEcId {a.ActEcId}: '{a.ActEcCodigo}' no es un CIIU de 4 dígitos.");
        }

        var bancos = new List<FilaDeCatalogo>();
        var rechazosBancos = new List<string>();
        foreach (var b in catalogos.Bancos ?? [])
        {
            if (Banco(b) is { } codigo)
                bancos.Add(new FilaDeCatalogo(codigo, b.Texto, Activo: b.Estado));
            else
                rechazosBancos.Add($"Bancos.Id {b.Id} ({b.Texto}): '{b.Codigo}' no es un código de compensación de 4 dígitos.");
        }

        var especialidades = new List<FilaDeCatalogo>();
        especialidades.AddRange((catalogos.Grupos ?? []).Select(g => new FilaDeCatalogo(Grupo(g.GruId), g.GruTexto, Nivel: 1)));
        especialidades.AddRange((catalogos.Categorias ?? []).Select(c => new FilaDeCatalogo(Categoria(c.CatId), c.CatTexto, Padre: Grupo(c.CatIdGrupo), Nivel: 2)));
        especialidades.AddRange((catalogos.Especialidades ?? [])
            .Select(e => new FilaDeCatalogo(Especialidad(e.EspId), e.EspTexto, Padre: Categoria(e.EspIdCategoria), Nivel: 3)));

        return
        [
            (CatalogoDePlataforma.Ciudades, ciudades, rechazosCiudades),
            (CatalogoDePlataforma.ActividadesEconomicas, actividades, rechazosActividades),
            (CatalogoDePlataforma.Bancos, bancos, rechazosBancos),
            (CatalogoDePlataforma.Especialidades, especialidades, [])
        ];
    }

    // ---------- países, documentos y textos ----------

    /// <summary>
    /// País ISO 3166-1 alfa-2 de un texto del Portal (nombre de <c>TigPais</c>, sigla o gentilicio de la cámara): primero
    /// <c>dbo.Paises</c> por sigla o nombre (sin tildes ni mayúsculas), luego la tabla de respaldo. Nulo si no se reconoce.
    /// </summary>
    public static string? Pais(string? texto, IReadOnlyList<Paises>? paises)
    {
        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length == 0)
            return null;
        var normalizado = ReglasDeCatalogo.NormalizarTexto(limpio);
        var mayusculas = limpio.ToUpperInvariant();

        foreach (var p in paises ?? [])
        {
            var sigla = (p.Sigla ?? string.Empty).Trim().ToUpperInvariant();
            var coincide = sigla == mayusculas || ReglasDeCatalogo.NormalizarTexto(p.Nombre) == normalizado;
            if (coincide && RegexIso2().IsMatch(sigla))
                return sigla;
            if (coincide && PaisesConocidos.TryGetValue(ReglasDeCatalogo.NormalizarTexto(p.Nombre), out var porNombre))
                return porNombre;
        }

        if (RegexIso2().IsMatch(mayusculas) && Validasoft.Dominio.Identificaciones.CatalogoIdentificacion.Paises.Any(p => p.Codigo == mayusculas))
            return mayusculas;
        return PaisesConocidos.GetValueOrDefault(normalizado);
    }

    /// <summary>Tipo de identificación de Validasoft de un tipo de documento del Portal (o nulo).</summary>
    public static string? TipoDeIdentificacion(string? tipoPortal) =>
        TiposDeDocumento.GetValueOrDefault((tipoPortal ?? string.Empty).Trim());

    /// <summary>
    /// Fecha guardada como texto: <c>aaaa-mm-dd</c> (con o sin hora), <c>dd/mm/aaaa</c> y variantes con guion o punto. Las
    /// fechas con el día y el mes ambiguos se leen como día/mes (formato colombiano). Nulo si no es una fecha.
    /// </summary>
    public static DateOnly? Fecha(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        if (limpio.Length == 0)
            return null;
        if (RegexFechaHora().Match(limpio) is { Success: true } conHora)
            limpio = conHora.Groups[1].Value;
        return DateOnly.TryParseExact(limpio, FormatosDeFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) ? fecha : null;
    }

    /// <summary>
    /// Porcentaje guardado como texto: <c>50</c>, <c>50%</c>, <c>33,33</c>, <c>33.33</c>. Fuera de 0–100 o ilegible ⇒ nulo. Más de
    /// cuatro decimales se redondea (<paramref name="redondeado"/>).
    /// </summary>
    public static decimal? Porcentaje(string? texto, out bool redondeado)
    {
        redondeado = false;
        var limpio = (texto ?? string.Empty).Replace("%", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        if (limpio.Length == 0)
            return null;
        if (limpio.Contains(',', StringComparison.Ordinal) && limpio.Contains('.', StringComparison.Ordinal))
            return null; // «1.234,5» no es un porcentaje
        limpio = limpio.Replace(',', '.');
        if (!decimal.TryParse(limpio, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var valor) || valor is < 0 or > 100)
            return null;
        var normal = Math.Round(valor, 4, MidpointRounding.AwayFromZero);
        redondeado = normal != valor;
        return normal;
    }

    /// <summary>
    /// Número de vía o de cruce de la dirección estructurada (<c>45</c>, <c>45A</c>, <c>45 A BIS</c>, <c>45BISB</c>) ⇒ número,
    /// letra y bis. Nulo si no tiene ese formato.
    /// </summary>
    public static (string Numero, string? Letra, bool Bis)? NumeroDeVia(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim().ToUpperInvariant();
        var m = RegexNumeroDeVia().Match(limpio);
        if (!m.Success)
            return null;
        var letra = m.Groups["letra"].Success ? m.Groups["letra"].Value : m.Groups["letra2"].Success ? m.Groups["letra2"].Value : null;
        return (m.Groups["numero"].Value, letra, m.Groups["bis"].Success);
    }

    public static string? SoloDigitos(string? texto)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length > 0 && RegexDigitos().IsMatch(limpio) ? limpio : null;
    }
}
