import { enlace, esperarCorreo } from '../../soporte/correo';
import { escribirCodigo } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

const ASUNTO_ENLACE = /^Tu enlace para entrar a Validasoft$/;

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Entra a Validasoft con tu correo cuando ya tienes una invitación',
    resumen:
      'Un proveedor que ya fue invitado no abre el enlace del correo: entra por «/entrar», escribe el correo de la invitación y recibe un enlace de un solo uso. Desde «Mis empresas» ve todas sus empresas y abre la que le interesa. Si el correo no está registrado, la pantalla responde igual.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado.',
      'Un tercero invitado por esa empresa, que nunca ha entrado a Validasoft con su correo.',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'tercero/invitacion-vencida-o-usada'],
  },
  async ({ page, browser }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    await invitar(e, e.sur, tercero, 'PROVEEDOR');

    // 1. La entrada con correo.
    await page.goto('/entrar');
    await expect(page.getByRole('heading', { name: 'Entrar con mi correo' })).toBeVisible();
    await page.getByRole('textbox', { name: 'Correo de la invitación' }).fill(tercero.correo);
    await paso(page, {
      titulo: 'Entra con tu correo',
      explicacion:
        'Desde la página de inicio entras con «Entrar con mi correo». Escribe el correo al que te llegó la invitación de la empresa: no hay contraseña. ' +
        'El botón «Enviarme el enlace» te manda a ese mismo correo un enlace para entrar; si la empresa lo pide, también un código.',
      resaltar: [page.getByRole('textbox', { name: 'Correo de la invitación' }), page.getByRole('button', { name: 'Enviarme el enlace' })],
      variantes: [
        { cuando: 'Escribes un correo que no es válido', entonces: 'La pantalla te pide un correo válido, por ejemplo nombre@empresa.com, y no envía nada.' },
        { cuando: 'Ya tienes una sesión abierta', entonces: 'No ves esta pantalla: Validasoft te lleva directo a «Mis empresas».' },
      ],
    });

    // 2. Revisa tu correo.
    await page.getByRole('button', { name: 'Enviarme el enlace' }).click();
    await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa tu correo',
      explicacion:
        'La pantalla dice «Revisa tu correo» y, si tu correo está registrado, te enviamos un enlace para entrar. ' +
        'El correo llega a la dirección de tu invitación; revisa también la carpeta de spam. El enlace sirve una sola vez: si ya lo usaste, pide otro con «Usar otro correo» o entra con el de la invitación.',
      resaltar: page.getByRole('heading', { name: 'Revisa tu correo' }),
    });

    // 3. El enlace abre Mis empresas.
    const correo = await esperarCorreo(tercero.correo, ASUNTO_ENLACE);
    await page.goto(enlace(correo, '/e/'));
    await expect(page.getByRole('heading', { name: 'Escribe el código' })).toBeVisible();
    await escribirCodigo(page, tercero.correo);
    await expect(page.getByRole('heading', { level: 1, name: `${e.sur.nombre} te invita a validar tu información` })).toBeVisible();
    await paso(page, {
      titulo: 'Abre el enlace del correo',
      explicacion:
        'El enlace te pide el código de 6 dígitos que llega al mismo correo, porque el tipo de vínculo Proveedor lo exige. Como todavía no aceptas la invitación de la empresa, el enlace te lleva a sus textos legales: ' +
        'los aceptas y entonces llegas a «Mis empresas», la lista de todas las empresas que te invitaron o con las que tienes un vínculo.',
      resaltar: page.getByRole('heading', { level: 1 }),
      variantes: [
        { cuando: 'La invitación ya fue aceptada', entonces: 'El enlace te lleva directo a «Mis empresas».' },
        { cuando: 'El enlace ya se usó o venció', entonces: 'En lugar de la invitación ves un aviso de enlace no válido; pide otro con tu correo.' },
      ],
    });
    for (const casilla of await page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).all()) await casilla.check();
    await page.getByRole('button', { name: 'Aceptar' }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.sur.nombre}` })).toBeVisible();
    await page.goto('/empresas');
    await expect(page.getByRole('heading', { level: 1, name: 'Mis empresas' })).toBeVisible();
    await expect(page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa tus empresas en «Mis empresas»',
      explicacion:
        '«Mis empresas» lista todas las empresas que te invitaron o con las que tienes un vínculo. Cada tarjeta muestra la empresa, el tipo de vínculo (Proveedor, Cliente…) y el estado de tu revisión. ' +
        'Abre la tarjeta para continuar con esa empresa.',
      resaltar: page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) }),
      variantes: [{ cuando: 'Tienes invitaciones de varias empresas', entonces: 'Ves una tarjeta por cada empresa y tipo de vínculo, cada una con su estado.' }],
    });

    // 4. Un correo sin invitación responde igual, desde una sesión sin entrar (con sesión, /entrar lleva a Mis empresas).
    const anonimo = await (await browser.newContext()).newPage();
    await anonimo.goto('/entrar');
    const desconocido = `nadie.${Date.now().toString(36)}@e2e.validasoft.co`;
    await anonimo.getByRole('textbox', { name: 'Correo de la invitación' }).fill(desconocido);
    await anonimo.getByRole('button', { name: 'Enviarme el enlace' }).click();
    await expect(anonimo.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
    await paso(anonimo, {
      titulo: 'Un correo sin invitación recibe la misma respuesta',
      explicacion:
        'Si escribes un correo que no está registrado en ninguna empresa, la pantalla responde lo mismo: «Revisa tu correo». Así nadie puede averiguar qué personas están invitadas. ' +
        'Si no te llega nada, revisa que escribiste el correo exacto con el que te invitó la empresa.',
      resaltar: anonimo.getByRole('heading', { name: 'Revisa tu correo' }),
      variantes: [
        { cuando: 'Pides muchos enlaces seguidos', entonces: 'La pantalla dice que pediste varios enlaces seguidos y te pide esperar unos minutos antes de volver a intentar.' },
      ],
    });
  },
);
