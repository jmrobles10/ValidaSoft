import type { EntradaCatalogoDto } from '@validasoft/api-client';
import { sinTildes } from '../api';

/** Un nodo del catálogo de especialidades: grupo (1) → categoría (2) → especialidad (3). */
export interface Nodo {
  codigo: string;
  nombre: string;
  nivel: number;
  hijos: Nodo[];
}

/** Arma el árbol de 3 niveles a partir de las entradas planas (`padre`, `nivel`); los grupos sin padre son la raíz. */
export function armarArbol(entradas: readonly EntradaCatalogoDto[]): Nodo[] {
  const porCodigo = new Map<string, Nodo>();
  for (const e of entradas) porCodigo.set(e.codigo, { codigo: e.codigo, nombre: e.nombre, nivel: e.nivel ?? 1, hijos: [] });
  const raiz: Nodo[] = [];
  for (const e of entradas) {
    const nodo = porCodigo.get(e.codigo);
    if (!nodo) continue;
    const padre = e.padre ? porCodigo.get(e.padre) : undefined;
    if (padre) padre.hijos.push(nodo);
    else if (nodo.nivel === 1) raiz.push(nodo);
  }
  const ordenar = (nodos: Nodo[]) => {
    nodos.sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'));
    nodos.forEach((n) => ordenar(n.hijos));
  };
  ordenar(raiz);
  return raiz;
}

/**
 * Lo que coincide con la búsqueda (sin tildes): un grupo o categoría que coincide trae todo lo suyo; si no, solo las
 * ramas con especialidades que coinciden.
 */
export function filtrarArbol(nodos: readonly Nodo[], texto: string): Nodo[] {
  const buscado = sinTildes(texto.trim());
  if (!buscado) return [...nodos];
  return nodos.flatMap((nodo) => {
    if (sinTildes(nodo.nombre).includes(buscado)) return [nodo];
    const hijos = filtrarArbol(nodo.hijos, texto);
    return hijos.length > 0 ? [{ ...nodo, hijos }] : [];
  });
}

/** Especialidades (nivel 3) bajo un nodo, para contar cuántas tiene elegidas cada grupo. */
export function hojas(nodo: Nodo): Nodo[] {
  return nodo.nivel === 3 || nodo.hijos.length === 0 ? [nodo] : nodo.hijos.flatMap(hojas);
}
