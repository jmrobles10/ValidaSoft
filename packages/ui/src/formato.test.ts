import { describe, expect, it } from 'vitest';
import { formatearDiaCivil, formatearFecha } from './formato';

describe('formatearDiaCivil', () => {
  it('escribe un día de calendario tal cual, sin correrlo al día anterior por la zona horaria', () => {
    // Como `Date`, la medianoche UTC de este día es el 25 de noviembre en Bogotá: un plazo de ley no puede mostrarse un día antes.
    expect(formatearFecha('2026-11-26')).toBe('25/11/2026');
    expect(formatearDiaCivil('2026-11-26')).toBe('26/11/2026');
    expect(formatearDiaCivil('2027-01-01')).toBe('01/01/2027');
  });

  it('una fecha con hora se muestra en la zona del tenant, como formatearFecha', () => {
    expect(formatearDiaCivil('2026-11-26T03:00:00Z')).toBe(formatearFecha('2026-11-26T03:00:00Z'));
    expect(formatearDiaCivil('2026-11-26T03:00:00Z')).toBe('25/11/2026');
    expect(formatearDiaCivil('2026-11-26T17:00:00Z')).toBe('26/11/2026');
  });
});
