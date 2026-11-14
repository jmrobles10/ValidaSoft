import type { NotificacionTerceroDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { claveDeTipo, destinoDeNotificacion, interpretarEvento, tiposConCorreo } from './bandeja';

const notificacion = (id: string, cambios: Partial<NotificacionTerceroDto> = {}): NotificacionTerceroDto => ({
  id,
  tipo: 'tercero.observado',
  titulo: 'Tienes observaciones por corregir',
  mensaje: 'Norte te pidió corregir 1 punto.',
  empresa: { id: 'emp', nombre: 'Norte', logoUrl: null },
  expedienteId: 'exp',
  vinculoId: 'vin',
  folio: 'N-1',
  enlace: '/expediente/exp',
  creadaEn: '2026-11-10T12:00:00Z',
  leidaEn: null,
  ...cambios,
});

describe('bandeja de la campana del tercero', () => {
  it('solo lleva a rutas de esta app, nunca a otro sitio', () => {
    expect(destinoDeNotificacion({ enlace: '/expediente/exp' })).toBe('/expediente/exp');
    expect(destinoDeNotificacion({ enlace: '/boveda' })).toBe('/boveda');
    expect(destinoDeNotificacion({ enlace: null })).toBeNull();
    expect(destinoDeNotificacion({ enlace: 'https://malo.example/expediente' })).toBeNull();
    expect(destinoDeNotificacion({ enlace: '//malo.example/x' })).toBeNull();
  });

  it('los tipos que van por correo tienen su texto y los demás (o los desconocidos) no se ofrecen para apagar', () => {
    expect(claveDeTipo('tercero.plazo_de_cargue')).toBe('plazoDeCargue');
    expect(claveDeTipo('tercero.desaprobado')).toBe('desaprobado');
    expect(claveDeTipo('tercero.reintegrado')).toBe('reintegrado');
    expect(claveDeTipo('tercero.vigencia')).toBe('vigencia');
    expect(claveDeTipo('tercero.invitacion')).toBeNull();
    expect(claveDeTipo('constructor')).toBeNull();
    expect(tiposConCorreo(['tercero.observado', 'tercero.futuro', 'tercero.certificado']).map((t) => t.clave)).toEqual(['observado', 'certificado']);
    expect(tiposConCorreo(['tercero.vigencia']).map((t) => t.clave)).toEqual(['vigencia']);
  });

  it('lo que llega por el stream se valida y los opcionales que el servidor omite entran a la caché como null', () => {
    expect(interpretarEvento({ id: '1', evento: 'no_leidas', datos: '4' })).toEqual({ tipo: 'no_leidas', total: 4 });
    const completa = notificacion('a');
    expect(interpretarEvento({ id: '2', evento: 'notificacion', datos: JSON.stringify(completa) })).toEqual({ tipo: 'notificacion', notificacion: completa });

    const { id, tipo, titulo, mensaje, creadaEn } = completa;
    const minima = { id, tipo, titulo, mensaje, creadaEn };
    expect(interpretarEvento({ id: '3', evento: 'notificacion', datos: JSON.stringify(minima) })).toEqual({
      tipo: 'notificacion',
      notificacion: { ...minima, empresa: null, expedienteId: null, vinculoId: null, folio: null, enlace: null, leidaEn: null },
    });

    expect(interpretarEvento({ id: '4', evento: 'notificacion', datos: JSON.stringify({ ...completa, empresa: { id: 'x' } }) })).toBeNull();
    expect(interpretarEvento({ id: '4', evento: 'notificacion', datos: '{"id":1}' })).toBeNull();
    expect(interpretarEvento({ id: '4', evento: 'cambio', datos: '{}' })).toBeNull();
  });
});
