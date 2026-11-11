import { IconButton, Tooltip } from '@mui/material';
import { Settings } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link as RouterLink } from 'react-router';

/**
 * Configuración como engranaje junto a la campana y el tema (docs/06 §3.3): sus secciones tienen barra lateral propia
 * (`LayoutConfiguracion`) y no compiten con las pestañas por el ancho. Activo en cualquier sección de Configuración.
 */
export function BotonConfiguracion({ destino, activo }: { destino: string; activo: boolean }) {
  const { t } = useTranslation();
  return (
    <Tooltip title={t('nav.configuracion')}>
      <IconButton
        component={RouterLink}
        to={destino}
        aria-label={t('nav.configuracion')}
        aria-current={activo ? 'page' : undefined}
        sx={{
          flex: 'none',
          ...(activo && { bgcolor: 'acento.tinte', color: 'acento.main', '&:hover': { bgcolor: 'acento.tinte', color: 'acento.main' } }),
        }}
      >
        <Settings size={18} strokeWidth={1.75} />
      </IconButton>
    </Tooltip>
  );
}
