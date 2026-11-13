import { API, llamar } from '../../soporte/api';
import { crearEscenario, invitar, nuevoTercero, ORIGEN_SISTEMA } from '../../soporte/semilla';
import { aceptarInvitacionDeClientePorApi, permitirOtroOrigenDelSistema } from '../nucleo/semillas';
import { expect, paso, recorrido } from '../nucleo';

const OTRO_ORIGEN_PERMITIDO = 'http://localhost:5198';

interface SesionEmbebida {
  token: string;
  url: string;
}

/**
 * Página de «un sistema cualquiera» con el iframe de Validasoft y la lista de mensajes que le llegan. Si `token` es null, el sistema no responde a la
 * petición de sesión (como si su backend tardara o fallara); si lo trae, lo entrega por `postMessage` solo al origen de Validasoft, como hace el ejemplo
 * de `docs/ejemplos/anfitrion-tercero`. El iframe no se llama `validasoft` a propósito: así el pantallazo es de la página completa, con los mensajes.
 */
function paginaAnfitriona(url: string, token: string | null): string {
  const origen = new URL(url).origin;
  return `<!doctype html>
<html lang="es-CO"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Sistema anfitrión</title>
<style>
  body{margin:0;background:#eef1f5;font-family:'Segoe UI',system-ui,sans-serif;color:#1d2533}
  header{background:#263238;color:#fff;height:48px;display:flex;align-items:center;gap:12px;padding:0 20px}
  main{max-width:960px;margin:16px auto;padding:0 16px;display:grid;gap:12px}
  #contenedor{background:#fff;border:1px solid #d5dbe5;border-radius:12px;overflow:hidden}
  iframe{display:block;width:100%;height:340px;border:0}
  h2{font-size:14px;margin:4px 0 0}
  #bitacora{list-style:none;margin:0;padding:8px 12px;font:12px/1.6 ui-monospace,Consolas,monospace;color:#44505f;background:#fff;border:1px solid #d5dbe5;border-radius:12px;min-height:40px}
</style></head>
<body><header><strong>Sistema anfitrión</strong><span>ficticio · ejemplo de integración</span></header>
<main>
  <div id="contenedor"><iframe id="marco" title="Validasoft" referrerpolicy="origin" src="${url}"></iframe></div>
  <h2>Mensajes que llegan desde Validasoft</h2>
  <ol id="bitacora" aria-label="Mensajes de Validasoft"></ol>
</main>
<script>
  const marco = document.getElementById('marco');
  const bitacora = document.getElementById('bitacora');
  const ORIGEN = ${JSON.stringify(origen)};
  const TOKEN = ${JSON.stringify(token)};
  let entregado = false; // solo la primera vez: un sistema real pediría otra sesión a su backend; aquí no hay otra que entregar
  window.addEventListener('message', (evento) => {
    if (evento.origin !== ORIGEN || evento.source !== marco.contentWindow || !evento.data || typeof evento.data.tipo !== 'string') return;
    if (evento.data.tipo === 'validasoft:resize') return;
    const fila = document.createElement('li');
    fila.textContent = '← ' + JSON.stringify(evento.data);
    bitacora.append(fila);
    if (TOKEN !== null && !entregado && (evento.data.tipo === 'validasoft:ready' || evento.data.tipo === 'validasoft:sesion-requerida')) {
      entregado = true;
      marco.contentWindow.postMessage({ tipo: 'validasoft:sesion', token: TOKEN }, ORIGEN);
      const respuesta = document.createElement('li');
      respuesta.textContent = '→ {"tipo":"validasoft:sesion","token":"…"}';
      bitacora.append(respuesta);
    }
  });
</script></body></html>`;
}

recorrido(
  {
    dominio: 'publico',
    actor: 'Público',
    titulo: 'Entiende la pantalla «Conectando con tu sistema» de una pantalla embebida',
    resumen:
      'Las pantallas del tercero embebidas (`/embed/…`) no tienen inicio de sesión propio: esperan a que el sistema que las aloja les entregue una sesión. Aquí se ve qué pasa al abrirlas sin sistema, cuando el sistema no responde y cuando entrega una sesión que no corresponde.',
    precondiciones: [
      'Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente y el sistema integrado E2E con dos orígenes permitidos.',
    ],
    relacionados: ['embebido/tercero-embebido', 'embebido/origenes-permitidos', 'embebido/sesion-vencida'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await permitirOtroOrigenDelSistema(e, OTRO_ORIGEN_PERMITIDO);
    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros en Espera S.A.S.'), 'CLIENTE');
    await aceptarInvitacionDeClientePorApi(invitacion);
    // Lo que haría el backend del sistema: pedir la sesión con su API key. Aquí solo se necesita la dirección de la pantalla y un token válido para ORIGEN_SISTEMA.
    const sesion = await llamar<SesionEmbebida>('POST', '/api/v1/sesiones-tercero', {
      llave: e.llave,
      empresa: invitacion.empresa.id,
      cuerpo: { vinculoId: invitacion.vinculoId, componente: 'boveda', origen: ORIGEN_SISTEMA, minutos: 10 },
    });

    // 1) La dirección abierta sin sistema anfitrión.
    await page.goto(`${API}/embed/boveda`);
    const esperando = page.getByLabel('Conectando con tu sistema…');
    await expect(esperando).toBeVisible();
    await paso(page, {
      titulo: 'Abre una pantalla embebida sin sistema',
      explicacion:
        'Las direcciones `/embed/…` no son para abrirlas en el navegador: las carga el sistema de la empresa dentro de un iframe, que es quien identifica a la persona. ' +
        'Si las abres directamente, no hay quien entregue la sesión y la pantalla se queda en «Conectando con tu sistema…», sin pedir correo ni mostrar datos del tercero.',
      resaltar: esperando,
      esperarCarga: false,
      variantes: [{ cuando: 'La persona busca sus documentos', entonces: 'Debe entrar por su correo en la dirección normal de Validasoft, no por una dirección /embed.' }],
    });

    // 2) El sistema anfitrión no entrega la sesión.
    const enEspera = await (await browser.newContext()).newPage();
    await enEspera.route(`${ORIGEN_SISTEMA}/**`, (ruta) => ruta.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: paginaAnfitriona(sesion.url, null) }));
    await enEspera.goto(`${ORIGEN_SISTEMA}/`);
    const marcoEnEspera = enEspera.frameLocator('#marco');
    await expect(marcoEnEspera.getByLabel('Conectando con tu sistema…')).toBeVisible();
    await expect(enEspera.getByRole('list', { name: 'Mensajes de Validasoft' })).toContainText('validasoft:ready');
    await paso(enEspera, {
      titulo: 'Mira qué pide la pantalla al sistema anfitrión',
      explicacion:
        'Dentro del iframe de un sistema permitido, la pantalla avisa al sistema que está lista (`validasoft:ready`) y espera. El sistema debe responder con la sesión que su backend pidió a Validasoft (`validasoft:sesion`). ' +
        'Mientras no llegue, el iframe sigue en «Conectando con tu sistema…» (1) y la lista de mensajes (2) solo muestra lo que la pantalla pidió.',
      resaltar: [marcoEnEspera.getByLabel('Conectando con tu sistema…'), enEspera.getByRole('list', { name: 'Mensajes de Validasoft' })],
      esperarCarga: false,
      configuracion: { ajuste: 'Orígenes permitidos del sistema', ruta: 'Configuración › Sistemas integrados (/sistemas)', efecto: 'Solo las páginas de esos orígenes pueden alojar el iframe y recibir la sesión.' },
    });

    // 3) El sistema entrega una sesión que no corresponde a su origen.
    const equivocada = await (await browser.newContext()).newPage();
    await equivocada.route(`${OTRO_ORIGEN_PERMITIDO}/**`, (ruta) =>
      ruta.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: paginaAnfitriona(sesion.url, sesion.token) }),
    );
    await equivocada.goto(`${OTRO_ORIGEN_PERMITIDO}/`);
    const marcoEquivocado = equivocada.frameLocator('#marco');
    const titulo = marcoEquivocado.getByRole('heading', { level: 1, name: 'Esta pantalla solo se muestra dentro de tu sistema' });
    await expect(titulo).toBeVisible();
    await expect(marcoEquivocado.getByText('No pudimos validar tu sesión. Vuelve a abrir esta pantalla desde tu sistema.')).toBeVisible();
    await paso(equivocada, {
      titulo: 'Entrega una sesión que no corresponde',
      explicacion:
        'La sesión que entrega el sistema solo vale para el origen para el que se emitió. Aquí la página está en otro origen permitido del mismo sistema, pero el token se pidió para el primero: ' +
        'la pantalla lo rechaza, pide otra sesión al sistema (`validasoft:sesion-requerida` con motivo «rechazada») y muestra «Esta pantalla solo se muestra dentro de tu sistema» (1) con un botón «Reintentar» (2), sin mostrar nada del tercero. Hay que volver a pedir la sesión para el origen correcto.',
      resaltar: [titulo, marcoEquivocado.getByRole('button', { name: 'Reintentar' })],
      variantes: [
        { cuando: 'El origen de la página no está registrado en el sistema', entonces: 'El navegador ni siquiera dibuja el iframe: Validasoft solo permite que lo aloje un origen permitido, y no se ve nada del tercero.' },
        { cuando: 'Se abre la dirección fuera de un iframe', entonces: 'Se queda en «Conectando con tu sistema…», como en el primer paso.' },
      ],
    });
  },
);
