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
    titulo: 'Abre un documento para verlo y descargarlo',
    resumen:
      'Olga, de Cumplimiento, abre el RUT del proveedor en el visor del expediente para leerlo, revisa su versión y su vigencia, y ve dónde se descarga. Cada vez que se abre o se descarga un documento queda registrado.',
    precondiciones: [
      'Un grupo con Constructora Norte, con el área Cumplimiento (Olga es miembro).',
      'Un proveedor invitado por Norte que ya subió su RUT y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/historial-del-expediente', 'revision/aprobar-documento-por-documento'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Papelería Central S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Papeleria Central') },
      { nombre: CAMARA, archivo: pdf('Camara Papeleria Central') },
    ]);

    // 1) El documento en el expediente, con «Ver o descargar».
    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Compras');
    const ver = regionDeArea(app, 'Compras').getByRole('button', { name: /^Ver/ }).first();
    await expect(ver).toBeVisible();
    await paso(page, {
      titulo: 'Localiza el documento que quieres ver',
      explicacion:
        'En «Documentos que pide el flujo», junto a cada documento de tu área está el botón «Ver», que abre la versión exacta que el proveedor subió. ' +
        'Revisa el documento antes de decidir sobre él.',
      resaltar: ver,
    });

    // 2) El visor muestra el archivo con su versión y sus opciones.
    await ver.click();
    const visor = app.getByRole('dialog', { name: new RegExp(`^${RUT} · `) });
    await expect(visor).toBeVisible();
    await expect(visor.getByRole('button', { name: 'Descargar', exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Abre el documento en el visor',
      explicacion:
        'El visor muestra el archivo dentro de la pantalla con el título del documento, su número de versión y su fecha de carga. El botón «Descargar» guarda una copia en tu equipo. ' +
        'Cada vez que alguien abre o descarga un documento, la acción queda registrada en la auditoría del expediente.',
      resaltar: [visor.getByRole('heading', { level: 2 }), visor.getByRole('button', { name: 'Descargar', exact: true })],
      variantes: [
        {
          cuando: 'El documento es un formulario sin archivo',
          entonces: 'No hay archivo para ver ni descargar: la versión es un formulario y sus datos se ven en el expediente.',
        },
      ],
    });
    await visor.getByRole('button', { name: 'Cerrar' }).click();
    await expect(visor).toBeHidden();
  },
);
