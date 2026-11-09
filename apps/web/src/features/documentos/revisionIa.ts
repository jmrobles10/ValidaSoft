import type { HechosDto, HechosIaDto } from '@validasoft/api-client';
import { LOCALE } from '@validasoft/ui';
import { formatearFechaDeclarada } from './formatoDatos';

/**
 * Umbral de confianza por omisión del plan de IA (IA-8: 0,85). El panel no conoce el umbral del plan de la empresa:
 * bajo este valor la confianza se pinta en ámbar.
 */
export const UMBRAL_CONFIANZA = 0.85;

/** Textos que la interpretación necesita y dependen del idioma. */
export interface TextosCamposIa {
  /** Nombres conocidos de los campos (`razonSocial` → «Razón social»). */
  nombres: Readonly<Record<string, string>>;
  si: string;
  no: string;
  vacio: string;
}

/** Valor extraído listo para mostrar: texto, lista (p. ej. representantes legales) u objeto con sus pares. */
export type ValorIa =
  | { tipo: 'texto'; texto: string }
  | { tipo: 'lista'; elementos: ValorIa[] }
  | { tipo: 'objeto'; pares: CampoIa[] };

export interface CampoIa {
  clave: string;
  etiqueta: string;
  valor: ValorIa;
}

const SOLO_FECHA = /^\d{4}-\d{2}-\d{2}$/;
const NUMERO = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 10 });

/** `fechaNacimiento` → «Fecha nacimiento»: separa camelCase (y los números) y deja solo la primera en mayúscula. */
export function separarCamelCase(clave: string): string {
  const texto = clave
    .replace(/[_-]+/g, ' ')
    .replace(/([a-záéíóúñ])([A-ZÁÉÍÓÚÑ0-9])/g, '$1 $2')
    .replace(/([0-9])([A-Za-z])/g, '$1 $2')
    .trim()
    .toLowerCase();
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

export function etiquetaCampo(clave: string, nombres: Readonly<Record<string, string>>): string {
  return nombres[clave] ?? separarCamelCase(clave);
}

/** Interpreta un valor JSON extraído por la IA (texto, número, booleano, fecha `aaaa-mm-dd`, arreglo u objeto). */
export function interpretarValor(valor: unknown, textos: TextosCamposIa): ValorIa {
  if (valor === null || valor === undefined) return { tipo: 'texto', texto: textos.vacio };
  if (typeof valor === 'boolean') return { tipo: 'texto', texto: valor ? textos.si : textos.no };
  if (typeof valor === 'number') return { tipo: 'texto', texto: NUMERO.format(valor) };
  if (typeof valor === 'string') {
    const limpio = valor.trim();
    if (!limpio) return { tipo: 'texto', texto: textos.vacio };
    return { tipo: 'texto', texto: SOLO_FECHA.test(limpio) ? formatearFechaDeclarada(limpio) : limpio };
  }
  if (Array.isArray(valor)) {
    const elementos = valor.map((v) => interpretarValor(v, textos));
    return elementos.length === 0 ? { tipo: 'texto', texto: textos.vacio } : { tipo: 'lista', elementos };
  }
  if (typeof valor === 'object') return { tipo: 'objeto', pares: camposExtraidos(valor as Record<string, unknown>, textos) };
  // JSON no trae otra cosa; por si acaso, nada que mostrar.
  return { tipo: 'texto', texto: textos.vacio };
}

/** Los campos extraídos con su etiqueta legible, en el orden en que llegan. */
export function camposExtraidos(campos: Readonly<Record<string, unknown>>, textos: TextosCamposIa): CampoIa[] {
  return Object.entries(campos).map(([clave, valor]) => ({ clave, etiqueta: etiquetaCampo(clave, textos.nombres), valor: interpretarValor(valor, textos) }));
}

/** La confianza (decimal de .NET como número o texto) entre 0 y 1; `null` si no es un número. */
export function confianzaDe(ia: Pick<HechosIaDto, 'confianza'>): number | null {
  const numero = typeof ia.confianza === 'number' ? ia.confianza : Number(ia.confianza);
  return Number.isFinite(numero) ? Math.min(1, Math.max(0, numero)) : null;
}

/** El tipo detectado no coincide con el canónico que pide el requisito (sin canónico esperado no se compara). */
export function tipoNoCoincide(tipoDetectado: string, tipoEsperado: string | null | undefined): boolean {
  if (!tipoEsperado || !tipoDetectado) return false;
  return tipoDetectado.trim().toUpperCase() !== tipoEsperado.trim().toUpperCase();
}

export type EstadoRevision = 'conIa' | 'sinAnalisis' | 'nada';

/** Con análisis visible, sin análisis (revisión manual) o nada que decir (sin hechos todavía). */
export function estadoRevision(hechos: HechosDto | null | undefined): EstadoRevision {
  if (!hechos) return 'nada';
  if (hechos.ia) return 'conIa';
  return hechos.conIa ? 'nada' : 'sinAnalisis';
}
