import { Badge, IconButton, Popover, Tooltip } from '@mui/material';
import { Bell } from 'lucide-react';
import type { ReactNode } from 'react';
import { textoContador } from './formato';

export interface CampanaDeNotificacionesProps {
  noLeidas: number;
  /** Nombre accesible del botón (ya con el contador si lo hay). */
  etiqueta: string;
  /** Texto del tooltip del botón. */
  titulo: string;
  /** El botón que abrió el panel; `null` = cerrado. Lo lleva la app porque abrir un aviso o las preferencias también lo cierra. */
  ancla: HTMLElement | null;
  onAnclar: (ancla: HTMLElement | null) => void;
  /** Tamaño del ícono de la campana (en px). */
  tamanoIcono: number;
  /** Ancho del panel (en px; nunca más que la ventana menos 16 px). */
  ancho: number;
  /** Alto máximo del panel (una expresión CSS, p. ej. `min(520px, calc(100vh - 64px))`). */
  alturaMaxima: string;
  /** El contenido del panel (`PanelDeNotificaciones`). */
  children: ReactNode;
}

/** Campana de la barra superior: botón con el contador de no leídas y, al pulsarlo, el panel en un popover. */
export function CampanaDeNotificaciones({ noLeidas, etiqueta, titulo, ancla, onAnclar, tamanoIcono, ancho, alturaMaxima, children }: CampanaDeNotificacionesProps) {
  return (
    <>
      <Tooltip title={titulo}>
        <IconButton
          aria-label={etiqueta}
          aria-haspopup="dialog"
          aria-expanded={ancla !== null}
          onClick={(e) => onAnclar(e.currentTarget)}
          sx={{ flex: 'none' }}
        >
          <Badge
            badgeContent={textoContador(noLeidas)}
            invisible={noLeidas === 0}
            color="error"
            sx={{ '& .MuiBadge-badge': { fontSize: 11, height: 16, minWidth: 16, px: 0.5 } }}
          >
            <Bell size={tamanoIcono} strokeWidth={1.75} />
          </Badge>
        </IconButton>
      </Tooltip>
      <Popover
        open={ancla !== null}
        anchorEl={ancla}
        onClose={() => onAnclar(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
        transformOrigin={{ vertical: 'top', horizontal: 'right' }}
        slotProps={{
          paper: {
            role: 'dialog',
            'aria-labelledby': 'titulo-notificaciones',
            sx: { width: ancho, maxWidth: 'calc(100vw - 16px)', maxHeight: alturaMaxima, display: 'flex', flexDirection: 'column' },
          },
        }}
      >
        {children}
      </Popover>
    </>
  );
}
