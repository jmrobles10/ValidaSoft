import type { TerceroDto } from '@validasoft/api-client';

/** "NIT 900123456-8" (tipo, número y dígito de verificación). */
export function identificacionDe(tercero: Pick<TerceroDto, 'identificacion'>): string {
  const { numero, digitoVerificacion, tipo } = tercero.identificacion;
  return `${tipo} ${numero}${digitoVerificacion ? `-${digitoVerificacion}` : ''}`;
}

/**
 * `TerceroResumenDto.identificacion` (expedientes y bandeja) llega como la clave interna `PAIS|TIPO|NUMERO`, sin
 * dígito de verificación: se muestra "NIT 900123456". Si ya viene legible, se deja igual.
 */
export function identificacionResumida(clave: string): string {
  const partes = clave.split('|');
  if (partes.length !== 3) return clave;
  const [, tipo = '', numero = ''] = partes;
  return `${tipo} ${numero}`.trim();
}
