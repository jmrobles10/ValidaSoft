import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Laura Restrepo';
const CORREO = 'laura.restrepo@constructorasur.co';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Agrega una persona, dale roles y desactívala',
    resumen:
      'La administradora agrega a una persona al grupo con un rol en una empresa y otro para todo el grupo, cambia sus roles y después la desactiva. Las personas entran desde su sistema; aquí se decide qué puede hacer cada una.',
    precondiciones: [
      'Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.», y personas con roles (Rubén, Olga, Sofía).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/permisos-por-rol', 'configuracion/areas-y-miembros'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'usuarios');
    await expect(app.getByRole('heading', { level: 1, name: 'Usuarios y roles' })).toBeVisible();
    await expect(app.getByRole('table', { name: 'Usuarios y roles' })).toBeVisible();

    // 1. La lista de personas del grupo.
    await paso(page, {
      titulo: 'Abre Configuración › Usuarios y roles',
      explicacion:
        'La tabla lista las personas del grupo con sus roles, agrupados por empresa: «Todo el grupo» aplica en todas las empresas y, si el rol es para una empresa, aparece con su nombre. ' +
        'La columna «Entra desde» muestra el sistema integrado por el que entra la persona. Un filtro separa las activas de las desactivadas.',
      resaltar: app.getByRole('table', { name: 'Usuarios y roles' }),
      configuracion: {
        ajuste: 'Usuarios y roles',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Decide qué puede hacer cada persona en cada empresa del grupo.',
      },
      variantes: [{ cuando: 'Una persona no tiene roles', entonces: 'Aparece «Sin roles» y no puede hacer nada en Validasoft hasta que le agregues al menos uno.' }],
    });

    // 2. Datos y roles de una persona nueva. Se documenta el panel antes de guardar.
    await app.getByRole('button', { name: 'Agregar persona' }).first().click();
    const panel = app.locator('[aria-labelledby="titulo-roles"]');
    await expect(panel).toBeVisible();
    await panel.getByLabel('Nombre').first().fill(NOMBRE);
    await panel.getByLabel('Correo').first().fill(CORREO);
    // El panel nace sin filas: la primera es Revisor de Constructora Sur; la segunda, Auditor de todo el grupo.
    // Solo la primera fila de «Empresa» y «Rol» tiene nombre accesible (las demás llevan el `aria-label` en el contenedor): se llega por él.
    const empresaDeLaFila = (n: number) => panel.locator('[aria-label="Empresa"]').nth(n).getByRole('combobox');
    const rolDeLaFila = (n: number) => panel.locator('[aria-label="Rol"]').nth(n).getByRole('combobox');
    await panel.getByRole('button', { name: 'Agregar rol' }).click();
    await empresaDeLaFila(0).click();
    await app.getByRole('option', { name: 'Constructora Sur S.A.S.', exact: true }).click();
    await panel.getByRole('button', { name: 'Agregar rol' }).click();
    await empresaDeLaFila(1).click();
    await app.getByRole('option', { name: 'Todo el grupo', exact: true }).click();
    await rolDeLaFila(1).click();
    await app.getByRole('option', { name: 'Auditor', exact: true }).click();
    await paso(page, {
      titulo: 'Agrega una persona con sus roles',
      explicacion:
        'Escribe el nombre y el correo de la persona. Luego agrega un rol por cada empresa o uno para todo el grupo: cada fila elige la empresa (o «Todo el grupo») y el rol. ' +
        'Los roles son Administrador, Revisor, Oficial de cumplimiento y Auditor. Aquí la persona es Revisora en Constructora Sur y Auditora de todo el grupo. Sin ningún rol la persona no puede hacer nada en Validasoft.',
      resaltar: [panel.getByLabel('Nombre').first(), panel.locator('[aria-label="Rol"]').nth(1)],
      variantes: [
        { cuando: 'El grupo admite el ingreso directo', entonces: 'Aparece la casilla «Enviar una invitación por correo», marcada por omisión: la persona recibe un correo para entrar con código y passkey.' },
        { cuando: 'El grupo no admite el ingreso directo', entonces: 'La casilla aparece desactivada y dice que hay que activar el ingreso directo en Configuración › Ingreso.' },
      ],
      configuracion: {
        ajuste: 'Roles por empresa',
        ruta: 'Configuración › Usuarios y roles (/usuarios)',
        efecto: 'Los cambios aplican en el próximo ingreso de la persona desde su sistema y quedan en la auditoría.',
      },
    });
    await panel.getByRole('button', { name: 'Agregar persona' }).click();
    await expect(app.getByText('Persona agregada.')).toBeVisible();
    await expect(app.getByRole('row').filter({ hasText: NOMBRE })).toBeVisible();

    // 3. El resultado en la tabla.
    const fila = app.getByRole('row').filter({ hasText: NOMBRE });
    await paso(page, {
      titulo: 'Revisa la nueva persona en la tabla',
      explicacion:
        'La persona aparece en la tabla con sus roles, cada uno con su empresa debajo. Como todavía no entró por su sistema, la columna «Último ingreso» dice «Nunca». ' +
        'Para cambiar sus roles más tarde, abre el menú de su fila y elige «Editar roles».',
      resaltar: fila,
      avisos: true,
    });

    // 4. Cambiar un rol. Se documenta el panel antes de guardar.
    await fila.getByRole('button', { name: `Acciones de ${NOMBRE}` }).click();
    await app.getByRole('menuitem', { name: 'Editar roles' }).click();
    const edicion = app.locator('[aria-labelledby="titulo-roles"]');
    await expect(edicion.getByRole('heading', { name: 'Editar roles' })).toBeVisible();
    await edicion.locator('[aria-label="Rol"]').nth(1).getByRole('combobox').click();
    await app.getByRole('option', { name: 'Oficial de cumplimiento', exact: true }).click();
    await paso(page, {
      titulo: 'Cambia los roles de la persona',
      explicacion:
        'El panel «Editar roles» muestra la persona y sus filas de roles, con los mismos controles que al agregarla. Quitar una fila con la X y agregar otra cambia sus permisos. ' +
        'En este caso cambiamos su rol para todo el grupo a Oficial de cumplimiento. El cambio no se aplica en su sesión actual: se nota en su próximo ingreso.',
      resaltar: edicion.locator('[aria-label="Rol"]').nth(1),
      variantes: [{ cuando: 'Quitas todas las filas de roles', entonces: 'La pantalla avisa que sin roles la persona no puede hacer nada en Validasoft.' }],
    });
    await edicion.getByRole('button', { name: 'Guardar roles' }).click();
    await expect(app.getByText('Roles actualizados.')).toBeVisible();
    await expect(fila).toContainText('Oficial de cumplimiento');
    await paso(page, {
      titulo: 'Confirma los roles nuevos',
      explicacion:
        'Al guardar, el panel se cierra y la fila muestra los roles nuevos. Los cambios quedan registrados en la auditoría de configuración con quién los hizo.',
      resaltar: fila,
      avisos: true,
    });

    // 5. Desactivar. El diálogo se confirma y se documenta al abrirlo.
    await fila.getByRole('button', { name: `Acciones de ${NOMBRE}` }).click();
    await app.getByRole('menuitem', { name: 'Editar roles' }).click();
    await app.locator('[aria-labelledby="titulo-roles"]').getByRole('button', { name: 'Desactivar', exact: true }).click();
    const confirmacion = app.getByRole('dialog', { name: `Desactivar a ${NOMBRE}` });
    await expect(confirmacion).toBeVisible();
    await paso(page, {
      titulo: 'Desactiva a la persona',
      explicacion:
        'Desactivar a una persona la deja sin acceso a ninguna empresa del grupo desde ningún sistema. Su historial se conserva: lo que hizo sigue en los expedientes y en la auditoría.',
      resaltar: confirmacion,
      variantes: [{ cuando: 'La persona está desactivada', entonces: 'Su fila sale atenuada con «Desactivada» y ya no aparece en el filtro de activas.' }],
    });
    await confirmacion.getByRole('button', { name: 'Desactivar', exact: true }).click();
    await expect(app.getByText('Persona desactivada.')).toBeVisible();
    await app.getByRole('button', { name: /^Estado/ }).click();
    await app.getByRole('menuitem', { name: 'Desactivadas', exact: true }).click();
    await expect(app.getByRole('row').filter({ hasText: NOMBRE })).toContainText('Desactivada');
    await paso(page, {
      titulo: 'Comprueba que la persona quedó desactivada',
      explicacion:
        'Con el filtro (1) en «Desactivadas» se ve la persona (2) con el estado «Desactivada». Desde ese momento ya no puede entrar a ninguna empresa del grupo.',
      resaltar: [app.getByRole('button', { name: /^Estado/ }), app.getByRole('row').filter({ hasText: NOMBRE })],
    });
  },
);
