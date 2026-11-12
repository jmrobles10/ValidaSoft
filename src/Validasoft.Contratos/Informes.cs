namespace Validasoft.Contratos;

// ---------- Informes (docs/05 §2.7, docs/11 §H8; IF-1…IF-6 del registro) ----------

/// <summary>Cantidad por clave (un estado, un tipo…).</summary>
public sealed record ConteoDto(string Clave, int Cantidad);

/// <summary>
/// Tiempo de un área en un expediente (IF-2): días en que tuvo el expediente <c>Pendiente</c> en revisión, sumando las
/// <see cref="Rondas"/> (cada vez que le llegó); <see cref="SlaIncumplido"/> si alguna ronda pasó de <see cref="SlaHoras"/>.
/// </summary>
public sealed record TiempoDeAreaDto(Guid AreaId, string Codigo, string Nombre, int Etapa, string Estado, double Dias, int Rondas, int? SlaHoras,
    bool SlaIncumplido);

/// <summary>
/// Una fila del informe de estado de expedientes. Días con un decimal: <see cref="DiasTotales"/> de la apertura al cierre
/// (o a hoy); se reparten en <see cref="DiasEnCarga"/> (el tercero aún cargaba), <see cref="DiasConTercero"/> (devuelto
/// por observaciones) y <see cref="DiasEnRevision"/> (en la empresa). <see cref="DiasHastaAprobacion"/> solo si se aprobó.
/// </summary>
public sealed record FilaEstadoExpedienteDto(
    Guid Id,
    string Folio,
    string Tipo,
    string Estado,
    Guid VinculoId,
    TerceroResumenDto Tercero,
    TipoVinculoResumenDto TipoVinculo,
    FlujoDeExpedienteDto? Flujo,
    DateTimeOffset AbiertoEn,
    DateTimeOffset? CerradoEn,
    int Requisitos,
    int RequisitosAprobados,
    int Devoluciones,
    double DiasTotales,
    double DiasEnCarga,
    double DiasConTercero,
    double DiasEnRevision,
    double? DiasHastaAprobacion,
    bool SlaIncumplido,
    IReadOnlyList<TiempoDeAreaDto> Areas);

/// <summary>Tiempos de un área sobre los expedientes del informe en que participó (días de revisión por expediente).</summary>
public sealed record ResumenDeAreaDto(Guid AreaId, string Codigo, string Nombre, int Expedientes, int Pendientes, double? PromedioDias, double? MedianaDias,
    double? MaximoDias, int SlaIncumplidos);

/// <summary>
/// Totales del informe sobre todas las filas que cumplen los filtros (no solo la página). Los tiempos (promedio y mediana
/// hasta la aprobación, y su reparto en carga, con el tercero y en revisión) son de los expedientes <c>Aprobado</c>: es la
/// misma medida de <c>IndicadoresDto.TiempoPromedioDias</c> en Inicio.
/// </summary>
public sealed record ResumenEstadoExpedientesDto(
    int Total,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorTipo,
    int Aprobados,
    double? TiempoPromedioDias,
    double? TiempoMedianaDias,
    double? PromedioEnCargaDias,
    double? PromedioConTerceroDias,
    double? PromedioEnRevisionDias,
    int Devoluciones,
    int ConSlaIncumplido,
    IReadOnlyList<ResumenDeAreaDto> PorArea);

public sealed record InformeEstadoExpedientesDto(ResumenEstadoExpedientesDto Resumen, IReadOnlyList<FilaEstadoExpedienteDto> Datos, string? SiguienteCursor);

/// <summary>Certificado del vínculo en el informe de terceros: la emisión actual.</summary>
public sealed record CertificadoDelInformeDto(string Folio, string Estado, bool Bloqueante, DateOnly? VigenteHasta);

/// <summary>Expediente en curso del vínculo (abierto, en revisión u observado).</summary>
public sealed record ExpedienteDelInformeDto(Guid Id, string Folio, string Tipo, string Estado);

/// <summary>Vigencia de los documentos compartidos con el vínculo (la versión actual de cada uno, para esta empresa).</summary>
public sealed record DocumentosDelInformeDto(int ConVencimiento, int Vigentes, int PorVencer, int EnGracia, int Vencidos, DateOnly? ProximoVencimiento);

/// <summary>
/// Una fila del informe de terceros y vínculos. <see cref="Documentos"/> es nulo sin <c>documentos:leer</c>.
/// <see cref="SeccionesPendientes"/>: secciones de la ficha que el expediente en curso aún no aprueba (nombres).
/// <see cref="EnRevision"/> (VR-1): el vínculo tiene una actualización abierta; <see cref="Estado"/> no cambia, pero el informe lo cuenta y
/// lo muestra como «En revisión» (igual que la lista de vínculos).
/// </summary>
public sealed record FilaInformeVinculoDto(
    Guid VinculoId,
    TerceroResumenDto Tercero,
    TipoVinculoResumenDto TipoVinculo,
    string Estado,
    DateTimeOffset CreadoEn,
    bool InvitacionAceptada,
    CertificadoDelInformeDto? Certificado,
    ExpedienteDelInformeDto? ExpedienteEnCurso,
    DocumentosDelInformeDto? Documentos,
    IReadOnlyList<string> SeccionesPendientes,
    bool EnRevision = false);

public sealed record ResumenVinculosDto(int Total, IReadOnlyList<ConteoDto> PorEstado, int ConCertificadoVigente, int ConDocumentosPorVencer,
    int ConDocumentosVencidos, int ConSeccionesPendientes);

public sealed record InformeVinculosDto(ResumenVinculosDto Resumen, IReadOnlyList<FilaInformeVinculoDto> Datos, string? SiguienteCursor);
