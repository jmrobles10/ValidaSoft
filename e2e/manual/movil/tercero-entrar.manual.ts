import { expect, paso, recorrido } from '../nucleo';
import { enlace, esperarCorreo } from '../../soporte/correo';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Entra al portal desde el celular con tu correo y el código',
    resumen:
      'El tercero entra al portal con el correo de su invitación. En el celular, escribe el correo, recibe el enlace y, si la empresa lo pide, escribe el código de seis dígitos en los recuadros de la pantalla.',
    precondiciones: [
      'Un tercero con una invitación de Constructora Norte enviada a su correo.',
      'El tercero no tiene sesión abierta en el celular.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'movil/tercero-privacidad'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Pacífico S.A.S.');
    await invitar(e, e.norte, tercero);

    // 1. Entrar con el correo.
    await page.goto('/entrar');
    await expect(page.getByRole('heading', { level: 1, name: 'Entrar con mi correo' })).toBeVisible();
    await paso(page, {
      titulo: 'Escribe el correo de tu invitación',
      explicacion:
        'Escribe el correo al que te llegó la invitación de la empresa y toca «Enviarme el enlace». No necesitas contraseña: el enlace sirve una sola vez para entrar.',
      resaltar: page.getByRole('textbox', { name: 'Correo de la invitación' }),
      variantes: [
        { cuando: 'El correo no tiene formato válido', entonces: 'La pantalla pide escribir un correo válido, por ejemplo nombre@empresa.com, antes de enviar.' },
      ],
    });

    // 2. El correo con el enlace.
    await page.getByRole('textbox', { name: 'Correo de la invitación' }).fill(tercero.correo);
    await page.getByRole('button', { name: 'Enviarme el enlace' }).click();
    await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
    const correo = await esperarCorreo(tercero.correo, new RegExp(`^${e.norte.nombre.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')} te invita`));
    await paso(page, {
      titulo: 'Revisa tu correo',
      explicacion:
        'La pantalla dice «Revisa tu correo». El mensaje llega a la dirección de tu invitación, con el enlace para entrar; revisa también la carpeta de spam.',
      resaltar: page.getByRole('heading', { name: 'Revisa tu correo' }),
      variantes: [
        { cuando: 'Pediste varios enlaces seguidos', entonces: 'La pantalla dice que espere unos minutos antes de volver a intentarlo.' },
      ],
    });

    // 3. El código de seis dígitos.
    await page.goto(enlace(correo, '/e/'));
    await expect(page.getByRole('heading', { name: 'Escribe el código' })).toBeVisible();
    await paso(page, {
      titulo: 'Escribe el código de seis dígitos',
      explicacion:
        'Cuando la empresa pide verificar tu correo, aparece «Escribe el código» con seis recuadros, uno por dígito. Escribe el código que te llegó por correo. ' +
        'El código vence: si pasa el tiempo, abre de nuevo el enlace de tu correo y te enviaremos uno nuevo.',
      resaltar: page.getByRole('textbox', { name: 'Dígito 1 de 6' }),
      variantes: [
        { cuando: 'La empresa no pide código', entonces: 'Vas directo a la invitación, sin esta pantalla.' },
      ],
    });
  },
);
