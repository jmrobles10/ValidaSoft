import { Box } from '@mui/material';
import { Logo } from '@validasoft/ui';
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
        <Logo tamano={20} titulo={t('app.nombre')} />
      </Box>
      {children}
    </Box>
  );
}
