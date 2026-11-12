import { limiteDelDia } from '../auditoria/modelo';
import type { FiltroDeIngresos } from './apiIdentidad';

/** Los eventos de ingreso que se pueden filtrar (`EventoDeIngreso` de la API), en el orden en que se ofrecen. */
export const EVENTOS_DE_INGRESO = [
  'IngresoExitoso',
  'IngresoFallido',
  'CodigoEnviado',
  'CodigoIncorrecto',
  'PasskeyRegistrada',
  'PasskeyRevocada',
  'PasskeyEliminada',
  'SesionCerrada',
  'SesionRevocada',
  'ReusoDeSesion',
  'ReautenticacionExitosa',
  'ReautenticacionFallida',
  'CodigosDeRecuperacionGenerados',
  'CodigoDeRecuperacionUsado',
] as const;

export type EventoDeIngreso = (typeof EVENTOS_DE_INGRESO)[number];

export type ResultadoDeIngreso = '' | 'exitoso' | 'fallido';

export interface FiltrosDeIngresos {
  operador: string;
  evento: EventoDeIngreso | '';
  resultado: ResultadoDeIngreso;
  /** Días calendario `AAAA-MM-DD` (los del campo de fecha), en la zona del navegador. */
  desde: string;
  hasta: string;
}

const PATRON_DIA = /^\d{4}-\d{2}-\d{2}$/;
const dia = (valor: string | null) => (valor && PATRON_DIA.test(valor) ? valor : '');

export function esEventoDeIngreso(valor: string | null): valor is EventoDeIngreso {
  return (EVENTOS_DE_INGRESO as readonly string[]).includes(valor ?? '');
}

/** Filtros de la URL (`?operador=&evento=&resultado=&desde=&hasta=`); lo inválido se ignora. */
export function leerFiltrosDeIngresos(params: URLSearchParams): FiltrosDeIngresos {
  const evento = params.get('evento');
  const resultado = params.get('resultado');
  return {
    operador: (params.get('operador') ?? '').trim(),
    evento: esEventoDeIngreso(evento) ? evento : '',
    resultado: resultado === 'exitoso' || resultado === 'fallido' ? resultado : '',
    desde: dia(params.get('desde')),
    hasta: dia(params.get('hasta')),
  };
}

export const hayFiltrosDeIngresos = (f: FiltrosDeIngresos): boolean => Boolean(f.operador || f.evento || f.resultado || f.desde || f.hasta);

/** Con las dos fechas, «desde» no puede ser posterior a «hasta» (la API respondería 422 `rango-invalido`). */
export const rangoDeIngresosInvalido = (f: Pick<FiltrosDeIngresos, 'desde' | 'hasta'>): boolean => Boolean(f.desde && f.hasta && f.desde > f.hasta);

/** Parámetros de la consulta: lo vacío no viaja. */
export function parametrosDeIngresos(f: FiltrosDeIngresos): FiltroDeIngresos {
  return {
    operador: f.operador || undefined,
    evento: f.evento || undefined,
    exitoso: f.resultado === '' ? undefined : f.resultado === 'exitoso',
    desde: f.desde ? limiteDelDia(f.desde, 'inicio') : undefined,
    hasta: f.hasta ? limiteDelDia(f.hasta, 'fin') : undefined,
  };
}

/** Un navegador largo («Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit…») dicho corto: «Chrome en Windows». */
export function dispositivoLegible(agente: string | null): string {
  if (!agente) return '—';
  const navegador = agente.includes('Edg/') ? 'Edge' : agente.includes('Firefox/') ? 'Firefox' : agente.includes('Chrome/') ? 'Chrome' : agente.includes('Safari/') ? 'Safari' : null;
  const sistema = agente.includes('Windows') ? 'Windows' : agente.includes('Android') ? 'Android' : /iPhone|iPad|iPod/.test(agente) ? 'iOS' : /Mac OS X|Macintosh/.test(agente) ? 'macOS' : agente.includes('Linux') ? 'Linux' : null;
  if (navegador && sistema) return `${navegador} en ${sistema}`;
  return navegador ?? sistema ?? agente.slice(0, 40);
}
