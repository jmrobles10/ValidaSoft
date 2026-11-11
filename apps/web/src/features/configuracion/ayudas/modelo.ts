import type { AyudaDeDocumentoDto } from '@validasoft/api-client';

/** Igual que `AyudasDeDocumentoPorEmpresa.LongitudMaxima` del backend. */
export const MAXIMO_AYUDA = 2000;

/** De quién es la ayuda que ve el tercero: de la empresa, del grupo o ninguna. */
export type OrigenAyuda = 'propia' | 'grupo' | 'ninguna';

export const ORIGENES_AYUDA: readonly OrigenAyuda[] = ['propia', 'grupo', 'ninguna'];

const tieneTexto = (texto: string | null | undefined): texto is string => Boolean(texto?.trim());

export function origenDeAyuda(ayuda: Pick<AyudaDeDocumentoDto, 'ayudaDeLaEmpresa' | 'ayudaDelGrupo'>): OrigenAyuda {
  if (tieneTexto(ayuda.ayudaDeLaEmpresa)) return 'propia';
  return tieneTexto(ayuda.ayudaDelGrupo) ? 'grupo' : 'ninguna';
}

/** El texto de la lista: una sola línea corta, sin saltos. */
export function resumirAyuda(texto: string | null, maximo = 140): string {
  const plano = (texto ?? '').replace(/\s+/g, ' ').trim();
  return plano.length <= maximo ? plano : `${plano.slice(0, maximo - 1).trimEnd()}…`;
}

/** Lo que viaja a la API: vacío es `null` (la empresa vuelve a usar la ayuda del grupo). */
export function textoParaGuardar(texto: string): string | null {
  const limpio = texto.trim();
  return limpio === '' ? null : limpio;
}

/** ¿Lo escrito difiere de lo que la empresa ya tiene guardado? */
export function cambioDeAyuda(guardada: string | null, texto: string): boolean {
  return (textoParaGuardar(texto) ?? '') !== (guardada?.trim() ?? '');
}
