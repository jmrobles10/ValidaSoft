/**
 * Diálogos y paneles laterales cuando la app está embebida y el contenedor ajusta la altura del iframe a su contenido
 * (`validasoft:resize`). En ese caso el iframe no tiene desplazamiento propio: MUI centra el diálogo en la altura total
 * del contenido y, si la persona bajó en la página del contenedor, queda fuera de la vista.
 *
 * Solución en dos partes, sin depender del contenedor:
 * 1. Se recuerda dónde fue la última interacción (clic o tecla sobre un control), el único punto que se sabe a la
 *    vista, y el diálogo se centra en él; los paneles laterales también, con un alto de {@link ALTO_PANEL} como mucho.
 * 2. Al abrirse se envía `validasoft:visible { top, height }` con la región del iframe que debe verse, para que el
 *    contenedor haga scroll si hace falta (opcional para el contenedor).
 *
 * Se aplica al aparecer el portal de MUI en `body` (un `MutationObserver`), no en la transición: un Drawer que se monta
 * ya abierto no anima y no llamaría a `onEntering`.
 */

/** Margen mínimo entre el diálogo y los bordes del iframe. */
export const MARGEN = 16;
/** Alto máximo de un panel lateral embebido: cabe en una ventana pequeña (800×600 con la barra del navegador). */
export const ALTO_PANEL = 520;

export interface RegionVisible {
  top: number;
  height: number;
}

/**
 * Posición vertical (en px del iframe) del borde superior de un elemento de `alto` px centrado en `puntoY`, sin salirse
 * del iframe; si no cabe o no hubo interacción, arriba con el margen.
 */
export function posicionAnclada(puntoY: number | null, alto: number, altoVentana: number): number {
  const maximo = altoVentana - alto - MARGEN;
  if (puntoY === null || maximo <= MARGEN) return MARGEN;
  return Math.round(Math.min(Math.max(puntoY - alto / 2, MARGEN), maximo));
}

/** Centra un diálogo (`.MuiDialog-root`) en `puntoY` y devuelve la región que ocupa. */
export function anclarDialogo(raiz: Element, puntoY: number | null, altoVentana: number): RegionVisible | null {
  const contenedor = raiz.querySelector<HTMLElement>('.MuiDialog-container');
  const papel = raiz.querySelector<HTMLElement>('.MuiDialog-paper');
  if (!contenedor || !papel) return null;
  const alto = papel.offsetHeight;
  const top = posicionAnclada(puntoY, alto, altoVentana);
  contenedor.style.alignItems = 'flex-start';
  papel.style.marginTop = `${String(top)}px`;
  return { top, height: alto };
}

/** Limita un panel lateral (`.MuiDrawer-modal`) a {@link ALTO_PANEL} y lo centra en `puntoY`. */
export function anclarPanel(raiz: Element, puntoY: number | null, altoVentana: number): RegionVisible | null {
  const papel = raiz.querySelector<HTMLElement>('.MuiDrawer-paper');
  if (!papel) return null;
  const alto = Math.min(altoVentana, ALTO_PANEL);
  const top = alto >= altoVentana ? 0 : posicionAnclada(puntoY, alto, altoVentana);
  papel.style.top = `${String(top)}px`;
  papel.style.height = `${String(alto)}px`;
  return { top, height: alto };
}

/**
 * Sigue clics y teclas para saber dónde está mirando la persona y ancla cada diálogo o panel que se abra; avisa la
 * región con `avisar`. Devuelve cómo dejar de hacerlo.
 */
export function anclarOverlays(avisar: (region: RegionVisible) => void, doc: Document = document): () => void {
  let ancla: number | null = null;
  const alApuntar = (evento: PointerEvent) => {
    ancla = evento.clientY;
  };
  const alTeclear = (evento: KeyboardEvent) => {
    const objetivo = evento.target;
    if (objetivo instanceof Element && objetivo !== doc.body) ancla = objetivo.getBoundingClientRect().top;
  };
  const observador = new MutationObserver((cambios) => {
    const altoVentana = doc.defaultView?.innerHeight ?? 0;
    for (const cambio of cambios) {
      for (const nodo of cambio.addedNodes) {
        if (!(nodo instanceof Element)) continue;
        const region = nodo.classList.contains('MuiDialog-root')
          ? anclarDialogo(nodo, ancla, altoVentana)
          : nodo.classList.contains('MuiDrawer-modal')
            ? anclarPanel(nodo, ancla, altoVentana)
            : null;
        if (region) avisar(region);
      }
    }
  });
  doc.addEventListener('pointerdown', alApuntar, true);
  doc.addEventListener('keydown', alTeclear, true);
  observador.observe(doc.body, { childList: true });
  return () => {
    doc.removeEventListener('pointerdown', alApuntar, true);
    doc.removeEventListener('keydown', alTeclear, true);
    observador.disconnect();
  };
}
