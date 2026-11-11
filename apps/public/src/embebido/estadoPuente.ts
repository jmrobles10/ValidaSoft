import { create } from 'zustand';

/** Estado del puente con el sistema anfitrión (lo escribe `usePuenteEmbebido`, desde el Shell; lo lee la página de entrada). */
export type EstadoDelPuente = 'esperando' | 'lista' | 'rechazada' | 'sin-contenedor';

interface Estado {
  estado: EstadoDelPuente;
  poner: (siguiente: EstadoDelPuente | ((actual: EstadoDelPuente) => EstadoDelPuente)) => void;
}

export const useEstadoPuente = create<Estado>()((set) => ({
  estado: 'esperando',
  poner: (siguiente) => set((s) => ({ estado: typeof siguiente === 'function' ? siguiente(s.estado) : siguiente })),
}));
