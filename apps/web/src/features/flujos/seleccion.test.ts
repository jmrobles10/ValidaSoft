import { ErrorApi, type CondicionDto, type FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { borradorDesdeVersion, borradorVacio, erroresBloqueantes, firmaBorrador, validarFlujo } from './editor';
import { errorDeApiEnSeleccion, PRIORIDAD_MAXIMA, seleccionADto, seleccionDesdeVersion, seSeleccionaSolo, validarSeleccion } from './seleccion';

const predicado = (atributo: string, operador: string, ...valores: string[]): CondicionDto => ({ tipo: 'Predicado', nodos: null, atributo, operador, valores });
const riesgoAlto = predicado('vinculo.atributos.nivelRiesgo', 'En', 'Alto');

const version = (cambios: Partial<FlujoVersionDto> = {}): FlujoVersionDto => ({
  id: 'v',
  version: 1,
  estado: 'Publicada',
  etapas: [{ orden: 1, areas: [{ areaId: 'a', aprobadoresRequeridos: 1, puedeRechazarExpediente: false, automatica: false }] }],
  politicaDevolucion: 'Inmediata',
  separacionDeFunciones: false,
  creadaEn: '2026-11-14T10:00:00Z',
  publicadaEn: '2026-11-14T11:00:00Z',
  retiradaEn: null,
  ...cambios,
});

const base = { codigo: 'CRITICO', nombre: 'Proveedor crítico', tipoVinculo: 'PROVEEDOR' };

describe('Selección automática del flujo (FR-1)', () => {
  it('una versión guardada antes (o sin regla) no se escoge sola', () => {
    expect(seleccionDesdeVersion(undefined)).toEqual({ seleccionCondicion: null, seleccionPrioridad: '' });
    expect(seleccionDesdeVersion({ condicion: null, prioridad: null })).toEqual({ seleccionCondicion: null, seleccionPrioridad: '' });
    expect(borradorVacio()).toMatchObject({ seleccionCondicion: null, seleccionPrioridad: '' });
  });

  it('lleva la condición y la prioridad de una versión al editor y de vuelta a la API sin alterar la condición', () => {
    const borrador = borradorDesdeVersion(base, version({ seleccion: { condicion: riesgoAlto, prioridad: 2 } }));
    expect(borrador.seleccionCondicion).toBe(riesgoAlto);
    expect(borrador.seleccionPrioridad).toBe('2');
    expect(seleccionADto(borrador)).toEqual({ condicion: riesgoAlto, prioridad: 2 });
  });

  it('sin condición la API recibe el objeto vacío (así la quita) aunque quede una prioridad escrita', () => {
    expect(seleccionADto({ seleccionCondicion: null, seleccionPrioridad: '5' })).toEqual({ condicion: null, prioridad: null });
  });

  it('con condición, una prioridad vacía o ilegible viaja nula y la API responde prioridad-requerida', () => {
    expect(seleccionADto({ seleccionCondicion: riesgoAlto, seleccionPrioridad: '' })).toEqual({ condicion: riesgoAlto, prioridad: null });
    expect(seleccionADto({ seleccionCondicion: riesgoAlto, seleccionPrioridad: ' 7 ' })).toEqual({ condicion: riesgoAlto, prioridad: 7 });
  });

  it.each([
    ['sin condición no hay nada que validar', null, '', []],
    ['prioridad vacía', riesgoAlto, '', ['prioridad-requerida']],
    ['solo espacios', riesgoAlto, '  ', ['prioridad-requerida']],
    ['cero', riesgoAlto, '0', ['prioridad-invalida']],
    ['sobre el máximo', riesgoAlto, String(PRIORIDAD_MAXIMA + 1), ['prioridad-invalida']],
    ['no es un número', riesgoAlto, 'abc', ['prioridad-invalida']],
    ['la primera', riesgoAlto, '1', []],
    ['el máximo', riesgoAlto, String(PRIORIDAD_MAXIMA), []],
  ])('valida la prioridad como el dominio: %s', (_caso, seleccionCondicion, seleccionPrioridad, codigos) => {
    const errores = validarSeleccion({ seleccionCondicion, seleccionPrioridad });
    expect(errores.map((e) => e.codigo)).toEqual(codigos);
    expect(errores.every((e) => e.campo === 'seleccion.prioridad' && !e.aviso)).toBe(true);
  });

  it('el borrador con condición y sin prioridad no se puede guardar', () => {
    const borrador = { ...borradorDesdeVersion(base, version({ seleccion: { condicion: riesgoAlto, prioridad: 2 } })), seleccionPrioridad: '' };
    const bloqueantes = erroresBloqueantes(validarFlujo(borrador, [{ id: 'a', empresaId: 'e', codigo: 'A', nombre: 'A', miembros: ['u'], activa: true }], false));
    expect(bloqueantes).toEqual([{ codigo: 'prioridad-requerida', campo: 'seleccion.prioridad' }]);
  });

  it('cambiar la condición o la prioridad cuenta como cambio sin guardar', () => {
    const borrador = borradorDesdeVersion(base, version({ seleccion: { condicion: riesgoAlto, prioridad: 2 } }));
    expect(firmaBorrador({ ...borrador })).toBe(firmaBorrador(borrador));
    expect(firmaBorrador({ ...borrador, seleccionPrioridad: '3' })).not.toBe(firmaBorrador(borrador));
    expect(firmaBorrador({ ...borrador, seleccionCondicion: null })).not.toBe(firmaBorrador(borrador));
  });

  it('reconoce los flujos que se escogen solos', () => {
    expect(seSeleccionaSolo({ condicion: riesgoAlto, prioridad: 1 })).toBe(true);
    expect(seSeleccionaSolo({ condicion: null, prioridad: null })).toBe(false);
    expect(seSeleccionaSolo(null)).toBe(false);
    expect(seSeleccionaSolo(undefined)).toBe(false);
  });

  describe('errores 422 de la API', () => {
    const error = (codigo: string, extensiones: Record<string, unknown> = {}) => new ErrorApi(422, codigo, 'detalle', null, null, {}, extensiones);

    it('la prioridad repetida en el tipo va al campo de prioridad', () => {
      expect(errorDeApiEnSeleccion(error('prioridad-duplicada', { flujo: 'CRITICO', prioridad: 1 }))).toEqual({ codigo: 'prioridad-duplicada', campo: 'seleccion.prioridad' });
      expect(errorDeApiEnSeleccion(error('prioridad-invalida'))).toEqual({ codigo: 'prioridad-invalida', campo: 'seleccion.prioridad' });
      expect(errorDeApiEnSeleccion(error('prioridad-requerida'))).toEqual({ codigo: 'prioridad-requerida', campo: 'seleccion.prioridad' });
    });

    it('una condición inválida va al selector, salvo la de un requisito (esa trae su posición)', () => {
      expect(errorDeApiEnSeleccion(error('condicion-invalida'))).toEqual({ codigo: 'condicion-invalida', campo: 'seleccion.condicion' });
      expect(errorDeApiEnSeleccion(error('condicion-invalida', { requisito: 2 }))).toBeNull();
      expect(errorDeApiEnSeleccion(error('seleccion-sin-condicion'))).toEqual({ codigo: 'prioridad-requerida', campo: 'seleccion.condicion' });
    });

    it('lo que no es de la selección, o no es un 422, no se toma', () => {
      expect(errorDeApiEnSeleccion(error('requisito-duplicado'))).toBeNull();
      expect(errorDeApiEnSeleccion(new ErrorApi(409, 'prioridad-duplicada', 'x', null, null, {}, {}))).toBeNull();
      expect(errorDeApiEnSeleccion(new Error('red'))).toBeNull();
      expect(errorDeApiEnSeleccion(null)).toBeNull();
    });
  });
});
