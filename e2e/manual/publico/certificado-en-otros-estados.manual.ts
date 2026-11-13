import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { completarInvitacion, esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero, SST } from '../../soporte/semilla';

const RUT = 'Registro Único Tributario';

recorrido(
  {
    dominio: 'publico',
    actor: 'Público',
    titulo: 'Lee el estado de un certificado suspendido o revocado',
    resumen:
      'Cuando la empresa suspende el vínculo o revoca el certificado, el enlace de verificación lo dice con un título propio y su consecuencia: quien lo consulta no debe aceptarlo, no se entrega el PDF y la página no revela el motivo interno.',
    precondiciones: [
      'Un proveedor aprobado por Constructora Sur con su certificado vigente.',
      'La administradora de Constructora Sur bloquea el vínculo y después revoca el certificado.',
    ],
    relacionados: ['publico/verificar-certificado-autentico', 'vinculos/bloquear-y-desbloquear'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    const invitacion = await invitar(e, e.sur, nuevoTercero('Andamios Seguros S.A.S.'));
    const hace2Dias = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' }).format(new Date(Date.now() - 2 * 86_400_000));
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${invitacion.correo}`) },
      { nombre: SST.nombre, archivo: pdf(`SST ${invitacion.correo}`), campos: { 'Fecha de expedición': hace2Dias } },
    ]);
    const sofia = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.sofia, e.sur);
    await abrirDesdeBandeja(sofia, invitacion.nombre, 'Revisión');
    await aprobarArea(sofia, 'Revisión');
    await esperarEstadoDelTercero(page, 'Aprobado');

    // La administradora bloquea el vínculo con un motivo interno.
    const ana = await abrirEmbebido(await (await browser.newContext()).newPage(), e, e.ana, e.sur, `vinculos/${invitacion.vinculoId}`);
    await expect(ana.getByRole('heading', { level: 1, name: invitacion.nombre })).toBeVisible();
    const enlace = await ana.getByRole('link', { name: 'Página de verificación' }).getAttribute('href');
    const publica = await (await browser.newContext()).newPage();
    const url = new URL(enlace ?? '');
    const ruta = `${url.pathname}${url.search}`;
    await ana.getByRole('button', { name: 'Más acciones' }).click();
    await ana.getByRole('menuitem', { name: /^Bloquear/ }).click();
    const bloquear = ana.getByRole('dialog', { name: /^Bloquear vínculo/ });
    await bloquear.getByRole('textbox', { name: 'Motivo' }).fill('Coincidencia en lista restrictiva por revisar.');
    await bloquear.getByRole('button', { name: 'Bloquear vínculo' }).click();

    // 1. Suspendido: el certificado no debe aceptarse y el motivo no se muestra.
    await publica.goto(ruta);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado está suspendido' })).toBeVisible();
    await expect(publica.getByRole('main')).not.toContainText('Coincidencia en lista restrictiva');
    await paso(publica, {
      titulo: 'Lee que el certificado está suspendido',
      explicacion:
        'El estado dice «Este certificado está suspendido» y explica desde cuándo la empresa suspendió temporalmente el vínculo: mientras siga así, no lo aceptes. ' +
        'El motivo que la empresa escribió es interno y no aparece aquí. El PDF tampoco se entrega mientras el certificado no esté vigente.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado está suspendido' }),
      celular: false,
      variantes: [
        { cuando: 'El certificado está en verificación', entonces: 'La página dice «Este certificado está en verificación» y explica que la empresa revisa de nuevo lo que el titular actualizó; sigue vigente hasta su vencimiento.' },
        { cuando: 'El certificado tiene pendientes', entonces: 'La página dice «Este certificado tiene pendientes» y pide confirmarlo con la empresa antes de aceptarlo.' },
        { cuando: 'Hay una emisión más reciente', entonces: 'La página dice «Hay una emisión más reciente» y ofrece «Ver la emisión actual» con su folio.' },
        { cuando: 'El certificado venció', entonces: 'La página dice «Este certificado venció» con la fecha y pide un certificado actualizado.' },
      ],
    });

    // Se levanta el bloqueo y luego se revoca el certificado (lo hace la administradora).
    await ana.getByRole('button', { name: 'Desbloquear', exact: true }).click();
    const desbloquear = ana.getByRole('dialog');
    await desbloquear.getByRole('textbox').first().fill('Se descartó la coincidencia.');
    await desbloquear.getByRole('button', { name: /^Desbloquear/ }).click();
    await ana.getByRole('button', { name: 'Revocar certificado' }).click();
    const revocar = ana.getByRole('dialog', { name: /^Revocar el certificado/ });
    await revocar.getByRole('textbox', { name: 'Justificación' }).fill('Se aprobó con un documento adulterado.');
    await revocar.getByRole('button', { name: 'Revocar certificado' }).click();
    await expect(revocar).toBeHidden();

    // 2. Revocado.
    await publica.goto(ruta);
    await expect(publica.getByRole('heading', { level: 2, name: 'Este certificado fue revocado' })).toBeVisible();
    await expect(publica.getByRole('main')).not.toContainText('Se aprobó con un documento adulterado.');
    await paso(publica, {
      titulo: 'Lee que el certificado fue revocado',
      explicacion:
        'Un certificado revocado dice «Este certificado fue revocado», la fecha desde la que dejó de valer y que no puede volver a usarse. La empresa puede decirte el motivo; la justificación que escribió no aparece en la página. ' +
        'Si el enlace es auténtico, la sección de autenticidad lo sigue confirmando, pero el certificado ya no sirve.',
      resaltar: publica.getByRole('heading', { level: 2, name: 'Este certificado fue revocado' }),
      celular: false,
    });
  },
);
