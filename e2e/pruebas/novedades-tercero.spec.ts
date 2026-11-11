import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { esperarCorreo, literal, textoSinEnlaces } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, observar } from '../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero, esperarTarjeta, responder, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario, type Invitacion, type Persona } from '../soporte/semilla';

/**
 * Novedades del tercero (NT-1…NT-4): la campana de su portal (con la marca de la empresa y el enlace a lo que hay que hacer), el
 * recordatorio de observaciones pendientes (individual y en lote, a lo sumo uno al día) y el aviso a las áreas correctas cuando
 * el tercero responde. Constructora Norte: Compras (Rubén) revisa el RUT; Cumplimiento (Olga) revisa el RUT y la Cámara.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

/** Grupo nuevo y un proveedor de Norte que aceptó y subió RUT y Cámara: el expediente está en revisión. */
async function proveedorEnRevision(page: Page, e: Escenario, nombre?: string): Promise<Invitacion> {
  const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf(`RUT ${invitacion.nombre} v1`) },
    { nombre: CAMARA, archivo: pdf(`Camara ${invitacion.nombre} v1`) },
  ]);
  return invitacion;
}

/** Ventana aparte (otra persona, otro navegador) con Validasoft embebido; en el expediente del tercero si se indica el área. */
async function enEmpresa(browser: Browser, e: Escenario, persona: Persona, expediente?: { tercero: string; area: string }): Promise<FrameLocator> {
  const app = await abrirEmbebido(await (await browser.newContext()).newPage(), e, persona);
  if (expediente) await abrirDesdeBandeja(app, expediente.tercero, expediente.area);
  return app;
}

/** Compras observa el RUT del tercero: el expediente vuelve a él. */
async function observarElRut(app: FrameLocator, nota: string): Promise<void> {
  await observar(app, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], nota);
}

/** La campana del portal del tercero: el botón (con o sin contador) y su panel. */
const campanaDelTercero = (page: Page) => page.getByRole('button', { name: /^Notificaciones(, \d+ sin leer)?$/ });
const panelDelTercero = (page: Page) => page.getByRole('dialog', { name: 'Notificaciones' });

/** Recarga el portal hasta que la campana muestre contador de no leídas (el aviso llega por la suscripción de eventos). */
async function esperarAvisoEnLaCampana(page: Page, titulo: string): Promise<void> {
  await expect(async () => {
    await page.reload();
    await campanaDelTercero(page).click({ timeout: 3000 });
    await expect(panelDelTercero(page).getByRole('listitem').filter({ hasText: titulo }).first()).toBeVisible({ timeout: 3000 });
  }).toPass({ timeout: 60_000 });
}

test('la observación llega a la campana del tercero con su empresa: la abre y llega al expediente, y puede apagar el correo', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await proveedorEnRevision(page, e);

  const ruben = await enEmpresa(browser, e, e.ruben, { tercero: invitacion.nombre, area: 'Compras' });
  await observarElRut(ruben, 'La página 2 está cortada.');

  // El tercero está en otra pantalla (Mi bóveda): la campana le avisa y el clic lo lleva a su revisión.
  await esperarEstadoDelTercero(page, 'Observado');
  await page.getByRole('link', { name: 'Mi bóveda' }).click();
  await expect(page).toHaveURL(/\/boveda$/);
  await esperarAvisoEnLaCampana(page, 'Tienes observaciones por corregir');
  await page.screenshot({ path: 'test-results/novedades-tercero-campana.png' });

  const aviso = panelDelTercero(page).getByRole('listitem').filter({ hasText: 'Tienes observaciones por corregir' });
  await expect(aviso).toContainText(`${e.norte.nombre} revisó tu validación`);
  await expect(aviso).toContainText('te pidió corregir 1 punto');
  await expect(aviso).toContainText('Sin leer');
  for (const interno of ['Rubén', 'Compras', 'Olga', 'Cumplimiento']) await expect(aviso).not.toContainText(interno);
  await aviso.getByRole('button').click();
  await expect(page).toHaveURL(/\/expediente\//);
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();
  await expect(page.getByRole('main').getByText(/ te pide corregir lo siguiente\./)).toBeVisible();

  // Quedó leída: con «Marcar todas» el contador desaparece.
  await campanaDelTercero(page).click();
  await expect(panelDelTercero(page).getByRole('listitem').filter({ hasText: 'Tienes observaciones por corregir' })).not.toContainText('Sin leer');
  await panelDelTercero(page).getByRole('button', { name: 'Marcar todas como leídas' }).click();
  await expect(page.getByText(/Marcamos \d+ notificaci(ón|ones) como leídas?\./)).toBeVisible();
  await page.keyboard.press('Escape');
  // Con el tiempo real puede entrar otro aviso enseguida (un documento por vencer, por ejemplo): el contador puede volver a
  // encenderse, así que se comprueba que el panel se cerró y no que el contador quede en cero.
  await expect(panelDelTercero(page)).toBeHidden();
  await expect(campanaDelTercero(page)).toBeVisible();

  // Apaga el correo de las observaciones: queda guardado para todas sus empresas.
  await campanaDelTercero(page).click();
  await panelDelTercero(page).getByRole('button', { name: 'Preferencias de correo' }).click();
  const preferencias = page.getByRole('dialog', { name: 'Preferencias de correo' });
  const interruptor = preferencias.getByRole('switch', { name: /Observaciones de una empresa/ });
  await expect(interruptor).toBeChecked();
  await interruptor.uncheck();
  await preferencias.getByRole('button', { name: 'Guardar' }).click();
  await expect(page.getByText('Guardamos tus preferencias de correo.')).toBeVisible();
  await campanaDelTercero(page).click();
  await panelDelTercero(page).getByRole('button', { name: 'Preferencias de correo' }).click();
  await expect(page.getByRole('dialog', { name: 'Preferencias de correo' }).getByRole('switch', { name: /Observaciones de una empresa/ })).not.toBeChecked();
  await page.screenshot({ path: 'test-results/novedades-tercero-preferencias.png' });

  // En el celular la campana cabe en la cabecera y el panel no se sale de la pantalla.
  await page.setViewportSize({ width: 375, height: 812 });
  await page.reload();
  await campanaDelTercero(page).click();
  await expect(panelDelTercero(page)).toBeVisible();
  const caja = await panelDelTercero(page).boundingBox();
  expect(caja && caja.x >= 0 && caja.x + caja.width <= 375).toBe(true);
  await page.screenshot({ path: 'test-results/novedades-tercero-campana-movil.png' });
});

test('el recordatorio de observaciones: individual desde el expediente y en lote desde la bandeja, uno al día, con correo y campana', async ({ page, browser }) => {
  const e = await crearEscenario();
  const a = await proveedorEnRevision(page, e);
  const ruben = await enEmpresa(browser, e, e.ruben, { tercero: a.nombre, area: 'Compras' });
  await observarElRut(ruben, 'Falta nitidez.');
  const otraPagina = await (await browser.newContext()).newPage();
  const b = await proveedorEnRevision(otraPagina, e, 'Tornillos del Sur S.A.S.');
  await abrirDesdeBandeja(ruben, b.nombre, 'Compras');
  await observarElRut(ruben, 'Falta nitidez.');

  // Individual: desde el expediente de A, con confirmación.
  await abrirDesdeBandeja(ruben, a.nombre, 'Compras');
  const recordar = ruben.getByRole('button', { name: 'Recordar al tercero' });
  await recordar.click();
  const dialogo = ruben.getByRole('dialog', { name: /Recordarle sus observaciones al tercero/ });
  await expect(dialogo).toContainText(a.nombre);
  await dialogo.getByRole('button', { name: 'Enviar recordatorio' }).click();
  await expect(ruben.getByText('Le recordamos al tercero sus observaciones.')).toBeVisible();
  await expect(recordar).toBeDisabled(); // una vez al día
  await expect(ruben.getByText(/Último recordatorio: /)).toBeVisible();

  // El tercero recibe el correo con lo que falta (sin revisores ni áreas) y, en su portal, el aviso que lleva al expediente.
  const correo = await esperarCorreo(a.correo, new RegExp(`^${literal(e.norte.nombre)} te recuerda las observaciones de tu validación `));
  expect(correo.texto).toContain(`${e.norte.nombre} sigue esperando tu respuesta`);
  expect(correo.texto).toContain(RUT);
  expect(correo.texto).toContain('Falta nitidez.');
  expect(correo.texto).toMatch(/\/e\/[A-Za-z0-9_-]{43}/);
  for (const interno of ['Rubén', 'Compras', 'Olga', 'Cumplimiento']) expect(textoSinEnlaces(correo.texto)).not.toContain(interno);
  await esperarEstadoDelTercero(page, 'Observado');
  await page.getByRole('link', { name: 'Mi bóveda' }).click();
  await esperarAvisoEnLaCampana(page, 'Recordatorio: observaciones pendientes');
  await panelDelTercero(page).getByRole('listitem').filter({ hasText: 'Recordatorio: observaciones pendientes' }).first().getByRole('button').click();
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();

  // En lote desde la bandeja: «Seleccionar observados» toma a los dos; A ya se recordó hoy y se omite, B recibe el suyo.
  await irASeccion(ruben, 'Vínculos');
  await irASeccion(ruben, 'Bandeja');
  await expect(ruben.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
  await ruben.getByRole('button', { name: 'Seleccionar observados' }).click();
  await ruben.owner().page().screenshot({ path: 'test-results/novedades-tercero-bandeja.png' });
  await ruben.getByRole('button', { name: 'Recordar (2)' }).click();
  await ruben.getByRole('dialog', { name: '¿Recordar las observaciones de 2 expedientes?' }).getByRole('button', { name: 'Enviar recordatorios' }).click();
  await expect(ruben.getByText('Recordamos a 1 tercero. 1 omitido: 1 ya se les recordó hoy.')).toBeVisible();
  await esperarCorreo(b.correo, new RegExp(`^${literal(e.norte.nombre)} te recuerda las observaciones de tu validación `));
  const conA = (await fetchCorreosDe(a.correo)).filter((asunto) => asunto.includes('te recuerda las observaciones'));
  expect(conA).toHaveLength(1);

  // «Todos los observados»: a los dos ya se les recordó hoy, así que no hay nadie más.
  await ruben.getByRole('button', { name: 'Recordar a los observados' }).click();
  await ruben.getByRole('dialog', { name: '¿Recordar a todos los terceros con observaciones pendientes?' }).getByRole('button', { name: 'Enviar recordatorios' }).click();
  await expect(ruben.getByText('No hay terceros por recordar: nadie tiene observaciones pendientes o ya se les recordó hoy.')).toBeVisible();
});

test('cuando el tercero responde se avisa a las áreas que vuelven a revisar lo corregido, aunque no hubieran observado', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await proveedorEnRevision(page, e);

  // Cumplimiento (Olga) aprueba el RUT y la Cámara; Compras (Rubén) observa el RUT v1: la aprobación de Olga sobre el RUT dependía de esa versión.
  const olga = await enEmpresa(browser, e, e.olga, { tercero: invitacion.nombre, area: 'Cumplimiento' });
  await aprobarArea(olga, 'Cumplimiento');
  const ruben = await enEmpresa(browser, e, e.ruben, { tercero: invitacion.nombre, area: 'Compras' });
  await observarElRut(ruben, 'La página 2 está cortada.');

  await esperarEstadoDelTercero(page, 'Observado');
  await subirDocumento(page, RUT, pdf(`RUT ${invitacion.nombre} corregido`), { version: 2 });
  await esperarTarjeta(page, RUT, 'Versión 2');
  await responder(page, 'Adjunto el RUT completo.');
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Le llega a Rubén, que observó, y también a Olga, cuya aprobación del RUT quedó sin efecto; cada una lleva al expediente.
  for (const [persona, app] of [['Rubén', ruben], ['Olga', olga]] as const) {
    await app.getByRole('button', { name: /^Notificaciones, \d+ sin leer/ }).click();
    const aviso = app.getByRole('listitem').filter({ hasText: 'El tercero respondió' }).first();
    await expect(aviso, `a ${persona} le debe llegar «El tercero respondió»`).toBeVisible({ timeout: 30_000 });
    await expect(aviso).toContainText(`${invitacion.nombre} respondió las observaciones`);
    await aviso.getByRole('button').click();
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  }
});

/** Asuntos de los correos que recibió `destinatario` (sin esperar: lo que ya llegó). */
async function fetchCorreosDe(destinatario: string): Promise<string[]> {
  const mailpit = process.env.E2E_MAILPIT ?? 'http://localhost:8025';
  const respuesta = await fetch(`${mailpit}/api/v1/search?query=${encodeURIComponent(`to:"${destinatario}"`)}&limit=50`);
  const { messages } = (await respuesta.json()) as { messages: { Subject: string }[] };
  return messages.map((m) => m.Subject);
}
