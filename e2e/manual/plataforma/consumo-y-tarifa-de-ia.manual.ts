import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Revisa el uso de IA de los clientes y cambia la tarifa del adicional',
    resumen:
      'El operador de Plataforma revisa cuánto consumió cada cliente en el mes en validaciones con IA: las incluidas en su plan, las adicionales, los tokens y el costo del proveedor. Luego cambia la tarifa general con la que se cobra cada validación adicional.',
    precondiciones: ['Un grupo con empresas, con o sin consumo de IA en el mes.', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/plan-de-ia-y-llave-openai', 'plataforma/plan-del-grupo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const grupo = `Grupo E2E ${e.sufijo}`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Uso de IA' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Uso de IA' })).toBeVisible();
    await expect(consola.getByText(/Total de /).first()).toBeVisible();

    await paso(page, {
      titulo: 'Abre Uso de IA y elige el mes',
      explicacion:
        'La pantalla lista, para cada grupo y empresa, lo que consumió en el mes elegido: validaciones incluidas y adicionales, tokens, costo del proveedor y el valor de lo adicional. Arriba, el mes define el período de todas las cifras; el buscador filtra por grupo o empresa. ' +
        'Los totales del mes suman todos los clientes con consumo.',
      resaltar: consola.getByText(/Total de /).first(),
      configuracion: {
        ajuste: 'Mes del uso de IA',
        ruta: 'Plataforma › Uso de IA',
        efecto: 'Cambia el período de los totales y de la tabla; el archivo exportado sale con el mismo mes.',
      },
    });

    await paso(page, {
      titulo: 'Lee el consumo de cada cliente',
      explicacion:
        'Cada fila es un grupo, con sus empresas plegadas: «Incluidas usadas» cuenta las validaciones del plan, «Adicionales» las que se cobran aparte, y «Tokens» separa entrada y salida. ' +
        'El costo en dólares es el que reporta el proveedor de IA por los tokens usados. «Valor adicional» es lo consumido por encima del plan, a la tarifa de cada empresa. Si ningún cliente consumió en el mes, la pantalla lo dice.',
      resaltar: consola.getByRole('table').first(),
      variantes: [
        {
          cuando: 'La empresa tiene precio propio por adicional',
          entonces: 'La fila lo muestra como «Precio propio» y su valor adicional se calcula con ese precio, no con la tarifa general.',
        },
        {
          cuando: 'La empresa no permite adicionales',
          entonces: 'Aparece «Sin adicionales» y su valor adicional es cero.',
        },
        {
          cuando: 'No hay consumo en el mes',
          entonces: 'La pantalla dice «Nadie consumió IA» en ese mes y explica que el consumo aparece cuando una empresa con plan valida documentos.',
        },
      ],
    });

    await consola.getByRole('button', { name: 'Cambiar', exact: true }).click();
    const tarifa = consola.getByRole('dialog', { name: 'Cambiar la tarifa general' });
    await expect(tarifa).toBeVisible();
    await tarifa.getByLabel('Precio').fill('1750');
    await paso(page, {
      titulo: 'Cambia la tarifa general del adicional',
      explicacion:
        'La tarifa general es lo que se cobra por cada validación que una empresa consume por encima de las incluidas en su plan. Escribes el precio (hasta dos decimales) y la moneda en código ISO, como COP o USD. ' +
        'Se aplica a las empresas que no tienen un precio propio; una empresa con precio propio en su plan de IA conserva el suyo.',
      resaltar: [tarifa.getByLabel('Precio'), tarifa.getByLabel('Moneda')],
      variantes: [
        { cuando: 'El precio queda en cero', entonces: 'Lo adicional no se valora: la pantalla avisa que aún no hay tarifa.' },
        { cuando: 'El precio tiene más de dos decimales o es negativo', entonces: 'El diálogo indica que el precio va entre 0 y 100 000 000, con hasta dos decimales.' },
      ],
      configuracion: {
        ajuste: 'Tarifa general del adicional',
        ruta: 'Plataforma › Uso de IA',
        efecto: 'Es el precio por defecto de las validaciones adicionales de todas las empresas sin precio propio.',
      },
    });

    await tarifa.getByRole('button', { name: 'Guardar', exact: true }).click();
    await expect(consola.getByText('Guardaste la tarifa.')).toBeVisible();
    await expect(consola.getByText(/1\.750/).first()).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba la tarifa vigente',
      explicacion:
        'Validasoft confirma que guardó la tarifa y la muestra con su fecha y quién la cambió. Desde el próximo cálculo, el valor de lo adicional de las empresas sin precio propio usa este precio. ' +
        'Las empresas con precio propio no cambian.',
      resaltar: consola.getByText('por validación adicional').first(),
    });
  },
);
