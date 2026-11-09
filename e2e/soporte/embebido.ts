import type { FrameLocator, Page } from '@playwright/test';
import { API, llamar } from './api';
import { ORIGEN_SISTEMA, type Empresa, type Escenario, type Persona } from './semilla';

/**
 * Página del sistema integrado (otro origen) que aloja la app de Validasoft en un iframe, como lo haría el SRM: cuando
 * Validasoft pide sesión, «su backend» (la prueba, en Node) firma la aserción de la persona y canjea el token en
 * `POST /api/v1/sesiones-usuario`; la página se lo pasa al iframe por postMessage (contrato de sesion/puenteEmbed.ts).
 * El iframe ocupa el alto de la ventana (no crece con `validasoft:resize`): así los diálogos, centrados en el iframe,
 * siempre quedan a la vista y el contenido largo se desplaza dentro del iframe.
 */
function paginaAnfitriona(rutaInicial: string, empresaId: string): string {
  return `<!doctype html>
<html lang="es-CO"><head><meta charset="utf-8"><title>Sistema integrado E2E</title>
<style>html,body{margin:0;background:#eef1f5;font-family:sans-serif} header{height:48px;display:flex;align-items:center;padding:0 16px;background:#263238;color:#fff}
iframe{display:block;width:100%;height:calc(100vh - 48px);border:0}</style></head>
<body><header>Sistema integrado E2E</header>
<iframe id="validasoft" title="Validasoft" src="${API}/app/#/e/${empresaId}/${rutaInicial}"></iframe>
<script>
  const marco = document.getElementById('validasoft');
  window.mensajesDeValidasoft = [];
  window.addEventListener('message', async (evento) => {
    if (evento.origin !== ${JSON.stringify(API)} || !evento.data || typeof evento.data.tipo !== 'string') return;
    window.mensajesDeValidasoft.push(evento.data);
    if (evento.data.tipo === 'validasoft:sesion-requerida') {
      const token = await window.sesionDeValidasoft();
      marco.contentWindow.postMessage({ tipo: 'validasoft:sesion', token, empresaId: ${JSON.stringify(empresaId)} }, ${JSON.stringify(API)});
    }
  });
</script></body></html>`;
}

/** Abre Validasoft embebido como `persona` en `empresa` (Norte por defecto); devuelve el iframe para usarlo. */
export async function abrirEmbebido(page: Page, e: Escenario, persona: Persona, empresa: Empresa = e.norte, rutaInicial = 'bandeja'): Promise<FrameLocator> {
  await page.route(`${ORIGEN_SISTEMA}/**`, (ruta) =>
    ruta.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: paginaAnfitriona(rutaInicial, empresa.id) }),
  );
  await page.exposeFunction('sesionDeValidasoft', async () => {
    const sesion = await llamar<{ token: string }>('POST', '/api/v1/sesiones-usuario', { llave: e.llave, delegado: e.delegado(persona) });
    return sesion.token;
  });
  await page.goto(`${ORIGEN_SISTEMA}/`);
  return page.frameLocator('#validasoft');
}
