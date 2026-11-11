using System.Globalization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Cumplimiento;

/// <summary>De dónde sale el valor de un factor: lo elige el oficial al hacer el estudio o lo calcula el sistema con lo que ya sabe.</summary>
public enum OrigenDelFactor
{
    Manual,
    Automatico
}

/// <summary>
/// Factores que el sistema sabe evaluar solo (CU-17) con las opciones que cada uno puede producir. La empresa los puede
/// quitar, ponderar y cambiar el puntaje de sus opciones, pero no inventar otras: el sistema no sabría cuál elegir.
/// </summary>
public static class FactoresAutomaticos
{
    public const string Jurisdiccion = "jurisdiccion";
    public const string CoincidenciasEnListas = "coincidencias-listas";
    public const string Pep = "pep";

    public const string Nacional = "nacional";
    public const string Extranjero = "extranjero";
    public const string SinCoincidencias = "sin-coincidencias";
    public const string Descartadas = "descartadas";
    public const string PorAnalizar = "por-analizar";
    public const string Confirmadas = "confirmadas";
    public const string SinPep = "sin-pep";
    public const string ConPep = "con-pep";

    /// <summary>Opciones que el sistema puede producir por cada factor automático.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> OpcionesPosibles = new Dictionary<string, IReadOnlyList<string>>
    {
        [Jurisdiccion] = [Nacional, Extranjero],
        [CoincidenciasEnListas] = [SinCoincidencias, Descartadas, PorAnalizar, Confirmadas],
        [Pep] = [SinPep, ConPep]
    };

    public static bool Es(string codigo) => OpcionesPosibles.ContainsKey(codigo);

    /// <summary>
    /// Valor de cada factor automático para un expediente: jurisdicción (país de la identificación del tercero), coincidencias
    /// de la última consulta de listas y si entre las personas consultadas hay alguna declarada PEP.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Evaluar(string claveIdentificacionDelTercero, VerificacionDeExpediente? listas,
        IEnumerable<PersonaConsultada> personas)
    {
        var pais = claveIdentificacionDelTercero.Split('|')[0];
        var coincidencias = listas?.Coincidencias ?? [];
        var coincidencia = coincidencias.Any(c => c.Resolucion is { Decision: DecisionCoincidencia.Confirmada }) ? Confirmadas
            : coincidencias.Any(c => !c.Resuelta) ? PorAnalizar
            : coincidencias.Count > 0 ? Descartadas
            : SinCoincidencias;
        return new Dictionary<string, string>
        {
            [Jurisdiccion] = string.Equals(pais, "CO", StringComparison.OrdinalIgnoreCase) ? Nacional : Extranjero,
            [CoincidenciasEnListas] = coincidencia,
            [Pep] = personas.Any(p => p.Rol == "Pep") ? ConPep : SinPep
        };
    }
}

/// <summary>
/// Una opción de un factor con su puntaje (0–100: mayor, más riesgo). <see cref="NivelMinimo"/> (2 o 3) fija un piso: elegirla
/// no deja el riesgo por debajo de ese nivel aunque el puntaje total sea bajo (p. ej. una coincidencia confirmada ⇒ Alto).
/// </summary>
public sealed record OpcionDeFactor(string Codigo, string Nombre, int Puntaje, int? NivelMinimo = null);

/// <summary>Un factor de la matriz: <see cref="Peso"/> (1–100) es su importancia relativa; el puntaje total es el promedio ponderado.</summary>
public sealed record FactorDeRiesgo(string Codigo, string Nombre, OrigenDelFactor Origen, int Peso, IReadOnlyList<OpcionDeFactor> Opciones, string? Ayuda = null);

/// <summary>
/// Lo que se hace con cada nivel de riesgo: la debida diligencia que exige y cada cuántos meses se vuelve a revisar (CU-17).
/// <see cref="Nivel"/>: 1 Bajo, 2 Medio, 3 Alto (los mismos números del estudio del legado).
/// </summary>
public sealed record NivelDeRiesgo(int Nivel, string Nombre, DebidaDiligencia DebidaDiligencia, int MesesRevision);

/// <summary>Un valor elegido para un factor manual.</summary>
public sealed record RespuestaDeFactor(string Factor, string Opcion);

/// <summary>Cómo pesó un factor en el resultado (se guarda con el estudio: lo que se decidió es lo que se ve, aunque la matriz cambie).</summary>
public sealed record DesgloseDeFactor(string Factor, string FactorNombre, OrigenDelFactor Origen, int Peso, string Opcion, string OpcionNombre, int Puntaje,
    int? NivelMinimo);

/// <summary>
/// Resultado de aplicar la matriz: puntaje 0–100 (un decimal), nivel final (el del puntaje o el piso de una opción), la debida
/// diligencia y la periodicidad que le toca, y el desglose por factor con la versión de la matriz usada.
/// </summary>
public sealed record ResultadoDeMatriz(decimal Puntaje, int Nivel, int NivelPorPuntaje, string? ForzadoPor, DebidaDiligencia DebidaDiligencia, int MesesRevision,
    int VersionMatriz, IReadOnlyList<DesgloseDeFactor> Desglose);

/// <summary>
/// Matriz de riesgo configurable de una empresa (CU-17, SAGRILAFT): factores con pesos y opciones puntuadas, dos límites que
/// reparten el puntaje en Bajo / Medio / Alto, y la debida diligencia (simplificada, ordinaria o intensificada) y la
/// periodicidad de revisión de cada nivel. Esquema <c>validacion</c>, multi-tenant, id = empresa. Es privada de la empresa.
/// El SRM solo guardaba un nivel elegido a mano (estrellas) sin cálculo ni reglas: aquí el nivel sale de los factores.
/// </summary>
public sealed partial class MatrizDeRiesgoEmpresa
{
    public const int MaximoFactores = 20;
    public const int MaximoOpciones = 8;
    public const int MesesRevisionMaximos = 60;

    public Guid Id { get; set; }

    /// <summary>Solo una matriz activa se usa en el estudio; sin ella el oficial elige el nivel a mano (como en el legado).</summary>
    public bool Activa { get; set; }

    /// <summary>Sube en cada cambio: cada estudio guarda con qué versión se calculó.</summary>
    public int Version { get; set; }

    public List<FactorDeRiesgo> Factores { get; set; } = [];

    /// <summary>Puntaje menor a este límite ⇒ Bajo (1–99).</summary>
    public int LimiteBajo { get; set; } = 34;

    /// <summary>Puntaje menor a este límite (y desde <see cref="LimiteBajo"/>) ⇒ Medio; desde aquí ⇒ Alto (2–100).</summary>
    public int LimiteMedio { get; set; } = 67;

    public List<NivelDeRiesgo> Niveles { get; set; } = [];

    public DateTimeOffset ActualizadaEn { get; set; }

    public Guid EmpresaId => Id;

    /// <summary>La plantilla que se ofrece a una empresa que aún no configuró la suya (sin guardar, inactiva).</summary>
    public static MatrizDeRiesgoEmpresa PorDefecto(Guid empresaId) => new()
    {
        Id = empresaId,
        Activa = false,
        Version = 0,
        Factores =
        [
            new FactorDeRiesgo(FactoresAutomaticos.Jurisdiccion, "Jurisdicción del tercero", OrigenDelFactor.Automatico, 15,
            [
                new OpcionDeFactor(FactoresAutomaticos.Nacional, "Colombia", 0),
                new OpcionDeFactor(FactoresAutomaticos.Extranjero, "Otro país", 70)
            ], "País de la identificación del tercero."),
            new FactorDeRiesgo(FactoresAutomaticos.CoincidenciasEnListas, "Coincidencias en listas restrictivas", OrigenDelFactor.Automatico, 30,
            [
                new OpcionDeFactor(FactoresAutomaticos.SinCoincidencias, "Sin coincidencias", 0),
                new OpcionDeFactor(FactoresAutomaticos.Descartadas, "Coincidencias descartadas (homónimos)", 30),
                new OpcionDeFactor(FactoresAutomaticos.PorAnalizar, "Coincidencias por analizar", 70, 2),
                new OpcionDeFactor(FactoresAutomaticos.Confirmadas, "Coincidencia confirmada", 100, 3)
            ], "Resultado de la última consulta de listas del expediente."),
            new FactorDeRiesgo(FactoresAutomaticos.Pep, "Personas expuestas políticamente (PEP)", OrigenDelFactor.Automatico, 20,
            [
                new OpcionDeFactor(FactoresAutomaticos.SinPep, "Sin PEP", 0),
                new OpcionDeFactor(FactoresAutomaticos.ConPep, "Con PEP entre sus personas", 80, 2)
            ], "Si algún representante, suplente o socio se declaró PEP."),
            new FactorDeRiesgo("sector", "Sector o actividad económica", OrigenDelFactor.Manual, 20,
            [
                new OpcionDeFactor("bajo", "Sector de bajo riesgo", 0),
                new OpcionDeFactor("medio", "Sector de riesgo medio", 50),
                new OpcionDeFactor("alto", "Sector de alto riesgo (efectivo, cambio, inmobiliario…)", 100)
            ], "Lo valora el oficial según la actividad del tercero."),
            new FactorDeRiesgo("operacion", "Tipo y volumen de la operación", OrigenDelFactor.Manual, 15,
            [
                new OpcionDeFactor("bajo", "Operación pequeña o habitual", 0),
                new OpcionDeFactor("medio", "Operación mediana", 50),
                new OpcionDeFactor("alto", "Operación grande o atípica", 100)
            ], "Lo valora el oficial según lo que se va a contratar.")
        ],
        LimiteBajo = 34,
        LimiteMedio = 67,
        Niveles =
        [
            new NivelDeRiesgo(1, "Bajo", DebidaDiligencia.Simple, 24),
            new NivelDeRiesgo(2, "Medio", DebidaDiligencia.Ordinaria, 12),
            new NivelDeRiesgo(3, "Alto", DebidaDiligencia.Ampliada, 6)
        ]
    };

    /// <summary>Los factores que el oficial debe responder.</summary>
    public IEnumerable<FactorDeRiesgo> FactoresManuales => Factores.Where(f => f.Origen == OrigenDelFactor.Manual);

    /// <summary>
    /// Reemplaza la configuración y sube la versión. 422: <c>factores-invalidos</c> (cantidad, códigos repetidos o mal escritos,
    /// nombres vacíos), <c>peso-invalido</c> (1–100), <c>opciones-invalidas</c> (2–8, códigos únicos, puntaje 0–100, piso 2 o 3),
    /// <c>factor-automatico-invalido</c> (opciones distintas de las que el sistema calcula), <c>limites-invalidos</c> y
    /// <c>niveles-invalidos</c> (los tres niveles, intensidad que no baja al subir el riesgo, Alto siempre intensificada).
    /// </summary>
    public void Actualizar(bool activa, IReadOnlyList<FactorDeRiesgo> factores, int limiteBajo, int limiteMedio, IReadOnlyList<NivelDeRiesgo> niveles,
        DateTimeOffset ahora)
    {
        ValidarFactores(factores);
        if (limiteBajo is < 1 or > 99 || limiteMedio is < 2 or > 100 || limiteBajo >= limiteMedio)
            throw ReglaDeNegocioException.Validacion("limites-invalidos", "Los límites van de 1 a 100 y el de Bajo debe ser menor que el de Medio.");
        var ordenados = ValidarNiveles(niveles);
        if (activa && factores.Count == 0)
            throw ReglaDeNegocioException.Validacion("factores-invalidos", "Una matriz activa necesita al menos un factor.");

        Activa = activa;
        Factores = [.. factores.Select(f => f with { Nombre = f.Nombre.Trim(), Ayuda = string.IsNullOrWhiteSpace(f.Ayuda) ? null : f.Ayuda.Trim() })];
        LimiteBajo = limiteBajo;
        LimiteMedio = limiteMedio;
        Niveles = ordenados;
        Version++;
        ActualizadaEn = ahora;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,39}$")]
    private static partial Regex RegexCodigo();

    private static void ValidarFactores(IReadOnlyList<FactorDeRiesgo> factores)
    {
        if (factores.Count > MaximoFactores)
            throw ReglaDeNegocioException.Validacion("factores-invalidos", $"La matriz admite hasta {MaximoFactores} factores.");
        if (factores.GroupBy(f => f.Codigo).Any(g => g.Count() > 1))
            throw ReglaDeNegocioException.Validacion("factores-invalidos", "Hay dos factores con el mismo código.");

        foreach (var factor in factores)
        {
            if (!RegexCodigo().IsMatch(factor.Codigo ?? string.Empty))
                throw ReglaDeNegocioException.Validacion("factores-invalidos", "El código de un factor usa minúsculas, números y guiones (hasta 40).");
            if (string.IsNullOrWhiteSpace(factor.Nombre) || factor.Nombre.Trim().Length > 120)
                throw ReglaDeNegocioException.Validacion("factores-invalidos", $"El factor «{factor.Codigo}» necesita un nombre de hasta 120 caracteres.");
            if (factor.Peso is < 1 or > 100)
                throw ReglaDeNegocioException.Validacion("peso-invalido", $"El peso de «{factor.Nombre}» va de 1 a 100.");
            if (factor.Ayuda is { Length: > 300 })
                throw ReglaDeNegocioException.Validacion("factores-invalidos", $"La ayuda de «{factor.Nombre}» admite hasta 300 caracteres.");
            if (factor.Opciones.Count is < 2 or > MaximoOpciones || factor.Opciones.GroupBy(o => o.Codigo).Any(g => g.Count() > 1))
                throw ReglaDeNegocioException.Validacion("opciones-invalidas",
                    $"«{factor.Nombre}» necesita entre 2 y {MaximoOpciones} opciones con códigos distintos.");
            foreach (var opcion in factor.Opciones)
            {
                if (!RegexCodigo().IsMatch(opcion.Codigo ?? string.Empty) || string.IsNullOrWhiteSpace(opcion.Nombre) || opcion.Nombre.Trim().Length > 160 ||
                    opcion.Puntaje is < 0 or > 100 || opcion.NivelMinimo is not (null or 2 or 3))
                    throw ReglaDeNegocioException.Validacion("opciones-invalidas",
                        $"Una opción de «{factor.Nombre}» no es válida: código en minúsculas, nombre, puntaje de 0 a 100 y piso opcional de 2 o 3.");
            }

            if (factor.Origen == OrigenDelFactor.Automatico)
            {
                if (!FactoresAutomaticos.OpcionesPosibles.TryGetValue(factor.Codigo ?? string.Empty, out var posibles) ||
                    !factor.Opciones.Select(o => o.Codigo).OrderBy(c => c, StringComparer.Ordinal).SequenceEqual(posibles.OrderBy(c => c, StringComparer.Ordinal)))
                    throw ReglaDeNegocioException.Validacion("factor-automatico-invalido",
                        $"«{factor.Nombre}» es automático: solo puede tener las opciones que el sistema calcula ({string.Join(", ", FactoresAutomaticos.OpcionesPosibles.GetValueOrDefault(factor.Codigo ?? string.Empty) ?? [])}).");
            }
            else if (FactoresAutomaticos.Es(factor.Codigo ?? string.Empty))
            {
                throw ReglaDeNegocioException.Validacion("factores-invalidos", $"El código «{factor.Codigo}» es de un factor automático: usa otro para un factor manual.");
            }
        }
    }

    private static List<NivelDeRiesgo> ValidarNiveles(IReadOnlyList<NivelDeRiesgo> niveles)
    {
        var ordenados = niveles.OrderBy(n => n.Nivel).ToList();
        if (ordenados.Count != 3 || !ordenados.Select(n => n.Nivel).SequenceEqual([1, 2, 3]))
            throw ReglaDeNegocioException.Validacion("niveles-invalidos", "Hay tres niveles: 1 Bajo, 2 Medio y 3 Alto.");
        if (ordenados.Any(n => n.MesesRevision is < 1 or > MesesRevisionMaximos))
            throw ReglaDeNegocioException.Validacion("niveles-invalidos", $"La revisión de cada nivel va de 1 a {MesesRevisionMaximos} meses.");
        if (ordenados.Any(n => !Enum.IsDefined(n.DebidaDiligencia)) ||
            ordenados.Zip(ordenados.Skip(1)).Any(p => p.Second.DebidaDiligencia < p.First.DebidaDiligencia))
            throw ReglaDeNegocioException.Validacion("niveles-invalidos", "La debida diligencia no puede ser más ligera en un nivel de más riesgo.");
        if (ordenados[2].DebidaDiligencia != DebidaDiligencia.Ampliada)
            throw ReglaDeNegocioException.Validacion("niveles-invalidos", "El nivel Alto exige debida diligencia intensificada.");
        return [.. ordenados.Select(n => n with { Nombre = n.Nivel switch { 1 => "Bajo", 2 => "Medio", _ => "Alto" } })];
    }

    /// <summary>
    /// Calcula el riesgo: <paramref name="manuales"/> son las respuestas del oficial a los factores manuales y
    /// <paramref name="automaticos"/> lo que el sistema evaluó (<see cref="FactoresAutomaticos.Evaluar"/>). 422
    /// <c>respuestas-incompletas</c> (falta un factor manual) y <c>opcion-invalida</c> (una opción que ese factor no tiene).
    /// </summary>
    public ResultadoDeMatriz Calcular(IReadOnlyList<RespuestaDeFactor> manuales, IReadOnlyDictionary<string, string> automaticos)
    {
        var respuestas = manuales.GroupBy(r => r.Factor).ToDictionary(g => g.Key, g => g.Last().Opcion);
        var faltantes = FactoresManuales.Where(f => !respuestas.ContainsKey(f.Codigo)).Select(f => f.Nombre).ToList();
        if (faltantes.Count > 0)
            throw ReglaDeNegocioException.Validacion("respuestas-incompletas", $"Falta responder: {string.Join(", ", faltantes)}.");

        var desglose = new List<DesgloseDeFactor>();
        foreach (var factor in Factores)
        {
            var codigo = factor.Origen == OrigenDelFactor.Manual ? respuestas[factor.Codigo] : automaticos.GetValueOrDefault(factor.Codigo);
            var opcion = factor.Opciones.FirstOrDefault(o => o.Codigo == codigo)
                         ?? throw ReglaDeNegocioException.Validacion("opcion-invalida", $"«{codigo}» no es una opción de «{factor.Nombre}».");
            desglose.Add(new DesgloseDeFactor(factor.Codigo, factor.Nombre, factor.Origen, factor.Peso, opcion.Codigo, opcion.Nombre, opcion.Puntaje, opcion.NivelMinimo));
        }

        return Resolver(desglose);
    }

    /// <summary>Del desglose al resultado: promedio ponderado, nivel por límites y pisos de las opciones, y lo que exige ese nivel.</summary>
    public ResultadoDeMatriz Resolver(IReadOnlyList<DesgloseDeFactor> desglose)
    {
        var pesos = desglose.Sum(d => d.Peso);
        var puntaje = pesos == 0 ? 0m : Math.Round(desglose.Sum(d => (decimal)d.Peso * d.Puntaje) / pesos, 1, MidpointRounding.AwayFromZero);
        var porPuntaje = puntaje < LimiteBajo ? 1 : puntaje < LimiteMedio ? 2 : 3;
        var piso = desglose.Where(d => d.NivelMinimo is not null).OrderByDescending(d => d.NivelMinimo).ThenByDescending(d => d.Puntaje).FirstOrDefault();
        var nivel = Math.Max(porPuntaje, piso?.NivelMinimo ?? 1);
        var forzado = nivel > porPuntaje ? $"{piso!.FactorNombre}: {piso.OpcionNombre}" : null;
        var configuracion = Niveles.FirstOrDefault(n => n.Nivel == nivel) ?? PorDefecto(Id).Niveles.First(n => n.Nivel == nivel);
        return new ResultadoDeMatriz(puntaje, nivel, porPuntaje, forzado, configuracion.DebidaDiligencia, configuracion.MesesRevision, Version, desglose);
    }

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"Matriz v{Version} ({Factores.Count} factores)");
}
