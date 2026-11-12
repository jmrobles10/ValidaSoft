import type { ErrorApi } from './errores';

/**
 * Configuración del cliente. La app la fija una vez al arrancar (y cada vez que cambian la sesión o la empresa no
 * hace falta: `obtenerToken` y `obtenerEmpresa` se leen en cada petición).
 */
export interface ConfiguracionCliente {
  /** Origen de la API. Vacío = mismo origen (en desarrollo Vite hace de proxy de `/api`). */
  baseUrl: string;
  /** Token Bearer de la sesión (`POST /api/v1/sesiones-usuario`), o `null` si no hay sesión. */
  obtenerToken: () => string | null;
  /** Empresa activa: viaja en `X-Empresa`. `null` en operaciones de tenant. */
  obtenerEmpresa: () => string | null;
  /** Se llama con cada 401 (y la URL pedida), antes de rechazar la promesa. */
  alNoAutenticado?: (error: ErrorApi, url: string) => void;
  /**
   * Una acción sensible de una sesión directa pide confirmar con la passkey (403 `reautenticacion-requerida`, PI-13). La app abre el diálogo, hace la
   * ceremonia y responde si se confirmó: con `true` el cliente repite la petición una vez; con `false`, rechaza con el error original.
   */
  alRequerirReautenticacion?: (error: ErrorApi) => Promise<boolean>;
  /** Genera `Idempotency-Key` y `X-Correlation-Id`. */
  generarId: () => string;
  /** Implementación de fetch (inyectable en pruebas). */
  fetch: typeof fetch;
}

const porDefecto = (): ConfiguracionCliente => ({
  baseUrl: '',
  obtenerToken: () => null,
  obtenerEmpresa: () => null,
  generarId: () => crypto.randomUUID(),
  fetch: (...argumentos) => globalThis.fetch(...argumentos),
});

let actual: ConfiguracionCliente = porDefecto();

export function configurarCliente(cambios: Partial<ConfiguracionCliente>): void {
  actual = { ...actual, ...cambios };
}

/** Vuelve a la configuración inicial (útil en pruebas). */
export function reiniciarCliente(): void {
  actual = porDefecto();
}

export function configuracionCliente(): Readonly<ConfiguracionCliente> {
  return actual;
}
