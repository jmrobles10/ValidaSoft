/**
 * Parser de Server-Sent Events según el estándar de HTML (sección «Interpreting an event stream»), para leer un stream
 * con `fetch`: `EventSource` no deja mandar `Authorization`, así que el cuerpo se lee por trozos y se interpreta aquí.
 *
 * - Las líneas terminan en `\n`, `\r\n` o `\r` (también si el `\r` y el `\n` llegan en trozos distintos).
 * - `:` al inicio es un comentario (el servidor manda `: conectado` y `: latido`).
 * - `id` fija el último id (el que se reenvía en `Last-Event-ID` al reconectar) aunque el evento no traiga datos.
 * - `data` se acumula con saltos de línea; un evento sin datos no se despacha.
 * - `retry` (solo dígitos) es la espera de reconexión que pide el servidor, en milisegundos.
 */

export interface EventoSse {
  /** Último id recibido hasta este evento (vacío si el servidor nunca mandó uno). */
  id: string;
  /** Campo `event`; `message` si no vino. */
  evento: string;
  datos: string;
}

export interface ParserSse {
  /** Interpreta un trozo de texto ya decodificado (UTF-8). */
  agregar: (trozo: string) => void;
  /** Último id visto: es el que va en `Last-Event-ID` al reconectar. */
  readonly ultimoId: string;
  /** Espera de reconexión pedida con `retry`, o `null`. */
  readonly reintento: number | null;
}

export function crearParserSse(alEvento: (evento: EventoSse) => void, idInicial = ''): ParserSse {
  let pendiente = '';
  let inicio = true;
  // Un `\r` al final de un trozo pudo ser la mitad de un `\r\n`: si el siguiente empieza por `\n`, se descarta.
  let terminoEnCr = false;
  let datos = '';
  let tipo = '';
  let idBuffer = idInicial;
  let ultimoId = idInicial;
  let reintento: number | null = null;

  const despachar = () => {
    ultimoId = idBuffer;
    if (datos === '') {
      tipo = '';
      return;
    }
    const evento: EventoSse = { id: ultimoId, evento: tipo || 'message', datos: datos.endsWith('\n') ? datos.slice(0, -1) : datos };
    datos = '';
    tipo = '';
    alEvento(evento);
  };

  const procesarLinea = (linea: string) => {
    if (linea === '') {
      despachar();
      return;
    }
    if (linea.startsWith(':')) return;
    const dosPuntos = linea.indexOf(':');
    const campo = dosPuntos === -1 ? linea : linea.slice(0, dosPuntos);
    let valor = dosPuntos === -1 ? '' : linea.slice(dosPuntos + 1);
    if (valor.startsWith(' ')) valor = valor.slice(1);
    switch (campo) {
      case 'data':
        datos += `${valor}\n`;
        break;
      case 'event':
        tipo = valor;
        break;
      case 'id':
        if (!valor.includes('\0')) idBuffer = valor;
        break;
      case 'retry':
        if (/^\d+$/.test(valor)) reintento = Number(valor);
        break;
      default:
        // Campo desconocido: se ignora.
        break;
    }
  };

  return {
    agregar(trozo) {
      let texto = trozo;
      if (terminoEnCr && texto.startsWith('\n')) texto = texto.slice(1);
      terminoEnCr = false;
      if (inicio && texto.length > 0) {
        if (texto.startsWith('﻿')) texto = texto.slice(1);
        inicio = false;
      }
      pendiente += texto;
      const lineas = pendiente.split(/\r\n|\r|\n/);
      // Lo que queda después del último separador aún no es una línea completa.
      pendiente = lineas.pop() ?? '';
      if (pendiente === '' && texto.endsWith('\r')) terminoEnCr = true;
      for (const linea of lineas) procesarLinea(linea);
    },
    get ultimoId() {
      return ultimoId;
    },
    get reintento() {
      return reintento;
    },
  };
}
