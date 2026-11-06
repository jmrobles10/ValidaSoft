namespace Validasoft.Contratos;

// DTOs de motivos, flujos aprobatorios, expedientes y bandeja (docs/05 §2.2, §2.4 y §4). Enums como texto.

// ---------- Motivos (docs/03 §13) ----------

/// <summary><see cref="Categoria"/>: Documento|Formulario|Contacto|Otro. <see cref="AplicaA"/>: Observacion|Rechazo|Ambos.</summary>
public sealed record CrearMotivoDto(string Codigo, string Nombre, string Categoria, string AplicaA, string TextoAlTercero);

public sealed record ActualizarMotivoDto(string Nombre, string Categoria, string AplicaA, string TextoAlTercero, bool? Activo = null);

/// <summary>
/// El <c>MotivoDto</c> de docs/05 §4 (aquí con otro nombre porque <see cref="MotivoDto"/> ya es el cuerpo de anular o
/// desvincular). <see cref="Base"/>: motivo de la plataforma (no editable).
/// </summary>
public sealed record MotivoTipificadoDto(
    Guid Id, string Codigo, string Nombre, string Categoria, string AplicaA, string TextoAlTercero, bool Base, bool Activo);

// ---------- Flujos ----------

/// <summary>Un área en una etapa. <see cref="Automatica"/>: aprueba sola al activarse la etapa.</summary>
public sealed record AreaDeEtapaDto(
    Guid AreaId, int AprobadoresRequeridos = 1, bool PuedeRechazarExpediente = false, bool Automatica = false, int? SlaHoras = null);

/// <summary>Las áreas de una etapa revisan en paralelo; las etapas van en secuencia por <see cref="Orden"/>.</summary>
public sealed record EtapaDto(int Orden, IReadOnlyList<AreaDeEtapaDto> Areas);

/// <summary>
/// Sin <see cref="Etapas"/>: una sola etapa con todas las áreas activas de la empresa en paralelo (P-4-4).
/// <see cref="PoliticaDevolucion"/>: <c>Inmediata</c> (por defecto) o <c>AlCerrarArea</c>.
/// </summary>
public sealed record CrearFlujoDto(
    string Codigo, string Nombre, string TipoVinculo, IReadOnlyList<EtapaDto>? Etapas = null, string? PoliticaDevolucion = null,
    bool SeparacionDeFunciones = false);

/// <summary>Edita el borrador (o crea uno nuevo si solo hay versión publicada). Lo que llega nulo conserva el valor de la última versión.</summary>
public sealed record EditarFlujoDto(
    string? Nombre = null, IReadOnlyList<EtapaDto>? Etapas = null, string? PoliticaDevolucion = null, bool? SeparacionDeFunciones = null);

public sealed record FlujoVersionDto(
    Guid Id, int Version, string Estado, IReadOnlyList<EtapaDto> Etapas, string PoliticaDevolucion, bool SeparacionDeFunciones,
    DateTimeOffset CreadaEn, DateTimeOffset? PublicadaEn, DateTimeOffset? RetiradaEn);

/// <summary>Flujo con su versión publicada y su borrador (si existen) y si es el flujo por defecto de su tipo de vínculo.</summary>
public sealed record FlujoDto(
    Guid Id, Guid EmpresaId, string Codigo, string Nombre, TipoVinculoResumenDto TipoVinculo, bool EsPorDefecto,
    FlujoVersionDto? Publicada, FlujoVersionDto? Borrador, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn);

/// <summary>La empresa sale de <c>X-Empresa</c>; si <see cref="EmpresaId"/> viene, debe coincidir.</summary>
public sealed record FlujoPorDefectoDto(Guid TipoVinculoId, Guid FlujoId, Guid? EmpresaId = null);

// ---------- Expedientes ----------

/// <summary><see cref="FlujoId"/> opcional: sin él, el flujo del vínculo o el por defecto de <c>(empresa, tipo)</c>.</summary>
public sealed record AbrirExpedienteDto(Guid? FlujoId = null);

public sealed record FlujoDeExpedienteDto(Guid Id, Guid VersionId, string Codigo, string Nombre, int Version, string Origen);

public sealed record TerceroResumenDto(Guid Id, string Nombre, string Identificacion);

/// <summary>
/// Veredicto de un área. <see cref="Usuario"/>: id del revisor (o del administrador); nulo si lo emitió el sistema.
/// <see cref="Superado"/>: dejó de contar porque el tercero reenvió tras la observación de esa área.
/// </summary>
public sealed record VeredictoEmitidoDto(
    Guid Id, string Veredicto, string Origen, string? Usuario, DateTimeOffset Fecha, IReadOnlyList<string> Motivos, string? Observacion,
    string? Justificacion, bool Vigente, bool Superado, bool Anulado, string? AnuladoPor);

/// <summary>
/// Área del expediente con sus aprobaciones (<see cref="Aprobaciones"/> de <see cref="AprobadoresRequeridos"/>) y los
/// miembros que aún no se pronuncian (<see cref="MiembrosPendientes"/>, vacío si el área ya se pronunció).
/// </summary>
public sealed record AreaExpedienteDto(
    Guid AreaId, string Codigo, string Nombre, string Estado, int AprobadoresRequeridos, int Aprobaciones, int AprobacionesFaltantes,
    IReadOnlyList<Guid> MiembrosPendientes, bool PuedeRechazarExpediente, bool Automatica, DateTimeOffset? PendienteDesde,
    DateTimeOffset? SlaVence, IReadOnlyList<VeredictoEmitidoDto> Veredictos);

public sealed record EtapaExpedienteDto(int Orden, string Estado, DateTimeOffset? ActivadaEn, DateTimeOffset? AprobadaEn,
    IReadOnlyList<AreaExpedienteDto> Areas);

public sealed record RechazoDto(Guid AreaId, string Usuario, IReadOnlyList<string> Motivos, string Justificacion, DateTimeOffset Fecha);

public sealed record ExpedienteDto(
    Guid Id,
    string Folio,
    Guid EmpresaId,
    Guid VinculoId,
    TerceroResumenDto Tercero,
    TipoVinculoResumenDto TipoVinculo,
    string Tipo,
    string Estado,
    FlujoDeExpedienteDto? Flujo,
    string PoliticaDevolucion,
    bool SeparacionDeFunciones,
    int? EtapaActual,
    IReadOnlyList<EtapaExpedienteDto> Etapas,
    int Reenvios,
    string? UltimaRespuesta,
    RechazoDto? Rechazo,
    string? MotivoAnulacion,
    DateTimeOffset AbiertoEn,
    DateTimeOffset? CerradoEn,
    DateTimeOffset ActualizadoEn,
    long Version);

/// <summary><see cref="Veredicto"/>: <c>Aprobado</c> u <c>Observado</c> (con al menos un motivo). El área va en la ruta.</summary>
public sealed record VeredictoDto(string Veredicto, IReadOnlyList<string>? Motivos = null, string? Observacion = null);

/// <summary>Rechazo terminal: solo un área con <c>puedeRechazarExpediente</c>; motivos y justificación obligatorios.</summary>
public sealed record RechazarExpedienteDto(Guid AreaId, IReadOnlyList<string> Motivos, string Justificacion);

public sealed record ReenviarExpedienteDto(string? Respuesta = null);

/// <summary>Sin <see cref="VeredictoId"/> se anula el veredicto vigente más reciente del área.</summary>
public sealed record AnularVeredictoDto(string Justificacion, Guid? VeredictoId = null);

/// <summary>Fila de la bandeja <c>BandejaRevision</c>: un expediente × un área.</summary>
public sealed record FilaBandejaDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo, Guid AreaId,
    string AreaCodigo, string AreaNombre, int Etapa, string EstadoArea, string EstadoExpediente, int Aprobaciones, int AprobadoresRequeridos,
    DateTimeOffset? Desde, DateTimeOffset ActualizadoEn);

// ---------- Público: el tercero ----------

public sealed record MotivoPublicoDto(string Codigo, string Texto);

public sealed record ObservacionPublicaDto(IReadOnlyList<MotivoPublicoDto> Motivos, string? Observacion, DateTimeOffset Fecha);

/// <summary>Lo que el tercero ve de su expediente: estado y observaciones vigentes, sin áreas ni revisores.</summary>
public sealed record ExpedientePublicoDto(
    Guid Id, string Folio, EmpresaPublicaDto Empresa, TipoVinculoResumenDto TipoVinculo, string Estado,
    IReadOnlyList<ObservacionPublicaDto> Observaciones, bool PuedeReenviar, int Reenvios, DateTimeOffset ActualizadoEn);
