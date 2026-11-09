import { describe, expect, it } from 'vitest';
import { ALTO_MINIMO, alturaDisponible, MARGEN_CONTENEDOR, RESERVA_PANTALLA, useAlturaFija, ventanaDelContenedor } from './alturaFija';

/** Ventana de un iframe del mismo origen cuyo borde superior está en `top` dentro de un contenedor de `alto` px. */
function iframeMismoOrigen(top: number, alto: number): Window {
  return {
    frameElement: { getBoundingClientRect: () => ({ top }) },
    parent: { innerHeight: alto },
    screen: { availHeight: 1040 },
  } as unknown as Window;
}

/** Ventana de un iframe de otro origen: `frameElement` lanza. */
function iframeOtroOrigen(pantalla: number): Window {
  return {
    get frameElement(): Element {
      throw new DOMException('cross-origin', 'SecurityError');
    },
    screen: { availHeight: pantalla },
  } as unknown as Window;
}

describe('alto fijo del iframe embebido', () => {
  it('del mismo origen: lo que se ve del contenedor desde el borde del iframe, menos el margen', () => {
    expect(alturaDisponible(iframeMismoOrigen(62, 720))).toBe(720 - 62 - MARGEN_CONTENEDOR);
  });

  it('si el iframe está más abajo o ya pasó, mide como si el contenedor lo subiera al margen', () => {
    expect(alturaDisponible(iframeMismoOrigen(793, 900))).toBe(900 - 2 * MARGEN_CONTENEDOR);
    expect(alturaDisponible(iframeMismoOrigen(-400, 900))).toBe(900 - 2 * MARGEN_CONTENEDOR);
  });

  it('de otro origen no se puede medir: estima con la pantalla disponible, y nunca baja del mínimo', () => {
    expect(alturaDisponible(iframeOtroOrigen(1040))).toBe(1040 - RESERVA_PANTALLA);
    expect(alturaDisponible(iframeOtroOrigen(600))).toBe(ALTO_MINIMO);
    expect(ventanaDelContenedor(iframeOtroOrigen(1040))).toBeNull();
  });

  it('cuenta las páginas que lo piden: sigue fijo mientras quede una montada', () => {
    const { entrar, salir } = useAlturaFija.getState();
    entrar();
    entrar();
    salir();
    expect(useAlturaFija.getState().paginas).toBe(1);
    salir();
    salir();
    expect(useAlturaFija.getState().paginas).toBe(0);
  });
});
