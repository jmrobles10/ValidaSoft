import type { BorradorFlujo, ErrorFlujo } from './editor';

// Vigencia periódica del vínculo y plazo de revalidación en el editor de flujos (VV-1, RV-4).

/** Vigencia periódica del vínculo por omisión (VV-1): sin vencimiento periódico; avisos a 30, 15 y 5 días, sin gracia. */
export const VIGENCIA_POR_DEFECTO = { dias: 0, avisos: [30, 15, 5], gracia: 0 } as const;
export const MAXIMO_VIGENCIA_VINCULO_DIAS = 3650;
export const MAXIMO_AVISOS_VIGENCIA = 5;
export const MAXIMO_GRACIA_VIGENCIA_DIAS = 365;
export const MAXIMO_DIAS_REVALIDACION = 365;

const aEntero = (texto: string): number => (texto.trim() === '' ? 0 : Number(texto.trim()));

/** Los avisos como los escribe la persona («30, 15 5»): números separados por coma o espacio, sin repetir, de mayor a menor. */
export function avisosDeVigencia(texto: string): number[] {
  const numeros = texto
    .split(/[\s,;]+/)
    .filter((a) => a !== '')
    .map(Number);
  return [...new Set(numeros)].sort((a, b) => b - a);
}

/**
 * La vigencia del editor como la recibe la API: días (0 = sin vencimiento periódico), avisos y gracia. Lo que no sea un número queda
 * como `NaN` y la validación del editor lo marca; la API valida los rangos (`vigencia-vinculo-invalida`, `aviso-vigencia-invalido`…).
 */
/** La vigencia lista para la API (`VigenciaVinculoDto`, con todos sus campos). */
export interface VigenciaParaApi {
  dias: number;
  diasAviso: number[];
  diasGracia: number;
}

export function vigenciaADto(borrador: Pick<BorradorFlujo, 'vigenciaDias' | 'vigenciaAvisos' | 'vigenciaGracia'>): VigenciaParaApi {
  return { dias: aEntero(borrador.vigenciaDias), diasAviso: avisosDeVigencia(borrador.vigenciaAvisos), diasGracia: aEntero(borrador.vigenciaGracia) };
}

/**
 * Replica `VigenciaDelVinculo.Validar` y la regla `vigencia-sin-secciones` del dominio (VV-1, VV-2): 0–3650 días, hasta 5 avisos entre
 * 1 y `días - 1` (con vigencia; sin ella, hasta 365), gracia de 0 a 365 y, con vigencia, al menos una sección de la ficha entre los
 * requisitos (es lo que el tercero ratifica al vencer).
 */
export function validarVigenciaDelVinculo(borrador: Pick<BorradorFlujo, 'vigenciaDias' | 'vigenciaAvisos' | 'vigenciaGracia' | 'requisitos'>): ErrorFlujo[] {
  const errores: ErrorFlujo[] = [];
  const { dias, diasAviso, diasGracia } = vigenciaADto(borrador);
  if (!Number.isInteger(dias) || dias < 0 || dias > MAXIMO_VIGENCIA_VINCULO_DIAS) errores.push({ codigo: 'vigencia-vinculo-invalida', campo: 'vigencia.dias' });
  if (!Number.isInteger(diasGracia) || diasGracia < 0 || diasGracia > MAXIMO_GRACIA_VIGENCIA_DIAS) errores.push({ codigo: 'gracia-vigencia-invalida', campo: 'vigencia.gracia' });
  const tope = dias > 0 ? dias - 1 : MAXIMO_GRACIA_VIGENCIA_DIAS;
  if (diasAviso.length > MAXIMO_AVISOS_VIGENCIA || diasAviso.some((a) => !Number.isInteger(a) || a < 1 || a > tope)) {
    errores.push({ codigo: 'aviso-vigencia-invalido', campo: 'vigencia.avisos' });
  }
  if (dias > 0 && !borrador.requisitos.some((r) => r.seccion !== null)) errores.push({ codigo: 'vigencia-sin-secciones', campo: 'vigencia.dias' });
  return errores;
}
