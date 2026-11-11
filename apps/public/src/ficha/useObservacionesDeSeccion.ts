import type { EstadoFichaDto, ObservacionPublicaDto } from '@validasoft/api-client';

/** Lo que una empresa observó de la sección, con los campos que señaló. */
export interface ObservacionDeEmpresa {
  empresa: string;
  expedienteId: string;
  observacion: ObservacionPublicaDto;
}

/**
 * Observaciones abiertas de la sección (F2, FM-28; FD-2): las que alguna empresa dejó sobre esa sección (y ese periodo, en la
 * financiera) en un expediente devuelto al tercero. Llegan con el estado de la ficha, que ya las trae agrupadas: el portal no pide un
 * expediente por cada vínculo para pintar el formulario.
 */
export function observacionesDeSeccion(estado: Pick<EstadoFichaDto, 'observaciones'>, codigo: string, periodo: number | null): ObservacionDeEmpresa[] {
  return (estado.observaciones ?? [])
    .filter((o) => o.seccion === codigo && (periodo === null || o.periodo === null || o.periodo === periodo))
    .map((o) => ({ empresa: o.empresa, expedienteId: o.expedienteId, observacion: o.observacion }));
}
