import type { Locator, Page } from '@playwright/test';

/**
 * Para los recorridos del Sistema integrador (que usa la API, no una pantalla): dibuja en la página una tarjeta con la petición y la respuesta
 * REALES de una llamada hecha en la prueba (`llamar` de `soporte/api.ts`), lista para `paso`. Devuelve el locator de cada bloque para resaltarlo.
 *
 *   const pagina = await mostrarCodigo(page, { titulo: 'Crea el vínculo', peticion: 'POST /api/v1/vinculos', cuerpo: {...}, respuesta: { estado: 201, cuerpo: vinculo } });
 *   await paso(page, { titulo: '…', explicacion: '…', resaltar: pagina.respuesta });
 *
 * Las llaves y los secretos NUNCA se muestran: pasa solo datos de ejemplo (sin `X-Api-Key` real).
 */
export interface BloqueDeCodigo {
  titulo: string;
  /** Línea de la petición: `POST /api/v1/vinculos`. */
  peticion: string;
  /** Cabeceras que importan para explicar la llamada (sin secretos reales). */
  cabeceras?: Record<string, string>;
  cuerpo?: unknown;
  respuesta?: { estado: number; cuerpo?: unknown };
}

const escapar = (texto: string) => texto.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

export async function mostrarCodigo(page: Page, bloque: BloqueDeCodigo): Promise<{ peticion: Locator; respuesta: Locator }> {
  const cabeceras = Object.entries(bloque.cabeceras ?? {})
    .map(([k, v]) => `${k}: ${v}`)
    .join('\n');
  const peticion = [bloque.peticion, cabeceras, bloque.cuerpo === undefined ? '' : `\n${JSON.stringify(bloque.cuerpo, null, 2)}`].filter(Boolean).join('\n');
  const respuesta = bloque.respuesta ? `HTTP ${String(bloque.respuesta.estado)}${bloque.respuesta.cuerpo === undefined ? '' : `\n\n${JSON.stringify(bloque.respuesta.cuerpo, null, 2)}`}` : '';
  await page.setContent(`<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
    <body style="margin:0;background:#eef1f5;font-family:system-ui,'Segoe UI',sans-serif;color:#1b2129">
      <main style="max-width:860px;margin:32px auto;padding:0 16px">
        <h1 style="font-size:20px;margin:0 0 16px">${escapar(bloque.titulo)}</h1>
        <section id="peticion" style="background:#fff;border:1px solid #d5dbe3;border-radius:14px;padding:16px 20px;margin-bottom:16px">
          <h2 style="font-size:13px;letter-spacing:.06em;text-transform:uppercase;color:#596473;margin:0 0 8px">Petición</h2>
          <pre style="margin:0;white-space:pre-wrap;word-break:break-word;font:13px/1.5 ui-monospace,Consolas,monospace">${escapar(peticion)}</pre>
        </section>
        ${
          respuesta
            ? `<section id="respuesta" style="background:#fff;border:1px solid #d5dbe3;border-radius:14px;padding:16px 20px">
          <h2 style="font-size:13px;letter-spacing:.06em;text-transform:uppercase;color:#596473;margin:0 0 8px">Respuesta</h2>
          <pre style="margin:0;white-space:pre-wrap;word-break:break-word;font:13px/1.5 ui-monospace,Consolas,monospace">${escapar(respuesta)}</pre>
        </section>`
            : ''
        }
      </main>
    </body>`);
  return { peticion: page.locator('#peticion'), respuesta: page.locator('#respuesta') };
}
