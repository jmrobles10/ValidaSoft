import { Box, CircularProgress, Skeleton, Stack, Typography } from '@mui/material';
import { Isotipo, PaginaEstado } from '@validasoft/ui';
import { Clock, Link2, LogOut, ShieldAlert } from 'lucide-react';
import { lazy, Suspense, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import type { EstadoSesion } from './sesionStore';

// En el build de producción esta rama se elimina y el formulario (con la firma de aserciones) no se empaqueta.
const FormularioSesionDev = import.meta.env.DEV ? lazy(() => import('./FormularioSesionDev').then((m) => ({ default: m.FormularioSesionDev }))) : null;

/** Cabecera mínima (sin empresa ni secciones) mientras no hay sesión. */
function MarcoMinimo({ children }: { children: ReactNode }) {
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

/** Esperando la sesión: esqueleto con la forma de la pantalla y qué hacer si tarda. Nunca un spinner a pantalla completa. */
function Esperando() {
  const { t } = useTranslation();
  return (
    <Stack spacing={1.5} sx={{ p: 3 }} aria-busy>
      <Stack direction="row" spacing={1} alignItems="center" role="status" sx={{ color: 'text.secondary', fontSize: 13, mb: 1 }}>
        <Link2 size={16} strokeWidth={1.75} aria-hidden />
        <span>{t('sesion.conectando')}</span>
      </Stack>
      <Skeleton variant="rounded" width={180} height={24} />
      <Skeleton variant="rounded" width={320} height={14} />
      <Stack spacing={1.75} sx={{ mt: 1.5, p: 2, border: 1, borderColor: 'divider', borderRadius: (th) => `${String(th.radio.md)}px`, bgcolor: 'background.paper' }}>
        {[70, 55, 82, 64, 48].map((ancho) => (
          <Skeleton key={ancho} variant="rounded" width={`${String(ancho)}%`} height={12} />
        ))}
      </Stack>
    </Stack>
  );
}

/** Lo que se ve mientras no hay una sesión válida. */
export function PantallaSesion({ estado, embebida }: { estado: EstadoSesion; embebida: boolean }) {
  const { t } = useTranslation();

  if (!embebida && FormularioSesionDev) {
    return (
      <Suspense fallback={<CircularProgress sx={{ m: 4 }} aria-label={t('comun.cargando')} />}>
        <FormularioSesionDev vencida={estado === 'vencida'} />
      </Suspense>
    );
  }

  if (!embebida) {
    return (
      <MarcoMinimo>
        <PaginaEstado icono={Link2} titulo={t('sesion.soloEmbebidaTitulo')} texto={t('sesion.soloEmbebida')} />
      </MarcoMinimo>
    );
  }

  return (
    <MarcoMinimo>
      {estado === 'cerrada' && <PaginaEstado icono={LogOut} titulo={t('sesion.cerrada')} texto={t('sesion.cerradaAyuda')} rol="status" />}
      {estado === 'vencida' && <PaginaEstado icono={Clock} titulo={t('sesion.vencida')} texto={t('sesion.vencidaAyuda')} rol="alert" />}
      {estado === 'rechazada' && <PaginaEstado icono={ShieldAlert} titulo={t('sesion.rechazada')} texto={t('sesion.rechazadaAyuda')} rol="alert" />}
      {(estado === 'sin-sesion' || estado === 'activa') && <Esperando />}
    </MarcoMinimo>
  );
}
