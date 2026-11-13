import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, observar } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero, responder, subirDocumento } from '../../soporte/proveedor';
import { crearEscenario, expedienteAbierto, invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'revision',
    actor: 'Revisor/área',
    titulo: 'Lee el historial de un expediente',
    resumen:
      'Un expediente pasó por una observación, una respuesta del proveedor y dos aprobaciones. Ana lo abre y lee el historial: quién hizo cada cosa, qué y cuándo, y la última respuesta del proveedor.',
    precondiciones: [
      'Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.',
      'Un proveedor que subió su RUT y su Cámara; Compras observó el RUT, el proveedor subió la versión 2 y respondió, y las dos áreas aprobaron.',
    ],
    relacionados: ['revision/observar-con-motivos', 'revision/responder-con-lo-corregido'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const proveedor = nuevoTercero('Cauchos Industriales S.A.S.');
    const invitacion = await invitar(e, e.norte, proveedor);
    const ventanaProveedor = await (await browser.newContext()).newPage();
    await completarInvitacion(ventanaProveedor, invitacion, [
      { nombre: RUT, archivo: pdf('RUT Cauchos v1') },
      { nombre: CAMARA, archivo: pdf('Camara Cauchos v1') },
    ]);
    const expediente = await expedienteAbierto(e, invitacion);

    // Compras observa el RUT; el proveedor corrige y responde; las dos áreas aprueban.
    const ruben = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ruben, e.norte, 'bandeja');
    await abrirDesdeBandeja(ruben, proveedor.nombre, 'Compras');
    await observar(ruben, 'Compras', [{ documento: `${RUT} · v1`, motivos: ['Ilegible'] }], 'La página 2 está cortada.');
    await esperarEstadoDelTercero(ventanaProveedor, 'Observado');
    await subirDocumento(ventanaProveedor, RUT, pdf('RUT Cauchos v2'), { version: 2 });
    await responder(ventanaProveedor, 'Adjunto el RUT completo.');
    await esperarEstadoDelTercero(ventanaProveedor, 'EnRevision');
    await abrirDesdeBandeja(ruben, proveedor.nombre, 'Compras');
    await aprobarArea(ruben, 'Compras');
    const olga = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.olga, e.norte, 'bandeja');
    await abrirDesdeBandeja(olga, proveedor.nombre, 'Cumplimiento');
    await aprobarArea(olga, 'Cumplimiento');
    await esperarEstadoDelTercero(ventanaProveedor, 'Aprobado');

    // 1) Ana abre el historial del expediente.
    const app = await abrirEmbebido(page, e, e.ana, e.norte, `expedientes/${expediente}`);
    await expect(app.getByRole('heading', { level: 1, name: proveedor.nombre })).toBeVisible();
    const historial = app.getByText('Historial', { exact: true }).first();
    await expect(historial).toBeVisible();
    await expect(app.getByText('Adjunto el RUT completo.').first()).toBeVisible();
    await paso(page, {
      titulo: 'Abre el historial del expediente',
      explicacion:
        'El historial es la línea de tiempo del expediente: cada observación, respuesta, aprobación y cambio de estado con quién lo hizo, qué fue y cuándo. ' +
        'Los veredictos de las áreas aparecen con su autor: «Tú» si fuiste tú, la persona del área, o un administrador cuando aprueba en nombre del área. Nada se borra: lo que se corrige queda como revertido o superado.',
      resaltar: historial,
      variantes: [
        { cuando: 'Un veredicto fue anulado o reemplazado', entonces: 'Sigue en el historial con su marca de «Anulado» o «Superado por el reenvío»; no desaparece.' },
      ],
    });

    // 2) La última respuesta del proveedor.
    await paso(page, {
      titulo: 'Revisa la última respuesta del proveedor',
      explicacion:
        'Cuando el proveedor responde a una observación, su respuesta aparece en «Última respuesta del tercero» con su texto y la fecha. ' +
        'Si el proveedor reenvió por otro canal y lo hizo el sistema integrado en su nombre, la respuesta lo indica.',
      resaltar: app.getByText('Última respuesta del tercero', { exact: true }).first(),
    });

    // Compras, la región del área, muestra la aprobación del área en el mismo expediente.
    await expect(app.getByText(/Todas las áreas aprobaron el /)).toBeVisible();
  },
);
