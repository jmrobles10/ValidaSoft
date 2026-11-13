import { abrirEmbebido } from '../../soporte/embebido';
import { expect, paso, recorrido } from '../nucleo';
import { crearEscenario } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'embebido',
    actor: 'Revisor/área',
    titulo: 'Abre la app de tu empresa dentro de tu sistema',
    resumen:
      'Rubén usa Validasoft sin ver una pantalla de ingreso: abre la página de su sistema, que muestra la app en un marco y le entrega la sesión por dentro. ' +
      'La app se abre en Constructora Norte y, al cerrar la sesión desde el sistema, la app lo avisa y deja de mostrar datos.',
    precondiciones: [
      'Un grupo con Constructora Norte, su flujo de proveedor publicado y Rubén como miembro del área Compras.',
      'La página del sistema integrado (E2E) registrada en Validasoft como origen permitido.',
    ],
    relacionados: ['embebido/navegacion-adaptativa-y-tema', 'embebido/sesion-vencida', 'integracion/identidad-delegada'],
  },
  async ({ page }) => {
    const e = await crearEscenario();

    const app = await abrirEmbebido(page, e, e.ruben, e.norte, 'bandeja');
    await expect(app.getByRole('heading', { level: 1, name: 'Bandeja de revisión' })).toBeVisible();
    await paso(page, {
      titulo: 'Abre Validasoft desde tu sistema',
      explicacion:
        'Tu sistema muestra Validasoft en un marco (iframe) dentro de sus propias pantallas. Rubén no ingresa con usuario y contraseña: cuando la app necesita sesión, el sistema le pide a su backend una sesión para Rubén y se la entrega a la app. ' +
        'La app aparece sin barra de ingreso y sin el marco de Validasoft, con la navegación propia de la empresa.',
      resaltar: page.locator('#validasoft'),
      variantes: [
        { cuando: 'El sistema no entrega sesión', entonces: 'La app se queda en «Conectando con tu sistema…» y, si tarda más de 10 segundos, pide volver a abrirla desde el sistema.' },
      ],
    });

    await paso(page, {
      titulo: 'Revisa la empresa con la que trabajas',
      explicacion:
        'La sesión lleva la empresa elegida por el sistema, así que la app abre directamente en Constructora Norte S.A.S. y su NIT aparece en la cabecera. ' +
        'Rubén solo ve los expedientes de las áreas de las que es miembro.',
      resaltar: app.getByText('Constructora Norte S.A.S.', { exact: true }).first(),
    });

    await page.evaluate(() => {
      const marco = document.getElementById('validasoft') as HTMLIFrameElement | null;
      marco?.contentWindow?.postMessage({ tipo: 'validasoft:cerrar-sesion' }, '*');
    });
    await expect(app.getByText('Cerraste la sesión', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Cierra la sesión desde tu sistema',
      explicacion:
        'Cuando el sistema cierra la sesión (por ejemplo, al salir de su propia cuenta), envía el aviso a la app y esta deja de mostrar datos. ' +
        'Para volver a entrar hay que abrir Validasoft de nuevo desde el sistema.',
      resaltar: app.getByText('Cerraste la sesión', { exact: true }),
    });
  },
);
