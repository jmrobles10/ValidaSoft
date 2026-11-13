import { expect, paso, recorrido } from '../nucleo';
import { crearEscenario, invitar, nuevoTercero } from '../../soporte/semilla';
import { entrarPorInvitacion } from '../../soporte/proveedor';

recorrido(
  {
    dominio: 'privacidad',
    actor: 'Tercero',
    titulo: 'Lee y acepta los textos legales de una empresa',
    resumen:
      'Antes de compartir sus documentos con una empresa, el tercero lee cada texto legal vigente con su versión y lo marca como aceptado. Validasoft registra qué aceptó y en qué versión; si un texto cambia mientras lo lee, debe volver a marcarlo.',
    precondiciones: [
      'Un grupo con Constructora Norte, con sus textos legales publicados, que invitó a un tercero por correo.',
      'El tercero abre el enlace de la invitación desde su correo y escribe el código de acceso.',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    const tercero = nuevoTercero('Aceros del Pulido S.A.S.');
    const invitacion = await invitar(e, e.norte, tercero);
    await entrarPorInvitacion(page, invitacion);
    const casillas = page.getByRole('checkbox', { name: 'Leí y acepto este texto' });
    await expect(page.getByRole('heading', { name: 'Lo que aceptas', exact: true })).toBeVisible();
    await expect(casillas.first()).toBeVisible();

    // 1. Los textos legales de la empresa.
    await paso(page, {
      titulo: 'Lee los textos legales de la empresa',
      explicacion:
        'En «Lo que aceptas» aparece cada texto legal que la empresa exige, con su nombre (por ejemplo, el aviso de tratamiento de datos de Validasoft y la autorización de la empresa), la versión publicada y la fecha de publicación. ' +
        'Lee el texto completo dentro de su recuadro antes de marcarlo: la aceptación queda registrada con esa versión exacta.',
      resaltar: page.getByRole('heading', { name: 'Lo que aceptas', exact: true }),
      variantes: [
        { cuando: 'La empresa no tiene textos publicados', entonces: 'La pantalla dice que la empresa aún no publica sus textos y no puedes aceptar la invitación todavía.' },
        { cuando: 'Un texto cambió mientras lo leías', entonces: 'Te mostramos la versión nueva y debes marcarla otra vez; la anterior ya no cuenta.' },
      ],
    });

    // 2. Marcar cada texto.
    for (const casilla of await casillas.all()) await casilla.check();
    await expect(casillas.first()).toBeChecked();
    await paso(page, {
      titulo: 'Marca cada texto que aceptas',
      explicacion:
        'Cada texto tiene su casilla «Leí y acepto este texto». Debes marcarlas todas: un texto sin marcar impide aceptar la invitación y la página te lo recuerda si intentas hacerlo. ' +
        'Desmarcar una casilla la quita de la aceptación hasta que la vuelvas a marcar.',
      resaltar: casillas,
    });

    // 3. Aceptar.
    await page.getByRole('button', { name: 'Aceptar' }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` })).toBeVisible();
    await paso(page, {
      titulo: 'Acepta la invitación',
      explicacion:
        'Con «Aceptar» quedan registrados tus consentimientos con la versión de cada texto y la fecha. Después pasas a «Tu revisión con» la empresa, donde ves los documentos que te pide, uno por uno. ' +
        'Si vuelves a abrir la invitación, aparece «Ya aceptaste esta invitación» con la fecha de tu aceptación.',
      resaltar: page.getByRole('heading', { level: 1, name: `Tu revisión con ${e.norte.nombre}` }),
      configuracion: {
        ajuste: 'Textos legales de cada empresa',
        ruta: 'Configuración › Empresas del grupo (/empresas), en Textos legales',
        efecto: 'Lo que la empresa publica aquí es lo que el tercero acepta en la invitación; cada publicación nueva es una versión distinta.',
      },
    });
  },
);
