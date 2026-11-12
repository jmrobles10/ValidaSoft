import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { Page } from '@playwright/test';

/**
 * Capturas de pantalla opcionales (reglas visuales: cada pantalla nueva, en claro y en celular). Solo con `E2E_CAPTURAS=<carpeta>`:
 * guarda `<nombre>-claro.png` y `<nombre>-celular.png` (390 × 844) de la página completa y deja la ventana como estaba.
 *
 * La app de la empresa corre en el iframe de la página anfitriona, que mide el alto de la ventana: para que la captura muestre toda la
 * pantalla (y no solo lo que cabe en el iframe), el iframe se estira al alto de su contenido antes de capturar y se deja como estaba.
 * Las pantallas con altura fija y scroll propio (`useScrollPropio`) conservan su alto: se capturan como las ve la persona en el contenedor.
 */
export async function capturar(page: Page, nombre: string): Promise<void> {
  const carpeta = process.env.E2E_CAPTURAS;
  if (!carpeta) return;
  mkdirSync(carpeta, { recursive: true });
  const original = page.viewportSize();
  await tomar(page, join(carpeta, `${nombre}-claro.png`));
  await page.setViewportSize({ width: 390, height: 844 });
  await tomar(page, join(carpeta, `${nombre}-celular.png`));
  if (original) await page.setViewportSize(original);
}

/** Estira el iframe de la página anfitriona (si lo hay) al alto de su contenido, captura la página completa y lo restaura. */
async function tomar(page: Page, ruta: string): Promise<void> {
  const marco = page.locator('iframe#validasoft');
  const hayMarco = (await marco.count()) > 0;
  if (hayMarco) {
    // El iframe es de otro origen: su alto se mide desde Playwright (su marco), no desde la página anfitriona. Con el ancho nuevo el
    // contenido se reacomoda: se espera a que el alto del documento se estabilice antes de medirlo.
    const contenido = await (await marco.elementHandle()).contentFrame();
    let alto = 0;
    for (let intento = 0; contenido && intento < 10; intento++) {
      await page.waitForTimeout(300);
      const actual = await contenido.evaluate(() => document.documentElement.scrollHeight);
      if (actual === alto) break;
      alto = actual;
    }
    if (alto > 0) {
      await marco.evaluate((el, valor) => {
        (el as HTMLIFrameElement).style.height = `${String(valor)}px`;
      }, alto);
      await page.waitForTimeout(300);
    }
  }
  await page.screenshot({ path: ruta, fullPage: true });
  if (hayMarco) await marco.evaluate((el) => (el as HTMLIFrameElement).style.removeProperty('height'));
}
