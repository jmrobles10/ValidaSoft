import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CONTACTO = 'proveedores@constructorasur.co';
const COLOR = '#1F6F5C';
const LOGO_SVG = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 120 120"><rect width="120" height="120" rx="16" fill="${COLOR}"/><text x="60" y="76" font-size="54" font-family="Arial" font-weight="700" text-anchor="middle" fill="#ffffff">CS</text></svg>`;

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Cambia el contacto, el logo y el color de la marca',
    resumen:
      'La administradora actualiza el contacto de soporte, el logo y el color de marca de la empresa. Esos datos son lo que el tercero ve en su portal y en los correos, y lo que aparece en los PDF.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.», sin logo y con el color de marca por omisión.',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/plantillas-de-correo', 'configuracion/dominio-de-correo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'datos-marca');
    await expect(app.getByRole('heading', { level: 1, name: 'Constructora Sur S.A.S.' })).toBeVisible();
    await expect(app.getByRole('button', { name: 'Guardar cambios', exact: true })).toBeDisabled();

    // 1. Lo que se ve y lo que no cambia.
    await paso(page, {
      titulo: 'Abre Configuración › Datos y marca',
      explicacion:
        'La pantalla tiene dos partes. «Datos de la empresa» muestra la razón social, la identificación y el prefijo de folio: se ven, pero no se cambian aquí, porque el prefijo ya aparece en los folios emitidos. ' +
        'La tarjeta «Marca en el portal del tercero» es lo que sí puedes editar: el contacto de soporte, el logo y el color.',
      resaltar: app.getByRole('heading', { name: 'Datos de la empresa', exact: true }),
      configuracion: {
        ajuste: 'Datos y marca de la empresa',
        ruta: 'Configuración › Datos y marca (/datos-marca)',
        efecto: 'Lo que el tercero ve en su portal y en los correos de la empresa.',
      },
      variantes: [{ cuando: 'No tienes permiso para administrar la empresa', entonces: 'La pantalla no muestra la página: solo la administración de la empresa puede cambiar los datos y la marca.' }],
    });

    // 2. Contacto de soporte.
    await app.getByRole('textbox', { name: /Contacto de soporte/ }).fill(CONTACTO);
    await paso(page, {
      titulo: 'Escribe el contacto de soporte',
      explicacion:
        'El contacto de soporte es un correo. Aparece al pie del portal del tercero y es la dirección de respuesta de los correos que envía Validasoft en nombre de la empresa: si el tercero responde a un correo, le llega a esta dirección. ' +
        'Si escribes un correo mal formado, el campo lo marca y el botón «Guardar cambios» no se activa.',
      resaltar: app.getByRole('textbox', { name: /Contacto de soporte/ }),
      variantes: [{ cuando: 'El correo no es válido', entonces: 'El campo dice «Escribe un correo válido» y no se puede guardar.' }],
      configuracion: {
        ajuste: 'Contacto de soporte para terceros',
        ruta: 'Configuración › Datos y marca (/datos-marca)',
        efecto: 'Es la dirección de respuesta de los correos y aparece en el pie del portal.',
      },
    });

    // 3. Logo y color. El archivo se carga sin abrir el selector del sistema.
    await app.locator('[data-testid="entrada-logo"]').setInputFiles({ name: 'logo-sur.svg', mimeType: 'image/svg+xml', buffer: Buffer.from(LOGO_SVG, 'utf8') });
    await expect(app.getByRole('img', { name: 'Logo de Constructora Sur S.A.S.' })).toBeVisible();
    await app.getByRole('textbox', { name: 'Color de marca' }).fill(COLOR);
    await expect(app.getByRole('group', { name: 'Así lo verá el tercero' })).toBeVisible();
    await paso(page, {
      titulo: 'Sube el logo y elige el color de marca',
      explicacion:
        'Con «Cambiar logo» subes un archivo SVG o PNG con fondo transparente, de máximo 200 KB. El color de marca se escribe en hexadecimal, por ejemplo #1F6F5C. ' +
        'La caja «Así lo verá el tercero» muestra un botón y un enlace con los colores que Validasoft deriva de tu color, ajustados para que el texto se lea bien. El logo y el color se usan en el portal, en los correos y en los PDF que genera Validasoft, como el certificado y la declaración PEP.',
      resaltar: [app.getByRole('img', { name: 'Logo de Constructora Sur S.A.S.' }), app.getByRole('group', { name: 'Así lo verá el tercero' })],
      variantes: [
        { cuando: 'El archivo pesa más de 200 KB o no es SVG ni PNG', entonces: 'Aparece el mensaje de error debajo del botón y el logo no cambia.' },
        { cuando: 'El color no tiene el formato #RRGGBB', entonces: 'El campo dice «Usa el formato #RRGGBB» y la vista previa no cambia.' },
        { cuando: 'Quitas el logo', entonces: 'Se usa la marca por omisión de Validasoft en el portal y los correos.' },
      ],
    });

    // 4. Guardar.
    await app.getByRole('button', { name: 'Guardar cambios', exact: true }).click();
    await expect(app.getByText('Datos y marca guardados.')).toBeVisible();
    await expect(app.getByRole('button', { name: 'Guardar cambios', exact: true })).toBeDisabled();
    await paso(page, {
      titulo: 'Guarda los cambios',
      explicacion:
        '«Guardar cambios» se activa solo cuando hay algo distinto de lo guardado. Al guardar aparece el aviso «Datos y marca guardados» y el botón vuelve a quedar desactivado. ' +
        'A partir de ahora, el portal del tercero y los correos nuevos usan el contacto, el logo y el color guardados.',
      resaltar: app.getByRole('button', { name: 'Guardar cambios', exact: true }),
      avisos: true,
    });
  },
);
