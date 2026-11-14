import { QueryClient } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';
import {
  agregarNotificacion,
  aplicarEventoDeBandeja,
  cambiosDePreferencias,
  claveDeTipoEn,
  esTextoONulo,
  interpretarEventoDeNotificaciones,
  marcarLeidaEnBandeja,
  marcarTodasEnBandeja,
  type BandejaDeNotificaciones,
  type NotificacionDeBandeja,
} from './bandeja';

interface Aviso extends NotificacionDeBandeja {
  titulo: string;
}

const aviso = (id: string, cambios: Partial<Aviso> = {}): Aviso => ({ id, titulo: `Aviso ${id}`, leidaEn: null, ...cambios });
const bandeja = (noLeidas: number, ...datos: Aviso[]): BandejaDeNotificaciones<Aviso> & { siguienteCursor: null } => ({ datos, siguienteCursor: null, noLeidas });
const leer = (datos: unknown): Aviso | null =>
  typeof datos === 'object' && datos !== null && typeof (datos as Aviso).id === 'string' && typeof (datos as Aviso).titulo === 'string' ? (datos as Aviso) : null;

describe('la bandeja en caché sigue cuadrando con el total de no leídas', () => {
  it('una notificación nueva sin leer entra arriba y suma; si ya estaba, la reemplaza sin volver a sumar', () => {
    const despues = agregarNotificacion(bandeja(1, aviso('a')), aviso('b'));
    expect(despues.datos.map((n) => n.id)).toEqual(['b', 'a']);
    expect(despues.noLeidas).toBe(2);
    expect(despues.siguienteCursor).toBeNull();

    const repetida = agregarNotificacion(despues, aviso('b', { titulo: 'otra vez' }));
    expect(repetida.noLeidas).toBe(2);
    expect(repetida.datos.map((n) => n.titulo)).toEqual(['otra vez', 'Aviso a']);

    expect(agregarNotificacion(bandeja(0), aviso('c', { leidaEn: '2026-11-08T10:00:00Z' })).noLeidas).toBe(0);
  });

  it('marcar una como leída resta solo si estaba sin leer y no toca la bandeja si no cambia nada', () => {
    const inicial = bandeja(2, aviso('a'), aviso('b'), aviso('c', { leidaEn: '2026-11-01T00:00:00Z' }));
    const unaMenos = marcarLeidaEnBandeja(inicial, 'a', '2026-11-08T12:00:00Z');
    expect(unaMenos.noLeidas).toBe(1);
    expect(unaMenos.datos[0]?.leidaEn).toBe('2026-11-08T12:00:00Z');
    expect(marcarLeidaEnBandeja(unaMenos, 'a', 'x')).toBe(unaMenos);
    expect(marcarLeidaEnBandeja(unaMenos, 'c', 'x')).toBe(unaMenos);
    expect(marcarLeidaEnBandeja(unaMenos, 'no-existe', 'x')).toBe(unaMenos);
  });

  it('marcar todas deja el total en cero aunque la lista sea una página de un total mayor', () => {
    const inicial = bandeja(41, aviso('a'), aviso('b', { leidaEn: '2026-11-01T00:00:00Z' }));
    const todas = marcarTodasEnBandeja(inicial, '2026-11-08T13:00:00Z');
    expect(todas.noLeidas).toBe(0);
    expect(todas.datos.map((n) => n.leidaEn)).toEqual(['2026-11-08T13:00:00Z', '2026-11-01T00:00:00Z']);
  });
});

describe('interpretarEventoDeNotificaciones', () => {
  it('lee no_leidas y notificacion, y descarta lo que no tiene la forma esperada', () => {
    expect(interpretarEventoDeNotificaciones({ id: '1', evento: 'no_leidas', datos: '3' }, leer)).toEqual({ tipo: 'no_leidas', total: 3 });
    expect(interpretarEventoDeNotificaciones({ id: '1', evento: 'no_leidas', datos: ' 0 ' }, leer)).toEqual({ tipo: 'no_leidas', total: 0 });
    expect(interpretarEventoDeNotificaciones({ id: '1', evento: 'no_leidas', datos: 'tres' }, leer)).toBeNull();
    expect(interpretarEventoDeNotificaciones({ id: '1', evento: 'no_leidas', datos: '-1' }, leer)).toBeNull();
    expect(interpretarEventoDeNotificaciones({ id: '1', evento: 'no_leidas', datos: '1.5' }, leer)).toBeNull();

    const n = aviso('z');
    expect(interpretarEventoDeNotificaciones({ id: '2', evento: 'notificacion', datos: JSON.stringify(n) }, leer)).toEqual({ tipo: 'notificacion', notificacion: n });
    expect(interpretarEventoDeNotificaciones({ id: '2', evento: 'notificacion', datos: '{"id":1}' }, leer)).toBeNull();
    expect(interpretarEventoDeNotificaciones({ id: '2', evento: 'notificacion', datos: 'no es json' }, leer)).toBeNull();
    expect(interpretarEventoDeNotificaciones({ id: '2', evento: 'otro', datos: '{}' }, leer)).toBeNull();
  });

  it('si la lectura de la app falla, el evento se descarta en vez de romper el stream', () => {
    const rota = () => {
      throw new Error('forma inesperada');
    };
    expect(interpretarEventoDeNotificaciones({ id: '2', evento: 'notificacion', datos: '{}' }, rota)).toBeNull();
  });
});

describe('aplicarEventoDeBandeja', () => {
  const clave = ['bandeja'];

  it('una notificación nueva entra arriba de la bandeja en caché; sin caché, se pide de nuevo', () => {
    const cliente = new QueryClient();
    const invalidar = vi.spyOn(cliente, 'invalidateQueries');

    aplicarEventoDeBandeja(cliente, clave, { tipo: 'notificacion', notificacion: aviso('a') });
    expect(invalidar).toHaveBeenCalledTimes(1);
    expect(cliente.getQueryData(clave)).toBeUndefined();

    cliente.setQueryData(clave, bandeja(0));
    aplicarEventoDeBandeja(cliente, clave, { tipo: 'notificacion', notificacion: aviso('b') });
    expect(invalidar).toHaveBeenCalledTimes(1);
    expect(cliente.getQueryData<BandejaDeNotificaciones<Aviso>>(clave)).toMatchObject({ noLeidas: 1, datos: [{ id: 'b' }] });
  });

  it('el total del servidor que no cuadra con la caché la invalida; si cuadra, no hace nada', () => {
    const cliente = new QueryClient();
    cliente.setQueryData(clave, bandeja(2, aviso('a'), aviso('b')));
    const invalidar = vi.spyOn(cliente, 'invalidateQueries');

    aplicarEventoDeBandeja(cliente, clave, { tipo: 'no_leidas', total: 2 });
    expect(invalidar).not.toHaveBeenCalled();

    aplicarEventoDeBandeja(cliente, clave, { tipo: 'no_leidas', total: 5 });
    expect(invalidar).toHaveBeenCalledTimes(1);
  });
});

describe('preferencias y tipos', () => {
  it('en preferencias solo se envía lo que cambió respecto de lo guardado (lo no guardado vale que sí)', () => {
    expect(cambiosDePreferencias({ a: false }, { a: false, b: true })).toEqual({});
    expect(cambiosDePreferencias({ a: false }, { a: true, b: false })).toEqual({ a: true, b: false });
  });

  it('la clave de textos de un tipo no confunde propiedades del prototipo', () => {
    const claves = { 'tercero.observado': 'observado', 'expediente.aprobado': 'aprobado' } as const;
    expect(claveDeTipoEn(claves, 'tercero.observado')).toBe('observado');
    expect(claveDeTipoEn(claves, 'otro.tipo')).toBeNull();
    expect(claveDeTipoEn(claves, 'constructor')).toBeNull();
    expect(claveDeTipoEn(claves, 'toString')).toBeNull();
  });

  it('esTextoONulo acepta texto, nulo y ausente', () => {
    expect([null, undefined, '', 'x'].every(esTextoONulo)).toBe(true);
    expect([1, {}, true].some(esTextoONulo)).toBe(false);
  });
});
