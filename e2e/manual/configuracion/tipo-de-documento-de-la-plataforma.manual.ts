import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CODIGO_TIPO = 'CAMARA_SUCURSAL';
const NOMBRE_TIPO = 'Certificado de existencia de la sucursal';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Crea un documento a partir de uno de la plataforma',
    resumen:
      'La administradora parte de la Cédula de ciudadanía, un documento conocido de la plataforma. Validasoft fija el ámbito compartido y los techos de formato y peso; ella intenta un límite más laxo, recibe el error y lo corrige antes de crear el tipo.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y el catálogo de documentos de la plataforma disponible.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo', 'configuracion/tipo-de-documento-solo-formulario-y-plantilla'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'tipos-documento');
    await expect(app.getByRole('heading', { level: 1, name: 'Tipos de documento' })).toBeVisible();
    await expect(app.getByRole('row', { name: /Cédula de ciudadanía/ })).toContainText('de la plataforma');

    // 1. La plataforma marca sus documentos conocidos.
    await paso(page, {
      titulo: 'Identifica los documentos de la plataforma en la tabla',
      explicacion:
        'Los documentos marcados «de la plataforma», como la Cédula de ciudadanía o el RUT, son conocidos: la plataforma sabe qué formatos y pesos son razonables para ellos. ' +
        'Se comparten entre empresas y su ámbito no lo decide cada empresa. Para crear uno nuevo a partir de ellos, abre «Nuevo tipo de documento».',
      resaltar: app.getByRole('row', { name: /Cédula de ciudadanía/ }),
      configuracion: { ajuste: 'Documentos de la plataforma', ruta: 'Configuración › Tipos de documento (/tipos-documento)', efecto: 'Un tipo creado desde uno de la plataforma hereda su ámbito y no puede ser más laxo que sus reglas.' },
    });

    // 2. Elegir el documento de la plataforma.
    await app.getByRole('button', { name: 'Nuevo tipo de documento' }).click();
    const panel = app.locator('[aria-labelledby="titulo-tipo-documento"]');
    await expect(panel).toBeVisible();
    await panel.getByRole('combobox', { name: 'Documento de la plataforma' }).click();
    await app.getByRole('option', { name: /^Certificado de existencia/ }).click();
    await panel.getByRole('textbox', { name: /^Código/ }).fill(CODIGO_TIPO);
    await panel.getByRole('textbox', { name: /^Nombre/ }).fill(NOMBRE_TIPO);
    await expect(panel.getByRole('combobox', { name: 'Ámbito' })).toContainText('Compartido');
    await paso(page, {
      titulo: 'Elige el documento de la plataforma',
      explicacion:
        'El código y el nombre son tuyos: aquí se distingue como el certificado de la sucursal. Sin documento de la plataforma, el tipo es propio y defines el ámbito.',
      resaltar: [panel.getByRole('combobox', { name: 'Documento de la plataforma' }), panel.getByRole('combobox', { name: 'Ámbito' })],
      variantes: [
        { cuando: 'Eliges un documento de la plataforma', entonces: 'El ámbito lo fija la plataforma y no lo puedes cambiar; el formato, el peso y la vigencia no pueden ser más laxos que los suyos.' },
        { cuando: 'Eliges «Ninguno: es un documento propio»', entonces: 'Defines tú el ámbito, el formato, el peso y la vigencia.' },
      ],
    });

    // 3. Techo de formatos.
    await expect(panel.getByText(/La plataforma solo admite PDF/)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa los formatos que admite la plataforma',
      explicacion:
        'La sección «Archivo» muestra lo que la plataforma permite para este documento. Los formatos que no están permitidos aparecen deshabilitados y el texto bajo ellos dice cuáles sí admite la plataforma. ' +
        'Puedes marcar menos formatos que los permitidos, pero no más: el tipo nunca puede aceptar algo que la plataforma no acepta para ese documento.',
      resaltar: panel.getByText(/La plataforma solo admite PDF/),
      variantes: [{ cuando: 'El documento no tiene formatos fijados por la plataforma', entonces: 'El texto dice «Ninguno marcado = PDF, JPG, PNG o WebP» y puedes elegir libremente.' }],
    });

    // 4. Un límite más laxo que el de la plataforma.
    await panel.getByRole('textbox', { name: 'Peso máximo (MB)' }).fill('999');
    await panel.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(panel.getByRole('alert')).toBeVisible();
    await paso(page, {
      titulo: 'Intenta un peso mayor que el de la plataforma',
      explicacion:
        'La plataforma fija un máximo de 20 MB para este documento y no lo muestra en el campo. Escribir 999 MB es un límite más laxo: al crear el tipo, Validasoft lo rechaza, dice cuál es el máximo de la plataforma y el panel queda abierto en rojo. ' +
        'Nada se guarda hasta que corrijas el valor; así el proveedor nunca recibe una regla que la plataforma no permite.',
      resaltar: panel.getByRole('alert'),
      variantes: [{ cuando: 'El peso es igual o menor que el de la plataforma', entonces: 'El tipo se crea sin error.' }],
    });

    // 5. Corregir y crear.
    await panel.getByRole('textbox', { name: 'Peso máximo (MB)' }).fill('2');
    await panel.getByRole('button', { name: 'Crear tipo' }).click();
    await expect(app.getByText(`Tipo «${NOMBRE_TIPO}» creado.`)).toBeVisible();
    await expect(app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` })).toContainText('de la plataforma');
    await paso(page, {
      titulo: 'Corrige el peso y crea el tipo',
      explicacion:
        'Con un peso de 2 MB, dentro del límite de la plataforma, el tipo se crea. La tabla lo muestra con «· de la plataforma» junto al código y con su ámbito compartido. ' +
        'Desde ahora puedes pedirlo en los flujos como cualquier otro documento.',
      resaltar: app.getByRole('row', { name: `Editar ${NOMBRE_TIPO}` }),
      avisos: true,
    });
  },
);
