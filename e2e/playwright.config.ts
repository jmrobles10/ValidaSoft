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
// En local las pruebas van aisladas del entorno de desarrollo: base `validasoft_e2e` y Mailpit propio en :8026 (si
// compartieran la base, la API de desarrollo tomaría sus mensajes de la cola y mandaría sus correos al Mailpit de
// desarrollo). En CI el entorno ya es limpio. E2E_COMPARTIDO=1 usa la base y el Mailpit de desarrollo.
const aislado = !process.env.CI && process.env.E2E_COMPARTIDO !== '1' && !process.env.E2E_BASE_URL;
process.env.E2E_AISLADO = aislado ? '1' : '0';
if (aislado) process.env.E2E_MAILPIT ??= 'http://localhost:8026';

const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${process.env.E2E_PUERTO ?? '5090'}`;

export default defineConfig({
  testDir: './pruebas',
  timeout: 120_000,
  expect: { timeout: 15_000 },
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  workers: process.env.CI ? 2 : 3,
  // En CI, además de la lista y el informe HTML, `github` deja cada falla como anotación del check run (se lee sin descargar el log).
  reporter: process.env.CI ? [['list'], ['github'], ['html', { open: 'never' }]] : [['list']],
  // En CI, un tope propio por debajo del del trabajo: si la corrida se alarga, Playwright se detiene limpio y alcanza a informar lo que falló
  // (con el tope del trabajo el runner la cancela sin informe ni anotaciones).
  globalTimeout: process.env.CI ? 35 * 60_000 : 0,
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
