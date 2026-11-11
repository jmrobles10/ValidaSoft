import type { RecordatorioDeFlujoDto } from '@validasoft/api-client';
import { diasValidos } from '../recordatorios/automatico';
import type { BorradorFlujo, ErrorFlujo } from './editor';

// Recordatorio automático de observaciones por flujo en el editor (OP-19).

/** `Empresa` = lo que diga la empresa en Plantillas; `Apagado` = este flujo nunca recuerda solo; `CadaDias` = cada N días aunque la empresa lo tenga apagado. */
export const MODOS_RECORDATORIO = ['Empresa', 'Apagado', 'CadaDias'] as const;
export type ModoRecordatorio = (typeof MODOS_RECORDATORIO)[number];

/** Por omisión el flujo sigue a la empresa; los días son los que la empresa trae de fábrica (cada 3). */
export const RECORDATORIO_POR_DEFECTO = { modo: 'Empresa', dias: 3 } as const;

type CamposRecordatorio = Pick<BorradorFlujo, 'recordatorioModo' | 'recordatorioDias'>;

const esModo = (valor: string | undefined): valor is ModoRecordatorio => MODOS_RECORDATORIO.some((m) => m === valor);

/** Lleva el recordatorio de una versión al editor; una versión guardada antes de esta opción sigue a la empresa. */
export function recordatorioDesdeVersion(recordatorio: RecordatorioDeFlujoDto | null | undefined): CamposRecordatorio {
  return {
    recordatorioModo: esModo(recordatorio?.modo) ? recordatorio.modo : RECORDATORIO_POR_DEFECTO.modo,
    recordatorioDias: String(recordatorio?.cadaDias ?? RECORDATORIO_POR_DEFECTO.dias),
  };
}

/** El recordatorio del editor como lo recibe la API: los días solo viajan con `CadaDias` (lo que no sea número queda en 0 y la API lo rechaza). */
export function recordatorioADto(borrador: CamposRecordatorio): { modo: ModoRecordatorio; cadaDias: number | null } {
  if (borrador.recordatorioModo !== 'CadaDias') return { modo: borrador.recordatorioModo, cadaDias: null };
  const dias = Number.parseInt(borrador.recordatorioDias.trim(), 10);
  return { modo: 'CadaDias', cadaDias: Number.isFinite(dias) ? dias : 0 };
}

/** Replica `DefinicionDeFlujo.ValidarRecordatorio`: con `CadaDias`, un entero de 1 a 60. */
export function validarRecordatorio(borrador: CamposRecordatorio): ErrorFlujo[] {
  return borrador.recordatorioModo === 'CadaDias' && !diasValidos(borrador.recordatorioDias) ? [{ codigo: 'recordatorio-invalido', campo: 'recordatorio.dias' }] : [];
}
