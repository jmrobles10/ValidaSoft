import { pdf } from '../../soporte/archivos';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, regionDeArea } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Observa un documento con motivos',
    resumen:
      'Rubén revisa el RUT del proveedor y encuentra dos fallas. Lo observa marcando el documento y los motivos tipificados, agrega una nota y el proveedor recibe todo en un solo correo, sin ver quién revisó.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Compras y Rubén como miembro. Los motivos de observación del grupo están configurados.',
      'Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/responder-con-lo-corregido', 'revision/aprobar-documento-por-documento', 'revision/rechazar-expediente'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Aceros del Norte S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Aceros del Norte') },
      { nombre: CAMARA, archivo: pdf('Camara Aceros del Norte') },
    ]);

    // 1) Abrir el diálogo «Observar» del área.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Compras');
    await regionDeArea(app, 'Compras').getByRole('button', { name: 'Observar', exact: true }).click();
    const dialogo = app.getByRole('dialog', { name: /^Observar en Compras/ });
    await dialogo.getByRole('checkbox', { name: `${RUT} · v1`, exact: true }).check();
    await dialogo.getByRole('group', { name: 'Motivos' }).nth(0).getByRole('checkbox', { name: /^Ilegible / }).check();
    await dialogo.getByRole('group', { name: 'Motivos' }).nth(0).getByRole('checkbox', { name: /^Incompleto / }).check();
    await dialogo.getByRole('textbox', { name: 'Nota para el tercero (opcional)' }).fill('La página 2 está cortada.');
    await paso(page, {
      titulo: 'Marca el documento y los motivos',
      explicacion:
        'Marca cada documento que quieres observar y, debajo de él, los motivos que aplican. Los motivos son textos fijos que configura la empresa; el proveedor los lee tal cual, y nunca ve tu nombre ni el del área. ' +
        'La nota es opcional y también la lee el proveedor. Después, el botón dice cuántos documentos observas.',
      resaltar: [dialogo.getByRole('checkbox', { name: `${RUT} · v1`, exact: true }), dialogo.getByRole('group', { name: 'Motivos' }).nth(0)],
      variantes: [
        { cuando: 'La política del flujo es «Se devuelve de inmediato»', entonces: 'La observación devuelve el expediente al proveedor en ese momento y frena a las demás áreas hasta que responda.' },
        { cuando: 'La política es «Se devuelve cuando la etapa se pronuncia»', entonces: 'El proveedor recibe la observación cuando todas las áreas de la etapa se hayan pronunciado.' },
      ],
      configuracion: {
        ajuste: 'Si un área observa',
        ruta: 'Configuración › Flujos (/flujos)',
        efecto: 'Decide cuándo se devuelve la observación al proveedor.',
      },
    });

    // 2) Enviar la observación.
    await dialogo.getByRole('button', { name: 'Observar 1 documento', exact: true }).click();
    await expect(app.getByText(/^Observaste 1 documento; el tercero recibe los motivos en un solo correo\.$/)).toBeVisible();
    await expect(app.getByText(/Una observación lo devolvió al tercero/)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cómo queda el expediente',
      explicacion:
        'El expediente queda «Devuelto al tercero»: nadie emite veredictos hasta que el proveedor responda. Al reenviar, solo vuelve a revisar tu área, que es la que observó. ' +
        'Las demás áreas no pueden aprobar ni observar en ese momento.',
      resaltar: app.getByText(/Una observación lo devolvió al tercero/),
    });

    // 3) Lo que recibe el proveedor: un solo correo con los motivos.
    const correo = await esperarCorreo(proveedor.correo, /tiene observaciones sobre tu validación/);
    await page.setContent(correo.html);
    await paso(page, {
      titulo: 'Así le llegan las observaciones al proveedor',
      explicacion:
        'El proveedor recibe un solo correo con los motivos de todos los documentos observados y la nota que escribiste. El correo no menciona a los revisores ni a las áreas. ' +
        'Desde el enlace del correo ve qué corregir y puede responder con una versión nueva del documento.',
      resaltar: page.getByText('El documento no se puede leer con claridad.').first(),
      celular: false,
    });
  },
);
