import type { BandejaNotificacionesTerceroDto, NotificacionTerceroDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { cambiosDePreferencias, claveDeTipo, destinoDeNotificacion, haceCuanto, marcarLeidaEnBandeja, marcarTodasEnBandeja, textoContador, tiposConCorreo } from './bandeja';

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

const bandeja = (datos: NotificacionTerceroDto[], noLeidas = datos.filter((n) => !n.leidaEn).length): BandejaNotificacionesTerceroDto => ({ datos, siguienteCursor: null, noLeidas });

describe('bandeja de la campana del tercero', () => {
  it('marcar una como leída baja el contador solo si estaba sin leer', () => {
    const inicial = bandeja([notificacion('a'), notificacion('b', { leidaEn: '2026-11-10T13:00:00Z' })]);
    const despues = marcarLeidaEnBandeja(inicial, 'a', '2026-11-10T14:00:00Z');
    expect(despues.noLeidas).toBe(0);
    expect(despues.datos[0]?.leidaEn).toBe('2026-11-10T14:00:00Z');
    expect(marcarLeidaEnBandeja(despues, 'a', '2026-11-10T15:00:00Z')).toBe(despues);
    expect(marcarLeidaEnBandeja(inicial, 'b', 'x')).toBe(inicial);
    expect(marcarLeidaEnBandeja(inicial, 'no-existe', 'x')).toBe(inicial);
  });

  it('marcar todas deja el total en cero aunque la lista sea una página de un total mayor', () => {
    const inicial = bandeja([notificacion('a'), notificacion('b')], 41);
    const despues = marcarTodasEnBandeja(inicial, '2026-11-10T14:00:00Z');
    expect(despues.noLeidas).toBe(0);
    expect(despues.datos.every((n) => n.leidaEn === '2026-11-10T14:00:00Z')).toBe(true);
  });

  it('el contador muestra el número hasta 99 y luego 99+', () => {
    expect([0, 1, 99, 100, 250].map(textoContador)).toEqual(['', '1', '99', '99+', '99+']);
  });

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
    expect(claveDeTipo('tercero.invitacion')).toBeNull();
    expect(claveDeTipo('constructor')).toBeNull();
    expect(tiposConCorreo(['tercero.observado', 'tercero.futuro', 'tercero.certificado']).map((t) => t.clave)).toEqual(['observado', 'certificado']);
  });

  it('en preferencias solo se envía lo que cambió respecto de lo guardado (lo no guardado vale que sí)', () => {
    expect(cambiosDePreferencias({ 'tercero.observado': false }, { 'tercero.observado': false, 'tercero.recordatorio': true })).toEqual({});
    expect(cambiosDePreferencias({ 'tercero.observado': false }, { 'tercero.observado': true, 'tercero.recordatorio': false })).toEqual({
      'tercero.observado': true,
      'tercero.recordatorio': false,
    });
  });

  it('«hace cuánto» pasa de «ahora» a minutos, horas, días y semanas', () => {
    const ahora = new Date('2026-11-10T12:00:00Z').getTime();
    expect(haceCuanto('2026-11-10T11:59:40Z', ahora)).toBe('ahora');
    expect(haceCuanto('2026-11-10T11:55:00Z', ahora)).toBe('hace 5 minutos');
    expect(haceCuanto('2026-11-10T09:00:00Z', ahora)).toBe('hace 3 horas');
    expect(haceCuanto('2026-11-09T12:00:00Z', ahora)).toBe('ayer');
    expect(haceCuanto('2026-10-20T12:00:00Z', ahora)).toBe('hace 3 semanas');
    expect(haceCuanto('2026-11-10T12:05:00Z', ahora)).toBe('ahora');
  });
});
