import { entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Invitacion } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import type { Page } from '@playwright/test';

async function aceptar(page: Page, empresa: string): Promise<void> {
  await expect(page.getByRole('heading', { level: 1, name: `${empresa} te invita a validar tu información` })).toBeVisible();
  for (const casilla of await page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).all()) await casilla.check();
  await page.getByRole('button', { name: 'Aceptar' }).click();
  await expect(
    page.getByRole('heading', { level: 1, name: `Tu revisión con ${empresa}` }).or(page.getByRole('heading', { level: 1, name: 'Ya aceptaste esta invitación' })),
  ).toBeVisible();
}

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Indica con quién hablar en cada empresa',
    resumen:
      'Puedes tener contactos distintos con cada empresa: quién atiende pagos, quién cumplimiento. Los editas desde «Mis empresas» y cada empresa solo ve los suyos. Cambiar los de una no toca los de la otra.',
    precondiciones: [
      'Dos empresas que invitan al mismo tercero como cliente: Constructora Norte y Constructora Sur.',
      'Un tercero que aceptó las dos invitaciones y no tiene contactos registrados todavía.',
    ],
    relacionados: ['tercero/cuentas-bancarias', 'tercero/campana-y-avisos'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros Multiempresa S.A.S.');
    const norte: Invitacion = await invitar(e, e.norte, tercero, 'CLIENTE');
    const sur: Invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, norte);
    await aceptar(page, e.norte.nombre);
    await entrarPorInvitacion(page, sur);
    await aceptar(page, e.sur.nombre);
    await page.goto('/empresas');

    // 1. En «Mis empresas», cada empresa tiene su botón de contactos.
    const botonNorte = page.getByRole('button', { name: `Mis contactos con ${e.norte.nombre}` });
    const botonSur = page.getByRole('button', { name: `Mis contactos con ${e.sur.nombre}` });
    await expect(botonNorte).toContainText('0 contactos');
    await expect(botonSur).toContainText('0 contactos');
    await paso(page, {
      titulo: 'Abre los contactos de una empresa',
      explicacion:
        'En la tarjeta de cada empresa hay un botón «Mis contactos con» esa empresa, que muestra cuántos tiene. ' +
        'Los contactos de Constructora Norte y los de Constructora Sur son listas separadas: lo que escribas en una no aparece en la otra.',
      resaltar: [botonNorte, botonSur],
    });

    // 2. Agrega contactos a Norte.
    await botonNorte.click();
    const dialogo = page.getByRole('dialog', { name: `Mis contactos con ${e.norte.nombre}` });
    await dialogo.getByRole('button', { name: 'Agregar contacto' }).click();
    const primero = dialogo.getByRole('group', { name: 'Contacto 1' });
    await primero.getByRole('textbox', { name: /Nombre/ }).fill('Marta Ríos');
    await primero.getByRole('textbox', { name: 'Cargo' }).fill('Tesorera');
    await primero.getByRole('textbox', { name: /Correo/ }).fill('pagos@aceros.co');
    await primero.getByRole('textbox', { name: 'Teléfono' }).fill('6015551234');
    await primero.getByRole('combobox', { name: /Rol/ }).click();
    await page.getByRole('option', { name: 'Pagos', exact: true }).click();
    await paso(page, {
      titulo: 'Escribe a quién le hablan en esa empresa',
      explicacion:
        'Cada contacto tiene nombre, cargo, correo, teléfono y un rol: comercial, pagos, cumplimiento o notificaciones. ' +
        'Cada rol puede tener una persona distinta. Aquí Marta Ríos recibe lo de pagos de Constructora Norte. Solo ve esta empresa.',
      resaltar: primero,
      variantes: [{ cuando: 'No agregas a nadie', entonces: 'Mientras no agregues a nadie aparece el aviso «Agrega al menos a la persona de notificaciones».' }],
    });

    await dialogo.getByRole('button', { name: 'Guardar contactos' }).click();
    await expect(page.getByText(`Guardaste tus contactos con ${e.norte.nombre}`)).toBeVisible();

    // 3. La otra empresa sigue igual.
    await expect(botonNorte).toContainText('1 contacto');
    await expect(botonSur).toContainText('0 contactos');
    await paso(page, {
      titulo: 'Comprueba que la otra empresa no cambió',
      explicacion:
        'Después de guardar, el botón de Constructora Norte dice «1 contacto» y el de Constructora Sur sigue en cero. ' +
        'Para editar los de Sur abres su propio botón; ninguna de las dos empresas ve los contactos que escribiste para la otra.',
      resaltar: [botonNorte, botonSur],
    });
  },
);
