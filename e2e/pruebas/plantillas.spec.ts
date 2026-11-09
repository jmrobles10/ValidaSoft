import { expect, test } from '@playwright/test';
import { esperarCorreo } from '../soporte/correo';
import { abrirEmbebido } from '../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../soporte/semilla';

/** Plantillas de correo de la empresa (PL-1): asunto, cabecera y pie de la invitación. */

test('Norte personaliza la invitación: la vista previa la muestra y la próxima invitación llega con su asunto, cabecera y pie', async ({ page }) => {
  const e = await crearEscenario();
  const ana = await abrirEmbebido(page, e, e.ana, e.norte, 'plantillas');

  await expect(ana.getByRole('heading', { level: 1, name: 'Plantillas de correo' })).toBeVisible();
  await ana.getByRole('row').filter({ hasText: 'Invitación' }).getByRole('button').click();
  const drawer = ana.getByRole('dialog', { name: /Invitación/ });

  await drawer.getByRole('textbox', { name: 'Asunto' }).fill('Bienvenido a la red de proveedores de ');
  await drawer.getByRole('button', { name: '{{empresa}}' }).first().click();
  await drawer.getByRole('textbox', { name: 'Cabecera' }).fill('Gracias por trabajar con nosotros.');
  await drawer.getByRole('textbox', { name: 'Pie' }).fill('Mesa de ayuda: 601 555 0100');

  // La vista previa (en un iframe sin scripts) ya trae la cabecera antes de guardar.
  await expect(drawer.getByText(`Asunto: Bienvenido a la red de proveedores de ${e.norte.nombre}`)).toBeVisible();
  await expect(drawer.frameLocator('iframe').getByText('Gracias por trabajar con nosotros.')).toBeVisible();
  await page.screenshot({ path: 'test-results/plantillas-web.png', fullPage: true });

  await drawer.getByRole('button', { name: 'Guardar' }).click();
  await expect(ana.getByText('Plantilla guardada. Los próximos correos saldrán así.')).toBeVisible();
  await expect(ana.getByRole('row').filter({ hasText: 'Invitación' })).toContainText('Personalizada');

  const invitacion = await invitar(e, e.norte, nuevoTercero('Ferretería La Plantilla S.A.S.'));
  const correo = await esperarCorreo(invitacion.correo, /Bienvenido a la red de proveedores/);
  expect(correo.asunto).toBe(`Bienvenido a la red de proveedores de ${e.norte.nombre}`);
  expect(correo.texto).toContain('Gracias por trabajar con nosotros.');
  expect(correo.texto).toContain('Mesa de ayuda: 601 555 0100');
  expect(correo.texto).toContain('/e/'); // el cuerpo con el enlace de acceso sigue intacto
});
