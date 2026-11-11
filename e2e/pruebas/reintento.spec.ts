import { expect, test, type Page } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, rechazar } from '../soporte/empresa';
import {
  crearTipoDeVinculo,
  invitarVinculo,
  leerExpediente,
  leerVinculo,
  publicarFlujoSinDocumentos,
  type OpcionesDeFlujo,
} from '../soporte/flujos';
import { aceptarInvitacion, entrarPorInvitacion } from '../soporte/proveedor';
import { crearEscenario, nuevoTercero, type Escenario, type Invitacion } from '../soporte/semilla';

/**
 * Reinicio tras un rechazo (DS-4): el tercero ve en «Mis empresas» el rechazo con los textos de los motivos y, si el flujo lo
 * permite (con sus días de espera), «Volver a postularme» abre una revisión nueva; si no, la empresa reabre desde la web.
 * Flujo de Norte sin documentos: Compras (Rubén) y Cumplimiento (Olga, puede rechazar) en paralelo.
 */

interface Caso {
  e: Escenario;
  invitacion: Invitacion;
}

/** El tercero acepta su invitación y Cumplimiento rechaza el expediente con un motivo. */
async function rechazado(page: Page, browser: import('@playwright/test').Browser, flujo: OpcionesDeFlujo): Promise<Caso> {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
  await publicarFlujoSinDocumentos(
    e,
    e.norte,
    'ALIADOS',
    'ALIADO',
    [[{ area: e.norte.areas.compras! }, { area: e.norte.areas.cumplimiento!, puedeRechazar: true }]],
    flujo,
  );
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
  await entrarPorInvitacion(page, invitacion);
  await aceptarInvitacion(page, invitacion);

  const ventana = await (await browser.newContext()).newPage();
  const olga = await abrirEmbebido(ventana, e, e.olga);
  await abrirDesdeBandeja(olga, invitacion.nombre, 'Cumplimiento');
  await rechazar(olga, ['No corresponde al tercero'], 'Aparece en una lista restrictiva con otro representante.');
  return { e, invitacion };
}

/** «Mis empresas» del tercero y la tarjeta de Norte. */
async function tarjetaDeNorte(page: Page, c: Caso) {
  await page.goto('/empresas');
  const tarjeta = page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: c.e.norte.nombre, exact: true }) });
  await expect(tarjeta).toBeVisible();
  return tarjeta;
}

test('el tercero rechazado ve los motivos y vuelve a postularse: se abre una revisión nueva', async ({ page, browser }) => {
  const c = await rechazado(page, browser, { reintento: { permitido: true, diasEspera: 0 } });

  const tarjeta = await tarjetaDeNorte(page, c);
  await expect(tarjeta.getByText(`${c.e.norte.nombre} cerró tu revisión sin aprobarla el`, { exact: false })).toBeVisible();
  // Lo que ve es el texto del motivo; nunca la justificación interna ni el área que rechazó.
  await expect(tarjeta.getByText('El documento no corresponde a la persona o empresa que se está validando.')).toBeVisible();
  await expect(tarjeta).not.toContainText('lista restrictiva');
  await expect(tarjeta).not.toContainText('Cumplimiento');

  await tarjeta.getByRole('button', { name: 'Volver a postularme' }).click();

  await expect(page).toHaveURL(/\/expediente\/[0-9a-f-]{36}$/);
  await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${c.e.norte.nombre}` })).toBeVisible();
  const vinculo = await leerVinculo(c.e, c.invitacion);
  expect(vinculo.estado).toBe('EnValidacion');
  expect((await leerExpediente(c.e, c.e.norte, vinculo.expedienteAbiertoId!)).estado).toBe('EnRevision');

  // Ya no hay rechazo que reintentar: la tarjeta cuenta el avance de la revisión nueva.
  const despues = await tarjetaDeNorte(page, c);
  await expect(despues.getByRole('button', { name: 'Volver a postularme' })).toHaveCount(0);
  await expect(despues.getByText(/cerró tu revisión sin aprobarla/)).toHaveCount(0);
});

test('con días de espera dice desde cuándo puede volver a postularse', async ({ page, browser }) => {
  const c = await rechazado(page, browser, { reintento: { permitido: true, diasEspera: 30 } });

  const tarjeta = await tarjetaDeNorte(page, c);
  await expect(tarjeta.getByText(/Podrás volver a postularte desde el /)).toBeVisible();
  await expect(tarjeta.getByRole('button', { name: 'Volver a postularme' })).toHaveCount(0);
  expect((await leerVinculo(c.e, c.invitacion)).estado).toBe('Invitado');
});

test('si el flujo no lo permite, el tercero ve a quién escribirle y la empresa reabre desde la web', async ({ page, browser }) => {
  const c = await rechazado(page, browser, {});

  const tarjeta = await tarjetaDeNorte(page, c);
  await expect(tarjeta.getByText(`escríbele a ${c.e.norte.nombre}`, { exact: false })).toBeVisible();
  await expect(tarjeta.getByRole('button', { name: 'Volver a postularme' })).toHaveCount(0);

  // La empresa (Ana) abre un expediente nuevo desde el vínculo; no depende del flujo ni de la espera.
  const ventana = await (await browser.newContext()).newPage();
  const ana = await abrirEmbebido(ventana, c.e, c.e.ana, c.e.norte, `vinculos/${c.invitacion.vinculoId}`);
  await expect(ana.getByRole('heading', { level: 1, name: c.invitacion.nombre })).toBeVisible();
  await expect(ana.getByText(/El último expediente se rechazó\. Abre uno nuevo/)).toBeVisible();
  await ana.getByRole('button', { name: 'Abrir expediente' }).click();
  await ana.getByRole('dialog', { name: 'Abrir expediente' }).getByRole('button', { name: 'Abrir expediente' }).click();
  await expect(ana.getByText(/^Expediente .* abierto\.$/)).toBeVisible();

  const vinculo = await leerVinculo(c.e, c.invitacion);
  expect(vinculo.estado).toBe('EnValidacion');
  expect((await leerExpediente(c.e, c.e.norte, vinculo.expedienteAbiertoId!)).estado).toBe('EnRevision');
});
