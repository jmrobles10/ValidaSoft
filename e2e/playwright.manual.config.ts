import { defineConfig, devices } from '@playwright/test';
import base from './playwright.config';

/**
 * Recorridos del manual (e2e/manual/README.md): mismas pruebas de punta a punta, pero con el propósito de documentar. Reutiliza TODO el
 * arranque del stack de `playwright.config.ts` (la API compilada que sirve las dos apps, PostgreSQL, Mailpit y MinIO; las mismas
 * variables E2E_PUERTO / E2E_BASE_DATOS / E2E_BASE_URL) y solo cambia qué corre: los archivos `*.manual.ts` de `manual/`.
 *
 * No hace parte de `pnpm e2e` ni del CI (esa configuración solo mira `pruebas/`). Se corre con `pnpm manual:recorrer`.
 */
export default defineConfig({
  ...base,
  testDir: './manual',
  testMatch: '**/*.manual.ts',
  // Carpeta propia: Playwright vacía su carpeta de resultados al empezar y no debe borrar la de las e2e normales.
  outputDir: './test-results-manual',
  timeout: 240_000,
  retries: 0,
  reporter: [['list']],
  globalTimeout: 0,
  projects: [
    {
      name: 'manual',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1280, height: 800 },
        // La app de la empresa, embebida de otro origen, mide su alto fijo como `screen.availHeight - 240` (sesion/alturaFija.ts): con la pantalla
        // igual a la ventana (800) las páginas de configuración y de detalle quedaban de 560 px, con el resto del iframe en blanco. Con una pantalla
        // de 1040 miden exactamente el iframe del pantallazo (800), como en un monitor real.
        screen: { width: 1280, height: 1040 },
        deviceScaleFactor: 1,
        launchOptions: { args: ['--disable-features=LocalNetworkAccessChecks'] },
      },
    },
  ],
});
