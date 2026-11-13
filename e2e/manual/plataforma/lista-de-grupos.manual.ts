import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Busca un grupo y revisa sus empresas',
    resumen:
      'El operador de Plataforma abre la lista de grupos (clientes de Validasoft), la busca por nombre, NIT o empresa y revisa en su tarjeta las empresas del grupo, con su identificación, su prefijo y su plan de IA.',
    precondiciones: [
      'Varios grupos creados, cada uno con sus empresas.',
      'Un operador de Plataforma con la consola abierta.',
    ],
    relacionados: ['plataforma/crear-grupo-y-empresa', 'plataforma/agregar-empresa-a-un-grupo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await crearEscenario();
    const consola = await abrirConsolaDePlataforma(page);
    await expect(consola.getByRole('heading', { level: 1, name: 'Grupos y empresas' })).toBeVisible();
    await expect(consola.getByText(/^Grupo E2E /).first()).toBeVisible();

    await paso(page, {
      titulo: 'Abre Grupos y empresas',
      explicacion:
        'Es la pantalla de inicio de la consola de Plataforma. Cada grupo es un cliente de Validasoft y aparece como una tarjeta con su número de empresas, su fecha de creación y sus acciones: «Plan y límites», «Sistemas» y «Agregar empresa». ' +
        'Con el buscador encuentras un grupo por su nombre, por el nombre o NIT de una de sus empresas.',
      resaltar: consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }),
      configuracion: {
        ajuste: 'Grupos y empresas de la plataforma',
        ruta: 'Plataforma › Grupos y empresas (/plataforma/grupos)',
        efecto: 'Es la lista de clientes que administra la plataforma.',
      },
    });

    await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(e.norte.nombre);
    await expect(consola.getByText(/^Grupo E2E /).first()).toBeVisible();
    await paso(page, {
      titulo: 'Busca un grupo por una de sus empresas',
      explicacion:
        'Escribe parte del nombre del grupo, de una empresa o un NIT. La lista se filtra mientras escribes; si nada coincide, la pantalla dice «Ningún grupo coincide» y ofrece limpiar la búsqueda. ' +
        'Cada tarjeta muestra la tabla de sus empresas, así que basta con reconocer la que buscas.',
      resaltar: consola.getByRole('table').first(),
      variantes: [
        { cuando: 'No coincide nada', entonces: 'Aparece «Ningún grupo coincide con» lo escrito y el botón «Limpiar la búsqueda».' },
      ],
    });

    await paso(page, {
      titulo: 'Revisa las empresas del grupo',
      explicacion:
        'La tabla de cada grupo muestra sus empresas con la identificación (NIT), el prefijo de folio (el que aparece en los números de expediente) y el plan de IA de cada una. ' +
        'Desde la fila, «Plan de IA» abre la configuración del nivel de IA de esa empresa.',
      resaltar: consola.getByRole('table').first(),
      configuracion: {
        ajuste: 'Plan de IA por empresa',
        ruta: 'Plataforma › Grupos y empresas › Plan de IA',
        efecto: 'Define el nivel de IA que puede usar esa empresa.',
      },
    });
  },
);
