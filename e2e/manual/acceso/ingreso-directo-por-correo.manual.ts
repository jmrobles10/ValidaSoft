import { expect, paso, recorrido } from '../nucleo';
import { crearGrupoDirecto, crearUsuario, TEXTO_ASUNTO_CODIGO } from '../../soporte/ingreso';
import { esperarCorreo, codigo } from '../../soporte/correo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Revisor/área',
    titulo: 'Entra a Validasoft con tu correo y un código',
    resumen:
      'Rita entra sin contraseña: escribe su correo, recibe un código de seis caracteres en su bandeja y lo escribe en la pantalla. ' +
      'Después la sesión sigue abierta al recargar la página, hasta que cierra sesión.',
    precondiciones: [
      'Un grupo con ingreso directo (sin SSO) y Rita como revisora de su empresa, sin passkey registrada.',
    ],
    relacionados: ['acceso/ingreso-con-passkey', 'acceso/mi-cuenta-sesiones-y-codigos'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    const correo = `rita.${Date.now().toString(36)}@e2e.validasoft.co`;
    await crearUsuario(g, 'Rita Revisora', 'Revisor', correo);

    await page.goto('/app/');
    await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Entrar con una passkey' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la pantalla de ingreso',
      explicacion:
        'Validasoft no pide contraseña. Escribe tu correo en el campo «Correo» y pulsa «Continuar». Si tu cuenta ya tiene una passkey, puedes usar el botón «Entrar con una passkey» en su lugar.',
      resaltar: [page.getByLabel('Correo'), page.getByRole('button', { name: 'Continuar' })],
      variantes: [
        { cuando: 'Tu navegador no admite passkeys', entonces: 'El botón de passkey no aparece y ves el aviso de usar el código del correo.' },
        { cuando: 'Tu empresa ingresa con su propio inicio de sesión', entonces: 'La pantalla te pide entrar con tu cuenta de trabajo en lugar del código.' },
      ],
    });

    await page.getByLabel('Correo').fill(correo);
    await page.getByRole('button', { name: 'Continuar' }).click();
    await expect(page.getByRole('heading', { name: 'Revisa tu correo' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa tu correo y escribe el código',
      explicacion:
        'Validasoft envía un correo con el asunto «' + TEXTO_ASUNTO_CODIGO + '». Escribe el código que trae en las casillas de esta pantalla; no hace falta pulsar nada más. ' +
        'El código entra solo al escribirlo completo.',
      resaltar: page.getByRole('heading', { name: 'Revisa tu correo' }),
    });

    const correoDelCodigo = await esperarCorreo(correo, new RegExp(TEXTO_ASUNTO_CODIGO));
    await page.keyboard.type(codigo(correoDelCodigo));
    const barra = page.getByRole('navigation', { name: 'Secciones' });
    await expect(barra).toBeVisible();
    await expect(page.getByText('Protege tu cuenta con una passkey')).toBeVisible();
    await paso(page, {
      titulo: 'Entra a la app con el código',
      explicacion:
        'Con el código correcto entras directo a la app, con las secciones de tu rol. Como tu cuenta aún no tiene passkey, Validasoft te sugiere registrar una para entrar más rápido, sin código.',
      resaltar: [barra, page.getByText('Protege tu cuenta con una passkey')],
    });

    await page.reload();
    await expect(barra).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toHaveCount(0);
    await paso(page, {
      titulo: 'Recarga sin volver a pedir el código',
      explicacion:
        'Al recargar la página, Validasoft recupera tu sesión con la cookie de inicio y no te pide otro código. Así no tienes que volver a ingresar en cada pestaña.',
      resaltar: barra,
      variantes: [{ cuando: 'Pasan más horas que la duración de la sesión del grupo', entonces: 'La sesión vence y la pantalla de ingreso vuelve a pedir el correo.' }],
    });

    await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
    await expect(page.getByText(correo)).toBeVisible();
    await page.getByRole('menuitem', { name: 'Cerrar sesión' }).click();
    await expect(page.getByText('Cerraste la sesión')).toBeVisible();
    await paso(page, {
      titulo: 'Cierra la sesión',
      explicacion:
        'Desde el menú de tu cuenta, «Cerrar sesión» termina la sesión en este navegador. Después, recargar la página ya no te devuelve a la app: hay que ingresar de nuevo con tu correo.',
      resaltar: page.getByText('Cerraste la sesión'),
    });
  },
);
