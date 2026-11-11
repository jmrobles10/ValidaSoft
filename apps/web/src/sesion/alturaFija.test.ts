import { afterEach, describe, expect, it } from 'vitest';
import { ALTO_MINIMO, alturaDisponible, contenedorConScroll, MARGEN_CONTENEDOR, RESERVA_PANTALLA, useAlturaFija, ventanaDelContenedor } from './alturaFija';

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

describe('contenedorConScroll', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('devuelve el ancestro con scroll propio, no los que solo recortan ni el propio elemento', () => {
    document.body.innerHTML = '<div id="a" style="overflow-y: auto"><div id="b" style="overflow: hidden"><div id="c"><span id="hijo"></span></div></div></div>';
    const hijo = document.getElementById('hijo');
    expect(contenedorConScroll(hijo)?.id).toBe('a');
    expect(contenedorConScroll(document.getElementById('a'))).toBeNull();
  });

  it('sin ancestro con scroll desplaza la ventana: null', () => {
    document.body.innerHTML = '<div><div id="x"></div></div>';
    expect(contenedorConScroll(document.getElementById('x'))).toBeNull();
    expect(contenedorConScroll(null)).toBeNull();
  });
});
