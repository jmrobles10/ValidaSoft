import { defineConfig } from 'vitest/config';

// Un solo comando (`pnpm test`) corre las pruebas de todos los paquetes del workspace.
export default defineConfig({
  test: {
    projects: ['packages/*', 'apps/*'],
  },
});
