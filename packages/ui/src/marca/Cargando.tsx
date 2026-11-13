import { Box, keyframes, useTheme } from '@mui/material';
import { useId } from 'react';
import type { Modo } from '../tokens';
import { ARCO_DE_CARGA, ARO, CAJA_ICONO, CHECK_RELLENO, coloresDeLogo, GROSOR_GUIA, GUIA_CHECK, LONGITUD_GUIA, TIEMPOS_CARGA, TRANSFORMACION_CHECK_ICONO } from './geometria';

const girar = keyframes`from { transform: rotate(0deg); } to { transform: rotate(360deg); }`;
// El check se revela (0 → 45 %), se queda (hasta 60 %) y se oculta de principio a fin (60 → 100 %).
const trazar = keyframes`
  0% { stroke-dashoffset: ${String(LONGITUD_GUIA)}; }
  45% { stroke-dashoffset: 0; }
  60% { stroke-dashoffset: 0; }
  100% { stroke-dashoffset: -${String(LONGITUD_GUIA)}; }
`;
const pulso = keyframes`0%, 100% { opacity: 1; } 50% { opacity: 0.55; }`;

export interface CargandoProps {
  /** Lado en px. Por omisión 48. */
  tamano?: number;
  /** Fuerza los colores de un modo. Por omisión, el del tema. */
  modo?: Modo;
  /** Nombre accesible (`role="status"`). Por omisión «Cargando». */
  etiqueta?: string;
  /** Solo decorativa: cuando la región que la contiene ya es el estado y dice qué se espera (no repite el anuncio). */
  decorativa?: boolean;
}

/**
 * Carga de marca (MA-1): el aro tenue, un arco del color del aro que gira (1 s, lineal) y el check que se revela y se oculta (1,4 s).
 * Con `prefers-reduced-motion` queda estática con un pulso suave de opacidad. Es para esperas largas sin forma que mostrar (una
 * conexión que se abre, un documento que se genera); donde se sabe qué va a llegar, la carga es un esqueleto (UI-1).
 */
export function Cargando({ tamano = 48, modo, etiqueta = 'Cargando', decorativa = false }: CargandoProps) {
  const tema = useTheme();
  const c = coloresDeLogo(modo ?? (tema.palette.mode === 'dark' ? 'dark' : 'light'));
  const mascara = useId();
  return (
    <Box
      component="svg"
      {...(decorativa ? { 'aria-hidden': true } : { role: 'status', 'aria-label': etiqueta })}
      width={tamano}
      height={tamano}
      viewBox={`${String(CAJA_ICONO.x)} ${String(CAJA_ICONO.y)} ${String(CAJA_ICONO.ancho)} ${String(CAJA_ICONO.alto)}`}
      focusable="false"
      sx={{
        display: 'block',
        flex: 'none',
        '& .giro': { transformOrigin: '50px 50px', animation: `${girar} ${String(TIEMPOS_CARGA.vuelta)}ms linear infinite` },
        '& .guia': { strokeDasharray: `${String(LONGITUD_GUIA)} ${String(LONGITUD_GUIA)}`, strokeDashoffset: LONGITUD_GUIA, animation: `${trazar} ${String(TIEMPOS_CARGA.check)}ms ease-in-out infinite` },
        '@media (prefers-reduced-motion: reduce)': {
          animation: `${pulso} 2s ease-in-out infinite`,
          '& .giro': { animation: 'none' },
          '& .guia': { animation: 'none', strokeDashoffset: 0 },
        },
      }}
    >
      <defs>
        <mask id={mascara} maskUnits="userSpaceOnUse" x="-20" y="-20" width="140" height="140">
          <path className="guia" d={GUIA_CHECK} fill="none" stroke="#FFFFFF" strokeWidth={GROSOR_GUIA} strokeLinecap="round" strokeLinejoin="round" />
        </mask>
      </defs>
      <circle cx={ARO.cx} cy={ARO.cy} r={ARO.r} fill="none" stroke={c.aro} strokeWidth={ARO.grosor} opacity={0.2} />
      <circle
        className="giro"
        cx={ARO.cx}
        cy={ARO.cy}
        r={ARO.r}
        fill="none"
        stroke={c.aro}
        strokeWidth={ARO.grosor}
        strokeLinecap="round"
        strokeDasharray={`${String(ARCO_DE_CARGA.visible)} ${String(ARCO_DE_CARGA.hueco)}`}
      />
      <g transform={TRANSFORMACION_CHECK_ICONO}>
        <path d={CHECK_RELLENO} fill={c.check} mask={`url(#${mascara})`} />
      </g>
    </Box>
  );
}
