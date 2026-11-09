import type { ComparticionPublicaDto, DocumentoBovedaDto, VersionDocumentoDto } from '@validasoft/api-client';
import { formatearFecha } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { textoVigencia } from '../vigencia/vigencia';
import { MAXIMO_PAGINAS } from './subida';

/** Por qué se devolvió una versión, en lenguaje del tercero y con qué hacer (BO-4). */
export function mensajeDevolucion(version: Pick<VersionDocumentoDto, 'motivoDevolucion' | 'detalleDevolucion'>, t: TFunction): string {
  switch (version.motivoDevolucion) {
    case 'pdf-con-contenido-activo':
      return t('boveda.devolucion.pdf-con-contenido-activo');
    case 'demasiadas-paginas':
      // Con un tipo que limita sus páginas, el detalle de la API dice el límite del tipo y cuántas trae el PDF.
      return version.detalleDevolucion ?? t('boveda.devolucion.demasiadas-paginas', { max: String(MAXIMO_PAGINAS) });
    default:
      return version.detalleDevolucion ?? t('boveda.devolucion.otro');
  }
}

/**
 * Qué tiene cada empresa de este documento: la versión vigente, una anterior o nada (no la exige o no aceptó), y hasta
 * cuándo le sirve («Vence el 28/09/2026»): cada empresa calcula su propia vigencia (TD-6).
 */
export function detalleComparticion(c: ComparticionPublicaDto, t: TFunction): string {
  if (!c.compartido || c.version === null) return t('boveda.noCompartida');
  const tiene = c.versionActualCompartida
    ? t('boveda.compartidaActual', { fecha: c.compartidaEn ? formatearFecha(c.compartidaEn) : '—' })
    : t('boveda.compartidaOtra', { n: String(c.version) });
  const vence = c.vigencia ? textoVigencia(c.vigencia, t) : null;
  return vence ? `${tiene} ${vence}.` : tiene;
}

/** Se puede retirar la vigente o una superada (BO-8); devueltas y retiradas no. */
export function sePuedeRetirar(version: Pick<VersionDocumentoDto, 'estado'>): boolean {
  return version.estado === 'Actual' || version.estado === 'Superada';
}

/**
 * Se puede descargar lo que quedó en la bóveda (no lo devuelto, que está en cuarentena, ni lo retirado). Un formulario
 * no tiene archivo (la API respondería 409 `version-sin-archivo`).
 */
export function sePuedeDescargar(version: Pick<VersionDocumentoDto, 'estado'> & Partial<Pick<VersionDocumentoDto, 'clase'>>): boolean {
  return (version.estado === 'Actual' || version.estado === 'Superada') && version.clase !== 'Formulario';
}

/**
 * Se sube una versión nueva desde la bóveda. Un formulario se actualiza desde la revisión de la empresa, que sabe qué
 * campos pide. La API marca la clase en la versión (el documento llega como `Archivo`).
 */
export function sePuedeSubirVersion(documento: Pick<DocumentoBovedaDto, 'clase' | 'versionActual'>): boolean {
  return documento.clase !== 'Formulario' && documento.versionActual?.clase !== 'Formulario';
}

/** Enlace para subir la versión nueva de un documento de la bóveda. */
export function enlaceSubirVersion(documento: Pick<DocumentoBovedaDto, 'id' | 'tipo'>): string {
  return `/boveda/subir?documento=${documento.id}&tipo=${encodeURIComponent(documento.tipo.codigo)}`;
}
