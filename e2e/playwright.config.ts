import { defineConfig, devices } from '@playwright/test';

/**
 * Pruebas de punta a punta de Validasoft (docs/04 §12): navegador real contra la API que sirve las dos apps, con
 * PostgreSQL, Mailpit y MinIO de verdad. Cada prueba crea su propio tenant, así que corren en paralelo sin pisarse.
 *
 *   pnpm --filter @validasoft/e2e test                 -> compila, publica y levanta la API en :5090 (soporte/api.mjs)
 *   E2E_BASE_URL=http://localhost:5080 pnpm ... test   -> usa una API ya levantada (debe servir /app/ y /)
 *
 * Requiere `docker compose -f infra/compose/compose.yaml up -d` (PostgreSQL, Mailpit y MinIO).
 */
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${process.env.E2E_PUERTO ?? '5090'}`;

export default defineConfig({
  testDir: './pruebas',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 3,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL,
    locale: 'es-CO',
    timezoneId: 'America/Bogota',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: {
        ...devices['Desktop Chrome'],
        // La página del sistema integrado la sirve `page.route` (sin dirección real): Chrome la toma por pública y su
        // control de acceso a la red local bloquearía el iframe de localhost. En producción ambos son orígenes públicos.
        launchOptions: { args: ['--disable-features=LocalNetworkAccessChecks'] },
      },
    },
  ],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: 'node soporte/api.mjs',
        url: `${baseURL}/health/ready`,
        reuseExistingServer: !process.env.CI,
        timeout: 600_000,
        stdout: 'ignore',
        stderr: 'pipe',
      },
});
