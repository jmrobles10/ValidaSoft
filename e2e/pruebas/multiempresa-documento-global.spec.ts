import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, regionDeArea } from '../soporte/empresa';
import { abrirFichaAceptada, abrirSeccion, guardarSeccionCompleta, llenarGeneralJuridica, publicarFlujoDeFicha } from '../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, esperarTarjeta, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Empresa, type Escenario, type Persona } from '../soporte/semilla';

/**
 * Dos empresas del grupo exigen lo mismo del mismo tercero (el RUT y la información general): un solo RUT en la bóveda, una sola ficha
 * y cada empresa decide en privado (M-8, FM-3, G-1). Las dos validan; cuando el tercero sube un RUT nuevo y cambia su información, cada
 * una recibe su propia revalidación: una la resuelve y la otra sigue viendo lo que aceptó (desactualizado) sin enterarse de la decisión
 * de la primera, y el tercero ve el estado distinto con cada una.
 */

const RUT = 'Registro Único Tributario';
const DIRECCION_VIEJA = 'Calle 45 # 12-08';
const DIRECCION_NUEVA = 'Calle 100 # 19-54';

async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa);
}

/** Aprueba lo que hay en el área: abre el expediente desde la bandeja cuando ya está (llega por una cola) y pulsa «Aprobar área». */
async function aprobarDesdeBandeja(app: FrameLocator, tercero: string, area: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(app, area);
}

/** «Mis empresas» → la tarjeta de la empresa → su revisión; devuelve la dirección de esa revisión. */
async function abrirRevisionCon(page: Page, empresa: string): Promise<void> {
  await page.goto('/empresas');
  const tarjeta = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: empresa, exact: true }) });
  await expect(tarjeta).toBeVisible();
  await expect(async () => {
    if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await tarjeta.getByRole('link').first().click({ timeout: 3000 });
    await expect(page).toHaveURL(/\/expediente\//, { timeout: 3000 });
  }).toPass({ timeout: 20_000 });
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${empresa}` })).toBeVisible();
}

// Flujos largos de punta a punta: con la máquina cargada el tope de 120 s por omisión queda justo.
test.setTimeout(180_000);

test('Norte y Sur validan el mismo RUT y la misma información; con una versión nueva cada una revalida por su cuenta y la decisión de una no se ve en la otra', async ({ page, browser }) => {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  const requisitos = [{ tipoDocumento: 'RUT' }, { seccion: 'FICHA_GENERAL' }];
  await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', e.norte.areas.compras!, 'ALIADO', requisitos);
  await publicarFlujoDeFicha(e, e.sur, 'ALIADOS_SUR', e.sur.areas.revision!, 'ALIADO', requisitos);
  const tercero = nuevoTercero('Aceros Compartidos S.A.S.');
  const enNorte = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
  const enSur = await invitarVinculo(e, e.sur, tercero, 'ALIADO');

  // El tercero acepta a Norte, diligencia su información general y sube su RUT: una sola vez.
  await entrarPorInvitacion(page, enNorte);
  await aceptarInvitacion(page, enNorte);
  await llenarGeneralJuridica(page, tercero.nombre);
  await abrirRevisionCon(page, e.norte.nombre);
  await subirDocumento(page, RUT, pdf('RUT compartido v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Al aceptar a Sur, el RUT y la información ya están en su bóveda y le llegan completos: sin subir ni diligenciar nada más.
  await entrarPorInvitacion(page, enSur);
  await aceptarInvitacion(page, enSur);
  await esperarEstadoDelTercero(page, 'EnRevision');

  // Cada empresa valida en su área, por su cuenta.
  const ruben = await ventanaDe(browser, e, e.ruben, e.norte);
  const sofia = await ventanaDe(browser, e, e.sofia, e.sur);
  await aprobarDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await aprobarDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await abrirRevisionCon(page, e.norte.nombre);
  await esperarEstadoDelTercero(page, 'Aprobado');
  await abrirRevisionCon(page, e.sur.nombre);
  await esperarEstadoDelTercero(page, 'Aprobado');

  // Las dos aceptaron la versión 1 de la información: la ven en su ficha aceptada.
  for (const app of [ruben, sofia]) {
    await expect(async () => {
      await abrirFichaAceptada(app, tercero.nombre);
      await expect(app.getByRole('region', { name: 'Información general' })).toContainText('Aceptada · versión 1', { timeout: 1500 });
    }).toPass({ timeout: 45_000 });
    await expect(app.getByRole('main')).not.toContainText('Versión nueva del tercero', { timeout: 1500 });
  }

  // El tercero sube un RUT nuevo a su bóveda y cambia su dirección: una versión nueva para las dos empresas.
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT compartido v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
  await abrirSeccion(page, 'general', 'Información general');
  await expect(page.getByRole('textbox', { name: 'Dirección', exact: true })).toHaveValue(DIRECCION_VIEJA);
  await page.getByRole('textbox', { name: 'Dirección', exact: true }).fill(DIRECCION_NUEVA);
  // El cambio necesita su soporte: el RUT nuevo, que es el documento global que las dos empresas ven.
  await page.getByRole('checkbox', { name: /^Registro Único Tributario · versión 2/ }).check();
  await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');

  // Cada empresa recibe su revalidación con las dos novedades; Norte la resuelve y Sur todavía no.
  await aprobarDesdeBandeja(ruben, tercero.nombre, 'Compras');

  // Norte ya acepta la versión 2; Sur sigue con lo que aceptó (versión 1) y ve la versión nueva del tercero como pendiente de revisión.
  await expect(async () => {
    await abrirFichaAceptada(ruben, tercero.nombre);
    await expect(ruben.getByRole('region', { name: 'Información general' })).toContainText('Aceptada · versión 2', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(ruben.getByRole('main')).not.toContainText('Versión nueva del tercero');
  await expect(async () => {
    await abrirFichaAceptada(sofia, tercero.nombre);
    await expect(sofia.getByText('1 sección tiene una versión nueva pendiente de revisión')).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(sofia.getByRole('region', { name: 'Información general' })).toContainText('Aceptada · versión 1');
  const posterior = sofia.getByRole('region', { name: 'Versión nueva del tercero, pendiente de revisión' });
  const cambios = posterior.getByRole('table', { name: 'Qué cambió frente a la versión 1 aceptada' });
  await expect(cambios).toContainText(DIRECCION_VIEJA);
  await expect(cambios).toContainText(DIRECCION_NUEVA);

  // La revalidación de Sur sigue abierta y no dice nada de lo que decidió Norte: ni la empresa, ni su área, ni su decisión.
  await abrirDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await expect(regionDeArea(sofia, 'Revisión').getByRole('button', { name: /^Aprobar área/ })).toBeVisible();
  const principal = sofia.getByRole('main');
  await expect(principal).not.toContainText(e.norte.nombre);
  await expect(principal).not.toContainText('Compras');
  await expect(principal).not.toContainText(e.ruben.nombre);

  // El tercero ve un estado distinto con cada empresa: con Norte sus dos novedades aprobadas y con Sur todavía en revisión.
  await abrirRevisionCon(page, e.norte.nombre);
  await esperarTarjeta(page, RUT, 'Aprobado');
  await esperarTarjeta(page, 'Información general', 'Aprobado');
  await abrirRevisionCon(page, e.sur.nombre);
  await esperarTarjeta(page, RUT, 'En revisión');
  await esperarTarjeta(page, 'Información general', 'En revisión');

  // Sur resuelve la suya y acepta la versión 2: cada una llegó a lo mismo por su cuenta.
  await aprobarArea(sofia, 'Revisión');
  await expect(async () => {
    await abrirFichaAceptada(sofia, tercero.nombre);
    await expect(sofia.getByRole('region', { name: 'Información general' })).toContainText('Aceptada · versión 2', { timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await expect(sofia.getByRole('main')).not.toContainText('Versión nueva del tercero');
  await abrirRevisionCon(page, e.sur.nombre);
  await esperarTarjeta(page, RUT, 'Aprobado');
  await esperarTarjeta(page, 'Información general', 'Aprobado');
});
