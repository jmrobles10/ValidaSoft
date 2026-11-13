import { cerrarSesion, confirmarConPasskey, crearGrupoDirecto, entrarConCodigo, entrarYRegistrarPasskey } from '../../soporte/ingreso';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Administrador de empresa',
    titulo: 'Genera códigos de recuperación por si pierdes tu passkey',
    resumen:
      'Ana genera diez códigos de recuperación en Mi cuenta. Cada uno sirve una sola vez. Si pierde su passkey, entra con el código del correo, usa uno de los códigos en lugar de la passkey y registra una nueva.',
    precondiciones: [
      'Un grupo con ingreso directo y Ana como administradora de todo el grupo, con su passkey ya registrada.',
      'Un autenticador de prueba que simula la huella o el PIN del dispositivo.',
    ],
    relacionados: ['acceso/ingreso-con-passkey', 'acceso/mi-cuenta-sesiones-y-codigos', 'acceso/ingreso-configuracion-del-grupo'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    const virtual = await entrarYRegistrarPasskey(page, g);

    await page.goto(`/app/#/e/${g.empresaId}/mi-cuenta`);
    await expect(page.getByRole('heading', { name: 'Códigos de recuperación' })).toBeVisible();
    await expect(page.getByText('Aún no tienes códigos de recuperación.')).toBeVisible();
    await paso(page, {
      titulo: 'Abre los códigos de recuperación en Mi cuenta',
      explicacion:
        'En Mi cuenta, la sección «Códigos de recuperación» muestra si tienes códigos activos. Hoy no tienes ninguno. Son el respaldo para entrar si pierdes el dispositivo donde guardas tu passkey.',
      resaltar: page.getByRole('heading', { name: 'Códigos de recuperación' }),
    });

    await page.getByRole('button', { name: 'Generar códigos' }).click();
    await page.getByRole('dialog', { name: 'Generar códigos de recuperación' }).getByRole('button', { name: 'Generar códigos' }).click();
    await confirmarConPasskey(page);
    const codigos = page.getByRole('dialog', { name: 'Tus códigos de recuperación' });
    await expect(codigos).toBeVisible();
    const lista = (await codigos.getByRole('listitem').allTextContents()).map((c) => c.trim());
    expect(lista).toHaveLength(10);
    await paso(page, {
      titulo: 'Guarda los diez códigos ahora',
      explicacion:
        'Validasoft muestra diez códigos una sola vez. Antes de verlos, el navegador te pidió confirmar con tu passkey. Cada código tiene la forma XXXXX-XXXXX y sirve una sola vez. ' +
        'Guárdalos en un lugar seguro, aparte de tu correo: si los pierdes, no vuelven a mostrarse.',
      resaltar: codigos,
      variantes: [
        { cuando: 'Ya tienes códigos y generas unos nuevos', entonces: 'Los anteriores dejan de servir y te muestran diez nuevos, una sola vez.' },
      ],
    });

    await codigos.getByLabel('Ya los guardé en un lugar seguro').check();
    await codigos.getByRole('button', { name: 'Listo' }).click();
    await expect(page.getByText(/Te quedan 10 de 10 códigos sin usar/)).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que los guardaste',
      explicacion:
        'Marca «Ya los guardé en un lugar seguro» para cerrar la hoja. Después Mi cuenta indica cuántos códigos quedan sin usar. Los códigos no vuelven a verse aunque abras de nuevo la sección.',
      resaltar: page.getByText(/Te quedan 10 de 10 códigos sin usar/),
    });

    await cerrarSesion(page);
    await virtual.cdp.send('WebAuthn.clearCredentials', { authenticatorId: virtual.id });
    await entrarConCodigo(page, g.correoAdmin);
    await expect(page.getByRole('heading', { name: 'Confirma que eres tú' })).toBeVisible();
    await page.getByRole('button', { name: 'No tengo mi passkey' }).click();
    await expect(page.getByRole('heading', { name: 'Usa un código de recuperación' })).toBeVisible();
    await paso(page, {
      titulo: 'Usa un código si no tienes tu passkey',
      explicacion:
        'Si pierdes tu passkey, en el paso de confirmación pulsa «No tengo mi passkey». Escribe uno de los códigos guardados y pulsa «Usar el código». El código solo abre la pantalla para registrar una passkey nueva.',
      resaltar: page.getByLabel('Código de recuperación'),
      variantes: [{ cuando: 'El código ya se usó o está mal escrito', entonces: 'La pantalla muestra un aviso y no te deja entrar.' }],
    });

    await page.getByLabel('Código de recuperación').fill(lista[0]?.toLowerCase().replace('-', '') ?? '');
    await page.getByRole('button', { name: 'Usar el código' }).click();
    await expect(page.getByRole('heading', { name: 'Registra tu passkey' })).toBeVisible();
    await page.getByRole('button', { name: 'Registrar passkey' }).click();
    await expect(page.getByRole('navigation', { name: 'Secciones' })).toBeVisible();
    await page.goto(`/app/#/e/${g.empresaId}/mi-cuenta`);
    await expect(page.getByText(/Te quedan 9 de 10 códigos sin usar/)).toBeVisible();
    await paso(page, {
      titulo: 'Registra una passkey nueva después del código',
      explicacion:
        'Con el código entras solo para registrar una passkey nueva. Después de registrarla, Mi cuenta muestra que te quedan nueve códigos sin usar. Un código usado no vuelve a servir.',
      resaltar: page.getByText(/Te quedan 9 de 10 códigos sin usar/),
    });
  },
);
