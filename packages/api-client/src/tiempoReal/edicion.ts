import { useEffect, useSyncExternalStore } from 'react';

/**
 * Guardia de edición del tiempo real (NT-12): una pantalla con cambios sin guardar declara que está editando un *alcance* (el
 * prefijo de ruta de las consultas que muestra, p. ej. `/public/v1/ficha`). Mientras tanto, un aviso de cambio que tocaría ese
 * alcance no vuelve a pedir los datos —no pisa lo que la persona está escribiendo—: queda *pendiente* y la pantalla muestra «Hay
 * cambios nuevos» con «Actualizar». Al terminar la edición (o al actualizar) el alcance vuelve a refrescarse solo.
 */

const ediciones = new Map<string, number>();
const pendientes = new Set<string>();
const oyentes = new Set<() => void>();
let version = 0;
/** Qué hace el tiempo real con los cambios que quedaron pendientes cuando la edición termina (volver a pedir esos datos). */
let aplicador: ((alcance: string) => void) | null = null;

export function fijarAplicadorDePendientes(aplicar: ((alcance: string) => void) | null): void {
  aplicador = aplicar;
}

function avisar(): void {
  version += 1;
  for (const oyente of oyentes) oyente();
}

/** Declara una edición en `alcance` y devuelve cómo terminarla. Varias ediciones del mismo alcance se cuentan. */
export function registrarEdicion(alcance: string): () => void {
  ediciones.set(alcance, (ediciones.get(alcance) ?? 0) + 1);
  let activa = true;
  return () => {
    if (!activa) return;
    activa = false;
    const quedan = (ediciones.get(alcance) ?? 1) - 1;
    if (quedan > 0) ediciones.set(alcance, quedan);
    else ediciones.delete(alcance);
    // Terminó la edición (guardó, canceló o salió): lo que llegó mientras tanto se aplica solo.
    if (quedan === 0 && pendientes.delete(alcance)) aplicador?.(alcance);
    avisar();
  };
}

export function hayEdicionEn(alcance: string): boolean {
  return (ediciones.get(alcance) ?? 0) > 0;
}

/** Hubo un cambio en `alcance` que no se aplicó porque se estaba editando. */
export function marcarCambiosPendientes(alcance: string): void {
  if (pendientes.has(alcance)) return;
  pendientes.add(alcance);
  avisar();
}

/** Quita el pendiente (la pantalla ya actualizó o la persona descartó lo suyo); `true` si lo había. */
export function resolverCambiosPendientes(alcance: string): boolean {
  const habia = pendientes.delete(alcance);
  if (habia) avisar();
  return habia;
}

function suscribir(oyente: () => void): () => void {
  oyentes.add(oyente);
  return () => oyentes.delete(oyente);
}

/** ¿Hay cambios nuevos sin aplicar en `alcance`? (la pantalla muestra «Hay cambios nuevos»). */
export function useCambiosPendientes(alcance: string): boolean {
  useSyncExternalStore(suscribir, () => version);
  return pendientes.has(alcance);
}

/** Mientras `activa` (hay cambios sin guardar), el alcance no se refresca solo. */
export function useEdicionEnCurso(alcance: string, activa: boolean): void {
  useEffect(() => {
    if (!activa) return undefined;
    return registrarEdicion(alcance);
  }, [alcance, activa]);
}

/** Estado interno para pruebas. */
export function reiniciarEdiciones(): void {
  ediciones.clear();
  pendientes.clear();
  avisar();
}
