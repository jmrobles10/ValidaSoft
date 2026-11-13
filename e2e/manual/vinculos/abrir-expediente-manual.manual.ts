import { abrirEmbebido } from '../../soporte/embebido';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

recorrido(
  {
    dominio: 'vinculos',
    actor: 'Administrador de empresa',
    titulo: 'Abre el expediente sin esperar a que el tercero acepte',
    resumen:
      'La administradora abre a mano el expediente de un proveedor invitado que todavía no ha aceptado la invitación, para que las áreas empiecen la validación sin esperar al tercero.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado como flujo por defecto.',
      'Un proveedor invitado que aún no ha aceptado la invitación.',
    ],
    relacionados: ['vinculos/detalle-del-vinculo', 'revision/aprobar-un-area'],
  },
  async ({ page }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    await invitar(e, e.sur, tercero, 'PROVEEDOR');

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'vinculos');
    await app.getByRole('row', { name: `Abrir vínculo de ${tercero.correo}` }).click();
    await expect(app.getByRole('heading', { level: 1 })).toBeVisible();
    const tarjeta = app.locator('section').filter({ has: app.getByRole('heading', { name: 'Expediente', exact: true }) });
    await expect(tarjeta.getByText('Sin expediente abierto.')).toBeVisible();

    await paso(page, {
      titulo: 'Encuentra la tarjeta Expediente del vínculo',
      explicacion:
        'Mientras el tercero no ha aceptado, la tarjeta «Expediente» dice «Sin expediente abierto» y ofrece el botón «Abrir expediente». ' +
        'Usa esa opción cuando necesitas empezar la validación ya, por ejemplo porque el tercero entregó sus documentos por otro canal.',
      resaltar: tarjeta,
      variantes: [
        { cuando: 'El tercero ya aceptó la invitación', entonces: 'La tarjeta muestra el expediente en curso y el botón «Ver expediente»; no hace falta abrirlo a mano.' },
        { cuando: 'El tipo de vínculo no tiene un flujo publicado', entonces: 'El diálogo dice que no hay flujos publicados y te manda a Configuración › Flujos aprobatorios.' },
      ],
    });

    await tarjeta.getByRole('button', { name: 'Abrir expediente' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Abrir expediente' });
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el flujo con el que se abre',
      explicacion:
        'El diálogo propone el flujo por defecto del tipo de vínculo, con sus etapas y áreas. Ese flujo define qué áreas revisarán los documentos del proveedor. ' +
        'Al confirmar, el vínculo pasa a «En validación» y las áreas ven el expediente en su bandeja, aunque el tercero todavía no haya aceptado la invitación.',
      resaltar: dialogo,
      configuracion: {
        ajuste: 'Flujo por defecto por tipo de vínculo',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Es el flujo que se propone al abrir el expediente a mano.',
      },
    });

    await dialogo.getByRole('button', { name: 'Abrir expediente', exact: true }).click();
    await expect(app.getByText(/Expediente .* abierto./).first()).toBeVisible();
    await expect(app.getByRole('heading', { name: 'Datos del expediente' })).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba que el expediente quedó abierto',
      explicacion:
        'Validasoft confirma con el folio del expediente y te lleva a su pantalla, con «Datos del expediente» y el estado de cada área. ' +
        'La invitación sigue pendiente: cuando el tercero acepte, el mismo expediente continúa y no se vuelve a empezar.',
      resaltar: app.getByRole('heading', { name: 'Datos del expediente' }),
      avisos: true,
    });
  },
);
