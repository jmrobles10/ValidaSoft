import { describe, expect, it } from 'vitest';
import { filaSagrilaft, informeSagrilaft } from './datosPrueba';
import {
  distribucionDeRiesgo,
  estadoCanonico,
  estadoDeEstudio,
  estadoDeListas,
  etiquetaDeMes,
  filtrarFilas,
  hayFiltros,
  marcasDePersona,
  maximoDeTendencia,
  participacionEnTexto,
  porcentajeDe,
  promedioDeDias,
  revisionDe,
  SIN_FILTROS,
} from './informeSagrilaft';

describe('Informe SAGRILAFT completo', () => {
  const informe = informeSagrilaft();

  it('filtra por estado del estudio y por nivel de riesgo, y juntos', () => {
    expect(filtrarFilas(informe.filas, SIN_FILTROS)).toHaveLength(3);
    expect(filtrarFilas(informe.filas, { estudio: 'Vencida', riesgo: '' }).map((f) => f.folio)).toEqual(['EXP-0002']);
    expect(filtrarFilas(informe.filas, { estudio: 'SinEstudio', riesgo: '' }).map((f) => f.folio)).toEqual(['EXP-0003']);
    expect(filtrarFilas(informe.filas, { estudio: '', riesgo: '2' }).map((f) => f.folio)).toEqual(['EXP-0001']);
    expect(filtrarFilas(informe.filas, { estudio: 'Vigente', riesgo: '3' })).toEqual([]);
    expect(hayFiltros(SIN_FILTROS)).toBe(false);
    expect(hayFiltros({ estudio: '', riesgo: '1' })).toBe(true);
  });

  it('un estado desconocido del servidor se toma como sin estudio o sin consulta', () => {
    expect(estadoDeEstudio(filaSagrilaft({ estadoDebidaDiligencia: 'Raro' }))).toBe('SinEstudio');
    expect(estadoDeListas(filaSagrilaft({ estadoListas: 'Raro' }))).toBe('SinConsulta');
    expect(estadoCanonico('SinEstudio')).toBe('Pendiente');
    expect(estadoCanonico('SinConsulta')).toBe('Pendiente');
    expect(estadoCanonico('PorVencer')).toBe('PorVencer');
  });

  it('la distribución del riesgo siempre trae los tres niveles', () => {
    expect(distribucionDeRiesgo(informe)).toEqual([
      { nivel: 1, cantidad: 0 },
      { nivel: 2, cantidad: 1 },
      { nivel: 3, cantidad: 1 },
    ]);
    expect(distribucionDeRiesgo({ riesgo: [] }).every((d) => d.cantidad === 0)).toBe(true);
  });

  it('las barras son proporcionales al máximo, con un mínimo visible', () => {
    expect(maximoDeTendencia(informe.tendencia)).toBe(4);
    expect(maximoDeTendencia([])).toBe(0);
    expect(porcentajeDe(4, 4)).toBe(100);
    expect(porcentajeDe(2, 4)).toBe(50);
    expect(porcentajeDe(1, 100)).toBe(4);
    expect(porcentajeDe(0, 4)).toBe(0);
    expect(porcentajeDe(3, 0)).toBe(0);
  });

  it('el mes se abrevia y lleva el año al inicio de la serie y en enero', () => {
    expect(etiquetaDeMes('2026-11', false)).toMatch(/^nov/i);
    expect(etiquetaDeMes('2027-01', true)).toMatch(/^ene.* 2027$/i);
    expect(etiquetaDeMes('raro', false)).toBe('raro');
  });

  it('la próxima revisión dice si vence, es hoy o ya venció', () => {
    expect(revisionDe(filaSagrilaft({ diasParaRevision: 325 }))).toEqual({ tipo: 'vence', dias: 325 });
    expect(revisionDe(filaSagrilaft({ diasParaRevision: 0 }))).toEqual({ tipo: 'hoy' });
    expect(revisionDe(filaSagrilaft({ diasParaRevision: -10 }))).toEqual({ tipo: 'vencida', dias: 10 });
    expect(revisionDe(filaSagrilaft({ proximaRevision: null, diasParaRevision: null }))).toEqual({ tipo: 'sin-fecha' });
  });

  it('el promedio y la participación se escriben con coma decimal', () => {
    expect(promedioDeDias(3.5)).toBe('3,5');
    expect(promedioDeDias('12.25')).toBe('12,3');
    expect(promedioDeDias(null)).toBeNull();
    expect(participacionEnTexto(33.5)).toBe('33,5 %');
    expect(participacionEnTexto('5')).toBe('5 %');
    expect(participacionEnTexto(null)).toBeNull();
  });

  it('las marcas de una persona son PEP, vínculo con PEP y beneficiario final', () => {
    expect(marcasDePersona({ esPep: true, vinculoPep: false, beneficiarioFinal: true })).toEqual(['pep', 'beneficiarioFinal']);
    expect(marcasDePersona({ esPep: false, vinculoPep: false, beneficiarioFinal: false })).toEqual([]);
  });
});
