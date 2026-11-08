import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';
import '../i18n';

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
