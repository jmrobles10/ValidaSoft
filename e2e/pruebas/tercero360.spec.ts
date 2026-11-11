import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../soporte/empresa';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../soporte/semilla';

/** Ficha del tercero para la empresa activa (Tercero 360, T360-1). */

const RUT = 'Registro Único Tributario';

/** `YYYY-MM-DD` de hoy menos `dias` en Bogotá. */
function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

test('desde la lista de certificados se abre la ficha del tercero con su vínculo, certificado y documentos', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Tornillería El Perno S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Perno') },
    { nombre: SST.nombre, archivo: pdf('SST Perno'), campos: { 'Fecha de expedición': haceDias(2) } },
  ]);

  const paginaSofia = await (await browser.newContext()).newPage();
  const sofia = await abrirEmbebido(paginaSofia, e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');

  await irASeccion(sofia, 'Certificados');
  await sofia.getByRole('link', { name: `Abrir la ficha de ${invitacion.nombre}` }).click();

  await expect(sofia.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  await expect(sofia.getByText('Identificación y contactos')).toBeVisible();
  await expect(sofia.getByText('Vigente').first()).toBeVisible();
  await expect(sofia.getByText(new RegExp(`${e.sur.prefijo}-C-\\d{4}-\\d{6}-1`))).toBeVisible();
  await expect(sofia.getByText(RUT).first()).toBeVisible();
  await expect(sofia.getByText('El tercero no tiene una cuenta designada.')).toBeVisible();
  await paginaSofia.screenshot({ path: 'test-results/tercero360-web.png', fullPage: true });

  await sofia.getByRole('link', { name: /Ver el vínculo/ }).first().click();
  await expect(sofia.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
});
