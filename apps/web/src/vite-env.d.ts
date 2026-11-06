/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origen de la API si no se sirve detrás del mismo origen (vacío = mismo origen). */
  readonly VITE_API_BASE_URL?: string;
  /** Orígenes que pueden alojar el iframe y entregarle la sesión, separados por coma. */
  readonly VITE_ORIGENES_CONTENEDOR?: string;
}
