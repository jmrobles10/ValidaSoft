import type { ActualizarRevisionAutomaticaDto, RevisionAutomaticaConfigDto } from '@validasoft/api-client';
import { esNivel, numeroDe, type NivelDeRevision } from '../../revisionAutomatica/modelo';

/** Confianza mínima que admite la API (`RevisionAutomaticaEmpresa.UmbralMinimo`): 0,85. En la pantalla va en porcentaje. */
export const UMBRAL_MINIMO = 85;
export const UMBRAL_INICIAL = 95;
export const MUESTREO_INICIAL = 10;
export const ESPERA_INICIAL = 10;
/** Topes de la API (`MaximoTope`, `MaximoEsperaMinutos`). */
export const MAXIMO_TOPE = 1_000_000;
export const MAXIMO_ESPERA = 1440;

/** Lo que la persona va editando: los números como texto (campos de dígitos) y las listas de tipos y áreas. */
export interface BorradorRevision {
  nivel: NivelDeRevision;
  /** Confianza mínima en porcentaje entero (85–100). */
  umbral: string;
  /** Porcentaje de lo aprobado que una persona confirma (0–100). */
  muestreo: string;
  /** Tope mensual de aprobaciones; vacío = sin tope. */
  tope: string;
  /** Minutos que se espera el análisis de la IA. */
  espera: string;
  /** Códigos de los tipos de documento que el sistema puede aprobar solo. */
  tipos: string[];
  /** Ids de las áreas cuya revisión hace el sistema. */
  areas: string[];
}

export function nivelDe(config: Pick<RevisionAutomaticaConfigDto, 'nivel'>): NivelDeRevision {
  return esNivel(config.nivel) ? config.nivel : 'Manual';
}

export function borradorDe(config: RevisionAutomaticaConfigDto): BorradorRevision {
  return {
    nivel: nivelDe(config),
    umbral: String(Math.round((numeroDe(config.umbralConfianza) ?? UMBRAL_INICIAL / 100) * 100)),
    muestreo: String(config.porcentajeMuestreo),
    tope: config.topeMensualAprobaciones === null ? '' : String(config.topeMensualAprobaciones),
    espera: String(config.esperaIaMinutos),
    tipos: [...config.tiposAutorizados],
    areas: [...config.areasAutorizadas],
  };
}

/**
 * Lo que de verdad se guardaría: cada nivel usa solo lo que muestra. Manual no usa nada; Asistido, la confianza mínima;
 * Automático, todo. Lo que el nivel elegido no muestra queda como estaba guardado, así editar y cambiar de nivel no deja
 * cambios ocultos ni errores que no se ven.
 */
export function efectivo(borrador: BorradorRevision, original: BorradorRevision): BorradorRevision {
  if (borrador.nivel === 'Automatico') return borrador;
  const base: BorradorRevision = { ...original, nivel: borrador.nivel };
  return borrador.nivel === 'Asistido' ? { ...base, umbral: borrador.umbral } : base;
}

function mismoConjunto(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && [...a].sort().join('|') === [...b].sort().join('|');
}

export function hayCambios(original: BorradorRevision, borrador: BorradorRevision): boolean {
  const e = efectivo(borrador, original);
  return (
    e.nivel !== original.nivel ||
    e.umbral !== original.umbral ||
    e.muestreo !== original.muestreo ||
    e.tope !== original.tope ||
    e.espera !== original.espera ||
    !mismoConjunto(e.tipos, original.tipos) ||
    !mismoConjunto(e.areas, original.areas)
  );
}

export type CampoDeRevision = 'umbral' | 'muestreo' | 'tope' | 'espera' | 'tipos' | 'areas';
export type ErroresDeRevision = Partial<Record<CampoDeRevision, 'rango' | 'vacio'>>;

function entre(texto: string, minimo: number, maximo: number): boolean {
  return /^\d+$/.test(texto) && Number(texto) >= minimo && Number(texto) <= maximo;
}

/** Los errores que la API rechazaría (422), dicho antes de enviar y solo de lo que el nivel usa. Sin errores, `{}`. */
export function validar(borrador: BorradorRevision): ErroresDeRevision {
  const errores: ErroresDeRevision = {};
  if (borrador.nivel === 'Manual') return errores;
  if (!entre(borrador.umbral, UMBRAL_MINIMO, 100)) errores.umbral = 'rango';
  if (borrador.nivel === 'Asistido') return errores;
  if (!entre(borrador.muestreo, 0, 100)) errores.muestreo = 'rango';
  if (borrador.tope !== '' && !entre(borrador.tope, 1, MAXIMO_TOPE)) errores.tope = 'rango';
  if (!entre(borrador.espera, 1, MAXIMO_ESPERA)) errores.espera = 'rango';
  if (borrador.tipos.length === 0) errores.tipos = 'vacio';
  if (borrador.areas.length === 0) errores.areas = 'vacio';
  return errores;
}

/** Cuerpo de `PUT /configuracion/revision-automatica` (reemplaza toda la configuración); recibe lo ya `efectivo` y válido. */
export function aDto(borrador: BorradorRevision): ActualizarRevisionAutomaticaDto {
  return {
    nivel: borrador.nivel,
    umbralConfianza: Number(borrador.umbral) / 100,
    porcentajeMuestreo: Number(borrador.muestreo),
    topeMensualAprobaciones: borrador.tope === '' ? null : Number(borrador.tope),
    tiposAutorizados: [...borrador.tipos],
    areasAutorizadas: [...borrador.areas],
    esperaIaMinutos: Number(borrador.espera),
  };
}

/** Umbral que exige el plan de IA de la empresa, en porcentaje entero (la API no deja que el de la empresa quede debajo). */
export function umbralDelPlan(config: Pick<RevisionAutomaticaConfigDto, 'umbralDelPlan'>): number {
  return Math.round((numeroDe(config.umbralDelPlan) ?? UMBRAL_MINIMO / 100) * 100);
}

/**
 * ¿Hay que avisar antes de guardar? Cuando el sistema pasa a aprobar sin que una persona vea los documentos, o cuando lo
 * que ya aprobaba sola crece: más tipos o áreas, menos confianza exigida o menos muestra para confirmar.
 */
export function pideConfirmacion(original: BorradorRevision, borrador: BorradorRevision): boolean {
  const e = efectivo(borrador, original);
  if (e.nivel !== 'Automatico') return false;
  if (original.nivel !== 'Automatico') return true;
  const sumaAlgo = (nuevos: readonly string[], antes: readonly string[]) => nuevos.some((x) => !antes.includes(x));
  return sumaAlgo(e.tipos, original.tipos) || sumaAlgo(e.areas, original.areas) || Number(e.umbral) < Number(original.umbral) || Number(e.muestreo) < Number(original.muestreo);
}
