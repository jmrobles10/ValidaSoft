import { create } from 'zustand';

interface Aviso {
  id: number;
  mensaje: string;
  severidad: 'success' | 'error' | 'info' | 'warning';
  /** El aviso cierra algo que la persona provocó (aprobar un expediente): lleva la animación de éxito de marca en lugar del ícono. */
  celebrar: boolean;
}

interface OpcionesAviso {
  celebrar?: boolean;
}

interface AvisosState {
  actual: Aviso | null;
  mostrar: (mensaje: string, severidad?: Aviso['severidad'], opciones?: OpcionesAviso) => void;
  cerrar: () => void;
}

let secuencia = 0;

/** Snackbar único de la app (docs/06 §2.3: "Error de acción: Snackbar con siguiente paso"). */
export const useAvisos = create<AvisosState>()((set) => ({
  actual: null,
  mostrar: (mensaje, severidad = 'success', opciones = {}) => set({ actual: { id: ++secuencia, mensaje, severidad, celebrar: severidad === 'success' && opciones.celebrar === true } }),
  cerrar: () => set({ actual: null }),
}));
