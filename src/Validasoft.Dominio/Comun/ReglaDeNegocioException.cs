namespace Validasoft.Dominio.Comun;

/// <summary>Naturaleza de una falla de negocio; la capa HTTP la traduce a un código de estado.</summary>
public enum TipoFalla
{
    Validacion,
    NoEncontrado,
    Conflicto,
    Prohibido,
    NoAutenticado,

    /// <summary>El recurso existió pero ya no se puede usar: enlace usado o vencido (410).</summary>
    Expirado,

    /// <summary>Se agotaron los intentos permitidos (429).</summary>
    DemasiadosIntentos
}

/// <summary>
/// Violación de una regla del dominio. El <see cref="Codigo"/> es estable y viaja al cliente
/// como <c>ProblemDetails.codigo</c> (docs/05 §3.3).
/// </summary>
public sealed class ReglaDeNegocioException(TipoFalla tipo, string codigo, string mensaje)
    : Exception(mensaje)
{
    public TipoFalla Tipo { get; } = tipo;

    public string Codigo { get; } = codigo;

    /// <summary>Datos adicionales que viajan como extensiones del ProblemDetails (p. ej. <c>intentosRestantes</c>).</summary>
    public IReadOnlyDictionary<string, object?>? Datos { get; init; }

    public static ReglaDeNegocioException Validacion(string codigo, string mensaje) => new(TipoFalla.Validacion, codigo, mensaje);

    public static ReglaDeNegocioException NoEncontrado(string codigo, string mensaje) => new(TipoFalla.NoEncontrado, codigo, mensaje);

    public static ReglaDeNegocioException Conflicto(string codigo, string mensaje) => new(TipoFalla.Conflicto, codigo, mensaje);

    public static ReglaDeNegocioException Prohibido(string codigo, string mensaje) => new(TipoFalla.Prohibido, codigo, mensaje);

    /// <summary>La credencial o la identidad delegada no se pudo verificar (401).</summary>
    public static ReglaDeNegocioException NoAutenticado(string codigo, string mensaje) => new(TipoFalla.NoAutenticado, codigo, mensaje);

    public static ReglaDeNegocioException Expirado(string codigo, string mensaje) => new(TipoFalla.Expirado, codigo, mensaje);

    public static ReglaDeNegocioException DemasiadosIntentos(string codigo, string mensaje) => new(TipoFalla.DemasiadosIntentos, codigo, mensaje);
}

/// <summary>Quién origina un cambio (docs/03 §3).</summary>
public enum ActorTipo
{
    Usuario,
    Tercero,
    Integracion,
    Sistema,
    Plataforma,
    Migracion
}
