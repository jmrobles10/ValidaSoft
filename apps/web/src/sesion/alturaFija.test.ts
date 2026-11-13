import { afterEach, describe, expect, it, vi } from 'vitest';
import {
  ALTO_MINIMO,
  alturaDisponible,
  contenedorConScroll,
  ESPERA_RESPUESTA_MS,
  MARGEN_CONTENEDOR,
  RESERVA_PANTALLA,
  useAlturaFija,
  ventanaDelContenedor,
  vigiarMarco,
  type AltoDelMarco,
} from './alturaFija';

/** Ventana de un iframe del mismo origen cuyo borde superior está en `top` dentro de un contenedor de `alto` px. */
function iframeMismoOrigen(top: number, alto: number): Window {
  return {
    frameElement: { getBoundingClientRect: () => ({ top }) },
    parent: { innerHeight: alto },
    screen: { availHeight: 1040 },
  } as unknown as Window;
}

/** Ventana de un iframe de otro origen: `frameElement` lanza. */
function iframeOtroOrigen(pantalla: number, alto = 0): Window {
  return {
    get frameElement(): Element {
      throw new DOMException('cross-origin', 'SecurityError');
    },
    innerHeight: alto,
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

  it('de otro origen, si el contenedor tiene el iframe de un alto suyo, llena ese alto real y no la estimación (no deja franja vacía ni se pasa)', () => {
    // Pantalla de 720: la estimación daría 480, pero el contenedor dejó el iframe en 672 (la ventana menos su cabecera).
    expect(alturaDisponible(iframeOtroOrigen(720, 672), 'alto-propio')).toBe(672);
    // Un iframe más bajo que el mínimo también se respeta: lo que no cabe se desplaza dentro, no queda cortado afuera.
    expect(alturaDisponible(iframeOtroOrigen(1040, 400), 'alto-propio')).toBe(400);
    // Si el iframe no está a la vista (alto 0), no hay alto real: se estima.
    expect(alturaDisponible(iframeOtroOrigen(1040, 0), 'alto-propio')).toBe(1040 - RESERVA_PANTALLA);
    // Si el contenedor lo estira o aún no se sabe, sigue la estimación.
    expect(alturaDisponible(iframeOtroOrigen(720, 672), 'sigue-resize')).toBe(ALTO_MINIMO);
    expect(alturaDisponible(iframeOtroOrigen(720, 672))).toBe(ALTO_MINIMO);
  });

  it('del mismo origen la medida del contenedor manda, con el marco que sea', () => {
    expect(alturaDisponible(iframeMismoOrigen(62, 720), 'alto-propio')).toBe(720 - 62 - MARGEN_CONTENEDOR);
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

describe('vigiarMarco: ¿el contenedor le hace caso a validasoft:resize?', () => {
  afterEach(() => {
    vi.useRealTimers();
  });

  /** Ventana de pruebas: `innerHeight` y `resize` que el contenedor «dispara» a mano. */
  function montar(altoInicial: number) {
    const ventana = new EventTarget() as unknown as Window & EventTarget;
    const estado = { alto: altoInicial };
    Object.defineProperty(ventana, 'innerHeight', { get: () => estado.alto });
    Object.defineProperty(ventana, 'setTimeout', { value: (f: () => void, ms: number) => globalThis.setTimeout(f, ms) });
    Object.defineProperty(ventana, 'clearTimeout', { value: (id: number) => globalThis.clearTimeout(id) });
    const decisiones: AltoDelMarco[] = [];
    const vigia = vigiarMarco(ventana, (marco) => decisiones.push(marco));
    const contenedorCambia = (alto: number) => {
      estado.alto = alto;
      ventana.dispatchEvent(new Event('resize'));
    };
    return { vigia, decisiones, contenedorCambia };
  }

  it('si el iframe pasa a ser del alto pedido, el contenedor lo estira: sigue-resize', () => {
    vi.useFakeTimers();
    const { vigia, decisiones, contenedorCambia } = montar(480);
    vigia.pedido(800);
    contenedorCambia(800);
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS * 2);
    expect(decisiones).toEqual(['sigue-resize']);
    vigia.detener();
  });

  it('si pasa la espera y el iframe no cambió, el contenedor lo tiene de un alto suyo: alto-propio', () => {
    vi.useFakeTimers();
    const { vigia, decisiones } = montar(672);
    vigia.pedido(480);
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS - 1);
    expect(decisiones).toEqual([]);
    vi.advanceTimersByTime(1);
    expect(decisiones).toEqual(['alto-propio']);
    vigia.detener();
  });

  it('un alto pedido que el iframe ya tiene no enseña nada y no decide', () => {
    vi.useFakeTimers();
    const { vigia, decisiones } = montar(672);
    vigia.pedido(672);
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS * 3);
    expect(decisiones).toEqual([]);
    vigia.detener();
  });

  it('una vez de alto propio, no vuelve atrás por los cambios de tamaño de la ventana del contenedor', () => {
    vi.useFakeTimers();
    const { vigia, decisiones, contenedorCambia } = montar(672);
    vigia.pedido(480);
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS);
    contenedorCambia(560);
    vigia.pedido(560);
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS * 2);
    expect(decisiones).toEqual(['alto-propio']);
    vigia.detener();
  });

  it('detenida, no queda ninguna espera pendiente', () => {
    vi.useFakeTimers();
    const { vigia, decisiones } = montar(672);
    vigia.pedido(480);
    vigia.detener();
    vi.advanceTimersByTime(ESPERA_RESPUESTA_MS * 2);
    expect(decisiones).toEqual([]);
  });
});
