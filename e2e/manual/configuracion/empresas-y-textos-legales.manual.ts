import { nitAleatorio } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const RAZON_SOCIAL = 'Constructora Oriente S.A.S.';
const PREFIJO = 'ORIENTE';
const TEXTO_AUTORIZACION = 'Autorizo a Constructora Oriente S.A.S. a tratar mis datos personales para validar mi vinculación como proveedor, conforme a la política de tratamiento publicada.';
const TEXTO_TERMINOS = 'Acepto los términos de vinculación con Constructora Oriente S.A.S., incluida la obligación de mantener actualizados mis documentos y mis datos.';

recorrido(
  {
    dominio: 'configuracion',
    actor: 'Administrador de empresa',
    titulo: 'Agrega una empresa del grupo y publica sus textos legales',
    resumen:
      'La administradora agrega una razón social nueva al grupo y publica la autorización de datos y los términos que los terceros deben aceptar antes de vincularse. Sin ambos textos, la empresa no puede recibir aceptaciones.',
    precondiciones: [
      'Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.».',
      'Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const app = await abrirEmbebido(page, e, e.ana, e.sur, 'empresas');
    await expect(app.getByRole('heading', { level: 1, name: 'Empresas del grupo' })).toBeVisible();
    await expect(app.getByRole('table', { name: 'Empresas del grupo' })).toBeVisible();

    // 1. Las empresas del grupo.
    await paso(page, {
      titulo: 'Abre Configuración › Empresas del grupo',
      explicacion:
        'La tabla lista las razones sociales del grupo con su identificación, su prefijo de folio y si están activas. Cada empresa decide por su cuenta: no ve las decisiones de las otras, aunque compartan usuarios y configuración del grupo. ' +
        'Al elegir una fila se abre debajo el detalle de esa empresa, con sus textos legales.',
      resaltar: app.getByRole('table', { name: 'Empresas del grupo' }),
      configuracion: {
        ajuste: 'Empresas del grupo',
        ruta: 'Configuración › Empresas del grupo (/empresas)',
        efecto: 'Cada empresa tiene sus propios flujos, áreas, textos legales y folios.',
      },
      variantes: [{ cuando: 'Una empresa está inactiva', entonces: 'Su fila sale con el estado «Inactiva» y sus textos legales no se pueden publicar de nuevo.' }],
    });

    // 2. Datos de la empresa nueva.
    await app.getByRole('button', { name: 'Agregar empresa', exact: true }).click();
    const dialogo = app.getByRole('dialog', { name: 'Agregar empresa' });
    await expect(dialogo).toBeVisible();
    await dialogo.getByRole('textbox', { name: 'Número de identificación' }).fill(nitAleatorio().numero);
    await dialogo.getByRole('textbox', { name: 'Razón social' }).fill(RAZON_SOCIAL);
    await dialogo.getByRole('textbox', { name: 'Prefijo de folio' }).fill(PREFIJO);
    await paso(page, {
      titulo: 'Escribe los datos de la empresa',
      explicacion:
        'Elige el tipo y el número de identificación (con o sin dígito de verificación: Validasoft lo calcula y lo compara), la razón social y el prefijo de folio. El prefijo son de 2 a 10 letras mayúsculas o números, y aparece en todos los folios de la empresa: no se puede cambiar después. ' +
        'El campo de ejemplo muestra cómo se verán sus folios.',
      resaltar: [dialogo.getByRole('textbox', { name: 'Razón social' }), dialogo.getByRole('textbox', { name: 'Prefijo de folio' })],
      variantes: [
        { cuando: 'El prefijo tiene menos de 2 o más de 10 caracteres, o minúsculas', entonces: 'El campo dice que debe tener entre 2 y 10 letras mayúsculas o números y no se puede agregar.' },
        { cuando: 'El número no tiene el formato del tipo elegido', entonces: 'El campo dice que el número no tiene el formato de ese tipo de identificación.' },
      ],
    });
    await dialogo.getByRole('button', { name: 'Agregar empresa', exact: true }).click();
    await expect(app.getByText(`${RAZON_SOCIAL} quedó en el grupo.`, { exact: false })).toBeVisible();
    const fila = app.getByRole('row', { name: `Ver ${RAZON_SOCIAL}` });
    await expect(fila).toBeVisible();
    await expect(fila).toContainText('Activa');

    // 3. La empresa nueva, sin textos todavía.
    await fila.click();
    const textos = app.getByRole('list', { name: 'Textos legales' });
    await expect(textos).toBeVisible();
    await expect(textos.getByRole('listitem').filter({ hasText: 'Autorización de tratamiento de datos' })).toContainText('Sin publicar');
    await paso(page, {
      titulo: 'Revisa los textos legales de la empresa nueva',
      explicacion:
        'La empresa nueva aparece con sus textos legales «Sin publicar»: la autorización de tratamiento de datos y los términos y condiciones. Mientras falte alguno, los terceros no pueden aceptar las invitaciones de la empresa. ' +
        'Lo que el tercero acepta al vincularse es exactamente lo que publiques aquí.',
      resaltar: textos,
      avisos: true,
      configuracion: {
        ajuste: 'Textos legales de la empresa',
        ruta: 'Configuración › Empresas del grupo (/empresas)',
        efecto: 'Es lo que el tercero lee y acepta antes de vincularse con la empresa.',
      },
    });

    // 4. Publicar la autorización. El editor se documenta antes de publicar.
    const autorizacion = textos.getByRole('listitem').filter({ hasText: 'Autorización de tratamiento de datos' });
    await autorizacion.getByRole('button', { name: 'Publicar', exact: true }).click();
    await autorizacion.getByRole('textbox', { name: 'Texto de la versión 1' }).fill(TEXTO_AUTORIZACION);
    await paso(page, {
      titulo: 'Escribe el texto de la autorización',
      explicacion:
        'Escribe el texto completo de la autorización de tratamiento de datos. Se publica como versión 1: el botón «Publicar versión 1» solo se activa cuando hay texto y cambió algo respecto de la versión vigente. ' +
        'Al pulsarlo, Validasoft pide confirmación, porque el texto queda vigente de inmediato y los terceros lo aceptan al vincularse.',
      resaltar: autorizacion.getByRole('textbox', { name: 'Texto de la versión 1' }),
      variantes: [
        { cuando: 'El texto está vacío', entonces: 'El botón de publicar está desactivado y dice que falta escribir el texto.' },
        { cuando: 'Ya hay una versión vigente', entonces: 'El editor muestra el aviso de que la versión nueva queda vigente y los terceros deben aceptarla de nuevo.' },
      ],
    });
    await autorizacion.getByRole('button', { name: 'Publicar versión 1', exact: true }).click();
    await app.getByRole('dialog', { name: /Publicar la versión 1 de Autorización/ }).getByRole('button', { name: 'Publicar versión 1', exact: true }).click();
    await expect(app.getByText('Versión 1 de Autorización de tratamiento de datos publicada.', { exact: false })).toBeVisible();

    // 5. Publicar los términos.
    const terminos = textos.getByRole('listitem').filter({ hasText: 'Términos y condiciones' });
    await terminos.getByRole('button', { name: 'Publicar', exact: true }).click();
    await terminos.getByRole('textbox', { name: 'Texto de la versión 1' }).fill(TEXTO_TERMINOS);
    await paso(page, {
      titulo: 'Publica los términos y condiciones',
      explicacion:
        'Los términos y condiciones se publican igual que la autorización, cada uno con su propia versión. Cuando la empresa tiene los dos textos vigentes, los terceros pueden aceptar sus invitaciones; para corregir un texto se publica otra versión y el historial de versiones queda guardado.',
      resaltar: terminos.getByRole('textbox', { name: 'Texto de la versión 1' }),
    });
    await terminos.getByRole('button', { name: 'Publicar versión 1', exact: true }).click();
    await app.getByRole('dialog', { name: /Publicar la versión 1 de Términos/ }).getByRole('button', { name: 'Publicar versión 1', exact: true }).click();
    await expect(app.getByText('Versión 1 de Términos y condiciones publicada.', { exact: false })).toBeVisible();

    // 6. Resultado: los dos textos vigentes.
    await expect(autorizacion).toContainText('Versión 1');
    await expect(terminos).toContainText('Versión 1');
    await paso(page, {
      titulo: 'Comprueba que los dos textos están vigentes',
      explicacion:
        'Cada texto muestra su versión vigente y la fecha desde la que rige. «Ver texto» despliega lo que el tercero leerá; si un texto cambia, «Publicar versión nueva» crea la versión siguiente sin borrar las anteriores.',
      resaltar: textos,
      avisos: true,
    });
  },
);
