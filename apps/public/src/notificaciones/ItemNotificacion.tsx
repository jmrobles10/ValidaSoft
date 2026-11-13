import { Box, ListItem, ListItemButton, Typography } from '@mui/material';
import type { NotificacionTerceroDto } from '@validasoft/api-client';
import { formatearFechaHora, Logo, visualmenteOculto } from '@validasoft/ui';
import { useTranslation } from 'react-i18next';
import { LogoEmpresa } from '../shared/componentes';
import { haceCuanto } from './bandeja';

/**
 * Una notificación de la campana: la marca de la empresa que la origina (logo y, si la tiene, su color en el borde), el título, el
 * texto y hace cuánto llegó. Las que no son de ninguna empresa (una propuesta desde un documento) llevan el ícono de Validasoft.
 */
export function ItemNotificacion({ notificacion, ahora, onAbrir }: { notificacion: NotificacionTerceroDto; ahora: number; onAbrir: (n: NotificacionTerceroDto) => void }) {
  const { t } = useTranslation();
  const sinLeer = !notificacion.leidaEn;
  const empresa = notificacion.empresa;
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
          borderLeft: 3,
          borderLeftColor: empresa?.colorMarca ?? 'transparent',
        }}
      >
        <Box sx={{ flex: 'none', mt: '2px' }}>{empresa ? <LogoEmpresa nombre={empresa.nombre} logoUrl={empresa.logoUrl} tamano={32} /> : <Logo variante="icono" tamano={28} />}</Box>
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
              sx={{ color: 'text.secondary', flex: 'none', fontSize: 12, lineHeight: '16px', whiteSpace: 'nowrap' }}
            >
              {haceCuanto(notificacion.creadaEn, ahora)}
            </Typography>
          </Box>
          <Typography component="span" sx={{ color: 'text.secondary', display: 'block', fontSize: 13, lineHeight: '18px', overflowWrap: 'anywhere' }}>
            {notificacion.mensaje}
          </Typography>
        </Box>
        <Box
          aria-hidden
          data-sin-leer={sinLeer || undefined}
          sx={{ flex: 'none', mt: '7px', width: 8, height: 8, borderRadius: '50%', bgcolor: sinLeer ? 'acento.main' : 'transparent' }}
        />
      </ListItemButton>
    </ListItem>
  );
}
