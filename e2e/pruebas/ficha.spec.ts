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

  // Guardar sin llenar: el servidor responde los faltantes exactos y el wizard no avanza.
  await page.getByRole('button', { name: 'Guardar y continuar' }).click();
  await expect(page.getByText(/Para completar esta sección faltan? \d+ datos?/)).toBeVisible();
  await expect(page).toHaveURL(/\/ficha\/general/);
  await page.screenshot({ path: 'test-results/ficha-general-faltantes.png', fullPage: true });

  // De vuelta en el resumen, General queda como borrador con sus faltantes.
  await page.getByRole('link', { name: 'Mi información' }).first().click();
  await expect(page.getByText(/Faltan? \d+ datos?/).first()).toBeVisible();
});
