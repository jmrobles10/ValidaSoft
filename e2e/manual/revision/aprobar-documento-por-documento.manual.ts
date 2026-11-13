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
    titulo: 'Aprueba el expediente documento por documento',
    resumen:
      'Olga, de Cumplimiento, no aprueba el área de una sola vez: decide sobre cada documento que pide el flujo. Activa la decisión por documento, aprueba el RUT y la Cámara de comercio por separado, y el área se cierra al decidir el último.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento. Olga es miembro de Cumplimiento.',
      'Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/aprobar-un-area', 'revision/observar-con-motivos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Plásticos Bogotá S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Plasticos Bogota') },
      { nombre: CAMARA, archivo: pdf('Camara Plasticos Bogota') },
    ]);

    // 1) Abrir el expediente y pasar la decisión a «por documento».
    const app = await abrirEmbebido(page, e, e.olga, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Cumplimiento');
    await app.getByRole('button', { name: 'Decidir por documento', exact: true }).click();
    await expect(app.getByRole('button', { name: 'Ocultar decisión por documento', exact: true })).toBeVisible();
    await expect(app.getByRole('button', { name: `Aprobar u observar ${RUT} por Cumplimiento` })).toBeVisible();
    await paso(page, {
      titulo: 'Activa la decisión por documento',
      explicacion:
        'En «Documentos que pide el flujo» el botón «Decidir por documento» muestra un botón «Aprobar u observar» junto a cada documento que tu área revisa. ' +
        'Así decides documento por documento en vez de aprobar el área completa de una vez. Vuelve a tocarlo para ocultar esos botones.',
      resaltar: app.getByRole('button', { name: 'Ocultar decisión por documento', exact: true }),
      variantes: [
        {
          cuando: 'El tercero sube una versión nueva de un documento ya decidido',
          entonces: 'Tu decisión anterior queda como «sin efecto» sobre la versión vieja y el documento vuelve a estar por decidir.',
        },
      ],
    });

    // 2) Aprobar el RUT.
    await app.getByRole('button', { name: `Aprobar u observar ${RUT} por Cumplimiento` }).click();
    const dialogoRut = app.getByRole('dialog', { name: `${RUT} · Cumplimiento` });
    await expect(dialogoRut).toBeVisible();
    await paso(page, {
      titulo: 'Aprueba el RUT',
      explicacion:
        'El diálogo muestra el documento y la versión que vas a decidir (la versión 1). Si el tercero sube otra versión antes de que confirmes, la pantalla te avisa y tu decisión no se aplica a la nueva. ' +
        'Aprobar deja registrado tu veredicto sobre este documento, con tu nombre y la versión.',
      resaltar: dialogoRut,
    });
    await dialogoRut.getByRole('button', { name: 'Aprobar', exact: true }).click();
    await expect(dialogoRut).toBeHidden();
    await expect(app.getByText(/· aprobó · v1/).first()).toBeVisible();

    // 3) Aprobar la Cámara: al decidir el último documento el área queda aprobada.
    await app.getByRole('button', { name: `Aprobar u observar ${CAMARA} por Cumplimiento` }).click();
    const dialogoCamara = app.getByRole('dialog', { name: `${CAMARA} · Cumplimiento` });
    await dialogoCamara.getByRole('button', { name: 'Aprobar', exact: true }).click();
    await expect(dialogoCamara).toBeHidden();
    await expect(regionDeArea(app, 'Cumplimiento').filter({ hasText: 'Aprobada' })).toBeVisible();
    await paso(page, {
      titulo: 'Cierra el área al decidir el último documento',
      explicacion:
        'Cuando apruebas el último documento que tu área revisa, el área pasa a «Aprobada» sin un paso adicional. Los documentos que ya decidiste quedan con tu nombre en su historial. ' +
        'Si ya no hay documentos por decidir, el botón de aprobar área ya no aparece.',
      resaltar: regionDeArea(app, 'Cumplimiento'),
    });
  },
);
