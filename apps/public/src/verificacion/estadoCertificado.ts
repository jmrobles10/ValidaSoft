import { esErrorApi, esErrorDeRed, type CertificadoPublicoDto } from '@validasoft/api-client';
import { formatearDiaCivil, formatearFecha, type EstadoCertificado } from '@validasoft/ui';
import type { TFunction } from 'i18next';

/** Estados que puede traer `CertificadoPublicoDto.estado` (`ConPendientes` llega con H6). */
const ESTADOS: readonly EstadoCertificado[] = ['Vigente', 'ConPendientes', 'Suspendido', 'Vencido', 'Revocado', 'Reemplazado'];

export function estadoDelCertificado(estado: string): EstadoCertificado | null {
  return ESTADOS.find((e) => e === estado) ?? null;
}

/** Ruta de esta página, la misma forma que pone el QR del PDF: `{UrlPublica}/verificar/{folio}?huella={12 hex}`. */
export function rutaDeVerificacion(folio: string, huella?: string | null): string {
  const base = `/verificar/${encodeURIComponent(folio)}`;
  return huella ? `${base}?huella=${encodeURIComponent(huella)}` : base;
}

/**
 * Qué significa el estado para quien escaneó el QR, con la fecha que importa: el vencimiento (si venció, la vigencia;
 * si sigue vigente, cuándo vence) o desde cuándo está así.
 */
export function textoDelEstado(c: CertificadoPublicoDto, t: TFunction): { titulo: string; texto: string } {
  const estado = estadoDelCertificado(c.estado);
  const valores = { empresa: c.emisor.empresa, tercero: c.titular.nombre, estado: c.estado, fecha: formatearFecha(c.estadoDesde) };
  if (!estado) return { titulo: t('verificacion.estados.otro.titulo', valores), texto: t('verificacion.estados.otro.texto', valores) };
  if (estado === 'Vencido' && c.vigenteHasta) valores.fecha = formatearDiaCivil(c.vigenteHasta);

  const titulo = t(`verificacion.estados.${estado}.titulo`, valores);
  const texto = t(`verificacion.estados.${estado}.texto`, valores);
  if (estado !== 'Vigente') return { titulo, texto };
  const vence = c.vigenteHasta ? t('verificacion.venceEl', { fecha: formatearDiaCivil(c.vigenteHasta) }) : t('verificacion.noVence');
  return { titulo, texto: `${texto} ${vence}` };
}

export type FalloDeConsulta = 'certificado-no-encontrado' | 'huella-invalida' | 'folio-ambiguo' | 'demasiadas-peticiones' | 'red' | 'no-disponible';

/**
 * Por qué no se pudo mostrar el certificado: 404 (folio inexistente, mal formado u otra huella), 422 de la huella,
 * 409 por folio repetido entre tenants, 429 del límite por IP, sin conexión o el servicio caído (5xx y lo demás).
 */
export function falloDeConsulta(error: unknown): FalloDeConsulta {
  if (esErrorDeRed(error)) return 'red';
  if (!esErrorApi(error)) return 'no-disponible';
  if (error.estado === 404) return 'certificado-no-encontrado';
  if (error.estado === 429) return 'demasiadas-peticiones';
  if (error.codigo === 'huella-invalida') return 'huella-invalida';
  if (error.codigo === 'folio-ambiguo') return 'folio-ambiguo';
  return 'no-disponible';
}

/** Se puede reintentar lo que no es culpa del enlace. */
export const falloReintentable = (fallo: FalloDeConsulta) => fallo === 'red' || fallo === 'no-disponible' || fallo === 'demasiadas-peticiones';
