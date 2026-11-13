import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, regionDeArea } from '../../soporte/empresa';
import { completarInvitacion, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import type { FrameLocator } from '@playwright/test';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Revisa una versión nueva que llega mientras decides',
    resumen:
      'Olga está a punto de aprobar el área Cumplimiento sobre el RUT versión 1 cuando el proveedor sube la versión 2. La pantalla se entera sola, el diálogo deja de permitir la aprobación y Olga aprueba sobre la versión nueva.',
    precondiciones: [
      'Un grupo con Constructora Norte, con Cumplimiento (Olga es miembro) y Compras.',
      'Un proveedor invitado por Norte que ya entregó su RUT (versión 1) y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/aprobar-documento-por-documento', 'revision/responder-con-lo-corregido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Maderas y Formaletas S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    // El proveedor trabaja en su propia ventana: la misma sesión sigue para subir la versión 2 después.
    const ventanaProveedor = await (await browser.newContext()).newPage();
    await completarInvitacion(ventanaProveedor, invitacion, [
      { nombre: RUT, archivo: pdf('RUT Maderas v1') },
      { nombre: CAMARA, archivo: pdf('Camara Maderas v1') },
    ]);

    // 1) Olga abre la aprobación del área con la versión 1.
    const app = await abrirEmbebido(page, e, e.olga, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Cumplimiento');
    await expect(regionDeArea(app, 'Cumplimiento')).toContainText(`${RUT} · v1`);
    await regionDeArea(app, 'Cumplimiento').getByRole('button', { name: /^Aprobar área/ }).click();
    const dialogo = app.getByRole('dialog', { name: /^Aprobar Cumplimiento/ });
    await expect(dialogo.getByRole('button', { name: /^Aprobar área/ })).toBeEnabled();
    await paso(page, {
      titulo: 'Confirma la aprobación con la versión que ves',
      explicacion:
        'El diálogo muestra los documentos que aprobarás, cada uno con su versión: aquí, el RUT versión 1. La aprobación se hace sobre la versión que ves en pantalla. ' +
        'Si el proveedor sube otra versión mientras el diálogo está abierto, la pantalla se actualiza sola.',
      resaltar: dialogo,
    });

    // 2) Mientras Olga decide, el proveedor sube la versión 2.
    await subirDocumento(ventanaProveedor, RUT, pdf('RUT Maderas v2'), { version: 2 });

    // 3) El diálogo avisa y no deja aprobar la versión superada.
    await expect(dialogo.getByText('El expediente cambió y esta decisión ya no aplica a su estado actual.')).toBeVisible({ timeout: 30_000 });
    await expect(dialogo.getByRole('button', { name: /^Aprobar área/ })).toBeDisabled();
    await paso(page, {
      titulo: 'Entérate de que hubo una versión nueva',
      explicacion:
        'Aparece el aviso «El expediente cambió y esta decisión ya no aplica a su estado actual» y el botón de aprobar queda deshabilitado. ' +
        'No puedes aprobar una versión que ya no es la vigente: cierra el diálogo y vuelve a decidir sobre la nueva.',
      resaltar: dialogo.getByText('El expediente cambió y esta decisión ya no aplica a su estado actual.'),
    });
    await dialogo.getByRole('button', { name: 'Cerrar' }).click();

    // 4) Ahora ve la versión 2 y aprueba sobre ella.
    await expect(regionDeArea(app, 'Cumplimiento')).toContainText(`${RUT} · v2`);
    await paso(page, {
      titulo: 'Aprueba la versión vigente',
      explicacion:
        'La sección del área ya muestra la versión 2 del RUT. Aprueba el área sobre esa versión: el veredicto queda registrado sobre la v2 y no sobre la anterior. ' +
        'La versión 1 deja de contar para Cumplimiento.',
      resaltar: regionDeArea(app, 'Cumplimiento'),
    });
    await aprobarVersionNueva(app);
    await expect(regionDeArea(app, 'Cumplimiento').filter({ hasText: 'Aprobada' })).toBeVisible();
  },
);

async function aprobarVersionNueva(app: FrameLocator): Promise<void> {
  await regionDeArea(app, 'Cumplimiento').getByRole('button', { name: /^Aprobar área/ }).click();
  const dialogo = app.getByRole('dialog', { name: /^Aprobar Cumplimiento/ });
  await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
  await expect(dialogo).toBeHidden();
}
