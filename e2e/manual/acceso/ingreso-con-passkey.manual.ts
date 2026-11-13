import { autenticadorVirtual } from '../../soporte/autenticador';
import { cerrarSesion, crearGrupoDirecto, crearUsuario, entrarConCodigo } from '../../soporte/ingreso';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Revisor/área',
    titulo: 'Registra una passkey y entra con tu huella o tu rostro',
    resumen:
      'Rita registra una passkey en Mi cuenta: la clave de su dispositivo, que se usa con la huella, el rostro o el PIN. ' +
      'Desde entonces entra con «Entrar con una passkey», sin esperar un código por correo.',
    precondiciones: [
      'Un grupo con ingreso directo y Rita como revisora, con su primera entrada por código ya hecha (sin passkey).',
      'Un autenticador de prueba del navegador que simula la huella o el PIN del dispositivo.',
    ],
    relacionados: ['acceso/ingreso-directo-por-correo', 'acceso/mi-cuenta-sesiones-y-codigos', 'acceso/codigos-de-recuperacion'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    const correo = `rita.${Date.now().toString(36)}@e2e.validasoft.co`;
    await crearUsuario(g, 'Rita Revisora', 'Revisor', correo);
    await autenticadorVirtual(page);
    await page.goto('/app/');
    await entrarConCodigo(page, correo);
    await expect(page.getByRole('navigation', { name: 'Secciones' })).toBeVisible();

    await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
    await page.getByRole('menuitem', { name: 'Mi cuenta' }).click();
    await expect(page.getByRole('heading', { name: 'Rita Revisora' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Passkeys' })).toBeVisible();
    await expect(page.getByText('Aún no tienes passkeys')).toBeVisible();
    await paso(page, {
      titulo: 'Abre Mi cuenta y revisa tus passkeys',
      explicacion:
        'Desde el menú de tu cuenta, abre «Mi cuenta». La sección «Passkeys» lista las que tienes registradas; hoy está vacía. ' +
        'Una passkey usa la huella, el rostro o el PIN de tu dispositivo (o una llave de seguridad), así que no necesitas recordar nada.',
      resaltar: page.getByRole('heading', { name: 'Passkeys' }),
      variantes: [{ cuando: 'Tu grupo exige passkey para todos', entonces: 'Validasoft te pide registrar una la próxima vez que ingreses.' }],
    });

    await page.getByRole('button', { name: 'Agregar passkey' }).click();
    const dialogo = page.getByRole('dialog');
    await page.getByLabel('Nombre para reconocerla').fill('Mi teléfono');
    await paso(page, {
      titulo: 'Agrega una passkey y ponle un nombre',
      explicacion:
        'Al agregar una passkey, escribe un nombre para reconocerla (por ejemplo, «Mi teléfono») y pulsa «Registrar passkey». El navegador te pedirá confirmar con tu huella, tu rostro o el PIN del dispositivo. ' +
        'Validasoft te avisa por correo cada vez que se registra una passkey en tu cuenta.',
      resaltar: dialogo,
    });
    await dialogo.getByRole('button', { name: 'Registrar passkey' }).click();
    await expect(page.getByText('Mi teléfono')).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la passkey registrada',
      explicacion:
        'La lista muestra la passkey con su nombre. Desde ahí puedes cambiarle el nombre o eliminarla. Mientras tu cuenta no exija passkey, puedes eliminar la última sin restricción.',
      resaltar: page.getByText('Mi teléfono'),
    });

    await cerrarSesion(page);
    await page.getByRole('button', { name: 'Entrar con una passkey' }).click();
    const barra = page.getByRole('navigation', { name: 'Secciones' });
    await expect(barra).toBeVisible();
    await paso(page, {
      titulo: 'Entra solo con tu passkey',
      explicacion:
        'En la pantalla de ingreso pulsa «Entrar con una passkey». El navegador te pide tu huella, tu rostro o el PIN, y entras directo a la app, sin correo ni código. ' +
        'Si la passkey no está disponible en este dispositivo, usa el código de tu correo como antes.',
      resaltar: barra,
    });
  },
);
