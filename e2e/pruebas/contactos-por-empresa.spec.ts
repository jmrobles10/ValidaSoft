import { expect, test, type Page } from '@playwright/test';
import { ClienteValidasoft } from '@validasoft/sdk';
import { API } from '../soporte/api';
import { capturar } from '../soporte/capturas';
import { abrirEmbebido } from '../soporte/embebido';
import { abrirEmpresa, entrarPorInvitacion } from '../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Invitacion } from '../soporte/semilla';

/**
 * Contactos por empresa (IO-3): el mismo tercero tiene contactos distintos con cada empresa, cada uno con nombre, cargo, correo,
 * teléfono y rol. El tercero los escribe desde «Mis empresas»; la empresa los ve y los edita en el vínculo (en la web embebida); cada
 * empresa ve solo los suyos y el cambio sale por el pull de eventos (el mismo sobre del webhook).
 */

async function aceptar(page: Page, empresa: string): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: `${empresa} te invita a validar tu información` })).toBeVisible();
  for (const casilla of await page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).all()) await casilla.check();
  await page.getByRole('button', { name: 'Aceptar' }).click();
  await expect(page.getByRole('heading', { level: 1, name: 'Ya aceptaste esta invitación' })).toBeVisible();
}

async function contactosDelTercero(page: Page, empresa: string, contactos: { nombre: string; cargo: string; correo: string; telefono: string; rol: string }[]): Promise<void> {
  await page.getByRole('button', { name: `Mis contactos con ${empresa}` }).click();
  const dialogo = page.getByRole('dialog', { name: `Mis contactos con ${empresa}` });
  for (const [i, c] of contactos.entries()) {
    await dialogo.getByRole('button', { name: 'Agregar contacto' }).click();
    const contacto = dialogo.getByRole('group', { name: `Contacto ${String(i + 1)}` });
    await contacto.getByRole('textbox', { name: /Nombre/ }).fill(c.nombre);
    await contacto.getByRole('textbox', { name: 'Cargo' }).fill(c.cargo);
    await contacto.getByRole('textbox', { name: /Correo/ }).fill(c.correo);
    await contacto.getByRole('textbox', { name: 'Teléfono' }).fill(c.telefono);
    await contacto.getByRole('combobox', { name: /Rol/ }).click();
    await page.getByRole('option', { name: c.rol, exact: true }).click();
  }
  await capturar(page, 'contactos-tercero');
  await dialogo.getByRole('button', { name: 'Guardar contactos' }).click();
  await expect(page.getByText(`Guardaste tus contactos con ${empresa}.`)).toBeVisible();
}

test('el tercero y cada empresa manejan contactos distintos con cada empresa, y el cambio sale por el pull de eventos', async ({ page, browser }) => {
  const e = await crearEscenario();
  const tercero = nuevoTercero('Aceros Multiempresa S.A.S.');
  const norte: Invitacion = await invitar(e, e.norte, tercero, 'CLIENTE');
  const sur: Invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');

  // El tercero entra por su invitación de Norte y acepta las dos empresas.
  await entrarPorInvitacion(page, norte);
  await aceptar(page, e.norte.nombre);
  await page.goto('/empresas');
  await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
  await aceptar(page, e.sur.nombre);
  await page.goto('/empresas');

  // Contactos con Norte (pagos y cumplimiento) y con Sur (solo comercial): listas independientes.
  await expect(page.getByRole('button', { name: `Mis contactos con ${e.norte.nombre}` })).toContainText('0 contactos');
  await contactosDelTercero(page, e.norte.nombre, [
    { nombre: 'Marta Ríos', cargo: 'Tesorera', correo: 'pagos@aceros.co', telefono: '6015551234', rol: 'Pagos' },
    { nombre: 'Olga Sánchez', cargo: 'Oficial', correo: 'cumplimiento@aceros.co', telefono: '', rol: 'Cumplimiento' },
  ]);
  await expect(page.getByRole('button', { name: `Mis contactos con ${e.norte.nombre}` })).toContainText('2 contactos');
  await expect(page.getByRole('button', { name: `Mis contactos con ${e.sur.nombre}` })).toContainText('0 contactos');
  await contactosDelTercero(page, e.sur.nombre, [{ nombre: 'Luis Pérez', cargo: 'Gerente comercial', correo: 'comercial@aceros.co', telefono: '3001234567', rol: 'Comercial' }]);
  await expect(page.getByRole('button', { name: `Mis contactos con ${e.sur.nombre}` })).toContainText('1 contacto');

  // Norte ve los suyos en el vínculo y agrega uno desde la web de la empresa.
  const web = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, `vinculos/${norte.vinculoId}`);
  const tarjeta = web.getByRole('region', { name: 'Contactos del tercero' });
  await expect(tarjeta).toContainText('Marta Ríos · Tesorera');
  await expect(tarjeta).toContainText('pagos@aceros.co · 6015551234');
  await expect(tarjeta).toContainText('Cumplimiento');
  await expect(tarjeta).not.toContainText('Luis Pérez'); // el contacto de Sur es privado de Sur

  await tarjeta.getByRole('button', { name: 'Editar' }).click();
  const dialogo = web.getByRole('dialog', { name: 'Contactos del tercero con esta empresa' });
  await dialogo.getByRole('button', { name: 'Agregar contacto' }).click();
  const nuevo = dialogo.getByRole('group', { name: 'Contacto 3' });
  await nuevo.getByRole('textbox', { name: /Nombre/ }).fill('Carla Ruiz');
  await nuevo.getByRole('textbox', { name: /Correo/ }).fill('notificaciones@aceros.co');
  await nuevo.getByRole('combobox', { name: /Rol/ }).click();
  await web.getByRole('option', { name: 'Notificaciones', exact: true }).click();
  await dialogo.getByRole('button', { name: 'Guardar contactos' }).click();
  await expect(web.getByText('Guardaste los contactos.')).toBeVisible();
  await expect(tarjeta).toContainText('Carla Ruiz');
  await capturar(web.owner().page(), 'contactos-empresa');

  // El tercero ve el contacto que agregó la empresa en su lista (es la misma lista por empresa).
  await page.reload();
  await expect(page.getByRole('button', { name: `Mis contactos con ${e.norte.nombre}` })).toContainText('3 contactos');

  // Sur sigue con su único contacto: la web de Sur no ve lo de Norte.
  const webSur = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur, `vinculos/${sur.vinculoId}`);
  const tarjetaSur = webSur.getByRole('region', { name: 'Contactos del tercero' });
  await expect(tarjetaSur).toContainText('Luis Pérez · Gerente comercial');
  await expect(tarjetaSur).not.toContainText('Marta Ríos');

  // Por la API: el vínculo y la ficha traen los contactos de esa empresa; el pull trae el evento con la lista completa.
  const srm = new ClienteValidasoft({ urlBase: API, apiKey: e.llave, empresaId: e.norte.id, reintentos: 0 });
  expect((await srm.vinculos.obtener(norte.vinculoId)).contactos?.map((c) => c.rol)).toEqual(['Pagos', 'Cumplimiento', 'Notificaciones']);
  await expect
    .poll(async () => {
      const pagina = await srm.eventos.listar({ tipos: ['vinculo.contactos_actualizados'], limite: 50 });
      return pagina.datos.filter((x) => (x.datos as { vinculoId?: string }).vinculoId === norte.vinculoId).length;
    })
    .toBe(2);
});
