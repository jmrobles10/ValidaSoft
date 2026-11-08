import { esErrorApi } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { mensajeDeError } from '../shared/errores';
import { enumerarFormatos, sonPropias } from '../tipoDocumento/reglas';
import { formatearTamano, REGLAS_GLOBALES, type FalloSubida, type ProblemaArchivo, type ReglasArchivo } from './subida';

/** Lo que el navegador encontró antes de subir, con las reglas del tipo («El máximo es 5 MB», «Sube un archivo PDF»). */
export function mensajeProblema(problema: ProblemaArchivo, t: TFunction): string {
  switch (problema.tipo) {
    case 'vacio':
      return t('subir.problemas.vacio');
    case 'demasiado-grande':
      return t('subir.problemas.demasiado-grande', { tamano: formatearTamano(problema.tamano), maximo: formatearTamano(problema.maximo) });
    case 'formato':
      return t('subir.problemas.formato', { formatos: enumerarFormatos(problema.formatos) });
  }
}

/**
 * Lo que respondió la API o el almacenamiento. Un 413/415 de un tipo que restringe algo trae en `detail` su descripción
 * («Certificado Norte admite solo PDF de hasta 1 MB.»): se muestra tal cual con el siguiente paso. Si no, los textos
 * propios con las reglas que aplican.
 */
export function mensajeFallo(fallo: FalloSubida, error: unknown, t: TFunction, reglas: ReglasArchivo = REGLAS_GLOBALES): string {
  switch (fallo) {
    case 'otro':
      return mensajeDeError(error, t);
    case 'formato':
    case 'demasiado-grande':
      if (sonPropias(reglas) && esErrorApi(error) && error.detalle) return t('subir.fallos.delTipo', { detalle: error.detalle });
      return fallo === 'formato'
        ? t('subir.fallos.formato', { formatos: enumerarFormatos(reglas.formatos) })
        : t('subir.fallos.demasiado-grande', { maximo: formatearTamano(reglas.tamanoMaximoBytes) });
    default:
      return t(`subir.fallos.${fallo}`);
  }
}
