import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { aceptarInvitacion, entrarPorInvitacion, esperarEstadoDelTercero, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { expedienteAbierto, invitar, nuevoTercero, crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Administrador de empresa',
    titulo: 'Exime a un tercero de un documento',
    resumen:
      'Ferretería Eximida es una persona jurídica sin Cámara de comercio, porque está inscrita en otro país. Ana, administradora, exime ese documento con un motivo: el expediente deja de pedirlo, las áreas no lo revisan y el proveedor lo ve como eximido.',
    precondiciones: [
      'Un grupo con Constructora Norte, con la Cámara de comercio pedida en el flujo de proveedores. Ana es administradora.',
      'Un proveedor invitado por Norte que ya subió su RUT y todavía le falta la Cámara de comercio.',
    ],
    relacionados: ['revision/aprobar-un-area', 'revision/historial-del-expediente'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const nombre = 'Ferretería Eximida S.A.S.';
    const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
    // El proveedor sube el RUT; la Cámara no la tiene.
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await subirDocumento(page, RUT, pdf(`RUT ${nombre}`));
    await expect(page.getByRole('main').getByText(/^Te falta 1 documento\. Súbelo/)).toBeVisible();

    // 1) Ana abre el expediente y ve el documento que falta.
    const paginaAna = await (await browser.newContext()).newPage();
    const ana = await abrirEmbebido(paginaAna, e, e.ana, e.norte, `expedientes/${await expedienteAbierto(e, invitacion)}`);
    await expect(ana.getByRole('heading', { level: 1, name: nombre })).toBeVisible();
    const documento = ana.getByRole('listitem').filter({ has: ana.getByRole('heading', { level: 4, name: CAMARA, exact: true }) }).first();
    await expect(documento.getByRole('button', { name: 'Eximir', exact: true })).toBeVisible();
    await paso(paginaAna, {
      titulo: 'Localiza el documento que no aplica',
      explicacion:
        'En «Documentos que pide el flujo» aparece cada documento con su estado. El de Cámara de comercio no tiene versión porque el proveedor no lo tiene. ' +
        'Con el botón «Eximir» quien administra la empresa puede quitar ese requisito para este expediente, con un motivo.',
      resaltar: documento,
      configuracion: {
        ajuste: 'Documento obligatorio del flujo',
        efecto: 'Un documento obligatorio bloquea la revisión hasta que el proveedor lo suba o se exima con motivo.',
      },
    });

    // 2) La exención pide un motivo.
    await documento.getByRole('button', { name: 'Eximir', exact: true }).click();
    const dialogo = ana.getByRole('dialog', { name: `Eximir ${CAMARA}` });
    await expect(dialogo.getByRole('button', { name: 'Eximir documento' })).toBeDisabled();
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Es una persona jurídica sin Cámara de comercio: está inscrita en la Cámara de otro país.');
    await paso(paginaAna, {
      titulo: 'Escribe el motivo de la exención',
      explicacion:
        'El diálogo explica que el documento deja de exigirse en este expediente y que sus veredictos dejan de contar. Sin justificación no se puede eximir. ' +
        'La justificación queda en el historial del expediente con tu nombre.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Eximir documento' }).click();

    // 3) El documento queda eximido y el expediente sigue en revisión.
    await expect(ana.getByText(`${CAMARA} eximido.`)).toBeVisible();
    await expect(documento).toContainText('Eximido por Ana Administradora');
    await expect(documento.getByRole('button', { name: 'Eximir', exact: true })).toHaveCount(0);
    await paso(paginaAna, {
      titulo: 'Revisa el documento eximido',
      explicacion:
        'El documento muestra «Eximido por» con tu nombre, la fecha y el motivo que escribiste. Ya no tiene botón de eximir y el expediente sigue su curso. ' +
        'Las áreas no lo revisan: solo se les pide lo que falta por decidir.',
      resaltar: documento,
    });

    // 4) El proveedor lo ve eximido y no se le vuelve a pedir.
    await esperarEstadoDelTercero(page, 'EnRevision');
    await expect(tarjeta(page, CAMARA)).toContainText('Eximido');
    await expect(tarjeta(page, CAMARA).getByRole('link', { name: /^Subir/ })).toHaveCount(0);
    await paso(page, {
      titulo: 'Así lo ve el proveedor',
      explicacion:
        'El proveedor ve la Cámara de comercio como eximida: ya no le piden subirla y su expediente queda en revisión con lo que sí se exige. ' +
        'El motivo de la exención es interno y no se le muestra.',
      resaltar: tarjeta(page, CAMARA),
    });
  },
);

