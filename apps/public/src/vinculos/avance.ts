import type { EstadoExpediente, EstadoVinculo } from '@validasoft/api-client';
import { situacionDelVinculo } from './situacion';

/**
 * Lo que le toca al tercero con una empresa, en sus palabras. El estado interno del vínculo («En validación») no le
 * dice nada: apenas acepta ya está «en validación» aunque no haya subido ningún documento. Se calcula con el expediente
 * público (`estado` y `requisitosFaltantes`):
 * - `faltan`: expediente `Abierto` con documentos pendientes → «Faltan N documentos» y la acción para subirlos.
 * - `enRevision`: la empresa está revisando (o el expediente está abierto sin nada pendiente).
 * - `observado`: la empresa devolvió algo → «Tienes observaciones».
 * - `aprobado`: expediente aprobado o vínculo `Activo`.
 * - `preVencimiento`: el vínculo está `PorVencer` (primer aviso de su vigencia periódica, VV-1) y su renovación espera que el
 *   tercero ratifique o actualice su información antes de la fecha (VV-3); si la aprueba la empresa a tiempo, nunca vence.
 * - `porVencer`: el vínculo está `PorVencer` pero no hay una renovación abierta a la vista: vence pronto.
 * - `ratificar`: el vínculo venció y su renovación espera que el tercero ratifique o actualice su información (VV-3).
 * `null` cuando no hay un paso propio que contar (sin aceptar, vínculo suspendido o terminado, expediente cerrado sin
 * aprobar): se muestra el estado tal cual.
 */
export type AvanceTercero =
  | { tipo: 'faltan'; documentos: number }
  | { tipo: 'enRevision' }
  | { tipo: 'observado' }
  | { tipo: 'aprobado' }
  | { tipo: 'preVencimiento'; hasta: string | null }
  | { tipo: 'porVencer'; hasta: string | null }
  | { tipo: 'ratificar' };

export function avanceDelTercero(
  vinculo: { estado: EstadoVinculo; invitacionAceptada: boolean; vigenteHasta?: string | null } | null | undefined,
  expediente: { estado: EstadoExpediente; requisitosFaltantes?: number; tipo?: string } | null | undefined,
): AvanceTercero | null {
  if (vinculo && (situacionDelVinculo(vinculo.estado) !== 'vigente' || !vinculo.invitacionAceptada)) return null;
  if (vinculo?.estado === 'PorVencer') {
    // Pre-vencimiento: la renovación abierta espera al tercero. Ya enviada (en revisión u observada) cuenta como cualquier expediente.
    const renovacion = expediente?.tipo === 'Renovacion' ? expediente.estado : null;
    if (renovacion === 'Abierto') return { tipo: 'preVencimiento', hasta: vinculo.vigenteHasta ?? null };
    if (renovacion !== 'EnRevision' && renovacion !== 'Observado') return { tipo: 'porVencer', hasta: vinculo.vigenteHasta ?? null };
  }
  if (vinculo?.estado === 'Vencido' && expediente?.tipo === 'Renovacion' && expediente.estado === 'Abierto') return { tipo: 'ratificar' };
  if (vinculo?.estado === 'Activo' || expediente?.estado === 'Aprobado') return { tipo: 'aprobado' };
  switch (expediente?.estado) {
    case 'Abierto': {
      const faltantes = expediente.requisitosFaltantes ?? 0;
      return faltantes > 0 ? { tipo: 'faltan', documentos: faltantes } : { tipo: 'enRevision' };
    }
    case 'EnRevision':
      return { tipo: 'enRevision' };
    case 'Observado':
      return { tipo: 'observado' };
    default:
      return null;
  }
}

/** Estado del chip (da el ícono y el tono) para cada avance; el texto lo pone `avance.<tipo>` en i18n. */
export const ESTADO_DEL_AVANCE = {
  faltan: 'ConPendientes',
  enRevision: 'EnRevision',
  observado: 'Observado',
  aprobado: 'Aprobado',
  preVencimiento: 'PorVencer',
  porVencer: 'PorVencer',
  ratificar: 'Vencido',
} as const;
