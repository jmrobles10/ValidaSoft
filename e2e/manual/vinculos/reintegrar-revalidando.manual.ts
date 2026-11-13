import { abrirEmbebido } from '../../soporte/embebido';
import { accionDeVinculo } from '../../soporte/devolucion';
import { abrirExpedienteManual, crearTipoDeVinculo, invitarVinculo, leerVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { areaDe } from '../nucleo/semillas';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Reintegra un vínculo revalidando',
    resumen:
      'La administradora reintegra un aliado desvinculado que nunca llegó a aprobarse. El diálogo le explica que no puede reintegrarlo sin revalidar, así que elige revalidar y el vínculo vuelve a su primera etapa para empezar de nuevo.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y una administradora.',
      'Un tipo de vínculo Aliado con un flujo de una etapa (Compras) y un vínculo con el expediente abierto, sin aprobar.',
    ],
    relacionados: ['vinculos/desvincular-y-reintegrar-sin-revalidar', 'vinculos/bloquear-y-desbloquear'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await crearTipoDeVinculo(e, 'ALIADO', 'Aliado');
    await publicarFlujoSinDocumentos(e, e.norte, 'ALIADOS', 'ALIADO', [[{ area: areaDe(e.norte, 'compras') }]]);
    const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(), 'ALIADO');
    await abrirExpedienteManual(e, invitacion);

    const app = await abrirEmbebido(page, e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    await accionDeVinculo(app, 'Desvincular', 'Se canceló la negociación.');
    await expect(app.getByText('Tercero desvinculado.')).toBeVisible();
    expect((await leerVinculo(e, invitacion)).expedienteAbiertoId).toBeNull();

    await paso(page, {
      titulo: 'Abre el reintegro del vínculo desvinculado',
      explicacion:
        'Un vínculo desvinculado muestra el botón «Reintegrar» en su encabezado. Reintegrar devuelve la relación con el tercero; antes de confirmar, el diálogo te dice qué puedes hacer según lo que el vínculo ya tenía aprobado.',
      resaltar: app.getByRole('button', { name: 'Reintegrar', exact: true }),
      variantes: [
        {
          cuando: 'El vínculo estaba aprobado y sus documentos siguen vigentes',
          entonces: 'Puedes elegir «Reintegrar sin revalidar» y el vínculo vuelve a Activo sin pedir documentos (ver el recorrido de desvincular y reintegrar).',
        },
      ],
    });

    await app.getByRole('button', { name: 'Reintegrar', exact: true }).click();
    const dialogo = app.getByRole('dialog', { name: /^Reintegrar vínculo/ });
    await expect(dialogo.getByText('No se puede reintegrar sin revalidar:')).toBeVisible();
    await expect(dialogo.getByRole('listitem').filter({ hasText: /El vínculo nunca fue aprobado/ })).toBeVisible();
    await expect(dialogo.getByRole('radio', { name: /Reintegrar sin revalidar/ })).toBeDisabled();
    await paso(page, {
      titulo: 'Lee por qué no se puede reintegrar sin revalidar',
      explicacion:
        'El diálogo lista la razón: el vínculo nunca fue aprobado, así que no hay una aprobación que restituir. La opción «Reintegrar sin revalidar» aparece deshabilitada. ' +
        'La única salida es revalidar, que abre el proceso de validación desde cero.',
      resaltar: [dialogo.getByText('No se puede reintegrar sin revalidar:'), dialogo.getByRole('radio', { name: /Reintegrar sin revalidar/ })],
      variantes: [
        {
          cuando: 'El vínculo estaba aprobado y uno de sus documentos terminó su vigencia (o está en gracia)',
          entonces: 'El diálogo lo lista como «Su vigencia terminó: …» y la opción sin revalidar no se ofrece: lo aprobado ya no está vigente y hay que revalidar.',
        },
        {
          cuando: 'El tercero subió una versión nueva en su bóveda que la empresa no revisó',
          entonces: 'El diálogo lo lista como «Cambió en la bóveda del tercero, sin que la empresa lo revisara: …» y tampoco se ofrece reintegrar sin revalidar.',
        },
      ],
    });

    await expect(dialogo.getByRole('radio', { name: /^Revalidar/ })).toBeChecked();
    await dialogo.getByRole('textbox', { name: 'Justificación' }).fill('Nuevo contrato, se vuelve a validar.');
    await paso(page, {
      titulo: 'Elige revalidar y escribe la justificación',
      explicacion:
        'Revalidar deja el vínculo en validación con un expediente nuevo que pide todos los documentos del flujo. La justificación queda en el historial, y el tercero no la ve. Si nunca fue aprobado, el vínculo vuelve a Invitado para empezar de nuevo.',
      resaltar: [dialogo.getByRole('radio', { name: /^Revalidar/ }), dialogo.getByRole('textbox', { name: 'Justificación' })],
    });

    await dialogo.getByRole('button', { name: 'Reintegrar vínculo' }).click();
    await expect(dialogo).toBeHidden();
    await expect(app.getByText('Invitado', { exact: true }).first()).toBeVisible();
    expect((await leerVinculo(e, invitacion)).estado).toBe('Invitado');
    await paso(page, {
      titulo: 'Comprueba que el vínculo volvió a Invitado',
      explicacion:
        'El vínculo vuelve al estado «Invitado» y el historial registra el reintegro con tu justificación. El tercero recibe el aviso de que se reintegró su vínculo y debe volver a aceptar la invitación para revalidar.',
      resaltar: app.getByText('Invitado', { exact: true }).first(),
      avisos: true,
    });
  },
);
