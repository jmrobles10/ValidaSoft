import { Box, keyframes, useTheme } from '@mui/material';
import { useEffect, useId, useRef } from 'react';
import type { Modo } from '../tokens';
import { ARO, CHECK_RELLENO, coloresDeLogo, DURACION_EXITO, GROSOR_GUIA, GUIA_CHECK, LONGITUD_ARO, LONGITUD_GUIA, RADIO_RELLENO, TIEMPOS_EXITO, TRANSFORMACION_CHECK_DENTRO } from './geometria';
import { useMovimientoReducido, usePrimeraVez } from './movimiento';

const dibujarAro = keyframes`from { stroke-dashoffset: ${String(LONGITUD_ARO)}; } to { stroke-dashoffset: 0; }`;
const llenar = keyframes`from { fill-opacity: 0; } to { fill-opacity: 1; }`;
const revelar = keyframes`from { stroke-dashoffset: ${String(LONGITUD_GUIA)}; } to { stroke-dashoffset: 0; }`;
const rebote = keyframes`0% { transform: scale(0.82); } 60% { transform: scale(1.06); } 100% { transform: scale(1); }`;
const pop = keyframes`0% { transform: scale(1); } 50% { transform: scale(1.1); } 100% { transform: scale(1); }`;

const t = TIEMPOS_EXITO;
const retrasoRelleno = t.aro;
const retrasoCheck = t.aro + t.relleno;
const retrasoPop = t.aro + t.relleno + t.check;

export interface AnimacionExitoProps {
  /** Lado en px. Por omisión 96. */
  tamano?: number;
  /** Fuerza los colores de un modo. Por omisión, el del tema. */
  modo?: Modo;
  /** Nombre accesible (`role="img"`). Por omisión «Listo». */
  etiqueta?: string;
  /** Solo decorativa (el mensaje ya está en texto al lado). */
  decorativa?: boolean;
  /** `false` la muestra ya terminada, sin animar (por omisión `true`). */
  animar?: boolean;
  /** Se llama una vez al terminar el «pop» (o, sin movimiento, enseguida de mostrar el éxito). */
  onFin?: () => void;
}

/**
 * Éxito de marca (MA-1): el aro se dibuja en verde (0,45 s), el círculo se llena (0,3 s), el check se revela en blanco, dentro del círculo, siguiendo su
 * trazo (0,4 s, con un leve rebote) y todo hace un «pop» 1 → 1,1 → 1. Con `prefers-reduced-motion` aparece ya terminado.
 * Úsala en momentos que la persona provocó y que cierran algo (enviar, aprobar, certificado emitido), no en cada confirmación.
 */
export function AnimacionExito({ tamano = 96, modo, etiqueta = 'Listo', decorativa = false, animar = true, onFin }: AnimacionExitoProps) {
  const tema = useTheme();
  const c = coloresDeLogo(modo ?? (tema.palette.mode === 'dark' ? 'dark' : 'light'));
  const reducido = useMovimientoReducido() || !animar;
  const mascara = useId();
  const aviso = useRef(onFin);
  aviso.current = onFin;
  const avisado = useRef(false);
  const avisar = () => {
    if (avisado.current) return;
    avisado.current = true;
    aviso.current?.();
  };

  // Sin movimiento no hay «pop» que termine: se avisa poco después de mostrar el resultado.
  useEffect(() => {
    if (!reducido) return;
    const espera = window.setTimeout(avisar, 400);
    return () => window.clearTimeout(espera);
  }, [reducido]);

  const accesible = decorativa ? { 'aria-hidden': true as const } : { role: 'img' as const, 'aria-label': etiqueta };
  const sinAnimar = reducido ? { animation: 'none !important' } : {};

  return (
    <Box
      component="svg"
      width={tamano}
      height={tamano}
      viewBox="0 0 100 100"
      focusable="false"
      data-duracion={DURACION_EXITO}
      {...accesible}
      sx={{
        display: 'block',
        flex: 'none',
        overflow: 'visible',
        '& .pop': { transformOrigin: '50px 50px', animation: `${pop} ${String(t.pop)}ms ease-in-out ${String(retrasoPop)}ms both`, ...sinAnimar },
        '& .aro': { transform: 'rotate(-90deg)', transformOrigin: '50px 50px', strokeDasharray: LONGITUD_ARO, animation: `${dibujarAro} ${String(t.aro)}ms cubic-bezier(0.4, 0, 0.2, 1) both`, ...sinAnimar },
        '& .relleno': { animation: `${llenar} ${String(t.relleno)}ms ease-out ${String(retrasoRelleno)}ms both`, ...sinAnimar },
        '& .guia': { strokeDasharray: `${String(LONGITUD_GUIA)} ${String(LONGITUD_GUIA)}`, animation: `${revelar} ${String(t.check)}ms cubic-bezier(0.22, 1, 0.36, 1) ${String(retrasoCheck)}ms both`, ...sinAnimar },
        '& .check': { transformOrigin: '50px 50px', animation: `${rebote} ${String(t.check)}ms cubic-bezier(0.34, 1.56, 0.64, 1) ${String(retrasoCheck)}ms both`, ...sinAnimar },
      }}
    >
      <defs>
        <mask id={mascara} maskUnits="userSpaceOnUse" x="-20" y="-20" width="140" height="140">
          <path className="guia" d={GUIA_CHECK} fill="none" stroke="#FFFFFF" strokeWidth={GROSOR_GUIA} strokeLinecap="round" strokeLinejoin="round" />
        </mask>
      </defs>
      <g className="pop" onAnimationEnd={avisar}>
        <circle className="relleno" cx={ARO.cx} cy={ARO.cy} r={RADIO_RELLENO} fill={c.check} />
        <circle className="aro" cx={ARO.cx} cy={ARO.cy} r={ARO.r} fill="none" stroke={c.check} strokeWidth={ARO.grosor} />
        <g className="check">
          <g transform={TRANSFORMACION_CHECK_DENTRO}>
            <path d={CHECK_RELLENO} fill={c.blanco} mask={`url(#${mascara})`} />
          </g>
        </g>
      </g>
    </Box>
  );
}

/**
 * Éxito que se anima solo la primera vez que `clave` se ve en la sesión del navegador: una pantalla a la que se vuelve seguido no repite
 * la celebración, y desde la segunda vez muestra el éxito ya terminado (el mismo dibujo, sin movimiento).
 */
export function AnimacionExitoUnaVez({ clave, ...resto }: Omit<AnimacionExitoProps, 'animar'> & { clave: string }) {
  const primera = usePrimeraVez(clave);
  return <AnimacionExito {...resto} animar={primera} />;
}
