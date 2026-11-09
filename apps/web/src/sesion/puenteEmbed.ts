/**
 * Contrato postMessage entre las pantallas embebidas de Validasoft y el sistema que las aloja (ID-3, docs/06 §3.2).
 *
 * Validasoft → contenedor:
 *   { tipo: 'validasoft:ready', version: 1 }
 *   { tipo: 'validasoft:sesion-requerida', motivo: 'inicial' | 'por-vencer' | 'vencida' | 'rechazada' }
 *   { tipo: 'validasoft:resize', height }
 *   { tipo: 'validasoft:visible', top, height }   región del iframe (px) que debe verse: un diálogo o panel recién abierto
 *
 * Contenedor → Validasoft:
 *   { tipo: 'validasoft:sesion', token, empresaId? }   token = `POST /api/v1/sesiones-usuario` (lo pide el backend del sistema)
 *   { tipo: 'validasoft:tema', mode: 'light' | 'dark' }
 *   { tipo: 'validasoft:cerrar-sesion' }
 *
 * Solo se aceptan mensajes de orígenes permitidos y solo se envían al origen del contenedor si está permitido. Los
 * orígenes permitidos se leen **en tiempo de ejecución** de `GET /public/v1/embed/origenes-permitidos` (los que cada
 * sistema integrado registró en Validasoft; los mismos de `frame-ancestors`, SE-2). `VITE_ORIGENES_CONTENEDOR` queda
 * como respaldo: se suma en desarrollo o si la consulta falla.
 */

export const VERSION_PROTOCOLO = 1;

export type MotivoSesionRequerida = 'inicial' | 'por-vencer' | 'vencida' | 'rechazada';

export type MensajeSaliente =
  | { tipo: 'validasoft:ready'; version: number }
  | { tipo: 'validasoft:sesion-requerida'; motivo: MotivoSesionRequerida }
  | { tipo: 'validasoft:resize'; height: number }
  | { tipo: 'validasoft:visible'; top: number; height: number };

export type MensajeEntrante =
  | { tipo: 'validasoft:sesion'; token: string; empresaId: string | null }
  | { tipo: 'validasoft:tema'; mode: 'light' | 'dark' }
  | { tipo: 'validasoft:cerrar-sesion' };

function esRegistro(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

/** Valida la forma del mensaje recibido; cualquier otra cosa se ignora. */
export function interpretarMensaje(datos: unknown): MensajeEntrante | null {
  if (!esRegistro(datos) || typeof datos.tipo !== 'string') return null;

  switch (datos.tipo) {
    case 'validasoft:sesion': {
      if (typeof datos.token !== 'string' || datos.token.split('.').length !== 3) return null;
      const empresaId = typeof datos.empresaId === 'string' && datos.empresaId ? datos.empresaId : null;
      return { tipo: 'validasoft:sesion', token: datos.token, empresaId };
    }
    case 'validasoft:tema':
      return datos.mode === 'light' || datos.mode === 'dark' ? { tipo: 'validasoft:tema', mode: datos.mode } : null;
    case 'validasoft:cerrar-sesion':
      return { tipo: 'validasoft:cerrar-sesion' };
    default:
      return null;
  }
}

/** Ruta pública (sin credencial) con los orígenes que pueden alojar las pantallas embebidas. */
export const RUTA_ORIGENES_PERMITIDOS = '/public/v1/embed/origenes-permitidos';

/**
 * Lista de orígenes permitidos: el propio (la demo embebida vive en el mismo origen), los que publica la API y los de
 * `VITE_ORIGENES_CONTENEDOR`. Estos últimos son respaldo: entran si aún no hay lista de la API (`remotos` nulo, porque
 * falló o no ha llegado) o si `incluirConfigurados` (desarrollo).
 */
export function origenesPermitidos(
  configurados: string | undefined,
  propio: string,
  remotos: readonly string[] | null = null,
  incluirConfigurados = false,
): string[] {
  const lista = (configurados ?? '')
    .split(',')
    .map((o) => o.trim().replace(/\/$/, ''))
    .filter(Boolean);
  const respaldo = remotos === null || incluirConfigurados ? lista : [];
  return [...new Set([propio, ...(remotos ?? []), ...respaldo])];
}

/**
 * Pide a la API los orígenes permitidos. Devuelve `null` si no responde o la respuesta no tiene la forma esperada
 * (`{ origenes: string[] }`): en ese caso se usa el respaldo.
 */
export async function cargarOrigenesPermitidos(baseUrl = '', fetchFn: typeof fetch = (...a) => globalThis.fetch(...a)): Promise<string[] | null> {
  try {
    const respuesta = await fetchFn(`${baseUrl}${RUTA_ORIGENES_PERMITIDOS}`, { headers: { Accept: 'application/json' } });
    if (!respuesta.ok) return null;
    const datos: unknown = await respuesta.json();
    if (!esRegistro(datos) || !Array.isArray(datos.origenes)) return null;
    return datos.origenes.filter((o): o is string => typeof o === 'string' && /^https?:\/\/[^/]+$/.test(o));
  } catch {
    return null;
  }
}

/** Cada cuánto se vuelve a pedir la lista (la API la publica con `vigenciaSegundos: 60`). */
export const RECARGA_ORIGENES_MS = 60_000;

/**
 * Lista vigente tras una consulta: si falló, se queda la última buena (y si nunca hubo, `null` = respaldo); si no
 * cambió, la misma referencia, para no volver a anunciar `ready` al contenedor cada minuto.
 */
export function siguienteListaDeOrigenes(previa: readonly string[] | null | undefined, nueva: string[] | null): readonly string[] | null {
  if (nueva === null) return previa ?? null;
  if (previa?.length === nueva.length && previa.every((o, i) => o === nueva[i])) return previa;
  return nueva;
}

export function origenPermitido(origen: string, permitidos: readonly string[]): boolean {
  return origen !== 'null' && permitidos.includes(origen);
}

/**
 * Origen del contenedor: `ancestorOrigins` (Chromium) o el `referrer`. Solo se usa si está permitido, para no
 * entregar mensajes a una página que no debería alojar a Validasoft.
 */
export function origenDelContenedor(ventana: Pick<Window, 'location' | 'document'>, permitidos: readonly string[]): string | null {
  const ancestros = ventana.location.ancestorOrigins as DOMStringList | undefined;
  const candidato = ancestros?.[0] ?? (ventana.document.referrer ? new URL(ventana.document.referrer).origin : null);
  return candidato && origenPermitido(candidato, permitidos) ? candidato : null;
}

export function estaEmbebida(ventana: Window = window): boolean {
  try {
    return ventana.self !== ventana.top;
  } catch {
    return true; // acceder a `top` de otro origen lanza: estamos en un iframe
  }
}
