import { describe, expect, it } from 'vitest';
import { aNumero, fechaNegocio, formatearDias, hayFiltrosEstado, leerFiltrosEstado, parametrosEstado, rangoInvalido, rutaInformeDeVentana, sinPaginacion } from './modelo';

describe('filtros del informe de estado', () => {
  it('lee la URL y solo envía lo que tiene valor; la fecha de cierre viaja, la de apertura es la de siempre', () => {
    const filtros = leerFiltrosEstado(new URLSearchParams('estado=Aprobado&fecha=Cierre&desde=2026-11-01&areaId=a1'));
    expect(filtros.fecha).toBe('Cierre');
    expect(hayFiltrosEstado(filtros)).toBe(true);
    expect(parametrosEstado(filtros)).toEqual({ estado: 'Aprobado', areaId: 'a1', desde: '2026-11-01', fecha: 'Cierre' });

    const vacios = leerFiltrosEstado(new URLSearchParams('fecha=Otra'));
    expect(vacios.fecha).toBe('Apertura');
    expect(hayFiltrosEstado(vacios)).toBe(false);
    expect(parametrosEstado(vacios)).toEqual({});
  });

  it('marca el rango al revés y la descarga quita la paginación', () => {
    expect(rangoInvalido({ desde: '2026-11-05', hasta: '2026-11-01' })).toBe(true);
    expect(rangoInvalido({ desde: '2026-11-01', hasta: '' })).toBe(false);
    expect(sinPaginacion({ estado: 'Aprobado', limite: 50, cursor: 'abc' })).toEqual({ estado: 'Aprobado' });
  });
});

describe('días e Inicio', () => {
  it('formatea días con coma decimal y tolera el decimal como texto', () => {
    expect(formatearDias(4.25, '—')).toBe('4,3');
    expect(formatearDias('2', '—')).toBe('2,0');
    expect(formatearDias(null, '—')).toBe('—');
    expect(aNumero('x')).toBeNull();
  });

  it('el enlace de Inicio usa la misma ventana de los indicadores: hoy y los días anteriores en Bogotá', () => {
    // 10/11/2026 a las 2 a. m. UTC: en Bogotá aún es el 9 de noviembre.
    const ahora = new Date(Date.UTC(2026, 10, 10, 2, 0));
    expect(fechaNegocio(0, ahora)).toBe('2026-11-09');
    expect(rutaInformeDeVentana('emp', 7, ahora)).toBe('/e/emp/informes/estado-expedientes?fecha=Cierre&estado=Aprobado&desde=2026-11-03');
  });
});
