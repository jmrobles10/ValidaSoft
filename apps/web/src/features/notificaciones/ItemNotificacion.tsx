import { Box, ListItem, ListItemButton, Typography } from '@mui/material';
import type { NotificacionDto } from '@validasoft/api-client';
import { formatearFechaHora, visualmenteOculto } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { haceCuanto } from './tipos';

/** Una notificación de la campana: marca de no leída, título, texto y hace cuánto llegó. */
export function ItemNotificacion({ notificacion, ahora, onAbrir }: { notificacion: NotificacionDto; ahora: number; onAbrir: (n: NotificacionDto) => void }) {
  const { t } = useTranslation();
  const sinLeer = !notificacion.leidaEn;
  return (
    <ListItem disablePadding sx={{ borderBottom: 1, borderColor: 'divider' }}>
      <ListItemButton
        onClick={() => onAbrir(notificacion)}
        sx={{ alignItems: 'flex-start', gap: 1.25, px: 2, py: 1.25, bgcolor: sinLeer ? 'action.hover' : undefined }}
      >
        <Box
          aria-hidden
          data-sin-leer={sinLeer || undefined}
          sx={{ flex: 'none', mt: '7px', width: 8, height: 8, borderRadius: '50%', bgcolor: sinLeer ? 'acento.main' : 'transparent' }}
        />
        <Box sx={{ minWidth: 0, flex: 1 }}>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1 }}>
            <Typography component="span" sx={{ flex: 1, minWidth: 0, fontSize: 14, lineHeight: '20px', fontWeight: sinLeer ? 600 : 500 }}>
              {sinLeer && <Box component="span" sx={visualmenteOculto}>{`${t('notificaciones.sinLeer')}: `}</Box>}
              {notificacion.titulo}
            </Typography>
            <Typography
              component="time"
              dateTime={notificacion.creadaEn}
              title={formatearFechaHora(notificacion.creadaEn)}
              color="text.secondary"
              sx={{ flex: 'none', fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}
            >
              {haceCuanto(notificacion.creadaEn, ahora)}
            </Typography>
          </Box>
          <Typography component="span" color="text.secondary" sx={{ display: 'block', fontSize: 13, lineHeight: '18px', overflowWrap: 'anywhere' }}>
            {notificacion.mensaje}
          </Typography>
        </Box>
      </ListItemButton>
    </ListItem>
  );
}
