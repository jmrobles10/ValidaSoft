// Compresión de los pantallazos del sitio: PNG → WebP con pérdida (calidad 82 por omisión), con el Chromium que ya usan los recorridos (Playwright):
// el navegador codifica el WebP desde un canvas, así que no hace falta ninguna librería nueva.
//
// Los pantallazos de la interfaz (superficies planas y texto) bajan de unos 100 KB a unos 15–25 KB sin que se note a simple vista, y el sitio completo cabe
// en una publicación de 64 MB. Si el navegador no está disponible, el generador deja los PNG (más pesados) y lo avisa.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname } from 'node:path';
import { pathToFileURL } from 'node:url';

/** Abre un Chromium sin cabeza y devuelve `{ convertir(origen, destino), cerrar() }`, o `null` si no hay navegador. */
export async function abrirConversor(raiz, { calidad = 82, paginas = 4 } = {}) {
  let navegador;
  try {
    const rutaDePlaywright = createRequire(`${raiz}/e2e/package.json`).resolve('@playwright/test');
    const modulo = await import(pathToFileURL(rutaDePlaywright).href);
    const chromium = modulo.chromium ?? modulo.default?.chromium;
    navegador = await chromium.launch({ args: ['--no-sandbox'] });
  } catch (error) {
    console.warn(`Aviso: no se pudo abrir el navegador para comprimir los pantallazos (${error instanceof Error ? error.message.split('\n')[0] : String(error)}); se dejan en PNG.`);
    return null;
  }
  const hojas = await Promise.all(Array.from({ length: paginas }, async () => (await navegador.newContext()).newPage()));
  const libres = [...hojas];
  const esperando = [];
  const tomar = () => (libres.length > 0 ? Promise.resolve(libres.pop()) : new Promise((resolver) => esperando.push(resolver)));
  const devolver = (hoja) => {
    const siguiente = esperando.shift();
    if (siguiente) siguiente(hoja);
    else libres.push(hoja);
  };

  return {
    /** Convierte el PNG de `origen` y escribe el WebP en `destino`; devuelve los bytes escritos. */
    async convertir(origen, destino) {
      const png = readFileSync(origen).toString('base64');
      const hoja = await tomar();
      try {
        const webp = await hoja.evaluate(
          async ({ datos, q }) => {
            const imagen = new Image();
            imagen.src = `data:image/png;base64,${datos}`;
            await imagen.decode();
            const lienzo = document.createElement('canvas');
            lienzo.width = imagen.naturalWidth;
            lienzo.height = imagen.naturalHeight;
            lienzo.getContext('2d').drawImage(imagen, 0, 0);
            const blob = await new Promise((r) => lienzo.toBlob(r, 'image/webp', q));
            const bytes = new Uint8Array(await blob.arrayBuffer());
            let texto = '';
            for (let i = 0; i < bytes.length; i += 0x8000) texto += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
            return btoa(texto);
          },
          { datos: png, q: calidad / 100 },
        );
        const buffer = Buffer.from(webp, 'base64');
        mkdirSync(dirname(destino), { recursive: true });
        writeFileSync(destino, buffer);
        return buffer.length;
      } finally {
        devolver(hoja);
      }
    },
    async cerrar() {
      await navegador.close();
    },
  };
}
