import { Alert, CircularProgress, Stack, Typography } from '@mui/material';
import { lazy, Suspense } from 'react';
import { useTranslation } from 'react-i18next';
import type { EstadoSesion } from './sesionStore';

// En el build de producción esta rama se elimina y el formulario (con la firma de aserciones) no se empaqueta.
const FormularioSesionDev = import.meta.env.DEV ? lazy(() => import('./FormularioSesionDev').then((m) => ({ default: m.FormularioSesionDev }))) : null;

/** Lo que se ve mientras no hay una sesión válida. */
export function PantallaSesion({ estado, embebida }: { estado: EstadoSesion; embebida: boolean }) {
  const { t } = useTranslation();

  if (!embebida && FormularioSesionDev) {
    return (
      <Suspense fallback={<CircularProgress sx={{ m: 4 }} />}>
        {estado === 'vencida' && <Alert severity="warning" sx={{ maxWidth: 640, mx: 'auto', mt: 4 }}>{t('sesion.vencida')}</Alert>}
        <FormularioSesionDev />
      </Suspense>
    );
  }

  if (!embebida) {
    return (
      <Stack alignItems="center" sx={{ p: 6 }}>
        <Typography color="text.secondary">{t('sesion.soloEmbebida')}</Typography>
      </Stack>
    );
  }

  if (estado === 'cerrada') {
    return (
      <Stack sx={{ p: 4, maxWidth: 560, mx: 'auto' }} role="status">
        <Typography variant="h3" component="h1">
          {t('sesion.cerrada')}
        </Typography>
      </Stack>
    );
  }

  if (estado === 'vencida' || estado === 'rechazada') {
    return (
      <Stack spacing={1} sx={{ p: 4, maxWidth: 560, mx: 'auto' }} role="alert">
        <Typography variant="h3" component="h1">
          {estado === 'vencida' ? t('sesion.vencida') : t('sesion.rechazada')}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          {t('sesion.vencidaAyuda')}
        </Typography>
      </Stack>
    );
  }

  return (
    <Stack spacing={2} alignItems="center" sx={{ p: 6 }} aria-live="polite">
      <CircularProgress size={28} />
      <Typography color="text.secondary">{t('sesion.esperando')}</Typography>
      <Typography variant="caption" color="text.secondary">
        {t('sesion.esperandoAyuda')}
      </Typography>
    </Stack>
  );
}
