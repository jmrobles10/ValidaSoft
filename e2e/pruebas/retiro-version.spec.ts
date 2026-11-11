import { expect, test, type Browser } from '@playwright/test';
import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { pdf } from '../soporte/archivos';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST, type Empresa, type Escenario, type Persona } from '../soporte/semilla';

/**
 * El tercero retira una versión (RT-1…RT-5, docs/02 §10.3): antes de confirmar ve lo que va a pasar con cada empresa, la empresa que
 * la había aprobado recibe la revalidación de la versión anterior con el motivo, y la tarjeta del documento lo cuenta sin
 * contradicciones. Deja las capturas (modo claro) en `E2E_CAPTURAS` (por omisión `test-results/retiro`).
 */
const CAPTURAS = process.env.E2E_CAPTURAS ?? join('test-results', 'retiro');
mkdirSync(CAPTURAS, { recursive: true });
const captura = (nombre: string) => join(CAPTURAS, `${nombre}.png`);

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';
const MOTIVO = 'Subí el RUT de otra empresa';

/** Fecha `YYYY-MM-DD` de hace `dias` días (zona de Bogotá). */
function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

async function embebido(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa, ruta?: string) {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa, ruta);
}

test('el tercero intenta retirar la versión que Norte aprobó: ve el impacto, confirma y Norte recibe la revalidación con el motivo', async ({ page, browser }) => {
  test.setTimeout(360_000);
  await page.setViewportSize({ width: 1180, height: 900 });
  const e = await crearEscenario();
  const tercero = nuevoTercero('Aceros del Retiro S.A.S.');
  const enNorte = await invitar(e, e.norte, tercero);
  const enSur = await invitar(e, e.sur, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf('RUT Retiro v1') },
    { nombre: CAMARA, archivo: pdf('Camara Retiro v1') },
  ]);
  await completarInvitacion(page, enSur, [{ nombre: SST.nombre, archivo: pdf('SST Retiro'), campos: { 'Fecha de expedición': haceDias(2) } }]);

  // Las dos empresas aprueban lo suyo con el RUT v1.
  const ruben = await embebido(browser, e, e.ruben, e.norte);
  await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await embebido(browser, e, e.olga, e.norte);
  await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
  await aprobarArea(olga, 'Cumplimiento');
  const sofia = await embebido(browser, e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');

  // Una versión 2 del RUT, desde la bóveda: las dos empresas la revalidan y Norte la aprueba (Sur la deja en revisión).
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT Retiro v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
  await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await expect(ruben.getByRole('region', { name: 'Compras', exact: true }).first()).toContainText(`${RUT} · v2`);
  await aprobarArea(ruben, 'Compras');
  await abrirDesdeBandeja(olga, tercero.nombre, 'Cumplimiento');
  await aprobarArea(olga, 'Cumplimiento');

  // La tarjeta antes de retirar: Norte aprobó la 2 y Sur, que había aprobado la 1, está revisando la 2 en su revalidación.
  const tarjetaRut = () => page.getByRole('article', { name: RUT });
  const conEmpresas = () => tarjetaRut().getByRole('list', { name: 'Con tus empresas' });
  await expect(async () => {
    await page.goto('/boveda');
    await expect(conEmpresas().getByRole('listitem').filter({ hasText: e.norte.nombre })).toContainText('Aprobó la versión 2, la vigente.', { timeout: 1500 });
    await expect(conEmpresas().getByRole('listitem').filter({ hasText: e.sur.nombre })).toContainText('Aprobó la versión 1. Debe revisar la versión 2, la vigente: revalidación abierta.', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });

  // «Retirar esta versión»: antes del motivo, lo que va a pasar con cada empresa y la alternativa de subir una versión corregida.
  await tarjetaRut().getByRole('button', { name: `Versión 2: ${RUT}` }).click();
  await page.getByRole('menuitem', { name: 'Retirar esta versión' }).click();
  const dialogo = page.getByRole('dialog', { name: `Retirar la versión 2 de ${RUT}` });
  await expect(dialogo.getByText(`Tu ${RUT} vigente pasará a ser la versión 1.`)).toBeVisible();
  await expect(dialogo.getByText(`${e.norte.nombre} la aprobó: tendrá que revisar de nuevo tu ${RUT} versión 1.`)).toBeVisible();
  await expect(dialogo.getByText(new RegExp(`^${e.sur.nombre} la está revisando \\(${e.sur.prefijo}-E-\\d{4}-\\d{6}\\): su revisión pasa a la versión 1 y tendrá que volver a revisarla\\.$`))).toBeVisible();
  await expect(dialogo.getByRole('link', { name: 'Subir una versión corregida' })).toBeVisible();
  await expect(dialogo.getByText('Les avisamos con tu motivo.', { exact: false })).toBeVisible();
  await expect(dialogo.getByRole('button', { name: 'Retirar versión' })).toBeDisabled();
  await dialogo.getByLabel(/Motivo/).fill(MOTIVO);
  await dialogo.screenshot({ path: captura('retiro-dialogo-con-impacto') });
  await dialogo.getByRole('button', { name: 'Retirar versión' }).click();
  await expect(page.getByText('Retiraste la versión 2.')).toBeVisible();
  await expect(dialogo).toBeHidden();

  // La tarjeta lo cuenta sin contradecirse: la vigente es la 1; Norte aprobó la 2 (que retiraste) y debe revisar la 1.
  await expect(async () => {
    await page.goto('/boveda');
    await expect(tarjetaRut()).toContainText('Versión 1 ·', { timeout: 1500 });
    await expect(conEmpresas().getByRole('listitem').filter({ hasText: e.norte.nombre })).toContainText(
      'Aprobó la versión 2, que retiraste. Debe revisar la versión 1: revalidación abierta.',
      { timeout: 1500 },
    );
    await expect(conEmpresas().getByRole('listitem').filter({ hasText: e.sur.nombre })).toContainText('Revisando la versión 1.', { timeout: 1500 });
  }).toPass({ timeout: 60_000 });
  await expect(tarjetaRut()).not.toContainText(/cuando la exija|recibirá la nueva/);
  await tarjetaRut().getByRole('button', { name: 'Ver versiones (2)' }).click();
  const versiones = tarjetaRut().locator('ol > li');
  await expect(versiones.filter({ hasText: 'Versión 2' })).toContainText('Retirada');
  await expect(versiones.filter({ hasText: 'Versión 1' })).toContainText('Vigente');
  await expect(tarjetaRut()).not.toContainText(/Actual/);
  await tarjetaRut().screenshot({ path: captura('retiro-tarjeta-varias-empresas') });

  // Norte ve la revalidación con el motivo: en la bandeja de revalidaciones, en el expediente y en su línea de tiempo.
  const revalidaciones = await embebido(browser, e, e.ruben, e.norte, 'revalidaciones');
  await expect(revalidaciones.getByRole('heading', { level: 1, name: 'Revalidaciones' })).toBeVisible();
  const fila = revalidaciones.getByRole('row').filter({ hasText: tercero.nombre });
  await expect(fila).toContainText(RUT);
  await expect(fila).toContainText(`El tercero retiró la v2: «${MOTIVO}»`);
  await expect(fila).toContainText('v2 → v1');
  // Una revalidación que va hacia atrás no se aprueba en lote.
  await expect(revalidaciones.getByRole('button', { name: /Aprobar .* sin cambios/ })).toHaveCount(0);
  await fila.click();
  await expect(revalidaciones.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
  await expect(revalidaciones.getByRole('main')).toContainText(`El tercero retiró la versión 2 (motivo: ${MOTIVO}). Vuelve la versión 1: revísala de nuevo.`);
  await expect(revalidaciones.getByRole('main')).toContainText(`El tercero retiró la versión 2 de ${RUT} (motivo: ${MOTIVO}); vuelve a la versión 1.`);

  // Y le llega el aviso por correo, con el motivo.
  const correo = await esperarCorreo(e.ruben.correo, /^Versión retirada por el tercero: /);
  expect(correo.texto).toContain(MOTIVO);
});
