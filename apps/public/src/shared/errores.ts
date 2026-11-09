import { esErrorApi, esErrorDeRed } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { esCO } from '../i18n/es-CO';

type CodigoConMensaje = Exclude<keyof typeof esCO.errores, 'red' | 'generico'>;

function tieneMensaje(codigo: string): codigo is CodigoConMensaje {
  return codigo in esCO.errores && codigo !== 'red' && codigo !== 'generico';
}

/** Mensaje con siguiente paso; sin texto propio usa el `detail` de la API (ya en español). Nunca códigos crudos. */
export function mensajeDeError(error: unknown, t: TFunction): string {
  if (esErrorDeRed(error)) return t('errores.red');
  if (!esErrorApi(error)) return t('errores.error-interno', { traceId: '—' });
  const traceId = error.traceId ?? error.correlationId ?? '—';
  if (tieneMensaje(error.codigo)) return t(`errores.${error.codigo}`, { traceId });
  if (error.estado >= 500) return t('errores.error-interno', { traceId });
  return t('errores.generico', { detalle: error.detalle });
}
