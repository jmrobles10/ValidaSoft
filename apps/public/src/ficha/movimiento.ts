import { keyframes } from '@emotion/react';

/**
 * Movimiento del wizard: la sección entra deslizándose apenas al cambiar y el estado de una sección «salta» cuando cambia.
 * Con «reducir movimiento» el tema deja las animaciones en 1 ms.
 */
export const entradaSeccion = keyframes`
  from { opacity: 0; transform: translateY(10px); }
  to { opacity: 1; transform: none; }
`;

export const cambioDeEstado = keyframes`
  0% { transform: scale(0.4); opacity: 0; }
  70% { transform: scale(1.15); opacity: 1; }
  100% { transform: scale(1); }
`;

export const aparicion = keyframes`
  from { opacity: 0; }
  to { opacity: 1; }
`;

/** Alto de la cabecera de la app (56 px más su borde): bajo ella se fijan el panel y el título de la sección. */
export const ALTO_CABECERA = 57;

/**
 * Margen superior del contenido en el Shell (`pt` 1.5 en el celular y 2.5 desde sm). Un elemento fijo se pega exactamente
 * donde empieza (cabecera + margen) para no correrse unos píxeles antes de quedarse quieto.
 */
export const MARGEN_SUPERIOR = { xs: 12, sm: 20 } as const;
