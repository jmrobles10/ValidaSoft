import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, aprobarUltimaAreaYEsperarCuenta, regionDeArea } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { exigirCuentaDesignada } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Oficial de cumplimiento',
    titulo: 'Aprueba la cuenta bancaria designada de un proveedor',
    resumen:
      'Las dos áreas aprueban el expediente de un proveedor, pero no se cierra: el tipo de vínculo exige una cuenta bancaria designada y aprobada. La oficial de cumplimiento coteja la cuenta con el número completo y la aprueba; con la segunda aprobación de otra persona, la cuenta queda designada y el expediente se aprueba solo.',
    precondiciones: [
      'Un grupo con Constructora Norte, una oficial de cumplimiento, un revisor de Compras y una administradora, y el tipo de vínculo Proveedor con «Exige cuenta bancaria designada» activado.',
      'Un proveedor que completó su invitación, registró una cuenta bancaria con certificación y la designó para Norte, y el área Compras ya aprobó su parte.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'configuracion/tipos-de-vinculo', 'tercero/cuentas-bancarias'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    // La semilla apaga la exigencia en el tipo Proveedor; este recorrido la necesita encendida (CB-4) antes de abrir el expediente.
    await exigirCuentaDesignada(e);
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

    // Siembra: Compras aprueba su parte; la última área en decidir es Cumplimiento, la de Olga.
    const expediente = await expedienteAbierto(e, invitacion);
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(ruben, tercero.nombre, 'Compras');
    await aprobarArea(ruben, 'Compras');

    // 1. Olga aprueba la última área: el expediente no se cierra, espera la cuenta.
    const app = await abrirEmbebido(page, e, e.olga, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, tercero.nombre, 'Cumplimiento');
    await aprobarUltimaAreaYEsperarCuenta(app, 'Cumplimiento');
    const aviso = app.getByRole('alert').filter({ hasText: 'Falta la cuenta bancaria designada' });
    await expect(aviso).toBeVisible();
    await expect(aviso).toContainText('Este tipo de vínculo exige una cuenta bancaria designada y aprobada.');
    await expect(app.getByText('Expediente aprobado', { exact: true })).toHaveCount(0);
    await paso(page, {
      titulo: 'Aprueba tu área: el expediente espera la cuenta',
      explicacion:
        'Compras ya había aprobado y ahora aprobaste Cumplimiento, la última área. Aun así el expediente no se cierra: sigue «En revisión» porque el tipo de vínculo Proveedor exige una cuenta bancaria designada y aprobada, y todavía no lo está. ' +
        'El aviso «Falta la cuenta bancaria designada» lo dice, y «Ver la cuenta» te lleva al vínculo, donde dos personas de la empresa aprueban la cuenta que designó el proveedor. ' +
        'Rechazar esa cuenta no rechaza el expediente: sigue esperando a que el proveedor designe otra.',
      resaltar: [aviso, regionDeArea(app, 'Cumplimiento')],
      variantes: [
        {
          cuando: 'El tipo de vínculo no exige cuenta designada',
          entonces: 'Al aprobar la última área el expediente se aprueba al instante: no aparece este aviso y la cuenta no frena el cierre.',
        },
        {
          cuando: 'El vínculo ya tenía una cuenta designada aprobada cuando se abrió el expediente',
          entonces: 'La cuenta ya cuenta como cumplida: el expediente se aprueba apenas decide la última área.',
        },
      ],
      configuracion: {
        ajuste: 'Exige cuenta bancaria designada (tipo de vínculo)',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Con la exigencia activa, el expediente no se aprueba hasta que la cuenta designada del vínculo esté aprobada, aunque todas las áreas hayan aprobado.',
      },
    });

    // 2. «Ver la cuenta» lleva al vínculo, donde está la tarjeta para aprobarla.
    await aviso.getByRole('link', { name: 'Ver la cuenta' }).click();
    await expect(aviso).toBeHidden();
    const tarjeta = app.locator('section', { hasText: 'Cuenta bancaria designada' });
    await expect(tarjeta).toContainText('Por aprobar');
    await expect(tarjeta).toContainText('0 de 2 aprobaciones');
    await paso(page, {
      titulo: 'Revisa la cuenta que el proveedor quiere designar',
      explicacion:
        'En el detalle del vínculo, la tarjeta «Cuenta bancaria designada» muestra la cuenta por aprobar con su banco, tipo y número enmascarado, y cuántas aprobaciones lleva. ' +
        'Antes de aprobar, coteja el número completo con la certificación bancaria del proveedor. Aquí llegas desde el aviso del expediente, pero también puedes abrir el vínculo desde la lista de vínculos.',
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
        efecto: 'Si el tipo la exige, el expediente del vínculo necesita una cuenta designada aprobada para cerrarse.',
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

    // 4. El expediente que esperaba la cuenta se aprueba solo.
    const paginaFinal = await (await browser.newContext()).newPage();
    const cierre = await abrirEmbebido(paginaFinal, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(cierre.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(cierre.getByRole('alert').getByText('Expediente aprobado')).toBeVisible();
    await expect(cierre.getByRole('alert').filter({ hasText: 'Falta la cuenta bancaria designada' })).toHaveCount(0);
    const historial = cierre.getByText('Cuenta bancaria designada aprobada', { exact: true });
    await expect(historial.first()).toBeVisible();
    await expect(cierre.getByText('Exige cuenta bancaria designada', { exact: true }).first()).toBeVisible();
    await paso(paginaFinal, {
      titulo: 'El expediente se aprueba solo',
      explicacion:
        'En el mismo momento en que la segunda persona aprueba la cuenta, el expediente que esperaba se cierra: queda «Expediente aprobado», el vínculo pasa a activo, se emite el certificado y el proveedor recibe el correo de aprobación. Nadie tiene que volver a pulsar «aprobar». ' +
        'El historial deja constancia: «Exige cuenta bancaria designada» cuando el expediente se abrió y «Cuenta bancaria designada aprobada» cuando se cumplió la exigencia.',
      resaltar: [cierre.getByRole('alert').getByText('Expediente aprobado'), historial.first()],
      avisos: true,
    });
  },
);
