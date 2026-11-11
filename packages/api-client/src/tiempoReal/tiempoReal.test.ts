import { QueryClient } from '@tanstack/react-query';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { interpretarCambio } from './cambios';
import { fijarAplicadorDePendientes, hayEdicionEn, marcarCambiosPendientes, registrarEdicion, reiniciarEdiciones, resolverCambiosPendientes } from './edicion';
import { invalidarPorCambio, invalidarTodoElMapa, type MapaDeInvalidacion } from './invalidar';

const MAPA: MapaDeInvalidacion = {
  expediente: ['/p/expedientes', '/p/vinculos'],
  vinculo: ['/p/vinculos'],
  cuenta: ['/p/boveda'],
  documento: ['/p/boveda', '/p/expedientes'],
  ficha: ['/p/ficha'],
  certificado: ['/p/vinculos'],
};

afterEach(() => {
  reiniciarEdiciones();
  fijarAplicadorDePendientes(null);
  vi.useRealTimers();
});

describe('avisos de cambio', () => {
  it('interpreta el cuerpo de un evento cambio y descarta lo que no tiene la forma esperada', () => {
    expect(interpretarCambio('{"recurso":"cuenta","id":"c1","vinculoId":"v1"}')).toEqual({ recurso: 'cuenta', id: 'c1', vinculoId: 'v1' });
    expect(interpretarCambio('{"recurso":"ficha","id":"s1","vinculoId":null}')).toEqual({ recurso: 'ficha', id: 's1', vinculoId: null });
    expect(interpretarCambio('{"recurso":"ficha","id":"s1"}')?.vinculoId).toBeNull();
    for (const malo of ['', 'no es json', 'null', '[]', '{"recurso":"desconocido","id":"x"}', '{"recurso":"cuenta"}', '{"recurso":"cuenta","id":7}']) {
      expect(interpretarCambio(malo), malo).toBeNull();
    }
  });
});

describe('guardia de edición', () => {
  it('cuenta las ediciones del mismo alcance y las termina una sola vez', () => {
    const una = registrarEdicion('/p/ficha');
    const otra = registrarEdicion('/p/ficha');
    expect(hayEdicionEn('/p/ficha')).toBe(true);
    expect(hayEdicionEn('/p/expedientes')).toBe(false);
    una();
    una(); // terminar dos veces no resta dos
    expect(hayEdicionEn('/p/ficha')).toBe(true);
    otra();
    expect(hayEdicionEn('/p/ficha')).toBe(false);
  });

  it('lo pendiente se aplica solo cuando termina la última edición del alcance', () => {
    const aplicados: string[] = [];
    fijarAplicadorDePendientes((alcance) => aplicados.push(alcance));
    const primera = registrarEdicion('/p/ficha');
    const segunda = registrarEdicion('/p/ficha');
    marcarCambiosPendientes('/p/ficha');
    primera();
    expect(aplicados).toEqual([]);
    segunda();
    expect(aplicados).toEqual(['/p/ficha']);

    // Sin pendiente, terminar la edición no vuelve a pedir nada; y resolver un pendiente dice si lo había.
    registrarEdicion('/p/ficha')();
    expect(aplicados).toEqual(['/p/ficha']);
    marcarCambiosPendientes('/p/vinculos');
    expect(resolverCambiosPendientes('/p/vinculos')).toBe(true);
    expect(resolverCambiosPendientes('/p/vinculos')).toBe(false);
  });
});

describe('invalidación por cambio', () => {
  function espiar(cliente: QueryClient) {
    const prefijos: string[] = [];
    vi.spyOn(cliente, 'invalidateQueries').mockImplementation((filtros) => {
      const predicado = filtros && 'predicate' in filtros ? filtros.predicate : undefined;
      for (const candidato of ['/p/expedientes', '/p/vinculos', '/p/boveda', '/p/ficha']) {
        if (predicado?.({ queryKey: [`${candidato}/x`] } as never)) prefijos.push(candidato);
      }
      return Promise.resolve();
    });
    return prefijos;
  }

  it('cada recurso refresca solo las consultas de su mapa y junta los avisos seguidos en una recarga', () => {
    vi.useFakeTimers();
    const cliente = new QueryClient();
    const prefijos = espiar(cliente);

    invalidarPorCambio(cliente, { recurso: 'documento', id: 'd1', vinculoId: null }, MAPA, 100);
    invalidarPorCambio(cliente, { recurso: 'documento', id: 'd2', vinculoId: null }, MAPA, 100);
    expect(prefijos).toEqual([]); // aún espera
    vi.advanceTimersByTime(100);
    expect(prefijos.sort()).toEqual(['/p/boveda', '/p/expedientes']);

    prefijos.length = 0;
    invalidarPorCambio(cliente, { recurso: 'cuenta', id: 'c1', vinculoId: null }, MAPA, 100);
    vi.advanceTimersByTime(100);
    expect(prefijos).toEqual(['/p/boveda']);
  });

  it('con una edición en curso no vuelve a pedir ese alcance, deja el aviso, y lo demás sí se refresca', () => {
    vi.useFakeTimers();
    const cliente = new QueryClient();
    const prefijos = espiar(cliente);
    registrarEdicion('/p/expedientes');

    invalidarPorCambio(cliente, { recurso: 'expediente', id: 'e1', vinculoId: 'v1' }, MAPA, 100);
    vi.advanceTimersByTime(100);
    expect(prefijos).toEqual(['/p/vinculos']);
    expect(resolverCambiosPendientes('/p/expedientes')).toBe(true);
  });

  it('si la edición empieza mientras se esperaba, tampoco pisa', () => {
    vi.useFakeTimers();
    const cliente = new QueryClient();
    const prefijos = espiar(cliente);

    invalidarPorCambio(cliente, { recurso: 'ficha', id: 's1', vinculoId: null }, MAPA, 100);
    const terminar = registrarEdicion('/p/ficha');
    vi.advanceTimersByTime(100);
    expect(prefijos).toEqual([]);
    terminar();
  });

  it('al reconectar o en el respaldo se refresca todo el mapa una vez por prefijo', () => {
    vi.useFakeTimers();
    const cliente = new QueryClient();
    const prefijos = espiar(cliente);

    invalidarTodoElMapa(cliente, MAPA, 50);
    vi.advanceTimersByTime(50);
    expect(prefijos.sort()).toEqual(['/p/boveda', '/p/expedientes', '/p/ficha', '/p/vinculos']);
  });
});
