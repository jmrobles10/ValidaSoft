/**
 * Pestañas con «Más» adaptativo (patrón «priority+», docs/06 §3.3): se muestran, en su orden, las pestañas que caben
 * en el ancho disponible y el resto pasa al menú «Más». Aquí solo está la cuenta, pura y sin DOM: los anchos los mide
 * `useMedidasPestanas` una sola vez (en una fila oculta) y esta función decide cuántas caben, sin bucles de medición.
 */

/** Separación entre pestañas (px): la misma del `gap` de la barra. */
export const SEPARACION_PESTANAS = 2;
/** Holgura (px) que se descuenta del ancho disponible para que un redondeo de subpíxeles nunca desborde la fila. */
export const HOLGURA_PESTANAS = 1;
/** Lo que el botón «Más» suma a su etiqueta: el ícono (16 px) y su separación (4 px). */
export const EXTRA_BOTON_MAS = 20;

export interface MedidasPestanas {
  /** Ancho de cada pestaña, en el orden en que se muestran (con su relleno). */
  anchos: readonly number[];
  /** Ancho del botón «Más» con su etiqueta por omisión («Más» y el ícono). */
  anchoMas: number;
  /** Ancho que tiene la barra para las pestañas. */
  disponible: number;
}

export interface EntradaCuantasCaben extends MedidasPestanas {
  /** Índice de la pestaña activa (`null` si ninguna): si queda en «Más», el botón lleva su nombre y es más ancho. */
  indiceActiva: number | null;
  separacion?: number;
  holgura?: number;
}

/** Ancho que ocupan las `cantidad` primeras pestañas, con las separaciones entre ellas. */
function anchoDeLas(anchos: readonly number[], cantidad: number, separacion: number): number {
  let total = 0;
  for (let i = 0; i < cantidad; i++) total += (anchos[i] ?? 0) + (i > 0 ? separacion : 0);
  return total;
}

/** Cuántas pestañas caben a la izquierda reservando `anchoMas` para el botón «Más» (cero si ni una cabe). */
function cuantasConMas(anchos: readonly number[], anchoMas: number, disponible: number, separacion: number): number {
  let caben = 0;
  for (let i = 1; i <= anchos.length; i++) {
    if (anchoDeLas(anchos, i, separacion) + separacion + anchoMas > disponible) break;
    caben = i;
  }
  return caben;
}

/**
 * Cuántas de las pestañas se muestran como pestañas (el resto va en «Más»). Si caben todas, no hay «Más». Si no, se
 * reserva el botón; y si la pestaña activa quedó en «Más», el botón muestra su nombre y se reserva ese ancho, que solo
 * puede dejar fuera más pestañas (nunca devolver la activa a la fila): no hace falta iterar.
 */
export function cuantasPestanasCaben({ anchos, anchoMas, disponible, indiceActiva, separacion = SEPARACION_PESTANAS, holgura = HOLGURA_PESTANAS }: EntradaCuantasCaben): number {
  const total = anchos.length;
  const espacio = disponible - holgura;
  if (total === 0) return 0;
  if (anchoDeLas(anchos, total, separacion) <= espacio) return total;
  const caben = cuantasConMas(anchos, anchoMas, espacio, separacion);
  if (indiceActiva === null || indiceActiva < caben) return caben;
  // La activa va en «Más»: el botón lleva su nombre (su ancho de pestaña más el ícono).
  const anchoConNombre = Math.max(anchoMas, (anchos[indiceActiva] ?? 0) + EXTRA_BOTON_MAS);
  return cuantasConMas(anchos, anchoConNombre, espacio, separacion);
}
