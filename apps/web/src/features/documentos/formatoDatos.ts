import type { DatoDeclaradoDto } from '@validasoft/api-client';
import { formatearDiaCivil, formatearFecha, LOCALE } from '@validasoft/ui';

/** Textos que el formato necesita y dependen del idioma. */
export interface TextosDatoDeclarado {
  si: string;
  no: string;
}

const SOLO_FECHA = /^\d{4}-\d{2}-\d{2}$/;
const NUMERO = /^-?\d+(\.\d+)?$/;
const formatoNumero = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 10 });

/**
 * Valor declarado listo para leer, según su `tipo` (TD-4). La API entrega los valores normalizados: número invariante
 * (`1234.5`), fecha `aaaa-mm-dd` y `true`/`false`. Una fecha sin hora es un día calendario: se formatea en UTC para que
 * la zona del tenant no la corra al día anterior. Lo que no tenga el formato esperado se muestra tal cual.
 */
export function formatearDatoDeclarado(dato: Pick<DatoDeclaradoDto, 'tipo' | 'valor'>, textos: TextosDatoDeclarado): string {
  const valor = dato.valor.trim();
  if (!valor) return '—';
  switch (dato.tipo) {
    case 'Fecha':
      return formatearFechaDeclarada(valor);
    case 'SiNo':
      return valor === 'true' ? textos.si : valor === 'false' ? textos.no : valor;
    case 'Numero':
      return NUMERO.test(valor) ? formatoNumero.format(Number(valor)) : valor;
    default:
      return valor;
  }
}

/** `2026-11-30` → `30/11/2026` (día calendario, sin correrlo por zona); un ISO con hora usa la zona del tenant. */
export function formatearFechaDeclarada(valor: string): string {
  if (SOLO_FECHA.test(valor)) return formatearDiaCivil(valor);
  return Number.isNaN(Date.parse(valor)) ? valor : formatearFecha(valor);
}

/** Lo mínimo que se mira de una versión para saber si trae archivo. */
export interface ClaseDeVersion {
  clase?: string | null;
}

/**
 * Versión sin archivo: solo formulario (`clase: Formulario`, o un tipo en modo `Formulario` cuando la versión no dice
 * su clase). Descargarla responde 409 `version-sin-archivo`, así que no se ofrece «Ver» ni se abre el visor de archivo.
 */
export function esVersionSinArchivo(version: ClaseDeVersion, modoDelTipo?: string | null): boolean {
  if (version.clase) return version.clase === 'Formulario';
  return modoDelTipo === 'Formulario';
}

/** Los datos declarados de una versión (vacío si no trae). */
export function datosDe(version: { datosDeclarados?: DatoDeclaradoDto[] | null } | null | undefined): DatoDeclaradoDto[] {
  return version?.datosDeclarados ?? [];
}
