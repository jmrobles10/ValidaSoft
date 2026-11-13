import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { API, llamar } from '../../soporte/api';
import { enlace, esperarCorreo, literal } from '../../soporte/correo';
import { crearEscenario, invitar, nuevoTercero, ORIGEN_SISTEMA, type Escenario, type Invitacion } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const PAGINA_ANFITRIONA = readFileSync(resolve(import.meta.dirname, '../../../docs/ejemplos/anfitrion-tercero/index.html'), 'utf8');

interface SesionEmbebida {
  token: string;
  expira: string;
  url: string;
  origen: string;
  componente: string;
  vinculoId: string;
}

/** El tercero acepta su invitación (lo hace por su enlace, sin pantalla): sin ella el sistema no puede embeber nada suyo. */
async function aceptarPorApi(invitacion: Invitacion): Promise<void> {
  const correo = await esperarCorreo(invitacion.correo, new RegExp(`^${literal(invitacion.empresa.nombre)} te invita a validar tu vínculo como cliente$`));
  const token = new URL(enlace(correo, '/e/')).pathname.split('/').pop() ?? '';
  const canje = await fetch(`${API}/public/v1/enlaces/${token}/canjear`, { method: 'POST' });
  expect(canje.status).toBe(200);
  const { accessToken } = (await canje.json()) as { accessToken: string };
  const cabeceras = { Authorization: `Bearer ${accessToken}`, 'Content-Type': 'application/json' };
  const ver = (await (await fetch(`${API}/public/v1/invitaciones/${invitacion.vinculoId}`, { headers: cabeceras })).json()) as {
    textosPorAceptar: { tipo: string; textoVersion: number }[];
  };
  const aceptada = await fetch(`${API}/public/v1/invitaciones/${invitacion.vinculoId}/aceptar`, {
    method: 'POST',
    headers: cabeceras,
    body: JSON.stringify({ consentimientos: ver.textosPorAceptar.map((t) => ({ tipo: t.tipo, textoVersion: t.textoVersion, aceptado: true })) }),
  });
  expect(aceptada.status).toBe(200);
}

/** Sesión que el backend del sistema pide a la API con su llave; la página anfitriona solo recibe el resultado. */
function canjearSesion(e: Escenario, invitacion: Invitacion) {
  return (componente: string) =>
    llamar<SesionEmbebida>('POST', '/api/v1/sesiones-tercero', {
      llave: e.llave,
      empresa: invitacion.empresa.id,
      cuerpo: { vinculoId: invitacion.vinculoId, componente, origen: ORIGEN_SISTEMA, minutos: 10 },
    });
}

recorrido(
  {
    dominio: 'embebido',
    actor: 'Tercero',
    titulo: 'Muestra la bóveda y la ficha del tercero dentro de tu sistema',
    resumen:
      'Un sistema ajeno a Validasoft muestra al tercero su bóveda de documentos y su ficha dentro de sus propias pantallas. El tercero no ve marca, menú ni inicio de sesión de Validasoft: ' +
      'el sistema pide una sesión corta para cada pantalla y la entrega al marco.',
    precondiciones: [
      'Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente, y el sistema integrado E2E con su página de ejemplo.',
    ],
    relacionados: ['embebido/app-de-la-empresa-en-un-iframe', 'tercero/aceptar-invitacion-persona-juridica', 'integracion/identidad-delegada'],
  },
  async ({ browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros Embebidos S.A.S.'), 'CLIENTE');
    await aceptarPorApi(invitacion);
    const canjear = canjearSesion(e, invitacion);

    const contextoLimpio = await (await browser.newContext()).newPage();
    const pedidas: string[] = [];
    await contextoLimpio.route(`${ORIGEN_SISTEMA}/**`, (ruta) => ruta.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: PAGINA_ANFITRIONA }));
    await contextoLimpio.exposeFunction('obtenerSesionDeValidasoft', async (componente: string) => {
      pedidas.push(componente);
      return canjear(componente);
    });
    await contextoLimpio.goto(`${ORIGEN_SISTEMA}/`);

    await paso(contextoLimpio, {
      titulo: 'Abre la pantalla del tercero en tu sistema',
      explicacion:
        'La página de tu sistema tiene una lista con las pantallas de Validasoft que puede mostrar al tercero: la bóveda de documentos, el expediente y la ficha. ' +
        'Eliges una y pulsas «Abrir». Tu sistema pide para esa pantalla una sesión a su backend, que la pide a Validasoft con su llave de API.',
      resaltar: [contextoLimpio.getByLabel('Pantalla del tercero'), contextoLimpio.getByRole('button', { name: 'Abrir' })],
      variantes: [
        { cuando: 'Pides una pantalla de un origen que no registraste', entonces: 'Validasoft no emite la sesión (403 «origen-no-permitido») y la pantalla no se abre.' },
      ],
    });

    await contextoLimpio.getByLabel('Pantalla del tercero').selectOption('boveda');
    await contextoLimpio.getByRole('button', { name: 'Abrir' }).click();
    const boveda = contextoLimpio.frameLocator('#validasoft');
    await expect(boveda.getByRole('heading', { level: 1, name: 'Mi bóveda' })).toBeVisible();
    await expect(boveda.getByText('Aún no tienes documentos')).toBeVisible();
    await paso(contextoLimpio, {
      titulo: 'Revisa la bóveda dentro de tu sistema',
      explicacion:
        'La bóveda aparece en el marco de tu sistema, sin marca, menú ni botón de salir de Validasoft, y sin pedirle al tercero el correo ni una contraseña. ' +
        'El tercero ve sus documentos y puede subir los que le pida la empresa. Tu sistema recibe los mensajes de la pantalla, como el tamaño para ajustar el marco.',
      resaltar: contextoLimpio.locator('#validasoft'),
    });

    await contextoLimpio.getByLabel('Pantalla del tercero').selectOption('ficha');
    await contextoLimpio.getByRole('button', { name: 'Abrir' }).click();
    await expect(contextoLimpio.frameLocator('#validasoft').getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await paso(contextoLimpio, {
      titulo: 'Abre la ficha del mismo tercero',
      explicacion:
        'La ficha es «Mi información»: los datos generales, tributarios y financieros del tercero. Es la misma persona, pero cada pantalla tiene su propia sesión corta: ' +
        'tu sistema pidió una sesión para la bóveda y otra para la ficha, y las dos vencen en 10 minutos.',
      resaltar: contextoLimpio.locator('#validasoft'),
    });
    expect(pedidas).toEqual(['boveda', 'ficha']);
  },
);
