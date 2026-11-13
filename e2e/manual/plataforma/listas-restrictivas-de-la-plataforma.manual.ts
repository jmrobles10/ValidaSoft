import { CLAVE_PLATAFORMA, llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

// Clave de prueba obvia: no es la de ninguna cuenta real de Infolaft.
const CLAVE_DE_PRUEBA = 'clave-de-prueba-0000';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Configura la cuenta de Infolaft de la plataforma',
    resumen:
      'El operador de Plataforma configura la única cuenta de Infolaft que usan todas las empresas para consultar listas restrictivas. Fija el usuario, la clave, el plan contratado y la cuota mensual por empresa. Las empresas la usan sin ver ni cargar credenciales propias.',
    precondiciones: ['Un operador de Plataforma con la consola abierta.', 'Una empresa del grupo que consulte listas restrictivas.'],
    relacionados: ['plataforma/secretos', 'configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await llamar('PUT', '/api/v1/plataforma/listas-restrictivas', { llave: CLAVE_PLATAFORMA, cuerpo: { credenciales: { proveedor: 'Infolaft', clave: '' } } });

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Listas restrictivas' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Listas restrictivas' })).toBeVisible();
    await expect(consola.getByText('Sin configurar', { exact: true })).toBeVisible();

    await paso(page, {
      titulo: 'Abre la cuenta de la plataforma',
      explicacion:
        'Infolaft es uno solo para todos los clientes: esta es la cuenta de la plataforma. Las empresas consultan con ella sin verla ni cargar la suya. ' +
        'Mientras no esté configurada, las empresas ven Infolaft como «No disponible».',
      resaltar: consola.getByRole('heading', { level: 1, name: 'Listas restrictivas' }),
      configuracion: {
        ajuste: 'Cuenta de Infolaft',
        ruta: 'Plataforma › Listas restrictivas (/plataforma/listas)',
        efecto: 'Es la cuenta con la que consultan todas las empresas; quien no la tenga configurada no puede consultar Infolaft.',
      },
    });

    await consola.getByRole('textbox', { name: 'Usuario de la cuenta' }).fill('usuario-de-prueba');
    await consola.getByLabel('Clave', { exact: true }).fill(CLAVE_DE_PRUEBA);
    await consola.getByRole('textbox', { name: 'Plan contratado' }).fill('Empresarial');
    await consola.getByRole('textbox', { name: 'Cuota mensual de cada empresa' }).fill('800');
    await paso(page, {
      titulo: 'Escribe usuario, clave, plan y cuota',
      explicacion:
        'La clave se cifra con la clave maestra y no se vuelve a mostrar: el campo queda vacío después de guardar. La cuota mensual es el número de búsquedas que cada empresa puede hacer al mes; al llegar a ella, la empresa no consulta más ese mes. ' +
        'El plan contratado es un texto informativo para quien administra la cuenta.',
      resaltar: [
        consola.getByRole('textbox', { name: 'Usuario de la cuenta' }),
        consola.getByLabel('Clave', { exact: true }),
        consola.getByRole('textbox', { name: 'Cuota mensual de cada empresa' }),
      ],
      variantes: [
        { cuando: 'Cuota vacía', entonces: 'Cada empresa consulta sin tope mensual.' },
        { cuando: 'Se modifica la cuenta y se deja la clave vacía', entonces: 'Se conserva la clave actual; «Vacío conserva la actual».' },
      ],
    });

    await consola.getByRole('button', { name: 'Guardar cuenta' }).click();
    await expect(consola.getByText('Guardamos la cuenta de Infolaft.')).toBeVisible();
    await expect(consola.getByText('Configurada · …0000', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Guarda la cuenta y comprueba que quedó configurada',
      explicacion:
        'Al guardar, la cuenta aparece como «Configurada» con los últimos cuatro caracteres de la clave, y el campo de clave se vacía. Ninguna empresa ve este panel: solo Plataforma. ' +
        'Quitar la cuenta deja a todas las empresas sin poder consultar Infolaft hasta que se configure otra.',
      resaltar: consola.getByText('Configurada · …0000', { exact: true }),
      configuracion: {
        ajuste: 'Quitar la cuenta de Infolaft',
        efecto: 'Ninguna empresa podrá consultar listas con Infolaft hasta que se configure otra cuenta.',
      },
    });

    const ventana = await (await browser.newContext()).newPage();
    const empresa = await abrirEmbebido(ventana, e, e.ana, e.norte, 'listas-restrictivas');
    await expect(empresa.getByRole('heading', { level: 1, name: 'Listas restrictivas' })).toBeVisible();
    const infolaft = empresa.getByRole('region', { name: 'Infolaft', exact: true });
    await expect(infolaft.getByText('Disponible', { exact: true })).toBeVisible();
    await expect(infolaft).toContainText('800 búsquedas');
    await paso(ventana, {
      titulo: 'Así la ve una empresa del grupo',
      explicacion:
        'La empresa ve Infolaft como «Disponible» con su cuota mensual, y nada más: no pide usuario ni clave ni muestra la cuenta de la plataforma. ' +
        'Por eso no hay nada que configurar en cada empresa para consultar listas.',
      resaltar: infolaft,
      variantes: [
        { cuando: 'La plataforma no ha configurado la cuenta', entonces: 'La empresa ve Infolaft como «No disponible» y no puede consultar con ella.' },
      ],
    });
  },
);
