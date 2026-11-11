import { describe, expect, it } from 'vitest';
import { cumplimientoParaBarra, desgloseDeRetraso, formatearCumplimiento, hayFiltrosSla, leerFiltrosSla, parametrosSla, tonoDeCumplimiento } from './modeloSla';

describe('informe de cumplimiento del plazo de revisión: modelo', () => {
  it('lee los filtros de la URL y solo manda a la API los que tienen valor', () => {
    const vacios = leerFiltrosSla(new URLSearchParams());
    expect(vacios).toEqual({ areaId: '', tipoVinculoId: '', desde: '', hasta: '' });
    expect(hayFiltrosSla(vacios)).toBe(false);
    expect(parametrosSla(vacios)).toEqual({});

    const filtros = leerFiltrosSla(new URLSearchParams('areaId=a1&desde=2026-11-01&hasta=2026-11-30&otro=x'));
    expect(hayFiltrosSla(filtros)).toBe(true);
    expect(parametrosSla(filtros)).toEqual({ areaId: 'a1', desde: '2026-11-01', hasta: '2026-11-30' });
  });

  it('el cumplimiento llega como número o texto (decimal de .NET) y se muestra con coma y %; sin revisiones no hay dato', () => {
    expect(formatearCumplimiento(87.5, '—')).toBe('87,5 %');
    expect(formatearCumplimiento('100', '—')).toBe('100 %');
    expect(formatearCumplimiento(66.7, '—')).toBe('66,7 %');
    expect(formatearCumplimiento(null, '—')).toBe('—');
    expect(formatearCumplimiento(undefined, '—')).toBe('—');
  });

  it('la barra va de 0 a 100 y sin dato queda vacía', () => {
    expect(cumplimientoParaBarra(87.5)).toBe(87.5);
    expect(cumplimientoParaBarra('250')).toBe(100);
    expect(cumplimientoParaBarra(-3)).toBe(0);
    expect(cumplimientoParaBarra(null)).toBe(0);
  });

  it('el tono de la barra: verde desde el 90 %, ámbar desde el 70 % y rojo por debajo', () => {
    expect(tonoDeCumplimiento(100)).toBe('success');
    expect(tonoDeCumplimiento(90)).toBe('success');
    expect(tonoDeCumplimiento(89.9)).toBe('warning');
    expect(tonoDeCumplimiento(70)).toBe('warning');
    expect(tonoDeCumplimiento(69.9)).toBe('error');
    expect(tonoDeCumplimiento(null)).toBe('inherit');
  });

  it('el retraso se cuenta en horas hasta 47 y de ahí en días y horas', () => {
    expect(desgloseDeRetraso(0.4)).toEqual({ dias: 0, horas: 0 });
    expect(desgloseDeRetraso(5.6)).toEqual({ dias: 0, horas: 6 });
    expect(desgloseDeRetraso('47')).toEqual({ dias: 0, horas: 47 });
    expect(desgloseDeRetraso(48)).toEqual({ dias: 2, horas: 0 });
    expect(desgloseDeRetraso('75.2')).toEqual({ dias: 3, horas: 3 });
    expect(desgloseDeRetraso(null)).toEqual({ dias: 0, horas: 0 });
  });
});
