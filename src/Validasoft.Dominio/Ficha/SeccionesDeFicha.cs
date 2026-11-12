using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>Códigos de las secciones canónicas de la ficha maestra (FM-2, docs/14 §4).</summary>
public static class CodigosDeSeccion
{
    public const string General = "FICHA_GENERAL";
    public const string Tributaria = "FICHA_TRIBUTARIA";
    public const string Legal = "FICHA_LEGAL";
    public const string Financiera = "FICHA_FINANCIERA";
    public const string Sst = "FICHA_SST";
    public const string Especialidades = "FICHA_ESPECIALIDADES";

    /// <summary>Canónico de archivo (global) con el que el tercero sube un soporte ad hoc de una sección (FM-5).</summary>
    public const string SoporteFicha = "SOPORTE_FICHA";

    /// <summary>
    /// Las cuentas bancarias del tercero como lo que respalda la certificación bancaria en el mapa de respaldo (FD-5, docs/14 §5.1). No es
    /// una sección de la ficha (no está en <see cref="CatalogoDeSecciones.Todas"/>, no tiene versiones ni se acepta): las cuentas tienen su
    /// propio flujo (CB-1…CB-3) y este código solo nombra «lo que respalda» para el cruce.
    /// </summary>
    public const string CuentasBancarias = "CUENTAS_BANCARIAS";
}

/// <summary>
/// Sección canónica: formulario global de plataforma (clase <c>Formulario</c>, ámbito <c>Global</c>) con su esquema y sus
/// reglas versionadas (FM-2, FM-16). <see cref="TiposDeSoporte"/> sale del mapa de docs/14 §5.1 (más <c>SOPORTE_FICHA</c>);
/// vacío con <see cref="SoporteDeCualquierTipo"/> = cualquier documento global del tercero.
/// </summary>
public sealed record DefinicionDeSeccion(
    string Codigo,
    string Nombre,
    bool PorPeriodo,
    bool ExigeSoporteEnCambios,
    bool SoporteDeCualquierTipo,
    IReadOnlyList<string> TiposDeSoporte,
    bool SoloJuridica,
    IEsquemaDeSeccion Esquema)
{
    public int VersionEsquema => Esquema.VersionEsquema;

    public int VersionReglas => Esquema.VersionReglas;

    /// <summary>¿El tipo de documento canónico puede respaldar esta sección?</summary>
    public bool AdmiteSoporte(string? tipoCanonico) =>
        SoporteDeCualquierTipo || (tipoCanonico is not null && TiposDeSoporte.Contains(tipoCanonico, StringComparer.OrdinalIgnoreCase));

    /// <summary>¿El wizard la pide a este tipo de persona? La persona natural no diligencia Legal ni Financiera (FM-17).</summary>
    public bool Obligatoria(TipoPersona tipoPersona) => !SoloJuridica || tipoPersona != TipoPersona.Natural;
}

/// <summary>Las seis secciones en el orden del wizard (General → Tributaria → Legal → Financiera → SST → Especialidades).</summary>
public static class CatalogoDeSecciones
{
    private static readonly string[] SoportesGeneral = ["RUT", "CAMARA_COMERCIO", "CEDULA", CodigosDeSeccion.SoporteFicha];
    private static readonly string[] SoportesTributaria = ["RUT", CodigosDeSeccion.SoporteFicha];
    private static readonly string[] SoportesLegal = ["CAMARA_COMERCIO", "RUT", "CEDULA", CodigosDeSeccion.SoporteFicha];
    private static readonly string[] SoportesFinanciera = ["ESTADOS_FINANCIEROS", CodigosDeSeccion.SoporteFicha];

    public static readonly IReadOnlyList<DefinicionDeSeccion> Todas =
    [
        new(CodigosDeSeccion.General, "Información general", false, true, false, SoportesGeneral, false, new EsquemaGeneral()),
        new(CodigosDeSeccion.Tributaria, "Información tributaria", false, true, false, SoportesTributaria, false, new EsquemaTributaria()),
        new(CodigosDeSeccion.Legal, "Información legal", false, true, false, SoportesLegal, true, new EsquemaLegal()),
        new(CodigosDeSeccion.Financiera, "Información financiera", true, true, false, SoportesFinanciera, true, new EsquemaFinanciera()),
        new(CodigosDeSeccion.Sst, "Seguridad y salud en el trabajo", false, false, true, [], false, new EsquemaSst()),
        new(CodigosDeSeccion.Especialidades, "Especialidades", false, false, true, [], false, new EsquemaEspecialidades())
    ];

    public static DefinicionDeSeccion? Buscar(string? codigo) =>
        Todas.FirstOrDefault(s => string.Equals(s.Codigo, (codigo ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>La sección o 404 <c>seccion-no-encontrada</c>.</summary>
    public static DefinicionDeSeccion Requerida(string? codigo) =>
        Buscar(codigo) ?? throw ReglaDeNegocioException.NoEncontrado("seccion-no-encontrada",
            $"No existe la sección '{codigo}'. Secciones: {string.Join(", ", Todas.Select(s => s.Codigo))}.");

    /// <summary>Orden del wizard para el tipo de persona: solo las que le son obligatorias.</summary>
    public static IReadOnlyList<DefinicionDeSeccion> OrdenDelWizard(TipoPersona tipoPersona) => [.. Todas.Where(s => s.Obligatoria(tipoPersona))];

    /// <summary>
    /// Periodo de la petición: la financiera lo exige (año entre 1990 y el actual, 422 <c>periodo-requerido</c> o
    /// <c>periodo-invalido</c>); las demás no lo admiten (<c>periodo-no-aplica</c>).
    /// </summary>
    public static int? PeriodoValido(DefinicionDeSeccion seccion, int? periodo, DateOnly hoy)
    {
        if (!seccion.PorPeriodo)
        {
            return periodo is null
                ? null
                : throw ReglaDeNegocioException.Validacion("periodo-no-aplica", $"{seccion.Nombre} no va por periodo.");
        }

        if (periodo is not { } anio)
            throw ReglaDeNegocioException.Validacion("periodo-requerido", "Indica el periodo (año) de la información financiera: ?periodo=2025.");
        if (anio < 1990 || anio > hoy.Year)
            throw ReglaDeNegocioException.Validacion("periodo-invalido", $"El periodo debe ser un año entre 1990 y {hoy.Year}.");
        return anio;
    }
}

/// <summary>Faltante de completitud: un campo sin diligenciar o una regla que no se cumple, con su motivo legible.</summary>
public sealed record FaltanteDeSeccion(string? Campo, string? Regla, string Mensaje);

/// <summary>Completitud calculada al guardar (o al recalcular) con la versión de reglas que la produjo (FM-16).</summary>
public sealed record CompletitudDeSeccion(bool Completa, IReadOnlyList<FaltanteDeSeccion> Faltantes, int ReglaVersion)
{
    public bool Igual(CompletitudDeSeccion otra) =>
        Completa == otra.Completa && ReglaVersion == otra.ReglaVersion && Faltantes.SequenceEqual(otra.Faltantes);
}

/// <summary>Dato con formato inválido: impide guardar (422 con <c>campo</c>).</summary>
public sealed record ErrorDeFormato(string Campo, string Codigo, string Mensaje);

/// <summary>Lo que el tercero es para las reglas: tipo de persona e identificación (de su registro), periodo y fecha.</summary>
public sealed record ContextoDeSeccion(TipoPersona TipoPersona, Identificacion Identificacion, int? Periodo, DateOnly Hoy)
{
    /// <summary>Consorcio y unión temporal se tratan como persona jurídica en la ficha.</summary>
    public bool EsJuridica => TipoPersona != TipoPersona.Natural;

    /// <summary>
    /// Guarda el propio tercero (HU 49093): quien responde «Sí» a «¿Es PEP?» no guarda sin su declaración completa (422). Lo que llega de un
    /// sistema de origen y el recálculo no lo exigen: la declaración que falta queda como faltante de la completitud.
    /// </summary>
    public bool ExigeDeclaracionPep { get; init; }

    /// <summary>IP desde la que el tercero firma una declaración PEP (la evidencia de su aceptación); nula si no se conoce.</summary>
    public string? IpDeFirma { get; init; }
}

/// <summary>Acumula lo que encuentran las reglas de una sección: errores de formato y faltantes de completitud.</summary>
public sealed class RevisionDeSeccion
{
    private readonly List<ErrorDeFormato> _errores = [];
    private readonly List<FaltanteDeSeccion> _faltantes = [];

    public IReadOnlyList<ErrorDeFormato> Errores => _errores;

    public IReadOnlyList<FaltanteDeSeccion> Faltantes => _faltantes;

    public void Formato(string campo, string codigo, string mensaje) => _errores.Add(new ErrorDeFormato(campo, codigo, mensaje));

    public void Formato(string campo, string mensaje) => Formato(campo, "valor-invalido", mensaje);

    /// <summary>Campo obligatorio vacío (<c>null</c>; un 0 o un <c>false</c> declarados sí cuentan).</summary>
    public void Falta(string campo, string mensaje) => _faltantes.Add(new FaltanteDeSeccion(campo, null, mensaje));

    public void Regla(string regla, string mensaje) => _faltantes.Add(new FaltanteDeSeccion(null, regla, mensaje));

    /// <summary>Obligatorio: si el valor es nulo (o texto vacío), faltante.</summary>
    public void Requerido(object? valor, string campo, string etiqueta)
    {
        if (valor is null || (valor is string texto && texto.Length == 0))
            Falta(campo, $"Falta «{etiqueta}».");
    }
}

/// <summary>Resultado de la regla única de una sección: formato (impide guardar) y completitud (se guarda con la versión).</summary>
public sealed record ResultadoDeRegla(IReadOnlyList<ErrorDeFormato> Errores, CompletitudDeSeccion Completitud);

/// <summary>
/// La regla única de la ficha (FM-16): la misma función valida el formato al guardar y calcula la completitud que se
/// persiste con la versión y la que recalcula Plataforma cuando cambia la versión de las reglas. Vacío (<c>null</c>) no
/// es cero y nada de aquí inventa datos.
/// </summary>
public static class ReglasDeFicha
{
    public const string ReglaSoporte = "soporte-del-cambio";
    public const string MensajeSoporte = "Falta el soporte del cambio";

    /// <summary>
    /// Evalúa los datos ya normalizados: formato (incluye catálogos), obligatorios por tipo de persona, filas mínimas,
    /// coherencias y, si la sección lo exige, el soporte del cambio (<paramref name="cambio"/>: los datos difieren de la
    /// versión anterior; la primera versión no lo exige).
    /// </summary>
    public static ResultadoDeRegla Evaluar(DefinicionDeSeccion seccion, object datos, ContextoDeSeccion contexto, ICatalogosDeFicha catalogos,
        bool cambio, int soportes)
    {
        var revision = new RevisionDeSeccion();
        seccion.Esquema.Revisar(datos, contexto, catalogos, revision);
        var faltantes = revision.Faltantes.ToList();
        if (cambio && seccion.ExigeSoporteEnCambios && soportes == 0)
            faltantes.Add(new FaltanteDeSeccion(null, ReglaSoporte, MensajeSoporte));
        return new ResultadoDeRegla(revision.Errores, new CompletitudDeSeccion(faltantes.Count == 0 && revision.Errores.Count == 0, faltantes,
            seccion.VersionReglas));
    }

    /// <summary>Al guardar: cualquier error de formato ⇒ 422 con el primero en <c>campo</c> y todos en <c>errores</c>.</summary>
    public static void AsegurarFormato(ResultadoDeRegla resultado)
    {
        if (resultado.Errores.Count == 0)
            return;
        var primero = resultado.Errores[0];
        throw new ReglaDeNegocioException(TipoFalla.Validacion, primero.Codigo, primero.Mensaje)
        {
            Datos = new Dictionary<string, object?>
            {
                ["campo"] = primero.Campo,
                ["errores"] = resultado.Errores.Select(e => new Dictionary<string, string>
                {
                    ["campo"] = e.Campo, ["codigo"] = e.Codigo, ["mensaje"] = e.Mensaje
                }).ToList()
            }
        };
    }

    /// <summary>
    /// Al recalcular, una versión ya guardada no se rechaza: sus errores de formato (p. ej. un código que salió del
    /// catálogo) quedan como faltantes de la completitud.
    /// </summary>
    public static CompletitudDeSeccion ParaRecalculo(ResultadoDeRegla resultado) =>
        resultado.Errores.Count == 0
            ? resultado.Completitud
            : resultado.Completitud with
            {
                Completa = false,
                Faltantes = [.. resultado.Errores.Select(e => new FaltanteDeSeccion(e.Campo, null, e.Mensaje)), .. resultado.Completitud.Faltantes]
            };

    /// <summary>Lee y normaliza los datos de la petición (422 de formato JSON con <c>campo</c>); <c>null</c> = sección vacía.</summary>
    public static object Leer(DefinicionDeSeccion seccion, JsonElement? datos, ContextoDeSeccion contexto) =>
        seccion.Esquema.Normalizar(seccion.Esquema.Leer(datos), contexto);
}
