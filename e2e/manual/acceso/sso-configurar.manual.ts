import { randomUUID } from 'node:crypto';
import { confirmarConPasskey, crearGrupoDirecto, entrarYRegistrarPasskey } from '../../soporte/ingreso';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Administrador de empresa',
    titulo: 'Configura el inicio de sesión único con el proveedor de tu empresa',
    resumen:
      'Ana conecta Validasoft con el proveedor de identidad de su empresa (OIDC): le da el nombre del botón, el emisor, el identificador del cliente y el secreto, y declara el dominio de correo de su empresa. ' +
      'Validasoft pide verificar el dominio con un registro DNS. El secreto se guarda y no vuelve a mostrarse.',
    precondiciones: [
      'Un grupo con ingreso directo y Ana como administradora de todo el grupo, con su passkey registrada.',
      'Un proveedor de identidad de prueba: el emisor es una dirección ficticia y nada se conecta a un proveedor real.',
    ],
    relacionados: ['acceso/sso-ingresar', 'acceso/ingreso-configuracion-del-grupo', 'acceso/ingreso-con-passkey'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    await entrarYRegistrarPasskey(page, g);
    const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
    const dominio = `sso-${sufijo}.e2e.validasoft.co`;

    await page.goto(`/app/#/e/${g.empresaId}/sso`);
    await expect(page.getByRole('heading', { level: 1, name: 'Inicio de sesión único' })).toBeVisible();
    await expect(page.getByText('Sin configurar', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Abre la configuración del inicio de sesión único',
      explicacion:
        'En Configuración › Inicio de sesión único, la empresa conecta su proveedor de identidad. Hoy está «Sin configurar». Los datos del proveedor son el nombre del botón que verán las personas, el emisor (issuer), el Client ID y el secreto del cliente.',
      resaltar: page.getByRole('heading', { level: 1, name: 'Inicio de sesión único' }),
      configuracion: {
        ajuste: 'Inicio de sesión único (SSO)',
        ruta: 'Configuración › Ingreso › Inicio de sesión único (/sso)',
        efecto: 'Con SSO configurado, las personas de la empresa pueden entrar con la cuenta de su proveedor.',
      },
    });

    await page.getByLabel('Nombre del botón').fill('Andina');
    await page.getByLabel('Emisor (issuer)').fill('http://localhost:9/v2.0');
    await page.getByLabel('Client ID').fill('validasoft-e2e');
    await page.getByRole('button', { name: 'Escribir secreto' }).click();
    await page.getByLabel('Secreto del cliente').fill('secreto-e2e-12345');
    await page.getByLabel('Dominio', { exact: true }).fill(dominio);
    await page.getByRole('button', { name: 'Agregar', exact: true }).click();
    if (await page.getByRole('dialog', { name: 'Confirma que eres tú' }).isVisible({ timeout: 2_000 }).catch(() => false)) await confirmarConPasskey(page);
    await expect(page.getByText(`@${dominio}`)).toBeVisible();
    await expect(page.getByText('Por verificar', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Declara el dominio de correo de tu empresa',
      explicacion:
        'Escribe el dominio de los correos de tu empresa y pulsa «Agregar». Validasoft muestra el dominio como «Por verificar» y el texto validasoft-verificacion=… que debes publicar en el archivo /.well-known/validasoft-verificacion.txt de ese dominio, y luego pulsar «Verificar». ' +
        'El inicio de sesión único solo vale para los correos de dominios que demuestres que son tuyos: mientras esté por verificar, no aplica a sus correos.',
      resaltar: page.getByText('Por verificar', { exact: true }),
    });

    await page.getByRole('button', { name: 'Guardar' }).click();
    await confirmarConPasskey(page);
    await expect(page.getByText('Guardaste el inicio de sesión único.')).toBeVisible();
    await page.reload();
    await expect(page.getByText('Secreto guardado, termina en 2345')).toBeVisible();
    await expect(page.getByText('Apagado', { exact: true })).toBeVisible();
    await expect(page.getByText('secreto-e2e-12345')).toHaveCount(0);
    await paso(page, {
      titulo: 'Guarda la configuración',
      explicacion:
        'Al guardar, Validasoft pide confirmar con tu passkey porque es una acción sensible. Después, la pantalla solo muestra con qué cuatro caracteres termina el secreto: el secreto completo nunca vuelve a verse. ' +
        'La pantalla indica que la configuración está «Apagado».',
      resaltar: page.getByText('Secreto guardado, termina en 2345'),
    });
  },
);
