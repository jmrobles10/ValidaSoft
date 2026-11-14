using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Flujos;

/// <summary>
/// Cuándo se escoge un flujo solo, sin que nadie lo indique (FR-1): una <see cref="Condicion"/> sobre el tercero y el vínculo
/// (el mismo motor Y/O/No que «se pide cuando» de los requisitos, <see cref="CondicionDeExigencia"/>) y una
/// <see cref="Prioridad"/> (la menor gana). Viaja con la versión del flujo y se congela al publicarla. Sin condición ni
/// prioridad el flujo no participa: solo se llega a él de forma explícita o, si es el por defecto, cuando ninguna regla aplica.
/// </summary>
public sealed record SeleccionDeFlujo(CondicionDeExigencia? Condicion, int? Prioridad)
{
    /// <summary>Prioridad más alta que admite un flujo (1 es la primera en evaluarse).</summary>
    public const int PrioridadMaxima = 999;

    /// <summary>Participa en la selección automática: tiene condición y prioridad.</summary>
    public bool Participa => Condicion is not null && Prioridad is not null;

    /// <summary>
    /// Valida y normaliza contra el tipo de vínculo del flujo: la condición con las mismas reglas de los requisitos (422
    /// <c>condicion-invalida</c>), con prioridad obligatoria (<c>prioridad-requerida</c>) entre 1 y
    /// <see cref="PrioridadMaxima"/> (<c>prioridad-invalida</c>); una prioridad sin condición sería un flujo que siempre
    /// gana y se rechaza (<c>seleccion-sin-condicion</c>). Sin nada que seleccionar devuelve nulo.
    /// </summary>
    public static SeleccionDeFlujo? Validar(SeleccionDeFlujo? seleccion, TipoVinculo? tipoDelFlujo)
    {
        if (seleccion is null)
            return null;

        var condicion = AtributosDeExigencia.Validar(seleccion.Condicion, tipoDelFlujo, "la selección del flujo");
        if (condicion is null)
            return seleccion.Prioridad is null
                ? null
                : throw ReglaDeNegocioException.Validacion("seleccion-sin-condicion",
                    "La prioridad solo se usa con una condición de selección: sin condición el flujo se escogería siempre.");

        return seleccion.Prioridad switch
        {
            null => throw ReglaDeNegocioException.Validacion("prioridad-requerida",
                "Un flujo con condición de selección necesita su prioridad."),
            < 1 or > PrioridadMaxima => throw ReglaDeNegocioException.Validacion("prioridad-invalida",
                $"La prioridad va de 1 a {PrioridadMaxima}."),
            var prioridad => new SeleccionDeFlujo(condicion, prioridad)
        };
    }
}

/// <summary>Una regla de selección con su resultado para un tercero y un vínculo: el flujo (su versión publicada), su prioridad y si cumple.</summary>
public sealed record ReglaEvaluada(Flujo Flujo, FlujoVersion Version, int Prioridad, CondicionDeExigencia Condicion, bool Cumple);

/// <summary>
/// Resultado de elegir el flujo: el escogido con su versión publicada y cómo se escogió, o ninguno si no hay regla que
/// aplique ni flujo por defecto publicado. <see cref="Reglas"/> son todas las reglas en orden de evaluación (para explicar la
/// elección); <see cref="Regla"/> es lo que se registra en el expediente cuando decidió una regla.
/// </summary>
public sealed record SeleccionResuelta(Flujo? Flujo, FlujoVersion? Version, OrigenFlujo Origen, SeleccionPorRegla? Regla, IReadOnlyList<ReglaEvaluada> Reglas);

/// <summary>
/// Elige el flujo de un vínculo cuando nadie indicó uno (FR-1, docs/03 §12.1): función pura, sin estado. Los flujos publicados
/// del tipo de vínculo con condición de selección se evalúan por prioridad (la menor primero; a igual prioridad, por código, que
/// la validación al publicar evita); el primero que cumple gana (<see cref="OrigenFlujo.Regla"/>). Si ninguno cumple, el flujo por
/// defecto (<see cref="OrigenFlujo.PorDefecto"/>). Un flujo explícito no pasa por aquí: gana siempre.
/// </summary>
public static class EvaluadorDeSeleccion
{
    /// <summary>
    /// <paramref name="flujosDelTipo"/>: los flujos de la empresa para el tipo del vínculo (los que tengan otro tipo o no estén
    /// publicados no cuentan). <paramref name="porDefecto"/>: el flujo por defecto de <c>(empresa, tipo)</c>, si lo hay.
    /// </summary>
    public static SeleccionResuelta Seleccionar(IEnumerable<Flujo> flujosDelTipo, Flujo? porDefecto, ContextoDeExigencia contexto)
    {
        var reglas = Reglas(flujosDelTipo)
            .Select(r => r with { Cumple = EvaluadorDeExigencia.Aplica(r.Condicion, contexto) })
            .ToList();

        var indice = reglas.FindIndex(r => r.Cumple);
        if (indice >= 0)
        {
            var ganadora = reglas[indice];
            return new SeleccionResuelta(ganadora.Flujo, ganadora.Version, OrigenFlujo.Regla,
                new SeleccionPorRegla(ganadora.Prioridad, ganadora.Condicion, indice + 1), reglas);
        }

        return SoloPorDefecto(porDefecto, reglas);
    }

    /// <summary>
    /// El flujo por defecto sin evaluar reglas (el tipo de vínculo no tiene ninguna). Uno sin versión publicada no sirve: no hay nada
    /// que abrir y el llamador responde <c>falta-flujo-por-defecto</c>.
    /// </summary>
    public static SeleccionResuelta SoloPorDefecto(Flujo? porDefecto, IReadOnlyList<ReglaEvaluada>? reglas = null)
    {
        var elegido = porDefecto?.VersionPublicada is null ? null : porDefecto;
        return new SeleccionResuelta(elegido, elegido?.VersionPublicada, OrigenFlujo.PorDefecto, null, reglas ?? []);
    }

    /// <summary>Reglas vigentes (sin evaluar: <see cref="ReglaEvaluada.Cumple"/> en falso) en orden de evaluación.</summary>
    public static IReadOnlyList<ReglaEvaluada> Reglas(IEnumerable<Flujo> flujosDelTipo) =>
    [
        .. flujosDelTipo
            .Select(f => (Flujo: f, Version: f.VersionPublicada))
            .Where(x => x.Version?.Seleccion is { Participa: true })
            .Select(x => new ReglaEvaluada(x.Flujo, x.Version!, x.Version!.Seleccion!.Prioridad!.Value, x.Version.Seleccion.Condicion!, false))
            .OrderBy(r => r.Prioridad)
            .ThenBy(r => r.Flujo.Codigo, StringComparer.Ordinal)
    ];

    /// <summary>
    /// Una prioridad no se repite entre los flujos publicados del mismo tipo de vínculo de la empresa (422
    /// <c>prioridad-duplicada</c>, con el código del flujo que ya la usa en <c>flujo</c>): el orden de evaluación debe ser
    /// inequívoco. <paramref name="flujo"/> es el que se guarda o se publica y no se compara consigo mismo.
    /// </summary>
    public static void ValidarPrioridadUnica(Flujo flujo, SeleccionDeFlujo? seleccion, IEnumerable<Flujo> flujosDelTipo)
    {
        if (seleccion is not { Participa: true, Prioridad: { } prioridad })
            return;

        var otro = flujosDelTipo.FirstOrDefault(f => f.Id != flujo.Id && f.EmpresaId == flujo.EmpresaId && f.TipoVinculoId == flujo.TipoVinculoId &&
                                                    f.VersionPublicada?.Seleccion is { Participa: true, Prioridad: { } usada } && usada == prioridad);
        if (otro is null)
            return;

        throw new ReglaDeNegocioException(TipoFalla.Validacion, "prioridad-duplicada",
            $"El flujo {otro.Codigo} ya usa la prioridad {prioridad} para este tipo de vínculo: cada flujo necesita la suya.")
        {
            Datos = new Dictionary<string, object?> { ["flujo"] = otro.Codigo, ["prioridad"] = prioridad }
        };
    }
}
