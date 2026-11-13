import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'publico',
    actor: 'Público',
    titulo: 'Llega a una dirección que no existe y vuelve a entrar',
    resumen:
      'Cuando un enlace está roto o se escribe mal una dirección, Validasoft muestra «No encontramos este enlace» con una salida clara: entrar con el correo para llegar a los documentos.',
    precondiciones: ['Nadie ha entrado al portal: la consulta se hace sin sesión, desde una ventana nueva del navegador.'],
    relacionados: ['publico/verificar-certificado-invalido'],
  },
  async ({ page }) => {
    // 1. La dirección no existe.
    await page.goto('/esta-pagina-no-existe');
    await expect(page.getByRole('heading', { level: 1, name: 'No encontramos este enlace' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre una dirección que no existe',
      explicacion:
        'La página dice «No encontramos este enlace» y pide revisar la dirección. Si buscas tus documentos, la salida es entrar con tu correo. ' +
        'El botón cambia según tu situación: sin sesión lleva a «Entrar con mi correo»; si ya entraste, lleva a «Mis empresas».',
      resaltar: page.getByRole('heading', { level: 1, name: 'No encontramos este enlace' }),
      celular: false,
      variantes: [
        { cuando: 'Tienes una sesión abierta', entonces: 'El botón lleva a «Mis empresas» en lugar de a la entrada.' },
        { cuando: 'Llegaste desde un enlace del correo que ya no sirve', entonces: 'El enlace puede ser viejo: busca el último correo de invitación o pide que te lo reenvíen.' },
      ],
    });

    // 2. Salir de la página.
    await page.getByRole('link', { name: 'Entrar con mi correo' }).click();
    await expect(page.getByRole('heading', { level: 1, name: 'Entrar con mi correo' })).toBeVisible();
    await paso(page, {
      titulo: 'Vuelve a entrar con tu correo',
      explicacion:
        'Con «Entrar con mi correo» llegas a la entrada del portal. Escribe tu correo y Validasoft te envía un código para entrar; si estabas buscando un certificado, usa el enlace de verificación original.',
      resaltar: page.getByRole('heading', { level: 1, name: 'Entrar con mi correo' }),
      celular: false,
    });
  },
);
