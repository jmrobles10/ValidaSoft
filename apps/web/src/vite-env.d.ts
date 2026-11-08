/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Origen de la API si no se sirve detrás del mismo origen (vacío = mismo origen). */
  readonly VITE_API_BASE_URL?: string;
  /**
   * Respaldo de los orígenes que pueden alojar el iframe y entregarle la sesión, separados por coma. La lista real la
   * publica la API (`GET /public/v1/embed/origenes-permitidos`); esta se suma en desarrollo o si la consulta falla.
   */
  readonly VITE_ORIGENES_CONTENEDOR?: string;
}
