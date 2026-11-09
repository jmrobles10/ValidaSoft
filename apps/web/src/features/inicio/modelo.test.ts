import type { MisPendientesDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import { accesosPendientes, aNumero, estaAlDia, formatearConteo, formatearDias, leerVentana, textoArea } from './modelo';

const t = i18n.t.bind(i18n);

function pendientes(parcial: Partial<MisPendientesDto> = {}): MisPendientesDto {
  return {
    porArea: [],
    revalidaciones: 0,
    revalidacionesBloqueantes: 0,
    cuentasPorAprobar: 0,
    aperturasPendientes: 0,
    documentosPorVencer: 0,
    documentosVencidos: 0,
    ...parcial,
  };
}

const area = (cantidad: number, masAntiguoDias: number) => ({ areaId: 'a1', codigo: 'COMPRAS', nombre: 'Compras', cantidad, masAntiguoDias });

describe('inicio', () => {
  it('lee la ventana de la URL: 30 por defecto y lo que no sea 7, 30 o 90 vuelve al defecto', () => {
    expect(leerVentana(new URLSearchParams())).toBe(30);
    expect(leerVentana(new URLSearchParams('dias=7'))).toBe(7);
    expect(leerVentana(new URLSearchParams('dias=90'))).toBe(90);
    expect(leerVentana(new URLSearchParams('dias=15'))).toBe(30);
    expect(leerVentana(new URLSearchParams('dias=abc'))).toBe(30);
  });

  it('dice cuántos expedientes tiene el área y hace cuánto llegó el más antiguo, o que está al día', () => {
    expect(textoArea(area(3, 5), t)).toBe('Compras: 3 expedientes por revisar · el más antiguo hace 5 días');
    expect(textoArea(area(1, 1), t)).toBe('Compras: 1 expediente por revisar · el más antiguo hace 1 día');
    expect(textoArea(area(2, 0), t)).toBe('Compras: 2 expedientes por revisar · el más antiguo es de hoy');
    expect(textoArea(area(0, 0), t)).toBe('Compras: Al día');
  });

  it('los accesos solo incluyen lo pendiente, con su destino; las cuentas no tienen pantalla propia', () => {
    expect(accesosPendientes(pendientes())).toEqual([]);
    expect(
      accesosPendientes(pendientes({ revalidaciones: 4, revalidacionesBloqueantes: 1, cuentasPorAprobar: 2, aperturasPendientes: 0, documentosPorVencer: 3, documentosVencidos: 1 })),
    ).toEqual([
      { clave: 'revalidaciones', cantidad: 4, ruta: 'revalidaciones', bloqueantes: 1 },
      { clave: 'cuentas', cantidad: 2, ruta: null },
      { clave: 'porVencer', cantidad: 3, ruta: 'vencimientos' },
      { clave: 'vencidos', cantidad: 1, ruta: 'vencimientos?estado=Vencida' },
    ]);
    expect(accesosPendientes(pendientes({ aperturasPendientes: 2 }))).toEqual([{ clave: 'aperturas', cantidad: 2, ruta: 'bandeja' }]);
  });

  it('está al día si ninguna área tiene expedientes y no hay otros pendientes', () => {
    expect(estaAlDia(pendientes())).toBe(true);
    expect(estaAlDia(pendientes({ porArea: [area(0, 0)] }))).toBe(true);
    expect(estaAlDia(pendientes({ porArea: [area(1, 0)] }))).toBe(false);
    expect(estaAlDia(pendientes({ documentosVencidos: 1 }))).toBe(false);
  });

  it('los tiempos llegan como número o texto: un decimal con coma, o «—» si no hay dato', () => {
    expect(aNumero(null)).toBeNull();
    expect(aNumero('4.25')).toBe(4.25);
    expect(aNumero('x')).toBeNull();
    expect(formatearDias(4.5, '—')).toBe('4,5');
    expect(formatearDias('12', '—')).toBe('12,0');
    expect(formatearDias(null, '—')).toBe('—');
    expect(formatearConteo(1234)).toBe('1.234');
  });
});
