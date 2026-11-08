/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origen de la API si no se sirve detrás del mismo origen (vacío = mismo origen; en desarrollo, proxy de Vite). */
  readonly VITE_API_BASE_URL?: string;
}
