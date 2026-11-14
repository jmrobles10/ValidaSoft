import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, aprobarUltimaAreaYEsperarCuenta } from '../../soporte/empresa';
import { abrirEmpresa, completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, type Escenario } from '../../soporte/semilla';
import type { Browser } from '@playwright/test';
import { expect, paso, recorrido } from '../nucleo';
import { exigirCuentaDesignada } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Designa la cuenta en la que te paga una empresa',
    resumen:
      'Tu validación con una empresa que exige cuenta bancaria espera hasta que designes una. Registras tu cuenta en «Mis cuentas» y eliges en cuál te paga cada empresa. Para designarla necesitas subir tu certificación bancaria, y la empresa la aprueba entre dos personas: en ese momento tu validación se cierra sola.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte» (RUT y Cámara de comercio), con Compras y Cumplimiento, y el tipo de vínculo Proveedor con «Exige cuenta bancaria designada» activado.',
      'Un tercero que completó su invitación de Norte, no tiene ninguna cuenta registrada y cuyo expediente ya aprobaron las dos áreas.',
    ],
    relacionados: ['tercero/contactos-por-empresa', 'tercero/campana-y-avisos', 'vinculos/cuenta-designada-aprobar', 'configuracion/tipos-de-vinculo'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    // La semilla apaga la exigencia en el tipo Proveedor; aquí se enciende (CB-4) antes de invitar: la exigencia se copia al abrir el expediente.
    await exigirCuentaDesignada(e);
    const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, enNorte, [
      { nombre: RUT, archivo: pdf('RUT Tornillo') },
      { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
    ]);
    await aprobarAreasDeNorte(e, browser, tercero.nombre);

    // 0. Las áreas aprobaron, pero la validación espera la cuenta bancaria.
    await esperarEstadoDelTercero(page, 'EnRevision');
    const aviso = page.getByRole('alert').filter({ hasText: 'Falta tu cuenta bancaria designada' });
    await expect(aviso).toBeVisible();
    await expect(aviso).toContainText(/aprueba tu validación cuando tu cuenta bancaria para esta empresa esté designada y aprobada/);
    await paso(page, {
      titulo: 'Mira por qué tu validación espera',
      explicacion:
        'Las áreas de la empresa ya aprobaron tu información, pero tu validación sigue «En revisión»: la empresa exige que tengas una cuenta bancaria designada y aprobada, y todavía no la hay. ' +
        'El aviso «Falta tu cuenta bancaria designada» lo explica y «Ver mis cuentas» te lleva a registrarla. Cuando la empresa aprueba la cuenta, tu validación se cierra sola, sin que tengas que hacer nada más.',
      resaltar: aviso,
      variantes: [
        { cuando: 'La empresa no exige cuenta bancaria designada', entonces: 'No ves este aviso: tu validación se cierra apenas las áreas aprueban.' },
        { cuando: 'Ya designaste una cuenta y la empresa no la ha aprobado', entonces: 'El aviso sigue ahí, y te dice que esperes a que la empresa apruebe la que ya designaste.' },
      ],
      configuracion: {
        ajuste: 'Exige cuenta bancaria designada (tipo de vínculo)',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Cada empresa decide si su tipo de vínculo exige que el tercero tenga una cuenta designada aprobada antes de cerrar la validación.',
      },
    });
    await aviso.getByRole('link', { name: 'Ver mis cuentas' }).click();

    // 1. Registrar la cuenta.
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

    // 5. Con la cuenta aprobada, la validación se cierra sola.
    await page.goto('/empresas');
    await abrirEmpresa(page, e.norte.nombre);
    await esperarEstadoDelTercero(page, 'Aprobado');
    await expect(page.getByRole('alert').filter({ hasText: 'Falta tu cuenta bancaria designada' })).toHaveCount(0);
    await paso(page, {
      titulo: 'Tu validación se cierra con la cuenta aprobada',
      explicacion:
        'En cuanto la segunda persona de la empresa aprobó tu cuenta, tu validación se aprobó sola: el aviso de la cuenta desapareció y tu vínculo con la empresa está activo. ' +
        'No tuviste que avisar ni reenviar nada. Si la empresa hubiera rechazado la cuenta, tu validación habría seguido esperando y verías el motivo para designar otra.',
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

/** Compras (Rubén) y Cumplimiento (Olga) aprueban el expediente: sin la cuenta aprobada, la validación sigue en revisión. Se siembra por la interfaz, sin documentarlo. */
async function aprobarAreasDeNorte(e: Escenario, browser: Browser, nombreDelTercero: string): Promise<void> {
  const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
  await abrirDesdeBandeja(ruben, nombreDelTercero, 'Compras');
  await aprobarArea(ruben, 'Compras');
  const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, 'bandeja');
  await abrirDesdeBandeja(olga, nombreDelTercero, 'Cumplimiento');
  await aprobarUltimaAreaYEsperarCuenta(olga, 'Cumplimiento');
}
