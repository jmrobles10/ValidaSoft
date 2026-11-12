import type { CampoCruzadoDto, DocumentoPorDecidirDto, ExpedienteDto, RequisitoExpedienteDto } from '@validasoft/api-client';
import { requisitosDe } from './requisitos';

/**
 * Cruce sección ↔ documento en la revisión (F5, FM-8, FM-13). Todo lo calcula la API: aquí solo se ordena para mostrarlo y se
 * replica la guarda de la aprobación (una discrepancia crítica pide justificación) para no ofrecer lo que la API rechazaría.
 */

/** Tono de cada resultado del cruce: coincide en verde, difiere en ámbar (rojo si es crítico), sin verificar en gris. */
export type TonoCruce = 'exito' | 'advertencia' | 'error' | 'neutro';

export function tonoDeCampo(campo: Pick<CampoCruzadoDto, 'resultado' | 'critico'>): TonoCruce {
  if (campo.resultado === 'Coincide') return 'exito';
  if (campo.resultado === 'Discrepa') return campo.critico ? 'error' : 'advertencia';
  return 'neutro';
}

const RESULTADOS = ['Coincide', 'Discrepa', 'NoVerificable'] as const;
export type ResultadoCruce = (typeof RESULTADOS)[number];
export const esResultadoCruce = (valor: string): valor is ResultadoCruce => (RESULTADOS as readonly string[]).includes(valor);

/** Confianza de la lectura en porcentaje es-CO («97 %»). */
export function porcentaje(valor: number): string {
  return new Intl.NumberFormat('es-CO', { style: 'percent', maximumFractionDigits: 0 }).format(valor);
}

/** Largo mínimo de la justificación para aprobar con una discrepancia crítica (`Expediente.LongitudMinimaJustificacion`). */
export const LONGITUD_MINIMA_JUSTIFICACION = 10;

/** ¿El cruce que ve la empresa tiene diferencias en campos críticos? Un requisito de sección, un documento que respalda secciones o la certificación frente a las cuentas. */
export function tieneDiscrepanciaCritica(requisito: Pick<RequisitoExpedienteDto, 'seccion' | 'soporteDeSecciones'>): boolean {
  return requisito.seccion?.cruce?.discrepanciaCritica === true || requisito.soporteDeSecciones?.discrepanciaCritica === true;
}

/** ¿La sección cambió un campo crítico (identidad o representante legal) sin un documento nuevo que la respalde? (FM-15, FD-4). */
export function tieneSinSoporteCritico(requisito: Pick<RequisitoExpedienteDto, 'seccion'>): boolean {
  return requisito.seccion?.sinSoporte?.critico === true;
}

/** Aprobarlo exige una justificación: el cruce difiere en campos críticos o el cambio crítico no trae un documento nuevo. */
export function requiereJustificacion(requisito: Pick<RequisitoExpedienteDto, 'seccion' | 'soporteDeSecciones'>): boolean {
  return tieneDiscrepanciaCritica(requisito) || tieneSinSoporteCritico(requisito);
}

/** De lo que la persona aprobaría de una vez, los requisitos que piden justificación (FM-13, FM-15). */
export function conDiscrepanciaCritica(expediente: Pick<ExpedienteDto, 'requisitos'>, porDecidir: readonly Pick<DocumentoPorDecidirDto, 'requisitoId'>[]): RequisitoExpedienteDto[] {
  const ids = new Set(porDecidir.map((d) => d.requisitoId));
  return requisitosDe(expediente).filter((r) => ids.has(r.id) && requiereJustificacion(r));
}

/** ¿La justificación alcanza para aprobar con discrepancias? */
export function justificacionSuficiente(texto: string): boolean {
  return texto.trim().length >= LONGITUD_MINIMA_JUSTIFICACION;
}
