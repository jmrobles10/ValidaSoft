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
  entrar: () => void;
  salir: () => void;
  fijarAltura: (altura: number | null) => void;
}

export const useAlturaFija = create<EstadoAlturaFija>((set) => ({
  paginas: 0,
  altura: null,
  entrar: () => set((s) => ({ paginas: s.paginas + 1 })),
  salir: () => set((s) => ({ paginas: Math.max(0, s.paginas - 1) })),
  fijarAltura: (altura) => set({ altura }),
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
 * Alto que se ve del contenedor desde el borde superior del iframe. Del mismo origen se mide (`frameElement` y la
 * ventana del contenedor); de otro origen no se puede, y se estima con la pantalla disponible. Si el borde superior
 * del iframe no está en la mitad de arriba de la ventana, el contenedor lo subirá al pedirle `visible { top: 0 }`:
 * se mide como si quedara a {@link MARGEN_CONTENEDOR} del borde.
 */
export function alturaDisponible(ventana: Window = window): number {
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
  return Math.round(Math.max(ALTO_MINIMO, medida ?? ventana.screen.availHeight - RESERVA_PANTALLA));
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
