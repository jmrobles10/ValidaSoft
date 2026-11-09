import { afterEach, describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { ALTO_PANEL, anclarOverlays, MARGEN, posicionAnclada, posicionEnRegion, regionVisibleDelIframe, type RegionVisible } from './overlaysEmbebidos';
import { usePuenteEmbed } from './usePuenteEmbed';

describe('posicionAnclada', () => {
  it('centra el elemento en el punto del clic, que es lo que la persona tiene a la vista', () => {
    expect(posicionAnclada(1200, 300, 3000)).toBe(1050);
  });

  it('nunca se sale del iframe: ni por arriba ni por abajo', () => {
    expect(posicionAnclada(40, 300, 3000)).toBe(MARGEN);
    expect(posicionAnclada(2950, 300, 3000)).toBe(3000 - 300 - MARGEN);
  });

  it('sin interacción previa, o si no cabe, arriba con el margen', () => {
    expect(posicionAnclada(null, 300, 3000)).toBe(MARGEN);
    expect(posicionAnclada(500, 700, 640)).toBe(MARGEN);
  });
});

describe('posicionEnRegion', () => {
  it('centra en la parte del iframe que se ve; si no cabe, arriba de esa parte', () => {
    expect(posicionEnRegion({ top: 1000, height: 800 }, 300, 3000)).toBe(1250);
    expect(posicionEnRegion({ top: 1000, height: 400 }, 600, 3000)).toBe(1000 + MARGEN);
    expect(posicionEnRegion({ top: 2800, height: 600 }, 300, 3000)).toBe(3000 - 300 - MARGEN);
  });
});

/** Ventana de un iframe del mismo origen: su borde superior en `top` de un contenedor de `altoContenedor` px. */
function iframe(top: number, alto: number, altoContenedor: number): Window {
  return { frameElement: { getBoundingClientRect: () => ({ top }) }, innerHeight: alto, parent: { innerHeight: altoContenedor } } as unknown as Window;
}

describe('regionVisibleDelIframe', () => {
  it('del mismo origen mide qué parte del iframe estirado cae dentro de la ventana del contenedor', () => {
    expect(regionVisibleDelIframe(iframe(-1500, 3000, 900))).toEqual({ top: 1500, height: 900 });
    expect(regionVisibleDelIframe(iframe(120, 3000, 900))).toEqual({ top: 0, height: 780 });
    expect(regionVisibleDelIframe(iframe(-2600, 3000, 900))).toEqual({ top: 2600, height: 400 });
  });

  it('sin poder medir (otro origen, fuera de un iframe o casi nada a la vista) no inventa una región', () => {
    const otroOrigen = {
      get frameElement(): Element {
        throw new DOMException('cross-origin', 'SecurityError');
      },
    } as unknown as Window;
    expect(regionVisibleDelIframe(otroOrigen)).toBeNull();
    expect(regionVisibleDelIframe({ frameElement: null } as unknown as Window)).toBeNull();
    expect(regionVisibleDelIframe(iframe(800, 3000, 900))).toBeNull();
  });
});

/** Portal como el que MUI agrega a `body`; jsdom no calcula alturas, así que se fija la del papel. */
function portal(clases: string, papel: string, alto: number): HTMLElement {
  const raiz = document.createElement('div');
  raiz.className = clases;
  raiz.innerHTML = `<div class="MuiDialog-container"><div class="${papel}"></div></div>`;
  Object.defineProperty(raiz.querySelector(`.${papel}`), 'offsetHeight', { value: alto });
  return raiz;
}

describe('anclarOverlays', () => {
  let detener: (() => void) | null = null;
  afterEach(() => {
    detener?.();
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  it('centra el diálogo en el control que lo abrió y avisa la región al contenedor', async () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(3000);
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region));

    document.dispatchEvent(new MouseEvent('pointerdown', { clientY: 1800 }));
    const dialogo = portal('MuiDialog-root MuiModal-root', 'MuiDialog-paper', 280);
    document.body.append(dialogo);
    await Promise.resolve();

    const top = 1800 - 140;
    expect(dialogo.querySelector<HTMLElement>('.MuiDialog-paper')?.style.marginTop).toBe(`${String(top)}px`);
    expect(dialogo.querySelector<HTMLElement>('.MuiDialog-container')?.style.alignItems).toBe('flex-start');
    expect(avisos).toEqual([{ top, height: 280 }]);
  });

  it('anclado al clic, el diálogo no pasa del borde inferior del iframe aunque crezca después de abrirse', async () => {
    // Iframe de otro origen y alto fijo (672 px): al marcar un documento el diálogo crece con sus motivos y el botón de
    // confirmar quedaba debajo del borde, inalcanzable. Ahora desplaza por dentro.
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(672);
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region));

    document.dispatchEvent(new MouseEvent('pointerdown', { clientY: 340 }));
    const dialogo = portal('MuiDialog-root MuiModal-root', 'MuiDialog-paper', 300);
    document.body.append(dialogo);
    await Promise.resolve();

    const papel = dialogo.querySelector<HTMLElement>('.MuiDialog-paper');
    const top = 340 - 150;
    expect(papel?.style.marginTop).toBe(`${String(top)}px`);
    expect(papel?.style.maxHeight).toBe(`${String(672 - top - MARGEN)}px`);
    expect(avisos).toEqual([{ top, height: 300 }]);
  });

  it('limita el panel lateral a una altura que cabe en una ventana pequeña', async () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(3000);
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region));

    document.dispatchEvent(new MouseEvent('pointerdown', { clientY: 900 }));
    const panel = portal('MuiDrawer-root MuiDrawer-modal MuiModal-root', 'MuiDrawer-paper', 3000);
    document.body.append(panel);
    await Promise.resolve();

    const papel = panel.querySelector<HTMLElement>('.MuiDrawer-paper');
    expect(papel?.style.top).toBe(`${String(900 - ALTO_PANEL / 2)}px`);
    expect(papel?.style.height).toBe(`${String(ALTO_PANEL)}px`);
    expect(avisos).toEqual([{ top: 900 - ALTO_PANEL / 2, height: ALTO_PANEL }]);
  });

  it('si se sabe qué parte del iframe se ve, centra ahí el diálogo (no junto al clic) y no lo deja pasar de ese alto', async () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(3000);
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region), () => ({ top: 1200, height: 700 }));

    document.dispatchEvent(new MouseEvent('pointerdown', { clientY: 1800 }));
    const dialogo = portal('MuiDialog-root MuiModal-root', 'MuiDialog-paper', 300);
    document.body.append(dialogo);
    await Promise.resolve();

    const papel = dialogo.querySelector<HTMLElement>('.MuiDialog-paper');
    expect(papel?.style.marginTop).toBe(`${String(1200 + (700 - 300) / 2)}px`);
    expect(papel?.style.maxHeight).toBe(`${String(700 - 2 * MARGEN)}px`);
    expect(avisos).toEqual([{ top: 1400, height: 300 }]);
  });

  it('con la parte visible conocida, el panel lateral la ocupa entera', async () => {
    vi.spyOn(window, 'innerHeight', 'get').mockReturnValue(3000);
    detener = anclarOverlays(() => undefined, () => ({ top: 800, height: 760 }));
    const panel = portal('MuiDrawer-root MuiDrawer-modal MuiModal-root', 'MuiDrawer-paper', 3000);
    document.body.append(panel);
    await Promise.resolve();
    const papel = panel.querySelector<HTMLElement>('.MuiDrawer-paper');
    expect(papel?.style.top).toBe('800px');
    expect(papel?.style.height).toBe('760px');
  });

  it('ignora otros portales (menús, avisos)', async () => {
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region));
    document.body.append(portal('MuiPopover-root MuiMenu-root', 'MuiPaper-root', 200));
    await Promise.resolve();
    expect(avisos).toEqual([]);
  });
});

describe('fuera del iframe', () => {
  afterEach(() => {
    document.body.innerHTML = '';
  });

  it('no ancla nada: el diálogo queda como lo centra MUI en la ventana', async () => {
    const { unmount } = renderHook(() => usePuenteEmbed());
    document.dispatchEvent(new MouseEvent('pointerdown', { clientY: 600 }));
    const dialogo = portal('MuiDialog-root MuiModal-root', 'MuiDialog-paper', 280);
    document.body.append(dialogo);
    await Promise.resolve();
    expect(dialogo.querySelector<HTMLElement>('.MuiDialog-paper')?.style.marginTop).toBe('');
    expect(dialogo.querySelector<HTMLElement>('.MuiDialog-container')?.style.alignItems).toBe('');
    unmount();
  });
});
