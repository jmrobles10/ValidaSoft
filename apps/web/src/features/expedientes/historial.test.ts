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
    datos: { areaId: 'a1', veredicto: 'Aprobado', origen, justificacion: 'A este tercero no se le pidió ningún documento que revise esta área.' },
  };
}

describe('historial del expediente', () => {
  it('un veredicto con origen Sistema se atribuye a Sistema aunque lo disparara el tercero', () => {
    const entrada = eventoExpedienteATimeline(veredicto('Sistema'), contexto, t);
    expect(entrada.titulo).toBe('Cumplimiento aprobó');
    expect(entrada.actor).toEqual({ nombre: 'Sistema', tipo: 'sistema' });
    expect(entrada.motivo).toBe('A este tercero no se le pidió ningún documento que revise esta área.');
  });

  it('un veredicto humano conserva el actor de la petición', () => {
    expect(eventoExpedienteATimeline(veredicto('Humano'), contexto, t).actor.tipo).toBe('tercero');
  });
});
