import { describe, expect, it } from 'vitest';
import { claveDeTipo, haceCuanto } from './tipos';

const AHORA = Date.parse('2026-11-08T12:00:00Z');

describe('haceCuanto', () => {
  it('dice hace cuánto en es-CO, con «ahora» para menos de un minuto o un reloj adelantado', () => {
    expect(haceCuanto('2026-11-08T11:59:40Z', AHORA)).toBe('ahora');
    expect(haceCuanto('2026-11-08T12:00:30Z', AHORA)).toBe('ahora');
    expect(haceCuanto('2026-11-08T11:55:00Z', AHORA)).toBe('hace 5 minutos');
    expect(haceCuanto('2026-11-08T09:00:00Z', AHORA)).toBe('hace 3 horas');
    expect(haceCuanto('2026-11-07T12:00:00Z', AHORA)).toBe('ayer');
    expect(haceCuanto('2026-10-25T12:00:00Z', AHORA)).toBe('hace 2 semanas');
  });
});

describe('claveDeTipo', () => {
  it('traduce el código del tipo a su clave de textos y no confunde propiedades del prototipo', () => {
    expect(claveDeTipo('expediente.tercero_respondio')).toBe('terceroRespondio');
    expect(claveDeTipo('webhook.deshabilitado')).toBe('webhookDeshabilitado');
    expect(claveDeTipo('campanias.terminada')).toBe('campaniaTerminada');
    expect(claveDeTipo('expediente.sla_por_vencer')).toBe('slaPorVencer');
    expect(claveDeTipo('expediente.sla_incumplido')).toBe('slaIncumplido');
    expect(claveDeTipo('constructor')).toBeNull();
    expect(claveDeTipo('otro.tipo')).toBeNull();
  });
});
