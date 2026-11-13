import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { regionDeArea } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Administrador de empresa',
    titulo: 'Aprueba un área en nombre de sus miembros',
    resumen:
      'Ana, administradora de la empresa, no es miembro de Compras y Rubén está de vacaciones. Abre el expediente, usa la acción de administrador para aprobar el área en nombre de Compras y deja escrita la razón.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras (miembro: Rubén) y Ana como administradora que no pertenece a ningún área.',
      'Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión y Compras no ha decidido.',
    ],
    relacionados: ['revision/aprobar-un-area', 'revision/desaprobar-y-reabrir-una-etapa'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Cerámicas Andinas S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Ceramicas Andinas') },
      { nombre: CAMARA, archivo: pdf('Camara Ceramicas Andinas') },
    ]);
    const expediente = await expedienteAbierto(e, invitacion);

    // 1) Ana entra directo al expediente: no está en ninguna bandeja de área.
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: proveedor.nombre })).toBeVisible();
    await expect(regionDeArea(app, 'Compras').getByRole('button', { name: /^Aprobar área/ })).toHaveCount(0);
    const menu = app.getByRole('button', { name: 'Acciones de administrador en Compras' });
    await paso(page, {
      titulo: 'Abre las acciones de administrador del área',
      explicacion:
        'Como administradora no eres miembro de Compras, así que no tienes el botón «Aprobar área». En la sección de Compras aparece el botón de acciones de administrador, con la opción de aprobar en nombre del área. ' +
        'Tu cuenta de administradora no es miembro de ningún área: lo que puedes hacer en un área es lo que te da el menú de administrador.',
      resaltar: [regionDeArea(app, 'Compras'), menu],
      variantes: [
        { cuando: 'Eres miembro del área', entonces: 'Decides con el botón «Aprobar área» como cualquier miembro; el menú de administrador no te cambia nada.' },
      ],
    });

    // 2) La opción pide una justificación y deja registro.
    await menu.click();
    await app.getByRole('menuitem', { name: 'Aprobar en nombre del área' }).click();
    const dialogo = app.getByRole('dialog', { name: /^Aprobar en nombre de Compras/ });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Rubén está de vacaciones; el RUT ya lo validó por correo.');
    await paso(page, {
      titulo: 'Escribe la justificación',
      explicacion:
        'El diálogo explica que la aprobación queda en el historial con tu nombre y la justificación que escribas. La justificación es obligatoria: sin ella no se puede aprobar. ' +
        'Úsala para decir por qué el área no decide por sí misma; quien revise el historial después verá esa razón.',
      resaltar: dialogo.getByRole('textbox', { name: /Justificación/ }),
    });

    // 3) Confirmar: el área queda aprobada y el veredicto lleva tu nombre.
    await dialogo.getByRole('button', { name: 'Aprobar en nombre del área', exact: true }).click();
    await expect(dialogo).toBeHidden();
    await expect(regionDeArea(app, 'Compras').filter({ hasText: 'Aprobada' })).toBeVisible();
    await expect(app.getByText(/aprobó en nombre del área/).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el veredicto registrado',
      explicacion:
        'El área Compras queda «Aprobada» y su historial muestra que el veredicto fue en nombre del área, con tu nombre. Cumplimiento sigue decidiendo por su cuenta: aprobar en nombre de un área no cierra las demás. ' +
        'Si fue un error, el veredicto se puede anular desde el mismo historial.',
      resaltar: [regionDeArea(app, 'Compras'), app.getByRole('region', { name: 'Cumplimiento', exact: true }).first()],
    });
  },
);
