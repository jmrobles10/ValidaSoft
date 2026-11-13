import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO = 'CONTRATISTA';
const NOMBRE = 'Contratista';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un tipo de vínculo y lo desactiva',
    resumen:
      'La administradora crea el tipo de vínculo «Contratista» con dos exigencias: cuenta bancaria designada y un código por correo en el enlace de invitación. Después lo desactiva para que no se usen más flujos con él.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y los tipos de vínculo base (Proveedor, Cliente).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'tipos-vinculo');
    await expect(app.getByRole('heading', { level: 1, name: 'Tipos de vínculo' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Proveedor/ })).toBeVisible();

    // 1. La lista de tipos de vínculo.
    await paso(page, {
      titulo: 'Abre Configuración › Tipos de vínculo',
      explicacion:
        'Un tipo de vínculo es la naturaleza de la relación con un tercero: proveedor, cliente, empleado. La tabla muestra cada tipo, cuántos atributos registra, si exige cuenta designada o código en el enlace, y si está activo. ' +
        'Los flujos aprobatorios se arman por tipo de vínculo, así que un tipo nuevo no sirve hasta que tenga su flujo.',
      resaltar: app.getByRole('table', { name: 'Tipos de vínculo' }),
      configuracion: {
        ajuste: 'Tipos de vínculo',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Cada invitación y cada flujo pertenecen a un tipo de vínculo.',
      },
    });

    // 2. Datos del tipo.
    await app.getByRole('button', { name: 'Nuevo tipo de vínculo' }).click();
    const panel = app.locator('[aria-labelledby="titulo-tipo-vinculo"]');
    await expect(panel).toBeVisible();
    await panel.getByRole('textbox', { name: /^Código/ }).fill(CODIGO);
    await panel.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE);
    await paso(page, {
      titulo: 'Escribe el código y el nombre del tipo',
      explicacion:
        'El código empieza con letra y solo lleva mayúsculas, números y guion bajo; mientras escribes se convierte a mayúsculas. Una vez creado no se puede cambiar, porque los flujos y las invitaciones lo usan. ' +
        'El nombre es el que ves en la tabla y en los flujos.',
      resaltar: [panel.getByRole('textbox', { name: /^Código/ }), panel.getByRole('textbox', { name: /^Nombre/ })],
      variantes: [{ cuando: 'El código ya existe en el grupo', entonces: 'Al crear, la pantalla dice que ya existe un tipo de vínculo con ese código y no lo guarda.' }],
    });

    // 3. Exigencias.
    await panel.getByText('Exige cuenta bancaria designada', { exact: true }).click();
    await panel.getByText('Exige un código por correo en el enlace', { exact: true }).click();
    await paso(page, {
      titulo: 'Activa las exigencias del vínculo',
      explicacion:
        '«Exige cuenta bancaria designada» hace que el expediente del vínculo no se apruebe hasta que haya una cuenta designada aprobada por dos personas de la empresa: aunque todas las áreas aprueben, queda «En revisión» con el aviso «Falta la cuenta bancaria designada» y se cierra solo al aprobarse la cuenta ([[vinculos/cuenta-designada-aprobar]]). «Exige un código por correo en el enlace» pide al tercero, además del enlace de la invitación, un código que le llega a su correo. ' +
        'Son reglas del tipo: todos los vínculos de este tipo las cumplen, sin importar el flujo que usen.',
      resaltar: [
        panel.getByText('Exige cuenta bancaria designada', { exact: true }),
        panel.getByText('Exige un código por correo en el enlace', { exact: true }),
      ],
      variantes: [
        { cuando: 'Activas «Exige cuenta bancaria designada»', entonces: 'El proveedor ve en su revisión el aviso «Falta tu cuenta bancaria designada» hasta que la empresa apruebe la cuenta que designe, y el expediente solo se aprueba entonces.' },
        { cuando: 'Activas «Exige código en el enlace»', entonces: 'Al aceptar la invitación, el tercero escribe el código que le llega al correo antes de ver el expediente.' },
        { cuando: 'No activas ninguna', entonces: 'El tercero acepta solo con el enlace de la invitación, como en los demás vínculos.' },
      ],
    });

    // 4. Crear.
    await panel.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(app.getByText(`Tipo de vínculo ${NOMBRE} creado.`)).toBeVisible();
    const fila = app.getByRole('row', { name: new RegExp(NOMBRE) });
    await expect(fila).toContainText('Cuenta designada');
    await expect(fila).toContainText('Código en el enlace');
    await paso(page, {
      titulo: 'Crea el tipo y revísalo en la tabla',
      explicacion:
        'Al crear, el panel se cierra y el nuevo tipo aparece activo, con sus exigencias en la columna «Exige». Para cambiar el nombre o las exigencias más tarde, abre el menú de la fila y elige «Editar»; el código ya no se puede editar.',
      resaltar: fila,
      avisos: true,
    });

    // 5. Desactivar.
    await fila.getByRole('button', { name: `Acciones de ${NOMBRE}` }).click();
    await app.getByRole('menuitem', { name: 'Desactivar tipo' }).click();
    const confirmacion = app.getByRole('dialog', { name: `Desactivar ${NOMBRE}` });
    await expect(confirmacion).toBeVisible();
    await paso(page, {
      titulo: 'Desactiva el tipo',
      explicacion:
        'Desactivar impide crear flujos nuevos con este tipo. Lo que ya existe no cambia: los vínculos y los flujos que ya lo usan siguen igual. La fila queda atenuada con el estado «Inactivo» y puede reactivarse desde el mismo menú.',
      resaltar: confirmacion,
      variantes: [{ cuando: 'El tipo está inactivo', entonces: 'El menú de la fila ofrece «Activar tipo» en lugar de «Desactivar tipo».' }],
    });
    await confirmacion.getByRole('button', { name: 'Desactivar tipo' }).click();
    await expect(app.getByText(`${NOMBRE} quedó inactivo.`)).toBeVisible();
    await expect(fila).toContainText('Inactivo');
    await paso(page, {
      titulo: 'Comprueba que el tipo quedó inactivo',
      explicacion:
        'La tabla ordena primero los tipos activos y el tipo desactivado queda al final con su estado «Inactivo». Desde ahora no aparece como opción al crear un flujo nuevo.',
      resaltar: fila,
      avisos: true,
    });
  },
);
