import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Oficial de cumplimiento',
    titulo: 'Aprueba la cuenta bancaria designada de un proveedor',
    resumen:
      'La oficial de cumplimiento revisa la cuenta bancaria que un proveedor designó para pagos de Constructora Norte, la coteja con el número completo y la aprueba. La cuenta solo queda designada cuando la aprueban dos personas distintas.',
    precondiciones: [
      'Un grupo con Constructora Norte y una oficial de cumplimiento, y un tipo de proveedor que exige cuenta designada.',
      'Un proveedor que completó su invitación, registró una cuenta bancaria con certificación y la designó para Norte.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Ferretería El Tornillo S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);

    // Siembra: el proveedor registra y designa su cuenta en su propia ventana (no se documenta aquí).
    const paginaTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(paginaTercero, invitacion, [
      { nombre: RUT, archivo: pdf('RUT Tornillo') },
      { nombre: CAMARA, archivo: pdf('Camara Tornillo') },
    ]);
    await paginaTercero.goto('/cuentas');
    await paginaTercero.getByRole('button', { name: 'Agregar cuenta' }).first().click();
    const nueva = paginaTercero.getByRole('dialog', { name: 'Agregar cuenta bancaria' });
    await nueva.getByRole('combobox', { name: 'Banco' }).fill('Bancolombia');
    await paginaTercero.getByRole('option', { name: 'Bancolombia' }).click();
    await nueva.getByRole('radio', { name: 'Ahorros' }).check();
    await nueva.getByLabel('Número de cuenta').fill('031-123456-78');
    await nueva.getByLabel('Titular de la cuenta').fill('Ferretería El Tornillo S.A.S.');
    await nueva.getByLabel('Identificación del titular').fill('NIT 900123456');
    await nueva.getByRole('button', { name: 'Guardar cuenta' }).click();
    await expect(paginaTercero.getByText(/· \*\*\*\*5678 ·/)).toBeVisible();
    const porEmpresa = paginaTercero.locator('section', { hasText: 'En qué cuenta te paga cada empresa' });
    await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
    await paginaTercero.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
    await paginaTercero.getByRole('link', { name: 'Subir certificación bancaria' }).click();
    const selector = paginaTercero.waitForEvent('filechooser');
    await paginaTercero.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf('Certificacion Bancolombia Tornillo'));
    await expect(paginaTercero.getByText(/Es tu versión 1 de Certificación bancaria/)).toBeVisible({ timeout: 30_000 });
    await paginaTercero.goto('/cuentas');
    await porEmpresa.getByRole('button', { name: 'Elegir cuenta' }).click();
    await paginaTercero.getByRole('dialog').getByRole('button', { name: 'Designar cuenta' }).click();
    await expect(paginaTercero.getByText(`Pendiente de aprobación de ${e.norte.nombre}`)).toBeVisible();

    const app = await abrirEmbebido(page, e, e.olga, e.norte, `vinculos/${invitacion.vinculoId}`);
    const tarjeta = app.locator('section', { hasText: 'Cuenta bancaria designada' });
    await expect(tarjeta).toContainText('Por aprobar');
    await expect(tarjeta).toContainText('0 de 2 aprobaciones');
    await paso(page, {
      titulo: 'Revisa la cuenta que el proveedor quiere designar',
      explicacion:
        'En el detalle del vínculo, la tarjeta «Cuenta bancaria designada» muestra la cuenta por aprobar con su banco, tipo y número enmascarado, y cuántas aprobaciones lleva. ' +
        'Antes de aprobar, coteja el número completo con la certificación bancaria del proveedor.',
      resaltar: tarjeta,
      variantes: [
        {
          cuando: 'El tipo de vínculo no exige cuenta designada',
          entonces: 'La tarjeta no aparece en el detalle y el vínculo no necesita cuenta para avanzar.',
        },
      ],
      configuracion: {
        ajuste: 'Exige cuenta bancaria designada (tipo de vínculo)',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Si el tipo la exige, el cierre del vínculo necesita una cuenta designada aprobada.',
      },
    });

    await tarjeta.getByRole('button', { name: 'Aprobar' }).click();
    await app.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
    await expect(tarjeta).toContainText('1 de 2 aprobaciones: Olga Oficial');
    await paso(page, {
      titulo: 'Aprueba la cuenta',
      explicacion:
        'Tu aprobación cuenta como la primera de dos. La misma persona no cuenta dos veces: la tarjeta dice que falta la aprobación de otra persona. Si la cuenta no es correcta, puedes rechazarla con un motivo: el proveedor lo ve y la designada anterior no cambia.',
      resaltar: tarjeta,
    });

    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
    const tarjetaAna = ana.locator('section', { hasText: 'Cuenta bancaria designada' });
    await tarjetaAna.getByRole('button', { name: 'Aprobar' }).click();
    await ana.getByRole('dialog').getByRole('button', { name: 'Aprobar' }).click();
    await expect(tarjetaAna).toContainText('Designada');
    await paso(paginaAna, {
      titulo: 'La segunda aprobación designa la cuenta',
      explicacion:
        'Con la segunda aprobación de otra persona de la empresa, la cuenta queda designada y la tarjeta muestra quién aprobó. La cuenta nueva reemplaza a la anterior.',
      resaltar: tarjetaAna,
      avisos: true,
    });
  },
);
