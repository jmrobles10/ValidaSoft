import { describe, expect, it } from 'vitest';
import { claveDeTipo } from './tipos';

describe('claveDeTipo', () => {
  it('traduce el código del tipo a su clave de textos y no confunde propiedades del prototipo', () => {
    expect(claveDeTipo('expediente.tercero_respondio')).toBe('terceroRespondio');
    expect(claveDeTipo('webhook.deshabilitado')).toBe('webhookDeshabilitado');
    expect(claveDeTipo('campanias.terminada')).toBe('campaniaTerminada');
    expect(claveDeTipo('expediente.sla_por_vencer')).toBe('slaPorVencer');
    expect(claveDeTipo('expediente.sla_incumplido')).toBe('slaIncumplido');
    expect(claveDeTipo('habeas.plazo')).toBe('habeasPlazo');
    expect(claveDeTipo('plan.limite')).toBe('planLimite');
    expect(claveDeTipo('vinculo.cuenta_propuesta')).toBe('cuentaPropuesta');
    expect(claveDeTipo('constructor')).toBeNull();
    expect(claveDeTipo('otro.tipo')).toBeNull();
  });
});
