import type { BandejaNotificacionesDto, NotificacionDto } from '@validasoft/api-client';
import type { EventoSse } from './sse';

/**
 * Cambios puros sobre la bandeja en caché (`GET /api/v1/notificaciones`): llegan por el stream o por las acciones de la
 * campana, y el total de no leídas (`noLeidas`) debe seguir cuadrando con la lista.
 */

/** Agrega una notificación nueva arriba (más reciente primero); si ya estaba, la reemplaza sin tocar el contador. */
export function agregarNotificacion(bandeja: BandejaNotificacionesDto, notificacion: NotificacionDto): BandejaNotificacionesDto {
  const existente = bandeja.datos.find((n) => n.id === notificacion.id);
  if (existente) return { ...bandeja, datos: bandeja.datos.map((n) => (n.id === notificacion.id ? notificacion : n)) };
  return {
    ...bandeja,
    datos: [notificacion, ...bandeja.datos],
    noLeidas: bandeja.noLeidas + (notificacion.leidaEn ? 0 : 1),
  };
}

/** Marca una como leída (si estaba sin leer, baja el contador). */
export function marcarLeidaEnBandeja(bandeja: BandejaNotificacionesDto, id: string, ahora: string): BandejaNotificacionesDto {
  const objetivo = bandeja.datos.find((n) => n.id === id);
  if (!objetivo || objetivo.leidaEn) return bandeja;
  return {
    ...bandeja,
    datos: bandeja.datos.map((n) => (n.id === id ? { ...n, leidaEn: ahora } : n)),
    noLeidas: Math.max(0, bandeja.noLeidas - 1),
  };
}

export function marcarTodasEnBandeja(bandeja: BandejaNotificacionesDto, ahora: string): BandejaNotificacionesDto {
  return { ...bandeja, datos: bandeja.datos.map((n) => (n.leidaEn ? n : { ...n, leidaEn: ahora })), noLeidas: 0 };
}

/** Lo que el badge muestra: nada sin no leídas, el número hasta 99 y luego `99+`. */
export function textoContador(noLeidas: number): string {
  if (noLeidas <= 0) return '';
  return noLeidas > 99 ? '99+' : String(noLeidas);
}

/**
 * A dónde lleva el clic: el `enlace` del servidor (ruta de `apps/web` sin host, `/e/{empresa}/expedientes/{id}`) o, en
 * un webhook deshabilitado (del grupo, sin enlace), la configuración de webhooks de la empresa activa.
 */
export function destinoDeNotificacion(notificacion: NotificacionDto, empresaId: string): string | null {
  if (notificacion.enlace?.startsWith('/')) return notificacion.enlace;
  if (notificacion.tipo === 'webhook.deshabilitado') return `/e/${empresaId}/webhooks`;
  return null;
}

export type EventoNotificaciones = { tipo: 'notificacion'; notificacion: NotificacionDto } | { tipo: 'no_leidas'; total: number };

function esTextoONulo(valor: unknown): valor is string | null {
  return valor === null || valor === undefined || typeof valor === 'string';
}

/** Valida lo que llega por el stream: no se confía en la forma del JSON. */
export function esNotificacion(valor: unknown): valor is NotificacionDto {
  if (typeof valor !== 'object' || valor === null) return false;
  const v = valor as Record<string, unknown>;
  return (
    typeof v.id === 'string' &&
    typeof v.tipo === 'string' &&
    typeof v.titulo === 'string' &&
    typeof v.mensaje === 'string' &&
    typeof v.creadaEn === 'string' &&
    esTextoONulo(v.leidaEn) &&
    esTextoONulo(v.enlace) &&
    esTextoONulo(v.empresaId) &&
    esTextoONulo(v.expedienteId) &&
    esTextoONulo(v.vinculoId) &&
    esTextoONulo(v.folio)
  );
}

/** `event: notificacion` (`data` = `NotificacionDto`) y `event: no_leidas` (`data` = el total); lo demás se ignora. */
export function interpretarEvento(evento: EventoSse): EventoNotificaciones | null {
  if (evento.evento === 'no_leidas') {
    const total = Number(evento.datos.trim());
    return Number.isInteger(total) && total >= 0 ? { tipo: 'no_leidas', total } : null;
  }
  if (evento.evento !== 'notificacion') return null;
  try {
    const datos: unknown = JSON.parse(evento.datos);
    if (!esNotificacion(datos)) return null;
    // El servidor omite o manda `null` en los opcionales; la caché los guarda siempre como `null`.
    return {
      tipo: 'notificacion',
      notificacion: {
        ...datos,
        empresaId: datos.empresaId ?? null,
        expedienteId: datos.expedienteId ?? null,
        vinculoId: datos.vinculoId ?? null,
        folio: datos.folio ?? null,
        enlace: datos.enlace ?? null,
        leidaEn: datos.leidaEn ?? null,
      },
    };
  } catch {
    return null;
  }
}
