import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { expect, test, type Page } from '@playwright/test';
import { API, ErrorDeApi, llamar } from '../soporte/api';
import { capturar } from '../soporte/capturas';
import { enlace, esperarCorreo, literal } from '../soporte/correo';
import { crearEscenario, invitar, nuevoTercero, ORIGEN_SISTEMA, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Embebido genérico del tercero (IO-5, docs/08 §2.2): una página ficticia de «cualquier sistema» (docs/ejemplos/anfitrion-tercero) monta
 * la bóveda y la ficha del tercero en un iframe con una sesión que su BACKEND canjea (POST /api/v1/sesiones-tercero). El token va por
 * postMessage, solo del origen permitido y solo para el origen para el que se emitió; otro origen no puede embeber ni usar ese token.
 */
const PAGINA_ANFITRIONA = readFileSync(resolve(import.meta.dirname, '../../docs/ejemplos/anfitrion-tercero/index.html'), 'utf8');
const OTRO_ORIGEN_PERMITIDO = 'http://localhost:5198';
const ORIGEN_NO_PERMITIDO = 'http://localhost:5197';

interface SesionEmbebida {
  token: string;
  expira: string;
  url: string;
  origen: string;
  componente: string;
  vinculoId: string;
}

/** El tercero acepta su invitación (la hace él, por su enlace): sin ella el sistema no puede embeber nada suyo. */
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

/** «El backend del sistema»: canjea la sesión con su API key (la página nunca la ve). */
function backendDelSistema(e: Escenario, invitacion: Invitacion, origen = ORIGEN_SISTEMA) {
  const pedidas: { componente: string; motivo: string }[] = [];
  const canjear = async (componente: string, motivo: string): Promise<SesionEmbebida> => {
    pedidas.push({ componente, motivo });
    return llamar<SesionEmbebida>('POST', '/api/v1/sesiones-tercero', {
      llave: e.llave,
      empresa: invitacion.empresa.id,
      cuerpo: { vinculoId: invitacion.vinculoId, componente, origen, minutos: 10 },
    });
  };
  return { pedidas, canjear };
}

/** Abre la página anfitriona en `origen` (la sirve `page.route`) con la sesión que le dé el «backend» dado. */
async function abrirAnfitrion(page: Page, origen: string, sesion: (componente: string, motivo: string) => Promise<SesionEmbebida>): Promise<void> {
  await page.route(`${origen}/**`, (ruta) => ruta.fulfill({ status: 200, contentType: 'text/html; charset=utf-8', body: PAGINA_ANFITRIONA }));
  await page.exposeFunction('obtenerSesionDeValidasoft', sesion);
  await page.goto(`${origen}/`);
}

async function mensajes(page: Page): Promise<string[]> {
  return page.evaluate(() => (window as unknown as { mensajesDeValidasoft: { tipo: string }[] }).mensajesDeValidasoft.map((m) => m.tipo));
}

async function terceroConInvitacionAceptada(): Promise<{ e: Escenario; invitacion: Invitacion }> {
  const e = await crearEscenario();
  // El sistema E2E tiene registrado ORIGEN_SISTEMA; se suma otro origen suyo para probar que el token solo sirve donde se emitió.
  const sistemas = await llamar<{ id: string; codigo: string }[]>('GET', '/api/v1/sistemas', { llave: e.admin });
  const sistema = sistemas.find((s) => s.codigo === 'E2E');
  if (!sistema) throw new Error('El sistema E2E no existe.');
  await llamar('PUT', `/api/v1/sistemas/${sistema.id}/origenes`, { llave: e.admin, cuerpo: { origenes: [ORIGEN_SISTEMA, OTRO_ORIGEN_PERMITIDO] } });
  const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros Embebidos S.A.S.'), 'CLIENTE');
  await aceptarPorApi(invitacion);
  return { e, invitacion };
}

test('un sistema ficticio embebe la bóveda y la ficha del tercero con una sesión de su backend, sin barra ni login propios', async ({ browser }) => {
  const { e, invitacion } = await terceroConInvitacionAceptada();
  const backend = backendDelSistema(e, invitacion);

  // Contexto limpio: el iframe no tiene cookie del tercero; entra solo con el token que entrega la página.
  const anfitrion = await (await browser.newContext()).newPage();
  await abrirAnfitrion(anfitrion, ORIGEN_SISTEMA, backend.canjear);

  await anfitrion.getByLabel('Pantalla del tercero').selectOption('boveda');
  await anfitrion.getByRole('button', { name: 'Abrir' }).click();
  const marco = anfitrion.frameLocator('#validasoft');
  await expect(marco.getByRole('heading', { level: 1, name: 'Mi bóveda' })).toBeVisible();
  // Dentro del iframe no hay marca, navegación ni pie (el sistema pone su propio marco) y nunca se pide el correo.
  await expect(marco.getByRole('navigation')).toHaveCount(0);
  await expect(marco.getByRole('button', { name: 'Salir' })).toHaveCount(0);
  await expect(marco.getByRole('textbox', { name: 'Correo de la invitación' })).toHaveCount(0);
  expect(await mensajes(anfitrion)).toEqual(expect.arrayContaining(['validasoft:ready', 'validasoft:resize']));
  await expect(marco.getByText('Aún no tienes documentos')).toBeVisible(); // ya cargó la lista (la captura no muestra el esqueleto)
  await capturar(anfitrion, 'embebido-boveda');
  expect(backend.pedidas).toEqual([{ componente: 'boveda', motivo: 'inicial' }]);

  // La ficha del mismo tercero, con otra sesión (otro componente = otro token).
  await anfitrion.getByLabel('Pantalla del tercero').selectOption('ficha');
  await anfitrion.getByRole('button', { name: 'Abrir' }).click();
  await expect(anfitrion.frameLocator('#validasoft').getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
  expect(backend.pedidas.map((p) => p.componente)).toEqual(['boveda', 'ficha']);
});

test('el backend solo emite sesiones para orígenes del sistema y un origen no permitido no puede embeber', async ({ browser }) => {
  const { e, invitacion } = await terceroConInvitacionAceptada();

  // 403: el origen no está entre los del sistema (y la API no emite el token).
  const rechazo = await llamar('POST', '/api/v1/sesiones-tercero', {
    llave: e.llave,
    empresa: invitacion.empresa.id,
    cuerpo: { vinculoId: invitacion.vinculoId, componente: 'boveda', origen: ORIGEN_NO_PERMITIDO },
  }).catch((error: unknown) => error);
  expect(rechazo).toBeInstanceOf(ErrorDeApi);
  expect((rechazo as ErrorDeApi).estado).toBe(403);
  expect((rechazo as ErrorDeApi).codigo).toBe('origen-no-permitido');

  // Un origen que ningún sistema registró no puede alojar el iframe (frame-ancestors): no se ve nada del tercero.
  const sesionValida = await backendDelSistema(e, invitacion).canjear('boveda', 'inicial');
  const intruso = await (await browser.newContext()).newPage();
  await abrirAnfitrion(intruso, ORIGEN_NO_PERMITIDO, () => Promise.resolve(sesionValida));
  await intruso.getByRole('button', { name: 'Abrir' }).click();
  await expect(intruso.locator('#validasoft')).toBeAttached();
  await expect(intruso.frameLocator('#validasoft').getByRole('heading', { name: 'Mi bóveda' })).toHaveCount(0, { timeout: 5000 });
});

test('un token emitido para un origen no se acepta en otro origen del mismo sistema', async ({ browser }) => {
  const { e, invitacion } = await terceroConInvitacionAceptada();
  // Token emitido para ORIGEN_SISTEMA, entregado por una página alojada en OTRO_ORIGEN_PERMITIDO (también permitido para el sistema).
  const paraOtroOrigen = await backendDelSistema(e, invitacion, ORIGEN_SISTEMA).canjear('boveda', 'inicial');
  const pagina = await (await browser.newContext()).newPage();
  await abrirAnfitrion(pagina, OTRO_ORIGEN_PERMITIDO, () => Promise.resolve(paraOtroOrigen));
  await pagina.getByRole('button', { name: 'Abrir' }).click();

  const marco = pagina.frameLocator('#validasoft');
  await expect(marco.getByRole('heading', { level: 1, name: 'Esta pantalla solo se muestra dentro de tu sistema' })).toBeVisible();
  await expect(marco.getByText('No pudimos validar tu sesión. Vuelve a abrir esta pantalla desde tu sistema.')).toBeVisible();
  await capturar(pagina, 'embebido-rechazado');
  await expect(marco.getByRole('heading', { name: 'Mi bóveda' })).toHaveCount(0);
  // El iframe pidió otra sesión al anfitrión: «rechazada».
  await expect.poll(() => mensajes(pagina)).toContain('validasoft:sesion-requerida');
});
