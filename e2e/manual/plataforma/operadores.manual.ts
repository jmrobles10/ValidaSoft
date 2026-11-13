import { randomUUID } from 'node:crypto';
import { abrirConsolaDePlataforma } from '../../soporte/plataforma';
import { expect, paso, recorrido } from '../nucleo';

recorrido(
  {
    dominio: 'plataforma',
    actor: 'Plataforma',
    titulo: 'Crea un operador de la consola y le quita el acceso',
    resumen:
      'Un operador de Plataforma da de alta a otra persona que operará la consola. Después la desactiva al dejar el puesto: se cierran sus sesiones y se revocan sus passkeys; luego, si hace falta, la reactiva para que registre una passkey nueva.',
    precondiciones: ['Un operador de Plataforma con la consola abierta.', 'Un correo que no esté registrado como operador.'],
    relacionados: ['plataforma/ingreso-del-operador', 'plataforma/ingresos-de-los-operadores', 'plataforma/secretos'],
  },
  async ({ page }) => {
    const sufijo = randomUUID().replace(/-/g, '').slice(0, 6);
    const nombre = `Operador E2E ${sufijo}`;
    const correo = `operador.${sufijo}@e2e.validasoft.co`;

    const consola = await abrirConsolaDePlataforma(page);
    await consola.getByRole('link', { name: 'Operadores' }).click();
    await expect(consola.getByRole('heading', { level: 1, name: 'Operadores' })).toBeVisible();
    await expect(consola.getByRole('table', { name: 'Operadores' })).toBeVisible();

    await paso(page, {
      titulo: 'Abre la lista de operadores',
      explicacion:
        'Los operadores son las personas que manejan la consola de Plataforma. La tabla muestra a cada uno con su correo, cómo entra, cuántas passkeys tiene, su último ingreso y su estado. ' +
        'Un operador de ingreso directo entra con su correo, un código y una passkey obligatoria; uno que entra desde un sistema integrado aparece con el nombre de ese sistema.',
      resaltar: consola.getByRole('table', { name: 'Operadores' }),
      variantes: [
        { cuando: 'Ingreso directo', entonces: 'Entra con «Correo, código y passkey» y la columna de passkeys cuenta las que tiene registradas.' },
        { cuando: 'Ingreso desde un sistema', entonces: 'Entra con «Backoffice» y el nombre del sistema integrado que lo autentica.' },
      ],
      configuracion: {
        ajuste: 'Operadores de la consola',
        ruta: 'Plataforma › Operadores (/plataforma/operadores)',
        efecto: 'Quien aparece aquí puede entrar a la consola de Plataforma según su forma de ingreso.',
      },
    });

    await consola.getByRole('button', { name: 'Nuevo operador' }).click();
    const dialogo = consola.getByRole('dialog', { name: 'Nuevo operador' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: 'Nombre' }).fill(nombre);
    await dialogo.getByRole('textbox', { name: 'Correo' }).fill(correo);
    await paso(page, {
      titulo: 'Da de alta al nuevo operador',
      explicacion:
        'Escribe el nombre y el correo de la persona. Al crearla, Validasoft le envía un correo para entrar: no hay contraseña que compartir. ' +
        'Entra con su correo, un código y una passkey obligatoria, que registra en su primer ingreso.',
      resaltar: [dialogo.getByRole('textbox', { name: 'Nombre' }), dialogo.getByRole('textbox', { name: 'Correo' })],
      variantes: [
        { cuando: 'Falta el nombre', entonces: 'El campo se marca en rojo y no se puede crear.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Crear operador' }).click();
    await expect(consola.getByText('Operador creado: le enviamos el correo para entrar.')).toBeVisible();
    await expect(consola.getByRole('cell', { name: correo })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el operador en la lista',
      explicacion:
        'El operador aparece en la lista como activo, con el correo con el que entrará. Su último ingreso dice «Nunca» hasta que entre por primera vez. ' +
        'Desde el menú de su fila puedes desactivarlo cuando deje el puesto.',
      resaltar: consola.getByRole('row', { name: new RegExp(correo) }),
    });

    await consola.getByRole('button', { name: `Acciones de ${nombre}` }).click();
    await consola.getByRole('menuitem', { name: 'Desactivar' }).click();
    const confirmar = consola.getByRole('dialog', { name: new RegExp(`Desactivar a ${nombre}`) });
    await expect(confirmar).toBeVisible();
    await paso(page, {
      titulo: 'Desactiva a un operador',
      explicacion:
        'Desactivar a un operador le quita la entrada a la consola de inmediato: sus sesiones se cierran, sus passkeys se revocan y su token deja de servir. ' +
        'Su historial se conserva en los ingresos de los operadores. Esto sirve cuando alguien deja el puesto o pierde su acceso.',
      resaltar: confirmar,
      configuracion: {
        ajuste: 'Desactivar un operador',
        efecto: 'Cierra sus sesiones y revoca sus passkeys; no se pierde su historial.',
      },
    });
    await confirmar.getByRole('button', { name: 'Desactivar', exact: true }).click();
    await expect(consola.getByText('Operador desactivado.')).toBeVisible();

    await consola.getByRole('button', { name: `Acciones de ${nombre}` }).click();
    await consola.getByRole('menuitem', { name: 'Reactivar' }).click();
    await consola.getByRole('dialog', { name: new RegExp(`Reactivar a ${nombre}`) }).getByRole('button', { name: 'Reactivar', exact: true }).click();
    await expect(consola.getByText(`${nombre} está activo de nuevo.`)).toBeVisible();
    await paso(page, {
      titulo: 'Reactiva al operador',
      explicacion:
        'Reactivarlo le permite volver a entrar a la consola. Sus sesiones anteriores no se recuperan: recibe un correo para entrar y registrar una passkey nueva. ' +
        'Mientras esté desactivado, su estado aparece como «Desactivado» en la lista.',
      resaltar: consola.getByRole('row', { name: new RegExp(correo) }),
      configuracion: {
        ajuste: 'Reactivar un operador',
        efecto: 'Le envía un correo para entrar y registrar una passkey nueva.',
      },
    });
  },
);
