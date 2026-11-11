import type { BandejaNotificacionesTerceroDto, EventoSse, NotificacionTerceroDto } from '@validasoft/api-client';

/**
 * Cambios puros sobre la bandeja en caché (`GET /public/v1/notificaciones`): vienen de las acciones de la campana y el total de
 * no leídas (`noLeidas`, de todas las empresas del tercero) debe seguir cuadrando con la lista.
 */

/** Marca una como leída (si estaba sin leer, baja el contador). */
export function marcarLeidaEnBandeja(bandeja: BandejaNotificacionesTerceroDto, id: string, ahora: string): BandejaNotificacionesTerceroDto {
  const objetivo = bandeja.datos.find((n) => n.id === id);
  if (!objetivo || objetivo.leidaEn) return bandeja;
  return {
    ...bandeja,
    datos: bandeja.datos.map((n) => (n.id === id ? { ...n, leidaEn: ahora } : n)),
    noLeidas: Math.max(0, bandeja.noLeidas - 1),
  };
}

export function marcarTodasEnBandeja(bandeja: BandejaNotificacionesTerceroDto, ahora: string): BandejaNotificacionesTerceroDto {
  return { ...bandeja, datos: bandeja.datos.map((n) => (n.leidaEn ? n : { ...n, leidaEn: ahora })), noLeidas: 0 };
}

/** Lo que el contador muestra: nada sin no leídas, el número hasta 99 y luego `99+`. */
export function textoContador(noLeidas: number): string {
  if (noLeidas <= 0) return '';
  return noLeidas > 99 ? '99+' : String(noLeidas);
}

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
} as const;

export type ClaveTipo = (typeof CLAVES_TIPO)[keyof typeof CLAVES_TIPO];

/** Clave de textos del tipo, o `null` si no va por correo (o es uno que esta versión de la app aún no conoce). */
export function claveDeTipo(tipo: string): ClaveTipo | null {
  const claves: Readonly<Record<string, ClaveTipo | undefined>> = CLAVES_TIPO;
  return Object.hasOwn(claves, tipo) ? (claves[tipo] ?? null) : null;
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

/** Lo que va en el `PUT` de preferencias: solo los tipos cuyo valor editado difiere del guardado (un tipo sin guardar vale `true`). */
export function cambiosDePreferencias(guardadas: Readonly<Record<string, boolean>>, editados: Readonly<Record<string, boolean>>): Record<string, boolean> {
  const cambios: Record<string, boolean> = {};
  for (const [tipo, valor] of Object.entries(editados)) {
    if ((guardadas[tipo] ?? true) !== valor) cambios[tipo] = valor;
  }
  return cambios;
}

const TIEMPO = new Intl.RelativeTimeFormat('es-CO', { numeric: 'auto' });

const UNIDADES: readonly [Intl.RelativeTimeFormatUnit, number][] = [
  ['year', 365 * 24 * 3600],
  ['month', 30 * 24 * 3600],
  ['week', 7 * 24 * 3600],
  ['day', 24 * 3600],
  ['hour', 3600],
  ['minute', 60],
];

/** «hace 5 minutos», «ayer», «hace 3 semanas»; menos de un minuto (o una fecha futura por desfase de reloj) es «ahora». */
export function haceCuanto(fecha: string, ahora: number): string {
  const segundos = Math.round((ahora - new Date(fecha).getTime()) / 1000);
  for (const [unidad, tamano] of UNIDADES) {
    if (segundos >= tamano) return TIEMPO.format(-Math.floor(segundos / tamano), unidad);
  }
  return TIEMPO.format(0, 'second');
}

/** Agrega una notificación nueva arriba (más reciente primero); si ya estaba, la reemplaza sin tocar el contador. */
export function agregarNotificacion(bandeja: BandejaNotificacionesTerceroDto, notificacion: NotificacionTerceroDto): BandejaNotificacionesTerceroDto {
  if (bandeja.datos.some((n) => n.id === notificacion.id)) return { ...bandeja, datos: bandeja.datos.map((n) => (n.id === notificacion.id ? notificacion : n)) };
  return { ...bandeja, datos: [notificacion, ...bandeja.datos], noLeidas: bandeja.noLeidas + (notificacion.leidaEn ? 0 : 1) };
}

export type EventoDelTercero = { tipo: 'notificacion'; notificacion: NotificacionTerceroDto } | { tipo: 'no_leidas'; total: number };

function esTextoONulo(valor: unknown): valor is string | null {
  return valor === null || valor === undefined || typeof valor === 'string';
}

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

/** `event: notificacion` (`data` = `NotificacionTerceroDto`) y `event: no_leidas` (`data` = el total); lo demás se ignora. */
export function interpretarEvento(evento: EventoSse): EventoDelTercero | null {
  if (evento.evento === 'no_leidas') {
    const total = Number(evento.datos.trim());
    return Number.isInteger(total) && total >= 0 ? { tipo: 'no_leidas', total } : null;
  }
  if (evento.evento !== 'notificacion') return null;
  try {
    const datos: unknown = JSON.parse(evento.datos);
    if (!esNotificacionDelTercero(datos)) return null;
    // El servidor omite o manda `null` en los opcionales; la caché los guarda siempre como `null`.
    return {
      tipo: 'notificacion',
      notificacion: {
        ...datos,
        empresa: datos.empresa ?? null,
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
