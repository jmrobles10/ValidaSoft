import { expect, paso, recorrido } from '../nucleo';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { terceroEnRevision } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Revisor/área',
    titulo: 'Revisa un expediente desde el celular',
    resumen:
      'Un revisor de área abre la app de la empresa desde el celular: ve su bandeja de revisión, abre el expediente de un tercero y encuentra el detalle con sus documentos y las decisiones de cada área.',
    precondiciones: [
      'Un grupo con Constructora Norte, cuya área Compras tiene a Rubén como revisor.',
      'Un tercero que entregó sus documentos y quedó «En revisión» con Compras, por lo que aparece en la bandeja de Rubén.',
    ],
    relacionados: ['revision/aprobar-un-area'],
  },
  async ({ browser }) => {
    const { e, tercero } = await terceroEnRevision(await (await browser.newContext()).newPage(), 'Aceros del Norte S.A.S.');
    const host = await (await browser.newContext()).newPage();

    // 1. La bandeja.
    const app = await abrirEmbebido(host, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await paso(host, {
      titulo: 'Abre tu bandeja de revisión',
      explicacion:
        'La bandeja lista los expedientes que esperan la decisión de tu área, con el tercero, el tipo de vínculo y el estado. Solo ves los de las áreas de las que eres miembro. ' +
        'En el celular, la app ocupa toda la pantalla y las demás secciones están en el menú «Más».',
      resaltar: app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' }),
      celular: true,
      variantes: [
        { cuando: 'Eres administrador y no miembro del área', entonces: 'Ves también las acciones de administrador y puedes decidir en nombre del área.' },
      ],
    });

    // 2. El expediente.
    await abrirDesdeBandeja(app, tercero.nombre, 'Compras');
    await expect(app.getByRole('heading', { level: 1, name: tercero.nombre })).toBeVisible();
    await paso(host, {
      titulo: 'Abre el expediente del tercero',
      explicacion:
        'El expediente muestra cada área y su decisión, los documentos con su versión y lo que ya decidieron las demás áreas, para que decidas con todo a la vista.',
      resaltar: app.getByRole('heading', { level: 1, name: tercero.nombre }),
      celular: true,
    });
  },
);
