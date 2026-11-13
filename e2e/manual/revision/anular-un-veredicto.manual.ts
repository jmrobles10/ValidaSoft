import { abrirEmbebido } from '../../soporte/embebido';
import { tarjetaDeArea } from '../../soporte/devolucion';
import { abrirExpedienteManual, aprobarPorApi, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { areaDe } from '../nucleo/semillas';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'revision',
    actor: 'Administrador de empresa',
    titulo: 'Anula el veredicto de un área',
    resumen:
      'Cumplimiento aprobó por error el expediente de un aliado, antes de que su revisión estuviera completa. Ana, administradora, anula ese veredicto desde el menú del área: el área vuelve a estar pendiente y la decisión queda registrada con la justificación.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga), que revisan en la misma etapa. Ana es administradora.',
      'Un expediente de un aliado en el que Cumplimiento ya emitió un veredicto de aprobación y Compras aún no decide.',
    ],
    relacionados: ['revision/desaprobar-y-reabrir-una-etapa', 'revision/aprobar-un-area'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS_ANULAR', 'ALIADO', [
      [{ area: areaDe(e.norte, 'compras') }, { area: areaDe(e.norte, 'cumplimiento'), puedeRechazar: true }],
    ]);
    const tercero = nuevoTercero('Mensajería Rápida S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    const expediente = await abrirExpedienteManual(e, invitacion);
    await aprobarPorApi(e, invitacion, expediente, areaDe(e.norte, 'cumplimiento'), e.olga);

    // 1) Cumplimiento aparece aprobada; Ana ve el menú de administrador de esa área.
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(tarjetaDeArea(app, 'Cumplimiento')).toContainText('Aprobada');
    const menu = app.getByRole('button', { name: 'Acciones de administrador en Cumplimiento' });
    await paso(page, {
      titulo: 'Localiza el veredicto que quieres anular',
      explicacion:
        'En la tarjeta del área aparece su veredicto con quién lo emitió y cuándo. Las acciones de administrador de esa área están en su menú, con «Anular veredicto». ' +
        'Anular deja sin efecto el veredicto más reciente del área; no borra el historial.',
      resaltar: [tarjetaDeArea(app, 'Cumplimiento'), menu],
      variantes: [
        {
          cuando: 'El veredicto era una observación',
          entonces: 'Anularla reanuda la revisión sin esperar a que el proveedor responda, si era la última observación pendiente.',
        },
        { cuando: 'No eres administrador', entonces: 'No ves el menú de acciones de administrador en el área.' },
      ],
    });

    // 2) Justificación y anulación.
    await menu.click();
    await app.getByRole('menuitem', { name: 'Anular veredicto' }).click();
    const dialogo = app.getByRole('dialog', { name: /^Anular el veredicto de Cumplimiento/ });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Aprobó antes de tener el certificado de la Cámara.');
    await paso(page, {
      titulo: 'Anula el veredicto con justificación',
      explicacion:
        'El diálogo dice qué veredicto se va a anular, con su tipo. La justificación es obligatoria y queda en el historial con tu nombre. ' +
        'Al confirmar, el área vuelve a estar pendiente y puede volver a decidir.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Anular veredicto', exact: true }).click();
    await expect(dialogo).toBeHidden();

    // 3) El área vuelve a estar pendiente.
    await expect(tarjetaDeArea(app, 'Cumplimiento')).not.toContainText('Aprobada');
    await expect(tarjetaDeArea(app, 'Cumplimiento')).toContainText('Anulado');
    await paso(page, {
      titulo: 'Verifica que el área volvió a estar pendiente',
      explicacion:
        'Cumplimiento ya no aparece como aprobada: su veredicto queda como «Anulado» en el historial del área y el área puede decidir otra vez. ' +
        'El expediente sigue en revisión hasta que todas las áreas aprueben.',
      resaltar: tarjetaDeArea(app, 'Cumplimiento'),
    });
  },
);
