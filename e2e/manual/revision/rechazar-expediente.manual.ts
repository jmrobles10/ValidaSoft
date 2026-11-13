import { pdf } from '../../soporte/archivos';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja } from '../../soporte/empresa';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Oficial de cumplimiento',
    titulo: 'Rechaza un expediente por un hallazgo grave',
    resumen:
      'Olga, oficial de cumplimiento, encuentra que la Cámara de comercio no corresponde al proveedor. Como su área puede rechazar, cierra el expediente con motivos que el proveedor lee y una justificación interna que no ve.',
    precondiciones: [
      'Un grupo con Constructora Norte. Olga es miembro de Cumplimiento y su área tiene «Puede rechazar» en el flujo.',
      'Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.',
    ],
    relacionados: ['revision/aprobar-un-area', 'revision/observar-con-motivos'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Textiles Caribe S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf('RUT Textiles Caribe') },
      { nombre: CAMARA, archivo: pdf('Camara Textiles Caribe') },
    ]);

    // 1) El botón «Rechazar» aparece en el expediente solo para el área que puede rechazar.
    const app = await abrirEmbebido(page, e, e.olga, e.norte, 'bandeja');
    await abrirDesdeBandeja(app, proveedor.nombre, 'Cumplimiento');
    const rechazar = app.getByRole('button', { name: 'Rechazar', exact: true }).first();
    await expect(rechazar).toBeVisible();
    await paso(page, {
      titulo: 'Localiza la acción de rechazar',
      explicacion:
        'Tu área puede rechazar el expediente completo cuando encuentra un hallazgo grave. El botón «Rechazar» está en la barra de decisión del expediente. ' +
        'El rechazo es definitivo: el expediente se cierra y el vínculo vuelve a Invitado.',
      resaltar: rechazar,
      variantes: [
        { cuando: 'Tu área no tiene «Puede rechazar»', entonces: 'No ves el botón «Rechazar»; solo puedes aprobar u observar.' },
        { cuando: 'El expediente está devuelto al proveedor por una observación', entonces: 'Igual puedes rechazar: un hallazgo grave no espera a que el proveedor responda.' },
      ],
      configuracion: {
        ajuste: 'Puede rechazar (por área, en el flujo)',
        ruta: 'Configuración › Flujos (/flujos)',
        efecto: 'Solo las áreas marcadas con «Puede rechazar» ven el botón.',
      },
    });

    // 2) Motivos para el proveedor y justificación interna.
    await rechazar.click();
    const dialogo = app.getByRole('dialog', { name: /^Rechazar expediente/ });
    await dialogo.getByRole('checkbox', { name: /^No corresponde al tercero /, exact: false }).check();
    await dialogo.getByRole('textbox', { name: /Justificación/ }).fill('La Cámara es de otra sociedad con el mismo nombre.');
    await paso(page, {
      titulo: 'Elige los motivos y justifica el rechazo',
      explicacion:
        'Marca los motivos: el proveedor lee el texto de cada uno. La justificación es interna y no sale en ningún correo: queda en el historial con tu nombre. ' +
        'Antes de confirmar, la pantalla recuerda que el rechazo cierra el expediente.',
      resaltar: [dialogo.getByRole('checkbox', { name: /^No corresponde al tercero /, exact: false }), dialogo.getByRole('textbox', { name: /Justificación/ })],
    });

    // 3) Confirmar y ver el resultado.
    await dialogo.getByRole('button', { name: 'Rechazar expediente', exact: true }).click();
    await expect(dialogo).toBeHidden();
    await expect(app.getByText(/Cumplimiento lo rechazó el .*\. El vínculo volvió a Invitado\./)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el expediente rechazado',
      explicacion:
        'El expediente queda cerrado y aparece el aviso de que el vínculo volvió a Invitado. Ya no se pueden emitir más veredictos sobre él, y la justificación queda visible solo para tu empresa.',
      resaltar: app.getByText(/Cumplimiento lo rechazó el .*\. El vínculo volvió a Invitado\./),
    });

    // 4) Lo que recibe el proveedor.
    const correo = await esperarCorreo(proveedor.correo, /cerró tu validación .* sin aprobarla$/);
    expect(correo.texto).toContain('El documento no corresponde a la persona o empresa que se está validando.');
    await page.setContent(correo.html);
    await paso(page, {
      titulo: 'Así le llega el rechazo al proveedor',
      explicacion:
        'El proveedor recibe un correo que dice que la empresa cerró la revisión sin aprobarla, con los motivos en su texto. La justificación interna no aparece: el proveedor solo ve los motivos. ' +
        'Si tiene dudas, debe escribirle a la empresa.',
      resaltar: page.getByText('El documento no corresponde a la persona o empresa que se está validando.').first(),
      celular: false,
    });
  },
);
