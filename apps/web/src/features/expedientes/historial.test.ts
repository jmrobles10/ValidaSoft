import type { EventoHistorialDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { eventoExpedienteATimeline } from './historial';

const t = i18n.t.bind(i18n);
const contexto = { usuarioActual: null, areas: new Map([['a1', 'Cumplimiento']]) };

function veredicto(origen: string): EventoHistorialDto {
  return {
    version: 7,
    tipo: 'veredicto_de_area_emitido',
    fecha: '2026-11-09T12:00:00Z',
    actorTipo: 'Tercero',
    actorId: 'tercero',
    sistema: null,
    datos: { areaId: 'a1', veredicto: 'Aprobado', origen, justificacion: 'Esta área no tiene documentos que revisar en este expediente.' },
  };
}

describe('historial del expediente', () => {
  it('un veredicto con origen Sistema se atribuye a Sistema aunque lo disparara el tercero', () => {
    const entrada = eventoExpedienteATimeline(veredicto('Sistema'), contexto, t);
    expect(entrada.titulo).toBe('Cumplimiento aprobó');
    expect(entrada.actor).toEqual({ nombre: 'Sistema', tipo: 'sistema' });
    expect(entrada.motivo).toBe('Esta área no tiene documentos que revisar en este expediente.');
  });

  it('un veredicto humano conserva el actor de la petición', () => {
    expect(eventoExpedienteATimeline(veredicto('Humano'), contexto, t).actor.tipo).toBe('tercero');
  });
});

describe('historial: ampliación de la actualización abierta', () => {
  it('«requisitos_agregados» dice qué se sumó a la actualización', () => {
    const evento: EventoHistorialDto = {
      version: 9,
      tipo: 'requisitos_agregados',
      fecha: '2026-11-09T12:00:00Z',
      actorTipo: 'Tercero',
      actorId: 'tercero',
      sistema: null,
      datos: { requisitos: [{ requisitoId: 'r1', nombre: 'Información general' }, { requisitoId: 'r2', nombre: 'Cámara de comercio' }], fecha: '2026-11-09T12:00:00Z' },
    };
    const entrada = eventoExpedienteATimeline(evento, contexto, t);
    expect(entrada.titulo).toBe('Se suman a la actualización abierta: Información general, Cámara de comercio');
  });
});
