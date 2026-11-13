import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const ASUNTO = 'Bienvenido a la red de proveedores de Constructora Sur';
const CABECERA = 'Gracias por trabajar con nosotros.';
const PIE = 'Mesa de ayuda: 601 555 0100';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Personaliza el correo de invitación y restablécelo',
    resumen:
      'La administradora abre Plantillas de correo, cambia el asunto, la cabecera y el pie del correo de invitación y lo guarda. Después restablece el texto predeterminado de Validasoft.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y sus plantillas de correo sin personalizar.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'plantillas');
    await expect(app.getByRole('heading', { level: 1, name: 'Plantillas de correo' })).toBeVisible();
    const filaInvitacion = app.getByRole('row').filter({ hasText: 'Invitación' });
    await expect(filaInvitacion).toContainText('Predeterminada');

    // 1. La lista de correos.
    await paso(page, {
      titulo: 'Abre Configuración › Plantillas de correo',
      explicacion:
        'La tabla lista los correos que Validasoft le envía al tercero en nombre de la empresa, por ejemplo la invitación. Cada fila muestra si el correo es «Predeterminada» (sale con el texto de Validasoft) o «Personalizada» (tiene un asunto, una cabecera o un pie propios) y su versión. ' +
        'Validasoft arma el cuerpo del correo (enlaces, documentos, plazos); tú solo agregas un texto antes y otro después.',
      resaltar: app.getByRole('table', { name: 'Plantillas de correo' }),
      configuracion: {
        ajuste: 'Plantillas de correo',
        ruta: 'Configuración › Plantillas de correo (/plantillas)',
        efecto: 'El asunto, la cabecera y el pie que se usan en cada correo del grupo para la empresa activa.',
      },
      variantes: [
        { cuando: 'No tienes permiso de configuración', entonces: 'El botón de cada fila dice «Ver» y el panel se abre en solo lectura, con un aviso que lo indica.' },
      ],
    });

    // 2. Elegir el correo. Se documenta antes de abrir el panel.
    await paso(page, {
      titulo: 'Elige el correo que quieres cambiar',
      explicacion:
        'Haz clic en «Editar» en la fila del correo (aquí, la invitación). Se abre un panel lateral con el asunto, la cabecera y el pie del correo. Los campos vacíos usan el texto de Validasoft, así que puedes cambiar solo el que necesites.',
      resaltar: filaInvitacion.getByRole('button'),
      variantes: [{ cuando: 'El correo ya es «Personalizada»', entonces: 'Los campos aparecen con el texto que guardaste y el panel ofrece «Restaurar predeterminado».' }],
    });

    // Con el panel abierto no se toman pantallazos: se llenan los campos, se comprueba la vista previa y se guarda.
    await filaInvitacion.getByRole('button').click();
    const panel = app.getByRole('dialog', { name: /Editar «Invitación/ });
    await expect(panel.getByRole('textbox', { name: 'Asunto' })).toBeVisible();
    await panel.getByRole('textbox', { name: 'Asunto' }).fill(ASUNTO);
    await panel.getByRole('textbox', { name: 'Cabecera' }).fill(CABECERA);
    await panel.getByRole('textbox', { name: 'Pie' }).fill(PIE);
    await expect(panel.getByText(`Asunto: ${ASUNTO}`)).toBeVisible();
    await expect(panel.frameLocator('iframe').getByText(CABECERA)).toBeVisible();
    await panel.getByRole('button', { name: 'Guardar' }).click();
    await expect(app.getByText('Plantilla guardada. Los próximos correos saldrán así.')).toBeVisible();
    await expect(filaInvitacion).toContainText('Personalizada');

    // 3. El resultado, con el panel ya cerrado.
    await paso(page, {
      titulo: 'Revisa la plantilla personalizada',
      explicacion:
        'Al guardar, el panel se cierra y la fila pasa a «Personalizada». Mientras escribías, la vista previa del correo se actualizaba en el mismo panel con datos de ejemplo. ' +
        'Para insertar un dato real en el texto, usa «Insertar variable»: por ejemplo {{empresa}} se reemplaza por el nombre de la empresa en cada correo. Los próximos correos de invitación saldrán con el asunto, la cabecera y el pie nuevos; los que ya se enviaron no cambian.',
      resaltar: filaInvitacion,
      avisos: true,
      variantes: [{ cuando: 'Dejas un campo vacío y guardas', entonces: 'Ese campo vuelve al texto de Validasoft y el correo sigue «Personalizada» si otro campo tiene texto.' }],
      configuracion: {
        ajuste: 'Asunto, cabecera y pie del correo',
        ruta: 'Configuración › Plantillas de correo (/plantillas)',
        efecto: 'Aplica a los correos que se envían después de guardar.',
      },
    });

    // 4. Restablecer el texto predeterminado. El diálogo se confirma sin pantallazo.
    await filaInvitacion.getByRole('button').click();
    await expect(panel.getByRole('button', { name: 'Restaurar predeterminado' })).toBeVisible();
    await panel.getByRole('button', { name: 'Restaurar predeterminado' }).click();
    const confirmacion = app.getByRole('dialog', { name: /¿Volver al correo predeterminado/ });
    await expect(confirmacion).toBeVisible();
    await confirmacion.getByRole('button', { name: 'Restaurar', exact: true }).click();
    await expect(app.getByText('El correo volvió al texto predeterminado.')).toBeVisible();
    await expect(filaInvitacion).toContainText('Predeterminada');

    await paso(page, {
      titulo: 'Restablece el texto predeterminado',
      explicacion:
        '«Restaurar predeterminado» abre una confirmación que quita el asunto, la cabecera y el pie de la plantilla; el texto vuelve al de Validasoft. Al confirmar, la fila vuelve a «Predeterminada» y el aviso confirma el cambio. ' +
        'Si cierras el panel con «Cancelar» o la «X», no se guarda ningún cambio pendiente.',
      resaltar: filaInvitacion,
      avisos: true,
      variantes: [{ cuando: 'El correo no está personalizado', entonces: 'No aparece «Restaurar predeterminado»: no hay nada que restablecer.' }],
    });
  },
);
