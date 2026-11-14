import { claveDeTipoEn } from '@validasoft/api-client';

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
  'campanias.terminada': 'campaniaTerminada',
  'expediente.sla_por_vencer': 'slaPorVencer',
  'expediente.sla_incumplido': 'slaIncumplido',
  'documento.por_vencer': 'porVencer',
  'documento.vencido': 'vencido',
  'webhook.deshabilitado': 'webhookDeshabilitado',
  'ia.cupo': 'cupoIa',
  'verificacion.coincidencia': 'coincidenciaListas',
  'verificacion.estudio_pendiente': 'estudioPendiente',
  'verificacion.expirada': 'verificacionExpirada',
  'habeas.solicitud': 'habeasSolicitud',
  'habeas.plazo': 'habeasPlazo',
  'plan.limite': 'planLimite',
  'vinculo.cuenta_propuesta': 'cuentaPropuesta',
  'invitaciones.por_aprobar': 'invitacionPorAprobar',
  'invitaciones.decidida': 'invitacionDecidida',
} as const;

export type ClaveTipo = (typeof CLAVES_TIPO)[keyof typeof CLAVES_TIPO];

/** Clave de textos del tipo, o `null` si es uno que esta versión de la app aún no conoce. */
export function claveDeTipo(tipo: string): ClaveTipo | null {
  return claveDeTipoEn(CLAVES_TIPO, tipo);
}
