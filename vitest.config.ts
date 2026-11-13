import { defineConfig } from 'vitest/config';

// Un solo comando (`pnpm test`) corre las pruebas de todos los paquetes del workspace, más la prueba de sincronía entre los flujos (`ci.yml` y `e2e.yml`) y
// la réplica local del CI (`infra/ci-local`, que no es un paquete: solo guarda esa prueba y se corre con Node) y las del generador del manual (`e2e/manual`).
export default defineConfig({
  test: {
    projects: ['packages/*', 'apps/*', 'marca', 'infra/ci-local', 'e2e/manual'],
  },
});
