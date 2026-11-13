import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RUT = 'Registro Único Tributario';

function haceDias(dias: number): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - dias * 86_400_000));
}

recorrido(
  {
    dominio: 'informes',
    actor: 'Administrador de empresa',
    titulo: 'Bloquea, desbloquea o revoca un certificado',
    resumen:
      'La administradora de Constructora Sur suspende el certificado de un proveedor mientras revisa una coincidencia, lo vuelve a activar y, finalmente, revoca otro certificado. En cada caso, quien verifica el certificado ve su estado actual sin el motivo interno.',
    precondiciones: [
      'Un grupo con Constructora Sur y el área Revisión con Sofía como miembro; el RUT y el certificado de seguridad y salud en el trabajo son requisitos del flujo de proveedor.',
      'Un proveedor aprobado con su certificado vigente, y la página de verificación del certificado abierta como lo haría cualquier persona.',
    ],
    relacionados: ['informes/certificados', 'publico/verificar-certificado-autentico', 'publico/certificado-en-otros-estados'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Andamios Seguros S.A.S.'));
    const pTercero = await (await browser.newContext()).newPage();
    await completarInvitacion(pTercero, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': haceDias(2) } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(pTercero, 'Aprobado');

    const app = await abrirEmbebido(page, e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(app.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    const enlace = app.getByRole('link', { name: 'Página de verificación' });
    await expect(enlace).toBeVisible();
    const url = new URL((await enlace.getAttribute('href')) ?? '');
    const ruta = `${url.pathname}${url.search}`;
    const publica = await (await browser.newContext()).newPage();

    // 1. Bloquear el vínculo: el certificado se suspende.
    await app.getByRole('button', { name: 'Más acciones' }).click();
    await app.getByRole('menuitem', { name: /^Bloquear/ }).click();
    const bloquear = app.getByRole('dialog', { name: /^Bloquear vínculo/ });
    await expect(bloquear.getByRole('textbox', { name: 'Motivo' })).toBeVisible();
    await paso(page, {
      titulo: 'Bloquea el vínculo mientras revisas',
      explicacion:
        'Bloquear un vínculo suspende su certificado: mientras esté bloqueado, quien lo verifica ve que está suspendido. El «Motivo» que escribes es interno: el tercero y quien verifica no lo ven. ' +
        'Úsalo para una coincidencia que debes revisar, no como sanción.',
      resaltar: bloquear,
      configuracion: {
        ajuste: 'Bloqueo del vínculo',
        ruta: 'Vínculos › detalle del vínculo › Más acciones',
        efecto: 'Un vínculo bloqueado no recibe versiones nuevas y su certificado queda suspendido hasta que lo desbloquees.',
      },
    });
    await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Coincidencia en lista restrictiva por revisar.');
    await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();
    await expect(bloquear).toBeHidden();

    // 2. Lo que ve quien verifica: suspendido.
    await publica.goto(ruta);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está suspendido' })).toBeVisible();
    await expect(publica.getByRole('main')).not.toContainText('Coincidencia en lista restrictiva');
    await paso(publica, {
      titulo: 'Mira lo que ve quien verifica el certificado',
      explicacion:
        'La página de verificación dice «Este certificado está suspendido» y que el PDF solo se entrega mientras el certificado esté vigente. El motivo del bloqueo no aparece en ninguna parte: es interno. ' +
        'El tercero recibe un correo que dice que suspendiste su certificado, sin el motivo.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado está suspendido' }),
    });

    // 3. Desbloquear: el certificado vuelve a vigente.
    await app.getByRole('button', { name: 'Desbloquear', exact: true }).click();
    const desbloquear = app.getByRole('dialog');
    await expect(desbloquear.getByRole('button', { name: /^Desbloquear/ })).toBeVisible();
    await paso(page, {
      titulo: 'Desbloquea el vínculo cuando se descarte la coincidencia',
      explicacion:
        'Al desbloquear, el certificado vuelve a quedar vigente. Escribe brevemente por qué se levanta el bloqueo; esa explicación es interna y no la ve quien verifica.',
      resaltar: desbloquear,
    });
    await desbloquear.getByRole('textbox').first().fill('Se descartó la coincidencia.');
    await desbloquear.getByRole('button', { name: /^Desbloquear/ }).click();
    await expect(desbloquear).toBeHidden();
    await publica.goto(ruta);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' })).toBeVisible();
    await paso(publica, {
      titulo: 'Confirma que el certificado vuelve a estar vigente',
      explicacion:
        'Quien verifica ya ve «Este certificado está vigente», y el PDF vuelve a estar disponible para descargarlo. El tercero no tiene que hacer nada para que se restablezca.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado está vigente' }),
    });

    // 4. Revocar: deja de valer para siempre.
    await app.getByRole('button', { name: 'Revocar certificado' }).click();
    const revocar = app.getByRole('dialog', { name: /^Revocar el certificado/ });
    await paso(page, {
      titulo: 'Revoca el certificado',
      explicacion:
        'Revocar el certificado es definitivo: deja de ser válido para quien lo verifique. Debes escribir la «Justificación», que es interna y no se le muestra a quien verifica. ' +
        'El tercero recibe un correo que dice que revocaste su certificado, sin la justificación.',
      resaltar: revocar,
    });
    await revocar.getByRole('textbox', { name: 'Justificación' }).fill('Se aprobó con un documento adulterado.');
    await revocar.getByRole('button', { name: 'Revocar certificado' }).click();
    await expect(revocar).toBeHidden();
    await publica.goto(ruta);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado fue revocado' })).toBeVisible();
    await expect(publica.getByRole('main')).not.toContainText('adulterado');
    await paso(publica, {
      titulo: 'Mira el certificado revocado',
      explicacion:
        'La verificación dice «Este certificado fue revocado» y la fecha en que dejó de valer. Ni la justificación ni el PDF aparecen: sin vigencia no se entrega el documento. ' +
        'Para saber el motivo, quien verifica debe preguntarle a la empresa.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado fue revocado' }),
    });
  },
);
