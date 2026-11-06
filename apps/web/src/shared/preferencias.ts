import type { PaletteMode } from '@mui/material';
import { create } from 'zustand';
import { createJSONStorage, persist } from 'zustand/middleware';

interface PreferenciasState {
  modo: PaletteMode;
  /** Última empresa elegida por usuario (docs/06 §2.2, SelectorEmpresa). */
  ultimaEmpresa: Record<string, string>;
  /** El contenedor fijó el tema: el usuario no lo cambia desde aquí. */
  temaDelContenedor: boolean;
  fijarModo: (modo: PaletteMode, delContenedor?: boolean) => void;
  recordarEmpresa: (usuarioId: string, empresaId: string) => void;
}

/** Almacenamiento que no falla si el navegador bloquea localStorage (iframes de terceros, modo privado). */
const almacenamientoSeguro = createJSONStorage(() => {
  try {
    const prueba = '__validasoft__';
    window.localStorage.setItem(prueba, prueba);
    window.localStorage.removeItem(prueba);
    return window.localStorage;
  } catch {
    const memoria = new Map<string, string>();
    return {
      getItem: (clave: string) => memoria.get(clave) ?? null,
      setItem: (clave: string, valor: string) => void memoria.set(clave, valor),
      removeItem: (clave: string) => void memoria.delete(clave),
    };
  }
});

/** Solo preferencias de UI (docs/06 §1.2): nada del dominio vive en Zustand persistido. */
export const usePreferencias = create<PreferenciasState>()(
  persist(
    (set) => ({
      modo: 'dark',
      ultimaEmpresa: {},
      temaDelContenedor: false,
      fijarModo: (modo, delContenedor = false) => set((s) => ({ modo, temaDelContenedor: s.temaDelContenedor || delContenedor })),
      recordarEmpresa: (usuarioId, empresaId) => set((s) => ({ ultimaEmpresa: { ...s.ultimaEmpresa, [usuarioId]: empresaId } })),
    }),
    {
      name: 'validasoft:preferencias',
      storage: almacenamientoSeguro,
      partialize: (s) => ({ modo: s.modo, ultimaEmpresa: s.ultimaEmpresa }),
    },
  ),
);
