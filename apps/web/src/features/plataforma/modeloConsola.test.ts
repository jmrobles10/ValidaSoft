import { describe, expect, it } from 'vitest';
import { borradorDesdePlan, cuerpoPlanIa, validarPlanIa } from './modelo';
import { aNumero, cuerpoDeTarifa, etiquetaDePeriodo, formatearEntero, formatearMoneda, formatearTokens, leerPrecio, periodoDe, periodosRecientes, precioDePlan, validarTarifa } from './modeloConsumo';
import { dispositivoLegible, esEventoDeIngreso, hayFiltrosDeIngresos, leerFiltrosDeIngresos, parametrosDeIngresos, rangoDeIngresosInvalido } from './modeloIngresos';

describe('periodos del consumo de IA', () => {
  it('el mes de una fecha es el de Bogotá, no el de UTC', () => {
    // 1 de diciembre 02:00 UTC es el 30 de noviembre 21:00 en Bogotá.
    expect(periodoDe(new Date('2026-12-01T02:00:00Z'))).toBe('2026-11');
    expect(periodoDe(new Date('2026-12-01T05:00:00Z'))).toBe('2026-12');
  });

  it('ofrece los últimos doce meses, del actual hacia atrás, cruzando el año', () => {
    const meses = periodosRecientes(new Date('2026-02-15T12:00:00Z'));
    expect(meses).toHaveLength(12);
    expect(meses[0]).toBe('2026-02');
    expect(meses[1]).toBe('2026-01');
    expect(meses[2]).toBe('2025-12');
    expect(meses[11]).toBe('2025-03');
  });

  it('dice el mes con palabras', () => {
    expect(etiquetaDePeriodo('2026-11')).toBe('noviembre de 2026');
    expect(etiquetaDePeriodo('2026-01')).toBe('enero de 2026');
  });
});

describe('cifras del consumo', () => {
  it('lee las cifras decimales que llegan como número o como texto', () => {
    expect(aNumero('12.5')).toBe(12.5);
    expect(aNumero(3)).toBe(3);
    expect(aNumero(null)).toBe(0);
    expect(aNumero('no es un número')).toBe(0);
  });

  it('abrevia los tokens grandes y deja enteros los pequeños', () => {
    expect(formatearTokens(950)).toBe('950');
    expect(formatearTokens(12_345)).toContain('mil');
    expect(formatearTokens(2_500_000)).toBe('2,5 M');
  });

  it('formatea enteros y monedas a la colombiana', () => {
    expect(formatearEntero(1234567)).toMatch(/1\.234\.567/);
    expect(formatearMoneda(1500, 'COP')).toMatch(/1\.500/);
    expect(formatearMoneda('2.5', 'USD')).toMatch(/2,50/);
    // Una moneda mal escrita no rompe la pantalla: cae en pesos.
    expect(formatearMoneda(100, 'xx')).toMatch(/100/);
  });
});

describe('tarifa del adicional', () => {
  it('lee precios con punto o coma y hasta dos decimales', () => {
    expect(leerPrecio('1500')).toBe(1500);
    expect(leerPrecio(' 1500,5 ')).toBe(1500.5);
    expect(leerPrecio('1500.55')).toBe(1500.55);
    for (const malo of ['', 'abc', '-1', '1.500,50', '1.234', '100000001']) expect(leerPrecio(malo)).toBeNull();
  });

  it('valida el precio y la moneda y arma el cuerpo con la moneda en mayúsculas', () => {
    expect(validarTarifa('1500', 'cop')).toEqual([]);
    expect(validarTarifa('x', 'pesos')).toEqual(['precio-invalido', 'moneda-invalida']);
    expect(cuerpoDeTarifa('1500,5', ' usd ')).toEqual({ precioPorValidacionAdicional: 1500.5, moneda: 'USD' });
  });

  it('el precio de un plan vacío usa la tarifa general (nulo) y uno mal escrito no se inventa (indefinido)', () => {
    expect(precioDePlan('')).toBeNull();
    expect(precioDePlan('  ')).toBeNull();
    expect(precioDePlan('900')).toBe(900);
    expect(precioDePlan('abc')).toBeUndefined();
  });
});

describe('precio propio en el plan de IA', () => {
  const plan = {
    empresaId: 'e1',
    configurado: true,
    validacionesIncluidasMes: 200,
    adicionalHabilitado: true,
    topeAdicionalMes: null,
    llaveConfigurada: true,
    ultimosCuatro: 'WXYZ',
    modeloPorDefecto: 'gpt-6-luna',
    modeloEscalamiento: 'gpt-6.1-terra',
    umbralConfianza: 0.85,
    activo: true,
    actualizadoEn: null,
  };

  it('un plan sin precio propio deja el campo vacío y lo envía como nulo', () => {
    const b = borradorDesdePlan(plan);
    expect(b.precio).toBe('');
    expect(cuerpoPlanIa(b).precioAdicional).toBeNull();
    expect(validarPlanIa(b)).toEqual([]);
  });

  it('con precio propio lo muestra y lo conserva al guardar', () => {
    const b = borradorDesdePlan({ ...plan, precioAdicional: '1200.00' });
    expect(b.precio).toBe('1200');
    expect(cuerpoPlanIa(b).precioAdicional).toBe(1200);
  });

  it('un precio mal escrito impide guardar (solo si el adicional está activo)', () => {
    expect(validarPlanIa({ ...borradorDesdePlan(plan), precio: '12abc' })).toEqual(['precio-invalido']);
    expect(validarPlanIa({ ...borradorDesdePlan(plan), adicional: false, precio: '12abc' })).toEqual([]);
  });
});

describe('filtros de los ingresos de operadores', () => {
  it('lee los filtros de la URL y descarta lo inválido', () => {
    const f = leerFiltrosDeIngresos(new URLSearchParams('operador=op1&evento=IngresoFallido&resultado=fallido&desde=2026-11-01&hasta=2026-11-30'));
    expect(f).toEqual({ operador: 'op1', evento: 'IngresoFallido', resultado: 'fallido', desde: '2026-11-01', hasta: '2026-11-30' });
    const malo = leerFiltrosDeIngresos(new URLSearchParams('evento=Inventado&resultado=quizas&desde=ayer&hasta=2026-13'));
    expect(malo).toEqual({ operador: '', evento: '', resultado: '', desde: '', hasta: '' });
    expect(esEventoDeIngreso('PasskeyRegistrada')).toBe(true);
    expect(esEventoDeIngreso('x')).toBe(false);
    expect(esEventoDeIngreso(null)).toBe(false);
  });

  it('sabe si hay filtros y si el rango está al revés', () => {
    expect(hayFiltrosDeIngresos(leerFiltrosDeIngresos(new URLSearchParams()))).toBe(false);
    expect(hayFiltrosDeIngresos(leerFiltrosDeIngresos(new URLSearchParams('resultado=exitoso')))).toBe(true);
    expect(rangoDeIngresosInvalido({ desde: '2026-11-10', hasta: '2026-11-01' })).toBe(true);
    expect(rangoDeIngresosInvalido({ desde: '2026-11-01', hasta: '2026-11-01' })).toBe(false);
    expect(rangoDeIngresosInvalido({ desde: '', hasta: '2026-11-01' })).toBe(false);
  });

  it('a la API solo viaja lo que se filtró, con el resultado como booleano y las fechas como límites del día', () => {
    expect(parametrosDeIngresos(leerFiltrosDeIngresos(new URLSearchParams()))).toEqual({
      operador: undefined,
      evento: undefined,
      exitoso: undefined,
      desde: undefined,
      hasta: undefined,
    });
    const p = parametrosDeIngresos(leerFiltrosDeIngresos(new URLSearchParams('resultado=exitoso&desde=2026-11-01&hasta=2026-11-02')));
    expect(p.exitoso).toBe(true);
    expect(new Date(p.desde ?? '').getTime()).toBeLessThan(new Date(p.hasta ?? '').getTime());
    expect(parametrosDeIngresos(leerFiltrosDeIngresos(new URLSearchParams('resultado=fallido'))).exitoso).toBe(false);
  });

  it('dice el dispositivo corto', () => {
    expect(dispositivoLegible('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36')).toBe('Chrome en Windows');
    expect(dispositivoLegible('Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit Safari/605')).toBe('Safari en iOS');
    expect(dispositivoLegible(null)).toBe('—');
    expect(dispositivoLegible('curl/8.0')).toBe('curl/8.0');
  });
});
