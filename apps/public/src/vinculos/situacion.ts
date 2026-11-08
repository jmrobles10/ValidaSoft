import type { EstadoVinculo } from '@validasoft/api-client';

/**
 * Lo que el tercero puede hacer con un vínculo según su estado, sin revelar el motivo interno:
 * - `suspendida`: la empresa lo bloqueó (temporalmente); no acepta, no sube para esa empresa, no reenvía.
 * - `terminada`: la empresa lo desvinculó o lo anuló; la invitación ya no está vigente.
 * - `vigente`: todo lo demás.
 */
export type SituacionVinculo = 'vigente' | 'suspendida' | 'terminada';

export function situacionDelVinculo(estado: EstadoVinculo | null | undefined): SituacionVinculo {
  if (estado === 'Bloqueado') return 'suspendida';
  if (estado === 'Desvinculado' || estado === 'Anulado') return 'terminada';
  return 'vigente';
}
