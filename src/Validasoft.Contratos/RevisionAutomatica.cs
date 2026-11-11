namespace Validasoft.Contratos;

// DTOs de la revisión automática con IA (ADR-0012): configuración por empresa, resumen por documento en el expediente y bandeja
// de excepciones. Enums como texto.

/// <summary>Una razón (código estable y texto en español) por la que el sistema no aprueba solo.</summary>
public sealed record MotivoDeRevisionDto(string Codigo, string Texto);

/// <summary>
/// Lo que la revisión automática registró de un documento para un área y la versión vigente. <see cref="Nivel"/>:
/// <c>Manual|Asistido|Automatico</c>. <see cref="Resultado"/>: <c>Sugerida|AprobadaAutomaticamente|AprobadaConMuestreo|Excepcion|SinAlcance</c>.
/// <see cref="Sugerencia"/> (solo Asistido): <c>Aprobar|Revisar</c>. <see cref="PendienteDeAtencion"/>: una persona debe decidir (excepción)
/// o confirmar (muestreo). <see cref="Resolucion"/>: <c>Confirmada|Revertida</c> si una persona ya resolvió el muestreo.
/// </summary>
public sealed record RevisionAutomaticaDto(
    Guid AreaId, string AreaCodigo, int SobreVersion, string Nivel, string Resultado, string? Sugerencia, IReadOnlyList<MotivoDeRevisionDto> Motivos,
    decimal? Confianza, DateTimeOffset Fecha, bool PendienteDeAtencion, string? Resolucion, string? ResueltaPor, DateTimeOffset? ResueltaEn,
    string? JustificacionResolucion);

public sealed record UsoRevisionAutomaticaDto(string Mes, int Aprobadas, int Muestreadas, int Excepciones, int Sugerencias, int? Tope);

/// <summary>Tipo de documento que el sistema puede analizar con IA (y, por tanto, aprobar solo).</summary>
public sealed record TipoAnalizableDto(string Codigo, string Nombre);

public sealed record AreaDisponibleDto(Guid Id, string Codigo, string Nombre);

/// <summary>
/// Revisión automática de la empresa activa. <see cref="IaDisponible"/>: el plan de IA está activo y con llave (necesario para el nivel
/// Automático). <see cref="UmbralDelPlan"/>: el umbral del plan de IA, que el de la empresa nunca baja. <see cref="Configurada"/>: ya hay
/// una configuración guardada (si no, es Manual por omisión).
/// </summary>
public sealed record RevisionAutomaticaConfigDto(
    bool Configurada, string Nivel, decimal UmbralConfianza, int PorcentajeMuestreo, int? TopeMensualAprobaciones, IReadOnlyList<string> TiposAutorizados,
    IReadOnlyList<Guid> AreasAutorizadas, int EsperaIaMinutos, DateTimeOffset? ActualizadaEn, bool IaDisponible, decimal UmbralDelPlan,
    UsoRevisionAutomaticaDto Uso, IReadOnlyList<TipoAnalizableDto> TiposAnalizables, IReadOnlyList<AreaDisponibleDto> Areas);

/// <summary>
/// Reemplaza la configuración. <see cref="Nivel"/>: <c>Manual|Asistido|Automatico</c>. Lo nulo toma el valor por omisión (umbral 0,95,
/// muestreo 10 %, espera 10 min; tope nulo = sin tope). Automático exige al menos un tipo y un área.
/// </summary>
public sealed record ActualizarRevisionAutomaticaDto(
    string Nivel, decimal? UmbralConfianza = null, int? PorcentajeMuestreo = null, int? TopeMensualAprobaciones = null,
    IReadOnlyList<string>? TiposAutorizados = null, IReadOnlyList<Guid>? AreasAutorizadas = null, int? EsperaIaMinutos = null);

/// <summary>
/// Una fila de la bandeja de excepciones: un documento que el sistema no aprobó solo (<see cref="Tipo"/> <c>Excepcion</c>) o una aprobación
/// automática que una persona debe confirmar por muestreo (<c>Muestreo</c>). <see cref="Resuelta"/>: ya no espera a nadie.
/// </summary>
public sealed record ExcepcionDeRevisionDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, TerceroResumenDto Tercero, string TipoVinculo, string EstadoExpediente, Guid RequisitoId,
    string Requisito, string? TipoCanonico, int Version, Guid AreaId, string AreaCodigo, string Tipo, string Resultado,
    IReadOnlyList<MotivoDeRevisionDto> Motivos, decimal? Confianza, DateTimeOffset Desde, bool Resuelta, string? Resolucion, string? ResueltaPor,
    DateTimeOffset? ResueltaEn);

/// <summary><see cref="Decision"/>: <c>Confirmar|Revertir</c>; la justificación es obligatoria al revertir.</summary>
public sealed record ResolverRevisionAutomaticaDto(Guid AreaId, string Decision, string? Justificacion = null);
