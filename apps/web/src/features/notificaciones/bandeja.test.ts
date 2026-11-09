import type { BandejaNotificacionesDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { agregarNotificacion, destinoDeNotificacion, interpretarEvento, marcarLeidaEnBandeja, marcarTodasEnBandeja, textoContador } from './bandeja';
import { notificacion } from './pruebas/datos';

const bandeja = (noLeidas: number, ...datos: BandejaNotificacionesDto['datos']): BandejaNotificacionesDto => ({ datos, siguienteCursor: null, noLeidas });

describe('contador de no leídas', () => {
  it('el badge no muestra nada en cero y se corta en 99+', () => {
    expect([0, 1, 99, 100, 250].map(textoContador)).toEqual(['', '1', '99', '99+', '99+']);
  });

  it('una notificación nueva sin leer entra arriba y suma; si ya estaba, no vuelve a sumar', () => {
    const vieja = notificacion({ id: 'a' });
    const nueva = notificacion({ id: 'b' });
    const despues = agregarNotificacion(bandeja(1, vieja), nueva);
    expect(despues.datos.map((n) => n.id)).toEqual(['b', 'a']);
    expect(despues.noLeidas).toBe(2);
    expect(agregarNotificacion(despues, { ...nueva, titulo: 'otra vez' }).noLeidas).toBe(2);
    expect(agregarNotificacion(bandeja(0), notificacion({ id: 'c', leidaEn: '2026-11-08T10:00:00Z' })).noLeidas).toBe(0);
  });

  it('marcar una como leída resta solo si estaba sin leer; marcar todas deja el contador en cero', () => {
    const inicial = bandeja(2, notificacion({ id: 'a' }), notificacion({ id: 'b' }), notificacion({ id: 'c', leidaEn: '2026-11-01T00:00:00Z' }));
    const unaMenos = marcarLeidaEnBandeja(inicial, 'a', '2026-11-08T12:00:00Z');
    expect(unaMenos.noLeidas).toBe(1);
    expect(unaMenos.datos[0]?.leidaEn).toBe('2026-11-08T12:00:00Z');
    expect(marcarLeidaEnBandeja(unaMenos, 'a', 'x')).toBe(unaMenos);
    expect(marcarLeidaEnBandeja(unaMenos, 'c', 'x').noLeidas).toBe(1);

    const todas = marcarTodasEnBandeja(unaMenos, '2026-11-08T13:00:00Z');
    expect(todas.noLeidas).toBe(0);
    expect(todas.datos.map((n) => n.leidaEn)).toEqual(['2026-11-08T12:00:00Z', '2026-11-08T13:00:00Z', '2026-11-01T00:00:00Z']);
  });
});

describe('interpretarEvento', () => {
  it('lee no_leidas y notificacion, y descarta lo que no tiene la forma esperada', () => {
    expect(interpretarEvento({ id: '1', evento: 'no_leidas', datos: '3' })).toEqual({ tipo: 'no_leidas', total: 3 });
    expect(interpretarEvento({ id: '1', evento: 'no_leidas', datos: 'tres' })).toBeNull();
    const n = notificacion({ id: 'z' });
    expect(interpretarEvento({ id: '2', evento: 'notificacion', datos: JSON.stringify(n) })).toEqual({ tipo: 'notificacion', notificacion: n });
    expect(interpretarEvento({ id: '2', evento: 'notificacion', datos: '{"id":1}' })).toBeNull();
    expect(interpretarEvento({ id: '2', evento: 'notificacion', datos: 'no es json' })).toBeNull();
    expect(interpretarEvento({ id: '2', evento: 'otro', datos: '{}' })).toBeNull();
  });
});

describe('destinoDeNotificacion', () => {
  it('usa el enlace del servidor y, en un webhook deshabilitado sin enlace, la configuración de webhooks', () => {
    expect(destinoDeNotificacion(notificacion({ enlace: '/e/emp/expedientes/x' }), 'emp')).toBe('/e/emp/expedientes/x');
    expect(destinoDeNotificacion(notificacion({ tipo: 'webhook.deshabilitado', enlace: null }), 'emp')).toBe('/e/emp/webhooks');
    expect(destinoDeNotificacion(notificacion({ enlace: null }), 'emp')).toBeNull();
  });
});
