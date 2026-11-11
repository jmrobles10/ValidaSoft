import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../soporte/semilla';

/** Ficha maestra del tercero (F1, docs/14): el wizard «Mi información» con la completitud que decide el servidor (FM-16). */

const RUT = 'Registro Único Tributario';

test('el proveedor abre Mi información, guarda General a medias, ve lo que falta y no avanza hasta completarla', async ({ page }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Ferretería La Ficha S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Ficha') },
    { nombre: SST.nombre, archivo: pdf('SST Ficha'), campos: { 'Fecha de expedición': new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date()) } },
  ]);

  await page.getByRole('link', { name: 'Mi información' }).first().click();
  await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
  await expect(page.getByText('Sin diligenciar').first()).toBeVisible();
  await page.screenshot({ path: 'test-results/ficha-resumen.png', fullPage: true });

  // «Continuar» lleva a la primera sección pendiente: General.
  await page.getByRole('button', { name: 'Continuar' }).or(page.getByRole('link', { name: 'Continuar' })).first().click();
  await expect(page).toHaveURL(/\/ficha\/general/);

  // La General llega prellenada con lo que ya se sabe del tercero (FD-1): hay algo que guardar aunque no haya tocado nada, pero no se
  // puede continuar: la barra dice cuánto falta (lo evalúa la API, FM-46).
  const barra = page.getByRole('group', { name: 'Guardar la sección' });
  await expect(page.getByRole('alert').filter({ hasText: 'Prellenamos tu información' })).toBeVisible();
  await expect(page.getByRole('textbox', { name: 'Razón social' })).toHaveValue('Ferretería La Ficha S.A.S.');
  await expect(barra.getByText(/Faltan? \d+ datos? para continuar/)).toBeVisible();
  await expect(barra.getByRole('button', { name: 'Guardar y continuar' })).toBeDisabled();
  await expect(barra.getByRole('button', { name: 'Guardar', exact: true })).toBeEnabled();
  // El panel acompaña la sección: todas las secciones con su estado y la abierta marcada.
  const panel = page.getByRole('navigation', { name: 'Secciones de mi información' });
  await expect(panel.getByRole('link', { name: /^Información general:/ })).toHaveAttribute('aria-current', 'step');

  // Un dato a medias: «Guardar» deja el borrador con los faltantes exactos del servidor y el wizard no avanza.
  await page.getByRole('textbox', { name: 'Teléfono' }).fill('601 555 1234');
  await expect(barra.getByText('Tienes cambios sin guardar')).toBeVisible();
  await barra.getByRole('button', { name: 'Guardar', exact: true }).click();
  await expect(page.getByText(/Para completar esta sección faltan? \d+ datos?/)).toBeVisible();
  await expect(page).toHaveURL(/\/ficha\/general/);
  await expect(barra.getByRole('button', { name: 'Guardar y continuar' })).toBeDisabled();
  await expect(panel.getByRole('link', { name: /^Información general: Faltan? \d+ datos?$/ })).toBeVisible();
  await page.screenshot({ path: 'test-results/ficha-general-faltantes.png', fullPage: true });

  // De vuelta en el resumen, General queda como borrador con sus faltantes.
  await panel.getByRole('link', { name: 'Mi información' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
  await expect(page.getByText(/Faltan? \d+ datos?/).first()).toBeVisible();
});
