import { abrirEmbebido } from '../../soporte/embebido';
import { emitirAprobacion, tarjetaDeArea } from '../../soporte/devolucion';
import { abrirExpedienteManual, crearArea, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Aprueba un área que pide dos personas distintas',
    resumen:
      'El Comité de Norte necesita dos aprobaciones de personas distintas. Ana invitó al proveedor, así que la separación de funciones le impide decidir. Rubén aprueba la primera; Olga, la segunda, y el vínculo queda activo.',
    precondiciones: [
      'Un grupo con Constructora Norte y el área Comité con Ana, Rubén y Olga como miembros. El flujo de Aliado pide 2 aprobaciones del Comité y tiene separación de funciones activada.',
      'Ana, la administradora, crea la invitación: queda registrada como quien invitó al proveedor.',
    ],
    relacionados: ['revision/etapas-en-secuencia', 'revision/aprobar-un-area'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    const comite = await crearArea(e, e.norte, 'comite', 'Comité', [e.ana, e.ruben, e.olga]);
    await publicarFlujoSinDocumentos(e, e.norte, 'COMITE_DOS', 'ALIADO', [[{ area: comite, aprobadores: 2 }]], { separacionDeFunciones: true });
    const tercero = nuevoTercero('Consultores Unidos S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO', e.ana);
    const expediente = await abrirExpedienteManual(e, invitacion);

    // 1) Ana invitó: el botón de emitir veredicto está, pero la API no la deja decidir.
    const ana = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(ana.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(tarjetaDeArea(ana, 'Comité')).toContainText('Faltan 2 aprobaciones');
    await ana.getByRole('button', { name: 'Emitir veredicto' }).click();
    const dialogoAna = ana.getByRole('dialog', { name: /^Veredicto de Comité/ });
    await dialogoAna.getByRole('button', { name: 'Aprobar', exact: true }).click();
    await expect(dialogoAna.getByRole('alert')).toContainText(/separación de funciones/);
    await paso(page, {
      titulo: 'Intenta aprobar siendo quien invitó',
      explicacion:
        'Con la separación de funciones activa, quien invitó al proveedor no puede decidir sobre su expediente, aunque sea miembro del área. Al intentarlo, la pantalla muestra un aviso que lo explica y el veredicto no se registra. ' +
        'Cancela el diálogo y deja la decisión a otra persona del área.',
      resaltar: dialogoAna.getByRole('alert'),
    });
    await dialogoAna.getByRole('button', { name: 'Cancelar' }).click();

    // 2) Rubén aprueba: falta otra persona. Las dos aprobaciones deben ser de personas distintas.
    const ventanaRuben = await (await browser.newContext()).newPage();
    const ruben = await abrirEmbebido(ventanaRuben, e, e.ruben, e.norte, `expedientes/${expediente}`);
    await emitirAprobacion(ruben, 'Comité');
    await expect(tarjetaDeArea(ruben, 'Comité')).toContainText('Falta 1 aprobación');
    await expect(tarjetaDeArea(ruben, 'Comité')).toContainText('Ya emitiste tu veredicto; falta otra persona del área.');
    await paso(page, {
      titulo: 'Aprueba como primera persona del área',
      explicacion:
        'La primera aprobación cuenta como «1 de 2». El área sigue pendiente y el mismo revisor ya no puede votar otra vez: la tarjeta del área dice que falta otra persona. ' +
        'El expediente sigue en revisión hasta que la segunda persona apruebe.',
      resaltar: tarjetaDeArea(ruben, 'Comité'),
    });

    // 3) Olga aprueba: la segunda persona cierra el área, el expediente y el vínculo.
    const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, `expedientes/${expediente}`);
    await emitirAprobacion(olga, 'Comité');
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
    await paso(page, {
      titulo: 'Aprueba como segunda persona y activa el vínculo',
      explicacion:
        'La segunda persona distinta completa las dos aprobaciones: el área queda aprobada, el expediente se aprueba y el vínculo queda activo. ' +
        'Si la misma persona hubiera intentado aprobar dos veces, la segunda no habría contado.',
      resaltar: olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./),
      configuracion: {
        ajuste: 'Aprobadores requeridos por área y separación de funciones (en el flujo)',
        ruta: 'Configuración › Flujos (/flujos)',
        efecto: 'Define cuántas personas distintas deben aprobar y si quien invitó puede decidir.',
      },
    });
  },
);
