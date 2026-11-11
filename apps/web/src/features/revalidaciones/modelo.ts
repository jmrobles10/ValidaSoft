import type { RevalidacionAAprobarDto, RevalidacionDto } from '@validasoft/api-client';
import { seccionFicha } from '../terceros/ficha/secciones';

/** Días desde los que el plazo se marca como urgente. */
export const DIAS_URGENTE = 3;

/** Una revalidación es un documento de un expediente: la clave combina los dos. */
export function claveRevalidacion(r: Pick<RevalidacionDto, 'expedienteId' | 'requisitoId'>): string {
  return `${r.expedienteId}|${r.requisitoId}`;
}

/** Revalidación de una sección de la información del tercero (F2, FM-28): su código es el de la sección. */
export function esRevalidacionDeSeccion(f: Pick<RevalidacionDto, 'tipoDocumentoCodigo'>): boolean {
  return seccionFicha(f.tipoDocumentoCodigo) !== null;
}

/** Las filas de la página que se pueden aprobar en lote (A-18: sin cambios frente a la versión aceptada). */
export function seleccionables(filas: readonly RevalidacionDto[]): RevalidacionDto[] {
  return filas.filter((f) => f.aprobableEnLote);
}

/** Lo que se envía al aprobar: solo lo seleccionado que sigue en la página y sigue siendo aprobable, sobre la versión vista. */
export function lote(filas: readonly RevalidacionDto[], seleccion: ReadonlySet<string>): RevalidacionAAprobarDto[] {
  return seleccionables(filas)
    .filter((f) => seleccion.has(claveRevalidacion(f)))
    .map((f) => ({ expedienteId: f.expedienteId, requisitoId: f.requisitoId, sobreVersion: f.versionActual.numero }));
}

export type TonoPlazo = 'bloqueante' | 'urgente' | 'normal';

export function tonoPlazo(f: Pick<RevalidacionDto, 'bloqueante' | 'diasRestantes'>): TonoPlazo {
  if (f.bloqueante) return 'bloqueante';
  return f.diasRestantes <= DIAS_URGENTE ? 'urgente' : 'normal';
}

/**
 * La revalidación está devuelta al tercero (el documento quedó observado): solo entonces se le puede recordar. El servidor
 * tiene la última palabra: si el expediente ya no está devuelto, lo omite y el resumen del envío dice por qué.
 */
export function esDevuelta(f: Pick<RevalidacionDto, 'estadoRequisito'>): boolean {
  return f.estadoRequisito === 'Observado';
}

/** Los expedientes (sin repetir) de las revalidaciones devueltas que están seleccionadas: a quienes se les recuerda. */
export function devueltosSeleccionados(filas: readonly RevalidacionDto[], seleccion: ReadonlySet<string>): Set<string> {
  return new Set(filas.filter((f) => esDevuelta(f) && seleccion.has(claveRevalidacion(f))).map((f) => f.expedienteId));
}
