namespace Validasoft.Contratos;

// DTOs de las verificaciones externas y el cumplimiento (H7, docs/05 §4; decisiones VE-5…VE-16). Enums como texto.

/// <summary>
/// Verificación que pide el flujo (<c>VerificacionRequeridaDto</c> de docs/05 §4). <see cref="Tipo"/>:
/// <c>ListasRestrictivas|EstudioDeCumplimiento</c>. <see cref="VigenciaDias"/> nula = la de la configuración (estudio: 30;
/// listas: <see cref="FrecuenciaReconsultaMeses"/>, 12 por omisión). El proveedor de listas no va en el flujo: es el que la
/// empresa configuró (VE-7).
/// </summary>
public sealed record VerificacionRequeridaDto(string Tipo, bool Bloqueante = true, int? VigenciaDias = null, int? FrecuenciaReconsultaMeses = null);

/// <summary>Persona consultada en listas: el tercero o una persona relacionada de la ficha (<c>Rol</c>).</summary>
public sealed record PersonaConsultadaDto(string Rol, string Nombre, string Identificacion);

/// <summary>
/// Coincidencia en listas. <see cref="Decision"/>: <c>Descartada|Confirmada</c> o nula (por analizar). <see cref="Heredada"/>:
/// la decisión viene de una consulta anterior y sigue vigente hasta <see cref="VigenteHasta"/>.
/// </summary>
public sealed record CoincidenciaDto(
    Guid Id, string Persona, string Rol, string Identificacion, string Lista, string NombreEncontrado, string? Detalle, decimal? Puntaje, string? Decision,
    string? Justificacion, string? ResueltaPor, DateTimeOffset? ResueltaEn, DateTimeOffset? VigenteHasta, bool Heredada);

/// <summary>Evidencia descargable (el PDF; con un proveedor real, además, el hash de su respuesta cruda guardada).</summary>
public sealed record EvidenciaVerificacionDto(string NombreArchivo, string TipoContenido, long Tamano, string Sha256, bool DelProveedor,
    string? Sha256Respuesta);

/// <summary>Consulta anterior reemplazada por una reconsulta.</summary>
public sealed record ConsultaAnteriorDto(string Proveedor, string ReferenciaExterna, string Resultado, DateTimeOffset Fecha, int Coincidencias,
    bool ConEvidencia);

/// <summary>
/// Estudio del Oficial de Cumplimiento (<c>EstudioCumplimientoDto</c> de docs/05 §4, VE-9). <see cref="DebidaDiligencia"/>:
/// <c>Simple|Ampliada</c>; <see cref="Concepto"/>: <c>Favorable|Desfavorable</c>.
/// </summary>
public sealed record EstudioCumplimientoDto(
    bool VerificadoEnListas, bool ConflictoDeInteres, string DebidaDiligencia, int NivelRiesgo, string? Observaciones,
    IReadOnlyList<EvidenciaEstudioDto> Evidencias, string Concepto, string Oficial, DateTimeOffset Fecha, DateTimeOffset VigenteHasta,
    ResultadoDeMatrizDto? Matriz = null, string? JustificacionDeNivel = null, IReadOnlyList<AdjuntoDeCumplimientoDto>? Adjuntos = null);

/// <summary>Documento del expediente citado como evidencia del estudio.</summary>
public sealed record EvidenciaEstudioDto(Guid DocumentoId, int Version, string? Nombre = null);

/// <summary>
/// Verificación del expediente (<c>VerificacionDto</c> de docs/05 §4). <see cref="Estado"/>:
/// <c>Pendiente|CoincidenciaPorAnalizar|Satisfecha|Fallida|Expirada</c>; <see cref="Resultado"/>:
/// <c>SinCoincidencias|Coincidencias|Aprobado|Rechazado</c> (nulo si aún no se consulta). <see cref="UltimoError"/>: por qué
/// falló la última consulta automática (se reintenta desde <see cref="ReintentarDesde"/>).
/// </summary>
public sealed record VerificacionDto(
    string Tipo, string Estado, bool Bloqueante, bool RequeridaPorElFlujo, string? Proveedor, string? Resultado, string? ReferenciaExterna,
    DateTimeOffset? RegistradaEn, string? RegistradaPor, DateTimeOffset? VigenteHasta, EvidenciaVerificacionDto? Evidencia,
    IReadOnlyList<PersonaConsultadaDto> Personas, IReadOnlyList<CoincidenciaDto> Coincidencias, EstudioCumplimientoDto? Estudio,
    IReadOnlyList<ConsultaAnteriorDto> Anteriores, string? UltimoError, DateTimeOffset? ReintentarDesde, int? VigenciaDias,
    int? FrecuenciaReconsultaMeses);

/// <summary><see cref="Decision"/>: <c>Descartada|Confirmada</c>; la justificación es obligatoria (docs/05 §4).</summary>
public sealed record ResolverCoincidenciaDto(string Decision, string Justificacion);

/// <summary>
/// Estudio que registra el oficial (VE-9). <see cref="NivelRiesgo"/> 1–3 (3 exige debida diligencia <c>Ampliada</c>);
/// observaciones obligatorias con conflicto de interés o concepto desfavorable. <see cref="Evidencias"/>: documentos del
/// expediente (documento y versión) que soportan el estudio.
/// </summary>
/// <remarks>
/// Con la matriz de riesgo de la empresa activa (CU-17) el nivel y la debida diligencia salen del cálculo: se envían las
/// <see cref="Respuestas"/> a los factores manuales y los demás pueden ir vacíos. Si se envía un <see cref="NivelRiesgo"/> distinto del
/// calculado, <see cref="JustificacionDeNivel"/> es obligatoria. Sin matriz activa, nivel y debida diligencia son obligatorios.
/// <see cref="Adjuntos"/>: ids de archivos subidos antes al expediente para este estudio (CU-16).
/// </remarks>
public sealed record RegistrarEstudioCumplimientoDto(
    int? NivelRiesgo, bool ConflictoDeInteres, string? DebidaDiligencia, string Concepto, string? Observaciones = null,
    IReadOnlyList<EvidenciaEstudioDto>? Evidencias = null, IReadOnlyList<RespuestaDeMatrizDto>? Respuestas = null, string? JustificacionDeNivel = null,
    IReadOnlyList<Guid>? Adjuntos = null);

/// <summary>URL prefirmada de corta vida para descargar una evidencia (la descarga queda en la auditoría).</summary>
public sealed record DescargaEvidenciaDto(string Url, DateTimeOffset Expira, string NombreArchivo);

/// <summary>
/// Fila de la bandeja del oficial de cumplimiento: un expediente abierto con verificaciones por atender, o el monitoreo de
/// un vínculo activo con coincidencias nuevas (<see cref="Origen"/> <c>Expediente|Monitoreo</c>). <see cref="Pendientes"/>:
/// tipo y estado de cada verificación que no está satisfecha.
/// </summary>
public sealed record FilaCumplimientoDto(
    string Origen, Guid? ExpedienteId, string Folio, Guid VinculoId, TerceroResumenDto Tercero, string? TipoVinculo, string? EstadoExpediente,
    IReadOnlyList<VerificacionPendienteDto> Pendientes, int CoincidenciasPorAnalizar, bool EsperandoSoloVerificaciones, DateTimeOffset Desde);

public sealed record VerificacionPendienteDto(string Tipo, string Estado, bool Bloqueante);

/// <summary>Monitoreo de listas de un vínculo activo (VE-12): la última reconsulta periódica y la próxima.</summary>
public sealed record MonitoreoListasDto(
    Guid VinculoId, Guid ExpedienteId, string Folio, int FrecuenciaDias, DateTimeOffset? ProximaConsulta, VerificacionDto Listas);

// ---------- Configuración de listas por empresa (VE-7, VE-11) ----------

/// <summary>
/// Proveedor de listas tal como lo ve el administrador: si es real, si la instalación lo habilitó, y las credenciales de la
/// empresa (la clave nunca vuelve: solo si está configurada y sus últimos 4 caracteres).
/// </summary>
public sealed record ProveedorListasDto(
    string Nombre, bool Real, bool Principal, bool HabilitadoEnInstalacion, bool Configurado, string? Usuario, string? UltimosCuatroClave,
    string? UrlBase, string? Plan, int? CuotaMensual, string? ModoBusqueda);

/// <summary>Consumo del mes con el proveedor (búsquedas = personas consultadas).</summary>
public sealed record UsoListasDto(string Mes, string Proveedor, int Consultas, int Busquedas, int ConCoincidencias, int? CuotaMensual);

/// <summary>
/// Listas restrictivas de la empresa: el proveedor elegido (nulo = el de la instalación), el que se usará
/// (<see cref="ProveedorEfectivo"/>, nulo si no hay ninguno disponible), los proveedores y el consumo del mes.
/// </summary>
public sealed record ListasRestrictivasConfigDto(
    string? Proveedor, string? ProveedorEfectivo, IReadOnlyList<ProveedorListasDto> Proveedores, IReadOnlyList<UsoListasDto> Uso,
    int DiasMonitoreo, int VigenciaEstudioDias);

/// <summary>
/// Cambia el proveedor (<see cref="Proveedor"/>: nombre, o vacío para usar el de la instalación; nulo = sin cambio) y/o las
/// credenciales de uno real.
/// </summary>
public sealed record ActualizarListasRestrictivasDto(string? Proveedor = null, CredencialesListasDto? Credenciales = null);

/// <summary>
/// Credenciales de la empresa con un proveedor real. <see cref="Clave"/>: nula conserva la actual, vacía quita las
/// credenciales. <see cref="ModoBusqueda"/>: <c>PorNombre|PorDocumento</c>.
/// </summary>
public sealed record CredencialesListasDto(string Proveedor, string? Usuario = null, string? Clave = null, string? UrlBase = null, string? Plan = null,
    int? CuotaMensual = null, string? ModoBusqueda = null);

// ---------- Informe SAGRILAFT (VE-14) ----------

public sealed record ResumenVerificacionesDto(string Tipo, string? Proveedor, int Total, int ConCoincidencias, int Confirmadas, int Pendientes);

public sealed record RiesgoDto(int Nivel, int Cantidad);

/// <summary>Una verificación de un expediente en el periodo (fila del informe y del Excel).</summary>
public sealed record FilaInformeCumplimientoDto(
    Guid ExpedienteId, string Folio, string Tercero, string Identificacion, string TipoVinculo, string EstadoExpediente, string Verificacion,
    string? Proveedor, string Estado, string? Resultado, DateTimeOffset? Fecha, int Personas, int Coincidencias, int Confirmadas, int Descartadas,
    int? NivelRiesgo, bool? ConflictoDeInteres, string? DebidaDiligencia, string? Concepto, DateTimeOffset? VigenteHasta);

/// <summary>
/// Informe SAGRILAFT (<c>InformeCumplimientoDto</c> de docs/05 §4): totales por verificación y proveedor, distribución del
/// riesgo, estudios vencidos, listas por reconsultar y el detalle por expediente.
/// </summary>
public sealed record InformeCumplimientoDto(
    DateOnly Desde, DateOnly Hasta, IReadOnlyList<ResumenVerificacionesDto> Verificaciones, IReadOnlyList<RiesgoDto> Riesgo, int EstudiosVencidos,
    int PendientesReconsulta, IReadOnlyList<FilaInformeCumplimientoDto> Filas);

// ---------- Eventos públicos ----------

/// <summary>
/// <c>expediente.verificacion_registrada</c> · <c>expediente.coincidencia_resuelta</c> · <c>expediente.verificacion_expirada</c>
/// (docs/05 §7). Sin nombres de personas ni listas: solo conteos, estado y resultado.
/// </summary>
public sealed record DatosVerificacionDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, IReadOnlyList<ReferenciaExternaDto> Referencias, string Tipo, string? Proveedor, string? Resultado,
    string Estado, bool Bloqueante, DateTimeOffset? VigenteHasta, int Coincidencias, int CoincidenciasPorAnalizar, string? Decision = null,
    int? NivelRiesgo = null);

// ---------- Procesamiento inmediato (Plataforma; fecha simulada solo en Development) ----------

public sealed record RevisarVerificacionesDto(Guid? TenantId = null, DateTimeOffset? Fecha = null);

/// <summary>Lo que atendió el procesamiento: consultas automáticas de listas, expiraciones de lo vencido y monitoreos de vínculos activos.</summary>
public sealed record ResultadoRevisionVerificacionesDto(int Consultas, int Expiraciones, int Monitoreos);
