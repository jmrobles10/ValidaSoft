import { abrirEmbebido } from '../../soporte/embebido';
import { etapa, tarjetaDeArea } from '../../soporte/devolucion';
import { abrirExpedienteManual, aprobarPorApi, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { areaDe } from '../nucleo/semillas';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'revision',
    actor: 'Administrador de empresa',
    titulo: 'Reabre una etapa ya aprobada',
    resumen:
      'Ana, administradora, descubre que Compras aprobó sin revisar bien un documento. Reabre la etapa 1: Compras vuelve a decidir, la etapa 2 espera y la aprobación anterior queda en el historial como revertida.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.',
      'Un flujo de Aliado con dos etapas en secuencia: Compras, luego Cumplimiento. Sin documentos que subir.',
      'Compras ya aprobó (etapa 1 aprobada) y el expediente está en la etapa 2.',
    ],
    relacionados: ['revision/desaprobar-un-expediente-aprobado', 'revision/etapas-en-secuencia'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS_REABRIR', 'ALIADO', [
      [{ area: areaDe(e.norte, 'compras') }],
      [{ area: areaDe(e.norte, 'cumplimiento'), puedeRechazar: true }],
    ]);
    const tercero = nuevoTercero('Servicios Integrales S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    const expediente = await abrirExpedienteManual(e, invitacion);
    await aprobarPorApi(e, invitacion, expediente, areaDe(e.norte, 'compras'), e.ruben);

    // 1) La etapa 1 aparece aprobada y Ana ve la acción de reabrirla.
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(etapa(app, 1)).toContainText('Aprobada');
    await expect(etapa(app, 2)).toContainText('En curso');
    const reabrir = app.getByRole('button', { name: 'Reabrir la etapa 1' });
    await paso(page, {
      titulo: 'Localiza la etapa que quieres reabrir',
      explicacion:
        'En «Etapas y áreas», cada etapa aprobada tiene el botón «Reabrir» para volver a decidir sobre ella. Solo lo ve quien administra la empresa: los revisores de un área no tienen esta acción. ' +
        'Reabrir una etapa no borra lo que se aprobó: queda en el historial como revertido.',
      resaltar: [etapa(app, 1), reabrir],
      variantes: [
        { cuando: 'Eres revisor de un área y no administrador', entonces: 'No ves el botón «Reabrir» ni las acciones de desaprobar.' },
        { cuando: 'La etapa aún está en curso', entonces: 'No hay botón «Reabrir»: solo se reabren etapas ya aprobadas.' },
      ],
    });

    // 2) Justifica la reapertura.
    await reabrir.click();
    const dialogo = app.getByRole('dialog', { name: /^Reabrir la etapa 1/ });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Compras aprobó sin ver el certificado de la Cámara.');
    await paso(page, {
      titulo: 'Justifica la reapertura',
      explicacion:
        'Escribe por qué reabres la etapa: la justificación queda en el historial con tu nombre y la fecha. La etapa reabierta y las siguientes vuelven a revisarse; las áreas de la etapa 1 deciden otra vez.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Reabrir etapa' }).click();
    await expect(dialogo).toBeHidden();

    // 3) El resultado: la etapa 1 vuelve a revisarse y la 2 espera.
    await expect(etapa(app, 1)).toContainText('En curso');
    await expect(etapa(app, 2)).toContainText('En espera');
    await expect(tarjetaDeArea(app, 'Compras')).toContainText('Revertido por un administrador');
    await expect(app.getByText('Devuelto a revisión por un administrador')).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cómo queda el expediente',
      explicacion:
        'La etapa 1 vuelve a «En curso» y la etapa 2 a «En espera». La aprobación de Compras aparece como «Revertido por un administrador», y el aviso del expediente registra quién reabrió y cuándo. ' +
        'Compras decide otra vez, y cuando apruebe, la etapa 2 empieza de nuevo.',
      resaltar: [etapa(app, 1), etapa(app, 2)],
    });
  },
);
