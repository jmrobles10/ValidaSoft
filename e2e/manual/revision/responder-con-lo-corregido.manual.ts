import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, observar, regionDeArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero, responder, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Revisa lo que corrigió el proveedor tras una observación',
    resumen:
      'Compras observó el RUT de un proveedor. El proveedor sube la versión corregida y responde. Rubén vuelve a su expediente y ve qué cambió, qué le piden al área y la respuesta del proveedor, para decidir de nuevo.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras (Rubén es miembro).',
      'Un expediente devuelto al proveedor por una observación de Compras sobre el RUT (versión 1).',
    ],
    relacionados: ['revision/observar-con-motivos', 'revision/recordatorio-de-observaciones', 'revision/historial-del-expediente'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Plásticos del Oriente S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    const ventanaProveedor = await (await browser.newContext()).newPage();
    await completarInvitacion(ventanaProveedor, invitacion, [
      { nombre: RUT, archivo: pdf('RUT Plasticos Oriente v1') },
      { nombre: CAMARA, archivo: pdf('Camara Plasticos Oriente v1') },
    ]);
    const expediente = await expedienteAbierto(e, invitacion);
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(ruben, proveedor.nombre, 'Compras');
    await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'El RUT no se lee en la parte de la dirección.');
    await esperarEstadoDelTercero(ventanaProveedor, 'Observado');

    // El proveedor corrige y responde.
    await subirDocumento(ventanaProveedor, RUT, pdf('RUT Plasticos Oriente v2'), { version: 2 });
    await responder(ventanaProveedor, 'Adjunto el RUT con la dirección legible.');
    await esperarEstadoDelTercero(ventanaProveedor, 'EnRevision');

    // 1) Rubén vuelve al expediente: el área vuelve a revisar la versión corregida.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: proveedor.nombre })).toBeVisible();
    await expect(regionDeArea(app, 'Compras')).toContainText(`${RUT} · v2`);
    await paso(page, {
      titulo: 'Revisa la versión corregida de tu área',
      explicacion:
        'Cuando el proveedor reenvía, el expediente deja de estar devuelto y tu área vuelve a revisar: en su sección aparece la versión nueva del documento, con el botón para aprobar lo que queda por decidir. ' +
        'Las áreas que no observaron no vuelven a revisar lo que ya aprobaron.',
      resaltar: regionDeArea(app, 'Compras'),
    });

    // 2) La respuesta del proveedor queda a la vista para decidir.
    await expect(app.getByText('Última respuesta del tercero', { exact: true }).first()).toBeVisible();
    await paso(page, {
      titulo: 'Lee la respuesta del proveedor',
      explicacion:
        'La última respuesta del proveedor aparece en el expediente con su texto, junto con la versión que subió. Léela antes de decidir: si la corrección resuelve el motivo, aprueba la versión nueva; si no, vuelve a observar con los motivos que correspondan.',
      resaltar: app.getByText('Última respuesta del tercero', { exact: true }).first(),
    });
  },
);
