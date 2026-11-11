import { describe, expect, it } from 'vitest';
import { HORAS_DE_SESION, minutosDeInactividad, sinCambios } from './modelo';

const base = { modos: ['Delegado', 'Directo'], sesionHoras: 8, inactividadMinutos: 30, passkeyParaTodos: false };

describe('política de ingreso del grupo', () => {
  it('ofrece cierres por inactividad que caben en la sesión', () => {
    expect(minutosDeInactividad(1)).toEqual([5, 10, 15, 30, 60]);
    expect(minutosDeInactividad(8)).toEqual([5, 10, 15, 30, 60, 120, 240, 480]);
    expect(Math.max(...minutosDeInactividad(2))).toBe(120);
    for (const horas of HORAS_DE_SESION) expect(minutosDeInactividad(horas).every((m) => m <= horas * 60)).toBe(true);
  });

  it('detecta cambios sin importar el orden de los modos', () => {
    expect(sinCambios(base, { ...base, modos: ['Directo', 'Delegado'] })).toBe(true);
    expect(sinCambios(base, { ...base, modos: ['Delegado'] })).toBe(false);
    expect(sinCambios(base, { ...base, sesionHoras: 12 })).toBe(false);
    expect(sinCambios(base, { ...base, inactividadMinutos: 15 })).toBe(false);
    expect(sinCambios(base, { ...base, passkeyParaTodos: true })).toBe(false);
  });
});
