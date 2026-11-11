import { useLayoutEffect, useRef, useState, type RefObject } from 'react';
import { flushSync } from 'react-dom';
import type { MedidasPestanas } from './pestanasAdaptativas';

function iguales(a: MedidasPestanas | null, b: MedidasPestanas): boolean {
  return a !== null && a.anchoMas === b.anchoMas && a.disponible === b.disponible && a.anchos.length === b.anchos.length && a.anchos.every((ancho, i) => ancho === b.anchos[i]);
}

/** Medidas a décimas de píxel: los subpíxeles de un zoom o de una fuente al cargar no deben provocar renders de más. */
const aDecimas = (n: number) => Math.round(n * 10) / 10;

/**
 * Mide la barra de pestañas una sola vez por cambio de tamaño: los anchos de todas las pestañas (y del botón «Más») en
 * una fila oculta que no afecta el diseño, y el ancho que tiene la barra. Nada de lo que se muestra alimenta estas
 * medidas (la barra ocupa lo que sobra del encabezado, no lo que contiene), así que no hay bucles.
 *
 * Los hijos de `refFila` son, en orden, las pestañas y al final el botón «Más». `firma` cambia cuando cambian las
 * pestañas o sus textos (hay que volver a medir). Sin layout (jsdom, un contenedor oculto) o sin `ResizeObserver` no hay
 * medidas y se muestran todas las pestañas; las pruebas pueden inyectarlas con `inyectadas`.
 */
export function useMedidasPestanas(
  firma: string,
  inyectadas?: MedidasPestanas,
): { refBarra: RefObject<HTMLElement | null>; refFila: RefObject<HTMLElement | null>; medidas: MedidasPestanas | null } {
  const refBarra = useRef<HTMLElement>(null);
  const refFila = useRef<HTMLElement>(null);
  const [medidas, setMedidas] = useState<MedidasPestanas | null>(null);

  useLayoutEffect(() => {
    const barra = refBarra.current;
    const fila = refFila.current;
    if (inyectadas || !barra || !fila || typeof ResizeObserver === 'undefined') return;
    const medir = (sincrono: boolean) => {
      const anchos = Array.from(fila.children, (hijo) => aDecimas(hijo.getBoundingClientRect().width));
      const anchoMas = anchos.pop() ?? 0;
      const disponible = aDecimas(barra.getBoundingClientRect().width);
      const siguiente: MedidasPestanas | null = disponible > 0 && anchoMas > 0 ? { anchos, anchoMas, disponible } : null;
      const actualizar = () => setMedidas((previas) => (siguiente === null ? null : iguales(previas, siguiente) ? previas : siguiente));
      // Antes de pintar: sin esto, al estrechar la ventana se verían un instante pestañas cortadas.
      if (sincrono) flushSync(actualizar);
      else actualizar();
    };
    medir(false);
    const observador = new ResizeObserver(() => medir(true));
    observador.observe(barra);
    observador.observe(fila);
    return () => observador.disconnect();
  }, [firma, inyectadas]);

  return { refBarra, refFila, medidas: inyectadas ?? medidas };
}
