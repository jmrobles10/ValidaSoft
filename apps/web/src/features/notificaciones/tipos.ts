/**
 * Tipos de notificación a personas (docs/05 §6.5, `TiposNotificacion` en el backend) y su clave de textos: los códigos
 * llevan punto (`expediente.nuevo_en_area`) y en i18next el punto separa niveles.
 */
export const CLAVES_TIPO = {
  'expediente.nuevo_en_area': 'nuevoEnArea',
  'expediente.tercero_respondio': 'terceroRespondio',
  'expediente.version_nueva': 'versionNueva',
  'documento.version_retirada': 'versionRetirada',
  'expediente.aprobado': 'aprobado',
  'expediente.rechazado': 'rechazado',
  'expediente.caducado': 'caducado',
  'invitaciones.lote_terminado': 'loteTerminado',
  'documento.por_vencer': 'porVencer',
  'documento.vencido': 'vencido',
  'webhook.deshabilitado': 'webhookDeshabilitado',
  'ia.cupo': 'cupoIa',
  'verificacion.coincidencia': 'coincidenciaListas',
  'verificacion.estudio_pendiente': 'estudioPendiente',
  'verificacion.expirada': 'verificacionExpirada',
  'vinculo.cuenta_propuesta': 'cuentaPropuesta',
} as const;

export type ClaveTipo = (typeof CLAVES_TIPO)[keyof typeof CLAVES_TIPO];

/** Clave de textos del tipo, o `null` si es uno que esta versión de la app aún no conoce. */
export function claveDeTipo(tipo: string): ClaveTipo | null {
  const claves: Readonly<Record<string, ClaveTipo | undefined>> = CLAVES_TIPO;
  return Object.hasOwn(claves, tipo) ? (claves[tipo] ?? null) : null;
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
