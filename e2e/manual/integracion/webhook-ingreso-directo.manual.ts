import { llamar, clavesDeSistema } from '../../soporte/api';
import { confirmarConPasskey, crearGrupoDirecto, entrarYRegistrarPasskey } from '../../soporte/ingreso';
import { Receptor } from '../../soporte/receptor';
import { expect, paso, recorrido } from '../nucleo';

const DESCRIPCION = 'ERP de compras';

recorrido(
  {
    dominio: 'integracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un webhook entrando directo a Validasoft y elige su sistema',
    resumen:
      'La administradora entra directo a Validasoft, con su correo y un código, y crea un webhook. Como no viene de un sistema integrado, el panel le pide elegir cuál de los sistemas del grupo es el dueño del webhook: a ese sistema se le avisan sus eventos.',
    precondiciones: [
      'Un grupo con ingreso directo, una administradora que ya registró su passkey y dos sistemas integrados activos: «ERP de compras» y «Portal de proveedores».',
      'Una dirección local que recibe las entregas del webhook.',
    ],
    relacionados: ['integracion/webhooks', 'integracion/sistema-integrador', 'acceso/ingreso-directo-por-correo'],
  },
  async ({ page }) => {
    const g = await crearGrupoDirecto();
    // Siembra: dos sistemas integrados activos del grupo (se documentan en integracion/sistema-integrador).
    for (const [codigo, nombre, puerto] of [
      ['ERP', 'ERP de compras', 5611],
      ['PORTAL', 'Portal de proveedores', 5612],
    ] as const) {
      await llamar('POST', '/api/v1/sistemas', {
        llave: g.admin,
        cuerpo: { codigo, nombre, clavePublicaPem: clavesDeSistema().publica, origenesPermitidos: [`http://localhost:${String(puerto)}`] },
      });
    }
    const receptor = await Receptor.iniciar();
    try {
      await entrarYRegistrarPasskey(page, g);
      await page.goto(`/app/#/e/${g.empresaId}/webhooks`);
      await expect(page.getByRole('heading', { level: 1, name: 'Webhooks' })).toBeVisible();

      await page.getByRole('button', { name: 'Nuevo webhook' }).first().click();
      const drawer = page.locator('[aria-labelledby="titulo-webhook"]');
      await expect(drawer).toBeVisible();
      await drawer.getByRole('textbox', { name: /^Dirección/ }).fill(receptor.url);
      await drawer.getByRole('textbox', { name: /^Descripción/ }).fill(DESCRIPCION);
      const sistema = drawer.getByRole('combobox', { name: /^Sistema integrado/ });
      await expect(sistema).toBeVisible();
      await sistema.click();
      await expect(page.getByRole('option', { name: /ERP de compras · ERP/ })).toBeVisible();
      await expect(page.getByRole('option', { name: /Portal de proveedores · PORTAL/ })).toBeVisible();
      await paso(page, {
        titulo: 'Elige el sistema integrado dueño del webhook',
        explicacion:
          'Entraste directo a Validasoft, no desde un sistema integrado, así que el panel pregunta de qué sistema es el webhook: sus eventos se le avisan a ese sistema. ' +
          'La lista trae los sistemas integrados activos del grupo, con su nombre y su código. Sin elegir uno no puedes guardar. ' +
          'Si entras desde un sistema integrado, el panel no pregunta: el webhook queda a nombre del sistema desde el que entraste.',
        // Con la lista abierta, el resto de la página queda oculto para el árbol accesible: se resalta la lista misma.
        resaltar: page.getByRole('listbox'),
        variantes: [
          { cuando: 'El grupo tiene un solo sistema integrado activo', entonces: 'El panel lo toma solo: no hay nada que elegir.' },
          {
            cuando: 'El grupo no tiene sistemas integrados activos',
            entonces: 'El campo queda desactivado y explica que primero hay que crear uno en Configuración › Sistemas integrados.',
          },
          { cuando: 'Editas un webhook que ya existe', entonces: 'No vuelve a pedir el sistema: el dueño de un webhook no cambia.' },
        ],
        configuracion: {
          ajuste: 'Sistemas integrados del grupo',
          ruta: 'Configuración › Sistemas integrados (/sistemas)',
          efecto: 'Solo los sistemas activos aparecen como dueños posibles de un webhook.',
        },
      });

      await page.getByRole('option', { name: /ERP de compras · ERP/ }).click();
      await expect(sistema).toContainText('ERP de compras');
      await drawer.getByRole('button', { name: 'Crear webhook' }).click();
      if (await page.getByRole('dialog', { name: 'Confirma que eres tú' }).isVisible({ timeout: 2_000 }).catch(() => false)) await confirmarConPasskey(page);
      const secreto = page.getByRole('dialog', { name: 'Secreto de firma' });
      await expect(secreto).toBeVisible();
      await secreto.getByRole('button', { name: 'Ya lo guardé' }).click();
      const fila = page.getByRole('row', { name: new RegExp(`Ver ${receptor.url.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`) });
      await expect(fila).toBeVisible();
      await paso(page, {
        titulo: 'Revisa el webhook creado',
        explicacion:
          'Al guardar, Validasoft muestra el secreto de firma una sola vez (como en [[integracion/webhooks]]) y el webhook aparece en la tabla, activo y con su dirección. ' +
          'Desde ahora los eventos que elegiste se envían a esa dirección y se le atribuyen al sistema que escogiste.',
        resaltar: fila,
        avisos: true,
      });
    } finally {
      await receptor.cerrar();
    }
  },
);
