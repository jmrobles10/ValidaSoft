import { esErrorApi, type CondicionDto, type SeleccionDeFlujoDto } from '@validasoft/api-client';
import type { BorradorFlujo, ErrorFlujo } from './editor';

// Selección automática del flujo en el editor (FR-1): condición y prioridad con las que el sistema escoge este flujo solo.

/** Prioridad más alta que admite un flujo; la 1 es la primera en evaluarse (`SeleccionDeFlujo.PrioridadMaxima`). */
export const PRIORIDAD_MAXIMA = 999;

type CamposSeleccion = Pick<BorradorFlujo, 'seleccionCondicion' | 'seleccionPrioridad'>;

/** Lleva la selección de una versión al editor; una versión guardada antes de esta opción no se escoge sola. */
export function seleccionDesdeVersion(seleccion: SeleccionDeFlujoDto | null | undefined): CamposSeleccion {
  return {
    seleccionCondicion: seleccion?.condicion ?? null,
    seleccionPrioridad: seleccion?.prioridad == null ? '' : String(seleccion.prioridad),
  };
}

/** Prioridad escrita como número entero; `null` si está vacía o no es un número. */
function prioridadDe(texto: string): number | null {
  const prioridad = Number.parseInt(texto.trim(), 10);
  return Number.isFinite(prioridad) ? prioridad : null;
}

/**
 * La selección del editor como la recibe la API: sin condición no hay prioridad (el flujo solo se usa de forma explícita o por
 * defecto) y el objeto vacío le dice a la API que la quite al editar. La API valida el resto.
 */
export function seleccionADto(borrador: CamposSeleccion): SeleccionDeFlujoDto {
  if (borrador.seleccionCondicion === null) return { condicion: null, prioridad: null };
  return { condicion: borrador.seleccionCondicion, prioridad: prioridadDe(borrador.seleccionPrioridad) };
}

/** Replica `SeleccionDeFlujo.Validar`: con condición, la prioridad es un entero de 1 a 999. */
export function validarSeleccion(borrador: CamposSeleccion): ErrorFlujo[] {
  if (borrador.seleccionCondicion === null) return [];
  const campo = 'seleccion.prioridad';
  if (borrador.seleccionPrioridad.trim() === '') return [{ codigo: 'prioridad-requerida', campo }];
  const prioridad = prioridadDe(borrador.seleccionPrioridad);
  return prioridad === null || prioridad < 1 || prioridad > PRIORIDAD_MAXIMA ? [{ codigo: 'prioridad-invalida', campo }] : [];
}

const CODIGOS_DE_SELECCION = ['prioridad-requerida', 'prioridad-invalida', 'prioridad-duplicada', 'seleccion-sin-condicion', 'condicion-invalida'] as const;
type CodigoDeSeleccion = (typeof CODIGOS_DE_SELECCION)[number];

const esCodigoDeSeleccion = (codigo: string): codigo is CodigoDeSeleccion => (CODIGOS_DE_SELECCION as readonly string[]).includes(codigo);

/**
 * Lleva un 422 de la API a la sección de selección para mostrarlo en su campo: la prioridad repetida dentro del tipo de vínculo
 * (`prioridad-duplicada`, que solo la API conoce) o una condición inválida que no es de un requisito (esa trae su posición en la
 * extensión `requisito`). `null` si el error es de otra cosa.
 */
export function errorDeApiEnSeleccion(error: unknown): ErrorFlujo | null {
  if (!esErrorApi(error) || !error.esValidacion || !esCodigoDeSeleccion(error.codigo)) return null;
  if (error.codigo === 'condicion-invalida' && typeof error.extensiones.requisito === 'number') return null;
  return {
    codigo: error.codigo === 'seleccion-sin-condicion' ? 'prioridad-requerida' : error.codigo,
    campo: error.codigo === 'condicion-invalida' || error.codigo === 'seleccion-sin-condicion' ? 'seleccion.condicion' : 'seleccion.prioridad',
  };
}

/** `true` si el flujo se escoge solo (tiene condición de selección). */
export function seSeleccionaSolo(seleccion: SeleccionDeFlujoDto | null | undefined): seleccion is SeleccionDeFlujoDto & { condicion: CondicionDto } {
  return Boolean(seleccion?.condicion);
}
