using System.Globalization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Revision;

/// <summary>
/// Cuánto hace el sistema por sí solo al revisar un documento con IA (ADR-0012): <c>Manual</c> (solo informa, las personas
/// deciden), <c>Asistido</c> (sugiere aprobar o revisar; nunca emite veredictos) y <c>Automatico</c> (aprueba lo que no tiene
/// excepciones; lo demás va a la bandeja de excepciones de una persona).
/// </summary>
public enum NivelDeRevisionAutomatica
{
    Manual,
    Asistido,
    Automatico
}

/// <summary>Cambios que la empresa pide sobre su revisión automática (reemplazo completo; lo nulo toma el valor por omisión).</summary>
public sealed record CambiosDeRevisionAutomatica(
    string Nivel,
    decimal? UmbralConfianza,
    int? PorcentajeMuestreo,
    int? TopeMensualAprobaciones,
    IReadOnlyList<string>? TiposAutorizados,
    IReadOnlyList<Guid>? AreasAutorizadas,
    int? EsperaIaMinutos);

/// <summary>
/// Revisión automática de una empresa (esquema <c>validacion</c>, multi-tenant; id = empresa). Cada empresa decide su nivel, su
/// umbral de confianza (nunca por debajo del de su plan de IA), el porcentaje que una persona confirma por muestreo, el tope de
/// aprobaciones automáticas al mes, los tipos de documento y las áreas cuya revisión puede hacer el sistema. Las decisiones de
/// una empresa son privadas: no se comparten con otra.
/// </summary>
public sealed class RevisionAutomaticaEmpresa
{
    public const decimal UmbralMinimo = 0.85m;
    public const decimal UmbralInicial = 0.95m;
    public const int PorcentajeMuestreoInicial = 10;
    public const int EsperaIaMinutosInicial = 10;
    public const int MaximoTope = 1_000_000;
    public const int MaximoEsperaMinutos = 1440;

    public Guid Id { get; set; }

    public NivelDeRevisionAutomatica Nivel { get; set; }

    public decimal UmbralConfianza { get; set; } = UmbralInicial;

    /// <summary>Porcentaje (0–100) de las aprobaciones automáticas que una persona confirma después (muestreo).</summary>
    public int PorcentajeMuestreo { get; set; } = PorcentajeMuestreoInicial;

    /// <summary>Aprobaciones automáticas máximas al mes; alcanzado el tope, todo va a una persona. Nulo = sin tope.</summary>
    public int? TopeMensualAprobaciones { get; set; }

    /// <summary>Códigos canónicos de documento (mayúsculas) que el sistema puede aprobar solo.</summary>
    public List<string> TiposAutorizados { get; set; } = [];

    /// <summary>Áreas de la empresa cuya revisión puede hacer el sistema.</summary>
    public List<Guid> AreasAutorizadas { get; set; } = [];

    /// <summary>Cuánto se espera el análisis de IA (o el cruce con la ficha) antes de mandar el documento a una persona.</summary>
    public int EsperaIaMinutos { get; set; } = EsperaIaMinutosInicial;

    public DateTimeOffset ActualizadaEn { get; set; }

    public Guid EmpresaId => Id;

    /// <summary>Sin configurar: Manual.</summary>
    public static RevisionAutomaticaEmpresa PorDefecto(Guid empresaId) => new() { Id = empresaId };

    /// <summary>
    /// Aplica los cambios validados. 422 <c>nivel-invalido</c>, <c>umbral-invalido</c> (0,85–1), <c>muestreo-invalido</c> (0–100),
    /// <c>tope-invalido</c>, <c>espera-invalida</c> y <c>automatico-sin-alcance</c> (Automático sin tipos o sin áreas).
    /// </summary>
    public void Actualizar(CambiosDeRevisionAutomatica cambios, DateTimeOffset ahora)
    {
        if (!Enum.TryParse<NivelDeRevisionAutomatica>(cambios.Nivel, true, out var nivel) || !Enum.IsDefined(nivel))
            throw ReglaDeNegocioException.Validacion("nivel-invalido", "El nivel es Manual, Asistido o Automatico.");
        var umbral = cambios.UmbralConfianza ?? UmbralInicial;
        if (umbral is < UmbralMinimo or > 1m)
            throw ReglaDeNegocioException.Validacion("umbral-invalido",
                $"El umbral de confianza va de {UmbralMinimo.ToString("0.00", CultureInfo.InvariantCulture)} a 1: por debajo se pierde la precisión del análisis.");
        var muestreo = cambios.PorcentajeMuestreo ?? PorcentajeMuestreoInicial;
        if (muestreo is < 0 or > 100)
            throw ReglaDeNegocioException.Validacion("muestreo-invalido", "El porcentaje de muestreo va de 0 a 100.");
        if (cambios.TopeMensualAprobaciones is < 1 or > MaximoTope)
            throw ReglaDeNegocioException.Validacion("tope-invalido", $"El tope mensual va de 1 a {MaximoTope} aprobaciones, o vacío para no tener tope.");
        var espera = cambios.EsperaIaMinutos ?? EsperaIaMinutosInicial;
        if (espera is < 1 or > MaximoEsperaMinutos)
            throw ReglaDeNegocioException.Validacion("espera-invalida", $"La espera del análisis va de 1 a {MaximoEsperaMinutos} minutos.");

        var tipos = (cambios.TiposAutorizados ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim().ToUpperInvariant()).Distinct().Order().ToList();
        var areas = (cambios.AreasAutorizadas ?? []).Distinct().ToList();
        if (nivel == NivelDeRevisionAutomatica.Automatico && (tipos.Count == 0 || areas.Count == 0))
            throw ReglaDeNegocioException.Validacion("automatico-sin-alcance",
                "Para aprobar automáticamente elige al menos un tipo de documento y un área cuya revisión haga el sistema.");

        Nivel = nivel;
        UmbralConfianza = umbral;
        PorcentajeMuestreo = muestreo;
        TopeMensualAprobaciones = cambios.TopeMensualAprobaciones;
        TiposAutorizados = tipos;
        AreasAutorizadas = areas;
        EsperaIaMinutos = espera;
        ActualizadaEn = ahora;
    }

    /// <summary>Umbral que se aplica: el de la empresa, que nunca baja del de su plan de IA.</summary>
    public decimal UmbralEfectivo(decimal umbralDelPlan) => Math.Max(UmbralConfianza, umbralDelPlan);
}

/// <summary>
/// Uso de la revisión automática de una empresa en un mes (zona del negocio): aprobaciones automáticas (con las de muestreo),
/// muestreadas, excepciones y sugerencias. Esquema <c>validacion</c>, multi-tenant, concurrencia optimista; id
/// <c>{empresa:N}:{yyyy-MM}</c>.
/// </summary>
public sealed class UsoRevisionAutomatica
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public string Mes { get; set; } = string.Empty;

    /// <summary>Aprobaciones automáticas del mes (incluye las elegidas para muestreo): lo que cuenta contra el tope.</summary>
    public int Aprobadas { get; set; }

    public int Muestreadas { get; set; }

    public int Excepciones { get; set; }

    public int Sugerencias { get; set; }

    public static string Clave(Guid empresaId, string mes) => $"{empresaId:N}:{mes}";

    public static string MesDe(DateOnly dia) => dia.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static UsoRevisionAutomatica Nuevo(Guid empresaId, string mes) => new() { Id = Clave(empresaId, mes), EmpresaId = empresaId, Mes = mes };

    /// <summary>¿Cabe otra aprobación automática bajo el tope?</summary>
    public bool Cabe(int? tope) => tope is not { } t || Aprobadas < t;

    public void Registrar(ResultadoDeRevision resultado)
    {
        switch (resultado)
        {
            case ResultadoDeRevision.AprobadaAutomaticamente:
                Aprobadas++;
                break;
            case ResultadoDeRevision.AprobadaConMuestreo:
                Aprobadas++;
                Muestreadas++;
                break;
            case ResultadoDeRevision.Excepcion:
                Excepciones++;
                break;
            case ResultadoDeRevision.Sugerida:
                Sugerencias++;
                break;
        }
    }
}
