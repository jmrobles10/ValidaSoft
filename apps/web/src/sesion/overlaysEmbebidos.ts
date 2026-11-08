/**
 * Diálogos y paneles laterales cuando la app está embebida y el contenedor ajusta la altura del iframe a su contenido
 * (`validasoft:resize`). En ese caso el iframe no tiene desplazamiento propio: MUI centra el diálogo en la altura total
 * del contenido y, si la persona bajó en la página del contenedor, queda fuera de la vista.
 *
 * Solución en dos partes, sin depender del contenedor:
 * 1. Si se sabe qué parte del iframe se ve en la ventana del contenedor (mismo origen, o altura fija con el iframe
 *    llevado a la vista), el diálogo se centra en esa parte y no pasa de su alto (desplaza por dentro); los paneles
 *    laterales la ocupan entera. Si no se sabe (otro origen con el iframe estirado), se centra en la última
 *    interacción (clic o tecla sobre un control), el único punto que se sabe a la vista; los paneles, con un alto de
 *    {@link ALTO_PANEL} como mucho.
 * 2. Al abrirse se envía `validasoft:visible { top, height }` con la región del iframe que debe verse, para que el
 *    contenedor haga scroll si hace falta (opcional para el contenedor).
 *
 * Fuera del iframe no se instala: MUI centra en la ventana, que es lo que se ve.
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

/** Alto mínimo de la parte visible para centrar ahí; por debajo, mejor que el contenedor desplace. */
const ALTO_MINIMO_REGION = 240;

/**
 * Borde superior de un elemento de `alto` px centrado en la región visible; si no cabe, arriba de la región con el
 * margen. Nunca se sale del iframe.
 */
export function posicionEnRegion(region: RegionVisible, alto: number, altoVentana: number): number {
  const top = alto + 2 * MARGEN <= region.height ? region.top + (region.height - alto) / 2 : region.top + MARGEN;
  return Math.round(Math.min(Math.max(top, MARGEN), Math.max(MARGEN, altoVentana - alto - MARGEN)));
}

/**
 * Parte del iframe que se ve en la ventana del contenedor (px del iframe). Solo se puede medir del mismo origen;
 * `null` si es de otro origen, si la página no está en un iframe o si se ve muy poco del iframe.
 */
export function regionVisibleDelIframe(ventana: Window = window): RegionVisible | null {
  try {
    const marco = ventana.frameElement;
    if (!marco) return null;
    const top = marco.getBoundingClientRect().top;
    const desde = Math.max(0, -top);
    const hasta = Math.min(ventana.innerHeight, ventana.parent.innerHeight - top);
    return hasta - desde >= ALTO_MINIMO_REGION ? { top: Math.round(desde), height: Math.round(hasta - desde) } : null;
  } catch {
    return null; // otro origen: `frameElement` lanza
  }
}

/** Dónde ubicar un diálogo o panel: la región visible si se conoce; si no, el punto de la última interacción. */
export type Ancla = { region: RegionVisible } | { puntoY: number | null };

/** Centra un diálogo (`.MuiDialog-root`) en la parte visible (o en el punto) y devuelve la región que ocupa. */
export function anclarDialogo(raiz: Element, ancla: Ancla, altoVentana: number): RegionVisible | null {
  const contenedor = raiz.querySelector<HTMLElement>('.MuiDialog-container');
  const papel = raiz.querySelector<HTMLElement>('.MuiDialog-paper');
  if (!contenedor || !papel) return null;
  if ('region' in ancla) papel.style.maxHeight = `${String(ancla.region.height - 2 * MARGEN)}px`;
  const alto = papel.offsetHeight;
  const top = 'region' in ancla ? posicionEnRegion(ancla.region, alto, altoVentana) : posicionAnclada(ancla.puntoY, alto, altoVentana);
  contenedor.style.alignItems = 'flex-start';
  papel.style.marginTop = `${String(top)}px`;
  return { top, height: alto };
}

/** Un panel lateral (`.MuiDrawer-modal`) ocupa la parte visible; sin ella, {@link ALTO_PANEL} centrado en el punto. */
export function anclarPanel(raiz: Element, ancla: Ancla, altoVentana: number): RegionVisible | null {
  const papel = raiz.querySelector<HTMLElement>('.MuiDrawer-paper');
  if (!papel) return null;
  let top: number;
  let alto: number;
  if ('region' in ancla) {
    alto = Math.min(altoVentana, ancla.region.height);
    top = Math.min(Math.max(0, ancla.region.top), altoVentana - alto);
  } else {
    alto = Math.min(altoVentana, ALTO_PANEL);
    top = alto >= altoVentana ? 0 : posicionAnclada(ancla.puntoY, alto, altoVentana);
  }
  papel.style.top = `${String(top)}px`;
  papel.style.height = `${String(alto)}px`;
  return { top, height: alto };
}

/**
 * Ancla cada diálogo o panel que se abra a la parte visible del iframe (`regionVisible`) o, si no se conoce, a la
 * última interacción (clic o tecla); avisa la región con `avisar`. Devuelve cómo dejar de hacerlo.
 */
export function anclarOverlays(
  avisar: (region: RegionVisible) => void,
  regionVisible: () => RegionVisible | null = () => null,
  doc: Document = document,
): () => void {
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
        const esDialogo = nodo.classList.contains('MuiDialog-root');
        if (!esDialogo && !nodo.classList.contains('MuiDrawer-modal')) continue;
        const visible = regionVisible();
        const donde: Ancla = visible ? { region: visible } : { puntoY: ancla };
        const region = esDialogo ? anclarDialogo(nodo, donde, altoVentana) : anclarPanel(nodo, donde, altoVentana);
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
