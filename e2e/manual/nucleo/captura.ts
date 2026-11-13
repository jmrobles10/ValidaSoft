import type { Frame, Locator, Page } from '@playwright/test';
import { normalizarRuta } from './validacion';

/**
 * Pantallazos del manual: esperan a que la pantalla termine de cargar, resaltan lo que se usa en el paso con un recuadro que solo existe
 * mientras se captura, y manejan las dos formas en que se ve la app:
 *
 * - la del tercero (`apps/public`), que ocupa toda la ventana;
 * - la de la empresa (`apps/web`), que corre en el iframe `#validasoft` de la página del sistema integrado (`soporte/embebido.ts`):
 *   se captura solo el iframe, sin la barra de la página anfitriona, con el tamaño que pide el paso.
 *
 * El celular (390 × 844) cambia el tamaño de la ventana de la MISMA página y la deja como estaba: no se abre otro contexto, así la
 * sesión, lo ya escrito y lo que está abierto se conservan.
 */

export const ESCRITORIO = { ancho: 1280, alto: 800 } as const;
export const CELULAR = { ancho: 390, alto: 844 } as const;

const COLOR = '#ff5c00';
const HALO = 'rgba(255, 92, 0, 0.28)';

/** Lo que cuenta como «todavía cargando»: skeletons, spinners, barras indeterminadas y regiones marcadas como ocupadas. */
const CARGANDO = [
  '.MuiSkeleton-root',
  '.MuiCircularProgress-root',
  '.MuiLinearProgress-root:not([aria-valuenow])',
  '[role="progressbar"]:not([aria-valuenow])',
  '[aria-busy="true"]',
].join(',');

export interface OpcionesDeCaptura {
  ancho: number;
  alto: number;
  completa: boolean;
  resaltar: Locator[];
  esperarCarga: boolean;
  /** Deja visibles los avisos (snackbars) que haya en pantalla; por omisión se ocultan mientras se captura. */
  avisos: boolean;
  /** No mueve el puntero a la esquina antes de capturar (para mostrar un tooltip o un menú que se abre con el puntero). */
  mantenerPuntero: boolean;
  /** En el celular un elemento puede no existir (menú distinto): se captura sin resaltarlo en vez de fallar. */
  resaltadoOpcional: boolean;
}

export interface ResultadoDeCaptura {
  resaltados: number;
}

function marcos(page: Page): Frame[] {
  return page.frames();
}

/** Espera a que ningún marco de la página muestre skeletons ni spinners (con una segunda revisión por las consultas encadenadas). */
export async function esperarCarga(page: Page, tiempo = 25_000): Promise<void> {
  const limite = Date.now() + tiempo;
  for (let vuelta = 0; vuelta < 2; vuelta++) {
    for (const marco of marcos(page)) {
      const restante = Math.max(1000, limite - Date.now());
      try {
        await marco.waitForFunction(
          (selector) => ![...document.querySelectorAll(selector)].some((el) => el.getClientRects().length > 0 && getComputedStyle(el).visibility !== 'hidden'),
          CARGANDO,
          { timeout: restante, polling: 100 },
        );
        await marco.evaluate(
          () => Promise.all([document.fonts.ready, new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))]).then(() => undefined),
        );
      } catch (error) {
        if (marco.isDetached()) continue;
        throw new Error(
          `La pantalla sigue mostrando un skeleton o un indicador de carga tras ${String(Math.round(tiempo / 1000))} s. Espera en el recorrido a que aparezca el contenido ` +
            `(expect(...).toBeVisible()) o, si el paso muestra la carga a propósito, usa esperarCarga: false. (${error instanceof Error ? error.message.split('\n')[0] ?? '' : ''})`,
        );
      }
    }
    // Un respiro para las consultas que empiezan cuando termina la anterior.
    await page.waitForTimeout(vuelta === 0 ? 150 : 0);
  }
}

/** Dibuja el recuadro (y el número, con varios elementos) sobre cada elemento visible; devuelve cuántos resaltó. */
async function resaltar(localizadores: Locator[], completa: boolean): Promise<number> {
  let total = 0;
  for (const [indice, localizador] of localizadores.entries()) {
    const numero = localizadores.length > 1 ? indice + 1 : 0;
    total += await localizador.evaluateAll(
      (elementos, datos) => {
        let hechos = 0;
        for (const el of elementos) {
          if (el.getClientRects().length === 0) continue;
          const caja = el.getBoundingClientRect();
          if (caja.width === 0 || caja.height === 0) continue;
          const doc = el.ownerDocument;
          const vista = doc.defaultView;
          if (!vista) continue;
          const margen = 4;
          const radio = parseFloat(vista.getComputedStyle(el).borderTopLeftRadius) || 0;
          const dx = datos.completa ? vista.scrollX : 0;
          const dy = datos.completa ? vista.scrollY : 0;
          const marco = doc.createElement('div');
          marco.setAttribute('data-manual-resaltado', '');
          Object.assign(marco.style, {
            position: datos.completa ? 'absolute' : 'fixed',
            left: `${String(caja.left + dx - margen)}px`,
            top: `${String(caja.top + dy - margen)}px`,
            width: `${String(caja.width + margen * 2)}px`,
            height: `${String(caja.height + margen * 2)}px`,
            border: `3px solid ${datos.color}`,
            borderRadius: `${String(radio + margen)}px`,
            boxShadow: `0 0 0 6px ${datos.halo}`,
            boxSizing: 'border-box',
            pointerEvents: 'none',
            zIndex: '2147483647',
          });
          doc.documentElement.appendChild(marco);
          if (datos.numero > 0) {
            const insignia = doc.createElement('div');
            insignia.setAttribute('data-manual-resaltado', '');
            insignia.textContent = String(datos.numero);
            Object.assign(insignia.style, {
              position: datos.completa ? 'absolute' : 'fixed',
              left: `${String(Math.max(2, caja.left + dx - margen - 12))}px`,
              top: `${String(Math.max(2, caja.top + dy - margen - 14))}px`,
              width: '26px',
              height: '26px',
              borderRadius: '13px',
              background: datos.color,
              color: '#fff',
              font: '700 14px/26px system-ui, sans-serif',
              textAlign: 'center',
              pointerEvents: 'none',
              zIndex: '2147483647',
              boxShadow: '0 1px 4px rgba(0,0,0,.35)',
            });
            doc.documentElement.appendChild(insignia);
          }
          hechos++;
        }
        return hechos;
      },
      { numero, completa, color: COLOR, halo: HALO },
    );
  }
  return total;
}

/** Oculta los avisos emergentes (snackbars): tapan contenido y desaparecen solos, así que no deben depender del momento de la captura. */
async function ocultarAvisos(page: Page): Promise<void> {
  for (const marco of marcos(page)) {
    await marco
      .evaluate(() => {
        const estilo = document.createElement('style');
        estilo.setAttribute('data-manual-oculto', '');
        estilo.textContent = '.MuiSnackbar-root { visibility: hidden !important; }';
        document.head.appendChild(estilo);
      })
      .catch(() => undefined);
  }
}

async function mostrarAvisos(page: Page): Promise<void> {
  for (const marco of marcos(page)) {
    await marco.evaluate(() => document.querySelectorAll('[data-manual-oculto]').forEach((n) => n.remove())).catch(() => undefined);
  }
}

/**
 * Lleva a la vista el primer elemento resaltado: si cabe, lo centra solo cuando hace falta; si es más alto que la ventana, deja su borde
 * superior justo debajo de la cabecera de la página (así se ve el comienzo de lo que se explica).
 */
async function llevarALaVista(page: Page, objetivo: Locator): Promise<void> {
  if ((await objetivo.count()) === 0) return;
  const primero = objetivo.first();
  const caja = await primero.boundingBox().catch(() => null);
  const ventana = page.viewportSize();
  if (caja && ventana && caja.height > ventana.height * 0.7) {
    await primero.evaluate((el) => {
      el.scrollIntoView({ block: 'start' });
      el.ownerDocument.defaultView?.scrollBy(0, -72);
    });
    return;
  }
  await primero.scrollIntoViewIfNeeded({ timeout: 5000 }).catch(() => undefined);
}

async function quitarResaltado(page: Page): Promise<void> {
  for (const marco of marcos(page)) {
    await marco.evaluate(() => document.querySelectorAll('[data-manual-resaltado]').forEach((n) => n.remove())).catch(() => undefined);
  }
}

async function ajustarVentana(page: Page, ancho: number, alto: number): Promise<void> {
  const actual = page.viewportSize();
  if (actual?.width === ancho && actual.height === alto) return;
  await page.setViewportSize({ width: ancho, height: alto });
  // Con el ancho nuevo la pantalla se acomoda sola (menús, columnas): se espera a que se asiente.
  await page.waitForTimeout(250);
}

/** Estira el iframe al alto de su contenido (para la página completa) esperando a que el alto del documento se estabilice. */
async function estirarMarco(page: Page, marco: Locator): Promise<void> {
  const contenido = await (await marco.elementHandle()).contentFrame();
  if (!contenido) return;
  let alto = 0;
  for (let intento = 0; intento < 10; intento++) {
    await page.waitForTimeout(250);
    const actual = await contenido.evaluate(() => document.documentElement.scrollHeight);
    if (actual === alto) break;
    alto = actual;
  }
  if (alto > 0) {
    await marco.evaluate((el, valor) => {
      (el as HTMLIFrameElement).style.height = `${String(valor)}px`;
    }, alto);
    await page.waitForTimeout(250);
  }
}

/** Ruta de la app visible ahora (la del iframe, si es la app de la empresa). */
export async function rutaDeLaApp(page: Page): Promise<string | null> {
  const marco = page.locator('iframe#validasoft');
  if ((await marco.count()) > 0) {
    const contenido = await (await marco.elementHandle()).contentFrame();
    if (contenido) return normalizarRuta(contenido.url());
  }
  return normalizarRuta(page.url());
}

/**
 * Captura `destino` (PNG) con el tamaño pedido, esperando la carga, resaltando lo indicado y dejando la ventana como estaba. Devuelve
 * cuántos elementos resaltó.
 */
export async function capturar(page: Page, destino: string, opciones: OpcionesDeCaptura): Promise<ResultadoDeCaptura> {
  const original = page.viewportSize();
  const marco = page.locator('iframe#validasoft');
  const embebido = (await marco.count()) > 0;
  // La barra de la página anfitriona resta alto al iframe: se compensa para que el iframe mida exactamente lo pedido.
  const desfase = embebido ? Math.round((await marco.boundingBox())?.y ?? 0) : 0;
  const estiloMarco = embebido ? await marco.evaluate((el) => (el as HTMLIFrameElement).style.height) : '';
  try {
    await ajustarVentana(page, opciones.ancho, opciones.alto + desfase);
    if (opciones.esperarCarga) await esperarCarga(page);
    if (opciones.completa && embebido) await estirarMarco(page, marco);

    if (!opciones.avisos) await ocultarAvisos(page);
    // Siempre se parte de lo alto de la página (la app conserva el desplazamiento al navegar): el pantallazo no depende de por dónde estaba
    // la ventana, y las cabeceras fijas no quedan dibujadas a media página en la captura completa. Solo se baja si lo resaltado no se ve.
    for (const marcoDeLaPagina of marcos(page)) await marcoDeLaPagina.evaluate(() => window.scrollTo(0, 0)).catch(() => undefined);
    // El puntero quieto sobre un menú o un botón lo pinta en estado «hover»: se lleva a una esquina vacía.
    if (!opciones.mantenerPuntero) await page.mouse.move(1, 1);
    if (!opciones.completa && opciones.resaltar[0]) await llevarALaVista(page, opciones.resaltar[0]);
    const resaltados = opciones.resaltar.length > 0 ? await resaltar(opciones.resaltar, opciones.completa) : 0;
    if (opciones.resaltar.length > 0 && resaltados === 0 && !opciones.resaltadoOpcional) {
      throw new Error('Ningún elemento de «resaltar» está visible en la pantalla: revisa el locator (¿es el elemento correcto y ya se muestra?).');
    }
    const comun = { path: destino, animations: 'disabled', caret: 'hide' } as const;
    if (embebido) await marco.screenshot(comun);
    else await page.screenshot({ ...comun, fullPage: opciones.completa });
    return { resaltados };
  } finally {
    await quitarResaltado(page);
    if (!opciones.avisos) await mostrarAvisos(page);
    if (embebido) await marco.evaluate((el, valor) => ((el as HTMLIFrameElement).style.height = valor), estiloMarco).catch(() => undefined);
    if (original) await ajustarVentana(page, original.width, original.height);
  }
}
