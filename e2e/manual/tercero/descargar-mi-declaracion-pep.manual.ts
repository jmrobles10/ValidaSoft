import { areaDe } from '../nucleo/semillas';
import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio, publicarFlujoDeFicha } from '../../soporte/ficha';
import { dialogoDeclaracion, diligenciarDeclaracion } from '../../soporte/declaracion-pep';
import { escribirFecha } from '../../soporte/fechas';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const PREGUNTA_PEP = '¿Eres una persona expuesta políticamente (PEP)?';
const BOTON_PDF = 'Descargar el PDF de tu declaración PEP';

recorrido(
  {
    dominio: 'tercero',
    actor: 'Tercero',
    titulo: 'Descarga el PDF de tu declaración PEP',
    resumen:
      'Una vez guardada y firmada tu declaración PEP, puedes descargar un PDF con tus propios datos. El PDF sale de lo que quedó guardado: si cambias la declaración sin guardar, no aparece el botón hasta que la guardes otra vez.',
    precondiciones: [
      'Un grupo empresarial con «Constructora Sur S.A.S.» y un flujo de Cliente que pide la sección de información general de la persona natural.',
      'Una persona natural invitada como cliente de Sur, que aceptó la invitación y que ya guardó su declaración PEP firmada.',
    ],
    relacionados: ['tercero/declaracion-pep-persona-natural', 'tercero/declaracion-pep-personas-relacionadas'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await publicarFlujoDeFicha(e, e.sur, 'PEP_PDF_MANUAL', areaDe(e.sur, 'revision'), 'CLIENTE', [{ seccion: 'FICHA_GENERAL' }]);
    const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);

    // Diligencia y firma la declaración, y guarda la sección.
    await abrirSeccion(page, 'general', 'Información general');
    await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
    await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
    await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
    await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
    await page.getByRole('radiogroup', { name: PREGUNTA_PEP }).getByRole('radio', { name: 'Sí' }).check();
    await expect(dialogoDeclaracion(page)).toBeVisible();
    await diligenciarDeclaracion(page, { firma: 'dibujar', completa: true });
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');

    // 1. El botón aparece con la declaración guardada.
    const boton = page.getByRole('button', { name: BOTON_PDF });
    await expect(boton).toBeVisible();
    await paso(page, {
      titulo: 'Encuentra el botón de descarga de tu declaración',
      explicacion:
        'Con la declaración guardada, en la sección de información general aparece el botón «Descargar el PDF de tu declaración PEP». ' +
        'Solo aparece cuando la declaración está firmada y guardada; si la estás editando y todavía no guardas, el botón no está.',
      resaltar: boton,
      variantes: [{ cuando: 'Respondiste «No» a la PEP', entonces: 'No hay declaración y tampoco aparece el botón.' }],
    });

    // 2. La descarga.
    const descarga = page.waitForEvent('download');
    await boton.click();
    const archivo = await descarga;
    expect(archivo.suggestedFilename()).toMatch(/\.pdf$/i);
    await paso(page, {
      titulo: 'Descarga el PDF y guárdalo',
      explicacion:
        'El PDF tiene tus datos de la declaración tal como quedaron firmados, con la firma que dibujaste. El PDF lleva el logo de Validasoft y, si la empresa que te invitó tiene logo, también el suyo. ' +
        'Guárdalo en tu equipo.',
      resaltar: boton,
    });
  },
);
