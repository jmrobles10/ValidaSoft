import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario } from '../../soporte/semilla';
import type { Browser } from '@playwright/test';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Designa la cuenta en la que te paga una empresa',
    resumen:
      'Registras tu cuenta bancaria en «Mis cuentas» y eliges en cuál te paga cada empresa. Para designarla necesitas subir tu certificación bancaria, y la empresa la aprueba entre dos personas antes de quedar designada.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte» (RUT y Cámara de comercio), con Compras y Cumplimiento, y un flujo que exige cuenta designada para el tipo de vínculo.',
      'Un tercero que completó su invitación de Norte y no tiene ninguna cuenta registrada.',
    ],
    relacionados: ['tercero/contactos-por-empresa', 'tercero/campana-y-avisos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, enNorte, [
      { nombre: RUT, archivo: pdf('RUT Tornillo') },
      { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
    ]);

    // 1. Registrar la cuenta.
    await page.goto('/cuentas');
    await expect(page.getByRole('heading', { level: 1, name: 'Mis cuentas bancarias' })).toBeVisible();
    await page.getByRole('button', { name: 'Agregar cuenta' }).first().click();
    const nueva = page.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
    await nueva.getByRole('combobox', { name: 'Banco' }).fill('Bancolombia');
    await page.getByRole('option', { name: 'Bancolombia' }).click();
    await nueva.getByRole('radio', { name: 'Ahorros' }).check();
    await nueva.getByLabel('Número de cuenta').fill('031-123456-78');
    await nueva.getByLabel('Titular de la cuenta').fill('Ferretería El Tornillo S.A.S.');
    await nueva.getByLabel('Identificación del titular').fill('NIT 900123456');
    await paso(page, {
      titulo: 'Registra tu cuenta bancaria',
      explicacion:
        'En «Agregar cuenta bancaria» eliges el banco de la lista, el tipo de cuenta y escribes el número, el titular y su identificación. ' +
        'Al guardar, la cuenta queda registrada y solo ves sus últimos cuatro dígitos. Puedes tener varias cuentas y usar una u otra con cada empresa.',
      resaltar: nueva,
    });
    await nueva.getByRole('button', { name: 'Guardar cuenta' }).click();
    await expect(page.getByText(/· \*\*\*\*5678 ·/)).toBeVisible();

    // 2. Designarla exige certificación bancaria.
    const porEmpresa = page.getByRole('region', { name: 'En qué cuenta te paga cada empresa' }).or(page.locator('section', { hasText: 'En qué cuenta te paga cada empresa' }));
    await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
    await expect(page.getByRole('link', { name: 'Subir certificación bancaria' })).toBeVisible();
    await paso(page, {
      titulo: 'Sube la certificación bancaria para designarla',
      explicacion:
        'Para designar una cuenta la empresa necesita la certificación que expide tu banco, a tu nombre. Si no la tienes en tu bóveda, la pantalla te lleva a subirla desde aquí. ' +
        'Con el documento aprobado, la cuenta puede designarse para cada empresa que la pida.',
      resaltar: page.getByRole('link', { name: 'Subir certificación bancaria' }),
    });
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('link', { name: 'Subir certificación bancaria' }).click();
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf('Certificacion Bancolombia Tornillo'));
    await expect(page.getByText(/Es tu versión 1 de Certificación bancaria/)).toBeVisible({ timeout: 30_000 });

    // 3. Designar: queda pendiente de la aprobación de la empresa.
    await page.goto('/cuentas');
    await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
    await expect(page.getByText(`Pendiente de aprobación de ${e.norte.nombre}`)).toBeVisible();
    await paso(page, {
      titulo: 'Designa la cuenta y espera la aprobación',
      explicacion:
        'Al designar la cuenta para una empresa, queda «Pendiente de aprobación» hasta que la empresa la apruebe. ' +
        'La empresa revisa el número completo contra la certificación y la aprueban dos personas distintas antes de que la designación quede en firme.',
      resaltar: page.getByText(`Pendiente de aprobación de ${e.norte.nombre}`),
    });

    // 4. Aprobada por dos personas: la ves designada.
    await aprobarCuenta(e, browser, enNorte.vinculoId);
    await page.goto('/cuentas');
    await expect(page.getByText(`Designada para ${e.norte.nombre}`)).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que la cuenta quedó designada',
      explicacion:
        'Cuando las dos personas de la empresa aprueban, la cuenta aparece como «Designada para» esa empresa, y ahí te pagarán. ' +
        'Si cambias de cuenta más adelante, la nueva pasa también por aprobación.',
      resaltar: page.getByText(`Designada para ${e.norte.nombre}`),
    });
  },
);

/** La empresa aprueba la cuenta en dos pasos (oficial de cumplimiento y administradora). Se siembra por la interfaz, sin documentarlo. */
async function aprobarCuenta(e: Escenario, browser: Browser, vinculoId: string): Promise<void> {
  const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, `vinculos/${vinculoId}`);
  const tarjetaOlga = olga.getByRole('region', { name: 'Cuenta bancaria designada' }).or(olga.locator('section', { hasText: 'Cuenta bancaria designada' }));
  await tarjetaOlga.getByRole('button', { name: 'Aprobar' }).click();
  await olga.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaOlga).toContainText('1 de 2 aprobaciones: Olga Oficial');
  const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, `vinculos/${vinculoId}`);
  const tarjetaAna = ana.getByRole('region', { name: 'Cuenta bancaria designada' }).or(ana.locator('section', { hasText: 'Cuenta bancaria designada' }));
  await tarjetaAna.getByRole('button', { name: 'Aprobar' }).click();
  await ana.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
  await expect(tarjetaAna).toContainText('Designada');
}
