namespace Validasoft.Contratos;

// ---------- Operación de la revisión (OP-1…): campañas, delegaciones, SLA, ayudas y avisos ----------

// ===== Campañas de renovación (OP-1…OP-6) =====

/// <summary>
/// Qué vínculos entran a la campaña: los que <b>vencen en los próximos <see cref="DiasParaVencer"/> días</b> (la vigencia del vínculo o
/// la de alguno de sus documentos, también lo que ya venció; 0 a 365), de un <see cref="TipoVinculoId"/> (opcional) y en alguno de los
/// <see cref="Estados"/> (<c>Activo</c>, <c>PorVencer</c>, <c>Vencido</c>; por omisión los tres).
/// </summary>
public sealed record FiltroDeCampaniaDto(int DiasParaVencer = 30, Guid? TipoVinculoId = null, IReadOnlyList<string>? Estados = null);

/// <summary>Crea la campaña y la empieza a procesar; <see cref="Nombre"/> opcional (por omisión «Renovación del …»).</summary>
public sealed record CrearCampaniaDto(FiltroDeCampaniaDto Filtro, string? Nombre = null);

/// <summary>
/// Un vínculo de la vista previa. <see cref="Accion"/>: <c>Abrir</c> (se abre su renovación y se le avisa al tercero), <c>Recordar</c> (ya la tiene abierta: se le
/// recuerda al tercero) u <c>Omitir</c> (con su
/// <see cref="Motivo"/>: ya tiene un expediente abierto, está bloqueado, nada por renovar…).
/// </summary>
public sealed record FilaVistaPreviaCampaniaDto(int N, Guid VinculoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo, string EstadoVinculo,
    DateOnly? VenceEn, string Accion, string? Motivo, bool ConCorreo);

/// <summary>
/// A quiénes afecta la campaña antes de confirmarla: <see cref="Abririan"/> renovaciones que se abrirían y <see cref="Recordarian"/> terceros a quienes se les
/// recordaría la que ya tienen abierta (con aviso por correo a
/// <see cref="ConCorreo"/> terceros y por la campana a todos) y <see cref="Omitidos"/> que no. <see cref="Filas"/> trae hasta 200;
/// <see cref="Truncada"/> si hay más.
/// </summary>
public sealed record VistaPreviaCampaniaDto(FiltroDeCampaniaDto Filtro, int Total, int Abririan, int Recordarian, int Omitidos, int ConCorreo, bool Truncada,
    IReadOnlyList<FilaVistaPreviaCampaniaDto> Filas);

public sealed record TotalesCampaniaDto(int Filas, int Procesadas, int Pendientes, int Exitosas, int Fallidas, int Omitidas);

/// <summary>
/// <see cref="Estado"/>: <c>Procesando</c>, <c>Pausada</c>, <c>Completada</c>, <c>CompletadaConErrores</c> o <c>Cancelada</c>.
/// <see cref="CreadaPor"/>: <c>{tipoDeActor}:{id}</c>.
/// </summary>
public sealed record CampaniaDto(Guid Id, string Nombre, FiltroDeCampaniaDto Filtro, string Estado, TotalesCampaniaDto Totales, string CreadaPor,
    DateTimeOffset CreadaEn, DateTimeOffset ActualizadaEn, DateTimeOffset? TerminadaEn, int Reintentos, long Version);

/// <summary>
/// Fila de la campaña. <see cref="Estado"/>: <c>Pendiente</c>, <c>Exitosa</c>, <c>Fallida</c>, <c>Omitida</c> o <c>Cancelada</c> (pendiente
/// de una campaña cancelada). <see cref="Accion"/> (exitosas): <c>RenovacionAbierta</c> o <c>Recordada</c>.
/// </summary>
public sealed record FilaCampaniaDto(int N, Guid VinculoId, Guid TerceroId, string Tercero, string Identificacion, string TipoVinculo, string EstadoVinculo,
    DateOnly? VenceEn, string Estado, string? Accion, string? Detalle, Guid? ExpedienteId, string? Folio, IReadOnlyList<ErrorDeFilaDto> Errores, int Intentos,
    DateTimeOffset? ProcesadaEn);

// ===== Delegación de revisión (OP-7…OP-9) =====

/// <summary>
/// Delegar la revisión: <see cref="AUsuarioId"/> revisa en lugar de <see cref="DeUsuarioId"/> (por omisión, quien llama) entre
/// <see cref="Desde"/> y <see cref="Hasta"/> (días de la zona del negocio, inclusive). <see cref="Areas"/>: las áreas delegadas; vacío =
/// todas aquellas de las que <c>de</c> es miembro.
/// </summary>
public sealed record CrearDelegacionDto(Guid AUsuarioId, DateOnly Desde, DateOnly Hasta, IReadOnlyList<Guid>? Areas = null, Guid? DeUsuarioId = null,
    string? Motivo = null);

public sealed record RevocarDelegacionDto(string? Motivo = null);

public sealed record AreaDeDelegacionDto(Guid AreaId, string Codigo, string Nombre);

/// <summary>
/// <see cref="Estado"/>: <c>Programada</c> (aún no empieza), <c>Vigente</c>, <c>Vencida</c> o <c>Revocada</c>. <see cref="TodasLasAreas"/>: delegó
/// todas las áreas que tuviera en el momento de crearla (<see cref="Areas"/> las lista).
/// </summary>
public sealed record DelegacionDto(Guid Id, Guid DeUsuarioId, string De, Guid AUsuarioId, string A, IReadOnlyList<AreaDeDelegacionDto> Areas, bool TodasLasAreas,
    DateOnly Desde, DateOnly Hasta, string? Motivo, string Estado, string CreadaPor, DateTimeOffset CreadaEn, DateTimeOffset? RevocadaEn,
    string? MotivoDeRevocacion);

// ===== SLA de revisión (OP-10…OP-14) =====

/// <summary>Plazo por defecto de un área cuando el flujo no le pone uno (<c>Horas</c> nulo = sin plazo por defecto).</summary>
public sealed record SlaDeAreaDto(Guid AreaId, int? Horas);

/// <summary>
/// SLA de la empresa: <see cref="PorcentajeAviso"/> (50 a 95) es cuánto del plazo debe consumirse para el aviso previo;
/// <see cref="AvisarAdministradores"/> suma a los administradores de la empresa en el aviso de incumplimiento; <see cref="Areas"/> trae
/// el plazo por defecto de cada área activa.
/// </summary>
public sealed record ConfiguracionSlaDto(int PorcentajeAviso, bool AvisarAdministradores, IReadOnlyList<SlaDeAreaDto> Areas, DateTimeOffset? ActualizadaEn);

public sealed record ActualizarConfiguracionSlaDto(int PorcentajeAviso, bool AvisarAdministradores, IReadOnlyList<SlaDeAreaDto>? Areas = null);

public sealed record RevisarSlaDto(Guid? TenantId = null, DateTimeOffset? Fecha = null);

public sealed record ResultadoRevisionSlaDto(int Revisados, int Avisos, int Incumplidos);

/// <summary>Rondas de revisión de un área: <see cref="ADentroDelPlazo"/> y <see cref="FueraDelPlazo"/> suman <see cref="Rondas"/> (solo las que tenían plazo).</summary>
public sealed record CumplimientoSlaDeAreaDto(Guid AreaId, string Codigo, string Nombre, int? SlaHoras, int Rondas, int ADentroDelPlazo, int FueraDelPlazo,
    double? Cumplimiento, double? PromedioDias, int VencidosAhora);

public sealed record ResumenSlaDto(int Expedientes, int Rondas, int ADentroDelPlazo, int FueraDelPlazo, double? Cumplimiento, int VencidosAhora);

/// <summary>Una revisión que hoy está fuera de plazo: qué expediente, qué área y cuántas horas de retraso lleva.</summary>
public sealed record SlaVencidoDto(Guid ExpedienteId, string Folio, Guid AreaId, string AreaNombre, string Tercero, DateTimeOffset Vence, double HorasDeRetraso);

public sealed record InformeSlaDto(ResumenSlaDto Resumen, IReadOnlyList<CumplimientoSlaDeAreaDto> PorArea, IReadOnlyList<SlaVencidoDto> Vencidos);

// ===== Ayuda por documento y empresa (OP-15…OP-16) =====

/// <summary>
/// La ayuda de un tipo de documento: <see cref="AyudaDelGrupo"/> (la del tipo, igual para todas las empresas del grupo),
/// <see cref="AyudaDeLaEmpresa"/> (la que la empresa activa escribió para sí; nula si usa la del grupo) y <see cref="AyudaEfectiva"/> (la que ve el tercero
/// de esta empresa).
/// </summary>
public sealed record AyudaDeDocumentoDto(Guid TipoDocumentoId, string Codigo, string Nombre, string? AyudaDelGrupo, string? AyudaDeLaEmpresa, string? AyudaEfectiva,
    DateTimeOffset? ActualizadaEn);

/// <summary><see cref="Texto"/> vacío: la empresa vuelve a usar la ayuda del grupo.</summary>
public sealed record ActualizarAyudaDeDocumentoDto(string? Texto);

// ===== Recordatorio automático por flujo (OP-19) =====

/// <summary>
/// Recordatorio automático de observaciones en un flujo: <see cref="Modo"/> <c>Empresa</c> (lo que diga la empresa, por omisión), <c>Apagado</c>
/// (este flujo nunca recuerda solo) o <c>CadaDias</c> (cada <see cref="CadaDias"/> días, aunque la empresa lo tenga apagado).
/// </summary>
public sealed record RecordatorioDeFlujoDto(string Modo = "Empresa", int? CadaDias = null);

/// <summary>Persona de la empresa a quien se le puede delegar la revisión (activa, con rol en la empresa y permiso de revisar).</summary>
public sealed record CandidatoDeDelegacionDto(Guid Id, string Nombre, string Correo);
