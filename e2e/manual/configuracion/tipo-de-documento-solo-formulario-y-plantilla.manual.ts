import { abrirEmbebido } from '../../soporte/embebido';
import { pdf } from '../../soporte/archivos';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE_TIPO = 'Ficha de datos del personal';
const CODIGO_TIPO = 'FICHA_PERSONAL';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un documento con formulario y plantilla descargable',
    resumen:
      'La administradora crea un tipo de documento que el proveedor entrega como archivo y como formulario con datos. Luego sube la plantilla que el proveedor descarga y llena, publica una segunda versión de esa plantilla y finalmente la retira.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y el flujo de Proveedor publicado.',
      'Un archivo PDF que servirá de plantilla (se siembra por la API del sistema de pruebas al subirlo en el paso correspondiente).',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'tipos-documento');
    await expect(app.getByRole('heading', { level: 1, name: 'Tipos de documento' })).toBeVisible();

    // 1. Nuevo tipo.
    await app.getByRole('button', { name: 'Nuevo tipo de documento' }).click();
    const panel = app.locator('[aria-labelledby="titulo-tipo-documento"]');
    await expect(panel).toBeVisible();
    await panel.getByRole('textbox', { name: /^Código/ }).fill(CODIGO_TIPO);
    await panel.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE_TIPO);
    await paso(page, {
      titulo: 'Identifica el documento',
      explicacion:
        'El panel lateral sirve para crear el tipo. Escribe un código en mayúsculas (no cambia después) y el nombre que verá el proveedor. ' +
        'Como es un documento propio, no partes de un documento de la plataforma: defines tú el formato, los campos y la vigencia.',
      resaltar: [panel.getByRole('textbox', { name: /^Código/ }), panel.getByRole('textbox', { name: /^Nombre/ })],
    });

    // 2. Cómo se entrega: archivo y formulario.
    await panel.getByRole('combobox', { name: 'Se entrega como' }).click();
    await app.getByRole('option', { name: 'Archivo y formulario', exact: true }).click();
    const campos = panel.getByRole('list', { name: 'Campos del formulario' });
    const datos = [
      { etiqueta: 'Nombre del responsable', tipo: 'Texto' },
      { etiqueta: 'Número de trabajadores', tipo: 'Número' },
      { etiqueta: 'Fecha de corte', tipo: 'Fecha' },
      { etiqueta: 'Tipo de contrato', tipo: 'Lista', opciones: 'Indefinido\nFijo\nPrestación de servicios' },
      { etiqueta: '¿Tiene SG-SST vigente?', tipo: 'Sí / No' },
    ];
    for (const [i, dato] of datos.entries()) {
      await panel.getByRole('button', { name: 'Agregar campo' }).click();
      const campo = campos.getByRole('listitem').nth(i);
      await campo.getByRole('textbox', { name: 'Etiqueta' }).fill(dato.etiqueta);
      await campo.getByRole('combobox', { name: 'Tipo', exact: true }).click();
      await app.getByRole('option', { name: dato.tipo, exact: true }).click();
      if (dato.opciones) await campo.getByRole('textbox', { name: 'Opciones' }).fill(dato.opciones);
    }
    await expect(campos.getByRole('listitem')).toHaveCount(datos.length);
    await paso(page, {
      titulo: 'Elige cómo lo entrega el proveedor y define sus campos',
      explicacion:
        'En «Se entrega como» eliges si el proveedor sube un archivo, llena un formulario o hace las dos cosas. Con formulario aparece la lista de campos que llenará: cada uno tiene etiqueta, clave, tipo y si es obligatorio. ' +
        'Aquí se usan cinco campos de cinco tipos: texto, número, fecha, lista con sus opciones (una por línea) y Sí / No. Las claves se generan de la etiqueta y las mueves con las flechas del campo.',
      resaltar: [panel.getByRole('combobox', { name: 'Se entrega como' }), campos],
      variantes: [
        { cuando: 'Se entrega como «Archivo»', entonces: 'El proveedor solo sube el archivo; no hay campos ni formulario.' },
        { cuando: 'Se entrega como «Formulario»', entonces: 'El proveedor llena los datos y no sube archivo: no aparecen los límites del archivo ni la sección de plantilla.' },
        { cuando: 'Se entrega como «Archivo y formulario»', entonces: 'El proveedor sube el archivo y llena los datos en la misma entrega; la sección de plantilla sí aparece.' },
      ],
    });

    // 3. Ámbito y límites del archivo.
    await panel.getByRole('checkbox', { name: /PDF/ }).check();
    await panel.getByRole('textbox', { name: 'Peso máximo (MB)' }).fill('5');
    await paso(page, {
      titulo: 'Fija el ámbito y el archivo que se acepta',
      explicacion:
        'Con formulario, el ámbito queda en «Solo de este vínculo»: el documento no se comparte con otras empresas porque lleva datos propios del proveedor para tu relación. ' +
        'Los formatos y el peso del archivo siguen igual que en cualquier documento: aquí solo PDF y hasta 5 MB. El resumen de la tabla mostrará «Formulario (5 campos)» junto al archivo.',
      resaltar: panel.getByRole('textbox', { name: 'Peso máximo (MB)' }),
      configuracion: {
        ajuste: 'Ámbito del documento',
        efecto: 'Un documento con formulario siempre es «Solo de este vínculo» y no se reutiliza con otras empresas.',
      },
    });

    // 4. Crear.
    await panel.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(app.getByText(`Tipo «${NOMBRE_TIPO}» creado.`)).toBeVisible();
    await expect(app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` })).toContainText('Formulario (5 campos)');
    await paso(page, {
      titulo: 'Crea el tipo y revísalo en la tabla',
      explicacion:
        'Al crear, el panel se cierra y la tabla muestra el nuevo tipo con «Formulario (5 campos)» en la columna «Qué recibe». ' +
        'Para subir la plantilla, abre su fila: la sección «Plantilla para descargar» aparece dentro del tipo ya creado.',
      resaltar: app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` }),
      avisos: true,
    });

    // 5. Abrir el tipo y subir la primera plantilla.
    await app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` }).click();
    const edicion = app.locator('[aria-labelledby="titulo-tipo-documento"]');
    await expect(edicion.getByText('Plantilla para descargar', { exact: true })).toBeVisible();
    await expect(edicion.getByText('Sin plantilla.', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la sección de plantilla',
      explicacion:
        'La sección «Plantilla para descargar» dice que el tipo no tiene plantilla. Es un formato que el proveedor descarga, llena, firma y sube de nuevo; puede ser PDF, Word (DOCX) o Excel (XLSX). ' +
        'Aparece solo en los tipos que reciben archivo: un documento que se entrega solo como formulario no tiene plantilla.',
      resaltar: edicion.locator('section').filter({ has: app.getByText('Plantilla para descargar', { exact: true }) }),
      variantes: [{ cuando: 'Se entrega como «Formulario»', entonces: 'La sección no aparece: el proveedor llena los datos en la pantalla de Validasoft.' }],
    });

    await paso(page, {
      titulo: 'Sube la plantilla',
      explicacion:
        'Con «Subir plantilla» eliges el archivo desde tu equipo. Validasoft acepta PDF, DOCX o XLSX; otro formato se rechaza con un aviso. ' +
        'Al subirlo queda como versión 1 con su peso y la fecha de carga, y el proveedor la verá en la pantalla de carga del documento.',
      resaltar: edicion.getByRole('button', { name: 'Subir plantilla', exact: true }),
      variantes: [
        { cuando: 'Subes un archivo que no es PDF, DOCX ni XLSX', entonces: 'La plantilla no se guarda y aparece el aviso «La plantilla debe ser PDF, DOCX o XLSX.»' },
        { cuando: 'Subes el mismo archivo que ya es la plantilla vigente', entonces: 'No se crea una versión nueva; el aviso dice que es el mismo archivo.' },
      ],
    });
    await edicion.locator('input[type="file"]').setInputFiles(pdf('Plantilla ficha de personal version uno'));
    await expect(app.getByText('Plantilla publicada (versión 1).')).toBeVisible();
    await expect(edicion.getByText(/^Versión 1 ·/)).toBeVisible();

    // 6. Publicar una versión nueva y ver el historial.
    await edicion.locator('input[type="file"]').setInputFiles(pdf('Plantilla ficha de personal version dos'));
    await expect(app.getByText('Plantilla publicada (versión 2).')).toBeVisible();
    await edicion.getByText('1 versión anterior', { exact: true }).click();
    await expect(edicion.getByText(/^v1 ·/)).toBeVisible();
    await paso(page, {
      titulo: 'Publica una versión nueva de la plantilla',
      explicacion:
        'Con «Subir una versión nueva» reemplazas la plantilla vigente sin perder la anterior. La versión 2 es la que descarga el proveedor desde ahora; ' +
        'las versiones anteriores quedan en «1 versión anterior», con su nombre, su fecha y un botón para descargarlas.',
      resaltar: [edicion.getByRole('button', { name: 'Subir una versión nueva', exact: true }), edicion.getByText('1 versión anterior', { exact: true })],
      avisos: true,
    });

    // 7. Retirar la plantilla.
    await paso(page, {
      titulo: 'Retira la plantilla',
      explicacion:
        '«Retirar» quita la plantilla vigente: el proveedor deja de verla y de poder descargarla, pero el tipo sigue activo y pide sus datos o su archivo igual. ' +
        'La versión retirada queda en el historial con la fecha en que se retiró, y puedes subir una plantilla nueva en cualquier momento.',
      resaltar: edicion.getByRole('button', { name: 'Retirar', exact: true }),
    });
    await edicion.getByRole('button', { name: 'Retirar', exact: true }).click();
    await expect(app.getByText('Plantilla retirada: el proveedor ya no la ve.')).toBeVisible();
    await expect(edicion.getByText('Sin plantilla.', { exact: true })).toBeVisible();
  },
);
