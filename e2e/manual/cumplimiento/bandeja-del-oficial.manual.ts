import { aprobarUltimaArea, enRevision, esperarListas, pedirVerificaciones, personaEmbebida } from '../../soporte/cumplimiento';
import { abrirEmbebido } from '../../soporte/embebido';
import { abrirDesdeBandeja, aprobarArea, irASeccion } from '../../soporte/empresa';
import { crearEscenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const CON_COINCIDENCIA = 'Ferretería Coincidencia S.A.S.';
const SIN_COINCIDENCIAS = 'Aceros del Norte S.A.S.';

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Atiende lo pendiente de Cumplimiento desde su bandeja',
    resumen:
      'La bandeja de Cumplimiento reúne lo que el oficial debe atender: listas por consultar, coincidencias por analizar, estudios pendientes o vencidos y monitoreos con novedades. Cada fila indica qué falta y dice cuándo es lo único que impide aprobar.',
    precondiciones: [
      'Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas y estudio de cumplimiento bloqueantes.',
      'Dos proveedores en revisión: uno con una coincidencia por analizar y Compras ya aprobada; otro sin coincidencias.',
    ],
    relacionados: ['cumplimiento/analizar-una-coincidencia', 'cumplimiento/informe-sagrilaft'],
  },
  async ({ page, browser }) => {
    const e = await crearEscenario();
    await pedirVerificaciones(e, [
      { tipo: 'ListasRestrictivas', bloqueante: true },
      { tipo: 'EstudioDeCumplimiento', bloqueante: true },
    ]);
    const pA = await (await browser.newContext()).newPage();
    await enRevision(pA, e, CON_COINCIDENCIA);
    await enRevision(await (await browser.newContext()).newPage(), e, SIN_COINCIDENCIAS);
    const ruben = await personaEmbebida(browser, e, e.ruben);
    await abrirDesdeBandeja(ruben, CON_COINCIDENCIA, 'Compras');
    await aprobarArea(ruben, 'Compras');

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await expect(olga.getByRole('heading', { level: 1, name: 'Cumplimiento' })).toBeVisible();
    // Las consultas de listas son automáticas: recarga la bandeja hasta que ambos expedientes aparezcan.
    await expect(async () => {
      await irASeccion(olga, 'Vínculos');
      await irASeccion(olga, 'Cumplimiento');
      await expect(olga.getByRole('row').filter({ hasText: CON_COINCIDENCIA })).toBeVisible({ timeout: 1500 });
      await expect(olga.getByRole('row').filter({ hasText: SIN_COINCIDENCIAS })).toBeVisible({ timeout: 1500 });
    }).toPass({ timeout: 60_000 });
    await paso(page, {
      titulo: 'Abre la bandeja de Cumplimiento',
      explicacion:
        'La pestaña «Por atender» lista los expedientes que esperan algo de Cumplimiento. Cada fila muestra el tercero, el folio, el origen, lo que falta («Por atender»), cuántas coincidencias hay y desde cuándo. ' +
        'Si no hay nada, la bandeja dice «No hay nada por atender». La pestaña «Informe SAGRILAFT» reúne la situación de todos los terceros.',
      resaltar: olga.getByRole('table').first(),
      variantes: [
        { cuando: 'Eres oficial de cumplimiento', entonces: 'Ves la bandeja completa de la empresa.' },
      ],
    });

    // Tu área aprueba el expediente con la coincidencia: lo único que queda es esa decisión.
    await esperarListas(olga, CON_COINCIDENCIA, 'Cumplimiento', 'Coincidencia por analizar');
    await aprobarUltimaArea(olga, 'Cumplimiento', /Listas restrictivas \(coincidencia por analizar\)/);
    await irASeccion(olga, 'Cumplimiento');
    const filaCoincidencia = olga.getByRole('row').filter({ hasText: CON_COINCIDENCIA });
    await expect(filaCoincidencia).toContainText('Solo falta esto para aprobar');
    await paso(page, {
      titulo: 'Identifica lo que impide aprobar',
      explicacion:
        'Cuando lo único que falta para aprobar es una verificación de Cumplimiento, la fila lo dice con «Solo falta esto para aprobar». ' +
        'Aquí la coincidencia de «Ferretería Coincidencia S.A.S.» es lo último: Compras ya aprobó y, al decidirla y aprobar tu área, el expediente se cierra. «Aceros del Norte S.A.S.» aparece también, con el estudio de cumplimiento pendiente.',
      resaltar: filaCoincidencia,
    });

    await filaCoincidencia.getByRole('link', { name: 'Abrir' }).click();
    await expect(olga.getByRole('heading', { level: 1, name: CON_COINCIDENCIA })).toBeVisible();
    await irASeccion(olga, 'Cumplimiento');
    await expect(olga.getByRole('row').filter({ hasText: SIN_COINCIDENCIAS })).toBeVisible();
  },
);
