import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Anula un vínculo creado por error',
    resumen:
      'La administradora anula un vínculo que se creó por error: escribe el motivo, confirma escribiendo la palabra ANULAR y el vínculo deja de estar activo para esta empresa, sin que pueda deshacerse.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.',
      'Un vínculo de proveedor en estado Invitado creado por error.',
    ],
    relacionados: ['vinculos/bloquear-y-desbloquear', 'vinculos/detalle-del-vinculo'],
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
      titulo: 'Abre la acción Anular',
      explicacion:
        'Anular sirve para vínculos creados por error, por ejemplo una invitación enviada a la persona equivocada. Está en «Más acciones», junto a las demás acciones del estado del vínculo. ' +
        'Anular no es un bloqueo ni una desvinculación: no se puede deshacer, aunque si ese tercero vuelve a serlo se crea un vínculo nuevo.',
      resaltar: app.getByRole('menuitem', { name: /^Anular/ }),
      variantes: [
        { cuando: 'El vínculo ya tiene un expediente aprobado', entonces: 'La acción Anular no aparece para ese vínculo: primero hay que desvincularlo.' },
      ],
    });

    await app.getByRole('menuitem', { name: /^Anular/ }).click();
    const dialogo = app.getByRole('dialog', { name: 'Anular vínculo' });
    await dialogo.getByRole('textbox', { name: 'Motivo' }).fill('Invitación enviada a un tercero equivocado');
    await dialogo.getByRole('textbox', { name: 'Escribe ANULAR para confirmar' }).fill('ANULAR');
    await paso(page, {
      titulo: 'Escribe el motivo y confirma con la palabra ANULAR',
      explicacion:
        'El diálogo pide el motivo, que queda en el historial con tu nombre, y que escribas la palabra ANULAR tal cual para confirmar. Sin esa palabra el botón de anular no se activa. ' +
        'Mientras no lo confirmes, nada cambia: puedes cerrar el diálogo con Cancelar.',
      resaltar: dialogo,
    });

    await dialogo.getByRole('button', { name: 'Anular vínculo' }).click();
    await expect(app.getByText('Vínculo anulado.')).toBeVisible();
    const chip = app.getByTitle(/^Anulado el \d{2}\/\d{2}\/\d{4}$/);
    const entrada = app.getByText('Vínculo anulado', { exact: true });
    await expect(chip).toBeVisible();
    await expect(entrada).toBeVisible();
    await expect(app.getByText('Invitación enviada a un tercero equivocado')).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba que el vínculo quedó anulado',
      explicacion:
        'Validasoft confirma con el aviso «Vínculo anulado.» y se queda en el detalle, que ahora muestra el estado «Anulado» con su fecha (1). El historial (2) conserva la anulación con el estado anterior, el motivo y quién la hizo. ' +
        'El vínculo deja de aparecer entre los vigentes de la empresa y ya no recibe invitaciones ni expedientes.',
      resaltar: [chip, entrada],
      avisos: true,
    });
  },
);
