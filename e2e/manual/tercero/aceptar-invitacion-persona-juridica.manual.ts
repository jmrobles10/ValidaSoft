import { pdf } from '../../soporte/archivos';
import { enlace, esperarCorreo } from '../../soporte/correo';
import {
  abrirSeccion,
  agregarPersona,
  guardarSeccionCompleta,
  llenarContactoYDomicilio,
  llenarEspecialidades,
  llenarFinanciera,
  llenarSst,
  llenarTributaria,
  SOCIO,
} from '../../soporte/ficha';
import { abrirEmpresa, aceptarInvitacion, escribirCodigo, esperarEstadoDelTercero, fraseDeEstado, subirDocumento, tarjeta } from '../../soporte/proveedor';
import { invitar, nuevoTercero } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';
import { escenarioConFichaCompleta } from '../nucleo/semillas';

const RUT = 'Registro Único Tributario';
const CAMARA = 'Certificado de existencia y representación legal';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Acepta la invitación y completa tu información como persona jurídica',
    resumen:
      'Una empresa invita a un proveedor persona jurídica. Recibe el correo, entra con un código, acepta los textos legales, diligencia las seis secciones de «Mi información» y sube sus documentos hasta que su expediente pasa a revisión.',
    precondiciones: [
      'Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado como flujo por defecto que pide RUT, Cámara de comercio y las seis secciones de la ficha.',
      'Un tercero persona jurídica invitado por el sistema integrado (el correo de invitación ya llegó).',
    ],
    relacionados: ['configuracion/tipo-de-documento-y-flujo'],
  },
  async ({ page }) => {
    const e = await escenarioConFichaCompleta();
    const tercero = nuevoTercero('Ferretería Andina S.A.S.');
    const invitacion = await invitar(e, e.sur, tercero, 'PROVEEDOR');

    // 1. El correo de invitación, dibujado como lo vería el tercero en su bandeja.
    const correo = await esperarCorreo(tercero.correo, /te invita a validar tu vínculo como proveedor$/);
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
        'La empresa te escribe desde Validasoft con el asunto «Constructora Sur S.A.S. te invita a validar tu vínculo como proveedor». El correo dice quién te invita y trae el enlace «Abrir Validasoft». ' +
        'El enlace es personal, de un solo uso y tiene fecha de vencimiento; si lo pierdes o se vence, la empresa puede enviarte otra invitación.',
      resaltar: page.getByRole('link', { name: 'Abrir Validasoft' }),
      variantes: [{ cuando: 'La empresa configuró un remitente propio', entonces: 'El correo sale con el dominio de la empresa y las respuestas llegan a su buzón.' }],
      configuracion: { ajuste: 'Plantillas de correo y Dominio de correo de la empresa', ruta: 'Configuración › Plantillas de correo y Dominio de correo (/plantillas, /dominio-correo)' },
    });

    // 2. El enlace pide el código que llega a un segundo correo.
    await page.goto(enlace(correo, '/e/'));
    await expect(page.getByRole('heading', { name: 'Escribe el código' })).toBeVisible();
    await paso(page, {
      titulo: 'Escribe el código de acceso',
      explicacion:
        'Como el tipo de vínculo «Proveedor» exige verificar el correo, Validasoft envía un código de 6 dígitos a la misma dirección de la invitación. Escríbelo en los seis recuadros y pulsa «Continuar». ' +
        'Si no llega, revisa la carpeta de spam; si el código se venció, abre de nuevo el enlace del correo para recibir otro. No necesitas crear contraseña.',
      resaltar: page.getByRole('textbox', { name: 'Dígito 1 de 6' }),
      variantes: [{ cuando: 'El tipo de vínculo no exige código (por ejemplo, Cliente)', entonces: 'Este paso no aparece: el enlace del correo abre directo la invitación.' }],
      configuracion: { ajuste: 'Exige un código por correo en el enlace', ruta: 'Configuración › Tipos de vínculo (/tipos-vinculo)', efecto: 'Activa o quita el código de 6 dígitos al abrir el enlace.' },
    });
    await escribirCodigo(page, tercero.correo);

    // 3. Textos legales.
    await expect(page.getByRole('heading', { level: 1, name: `${e.sur.nombre} te invita a validar tu información` })).toBeVisible();
    // Con un texto sin marcar, «Aceptar» no avanza: la pantalla pide marcar cada uno.
    await page.getByRole('button', { name: 'Aceptar' }).click();
    await expect(page.getByText('Marca cada texto para continuar.')).toBeVisible();
    for (const casilla of await page.getByRole('checkbox', { name: 'Leí y acepto este texto' }).all()) await casilla.check();
    await paso(page, {
      titulo: 'Lee y acepta los textos legales',
      explicacion:
        'Antes de entregar nada ves quién te invita y los textos que debes aceptar: el aviso de tratamiento de datos de Validasoft y los que la empresa publicó (su autorización y sus términos y condiciones). ' +
        'Cada texto muestra su versión y la fecha en que se publicó. Marca «Leí y acepto este texto» en cada uno; si pulsas «Aceptar» con alguno sin marcar, la pantalla te pide marcar cada texto para continuar. ' +
        'Al aceptar, Validasoft guarda la versión de cada texto, la fecha y tu conexión como constancia, y desde ese momento tus documentos se comparten con la empresa.',
      resaltar: [page.getByRole('checkbox', { name: 'Leí y acepto este texto' }), page.getByRole('button', { name: 'Aceptar' })],
      completa: true,
      variantes: [
        { cuando: 'La empresa aún no publicó su autorización y sus términos', entonces: 'No puedes aceptar: la pantalla dice que la empresa aún no publica sus textos y que te avisarán.' },
        { cuando: 'Ya habías aceptado esta invitación', entonces: 'La pantalla dice «Ya aceptaste esta invitación» con la fecha, y puedes ir a tu revisión.' },
      ],
      configuracion: { ajuste: 'Textos legales de la empresa', ruta: 'Configuración › Empresas del grupo (/empresas)', efecto: 'Definen el contenido de la autorización y de los términos que aceptas.' },
    });
    await aceptarInvitacion(page, invitacion);

    // 4. Lo que pide la empresa.
    await expect(fraseDeEstado(page, 'Abierto')).toBeVisible();
    await paso(page, {
      titulo: 'Revisa lo que te pide la empresa',
      explicacion:
        'En «Tu revisión con …» una frase (1) resume el estado («Te faltan 8 documentos» cuenta los documentos y las secciones de tu información) y la fecha límite para completarlo. ' +
        'Debajo, «Lo que te pide» separa lo que te toca (2: documentos por subir y secciones por diligenciar) de lo que ya está en manos de la empresa («Lo revisa …»). ' +
        'Cada requisito muestra su estado y, en los documentos, el formato y el tamaño permitidos.',
      resaltar: [fraseDeEstado(page, 'Abierto'), tarjeta(page, RUT)],
      variantes: [
        { cuando: 'Eres persona natural', entonces: 'En lugar de la Cámara de comercio te pide la cédula, y tu información tiene cuatro secciones (sin Legal ni Financiera).' },
        { cuando: 'Eres persona jurídica', entonces: 'Te pide RUT, Cámara de comercio y las seis secciones de tu información.' },
      ],
      configuracion: [
        { ajuste: 'Requisitos del flujo y condiciones «se pide cuando»', ruta: 'Configuración › Flujos aprobatorios (/flujos)', efecto: 'Definen qué documentos y secciones se le piden a cada tipo de persona.' },
        { ajuste: 'Plazo de cargue', ruta: 'Configuración › Flujos aprobatorios (/flujos)', efecto: 'Fija hasta cuándo puedes completar el expediente.' },
      ],
    });

    // 5. Mi información.
    await page.goto('/ficha');
    await expect(page.getByRole('heading', { level: 1, name: 'Mi información' })).toBeVisible();
    await paso(page, {
      titulo: 'Entra a «Mi información»',
      explicacion:
        '«Mi información» es tu ficha: una sola para todas las empresas con las que te relacionas. Se divide en secciones y arriba ves cuántas llevas completas. ' +
        'Puedes entrar en el orden que quieras y volver después; lo que diligencies aquí se comparte con las empresas que te lo piden.',
      resaltar: page.getByRole('list', { name: 'Mi información' }),
    });

    // 6. Información general.
    await abrirSeccion(page, 'general', 'Información general');
    await expect(page.getByRole('textbox', { name: 'Razón social', exact: true })).toHaveValue(tercero.nombre);
    await llenarContactoYDomicilio(page, '601 555 1234', 'Calle 45 # 12-08');
    const barra = page.getByRole('group', { name: 'Guardar la sección' });
    await expect(barra.getByText('Lista: puedes continuar')).toBeVisible();
    await paso(page, {
      titulo: 'Diligencia la información general',
      explicacion:
        'La identificación, el tipo de persona, la razón social y el correo llegan prellenados con lo que la empresa registró al invitarte; revísalos. Completa teléfono, país, ciudad, dirección y tu actividad económica (CIIU). ' +
        'La barra de abajo dice si la sección ya está lista; mientras falte un dato, «Guardar y continuar» queda deshabilitado y te indica cuántos faltan.',
      resaltar: barra,
      variantes: [{ cuando: 'Eres persona natural', entonces: 'En vez de razón social ves nombres y apellidos, la fecha y el lugar de expedición del documento y las preguntas de persona expuesta políticamente (PEP).' }],
    });
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');

    // 7. Tributaria.
    await llenarTributaria(page);
    await paso(page, {
      titulo: 'Completa la información tributaria',
      explicacion:
        'Elige las responsabilidades de la casilla 53 de tu RUT y responde las preguntas sobre ICA y CREE. Al guardar, la sección queda marcada como completa y el avance de «Mi información» sube.',
      resaltar: page.getByRole('combobox', { name: 'Responsabilidades (casilla 53 del RUT)' }),
    });

    // 8. Legal: el cuadro «Agregar persona», ya diligenciado.
    await abrirSeccion(page, 'legal', 'Información legal');
    await page.getByRole('button', { name: 'Agregar persona' }).click();
    const dialogo = page.getByRole('dialog', { name: 'Agregar persona' });
    await dialogo.getByRole('combobox', { name: 'Rol' }).click();
    await page.getByRole('option', { name: 'Representante legal', exact: true }).click();
    await dialogo.getByRole('combobox', { name: 'Tipo de identificación' }).click();
    await page.getByRole('option', { name: 'CC', exact: true }).click();
    await dialogo.getByRole('textbox', { name: 'Número de identificación' }).fill('1020304050');
    await dialogo.getByRole('textbox', { name: 'Nombres', exact: true }).fill('Marta Lucía');
    await dialogo.getByRole('textbox', { name: 'Apellidos', exact: true }).fill('Ríos Duarte');
    await dialogo.getByRole('radiogroup', { name: '¿Es persona expuesta políticamente (PEP)?' }).getByRole('radio', { name: 'No' }).check();
    await dialogo.getByRole('radiogroup', { name: '¿Tiene vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
    await paso(page, {
      titulo: 'Agrega a tu representante legal y a tus socios',
      explicacion:
        'La información legal pide las personas que están detrás de la empresa: representante legal, socios o accionistas, junta directiva, revisor fiscal y contador, como en tu Cámara de comercio. ' +
        'En «Agregar persona» eliges el rol, indicas el documento y el nombre y declaras si es persona expuesta políticamente (PEP) o tiene vínculo con una. Para un socio también va su participación (%).',
      resaltar: dialogo.getByRole('combobox', { name: 'Rol' }),
      variantes: [{ cuando: 'Agregas un socio o accionista', entonces: 'Además del documento y el nombre, indicas su porcentaje de participación.' }],
    });
    await dialogo.getByRole('button', { name: 'Listo' }).click();
    await expect(dialogo).toBeHidden();
    await agregarPersona(page, SOCIO);

    // 9. Guardar la legal.
    const barraLegal = page.getByRole('group', { name: 'Guardar la sección' });
    await guardarSeccionCompleta(page, 'Guardaste Información legal: está completa.');
    await expect(barraLegal).toBeVisible();
    await paso(page, {
      titulo: 'Guarda la información legal',
      explicacion:
        'La sección no queda completa solo con el representante legal: la empresa exige al menos un socio o accionista. Con los dos en la lista, «Guardar» confirma la sección y el avance de «Mi información» sube a tres de seis.',
      resaltar: page.getByRole('list', { name: 'Personas relacionadas' }),
      variantes: [{ cuando: 'Solo registras al representante legal', entonces: 'La barra dice «Falta 1 dato para continuar» y un aviso pide registrar al menos un socio o accionista.' }],
    });

    // 10. Financiera.
    await llenarFinanciera(page);
    await paso(page, {
      titulo: 'Registra la información financiera',
      explicacion:
        'Indica la fecha de corte del último cierre y las cifras de activos, pasivos, patrimonio, ingresos y gastos. Validasoft comprueba que activos = pasivos + patrimonio y te avisa si no cuadra. ' +
        'El título de la sección lleva el año del periodo.',
      resaltar: page.getByRole('status', { name: 'Cuadre: activos = pasivos + patrimonio' }),
    });

    // 11. SST.
    await llenarSst(page);
    await paso(page, {
      titulo: 'Responde seguridad y salud en el trabajo',
      explicacion: 'Son preguntas de Sí o No sobre tu sistema de gestión de seguridad y salud en el trabajo. Responde todas para que la sección quede completa.',
      resaltar: page.getByRole('radiogroup').first(),
    });

    // 12. Especialidades.
    await llenarEspecialidades(page);
    await paso(page, {
      titulo: 'Elige tus especialidades',
      explicacion: 'Busca por nombre y marca las especialidades que ofreces; quedan resumidas en «Tus especialidades». La empresa las usa para saber en qué puede contratarte.',
      resaltar: page.getByRole('region', { name: 'Tus especialidades' }),
    });

    // 13. Resumen completo.
    await page.goto('/ficha');
    await expect(page.getByText('Tu información está completa', { exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Confirma que tu información está completa',
      explicacion:
        'Con las seis secciones guardadas, «Mi información» muestra «Tu información está completa». Desde aquí puedes volver a cualquier sección para corregirla cuando cambie algo de tu empresa.',
      resaltar: page.getByText('Tu información está completa', { exact: true }),
    });

    // 14. Volver a la revisión con la empresa.
    await page.goto('/empresas');
    await expect(page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true })).toBeVisible();
    await paso(page, {
      titulo: 'Vuelve a tu revisión con la empresa',
      explicacion:
        '«Mis empresas» lista todas las empresas que te invitaron, cada una con su tipo de vínculo (Proveedor, Cliente…). Abre la tarjeta de Constructora Sur para ver qué documentos te faltan.',
      resaltar: page.getByRole('listitem').filter({ has: page.getByRole('heading', { level: 2, name: e.sur.nombre, exact: true }) }),
    });
    await abrirEmpresa(page, e.sur.nombre, 'PROVEEDOR');

    // 15. Subir un documento.
    await expect(fraseDeEstado(page, 'Abierto')).toBeVisible();
    await tarjeta(page, RUT).getByRole('link', { name: /^Subir/ }).click();
    await expect(page.getByRole('heading', { level: 1, name: `Subir ${RUT}` })).toBeVisible();
    await paso(page, {
      titulo: 'Elige el archivo del documento',
      explicacion:
        'Cada documento tiene su propia pantalla de carga con las reglas del tipo: formatos permitidos, tamaño máximo y, si aplica, los datos que debes escribir (por ejemplo, la fecha de expedición). ' +
        'Pulsa «Elegir archivo» y selecciona el PDF (también puedes arrastrarlo a la pantalla); Validasoft lo guarda en tu bóveda.',
      resaltar: page.getByRole('button', { name: 'Elegir archivo' }),
      configuracion: { ajuste: 'Formatos, tamaño, campos y vigencia del tipo de documento', ruta: 'Configuración › Tipos de documento (/tipos-documento)' },
    });
    const selector = page.waitForEvent('filechooser');
    await page.getByRole('button', { name: 'Elegir archivo' }).click();
    await (await selector).setFiles(pdf(`RUT ${tercero.identificacion.numero}`));
    await expect(page.getByText(`Es tu versión 1 de ${RUT}`)).toBeVisible({ timeout: 30_000 });
    await paso(page, {
      titulo: 'Confirma la versión que quedó guardada',
      explicacion:
        'Validasoft confirma «Listo. Es tu versión 1» del documento y dice qué empresa ya la tiene. Si más adelante subes otro archivo del mismo documento, quedará como versión 2 y las empresas que lo usan vuelven a revisarlo. ' +
        'Pulsa «Volver a mi revisión» para seguir con el siguiente documento; también puedes ver tu bóveda o elegir otro archivo.',
      resaltar: page.getByText(`Es tu versión 1 de ${RUT}`),
    });
    await page.getByRole('link', { name: 'Volver a mi revisión' }).click();
    await subirDocumento(page, CAMARA, pdf(`Camara ${tercero.identificacion.numero}`));

    // 16. En revisión.
    await esperarEstadoDelTercero(page, 'EnRevision');
    await paso(page, {
      titulo: 'Espera la revisión de la empresa',
      explicacion:
        'Cuando no falta ningún documento ni sección, el expediente pasa solo a revisión: no hay un botón «Enviar». La frase de arriba dice que la empresa está revisando tu información y que te avisa por correo si necesita algo. ' +
        'Cada documento muestra su vigencia («Vigente», «Por vencer») y cuándo vence, y te deja subir otra versión.',
      resaltar: fraseDeEstado(page, 'EnRevision'),
      variantes: [
        { cuando: 'La empresa observa un documento', entonces: 'Recibes un correo con los motivos y la frase cambia a «te pide corregir lo siguiente»: corriges lo observado y envías tu respuesta.' },
        { cuando: 'La empresa aprueba', entonces: 'La frase cambia a «aprobó tu información. Tu vínculo está activo».' },
      ],
    });
  },
);
