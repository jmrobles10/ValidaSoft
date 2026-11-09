import { expect, test, type Browser } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero, esperarTarjeta, subirDocumento, tarjeta } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Empresa, type Escenario, type Persona } from '../soporte/semilla';

/** Versiones nuevas de un documento: durante la revisión, después de aprobado y cargas que no se admiten. */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

/** Fecha `YYYY-MM-DD` de hace `dias` días (zona de Bogotá). */
function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

async function embebido(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa) {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa);
}

test('una versión nueva del RUT durante la revisión invalida lo que Compras ya había aprobado sobre él', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Eléctricos del Caribe S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Electricos v1') },
    { nombre: CAMARA, archivo: pdf('Camara Electricos v1') },
  ]);

  const ruben = await embebido(browser, e, e.ruben, e.norte);
  await abrirDesdeBandeja(ruben, invitacion.nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');

  // Sin que nadie se lo pida, el tercero actualiza su RUT.
  await subirDocumento(page, RUT, pdf('RUT Electricos v2 actualizado'), { version: 2 });
  await esperarTarjeta(page, RUT, 'Versión 2');

  // Compras vuelve a tener el RUT por decidir, ahora en la versión 2; Cumplimiento también lo ve en la versión 2.
  await abrirDesdeBandeja(ruben, invitacion.nombre, 'Compras');
  await expect(regionDeArea(ruben, 'Compras')).toContainText(`${RUT} · v2`);
  await expect(regionDeArea(ruben, 'Compras').getByRole('button', { name: 'Aprobar área (1 documento)' })).toBeVisible();
  await expect(regionDeArea(ruben, 'Cumplimiento')).toContainText(`${RUT} · v2`);
});

test('quien decide con la pantalla vieja no aprueba una versión superada: se le avisa y decide sobre la nueva', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Maderas y Formaletas S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Maderas v1') },
    { nombre: CAMARA, archivo: pdf('Camara Maderas v1') },
  ]);

  // Olga abre el expediente con el RUT v1...
  const olga = await embebido(browser, e, e.olga, e.norte);
  await abrirDesdeBandeja(olga, invitacion.nombre, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText(`${RUT} · v1`);

  // ...mientras el tercero sube la versión 2.
  await subirDocumento(page, RUT, pdf('RUT Maderas v2'), { version: 2 });
  await esperarTarjeta(page, RUT, 'Versión 2');

  // Al aprobar con lo que tenía en pantalla, el servidor responde «versión superada»: no se aprueba nada.
  await regionDeArea(olga, 'Cumplimiento').getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = olga.getByRole('dialog', { name: /^Aprobar Cumplimiento/ });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo.getByText(/Cambió lo que hay por decidir en: Registro Único Tributario \(el tercero subió una versión nueva\)/)).toBeVisible();
  await expect(dialogo.getByRole('button', { name: /^Aprobar área/ })).toBeDisabled();
  await dialogo.getByRole('button', { name: 'Cerrar' }).click();

  // Con el expediente recargado ve la versión 2 y aprueba sobre ella.
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText(`${RUT} · v2`);
  await aprobarArea(olga, 'Cumplimiento');
  await expect(regionDeArea(olga, 'Cumplimiento')).toContainText('Aprobada');
});

test('cargas que no se admiten: formato distinto al exigido y el mismo archivo otra vez', async ({ page }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Seguridad Integral Ltda.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Seguridad v1') },
    { nombre: CAMARA, archivo: pdf('Camara Seguridad v1') },
  ]);

  // Una imagen donde solo se admite PDF.
  await tarjeta(page, RUT).getByRole('link', { name: /^Subir/ }).click();
  let selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles({ name: 'rut.png', mimeType: 'image/png', buffer: Buffer.from('89504e470d0a1a0a0000000d49484452', 'hex') });
  await expect(page.getByRole('alert').filter({ hasText: 'Este formato no se acepta para este documento. Sube un archivo PDF.' })).toBeVisible();

  // El mismo PDF que ya es la versión 1.
  await page.goBack();
  await tarjeta(page, RUT).getByRole('link', { name: /^Subir/ }).click();
  selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT Seguridad v1'));
  await expect(page.getByRole('alert').filter({ hasText: 'Es el mismo archivo que ya tienes' })).toContainText('Es idéntico a tu versión 1: no hace falta subirlo de nuevo.');

  // Nada de eso cambió el RUT: sigue en la versión 1 y la revisión sigue igual.
  await page.goBack();
  await esperarTarjeta(page, RUT, 'Versión 1');
});

test('después de aprobado, el proveedor actualiza su RUT desde la bóveda: llega a Norte y a Sur y los vínculos siguen activos', async ({ page, browser }) => {
  const e = await crearEscenario();
  const tercero = nuevoTercero('Aceros del Pacífico S.A.S.');
  const enNorte = await invitar(e, e.norte, tercero);
  const enSur = await invitar(e, e.sur, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf('RUT Pacifico v1') },
    { nombre: CAMARA, archivo: pdf('Camara Pacifico v1') },
  ]);
  await completarInvitacion(page, enSur, [{ nombre: SST.nombre, archivo: pdf('SST Pacifico'), campos: { 'Fecha de expedición': haceDias(2) } }]);

  const ruben = await embebido(browser, e, e.ruben, e.norte);
  await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await embebido(browser, e, e.olga, e.norte);
  await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
  await aprobarArea(olga, 'Cumplimiento');
  const sofia = await embebido(browser, e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');

  // La Cámara vence en 30 días: aunque la revisión está aprobada, la tarjeta ofrece renovarla.
  await page.goto('/empresas');
  await abrirEmpresa(page, e.norte.nombre);
  await esperarEstadoDelTercero(page, 'Aprobado');
  await expect(tarjeta(page, CAMARA).getByRole('link', { name: /^Subir/ })).toBeVisible();

  // El RUT se actualiza una sola vez, desde la bóveda.
  await page.goto('/boveda');
  const rutEnBoveda = page.getByRole('article', { name: RUT });
  await rutEnBoveda.getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT Pacifico v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

  // Las dos empresas tienen la versión 2.
  await expect(async () => {
    await page.goto('/boveda');
    const conEmpresas = page.getByRole('article', { name: RUT }).getByRole('list', { name: 'Con tus empresas' });
    await expect(conEmpresas.getByRole('listitem').filter({ hasText: e.norte.nombre })).toContainText('v2', { timeout: 1500 });
    await expect(conEmpresas.getByRole('listitem').filter({ hasText: e.sur.nombre })).toContainText('v2', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });

  // En Norte el vínculo sigue activo con su certificado vigente y el RUT compartido ya es la versión 2.
  const vinculo = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, `vinculos/${enNorte.vinculoId}`);
  await expect(vinculo.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
  await expect(vinculo.getByText(/^Activo$/).first()).toBeVisible();
  // El certificado sigue vigente; vence con el documento que vence primero (la Cámara, a los 30 días).
  await expect(vinculo.getByRole('main')).toContainText(new RegExp(`Vigente\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-1`));
  await expect(vinculo.getByRole('link', { name: 'Página de verificación' })).toBeVisible();
  // «Documentos compartidos»: cada uno con su botón «Ver o descargar {documento}, versión n».
  await expect(vinculo.getByRole('button', { name: `Ver o descargar ${RUT}, versión 2` })).toBeVisible();
  await expect(vinculo.getByRole('button', { name: `Ver o descargar ${CAMARA}, versión 1` })).toBeVisible();
});
