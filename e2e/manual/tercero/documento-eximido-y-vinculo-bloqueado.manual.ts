import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { aceptarInvitacion, entrarPorInvitacion, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Ve un documento eximido y un vínculo suspendido',
    resumen:
      'La empresa puede eximirte de un documento con un motivo: ya no te lo pide y tu expediente sigue sin él. Si suspende tu validación, no puedes enviar documentos ni responder, y la pantalla no te muestra el motivo interno.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Norte», su flujo de Proveedor (RUT y Cámara de comercio) y la administradora del grupo.',
      'Un tercero que aceptó la invitación y subió el RUT, con la Cámara de comercio pendiente que la empresa decide eximir.',
    ],
    relacionados: ['tercero/subir-version-nueva', 'tercero/vinculo-en-revision-por-actualizacion'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const nombre = 'Ferretería Eximida S.A.S.';
    const invitacion = await invitar(e, e.norte, nuevoTercero(nombre));
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await subirDocumento(page, RUT, pdf(`RUT ${nombre}`));
    await expect(page.getByRole('main').getByText(/^Te falta 1 documento\. Súbelo/)).toBeVisible();

    // La administradora exime la Cámara con su motivo.
    const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, `expedientes/${await expedienteAbierto(e, invitacion)}`);
    const documento = ana.getByRole('listitem').filter({ has: ana.getByRole('heading', { level: 4, name: CAMARA, exact: true }) }).first();
    await documento.getByRole('button', { name: 'Eximir', exact: true }).click();
    const dialogo = ana.getByRole('dialog', { name: `Eximir ${CAMARA}` });
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('Es una persona jurídica sin Cámara de comercio: está inscrita en la Cámara de otro país.');
    await dialogo.getByRole('button', { name: 'Eximir documento' }).click();
    await expect(ana.getByText(`${CAMARA} eximido.`)).toBeVisible();

    // 1. El tercero ve la Cámara eximida.
    await page.goto('/empresas');
    await page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) }).getByRole('link').first().click();
    await expect(tarjeta(page, CAMARA)).toContainText('Eximido');
    await paso(page, {
      titulo: 'Revisa el documento que te eximieron',
      explicacion:
        'En tu revisión, la Cámara de comercio aparece como «Eximido». La empresa ya no te la pide para este expediente, así que no tienes que subirla. ' +
        'El RUT sigue exigido y se revisa como de costumbre.',
      resaltar: tarjeta(page, CAMARA),
    });

    // 2. Con el vínculo suspendido, la pantalla dice que no puedes enviar y no muestra el motivo.
    const anaVinculo = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.norte, `vinculos/${invitacion.vinculoId}`);
    await anaVinculo.getByRole('button', { name: 'Más acciones' }).click();
    await anaVinculo.getByRole('menuitem', { name: /^Bloquear/ }).click();
    const bloquear = anaVinculo.getByRole('dialog', { name: /^Bloquear vínculo/ });
    await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Revisión de sanciones.');
    await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
    await expect(bloquear).toBeHidden();

    await page.goto('/empresas');
    await page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.norte.nombre, exact: true }) }).getByRole('link').first().click();
    await expect(page.getByRole('main')).toContainText(`${e.norte.nombre} suspendió temporalmente esta validación`);
    await expect(page.getByRole('main')).not.toContainText('Revisión de sanciones.');
    await paso(page, {
      titulo: 'Entiende lo que significa un vínculo suspendido',
      explicacion:
        'Si la empresa suspende la validación, la pantalla dice que se suspendió temporalmente y que por ahora no puedes aceptar, enviar documentos ni responder. ' +
        'No ves el motivo interno de la empresa: solo sabes que te avisarán por correo cuando la reanude.',
      resaltar: page.getByRole('main').getByText(`${e.norte.nombre} suspendió temporalmente esta validación`),
    });
  },
);
