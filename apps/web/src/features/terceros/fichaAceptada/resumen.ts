import type { FichaAceptadaDelVinculoDto, SeccionAceptadaVistaDto } from '@validasoft/api-client';

/** Cuántas secciones aceptó la empresa y en cuántas el tercero guardó después una versión nueva que aún no revisa. */
export interface ResumenDeFichaAceptada {
  aceptadas: number;
  pendientes: number;
}

export const tienePendiente = (seccion: Pick<SeccionAceptadaVistaDto, 'cambioPosterior'>): boolean => seccion.cambioPosterior !== null && seccion.cambioPosterior !== undefined;

export function resumirFichaAceptada(ficha: Pick<FichaAceptadaDelVinculoDto, 'secciones'>): ResumenDeFichaAceptada {
  return { aceptadas: ficha.secciones.length, pendientes: ficha.secciones.filter(tienePendiente).length };
}
