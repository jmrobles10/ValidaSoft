using System.Globalization;
using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Flujos;

/// <summary>Nodo del árbol de una condición (docs/03 §12): combina otros nodos o compara un atributo.</summary>
public enum TipoNodoCondicion
{
    Y,
    O,
    No,
    Predicado
}

public enum OperadorCondicion
{
    Igual,
    Distinto,
    En,
    NoEn,
    ContieneAlguno,
    ContieneTodos,
    Existe,
    Mayor,
    Menor,
    Entre
}

/// <summary>
/// «Se pide cuando» de un requisito (RQ-12). <see cref="TipoNodoCondicion.Y"/>/<see cref="TipoNodoCondicion.O"/> combinan
/// <see cref="Nodos"/>; <see cref="TipoNodoCondicion.No"/> niega su único nodo; <see cref="TipoNodoCondicion.Predicado"/>
/// compara <see cref="Atributo"/> con <see cref="Valores"/> (texto invariante: números con punto, fechas
/// <c>yyyy-MM-dd</c>, booleanos <c>true</c>/<c>false</c>). Sin condición el requisito se pide siempre.
/// </summary>
public sealed record CondicionDeExigencia(
    TipoNodoCondicion Tipo,
    IReadOnlyList<CondicionDeExigencia>? Nodos = null,
    string? Atributo = null,
    OperadorCondicion? Operador = null,
    IReadOnlyList<string>? Valores = null)
{
    public static CondicionDeExigencia Predicado(string atributo, OperadorCondicion operador, params string[] valores) =>
        new(TipoNodoCondicion.Predicado, null, atributo, operador, valores);

    public static CondicionDeExigencia Todas(params CondicionDeExigencia[] nodos) => new(TipoNodoCondicion.Y, nodos);

    public static CondicionDeExigencia Alguna(params CondicionDeExigencia[] nodos) => new(TipoNodoCondicion.O, nodos);

    public static CondicionDeExigencia Negar(CondicionDeExigencia nodo) => new(TipoNodoCondicion.No, [nodo]);
}

/// <summary>
/// Lo que se sabe del tercero y del vínculo al decidir qué documentos se le piden. <see cref="TipoVinculo"/> es el código
/// del tipo del vínculo; <see cref="Atributos"/>, los del vínculo (los valida su tipo).
/// </summary>
public sealed record ContextoDeExigencia(
    TipoPersona TipoPersona,
    string Pais,
    string TipoIdentificacion,
    bool EsExtranjero,
    string TipoVinculo,
    IReadOnlyDictionary<string, JsonElement> Atributos)
{
    public static ContextoDeExigencia De(Tercero tercero, string tipoVinculo, IReadOnlyDictionary<string, JsonElement>? atributos) =>
        new(tercero.TipoPersona, tercero.Identificacion.Pais, tercero.Identificacion.Tipo, tercero.EsExtranjero, tipoVinculo,
            atributos ?? new Dictionary<string, JsonElement>());
}

/// <summary>
/// Catálogo de atributos que puede mirar una condición y validación de las condiciones al guardar el flujo (422
/// <c>condicion-invalida</c>). Los del tercero valen para cualquier flujo; <c>vinculo.atributos.{clave}</c> solo si el
/// tipo de vínculo del flujo admite esa clave, y con su tipo.
/// </summary>
public static class AtributosDeExigencia
{
    public const string TipoPersona = "tercero.tipoPersona";
    public const string Pais = "tercero.pais";
    public const string TipoIdentificacion = "tercero.tipoIdentificacion";
    public const string EsExtranjero = "tercero.esExtranjero";
    public const string TipoVinculo = "vinculo.tipo";
    public const string PrefijoAtributos = "vinculo.atributos.";

    public const int ProfundidadMaxima = 4;
    public const int PredicadosMaximos = 20;

    private static readonly string[] TiposDePersona = Enum.GetNames<TipoPersona>();

    /// <summary>Valida y normaliza (valores sin espacios; país y códigos en mayúsculas; tipo de persona con su nombre exacto).</summary>
    public static CondicionDeExigencia? Validar(CondicionDeExigencia? condicion, TipoVinculo? tipoDelFlujo, string documento)
    {
        if (condicion is null || (condicion.Tipo is TipoNodoCondicion.Y && (condicion.Nodos?.Count ?? 0) == 0))
            return null;

        var predicados = 0;
        var normalizada = Validar(condicion, tipoDelFlujo, documento, 1, ref predicados);
        if (predicados > PredicadosMaximos)
            throw Invalida(documento, $"admite hasta {PredicadosMaximos} comparaciones.");
        return normalizada;
    }

    private static CondicionDeExigencia Validar(CondicionDeExigencia nodo, TipoVinculo? tipo, string documento, int profundidad, ref int predicados)
    {
        if (profundidad > ProfundidadMaxima)
            throw Invalida(documento, $"admite hasta {ProfundidadMaxima} niveles de agrupación.");

        switch (nodo.Tipo)
        {
            case TipoNodoCondicion.Y or TipoNodoCondicion.O:
            {
                if (nodo.Nodos is not { Count: > 0 } hijos)
                    throw Invalida(documento, "un grupo «Y»/«O» necesita al menos una condición.");
                var normalizados = new List<CondicionDeExigencia>();
                foreach (var hijo in hijos)
                    normalizados.Add(Validar(hijo, tipo, documento, profundidad + 1, ref predicados));
                return new CondicionDeExigencia(nodo.Tipo, normalizados);
            }
            case TipoNodoCondicion.No:
                if (nodo.Nodos is not { Count: 1 } unico)
                    throw Invalida(documento, "«No» niega exactamente una condición.");
                return new CondicionDeExigencia(TipoNodoCondicion.No, [Validar(unico[0], tipo, documento, profundidad + 1, ref predicados)]);
            case TipoNodoCondicion.Predicado:
                predicados++;
                return ValidarPredicado(nodo, tipo, documento);
            default:
                throw Invalida(documento, "tiene un nodo desconocido.");
        }
    }

    private static CondicionDeExigencia ValidarPredicado(CondicionDeExigencia nodo, TipoVinculo? tipo, string documento)
    {
        var atributo = (nodo.Atributo ?? string.Empty).Trim();
        var clase = ClaseDe(atributo, tipo, documento);
        if (nodo.Operador is not { } operador)
            throw Invalida(documento, $"falta el operador de «{atributo}».");
        if (!clase.Operadores.Contains(operador))
            throw Invalida(documento, $"«{atributo}» no admite el operador {operador}.");

        var valores = (nodo.Valores ?? []).Select(v => (v ?? string.Empty).Trim()).ToList();
        var esperados = operador switch
        {
            OperadorCondicion.Existe => (0, 0),
            OperadorCondicion.Entre => (2, 2),
            OperadorCondicion.En or OperadorCondicion.NoEn or OperadorCondicion.ContieneAlguno or OperadorCondicion.ContieneTodos => (1, 50),
            _ => (1, 1)
        };
        if (valores.Count < esperados.Item1 || valores.Count > esperados.Item2)
            throw Invalida(documento, operador == OperadorCondicion.Existe
                ? $"«{atributo}» existe no lleva valores."
                : $"«{atributo}» {operador} necesita {(esperados.Item1 == esperados.Item2 ? esperados.Item1.ToString(CultureInfo.InvariantCulture) : "al menos un")} valor(es).");

        var normalizados = valores.Select(v => clase.Normalizar(v) ?? throw Invalida(documento, $"«{v}» no es un valor válido para «{atributo}».")).ToList();
        if (operador == OperadorCondicion.Entre && Comparar(clase.Tipo, normalizados[0], normalizados[1]) > 0)
            throw Invalida(documento, $"en «{atributo}» entre, el primer valor debe ser el menor.");

        return CondicionDeExigencia.Predicado(atributo, operador, [.. normalizados.Distinct(StringComparer.Ordinal)]);
    }

    /// <summary>Clase de valor de un atributo: su tipo, los operadores que admite y cómo normaliza un valor (nulo ⇒ inválido).</summary>
    private sealed record Clase(TipoAtributo Tipo, IReadOnlySet<OperadorCondicion> Operadores, Func<string, string?> Normalizar);

    private static readonly HashSet<OperadorCondicion> DeTexto = [OperadorCondicion.Igual, OperadorCondicion.Distinto, OperadorCondicion.En, OperadorCondicion.NoEn];
    private static readonly HashSet<OperadorCondicion> DeBooleano = [OperadorCondicion.Igual, OperadorCondicion.Distinto];
    private static readonly HashSet<OperadorCondicion> DeOrden =
        [OperadorCondicion.Igual, OperadorCondicion.Distinto, OperadorCondicion.Mayor, OperadorCondicion.Menor, OperadorCondicion.Entre];
    private static readonly HashSet<OperadorCondicion> DeLista = [OperadorCondicion.ContieneAlguno, OperadorCondicion.ContieneTodos];

    private static Clase ClaseDe(string atributo, TipoVinculo? tipo, string documento)
    {
        switch (atributo)
        {
            case TipoPersona:
                return new Clase(TipoAtributo.Opcion, DeTexto, v => TiposDePersona.FirstOrDefault(t => string.Equals(t, v, StringComparison.OrdinalIgnoreCase)));
            case Pais or TipoIdentificacion or TipoVinculo:
                return new Clase(TipoAtributo.Texto, DeTexto, v => v.Length == 0 ? null : v.ToUpperInvariant());
            case EsExtranjero:
                return new Clase(TipoAtributo.Booleano, DeBooleano, NormalizarBooleano);
        }

        if (!atributo.StartsWith(PrefijoAtributos, StringComparison.Ordinal))
            throw Invalida(documento, $"«{atributo}» no es un atributo que se pueda usar en una condición.");

        var clave = atributo[PrefijoAtributos.Length..];
        var permitido = tipo?.AtributosPermitidos.FirstOrDefault(a => a.Clave == clave)
                        ?? throw Invalida(documento, $"el tipo de vínculo {tipo?.Codigo ?? "del flujo"} no tiene el atributo «{clave}».");
        var conExiste = (HashSet<OperadorCondicion> baseOps) => new HashSet<OperadorCondicion>(baseOps) { OperadorCondicion.Existe };
        var opciones = permitido.Opciones ?? [];
        return permitido.Tipo switch
        {
            TipoAtributo.Texto => new Clase(TipoAtributo.Texto, conExiste(DeTexto), v => v.Length == 0 ? null : v),
            TipoAtributo.Opcion => new Clase(TipoAtributo.Opcion, conExiste(DeTexto), v => opciones.FirstOrDefault(o => o == v)),
            TipoAtributo.ListaOpciones => new Clase(TipoAtributo.ListaOpciones, conExiste(DeLista), v => opciones.FirstOrDefault(o => o == v)),
            TipoAtributo.Booleano => new Clase(TipoAtributo.Booleano, conExiste(DeBooleano), NormalizarBooleano),
            TipoAtributo.Numero => new Clase(TipoAtributo.Numero, conExiste(DeOrden),
                v => decimal.TryParse(v, NumberStyles.Number & ~NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var n)
                    ? n.ToString(CultureInfo.InvariantCulture) : null),
            TipoAtributo.Fecha => new Clase(TipoAtributo.Fecha, conExiste(DeOrden),
                v => DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f)
                    ? f.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null),
            _ => throw Invalida(documento, $"«{clave}» tiene un tipo que no se puede comparar.")
        };
    }

    private static string? NormalizarBooleano(string v) =>
        bool.TryParse(v, out var b) ? (b ? "true" : "false") : null;

    /// <summary>Compara dos valores ya normalizados de un atributo ordenable (número o fecha).</summary>
    internal static int Comparar(TipoAtributo tipo, string a, string b) => tipo == TipoAtributo.Numero
        ? decimal.Parse(a, CultureInfo.InvariantCulture).CompareTo(decimal.Parse(b, CultureInfo.InvariantCulture))
        : string.CompareOrdinal(a, b); // yyyy-MM-dd ordena como texto

    private static ReglaDeNegocioException Invalida(string documento, string detalle) =>
        ReglaDeNegocioException.Validacion("condicion-invalida", $"La condición de {documento} {detalle}");
}

/// <summary>
/// Qué requisitos del flujo se le piden a este tercero en este vínculo (docs/03 §12): función pura, sin estado. Un
/// atributo del vínculo sin valor solo cumple <c>Distinto</c>, <c>NoEn</c> y la negación de <c>Existe</c>: ante la duda
/// sobre un dato faltante, «se pide si es X» no pide y «se pide salvo que sea X» sí pide.
/// </summary>
public static class EvaluadorDeExigencia
{
    public static IReadOnlyList<RequisitoDelFlujo> Filtrar(IEnumerable<RequisitoDelFlujo> requisitos, ContextoDeExigencia contexto) =>
        [.. requisitos.Where(r => Aplica(r.Condicion, contexto))];

    public static bool Aplica(CondicionDeExigencia? condicion, ContextoDeExigencia contexto) => condicion is null || Evaluar(condicion, contexto);

    private static bool Evaluar(CondicionDeExigencia nodo, ContextoDeExigencia contexto) => nodo.Tipo switch
    {
        TipoNodoCondicion.Y => (nodo.Nodos ?? []).All(n => Evaluar(n, contexto)),
        TipoNodoCondicion.O => (nodo.Nodos ?? []).Any(n => Evaluar(n, contexto)),
        TipoNodoCondicion.No => !(nodo.Nodos is { Count: 1 } unico && Evaluar(unico[0], contexto)),
        TipoNodoCondicion.Predicado => Predicado(nodo, contexto),
        _ => false
    };

    private static bool Predicado(CondicionDeExigencia nodo, ContextoDeExigencia contexto)
    {
        var valores = nodo.Valores ?? [];
        var operador = nodo.Operador ?? OperadorCondicion.Igual;
        var (presente, actual) = Leer(nodo.Atributo ?? string.Empty, contexto);
        if (!presente)
            return operador is OperadorCondicion.Distinto or OperadorCondicion.NoEn;
        if (operador == OperadorCondicion.Existe)
            return true;

        var lista = actual.Lista;
        var escalar = actual.Escalar;
        return operador switch
        {
            OperadorCondicion.Igual => escalar is not null && Igual(actual, escalar, valores[0]),
            OperadorCondicion.Distinto => escalar is null || !Igual(actual, escalar, valores[0]),
            OperadorCondicion.En => escalar is not null && valores.Any(v => Igual(actual, escalar, v)),
            OperadorCondicion.NoEn => escalar is null || !valores.Any(v => Igual(actual, escalar, v)),
            OperadorCondicion.ContieneAlguno => lista is not null && valores.Any(lista.Contains),
            OperadorCondicion.ContieneTodos => lista is not null && valores.All(lista.Contains),
            OperadorCondicion.Mayor => actual.Ordenable is { } m && AtributosDeExigencia.Comparar(m, escalar!, valores[0]) > 0,
            OperadorCondicion.Menor => actual.Ordenable is { } n && AtributosDeExigencia.Comparar(n, escalar!, valores[0]) < 0,
            OperadorCondicion.Entre => actual.Ordenable is { } e && AtributosDeExigencia.Comparar(e, escalar!, valores[0]) >= 0 &&
                                       AtributosDeExigencia.Comparar(e, escalar!, valores[1]) <= 0,
            _ => false
        };
    }

    /// <summary>Valor leído del contexto: texto escalar (normalizado como en la validación), lista o nada.</summary>
    private sealed record Valor(string? Escalar, IReadOnlyList<string>? Lista, TipoAtributo? Ordenable);

    /// <summary>Números por valor (<c>1.0</c> = <c>1</c>); lo demás, texto exacto (ya normalizado al validar).</summary>
    private static bool Igual(Valor actual, string escalar, string esperado) => actual.Ordenable == TipoAtributo.Numero
        ? AtributosDeExigencia.Comparar(TipoAtributo.Numero, escalar, esperado) == 0
        : string.Equals(escalar, esperado, StringComparison.Ordinal);

    private static (bool Presente, Valor Valor) Leer(string atributo, ContextoDeExigencia c)
    {
        switch (atributo)
        {
            case AtributosDeExigencia.TipoPersona:
                return (true, new Valor(c.TipoPersona.ToString(), null, null));
            case AtributosDeExigencia.Pais:
                return (true, new Valor(c.Pais.ToUpperInvariant(), null, null));
            case AtributosDeExigencia.TipoIdentificacion:
                return (true, new Valor(c.TipoIdentificacion.ToUpperInvariant(), null, null));
            case AtributosDeExigencia.TipoVinculo:
                return (true, new Valor(c.TipoVinculo.ToUpperInvariant(), null, null));
            case AtributosDeExigencia.EsExtranjero:
                return (true, new Valor(c.EsExtranjero ? "true" : "false", null, null));
        }

        if (!atributo.StartsWith(AtributosDeExigencia.PrefijoAtributos, StringComparison.Ordinal) ||
            !c.Atributos.TryGetValue(atributo[AtributosDeExigencia.PrefijoAtributos.Length..], out var json))
            return (false, new Valor(null, null, null));

        return json.ValueKind switch
        {
            JsonValueKind.String when DateOnly.TryParseExact(json.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) =>
                (true, new Valor(json.GetString(), null, TipoAtributo.Fecha)),
            JsonValueKind.String => (true, new Valor(json.GetString(), null, null)),
            JsonValueKind.Number when json.TryGetDecimal(out var n) => (true, new Valor(n.ToString(CultureInfo.InvariantCulture), null, TipoAtributo.Numero)),
            JsonValueKind.True => (true, new Valor("true", null, null)),
            JsonValueKind.False => (true, new Valor("false", null, null)),
            JsonValueKind.Array => (true, new Valor(null, [.. json.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)], null)),
            _ => (false, new Valor(null, null, null))
        };
    }
}
