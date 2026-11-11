import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import type { Page } from '@playwright/test';

/**
 * Capturas de pantalla opcionales (reglas visuales: cada pantalla nueva, en claro y en celular). Solo con `E2E_CAPTURAS=<carpeta>`:
 * guarda `<nombre>-claro.png` y `<nombre>-celular.png` (390 × 844) y deja la ventana como estaba.
 */
export async function capturar(page: Page, nombre: string): Promise<void> {
  const carpeta = process.env.E2E_CAPTURAS;
  if (!carpeta) return;
  mkdirSync(carpeta, { recursive: true });
  const original = page.viewportSize();
  await page.screenshot({ path: join(carpeta, `${nombre}-claro.png`) });
  await page.setViewportSize({ width: 390, height: 844 });
  await page.screenshot({ path: join(carpeta, `${nombre}-celular.png`) });
  if (original) await page.setViewportSize(original);
}
