const LOCALE = 'es-CO';

const formatoCifra = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 4 });
const formatoPesos = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'COP', maximumFractionDigits: 2 });

/** «1.500.000,5»: como se escribe una cifra en Colombia (y como se vuelve a leer con `leerCifra`). */
export const escribirCifra = (valor: number | null | undefined) => (valor === null || valor === undefined ? '' : formatoCifra.format(valor));

export const formatearPesos = (valor: number) => formatoPesos.format(valor);

const MILES_CON_PUNTO = /^-?\d{1,3}(\.\d{3})+$/;

/**
 * Lee lo que escribe el tercero: «1.500.000», «1500000,50», «1500000.50» o «$ 1.500.000». Con coma, los puntos son de
 * miles; sin coma, un punto seguido de grupos de tres dígitos también es de miles. Vacío ⇒ `null`; ilegible ⇒ `undefined`.
 */
export function leerCifra(texto: string): number | null | undefined {
  const limpio = texto.replace(/[\s$]/g, '');
  if (!limpio) return null;
  let normal: string;
  if (limpio.includes(',')) normal = limpio.replace(/\./g, '').replace(',', '.');
  else if (MILES_CON_PUNTO.test(limpio)) normal = limpio.replace(/\./g, '');
  else normal = limpio;
  if (!/^-?\d+(\.\d+)?$/.test(normal)) return undefined;
  return Number(normal);
}

/**
 * Cuadre informativo de la financiera (no decide la completitud, que es del servidor): diferencia entre activos y
 * pasivos + patrimonio y si cabe en la tolerancia del 0,5 % de los activos que usa la regla del dominio.
 */
export function cuadre(activos: number | null | undefined, pasivos: number | null | undefined, patrimonio: number | null | undefined) {
  if (activos === null || activos === undefined || pasivos === null || pasivos === undefined || patrimonio === null || patrimonio === undefined) return null;
  const suma = pasivos + patrimonio;
  const diferencia = activos - suma;
  return { suma, diferencia, cuadra: Math.abs(diferencia) <= Math.abs(activos) * 0.005 };
}
