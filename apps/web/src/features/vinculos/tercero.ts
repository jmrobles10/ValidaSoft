import type { TerceroDto } from '@validasoft/api-client';

/** "NIT 900123456-8" (tipo, número y dígito de verificación). */
export function identificacionDe(tercero: Pick<TerceroDto, 'identificacion'>): string {
  const { numero, digitoVerificacion, tipo } = tercero.identificacion;
  return `${tipo} ${numero}${digitoVerificacion ? `-${digitoVerificacion}` : ''}`;
}
