import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Reenvía la invitación a un tercero',
    resumen:
      'La administradora reenvía la invitación a un proveedor que aún no ha aceptado, elige el correo y la vigencia del enlace, y confirma el envío.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.',
      'Un vínculo de proveedor invitado que no ha sido aceptado.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'tercero/invitacion-vencida-o-usada'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Norte S.A.S.');
    await invitar(e, e.norte, tercero, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.norte, 'vinculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1 })).toBeVisible();

    await paso(page, {
      titulo: 'Abre la acción «Reenviar invitación»',
      explicacion:
        'Mientras el vínculo está en «Invitado», el detalle muestra el botón «Reenviar invitación» junto al nombre. Úsalo cuando el tercero no encontró el correo, el enlace venció o cambiaste el correo de contacto. ' +
        'Cada envío aumenta el contador de «Envíos» en Datos del vínculo.',
      resaltar: app.getByRole('button', { name: 'Reenviar invitación' }),
      variantes: [
        { cuando: 'El tercero ya aceptó la invitación', entonces: 'Ya no aparece el botón: el vínculo ya no está en «Invitado».' },
      ],
    });

    await app.getByRole('button', { name: 'Reenviar invitación' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Reenviar invitación' });
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Confirma el correo y la vigencia del enlace',
      explicacion:
        'El diálogo propone el correo de contacto del tercero; puedes cambiarlo si el tercero usa otra dirección. «Vigencia del enlace (horas)» indica cuánto tiempo sirve el enlace nuevo: pasado ese plazo, el enlace deja de abrir y hay que reenviar. ' +
        'El tercero recibe un correo con el enlace para completar su información.',
      resaltar: dialogo,
    });

    await dialogo.getByRole('button', { name: 'Reenviar invitación' }).click();
    await expect(app.getByText(`Invitación enviada a ${tercero.correo}.`)).toBeVisible();
    await expect(app.getByText(/Envíos: 2/)).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba el nuevo envío en el detalle',
      explicacion:
        'Validasoft confirma con un aviso que la invitación se envió a ese correo y el detalle actualiza «Invitación» con el número de envíos. ' +
        'El historial registra el reenvío con su fecha y quién lo hizo.',
      resaltar: app.getByText(/Envíos: 2/),
      avisos: true,
    });
  },
);
