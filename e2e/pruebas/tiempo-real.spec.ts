import { expect, test, type Browser, type Frame, type FrameLocator, type Locator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, observar } from '../soporte/empresa';
import { completarInvitacion, fraseDeEstado, responder, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario, type Persona } from '../soporte/semilla';

/**
 * Tiempo real entre la empresa y el tercero (NT-12…NT-16): lo que uno hace aparece en la pantalla del otro, ya abierta, sin recargar
 * y con la campana encendida. Cada prueba usa dos o tres navegadores (el portal del tercero y la web de la empresa embebida).
 * «Sin recargar» se demuestra con una marca en la ventana de cada pantalla: si la página se recargara, la marca se perdería.
 */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
/** Cuánto se espera a que el aviso viaje por el stream y la pantalla se refresque (la suscripción de eventos corre cada pocos segundos). */
const EN_VIVO = { timeout: 30_000 } as const;

type ConMarca = Window & { __sinRecargar?: string };

/** Deja una marca en la ventana: si la página se recarga, desaparece. */
async function sellar(destino: Page | Frame): Promise<void> {
  await destino.evaluate(() => {
    (window as ConMarca).__sinRecargar = 'viva';
  });
}

async function sigueSinRecargar(destino: Page | Frame): Promise<boolean> {
  return destino.evaluate(() => (window as ConMarca).__sinRecargar === 'viva');
}

/** La ventana (frame) de Validasoft embebido, para sellarla y revisarla. */
async function marcoDe(app: FrameLocator): Promise<Frame> {
  const frame = await (await app.owner().elementHandle()).contentFrame();
  if (!frame) throw new Error('No se encontró la ventana de Validasoft embebido.');
  return frame;
}

/** Cuántas notificaciones sin leer dice la campana (la del portal y la de la web se llaman igual). */
async function noLeidas(campana: Locator): Promise<number> {
  const etiqueta = (await campana.getAttribute('aria-label')) ?? '';
  return Number(/(\d+) sin leer/.exec(etiqueta)?.[1] ?? 0);
}

const campanaDelTercero = (page: Page) => page.getByRole('button', { name: /^Notificaciones(, \d+ sin leer)?$/ });
const campanaDeLaEmpresa = (app: FrameLocator) => app.getByRole('button', { name: /^Notificaciones(, \d+ sin leer)?$/ });

/** Ventana aparte (otra persona, otro navegador) con el vínculo del tercero abierto en la web de la empresa. */
async function vinculoEnLaEmpresa(browser: Browser, e: Escenario, persona: Persona, vinculoId: string): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, e.norte, `vinculos/${vinculoId}`);
}

/** El tercero registra una cuenta y sube la certificación bancaria (sin designarla todavía). */
async function registrarCuentaConCertificacion(page: Page): Promise<void> {
  await page.goto('/cuentas');
  await page.getByRole('button', { name: 'Agregar cuenta' }).first().click();
  const nueva = page.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
  await nueva.getByRole('combobox', { name: 'Banco' }).fill('Bancolombia'); // el banco sale del catálogo
  await page.getByRole('option', { name: 'Bancolombia' }).click();
  await nueva.getByRole('radio', { name: 'Ahorros' }).check();
  await nueva.getByLabel('Número de cuenta').fill('031-123456-78');
  await nueva.getByLabel('Titular de la cuenta').fill('Ferretería El Tornillo S.A.S.');
  await nueva.getByLabel('Identificación del titular').fill('NIT 900123456');
  await nueva.getByRole('button', { name: 'Guardar cuenta' }).click();
  await expect(page.getByText(/· \*\*\*\*5678 ·/)).toBeVisible();

  // La certificación bancaria se sube desde el enlace que le muestra el diálogo al intentar designar sin ella.
  const porEmpresa = page.locator('section', { hasText: 'En qué cuenta te paga cada empresa' });
  await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
  await page.getByRole('link', { name: 'Subir certificación bancaria' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('Certificacion Bancolombia Tornillo'));
  await expect(page.getByText(/Es tu versión 1 de Certificación bancaria/)).toBeVisible({ timeout: 30_000 });
}

test('el tercero designa su cuenta: la ficha ya abierta de la empresa la muestra y la campana se enciende sin recargar; la aprobación vuelve al portal', async ({ page, browser }) => {
  const e = await crearEscenario();
  const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
  const enNorte = await invitar(e, e.norte, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf('RUT Tornillo') },
    { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
  ]);
  await registrarCuentaConCertificacion(page);

  // Olga y Ana (quienes aprueban cuentas) tienen el vínculo abierto: aún no hay cuenta designada.
  const olga = await vinculoEnLaEmpresa(browser, e, e.olga, enNorte.vinculoId);
  const ana = await vinculoEnLaEmpresa(browser, e, e.ana, enNorte.vinculoId);
  const tarjetaOlga = olga.getByRole('region', { name: 'Cuenta bancaria designada' }).or(olga.locator('section', { hasText: 'Cuenta bancaria designada' }));
  const tarjetaAna = ana.getByRole('region', { name: 'Cuenta bancaria designada' }).or(ana.locator('section', { hasText: 'Cuenta bancaria designada' }));
  await expect(tarjetaOlga).toContainText('El tercero aún no ha designado una cuenta');
  await expect(tarjetaAna).toContainText('El tercero aún no ha designado una cuenta');
  const marcoOlga = await marcoDe(olga);
  const marcoAna = await marcoDe(ana);
  await sellar(marcoOlga);
  await sellar(marcoAna);
  const antesOlga = await noLeidas(campanaDeLaEmpresa(olga));

  // El tercero designa la cuenta para Norte.
  await page.goto('/cuentas');
  const porEmpresa = page.locator('section', { hasText: 'En qué cuenta te paga cada empresa' });
  await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
  await expect(page.getByText(`Pendiente de aprobación de ${e.norte.nombre}`)).toBeVisible();
  await sellar(page);

  // La ficha abierta de Olga y la de Ana muestran la cuenta por aprobar, con el número completo, sin recargar.
  for (const tarjeta of [tarjetaOlga, tarjetaAna]) {
    await expect(tarjeta).toContainText('Por aprobar', EN_VIVO);
    await expect(tarjeta).toContainText('03112345678');
    await expect(tarjeta).toContainText('0 de 2 aprobaciones');
  }
  // La campana de Olga se enciende con el aviso de lo que hay que aprobar y la lleva al vínculo.
  await expect(async () => expect(await noLeidas(campanaDeLaEmpresa(olga))).toBeGreaterThan(antesOlga)).toPass({ timeout: 30_000 });
  await campanaDeLaEmpresa(olga).click();
  const aviso = olga.getByRole('listitem').filter({ hasText: 'Cuenta bancaria por aprobar' }).first();
  await expect(aviso).toContainText(`${tercero.nombre} propuso la cuenta Bancolombia ****5678`);
  await expect(aviso).toContainText(e.norte.nombre);
  await olga.owner().page().screenshot({ path: 'test-results/tiempo-real-empresa-cuenta.png' });
  await olga.owner().page().keyboard.press('Escape');

  // Olga la aprueba: Ana, sin recargar, ve que falta solo ella.
  await tarjetaOlga.getByRole('button', { name: 'Aprobar' }).click();
  await olga.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaOlga).toContainText('1 de 2 aprobaciones: Olga Oficial');
  await expect(tarjetaAna).toContainText('1 de 2 aprobaciones: Olga Oficial', EN_VIVO);

  // Ana completa las dos: queda designada para las dos personas y para el tercero, que sigue en «Mis cuentas» sin recargar.
  await tarjetaAna.getByRole('button', { name: 'Aprobar' }).click();
  await ana.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaAna).toContainText('Designada');
  await expect(tarjetaOlga).toContainText('Ana Administradora', EN_VIVO);
  await expect(page.getByText(`Designada para ${e.norte.nombre}`)).toBeVisible(EN_VIVO);
  await campanaDelTercero(page).click();
  const avisoDelTercero = page.getByRole('dialog', { name: 'Notificaciones' }).getByRole('listitem').filter({ hasText: 'Cuenta aprobada' });
  await expect(avisoDelTercero).toContainText(`${e.norte.nombre} aprobó la cuenta Bancolombia ****5678`, EN_VIVO);
  await page.screenshot({ path: 'test-results/tiempo-real-portal-cuenta.png' });

  // Nadie recargó: lo que se vio llegó por el stream.
  expect(await sigueSinRecargar(page)).toBe(true);
  expect(await sigueSinRecargar(marcoOlga)).toBe(true);
  expect(await sigueSinRecargar(marcoAna)).toBe(true);
});

test('la empresa observa y el portal del tercero, ya abierto, lo muestra con la campana encendida; el tercero responde y la empresa lo ve, sin recargar', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Aceros del Norte S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Aceros v1') },
    { nombre: CAMARA, archivo: pdf('Camara Aceros v1') },
  ]);

  // El tercero deja abierta «Tu revisión» (en revisión) y Rubén, el expediente del tercero.
  await expect(fraseDeEstado(page, 'EnRevision')).toBeVisible();
  await sellar(page);
  const antes = await noLeidas(campanaDelTercero(page));
  const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben);
  await abrirDesdeBandeja(ruben, invitacion.nombre, 'Compras');
  const marcoRuben = await marcoDe(ruben);
  await sellar(marcoRuben);

  // La empresa observa el RUT.
  await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'La página 2 está cortada.');

  // Sin recargar, el portal del tercero pasa a «Observado» y su campana se enciende con el aviso de la empresa.
  await expect(fraseDeEstado(page, 'Observado')).toBeVisible(EN_VIVO);
  await expect(async () => expect(await noLeidas(campanaDelTercero(page))).toBeGreaterThan(antes)).toPass({ timeout: 30_000 });
  await campanaDelTercero(page).click();
  const aviso = page.getByRole('dialog', { name: 'Notificaciones' }).getByRole('listitem').filter({ hasText: 'Tienes observaciones por corregir' });
  await expect(aviso).toContainText(`${e.norte.nombre} revisó tu validación`);
  await page.screenshot({ path: 'test-results/tiempo-real-portal-observado.png' });
  await page.keyboard.press('Escape');

  // El tercero corrige y responde desde la misma pantalla; Rubén, con el expediente abierto, ve que ya no está devuelto.
  await expect(ruben.getByRole('button', { name: 'Recordar al tercero' })).toBeVisible();
  await subirDocumento(page, RUT, pdf('RUT Aceros corregido'), { version: 2 });
  await responder(page, 'Adjunto el RUT completo.');
  await expect(ruben.getByRole('button', { name: 'Recordar al tercero' })).toBeHidden(EN_VIVO);
  await ruben.owner().page().screenshot({ path: 'test-results/tiempo-real-empresa-respuesta.png' });

  // El portal del tercero (que navegó dentro de la app) y la web de la empresa nunca se recargaron.
  expect(await sigueSinRecargar(page)).toBe(true);
  expect(await sigueSinRecargar(marcoRuben)).toBe(true);
});
