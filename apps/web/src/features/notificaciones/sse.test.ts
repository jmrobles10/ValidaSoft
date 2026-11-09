import { describe, expect, it } from 'vitest';
import { crearParserSse, type EventoSse } from './sse';

function parsear(trozos: string[], idInicial?: string) {
  const eventos: EventoSse[] = [];
  const parser = crearParserSse((e) => eventos.push(e), idInicial);
  for (const trozo of trozos) parser.agregar(trozo);
  return { eventos, parser };
}

describe('crearParserSse', () => {
  it('interpreta id, event y data como los escribe el servidor', () => {
    const { eventos, parser } = parsear(['id: 42\nevent: notificacion\ndata: {"a":1}\n\n']);
    expect(eventos).toEqual([{ id: '42', evento: 'notificacion', datos: '{"a":1}' }]);
    expect(parser.ultimoId).toBe('42');
  });

  it('arma un evento partido entre trozos, incluso a mitad de un campo o de un \\r\\n', () => {
    const { eventos } = parsear(['id: 7\nev', 'ent: no_le', 'idas\r', '\ndata: 3\r', '\n', '\r\n']);
    expect(eventos).toEqual([{ id: '7', evento: 'no_leidas', datos: '3' }]);
  });

  it('ignora los comentarios (conectado y latido) y no despacha sin datos', () => {
    const { eventos } = parsear([': conectado\n\n', ': latido\n\n', 'event: solo-tipo\n\n', 'data: hola\n\n']);
    expect(eventos).toEqual([{ id: '', evento: 'message', datos: 'hola' }]);
  });

  it('une varias líneas data con salto de línea y quita un solo espacio tras los dos puntos', () => {
    const { eventos } = parsear(['data:uno\ndata:  dos\ndata\n\n']);
    expect(eventos[0]?.datos).toBe('uno\n dos\n');
  });

  it('un id sin datos también cuenta para reanudar, y el id se conserva en los eventos siguientes', () => {
    const { eventos, parser } = parsear(['id: 10\n\n', 'data: a\n\n']);
    expect(parser.ultimoId).toBe('10');
    expect(eventos).toEqual([{ id: '10', evento: 'message', datos: 'a' }]);
  });

  it('parte del id con el que se reconecta (Last-Event-ID) y lo reemplaza con el siguiente', () => {
    const { eventos, parser } = parsear(['data: sin id nuevo\n\n', 'id: 99\ndata: x\n\n'], '50');
    expect(eventos.map((e) => e.id)).toEqual(['50', '99']);
    expect(parser.ultimoId).toBe('99');
  });

  it('lee retry solo si son dígitos, quita el BOM inicial y no despacha un evento sin línea en blanco', () => {
    const { eventos, parser } = parsear(['﻿retry: 5000\nretry: x\ndata: incompleto\n']);
    expect(parser.reintento).toBe(5000);
    expect(eventos).toEqual([]);
  });
});
