namespace Validasoft.Contratos;

// DTOs del cumplimiento completo (H7, CU-15…CU-21): matriz de riesgo, archivos del oficial, consulta de listas hecha por fuera,
// rechazo directo del oficial e informe SAGRILAFT completo. Enums como texto.

// ---------- Matriz de riesgo (CU-17) ----------

/// <summary>Opción de un factor con su puntaje (0–100) y, opcionalmente, un piso de nivel (2 o 3).</summary>
public sealed record OpcionDeFactorDto(string Codigo, string Nombre, int Puntaje, int? NivelMinimo = null);

/// <summary>Factor de la matriz. <see cref="Origen"/>: <c>Manual</c> (lo responde el oficial) o <c>Automatico</c> (lo calcula el sistema).</summary>
public sealed record FactorDeRiesgoDto(string Codigo, string Nombre, string Origen, int Peso, IReadOnlyList<OpcionDeFactorDto> Opciones, string? Ayuda = null);

/// <summary>Nivel 1 Bajo, 2 Medio o 3 Alto, con su debida diligencia (<c>Simple|Ordinaria|Ampliada</c>) y cada cuántos meses se revisa.</summary>
public sealed record NivelDeRiesgoDto(int Nivel, string Nombre, string DebidaDiligencia, int MesesRevision);

/// <summary>
/// Matriz de riesgo de la empresa. <see cref="EsPorDefecto"/>: aún no se guardó una propia y esta es la plantilla que se ofrece.
/// Puntaje menor a <see cref="LimiteBajo"/> ⇒ Bajo; menor a <see cref="LimiteMedio"/> ⇒ Medio; desde ahí ⇒ Alto.
/// </summary>
public sealed record MatrizDeRiesgoDto(
    bool Activa, bool EsPorDefecto, int Version, IReadOnlyList<FactorDeRiesgoDto> Factores, int LimiteBajo, int LimiteMedio, IReadOnlyList<NivelDeRiesgoDto> Niveles,
    DateTimeOffset? ActualizadaEn);

/// <summary>Guarda la matriz completa (reemplaza la anterior). Es privada de la empresa y queda en la auditoría.</summary>
public sealed record ActualizarMatrizDeRiesgoDto(
    bool Activa, IReadOnlyList<FactorDeRiesgoDto> Factores, int LimiteBajo, int LimiteMedio, IReadOnlyList<NivelDeRiesgoDto> Niveles);

/// <summary>La opción que el oficial elige para un factor manual.</summary>
public sealed record RespuestaDeMatrizDto(string Factor, string Opcion);

/// <summary>Vista previa del cálculo para un expediente sin registrar nada: <see cref="Respuestas"/> a los factores manuales.</summary>
public sealed record CalcularMatrizDto(IReadOnlyList<RespuestaDeMatrizDto>? Respuestas = null);

/// <summary>Cómo pesó un factor en el resultado.</summary>
public sealed record DesgloseDeMatrizDto(
    string Factor, string FactorNombre, string Origen, int Peso, string Opcion, string OpcionNombre, int Puntaje, int? NivelMinimo);

/// <summary>
/// Resultado de la matriz: puntaje 0–100, nivel final (<see cref="NivelPorPuntaje"/> es el del puntaje; si una opción fijó un piso,
/// <see cref="ForzadoPor"/> dice cuál), la debida diligencia y la periodicidad que le toca, y el desglose.
/// </summary>
public sealed record ResultadoDeMatrizDto(
    decimal Puntaje, int Nivel, int NivelPorPuntaje, string? ForzadoPor, string DebidaDiligencia, int MesesRevision, int VersionMatriz,
    IReadOnlyList<DesgloseDeMatrizDto> Desglose);

// ---------- Archivos del oficial y consulta manual (CU-16) ----------

/// <summary>Archivo que el oficial subió al expediente. <see cref="Proposito"/>: <c>Estudio|ConsultaManual</c>.</summary>
public sealed record AdjuntoDeCumplimientoDto(Guid Id, string Nombre, string TipoContenido, long Tamano, string Sha256, string Proposito, string SubidoPor,
    DateTimeOffset Fecha);

/// <summary>Paso 1 de la subida de un archivo del oficial: nombre, tamaño, tipo (PDF o imagen) y <see cref="Proposito"/> (<c>Estudio|ConsultaManual</c>).</summary>
public sealed record IniciarAdjuntoDeCumplimientoDto(string NombreArchivo, long TamanoBytes, string TipoMime, string Proposito);

/// <summary>Respuesta del paso 1: la carga y la URL a la que se sube el archivo (como toda carga).</summary>
public sealed record CargaDeAdjuntoDto(Guid CargaId, InstruccionCargaDto Carga);

/// <summary>Paso 2: confirma la carga con el mismo nombre y propósito del paso 1.</summary>
public sealed record ConfirmarAdjuntoDeCumplimientoDto(string NombreArchivo, string Proposito);

/// <summary>Coincidencia hallada en una consulta hecha por fuera: la persona (su <c>clave</c> de la lista de consultadas) y lo que decía la lista.</summary>
public sealed record CoincidenciaManualDto(string ClavePersona, string Lista, string NombreEncontrado, string? Detalle = null, decimal? Puntaje = null);

/// <summary>
/// Registra una consulta de listas hecha fuera de Validasoft (la página del proveedor, por ejemplo) con su PDF ya subido
/// (<see cref="AdjuntoId"/>). <see cref="Fuente"/>: dónde se hizo; <see cref="ConsultadaEn"/>: cuándo (hasta 30 días atrás);
/// <see cref="Coincidencias"/> vacías = sin coincidencias. <see cref="MesesVigencia"/>: por omisión los del flujo.
/// </summary>
public sealed record RegistrarConsultaManualDto(
    Guid AdjuntoId, string Fuente, DateTimeOffset ConsultadaEn, IReadOnlyList<CoincidenciaManualDto>? Coincidencias = null, int? MesesVigencia = null);

/// <summary>Las personas que cubre una consulta de listas hoy (el tercero, sus representantes, socios y PEP): sobre ellas se reportan las coincidencias.</summary>
public sealed record PersonasParaConsultaDto(IReadOnlyList<PersonaConsultadaDto> Personas, IReadOnlyList<string> Claves);

// ---------- Rechazo directo del oficial (CU-18) ----------

/// <summary>El oficial rechaza el expediente por un hallazgo: motivos del catálogo de rechazo y justificación obligatorios.</summary>
public sealed record RechazarPorCumplimientoDto(IReadOnlyList<string> Motivos, string Justificacion);

// ---------- Informe SAGRILAFT completo (CU-14 del informe) ----------

/// <summary>Una persona relacionada del tercero (información legal del informe SAGRILAFT).</summary>
public sealed record PersonaInformeDto(
    string Rol, string TipoIdentificacion, string Identificacion, string Nombre, decimal? Participacion, bool EsPep, bool VinculoPep, bool BeneficiarioFinal);

/// <summary>
/// Una fila por tercero con su situación de cumplimiento más reciente (las columnas del informe del SRM y lo que no tenía):
/// última consulta de listas, estudio, nivel de riesgo, debida diligencia, próxima revisión y estado.
/// <see cref="EstadoDebidaDiligencia"/>: <c>SinEstudio|Vigente|PorVencer|Vencida</c>; <see cref="EstadoListas"/>: <c>SinConsulta|Vigente|PorVencer|Vencida</c>.
/// </summary>
public sealed record FilaTerceroSagrilafDto(
    Guid VinculoId, Guid TerceroId, string Folio, string Tercero, string TipoIdentificacion, string Identificacion, string TipoPersona, string TipoVinculo,
    string? Pais, string? Ciudad, string? Correo, string? Actividad, string EstadoVinculo, DateTimeOffset? UltimaConsultaListas, string? ProveedorListas,
    string EstadoListas, int CoincidenciasPorAnalizar, DateTimeOffset? FechaEstudio, string? Oficial, int? NivelRiesgo, string? DebidaDiligencia,
    string? Concepto, bool? ConflictoDeInteres, DateTimeOffset? ProximaRevision, int? DiasParaRevision, string EstadoDebidaDiligencia, int Personas,
    int Pep);

/// <summary>Cantidad de estudios cerrados y de consultas por mes (tendencia de los últimos 12 meses).</summary>
public sealed record MesDeCumplimientoDto(string Mes, int Estudios, int Consultas);

/// <summary>Tiempos del oficial: promedio de días entre que una verificación queda pendiente y se resuelve, y cuántas siguen abiertas.</summary>
public sealed record TiemposDeCumplimientoDto(decimal? PromedioDiasEnResolver, int Resueltas, int Abiertas, int AbiertasMasDe7Dias);

/// <summary>
/// Informe SAGRILAFT completo (los dos del SRM —tablero y detalle por tercero— y más): totales y tendencia, distribución del riesgo,
/// vencidos y por vencer, tiempos, una fila por tercero y las personas relacionadas de cada uno. Se calcula al consultar; la
/// misma información sale en Excel en tres hojas (Terceros, Personas, Verificaciones).
/// </summary>
public sealed record InformeSagrilaftDto(
    DateOnly Desde, DateOnly Hasta, int Terceros, int ConEstudioVigente, int EstudiosPorVencer, int EstudiosVencidos, int ListasPorVencer, int ListasVencidas,
    int SinEstudio, IReadOnlyList<RiesgoDto> Riesgo, IReadOnlyList<MesDeCumplimientoDto> Tendencia, TiemposDeCumplimientoDto Tiempos,
    IReadOnlyList<FilaTerceroSagrilafDto> Filas);

/// <summary>Las personas relacionadas de los terceros del informe (hoja «Personas» del Excel; también para el detalle de un tercero).</summary>
public sealed record PersonasDelTerceroDto(Guid TerceroId, string Tercero, IReadOnlyList<PersonaInformeDto> Personas);
