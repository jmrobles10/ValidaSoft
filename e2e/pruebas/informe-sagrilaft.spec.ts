import { expect, test } from '@playwright/test';
import { llamar } from '../soporte/api';
import { captura, personaEmbebida } from '../soporte/cumplimiento';
import { irASeccion } from '../soporte/empresa';
import { abrirExpedienteManual, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario } from '../soporte/semilla';

/**
 * Informe SAGRILAFT completo (CU-19): una fila por tercero con su estudio, su riesgo y su próxima revisión, las tarjetas de lo
 * vigente, por vencer, vencido y sin estudio, la distribución del riesgo, la tendencia, los filtros y el Excel de tres hojas.
 * Tres terceros con un flujo de solo cumplimiento: dos con estudio (riesgo medio y alto) y uno sin estudio.
 */

const TIPO = 'PROV_SAGRILAFT';

async function terceroConEstudio(e: Escenario, nombre: string, estudio: { nivelRiesgo: 1 | 2 | 3; debidaDiligencia: 'Simple' | 'Ordinaria' | 'Ampliada' } | null): Promise<void> {
  const invitacion = await invitarVinculo(e, e.norte, nuevoTercero(nombre), TIPO);
  const expedienteId = await abrirExpedienteManual(e, invitacion);
  if (estudio === null) return;
  await llamar('POST', `/api/v1/expedientes/${expedienteId}/verificaciones/estudio-cumplimiento`, {
    llave: e.llave,
    empresa: e.norte.id,
    delegado: e.delegado(e.olga),
    cuerpo: { ...estudio, conflictoDeInteres: false, concepto: 'Favorable', observaciones: 'Sin hallazgos en listas ni prensa.', evidencias: [] },
  });
}

test('el oficial ve la situación SAGRILAFT de sus terceros, filtra por estado y riesgo y descarga el Excel', async ({ browser }) => {
  const e = await crearEscenario();
  await crearTipoDeVinculo(e, TIPO, 'Proveedor SAGRILAFT');
  const cumplimiento = e.norte.areas.cumplimiento;
  if (!cumplimiento) throw new Error('Falta el área Cumplimiento de Norte.');
  const flujoId = await publicarFlujoSinDocumentos(e, e.norte, 'SAGRILAFT', TIPO, [[{ area: cumplimiento }]]);
  await llamar('PUT', `/api/v1/flujos/${flujoId}`, {
    llave: e.admin,
    empresa: e.norte.id,
    cuerpo: { verificaciones: [{ tipo: 'ListasRestrictivas', bloqueante: true }, { tipo: 'EstudioDeCumplimiento', bloqueante: true }] },
  });
  await llamar('POST', `/api/v1/flujos/${flujoId}/publicar`, { llave: e.admin, empresa: e.norte.id });

  await terceroConEstudio(e, 'Aceros Medios S.A.S.', { nivelRiesgo: 2, debidaDiligencia: 'Ordinaria' });
  await terceroConEstudio(e, 'Constructora Alta S.A.S.', { nivelRiesgo: 3, debidaDiligencia: 'Ampliada' });
  await terceroConEstudio(e, 'Distribuidora Sin Estudio S.A.S.', null);

  const olga = await personaEmbebida(browser, e, e.olga);
  await irASeccion(olga, 'Cumplimiento');
  await olga.getByRole('tab', { name: 'Informe SAGRILAFT' }).click();

  // Las tarjetas cuentan lo que hay.
  const tarjeta = (clave: string) => olga.locator(`[data-tarjeta="${clave}"]`);
  await expect(tarjeta('terceros')).toContainText('3');
  await expect(tarjeta('conEstudioVigente')).toContainText('2');
  await expect(tarjeta('sinEstudio')).toContainText('1');
  await expect(tarjeta('estudiosVencidos')).toContainText('0');

  // La distribución del riesgo, los tiempos y la tendencia de 12 meses.
  const riesgo = olga.getByRole('region', { name: 'Distribución del riesgo' });
  await expect(riesgo.getByRole('listitem', { name: '2 · Medio: 1' })).toBeVisible();
  await expect(riesgo.getByRole('listitem', { name: '3 · Alto: 1' })).toBeVisible();
  await expect(olga.getByRole('region', { name: 'Tiempos de cumplimiento' })).toBeVisible();
  await expect(olga.getByRole('table', { name: 'Últimos 12 meses' })).toContainText('2');

  // Una fila por tercero, con el estado de su estudio y su riesgo.
  const tabla = olga.getByRole('table', { name: 'Informe SAGRILAFT' });
  await expect(tabla.getByRole('row', { name: /Aceros Medios/ })).toContainText('2 · Medio');
  await expect(tabla.getByRole('row', { name: /Aceros Medios/ })).toContainText('Ordinaria');
  await expect(tabla.getByRole('row', { name: /Constructora Alta/ })).toContainText('3 · Alto');
  await expect(tabla.getByRole('row', { name: /Constructora Alta/ })).toContainText('Intensificada');
  await expect(tabla.getByRole('row', { name: /Distribuidora Sin Estudio/ })).toContainText('Sin estudio');
  await olga.owner().page().screenshot({ path: captura('informe-sagrilaft-web'), fullPage: true });

  // Filtros por estado del estudio y por nivel de riesgo.
  await olga.getByRole('combobox', { name: 'Estudio' }).click();
  await olga.getByRole('option', { name: 'Sin estudio', exact: true }).click();
  await expect(tabla.getByRole('row')).toHaveCount(2);
  await expect(tabla).toContainText('Distribuidora Sin Estudio');
  await olga.getByRole('button', { name: 'Quitar filtros' }).click();
  await olga.getByRole('combobox', { name: 'Riesgo' }).click();
  await olga.getByRole('option', { name: '3 · Alto' }).click();
  await expect(tabla.getByRole('row')).toHaveCount(2);
  await expect(tabla).toContainText('Constructora Alta');
  await olga.getByRole('button', { name: 'Quitar filtros' }).click();
  await expect(tabla.getByRole('row')).toHaveCount(4);

  // En el celular las tarjetas se apilan y la tabla se desplaza.
  const pagina = olga.owner().page();
  await pagina.setViewportSize({ width: 390, height: 900 });
  await expect(tarjeta('terceros')).toBeVisible();
  await pagina.screenshot({ path: captura('informe-sagrilaft-movil'), fullPage: true });
  await pagina.setViewportSize({ width: 1280, height: 720 });

  // El mismo informe en Excel (hojas Terceros, Personas y Verificaciones).
  const [descarga] = await Promise.all([pagina.waitForEvent('download'), olga.getByRole('button', { name: 'Descargar Excel' }).click()]);
  expect(descarga.suggestedFilename()).toMatch(/^informe-sagrilaft-completo-\d{8}\.xlsx$/);

  // La otra vista conserva el informe de consultas y estudios del periodo.
  await olga.getByRole('button', { name: 'Consultas y estudios' }).click();
  await expect(olga.getByRole('button', { name: 'Exportar a Excel' })).toBeVisible();
});
