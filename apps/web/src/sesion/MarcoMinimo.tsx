import { Box, Typography } from '@mui/material';
import { Isotipo } from '@validasoft/ui';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/** Cabecera mínima (sin empresa ni secciones) mientras no hay sesión. */
export function MarcoMinimo({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  return (
    <Box sx={{ minHeight: '100vh', bgcolor: 'background.default' }}>
      <Box
        component="header"
        sx={{ display: 'flex', alignItems: 'center', gap: 1, height: (th) => th.densidad.cabeceraEmbebida, px: 2, borderBottom: 1, borderColor: 'divider', bgcolor: 'background.paper' }}
      >
        <Isotipo />
        <Typography component="span" sx={{ fontSize: 15, fontWeight: 600, letterSpacing: '-0.01em' }}>
          {t('app.nombre')}
        </Typography>
      </Box>
      {children}
    </Box>
  );
}
