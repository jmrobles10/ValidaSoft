import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv, type Plugin } from 'vite';

/**
 * Solo en `vite dev`: sirve las credenciales que dejó `pnpm semilla:dev` (archivo ignorado por git) para
 * prellenar el formulario de sesión de desarrollo y la página `embed-demo.html`. Nunca entra al build.
 */
function semillaDeDesarrollo(): Plugin {
  return {
    name: 'validasoft-semilla-dev',
    apply: 'serve',
    configureServer(servidor) {
      servidor.middlewares.use('/__dev/semilla.json', (_peticion, respuesta) => {
        readFile(resolve(import.meta.dirname, '.semilla.local.json'), 'utf8')
          .then((contenido) => {
            respuesta.setHeader('Content-Type', 'application/json');
            respuesta.end(contenido);
          })
          .catch(() => {
            respuesta.statusCode = 404;
            respuesta.end('{}');
          });
      });
    },
  };
}

export default defineConfig(({ mode }) => {
  const entorno = loadEnv(mode, import.meta.dirname, '');
  const api = entorno.VALIDASOFT_API_URL ?? 'http://localhost:5080';

  return {
    plugins: [react(), semillaDeDesarrollo()],
    server: {
      port: 5173,
      strictPort: true,
      // La API no publica CORS: en desarrollo el navegador habla con Vite y Vite con la API (mismo origen).
      proxy: { '/api': { target: api, changeOrigin: true } },
    },
    preview: { port: 4173, proxy: { '/api': { target: api, changeOrigin: true } } },
    // El build solo empaqueta index.html: embed-demo.html es de desarrollo (Vite la sirve en `pnpm dev`).
    build: {
      rollupOptions: {
        output: {
          manualChunks: {
            react: ['react', 'react-dom', 'react-router'],
            mui: ['@mui/material', '@emotion/react', '@emotion/styled'],
          },
        },
      },
    },
  };
});
