namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Comprobación externa del expediente (docs/03 §2, §13): listas restrictivas (del tercero y de sus personas relacionadas)
/// y el estudio del Oficial de Cumplimiento (SAGRILAFT). No son hechos del documento: son juicios sobre el expediente.
/// </summary>
public enum TipoVerificacion
{
    ListasRestrictivas,
    EstudioDeCumplimiento
}

/// <summary>
/// Estado de una verificación (docs/03 §3.1). Una bloqueante en <c>Pendiente</c>, <c>CoincidenciaPorAnalizar</c>,
/// <c>Fallida</c> o <c>Expirada</c> impide <c>ExpedienteAprobado</c> aunque todas las áreas hayan aprobado (VE-5).
/// </summary>
public enum EstadoVerificacion
{
    Pendiente,
    CoincidenciaPorAnalizar,
    Satisfecha,
    Fallida,
    Expirada
}

/// <summary>Resultado de la última consulta o estudio (docs/05 §4 <c>VerificacionDto</c>).</summary>
public enum ResultadoVerificacion
{
    SinCoincidencias,
    Coincidencias,
    Aprobado,
    Rechazado
}

/// <summary>Lo que decide Cumplimiento sobre una coincidencia (P-9-4): descartarla (homónimo) o confirmarla (f-4).</summary>
public enum DecisionCoincidencia
{
    Descartada,
    Confirmada
}

/// <summary>
/// Debida diligencia del estudio SAGRILAFT, de menor a mayor intensidad (los textos van como se guardan; en pantalla se leen
/// «simplificada», «ordinaria» e «intensificada»): <c>Simple</c>, <c>Ordinaria</c> (CU-17) y <c>Ampliada</c> (la intensificada;
/// obligatoria con riesgo alto, VE-9). Se guarda como texto, así que agregar <c>Ordinaria</c> no cambia lo ya registrado.
/// </summary>
public enum DebidaDiligencia
{
    Simple,
    Ordinaria,
    Ampliada
}

/// <summary>Decisión del oficial en el estudio: favorable satisface la verificación; desfavorable la deja <c>Fallida</c>.</summary>
public enum ConceptoCumplimiento
{
    Favorable,
    Desfavorable
}

/// <summary>
/// Verificación que pide el flujo (docs/03 §13 <c>VerificacionesRequeridas[]</c>). <see cref="Bloqueante"/>: impide cerrar
/// el expediente mientras no esté satisfecha y vigente. <see cref="VigenciaDias"/>: vigencia del resultado (estudio: 30 por
/// omisión de la configuración); <see cref="FrecuenciaReconsultaMeses"/>: listas, cada cuánto se reconsulta (12).
/// </summary>
public sealed record VerificacionRequeridaDelFlujo(
    TipoVerificacion Tipo, bool Bloqueante = true, int? VigenciaDias = null, int? FrecuenciaReconsultaMeses = null);

/// <summary>Persona consultada en listas: el tercero o una persona relacionada de su ficha (representante, suplente, socio, PEP).</summary>
/// <param name="Clave">Identificación normalizada (<c>CO|CC|123</c>), clave para heredar decisiones de una consulta anterior.</param>
/// <param name="Rol"><c>Tercero</c>, <c>RepresentanteLegal</c>, <c>Suplente</c>, <c>Socio</c> o <c>Pep</c>.</param>
public sealed record PersonaConsultada(string Clave, string Rol, string Nombre, string Identificacion);

/// <summary>Una decisión de Cumplimiento sobre una coincidencia, vigente hasta <see cref="VigenteHasta"/> (+12 meses).</summary>
public sealed record ResolucionDeCoincidencia(DecisionCoincidencia Decision, string Justificacion, string Por, DateTimeOffset Fecha,
    DateTimeOffset VigenteHasta);

/// <summary>
/// Coincidencia encontrada para una persona consultada. <see cref="Heredada"/>: la misma coincidencia (persona, lista y
/// nombre encontrado) ya se había descartado y la decisión sigue vigente, así que la reconsulta no la vuelve a pedir.
/// </summary>
public sealed record CoincidenciaEncontrada(
    Guid Id, string ClavePersona, string NombrePersona, string Rol, string Lista, string NombreEncontrado, string? Detalle, decimal? Puntaje,
    ResolucionDeCoincidencia? Heredada = null);

/// <summary>
/// Archivo de evidencia guardado en el almacén del tenant: el PDF que devolvió el proveedor o el que generó Validasoft con
/// lo consultado (<see cref="DelProveedor"/> = false). Con un proveedor real se guarda además su respuesta cruda (JSON) y su
/// SHA-256 (<see cref="ClaveRespuesta"/>, <see cref="Sha256Respuesta"/>; VE-10), que el PDF cita.
/// </summary>
public sealed record EvidenciaDeVerificacion(string Bucket, string Clave, string Sha256, long Tamano, string TipoContenido, string NombreArchivo,
    bool DelProveedor, string? ClaveRespuesta = null, string? Sha256Respuesta = null);

/// <summary>Documento del expediente que el oficial cita como evidencia del estudio (docs/05 §4 <c>evidencias[] {documentoId, version}</c>).</summary>
public sealed record EvidenciaDeEstudio(Guid DocumentoId, int Version, string? Nombre);

/// <summary>Lo que registra el Oficial de Cumplimiento (campos del legado: listas, conflicto de interés, riesgo 1–3, debida diligencia).</summary>
/// <param name="Matriz">Con matriz de riesgo activa (CU-17): el cálculo del que salieron el nivel y la debida diligencia.</param>
/// <param name="JustificacionDeNivel">Si el oficial ajustó el nivel de la matriz, por qué (obligatoria en ese caso).</param>
/// <param name="Adjuntos">Archivos que el oficial subió para soportar el estudio (CU-16).</param>
public sealed record DatosDeEstudio(int NivelRiesgo, bool ConflictoDeInteres, DebidaDiligencia DebidaDiligencia, string? Observaciones,
    IReadOnlyList<EvidenciaDeEstudio> Evidencias, ConceptoCumplimiento Concepto, Cumplimiento.ResultadoDeMatriz? Matriz = null,
    string? JustificacionDeNivel = null, IReadOnlyList<Guid>? Adjuntos = null);

/// <summary>Resultado de una consulta a un proveedor de listas, listo para registrarse (sin ids de coincidencia todavía).</summary>
public sealed record ResultadoDeListas(string Proveedor, string ReferenciaExterna, IReadOnlyList<PersonaConsultada> Personas,
    IReadOnlyList<CoincidenciaHallada> Coincidencias, EvidenciaDeVerificacion? Evidencia, DateTimeOffset VigenteHasta);

/// <summary>Coincidencia tal como la reporta el proveedor para una persona consultada.</summary>
public sealed record CoincidenciaHallada(string ClavePersona, string Lista, string NombreEncontrado, string? Detalle, decimal? Puntaje);

// Eventos del stream Expediente-{id} (H7). Actor, sistema y correlación viajan en los metadatos.

/// <summary>Las verificaciones que pide la versión del flujo, copiadas al abrir el expediente (todas nacen <c>Pendiente</c>).</summary>
public sealed record VerificacionesRequeridas(IReadOnlyList<VerificacionRequeridaDelFlujo> Verificaciones, DateTimeOffset Fecha);

/// <summary>
/// Resultado de una consulta de listas (docs/03 §6.3). Idempotente por (expediente, tipo, <see cref="ReferenciaExterna"/>).
/// Coincidencias sin decisión vigente ⇒ <c>CoincidenciaPorAnalizar</c>; sin ellas ⇒ <c>Satisfecha</c> hasta
/// <see cref="VigenteHasta"/>. Una reconsulta reemplaza el resultado (el anterior queda en el historial con su evidencia).
/// </summary>
public sealed record VerificacionExternaRegistrada(
    TipoVerificacion Tipo, string Proveedor, ResultadoVerificacion Resultado, string ReferenciaExterna, IReadOnlyList<PersonaConsultada> Personas,
    IReadOnlyList<CoincidenciaEncontrada> Coincidencias, EvidenciaDeVerificacion? Evidencia, DateTimeOffset VigenteHasta, string Por,
    DateTimeOffset Fecha);

/// <summary>Cumplimiento decidió una coincidencia (P-9-4); la decisión vale 12 meses para las reconsultas (<see cref="VigenteHasta"/>).</summary>
public sealed record CoincidenciaResuelta(TipoVerificacion Tipo, Guid CoincidenciaId, DecisionCoincidencia Decision, string Justificacion, string Por,
    DateTimeOffset VigenteHasta, DateTimeOffset Fecha);

/// <summary>
/// Estudio del Oficial de Cumplimiento (SAGRILAFT): favorable ⇒ <c>Satisfecha</c> hasta <see cref="VigenteHasta"/> (30 días por
/// omisión); desfavorable ⇒ <c>Fallida</c>. <see cref="VerificadoEnListas"/>: las listas estaban satisfechas al registrarlo.
/// </summary>
public sealed record EstudioDeCumplimientoRegistrado(DatosDeEstudio Estudio, bool VerificadoEnListas, string Por, DateTimeOffset VigenteHasta,
    DateTimeOffset Fecha);

/// <summary>La vigencia del resultado pasó con el expediente abierto: hay que volver a consultar (listas) o a estudiar.</summary>
/// <param name="Motivo">Nulo si venció su vigencia; si no, por qué se expiró antes (p. ej. cambió el representante legal, CU-15).</param>
public sealed record VerificacionExpirada(TipoVerificacion Tipo, DateTimeOffset VencioEn, DateTimeOffset Fecha, string? Motivo = null);

/// <summary>Para qué se subió un archivo de cumplimiento: soportar el estudio del oficial o ser la evidencia de una consulta de listas hecha por fuera.</summary>
public enum PropositoDeAdjunto
{
    Estudio,
    ConsultaManual
}

/// <summary>
/// Archivo que sube el oficial de cumplimiento al expediente (CU-16): PDF o imagen guardada en el bucket del tenant, con su
/// SHA-256. No pasa por la bóveda del tercero: es de la empresa y no se comparte.
/// </summary>
public sealed record AdjuntoDeCumplimiento(Guid Id, string Nombre, string TipoContenido, long Tamano, string Sha256, string Bucket, string Clave,
    PropositoDeAdjunto Proposito, string Por, DateTimeOffset Fecha);

/// <summary>El oficial subió un archivo al expediente (el estudio lo cita por su id; la consulta manual lo usa de evidencia).</summary>
public sealed record AdjuntoDeCumplimientoSubido(AdjuntoDeCumplimiento Adjunto);

/// <summary>La consulta automática al proveedor falló (no disponible, sin credenciales…); se reintenta desde <see cref="ReintentarDesde"/>.</summary>
public sealed record ConsultaDeVerificacionFallida(TipoVerificacion Tipo, string Proveedor, string Error, DateTimeOffset ReintentarDesde,
    DateTimeOffset Fecha);

/// <summary>Alguien descargó la evidencia de una consulta (auditoría de acceso, como la descarga del certificado).</summary>
public sealed record EvidenciaDeVerificacionDescargada(TipoVerificacion Tipo, string ReferenciaExterna, string ActorTipo, string ActorId, string? Ip,
    string? UserAgent, DateTimeOffset Fecha);
