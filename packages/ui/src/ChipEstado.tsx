import { Box, Chip, Tooltip, useTheme } from '@mui/material';
import { Sparkles } from 'lucide-react';
import { visualmenteOculto } from './accesibilidad';
import { aparienciaDe, type Estado, type TonoEstado } from './estados';
import { formatearFecha } from './formato';

export interface ChipEstadoProps {
  estado: Estado;
  /** Fecha que da contexto al estado; va al tooltip y al texto para lectores de pantalla ("Venció el 28/09/2026"). */
  fecha?: string | Date | null;
  /** Lo decidió `Sistema (IA)`: agrega la chispa sin quitar el ícono del estado. */
  automatico?: boolean;
  /** `ConPendientes` informativo → bloqueante (tono error y octágono). */
  bloqueante?: boolean;
  /** small = web (24 px), medium = public (28 px). */
  size?: 'small' | 'medium';
  /**
   * Texto en vez del canónico del estado, para decirle al tercero lo que le toca («Faltan 3 documentos»); el ícono y
   * el tono siguen siendo los del estado.
   */
  etiqueta?: string;
}

/** Tono final teniendo en cuenta `bloqueante`. */
export function tonoDe(estado: Estado, bloqueante = false): TonoEstado {
  return aparienciaDe(estado, bloqueante).tono;
}

/** Detalle del tooltip: fecha con el verbo del estado, bloqueante y decisión automática. `null` si no hay nada que decir. */
export function textoTooltip(estado: Estado, fecha?: string | Date | null, automatico = false, bloqueante = false): string | null {
  const { verboFecha } = aparienciaDe(estado, bloqueante);
  const partes = [
    fecha ? `${verboFecha ?? 'Desde el'} ${formatearFecha(fecha)}` : null,
    bloqueante ? 'Bloqueante' : null,
    automatico ? 'Decidido por Sistema (IA)' : null,
  ].filter((x): x is string => x !== null);
  return partes.length > 0 ? partes.join(' · ') : null;
}

/**
 * Estado de cualquier entidad (dirección Registro): contorno sutil, texto primario y solo el ícono en el tono del
 * estado. Se lee sin color: cada estado tiene su ícono y su texto.
 */
export function ChipEstado({ estado, fecha = null, automatico = false, bloqueante = false, size = 'small', etiqueta: texto }: ChipEstadoProps) {
  const tema = useTheme();
  const { etiqueta: canonica, tono, icono: Icono } = aparienciaDe(estado, bloqueante);
  const etiqueta = texto ?? canonica;
  const tamIcono = size === 'small' ? 14 : 16;
  const detalle = textoTooltip(estado, fecha, automatico, bloqueante);

  const chip = (
    <Chip
      variant="outlined"
      size={size}
      data-estado={estado}
      icon={<Icono size={tamIcono} strokeWidth={1.75} aria-hidden />}
      label={
        <Box component="span" sx={{ display: 'inline-flex', alignItems: 'center', gap: 0.5 }}>
          {etiqueta}
          {automatico && <Sparkles size={tamIcono} strokeWidth={1.75} aria-hidden color={tema.palette.estado.info.texto} />}
          {detalle && <Box component="span" sx={visualmenteOculto}>{`. ${detalle}`}</Box>}
        </Box>
      }
      sx={{
        color: 'text.primary',
        borderColor: 'borde.sutil',
        borderRadius: `${String(tema.radio.xs)}px`,
        '& .MuiChip-icon': { color: tema.palette.estado[tono].texto },
      }}
    />
  );

  return detalle ? (
    <Tooltip title={detalle} describeChild>
      {chip}
    </Tooltip>
  ) : (
    chip
  );
}
