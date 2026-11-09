import react from '@vitejs/plugin-react';
import { defineProject } from 'vitest/config';

export default defineProject({
  plugins: [react()],
  test: {
    name: 'web',
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/pruebas/preparar.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    // Las pruebas de interacción (user-event sobre MUI en jsdom) tardan 2–3 s solas; con los tres proyectos en paralelo
    // pasaban de los 5 s por omisión.
    testTimeout: 20_000,
  },
});
