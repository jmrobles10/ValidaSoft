import { expect, paso, recorrido } from '../nucleo';
import { pdf } from '../../soporte/archivos';
import { completarInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'privacidad',
    actor: 'Tercero',
    titulo: 'Pide conocer, corregir o suprimir tus datos personales',
    resumen:
      'El tercero abre «Privacidad y mis datos», crea una solicitud sobre sus datos (consulta, corrección o supresión), elige a quién se la dirige (Validasoft o una empresa con la que tiene vínculo) y recibe su radicado con la fecha límite de respuesta.',
    precondiciones: [
      'Un grupo con Constructora Norte, que invitó a un tercero que ya aceptó la invitación y entregó sus documentos.',
      'El tercero entra al portal con su sesión abierta.',
    ],
    relacionados: ['privacidad/exportar-mi-boveda'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Sur S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    await completarInvitacion(page, invitacion, [
      { nombre: 'Registro Único Tributario', archivo: pdf('RUT Aceros del Sur') },
      { nombre: 'Certificado de existencia y representación legal', archivo: pdf('Camara Aceros del Sur') },
    ]);

    // 1. La pantalla sin solicitudes.
    await page.goto('/privacidad');
    await expect(page.getByRole('heading', { level: 1, name: 'Privacidad y mis datos' })).toBeVisible();
    await expect(page.getByText('Aún no has hecho solicitudes')).toBeVisible();
    await paso(page, {
      titulo: 'Abre Privacidad y mis datos',
      explicacion:
        'Desde el menú superior entras a «Privacidad», que abre «Privacidad y mis datos» y reúne tus derechos sobre tus datos personales según la Ley 1581 de 2012. ' +
        'Aquí puedes pedir una copia de tu bóveda y hacer solicitudes sobre tus datos; cada solicitud tiene un radicado y una fecha límite de respuesta. Si aún no has hecho ninguna, la lista aparece vacía.',
      resaltar: page.getByRole('button', { name: 'Nueva solicitud' }),
    });

    // 2. El tipo de solicitud.
    await page.getByRole('button', { name: 'Nueva solicitud' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Nueva solicitud sobre mis datos' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('radio', { name: /Conocer mis datos/ }).check();
    await paso(page, {
      titulo: 'Elige qué quieres hacer',
      explicacion:
        'La solicitud empieza con el derecho que ejerces: «Conocer mis datos» te dice qué datos tuyos tienen y para qué; «Corregir un dato» pide actualizar uno que está mal; «Suprimir mis datos» pide eliminarlos. ' +
        'Cada opción muestra en cuántos días hábiles te responden. Elige una para continuar.',
      resaltar: [
        dialogo.getByRole('radio', { name: /Conocer mis datos/ }),
        dialogo.getByRole('radio', { name: /Corregir un dato/ }),
        dialogo.getByRole('radio', { name: /Suprimir mis datos/ }),
      ],
      variantes: [
        { cuando: 'Eliges «Corregir un dato»', entonces: 'Debes escribir qué dato está mal y cuál es el correcto (mínimo 10 caracteres).' },
        { cuando: 'Eliges «Suprimir mis datos»', entonces: 'Aparece el aviso de lo que la ley obliga a guardar: se conserva íntegro y no se comparte con nadie más; lo demás se elimina y no se puede recuperar.' },
      ],
    });

    // 3. A quién se la pides.
    await dialogo.getByRole('combobox', { name: 'Se la pides a' }).click();
    await page.getByRole('option', { name: e.norte.nombre }).click();
    await expect(dialogo.getByText(`${e.norte.nombre} recibe tu solicitud ahora y te responde en máximo 10 días hábiles`, { exact: false })).toBeVisible();
    await paso(page, {
      titulo: 'Elige a quién se la pides',
      explicacion:
        'En «Se la pides a» eliges Validasoft, que responde por lo que guarda en tu bóveda (documentos, información y cuentas), o una empresa con la que tienes un vínculo, que responde por los datos de esa relación. ' +
        'El aviso de abajo dice quién recibe la solicitud ahora, en cuántos días hábiles te responde y que te llegará un correo con el radicado.',
      resaltar: dialogo.getByRole('combobox', { name: 'Se la pides a' }),
      variantes: [
        { cuando: 'Eliges «Validasoft (mi bóveda)»', entonces: 'Validasoft responde por tu bóveda y la solicitud no pasa por ninguna empresa.' },
        { cuando: 'Eliges una empresa', entonces: 'Solo aparecen las empresas con las que tienes una invitación aceptada.' },
      ],
    });

    // 4. Enviar y recibir el radicado.
    await dialogo.getByRole('button', { name: 'Enviar solicitud' }).click();
    const tarjeta = page.getByRole('article').filter({ hasText: 'Conocer mis datos' });
    await expect(tarjeta).toContainText(/HD-\d{4}-[0-9A-F]{8}/);
    await expect(tarjeta).toContainText(`Dirigida a ${e.norte.nombre}`);
    await paso(page, {
      titulo: 'Envía la solicitud y guarda su radicado',
      explicacion:
        'Al enviarla, la solicitud aparece en la lista con su radicado (HD, el año y un código), la empresa a la que va dirigida y los días hábiles que quedan para la respuesta. ' +
        'También te llega un acuse al correo con el mismo radicado. Mientras esté en trámite, no puedes enviar otra igual a la misma destinataria.',
      resaltar: tarjeta,
      avisos: true,
    });
  },
);
