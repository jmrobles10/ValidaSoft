import { esErrorApi, type SesionTerceroDto } from '@validasoft/api-client';
import { create } from 'zustand';
import { cerrarSesionEnServidor, renovarSesion } from './sesionApi';

/**
 * Sesión del tercero. El access token (JWT de 30 min) vive en memoria y viaja como `Authorization: Bearer`; la cookie
 * `vs_tercero` (HttpOnly, hasta 8 h desde el canje) permite pedir otro con `POST /public/v1/sesion/renovar`:
 * - al arrancar se intenta renovar (`restaurando`): si hay cookie vigente, la persona sigue dentro tras recargar;
 * - unos minutos antes de vencer se renueva solo;
 * - si una llamada responde 401, se renueva una vez y se reintenta (`envolverConRenovacion`).
 * `salio`: la persona cerró la sesión con «Salir» (también en el servidor, que borra la cookie).
 */
export type EstadoSesionTercero = 'restaurando' | 'sin-sesion' | 'activa' | 'vencida' | 'salio';

/** Se pide el access nuevo este tiempo antes de que venza. */
export const ANTICIPO_RENOVACION_MS = 2 * 60 * 1000;
/** Si la renovación falla por la red (no por 401), se reintenta tras este tiempo mientras el access siga vigente. */
export const REINTENTO_RENOVACION_MS = 30 * 1000;

export interface SesionTerceroState {
  sesion: SesionTerceroDto | null;
  estado: EstadoSesionTercero;
  establecer: (sesion: SesionTerceroDto) => void;
  /** Al arrancar: entra directo si la cookie sigue vigente; si no, muestra la entrada. */
  restaurar: () => Promise<void>;
  /** Renueva ya (una sola petición aunque la pidan varias llamadas a la vez); `null` si la sesión terminó. */
  renovarAhora: () => Promise<string | null>;
  /** La sesión terminó (cookie vencida o revocada): se pide entrar de nuevo. */
  vencer: () => void;
  /** «Salir»: borra la cookie en el servidor y la sesión aquí. */
  salir: () => Promise<void>;
}

export interface ApiSesion {
  renovar: () => Promise<SesionTerceroDto>;
  cerrar: () => Promise<void>;
}

/** La renovación falló porque la sesión terminó (401 `sesion-expirada` o `sesion-revocada`), no por la red. */
function sesionTerminada(error: unknown): boolean {
  return esErrorApi(error) && (error.estado === 401 || error.estado === 403);
}

export function crearSesionTercero(api: ApiSesion) {
  let temporizador: ReturnType<typeof setTimeout> | null = null;
  let enCurso: Promise<string | null> | null = null;
  const detener = () => {
    if (temporizador) clearTimeout(temporizador);
    temporizador = null;
  };

  return create<SesionTerceroState>()((set, get) => {
    const programar = (sesion: SesionTerceroDto, espera?: number) => {
      detener();
      const restante = new Date(sesion.expira).getTime() - Date.now();
      temporizador = setTimeout(
        () => {
          if (get().sesion === sesion) void get().renovarAhora();
        },
        espera ?? Math.max(0, restante - ANTICIPO_RENOVACION_MS),
      );
    };

    return {
      sesion: null,
      estado: 'restaurando',
      establecer: (sesion) => {
        set({ sesion, estado: 'activa' });
        programar(sesion);
      },
      restaurar: async () => {
        try {
          const sesion = await api.renovar();
          // Si mientras tanto entró por un enlace, manda esa sesión.
          if (get().estado === 'restaurando') get().establecer(sesion);
        } catch {
          if (get().estado === 'restaurando') set({ estado: 'sin-sesion' });
        }
      },
      renovarAhora: () => {
        enCurso ??= api
          .renovar()
          .then(
            (sesion) => {
              get().establecer(sesion);
              return sesion.accessToken;
            },
            (error: unknown) => {
              const actual = get().sesion;
              if (sesionTerminada(error) || !actual) {
                get().vencer();
              } else if (new Date(actual.expira).getTime() > Date.now()) {
                programar(actual, REINTENTO_RENOVACION_MS); // sin red: se intenta de nuevo mientras el access sirva
              } else {
                get().vencer();
              }
              return null;
            },
          )
          .finally(() => {
            enCurso = null;
          });
        return enCurso;
      },
      vencer: () => {
        detener();
        set((s) => ({ sesion: null, estado: s.estado === 'activa' ? 'vencida' : s.estado === 'restaurando' ? 'sin-sesion' : s.estado }));
      },
      salir: async () => {
        detener();
        try {
          await api.cerrar();
        } catch {
          // Sin red o ya cerrada: igual se sale aquí; la cookie vence sola.
        }
        set({ sesion: null, estado: 'salio' });
      },
    };
  });
}

export const useSesionTercero = crearSesionTercero({ renovar: renovarSesion, cerrar: cerrarSesionEnServidor });

/** Nombre de la empresa de un vínculo de la sesión (para "La recibió Constructora Norte"). */
export function nombreEmpresaDeVinculo(sesion: SesionTerceroDto | null, vinculoId: string): string | null {
  return sesion?.vinculos.find((v) => v.id === vinculoId)?.empresa.nombre ?? null;
}
