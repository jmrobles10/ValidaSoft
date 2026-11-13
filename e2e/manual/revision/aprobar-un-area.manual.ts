import { pdf } from '../../soporte/archivos';
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
    titulo: 'Aprueba el área de Compras',
    resumen:
      'Rubén abre el expediente de un proveedor que llegó a su bandeja, revisa los documentos de su área y aprueba el área Compras. El vínculo solo se activa cuando todas las áreas aprueban.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento. Rubén es miembro de Compras y Olga de Cumplimiento.',
      'Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/inicio-lo-que-te-toca', 'revision/aprobar-documento-por-documento', 'revision/observar-con-motivos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Aceros del Norte S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Aceros del Norte') },
      { nombre: CAMARA, archivo: pdf('Camara Aceros del Norte') },
    ]);

    // 1) Abrir el expediente desde la bandeja de Rubén.
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Compras');
    await expect(regionDeArea(app, 'Compras')).toBeVisible();
    await paso(page, {
      titulo: 'Abre el expediente del proveedor',
      explicacion:
        'El expediente muestra el tercero y, en «Decisión de cada área», una sección por área. Tu área, Compras, tiene el botón «Aprobar área» con los documentos que le quedan por decidir. ' +
        'Las demás áreas aparecen también, pero sin botones de decisión si no eres miembro de ellas.',
      resaltar: [app.getByRole('heading', { level: 1, name: proveedor.nombre }), regionDeArea(app, 'Compras')],
      variantes: [
        { cuando: 'Eres miembro de otra área', entonces: 'Esa área muestra «No eres miembro de esta área.» y no tiene botones de decisión.' },
        { cuando: 'Eres administrador y no eres miembro del área', entonces: 'Puedes aprobar en nombre del área con su menú de administrador; el veredicto queda registrado a tu nombre.' },
      ],
    });

    // 2) Revisar los documentos del área y aprobarla.
    await regionDeArea(app, 'Compras').getByRole('button', { name: /^Aprobar área/ }).click();
    const dialogo = app.getByRole('dialog', { name: /^Aprobar Compras/ });
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Confirma la aprobación del área',
      explicacion:
        'La confirmación dice cuántos documentos aprueba tu área. Se aprueban en la versión que estás viendo: si el tercero sube una versión nueva después, esa versión vuelve a revisarse. ' +
        'Al confirmar, tu veredicto queda registrado con tu nombre y la fecha.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: /^Aprobar/ }).last().click();
    await expect(dialogo).toBeHidden();
    await expect(regionDeArea(app, 'Compras').filter({ hasText: 'Aprobada' })).toBeVisible();

    // 3) El resultado: Compras queda aprobada y el vínculo espera a Cumplimiento.
    await paso(page, {
      titulo: 'Verifica el estado de las demás áreas',
      explicacion:
        'Tu área queda como «Aprobada». El tercero sigue «En revisión» mientras falte alguna área: el vínculo se activa solo cuando todas aprueban. ' +
        'Cumplimiento sigue pendiente y decide por su cuenta.',
      resaltar: [regionDeArea(app, 'Compras'), regionDeArea(app, 'Cumplimiento')],
    });
  },
);
