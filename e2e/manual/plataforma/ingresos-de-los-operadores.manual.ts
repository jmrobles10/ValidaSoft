import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Revisa quién entró a la consola y filtra los ingresos fallidos',
    resumen:
      'El operador de Plataforma revisa la bitácora de ingresos de los operadores: quién entró, quién falló al entrar, quién registró o revocó una passkey y quién confirmó una acción sensible. Filtra por resultado para ver solo los fallidos y luego limpia los filtros.',
    precondiciones: ['Varios ingresos de operadores registrados (exitosos y fallidos).', 'Un operador de Plataforma con la consola abierta.'],
    relacionados: ['plataforma/operadores', 'plataforma/ingreso-del-operador'],
  },
  async ({ page }) => {
    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Ingresos' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Ingresos de los operadores' })).toBeVisible();
    await expect(consola.getByRole('table', { name: 'Ingresos de los operadores' })).toBeVisible();

    await paso(page, {
      titulo: 'Abre la bitácora de ingresos',
      explicacion:
        'Cada fila es un evento de la consola: quién lo hizo, cuándo y con qué resultado. Aparecen primero los más recientes. ' +
        'Se registran los ingresos exitosos y fallidos, los códigos enviados o incorrectos, el registro y la revocación de passkeys, y las acciones sensibles confirmadas.',
      resaltar: consola.getByRole('table', { name: 'Ingresos de los operadores' }),
      variantes: [
        { cuando: 'Ingreso fallido', entonces: 'La fila aparece con resultado «Fallido» y el evento que lo explica, por ejemplo un código incorrecto.' },
        { cuando: 'Ingreso exitoso', entonces: 'La fila aparece con resultado «Exitoso».' },
      ],
      configuracion: {
        ajuste: 'Bitácora de ingresos de los operadores',
        ruta: 'Plataforma › Ingresos (/plataforma/ingresos)',
        efecto: 'Es el registro de quién entra a la consola y de sus acciones sensibles.',
      },
    });

    await consola.getByRole('button', { name: /Resultado/ }).click();
    await consola.getByRole('menuitem', { name: 'Fallido' }).click();
    await expect(consola.getByRole('button', { name: 'Limpiar filtros' })).toBeVisible();
    await paso(page, {
      titulo: 'Filtra solo los ingresos fallidos',
      explicacion:
        'Con el filtro «Resultado» eliges «Fallido» para ver solo los intentos que no pasaron. Los demás filtros, como operador, evento o fechas («Desde» y «Hasta»), se combinan con este.',
      resaltar: consola.getByRole('button', { name: /Resultado/ }),
      configuracion: {
        ajuste: 'Filtros de la bitácora',
        efecto: 'Los filtros solo cambian la vista; no modifican los eventos registrados.',
      },
    });

    await consola.getByRole('button', { name: 'Limpiar filtros' }).click();
    await paso(page, {
      titulo: 'Limpia los filtros',
      explicacion:
        'El botón «Limpiar filtros» quita todos los filtros y vuelve a mostrar la bitácora completa, con los eventos más recientes primero.',
      resaltar: consola.getByRole('table', { name: 'Ingresos de los operadores' }),
    });
  },
);
