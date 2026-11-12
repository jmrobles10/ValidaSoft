using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>
/// Esquema en código de una sección (P-3-1): cómo se leen los datos (JSON en snake_case con tipos fuertes), cómo se
/// normalizan, a qué catálogos refieren y sus reglas (formato + completitud). <see cref="VersionEsquema"/> y
/// <see cref="VersionReglas"/> se guardan con cada versión; al subir <see cref="VersionReglas"/> se recalcula (FM-16).
/// </summary>
public interface IEsquemaDeSeccion
{
    int VersionEsquema { get; }

    int VersionReglas { get; }

    /// <summary>JSON de la petición ⇒ datos tipados. Clave desconocida o tipo inválido ⇒ 422 con <c>campo</c>.</summary>
    object Leer(JsonElement? datos);

    /// <summary>Recorta textos, normaliza códigos y cifras y fija lo que no edita el tercero (identificación, DV, periodo).</summary>
    object Normalizar(object datos, ContextoDeSeccion contexto);

    /// <summary>
    /// Fija lo que decide el servidor y no el tercero (hoy, los sellos de la firma de una declaración PEP): compara con los datos de la
    /// versión vigente (<paramref name="anterior"/>, nulo si no hay) para conservar lo que no cambió. Se llama con los datos ya normalizados.
    /// </summary>
    object Sellar(object datos, JsonElement? anterior, ContextoDeSeccion contexto, OrigenDeSeccion origen);

    IReadOnlyList<ReferenciaACatalogo> Referencias(object datos);

    void Revisar(object datos, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion revision);

    /// <summary>JSON canónico (snake_case, sin nulos, en el orden del esquema) que se guarda y del que sale la huella.</summary>
    JsonElement Canonico(object datos);

    object Deserializar(JsonElement canonico);
}

/// <summary>Opciones JSON de la ficha y la huella de los datos.</summary>
public static partial class JsonDeFicha
{
    /// <summary>Lectura estricta: snake_case, sin claves desconocidas, números como número, enums como texto.</summary>
    public static readonly JsonSerializerOptions Lectura = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        Converters = { new JsonStringEnumConverter(null, allowIntegerValues: false) }
    };

    /// <summary>Escritura canónica: igual que la lectura y sin nulos (vacío = ausente).</summary>
    public static readonly JsonSerializerOptions Canonica = new(Lectura)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    [GeneratedRegex("'([^']+)' could not be mapped")]
    private static partial Regex RegexNoMapeada();

    /// <summary>SHA-256 (hex en minúsculas) del JSON canónico.</summary>
    public static string Huella(JsonElement canonico) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico.GetRawText())));

    public static T Leer<T>(JsonElement? datos) where T : class, new()
    {
        if (datos is not { } json || json.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return new T();
        if (json.ValueKind != JsonValueKind.Object)
            throw Error("datos-invalidos", "datos", "Los datos de la sección deben ser un objeto JSON.");

        try
        {
            return json.Deserialize<T>(Lectura) ?? new T();
        }
        catch (JsonException e)
        {
            var ruta = Campo(e.Path);
            if (RegexNoMapeada().Match(e.Message) is { Success: true } noMapeada)
            {
                var clave = ruta.Length == 0 ? noMapeada.Groups[1].Value : $"{ruta}.{noMapeada.Groups[1].Value}";
                if (ruta.EndsWith(noMapeada.Groups[1].Value, StringComparison.Ordinal))
                    clave = ruta;
                throw Error("campo-desconocido", clave, $"La sección no tiene el campo '{clave}'.");
            }

            throw Error("valor-invalido", ruta.Length == 0 ? "datos" : ruta,
                $"«{(ruta.Length == 0 ? "datos" : ruta)}» tiene un formato inválido: fechas aaaa-mm-dd, cifras como número con punto decimal, sí/no como true/false y opciones como texto.");
        }
    }

    public static T Deserializar<T>(JsonElement canonico) where T : class, new() => canonico.Deserialize<T>(Canonica) ?? new T();

    /// <summary><c>$.personas[0].porcentaje</c> ⇒ <c>personas[0].porcentaje</c>.</summary>
    private static string Campo(string? ruta) =>
        string.IsNullOrEmpty(ruta) || ruta == "$" ? string.Empty : ruta.StartsWith("$.", StringComparison.Ordinal) ? ruta[2..] : ruta.TrimStart('$');

    internal static ReglaDeNegocioException Error(string codigo, string campo, string mensaje) =>
        new(TipoFalla.Validacion, codigo, mensaje) { Datos = new Dictionary<string, object?> { ["campo"] = campo } };
}

/// <summary>Base de los esquemas: lectura, canónico y utilidades de normalización y validación.</summary>
public abstract partial class EsquemaDeSeccion<T> : IEsquemaDeSeccion where T : class, new()
{
    public const int MaximoTexto = 300;

    public virtual int VersionEsquema => 1;

    public virtual int VersionReglas => 1;

    public object Leer(JsonElement? datos) => JsonDeFicha.Leer<T>(datos);

    public object Normalizar(object datos, ContextoDeSeccion contexto) => Normalizar((T)datos, contexto);

    public IReadOnlyList<ReferenciaACatalogo> Referencias(object datos) => [.. Referencias((T)datos).Where(r => r.Codigo.Length > 0).Distinct()];

    public void Revisar(object datos, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion revision) =>
        Revisar((T)datos, contexto, catalogos, revision);

    public JsonElement Canonico(object datos) => JsonSerializer.SerializeToElement((T)datos, JsonDeFicha.Canonica);

    public object Deserializar(JsonElement canonico) => JsonDeFicha.Deserializar<T>(canonico);

    public object Sellar(object datos, JsonElement? anterior, ContextoDeSeccion contexto, OrigenDeSeccion origen) =>
        Sellar((T)datos, anterior is { ValueKind: JsonValueKind.Object } json ? JsonDeFicha.Deserializar<T>(json) : null, contexto, origen);

    protected abstract T Normalizar(T datos, ContextoDeSeccion contexto);

    /// <summary>Por omisión no hay nada que sellar.</summary>
    protected virtual T Sellar(T datos, T? anterior, ContextoDeSeccion contexto, OrigenDeSeccion origen) => datos;

    protected virtual IEnumerable<ReferenciaACatalogo> Referencias(T datos) => [];

    protected abstract void Revisar(T datos, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion revision);

    // ---------- normalización ----------

    [GeneratedRegex(@"\s+")]
    private static partial Regex RegexEspacios();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex RegexCorreo();

    [GeneratedRegex(@"^\+?[0-9][0-9 ()-]{5,24}$")]
    private static partial Regex RegexTelefono();

    [GeneratedRegex("^[A-Z]{2}$")]
    private static partial Regex RegexPais();

    /// <summary>Recorta y colapsa espacios; vacío ⇒ <c>null</c>.</summary>
    protected static string? Texto(string? valor)
    {
        if (valor is null)
            return null;
        var limpio = RegexEspacios().Replace(valor.Trim(), " ");
        return limpio.Length == 0 ? null : limpio;
    }

    protected static string? Mayusculas(string? valor) => Texto(valor)?.ToUpperInvariant();

    protected static string? Minusculas(string? valor) => Texto(valor)?.ToLowerInvariant();

    /// <summary>Código de catálogo normalizado (o el texto tal cual si no tiene el formato, para que la regla lo rechace).</summary>
    protected static string? Codigo(CatalogoDePlataforma catalogo, string? valor) =>
        Texto(valor) is { } texto ? ReglasDeCatalogo.NormalizarCodigo(catalogo, texto) ?? texto : null;

    /// <summary>Quita los ceros a la derecha (100,50 y 100,5 son la misma cifra y la misma huella).</summary>
    protected static decimal? Cifra(decimal? valor) => valor is { } v ? v / 1.000000000000000000000000000000000m : null;

    /// <summary>Lista vacía ⇒ <c>null</c>; elementos recortados, sin vacíos.</summary>
    protected static IReadOnlyList<string>? Lista(IEnumerable<string?>? valores, Func<string?, string?> normalizar, bool ordenar)
    {
        var lista = (valores ?? []).Select(normalizar).OfType<string>().ToList();
        if (ordenar)
            lista = [.. lista.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        return lista.Count == 0 ? null : lista;
    }

    protected static IReadOnlyList<TFila>? Filas<TFila>(IEnumerable<TFila?>? filas, Func<TFila, TFila> normalizar) where TFila : class
    {
        var lista = (filas ?? []).OfType<TFila>().Select(normalizar).ToList();
        return lista.Count == 0 ? null : lista;
    }

    // ---------- validación ----------

    protected static void Longitud(RevisionDeSeccion r, string? valor, string campo, string etiqueta, int maximo = MaximoTexto)
    {
        if (valor is not null && valor.Length > maximo)
            r.Formato(campo, $"«{etiqueta}» admite hasta {maximo} caracteres.");
    }

    protected static void Correo(RevisionDeSeccion r, string? valor, string campo)
    {
        if (valor is not null && (valor.Length > 254 || !RegexCorreo().IsMatch(valor)))
            r.Formato(campo, $"«{valor}» no es un correo válido.");
    }

    protected static void Telefono(RevisionDeSeccion r, string? valor, string campo)
    {
        if (valor is null)
            return;
        var digitos = valor.Count(char.IsAsciiDigit);
        if (!RegexTelefono().IsMatch(valor) || digitos is < 7 or > 15)
            r.Formato(campo, $"«{valor}» no es un teléfono válido (7 a 15 dígitos; puede llevar +, espacios, guiones y paréntesis).");
    }

    protected static void Pais(RevisionDeSeccion r, string? valor, string campo)
    {
        if (valor is not null && !RegexPais().IsMatch(valor))
            r.Formato(campo, $"«{valor}» no es un código de país ISO 3166-1 alfa-2 (p. ej. CO).");
    }

    protected static void NoFutura(RevisionDeSeccion r, DateOnly? fecha, DateOnly hoy, string campo, string etiqueta)
    {
        if (fecha > hoy)
            r.Formato(campo, $"«{etiqueta}» no puede ser posterior a hoy.");
    }

    /// <summary>El código existe y está activo en el catálogo (y cumple <paramref name="condicion"/>); si no ⇒ 422 <c>codigo-invalido</c>.</summary>
    protected static EntradaDeCatalogo? EnCatalogo(RevisionDeSeccion r, ICatalogosDeFicha catalogos, CatalogoDePlataforma catalogo, string? codigo,
        string campo, string etiqueta, Func<EntradaDeCatalogo, bool>? condicion = null, string? requisito = null)
    {
        if (codigo is null)
            return null;
        if (ReglasDeCatalogo.NormalizarCodigo(catalogo, codigo) is null)
        {
            r.Formato(campo, "codigo-invalido", $"«{etiqueta}»: '{codigo}' no es un {ReglasDeCatalogo.FormatoDeCodigo(catalogo)}.");
            return null;
        }

        var entrada = catalogos.Buscar(catalogo, codigo);
        if (entrada is null)
        {
            r.Formato(campo, "codigo-invalido", $"«{etiqueta}»: el código '{codigo}' no está en el catálogo de {ReglasDeCatalogo.Ruta(catalogo)}.");
            return null;
        }

        if (condicion is not null && !condicion(entrada))
        {
            r.Formato(campo, "codigo-invalido", $"«{etiqueta}»: '{codigo}' {requisito ?? "no es válido aquí"}.");
            return null;
        }

        return entrada;
    }

    /// <summary>Identificación de una persona (CO o genérica): formato y DV. Devuelve el número normalizado y su DV, o nulo.</summary>
    protected static Identificacion? IdentificacionValida(string? tipo, string? numero)
    {
        if (tipo is null || numero is null)
            return null;
        try
        {
            return Identificacion.Crear("CO", tipo, numero);
        }
        catch (ReglaDeNegocioException)
        {
            return null;
        }
    }

    protected static string Pesos(decimal valor) => valor.ToString("#,##0.##", CultureInfo.GetCultureInfo("es-CO"));
}

/// <summary><c>FICHA_GENERAL</c>.</summary>
public sealed partial class EsquemaGeneral : EsquemaDeSeccion<DatosGenerales>
{
    public const int MaximoCiiuSecundarias = 3;

    public static readonly IReadOnlyList<string> TiposDeVia =
        ["Calle", "Carrera", "Avenida", "AvenidaCalle", "AvenidaCarrera", "Diagonal", "Transversal", "Circular", "Autopista", "Kilometro"];

    public static readonly IReadOnlyList<string> Cuadrantes = ["Sur", "Este", "Norte", "Oeste"];

    [GeneratedRegex(@"^[0-9]{1,4}$")]
    private static partial Regex RegexNumeroVia();

    [GeneratedRegex(@"^[A-Z]{1,2}$")]
    private static partial Regex RegexLetra();

    // Cambia la forma de los datos (declaración PEP) y la regla (el «Sí» a PEP exige la declaración).
    public override int VersionEsquema => 2;

    public override int VersionReglas => 2;

    protected override DatosGenerales Sellar(DatosGenerales d, DatosGenerales? anterior, ContextoDeSeccion contexto, OrigenDeSeccion origen) =>
        d.DeclaracionPep is null
            ? d
            : d with
            {
                DeclaracionPep = DeclaracionPepReglas.Sellar(d.DeclaracionPep, anterior?.DeclaracionPep, d.Pep == true, anterior?.Pep == true,
                    origen == OrigenDeSeccion.Tercero, contexto.Hoy, contexto.IpDeFirma)
            };

    protected override DatosGenerales Normalizar(DatosGenerales d, ContextoDeSeccion contexto)
    {
        var id = contexto.Identificacion;
        var tipoId = CatalogoIdentificacion.Buscar(id.Pais, id.Tipo);
        var dv = tipoId is { TieneDv: true } ? DigitoVerificacion.Calcular(tipoId.AlgoritmoDv, id.Numero) : null;

        if (d.TipoPersona is { } tipoPersona && tipoPersona != contexto.TipoPersona)
            throw JsonDeFicha.Error("identificacion-no-editable", "tipo_persona",
                "El tipo de persona es el del registro del tercero; para cambiarlo, comunícate con la empresa que te invitó.");
        if (Mayusculas(d.TipoIdentificacion) is { } tipo && tipo != id.Tipo)
            throw JsonDeFicha.Error("identificacion-no-editable", "tipo_identificacion",
                "El tipo de identificación es el del registro del tercero; no se cambia desde la ficha.");
        if (Mayusculas(d.Numero) is { } numero)
        {
            var dvDelNumero = tipoId is { TieneDv: true } && numero.Contains('-') ? numero[(numero.LastIndexOf('-') + 1)..].Trim() : null;
            var sinDv = dvDelNumero is null ? numero : numero[..numero.LastIndexOf('-')];
            if (Regex.Replace(sinDv, @"[\s.\-]", string.Empty) != id.Numero)
                throw JsonDeFicha.Error("identificacion-no-editable", "numero",
                    "El número de identificación es el del registro del tercero; no se cambia desde la ficha.");
            if (dvDelNumero is { Length: > 0 } && dvDelNumero != dv)
                throw JsonDeFicha.Error("dv-no-editable", "numero", $"El dígito de verificación lo calcula Validasoft (DIAN módulo 11): para {id.Numero} es {dv}.");
        }

        if (Texto(d.Dv) is { } dvDeclarado && dvDeclarado != dv)
            throw JsonDeFicha.Error("dv-no-editable", "dv",
                dv is null
                    ? $"{id.Tipo} no lleva dígito de verificación."
                    : $"El dígito de verificación lo calcula Validasoft (DIAN módulo 11): para {id.Numero} es {dv}.");

        AsegurarCamposDelTipoDePersona(d, contexto);

        var principal = Codigo(CatalogoDePlataforma.ActividadesEconomicas, d.CiiuPrincipal);
        return d with
        {
            LugarExpedicion = Texto(d.LugarExpedicion),
            RazonSocial = Texto(d.RazonSocial),
            Nombres = Texto(d.Nombres),
            Apellidos = Texto(d.Apellidos),
            TipoPersona = contexto.TipoPersona,
            TipoIdentificacion = id.Tipo,
            Numero = id.Numero,
            Dv = dv,
            Correo = Minusculas(d.Correo),
            Telefono = Texto(d.Telefono),
            Pais = Mayusculas(d.Pais),
            Ciudad = Codigo(CatalogoDePlataforma.Ciudades, d.Ciudad),
            Direccion = Texto(d.Direccion),
            DireccionEstructurada = d.DireccionEstructurada is { } e ? NormalizarDireccion(e) : null,
            CiiuPrincipal = principal,
            CiiuSecundarias = Lista(d.CiiuSecundarias, c => Codigo(CatalogoDePlataforma.ActividadesEconomicas, c), ordenar: false)?.Distinct().ToList(),
            PaginaWeb = PaginaWeb(d.PaginaWeb),
            DeclaracionPep = contexto.EsJuridica ? null : DeclaracionPepReglas.Normalizar(d.DeclaracionPep, new OrigenDeDeclaracionPep(true, null,
                string.Join(' ', new[] { Texto(d.Nombres), Texto(d.Apellidos) }.OfType<string>()) is { Length: > 0 } nombre ? nombre : null, id.Tipo, id.Numero, null,
                Texto(d.Direccion)))
        };
    }

    /// <summary>
    /// Campos de la General por tipo de persona (FD-8): la razón social es de la jurídica (y consorcio); nombres, apellidos y lo propio
    /// de la persona natural (PEP, fecha y lugar de expedición de su documento) no aplican a una jurídica. Enviarlos es un 422
    /// <c>campo-no-aplica</c>, no un dato que se guarda y se ignora.
    /// </summary>
    private static void AsegurarCamposDelTipoDePersona(DatosGenerales d, ContextoDeSeccion contexto)
    {
        if (contexto.EsJuridica)
        {
            if (Texto(d.Nombres) is not null)
                throw NoAplica("nombres", "Nombres", "una persona jurídica: diligencia la razón social");
            if (Texto(d.Apellidos) is not null)
                throw NoAplica("apellidos", "Apellidos", "una persona jurídica: diligencia la razón social");
            if (d.Pep is not null)
                throw NoAplica("pep", "PEP", "una persona jurídica: se declara en cada persona de la información legal");
            if (d.VinculoPep is not null)
                throw NoAplica("vinculo_pep", "Vínculo con PEP", "una persona jurídica: se declara en cada persona de la información legal");
            if (d.FechaExpedicion is not null)
                throw NoAplica("fecha_expedicion", "Fecha de expedición", "una persona jurídica");
            if (Texto(d.LugarExpedicion) is not null)
                throw NoAplica("lugar_expedicion", "Lugar de expedición", "una persona jurídica");
            if (d.DeclaracionPep is not null)
                throw NoAplica("declaracion_pep", "Declaración PEP", "una persona jurídica: se diligencia por cada persona de la información legal");
        }
        else if (Texto(d.RazonSocial) is not null)
        {
            throw NoAplica("razon_social", "Razón social", "una persona natural: diligencia nombres y apellidos");
        }
    }

    private static ReglaDeNegocioException NoAplica(string campo, string etiqueta, string aQuien) =>
        JsonDeFicha.Error("campo-no-aplica", campo, $"«{etiqueta}» no aplica a {aQuien}.");

    private static DireccionEstructurada? NormalizarDireccion(DireccionEstructurada e)
    {
        var normal = e with
        {
            TipoVia = TiposDeVia.FirstOrDefault(t => string.Equals(t, Texto(e.TipoVia), StringComparison.OrdinalIgnoreCase)) ?? Texto(e.TipoVia),
            NumeroVia = Texto(e.NumeroVia),
            LetraVia = Mayusculas(e.LetraVia),
            Cuadrante = Cuadrantes.FirstOrDefault(c => string.Equals(c, Texto(e.Cuadrante), StringComparison.OrdinalIgnoreCase)) ?? Texto(e.Cuadrante),
            NumeroCruce = Texto(e.NumeroCruce),
            LetraCruce = Mayusculas(e.LetraCruce),
            Placa = Texto(e.Placa),
            Complemento = Texto(e.Complemento)
        };
        return normal == new DireccionEstructurada() ? null : normal;
    }

    private static string? PaginaWeb(string? valor)
    {
        var texto = Texto(valor);
        if (texto is null)
            return null;
        return texto.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || texto.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? texto
            : "https://" + texto;
    }

    protected override IEnumerable<ReferenciaACatalogo> Referencias(DatosGenerales d)
    {
        if (d.Ciudad is { } ciudad)
            yield return new(CatalogoDePlataforma.Ciudades, ciudad);
        if (d.CiiuPrincipal is { } principal)
            yield return new(CatalogoDePlataforma.ActividadesEconomicas, principal);
        foreach (var ciiu in d.CiiuSecundarias ?? [])
            yield return new(CatalogoDePlataforma.ActividadesEconomicas, ciiu);
    }

    protected override void Revisar(DatosGenerales d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        if (contexto.EsJuridica)
        {
            r.Requerido(d.RazonSocial, "razon_social", "Razón social");
        }
        else
        {
            r.Requerido(d.Nombres, "nombres", "Nombres");
            r.Requerido(d.Apellidos, "apellidos", "Apellidos");
        }

        Longitud(r, d.RazonSocial, "razon_social", "Razón social");
        Longitud(r, d.Nombres, "nombres", "Nombres", 150);
        Longitud(r, d.Apellidos, "apellidos", "Apellidos", 150);
        NoFutura(r, d.FechaExpedicion, contexto.Hoy, "fecha_expedicion", "Fecha de expedición");
        Longitud(r, d.LugarExpedicion, "lugar_expedicion", "Lugar de expedición", 100);
        if (!contexto.EsJuridica)
            DeclaracionPepReglas.Revisar(d.DeclaracionPep, d.Pep == true, "declaracion_pep", contexto, r);

        r.Requerido(d.Correo, "correo", "Correo electrónico");
        Correo(r, d.Correo, "correo");
        r.Requerido(d.Telefono, "telefono", "Teléfono");
        Telefono(r, d.Telefono, "telefono");

        r.Requerido(d.Pais, "pais", "País");
        Pais(r, d.Pais, "pais");
        if (d.Pais == "CO")
            r.Requerido(d.Ciudad, "ciudad", "Ciudad");
        else if (d.Pais is not null && d.Ciudad is not null)
            r.Formato("ciudad", "La ciudad (código DANE) solo aplica a domicilios en Colombia.");
        EnCatalogo(r, catalogos, CatalogoDePlataforma.Ciudades, d.Ciudad, "ciudad", "Ciudad");

        r.Requerido(d.Direccion, "direccion", "Dirección");
        Longitud(r, d.Direccion, "direccion", "Dirección");
        if (d.DireccionEstructurada is { } e)
            RevisarDireccion(e, r);

        r.Requerido(d.CiiuPrincipal, "ciiu_principal", "Actividad económica principal (CIIU)");
        EnCatalogo(r, catalogos, CatalogoDePlataforma.ActividadesEconomicas, d.CiiuPrincipal, "ciiu_principal", "Actividad económica principal");
        var secundarias = d.CiiuSecundarias ?? [];
        if (secundarias.Count > MaximoCiiuSecundarias)
            r.Formato("ciiu_secundarias", "demasiadas-filas", $"Puedes indicar hasta {MaximoCiiuSecundarias} actividades secundarias.");
        for (var i = 0; i < secundarias.Count; i++)
        {
            if (secundarias[i] == d.CiiuPrincipal)
                r.Formato($"ciiu_secundarias[{i}]", "La actividad secundaria no puede ser la misma principal.");
            EnCatalogo(r, catalogos, CatalogoDePlataforma.ActividadesEconomicas, secundarias[i], $"ciiu_secundarias[{i}]", "Actividad secundaria");
        }

        if (d.PaginaWeb is { } web && (web.Length > MaximoTexto || !Uri.TryCreate(web, UriKind.Absolute, out var url) || !url.Host.Contains('.')))
            r.Formato("pagina_web", $"«{web}» no es una página web válida.");
        if (d.AnioInicio is { } anio && (anio < 1800 || anio > contexto.Hoy.Year))
            r.Formato("anio_inicio", $"El año de inicio debe estar entre 1800 y {contexto.Hoy.Year}.");
    }

    private static void RevisarDireccion(DireccionEstructurada e, RevisionDeSeccion r)
    {
        const string Base = "direccion_estructurada";
        if (e.TipoVia is null || !TiposDeVia.Contains(e.TipoVia))
            r.Formato($"{Base}.tipo_via", $"El tipo de vía debe ser uno de: {string.Join(", ", TiposDeVia)}.");
        if (e.NumeroVia is null || !RegexNumeroVia().IsMatch(e.NumeroVia))
            r.Formato($"{Base}.numero_via", "El número de la vía son 1 a 4 dígitos.");
        if (e.LetraVia is not null && !RegexLetra().IsMatch(e.LetraVia))
            r.Formato($"{Base}.letra_via", "La letra de la vía son una o dos letras.");
        if (e.LetraCruce is not null && !RegexLetra().IsMatch(e.LetraCruce))
            r.Formato($"{Base}.letra_cruce", "La letra del cruce son una o dos letras.");
        if (e.Cuadrante is not null && !Cuadrantes.Contains(e.Cuadrante))
            r.Formato($"{Base}.cuadrante", $"El cuadrante debe ser uno de: {string.Join(", ", Cuadrantes)}.");
        if (e.NumeroCruce is not null && !RegexNumeroVia().IsMatch(e.NumeroCruce))
            r.Formato($"{Base}.numero_cruce", "El número del cruce son 1 a 4 dígitos.");
        if (e.Placa is not null && !RegexNumeroVia().IsMatch(e.Placa))
            r.Formato($"{Base}.placa", "La placa son 1 a 4 dígitos.");
        Longitud(r, e.Complemento, $"{Base}.complemento", "Complemento", 100);
    }
}

/// <summary><c>FICHA_TRIBUTARIA</c> (+ ICA).</summary>
public sealed class EsquemaTributaria : EsquemaDeSeccion<DatosTributarios>
{
    public const string GranContribuyente = "13";
    public const string Autorretenedor = "15";
    public const string ResponsableIva = "48";
    public const string NoResponsableIva = "49";
    public const int MaximoFilasIca = 50;

    protected override DatosTributarios Normalizar(DatosTributarios d, ContextoDeSeccion contexto) => d with
    {
        Responsabilidades = Lista(d.Responsabilidades, c => Codigo(CatalogoDePlataforma.ResponsabilidadesDian, c), ordenar: true),
        ResolucionGranContribuyente = NormalizarResolucion(d.ResolucionGranContribuyente),
        ResolucionAutorretenedor = NormalizarResolucion(d.ResolucionAutorretenedor),
        Ica = Filas(d.Ica, f => f with
        {
            Municipio = Codigo(CatalogoDePlataforma.Ciudades, f.Municipio),
            Ciiu = Codigo(CatalogoDePlataforma.ActividadesEconomicas, f.Ciiu),
            TarifaPorMil = Cifra(f.TarifaPorMil)
        })?.Where(f => f != new FilaIca()).ToList() is { Count: > 0 } filas ? filas : null
    };

    private static Resolucion? NormalizarResolucion(Resolucion? r)
    {
        if (r is null)
            return null;
        var normal = r with { Numero = Texto(r.Numero) };
        return normal == new Resolucion() ? null : normal;
    }

    protected override IEnumerable<ReferenciaACatalogo> Referencias(DatosTributarios d)
    {
        foreach (var codigo in d.Responsabilidades ?? [])
            yield return new(CatalogoDePlataforma.ResponsabilidadesDian, codigo);
        foreach (var fila in d.Ica ?? [])
        {
            if (fila.Municipio is { } municipio)
                yield return new(CatalogoDePlataforma.Ciudades, municipio);
            if (fila.Ciiu is { } ciiu)
                yield return new(CatalogoDePlataforma.ActividadesEconomicas, ciiu);
        }
    }

    protected override void Revisar(DatosTributarios d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        var responsabilidades = d.Responsabilidades ?? [];
        if (responsabilidades.Count == 0)
            r.Falta("responsabilidades", "Indica tus responsabilidades de la casilla 53 del RUT.");
        for (var i = 0; i < responsabilidades.Count; i++)
            EnCatalogo(r, catalogos, CatalogoDePlataforma.ResponsabilidadesDian, responsabilidades[i], $"responsabilidades[{i}]", "Responsabilidad");
        if (responsabilidades.Contains(ResponsableIva) && responsabilidades.Contains(NoResponsableIva))
            r.Regla("responsabilidades-incompatibles", "No puedes ser responsable de IVA (48) y no responsable de IVA (49) a la vez.");

        RevisarResolucion(d.ResolucionGranContribuyente, responsabilidades.Contains(GranContribuyente), "resolucion_gran_contribuyente",
            "gran contribuyente", contexto, r);
        RevisarResolucion(d.ResolucionAutorretenedor, responsabilidades.Contains(Autorretenedor), "resolucion_autorretenedor", "autorretenedor",
            contexto, r);

        r.Requerido(d.ResponsableIca, "responsable_ica", "¿Eres contribuyente del impuesto de industria y comercio (ICA)?");
        var ica = d.Ica ?? [];
        if (d.ResponsableIca == true && ica.Count == 0)
            r.Regla("ica-requerido", "Como contribuyente de ICA, indica al menos un municipio donde declaras y su actividad.");
        if (d.ResponsableIca == false && ica.Count > 0)
            r.Regla("ica-sin-responsabilidad", "Declaraste municipios de ICA, pero indicaste que no eres contribuyente de ICA.");
        if (ica.Count > MaximoFilasIca)
            r.Formato("ica", "demasiadas-filas", $"Puedes indicar hasta {MaximoFilasIca} municipios de ICA.");

        var vistos = new HashSet<(string?, string?)>();
        for (var i = 0; i < ica.Count; i++)
        {
            var fila = ica[i];
            var campo = $"ica[{i}]";
            r.Requerido(fila.Municipio, $"{campo}.municipio", "Municipio de ICA");
            r.Requerido(fila.Ciiu, $"{campo}.ciiu", "Actividad de ICA (CIIU)");
            EnCatalogo(r, catalogos, CatalogoDePlataforma.Ciudades, fila.Municipio, $"{campo}.municipio", "Municipio de ICA");
            EnCatalogo(r, catalogos, CatalogoDePlataforma.ActividadesEconomicas, fila.Ciiu, $"{campo}.ciiu", "Actividad de ICA");
            if (fila.TarifaPorMil is < 0 or > 100)
                r.Formato($"{campo}.tarifa_por_mil", "La tarifa de ICA va por mil, entre 0 y 100.");
            if (!vistos.Add((fila.Municipio, fila.Ciiu)))
                r.Formato(campo, "fila-duplicada", "Ese municipio con esa actividad ya está en la lista.");
        }
    }

    private static void RevisarResolucion(Resolucion? resolucion, bool exigida, string campo, string calidad, ContextoDeSeccion contexto,
        RevisionDeSeccion r)
    {
        if (exigida)
        {
            r.Requerido(resolucion?.Numero, $"{campo}.numero", $"Número de la resolución de {calidad}");
            r.Requerido(resolucion?.Fecha, $"{campo}.fecha", $"Fecha de la resolución de {calidad}");
        }

        Longitud(r, resolucion?.Numero, $"{campo}.numero", "Número de la resolución", 50);
        NoFutura(r, resolucion?.Fecha, contexto.Hoy, $"{campo}.fecha", "Fecha de la resolución");
    }
}

/// <summary><c>FICHA_FINANCIERA</c> de un periodo.</summary>
public sealed class EsquemaFinanciera : EsquemaDeSeccion<DatosFinancieros>
{
    /// <summary>Tolerancia de la ecuación contable: 0,5 % de los activos.</summary>
    public const decimal Tolerancia = 0.005m;

    public const decimal MaximoCifra = 1_000_000_000_000_000m;

    protected override DatosFinancieros Normalizar(DatosFinancieros d, ContextoDeSeccion contexto)
    {
        if (d.Periodo is { } periodo && periodo != contexto.Periodo)
            throw JsonDeFicha.Error("periodo-no-coincide", "periodo", $"Los datos son del periodo {periodo} y estás guardando el {contexto.Periodo}.");

        return d with
        {
            Periodo = contexto.Periodo,
            Activos = Cifra(d.Activos),
            Pasivos = Cifra(d.Pasivos),
            Patrimonio = Cifra(d.Patrimonio),
            IngresosBrutos = Cifra(d.IngresosBrutos),
            IngresosNetos = Cifra(d.IngresosNetos),
            GastosOperacionales = Cifra(d.GastosOperacionales),
            GastosNoOperacionales = Cifra(d.GastosNoOperacionales),
            OtrosIngresos = Cifra(d.OtrosIngresos),
            MonedaExtranjera = d.MonedaExtranjera is { } m
                ? m with
                {
                    Tipos = m.Tipos is { Count: > 0 } tipos ? [.. tipos.Distinct().Order()] : null,
                    Paises = Lista(m.Paises, Mayusculas, ordenar: true)
                }
                : null
        };
    }

    protected override void Revisar(DatosFinancieros d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        r.Requerido(d.FechaCorte, "fecha_corte", "Fecha de corte");
        NoFutura(r, d.FechaCorte, contexto.Hoy, "fecha_corte", "Fecha de corte");
        if (d.FechaCorte is { } corte && corte.Year != d.Periodo)
            r.Formato("fecha_corte", $"La fecha de corte debe ser del periodo {d.Periodo}.");

        Cifra(r, d.Activos, "activos", "Activos totales", negativo: false);
        Cifra(r, d.Pasivos, "pasivos", "Pasivos totales", negativo: false);
        Cifra(r, d.Patrimonio, "patrimonio", "Patrimonio", negativo: true);
        Cifra(r, d.IngresosBrutos, "ingresos_brutos", "Ingresos brutos", negativo: false);
        Cifra(r, d.IngresosNetos, "ingresos_netos", "Ingresos netos", negativo: false);
        Cifra(r, d.GastosOperacionales, "gastos_operacionales", "Gastos operacionales", negativo: false);
        Cifra(r, d.GastosNoOperacionales, "gastos_no_operacionales", "Gastos no operacionales", negativo: false);
        Cifra(r, d.OtrosIngresos, "otros_ingresos", "Otros ingresos", negativo: false);

        if (d is { Activos: { } activos, Pasivos: { } pasivos, Patrimonio: { } patrimonio } &&
            Math.Abs(activos - (pasivos + patrimonio)) > Math.Abs(activos) * Tolerancia)
            r.Regla("ecuacion-contable",
                $"Los activos ({Pesos(activos)}) deben ser iguales a pasivos más patrimonio ({Pesos(pasivos + patrimonio)}), con una tolerancia del 0,5 %.");
        if (d is { IngresosBrutos: { } brutos, IngresosNetos: { } netos } && netos > brutos)
            r.Regla("ingresos-netos-mayores", "Los ingresos netos no pueden ser mayores que los ingresos brutos.");

        var moneda = d.MonedaExtranjera;
        r.Requerido(moneda?.Realiza, "moneda_extranjera.realiza", "¿Realizas operaciones en moneda extranjera?");
        if (moneda?.Realiza == true)
        {
            if (moneda.Tipos is not { Count: > 0 })
                r.Falta("moneda_extranjera.tipos", "Indica qué operaciones en moneda extranjera realizas.");
            if (moneda.Paises is not { Count: > 0 })
                r.Falta("moneda_extranjera.paises", "Indica con qué países realizas operaciones en moneda extranjera.");
        }
        else if (moneda?.Realiza == false && (moneda.Tipos is { Count: > 0 } || moneda.Paises is { Count: > 0 }))
        {
            r.Regla("moneda-extranjera-incoherente", "Indicaste operaciones o países en moneda extranjera, pero que no realizas operaciones en moneda extranjera.");
        }

        var paises = moneda?.Paises ?? [];
        for (var i = 0; i < paises.Count; i++)
            Pais(r, paises[i], $"moneda_extranjera.paises[{i}]");
    }

    private static void Cifra(RevisionDeSeccion r, decimal? valor, string campo, string etiqueta, bool negativo)
    {
        r.Requerido(valor, campo, etiqueta);
        if (valor is not { } v)
            return;
        if (!negativo && v < 0)
            r.Formato(campo, $"«{etiqueta}» no puede ser negativo.");
        if (Math.Abs(v) >= MaximoCifra)
            r.Formato(campo, $"«{etiqueta}» está fuera de rango.");
        if (v.Scale > 2)
            r.Formato(campo, $"«{etiqueta}» admite hasta dos decimales.");
    }
}

/// <summary><c>FICHA_LEGAL</c>: personas relacionadas (fase 1).</summary>
public sealed class EsquemaLegal : EsquemaDeSeccion<DatosLegales>
{
    public const int MaximoPersonas = 100;

    // Cambia la forma de los datos (declaración PEP por persona) y la regla (el «Sí» a PEP exige la declaración).
    public override int VersionEsquema => 2;

    public override int VersionReglas => 2;

    protected override DatosLegales Sellar(DatosLegales d, DatosLegales? anterior, ContextoDeSeccion contexto, OrigenDeSeccion origen)
    {
        if (d.Personas is null || d.Personas.All(p => p.DeclaracionPep is null))
            return d;
        var delTercero = origen == OrigenDeSeccion.Tercero;
        return d with
        {
            Personas =
            [
                .. d.Personas.Select(p =>
                {
                    if (p.DeclaracionPep is null)
                        return p;
                    // La misma persona en la versión anterior: mismo rol e identificación (la identidad de la fila).
                    var previa = anterior?.Personas?.FirstOrDefault(x => x.Rol == p.Rol && x.TipoIdentificacion == p.TipoIdentificacion && x.Numero == p.Numero);
                    return p with
                    {
                        DeclaracionPep = DeclaracionPepReglas.Sellar(p.DeclaracionPep, previa?.DeclaracionPep, p.Pep == true, previa?.Pep == true, delTercero,
                            contexto.Hoy, contexto.IpDeFirma)
                    };
                })
            ]
        };
    }

    protected override DatosLegales Normalizar(DatosLegales d, ContextoDeSeccion contexto) => d with
    {
        Personas = Filas(d.Personas, NormalizarPersona),
        RevisorFiscalFirma = NormalizarFirma(d.RevisorFiscalFirma)
    };

    private static FirmaRevisora? NormalizarFirma(FirmaRevisora? firma)
    {
        if (firma is null)
            return null;
        var normal = firma with { Nit = Mayusculas(firma.Nit), RazonSocial = Texto(firma.RazonSocial) };
        return normal == new FirmaRevisora() ? null : normal;
    }

    private static PersonaRelacionada NormalizarPersona(PersonaRelacionada p)
    {
        var tipo = Mayusculas(p.TipoIdentificacion);
        var numero = Mayusculas(p.Numero);
        var id = IdentificacionValida(tipo, numero);
        var normal = p with
        {
            TipoRepresentacion = Texto(p.TipoRepresentacion),
            TipoIdentificacion = tipo,
            // Un número válido queda sin puntos ni DV; uno inválido queda tal cual para que la regla lo rechace.
            Numero = id?.Numero ?? numero,
            // El DV lo calcula Validasoft: uno declarado distinto se conserva para que la regla lo rechace.
            Dv = id is null || (Texto(p.Dv) is { } declarado && declarado != id.DigitoVerificacion) ? Texto(p.Dv) : id.DigitoVerificacion,
            Nombres = Texto(p.Nombres),
            Apellidos = Texto(p.Apellidos),
            Cargo = Texto(p.Cargo),
            LugarExpedicion = Texto(p.LugarExpedicion),
            Porcentaje = Cifra(p.Porcentaje),
            Nacionalidad = Mayusculas(p.Nacionalidad),
            Residencia = Mayusculas(p.Residencia),
            Correo = Minusculas(p.Correo),
            Telefono = Texto(p.Telefono)
        };
        // Los datos básicos de la declaración se copian de la persona ya normalizada (nombre, documento y rol).
        return normal with
        {
            DeclaracionPep = DeclaracionPepReglas.Normalizar(p.DeclaracionPep, new OrigenDeDeclaracionPep(false, normal.Rol,
                string.Join(' ', new[] { normal.Nombres, normal.Apellidos }.OfType<string>()) is { Length: > 0 } nombre ? nombre : null, normal.TipoIdentificacion,
                normal.Numero, normal.Nacionalidad, null))
        };
    }

    protected override void Revisar(DatosLegales d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        var personas = d.Personas ?? [];
        if (personas.Count > MaximoPersonas)
            r.Formato("personas", "demasiadas-filas", $"Puedes registrar hasta {MaximoPersonas} personas.");

        var vistas = new HashSet<(RolPersonaRelacionada?, string?, string?)>();
        for (var i = 0; i < personas.Count; i++)
        {
            var p = personas[i];
            var campo = $"personas[{i}]";
            r.Requerido(p.Rol, $"{campo}.rol", "Rol");
            r.Requerido(p.TipoIdentificacion, $"{campo}.tipo_identificacion", "Tipo de identificación");
            r.Requerido(p.Numero, $"{campo}.numero", "Número de identificación");
            r.Requerido(p.Nombres, $"{campo}.nombres", p.TipoIdentificacion == "NIT" ? "Razón social" : "Nombres");
            if (p.TipoIdentificacion != "NIT")
                r.Requerido(p.Apellidos, $"{campo}.apellidos", "Apellidos");
            r.Requerido(p.Pep, $"{campo}.pep", "¿Es persona expuesta políticamente (PEP)?");
            r.Requerido(p.VinculoPep, $"{campo}.vinculo_pep", "¿Tiene vínculo con una persona expuesta políticamente?");
            if (p.Rol == RolPersonaRelacionada.Socio)
                r.Requerido(p.Porcentaje, $"{campo}.porcentaje", "Porcentaje de participación");

            if (p.TipoIdentificacion is { } tipo && CatalogoIdentificacion.Buscar("CO", tipo) is null)
            {
                r.Formato($"{campo}.tipo_identificacion", $"«{tipo}» no es un tipo de identificación válido (CC, CE, NIT, PA, PPT, TI, PASAPORTE, ID_FISCAL).");
            }
            else if (p is { TipoIdentificacion: { } tipoValido, Numero: { } numero })
            {
                var id = IdentificacionValida(tipoValido, numero);
                if (id is null)
                    r.Formato($"{campo}.numero", $"«{numero}» no es un número de {tipoValido} válido.");
                else if (p.Dv != id.DigitoVerificacion)
                    r.Formato($"{campo}.dv", "dv-no-editable", $"El dígito de verificación lo calcula Validasoft (DIAN módulo 11): para {id.Numero} es {id.DigitoVerificacion}.");
            }

            Longitud(r, p.Nombres, $"{campo}.nombres", "Nombres", 200);
            Longitud(r, p.Apellidos, $"{campo}.apellidos", "Apellidos", 150);
            Longitud(r, p.Cargo, $"{campo}.cargo", "Cargo", 150);
            Longitud(r, p.LugarExpedicion, $"{campo}.lugar_expedicion", "Lugar de expedición", 100);
            Longitud(r, p.TipoRepresentacion, $"{campo}.tipo_representacion", "Tipo de representación", 100);
            if (p.Porcentaje is < 0 or > 100)
                r.Formato($"{campo}.porcentaje", "El porcentaje de participación va de 0 a 100.");
            if (p.Porcentaje is { Scale: > 4 })
                r.Formato($"{campo}.porcentaje", "El porcentaje admite hasta cuatro decimales.");
            NoFutura(r, p.FechaExpedicion, contexto.Hoy, $"{campo}.fecha_expedicion", "Fecha de expedición");
            NoFutura(r, p.FechaNacimiento, contexto.Hoy, $"{campo}.fecha_nacimiento", "Fecha de nacimiento");
            if (p is { FechaNacimiento: { } nacimiento, FechaExpedicion: { } expedicion } && nacimiento >= expedicion)
                r.Formato($"{campo}.fecha_nacimiento", "La fecha de nacimiento debe ser anterior a la de expedición del documento.");
            Pais(r, p.Nacionalidad, $"{campo}.nacionalidad");
            Pais(r, p.Residencia, $"{campo}.residencia");
            Correo(r, p.Correo, $"{campo}.correo");
            Telefono(r, p.Telefono, $"{campo}.telefono");
            DeclaracionPepReglas.Revisar(p.DeclaracionPep, p.Pep == true, $"{campo}.declaracion_pep", contexto, r);
            if (p.Rol is not null && p.Numero is not null && !vistas.Add((p.Rol, p.TipoIdentificacion, p.Numero)))
                r.Formato(campo, "persona-duplicada", "Esa persona ya está registrada con ese rol.");
        }

        if (!personas.Any(p => p.Rol == RolPersonaRelacionada.RepresentanteLegal))
            r.Regla("representante-legal-requerido", "Registra al menos un representante legal.");
        if (contexto.EsJuridica && !personas.Any(p => p.Rol == RolPersonaRelacionada.Socio))
            r.Regla("socio-requerido", "Registra al menos un socio o accionista.");
        var participacion = personas.Where(p => p.Rol == RolPersonaRelacionada.Socio).Sum(p => p.Porcentaje ?? 0);
        if (participacion > 100)
            r.Regla("participacion-mayor-a-100", $"La participación de los socios suma {participacion.ToString(CultureInfo.InvariantCulture)} %: no puede pasar de 100 %.");

        if (d.RevisorFiscalFirma is { } firma)
        {
            r.Requerido(firma.Nit, "revisor_fiscal_firma.nit", "NIT de la firma de revisoría fiscal");
            r.Requerido(firma.RazonSocial, "revisor_fiscal_firma.razon_social", "Razón social de la firma de revisoría fiscal");
            if (firma.Nit is not null && IdentificacionValida("NIT", firma.Nit) is null)
                r.Formato("revisor_fiscal_firma.nit", $"«{firma.Nit}» no es un NIT válido.");
            Longitud(r, firma.RazonSocial, "revisor_fiscal_firma.razon_social", "Razón social");
        }
    }
}

/// <summary><c>FICHA_SST</c>: las ocho preguntas sí/no son obligatorias (un «no» es una respuesta; vacío no).</summary>
public sealed class EsquemaSst : EsquemaDeSeccion<DatosSst>
{
    protected override DatosSst Normalizar(DatosSst d, ContextoDeSeccion contexto) => d;

    protected override void Revisar(DatosSst d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        r.Requerido(d.ProgramaSaludOcupacional, "programa_salud_ocupacional", "¿Tiene programa de salud ocupacional (SG-SST)?");
        r.Requerido(d.IdentificaFactoresDeRiesgo, "identifica_factores_de_riesgo", "¿Identifica y controla los factores de riesgo?");
        r.Requerido(d.ComiteParitario, "comite_paritario", "¿Tiene comité paritario (COPASST) o vigía?");
        r.Requerido(d.ProgramaHigieneYSeguridad, "programa_higiene_y_seguridad", "¿Tiene programa de higiene y seguridad industrial?");
        r.Requerido(d.GestionAmbiental, "gestion_ambiental", "¿Tiene gestión ambiental?");
        r.Requerido(d.ReglamentoInterno, "reglamento_interno", "¿Tiene reglamento interno de trabajo?");
        r.Requerido(d.PoliticaHseq, "politica_hseq", "¿Tiene política HSEQ?");
        r.Requerido(d.LicenciasYPermisos, "licencias_y_permisos", "¿Tiene las licencias y permisos que su actividad exige?");
    }
}

/// <summary><c>FICHA_ESPECIALIDADES</c>: al menos una, de nivel 3 del catálogo.</summary>
public sealed class EsquemaEspecialidades : EsquemaDeSeccion<DatosEspecialidades>
{
    public const int Maximo = 200;

    protected override DatosEspecialidades Normalizar(DatosEspecialidades d, ContextoDeSeccion contexto) => d with
    {
        Especialidades = Lista(d.Especialidades, c => Codigo(CatalogoDePlataforma.Especialidades, c), ordenar: true)
    };

    protected override IEnumerable<ReferenciaACatalogo> Referencias(DatosEspecialidades d) =>
        (d.Especialidades ?? []).Select(c => new ReferenciaACatalogo(CatalogoDePlataforma.Especialidades, c));

    protected override void Revisar(DatosEspecialidades d, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos, RevisionDeSeccion r)
    {
        var especialidades = d.Especialidades ?? [];
        if (especialidades.Count == 0)
            r.Regla("especialidad-requerida", "Indica al menos una especialidad.");
        if (especialidades.Count > Maximo)
            r.Formato("especialidades", "demasiadas-filas", $"Puedes indicar hasta {Maximo} especialidades.");
        for (var i = 0; i < especialidades.Count; i++)
            EnCatalogo(r, catalogos, CatalogoDePlataforma.Especialidades, especialidades[i], $"especialidades[{i}]", "Especialidad",
                e => e.Nivel == 3, "es un grupo o una categoría: elige una especialidad (nivel 3)");
    }
}
