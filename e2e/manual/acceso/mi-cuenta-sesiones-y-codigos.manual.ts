import { crearGrupoDirecto, crearUsuario, entrarConCodigo } from '../../soporte/ingreso';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'acceso',
    actor: 'Revisor/área',
    titulo: 'Revisa tus sesiones abiertas y cierra la que no reconoces',
    resumen:
      'En Mi cuenta, Rita ve cómo entra a Validasoft (código del correo, passkey o inicio de sesión de su empresa), en qué dispositivos tiene sesiones abiertas y puede cerrar cualquiera de ellas. ' +
      'Desde la misma pantalla están sus passkeys y sus códigos de recuperación.',
    precondiciones: ['Un grupo con ingreso directo y Rita como revisora, con dos sesiones abiertas: la de este navegador y otra en un dispositivo distinto.'],
    relacionados: ['acceso/ingreso-con-passkey', 'acceso/codigos-de-recuperacion', 'acceso/ingreso-directo-por-correo'],
  },
  async ({ page, browser }) => {
    const g = await crearGrupoDirecto();
    const correo = `rita.${Date.now().toString(36)}@e2e.validasoft.co`;
    await crearUsuario(g, 'Rita Revisora', 'Revisor', correo);
    await page.goto('/app/');
    await entrarConCodigo(page, correo);
    await expect(page.getByRole('navigation', { name: 'Secciones' })).toBeVisible();

    // Otro dispositivo: un segundo navegador entra con el mismo correo.
    const otro = await (await browser.newContext()).newPage();
    await otro.goto('/app/');
    await entrarConCodigo(otro, correo);
    await expect(otro.getByRole('navigation', { name: 'Secciones' })).toBeVisible();

    await page.getByRole('button', { name: 'Menú de tu cuenta' }).click();
    await page.getByRole('menuitem', { name: 'Mi cuenta' }).click();
    await expect(page.getByRole('heading', { name: 'Rita Revisora' })).toBeVisible();
    await expect(page.getByText('Esta sesión', { exact: true })).toBeVisible();
    await expect(page.getByText(correo).first()).toBeVisible();
    await paso(page, {
      titulo: 'Revisa cómo entras y tus sesiones',
      explicacion:
        'Mi cuenta muestra cómo entras a Validasoft (con el código del correo o con tu passkey) y la lista de sesiones abiertas. La sesión de este navegador aparece marcada como «Esta sesión». ' +
        'Cada sesión muestra dónde y cuándo se abrió, para que reconozcas la que no es tuya.',
      resaltar: page.getByText('Esta sesión', { exact: true }),
      variantes: [
        { cuando: 'Entraste desde un sistema integrado', entonces: 'Mi cuenta te indica que tu acceso se gestiona en ese sistema y no muestra cómo cambiarlo.' },
      ],
    });

    await page.getByRole('button', { name: 'Cerrar', exact: true }).first().click();
    await expect(page.getByText('Sesión cerrada.')).toBeVisible();
    await paso(page, {
      titulo: 'Cierra una sesión que no reconoces',
      explicacion:
        'Con «Cerrar» junto a una sesión de la lista, esa sesión queda terminada y la tuya sigue abierta. Quien la usaba tendrá que ingresar de nuevo con su correo.',
      resaltar: page.getByText('Sesión cerrada.'),
    });

    await page.getByRole('button', { name: 'Cerrar esta sesión' }).click();
    await expect(page.getByRole('heading', { name: 'Ingresa a Validasoft' })).toBeVisible();
    await paso(page, {
      titulo: 'Cierra esta sesión desde Mi cuenta',
      explicacion:
        'El botón «Cerrar esta sesión» cierra la sesión del navegador en el que estás y te devuelve a la pantalla de ingreso. Las demás sesiones siguen abiertas, salvo que las cierres tú.',
      resaltar: page.getByRole('heading', { name: 'Ingresa a Validasoft' }),
    });
  },
);
