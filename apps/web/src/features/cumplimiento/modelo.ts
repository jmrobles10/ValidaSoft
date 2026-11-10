import { SCOPES, type ConceptoCumplimiento, type DebidaDiligencia, type ExpedienteDto, type RegistrarEstudioCumplimientoDto, type VerificacionDto } from '@validasoft/api-client';

/**
 * Reglas de la pantalla de verificaciones (VE-5…VE-16), las mismas que valida la API: qué puede hacer quien mira y qué
 * le falta al estudio del oficial antes de enviarlo.
 */

export const ESTADOS_ABIERTOS: readonly ExpedienteDto['estado'][] = ['Abierto', 'EnRevision', 'Observado'];

export interface AccionesDeVerificacion {
  /** Consultar o reconsultar listas (`verificaciones:escribir`, expediente abierto). */
  consultar: boolean;
  /** Resolver coincidencias y registrar el estudio (`cumplimiento:decidir`, expediente abierto). */
  decidir: boolean;
  /** Descargar la evidencia (`documentos:descargar`). */
  descargar: boolean;
}

export function accionesDeVerificacion(expediente: Pick<ExpedienteDto, 'estado'>, scopes: ReadonlySet<string>): AccionesDeVerificacion {
  const abierto = ESTADOS_ABIERTOS.includes(expediente.estado);
  return {
    consultar: abierto && scopes.has(SCOPES.verificacionesEscribir),
    decidir: abierto && scopes.has(SCOPES.cumplimientoDecidir),
    descargar: scopes.has(SCOPES.documentosDescargar),
  };
}

/** Clave de textos del nivel de riesgo 1–3 (lo que no es 2 ni 3 se muestra como 1). */
export function claveRiesgo(nivel: number): 'r1' | 'r2' | 'r3' {
  return nivel === 3 ? 'r3' : nivel === 2 ? 'r2' : 'r1';
}

/** Qué verificación bloquea la aprobación: las bloqueantes que no están satisfechas. */
export function verificacionesQueBloquean(expediente: Pick<ExpedienteDto, 'verificaciones'>): VerificacionDto[] {
  return (expediente.verificaciones ?? []).filter((v) => v.bloqueante && v.estado !== 'Satisfecha');
}

export interface BorradorEstudio {
  nivelRiesgo: 1 | 2 | 3 | null;
  conflictoDeInteres: boolean;
  debidaDiligencia: DebidaDiligencia;
  concepto: ConceptoCumplimiento | null;
  observaciones: string;
  /** `documentoId:version` de los documentos del expediente citados como evidencia. */
  evidencias: string[];
}

export type ErrorEstudio = 'nivel-riesgo-requerido' | 'concepto-requerido' | 'debida-diligencia-ampliada-requerida' | 'observaciones-requeridas';

export const ESTUDIO_VACIO: BorradorEstudio = {
  nivelRiesgo: null,
  conflictoDeInteres: false,
  debidaDiligencia: 'Simple',
  concepto: null,
  observaciones: '',
  evidencias: [],
};

/** Las reglas del dominio (`Expediente.RegistrarEstudio`): riesgo alto ⇒ ampliada; conflicto o desfavorable ⇒ observaciones. */
export function validarEstudio(borrador: BorradorEstudio): ErrorEstudio[] {
  const errores: ErrorEstudio[] = [];
  if (borrador.nivelRiesgo === null) errores.push('nivel-riesgo-requerido');
  if (borrador.concepto === null) errores.push('concepto-requerido');
  if (borrador.nivelRiesgo === 3 && borrador.debidaDiligencia !== 'Ampliada') errores.push('debida-diligencia-ampliada-requerida');
  if (!borrador.observaciones.trim() && (borrador.conflictoDeInteres || borrador.concepto === 'Desfavorable')) errores.push('observaciones-requeridas');
  return errores;
}

export function estudioADto(borrador: BorradorEstudio): RegistrarEstudioCumplimientoDto {
  return {
    nivelRiesgo: borrador.nivelRiesgo ?? 1,
    conflictoDeInteres: borrador.conflictoDeInteres,
    debidaDiligencia: borrador.debidaDiligencia,
    concepto: borrador.concepto ?? 'Favorable',
    observaciones: borrador.observaciones.trim() || null,
    evidencias: borrador.evidencias.map((clave) => {
      const [documentoId = '', version = '0'] = clave.split(':');
      return { documentoId, version: Number(version) };
    }),
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
