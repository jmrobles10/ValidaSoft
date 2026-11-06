import react from '@vitejs/plugin-react';
import { defineConfig, loadEnv } from 'vite';

/**
 * App del tercero (docs/06 §3.2). Rutas con historial del navegador: el correo trae `{UrlPublica}/e/{token}`.
 * En desarrollo Vite hace de proxy de `/public/v1` hacia la API (que no publica CORS) y de `/__mailpit` hacia Mailpit,
 * para leer el enlace y el código desde `/dev/correos`.
 */
export default defineConfig(({ mode }) => {
  const entorno = loadEnv(mode, import.meta.dirname, '');
  const api = entorno.VALIDASOFT_API_URL ?? 'http://localhost:5080';
  const mailpit = entorno.VALIDASOFT_MAILPIT_URL ?? 'http://localhost:8025';
  const proxy = { '/public/v1': { target: api, changeOrigin: true } };

  return {
    plugins: [react()],
    server: {
      port: 5181,
      strictPort: true,
      proxy: {
        ...proxy,
        '/__mailpit': { target: mailpit, changeOrigin: true, rewrite: (ruta: string) => ruta.replace(/^\/__mailpit/, '') },
      },
    },
    preview: { port: 4181, proxy },
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
