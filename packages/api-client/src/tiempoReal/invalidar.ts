import type { QueryClient } from '@tanstack/react-query';
import type { CambioEnVivo, RecursoDeCambio } from './cambios';
import { hayEdicionEn, marcarCambiosPendientes, resolverCambiosPendientes } from './edicion';

/** Qué consultas (por prefijo de ruta de su clave) refresca cada tipo de recurso que cambia. */
export type MapaDeInvalidacion = Readonly<Record<RecursoDeCambio, readonly string[]>>;

/** Varios avisos seguidos del mismo prefijo (un lote de eventos) se juntan en una sola recarga. */
export const RETARDO_DE_INVALIDACION = 250;

const temporizadores = new WeakMap<QueryClient, Map<string, ReturnType<typeof setTimeout>>>();

function delPrefijo(prefijo: string) {
  return (consulta: { queryKey: readonly unknown[] }) => {
    const primera = consulta.queryKey[0];
    return typeof primera === 'string' && primera.startsWith(prefijo);
  };
}

/** Vuelve a pedir ya las consultas activas del prefijo (las inactivas quedan caducadas para su próximo uso). */
export function invalidarAlcance(cliente: QueryClient, prefijo: string): Promise<void> {
  return cliente.invalidateQueries({ predicate: delPrefijo(prefijo) });
}

/**
 * Refresca el prefijo respetando la guardia de edición: si la pantalla está editando ese alcance, no toca los datos y deja el aviso
 * «Hay cambios nuevos»; si no, vuelve a pedirlos tras un instante (junta los avisos seguidos).
 */
export function invalidarPrefijo(cliente: QueryClient, prefijo: string, retardo = RETARDO_DE_INVALIDACION): void {
  if (hayEdicionEn(prefijo)) {
    marcarCambiosPendientes(prefijo);
    return;
  }
  const propios = temporizadores.get(cliente) ?? new Map<string, ReturnType<typeof setTimeout>>();
  temporizadores.set(cliente, propios);
  if (propios.has(prefijo)) return;
  propios.set(
    prefijo,
    setTimeout(() => {
      propios.delete(prefijo);
      // Pudo empezar una edición mientras se esperaba.
      if (hayEdicionEn(prefijo)) marcarCambiosPendientes(prefijo);
      else void invalidarAlcance(cliente, prefijo);
    }, retardo),
  );
}

/** Un aviso de cambio: refresca lo que muestra la pantalla de ese recurso. */
export function invalidarPorCambio(cliente: QueryClient, cambio: CambioEnVivo, mapa: MapaDeInvalidacion, retardo?: number): void {
  for (const prefijo of mapa[cambio.recurso]) invalidarPrefijo(cliente, prefijo, retardo);
}

/** Todo lo que el mapa refresca (al reconectar o en el sondeo de respaldo, cuando no se sabe qué se perdió). */
export function invalidarTodoElMapa(cliente: QueryClient, mapa: MapaDeInvalidacion, retardo?: number): void {
  for (const prefijo of new Set(Object.values(mapa).flat())) invalidarPrefijo(cliente, prefijo, retardo);
}

/** «Actualizar» del aviso «Hay cambios nuevos»: la persona decidió tomar lo nuevo (descarta lo que tenía sin guardar). */
export function aplicarCambiosPendientes(cliente: QueryClient, prefijo: string): Promise<void> {
  resolverCambiosPendientes(prefijo);
  return invalidarAlcance(cliente, prefijo);
}
