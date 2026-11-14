import { Box, ListItem, ListItemButton, Typography } from '@mui/material';
import type { ReactNode } from 'react';
import { visualmenteOculto } from '../accesibilidad';
import { formatearFechaHora } from '../formato';
import { haceCuanto } from './formato';

/** Lo que el panel muestra de una notificación (la de la web de empresa y la del tercero traen más campos, que no se usan aquí). */
export interface NotificacionVisible {
  id: string;
  titulo: string;
  mensaje: string;
  creadaEn: string;
  leidaEn: string | null;
}

/** La empresa que originó el aviso (portal del tercero): su logo a la izquierda y, si la tiene, su color en el borde. */
export interface MarcaDeNotificacion {
  icono: ReactNode;
  color?: string | null;
}

export interface ItemDeNotificacionProps<N extends NotificacionVisible> {
  notificacion: N;
  ahora: number;
  onAbrir: (notificacion: N) => void;
  /** Lo que leen los lectores de pantalla antes del título de una no leída («Sin leer»). */
  textoSinLeer: string;
  /** Sin marca, el punto de «sin leer» va primero; con marca va al final, después del texto. */
  marca?: MarcaDeNotificacion;
}

function PuntoSinLeer({ sinLeer }: { sinLeer: boolean }) {
  return (
    <Box
      aria-hidden
      data-sin-leer={sinLeer || undefined}
      sx={{ flex: 'none', mt: '7px', width: 8, height: 8, borderRadius: '50%', bgcolor: sinLeer ? 'acento.main' : 'transparent' }}
    />
  );
}

/** Una notificación de la campana: marca de no leída, título, texto y hace cuánto llegó (con la marca de la empresa, si la trae). */
export function ItemDeNotificacion<N extends NotificacionVisible>({ notificacion, ahora, onAbrir, textoSinLeer, marca }: ItemDeNotificacionProps<N>) {
  const sinLeer = !notificacion.leidaEn;
  return (
    <ListItem disablePadding sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <ListItemButton
        onClick={() => onAbrir(notificacion)}
        sx={{
          alignItems: 'flex-start',
          gap: 1.25,
          px: 2,
          py: 1.25,
          bgcolor: sinLeer ? 'action.hover' : undefined,
          ...(marca ? { borderLeft: 3, borderLeftColor: marca.color ?? 'transparent' } : {}),
        }}
      >
        {marca ? <Box sx={{ flex: 'none', mt: '2px' }}>{marca.icono}</Box> : <PuntoSinLeer sinLeer={sinLeer} />}
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
            <Typography component="span" sx={{ flex: 1, minWidth: 0, fontSize: 14, lineHeight: '20px', fontWeight: sinLeer ? 600 : 500 }}>
              {sinLeer && <Box component="span" sx={visualmenteOculto}>{`${textoSinLeer}: `}</Box>}
              {notificacion.titulo}
            </Typography>
            <Typography
              component="time"
              dateTime={notificacion.creadaEn}
              title={formatearFechaHora(notificacion.creadaEn)}
              sx={{ color: 'text.secondary', flex: 'none', fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}
            >
              {haceCuanto(notificacion.creadaEn, ahora)}
            </Typography>
          </Box>
          <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13, lineHeight: '18px', overflowWrap: 'anywhere' }}>
            {notificacion.mensaje}
          </Typography>
        </Box>
        {marca && <PuntoSinLeer sinLeer={sinLeer} />}
      </ListItemButton>
    </ListItem>
  );
}
