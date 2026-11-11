import type { EventoHistorialDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { eventoExpedienteATimeline } from '../expedientes/historial';

const t = i18n.t.bind(i18n);
const contexto = { usuarioActual: null, areas: new Map([['a1', 'Compras']]), requisitos: new Map([['r1', 'Registro Único Tributario']]) };

function evento(tipo: string, datos: Record<string, unknown>, actorTipo = 'Sistema'): EventoHistorialDto {
  return { version: 12, tipo, fecha: '2026-11-10T12:00:00Z', actorTipo, actorId: null, sistema: null, datos };
}

const registrada = (resultado: unknown, extra: Record<string, unknown> = {}) =>
  evento('revision_automatica_registrada', { requisitoId: 'r1', areaId: 'a1', sobreVersion: 1, nivel: 'Automatico', resultado, confianza: 0.97, motivos: [], ...extra });

describe('historial: revisión automática', () => {
  it('«aprobó» dice el documento, el área y la confianza', () => {
    const e = eventoExpedienteATimeline(registrada('AprobadaAutomaticamente'), contexto, t);
    expect(e.titulo).toMatch(/^La revisión automática aprobó Registro Único Tributario en el área Compras \(confianza 97\s?%\)$/);
    expect(e.detalle).toBeNull();
    expect(e.actor).toEqual({ nombre: 'Sistema (IA)', tipo: 'sistema' });
  });

  it('con muestreo avisa que queda para confirmar', () => {
    const e = eventoExpedienteATimeline(registrada('AprobadaConMuestreo'), contexto, t);
    expect(e.titulo).toMatch(/aprobó Registro Único Tributario en el área Compras \(confianza 97\s?%\): queda para confirmar$/);
  });

  it('una excepción manda el documento a una persona y lista los motivos en español', () => {
    const e = eventoExpedienteATimeline(
      registrada('Excepcion', {
        confianza: 0.6,
        motivos: [
          { codigo: 'confianza-baja', texto: 'La confianza del análisis (60 %) está bajo el umbral de la empresa (95 %).' },
          { codigo: 'alertas-de-manipulacion', texto: 'El análisis detectó señales de manipulación.' },
        ],
      }),
      contexto,
      t,
    );
    expect(e.titulo).toBe('La revisión automática mandó Registro Único Tributario a una persona del área Compras');
    expect(e.detalle).toBe('Motivos: Confianza baja · Señales de manipulación');
  });

  it('Asistido sugiere aprobar o revisar; revisar trae sus motivos', () => {
    expect(eventoExpedienteATimeline(registrada('Sugerida', { sugerencia: 'Aprobar' }), contexto, t).titulo).toBe(
      'La revisión automática sugiere aprobar Registro Único Tributario en el área Compras',
    );
    const revisar = eventoExpedienteATimeline(registrada('Sugerida', { sugerencia: 'Revisar', motivos: [{ codigo: 'ilegible', texto: 'No es legible.' }] }), contexto, t);
    expect(revisar.titulo).toBe('La revisión automática sugiere revisar Registro Único Tributario en el área Compras');
    expect(revisar.detalle).toBe('Motivos: Documento ilegible');
  });

  it('los enums como número (datos viejos) se leen igual', () => {
    const e = eventoExpedienteATimeline(registrada(1), contexto, t);
    expect(e.titulo).toMatch(/^La revisión automática aprobó /);
    expect(eventoExpedienteATimeline(registrada(3), contexto, t).titulo).toMatch(/mandó Registro Único Tributario a una persona/);
  });

  it('lo que una persona resuelve del muestreo dice confirmó o revirtió, con su justificación', () => {
    const confirmada = eventoExpedienteATimeline(
      evento('revision_automatica_resuelta', { requisitoId: 'r1', areaId: 'a1', sobreVersion: 1, decision: 'Confirmada', por: 'u-1', justificacion: null }, 'Usuario'),
      contexto,
      t,
    );
    expect(confirmada.titulo).toBe('Una persona confirmó la aprobación automática de Registro Único Tributario en el área Compras');
    expect(confirmada.actor.tipo).toBe('persona');

    const revertida = eventoExpedienteATimeline(
      evento('revision_automatica_resuelta', { requisitoId: 'r1', areaId: 'a1', sobreVersion: 1, decision: 'Revertida', por: 'u-1', justificacion: 'El RUT está vencido.' }, 'Usuario'),
      contexto,
      t,
    );
    expect(revertida.titulo).toBe('Una persona revirtió la aprobación automática de Registro Único Tributario en el área Compras');
    expect(revertida.motivo).toBe('El RUT está vencido.');
  });

  it('un resultado que esta versión no conoce deja el título del evento, sin romper', () => {
    expect(eventoExpedienteATimeline(registrada('Algo nuevo'), contexto, t).titulo).toBe('Revisión automática');
  });
});
