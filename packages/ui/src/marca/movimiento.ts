import { useMediaQuery } from '@mui/material';
import { useState } from 'react';

/** La persona pidió menos movimiento (`prefers-reduced-motion`): las animaciones de marca pasan a su versión estática. */
export function useMovimientoReducido(): boolean {
  return useMediaQuery('(prefers-reduced-motion: reduce)');
}

/**
 * `true` solo la primera vez que se pide `clave` en la sesión del navegador (sessionStorage), para que una celebración no se repita
 * cada vez que se vuelve a una pantalla que ya la mostró. Sin almacenamiento (iframes de terceros, modo privado) siempre es `true`.
 */
export function usePrimeraVez(clave: string): boolean {
  const [primera] = useState(() => {
    const llave = `validasoft:celebrado:${clave}`;
    try {
      if (window.sessionStorage.getItem(llave)) return false;
      window.sessionStorage.setItem(llave, '1');
    } catch {
      // Sin almacenamiento no se puede recordar: se celebra.
    }
    return true;
  });
  return primera;
}
