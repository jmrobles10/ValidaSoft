import { pdf } from '../../soporte/archivos';
import { esperarCorreo } from '../../soporte/correo';
import { abrirEmbebido } from '../../soporte/embebido';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

/** Fecha `YYYY-MM-DD` de hoy en Bogotá más `dias` (negativo: en el pasado). */
function fecha(dias: number): string {
  const hoy = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date());
  const [a, m, d] = hoy.split('-').map(Number) as [number, number, number];
  return new Date(Date.UTC(a, m - 1, d + dias)).toISOString().slice(0, 10);
}

recorrido(
  {
    dominio: 'documentos',
    actor: 'Administrador de empresa',
    titulo: 'Recuerda a un tercero un documento que vence',
    resumen:
      'El administrador de Constructora Sur ve en Vencimientos el certificado de un proveedor que vence en pocos días, selecciona la fila y le envía un recordatorio por correo para que suba una versión nueva a tiempo.',
    precondiciones: [
      'Un grupo con Constructora Sur, con el certificado de seguridad y salud en el trabajo como requisito del flujo de proveedor (30 días de vigencia desde la expedición, 10 de aviso y 5 de gracia).',
      'Un proveedor con el certificado expedido hace 25 días: vence en 5 días y ya está «Por vencer».',
    ],
    relacionados: ['documentos/vencimientos', 'documentos/ciclo-de-vigencia'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Metalúrgica Recordada S.A.S.'));
    await completarInvitacion(await (await browser.newContext()).newPage(), invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': fecha(-25) } },
    ]);

    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'vencimientos');
    await expect(app.getByRole('heading', { level: 1, name: 'Vencimientos' })).toBeVisible();
    const fila = app.getByRole('row').filter({ hasText: invitacion.nombre });
    await expect(fila).toContainText('Por vencer');
    await expect(fila).toContainText('Vence en 5 días');

    // 1. Selecciona el documento.
    await fila.getByRole('checkbox').check();
    await expect(app.getByRole('button', { name: 'Recordar al tercero (1)' })).toBeVisible();
    await paso(page, {
      titulo: 'Selecciona el documento que quieres recordar',
      explicacion:
        'Marca la casilla de la fila del documento que vence. Al marcarla, el botón «Recordar al tercero» cuenta cuántos documentos seleccionaste, y con ese botón se envía el recordatorio.',
      resaltar: [fila.getByRole('checkbox'), app.getByRole('button', { name: 'Recordar al tercero (1)' })],
    });

    // 2. Revisa el recordatorio antes de enviarlo.
    await app.getByRole('button', { name: 'Recordar al tercero (1)' }).click();
    const dialogo = app.getByRole('dialog');
    await expect(dialogo.getByRole('button', { name: 'Enviar recordatorio' })).toBeVisible();
    await paso(page, {
      titulo: 'Confirma el recordatorio',
      explicacion:
        'El diálogo dice a cuántos terceros les llega un correo y recuerda que a cada tercero se le recuerda a lo sumo una vez por día: lo que ya se recordó hoy se omite. El correo lleva el nombre del documento y la fecha en que vence para tu empresa, para que suba una versión nueva antes de esa fecha. ' +
        'Nada se envía hasta que pulses «Enviar recordatorio».',
      resaltar: dialogo,
    });

    // 3. Se envía y queda registrado.
    await dialogo.getByRole('button', { name: 'Enviar recordatorio' }).click();
    await expect(app.getByText(/^Enviaste 1 recordatorio a/)).toBeVisible();
    const correo = await esperarCorreo(invitacion.correo, /vencer/);
    expect(correo.texto).toContain(`${SST.nombre}: vence para ${e.sur.nombre} el`);
    await paso(page, {
      titulo: 'Verifica que el recordatorio salió',
      explicacion:
        'Aparece el aviso «Enviaste 1 recordatorio» con el destinatario, y el tercero recibe el correo con el documento y la fecha. El recordatorio solo avisa: no cambia el documento ni renueva nada; eso lo hace el tercero al subir una versión nueva.',
      resaltar: app.getByText(/^Enviaste 1 recordatorio a/),
      avisos: true,
    });
  },
);
