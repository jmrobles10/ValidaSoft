import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Bloquea un vínculo y luego lo desbloquea',
    resumen:
      'La administradora bloquea un vínculo con un motivo y una fecha de fin para suspender al tercero sin borrar su historial, y después lo desbloquea con una justificación para que vuelva al estado anterior.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.',
      'Un vínculo de proveedor en estado Invitado.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'vinculos/vinculo-con-actualizacion-pendiente'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Norte S.A.S.');
    await invitar(e, e.norte, tercero, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1 })).toBeVisible();

    await app.getByRole('button', { name: 'Más acciones' }).click();
    await paso(page, {
      titulo: 'Abre el menú de más acciones',
      explicacion:
        'Las acciones que cambian el vínculo están en «Más acciones». Lo que aparece depende del estado del vínculo: un vínculo Invitado permite bloquear, desvincular y anular; un bloqueado ofrece desbloquear. ' +
        'Solo quien tiene permiso para bloquear ve la opción.',
      resaltar: app.getByRole('menuitem', { name: /^Bloquear/ }),
      variantes: [
        { cuando: 'Tu rol solo puede leer vínculos', entonces: 'El menú no aparece y el detalle es de solo lectura.' },
      ],
      configuracion: {
        ajuste: 'Usuarios y roles',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'El permiso de bloquear vínculos lo da el rol de cada persona.',
      },
      mantenerPuntero: true,
    });

    await app.getByRole('menuitem', { name: /^Bloquear/ }).click();
    const dialogo = app.getByRole('dialog', { name: 'Bloquear vínculo' });
    await dialogo.getByRole('textbox', { name: 'Motivo' }).fill('Documentos pendientes de revisión');
    const hasta = dialogo.getByRole('group', { name: 'Bloquear hasta (opcional)' });
    await hasta.getByRole('spinbutton', { name: 'Día' }).click();
    await page.keyboard.type('31122026');
    await paso(page, {
      titulo: 'Escribe el motivo y la fecha del bloqueo',
      explicacion:
        'El bloqueo suspende la relación con tu empresa sin borrar el historial. El motivo queda en la auditoría de la empresa; el tercero no lo ve. «Bloquear hasta» es opcional: si la dejas vacía, el bloqueo dura hasta que lo quites. ' +
        'Mientras esté bloqueado, los sistemas integrados reciben el cambio.',
      resaltar: dialogo,
    });

    await dialogo.getByRole('button', { name: 'Bloquear vínculo' }).click();
    await expect(app.getByText('Vínculo bloqueado.')).toBeVisible();
    await expect(app.getByText('Vínculo bloqueado', { exact: true }).first()).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba que el vínculo quedó bloqueado',
      explicacion:
        'El detalle muestra el aviso «Vínculo bloqueado» con desde cuándo y hasta cuándo rige. La lista de vínculos lo marca con el estado Bloqueado, y el tercero deja de poder avanzar con esta empresa mientras dure el bloqueo.',
      resaltar: app.getByText('Vínculo bloqueado', { exact: true }).first(),
      avisos: true,
    });

    await app.getByRole('button', { name: 'Desbloquear', exact: true }).click();
    const desbloqueo = app.getByRole('dialog', { name: 'Desbloquear vínculo' });
    await desbloqueo.getByRole('textbox', { name: 'Justificación' }).fill('Documentos recibidos, se levanta el bloqueo');
    await paso(page, {
      titulo: 'Desbloquea con una justificación',
      explicacion:
        'Desbloquear devuelve el vínculo al estado que tenía antes del bloqueo y deja registrada tu justificación en el historial. ' +
        'No borra el motivo original: queda como antecedente.',
      resaltar: desbloqueo,
    });

    await desbloqueo.getByRole('button', { name: 'Desbloquear vínculo' }).click();
    await expect(app.getByText('Vínculo desbloqueado.')).toBeVisible();
    await expect(app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).or(app.getByText('Invitado').first())).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba que el vínculo volvió a su estado',
      explicacion:
        'El vínculo vuelve a «Invitado», el estado previo al bloqueo, y el historial registra el bloqueo y el desbloqueo con sus fechas. Si el vínculo ya estaba aprobado, volvería a ese estado.',
      resaltar: app.getByText('Invitado').first(),
      avisos: true,
    });
  },
);
