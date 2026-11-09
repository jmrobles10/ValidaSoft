import type { AccesoSoporteDto } from '@validasoft/api-client';

/** Igual que `AccesoSoporte` del dominio (CF-2). */
export const HORAS_MINIMAS = 1;
export const HORAS_MAXIMAS = 72;
export const DURACIONES = [1, 4, 8, 24, 48, 72] as const;

export interface BorradorAcceso {
  empresaId: string;
  horas: number;
  justificacion: string;
}

export type ErrorAcceso = 'empresa-requerida' | 'duracion-invalida' | 'justificacion-requerida' | 'texto-demasiado-largo';

export function validarAcceso(b: BorradorAcceso): ErrorAcceso[] {
  const errores: ErrorAcceso[] = [];
  if (!b.empresaId) errores.push('empresa-requerida');
  if (!Number.isInteger(b.horas) || b.horas < HORAS_MINIMAS || b.horas > HORAS_MAXIMAS) errores.push('duracion-invalida');
  const texto = b.justificacion.trim();
  if (texto.length < 10) errores.push('justificacion-requerida');
  if (texto.length > 500) errores.push('texto-demasiado-largo');
  return errores;
}

export type EstadoAcceso = 'vigente' | 'revocado' | 'expirado';

export function estadoDe(a: Pick<AccesoSoporteDto, 'vigente' | 'revocadoEn'>): EstadoAcceso {
  return a.vigente ? 'vigente' : a.revocadoEn ? 'revocado' : 'expirado';
}

/** «en 23 h», «en 40 min» o nulo si ya pasó. */
export function restante(expiraEn: string, ahora: Date): { horas: number; minutos: number } | null {
  const ms = new Date(expiraEn).getTime() - ahora.getTime();
  if (ms <= 0) return null;
  const minutos = Math.ceil(ms / 60_000);
  return { horas: Math.floor(minutos / 60), minutos: minutos % 60 };
}
