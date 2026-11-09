import type { IndicadoresDto, MisPendientesDto, PendientesDeAreaDto } from '@validasoft/api-client';
import { LOCALE } from '@validasoft/ui';
import type { TFunction } from 'i18next';

/** Ventanas del selector de indicadores, en días. */
export const VENTANAS = [7, 30, 90] as const;
export type Ventana = (typeof VENTANAS)[number];
export const VENTANA_POR_DEFECTO: Ventana = 30;

function esVentana(valor: number): valor is Ventana {
  return (VENTANAS as readonly number[]).includes(valor);
}

/** Ventana de la URL (`?dias=`); lo que no sea una opción válida vuelve a 30. */
export function leerVentana(params: URLSearchParams): Ventana {
  const dias = Number(params.get('dias') ?? VENTANA_POR_DEFECTO);
  return esVentana(dias) ? dias : VENTANA_POR_DEFECTO;
}

/** «Compras: 3 expedientes por revisar · el más antiguo hace 5 días», o «Compras: Al día». */
export function textoArea(area: PendientesDeAreaDto, t: TFunction): string {
  if (area.cantidad === 0) return t('inicio.pendientes.area', { nombre: area.nombre, detalle: t('inicio.pendientes.alDia') });
  const antiguo = area.masAntiguoDias === 0 ? t('inicio.pendientes.antiguoHoy') : t('inicio.pendientes.antiguo', { count: area.masAntiguoDias });
  return t('inicio.pendientes.area', { nombre: area.nombre, detalle: `${t('inicio.pendientes.porRevisar', { count: area.cantidad })} · ${antiguo}` });
}

export type ClaveAcceso = 'revalidaciones' | 'cuentas' | 'aperturas' | 'porVencer' | 'vencidos';

/** Un pendiente que no es de un área: cuántos son y a qué pantalla lleva (`null` si aún no tiene una propia). */
export interface Acceso {
  clave: ClaveAcceso;
  cantidad: number;
  ruta: string | null;
  /** Revalidaciones cuyo plazo ya venció y bloquean el certificado. */
  bloqueantes?: number;
}

/** Los accesos con algo pendiente, en el orden en que se muestran. */
export function accesosPendientes(p: MisPendientesDto): Acceso[] {
  const todos: Acceso[] = [
    { clave: 'revalidaciones', cantidad: p.revalidaciones, ruta: 'revalidaciones', bloqueantes: p.revalidacionesBloqueantes },
    // Las cuentas se aprueban en el detalle del vínculo y la lista de vínculos no filtra por cuenta: sin enlace.
    { clave: 'cuentas', cantidad: p.cuentasPorAprobar, ruta: null },
    // La bandeja tiene el botón de aperturas pendientes.
    { clave: 'aperturas', cantidad: p.aperturasPendientes, ruta: 'bandeja' },
    { clave: 'porVencer', cantidad: p.documentosPorVencer, ruta: 'vencimientos' },
    { clave: 'vencidos', cantidad: p.documentosVencidos, ruta: 'vencimientos?estado=Vencida' },
  ];
  return todos.filter((a) => a.cantidad > 0);
}

/** Nada por revisar en las áreas ni en los demás pendientes. */
export function estaAlDia(p: MisPendientesDto): boolean {
  return p.porArea.every((a) => a.cantidad === 0) && accesosPendientes(p).length === 0;
}

/** El contrato admite el tiempo como número o texto (decimal de .NET); `null` si no hay dato o no es un número. */
export function aNumero(valor: IndicadoresDto['tiempoPromedioDias']): number | null {
  if (valor === null) return null;
  const numero = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(numero) ? numero : null;
}

const ENTERO = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 });
const UN_DECIMAL = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** Conteos con separador de miles de es-CO. */
export function formatearConteo(valor: number): string {
  return ENTERO.format(valor);
}

/** Días con un decimal («4,5»), o `sinDato` («—») si no hay expedientes aprobados en la ventana. */
export function formatearDias(valor: IndicadoresDto['tiempoPromedioDias'], sinDato: string): string {
  const numero = aNumero(valor);
  return numero === null ? sinDato : UN_DECIMAL.format(numero);
}
