import { Avatar, Box, Stack, Tooltip, Typography, type SxProps, type Theme } from '@mui/material';
import { Building2, Plug, Sparkles } from 'lucide-react';
import type { ReactNode } from 'react';

/** Rejilla de datos etiqueta/valor (`dl`): 3 columnas en web, 1 en pantallas angostas. */
export function RejillaDatos({ children, columnas = 3 }: { children: ReactNode; columnas?: 2 | 3 }) {
  return (
    <Box
      component="dl"
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: 'minmax(0, 1fr)', sm: 'repeat(2, minmax(0, 1fr))', lg: `repeat(${String(columnas)}, minmax(0, 1fr))` },
        gap: '14px 20px',
        m: 0,
      }}
    >
      {children}
    </Box>
  );
}

export function Dato({ etiqueta, children, ancho = false }: { etiqueta: string; children: ReactNode; ancho?: boolean }) {
  return (
    <Stack spacing={0.25} sx={{ minWidth: 0, gridColumn: ancho ? '1 / -1' : undefined }}>
      <Typography component="dt" variant="caption" sx={{ color: 'text.secondary', fontWeight: 400 }}>
        {etiqueta}
      </Typography>
      <Typography component="dd" variant="body1" sx={{ m: 0, overflowWrap: 'anywhere' }}>
        {children}
      </Typography>
    </Stack>
  );
}

/**
 * Un valor que puede ser muy largo (un correo, una referencia, un identificador) dentro de un `Dato`: nunca se parte a mitad de la palabra.
 * Con `partirEn="@"` un correo que no cabe en una línea se parte justo después de la «@» (el usuario arriba y el dominio abajo); si aun así una
 * parte no cabe, se recorta con puntos suspensivos. El valor completo está en el tooltip, y el texto sigue siendo seleccionable para copiarlo.
 * Con `mono` usa la fuente de los identificadores.
 */
export function ValorLargo({ valor, partirEn, mono = false }: { valor: string; partirEn?: string; mono?: boolean }) {
  const corte = partirEn ? valor.indexOf(partirEn) : -1;
  const partes = partirEn && corte > 0 ? [valor.slice(0, corte + partirEn.length), valor.slice(corte + partirEn.length)] : [valor];
  return (
    <Tooltip title={valor} placement="top-start" enterDelay={500} describeChild>
      <Box
        component="span"
        sx={{ display: 'flex', flexWrap: 'wrap', minWidth: 0, maxWidth: '100%', ...(mono && { typography: 'mono' }) }}
      >
        {partes.map((parte) => (
          <Box key={parte} component="span" sx={{ display: 'block', minWidth: 0, maxWidth: '100%', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
            {parte}
          </Box>
        ))}
      </Box>
    </Tooltip>
  );
}

/** Referencia externa en una celda: "SRM · PRV-002". */
export function ReferenciaExterna({ sistema, id }: { sistema: string; id: string }) {
  return (
    <Box
      component="span"
      sx={{
        display: 'inline-block',
        px: 0.75,
        py: '1px',
        borderRadius: (t) => `${String(t.radio.xs)}px`,
        bgcolor: 'superficie.nivel2',
        color: 'text.secondary',
        fontSize: 12,
        lineHeight: '18px',
        whiteSpace: 'nowrap',
      }}
    >
      {sistema} · <Box component="span" sx={{ fontFamily: (t) => t.typography.mono.fontFamily }}>{id}</Box>
    </Box>
  );
}

export type TipoAvatar = 'persona' | 'tercero' | 'sistema' | 'integracion';

/** Iniciales de un nombre: "Ana Administradora" → "AA". */
export function iniciales(nombre: string): string {
  const partes = nombre.trim().split(/\s+/).filter(Boolean);
  const [primera, segunda] = [partes[0] ?? '', partes.length > 1 ? (partes.at(-1) ?? '') : ''];
  return (primera.charAt(0) + segunda.charAt(0)).toUpperCase() || '?';
}

/**
 * Avatar por tipo de actor: persona (iniciales, redondo), `Sistema (IA)` (chispa en tono info), integración (enchufe)
 * y tercero (edificio). Decorativo: el nombre siempre va en texto al lado.
 */
export function AvatarActor({ tipo, nombre = '', tamano = 28 }: { tipo: TipoAvatar; nombre?: string; tamano?: number }) {
  const icono = Math.round(tamano * 0.55);
  const base = { width: tamano, height: tamano, fontSize: Math.round(tamano * 0.42) };
  if (tipo === 'persona') {
    return (
      <Avatar aria-hidden sx={base}>
        {iniciales(nombre)}
      </Avatar>
    );
  }
  const cuadrado = (t: Theme) => `${String(t.radio.sm)}px`;
  const estilos: Record<Exclude<TipoAvatar, 'persona'>, SxProps<Theme>> = {
    sistema: { ...base, bgcolor: 'estado.info.fondo', color: 'estado.info.texto' },
    integracion: { ...base, borderRadius: cuadrado, bgcolor: 'superficie.nivel2', color: 'text.secondary' },
    tercero: { ...base, borderRadius: cuadrado, bgcolor: 'estado.neutro.fondo', color: 'estado.neutro.texto' },
  };
  const Icono = tipo === 'sistema' ? Sparkles : tipo === 'integracion' ? Plug : Building2;
  return (
    <Avatar aria-hidden sx={estilos[tipo]}>
      <Icono size={icono} strokeWidth={1.75} />
    </Avatar>
  );
}
