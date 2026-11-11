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

import { VERSION_PROTOCOLO as VERSION_DEL_SDK } from '@validasoft/sdk';

export const VERSION_PROTOCOLO = VERSION_DEL_SDK;

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

// Lo que no depende del tipo de mensaje (orígenes permitidos, contenedor, ¿embebida?) es del SDK, que lo comparten las dos apps embebibles.
export {
  cargarOrigenesPermitidos,
  estaEmbebida,
  origenDelContenedor,
  origenesPermitidos,
  origenPermitido,
  RECARGA_ORIGENES_MS,
  RUTA_ORIGENES_PERMITIDOS,
  siguienteListaDeOrigenes,
} from '@validasoft/sdk';
