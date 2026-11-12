import type { FijarTarifaDeIaDto } from '@validasoft/api-client';

/**
 * Consumo de IA por cliente (PI-12): periodos, formato de cifras y la tarifa del adicional. Solo lógica pura; lo que se ve está en `PaginaConsumoIa`.
 */

export const MESES_ATRAS = 12;
export const MONEDA_POR_DEFECTO = 'COP';
export const PRECIO_MAXIMO = 100_000_000;

const ZONA = 'America/Bogota';

/** El mes calendario (`AAAA-MM`) de una fecha en la zona de la operación (Bogotá), que es la del corte del consumo. */
export function periodoDe(fecha: Date): string {
  const partes = new Intl.DateTimeFormat('en-CA', { timeZone: ZONA, year: 'numeric', month: '2-digit' }).formatToParts(fecha);
  const anio = partes.find((p) => p.type === 'year')?.value ?? '1970';
  const mes = partes.find((p) => p.type === 'month')?.value ?? '01';
  return `${anio}-${mes}`;
}

/** Los últimos `cantidad` meses, del actual hacia atrás, para elegir el periodo. */
export function periodosRecientes(ahora: Date, cantidad = MESES_ATRAS): string[] {
  const [anio, mes] = periodoDe(ahora).split('-').map(Number) as [number, number];
  return Array.from({ length: cantidad }, (_, i) => {
    const total = anio * 12 + (mes - 1) - i;
    return `${String(Math.floor(total / 12))}-${String((total % 12) + 1).padStart(2, '0')}`;
  });
}

/** «noviembre de 2026». */
export function etiquetaDePeriodo(periodo: string): string {
  const [anio, mes] = periodo.split('-').map(Number) as [number, number];
  const texto = new Intl.DateTimeFormat('es-CO', { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(Date.UTC(anio, mes - 1, 1)));
  return texto;
}

/** Las cifras decimales de la API llegan como número o como texto. */
export function aNumero(valor: number | string | null | undefined): number {
  if (valor === null || valor === undefined) return 0;
  const n = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(n) ? n : 0;
}

export function formatearEntero(valor: number | string): string {
  return new Intl.NumberFormat('es-CO', { maximumFractionDigits: 0 }).format(aNumero(valor));
}

/** Tokens en miles o millones: «1,2 M». */
export function formatearTokens(valor: number | string): string {
  const n = aNumero(valor);
  if (n >= 1_000_000) return `${new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 }).format(n / 1_000_000)} M`;
  if (n >= 10_000) return `${new Intl.NumberFormat('es-CO', { maximumFractionDigits: 1 }).format(n / 1_000)} mil`;
  return formatearEntero(n);
}

/** Dólares con dos a cuatro decimales (cada llamada cuesta fracciones de centavo). */
export function formatearUsd(valor: number | string): string {
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 4 }).format(aNumero(valor));
}

export function formatearMoneda(valor: number | string, moneda: string): string {
  const codigo = /^[A-Z]{3}$/.test(moneda) ? moneda : MONEDA_POR_DEFECTO;
  return new Intl.NumberFormat('es-CO', { style: 'currency', currency: codigo, maximumFractionDigits: codigo === 'COP' ? 0 : 2 }).format(aNumero(valor));
}

/** Un precio escrito («1500», «1.500,50» no: solo dígitos y un decimal con punto o coma) como número; `null` si no lo es. */
export function leerPrecio(texto: string): number | null {
  const limpio = texto.trim().replace(',', '.');
  if (!/^\d+(\.\d{1,2})?$/.test(limpio)) return null;
  const n = Number(limpio);
  return n <= PRECIO_MAXIMO ? n : null;
}

export type ErrorTarifa = 'precio-invalido' | 'moneda-invalida';

export function validarTarifa(precio: string, moneda: string): ErrorTarifa[] {
  const errores: ErrorTarifa[] = [];
  if (leerPrecio(precio) === null) errores.push('precio-invalido');
  if (!/^[A-Za-z]{3}$/.test(moneda.trim())) errores.push('moneda-invalida');
  return errores;
}

export function cuerpoDeTarifa(precio: string, moneda: string): FijarTarifaDeIaDto {
  return { precioPorValidacionAdicional: leerPrecio(precio) ?? 0, moneda: moneda.trim().toUpperCase() || MONEDA_POR_DEFECTO };
}

/**
 * El precio propio de una empresa en su plan de IA: vacío = usa la tarifa general. Distinto de cero y de la tarifa es un precio propio (el
 * servidor lo guarda tal cual); `undefined` si lo escrito no es un precio.
 */
export function precioDePlan(texto: string): number | null | undefined {
  if (texto.trim() === '') return null;
  return leerPrecio(texto) ?? undefined;
}
