import type { EstadoVinculo, ListarVinculosParams, VinculoDto } from '@validasoft/api-client';

/**
 * Lo que la empresa ve de un vínculo (VR-1): con una actualización abierta (el tercero actualizó información que ya había aprobado) se
 * muestra «En revisión» aunque su estado de dominio siga siendo Activo, Por vencer o Vencido (no cambia: tampoco el certificado ni la
 * vigencia). Un solo término por concepto: la lista, el detalle, la ficha, las cifras y los filtros usan esta misma situación.
 */
export const EN_REVISION = 'EnRevision' as const;

export type EstadoVisibleDelVinculo = EstadoVinculo | typeof EN_REVISION;

export function estadoVisibleDelVinculo(vinculo: Pick<VinculoDto, 'estado' | 'enRevision'>): EstadoVisibleDelVinculo {
  return vinculo.enRevision ? EN_REVISION : vinculo.estado;
}

/**
 * Lo que pide el filtro «Estado» a la API: «En revisión» es `enRevision=true`; cualquier otro estado excluye a los que se ven «En revisión»
 * (`enRevision=false`), así «Activo» en la lista no repite lo que la tabla muestra «En revisión».
 */
export function parametrosDeEstado(estado: string): Pick<ListarVinculosParams, 'estado' | 'enRevision'> {
  if (!estado) return {};
  return estado === EN_REVISION ? { enRevision: true } : { estado, enRevision: false };
}
