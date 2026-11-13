import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO = 'JURIDICA';
const NOMBRE = 'Jurídica';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un área, asígnale personas y desactívala',
    resumen:
      'La administradora de Constructora Sur crea el área Jurídica para una etapa de revisión, agrega a Sofía como miembro y, cuando ya no recibe trabajo, la desactiva. Las áreas son de la empresa y sus miembros deciden quién ve cada expediente.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con Sofía como miembro.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/flujo-etapas-y-areas-en-paralelo', 'configuracion/usuarios-y-roles'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'areas');
    await expect(app.getByRole('heading', { level: 1, name: `Áreas de ${e.sur.nombre}` })).toBeVisible();
    await expect(app.getByRole('button', { name: /^Revisión/ })).toBeVisible();

    // 1. La lista de áreas.
    await paso(page, {
      titulo: 'Abre Configuración › Áreas',
      explicacion:
        'Cada área revisa una etapa de los flujos. La lista muestra las áreas de esta empresa con cuántos miembros tiene cada una; las inactivas aparecen atenuadas con la etiqueta «Inactiva». ' +
        'Las áreas son de la empresa, no del grupo: otra empresa del mismo grupo tiene sus propias áreas.',
      resaltar: app.getByRole('button', { name: /^Revisión/ }),
      configuracion: {
        ajuste: 'Áreas de la empresa y sus miembros',
        ruta: 'Configuración › Áreas (/areas)',
        efecto: 'Solo se pueden elegir áreas activas en los flujos; quién decide en cada una sale de sus miembros.',
      },
    });

    // 2. Crear.
    await app.getByRole('button', { name: 'Nueva área' }).click();
    const dialogo = app.getByRole('dialog', { name: 'Nueva área' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: /^Código/ }).fill(CODIGO);
    await dialogo.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE);
    await paso(page, {
      titulo: 'Crea el área',
      explicacion:
        'El código es corto y sin espacios (por ejemplo COMPRAS) y no se puede cambiar después; el nombre es el que ven los flujos y los expedientes. ' +
        'Al crearla, el área queda activa y sin miembros: todavía no decide nada hasta que le asignes personas.',
      resaltar: dialogo,
      variantes: [{ cuando: 'El código ya lo usa otra área de la empresa', entonces: 'La pantalla dice que la empresa ya tiene un área con ese código y no la crea.' }],
    });
    await dialogo.getByRole('button', { name: 'Crear área' }).click();
    await expect(app.getByText('Área creada.')).toBeVisible();

    // 3. Asignar miembros.
    await app.getByRole('button', { name: new RegExp(`^${NOMBRE}`) }).click();
    await expect(app.getByText('Esta área aún no tiene miembros.')).toBeVisible();
    await app.getByRole('combobox', { name: 'Agregar persona por nombre o correo' }).click();
    await app.getByRole('combobox', { name: 'Agregar persona por nombre o correo' }).fill(e.sofia.nombre);
    await app.getByRole('option', { name: new RegExp(e.sofia.nombre) }).click();
    await app.getByRole('button', { name: 'Agregar', exact: true }).click();
    await expect(app.getByText('Miembros actualizados.')).toBeVisible();
    await paso(page, {
      titulo: 'Agrega personas al área',
      explicacion:
        'Escribe el nombre o el correo de una persona y elígela. Solo aparecen personas que ya tienen un rol en esta empresa; si no encuentras a alguien, primero debes darle acceso desde «Usuarios y roles». ' +
        'Cada miembro aparece con su rol en la empresa. Un miembro del área ve en la bandeja los expedientes que le toca revisar a esa área; quien no es miembro no los ve.',
      resaltar: [app.getByRole('combobox', { name: 'Agregar persona por nombre o correo' }), app.getByRole('table', { name: 'Miembros' })],
      variantes: [{ cuando: 'La persona ya es miembro del área', entonces: 'No aparece en la lista de candidatos y la pantalla no la vuelve a agregar.' }],
      configuracion: { ajuste: 'Miembros del área', ruta: 'Configuración › Áreas (/areas)', efecto: 'Quién es miembro decide qué expedientes aparecen en la bandeja de revisión.' },
    });

    // 4. Desactivar.
    await app.getByRole('button', { name: 'Editar área' }).click();
    const edicion = app.getByRole('dialog', { name: `Editar ${NOMBRE}` });
    await expect(edicion).toBeVisible();
    await edicion.getByText('Activa', { exact: true }).click();
    await paso(page, {
      titulo: 'Desactiva el área',
      explicacion:
        'Al editar un área puedes desactivarla con el interruptor «Activa». Una área inactiva no recibe trabajo nuevo: los flujos no la ofrecen para etapas nuevas y sus miembros dejan de recibir expedientes. ' +
        'Los expedientes que ya tenía siguen su curso y puedes volver a activarla en cualquier momento.',
      resaltar: edicion,
      variantes: [{ cuando: 'El área está activa y tiene miembros', entonces: 'Desactivarla no quita a sus miembros: al reactivarla siguen asignados.' }],
    });
    await edicion.getByRole('button', { name: 'Guardar' }).click();
    await expect(app.getByText('Área actualizada.')).toBeVisible();
    await expect(app.getByRole('button', { name: new RegExp(`^${NOMBRE}`) })).toContainText('Inactiva');
    await paso(page, {
      titulo: 'Comprueba que el área quedó inactiva',
      explicacion:
        'En la lista, el área aparece atenuada y con la etiqueta «Inactiva» en lugar del número de miembros. Su ficha muestra el aviso de que no recibe trabajo nuevo, y se reactiva con «Editar área».',
      resaltar: app.getByRole('button', { name: new RegExp(`^${NOMBRE}`) }),
      avisos: true,
    });
  },
);
