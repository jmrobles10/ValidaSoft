using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Vinculos;

// Exención del vínculo (EX-1…): la empresa decide no perseguir la renovación de un tercero ya aprobado. Hechos del stream
// Vinculo-{id}, privados de la empresa (el stream es del tenant): nada de esto viaja a la bóveda ni a otra empresa. Quién la
// registró o revocó viaja en el cuerpo (para la pantalla) y, como siempre, en los metadatos del evento.

/// <summary>Cómo está una exención: <c>Vigente</c>, <c>Revocada</c> por una persona o <c>Vencida</c> al pasar su fecha.</summary>
public enum EstadoDeExencion
{
    Vigente,
    Revocada,
    Vencida
}

/// <summary>
/// Lo que cubre una exención: un tipo de documento del tenant (<see cref="TipoDocumentoTenantId"/>) o una sección de la ficha
/// (<see cref="Seccion"/>, código <c>FICHA_*</c>). <see cref="Codigo"/> y <see cref="Nombre"/> son lo que decían al eximir (para la pantalla y
/// para casar con el contenido de un certificado, que nombra los requisitos por código).
/// </summary>
public sealed record ObjetivoDeExencion(TipoDeRequisito Tipo, Guid? TipoDocumentoTenantId, string? Seccion, string Codigo, string Nombre)
{
    public static ObjetivoDeExencion DeDocumento(Guid tipoDocumentoTenantId, string codigo, string nombre) =>
        new(TipoDeRequisito.Documento, tipoDocumentoTenantId, null, codigo, nombre);

    public static ObjetivoDeExencion DeSeccion(string seccion, string nombre) =>
        new(TipoDeRequisito.Seccion, null, seccion.Trim().ToUpperInvariant(), seccion.Trim().ToUpperInvariant(), nombre);

    /// <summary>Identifica el objetivo sin importar cómo se llamaba: dos objetivos con la misma clave son el mismo.</summary>
    public string Clave => Tipo == TipoDeRequisito.Documento ? $"D|{TipoDocumentoTenantId:N}" : $"S|{Seccion}";

    /// <summary>¿Es el de este requisito? Un documento por el tipo del tenant; una sección por su código.</summary>
    public bool Cubre(TipoDeRequisito tipo, Guid? tipoDocumentoTenantId, string? seccion) =>
        Tipo == tipo && (tipo == TipoDeRequisito.Documento
            ? tipoDocumentoTenantId is { } id && id == TipoDocumentoTenantId
            : string.Equals(Seccion, seccion?.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// La empresa eximió al vínculo de <see cref="Objetivos"/> (o de <see cref="Todos"/> los requisitos y de la vigencia periódica) con
/// <see cref="Justificacion"/>, hasta <see cref="Hasta"/> (inclusive; nula = hasta que se revoque).
/// </summary>
public sealed record ExencionDeVinculoRegistrada(
    Guid ExencionId,
    bool Todos,
    IReadOnlyList<ObjetivoDeExencion> Objetivos,
    string Justificacion,
    DateOnly? Hasta,
    string Por,
    DateTimeOffset Fecha);

/// <summary>Una persona revocó la exención con <see cref="Justificacion"/>: desde ahora el vínculo vuelve a regirse por las vigencias.</summary>
public sealed record ExencionDeVinculoRevocada(Guid ExencionId, string Justificacion, string Por, DateTimeOffset Fecha);

/// <summary>Pasó la fecha <see cref="Hasta"/> de la exención (lo emite el job de vigencias): se recalcula el estado del vínculo.</summary>
public sealed record ExencionDeVinculoVencida(Guid ExencionId, DateOnly Hasta, DateTimeOffset Fecha);

/// <summary>
/// El vínculo estaba <c>PorVencer</c> o <c>Vencido</c> sin expediente abierto y una exención total lo deja al día: vuelve a <c>Activo</c>
/// (no se renueva nada que pedirle al tercero). Con un expediente abierto el cierre del expediente es el camino de vuelta.
/// </summary>
public sealed record VinculoRestablecidoPorExencion(EstadoVinculo EstadoPrevio, Guid ExencionId, DateTimeOffset Fecha);
