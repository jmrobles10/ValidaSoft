import type { ObtenerInformeSlaParams } from '@validasoft/api-client';
import { LOCALE } from '@validasoft/ui';
import { aNumero } from './modelo';

/** Filtros del informe de cumplimiento del plazo de revisión en la URL (vacío = sin filtro). */
export interface FiltrosSla {
  areaId: string;
  tipoVinculoId: string;
  desde: string;
  hasta: string;
}

const CLAVES_SLA = ['areaId', 'tipoVinculoId', 'desde', 'hasta'] as const;

export function leerFiltrosSla(p: URLSearchParams): FiltrosSla {
  return { areaId: p.get('areaId') ?? '', tipoVinculoId: p.get('tipoVinculoId') ?? '', desde: p.get('desde') ?? '', hasta: p.get('hasta') ?? '' };
}

export function hayFiltrosSla(f: FiltrosSla): boolean {
  return CLAVES_SLA.some((c) => f[c] !== '');
}

/** Parámetros de la API: lo vacío no viaja. */
export function parametrosSla(f: FiltrosSla): ObtenerInformeSlaParams {
  const p: ObtenerInformeSlaParams = {};
  for (const clave of CLAVES_SLA) if (f[clave]) p[clave] = f[clave];
  return p;
}

const UN_DECIMAL = new Intl.NumberFormat(LOCALE, { minimumFractionDigits: 0, maximumFractionDigits: 1 });

/** «87,5 %», o `sinDato` si no hubo revisiones con plazo en el periodo. */
export function formatearCumplimiento(valor: number | string | null | undefined, sinDato: string): string {
  const numero = aNumero(valor);
  return numero === null ? sinDato : `${UN_DECIMAL.format(numero)} %`;
}

/** Cumplimiento como número 0–100 para la barra; sin dato es 0. */
export function cumplimientoParaBarra(valor: number | string | null | undefined): number {
  const numero = aNumero(valor);
  return numero === null ? 0 : Math.min(100, Math.max(0, numero));
}

/** Tono de la barra: verde con el 90 % o más, ámbar de 70 a 90 y rojo por debajo. */
export function tonoDeCumplimiento(valor: number | string | null | undefined): 'success' | 'warning' | 'error' | 'inherit' {
  const numero = aNumero(valor);
  if (numero === null) return 'inherit';
  if (numero >= 90) return 'success';
  return numero >= 70 ? 'warning' : 'error';
}

/** Un retraso en horas (el contrato las trae como número o texto): de 48 horas en adelante se cuenta en días. */
export function desgloseDeRetraso(horas: number | string | null | undefined): { dias: number; horas: number } {
  const total = Math.max(0, Math.round(aNumero(horas) ?? 0));
  return total < 48 ? { dias: 0, horas: total } : { dias: Math.floor(total / 24), horas: total % 24 };
}
