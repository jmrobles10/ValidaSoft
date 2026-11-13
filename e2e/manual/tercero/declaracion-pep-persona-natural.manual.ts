import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio, publicarFlujoDeFicha } from '../../soporte/ficha';
import { dialogoDeclaracion, diligenciarDeclaracion } from '../../soporte/declaracion-pep';
import { escribirFecha } from '../../soporte/fechas';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const PREGUNTA_PEP = '¿Eres una persona expuesta políticamente (PEP)?';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Declara que eres una persona expuesta políticamente',
    resumen:
      'Si una persona natural responde que sí es PEP, la declaración jurada se llena y firma en la misma sección de información general. La firma queda guardada con la sección, y solo entonces puedes descargar tu PDF.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Cliente que pide la sección de información general del tercero.',
      'Una persona natural invitada como cliente de Sur, que aceptó la invitación y tiene su información general sin diligenciar.',
    ],
    relacionados: ['tercero/descargar-mi-declaracion-pep', 'tercero/declaracion-pep-personas-relacionadas'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await publicarFlujoDeFicha(e, e.sur, 'PEP_NATURAL_MANUAL', e.sur.areas.revision!, 'CLIENTE', [{ seccion: 'FICHA_GENERAL' }]);
    const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);

    await abrirSeccion(page, 'general', 'Información general');
    await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
    await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
    await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');

    // 1. La pregunta PEP: al decir que sí se abre la declaración.
    await paso(page, {
      titulo: 'Responde la pregunta de PEP',
      explicacion:
        'La pregunta «¿Eres una persona expuesta políticamente (PEP)?» pide tu respuesta. Al decir que sí se abre la declaración jurada PEP, que debes llenar y firmar antes de guardar la sección. ' +
        'Si respondes que no, la declaración no se pide.',
      resaltar: page.getByRole('radiogroup', { name: PREGUNTA_PEP }),
      variantes: [{ cuando: 'Respondes «No»', entonces: 'No se abre la declaración y la sección se puede guardar sin firma.' }],
    });
    await page.getByRole('radiogroup', { name: PREGUNTA_PEP }).getByRole('radio', { name: 'Sí' }).check();
    const dialogo = dialogoDeclaracion(page);
    await expect(dialogo).toBeVisible();

    // 2. El diálogo de la declaración.
    await paso(page, {
      titulo: 'Llena la declaración jurada',
      explicacion:
        'En el diálogo indicas tu cargo o actividad, la institución, las fechas de vinculación y desvinculación y si tienes cuentas en el exterior. ' +
        'Al final firmas: puedes dibujar tu firma con el mouse o subir una imagen. Declaras bajo juramento que la información es cierta.',
      resaltar: dialogo,
    });
    await diligenciarDeclaracion(page, { firma: 'dibujar' });

    // 3. La declaración queda lista, pero sin guardar.
    await expect(page.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
    await paso(page, {
      titulo: 'Confirma la declaración y guarda la sección',
      explicacion:
        'Cuando confirmas el diálogo, la sección muestra que la declaración está lista y se guarda junto con la sección. ' +
        'Hasta que guardes, el PDF de la declaración no está disponible: solo sale lo que quedó guardado.',
      resaltar: page.getByText('Declaración lista: se guarda con la sección'),
    });

    // 4. Guardar: la declaración queda firmada con su fecha.
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
    await expect(page.getByText(/^Declaración firmada el \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
    await paso(page, {
      titulo: 'Verifica que la declaración quedó firmada',
      explicacion:
        'Al guardar, la sección muestra «Declaración firmada» con la fecha de hoy. A partir de aquí puedes descargar tu PDF desde la misma sección, y cualquier cambio posterior en la declaración vuelve a pedir guardar.',
      resaltar: page.getByText(/^Declaración firmada el \d{2}\/\d{2}\/\d{4}$/),
    });
  },
);
