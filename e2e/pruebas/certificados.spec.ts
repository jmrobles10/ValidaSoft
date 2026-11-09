import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../soporte/semilla';

/** Lista de certificados de la empresa (CE-11). */

const RUT = 'Registro Único Tributario';

/** `YYYY-MM-DD` de hoy menos `dias` en Bogotá. */
function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

test('al aprobar, el certificado aparece en la lista de Sur y se encuentra por el nombre del tercero', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Ferretería La Tuerca S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Tuerca') },
    { nombre: SST.nombre, archivo: pdf('SST Tuerca'), campos: { 'Fecha de expedición': haceDias(2) } },
  ]);

  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');

  await sofia.getByRole('link', { name: 'Certificados', exact: true }).click();
  await expect(sofia.getByRole('heading', { level: 1, name: 'Certificados' })).toBeVisible();
  const fila = sofia.getByRole('row').filter({ hasText: invitacion.nombre });
  await expect(fila).toContainText('Vigente');
  await expect(fila).toContainText(new RegExp(`${e.sur.prefijo}-C-\\d{4}-\\d{6}-1`));

  await sofia.getByRole('searchbox').or(sofia.getByPlaceholder('Buscar por tercero, identificación o folio')).first().fill('Tuerca');
  await expect(sofia.getByRole('row').filter({ hasText: invitacion.nombre })).toBeVisible();
  await paginaSofia.screenshot({ path: 'test-results/certificados-web.png', fullPage: true });

  await fila.click();
  await expect(sofia.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
});
