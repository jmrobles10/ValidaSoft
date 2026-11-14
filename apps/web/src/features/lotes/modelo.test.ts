import type { FilaVistaPreviaDto, TotalesLoteDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { diasRestantes } from '../expedientes/plazo';
import { plazoADto } from '../flujos/editor';
import { aBase64, accionesDeLote, conProblemas, enCurso, identificacionLegible, porcentaje, procesables } from './modelo';

const totales = (cambios: Partial<TotalesLoteDto> = {}): TotalesLoteDto => ({
  filas: 10, procesadas: 0, pendientes: 7, exitosas: 0, fallidas: 0, duplicadas: 1, invalidas: 2, ...cambios,
});

describe('lote de invitación masiva', () => {
  it('el avance se mide sobre las filas que se procesan (sin inválidas ni repetidas)', () => {
    expect(procesables(totales())).toBe(7);
    expect(porcentaje(totales())).toBe(0);
    expect(porcentaje(totales({ procesadas: 3, pendientes: 4, exitosas: 3 }))).toBe(43);
    expect(porcentaje(totales({ procesadas: 7, pendientes: 0, exitosas: 6, fallidas: 1 }))).toBe(100);
    expect(porcentaje(totales({ filas: 3, pendientes: 0, duplicadas: 1, invalidas: 2 }))).toBe(100);
  });

  it('las acciones dependen del estado', () => {
    expect(accionesDeLote({ estado: 'Procesando', totales: totales() })).toEqual(['pausar', 'cancelar']);
    expect(accionesDeLote({ estado: 'Pausado', totales: totales() })).toEqual(['reanudar', 'cancelar']);
    // Por aprobar (decisión AI): no se pausa ni se reanuda; quien lo creó puede retirarlo mientras espera.
    expect(accionesDeLote({ estado: 'PorAprobar', totales: totales() })).toEqual(['cancelar']);
    expect(enCurso({ estado: 'PorAprobar' })).toBe(false);
    expect(accionesDeLote({ estado: 'CompletadoConErrores', totales: totales({ fallidas: 2 }) })).toEqual(['reintentar']);
    expect(accionesDeLote({ estado: 'Completado', totales: totales() })).toEqual([]);
    expect(accionesDeLote({ estado: 'Cancelado', totales: totales({ fallidas: 2 }) })).toEqual([]);
    expect(enCurso({ estado: 'Procesando' })).toBe(true);
    expect(enCurso({ estado: 'Pausado' })).toBe(false);
  });

  it('la vista previa filtra lo que no se va a invitar', () => {
    const fila = (n: number, estado: string): FilaVistaPreviaDto => ({ n, estado, datos: {}, errores: [], duplicadaDe: null, accionPrevista: null });
    expect(conProblemas([fila(1, 'Valida'), fila(2, 'Invalida'), fila(3, 'Duplicada')]).map((f) => f.n)).toEqual([2, 3]);
  });

  it('el archivo viaja en base64 sin el prefijo data:', async () => {
    expect(await aBase64(new Blob(['año;ñ']))).toBe(btoa(String.fromCharCode(...new TextEncoder().encode('año;ñ'))));
  });

  it('muestra la identificación normalizada sin el país', () => {
    expect(identificacionLegible('CO|NIT|900123456')).toBe('NIT 900123456');
    expect(identificacionLegible('NIT 1')).toBe('NIT 1');
  });
});

describe('plazo de cargue', () => {
  it('cuenta los días que faltan hacia arriba y nunca negativos', () => {
    const ahora = Date.parse('2026-11-10T12:00:00Z');
    expect(diasRestantes('2026-11-25T12:00:00Z', ahora)).toBe(15);
    expect(diasRestantes('2026-11-10T18:00:00Z', ahora)).toBe(1);
    expect(diasRestantes('2026-11-01T00:00:00Z', ahora)).toBe(0);
  });

  it('el editor del flujo envía días y recordatorios ordenados sin repetir', () => {
    expect(plazoADto({ plazoDias: '20', plazoRecordatorios: '1, 7 7;3' })).toEqual({ dias: 20, recordatorios: [7, 3, 1] });
    expect(plazoADto({ plazoDias: '0', plazoRecordatorios: '' })).toEqual({ dias: 0, recordatorios: [] });
    expect(plazoADto({ plazoDias: 'x', plazoRecordatorios: 'a' })).toEqual({ dias: 30, recordatorios: [] });
  });
});
