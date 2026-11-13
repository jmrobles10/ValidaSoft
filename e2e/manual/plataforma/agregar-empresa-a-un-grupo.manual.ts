import { randomUUID } from 'node:crypto';
import { nitAleatorio } from '../../soporte/api';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Agrega una empresa a un grupo existente',
    resumen:
      'El operador de Plataforma busca un grupo ya creado y le agrega otra empresa con el mismo formulario del alta, sin volver a crear el grupo. La empresa nueva aparece en la tabla del grupo, junto a las que ya tenía.',
    precondiciones: ['Un grupo creado con al menos una empresa.', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/crear-grupo-y-empresa', 'plataforma/lista-de-grupos'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grupo = `Grupo E2E ${e.sufijo}`;
    const nueva = 'Obras del Sur S.A.S.';
    const prefijo = `S${randomUUID().replace(/-/g, '').slice(0, 5).toUpperCase()}`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('searchbox', { name: 'Buscar por grupo, empresa o NIT' }).fill(grupo);
    await expect(consola.getByRole('region', { name: grupo })).toBeVisible();

    await paso(page, {
      titulo: 'Busca el grupo y pulsa «Agregar empresa»',
      explicacion:
        'Cada grupo tiene en su tarjeta el botón «Agregar una empresa». Con él no se crea un grupo nuevo: la empresa entra al grupo que ya existe. ' +
        'Si no ves el grupo, búscalo por su nombre o por el NIT de una de sus empresas.',
      resaltar: consola.getByRole('button', { name: `Agregar una empresa a ${grupo}` }),
      configuracion: {
        ajuste: 'Empresas de un grupo',
        ruta: 'Plataforma › Grupos y empresas › Agregar empresa',
        efecto: 'Suma una empresa al grupo; la tabla del grupo la muestra con su NIT, su prefijo y su plan de IA.',
      },
    });
    await consola.getByRole('button', { name: `Agregar una empresa a ${grupo}` }).click();

    const dialogo = consola.getByRole('dialog', { name: /Agregar empresa/ });
    await expect(dialogo.getByText(`Para el grupo ${grupo}`)).toBeVisible();
    await dialogo.getByRole('textbox', { name: /Número de identificación/ }).fill(nitAleatorio().numero);
    await dialogo.getByRole('textbox', { name: /Razón social/ }).fill(nueva);
    await dialogo.getByRole('textbox', { name: /Prefijo de folio/ }).fill(prefijo);

    await paso(page, {
      titulo: 'Escribe los datos de la nueva empresa',
      explicacion:
        'El diálogo usa el mismo formulario de la primera empresa del alta: país y tipo de identificación, número, razón social y prefijo de folio. El prefijo es el que aparece en los números de sus expedientes y no cambia después, así que elige uno corto y reconocible. ',
      resaltar: [
        dialogo.getByRole('textbox', { name: /Número de identificación/ }),
        dialogo.getByRole('textbox', { name: /Razón social/ }),
        dialogo.getByRole('textbox', { name: /Prefijo de folio/ }),
      ],
      variantes: [
        { cuando: 'El número no tiene la forma del tipo elegido', entonces: 'El campo lo indica debajo y no deja agregar la empresa.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Agregar empresa' }).click();
    await expect(dialogo).toBeHidden();
    await expect(consola.getByRole('region', { name: grupo }).getByText(nueva)).toBeVisible();

    await paso(page, {
      titulo: 'Comprueba que la empresa está en el grupo',
      explicacion:
        'La tabla del grupo ahora incluye la empresa nueva junto a las que ya tenía, con su identificación, su prefijo y su plan de IA. Desde su fila puedes configurar ese plan igual que para las demás. ' +
        'El grupo no cambió de nombre ni de identificador: solo creció.',
      resaltar: consola.getByRole('region', { name: grupo }).getByRole('table'),
      configuracion: {
        ajuste: 'Plan de IA por empresa',
        ruta: 'Plataforma › Grupos y empresas › Plan de IA',
        efecto: 'Define el nivel de IA que puede usar esa empresa.',
      },
    });
  },
);
