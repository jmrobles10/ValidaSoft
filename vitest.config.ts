import { defineConfig } from 'vitest/config';

// Un solo comando (`pnpm test`) corre las pruebas de todos los paquetes del workspace, más la prueba de sincronía entre `ci.yml` y
// la réplica local del CI (`infra/ci-local`, que no es un paquete: solo guarda esa prueba y se corre con Node).
export default defineConfig({
  test: {
    projects: ['packages/*', 'apps/*', 'infra/ci-local'],
  },
});
