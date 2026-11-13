import { llamar } from '../../soporte/api';
import { abrirEmbebido } from '../../soporte/embebido';
import { irASeccion } from '../../soporte/empresa';
import { abrirExpedienteManual, crearTipoDeVinculo, invitarVinculo, publicarFlujoSinDocumentos } from '../../soporte/flujos';
import { crearEscenario, nuevoTercero, type Escenario } from '../../soporte/semilla';
import { expect, paso, recorrido } from '../nucleo';

const TIPO = 'PROV_SAGRILAFT';

/** Invita a un tercero con el flujo de cumplimiento y, si hay estudio, lo registra con su nivel de riesgo y su debida diligencia. */
async function terceroConEstudio(
  e: Escenario,
  nombre: string,
  estudio: { nivelRiesgo: 1 | 2 | 3; debidaDiligencia: 'Simple' | 'Ordinaria' | 'Ampliada' } | null,
): Promise<void> {
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

recorrido(
  {
    dominio: 'cumplimiento',
    actor: 'Oficial de cumplimiento',
    titulo: 'Consulta el informe SAGRILAFT de los terceros',
    resumen:
      'El oficial de cumplimiento abre el informe SAGRILAFT de la empresa: una fila por tercero con su estudio y su riesgo, tarjetas con lo vigente, por vencer, vencido y sin estudio, la distribución del riesgo, los filtros y la descarga en Excel.',
    precondiciones: [
      'Un grupo con Constructora Norte y un tipo de vínculo de cumplimiento con un flujo que pide listas restrictivas y el estudio de cumplimiento.',
      'Tres terceros de ese tipo: dos con estudio vigente (riesgo medio y alto) y uno sin estudio.',
    ],
    relacionados: ['cumplimiento/estudio-de-cumplimiento', 'cumplimiento/bandeja-del-oficial'],
  },
  async ({ page }) => {
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

    const olga = await abrirEmbebido(page, e, e.olga, e.norte, 'cumplimiento');
    await irASeccion(olga, 'Cumplimiento');
    await olga.getByRole('tab', { name: 'Informe SAGRILAFT' }).click();
    const tarjeta = (clave: string) => olga.locator(`[data-tarjeta="${clave}"]`);
    await expect(tarjeta('terceros')).toContainText('3');
    const tabla = olga.getByRole('table', { name: 'Informe SAGRILAFT' });
    await expect(tabla.getByRole('row', { name: /Distribuidora Sin Estudio/ })).toContainText('Sin estudio');

    await paso(page, {
      titulo: 'Abre el informe SAGRILAFT',
      explicacion:
        'El informe muestra la situación de todos los terceros de la empresa frente al SAGRILAFT. Las tarjetas cuentan los terceros, los que tienen estudio vigente, los que lo tienen por vencer o vencido y los que no tienen estudio. ' +
        'Es la misma información que ves en la bandeja, pero completa y para todos los terceros, no solo los que esperan una decisión.',
      resaltar: [tarjeta('terceros'), tarjeta('sinEstudio')],
      variantes: [
        { cuando: 'Eres revisor de un área', entonces: 'No ves la pestaña del informe: el informe es del oficial de cumplimiento.' },
      ],
    });

    const riesgo = olga.getByRole('region', { name: 'Distribución del riesgo' });
    await expect(riesgo.getByRole('listitem', { name: '3 · Alto: 1' })).toBeVisible();
    await paso(page, {
      titulo: 'Revisa la distribución del riesgo y los tiempos',
      explicacion:
        'La distribución cuenta cuántos terceros hay en cada nivel de riesgo (Bajo, Medio o Alto). Los tiempos de cumplimiento muestran qué tan a tiempo están los estudios, y el gráfico de los últimos 12 meses muestra la tendencia mes a mes.',
      resaltar: [riesgo, olga.getByRole('region', { name: 'Tiempos de cumplimiento' })],
    });

    await paso(page, {
      titulo: 'Lee una fila por tercero',
      explicacion:
        'Cada fila es un tercero con su estudio: el nivel de riesgo, la debida diligencia (Simple, Ordinaria o Intensificada) y el estado del estudio. Si el tercero no tiene estudio, la fila dice «Sin estudio». ' +
        'Por ejemplo, «Constructora Alta S.A.S.» aparece en riesgo alto con debida diligencia intensificada.',
      resaltar: tabla,
    });

    await olga.getByRole('combobox', { name: 'Estudio' }).click();
    await olga.getByRole('option', { name: 'Sin estudio', exact: true }).click();
    await expect(tabla.getByRole('row')).toHaveCount(2);
    await paso(page, {
      titulo: 'Filtra por el estado del estudio',
      explicacion:
        'Los filtros reducen la tabla: con «Estudio» en «Sin estudio» quedan solo los terceros que no tienen estudio, para atenderlos. «Quitar filtros» vuelve a mostrar todos. El filtro de «Riesgo» funciona igual para encontrar, por ejemplo, los de riesgo alto.',
      resaltar: [olga.getByRole('combobox', { name: 'Estudio' }), tabla],
    });
    await olga.getByRole('button', { name: 'Quitar filtros' }).click();

    const [descarga] = await Promise.all([page.waitForEvent('download'), olga.getByRole('button', { name: 'Descargar Excel' }).click()]);
    expect(descarga.suggestedFilename()).toMatch(/^informe-sagrilaft-completo-\d{8}\.xlsx$/);
    await paso(page, {
      titulo: 'Descarga el informe en Excel',
      explicacion:
        'El botón «Descargar Excel» baja el mismo informe en un archivo con tres hojas: Terceros, Personas y Verificaciones. Lo usas para entregarlo a un auditor o para archivarlo. ' +
        'La otra vista, «Consultas y estudios», conserva el informe de consultas y estudios del periodo y se exporta también a Excel.',
      resaltar: olga.getByRole('button', { name: 'Descargar Excel' }),
    });
  },
);
