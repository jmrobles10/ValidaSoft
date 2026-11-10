import { readFile } from 'node:fs/promises';
import { expect, test, type Download } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../soporte/empresa';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../soporte/semilla';

/** Informes y exportaciones (IF-1…IF-6): informe de estado en la web, su Excel y el ZIP del expediente. */

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

/** Contenido del archivo descargado (el ZIP y el .xlsx guardan los nombres de sus entradas sin comprimir). */
async function contenido(descarga: Download): Promise<Buffer> {
  return readFile(await descarga.path());
}

test('la administradora ve el informe de estado con los tiempos por área, lo baja en Excel y exporta el expediente en ZIP', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.sur, nuevoTercero('Ferretería El Remache S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Remache') },
    { nombre: SST.nombre, archivo: pdf('SST Remache'), campos: { 'Fecha de expedición': haceDias(2) } },
  ]);
  const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
  await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
  await aprobarArea(sofia, 'Revisión');

  // Ana abre Informes: el expediente aprobado, con su folio, y el área con sus tiempos.
  const paginaAna = await (await browser.newContext({ acceptDownloads: true })).newPage();
  const ana = await abrirEmbebido(paginaAna, e, e.ana, e.sur, 'informes/estado-expedientes');
  await expect(ana.getByRole('heading', { level: 1, name: 'Informes' })).toBeVisible();
  const fila = ana.getByRole('row').filter({ hasText: invitacion.nombre });
  await expect(fila).toContainText('Aprobado');
  await expect(fila).toContainText('2 de 2');
  const folio = (await fila.getByText(new RegExp(`^${e.sur.prefijo}-E-\\d{4}-\\d{6}$`)).textContent()) ?? '';
  expect(folio).toMatch(new RegExp(`^${e.sur.prefijo}-E-`));
  await expect(ana.getByRole('table', { name: 'Tiempos por área' }).getByRole('row', { name: /Revisión/ })).toBeVisible();
  await paginaAna.screenshot({ path: 'test-results/informe-estado-web.png', fullPage: true });

  // Filtrado por estado, lo baja en Excel con los mismos filtros.
  await ana.getByRole('button', { name: /^Estado/ }).click();
  await ana.getByRole('menuitem', { name: 'Aprobado' }).click();
  await expect(fila).toBeVisible();
  const excel = paginaAna.waitForEvent('download');
  await ana.getByRole('button', { name: 'Descargar Excel' }).click();
  const libro = await excel;
  expect(libro.suggestedFilename()).toMatch(/^estado-expedientes-constructora-sur-sas-\d{4}-\d{2}-\d{2}\.xlsx$/);
  const bytesLibro = await contenido(libro);
  expect(bytesLibro.subarray(0, 2).toString('latin1')).toBe('PK');
  expect(bytesLibro.includes('xl/worksheets/sheet3.xml')).toBe(true);

  // Desde la fila llega al expediente y lo exporta completo.
  await fila.click();
  await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
  const zip = paginaAna.waitForEvent('download');
  await ana.getByRole('button', { name: 'Exportar expediente' }).click();
  const archivo = await zip;
  expect(archivo.suggestedFilename()).toBe(`expediente-${folio}.zip`);
  const bytesZip = await contenido(archivo);
  expect(bytesZip.subarray(0, 2).toString('latin1')).toBe('PK');
  for (const entrada of ['resumen.pdf', 'indice.json', 'linea-de-tiempo.json', 'documentos/RUT-v1-', `documentos/${SST.codigo}-v1-`]) {
    expect(bytesZip.includes(entrada), entrada).toBe(true);
  }

  // La exportación y cada documento incluido quedan en la auditoría de accesos.
  await ana.getByRole('link', { name: 'Auditoría', exact: true }).click();
  await ana.getByRole('tab', { name: /Accesos/ }).click();
  await expect(ana.getByRole('row').filter({ hasText: 'Exportado' }).filter({ hasText: folio })).toBeVisible();
  await expect(ana.getByRole('row').filter({ hasText: 'Informe de estado de expedientes' }).first()).toBeVisible();
  await paginaAna.screenshot({ path: 'test-results/auditoria-exportaciones-web.png', fullPage: true });
});
