import type { EventoHistorialDto } from '@validasoft/api-client';
import i18n from 'i18next';
import { describe, expect, it } from 'vitest';
import { eventoATimeline } from '../historial';

const t = i18n.t.bind(i18n);

function evento(tipo: string, datos: Record<string, unknown>): EventoHistorialDto {
  return { version: 12, tipo, fecha: '2026-11-12T15:00:00Z', actorTipo: 'Usuario', actorId: 'u1', sistema: null, datos };
}

describe('historial del vínculo: exenciones (EX-1)', () => {
  it('registrada: dice de qué se eximió, hasta cuándo y la justificación', () => {
    const entrada = eventoATimeline(
      evento('exencion_de_vinculo_registrada', {
        ExencionId: 'ex1',
        Todos: false,
        Objetivos: [
          { Tipo: 0, Codigo: 'RUT', Nombre: 'Registro Único Tributario' },
          { Tipo: 1, Codigo: 'FICHA_GENERAL', Nombre: 'Información general' },
        ],
        Justificacion: 'La renueva Contabilidad.',
        Hasta: '2026-12-31',
      }),
      { usuarioActual: 'u1' },
      t,
    );

    expect(entrada.titulo).toBe('Exención registrada');
    expect(entrada.detalle).toBe('Registro Único Tributario, Información general · Hasta el 31/12/2026');
    expect(entrada.motivo).toBe('La renueva Contabilidad.');
    expect(entrada.actor.nombre).toBe('Tú');
  });

  it('registrada de todo y sin fecha', () => {
    const entrada = eventoATimeline(evento('exencion_de_vinculo_registrada', { Todos: true, Objetivos: [], Justificacion: 'Estratégico.', Hasta: null }), { usuarioActual: null }, t);

    expect(entrada.detalle).toBe('Todos los requisitos y la vigencia del vínculo');
  });

  it('revocada lleva su justificación; vencida y restablecida tienen su título', () => {
    const revocada = eventoATimeline(evento('exencion_de_vinculo_revocada', { Justificacion: 'Ya no aplica.' }), { usuarioActual: null }, t);
    expect(revocada.titulo).toBe('Exención revocada');
    expect(revocada.motivo).toBe('Ya no aplica.');

    expect(eventoATimeline(evento('exencion_de_vinculo_vencida', { Hasta: '2026-11-30' }), { usuarioActual: null }, t).titulo).toBe('Exención vencida');

    const restablecida = eventoATimeline(evento('vinculo_restablecido_por_exencion', { EstadoPrevio: 4 }), { usuarioActual: null }, t);
    expect(restablecida.titulo).toBe('Vínculo restablecido por una exención');
    expect(restablecida.antes).toBe('Vencido');
    expect(restablecida.despues).toBe('Activo');
  });
});
