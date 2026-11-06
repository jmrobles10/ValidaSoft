import type { ComparticionPublicaDto, VersionDocumentoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { MAXIMO_PAGINAS } from './subida';

/** Por qué se devolvió una versión, en lenguaje del tercero y con qué hacer (BO-4). */
export function mensajeDevolucion(version: Pick<VersionDocumentoDto, 'motivoDevolucion' | 'detalleDevolucion'>, t: TFunction): string {
  switch (version.motivoDevolucion) {
    case 'pdf-con-contenido-activo':
      return t('boveda.devolucion.pdf-con-contenido-activo');
    case 'demasiadas-paginas':
      return t('boveda.devolucion.demasiadas-paginas', { max: String(MAXIMO_PAGINAS) });
    default:
      return version.detalleDevolucion ?? t('boveda.devolucion.otro');
  }
}

/** Qué tiene cada empresa de este documento: la versión vigente, una anterior o nada (no la exige o no aceptó). */
export function detalleComparticion(c: ComparticionPublicaDto, t: TFunction): string {
  if (!c.compartido || c.version === null) return t('boveda.noCompartida');
  if (c.versionActualCompartida) return t('boveda.compartidaActual', { fecha: c.compartidaEn ? formatearFecha(c.compartidaEn) : '—' });
  return t('boveda.compartidaOtra', { n: String(c.version) });
}

/** Se puede retirar la vigente o una superada (BO-8); devueltas y retiradas no. */
export function sePuedeRetirar(version: Pick<VersionDocumentoDto, 'estado'>): boolean {
  return version.estado === 'Actual' || version.estado === 'Superada';
}

/** Se puede descargar lo que quedó en la bóveda (no lo devuelto, que está en cuarentena, ni lo retirado). */
export function sePuedeDescargar(version: Pick<VersionDocumentoDto, 'estado'>): boolean {
  return version.estado === 'Actual' || version.estado === 'Superada';
}
