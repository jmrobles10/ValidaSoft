import { create } from 'zustand';

export interface Aviso {
  id: number;
  mensaje: string;
  severidad: 'success' | 'info' | 'error';
}

interface AvisosState {
  actual: Aviso | null;
  mostrar: (mensaje: string, severidad?: Aviso['severidad']) => void;
  cerrar: () => void;
}

let secuencia = 0;

/** Aviso único de la app (docs/06 §2.3). */
export const useAvisos = create<AvisosState>()((set) => ({
  actual: null,
  mostrar: (mensaje, severidad = 'success') => set({ actual: { id: ++secuencia, mensaje, severidad } }),
  cerrar: () => set({ actual: null }),
}));
