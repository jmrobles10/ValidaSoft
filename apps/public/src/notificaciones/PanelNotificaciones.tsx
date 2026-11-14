import { Box, Button, IconButton, List, Skeleton, Stack, Tooltip, Typography } from '@mui/material';
import type { BandejaNotificacionesTerceroDto, NotificacionTerceroDto } from '@validasoft/api-client';
import { ErrorEnLinea, EstadoVacio } from '@validasoft/ui';
import { BellOff, Settings } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { ItemNotificacion } from './ItemNotificacion';
import { useAhora } from './useAhora';

export interface PanelNotificacionesProps {
  bandeja: BandejaNotificacionesTerceroDto | undefined;
  cargando: boolean;
  error: string | null;
  onReintentar: () => void;
  onAbrir: (notificacion: NotificacionTerceroDto) => void;
  onMarcarTodas: () => void;
  /** Sin él no se ofrecen las preferencias (son del tercero entero: una sesión aislada no las alcanza). */
  onPreferencias?: () => void;
}

/** Contenido del panel de la campana: cabecera con acciones, lista (o esqueleto, error o vacío) y aviso al pie. */
export function PanelNotificaciones({ bandeja, cargando, error, onReintentar, onAbrir, onMarcarTodas, onPreferencias }: PanelNotificacionesProps) {
  const { t } = useTranslation();
  const ahora = useAhora();
  const noLeidas = bandeja?.noLeidas ?? 0;

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
      <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flex: 'none', pl: 2, pr: 1, py: 1, borderBottom: 1, borderColor: 'divider' }}>
        <Typography id="titulo-notificaciones" component="h2" sx={{ flex: 1, fontSize: 15, lineHeight: '22px', fontWeight: 600 }}>
          {t('notificaciones.titulo')}
        </Typography>
        <Button size="small" variant="text" onClick={onMarcarTodas} disabled={noLeidas === 0} sx={{ whiteSpace: 'nowrap', minHeight: 32 }}>
          {t('notificaciones.marcarTodas')}
        </Button>
        {onPreferencias && (
          <Tooltip title={t('notificaciones.preferencias.abrir')}>
            <IconButton size="small" aria-label={t('notificaciones.preferencias.abrir')} onClick={onPreferencias}>
              <Settings size={18} strokeWidth={1.75} />
            </IconButton>
          </Tooltip>
        )}
      </Stack>
      <Box sx={{ flex: 1, minHeight: 0, overflowY: 'auto' }}>
        {cargando && !bandeja ? (
          <Stack spacing={1.5} sx={{ p: 2 }} aria-label={t('comun.cargando')} role="status">
            {[0, 1, 2].map((i) => (
              <Skeleton key={i} variant="rounded" height={52} />
            ))}
          </Stack>
        ) : error && !bandeja ? (
          <Box sx={{ p: 2 }}>
            <ErrorEnLinea mensaje={error} onReintentar={onReintentar} textoReintentar={t('comun.reintentar')} />
          </Box>
        ) : !bandeja || bandeja.datos.length === 0 ? (
          <EstadoVacio compacto icono={BellOff} titulo={t('notificaciones.vacio')} texto={t('notificaciones.vacioAyuda')} />
        ) : (
          <List disablePadding aria-labelledby="titulo-notificaciones">
            {bandeja.datos.map((n) => (
              <ItemNotificacion key={n.id} notificacion={n} ahora={ahora} onAbrir={onAbrir} />
            ))}
          </List>
        )}
        {bandeja?.siguienteCursor && (
          <Typography variant="caption" component="p" sx={{ color: 'text.secondary', px: 2, py: 1.5, textAlign: 'center' }}>
            {t('notificaciones.hayMas')}
          </Typography>
        )}
      </Box>
    </Box>
  );
}
