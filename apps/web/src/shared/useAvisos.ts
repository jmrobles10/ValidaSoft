import { create } from 'zustand';

interface Aviso {
  id: number;
  mensaje: string;
  severidad: 'success' | 'error' | 'info' | 'warning';
}

interface AvisosState {
  actual: Aviso | null;
  mostrar: (mensaje: string, severidad?: Aviso['severidad']) => void;
  cerrar: () => void;
}

let secuencia = 0;

/** Snackbar único de la app (docs/06 §2.3: "Error de acción: Snackbar con siguiente paso"). */
export const useAvisos = create<AvisosState>()((set) => ({
  actual: null,
  mostrar: (mensaje, severidad = 'success') => set({ actual: { id: ++secuencia, mensaje, severidad } }),
  cerrar: () => set({ actual: null }),
}));
