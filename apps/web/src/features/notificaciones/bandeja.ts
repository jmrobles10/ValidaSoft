import { esTextoONulo, interpretarEventoDeNotificaciones, type EventoDeNotificaciones, type EventoSse, type NotificacionDto } from '@validasoft/api-client';

/**
 * Lo propio de la bandeja de la web de empresa (`GET /api/v1/notificaciones`). Los cambios sobre la bandeja en caché (agregar, marcar
 * leídas) y el contador son los mismos del portal del tercero y viven en `@validasoft/api-client` y `@validasoft/ui`.
 */

/**
 * A dónde lleva el clic: el `enlace` del servidor (ruta de `apps/web` sin host, `/e/{empresa}/expedientes/{id}`) o, en
 * un webhook deshabilitado (del grupo, sin enlace), la configuración de webhooks de la empresa activa.
 */
export function destinoDeNotificacion(notificacion: NotificacionDto, empresaId: string): string | null {
  if (notificacion.enlace?.startsWith('/')) return notificacion.enlace;
  if (notificacion.tipo === 'webhook.deshabilitado') return `/e/${empresaId}/webhooks`;
  return null;
}

export type EventoNotificaciones = EventoDeNotificaciones<NotificacionDto>;

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

/** El servidor omite o manda `null` en los opcionales; la caché los guarda siempre como `null`. */
function normalizar(datos: unknown): NotificacionDto | null {
  if (!esNotificacion(datos)) return null;
  return {
    ...datos,
    empresaId: datos.empresaId ?? null,
    expedienteId: datos.expedienteId ?? null,
    vinculoId: datos.vinculoId ?? null,
    folio: datos.folio ?? null,
    enlace: datos.enlace ?? null,
    leidaEn: datos.leidaEn ?? null,
  };
}

/** `event: notificacion` (`data` = `NotificacionDto`) y `event: no_leidas` (`data` = el total); lo demás se ignora. */
export function interpretarEvento(evento: EventoSse): EventoNotificaciones | null {
  return interpretarEventoDeNotificaciones(evento, normalizar);
}
