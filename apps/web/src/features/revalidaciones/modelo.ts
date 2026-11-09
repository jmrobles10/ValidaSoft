import type { RevalidacionAAprobarDto, RevalidacionDto } from '@validasoft/api-client';

/** Días desde los que el plazo se marca como urgente. */
export const DIAS_URGENTE = 3;

/** Una revalidación es un documento de un expediente: la clave combina los dos. */
export function claveRevalidacion(r: Pick<RevalidacionDto, 'expedienteId' | 'requisitoId'>): string {
  return `${r.expedienteId}|${r.requisitoId}`;
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
