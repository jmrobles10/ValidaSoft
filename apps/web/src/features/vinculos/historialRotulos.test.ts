import type { EventoHistorialDto } from '@validasoft/api-client';
import { readdirSync, readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import { esCO } from '../../i18n/es-CO';
import { eventoExpedienteATimeline } from '../expedientes/historial';
import { eventoATimeline, tituloDeTipoDesconocido } from './historial';

const t = i18n.t.bind(i18n);

/** Todos los textos (hojas de cadena) de un bloque de es-CO, con su ruta. */
function textos(bloque: unknown, ruta = ''): [string, string][] {
  if (typeof bloque === 'string') return [[ruta, bloque]];
  if (bloque && typeof bloque === 'object') return Object.entries(bloque).flatMap(([clave, valor]) => textos(valor, ruta ? `${ruta}.${clave}` : clave));
  return [];
}

/** Un rótulo empieza con mayúscula (o con un dato que ya la trae, como el nombre de un área: «{{area}} aprobó»). */
const empiezaEnMayuscula = (texto: string) => texto.startsWith('{{') || (texto.length > 0 && texto.startsWith(texto.charAt(0).toLocaleUpperCase('es-CO')));

function evento(tipo: string, datos: Record<string, unknown> = {}): EventoHistorialDto {
  return { version: 3, tipo, fecha: '2026-11-12T15:00:00Z', actorTipo: 'Tercero', actorId: 'tercero', sistema: null, datos };
}

/** `ConsentimientosRegistrados` → `consentimientos_registrados`: el nombre del evento en Marten. */
function aSnake(nombre: string): string {
  return nombre
    .replace(/([a-z0-9])([A-Z])/g, '$1_$2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1_$2')
    .toLowerCase();
}

/** Los eventos que el dominio aplica a un agregado (`void Apply(Evento e)`): lo que puede aparecer en su historial. */
function eventosDelDominio(carpeta: string): string[] {
  // Este archivo está en apps/web/src/features/vinculos: la raíz del repositorio queda cinco carpetas arriba.
  const dir = resolve(dirname(fileURLToPath(import.meta.url)), '../../../../..', 'src', 'Validasoft.Dominio', carpeta);
  return readdirSync(dir)
    .filter((archivo) => archivo.endsWith('.cs'))
    .flatMap((archivo) => [...readFileSync(resolve(dir, archivo), 'utf8').matchAll(/void Apply\((\w+) /g)].map((m) => aSnake(m[1] ?? '')));
}

describe('rótulos del historial (PP-2)', () => {
  it('todos los rótulos del historial del vínculo y del expediente empiezan con mayúscula', () => {
    const rotulos = [...textos(esCO.historial, 'historial'), ...textos(esCO.expedientes.historial.eventos, 'expedientes.historial.eventos')];
    expect(rotulos.length).toBeGreaterThan(40);
    expect(rotulos.filter(([, texto]) => !empiezaEnMayuscula(texto)).map(([ruta]) => ruta)).toEqual([]);
  });

  it('«consentimientos registrados» ya no sale en minúscula en la línea de tiempo del vínculo', () => {
    const entrada = eventoATimeline(evento('consentimientos_registrados'), { usuarioActual: null }, t);
    expect(entrada.titulo).toBe('Consentimientos registrados');
  });

  it('un evento sin texto propio sale con la inicial en mayúscula y sin guiones bajos, en el vínculo y en el expediente', () => {
    expect(tituloDeTipoDesconocido('evento_de_prueba_nuevo')).toBe('Evento de prueba nuevo');
    expect(eventoATimeline(evento('evento_de_prueba_nuevo'), { usuarioActual: null }, t).titulo).toBe('Evento de prueba nuevo');
    expect(eventoExpedienteATimeline(evento('evento_de_prueba_nuevo'), { usuarioActual: null, areas: new Map() }, t).titulo).toBe('Evento de prueba nuevo');
  });

  it('cada evento que el dominio aplica al vínculo y al expediente tiene su rótulo (ninguno cae al texto de reserva)', () => {
    const delVinculo = new Set(Object.keys(esCO.historial));
    const delExpediente = new Set(Object.keys(esCO.expedientes.historial.eventos));
    expect(eventosDelDominio('Vinculos').filter((tipo) => !delVinculo.has(tipo))).toEqual([]);
    expect(eventosDelDominio('Expedientes').filter((tipo) => !delExpediente.has(tipo))).toEqual([]);
  });
});
