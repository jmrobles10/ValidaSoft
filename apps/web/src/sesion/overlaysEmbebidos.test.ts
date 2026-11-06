import { afterEach, describe, expect, it, vi } from 'vitest';
import { ALTO_PANEL, anclarOverlays, MARGEN, posicionAnclada, type RegionVisible } from './overlaysEmbebidos';

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

  it('ignora otros portales (menús, avisos)', async () => {
    const avisos: RegionVisible[] = [];
    detener = anclarOverlays((region) => avisos.push(region));
    document.body.append(portal('MuiPopover-root MuiMenu-root', 'MuiPaper-root', 200));
    await Promise.resolve();
    expect(avisos).toEqual([]);
  });
});
