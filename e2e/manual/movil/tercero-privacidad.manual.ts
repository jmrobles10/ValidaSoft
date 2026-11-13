import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { CAMARA, RUT } from './siembra';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Haz una solicitud sobre tus datos desde el celular',
    resumen:
      'En el celular, «Privacidad y mis datos» ocupa la pantalla completa. El tercero abre la nueva solicitud, elige qué quiere hacer y a quién se la pide, y ve su radicado con la fecha límite.',
    precondiciones: [
      'Un tercero invitado por Constructora Norte que entregó sus documentos y tiene sesión abierta en el portal.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['privacidad/solicitud-sobre-mis-datos'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Sur S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, invitacion, [
      { nombre: RUT, archivo: pdf(`RUT ${tercero.nombre}`) },
      { nombre: CAMARA, archivo: pdf(`Camara ${tercero.nombre}`) },
    ]);

    // 1. La pantalla de privacidad.
    await page.goto('/privacidad');
    await expect(page.getByRole('heading', { level: 1, name: 'Privacidad y mis datos' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre Privacidad y mis datos en el celular',
      explicacion:
        'Arriba de la pantalla está «Exportar mi bóveda» y debajo «Mis solicitudes sobre mis datos», con el botón «Nueva solicitud». Cada solicitud que hagas aparece como una tarjeta con su radicado, a quién la dirigiste y el plazo para responderte.',
      resaltar: page.getByRole('button', { name: 'Nueva solicitud' }),
    });

    // 2. El diálogo.
    await page.getByRole('button', { name: 'Nueva solicitud' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('radio', { name: /Conocer mis datos/ }).check();
    await paso(page, {
      titulo: 'Elige qué quieres hacer y a quién se lo pides',
      explicacion:
        'El formulario te pregunta qué quieres hacer (conocer, corregir o suprimir tus datos) y a quién se lo pides: Validasoft, por lo que guarda en tu bóveda, o una empresa con la que tienes vínculo. ' +
        'Antes de enviar, el aviso te dice quién recibe la solicitud y en cuántos días hábiles te responde; «Enviar solicitud» la radica.',
      resaltar: dialogo.getByRole('combobox', { name: 'Se la pides a' }),
      variantes: [
        { cuando: 'Eliges «Suprimir mis datos»', entonces: 'Aparece el aviso de lo que la ley obliga a guardar, que se conserva íntegro y no se comparte.' },
      ],
    });
  },
);
