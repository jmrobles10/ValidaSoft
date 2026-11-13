import { enlace, esperarCorreo } from '../../soporte/correo';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Qué pasa cuando un enlace de invitación ya se usó, venció o no existe',
    resumen:
      'Cada enlace de invitación sirve una sola vez y tiene fecha de vencimiento. Cuando el tercero intenta abrir uno usado, vencido o inexistente, Validasoft no lo deja entrar y le dice qué hacer: pedir a la empresa una invitación nueva.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor que pide código por correo.',
      'Un tercero invitado cuya invitación ya fue abierta y aceptada una vez desde su correo.',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'tercero/entrar-sin-invitacion'],
  },
  async ({ page, browser }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');

    // 1. Primer uso del enlace: el tercero acepta y el enlace queda consumido.
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);
    await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.sur.nombre}` })).toBeVisible();
    await paso(page, {
      titulo: 'Usa el enlace del correo una sola vez',
      explicacion:
        'Al abrir el enlace y aceptar la invitación, el enlace queda consumido: Validasoft lo guarda como usado. Desde ahora tu acceso a esta empresa es tu sesión, no el enlace. ' +
        'Si cierras el navegador, entra por «/entrar» con tu correo: no necesitas el enlace otra vez.',
      resaltar: page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.sur.nombre}` }),
      variantes: [
        { cuando: 'El enlace ya se usó y cierras el navegador', entonces: 'El enlace no sirve de nuevo: entra por «/entrar» con tu correo.' },
      ],
    });

    // 2. Abrir el mismo enlace de nuevo, desde otra sesión, muestra que ya se usó.
    const correo = await esperarCorreo(tercero.correo, /te invita a validar tu vínculo como proveedor$/);
    const otra = await (await browser.newContext()).newPage();
    await otra.goto(enlace(correo, '/e/'));
    await expect(otra.getByRole('heading', { name: 'Este enlace ya se usó' })).toBeVisible();
    await paso(otra, {
      titulo: 'Intenta abrir un enlace que ya se usó',
      explicacion:
        'La pantalla dice «Este enlace ya se usó» y explica que cada enlace sirve una sola vez. Si cerraste la página sin terminar, pide a la empresa que te reenvíe la invitación: te llegará un correo con un enlace nuevo. ' +
        'Validasoft no te deja entrar con un enlace usado, aunque sea tuyo.',
      resaltar: otra.getByRole('heading', { name: 'Este enlace ya se usó' }),
      variantes: [
        { cuando: 'El enlace venció por fecha', entonces: 'La pantalla dice «Este enlace venció» y pide a la empresa un enlace nuevo.' },
      ],
    });

    // 3. Un enlace que no existe.
    await otra.goto('/e/enlace-que-no-existe');
    await expect(otra.getByRole('heading', { name: 'No encontramos este enlace' })).toBeVisible();
    await paso(otra, {
      titulo: 'Un enlace que no existe',
      explicacion:
        'Si el enlace está incompleto o ya no existe, la pantalla dice «No encontramos este enlace». Puede que la empresa haya enviado uno más reciente: busca el último correo de invitación. ' +
        'Si no lo tienes, pide a la empresa que te lo reenvíe.',
      resaltar: otra.getByRole('heading', { name: 'No encontramos este enlace' }),
    });
  },
);
