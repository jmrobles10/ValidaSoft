import { act, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

/** Nombre (`data-testid`) del contenedor con scroll propio que simulan las pruebas de la lista virtual. */
export const CONTENEDOR_CON_SCROLL = 'main';
const ALTO_DEL_DOCUMENTO = 100_000;

/**
 * jsdom no calcula el diseño y el virtualizador necesita medidas: cada fila «mide» `altoFila`, el contenedor con scroll
 * propio `altoContenedor` (con mucho por desplazar), la ventana 768 (la de jsdom) con un documento alto, y la lista
 * queda a cero de lo que se desplazó (sin nada encima). Sin esto cualquier desplazamiento se toma por un rebote.
 * Llamarla en `beforeEach` y `vi.restoreAllMocks()` en `afterEach`.
 */
export function simularDisenoDeLista({ altoFila = 100, altoContenedor = 500 }: { altoFila?: number; altoContenedor?: number } = {}) {
  const esContenedor = (e: Element) => e.getAttribute('data-testid') === CONTENEDOR_CON_SCROLL;
  Object.defineProperty(window, 'scrollY', { value: 0, configurable: true });
  vi.spyOn(HTMLElement.prototype, 'offsetHeight', 'get').mockImplementation(function (this: HTMLElement) {
    return this.dataset.index === undefined ? altoContenedor : altoFila;
  });
  vi.spyOn(Element.prototype, 'clientHeight', 'get').mockImplementation(function (this: Element) {
    return esContenedor(this) ? altoContenedor : 0;
  });
  vi.spyOn(HTMLElement.prototype, 'getBoundingClientRect').mockImplementation(function (this: HTMLElement) {
    const raiz = this.closest<HTMLElement>(`[data-testid="${CONTENEDOR_CON_SCROLL}"]`);
    const top = this === raiz ? 0 : -(raiz ? raiz.scrollTop : window.scrollY);
    return { top, bottom: top, left: 0, right: 0, width: 0, height: 0, x: 0, y: top, toJSON: () => ({}) };
  });
  vi.spyOn(Element.prototype, 'scrollHeight', 'get').mockImplementation(function (this: Element) {
    return esContenedor(this) || this === document.documentElement ? ALTO_DEL_DOCUMENTO : 0;
  });
}

/** Desplaza la ventana a `scrollY` y avisa a quien escucha el evento `scroll`. */
export function desplazarVentana(scrollY: number) {
  act(() => {
    Object.defineProperty(window, 'scrollY', { value: scrollY, configurable: true });
    fireEvent.scroll(window);
  });
}
