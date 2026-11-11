import { leerSesion, type SesionValidasoft } from '@validasoft/api-client';
import { create } from 'zustand';

/**
 * `cerrada`: el contenedor envió `validasoft:cerrar-sesion` (o la persona cerró su sesión directa); no se pide otra hasta que él la entregue.
 * Los demás son del ingreso directo: `inactiva` (el token venció sin actividad; «Continuar» la reanuda mientras la cookie siga vigente),
 * `terminada` (se agotó la sesión o ya no se pudo renovar) y `revocada` (la cerró la persona desde otro dispositivo o un administrador).
 */
export type EstadoSesion = 'sin-sesion' | 'activa' | 'vencida' | 'rechazada' | 'cerrada' | 'inactiva' | 'terminada' | 'revocada';

interface OpcionesDeSesion {
  /** Entró solo con el código y su cuenta no tiene passkey: conviene sugerirle registrar una. */
  sugerirPasskey?: boolean;
}

interface SesionState {
  /** El token vive solo en memoria (docs/06 §1.2): nunca en localStorage. */
  sesion: SesionValidasoft | null;
  estado: EstadoSesion;
  /** Empresa sugerida por el contenedor al entregar la sesión. */
  empresaSugerida: string | null;
  /** Aviso «Protege tu cuenta con una passkey»: sigue hasta que la persona lo descarte o registre una. */
  sugerirPasskey: boolean;
  establecer: (token: string, empresaSugerida?: string | null, opciones?: OpcionesDeSesion) => boolean;
  marcar: (estado: Exclude<EstadoSesion, 'activa'>) => void;
  descartarSugerenciaDePasskey: () => void;
}

export const useSesion = create<SesionState>()((set) => ({
  sesion: null,
  estado: 'sin-sesion',
  empresaSugerida: null,
  sugerirPasskey: false,
  establecer: (token, empresaSugerida = null, opciones = {}) => {
    try {
      const sesion = leerSesion(token);
      if (sesion.expira.getTime() <= Date.now()) {
        set({ sesion: null, estado: 'vencida' });
        return false;
      }
      // Renovar el token no vuelve a sugerir lo que la persona ya descartó.
      set((previo) => ({ sesion, estado: 'activa', empresaSugerida, sugerirPasskey: opciones.sugerirPasskey ?? previo.sugerirPasskey }));
      return true;
    } catch {
      set({ sesion: null, estado: 'rechazada' });
      return false;
    }
  },
  marcar: (estado) => set({ sesion: null, estado, ...(estado !== 'inactiva' && { sugerirPasskey: false }) }),
  descartarSugerenciaDePasskey: () => set({ sugerirPasskey: false }),
}));
