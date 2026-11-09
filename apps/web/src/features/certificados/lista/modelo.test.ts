import { describe, expect, it } from 'vitest';
import { fechaDelEstado, leerFiltros, parametrosConsulta } from './modelo';

describe('lista de certificados', () => {
  it('lee los filtros de la URL; un estado que no existe se ignora y la búsqueda se recorta', () => {
    expect(leerFiltros(new URLSearchParams())).toEqual({ estado: '', q: '' });
    expect(leerFiltros(new URLSearchParams('estado=Bloqueante&q=%20Norte%20'))).toEqual({ estado: 'Bloqueante', q: 'Norte' });
    expect(leerFiltros(new URLSearchParams('estado=Suspendido'))).toEqual({ estado: 'Suspendido', q: '' });
    expect(leerFiltros(new URLSearchParams('estado=Reemplazado'))).toEqual({ estado: '', q: '' });
    expect(leerFiltros(new URLSearchParams('estado=vigente'))).toEqual({ estado: '', q: '' });
  });

  it('lo vacío no viaja en la consulta', () => {
    expect(parametrosConsulta({ estado: '', q: '' }, 25, null)).toEqual({ estado: undefined, q: undefined, limite: 25, cursor: undefined });
    expect(parametrosConsulta({ estado: 'Vencido', q: 'CERT-1' }, 25, 'abc')).toEqual({ estado: 'Vencido', q: 'CERT-1', limite: 25, cursor: 'abc' });
  });

  it('el chip dice desde cuándo está en su estado, salvo el vigente', () => {
    expect(fechaDelEstado({ estado: 'Vigente', estadoDesde: '2026-10-01T12:00:00Z' })).toBeNull();
    expect(fechaDelEstado({ estado: 'Suspendido', estadoDesde: '2026-10-01T12:00:00Z' })).toBe('2026-10-01T12:00:00Z');
  });
});
