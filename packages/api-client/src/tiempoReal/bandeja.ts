import { useQueryClient, type QueryClient, type QueryKey } from '@tanstack/react-query';
import type { EventoSse } from './sse';

/**
 * Lo común de la campana de la web de empresa (`BandejaNotificacionesDto`) y la del portal del tercero
 * (`BandejaNotificacionesTerceroDto`): la bandeja vive en la caché de consultas y cambia por lo que llega por el stream o por las
 * acciones de la campana. Son cambios puros y el total de no leídas (`noLeidas`) debe seguir cuadrando con la lista.
 */

/** Lo mínimo que se necesita de una notificación para llevar la cuenta: cada app agrega sus propios campos. */
export interface NotificacionDeBandeja {
  id: string;
  leidaEn: string | null;
}

export interface BandejaDeNotificaciones<N extends NotificacionDeBandeja> {
  datos: N[];
  noLeidas: number;
}

/** Agrega una notificación nueva arriba (más reciente primero); si ya estaba, la reemplaza sin tocar el contador. */
export function agregarNotificacion<N extends NotificacionDeBandeja, B extends BandejaDeNotificaciones<N>>(bandeja: B, notificacion: N): B {
  if (bandeja.datos.some((n) => n.id === notificacion.id)) return { ...bandeja, datos: bandeja.datos.map((n) => (n.id === notificacion.id ? notificacion : n)) };
  return { ...bandeja, datos: [notificacion, ...bandeja.datos], noLeidas: bandeja.noLeidas + (notificacion.leidaEn ? 0 : 1) };
}

/** Marca una como leída (si estaba sin leer, baja el contador). */
export function marcarLeidaEnBandeja<N extends NotificacionDeBandeja, B extends BandejaDeNotificaciones<N>>(bandeja: B, id: string, ahora: string): B {
  const objetivo = bandeja.datos.find((n) => n.id === id);
  if (!objetivo || objetivo.leidaEn) return bandeja;
  return {
    ...bandeja,
    datos: bandeja.datos.map((n) => (n.id === id ? { ...n, leidaEn: ahora } : n)),
    noLeidas: Math.max(0, bandeja.noLeidas - 1),
  };
}

export function marcarTodasEnBandeja<N extends NotificacionDeBandeja, B extends BandejaDeNotificaciones<N>>(bandeja: B, ahora: string): B {
  return { ...bandeja, datos: bandeja.datos.map((n) => (n.leidaEn ? n : { ...n, leidaEn: ahora })), noLeidas: 0 };
}

/** Lo que llega por el stream de notificaciones: una nueva, o el total de no leídas para cuadrar el contador. */
export type EventoDeNotificaciones<N> = { tipo: 'notificacion'; notificacion: N } | { tipo: 'no_leidas'; total: number };

export function esTextoONulo(valor: unknown): valor is string | null {
  return valor === null || valor === undefined || typeof valor === 'string';
}

/**
 * `event: notificacion` (`data` = la notificación en JSON) y `event: no_leidas` (`data` = el total); lo demás se ignora. La forma de
 * la notificación la valida cada app (`leerNotificacion` devuelve `null` si no es la esperada): no se confía en el JSON del stream.
 */
export function interpretarEventoDeNotificaciones<N>(evento: EventoSse, leerNotificacion: (datos: unknown) => N | null): EventoDeNotificaciones<N> | null {
  if (evento.evento === 'no_leidas') {
    const total = Number(evento.datos.trim());
    return Number.isInteger(total) && total >= 0 ? { tipo: 'no_leidas', total } : null;
  }
  if (evento.evento !== 'notificacion') return null;
  try {
    const notificacion = leerNotificacion(JSON.parse(evento.datos));
    return notificacion ? { tipo: 'notificacion', notificacion } : null;
  } catch {
    return null;
  }
}

/**
 * Aplica a la bandeja en caché lo que llegó por el stream: cada notificación nueva entra arriba; si el total de no leídas que manda el
 * servidor no cuadra con la caché (llegó algo antes de abrir el stream, o se leyó en otra pestaña), se vuelve a pedir la bandeja.
 */
export function aplicarEventoDeBandeja<B extends BandejaDeNotificaciones<NotificacionDeBandeja>>(
  cliente: QueryClient,
  clave: QueryKey,
  evento: EventoDeNotificaciones<B['datos'][number]>,
): void {
  const actual = cliente.getQueryData<B>(clave);
  if (evento.tipo === 'notificacion') {
    if (actual) cliente.setQueryData<B>(clave, agregarNotificacion(actual, evento.notificacion));
    else void cliente.invalidateQueries({ queryKey: clave });
    return;
  }
  if (actual?.noLeidas !== evento.total) void cliente.invalidateQueries({ queryKey: clave });
}

/** Aplica un cambio optimista a la bandeja y devuelve con qué deshacerlo si la API lo rechaza. */
export function useCambioOptimistaDeBandeja<B extends BandejaDeNotificaciones<NotificacionDeBandeja>>(clave: QueryKey) {
  const cliente = useQueryClient();
  return {
    aplicar: async (cambio: (bandeja: B) => B) => {
      await cliente.cancelQueries({ queryKey: clave });
      const previa = cliente.getQueryData<B>(clave);
      if (previa) cliente.setQueryData<B>(clave, cambio(previa));
      return previa;
    },
    deshacer: (previa: B | undefined) => {
      if (previa) cliente.setQueryData(clave, previa);
      void cliente.invalidateQueries({ queryKey: clave });
    },
  };
}

/** Clave de textos de un tipo de notificación (en i18next el punto de los códigos separa niveles), o `null` si es uno que esta versión no conoce. */
export function claveDeTipoEn<C extends string>(claves: Readonly<Record<string, C>>, tipo: string): C | null {
  return Object.hasOwn(claves, tipo) ? (claves[tipo] ?? null) : null;
}

/**
 * Lo que va en el `PUT` de preferencias: solo los tipos cuyo valor editado difiere del guardado (el servidor deja los demás como
 * estaban). Un tipo que no viene en `guardadas` vale `true` (el valor por defecto).
 */
export function cambiosDePreferencias(guardadas: Readonly<Record<string, boolean>>, editados: Readonly<Record<string, boolean>>): Record<string, boolean> {
  const cambios: Record<string, boolean> = {};
  for (const [tipo, valor] of Object.entries(editados)) {
    if ((guardadas[tipo] ?? true) !== valor) cambios[tipo] = valor;
  }
  return cambios;
}
