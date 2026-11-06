import type { SesionTerceroDto } from '@validasoft/api-client';
import { create } from 'zustand';

/**
 * Sesión del tercero (docs/05 §2.9.1): JWT de 30 minutos que devuelve el canje del enlace. Vive **solo en memoria**
 * (Bearer); la cookie llegará después. Al recargar la página se pierde y hay que volver a abrir el enlace del correo.
 * `salio`: la persona cerró la sesión con «Salir».
 */
export type EstadoSesionTercero = 'sin-sesion' | 'activa' | 'vencida' | 'salio';

interface SesionTerceroState {
  sesion: SesionTerceroDto | null;
  estado: EstadoSesionTercero;
  establecer: (sesion: SesionTerceroDto) => void;
  /** La API respondió 401 o el token venció: se pide abrir de nuevo el enlace. */
  vencer: () => void;
  cerrar: () => void;
}

let temporizador: ReturnType<typeof setTimeout> | null = null;

function programarVencimiento(sesion: SesionTerceroDto, vencer: () => void) {
  if (temporizador) clearTimeout(temporizador);
  const restante = new Date(sesion.expira).getTime() - Date.now();
  temporizador = setTimeout(vencer, Math.max(0, restante));
}

export const useSesionTercero = create<SesionTerceroState>()((set, get) => ({
  sesion: null,
  estado: 'sin-sesion',
  establecer: (sesion) => {
    set({ sesion, estado: 'activa' });
    programarVencimiento(sesion, () => {
      if (get().sesion === sesion) get().vencer();
    });
  },
  vencer: () => {
    if (temporizador) clearTimeout(temporizador);
    set((s) => ({ sesion: null, estado: s.sesion ? 'vencida' : s.estado }));
  },
  cerrar: () => {
    if (temporizador) clearTimeout(temporizador);
    set({ sesion: null, estado: 'salio' });
  },
}));

/** Nombre de la empresa de un vínculo de la sesión (para "La recibió Constructora Norte"). */
export function nombreEmpresaDeVinculo(sesion: SesionTerceroDto | null, vinculoId: string): string | null {
  return sesion?.vinculos.find((v) => v.id === vinculoId)?.empresa.nombre ?? null;
}
