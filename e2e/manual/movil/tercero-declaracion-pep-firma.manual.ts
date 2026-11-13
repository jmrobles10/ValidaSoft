import { expect, paso, recorrido } from '../nucleo';
import { escribirFecha } from '../../soporte/fechas';
import { areaDe } from '../nucleo/semillas';
import { abrirSeccion, guardarSeccionCompleta, llenarContactoYDomicilio, publicarFlujoDeFicha } from '../../soporte/ficha';
import { dialogoDeclaracion, diligenciarDeclaracion, TITULO_DECLARACION } from '../../soporte/declaracion-pep';
import { aceptarInvitacion, entrarPorInvitacion } from '../../soporte/proveedor';
import { crearEscenario, invitar, nuevaPersonaNatural } from '../../soporte/semilla';

recorrido(
  {
    dominio: 'movil',
    actor: 'Tercero',
    titulo: 'Declara que eres una persona expuesta políticamente y firma con el dedo',
    resumen:
      'Una persona natural que responde «Sí» a la pregunta de PEP abre la declaración jurada, la completa y firma dibujando con el dedo en la pantalla del celular. La declaración queda firmada con la fecha de hoy y se guarda junto con la sección.',
    precondiciones: [
      'Una empresa que pide la sección «Información general» como requisito para personas naturales.',
      'Una persona natural invitada por esa empresa, que acepta la invitación y entra al portal.',
      'La pantalla se ve a 390 × 844 (celular).',
    ],
    relacionados: ['tercero/aceptar-invitacion-persona-juridica', 'movil/tercero-mi-informacion'],
  },
  async ({ page }) => {
    const e = await crearEscenario();
    await publicarFlujoDeFicha(e, e.sur, 'PEP_FICHA_GENERAL', areaDe(e.sur, 'revision'), 'CLIENTE', [{ seccion: 'FICHA_GENERAL' }]);
    const tercero = nuevaPersonaNatural('Ana María Pérez Gómez');
    const invitacion = await invitar(e, e.sur, tercero, 'CLIENTE');
    await entrarPorInvitacion(page, invitacion);
    await aceptarInvitacion(page, invitacion);

    await abrirSeccion(page, 'general', 'Información general');
    await llenarContactoYDomicilio(page, '300 555 1234', 'Carrera 7 # 32-16');
    await escribirFecha(page, 'Fecha de expedición de tu documento', '2012-03-20');
    await page.getByRole('textbox', { name: 'Lugar de expedición', exact: true }).fill('Bogotá');
    await page.getByRole('radiogroup', { name: '¿Tienes vínculo con una persona expuesta políticamente?' }).getByRole('radio', { name: 'No' }).check();
    const pregunta = page.getByRole('radiogroup', { name: '¿Eres una persona expuesta políticamente (PEP)?' });

    // 1. Responder «Sí» abre la declaración.
    await pregunta.getByRole('radio', { name: 'Sí' }).check();
    const dialogo = dialogoDeclaracion(page);
    await expect(dialogo).toBeVisible();
    await expect(dialogo.getByRole('textbox', { name: 'Nombre completo o razón social' })).toHaveValue('Ana María Pérez Gómez');
    await paso(page, {
      titulo: 'Responde «Sí» a la pregunta de PEP',
      explicacion:
        'Si eres una persona expuesta políticamente, responde «Sí» y se abre la «' + TITULO_DECLARACION + '». Tu nombre, tu documento, tu relación con la empresa y tu dirección ya vienen de lo que diligenciaste, y aparecen solo de lectura. ' +
        'Mientras la declaración no esté completa y firmada, la sección no se puede guardar con «Sí».',
      resaltar: dialogo,
      variantes: [
        { cuando: 'Respondes «No»', entonces: 'No se abre la declaración y la sección se guarda sin ella.' },
        { cuando: 'Cierras sin declarar', entonces: 'Validasoft pregunta si sigues diligenciando o si quieres salir sin declarar; al salir, la respuesta vuelve a «No».' },
      ],
    });

    // 2. Diligenciar y firmar dibujando.
    await diligenciarDeclaracion(page, { firma: 'dibujar', completa: true });
    await expect(page.getByText('Declaración lista: se guarda con la sección')).toBeVisible();
    await paso(page, {
      titulo: 'Firma dibujando con el dedo',
      explicacion:
        'Completa los datos del cargo o la función pública, la institución y las fechas. Al final, la firma se dibuja en el recuadro con el dedo; la vista previa muestra cómo quedará. ' +
        'Declaras bajo juramento y confirmas: la declaración queda lista y se guarda con la sección.',
      resaltar: page.getByText('Declaración lista: se guarda con la sección'),
      variantes: [
        { cuando: 'Prefieres no dibujar', entonces: 'Puedes subir una imagen de tu firma en lugar de dibujarla.' },
      ],
    });

    // 3. Guardar la sección.
    await guardarSeccionCompleta(page, 'Guardaste Información general: está completa.');
    await expect(page.getByText(/^Declaración firmada el \d{2}\/\d{2}\/\d{4}$/)).toBeVisible();
    await paso(page, {
      titulo: 'Guarda y comprueba que quedó firmada',
      explicacion:
        'Al guardar la sección, la declaración queda firmada con la fecha de hoy. Si vuelves a la pregunta, el botón «Ver o editar declaración» abre la declaración con lo que escribiste. ' +
        'La empresa recibe la declaración firmada con el expediente y puede descargar su PDF.',
      resaltar: page.getByText(/^Declaración firmada el/),
      avisos: true,
    });
  },
);
