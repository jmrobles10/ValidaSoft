import { expect, test } from '@playwright/test';
import { pdf } from '../soporte/archivos';
import { abrirEmbebido } from '../soporte/embebido';
import { completarInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../soporte/semilla';

/** Inicio de la empresa (IN-1, IN-2): lo que le toca a cada revisor y los indicadores. */

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

test('el revisor abre en Inicio, ve lo que espera a su área y llega a la bandeja filtrada', async ({ page, browser }) => {
  const e = await crearEscenario();
  const invitacion = await invitar(e, e.norte, nuevoTercero('Ladrillera del Valle S.A.S.'));
  await completarInvitacion(page, invitacion, [
    { nombre: RUT, archivo: pdf('RUT Ladrillera') },
    { nombre: CAMARA, archivo: pdf('Camara Ladrillera') },
  ]);

  const paginaRuben = await (await browser.newContext()).newPage();
  const ruben = await abrirEmbebido(paginaRuben, e, e.ruben, e.norte, 'inicio');
  await expect(ruben.getByRole('heading', { level: 1, name: 'Inicio' })).toBeVisible();
  const compras = ruben.getByRole('link', { name: /Compras: 1 expediente por revisar/ });
  await expect(compras).toBeVisible();
  await expect(ruben.getByText('Vínculos', { exact: true }).first()).toBeVisible();
  await paginaRuben.screenshot({ path: 'test-results/inicio-web.png', fullPage: true });

  await compras.click();
  await expect(ruben.getByRole('heading', { level: 1, name: 'Bandeja' })).toBeVisible();
  await expect(ruben.getByRole('row').filter({ hasText: invitacion.nombre })).toBeVisible();
});
