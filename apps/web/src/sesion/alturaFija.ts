import { useEffect } from 'react';
import { create } from 'zustand';

/**
 * Páginas de edición con encabezado fijo dentro del iframe embebido.
 *
 * Embebida, la app avisa su alto con `validasoft:resize` y el contenedor estira el iframe: el iframe no tiene scroll
 * propio y un `position: sticky` no se pega a nada (la ventana del iframe mide todo el contenido). Por eso las páginas
 * largas de edición piden **altura fija**: la app mide lo que se ve del contenedor, se queda de ese alto (y lo avisa
 * con `resize`), `main` pasa a tener scroll propio y el encabezado se pega arriba de él. Con `validasoft:visible
 * { top: 0, height }` el contenedor lleva el iframe a la vista. Fuera del iframe no cambia nada: la ventana desplaza
 * y el encabezado se pega bajo la barra superior.
 */
interface EstadoAlturaFija {
  /** Páginas montadas que piden altura fija (una ruta anidada puede pedirla dos veces). */
  paginas: number;
  /** Alto fijo del documento embebido en px; `null` = el iframe crece con el contenido. */
  altura: number | null;
  /** Qué hace el contenedor (de otro origen) con el alto del iframe; ver {@link vigiarMarco}. */
  marco: AltoDelMarco;
  entrar: () => void;
  salir: () => void;
  fijarAltura: (altura: number | null) => void;
  fijarMarco: (marco: AltoDelMarco) => void;
}

export const useAlturaFija = create<EstadoAlturaFija>((set) => ({
  paginas: 0,
  altura: null,
  marco: 'desconocido',
  entrar: () => set((s) => ({ paginas: s.paginas + 1 })),
  salir: () => set((s) => ({ paginas: Math.max(0, s.paginas - 1) })),
  fijarAltura: (altura) => set({ altura }),
  fijarMarco: (marco) => set({ marco }),
}));

/** La página que lo llama, mientras esté montada, usa altura fija con scroll propio cuando la app está embebida. */
export function useScrollPropio(): void {
  useEffect(() => {
    useAlturaFija.getState().entrar();
    return () => useAlturaFija.getState().salir();
  }, []);
}

/** Margen entre el iframe y los bordes de la ventana del contenedor. */
export const MARGEN_CONTENEDOR = 16;
/** Alto mínimo: por debajo, el encabezado y la barra superior se comerían el contenido. */
export const ALTO_MINIMO = 480;
/** Lo que se reserva de la pantalla para la barra del navegador y la cabecera del contenedor cuando no se puede medir. */
export const RESERVA_PANTALLA = 240;

/**
 * Qué hace el contenedor de otro origen con el alto del iframe, que es lo único que se le puede observar desde dentro:
 * - `sigue-resize`: lo estira al alto que se le avisa con `validasoft:resize` (el caso del SDK); la app se queda del alto pedido.
 * - `alto-propio`: lo deja de un alto suyo (p. ej. toda la ventana bajo su cabecera) y no le hace caso a `resize`; la app
 *   llena ese alto real, sin quedarse corta (una franja vacía abajo) ni pasarse (contenido que queda fuera de la ventana).
 * - `desconocido`: aún no se sabe.
 */
export type AltoDelMarco = 'desconocido' | 'sigue-resize' | 'alto-propio';

/** Diferencia de px que se tolera entre el alto pedido y el que el contenedor le dio al iframe (redondeos). */
export const TOLERANCIA_ALTO = 2;
/** Cuánto se espera a que el contenedor cambie el tamaño del iframe tras un `resize` antes de darlo por de alto propio. */
export const ESPERA_RESPUESTA_MS = 800;

/**
 * Alto que se ve del contenedor desde el borde superior del iframe. Del mismo origen se mide (`frameElement` y la
 * ventana del contenedor); de otro origen no se puede, y se usa el alto real del iframe si el contenedor lo fija
 * ({@link AltoDelMarco} `alto-propio`) o, si no, se estima con la pantalla disponible. Si el borde superior
 * del iframe no está en la mitad de arriba de la ventana, el contenedor lo subirá al pedirle `visible { top: 0 }`:
 * se mide como si quedara a {@link MARGEN_CONTENEDOR} del borde.
 */
export function alturaDisponible(ventana: Window = window, altoDelMarco: AltoDelMarco = 'desconocido'): number {
  let medida: number | null = null;
  try {
    const marco = ventana.frameElement;
    if (marco) {
      const altoVentana = ventana.parent.innerHeight;
      const top = marco.getBoundingClientRect().top;
      const arriba = top >= MARGEN_CONTENEDOR && top <= altoVentana / 2 ? top : MARGEN_CONTENEDOR;
      medida = altoVentana - arriba - MARGEN_CONTENEDOR;
    }
  } catch {
    medida = null; // otro origen: `frameElement` lanza o devuelve null
  }
  // Alto 0 = el iframe no está a la vista o no se ha dibujado: ahí no hay un alto real del que fiarse.
  if (medida === null && altoDelMarco === 'alto-propio' && ventana.innerHeight > 0) return Math.round(ventana.innerHeight);
  return Math.round(Math.max(ALTO_MINIMO, medida ?? ventana.screen.availHeight - RESERVA_PANTALLA));
}

export interface VigiaDelMarco {
  /** Avisa que se pidió (con `validasoft:resize`) este alto: si el iframe no cambia de tamaño, el contenedor no le hace caso. */
  pedido: (altura: number) => void;
  detener: () => void;
}

/**
 * Descubre, desde dentro del iframe, si el contenedor de otro origen le hace caso a `validasoft:resize`. Tras cada alto
 * pedido que no sea el que ya tiene el iframe espera {@link ESPERA_RESPUESTA_MS}: si el iframe cambia de tamaño (evento
 * `resize` de su ventana) y queda del alto pedido, el contenedor lo estira (`sigue-resize`); si no pasa nada, el
 * contenedor lo tiene de un alto suyo (`alto-propio`) y desde entonces se sigue el alto real de la ventana del iframe.
 * Avisa a `alDecidir` cada vez que cambia lo que se sabe.
 */
export function vigiarMarco(ventana: Window, alDecidir: (marco: AltoDelMarco) => void): VigiaDelMarco {
  let marco: AltoDelMarco = 'desconocido';
  let pedida: number | null = null;
  let espera: number | undefined;

  const decidir = (nuevo: AltoDelMarco) => {
    if (nuevo === marco) return;
    marco = nuevo;
    alDecidir(marco);
  };
  const cancelarEspera = () => {
    if (espera === undefined) return;
    ventana.clearTimeout(espera);
    espera = undefined;
  };
  const alRedimensionar = () => {
    if (marco === 'alto-propio' || pedida === null) return;
    cancelarEspera();
    if (Math.abs(ventana.innerHeight - pedida) <= TOLERANCIA_ALTO) decidir('sigue-resize');
  };
  ventana.addEventListener('resize', alRedimensionar);

  return {
    pedido: (altura) => {
      pedida = altura;
      cancelarEspera();
      if (marco === 'alto-propio' || Math.abs(ventana.innerHeight - altura) <= TOLERANCIA_ALTO) return;
      espera = ventana.setTimeout(() => {
        espera = undefined;
        decidir('alto-propio');
      }, ESPERA_RESPUESTA_MS);
    },
    detener: () => {
      cancelarEspera();
      ventana.removeEventListener('resize', alRedimensionar);
    },
  };
}

/** Ventana del contenedor si es del mismo origen (para seguir su tamaño); `null` si no se puede. */
export function ventanaDelContenedor(ventana: Window = window): Window | null {
  try {
    return ventana.frameElement ? ventana.parent : null;
  } catch {
    return null;
  }
}

/**
 * El ancestro más cercano de `elemento` con scroll propio (`overflow-y: auto | scroll`); `null` si lo que se desplaza es la
 * ventana. Lo usan las listas virtuales para saber qué observar: embebida con altura fija es el `main` de la app.
 */
export function contenedorConScroll(elemento: HTMLElement | null): HTMLElement | null {
  for (let padre = elemento?.parentElement ?? null; padre && padre !== document.body && padre !== document.documentElement; padre = padre.parentElement) {
    const overflow = getComputedStyle(padre).overflowY;
    if (overflow === 'auto' || overflow === 'scroll') return padre;
  }
  return null;
}
