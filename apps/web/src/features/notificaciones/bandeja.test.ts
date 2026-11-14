import { describe, expect, it } from 'vitest';
import { destinoDeNotificacion, esNotificacion, interpretarEvento } from './bandeja';
import { notificacion } from './pruebas/datos';

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

  it('los opcionales que el servidor omite entran a la caché como null', () => {
    const { id, tipo, titulo, mensaje, creadaEn } = notificacion({ id: 'm' });
    const minima = { id, tipo, titulo, mensaje, creadaEn };
    expect(esNotificacion(minima)).toBe(true);
    expect(interpretarEvento({ id: '3', evento: 'notificacion', datos: JSON.stringify(minima) })).toEqual({
      tipo: 'notificacion',
      notificacion: { ...minima, empresaId: null, expedienteId: null, vinculoId: null, folio: null, enlace: null, leidaEn: null },
    });
  });
});

describe('destinoDeNotificacion', () => {
  it('usa el enlace del servidor y, en un webhook deshabilitado sin enlace, la configuración de webhooks', () => {
    expect(destinoDeNotificacion(notificacion({ enlace: '/e/emp/expedientes/x' }), 'emp')).toBe('/e/emp/expedientes/x');
    expect(destinoDeNotificacion(notificacion({ tipo: 'webhook.deshabilitado', enlace: null }), 'emp')).toBe('/e/emp/webhooks');
    expect(destinoDeNotificacion(notificacion({ enlace: null }), 'emp')).toBeNull();
  });
});
