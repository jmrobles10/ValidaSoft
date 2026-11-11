import { expect, test, type Browser } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario, type Persona } from '../soporte/semilla';

/** Cuenta bancaria designada (CB-1, CB-2): la registra y la elige el tercero; la aprueban dos personas de la empresa. */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

async function vinculoEnLaEmpresa(browser: Browser, e: Escenario, persona: Persona, vinculoId: string) {
  return abrirEmbebido(await (await browser.newContext()).newPage(), e, persona, e.norte, `vinculos/${vinculoId}`);
}

test('el proveedor designa su cuenta para Norte y queda designada cuando la aprueban dos personas', async ({ page, browser }) => {
  const e = await crearEscenario();
  const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
  const enNorte = await invitar(e, e.norte, tercero);
  await completarInvitacion(page, enNorte, [
    { nombre: RUT, archivo: pdf('RUT Tornillo') },
    { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
  ]);

  // Registra su cuenta: solo ve los últimos cuatro dígitos.
  await page.goto('/cuentas');
  await expect(page.getByRole('heading', { level: 1, name: 'Mis cuentas bancarias' })).toBeVisible();
  await page.getByRole('button', { name: 'Agregar cuenta' }).first().click();
  const nueva = page.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
  // El banco sale del catálogo (con su código de compensación y SWIFT, que viajan al ERP): se busca y se elige de la lista.
  await nueva.getByRole('combobox', { name: 'Banco' }).fill('Bancolombia');
  await page.getByRole('option', { name: 'Bancolombia' }).click();
  await nueva.getByRole('radio', { name: 'Ahorros' }).check();
  await nueva.getByLabel('Número de cuenta').fill('031-123456-78');
  await nueva.getByLabel('Titular de la cuenta').fill('Ferretería El Tornillo S.A.S.');
  await nueva.getByLabel('Identificación del titular').fill('NIT 900123456');
  await nueva.getByRole('button', { name: 'Guardar cuenta' }).click();
  await expect(page.getByText(/· \*\*\*\*5678 ·/)).toBeVisible();

  // Sin certificación bancaria no puede designarla: la pantalla lo lleva a subirla.
  const porEmpresa = page.getByRole('region', { name: 'En qué cuenta te paga cada empresa' }).or(page.locator('section', { hasText: 'En qué cuenta te paga cada empresa' }));
  await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
  await page.getByRole('link', { name: 'Subir certificación bancaria' }).click();
  const selector = page.waitForEvent('filechooser');
  await page.getByRole('button', { name: 'Elegir archivo' }).click();
  await (await selector).setFiles(pdf('Certificacion Bancolombia Tornillo'));
  await expect(page.getByText(/Es tu versión 1 de Certificación bancaria/)).toBeVisible({ timeout: 30_000 });

  await page.goto('/cuentas');
  await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
  await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
  await expect(page.getByText(`Pendiente de aprobación de ${e.norte.nombre}`)).toBeVisible();

  // Olga (oficial de cumplimiento) la coteja con el número completo y la aprueba: falta otra persona.
  const olga = await vinculoEnLaEmpresa(browser, e, e.olga, enNorte.vinculoId);
  const tarjetaOlga = olga.getByRole('region', { name: 'Cuenta bancaria designada' }).or(olga.locator('section', { hasText: 'Cuenta bancaria designada' }));
  await expect(tarjetaOlga).toContainText('Por aprobar');
  await expect(tarjetaOlga).toContainText('03112345678');
  await expect(tarjetaOlga).toContainText('0 de 2 aprobaciones');
  await tarjetaOlga.getByRole('button', { name: 'Aprobar' }).click();
  await olga.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaOlga).toContainText('1 de 2 aprobaciones: Olga Oficial');
  await expect(tarjetaOlga).toContainText('Ya la aprobaste; falta que la apruebe otra persona.');

  // Ana (administradora) completa las dos: queda designada.
  const ana = await vinculoEnLaEmpresa(browser, e, e.ana, enNorte.vinculoId);
  const tarjetaAna = ana.getByRole('region', { name: 'Cuenta bancaria designada' }).or(ana.locator('section', { hasText: 'Cuenta bancaria designada' }));
  await tarjetaAna.getByRole('button', { name: 'Aprobar' }).click();
  await ana.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaAna).toContainText('Designada');
  await expect(tarjetaAna).toContainText('Olga Oficial');
  await expect(tarjetaAna).toContainText('Ana Administradora');
  await tarjetaAna.page().screenshot({ path: 'test-results/cuenta-designada-web.png', fullPage: true });

  // El proveedor la ve designada.
  await page.goto('/cuentas');
  await expect(page.getByText(`Designada para ${e.norte.nombre}`)).toBeVisible();
  await page.screenshot({ path: 'test-results/cuentas-portal.png', fullPage: true });
});
