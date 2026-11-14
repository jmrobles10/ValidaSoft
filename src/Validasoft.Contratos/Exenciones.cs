namespace Validasoft.Contratos;

// ---------- Exención del vínculo (EX-1…EX-12): privada de la empresa, por vínculo ----------

/// <summary>
/// De qué se exime al vínculo: un tipo de documento del tenant (<see cref="Tipo"/> <c>Documento</c>, con <see cref="TipoDocumentoTenantId"/>) o una sección de la ficha
/// (<c>Seccion</c>, con <see cref="Seccion"/>, código <c>FICHA_*</c>). <see cref="Codigo"/> y <see cref="Nombre"/> son de solo lectura: en la petición basta el identificador.
/// </summary>
public sealed record ObjetivoDeExencionDto(string Tipo, Guid? TipoDocumentoTenantId = null, string? Seccion = null, string? Codigo = null, string? Nombre = null);

/// <summary>
/// Eximir al vínculo (<c>POST /vinculos/{id}/exenciones</c>, <c>vinculos:eximir</c>): de <see cref="Todos"/> los requisitos y de la vigencia periódica o de
/// <see cref="Objetivos"/> concretos (no las dos cosas), con <see cref="Justificacion"/> obligatoria y <see cref="Hasta"/> opcional (último día, inclusive; sin él, hasta
/// que se revoque).
/// </summary>
public sealed record EximirVinculoDto(bool Todos, IReadOnlyList<ObjetivoDeExencionDto>? Objetivos, string Justificacion, DateOnly? Hasta);

/// <summary>
/// Una exención del vínculo. <see cref="Estado"/> a hoy: <c>Vigente</c>, <c>Revocada</c> (con quién y por qué) o <c>Vencida</c> (pasó su fecha). Es privada de la
/// empresa: no sale en ninguna vista del tercero ni de otra empresa, ni su justificación en el certificado.
/// </summary>
public sealed record ExencionDeVinculoDto(
    Guid Id,
    bool Todos,
    IReadOnlyList<ObjetivoDeExencionDto> Objetivos,
    string Justificacion,
    DateOnly? Hasta,
    string Por,
    DateTimeOffset CreadaEn,
    string Estado,
    string? RevocadaPor = null,
    string? JustificacionDeRevocacion = null,
    DateTimeOffset? RevocadaEn = null,
    DateTimeOffset? VencidaEn = null);

/// <summary>Resumen de la exención vigente que cubre una fila de la bandeja de vencimientos (el chip «Exento» y su fecha).</summary>
public sealed record ExencionResumenDto(Guid Id, bool Todos, string Justificacion, DateOnly? Hasta);
