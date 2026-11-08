import '@fontsource-variable/geist';
import '@fontsource-variable/geist-mono';
import './i18n';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from './app/App';

const raiz = document.getElementById('raiz');
if (!raiz) throw new Error('Falta el contenedor #raiz en index.html.');

createRoot(raiz).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
