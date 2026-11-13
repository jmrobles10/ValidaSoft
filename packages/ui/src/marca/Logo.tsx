import { Box, useTheme } from '@mui/material';
import { FUENTE, type Modo } from '../tokens';
import {
  ALTO_V,
  ARO,
  ARO_ABIERTO,
  CAJA_ICONO,
  CAJA_V,
  CHECK_RELLENO,
  coloresDeLogo,
  FAVICON,
  MARGEN_V,
  TEXTO_LOGOTIPO,
  TRANSFORMACION_CHECK_ICONO,
} from './geometria';

export type VarianteLogo = 'logotipo' | 'icono' | 'favicon';

export interface LogoProps {
  /**
   * `logotipo` (por omisión): la «V» caligráfica y «alidaSoft», sin aro; va en barras, ingreso, correos y documentos.
   * `icono`: el aro abierto con el check; va donde solo cabe un símbolo (app instalada, avatar de la marca, notificaciones).
   * `favicon`: el círculo verde lleno con el check blanco, que se lee bien a 16 px.
   */
  variante?: VarianteLogo;
  /**
   * Logotipo: tamaño de la letra en px (la «V» mide lo mismo; por omisión 20). Ícono y favicon: lado del cuadrado en px (por omisión 24).
   */
  tamano?: number;
  /** Fuerza los colores de un modo (sobre un fondo que no sigue al tema). Por omisión, el del tema. */
  modo?: Modo;
  /** Nombre accesible. Por omisión «Validasoft». */
  titulo?: string;
  /** Solo decorativo: lo oculta a los lectores de pantalla (cuando el nombre ya está en texto al lado). */
  decorativo?: boolean;
}

/** Atributos de accesibilidad del logo: con nombre (`role="img"`) o decorativo. */
function accesibilidad(titulo: string, decorativo: boolean) {
  return decorativo ? { 'aria-hidden': true as const } : { role: 'img' as const, 'aria-label': titulo };
}

/** El ícono de la app: aro abierto y check. */
function Icono({ lado, colores, titulo, decorativo }: { lado: number; colores: ReturnType<typeof coloresDeLogo>; titulo: string; decorativo: boolean }) {
  return (
    <svg
      width={lado}
      height={lado}
      viewBox={`${String(CAJA_ICONO.x)} ${String(CAJA_ICONO.y)} ${String(CAJA_ICONO.ancho)} ${String(CAJA_ICONO.alto)}`}
      focusable="false"
      style={{ flex: 'none', display: 'block' }}
      {...accesibilidad(titulo, decorativo)}
    >
      <path d={ARO_ABIERTO} fill="none" stroke={colores.aro} strokeWidth={ARO.grosor} strokeLinecap="round" />
      <path d={CHECK_RELLENO} fill={colores.check} transform={TRANSFORMACION_CHECK_ICONO} />
    </svg>
  );
}

/** El favicon: círculo verde con el check blanco dentro. */
function Favicon({ lado, colores, titulo, decorativo }: { lado: number; colores: ReturnType<typeof coloresDeLogo>; titulo: string; decorativo: boolean }) {
  return (
    <svg width={lado} height={lado} viewBox="0 0 100 100" focusable="false" style={{ flex: 'none', display: 'block' }} {...accesibilidad(titulo, decorativo)}>
      <circle cx={ARO.cx} cy={ARO.cy} r={FAVICON.radio} fill={colores.check} />
      <path d={CHECK_RELLENO} fill={colores.blanco} stroke={colores.blanco} strokeWidth={FAVICON.contorno} strokeLinejoin="round" transform={FAVICON.transformacion} />
    </svg>
  );
}

/**
 * Logo de Validasoft (MA-1, docs/marca.md). Cambia solo con el modo del tema (o con `modo`) y no toma el color de la empresa.
 * El nombre accesible va en el contenedor; el dibujo y el texto suelto se ocultan para que no se lea «alida Soft».
 */
export function Logo({ variante = 'logotipo', tamano, modo, titulo = 'Validasoft', decorativo = false }: LogoProps) {
  const tema = useTheme();
  const colores = coloresDeLogo(modo ?? (tema.palette.mode === 'dark' ? 'dark' : 'light'));

  if (variante === 'icono') return <Icono lado={tamano ?? 24} colores={colores} titulo={titulo} decorativo={decorativo} />;
  if (variante === 'favicon') return <Favicon lado={tamano ?? 24} colores={colores} titulo={titulo} decorativo={decorativo} />;

  const letra = tamano ?? 20;
  const alto = ALTO_V * letra;
  const ancho = (alto * CAJA_V.ancho) / CAJA_V.alto;
  return (
    <Box
      component="span"
      {...accesibilidad(titulo, decorativo)}
      sx={{ display: 'inline-flex', alignItems: 'baseline', flex: 'none', fontFamily: FUENTE.sans, fontSize: `${String(letra)}px`, lineHeight: 1, letterSpacing: '-0.015em', whiteSpace: 'nowrap', userSelect: 'none' }}
    >
      {/* La «V»: el check del alto de la letra, sobre la línea base y muy pegado a la «a» (su cola se mete bajo ella). */}
      <Box component="span" aria-hidden data-parte="v" sx={{ display: 'inline-block', width: `${String(ancho)}px`, height: `${String(alto)}px`, marginRight: `${String(MARGEN_V * letra)}px`, flex: 'none' }}>
        <svg viewBox={`${String(CAJA_V.x)} ${String(CAJA_V.y)} ${String(CAJA_V.ancho)} ${String(CAJA_V.alto)}`} width="100%" height="100%" aria-hidden focusable="false" style={{ display: 'block', overflow: 'visible' }}>
          <path d={CHECK_RELLENO} fill={colores.check} />
        </svg>
      </Box>
      <Box component="span" aria-hidden data-parte="texto" sx={{ color: colores.texto, fontWeight: 500 }}>
        {TEXTO_LOGOTIPO.cuerpo}
        <Box component="span" sx={{ color: colores.soft, fontWeight: 400 }}>
          {TEXTO_LOGOTIPO.marca}
        </Box>
      </Box>
    </Box>
  );
}
