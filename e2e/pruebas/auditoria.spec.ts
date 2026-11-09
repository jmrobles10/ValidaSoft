import { expect, test } from '@playwright/test';
import { abrirEmbebido } from '../soporte/embebido';
import { crearEscenario } from '../soporte/semilla';

/** Auditoría de la empresa (AU-1): cambios de configuración con su diff y accesos. */

test('la administradora ve en Auditoría quién creó y cambió las áreas de Norte, campo por campo', async ({ page }) => {
  const e = await crearEscenario();
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'auditoria');

  await expect(ana.getByRole('heading', { level: 1, name: 'Auditoría' })).toBeVisible();
  await expect(ana.getByRole('tab', { name: 'Configuración' })).toHaveAttribute('aria-selected', 'true');

  // El cambio de miembros del área de Compras, con el campo que cambió.
  const fila = ana.getByRole('row').filter({ hasText: 'Compras' }).filter({ hasText: 'Modificado' }).first();
  await expect(fila).toContainText('Área');
  await expect(fila).toContainText('Esta empresa');
  await page.screenshot({ path: 'test-results/auditoria-web.png', fullPage: true });
  await fila.click();

  const detalle = ana.getByRole('dialog');
  await expect(detalle.getByText('Cambios campo por campo')).toBeVisible();
  await expect(detalle.getByText('miembros')).toBeVisible();
  await expect(detalle.getByText('Modificado').first()).toBeVisible();
  await page.screenshot({ path: 'test-results/auditoria-detalle.png', fullPage: true });
  await detalle.getByRole('button', { name: 'Cerrar el detalle' }).click();

  // En Accesos todavía no hay descargas.
  await ana.getByRole('tab', { name: 'Accesos' }).click();
  await expect(ana.getByText('No hay registros de auditoría.')).toBeVisible();
});
