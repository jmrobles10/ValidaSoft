import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { CAMARA, RUT } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Registra tu cuenta bancaria desde el celular',
    resumen:
      'En el celular, el tercero registra su cuenta bancaria en Mis cuentas, con el diálogo a pantalla completa. Solo ve los últimos cuatro dígitos del número, y la cuenta queda por designar hasta que la empresa la apruebe.',
    precondiciones: [
      'Un tercero invitado por Constructora Norte que entregó sus documentos y quedó «En revisión».',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
    const enNorte = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, enNorte, [
      { nombre: RUT, archivo: pdf('RUT Tornillo') },
      { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
    ]);

    // 1. Mis cuentas.
    await page.goto('/cuentas');
    await expect(page.getByRole('heading', { level: 1, name: 'Mis cuentas bancarias' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre Mis cuentas',
      explicacion:
        'Mis cuentas guarda las cuentas bancarias a las que las empresas te pagan. Cada cuenta se muestra con su banco y los últimos cuatro dígitos del número; el número completo no vuelve a aparecer en la pantalla. ' +
        'Con «Agregar cuenta» registras una nueva.',
      resaltar: page.getByRole('button', { name: 'Agregar cuenta' }).first(),
      variantes: [
        { cuando: 'No tienes cuentas', entonces: 'La pantalla dice que aún no registras una cuenta y el botón para agregarla queda visible.' },
      ],
    });

    // 2. El diálogo de la cuenta.
    await page.getByRole('button', { name: 'Agregar cuenta' }).first().click();
    const nueva = page.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
    await expect(nueva).toBeVisible();
    await nueva.getByRole('combobox', { name: 'Banco' }).fill('Bancolombia');
    await page.getByRole('option', { name: 'Bancolombia' }).click();
    await nueva.getByRole('radio', { name: 'Ahorros' }).check();
    await nueva.getByLabel('Número de cuenta').fill('031-123456-78');
    await nueva.getByLabel('Titular de la cuenta').fill('Ferretería El Tornillo S.A.S.');
    await nueva.getByLabel('Identificación del titular').fill('NIT 900123456');
    await paso(page, {
      titulo: 'Escribe los datos de la cuenta',
      explicacion:
        'Elige el banco de la lista, el tipo de cuenta (ahorros o corriente), el número, el titular y su identificación. El banco y su código viajan a la empresa, por eso se toman de la lista y no se escriben. ' +
        '«Guardar cuenta» la registra.',
      resaltar: nueva,
      variantes: [
        { cuando: 'Designas la cuenta para una empresa', entonces: 'Hace falta certificación bancaria: la pantalla te lleva a subirla antes de designarla.' },
      ],
    });

    // 3. Guardar.
    await nueva.getByRole('button', { name: 'Guardar cuenta' }).click();
    await expect(page.getByText(/· \*\*\*\*5678 ·/)).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba la cuenta guardada',
      explicacion:
        'La cuenta aparece en la lista con su banco y los últimos cuatro dígitos (****5678). Para que una empresa te pague en ella, debes designarla en «En qué cuenta te paga cada empresa»; la empresa la aprueba antes de quedar designada.',
      resaltar: page.getByText(/· \*\*\*\*5678 ·/),
      avisos: true,
    });
  },
);
