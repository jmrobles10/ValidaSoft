import type { FilaBandejaDto, RecordatoriosEnviadosDto } from '@validasoft/api-client';
import { createInstance } from 'i18next';
import { describe, expect, it } from 'vitest';
import { esCO } from '../../i18n/es-CO';
import { diasValidos } from './automatico';
import { mensajeDelLote, observadosDe, recordadoHoy } from './modelo';

const fila = (expedienteId: string, areaId: string, estadoExpediente: FilaBandejaDto['estadoExpediente']): FilaBandejaDto => ({
  expedienteId,
  folio: `N-${expedienteId}`,
  vinculoId: 'v',
  tercero: { id: 't', nombre: 'Aceros', identificacion: 'NIT 9' },
  tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
  areaId,
  areaCodigo: 'A',
  areaNombre: 'Compras',
  etapa: 1,
  estadoArea: 'Pendiente',
  estadoExpediente,
  aprobaciones: 0,
  aprobadoresRequeridos: 1,
  desde: null,
  actualizadoEn: '2026-11-10T12:00:00Z',
  requisitosPendientes: 0,
});

async function traductor() {
  const i18n = createInstance();
  await i18n.init({ lng: 'es-CO', resources: { 'es-CO': { translation: esCO } }, interpolation: { escapeValue: false } });
  return i18n.t.bind(i18n);
}

describe('recordatorio de observaciones', () => {
  it('«hoy» es el día de calendario de Bogotá, no el de UTC', () => {
    // 2026-11-10 23:30 en Bogotá = 2026-11-11 04:30 UTC.
    const ahora = new Date('2026-11-11T04:30:00Z');
    expect(recordadoHoy('2026-11-10T15:00:00Z', ahora)).toBe(true);
    expect(recordadoHoy('2026-11-11T02:00:00Z', ahora)).toBe(true); // 21:00 de Bogotá del 10
    expect(recordadoHoy('2026-11-10T04:00:00Z', ahora)).toBe(false); // 23:00 de Bogotá del 9
    expect(recordadoHoy(null, ahora)).toBe(false);
    expect(recordadoHoy(undefined, ahora)).toBe(false);
  });

  it('de las filas de la bandeja toma los expedientes observados, uno por expediente aunque salga por cada área', () => {
    const filas = [fila('e1', 'a1', 'Observado'), fila('e1', 'a2', 'Observado'), fila('e2', 'a1', 'EnRevision'), fila('e3', 'a1', 'Observado')];
    expect(observadosDe(filas)).toEqual(['e1', 'e3']);
    expect(observadosDe([])).toEqual([]);
  });

  it('el resumen del lote dice a cuántos se recordó y por qué se omitió el resto', async () => {
    const t = await traductor();
    const resultado: RecordatoriosEnviadosDto = {
      enviados: 2,
      omitidos: 3,
      detalle: [
        { expedienteId: 'a', folio: 'A', resultado: 'Enviado' },
        { expedienteId: 'b', folio: 'B', resultado: 'SoloCampana' },
        { expedienteId: 'c', folio: 'C', resultado: 'YaRecordadoHoy' },
        { expedienteId: 'd', folio: 'D', resultado: 'YaRecordadoHoy' },
        { expedienteId: 'e', folio: 'E', resultado: 'NoObservado' },
      ],
    };
    expect(mensajeDelLote(resultado, t)).toEqual({
      mensaje: 'Recordamos a 2 terceros. 3 omitidos: 2 ya se les recordó hoy, 1 ya no están devueltos al tercero.',
      severidad: 'success',
    });
    expect(mensajeDelLote({ enviados: 1, omitidos: 0, detalle: [{ expedienteId: 'a', folio: 'A', resultado: 'Enviado' }] }, t).mensaje).toBe('Recordamos a 1 tercero.');
    expect(mensajeDelLote({ enviados: 0, omitidos: 0, detalle: [] }, t)).toEqual({
      mensaje: 'No hay terceros por recordar: nadie tiene observaciones pendientes o ya se les recordó hoy.',
      severidad: 'info',
    });
    expect(mensajeDelLote({ enviados: 0, omitidos: 1, detalle: [{ expedienteId: 'c', folio: 'C', resultado: 'YaRecordadoHoy' }] }, t).severidad).toBe('info');
  });

  it('el recordatorio automático admite de 1 a 60 días enteros', () => {
    expect(['1', '3', '60', ' 7 '].map(diasValidos)).toEqual([true, true, true, true]);
    expect(['', '0', '61', '2,5', '-1', 'abc', '100', '1e1'].map(diasValidos)).toEqual([false, false, false, false, false, false, false, false]);
  });
});
