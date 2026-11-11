import { cerrarSesionTercero, esErrorApi, renovarSesionTercero, solicitarAcceso as solicitarAccesoApi, type SesionTerceroDto, type SolicitudRecibidaDto } from '@validasoft/api-client';

/**
 * Reingreso del tercero (`feature/H2-reingreso`):
 * - `POST /public/v1/acceso { correo }` → 202 siempre, con el mismo mensaje exista o no el correo (422
 *   `correo-invalido` solo si está mal formado). El enlace pedido así vence en 1 h.
 * - Al canjear el enlace o el OTP, la API deja la cookie `vs_tercero` (HttpOnly, SameSite=Lax, Path=/public/v1/sesion,
 *   8 h como máximo desde el canje). `POST /public/v1/sesion/renovar` la cambia por un access nuevo de 30 min y la
 *   **rota**; `POST /public/v1/sesion/cerrar` la borra. Ambas exigen `X-Validasoft-Cliente: public`.
 * - Sin periodo de gracia: presentar una cookie ya rotada cuenta como reuso y revoca la sesión (401
 *   `sesion-revocada`). Por eso las renovaciones se serializan entre pestañas (Web Locks) y un 409
 *   `renovacion-concurrente` (otra renovación ganó con la misma cookie) se reintenta una sola vez.
 *
 * El navegador manda la cookie solo (mismo origen); por eso estas llamadas van sin `Authorization`.
 */
export const CABECERA_CLIENTE = { 'X-Validasoft-Cliente': 'public' } as const;
const SIN_SESION = { token: null, empresa: null, headers: CABECERA_CLIENTE } as const;
/** Nombre del candado compartido por todas las pestañas del mismo origen. */
export const CANDADO_RENOVACION = 'validasoft-tercero-renovacion';
/** Espera antes de reintentar tras `renovacion-concurrente`: la respuesta ganadora ya dejó la cookie nueva. */
export const ESPERA_REINTENTO_CONCURRENTE_MS = 300;

export function solicitarAcceso(correo: string): Promise<SolicitudRecibidaDto> {
  return solicitarAccesoApi({ correo: correo.trim() }, { token: null, empresa: null });
}

/** Ejecuta `tarea` con el candado de renovación de todas las pestañas; sin Web Locks, la ejecuta directo. */
export function conCandado<T>(tarea: () => Promise<T>, candados: LockManager | undefined = candadosDelNavegador()): Promise<T> {
  if (!candados) return tarea();
  // El candado se suelta cuando termina `tarea` (la promesa que devuelve el callback).
  return new Promise<T>((resolver, rechazar) => {
    void candados.request(CANDADO_RENOVACION, () => tarea().then(resolver, rechazar));
  });
}

function candadosDelNavegador(): LockManager | undefined {
  return typeof navigator !== 'undefined' && 'locks' in navigator ? navigator.locks : undefined;
}

export function esRenovacionConcurrente(error: unknown): boolean {
  return esErrorApi(error) && error.codigo === 'renovacion-concurrente';
}

/** Una renovación con su único reintento ante `renovacion-concurrente`. */
export async function renovarUnaVez(renovar: () => Promise<SesionTerceroDto>, esperar = (ms: number) => new Promise((r) => setTimeout(r, ms))): Promise<SesionTerceroDto> {
  try {
    return await renovar();
  } catch (error) {
    if (!esRenovacionConcurrente(error)) throw error;
    await esperar(ESPERA_REINTENTO_CONCURRENTE_MS);
    return renovar();
  }
}

export function renovarSesion(): Promise<SesionTerceroDto> {
  return conCandado(() => renovarUnaVez(() => renovarSesionTercero(SIN_SESION)));
}

export async function cerrarSesionEnServidor(): Promise<void> {
  await conCandado(() => cerrarSesionTercero(SIN_SESION));
}

/** Rutas de la sesión: un 401 en ellas no se reintenta renovando (sería un ciclo). */
export function esRutaDeSesion(url: string): boolean {
  return /\/public\/v1\/(sesion\/|enlaces\/|acceso)/.test(url);
}

/**
 * ¿Un 401 en esta URL da por terminada la sesión que hay? No si es de una ruta de la sesión: lo resuelve quien la llamó (`restaurar`,
 * `renovarAhora`, el canje del enlace, el puente del iframe). Sin esta salvedad el 401 tardío del intento de restaurar al arrancar sin
 * cookie tumbaba una sesión abierta mientras tanto (por el enlace, o la que el sistema anfitrión entrega a un iframe).
 */
export function unNoAutenticadoTerminaLaSesion(url: string): boolean {
  return !esRutaDeSesion(url);
}
