import { describe, expect, it } from 'vitest';
import { estadoVisibleDelVinculo, parametrosDeEstado } from './estadoVisible';

describe('estado que ve la empresa de un vínculo (VR-1)', () => {
  it('con una actualización abierta es «En revisión» aunque su estado de dominio no cambie', () => {
    expect(estadoVisibleDelVinculo({ estado: 'Activo', enRevision: true })).toBe('EnRevision');
    expect(estadoVisibleDelVinculo({ estado: 'PorVencer', enRevision: true })).toBe('EnRevision');
    expect(estadoVisibleDelVinculo({ estado: 'Vencido', enRevision: true })).toBe('EnRevision');
  });

  it('sin ella (o sin la marca, de una API anterior) es el estado de siempre', () => {
    expect(estadoVisibleDelVinculo({ estado: 'Activo', enRevision: false })).toBe('Activo');
    expect(estadoVisibleDelVinculo({ estado: 'Activo' })).toBe('Activo');
    expect(estadoVisibleDelVinculo({ estado: 'Bloqueado' })).toBe('Bloqueado');
  });

  it('el filtro «En revisión» pide solo esos y cualquier otro estado excluye a los que se ven «En revisión»', () => {
    expect(parametrosDeEstado('EnRevision')).toEqual({ enRevision: true });
    expect(parametrosDeEstado('Activo')).toEqual({ estado: 'Activo', enRevision: false });
    expect(parametrosDeEstado('')).toEqual({});
  });
});
