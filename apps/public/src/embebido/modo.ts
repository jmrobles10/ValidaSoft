import { estaEmbebida } from '@validasoft/sdk';

const CLAVE = 'validasoft:embebido';

/**
 * ¿La app corre dentro del iframe de un sistema integrador (IO-5)? Se decide al cargar: dentro de un iframe y entrando por `/embed/…`
 * (o ya habiendo entrado así en esta pestaña: las pantallas siguen en modo embebido mientras la persona navega adentro). Fuera de un
 * iframe, `/embed/…` no hace nada especial: es la app normal.
 */
export function detectarModoEmbebido(ventana: Window = window): boolean {
  if (!estaEmbebida(ventana)) return false;
  try {
    if (ventana.location.pathname.startsWith('/embed/')) {
      ventana.sessionStorage.setItem(CLAVE, '1');
      return true;
    }
    return ventana.sessionStorage.getItem(CLAVE) === '1';
  } catch {
    return ventana.location.pathname.startsWith('/embed/');
  }
}

export const MODO_EMBEBIDO = typeof window !== 'undefined' && detectarModoEmbebido();
