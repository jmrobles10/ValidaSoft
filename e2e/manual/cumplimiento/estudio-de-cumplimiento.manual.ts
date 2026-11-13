import { aprobarUltimaArea, enRevision, pedirVerificaciones, personaEmbebida } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea } from '../../soporte/empresa';
import { esperarEstadoDelTercero } from '../../soporte/proveedor';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const NOMBRE = 'Aceros del Norte S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Registra el estudio de cumplimiento de un proveedor',
    resumen:
      'La oficial de cumplimiento registra el estudio SAGRILAFT de un proveedor de riesgo alto: elige el nivel, declara si hay conflicto de interés, ajusta la debida diligencia y da un concepto. Al registrarlo, el último pendiente se cumple y el vínculo se aprueba.',
    precondiciones: [
      'Un grupo con Constructora Norte, sus áreas Compras (Rubén) y Cumplimiento (Olga), con un flujo de proveedores que pide listas restrictivas y el estudio de cumplimiento, ambos bloqueantes.',
      'Un proveedor sin coincidencias que ya entregó sus documentos y tiene aprobadas Compras y Cumplimiento: falta solo el estudio.',
    ],
    relacionados: ['cumplimiento/estudio-vencido-y-renovacion', 'cumplimiento/adjuntos-del-estudio'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [
      { tipo: 'ListasRestrictivas', bloqueante: true },
      { tipo: 'EstudioDeCumplimiento', bloqueante: true },
    ]);
    const pTercero = await (await browser.newContext()).newPage();
    await enRevision(pTercero, e, NOMBRE);
    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, NOMBRE, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await abrirDesdeBandeja(olga, NOMBRE, 'Cumplimiento');
    await aprobarUltimaArea(olga, 'Cumplimiento', /Estudio de cumplimiento \(pendiente\)/);
    await esperarEstadoDelTercero(pTercero, 'EnRevision');

    await olga.getByRole('button', { name: 'Registrar estudio' }).click();
    const dialogo = olga.getByRole('dialog', { name: /Estudio de cumplimiento/ });
    await expect(dialogo).toBeVisible();
    await paso(page, {
      titulo: 'Abre el estudio de cumplimiento',
      explicacion:
        'El estudio es el análisis SAGRILAFT del tercero que hace el oficial. Un recuadro explica qué pasará al registrarlo: si es lo último que falta, el expediente se aprueba; si el concepto es desfavorable, la verificación queda fallida y el expediente no se aprueba. ' +
        'El estudio queda vigente el número de días que fija el flujo y, al vencer, hay que volver a hacerlo.',
      resaltar: dialogo,
      configuracion: {
        ajuste: 'Vigencia del estudio de cumplimiento',
        ruta: 'Configuración › Flujos aprobatorios (/flujos)',
        efecto: 'Define en el flujo cuántos días vale el estudio y cuándo se pide.',
      },
    });

    await dialogo.getByRole('button', { name: '3 · Alto' }).click();
    await dialogo.getByRole('combobox', { name: 'Concepto' }).click();
    await olga.getByRole('option', { name: 'Favorable', exact: true }).click();
    await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
    await expect(dialogo.getByText('Con riesgo alto la debida diligencia debe ser intensificada.')).toBeVisible();
    await paso(page, {
      titulo: 'Elige el nivel de riesgo y el concepto',
      explicacion:
        'Eliges el nivel de riesgo (Bajo, Medio o Alto) y el concepto (Favorable o Desfavorable). Si la empresa tiene una matriz de riesgo activa, el nivel sale del puntaje de sus factores y solo puedes ajustarlo con una justificación. ' +
        'Con riesgo alto, la debida diligencia no puede ser simplificada: el sistema lo impide y muestra el mensaje en rojo antes de registrar.',
      resaltar: [dialogo.getByRole('button', { name: '3 · Alto' }), dialogo.getByRole('combobox', { name: 'Debida diligencia' })],
      variantes: [
        { cuando: 'La empresa tiene una matriz de riesgo activa', entonces: 'Aparece el resultado de la matriz (puntaje, nivel, debida diligencia y revisión en meses) y, para cambiar el nivel, debes justificarlo.' },
        { cuando: 'No hay matriz activa', entonces: 'Eliges el nivel a mano, sin cálculo, y la debida diligencia mínima depende del nivel.' },
        { cuando: 'El concepto es «Desfavorable»', entonces: 'Debes escribir las observaciones y la verificación queda fallida: el expediente no se aprueba.' },
      ],
      configuracion: {
        ajuste: 'Matriz de riesgo SAGRILAFT',
        ruta: 'Configuración › Matriz de riesgo',
        efecto: 'Sus factores, pesos y límites de nivel calculan el riesgo del tercero en cada estudio.',
      },
    });

    await dialogo.getByRole('combobox', { name: 'Debida diligencia' }).click();
    await olga.getByRole('option', { name: 'Intensificada' }).click();
    await dialogo.getByRole('textbox', { name: 'Observaciones' }).fill('Sector de alto riesgo; sin hallazgos en listas ni prensa.');
    await paso(page, {
      titulo: 'Declara conflictos y registra el concepto',
      explicacion:
        'Marca si hay conflicto de interés con alguien de la empresa y escribe las observaciones que sustentan el concepto; si declaras un conflicto, las observaciones son obligatorias. ' +
        'Con la debida diligencia intensificada el riesgo alto queda cubierto. Al registrar, el estudio se guarda con tu nombre y la fecha, y se muestra en la sección «Estudio de cumplimiento» del expediente.',
      resaltar: [dialogo.getByRole('combobox', { name: 'Debida diligencia' }), dialogo.getByRole('checkbox', { name: 'Hay conflicto de interés' }), dialogo.getByRole('textbox', { name: 'Observaciones' })],
      variantes: [
        { cuando: 'Marcas conflicto de interés', entonces: 'El estudio queda registrado con esa declaración y el informe SAGRILAFT la muestra.' },
      ],
    });

    await dialogo.getByRole('button', { name: 'Registrar estudio' }).click();
    await expect(dialogo).toBeHidden();
    await expect(olga.getByText(/Todas las áreas aprobaron el .* y el vínculo quedó activo\./)).toBeVisible();
    await paso(page, {
      titulo: 'Revisa el estudio registrado y el vínculo aprobado',
      explicacion:
        'Al registrar, el estudio queda con el nivel «3 · Alto» y su vigencia, y como era lo último que faltaba, el expediente se aprueba y el vínculo queda activo. El tercero recibe el correo con el resultado.',
      resaltar: olga.getByRole('region', { name: 'Estudio de cumplimiento' }),
    });
    await esperarEstadoDelTercero(pTercero, 'Aprobado');
  },
);
