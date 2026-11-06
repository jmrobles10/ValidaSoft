import { leerSesion, type SesionValidasoft } from '@validasoft/api-client';
import { create } from 'zustand';

/** `cerrada`: el contenedor envió `validasoft:cerrar-sesion`; no se pide otra hasta que él la entregue. */
export type EstadoSesion = 'sin-sesion' | 'activa' | 'vencida' | 'rechazada' | 'cerrada';

interface SesionState {
  /** El token vive solo en memoria (docs/06 §1.2): nunca en localStorage. */
  sesion: SesionValidasoft | null;
  estado: EstadoSesion;
  /** Empresa sugerida por el contenedor al entregar la sesión. */
  empresaSugerida: string | null;
  establecer: (token: string, empresaSugerida?: string | null) => boolean;
  marcar: (estado: Exclude<EstadoSesion, 'activa'>) => void;
}

export const useSesion = create<SesionState>()((set) => ({
  sesion: null,
  estado: 'sin-sesion',
  empresaSugerida: null,
  establecer: (token, empresaSugerida = null) => {
    try {
      const sesion = leerSesion(token);
      if (sesion.expira.getTime() <= Date.now()) {
        set({ sesion: null, estado: 'vencida' });
        return false;
      }
      set({ sesion, estado: 'activa', empresaSugerida });
      return true;
    } catch {
      set({ sesion: null, estado: 'rechazada' });
      return false;
    }
  },
  marcar: (estado) => set({ sesion: null, estado }),
}));
