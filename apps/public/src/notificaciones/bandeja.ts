import { claveDeTipoEn, esTextoONulo, interpretarEventoDeNotificaciones, type EventoDeNotificaciones, type EventoSse, type NotificacionTerceroDto } from '@validasoft/api-client';

/**
 * Lo propio de la bandeja del tercero (`GET /public/v1/notificaciones`): el total de no leídas (`noLeidas`) es de todas sus empresas.
 * Los cambios sobre la bandeja en caché (agregar, marcar leídas) y el contador son los mismos de la web de empresa y viven en
 * `@validasoft/api-client` y `@validasoft/ui`.
 */

/**
 * A dónde lleva el clic: el `enlace` del servidor (ruta de esta app sin host: `/expediente/{id}`, `/boveda`, `/ficha`…). Solo
 * rutas internas: nunca un enlace a otro sitio, aunque el dato lo trajera.
 */
export function destinoDeNotificacion(notificacion: Pick<NotificacionTerceroDto, 'enlace'>): string | null {
  const enlace = notificacion.enlace;
  return enlace?.startsWith('/') && !enlace.startsWith('//') ? enlace : null;
}

/** Tipos de notificación al tercero (docs/05 §8.5) y su clave de textos: los códigos llevan punto y en i18next el punto separa niveles. */
export const CLAVES_TIPO = {
  'tercero.observado': 'observado',
  'tercero.recordatorio': 'recordatorio',
  'tercero.resultado': 'resultado',
  'tercero.documento_por_vencer': 'documentoPorVencer',
  'tercero.documento_vencido': 'documentoVencido',
  'tercero.certificado': 'certificado',
  'tercero.plazo_de_cargue': 'plazoDeCargue',
  'tercero.desaprobado': 'desaprobado',
  'tercero.reintegrado': 'reintegrado',
  'tercero.vigencia': 'vigencia',
} as const;

export type ClaveTipo = (typeof CLAVES_TIPO)[keyof typeof CLAVES_TIPO];

/** Clave de textos del tipo, o `null` si no va por correo (o es uno que esta versión de la app aún no conoce). */
export function claveDeTipo(tipo: string): ClaveTipo | null {
  return claveDeTipoEn(CLAVES_TIPO, tipo);
}

/**
 * Solo los tipos que esta versión conoce y que el servidor dice que pueden ir por correo, en el orden del servidor (los
 * desconocidos no se muestran: sin texto no se podría explicar qué se apaga).
 */
export function tiposConCorreo(tiposDelServidor: readonly string[]): { tipo: string; clave: ClaveTipo }[] {
  return tiposDelServidor.flatMap((tipo) => {
    const clave = claveDeTipo(tipo);
    return clave ? [{ tipo, clave }] : [];
  });
}

export type EventoDelTercero = EventoDeNotificaciones<NotificacionTerceroDto>;

/** Valida lo que llega por el stream: no se confía en la forma del JSON. */
export function esNotificacionDelTercero(valor: unknown): valor is NotificacionTerceroDto {
  if (typeof valor !== 'object' || valor === null) return false;
  const v = valor as Record<string, unknown>;
  const empresaValida = v.empresa === null || v.empresa === undefined || (typeof v.empresa === 'object' && typeof (v.empresa as Record<string, unknown>).nombre === 'string');
  return (
    typeof v.id === 'string' &&
    typeof v.tipo === 'string' &&
    typeof v.titulo === 'string' &&
    typeof v.mensaje === 'string' &&
    typeof v.creadaEn === 'string' &&
    empresaValida &&
    esTextoONulo(v.leidaEn) &&
    esTextoONulo(v.enlace) &&
    esTextoONulo(v.expedienteId) &&
    esTextoONulo(v.vinculoId) &&
    esTextoONulo(v.folio)
  );
}

/** El servidor omite o manda `null` en los opcionales; la caché los guarda siempre como `null`. */
function normalizar(datos: unknown): NotificacionTerceroDto | null {
  if (!esNotificacionDelTercero(datos)) return null;
  return {
    ...datos,
    empresa: datos.empresa ?? null,
    expedienteId: datos.expedienteId ?? null,
    vinculoId: datos.vinculoId ?? null,
    folio: datos.folio ?? null,
    enlace: datos.enlace ?? null,
    leidaEn: datos.leidaEn ?? null,
  };
}

/** `event: notificacion` (`data` = `NotificacionTerceroDto`) y `event: no_leidas` (`data` = el total); lo demás se ignora. */
export function interpretarEvento(evento: EventoSse): EventoDelTercero | null {
  return interpretarEventoDeNotificaciones(evento, normalizar);
}
