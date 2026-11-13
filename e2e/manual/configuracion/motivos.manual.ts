import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO = 'foto-borrosa-id';
const NOMBRE = 'Foto del documento borrosa';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un motivo de observación propio y lo desactiva',
    resumen:
      'La administradora agrega a los motivos del grupo uno propio, «Foto del documento borrosa», con el texto que lee el proveedor. Luego lo desactiva para que los revisores ya no lo puedan elegir.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y los motivos de la plataforma ya cargados.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'motivos');
    await expect(app.getByRole('table', { name: 'Motivos de observación y rechazo' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Documento ilegible|ilegible/i }).first()).toBeVisible();

    await paso(page, {
      titulo: 'Abre Configuración › Motivos',
      explicacion:
        'Un motivo es la razón tipificada por la que un revisor observa o rechaza un expediente. Los de la plataforma son fijos y se marcan con «Plataforma»; el grupo puede agregar los suyos. ' +
        'La columna «Texto al tercero» es lo único que lee el proveedor: nunca ve el nombre del motivo ni quién lo eligió.',
      resaltar: app.getByRole('table', { name: 'Motivos de observación y rechazo' }),
      configuracion: {
        ajuste: 'Motivos de observación y rechazo',
        ruta: 'Configuración › Motivos (/motivos)',
        efecto: 'Los revisores eligen de esta lista cuando observan o rechazan un expediente.',
      },
    });

    await app.getByRole('button', { name: 'Nuevo motivo' }).click();
    const panel = app.locator('[aria-labelledby="titulo-motivo"]');
    await expect(panel).toBeVisible();
    await panel.getByRole('textbox', { name: /^Código/ }).fill(CODIGO);
    await panel.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE);
    await panel.getByRole('combobox', { name: 'Categoría' }).click();
    await app.getByRole('option', { name: 'Documento', exact: true }).click();
    await panel.getByRole('combobox', { name: 'Se usa al' }).click();
    await app.getByRole('option', { name: 'Observar', exact: true }).click();
    await panel.getByRole('textbox', { name: 'Texto al tercero' }).fill('La foto del documento está borrosa. Súbela de nuevo, con buena luz y que se lean todos los datos.');
    await paso(page, {
      titulo: 'Define el motivo y el texto para el proveedor',
      explicacion:
        'El código (minúsculas, números y guiones) y el nombre identifican el motivo para los revisores; el código no se puede cambiar. «Categoría» agrupa el motivo y «Se usa al» dice si sirve para observar, rechazar o ambos. ' +
        'El texto es lo que el proveedor lee: dile qué corregir y cómo, sin nombrar áreas ni personas.',
      resaltar: panel,
      variantes: [
        { cuando: 'Se usa al «Observar»', entonces: 'Los revisores lo pueden elegir para devolver el expediente, pero no para rechazarlo.' },
        { cuando: 'Se usa al «Observar y rechazar»', entonces: 'El motivo aparece en ambas acciones.' },
      ],
    });

    await panel.getByRole('button', { name: 'Crear motivo' }).click();
    await expect(app.getByText('Motivo creado.')).toBeVisible();
    await expect(app.getByRole('row', { name: new RegExp(NOMBRE) })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el motivo en la tabla',
      explicacion:
        'El motivo nuevo aparece con la marca de su categoría y la acción en la que se usa. A diferencia de los de la plataforma, sí puedes editarlo: con el botón de lápiz de su fila.',
      resaltar: app.getByRole('row', { name: new RegExp(NOMBRE) }),
      avisos: true,
    });

    await app.getByRole('button', { name: `Editar ${NOMBRE}` }).click();
    const edicion = app.locator('[aria-labelledby="titulo-motivo"]');
    await expect(edicion).toBeVisible();
    await edicion.getByText('Disponible para los revisores', { exact: true }).click();
    await paso(page, {
      titulo: 'Desactiva el motivo',
      explicacion:
        'El interruptor «Disponible para los revisores» decide si el motivo se ofrece al observar o rechazar. Apagado, el motivo sigue en la tabla para consultar los expedientes que ya lo usaron, pero ningún revisor puede elegirlo.',
      resaltar: edicion,
      variantes: [{ cuando: 'El motivo es de la plataforma', entonces: 'No hay lápiz: los motivos de la plataforma no se modifican.' }],
    });
    await edicion.getByRole('button', { name: 'Guardar' }).click();
    await expect(app.getByText('Motivo actualizado.')).toBeVisible();
    await paso(page, {
      titulo: 'Comprueba el motivo desactivado',
      explicacion:
        'El motivo queda con estado «Inactivo» en la tabla. Los expedientes que ya fueron observados con él no cambian, y desde ahora no aparece en la lista al observar o rechazar.',
      resaltar: app.getByRole('row', { name: new RegExp(NOMBRE) }),
      avisos: true,
    });
  },
);
