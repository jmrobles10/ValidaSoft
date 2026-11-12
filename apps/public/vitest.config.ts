import react from '@vitejs/plugin-react';
import { defineProject } from 'vitest/config';

export default defineProject({
  plugins: [react()],
  test: {
    name: 'public',
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/pruebas/preparar.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    // Las pruebas de interacción (user-event sobre MUI en jsdom) tardan 2–3 s solas; con los tres proyectos en paralelo
    // pasaban de los 5 s por omisión y, con la máquina o el runner cargados, de 20 s: el tope solo protege de cuelgues, no mide velocidad.
    testTimeout: 60_000,
  },
});
