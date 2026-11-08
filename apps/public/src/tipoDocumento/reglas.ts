import type { ReglasArchivoDto, TipoDocumentoRequisitoDto } from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { formatearTamano, REGLAS_GLOBALES, TIPOS_PERMITIDOS, type ReglasArchivo } from '../boveda/subida';

/** Cómo se entrega un tipo de documento (TD-4): solo archivo, solo campos, o archivo y campos en la misma versión. */
export type ModoEntrega = 'Archivo' | 'Formulario' | 'ArchivoYFormulario';

/** Sin `modo` (API anterior o tipo sin configurar) se entrega un archivo, como siempre. */
export function modoDe(tipo: Pick<TipoDocumentoRequisitoDto, 'modo'> | null | undefined): ModoEntrega {
  return tipo?.modo === 'Formulario' || tipo?.modo === 'ArchivoYFormulario' ? tipo.modo : 'Archivo';
}

const ETIQUETAS: Readonly<Record<string, string>> = { 'application/pdf': 'PDF', 'image/jpeg': 'JPG', 'image/png': 'PNG', 'image/webp': 'WebP' };
const EXTENSIONES: Readonly<Record<string, string>> = { 'application/pdf': '.pdf', 'image/jpeg': '.jpg,.jpeg', 'image/png': '.png', 'image/webp': '.webp' };

/**
 * Reglas efectivas del tipo (`ReglasArchivoDto`, ya lo más estricto entre global, canónico y empresa). Solo se toman
 * formatos de la lista blanca de la bóveda; sin reglas, o con valores vacíos, quedan las globales.
 */
export function reglasDe(archivo: ReglasArchivoDto | null | undefined): ReglasArchivo {
  if (!archivo) return REGLAS_GLOBALES;
  const pedidos = new Set(archivo.formatos.map((f) => f.trim().toLowerCase()));
  const formatos = TIPOS_PERMITIDOS.filter((f) => pedidos.has(f));
  return {
    formatos: formatos.length > 0 ? formatos : REGLAS_GLOBALES.formatos,
    tamanoMaximoBytes: archivo.tamanoMaximoBytes > 0 ? archivo.tamanoMaximoBytes : REGLAS_GLOBALES.tamanoMaximoBytes,
    paginasMaximas: archivo.paginasMaximas > 0 ? archivo.paginasMaximas : REGLAS_GLOBALES.paginasMaximas,
  };
}

/** El tipo restringe algo frente a la bóveda: entonces el `detail` de un 413/415 nombra el tipo y su límite. */
export function sonPropias(reglas: ReglasArchivo): boolean {
  return (
    reglas.formatos.length < REGLAS_GLOBALES.formatos.length ||
    reglas.tamanoMaximoBytes < REGLAS_GLOBALES.tamanoMaximoBytes ||
    reglas.paginasMaximas < REGLAS_GLOBALES.paginasMaximas
  );
}

/** `accept` del selector de archivos: los MIME y sus extensiones (algunos sistemas filtran solo por extensión). */
export function aceptaDe(reglas: ReglasArchivo): string {
  return reglas.formatos.flatMap((f) => [f, EXTENSIONES[f] ?? '']).filter(Boolean).join(',');
}

/** `accept` de «Tomar foto»: solo las imágenes admitidas; `null` si el tipo no admite fotos (p. ej. solo PDF). */
export function aceptaFotos(reglas: ReglasArchivo): string | null {
  const imagenes = reglas.formatos.filter((f) => f.startsWith('image/'));
  return imagenes.length > 0 ? imagenes.join(',') : null;
}

/** «PDF», «PDF o JPG», «PDF, JPG, PNG o WebP». */
export function enumerarFormatos(formatos: readonly string[]): string {
  return new Intl.ListFormat('es-CO', { type: 'disjunction' }).format(formatos.map((f) => ETIQUETAS[f] ?? f));
}

/** Las reglas en palabras del tercero: «Solo PDF de hasta 5 MB y 10 páginas». Las páginas solo cuentan en un PDF. */
export function textoReglas(reglas: ReglasArchivo, t: TFunction): string {
  const formatos = reglas.formatos.length === 1 ? t('reglas.solo', { formato: enumerarFormatos(reglas.formatos) }) : enumerarFormatos(reglas.formatos);
  const tamano = formatearTamano(reglas.tamanoMaximoBytes);
  return reglas.formatos.includes('application/pdf')
    ? t('reglas.conPaginas', { formatos, tamano, count: reglas.paginasMaximas })
    : t('reglas.sinPaginas', { formatos, tamano });
}
