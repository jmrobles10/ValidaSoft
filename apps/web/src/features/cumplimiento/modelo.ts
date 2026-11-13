import {
  SCOPES,
  type ConceptoCumplimiento,
  type DebidaDiligencia,
  type ExpedienteDto,
  type FactorDeRiesgoDto,
  type MatrizDeRiesgoDto,
  type NivelDeRiesgoDto,
  type RegistrarEstudioCumplimientoDto,
  type RespuestaDeMatrizDto,
  type ResultadoDeMatrizDto,
  type VerificacionDto,
} from '@validasoft/api-client';

/**
 * Reglas de la pantalla de verificaciones (VE-5…VE-16), las mismas que valida la API: qué puede hacer quien mira y qué
 * le falta al estudio del oficial antes de enviarlo, con o sin matriz de riesgo activa (CU-17).
 */

export const ESTADOS_ABIERTOS: readonly ExpedienteDto['estado'][] = ['Abierto', 'EnRevision', 'Observado'];
/** Largo mínimo de la justificación cuando el oficial ajusta el nivel que dio la matriz (el mismo que valida la API). */
export const LONGITUD_MINIMA_JUSTIFICACION = 10;
export const DEBIDAS_EN_ORDEN: readonly DebidaDiligencia[] = ['Simple', 'Ordinaria', 'Ampliada'];

export interface AccionesDeVerificacion {
  /** Consultar o reconsultar listas (`verificaciones:escribir`, expediente abierto). */
  consultar: boolean;
  /** Registrar una consulta de listas hecha por fuera (`verificaciones:escribir`, expediente abierto). */
  consultaManual: boolean;
  /** Resolver coincidencias y registrar el estudio (`cumplimiento:decidir`, expediente abierto). */
  decidir: boolean;
  /** Rechazar por cumplimiento (`cumplimiento:decidir`, en revisión u observado y con una verificación fallida). */
  rechazar: boolean;
  /** Descargar la evidencia (`documentos:descargar`). */
  descargar: boolean;
}

/** El hallazgo grave que habilita al oficial a rechazar: una verificación fallida (coincidencia confirmada o estudio desfavorable). */
export function hayHallazgoGrave(expediente: Pick<ExpedienteDto, 'verificaciones'>): boolean {
  return (expediente.verificaciones ?? []).some((v) => v.estado === 'Fallida');
}

export function accionesDeVerificacion(expediente: Pick<ExpedienteDto, 'estado'> & Partial<Pick<ExpedienteDto, 'verificaciones'>>, scopes: ReadonlySet<string>): AccionesDeVerificacion {
  const abierto = ESTADOS_ABIERTOS.includes(expediente.estado);
  const decidir = abierto && scopes.has(SCOPES.cumplimientoDecidir);
  return {
    consultar: abierto && scopes.has(SCOPES.verificacionesEscribir),
    consultaManual: abierto && scopes.has(SCOPES.verificacionesEscribir),
    decidir,
    rechazar: decidir && (expediente.estado === 'EnRevision' || expediente.estado === 'Observado') && hayHallazgoGrave(expediente),
    descargar: scopes.has(SCOPES.documentosDescargar),
  };
}

/** Clave de textos del nivel de riesgo 1–3 (lo que no es 2 ni 3 se muestra como 1). */
export function claveRiesgo(nivel: number): 'r1' | 'r2' | 'r3' {
  return nivel === 3 ? 'r3' : nivel === 2 ? 'r2' : 'r1';
}

/** Tono del nivel: verde, ámbar y rojo (los de la paleta de estados). */
export function tonoDeRiesgo(nivel: number): 'exito' | 'advertencia' | 'error' {
  return nivel === 3 ? 'error' : nivel === 2 ? 'advertencia' : 'exito';
}

/** Puntaje de la matriz (0–100, un decimal) con coma decimal. */
export function formatearPuntaje(puntaje: number | string): string {
  return Number(puntaje).toLocaleString('es-CO', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
}

/** Qué verificación bloquea la aprobación: las bloqueantes que no están satisfechas. */
export function verificacionesQueBloquean(expediente: Pick<ExpedienteDto, 'verificaciones'>): VerificacionDto[] {
  return (expediente.verificaciones ?? []).filter((v) => v.bloqueante && v.estado !== 'Satisfecha');
}

// ── Estudio del oficial ────────────────────────────────────────────────────────────────────────────────────────────

export interface BorradorEstudio {
  nivelRiesgo: 1 | 2 | 3 | null;
  conflictoDeInteres: boolean;
  debidaDiligencia: DebidaDiligencia;
  concepto: ConceptoCumplimiento | null;
  observaciones: string;
  /** `documentoId:version` de los documentos del expediente citados como evidencia. */
  evidencias: string[];
  /** Con matriz activa: código del factor → código de la opción elegida. */
  respuestas: Record<string, string>;
  /** Con matriz: el oficial cambia el nivel que dio el cálculo (`nivelRiesgo` es el nuevo) y debe explicarlo. */
  ajustarNivel: boolean;
  justificacionDeNivel: string;
  /** Con matriz: la debida diligencia elegida; `null` = la que exige su nivel. */
  debidaElegida: DebidaDiligencia | null;
  /** Archivos del estudio ya subidos (ids de `adjuntosDeCumplimiento`). */
  adjuntos: string[];
}

export type ErrorEstudio =
  | 'nivel-riesgo-requerido'
  | 'concepto-requerido'
  | 'debida-diligencia-ampliada-requerida'
  | 'observaciones-requeridas'
  | 'respuestas-incompletas'
  | 'calculo-pendiente'
  | 'justificacion-de-nivel-requerida';

export const ESTUDIO_VACIO: BorradorEstudio = {
  nivelRiesgo: null,
  conflictoDeInteres: false,
  debidaDiligencia: 'Simple',
  concepto: null,
  observaciones: '',
  evidencias: [],
  respuestas: {},
  ajustarNivel: false,
  justificacionDeNivel: '',
  debidaElegida: null,
  adjuntos: [],
};

/** Lo que el estudio necesita de la matriz activa: los factores que responde el oficial, la exigencia de cada nivel y el cálculo vigente. */
export interface ContextoMatriz {
  factoresManuales: readonly FactorDeRiesgoDto[];
  niveles: readonly NivelDeRiesgoDto[];
  resultado: ResultadoDeMatrizDto | null;
}

export function factoresManuales(matriz: Pick<MatrizDeRiesgoDto, 'factores'>): FactorDeRiesgoDto[] {
  return matriz.factores.filter((f) => f.origen === 'Manual');
}

export function respuestasCompletas(factores: readonly FactorDeRiesgoDto[], respuestas: Readonly<Record<string, string>>): boolean {
  return factores.every((f) => f.opciones.some((o) => o.codigo === respuestas[f.codigo]));
}

/** Las respuestas en el orden de los factores (estable para pedir el cálculo: la misma elección es la misma consulta). */
export function respuestasADto(factores: readonly FactorDeRiesgoDto[], respuestas: Readonly<Record<string, string>>): RespuestaDeMatrizDto[] {
  return factores.flatMap((f) => {
    const opcion = respuestas[f.codigo];
    return opcion !== undefined && f.opciones.some((o) => o.codigo === opcion) ? [{ factor: f.codigo, opcion }] : [];
  });
}

/** La debida diligencia mínima de un nivel según la matriz (si la matriz no lo dice, la del legado: 1 simplificada, 2 ordinaria, 3 intensificada). */
export function debidaMinima(niveles: readonly NivelDeRiesgoDto[], nivel: number): DebidaDiligencia {
  return niveles.find((n) => n.nivel === nivel)?.debidaDiligencia ?? DEBIDAS_EN_ORDEN[Math.min(Math.max(nivel, 1), 3) - 1] ?? 'Simple';
}

export function intensidad(debida: DebidaDiligencia): number {
  return DEBIDAS_EN_ORDEN.indexOf(debida);
}

/** El nivel con el que quedaría el estudio: el ajustado por el oficial o, si no ajusta, el de la matriz. */
export function nivelEfectivo(borrador: Pick<BorradorEstudio, 'ajustarNivel' | 'nivelRiesgo'>, resultado: Pick<ResultadoDeMatrizDto, 'nivel'> | null): number | null {
  return borrador.ajustarNivel && borrador.nivelRiesgo !== null ? borrador.nivelRiesgo : (resultado?.nivel ?? null);
}

/** La debida diligencia con la que quedaría: la elegida, pero nunca más ligera que la que exige su nivel. */
export function debidaEfectiva(borrador: Pick<BorradorEstudio, 'debidaElegida'>, niveles: readonly NivelDeRiesgoDto[], nivel: number): DebidaDiligencia {
  const minima = debidaMinima(niveles, nivel);
  return borrador.debidaElegida !== null && intensidad(borrador.debidaElegida) > intensidad(minima) ? borrador.debidaElegida : minima;
}

/** Hay ajuste cuando el oficial dejó un nivel distinto al de la matriz. */
export function esAjusteDeNivel(borrador: Pick<BorradorEstudio, 'ajustarNivel' | 'nivelRiesgo'>, resultado: Pick<ResultadoDeMatrizDto, 'nivel'> | null): boolean {
  return resultado !== null && borrador.ajustarNivel && borrador.nivelRiesgo !== null && borrador.nivelRiesgo !== resultado.nivel;
}

/**
 * Las reglas del dominio (`Expediente.RegistrarEstudio`). Sin matriz: nivel y concepto obligatorios, riesgo alto ⇒ debida
 * diligencia intensificada. Con matriz: responder todos los factores manuales, tener el cálculo y, si el oficial cambia el nivel,
 * justificarlo; conflicto o desfavorable ⇒ observaciones en ambos casos.
 */
export function validarEstudio(borrador: BorradorEstudio, matriz?: ContextoMatriz): ErrorEstudio[] {
  const errores: ErrorEstudio[] = [];
  const nivel = matriz ? nivelEfectivo(borrador, matriz.resultado) : borrador.nivelRiesgo;
  const debida = matriz && nivel !== null ? debidaEfectiva(borrador, matriz.niveles, nivel) : borrador.debidaDiligencia;
  if (matriz) {
    if (!respuestasCompletas(matriz.factoresManuales, borrador.respuestas)) errores.push('respuestas-incompletas');
    else if (matriz.resultado === null) errores.push('calculo-pendiente');
  } else if (nivel === null) {
    errores.push('nivel-riesgo-requerido');
  }
  if (borrador.concepto === null) errores.push('concepto-requerido');
  if (nivel === 3 && debida !== 'Ampliada') errores.push('debida-diligencia-ampliada-requerida');
  if (matriz && esAjusteDeNivel(borrador, matriz.resultado) && borrador.justificacionDeNivel.trim().length < LONGITUD_MINIMA_JUSTIFICACION) {
    errores.push('justificacion-de-nivel-requerida');
  }
  if (!borrador.observaciones.trim() && (borrador.conflictoDeInteres || borrador.concepto === 'Desfavorable')) errores.push('observaciones-requeridas');
  return errores;
}

export function estudioADto(borrador: BorradorEstudio, matriz?: ContextoMatriz): RegistrarEstudioCumplimientoDto {
  const base: RegistrarEstudioCumplimientoDto = {
    nivelRiesgo: borrador.nivelRiesgo ?? 1,
    conflictoDeInteres: borrador.conflictoDeInteres,
    debidaDiligencia: borrador.debidaDiligencia,
    concepto: borrador.concepto ?? 'Favorable',
    observaciones: borrador.observaciones.trim() || null,
    evidencias: borrador.evidencias.map((clave) => {
      const [documentoId = '', version = '0'] = clave.split(':');
      return { documentoId, version: Number(version) };
    }),
    ...(borrador.adjuntos.length > 0 ? { adjuntos: borrador.adjuntos } : {}),
  };
  if (!matriz) return base;
  const nivel = nivelEfectivo(borrador, matriz.resultado) ?? 1;
  const ajuste = esAjusteDeNivel(borrador, matriz.resultado);
  return {
    ...base,
    // Sin ajuste el nivel sale del cálculo del servidor (con las mismas respuestas); con ajuste se manda el nuevo y su porqué.
    nivelRiesgo: ajuste ? nivel : null,
    debidaDiligencia: debidaEfectiva(borrador, matriz.niveles, nivel),
    respuestas: respuestasADto(matriz.factoresManuales, borrador.respuestas),
    ...(ajuste ? { justificacionDeNivel: borrador.justificacionDeNivel.trim() } : {}),
  };
}

/** Documentos del expediente con versión que el oficial puede citar como evidencia. */
export function evidenciasPosibles(expediente: Pick<ExpedienteDto, 'requisitos'>): { clave: string; nombre: string }[] {
  return (expediente.requisitos ?? []).flatMap((r) =>
    r.versionActual && r.tipoRequisito !== 'Seccion'
      ? [{ clave: `${r.versionActual.documentoId}:${String(r.versionActual.n)}`, nombre: `${r.tipoDocumento.nombre} (v${String(r.versionActual.n)})` }]
      : [],
  );
}

/** El nombre de quien resolvió/consultó: «Sistema» para las consultas automáticas. */
export function esSistema(id: string | null | undefined): boolean {
  return !id || id === 'sistema';
}

// ── Qué pasará al registrar el estudio ─────────────────────────────────────────────────────────────────────────────

export type EfectoDelEstudio =
  | { tipo: 'aprueba' }
  | { tipo: 'desfavorable' }
  /** Las áreas ya aprobaron pero faltan otras verificaciones bloqueantes. */
  | { tipo: 'espera-verificaciones'; faltan: VerificacionDto[] }
  /** Con el estudio no falta cumplimiento, pero el expediente espera la cuenta bancaria designada y aprobada del vínculo. */
  | { tipo: 'espera-cuenta' }
  | { tipo: 'espera-areas' };

/**
 * Lo que provoca registrar el estudio con ese concepto: aprobar el expediente si era lo último que faltaba (áreas aprobadas y
 * ninguna otra verificación bloqueante pendiente), dejarlo esperando otra verificación o las áreas, o fallar la verificación.
 */
export function efectoDelEstudio(
  expediente: Pick<ExpedienteDto, 'verificaciones' | 'esperandoVerificaciones' | 'esperandoCuentaDesignada'>,
  concepto: ConceptoCumplimiento | null,
): EfectoDelEstudio {
  if (concepto === 'Desfavorable') return { tipo: 'desfavorable' };
  const otras = verificacionesQueBloquean(expediente).filter((v) => v.tipo !== 'EstudioDeCumplimiento');
  if (otras.length > 0) return { tipo: 'espera-verificaciones', faltan: otras };
  if (expediente.esperandoVerificaciones === true) return expediente.esperandoCuentaDesignada === true ? { tipo: 'espera-cuenta' } : { tipo: 'aprueba' };
  return { tipo: 'espera-areas' };
}

// ── Archivos del oficial ───────────────────────────────────────────────────────────────────────────────────────────

export const MIMES_DE_ADJUNTO: readonly string[] = ['application/pdf', 'image/png', 'image/jpeg', 'image/webp'];
/** Lo que admite la API: hasta 15 MB. */
export const TAMANO_MAXIMO_ADJUNTO = 15 * 1024 * 1024;
export const ACEPTA_ADJUNTO = MIMES_DE_ADJUNTO.join(',');

/** Por qué no se sube un archivo (el servidor revisa el contenido real; esto evita el viaje): tipo, tamaño o vacío. */
export function errorDeArchivo(archivo: Pick<File, 'type' | 'size'>): 'tipo' | 'tamano' | null {
  if (!MIMES_DE_ADJUNTO.includes(archivo.type)) return 'tipo';
  return archivo.size <= 0 || archivo.size > TAMANO_MAXIMO_ADJUNTO ? 'tamano' : null;
}

/** «1,2 MB», «340 KB». */
export function formatearPeso(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toLocaleString('es-CO', { maximumFractionDigits: 1 })} MB`;
  return `${Math.max(1, Math.round(bytes / 1024)).toLocaleString('es-CO')} KB`;
}
