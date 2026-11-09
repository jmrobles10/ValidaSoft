import type { UsoIaDiaDto, UsoIaDto } from '@validasoft/api-client';
import { claveDia, LOCALE, ZONA_HORARIA_POR_DEFECTO } from '@validasoft/ui';

/** Meses que ofrece el selector: el actual y los 11 anteriores. */
export const MESES_SELECTOR = 12;
/** Desde aquí la barra de las incluidas pasa a ámbar (el backend avisa al 80 % y al 100 %, IA-4). */
export const UMBRAL_ALERTA = 80;

const MES = /^\d{4}-(0[1-9]|1[0-2])$/;

/** Mes calendario `aaaa-mm` de hoy en la zona dada (la del tenant; Bogotá por omisión). */
export function mesActual(ahora: Date = new Date(), zona = ZONA_HORARIA_POR_DEFECTO): string {
  return claveDia(ahora, zona).slice(0, 7);
}

/** `2026-11` → `[2026, 11]`. */
function partesMes(mes: string): [number, number] {
  return [Number(mes.slice(0, 4)), Number(mes.slice(5, 7))];
}

/** El mes y los `cantidad - 1` anteriores, del más reciente al más antiguo. */
export function ultimosMeses(actual: string, cantidad = MESES_SELECTOR): string[] {
  const [anio, mes] = partesMes(actual);
  return Array.from({ length: cantidad }, (_, i) => {
    const fecha = new Date(Date.UTC(anio, mes - 1 - i, 1));
    return `${String(fecha.getUTCFullYear())}-${String(fecha.getUTCMonth() + 1).padStart(2, '0')}`;
  });
}

/** Mes de la URL (`?mes=`) si es uno de los que ofrece el selector; si no, `null` (el actual). */
export function leerMes(params: URLSearchParams, opciones: readonly string[]): string | null {
  const mes = params.get('mes');
  return mes && MES.test(mes) && opciones.includes(mes) ? mes : null;
}

/** `2026-11` → «Noviembre de 2026». */
export function etiquetaMes(mes: string): string {
  const texto = new Intl.DateTimeFormat(LOCALE, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(new Date(`${mes}-01T00:00:00Z`));
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

/** El contrato admite los decimales de .NET como número o texto. */
export function aNumero(valor: number | string): number {
  const numero = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(numero) ? numero : 0;
}

/** Porcentaje usado de las incluidas (sin incluidas, cualquier consumo ya es el 100 %). */
export function porcentajeUsado(usadas: number, incluidas: number): number {
  if (incluidas <= 0) return usadas > 0 ? 100 : 0;
  return (usadas / incluidas) * 100;
}

export type NivelUso = 'normal' | 'alerta' | 'agotado';

export function nivelDeUso(porcentaje: number): NivelUso {
  if (porcentaje >= 100) return 'agotado';
  return porcentaje >= UMBRAL_ALERTA ? 'alerta' : 'normal';
}

const ENTERO = new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 0 });
const PORCENTAJE = new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 0 });
const USD_CENTAVOS = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 2 });
const USD_FINO = new Intl.NumberFormat(LOCALE, { style: 'currency', currency: 'USD', minimumFractionDigits: 4, maximumFractionDigits: 4 });

export function formatearConteo(valor: number): string {
  return ENTERO.format(valor);
}

/** `92` → «92 %» (es-CO). */
export function formatearPorcentaje(porcentaje: number): string {
  return PORCENTAJE.format(porcentaje / 100);
}

/** Dólares en es-CO; bajo 1 USD con 4 decimales para que no se vea «US$ 0,00». */
export function formatearUsd(valor: number | string): string {
  const numero = aNumero(valor);
  return Math.abs(numero) < 1 ? USD_FINO.format(numero) : USD_CENTAVOS.format(numero);
}

export interface DiaDeUso {
  dia: number;
  incluidas: number;
  adicionales: number;
}

/** Todos los días del mes con lo consumido (la API solo trae los días con uso). */
export function serieDelMes(mes: string, porDia: readonly UsoIaDiaDto[]): DiaDeUso[] {
  const [anio, numeroMes] = partesMes(mes);
  const dias = new Date(Date.UTC(anio, numeroMes, 0)).getUTCDate();
  const porFecha = new Map(porDia.map((d) => [d.fecha.slice(0, 10), d]));
  return Array.from({ length: dias }, (_, i) => {
    const dato = porFecha.get(`${mes}-${String(i + 1).padStart(2, '0')}`);
    return { dia: i + 1, incluidas: dato?.incluidas ?? 0, adicionales: dato?.adicionales ?? 0 };
  });
}

/** Sin plan configurado o con el plan inactivo la revisión automática no está contratada. */
export function tienePlanActivo(uso: UsoIaDto): boolean {
  return uso.plan.configurado && uso.plan.activo;
}
