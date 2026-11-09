import { describe, expect, it } from 'vitest';
import { situacionDelVinculo } from './situacion';

describe('situación del vínculo para el tercero', () => {
  it('bloqueado es suspendida; desvinculado y anulado, terminada; lo demás, vigente', () => {
    expect(situacionDelVinculo('Bloqueado')).toBe('suspendida');
    expect(situacionDelVinculo('Desvinculado')).toBe('terminada');
    expect(situacionDelVinculo('Anulado')).toBe('terminada');
    for (const estado of ['Invitado', 'EnValidacion', 'Activo', 'PorVencer', 'Vencido'] as const) expect(situacionDelVinculo(estado)).toBe('vigente');
    expect(situacionDelVinculo(undefined)).toBe('vigente');
  });
});
