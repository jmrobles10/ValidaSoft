import { expect, test, type Browser, type FrameLocator, type Page } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion, regionDeArea } from '../soporte/empresa';
import { publicarFlujoDeFicha } from '../soporte/ficha';
import { crearTipoDeVinculo, invitarVinculo } from '../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, esperarTarjeta, subirDocumento } from '../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Empresa, type Escenario, type Persona, type Tercero } from '../soporte/semilla';

/**
 * Un tercero ya aprobado por dos empresas sube una versión nueva del RUT que las dos exigen (VR-1): en cada empresa el documento queda «En revisión»
 * y el vínculo se ve «En revisión» (no «Activo») en la lista, el filtro, el inicio y el detalle; el tercero lo ve igual en «Mis empresas», en su
 * revisión y en su documento (nunca «Aprobado» arriba y «En revisión» abajo). Una empresa la aprueba y su vínculo vuelve a «Activo» mientras la otra
 * sigue en revisión. El estado del vínculo y su certificado no cambian mientras tanto: el certificado queda «Con pendientes» (A-2, RV-3).
 */

const RUT = 'Registro Único Tributario';

// Flujo largo de punta a punta con dos empresas y tres personas: con la máquina cargada el tope de 120 s por omisión queda justo.
test.setTimeout(240_000);

async function ventanaDe(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa, ruta = 'bandeja'): Promise<FrameLocator> {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, empresa, ruta);
}

/** Aprueba lo que hay en el área: abre el expediente desde la bandeja cuando ya está (llega por una cola) y pulsa «Aprobar área». */
async function aprobarDesdeBandeja(app: FrameLocator, tercero: string, area: string): Promise<void> {
  await expect(async () => {
    await abrirDesdeBandeja(app, tercero, area);
    await expect(regionDeArea(app, area).getByRole('button', { name: /^Aprobar área/ })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
  await aprobarArea(app, area);
}

/** La tarjeta de la empresa en «Mis empresas» (el portal del tercero). */
function tarjetaDeEmpresa(page: Page, empresa: string) {
  return page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: empresa, exact: true }) });
}

/** «Mis empresas» → la tarjeta de la empresa → su revisión. */
async function abrirRevisionCon(page: Page, empresa: string): Promise<void> {
  await page.goto('/empresas');
  const tarjeta = tarjetaDeEmpresa(page, empresa);
  await expect(tarjeta).toBeVisible();
  await expect(async () => {
    if (/\/empresas\/?$/.test(new URL(page.url()).pathname)) await tarjeta.getByRole('link').first().click({ timeout: 3000 });
    await expect(page).toHaveURL(/\/expediente\//, { timeout: 3000 });
  }).toPass({ timeout: 20_000 });
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${empresa}` })).toBeVisible();
}

/** Recarga «Mis empresas» hasta que la tarjeta de la empresa muestre su estado (el cambio llega por una cola). */
async function esperarTarjetaDeEmpresa(page: Page, empresa: string, estado: 'En revisión' | 'Aprobado'): Promise<void> {
  await expect(async () => {
    await page.goto('/empresas');
    await expect(tarjetaDeEmpresa(page, empresa).getByText(estado, { exact: true })).toBeVisible({ timeout: 1500 });
  }).toPass({ timeout: 45_000 });
}

/** Lo que la empresa ve de ese vínculo en su lista: el estado visible de la fila. */
function filaDelVinculo(app: FrameLocator, tercero: Tercero) {
  return app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` });
}

/** Lista de vínculos de la empresa como `persona`, recién abierta (sin datos viejos), con la fila del tercero a la vista. */
async function listaDeVinculos(browser: Browser, e: Escenario, persona: Persona, empresa: Empresa, tercero: Tercero): Promise<FrameLocator> {
  const app = await ventanaDe(browser, e, persona, empresa, 'vinculos');
  await expect(app.getByRole('heading', { level: 1, name: 'Vínculos' })).toBeVisible();
  await expect(filaDelVinculo(app, tercero)).toBeVisible();
  return app;
}

async function filtrarPorEstado(app: FrameLocator, estado: string): Promise<void> {
  await app.getByRole('button', { name: /^Estado/ }).click();
  await app.getByRole('menuitem', { name: estado, exact: true }).click();
}

test('Un tercero aprobado por dos empresas sube una versión nueva: ambas, el portal y el documento muestran «En revisión»; una aprueba y vuelve a «Activo» mientras la otra sigue', async ({ page, browser }) => {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  const requisitos = [{ tipoDocumento: 'RUT' }];
  await publicarFlujoDeFicha(e, e.norte, 'ALIADOS_NORTE', e.norte.areas.compras!, 'ALIADO', requisitos);
  await publicarFlujoDeFicha(e, e.sur, 'ALIADOS_SUR', e.sur.areas.revision!, 'ALIADO', requisitos);
  const tercero = nuevoTercero('Aceros en Revisión S.A.S.');
  const enNorte = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
  const enSur = await invitarVinculo(e, e.sur, tercero, 'ALIADO');

  // El tercero acepta a las dos con su RUT en la bóveda, y cada empresa lo aprueba por su cuenta: dos vínculos activos y aprobados.
  await entrarPorInvitacion(page, enNorte);
  await aceptarInvitacion(page, enNorte);
  await subirDocumento(page, RUT, pdf('RUT en revisión v1'));
  await esperarEstadoDelTercero(page, 'EnRevision');
  await entrarPorInvitacion(page, enSur);
  await aceptarInvitacion(page, enSur);
  await esperarEstadoDelTercero(page, 'EnRevision');
  const ruben = await ventanaDe(browser, e, e.ruben, e.norte);
  const sofia = await ventanaDe(browser, e, e.sofia, e.sur);
  await aprobarDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await aprobarDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await esperarTarjetaDeEmpresa(page, e.norte.nombre, 'Aprobado');
  await esperarTarjetaDeEmpresa(page, e.sur.nombre, 'Aprobado');
  for (const [persona, empresa] of [[e.olga, e.norte], [e.sofia, e.sur]] as const) {
    const lista = await listaDeVinculos(browser, e, persona, empresa, tercero);
    await expect(filaDelVinculo(lista, tercero)).toContainText('Activo');
  }

  // El tercero sube un RUT nuevo desde su bóveda: una versión nueva para las dos empresas.
  await page.goto('/boveda');
  await page.getByRole('article', { name: RUT }).getByRole('link', { name: 'Subir versión' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('RUT en revisión v2'));
  await expect(page.getByText(`Es tu versión 2 de ${RUT}`)).toBeVisible({ timeout: 30_000 });

  // El tercero, en «Mis empresas»: las dos en revisión, ninguna «Aprobado» (el vínculo sigue activo, pero lo que le toca es lo de la actualización).
  await esperarTarjetaDeEmpresa(page, e.norte.nombre, 'En revisión');
  await esperarTarjetaDeEmpresa(page, e.sur.nombre, 'En revisión');
  for (const empresa of [e.norte.nombre, e.sur.nombre]) {
    const tarjeta = tarjetaDeEmpresa(page, empresa);
    await expect(tarjeta.getByText('Aprobado', { exact: true })).toHaveCount(0);
    await expect(tarjeta).toContainText(`${empresa} está revisando tu información.`);
  }
  await capturar(page, 'vinculo-en-revision-mis-empresas');

  // Su revisión con cada empresa: el encabezado y la tarjeta del documento dicen lo mismo (una sola verdad en pantalla).
  for (const empresa of [e.norte.nombre, e.sur.nombre]) {
    await abrirRevisionCon(page, empresa);
    await esperarEstadoDelTercero(page, 'EnRevision');
    await esperarTarjeta(page, RUT, 'En revisión');
    const principal = page.getByRole('main');
    await expect(principal.getByText('En revisión', { exact: true }).first()).toBeVisible();
    await expect(principal).not.toContainText('aprobó tu información');
    await expect(principal.getByText('Aprobado', { exact: true })).toHaveCount(0);
  }
  await capturar(page, 'vinculo-en-revision-portal');

  // Cada empresa ve su vínculo «En revisión» (no «Activo») en la lista y su filtro, con el RUT nuevo por decidir en su área.
  const listaNorte = await listaDeVinculos(browser, e, e.olga, e.norte, tercero);
  await expect(filaDelVinculo(listaNorte, tercero)).toContainText('En revisión');
  await expect(filaDelVinculo(listaNorte, tercero)).not.toContainText('Activo');
  await capturar(listaNorte.owner().page(), 'vinculo-en-revision-lista');
  await filtrarPorEstado(listaNorte, 'En revisión');
  await expect(filaDelVinculo(listaNorte, tercero)).toBeVisible();
  await filtrarPorEstado(listaNorte, 'Activo');
  await expect(listaNorte.getByText('Ningún vínculo coincide con esos filtros.')).toBeVisible();
  await expect(filaDelVinculo(listaNorte, tercero)).toHaveCount(0);

  const listaSur = await listaDeVinculos(browser, e, e.sofia, e.sur, tercero);
  await expect(filaDelVinculo(listaSur, tercero)).toContainText('En revisión');
  await expect(filaDelVinculo(listaSur, tercero)).not.toContainText('Activo');

  // El inicio de la empresa cuenta el vínculo en «En revisión» y no en «Activos»; la cifra lleva a la lista filtrada.
  const inicio = await ventanaDe(browser, e, e.olga, e.norte, 'inicio');
  const cifras = inicio.getByRole('region', { name: 'Vínculos' });
  await expect(cifras.getByRole('link', { name: 'En revisión' })).toBeVisible();
  await expect(cifras.getByText('Activos', { exact: true }).locator('xpath=ancestor::*[self::div or self::li][1]')).toContainText('0');
  await cifras.getByRole('link', { name: 'En revisión' }).click();
  await expect(filaDelVinculo(inicio, tercero)).toContainText('En revisión');

  // El detalle del vínculo: «En revisión» arriba y el certificado «Con pendientes» (sigue siendo válido: nada se suspende por la revisión).
  const detalleNorte = await ventanaDe(browser, e, e.olga, e.norte, `vinculos/${enNorte.vinculoId}`);
  await expect(detalleNorte.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
  // El chip del encabezado lleva su fecha («En revisión desde el …»); un «Activo desde el …» sería la segunda verdad. (El historial conserva sus chips «Activo».)
  await expect(detalleNorte.getByText(/^En revisión\. En revisión desde el /).first()).toBeVisible();
  await expect(detalleNorte.getByText(/^Activo\. Activo desde el /)).toHaveCount(0);
  await expect(detalleNorte.getByRole('main')).toContainText(new RegExp(`Con pendientes\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-1`));
  await capturar(detalleNorte.owner().page(), 'vinculo-en-revision-detalle');

  // Norte aprueba el RUT nuevo: su vínculo vuelve a «Activo» y el tercero lo ve «Aprobado» con Norte; Sur sigue en revisión por todos lados.
  await aprobarDesdeBandeja(ruben, tercero.nombre, 'Compras');
  await esperarTarjetaDeEmpresa(page, e.norte.nombre, 'Aprobado');
  await esperarTarjetaDeEmpresa(page, e.sur.nombre, 'En revisión');
  await expect(tarjetaDeEmpresa(page, e.sur.nombre).getByText('Aprobado', { exact: true })).toHaveCount(0);
  const norteDespues = await listaDeVinculos(browser, e, e.olga, e.norte, tercero);
  await expect(filaDelVinculo(norteDespues, tercero)).toContainText('Activo');
  await expect(filaDelVinculo(norteDespues, tercero)).not.toContainText('En revisión');
  const detalleNorteDespues = await ventanaDe(browser, e, e.olga, e.norte, `vinculos/${enNorte.vinculoId}`);
  await expect(detalleNorteDespues.getByText(/^Activo\. Activo desde el /).first()).toBeVisible();
  await expect(detalleNorteDespues.getByRole('main')).toContainText(new RegExp(`Vigente\\s*${e.norte.prefijo}-C-\\d{4}-\\d{6}-2`));
  const surSigue = await listaDeVinculos(browser, e, e.sofia, e.sur, tercero);
  await expect(filaDelVinculo(surSigue, tercero)).toContainText('En revisión');
  await abrirRevisionCon(page, e.sur.nombre);
  await esperarEstadoDelTercero(page, 'EnRevision');
  await esperarTarjeta(page, RUT, 'En revisión');

  // Sur la resuelve a su tiempo: también vuelve a «Activo» y «Aprobado».
  await aprobarDesdeBandeja(sofia, tercero.nombre, 'Revisión');
  await esperarTarjetaDeEmpresa(page, e.sur.nombre, 'Aprobado');
  const surDespues = await listaDeVinculos(browser, e, e.sofia, e.sur, tercero);
  await expect(filaDelVinculo(surDespues, tercero)).toContainText('Activo');
  await irASeccion(surDespues, 'Vínculos');
  await expect(filaDelVinculo(surDespues, tercero)).not.toContainText('En revisión');
});
