import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { afterEach } from 'vitest';
import '../i18n';

// `findBy*`/`waitFor` esperan 5 s (1 s por omisión): con los tres proyectos en paralelo el primer render de MUI tarda más.
configure({ asyncUtilTimeout: 5_000 });

// jsdom no implementa `Blob.arrayBuffer` (los navegadores sí): se lee con FileReader y se copia a un ArrayBuffer de
// Node para que `crypto.subtle` lo acepte.
if (!('arrayBuffer' in Blob.prototype)) {
  Object.defineProperty(Blob.prototype, 'arrayBuffer', {
    value(this: Blob) {
      return new Promise<ArrayBuffer>((resolver, rechazar) => {
        const lector = new FileReader();
        lector.onload = () => resolver(Uint8Array.from(new Uint8Array(lector.result as ArrayBuffer)).buffer);
        lector.onerror = () => rechazar(lector.error ?? new Error('No se pudo leer el archivo'));
        lector.readAsArrayBuffer(this);
      });
    },
  });
}

afterEach(() => {
  cleanup();
});
