import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, observar } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Recuerda al proveedor sus observaciones pendientes',
    resumen:
      'Compras observó el RUT de un proveedor y este no ha respondido. Rubén envía un recordatorio individual desde el expediente: el proveedor recibe un correo con lo que falta corregir y un aviso en su portal. Solo se puede enviar uno al día por expediente.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras (Rubén es miembro).',
      'Un expediente devuelto al proveedor por una observación de Compras sobre el RUT, sin respuesta todavía.',
    ],
    relacionados: ['revision/observar-con-motivos', 'revision/responder-con-lo-corregido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Confecciones Sol S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Confecciones Sol') },
      { nombre: CAMARA, archivo: pdf('Camara Confecciones Sol') },
    ]);
    const expediente = await expedienteAbierto(e, invitacion);
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(ruben, proveedor.nombre, 'Compras');
    await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Incompleto'] }], 'Falta la página 3 del RUT.');

    // 1) El expediente observado tiene el botón de recordatorio.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: proveedor.nombre })).toBeVisible();
    const recordar = app.getByRole('button', { name: 'Recordar al tercero', exact: true });
    await expect(recordar).toBeVisible();
    await paso(page, {
      titulo: 'Localiza el recordatorio en el expediente',
      explicacion:
        'Un expediente devuelto al proveedor muestra el botón «Recordar al tercero». Sirve para cuando el proveedor no ha respondido a tus observaciones. ' +
        'Si ya se le recordó hoy, el botón dice que ya se le recordó y no envía otro.',
      resaltar: recordar,
      variantes: [
        { cuando: 'El proveedor ya se le recordó hoy', entonces: 'El botón queda como «Ya se le recordó hoy» y no se puede enviar otro recordatorio hasta el día siguiente.' },
        { cuando: 'El expediente no está devuelto al proveedor', entonces: 'No hay botón: no hay observaciones que recordar.' },
      ],
    });

    // 2) Confirmación antes de enviar.
    await recordar.click();
    const dialogo = app.getByRole('dialog', { name: '¿Recordarle sus observaciones al tercero?' });
    await expect(dialogo).toContainText('Solo se puede una vez al día.');
    await paso(page, {
      titulo: 'Confirma el recordatorio',
      explicacion:
        'Antes de enviar, el diálogo dice qué recibe el proveedor: un correo con lo que falta corregir y un enlace para responder, y un aviso en las notificaciones de su portal. ' +
        'Se puede enviar una vez al día por expediente.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Enviar recordatorio', exact: true }).click();
    await expect(dialogo).toBeHidden();
    await expect(app.getByText(/Último recordatorio:/).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa que el recordatorio quedó registrado',
      explicacion:
        'El expediente muestra la fecha del último recordatorio, y el botón ya no deja enviar otro hoy. ' +
        'El proveedor sigue viendo las observaciones en su portal hasta que responda o cargue la versión corregida.',
      resaltar: app.getByText(/Último recordatorio:/).first(),
    });
  },
);
