import type { TFunction } from 'i18next';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { leerFiltros, limiteDelDia, parametrosConsulta, rangoInvalido, textoActor, textoValor } from './modelo';

const t = i18n.t.bind(i18n) as TFunction;

describe('auditoría', () => {
  it('lee los filtros de la URL; tipo, entidad y fechas inválidos se ignoran', () => {
    expect(leerFiltros(new URLSearchParams())).toEqual({ tipo: 'Configuracion', entidad: '', entidadId: '', actor: '', desde: '', hasta: '' });
    expect(leerFiltros(new URLSearchParams('tipo=Dominio&entidad=Webhook&desde=1-11-2026'))).toEqual({
      tipo: 'Configuracion',
      entidad: 'Webhook',
      entidadId: '',
      actor: '',
      desde: '',
      hasta: '',
    });
    // Webhook no es una entidad de accesos.
    expect(leerFiltros(new URLSearchParams('tipo=Acceso&entidad=Webhook&actor=%20u1%20&hasta=2026-11-09'))).toMatchObject({
      tipo: 'Acceso',
      entidad: '',
      actor: 'u1',
      hasta: '2026-11-09',
    });
  });

  it('las fechas viajan como inicio y fin del día en la zona del navegador; lo vacío y el tipo por defecto no viajan', () => {
    const filtros = leerFiltros(new URLSearchParams('desde=2026-11-01&hasta=2026-11-09'));
    expect(parametrosConsulta(filtros, 50, null)).toEqual({
      tipo: undefined,
      entidad: undefined,
      entidadId: undefined,
      actor: undefined,
      desde: new Date(2026, 10, 1).toISOString(),
      hasta: new Date(2026, 10, 9, 23, 59, 59, 999).toISOString(),
      limite: 50,
      cursor: undefined,
    });
    expect(limiteDelDia('2026-02-28', 'inicio')).toBe(new Date(2026, 1, 28).toISOString());
    expect(parametrosConsulta(leerFiltros(new URLSearchParams('tipo=Acceso')), 50, 'c2')).toMatchObject({ tipo: 'Acceso', cursor: 'c2' });
  });

  it('detecta un rango con «desde» posterior a «hasta»', () => {
    expect(rangoInvalido({ desde: '2026-11-09', hasta: '2026-11-01' })).toBe(true);
    expect(rangoInvalido({ desde: '2026-11-01', hasta: '2026-11-01' })).toBe(false);
    expect(rangoInvalido({ desde: '2026-11-09', hasta: '' })).toBe(false);
  });

  it('el actor se muestra por nombre o por tipo con el id corto', () => {
    expect(textoActor({ tipo: 'Usuario', id: 'u-1', nombre: 'Ana Pérez', sistema: null }, t)).toBe('Ana Pérez');
    expect(textoActor({ tipo: 'Integracion', id: '0f8fad5b-d9cb-469f-a165-70867728950e', nombre: null, sistema: 'SRM' }, t)).toBe('Integración 0f8fad5b');
    expect(textoActor({ tipo: 'Plataforma', id: null, nombre: null, sistema: null }, t)).toBe('Plataforma Validasoft');
    expect(textoActor({ tipo: 'Robot', id: null, nombre: null, sistema: null }, t)).toBe('Robot');
  });

  it('los valores del diff: textos tal cual, lo demás en JSON compacto', () => {
    expect(textoValor(null)).toBeNull();
    expect(textoValor('Compras')).toBe('Compras');
    expect(textoValor(3)).toBe('3');
    expect(textoValor(false)).toBe('false');
    expect(textoValor({ a: 1, b: ['x'] })).toBe('{"a":1,"b":["x"]}');
  });
});
