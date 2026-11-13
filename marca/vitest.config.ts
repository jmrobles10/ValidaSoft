import { defineProject } from 'vitest/config';

// Pruebas del generador de recursos de marca (codificador de GIF y sincronía de los recursos publicados): solo Node, sin navegador.
export default defineProject({
  test: {
    name: 'marca',
    environment: 'node',
    include: ['*.test.ts'],
    testTimeout: 30_000,
  },
});
