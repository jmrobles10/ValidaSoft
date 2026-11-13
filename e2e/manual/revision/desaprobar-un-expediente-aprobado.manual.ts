import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { accionDeExpediente, etapa } from '../../soporte/devolucion';
import { abrirExpedienteManual, aprobarPorApi, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'revision',
    actor: 'Administrador de empresa',
    titulo: 'Desaprueba un expediente aprobado',
    resumen:
      'Ana descubre una inhabilidad después de aprobar a un aliado. Desaprueba el expediente: el vínculo vuelve a validación, su certificado se revoca y el proveedor recibe un aviso sin la justificación interna. Al aprobarlo de nuevo se emite otro certificado.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.',
      'Un flujo de Aliado con dos etapas en secuencia. El vínculo ya está aprobado y tiene su certificado vigente.',
    ],
    relacionados: ['revision/desaprobar-y-reabrir-una-etapa'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS_DESAPROBAR', 'ALIADO', [
      [{ area: e.norte.areas.compras! }],
      [{ area: e.norte.areas.cumplimiento!, puedeRechazar: true }],
    ]);
    const tercero = nuevoTercero('Logística Global S.A.S.');
    const invitacion = await invitarVinculo(e, e.norte, tercero, 'ALIADO');
    const expediente = await abrirExpedienteManual(e, invitacion);
    await aprobarPorApi(e, invitacion, expediente, e.norte.areas.compras!, e.ruben);
    await aprobarPorApi(e, invitacion, expediente, e.norte.areas.cumplimiento!, e.olga);

    // 1) El expediente aprobado: Ana lo abre y ve el aviso de aprobación.
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await expect(app.getByRole('alert').getByText('Expediente aprobado')).toBeVisible();
    await paso(page, {
      titulo: 'Abre un expediente ya aprobado',
      explicacion:
        'El expediente aprobado muestra el aviso «Expediente aprobado» con la fecha de aprobación. Las dos etapas están aprobadas. ' +
        'Desde «Más acciones» quien administra la empresa puede desaprobarlo si aparece un hallazgo que cambia la decisión.',
      resaltar: app.getByRole('alert').getByText('Expediente aprobado'),
      variantes: [
        { cuando: 'Eres revisor de un área', entonces: 'No ves «Más acciones» ni la opción de desaprobar: solo un administrador puede desaprobar.' },
      ],
    });

    // 2) Desaprobar con justificación.
    await app.getByRole('button', { name: 'Más acciones' }).click();
    await app.getByRole('menuitem', { name: 'Desaprobar expediente' }).click();
    const dialogo = app.getByRole('dialog', { name: /^Desaprobar expediente/ });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Apareció una inhabilidad que no estaba en las listas.');
    await paso(page, {
      titulo: 'Desaprueba el expediente',
      explicacion:
        'La desaprobación dice qué pasa: el mismo expediente vuelve a revisión, el vínculo pasa a En validación y su certificado se revoca. ' +
        'Eliges desde qué etapa se revisa de nuevo: la última, la primera o una etapa concreta. Las etapas anteriores conservan su aprobación. La justificación es interna.',
      resaltar: dialogo,
      configuracion: {
        ajuste: 'Desde qué etapa se revisa de nuevo',
        efecto: 'Las etapas anteriores a la elegida conservan su aprobación.',
      },
    });
    await dialogo.getByRole('button', { name: 'Desaprobar expediente' }).click();
    await expect(dialogo).toBeHidden();

    // 3) El expediente vuelve a revisión.
    await expect(app.getByText('Devuelto a revisión por un administrador')).toBeVisible();
    await expect(app.getByRole('alert').getByText('Expediente aprobado')).toHaveCount(0);
    await expect(etapa(app, 2)).toContainText('En curso');
    await paso(page, {
      titulo: 'Revisa el expediente desaprobado',
      explicacion:
        'El expediente ya no está aprobado: la etapa 1 conserva su aprobación y la etapa 2 vuelve a «En curso». El vínculo queda en validación con este mismo expediente. ' +
        'Cuando el área vuelva a aprobar, se emite un certificado nuevo y el anterior queda como revocado.',
      resaltar: [app.getByText('Devuelto a revisión por un administrador'), etapa(app, 2)],
    });

    // 4) El proveedor se entera por correo, sin la justificación.
    const aviso = await esperarCorreo(invitacion.correo, /volvió a revisar tu validación/);
    expect(aviso.texto).not.toContain('inhabilidad');
    await page.setContent(aviso.html);
    await paso(page, {
      titulo: 'Así se entera el proveedor',
      explicacion:
        'El proveedor recibe un correo que dice que la empresa volvió a revisar su validación. La justificación interna no sale en el correo: el proveedor solo sabe que se revisa de nuevo.',
      celular: false,
    });
  },
);
