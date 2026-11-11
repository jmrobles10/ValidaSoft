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

describe('historial: el tercero retiró una versión (RT-3)', () => {
  const evento = (tipo: string, datos: Record<string, unknown>): EventoHistorialDto => ({
    version: 11,
    tipo,
    fecha: '2026-11-11T12:00:00Z',
    actorTipo: 'Sistema',
    actorId: 'sistema',
    sistema: null,
    datos,
  });
  const conRequisitos = { ...contexto, requisitos: new Map([['r-rut', 'Registro Único Tributario']]) };

  it('un expediente abierto que cambia de versión dice qué se retiró, por qué y cuál vuelve', () => {
    const entrada = eventoExpedienteATimeline(
      evento('version_adjuntada_a_requisito', {
        requisitoId: 'r-rut',
        version: { documentoId: 'd1', numero: 7 },
        numeroAnterior: 8,
        retiro: { numero: 8, motivo: 'Subí el RUT de otra empresa', fecha: '2026-11-11T11:59:00Z' },
      }),
      conRequisitos,
      t,
    );
    expect(entrada.titulo).toBe('El tercero retiró la versión 8 de Registro Único Tributario (motivo: Subí el RUT de otra empresa); vuelve a la versión 7.');
  });

  it('sin retiro sigue diciendo que llegó una versión', () => {
    const entrada = eventoExpedienteATimeline(
      evento('version_adjuntada_a_requisito', { requisitoId: 'r-rut', version: { documentoId: 'd1', numero: 9 }, numeroAnterior: 8, retiro: null }),
      conRequisitos,
      t,
    );
    expect(entrada.titulo).toBe('Registro Único Tributario: llegó la versión 9');
  });

  it('una actualización que se abre por un retiro lo dice con el motivo en vez de listar documentos', () => {
    const entrada = eventoExpedienteATimeline(
      evento('requisitos_calculados', {
        requisitos: [
          { requisitoId: 'r-rut', nombre: 'Registro Único Tributario', version: { documentoId: 'd1', numero: 7 }, retiro: { numero: 8, motivo: 'Archivo equivocado', fecha: '2026-11-11T11:59:00Z' } },
        ],
      }),
      conRequisitos,
      t,
    );
    expect(entrada.titulo).toBe('El tercero retiró la versión 8 de Registro Único Tributario (motivo: Archivo equivocado); vuelve a la versión 7.');

    const normal = eventoExpedienteATimeline(
      evento('requisitos_calculados', { requisitos: [{ requisitoId: 'r-rut', nombre: 'Registro Único Tributario', version: null }] }),
      conRequisitos,
      t,
    );
    expect(normal.titulo).toBe('Documentos que pide el flujo');
    expect(normal.detalle).toBe('Registro Único Tributario');
  });
});
