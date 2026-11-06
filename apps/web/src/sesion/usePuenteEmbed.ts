import { msRestantes } from '@validasoft/api-client';
import { useEffect, useMemo } from 'react';
import { usePreferencias } from '../shared/preferencias';
import {
  estaEmbebida,
  interpretarMensaje,
  origenDelContenedor,
  origenesPermitidos,
  origenPermitido,
  VERSION_PROTOCOLO,
  type MensajeSaliente,
  type MotivoSesionRequerida,
} from './puenteEmbed';
import { useSesion, type EstadoSesion } from './sesionStore';

/** Se pide la renovación este tiempo antes de que venza el token de 30 minutos. */
export const AVISO_RENOVACION_MS = 2 * 60 * 1000;

/** Cada cuánto se vuelve a pedir la sesión mientras no llega. */
export const REINTENTO_SESION_MS = 5_000;

/** `cerrada` no pide nada: el contenedor cerró la sesión a propósito. */
const MOTIVO_POR_ESTADO: Partial<Record<EstadoSesion, MotivoSesionRequerida>> = {
  'sin-sesion': 'inicial',
  vencida: 'vencida',
  rechazada: 'rechazada',
};

export interface PuenteEmbed {
  embebida: boolean;
  /** Origen del contenedor si está permitido; `null` si la página no está embebida o el contenedor no es de confianza. */
  origenContenedor: string | null;
}

/**
 * Conecta la app con el sistema que la aloja: anuncia `ready`, recibe la sesión y el tema, pide renovación antes
 * del vencimiento y avisa la altura para que el iframe no tenga barra de desplazamiento propia.
 */
export function usePuenteEmbed(): PuenteEmbed {
  const embebida = useMemo(() => estaEmbebida(), []);
  const permitidos = useMemo(
    () => origenesPermitidos(import.meta.env.VITE_ORIGENES_CONTENEDOR, window.location.origin),
    [],
  );
  const origenContenedor = useMemo(() => (embebida ? origenDelContenedor(window, permitidos) : null), [embebida, permitidos]);
  const estado = useSesion((s) => s.estado);
  const sesion = useSesion((s) => s.sesion);

  const enviar = useMemo(
    () => (mensaje: MensajeSaliente) => {
      if (embebida && origenContenedor) window.parent.postMessage(mensaje, origenContenedor);
    },
    [embebida, origenContenedor],
  );

  useEffect(() => {
    if (!embebida) return undefined;

    const alRecibir = (evento: MessageEvent) => {
      if (evento.source !== window.parent || !origenPermitido(evento.origin, permitidos)) return;
      const mensaje = interpretarMensaje(evento.data);
      if (!mensaje) return;

      switch (mensaje.tipo) {
        case 'validasoft:sesion':
          useSesion.getState().establecer(mensaje.token, mensaje.empresaId);
          break;
        case 'validasoft:tema':
          usePreferencias.getState().fijarModo(mensaje.mode, true);
          break;
        case 'validasoft:cerrar-sesion':
          useSesion.getState().marcar('cerrada');
          break;
      }
    };

    window.addEventListener('message', alRecibir);
    enviar({ tipo: 'validasoft:ready', version: VERSION_PROTOCOLO });
    return () => window.removeEventListener('message', alRecibir);
  }, [embebida, permitidos, enviar]);

  // Pide sesión al arrancar sin ella y cada vez que vence o la API la rechaza. Mientras no llegue, la vuelve a pedir
  // cada pocos segundos: si el contenedor se demoró o el mensaje se perdió, la pantalla no queda esperando para siempre.
  useEffect(() => {
    const motivo = MOTIVO_POR_ESTADO[estado];
    if (!motivo) return undefined;
    enviar({ tipo: 'validasoft:sesion-requerida', motivo });
    const reintento = window.setInterval(() => enviar({ tipo: 'validasoft:sesion-requerida', motivo }), REINTENTO_SESION_MS);
    return () => window.clearInterval(reintento);
  }, [estado, enviar]);

  // Renovación anticipada y vencimiento local del token.
  useEffect(() => {
    if (!sesion) return undefined;
    const restante = msRestantes(sesion);
    const aviso = window.setTimeout(
      () => enviar({ tipo: 'validasoft:sesion-requerida', motivo: 'por-vencer' }),
      Math.max(0, restante - AVISO_RENOVACION_MS),
    );
    const vencimiento = window.setTimeout(() => {
      if (useSesion.getState().sesion === sesion) useSesion.getState().marcar('vencida');
    }, Math.max(0, restante));
    return () => {
      window.clearTimeout(aviso);
      window.clearTimeout(vencimiento);
    };
  }, [sesion, enviar]);

  // Altura del contenido para que el contenedor ajuste el iframe.
  useEffect(() => {
    if (!embebida || typeof ResizeObserver === 'undefined') return undefined;
    let ultima = 0;
    const observador = new ResizeObserver(() => {
      const altura = Math.ceil(document.documentElement.scrollHeight);
      if (altura !== ultima) {
        ultima = altura;
        enviar({ tipo: 'validasoft:resize', height: altura });
      }
    });
    observador.observe(document.body);
    return () => observador.disconnect();
  }, [embebida, enviar]);

  return { embebida, origenContenedor };
}
