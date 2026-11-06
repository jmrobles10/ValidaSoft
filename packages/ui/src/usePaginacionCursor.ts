import { useCallback, useState } from 'react';

/**
 * Paginación por cursor opaco (`PaginaDto.siguienteCursor`). La API solo sabe ir hacia adelante, así que para volver
 * se guarda la pila de cursores ya visitados. `null` = primera página.
 */
export interface PaginacionCursor {
  /** Cursor de la página actual (se envía como `cursor`). */
  cursor: string | null;
  /** Número de página, desde 1. */
  pagina: number;
  hayAnterior: boolean;
  irASiguiente: (siguienteCursor: string) => void;
  irAAnterior: () => void;
  /** Vuelve a la primera página (p. ej. al cambiar filtros). */
  reiniciar: () => void;
}

export function usePaginacionCursor(): PaginacionCursor {
  const [pila, setPila] = useState<(string | null)[]>([null]);

  const irASiguiente = useCallback((siguiente: string) => {
    setPila((actual) => (actual.at(-1) === siguiente ? actual : [...actual, siguiente]));
  }, []);

  const irAAnterior = useCallback(() => {
    setPila((actual) => (actual.length > 1 ? actual.slice(0, -1) : actual));
  }, []);

  const reiniciar = useCallback(() => {
    setPila((actual) => (actual.length === 1 ? actual : [null]));
  }, []);

  return {
    cursor: pila.at(-1) ?? null,
    pagina: pila.length,
    hayAnterior: pila.length > 1,
    irASiguiente,
    irAAnterior,
    reiniciar,
  };
}
