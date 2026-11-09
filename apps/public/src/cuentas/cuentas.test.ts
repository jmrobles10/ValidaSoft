import { describe, expect, it } from 'vitest';
import { cuerpoRegistro, DATOS_VACIOS, enmascararNumero, validarCuenta } from './cuentas';

const COMPLETOS = { banco: 'Bancolombia', tipoCuenta: 'Ahorros', numero: '1234 5678', titular: 'Ana Pérez', identificacionTitular: '1020304050', moneda: 'COP' } as const;

describe('validar una cuenta como el dominio', () => {
  it('pide banco, tipo, número, titular e identificación; la moneda es opcional', () => {
    expect(validarCuenta(DATOS_VACIOS)).toEqual({
      banco: 'banco-requerido',
      tipoCuenta: 'tipo-cuenta-invalido',
      numero: 'numero-cuenta-invalido',
      titular: 'titular-requerido',
      identificacionTitular: 'identificacion-titular-requerida',
    });
    expect(validarCuenta({ ...COMPLETOS, moneda: '' })).toEqual({});
  });

  it.each([
    ['12345', true],
    ['123456', false],
    ['123-456 7890', false],
    ['1'.repeat(20), false],
    ['1'.repeat(21), true],
    ['12345a78', true],
  ])('el número «%s» es inválido: %s', (numero, invalido) => {
    expect('numero' in validarCuenta({ ...COMPLETOS, numero })).toBe(invalido);
  });

  it('la moneda son tres letras', () => {
    expect(validarCuenta({ ...COMPLETOS, moneda: 'usd' })).toEqual({});
    expect(validarCuenta({ ...COMPLETOS, moneda: 'US' })).toEqual({ moneda: 'moneda-invalida' });
  });

  it('el cuerpo va sin espacios sobrantes y con la moneda en mayúsculas (o nula si no la escribió)', () => {
    expect(cuerpoRegistro({ ...COMPLETOS, banco: '  Bancolombia ', moneda: 'usd' })).toMatchObject({ banco: 'Bancolombia', moneda: 'USD' });
    expect(cuerpoRegistro({ ...COMPLETOS, moneda: ' ' }).moneda).toBeNull();
    expect(enmascararNumero('12-34 5678')).toBe('****5678');
  });
});
