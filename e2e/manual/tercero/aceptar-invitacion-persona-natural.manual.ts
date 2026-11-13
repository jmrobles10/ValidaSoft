import { pdf } from '../../soporte/archivos';
import { enlace, esperarCorreo } from '../../soporte/correo';
import {
  abrirSeccion,
  llenarEspecialidades,
  llenarGeneralNatural,
  llenarSst,
  llenarTributaria,
} from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, esperarEstadoDelTercero, fraseDeEstado, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { invitar, nuevaPersonaNatural } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFlujoDeClientePorTipo } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';
const CEDULA = 'Cédula de ciudadanía';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Acepta la invitación y completa tu información como persona natural',
    resumen:
      'Una empresa invita a una persona natural como cliente. Recibe el correo, acepta los textos legales y ve que le piden el RUT y la cédula, y cuatro secciones de «Mi información» (sin Legal ni Financiera). Completa cada sección y sube sus documentos hasta que su expediente pasa a revisión.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Cliente publicado que pide, según el tipo de persona, el RUT, la cédula (solo a personas naturales), la Cámara de comercio (solo a personas jurídicas) y las seis secciones de la ficha.',
      'Una persona natural invitada como cliente por el sistema integrado (el correo de invitación ya llegó).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica'],
  },
  async ({ page }) => {
    const e = await escenarioConFlujoDeClientePorTipo();
    const tercero = nuevaPersonaNatural('Carlos Andrés Gómez Ruiz');
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');

    // 1. El correo de invitación, dibujado como lo vería el tercero en su bandeja.
    const correo = await esperarCorreo(tercero.correo, /te invita a validar tu vínculo como cliente$/);
    await page.setContent(`<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
      <body style="margin:0;background:#e9edf2;font-family:system-ui,'Segoe UI',sans-serif;color:#1b2129">
        <div style="max-width:600px;margin:40px auto;padding:0 12px">
          <div style="background:#fff;border:1px solid #d5dbe3;border-radius:14px;overflow:hidden">
            <div style="padding:16px 22px;background:#f7f8fa;border-bottom:1px solid #e3e7ec;font-size:14px;line-height:1.6;color:#4a5460">
              <div><b>De:</b> ${correo.desde}</div><div><b>Para:</b> ${tercero.correo}</div><div><b>Asunto:</b> ${correo.asunto}</div>
            </div>
            <div style="padding:26px 22px;font-size:16px;line-height:1.65">${correo.html}</div>
          </div>
        </div>
      </body>`);
    await paso(page, {
      titulo: 'Abre la invitación que llega a tu correo',
      explicacion:
        'La empresa te escribe desde Validasoft con el asunto «Constructora Sur S.A.S. te invita a validar tu vínculo como cliente». El correo dice quién te invita y trae el enlace «Abrir Validasoft». ' +
        'El enlace es personal, de un solo uso y tiene fecha de vencimiento; si se vence, la empresa puede enviarte otra invitación.',
      resaltar: page.getByRole('link', { name: 'Abrir Validasoft' }),
      variantes: [
        { cuando: 'El tipo de vínculo es Proveedor', entonces: 'Antes de abrir la invitación debes escribir un código de 6 dígitos que llega a este mismo correo.' },
        { cuando: 'El tipo de vínculo es Cliente', entonces: 'El enlace abre la invitación directamente, sin código.' },
      ],
      configuracion: {
        ajuste: 'Exige un código por correo en el enlace',
        ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)',
        efecto: 'Según el tipo de vínculo, el enlace pide o no el código de 6 dígitos.',
      },
    });

    // 2. El enlace abre la invitación de Cliente: textos legales, sin código.
    await page.goto(enlace(correo, '/e/'));
    await expect(page.getByRole('heading', { level: 1, name: `${e.sur.nombre} te invita a validar tu información` })).toBeVisible();
    await paso(page, {
      titulo: 'Lee y acepta los textos legales',
      explicacion:
        'Como cliente, el enlace te lleva directo a la invitación: no escribes ningún código. Ves quién te invita y los textos que debes aceptar, el aviso de tratamiento de datos de Validasoft y los que la empresa publicó. ' +
        'Marca «Leí y acepto este texto» en cada uno y pulsa «Aceptar». Al aceptar, Validasoft guarda la versión de cada texto y la fecha como constancia.',
      resaltar: [page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).first(), page.getByRole('button', { name: 'Aceptar' })],
      completa: true,
      variantes: [
        { cuando: 'La empresa aún no publicó su autorización y sus términos', entonces: 'No puedes aceptar: la pantalla dice que la empresa aún no publica sus textos y que te avisarán.' },
      ],
      configuracion: {
        ajuste: 'Textos legales de la empresa',
        ruta: 'Configuración › Empresas del grupo (/empresas)',
        efecto: 'Definen el contenido de la autorización y de los términos que aceptas.',
      },
    });
    await aceptarInvitacion(page, invitacion);

    // 3. Lo que pide la empresa a una persona natural.
    await expect(fraseDeEstado(page, 'Abierto')).toBeVisible();
    await expect(tarjeta(page, CEDULA)).toBeVisible();
    await expect(tarjeta(page, CAMARA)).toHaveCount(0);
    await paso(page, {
      titulo: 'Revisa lo que te pide la empresa',
      explicacion:
        'En «Tu revisión con …» la frase resume tu estado («Te faltan documentos y secciones») y la fecha límite. «Lo que te pide» lista lo que te toca: como persona natural te piden el RUT y la cédula de ciudadanía, y cuatro secciones de tu información. ' +
        'No te piden la Cámara de comercio: ese documento es solo para personas jurídicas.',
      resaltar: [fraseDeEstado(page, 'Abierto'), tarjeta(page, CEDULA)],
      variantes: [
        { cuando: 'Eres persona jurídica', entonces: 'En lugar de la cédula te pide la Cámara de comercio, y tu información tiene las seis secciones.' },
      ],
      configuracion: [
        {
          ajuste: 'Requisitos del flujo y condiciones «se pide cuando»',
          ruta: 'Configuración › Flujos aprobatorios (/flujos)',
          efecto: 'Cada documento puede pedirse solo a un tipo de persona: así la cédula es solo para naturales.',
        },
      ],
    });

    // 4. Mi información: cuatro secciones.
    await page.goto('/ficha');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await expect(page.getByText('0 de 4 secciones completas').first()).toBeVisible();
    await paso(page, {
      titulo: 'Entra a «Mi información»',
      explicacion:
        '«Mi información» es tu ficha, una sola para todas las empresas con las que te relacionas. Como persona natural tiene cuatro secciones: General, Tributaria, Seguridad y salud en el trabajo, y Especialidades. ' +
        'Arriba ves cuántas llevas completas («0 de 4 secciones completas»).',
      resaltar: page.getByRole('list', { name: 'Mi información' }),
    });

    // 5. Legal y Financiera no te aplican.
    await page.goto('/ficha/legal');
    await expect(page.getByRole('heading', { name: 'Esta sección no aplica para ti' })).toBeVisible();
    await paso(page, {
      titulo: 'Verifica que las secciones de persona jurídica no te aplican',
      explicacion:
        'La información legal (socios, junta directiva, revisor fiscal) y la financiera (balances) solo se piden a personas jurídicas. Si entras a una de ellas por la dirección, la pantalla dice «Esta sección no aplica para ti» y no tienes nada que diligenciar.',
      resaltar: page.getByRole('heading', { name: 'Esta sección no aplica para ti' }),
      variantes: [{ cuando: 'Eres persona jurídica', entonces: 'Ambas secciones aparecen en tu lista y debes diligenciarlas.' }],
    });

    // 6. Información general de la natural.
    await abrirSeccion(page, 'general', 'Información general');
    await expect(page.getByRole('textbox', { name: 'Nombres', exact: true })).toHaveValue('Carlos Andrés');
    await expect(page.getByRole('textbox', { name: 'Apellidos', exact: true })).toHaveValue('Gómez Ruiz');
    await expect(page.getByRole('textbox', { name: 'Razón social', exact: true })).toHaveCount(0);
    await paso(page, {
      titulo: 'Diligencia tu información general',
      explicacion:
        'Tus nombres y apellidos llegan prellenados con lo que la empresa registró al invitarte; revísalos. En lugar de la razón social, pide la fecha y el lugar de expedición de tu documento. ' +
        'Completa también el teléfono, el domicilio y tu actividad económica, y responde las preguntas de persona expuesta políticamente (PEP): si lo eres, o si tienes vínculo con una, Validasoft te pedirá los datos. Mientras falte un dato, «Guardar y continuar» queda deshabilitado.',
      resaltar: page.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' }),
      variantes: [
        { cuando: 'Eres persona jurídica', entonces: 'En vez de nombres y apellidos ves la razón social, y no hay preguntas de PEP propias.' },
      ],
    });
    await llenarGeneralNatural(page);

    // 7. Tributaria, SST y especialidades.
    await llenarTributaria(page);
    await paso(page, {
      titulo: 'Completa la información tributaria',
      explicacion:
        'Elige las responsabilidades de la casilla 53 de tu RUT y responde las preguntas sobre ICA y CREE. Al guardar, la sección queda completa y el avance de «Mi información» sube.',
      resaltar: page.getByRole('combobox', { name: 'Responsabilidades (casilla 53 del RUT)' }),
    });
    await llenarSst(page);
    await paso(page, {
      titulo: 'Responde seguridad y salud en el trabajo',
      explicacion: 'Son preguntas de Sí o No sobre tu sistema de gestión de seguridad y salud en el trabajo. Responde todas para que la sección quede completa.',
      resaltar: page.getByRole('radiogroup').first(),
    });
    await llenarEspecialidades(page);
    await paso(page, {
      titulo: 'Elige tus especialidades',
      explicacion: 'Busca por nombre y marca las especialidades que ofreces; quedan resumidas en «Tus especialidades». La empresa las usa para saber en qué puede contratarte.',
      resaltar: page.getByRole('region', { name: 'Tus especialidades' }),
    });

    // 8. Mi información completa.
    await page.goto('/ficha');
    await expect(page.getByText('Tu información está completa', { exact: true })).toBeVisible();
    await expect(page.getByText('4 de 4 secciones completas').first()).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que tu información está completa',
      explicacion:
        'Con las cuatro secciones guardadas, «Mi información» muestra «Tu información está completa» y «4 de 4 secciones completas». Desde aquí puedes volver a cualquier sección para corregirla cuando cambie algo.',
      resaltar: page.getByText('Tu información está completa', { exact: true }),
    });

    // 9. Subir la cédula y el RUT.
    await page.goto('/empresas');
    await abrirEmpresa(page, e.sur.nombre, 'CLIENTE');
    await expect(fraseDeEstado(page, 'Abierto')).toBeVisible();
    await tarjeta(page, CEDULA).getByRole('link', { name: /^Subir/ }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Subir ${CEDULA}` })).toBeVisible();
    await paso(page, {
      titulo: 'Sube tu cédula de ciudadanía',
      explicacion:
        'Cada documento tiene su pantalla de carga con sus reglas: formatos permitidos, tamaño máximo y, si aplica, los datos que debes escribir. Pulsa «Elegir archivo» y selecciona el PDF de tu cédula; Validasoft lo guarda en tu bóveda y te confirma la versión.',
      resaltar: page.getByRole('button', { name: 'Elegir archivo' }),
      configuracion: { ajuste: 'Formatos, tamaño, campos y vigencia del tipo de documento', ruta: 'Configuración › Tipos de documento (/tipos-documento)' },
    });
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`Cedula ${tercero.identificacion.numero}`));
    await expect(page.getByText(`Es tu versión 1 de ${CEDULA}`)).toBeVisible({ timeout: 30_000 });
    await page.getByRole('link', { name: 'Volver a mi revisión' }).click();
    await subirDocumento(page, RUT, pdf(`RUT ${tercero.identificacion.numero}`));

    // 10. En revisión.
    await esperarEstadoDelTercero(page, 'EnRevision');
    await expect(tarjeta(page, RUT)).toBeVisible();
    await paso(page, {
      titulo: 'Espera la revisión de la empresa',
      explicacion:
        'Cuando no falta ningún documento ni sección, el expediente pasa solo a revisión: no hay un botón «Enviar». La frase de arriba dice que la empresa está revisando tu información y que te avisa por correo si necesita algo. ' +
        'La empresa recibe exactamente lo que se pide a una persona natural: el RUT, la cédula y las cuatro secciones.',
      resaltar: fraseDeEstado(page, 'EnRevision'),
      variantes: [
        { cuando: 'La empresa observa un documento', entonces: 'Recibes un correo con los motivos y la frase cambia a «te pide corregir lo siguiente».' },
        { cuando: 'La empresa aprueba', entonces: 'La frase cambia a «aprobó tu información. Tu vínculo está activo».' },
      ],
    });
  },
);
